import { useI18n } from '@afk4/i18n';
import { Button, useBlockedReason } from '@afk4/ui/react';

// Пополнение баланса у стойки: поле суммы и под ним главная кнопка карточки клиента во всю ширину.
// Одна форма, чтобы Enter в поле пополнял. Остальные денежные действия — второстепенным рядом под
// ней (ClientDrawer), корректировка — в «⋯»: раньше здесь стояли пять видов кнопок подряд.
export function WalletZone({ topUpAmount, onChangeTopUpAmount, onTopUp, blockedReason = null }: {
  topUpAmount: string;
  onChangeTopUpAmount: (value: string) => void;
  onTopUp: () => void;
  // Почему кнопка погашена (закрытая смена): словами рядом, а не выяснять нажатием.
  blockedReason?: string | null;
}) {
  const { t } = useI18n();
  const blocked = useBlockedReason(blockedReason);

  return (
    <form
      className="clients-topup"
      onSubmit={(event) => {
        event.preventDefault();
        if (blockedReason !== null) return;
        onTopUp();
      }}
    >
      <div className="ui-field">
        <label htmlFor="wallet-topup-amount">{t('op.players.actions.topUpAmountLabel')}</label>
        <input
          id="wallet-topup-amount"
          inputMode="decimal"
          placeholder={t('op.players.wallet.customAmountPlaceholder')}
          value={topUpAmount}
          onChange={(event) => onChangeTopUpAmount(event.currentTarget.value)}
        />
      </div>
      <Button type="submit" variant="primary" block disabled={blockedReason !== null} aria-describedby={blocked.describedBy}>{t('op.players.actions.topUpBtn')}</Button>
      {blocked.hint}
    </form>
  );
}
