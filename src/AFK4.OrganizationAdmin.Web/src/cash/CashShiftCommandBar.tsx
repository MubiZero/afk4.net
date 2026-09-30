import { useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { Lock, ArrowDownToLine, ArrowUpFromLine, Unlock, FileText } from 'lucide-react';
import { Button } from '@afk4/ui/react';
import {
  createAuthenticatedOperatorClients,
  parseMoneyInputMinorUnits,
  parseNonNegativeMoneyInputMinorUnits
} from '../operatorHelpers';
import { projectOperatorError } from '../apiErrors';
import { retryKeys } from '../unsettledKeys';
import { hasPermission, permissionNames } from '../operatorPermissions';
import type { OperatorBackendContext, Feedback } from '../operatorTypes';
import type { OperatorAuthSession } from '../authClient';
import type { OpenShiftRequest, RecordCashMovementRequest, CloseShiftRequest, ShiftDto } from '../api/clients/shifts';
import type { BranchSettingsDto, ShiftRevenueDto, StaffUserDto } from '../operatorApiClients';
import type { ShiftTipsDto } from '../api/clients/tips';
import { isSignOffRequired, signOffCandidates } from './shiftSignOff';
import { ShiftReportModal } from './ShiftReportModal';
import { buildShiftReportData, buildShiftReportText, printShiftReport, type ShiftReportData } from './shiftReport';
import { OpenShiftModal } from './OpenShiftModal';
import { CashMovementModal } from './CashMovementModal';
import { useFeedbackToasts } from '../useFeedbackToasts';
import { CloseShiftModal } from './CloseShiftModal';
import { usePrefillStartingCash, type ClosedShiftHistoryReader } from './useLastClosingCash';

export interface CashShiftActionsClient {
  openShift(branchId: string, request: OpenShiftRequest): Promise<unknown>;
  recordCashMovement(shiftId: string, request: RecordCashMovementRequest): Promise<unknown>;
  closeShift(shiftId: string, request: CloseShiftRequest): Promise<ShiftDto>;
}

/** Откуда экран узнаёт допуск филиала и кто вправе подписать расхождение. */
export interface CloseShiftContextClient {
  getBranchSettings(branchId: string): Promise<BranchSettingsDto>;
  getStaffUsers(branchId: string): Promise<StaffUserDto[]>;
  /** Чаевые смены — чтобы перед закрытием сказать, что они не выданы. Нет — окно молчит. */
  getShiftTips?(shiftId: string): Promise<ShiftTipsDto>;
}

type ActiveModal = 'open' | 'cash_in' | 'cash_out' | 'close' | null;

// Командная панель смены в шапке-якоре: кнопки по статусу+правам, модалки и оркестрация
// shifts.* (idempotency, feedback). После успеха зовёт onShiftChanged → раздел перечитывает смену.
export function CashShiftCommandBar({
  backend,
  session,
  shiftId,
  isOpen,
  openedByStaffUserId = null,
  expectedCash,
  currencyCode,
  revenue = null,
  onShiftChanged,
  actions: injectedActions,
  closeContext: injectedCloseContext,
  shiftHistory
}: {
  backend: OperatorBackendContext | null;
  session: OperatorAuthSession | null;
  shiftId: string | null;
  isOpen: boolean;
  /// Кто открыл текущую смену. Нужен для узкого права «закрыть свою»: без него кнопка либо
  /// пряталась бы у кассира вовсе, либо обещала бы то, на что сервер ответит отказом.
  openedByStaffUserId?: string | null;
  expectedCash: { currencyCode: string; minorUnits: number } | null;
  currencyCode: string;
  revenue?: ShiftRevenueDto | null;
  onShiftChanged: () => void;
  actions?: CashShiftActionsClient;
  closeContext?: CloseShiftContextClient;
  shiftHistory?: ClosedShiftHistoryReader;
}) {
  const { t } = useI18n();
  // Реальный клиент строим лениво (только при вызове run), потому что PlatformApiClient
  // бросает Invalid URL при инициализации, если конфиг невалиден (фейк-backend в тестах).
  const getActions = (): CashShiftActionsClient | null => {
    if (injectedActions) return injectedActions;
    if (!backend) return null;
    return createAuthenticatedOperatorClients(backend.config, backend.session).shifts;
  };

  const [activeModal, setActiveModal] = useState<ActiveModal>(null);
  const [report, setReport] = useState<{ variant: 'x' | 'z'; data: ShiftReportData } | null>(null);
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState<Feedback>({ label: '', state: 'idle' });
  useFeedbackToasts(feedback);
  const [startingCash, setStartingCash] = useState('0');
  // Пусто, а не «Утренняя смена»: подстановка врала в любое время суток, а комментарий необязателен.
  const [openingNote, setOpeningNote] = useState('');
  // Пустое поле, а не предзаполненные 10.00: в спешке легко подтвердить чужую сумму, просто
  // не заметив, что в поле уже что-то стоит.
  const [movementAmount, setMovementAmount] = useState('');
  const [movementReason, setMovementReason] = useState(t('op.cash.movement.defaultReason'));
  const [countedCash, setCountedCash] = useState('');
  const [closingNote, setClosingNote] = useState(t('op.cash.close.defaultNote'));
  const [signOffStaffUserId, setSignOffStaffUserId] = useState('');
  const [signOffReason, setSignOffReason] = useState('');
  // Допуск и состав смены нужны только в момент закрытия — грузим при открытии модалки, а не
  // при каждом показе панели.
  const [toleranceMinorUnits, setToleranceMinorUnits] = useState<number | null>(null);
  const [staff, setStaff] = useState<StaffUserDto[]>([]);
  // Сервер отказал «нужна подпись старшего»: показываем поле подписи, даже если допуск филиала
  // не подгрузился и посчитать необходимость подписи заранее было нечем.
  const [signOffDemanded, setSignOffDemanded] = useState(false);
  // Невыданные чаевые закрываемой смены: закрыть можно, но человек должен знать, что долг остаётся.
  const [unpaidTips, setUnpaidTips] = useState<{ minorUnits: number; name: string } | null>(null);

  const getCloseContext = (): CloseShiftContextClient | null => {
    if (injectedCloseContext) return injectedCloseContext;
    if (!backend) return null;
    try {
      // `?? null`, а не просто поле: в наборах соседних экранов клиент подменяется заглушкой без
      // `settings`, и `undefined` проскакивал бы мимо проверки ниже.
      const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
      if (!clients.settings) return null;
      const tips = clients.tips;
      return {
        getBranchSettings: (branchId) => clients.settings.getBranchSettings(branchId),
        getStaffUsers: (branchId) => clients.settings.getStaffUsers(branchId),
        getShiftTips: tips ? (id) => tips.forShift(id) : undefined
      };
    } catch {
      // Та же причина, что у getActions выше: PlatformApiClient бросает на невалидном конфиге.
      // Без допуска подпись просто не спрашивается заранее — решает сервер.
      return null;
    }
  };

  const loadCloseContext = async () => {
    const context = getCloseContext();
    const branchId = backend?.branchId;
    if (context === null || !branchId) return;
    // Молча: без допуска подпись просто не спрашивается заранее, и решает сервер — как и раньше.
    await Promise.all([
      context.getBranchSettings(branchId)
        .then((settings) => setToleranceMinorUnits(settings.shiftDiscrepancyToleranceMinorUnits ?? null))
        .catch(() => setToleranceMinorUnits(null)),
      context.getStaffUsers(branchId).then(setStaff).catch(() => setStaff([])),
      shiftId && context.getShiftTips
        ? context.getShiftTips(shiftId)
          .then((tips) => {
            const unpaid = (tips?.total?.minorUnits ?? 0) - (tips?.paidOut?.minorUnits ?? 0);
            setUnpaidTips(unpaid > 0 ? { minorUnits: unpaid, name: tips.recipientName } : null);
          })
          .catch(() => setUnpaidTips(null))
        : Promise.resolve()
    ]);
  };

  const openCloseModal = () => {
    setActiveModal('close');
    setSignOffDemanded(false);
    setUnpaidTips(null);
    void loadCloseContext();
  };

  const canOpen = !isOpen && hasPermission(session, permissionNames.openShift);
  const canCash = isOpen && hasPermission(session, permissionNames.manageShiftCash);
  // Широкое право закрывает любую смену; кассир — только свою, ту, которую сам открыл. Сверку
  // кассы это не отменяет, а расхождение сверх допуска по-прежнему потребует второго человека:
  // «закрыть поверх недостачи» в одиночку нельзя.
  const canCloseAny = isOpen && hasPermission(session, permissionNames.closeShift);
  const canCloseOwn = isOpen
    && !canCloseAny
    && session !== null
    && openedByStaffUserId !== null
    && openedByStaffUserId === session.staffUserId
    && hasPermission(session, permissionNames.closeOwnShift);
  const canClose = canCloseAny || canCloseOwn;
  usePrefillStartingCash(backend, canOpen, setStartingCash, shiftHistory);
  const canXReport = isOpen && hasPermission(session, permissionNames.viewReports);

  const run = async (label: string, fn: (actions: CashShiftActionsClient) => Promise<void>) => {
    const actions = getActions();
    if (actions === null || backend === null) return;
    setBusy(true);
    setFeedback({ label, state: 'pending' });
    try {
      await fn(actions);
      setActiveModal(null);
      setFeedback({ label, state: 'confirmed' });
      onShiftChanged();
    } catch (error) {
      // Сервер сказал «нужна подпись старшего» — значит поле подписи обязано появиться, даже
      // если допуск филиала не подгрузился и посчитать это заранее было нечем. Без этого
      // кассир с реальной недостачей закрыть смену не может вообще.
      if (isSignOffRequired(error)) {
        setSignOffDemanded(true);
        void loadCloseContext();
      }
      setFeedback({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setBusy(false);
    }
  };

  const submitOpen = () =>
    run(t('op.cash.action.open'), async (actions) => {
      const minor = parseNonNegativeMoneyInputMinorUnits(startingCash);
      if (minor === null) throw new Error(t('op.cash.open.startingCashLabel'));
      const branchId = backend!.branchId;
      const opening = {
        organizationId: backend!.session.organizationId,
        startingCash: { currencyCode, minorUnits: minor },
        openingNote: openingNote.trim()
      };
      await retryKeys.send('shift-open', [branchId, opening], (idempotencyKey) =>
        actions.openShift(branchId, { ...opening, idempotencyKey }));
    });

  const submitMovement = (movementType: 'cash_in' | 'cash_out') => () =>
    run(movementType === 'cash_in' ? t('op.cash.movement.titleIn') : t('op.cash.movement.titleOut'), async (actions) => {
      const minor = parseMoneyInputMinorUnits(movementAmount);
      const reason = movementReason.trim();
      if (minor === null || !reason || shiftId === null) throw new Error(t('op.cash.movement.amountLabel'));
      const movement = {
        organizationId: backend!.session.organizationId,
        movementType,
        amount: { currencyCode, minorUnits: minor },
        reason
      };
      await retryKeys.send('shift-cash-movement', [shiftId, movement], (idempotencyKey) =>
        actions.recordCashMovement(shiftId, { ...movement, idempotencyKey }));
      setMovementAmount('');
      setMovementReason(t('op.cash.movement.defaultReason'));
    });

  // counted=0 валиден (реально пустая касса), поэтому parseNonNegativeMoneyInputMinorUnits
  const submitClose = () =>
    run(t('op.cash.action.close'), async (actions) => {
      setSignOffDemanded(false);
      const minor = parseNonNegativeMoneyInputMinorUnits(countedCash);
      if (minor === null || shiftId === null) throw new Error(t('op.cash.close.countedLabel'));
      const closing = {
        organizationId: backend!.session.organizationId,
        countedCash: { currencyCode, minorUnits: minor },
        closingNote: closingNote.trim(),
        // Пусто — обычное закрытие в пределах допуска; сервер тогда подписи и не спросит.
        managerSignOffStaffUserId: signOffStaffUserId || null,
        signOffReason: signOffReason.trim() || null
      };
      // Ответ потерялся — повтор с тем же ключом вернёт исходное закрытие и его Z-сводку,
      // а не отказ «смена уже закрыта».
      const closed = await retryKeys.send('shift-close', [shiftId, closing], (idempotencyKey) =>
        actions.closeShift(shiftId, { ...closing, idempotencyKey }));
      // Z-сводка: снимок выручки (revenue) + counted/difference/closedAt из ответа close.
      if (revenue) setReport({ variant: 'z', data: buildShiftReportData(revenue, closed) });
    });

  const printReport = () => {
    if (report === null) return;
    const title = report.variant === 'x' ? t('op.cash.report.xTitle') : t('op.cash.report.zTitle');
    // Окно печати могло не открыться (блокировщик всплывающих окон). Молчать тут нельзя:
    // кассир жмёт «Печать» при сдаче кассы и не понимает, повторить или звать техподдержку.
    if (!printShiftReport(title, buildShiftReportText(report.data, report.variant, currencyCode, t))) {
      setFeedback({ label: title, state: 'failed', detail: t('op.cash.report.printBlocked') });
    }
  };

  return (
    <div className="cash-head-commands">
      {canOpen && (
        // Смены нет — касса ничего не продаёт, и открыть её — единственный следующий шаг.
        <Button variant="primary" size="sm" onClick={() => setActiveModal('open')}>
          <Unlock size={14} aria-hidden="true" />{t('op.cash.action.open')}
        </Button>
      )}
      {canCash && (
        <>
          <Button variant="ghost" size="sm" onClick={() => setActiveModal('cash_in')}>
            <ArrowDownToLine size={14} aria-hidden="true" />{t('op.cash.action.cashIn')}
          </Button>
          <Button variant="ghost" size="sm" onClick={() => setActiveModal('cash_out')}>
            <ArrowUpFromLine size={14} aria-hidden="true" />{t('op.cash.action.cashOut')}
          </Button>
        </>
      )}
      {canClose && (
        <Button variant="danger" size="sm" onClick={openCloseModal}>
          <Lock size={14} aria-hidden="true" />{t('op.cash.action.close')}
        </Button>
      )}
      {canXReport && revenue && (
        <Button variant="ghost" size="sm" onClick={() => setReport({ variant: 'x', data: buildShiftReportData(revenue) })}>
          <FileText size={14} aria-hidden="true" />{t('op.cash.action.xReport')}
        </Button>
      )}
      {activeModal === 'open' && (
        <OpenShiftModal
          startingCash={startingCash}
          note={openingNote}
          onChangeStartingCash={setStartingCash}
          onChangeNote={setOpeningNote}
          onClose={() => setActiveModal(null)}
          onSubmit={submitOpen}
          busy={busy}
        />
      )}
      {(activeModal === 'cash_in' || activeModal === 'cash_out') && (
        <CashMovementModal
          movementType={activeModal}
          amount={movementAmount}
          reason={movementReason}
          onChangeAmount={setMovementAmount}
          onChangeReason={setMovementReason}
          onClose={() => setActiveModal(null)}
          onSubmit={submitMovement(activeModal)}
          busy={busy}
        />
      )}
      {activeModal === 'close' && (
        <CloseShiftModal
          toleranceMinorUnits={toleranceMinorUnits}
          signOffDemanded={signOffDemanded}
          signOffCandidates={signOffCandidates(staff, openedByStaffUserId, session?.staffUserId ?? null)}
          signOffStaffUserId={signOffStaffUserId}
          signOffReason={signOffReason}
          onChangeSignOffStaffUserId={setSignOffStaffUserId}
          onChangeSignOffReason={setSignOffReason}
          expectedCash={expectedCash}
          unpaidTips={unpaidTips}
          counted={countedCash}
          note={closingNote}
          currencyCode={currencyCode}
          onChangeCounted={setCountedCash}
          onChangeNote={setClosingNote}
          onClose={() => setActiveModal(null)}
          onSubmit={submitClose}
          busy={busy}
        />
      )}
      {report && (
        <ShiftReportModal
          variant={report.variant}
          data={report.data}
          currencyCode={currencyCode}
          onClose={() => setReport(null)}
          onPrint={printReport}
        />
      )}
    </div>
  );
}
