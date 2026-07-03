import { useI18n } from '@afk4/i18n';
import { CircleDollarSign, ReceiptText, SlidersHorizontal } from 'lucide-react';
import { Money } from '../operatorPrimitives';

// Верхняя зона карточки клиента: ряд ИНФОРМАЦИИ (плитки Баланс/Долг) отдельно от ряда ДЕЙСТВИЙ
// (быстрое пополнение: поле суммы + кнопка; причина необязательна — дефолт подставляется в
// оркестраторе при сабмите; «Погасить долг» — только при долге, открывает диалог; «Ручная
// корректировка» — только при праве). Информация и действие не смешиваются в одной строке.
// Возврат живёт построчно в Истории, не здесь.
export function WalletZone({
  balanceMinorUnits,
  debtMinorUnits,
  currencyCode,
  topUpAmount,
  canTopUp,
  onChangeTopUpAmount,
  onTopUp,
  canPayDebt,
  onOpenPayDebt,
  canCorrect,
  onCorrect,
}: {
  balanceMinorUnits: number;
  debtMinorUnits: number;
  currencyCode: string;
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

  return (
    <div className="clients-wallet-zone">
      <div className="clients-wallet-info">
        <div className="ui-card ui-card--stat clients-wallet-balance">
          <span>{t('op.players.chip.balance')}</span>
          <strong><Money minorUnits={balanceMinorUnits} currencyCode={currencyCode} /></strong>
        </div>
        <div className={`ui-card ui-card--stat clients-wallet-debt${hasDebt ? ' is-danger' : ''}`}>
          <span>{t('op.players.chip.debt')}</span>
          <strong><Money minorUnits={debtMinorUnits} currencyCode={currencyCode} /></strong>
        </div>
      </div>

      <div className="clients-wallet-actions">
        <form
          className="clients-wallet-quickpay"
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
              placeholder="0.00"
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

        {hasDebt && (
          <button type="button" className="ui-btn ui-btn--danger" disabled={!canPayDebt} onClick={onOpenPayDebt}>
            <ReceiptText size={14} aria-hidden="true" />
            {t('op.players.actions.writeOffDebtBtn')}
          </button>
        )}
        {canCorrect && (
          <button type="button" className="ui-btn ui-btn--ghost" onClick={onCorrect}>
            <SlidersHorizontal size={14} aria-hidden="true" />
            {t('op.players.correction.openLink')}
          </button>
        )}
      </div>
    </div>
  );
}
