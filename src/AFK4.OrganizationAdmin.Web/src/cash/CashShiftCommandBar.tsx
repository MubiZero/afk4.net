import { useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { Lock, ArrowDownToLine, ArrowUpFromLine, Unlock, FileText } from 'lucide-react';
import {
  createAuthenticatedOperatorClients,
  createIdempotencyKey,
  parseMoneyInputMinorUnits,
  parseNonNegativeMoneyInputMinorUnits
} from '../operatorHelpers';
import { projectOperatorError } from '../apiErrors';
import { hasPermission, permissionNames } from '../operatorPermissions';
import type { OperatorBackendContext, Feedback } from '../operatorTypes';
import type { OperatorAuthSession } from '../authClient';
import type { OpenShiftRequest, RecordCashMovementRequest, CloseShiftRequest, ShiftDto } from '../api/clients/shifts';
import type { BranchSettingsDto, ShiftRevenueDto, StaffUserDto } from '../operatorApiClients';
import { isSignOffRequired, signOffCandidates } from './shiftSignOff';
import { ShiftReportModal } from './ShiftReportModal';
import { buildShiftReportData, buildShiftReportText, printShiftReport, type ShiftReportData } from './shiftReport';
import { OpenShiftModal } from './OpenShiftModal';
import { CashMovementModal } from './CashMovementModal';
import { useFeedbackToasts } from '../useFeedbackToasts';
import { CloseShiftModal } from './CloseShiftModal';

export interface CashShiftActionsClient {
  openShift(branchId: string, request: OpenShiftRequest): Promise<unknown>;
  recordCashMovement(shiftId: string, request: RecordCashMovementRequest): Promise<unknown>;
  closeShift(shiftId: string, request: CloseShiftRequest): Promise<ShiftDto>;
}

/** Откуда экран узнаёт допуск филиала и кто вправе подписать расхождение. */
export interface CloseShiftContextClient {
  getBranchSettings(branchId: string): Promise<BranchSettingsDto>;
  getStaffUsers(branchId: string): Promise<StaffUserDto[]>;
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
  closeContext: injectedCloseContext
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
  const [startingCash, setStartingCash] = useState('0.00');
  const [openingNote, setOpeningNote] = useState(t('op.cash.open.defaultNote'));
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

  const getCloseContext = (): CloseShiftContextClient | null => {
    if (injectedCloseContext) return injectedCloseContext;
    if (!backend) return null;
    try {
      // `?? null`, а не просто поле: в наборах соседних экранов клиент подменяется заглушкой без
      // `settings`, и `undefined` проскакивал бы мимо проверки ниже.
      return createAuthenticatedOperatorClients(backend.config, backend.session).settings ?? null;
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
      context.getStaffUsers(branchId).then(setStaff).catch(() => setStaff([]))
    ]);
  };

  const openCloseModal = () => {
    setActiveModal('close');
    setSignOffDemanded(false);
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
      await actions.openShift(backend!.branchId, {
        organizationId: backend!.session.organizationId,
        startingCash: { currencyCode, minorUnits: minor },
        openingNote: openingNote.trim(),
        idempotencyKey: createIdempotencyKey('shift-open')
      });
    });

  const submitMovement = (movementType: 'cash_in' | 'cash_out') => () =>
    run(movementType === 'cash_in' ? t('op.cash.movement.titleIn') : t('op.cash.movement.titleOut'), async (actions) => {
      const minor = parseMoneyInputMinorUnits(movementAmount);
      const reason = movementReason.trim();
      if (minor === null || !reason || shiftId === null) throw new Error(t('op.cash.movement.amountLabel'));
      await actions.recordCashMovement(shiftId, {
        organizationId: backend!.session.organizationId,
        movementType,
        amount: { currencyCode, minorUnits: minor },
        reason,
        idempotencyKey: createIdempotencyKey('shift-cash-movement')
      });
      setMovementAmount('');
      setMovementReason(t('op.cash.movement.defaultReason'));
    });

  // counted=0 валиден (реально пустая касса), поэтому parseNonNegativeMoneyInputMinorUnits
  const submitClose = () =>
    run(t('op.cash.action.close'), async (actions) => {
      setSignOffDemanded(false);
      const minor = parseNonNegativeMoneyInputMinorUnits(countedCash);
      if (minor === null || shiftId === null) throw new Error(t('op.cash.close.countedLabel'));
      const closed = await actions.closeShift(shiftId, {
        organizationId: backend!.session.organizationId,
        countedCash: { currencyCode, minorUnits: minor },
        closingNote: closingNote.trim(),
        idempotencyKey: createIdempotencyKey('shift-close'),
        // Пусто — обычное закрытие в пределах допуска; сервер тогда подписи и не спросит.
        managerSignOffStaffUserId: signOffStaffUserId || null,
        signOffReason: signOffReason.trim() || null
      });
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
        <button type="button" className="ui-btn ui-btn--ghost ui-btn--sm cash-command-btn" onClick={() => setActiveModal('open')}>
          <Unlock size={14} aria-hidden="true" />{t('op.cash.action.open')}
        </button>
      )}
      {canCash && (
        <>
          <button type="button" className="ui-btn ui-btn--ghost ui-btn--sm cash-command-btn" onClick={() => setActiveModal('cash_in')}>
            <ArrowDownToLine size={14} aria-hidden="true" />{t('op.cash.action.cashIn')}
          </button>
          <button type="button" className="ui-btn ui-btn--ghost ui-btn--sm cash-command-btn" onClick={() => setActiveModal('cash_out')}>
            <ArrowUpFromLine size={14} aria-hidden="true" />{t('op.cash.action.cashOut')}
          </button>
        </>
      )}
      {canClose && (
        <button type="button" className="ui-btn ui-btn--ghost ui-btn--sm ui-btn--danger cash-command-btn danger" onClick={openCloseModal}>
          <Lock size={14} aria-hidden="true" />{t('op.cash.action.close')}
        </button>
      )}
      {canXReport && revenue && (
        <button type="button" className="ui-btn ui-btn--ghost ui-btn--sm cash-command-btn" onClick={() => setReport({ variant: 'x', data: buildShiftReportData(revenue) })}>
          <FileText size={14} aria-hidden="true" />{t('op.cash.action.xReport')}
        </button>
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
