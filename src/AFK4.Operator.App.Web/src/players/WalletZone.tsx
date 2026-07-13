import { useI18n } from '@afk4/i18n';
import { CircleDollarSign, ReceiptText, SlidersHorizontal } from 'lucide-react';

// Форма денежных действий клиента: поле «своя сумма» + кнопка пополнения, затем «Погасить
// долг» (только при долге) / «Ручная корректировка» (только при праве). Баланс/долг как
// ЦИФРЫ — забота вызывающего экрана (ClientDetail рисует свои плитки-стат, ClientDrawer —
// баланс-герой/долг-callout выше формы): WalletZone больше не показывает деньги, только
// действия с ними. Возврат живёт построчно в Истории, не здесь.
export function WalletZone({
  debtMinorUnits,
  topUpAmount,
  canTopUp,
  onChangeTopUpAmount,
  onTopUp,
  canPayDebt,
  onOpenPayDebt,
  canCorrect,
  onCorrect,
}: {
  debtMinorUnits: number;
  topUpAmount: string;
  canTopUp: boolean;
  onChangeTopUpAmount: (value: string) => void;
  onTopUp: () => void;
  canPayDebt: boolean;
  onOpenPayDebt: () => void;
  canCorrect: boolean;
  onCorrect: () => void;
}) {
  const { t } = useI18n();
  const hasDebt = debtMinorUnits > 0;
  const hasSecondaryActions = hasDebt || canCorrect;

  return (
    <div className="clients-wallet-zone">
      <form
        className="clients-wallet-quickpay topup-row"
        onSubmit={(event) => {
          event.preventDefault();
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
            disabled={!canTopUp}
            onChange={(event) => onChangeTopUpAmount(event.currentTarget.value)}
          />
        </div>
        <button type="submit" className="ui-btn ui-btn--primary" disabled={!canTopUp}>
          <CircleDollarSign size={15} aria-hidden="true" />
          {t('op.players.actions.topUpBtn')}
        </button>
      </form>

      {hasSecondaryActions && (
        <div className="clients-wallet-secondary">
          {hasDebt && (
            <button type="button" className="ui-btn ui-btn--danger" disabled={!canPayDebt} onClick={onOpenPayDebt}>
              <ReceiptText size={14} aria-hidden="true" />
              {t('op.players.actions.writeOffDebtBtn')}
            </button>
          )}
          {canCorrect && (
            <button type="button" className="ui-btn" onClick={onCorrect}>
              <SlidersHorizontal size={14} aria-hidden="true" />
              {t('op.players.correction.openLink')}
            </button>
          )}
        </div>
      )}
    </div>
  );
}
