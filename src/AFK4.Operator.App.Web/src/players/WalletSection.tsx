import { useI18n } from '@afk4/i18n';
import { CircleDollarSign, ReceiptText, SlidersHorizontal } from 'lucide-react';
import { formatMinorUnits } from '../operatorHelpers';

// Кошелёк: крупно баланс/долг + две раздельные формы (Пополнить / Погасить долг).
// Долговая форма активна только при debt>0 (управляется canPayDebt из оркестратора).
// feedback показывается глобально в оркестраторе — единый источник, здесь не дублируем.
export function WalletSection({
  balanceMinorUnits,
  debtMinorUnits,
  currencyCode,
  topUpAmount,
  topUpReason,
  debtAmount,
  debtReason,
  canTopUp,
  canPayDebt,
  canCorrect,
  onChangeTopUpAmount,
  onChangeTopUpReason,
  onChangeDebtAmount,
  onChangeDebtReason,
  onTopUp,
  onPayDebt,
  onCorrect,
}: {
  balanceMinorUnits: number;
  debtMinorUnits: number;
  currencyCode: string;
  topUpAmount: string;
  topUpReason: string;
  debtAmount: string;
  debtReason: string;
  canTopUp: boolean;
  canPayDebt: boolean;
  canCorrect: boolean;
  onChangeTopUpAmount: (value: string) => void;
  onChangeTopUpReason: (value: string) => void;
  onChangeDebtAmount: (value: string) => void;
  onChangeDebtReason: (value: string) => void;
  onTopUp: () => void;
  onPayDebt: () => void;
  onCorrect: () => void;
}) {
  const { t } = useI18n();
  const hasDebt = debtMinorUnits > 0;

  return (
    <div className="clients-wallet-section">
      <div className="clients-wallet-figures">
        <div className="clients-wallet-figure">
          <span>{t('op.players.wallet.balanceLabel')}</span>
          <strong>{formatMinorUnits(balanceMinorUnits, currencyCode)}</strong>
        </div>
        <div className={`clients-wallet-figure${hasDebt ? ' is-debt' : ''}`}>
          <span>{t('op.players.wallet.debtLabel')}</span>
          <strong>{formatMinorUnits(debtMinorUnits, currencyCode)}</strong>
        </div>
      </div>

      <form
        className="clients-wallet-form"
        onSubmit={(event) => {
          event.preventDefault();
          onTopUp();
        }}
      >
        <strong className="clients-section-title">{t('op.players.wallet.topUpTitle')}</strong>
        <label htmlFor="wallet-topup-amount">{t('op.players.actions.topUpAmountLabel')}</label>
        <input
          id="wallet-topup-amount"
          inputMode="decimal"
          value={topUpAmount}
          disabled={!canTopUp}
          onChange={(event) => onChangeTopUpAmount(event.currentTarget.value)}
        />
        <label htmlFor="wallet-topup-reason">{t('op.players.actions.topUpReasonLabel')}</label>
        <input
          id="wallet-topup-reason"
          value={topUpReason}
          disabled={!canTopUp}
          onChange={(event) => onChangeTopUpReason(event.currentTarget.value)}
        />
        <button type="submit" className="clients-primary-action" disabled={!canTopUp}>
          <CircleDollarSign size={15} aria-hidden="true" />
          {t('op.players.actions.topUpBtn')}
        </button>
      </form>

      <form
        className={`clients-wallet-form${hasDebt ? '' : ' is-muted'}`}
        onSubmit={(event) => {
          event.preventDefault();
          onPayDebt();
        }}
      >
        <strong className="clients-section-title">{t('op.players.wallet.payDebtTitle')}</strong>
        <label htmlFor="wallet-debt-amount">{t('op.players.actions.debtAmountLabel')}</label>
        <input
          id="wallet-debt-amount"
          inputMode="decimal"
          value={debtAmount}
          disabled={!canPayDebt}
          onChange={(event) => onChangeDebtAmount(event.currentTarget.value)}
        />
        <label htmlFor="wallet-debt-reason">{t('op.players.actions.debtReasonLabel')}</label>
        <input
          id="wallet-debt-reason"
          value={debtReason}
          disabled={!canPayDebt}
          onChange={(event) => onChangeDebtReason(event.currentTarget.value)}
        />
        <button type="submit" className="clients-primary-action clients-debt-action" disabled={!canPayDebt}>
          <ReceiptText size={15} aria-hidden="true" />
          {t('op.players.actions.writeOffDebtBtn')}
        </button>
      </form>

      {canCorrect && (
        <button type="button" className="clients-wallet-correction-link" onClick={onCorrect}>
          <SlidersHorizontal size={14} aria-hidden="true" />
          {t('op.players.correction.openLink')}
        </button>
      )}
    </div>
  );
}
