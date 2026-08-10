import { useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { Undo2 } from 'lucide-react';
import type { LedgerEntryDto } from '../operatorApiClients';
import { formatMinorUnits, formatMoneyInputMinorUnits, parseMoneyInputMinorUnits } from '../operatorHelpers';
import { PanelModal } from '../PanelModal';
import { projectLedgerEntry } from './playersModel';

// Полный или частичный возврат операции со строки истории. tone=warning — опасное действие.
// Реальный вызов и повторная серверная валидация суммы остаются в оркестраторе.
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
  onConfirm: (minorUnits: number) => void;
  busy: boolean;
}) {
  const { t } = useI18n();
  const view = projectLedgerEntry(entry, t);
  const originalMinorUnits = Math.abs(view.amountMinorUnits);
  const amount = formatMinorUnits(originalMinorUnits, view.currencyCode || currencyCode);
  const [refundAmount, setRefundAmount] = useState(() => formatMoneyInputMinorUnits(originalMinorUnits));
  useEffect(() => setRefundAmount(formatMoneyInputMinorUnits(originalMinorUnits)), [originalMinorUnits]);
  const refundMinorUnits = parseMoneyInputMinorUnits(refundAmount);
  const amountValid = refundMinorUnits !== null && refundMinorUnits > 0 && refundMinorUnits <= originalMinorUnits;

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
          if (amountValid) onConfirm(refundMinorUnits);
        }}
      >
        <p className="clients-refund-summary">
          <span>{view.typeLabel}</span>
          <strong>{amount}</strong>
          <em>{view.timeLabel}</em>
        </p>

        <label htmlFor="refund-amount">{t('op.players.refund.amountLabel')}</label>
        <input id="refund-amount" inputMode="decimal" value={refundAmount} disabled={busy} onChange={(event) => setRefundAmount(event.currentTarget.value)} />

        <label htmlFor="refund-reason">{t('op.players.refund.reasonLabel')}</label>
        <input
          id="refund-reason"
          value={reason}
          disabled={busy}
          onChange={(event) => onChangeReason(event.currentTarget.value)}
        />

        <button type="submit" className="ui-btn ui-btn--danger" disabled={busy || !amountValid}>
          <Undo2 size={15} aria-hidden="true" />
          {t('op.players.refund.confirm')}
        </button>
      </form>
    </PanelModal>
  );
}
