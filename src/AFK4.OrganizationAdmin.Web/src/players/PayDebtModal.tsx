import { useI18n } from '@afk4/i18n';
import { ReceiptText } from 'lucide-react';
import { PanelModal } from '../PanelModal';

// Диалог погашения долга. Презентационный: реальный вызов — в оркестраторе (writeOffDebt).
// Раньше был всегда развёрнутой inline-формой в WalletSection; теперь открывается кнопкой из
// WalletZone. Та же бизнес-логика, только спрятана до нажатия.
export function PayDebtModal({
  amount,
  reason,
  onChangeAmount,
  onChangeReason,
  onClose,
  onSubmit,
  busy,
}: {
  amount: string;
  reason: string;
  onChangeAmount: (value: string) => void;
  onChangeReason: (value: string) => void;
  onClose: () => void;
  onSubmit: () => void;
  busy: boolean;
}) {
  const { t } = useI18n();

  return (
    <PanelModal title={t('op.players.wallet.payDebtTitle')} tone="danger" onClose={onClose}>
      <form
        className="clients-paydebt-form"
        onSubmit={(event) => {
          event.preventDefault();
          onSubmit();
        }}
      >
        <div className="ui-field">
          <label htmlFor="paydebt-amount">{t('op.players.actions.debtAmountLabel')}</label>
          <input
            id="paydebt-amount"
            inputMode="decimal"
            placeholder="0.00"
            value={amount}
            disabled={busy}
            onChange={(event) => onChangeAmount(event.currentTarget.value)}
          />
        </div>
        <div className="ui-field">
          <label htmlFor="paydebt-reason">{t('op.players.actions.debtReasonLabel')}</label>
          <input
            id="paydebt-reason"
            placeholder={t('op.players.actions.writeOffDebtDefault')}
            value={reason}
            disabled={busy}
            onChange={(event) => onChangeReason(event.currentTarget.value)}
          />
        </div>
        <button type="submit" className="ui-btn ui-btn--danger ui-btn--block" disabled={busy}>
          <ReceiptText size={15} aria-hidden="true" />
          {t('op.players.actions.writeOffDebtBtn')}
        </button>
      </form>
    </PanelModal>
  );
}
