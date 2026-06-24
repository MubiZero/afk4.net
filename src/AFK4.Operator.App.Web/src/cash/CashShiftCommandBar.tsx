import { useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { Lock, ArrowDownToLine, ArrowUpFromLine, Unlock } from 'lucide-react';
import {
  createAuthenticatedOperatorClients,
  createIdempotencyKey,
  parseMoneyInputMinorUnits,
  parseNonNegativeMoneyInputMinorUnits
} from '../operatorHelpers';
import { projectOperatorError } from '../apiErrors';
import { FeedbackNotice } from '../operatorPrimitives';
import { hasPermission, permissionNames } from '../operatorPermissions';
import type { OperatorBackendContext, Feedback } from '../operatorTypes';
import type { OperatorAuthSession } from '../authClient';
import type { OpenShiftRequest, RecordCashMovementRequest, CloseShiftRequest } from '../api/clients/shifts';
import { OpenShiftModal } from './OpenShiftModal';
import { CashMovementModal } from './CashMovementModal';
import { CloseShiftModal } from './CloseShiftModal';

export interface CashShiftActionsClient {
  openShift(branchId: string, request: OpenShiftRequest): Promise<unknown>;
  recordCashMovement(shiftId: string, request: RecordCashMovementRequest): Promise<unknown>;
  closeShift(shiftId: string, request: CloseShiftRequest): Promise<unknown>;
}

type ActiveModal = 'open' | 'cash_in' | 'cash_out' | 'close' | null;

// Командная панель смены в шапке-якоре: кнопки по статусу+правам, модалки и оркестрация
// shifts.* (idempotency, feedback). После успеха зовёт onShiftChanged → раздел перечитывает смену.
export function CashShiftCommandBar({
  backend,
  session,
  shiftId,
  isOpen,
  expectedCash,
  currencyCode,
  onShiftChanged,
  actions: injectedActions
}: {
  backend: OperatorBackendContext | null;
  session: OperatorAuthSession | null;
  shiftId: string | null;
  isOpen: boolean;
  expectedCash: { currencyCode: string; minorUnits: number } | null;
  currencyCode: string;
  onShiftChanged: () => void;
  actions?: CashShiftActionsClient;
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
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState<Feedback>({ label: '', state: 'idle' });
  const [startingCash, setStartingCash] = useState('0.00');
  const [openingNote, setOpeningNote] = useState(t('op.cash.open.defaultNote'));
  const [movementAmount, setMovementAmount] = useState('10.00');
  const [movementReason, setMovementReason] = useState(t('op.cash.movement.defaultReason'));
  const [countedCash, setCountedCash] = useState('');
  const [closingNote, setClosingNote] = useState(t('op.cash.close.defaultNote'));

  const canOpen = !isOpen && hasPermission(session, permissionNames.openShift);
  const canCash = isOpen && hasPermission(session, permissionNames.manageShiftCash);
  const canClose = isOpen && hasPermission(session, permissionNames.closeShift);

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
      setMovementAmount('10.00');
      setMovementReason(t('op.cash.movement.defaultReason'));
    });

  // counted=0 валиден (реально пустая касса), поэтому parseNonNegativeMoneyInputMinorUnits
  const submitClose = () =>
    run(t('op.cash.action.close'), async (actions) => {
      const minor = parseNonNegativeMoneyInputMinorUnits(countedCash);
      if (minor === null || shiftId === null) throw new Error(t('op.cash.close.countedLabel'));
      await actions.closeShift(shiftId, {
        organizationId: backend!.session.organizationId,
        countedCash: { currencyCode, minorUnits: minor },
        closingNote: closingNote.trim(),
        idempotencyKey: createIdempotencyKey('shift-close')
      });
    });

  return (
    <div className="cash-head-commands">
      {canOpen && (
        <button type="button" className="cash-command-btn" onClick={() => setActiveModal('open')}>
          <Unlock size={14} aria-hidden="true" />{t('op.cash.action.open')}
        </button>
      )}
      {canCash && (
        <>
          <button type="button" className="cash-command-btn" onClick={() => setActiveModal('cash_in')}>
            <ArrowDownToLine size={14} aria-hidden="true" />{t('op.cash.action.cashIn')}
          </button>
          <button type="button" className="cash-command-btn" onClick={() => setActiveModal('cash_out')}>
            <ArrowUpFromLine size={14} aria-hidden="true" />{t('op.cash.action.cashOut')}
          </button>
        </>
      )}
      {canClose && (
        <button type="button" className="cash-command-btn danger" onClick={() => setActiveModal('close')}>
          <Lock size={14} aria-hidden="true" />{t('op.cash.action.close')}
        </button>
      )}
      {feedback.state !== 'idle' && <FeedbackNotice feedback={feedback} />}

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
    </div>
  );
}
