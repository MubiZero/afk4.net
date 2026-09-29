import { useI18n } from '@afk4/i18n';
import { Button } from '@afk4/ui/react';
import { PanelModal } from '../PanelModal';

// Диалог оплаты долга. Презентационный: реальный вызов — в оркестраторе (writeOffDebt).
// Это приём денег от игрока, а не списание: главная кнопка, не красная. Красным рисуется только
// необратимое, а оплату долга, как и любое движение по журналу, можно вернуть.
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
    <PanelModal title={t('op.players.actions.payDebtBtn')} onClose={onClose}>
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
            placeholder="0,00"
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
        <Button type="submit" variant="primary" block disabled={busy}>{t('op.players.actions.payDebtBtn')}</Button>
      </form>
    </PanelModal>
  );
}
