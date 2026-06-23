import { useI18n } from '@afk4/i18n';
import { Undo2 } from 'lucide-react';
import type { LedgerEntryDto } from '../operatorApiClients';
import { formatMinorUnits } from '../operatorHelpers';
import { PanelModal } from '../PanelModal';
import { projectLedgerEntry } from './playersModel';

// Подтверждение ПОЛНОГО возврата операции (со строки истории). tone=warning — опасное действие.
// Реальный вызов держит оркестратор; сумма возврата = полная сумма записи.
export function RefundModal({
  entry,
  currencyCode,
  reason,
  onChangeReason,
  onClose,
  onConfirm,
  busy,
}: {
  entry: LedgerEntryDto;
  currencyCode: string;
  reason: string;
  onChangeReason: (value: string) => void;
  onClose: () => void;
  onConfirm: () => void;
  busy: boolean;
}) {
  const { t } = useI18n();
  const view = projectLedgerEntry(entry, t);
  const amount = formatMinorUnits(Math.abs(view.amountMinorUnits), view.currencyCode || currencyCode);

  return (
    <PanelModal
      title={t('op.players.refund.title')}
      subtitle={t('op.players.refund.subtitle')}
      onClose={onClose}
      tone="warning"
    >
      <form
        className="clients-refund-form"
        onSubmit={(event) => {
          event.preventDefault();
          onConfirm();
        }}
      >
        <p className="clients-refund-summary">
          <span>{view.typeLabel}</span>
          <strong>{amount}</strong>
          <em>{view.timeLabel}</em>
        </p>

        <label htmlFor="refund-reason">{t('op.players.refund.reasonLabel')}</label>
        <input
          id="refund-reason"
          value={reason}
          disabled={busy}
          onChange={(event) => onChangeReason(event.currentTarget.value)}
        />

        <button type="submit" className="clients-primary-action clients-danger-action" disabled={busy}>
          <Undo2 size={15} aria-hidden="true" />
          {t('op.players.refund.confirm')}
        </button>
      </form>
    </PanelModal>
  );
}
