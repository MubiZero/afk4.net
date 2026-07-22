import { useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import QRCode from 'qrcode';
import { QrCode } from 'lucide-react';
import { PanelModal } from '../PanelModal';
import { Money } from '../operatorPrimitives';
import { emptyFeedback, parseMoneyInputMinorUnits } from '../operatorHelpers';
import { projectOperatorError } from '../apiErrors';
import { useFeedbackToasts } from '../useFeedbackToasts';
import type { Feedback } from '../operatorTypes';
import type { CreateDcTopUpRequest, DcTopUpDto, Guid } from '../operatorApiClients';

// Диалог принимает только тот срез бэкенда, который реально использует (создать/отменить/
// подтвердить намерение), а не весь auth-контекст (config+session) — так тест подставляет
// голые моки без createAuthenticatedOperatorClients и без mock.module (см. память
// frontends-on-bun-test: mock.module течёт process-wide между тестовыми файлами).
export interface DcTopUpDialogBackend {
  dcTopUps: {
    create(branchId: Guid, request: CreateDcTopUpRequest): Promise<DcTopUpDto>;
    cancel(branchId: Guid, intentId: Guid): Promise<void>;
    confirm(intentId: Guid): Promise<unknown>;
  };
}

// Отложенный спиннер (§31): кнопка блокируется мгновенно на клик (busy=true в тот же тик),
// но текст-индикатор ожидания появляется только спустя короткую паузу — короткие запросы
// не успевают мигнуть спиннером.
function useBusyLabel(busy: boolean, idleLabel: string, busyLabel: string, delayMs = 250): string {
  const [showBusy, setShowBusy] = useState(false);

  useEffect(() => {
    if (!busy) {
      setShowBusy(false);
      return undefined;
    }
    const timer = window.setTimeout(() => setShowBusy(true), delayMs);
    return () => window.clearTimeout(timer);
  }, [busy, delayMs]);

  return busy && showBusy ? busyLabel : idleLabel;
}

export function DcTopUpDialog({
  backend,
  branchId,
  playerAccountId,
  onClose,
  onCredited
}: {
  backend: DcTopUpDialogBackend;
  branchId: Guid;
  playerAccountId: Guid;
  onClose: () => void;
  onCredited: () => void;
}) {
  const { t } = useI18n();
  const [amount, setAmount] = useState('');
  const [intent, setIntent] = useState<DcTopUpDto | null>(null);
  const [qr, setQr] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const [cancelling, setCancelling] = useState(false);
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  useFeedbackToasts(feedback);

  useEffect(() => {
    if (!intent) {
      setQr(null);
      return;
    }
    let disposed = false;
    QRCode.toDataURL(intent.payUrl)
      .then((dataUrl) => { if (!disposed) setQr(dataUrl); })
      .catch(() => { if (!disposed) setQr(null); });
    return () => { disposed = true; };
  }, [intent]);

  const amountMinorUnits = parseMoneyInputMinorUnits(amount);
  const busy = creating || confirming || cancelling;

  const createIntent = async () => {
    if (amountMinorUnits === null) {
      return;
    }
    setCreating(true);
    setFeedback({ label: t('op.dc.topup.feedbackLabel'), state: 'pending' });
    try {
      const dto = await backend.dcTopUps.create(branchId, {
        playerAccountId,
        amountMinorUnits,
        currencyCode: 'TJS'
      });
      setIntent(dto);
      setFeedback(emptyFeedback);
    } catch (error) {
      setFeedback({ label: t('op.dc.topup.feedbackLabel'), state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setCreating(false);
    }
  };

  const confirmReceived = async () => {
    if (!intent) return;
    setConfirming(true);
    setFeedback({ label: t('op.dc.topup.feedbackLabel'), state: 'pending' });
    try {
      await backend.dcTopUps.confirm(intent.intentId);
      onCredited();
      onClose();
    } catch (error) {
      // fulfil требует открытую смену — типичная причина отказа; показываем понятную деталь
      // и оставляем диалог открытым (намерение всё ещё pending, оператор может повторить).
      setFeedback({ label: t('op.dc.topup.feedbackLabel'), state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setConfirming(false);
    }
  };

  const cancelIntent = async () => {
    if (!intent) {
      onClose();
      return;
    }
    setCancelling(true);
    try {
      await backend.dcTopUps.cancel(branchId, intent.intentId);
      onClose();
    } catch (error) {
      setFeedback({ label: t('op.dc.topup.feedbackLabel'), state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setCancelling(false);
    }
  };

  const showQrLabel = useBusyLabel(creating, t('op.dc.topup.showQr'), '…');
  const receivedLabel = useBusyLabel(confirming, t('op.dc.topup.received'), '…');
  const cancelLabel = useBusyLabel(cancelling, t('op.dc.topup.cancel'), '…');

  return (
    <PanelModal title={t('op.dc.topup.open')} onClose={() => void cancelIntent()} closeDisabled={busy}>
      {!intent ? (
        <form
          className="dc-topup-form"
          onSubmit={(event) => {
            event.preventDefault();
            void createIntent();
          }}
        >
          <div className="ui-field">
            <label htmlFor="dc-topup-amount">{t('op.dc.topup.amount')}</label>
            <input
              id="dc-topup-amount"
              inputMode="decimal"
              placeholder="0.00"
              value={amount}
              disabled={creating}
              onChange={(event) => setAmount(event.currentTarget.value)}
            />
          </div>
          <button type="submit" className="ui-btn ui-btn--primary" disabled={creating || amountMinorUnits === null}>
            <QrCode size={15} aria-hidden="true" />
            {showQrLabel}
          </button>
        </form>
      ) : (
        <div className="dc-topup-qr">
          {qr && <img src={qr} alt={t('op.dc.topup.hint')} />}
          <p className="dc-topup-amount-line"><Money minorUnits={intent.amountMinorUnits} currencyCode={intent.currencyCode} /></p>
          <p>{t('op.dc.topup.comment')}: <strong>{intent.comment}</strong></p>
          <p>{t('op.dc.topup.cardLast4')}: <strong>•••• {intent.cardLast4}</strong></p>
          <p className="dc-topup-hint">{t('op.dc.topup.hint')}</p>

          <div className="dc-topup-actions">
            <button type="button" className="ui-btn ui-btn--primary" disabled={busy} onClick={() => void confirmReceived()}>
              {receivedLabel}
            </button>
            <button type="button" className="ui-btn" disabled={busy} onClick={() => void cancelIntent()}>
              {cancelLabel}
            </button>
          </div>
        </div>
      )}
    </PanelModal>
  );
}
