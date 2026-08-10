import { useI18n } from '@afk4/i18n';
import { SlidersHorizontal } from 'lucide-react';
import { PanelModal } from '../PanelModal';
import { parseMoneyInputMinorUnits } from '../operatorHelpers';

export type CorrectionAccount = 'wallet' | 'debt' | 'package_time' | 'bonus_time';
export type CorrectionDirection = 'credit' | 'debit';

export function correctionQuantities(account: CorrectionAccount, direction: CorrectionDirection, value: string): { minorUnits: number; quantitySeconds: number } | null {
  const sign = direction === 'debit' ? -1 : 1;
  if (account === 'package_time' || account === 'bonus_time') {
    const minutes = Number(value);
    return Number.isInteger(minutes) && minutes > 0
      ? { minorUnits: 0, quantitySeconds: sign * minutes * 60 }
      : null;
  }
  const minorUnits = parseMoneyInputMinorUnits(value);
  return minorUnits !== null && minorUnits > 0
    ? { minorUnits: sign * minorUnits, quantitySeconds: 0 }
    : null;
}

// Ручная корректировка денег или времени. Презентационный компонент: реальный вызов —
// в оркестраторе. Знак суммы/минут задаёт направление (credit=+, debit=−).
export function CorrectionModal({
  account,
  direction,
  amount,
  reason,
  onChangeAccount,
  onChangeDirection,
  onChangeAmount,
  onChangeReason,
  onClose,
  onSubmit,
  busy,
}: {
  account: CorrectionAccount;
  direction: CorrectionDirection;
  amount: string;
  reason: string;
  onChangeAccount: (value: CorrectionAccount) => void;
  onChangeDirection: (value: CorrectionDirection) => void;
  onChangeAmount: (value: string) => void;
  onChangeReason: (value: string) => void;
  onClose: () => void;
  onSubmit: () => void;
  busy: boolean;
}) {
  const { t } = useI18n();

  return (
    <PanelModal
      title={t('op.players.correction.title')}
      subtitle={t('op.players.correction.subtitle')}
      onClose={onClose}
    >
      <form
        className="clients-correction-form"
        onSubmit={(event) => {
          event.preventDefault();
          onSubmit();
        }}
      >
        <fieldset className="clients-segment">
          <legend>{t('op.players.correction.accountLabel')}</legend>
          <button type="button" className={account === 'wallet' ? 'active' : ''} disabled={busy} onClick={() => onChangeAccount('wallet')}>
            {t('op.players.correction.accountWallet')}
          </button>
          <button type="button" className={account === 'debt' ? 'active' : ''} disabled={busy} onClick={() => onChangeAccount('debt')}>
            {t('op.players.correction.accountDebt')}
          </button>
          <button type="button" className={account === 'package_time' ? 'active' : ''} disabled={busy} onClick={() => onChangeAccount('package_time')}>
            {t('ledger.account.package_time')}
          </button>
          <button type="button" className={account === 'bonus_time' ? 'active' : ''} disabled={busy} onClick={() => onChangeAccount('bonus_time')}>
            {t('ledger.account.bonus_time')}
          </button>
        </fieldset>

        <fieldset className="clients-segment">
          <legend>{t('op.players.correction.directionLabel')}</legend>
          <button type="button" className={direction === 'credit' ? 'active' : ''} disabled={busy} onClick={() => onChangeDirection('credit')}>
            {t('op.players.correction.directionCredit')}
          </button>
          <button type="button" className={direction === 'debit' ? 'active' : ''} disabled={busy} onClick={() => onChangeDirection('debit')}>
            {t('op.players.correction.directionDebit')}
          </button>
        </fieldset>

        <label htmlFor="correction-amount">{account === 'package_time' || account === 'bonus_time' ? t('op.players.correction.minutesLabel') : t('op.players.correction.amountLabel')}</label>
        <input
          id="correction-amount"
          inputMode="decimal"
          value={amount}
          disabled={busy}
          onChange={(event) => onChangeAmount(event.currentTarget.value)}
        />

        <label htmlFor="correction-reason">{t('op.players.correction.reasonLabel')}</label>
        <input
          id="correction-reason"
          value={reason}
          disabled={busy}
          onChange={(event) => onChangeReason(event.currentTarget.value)}
        />

        <button type="submit" className="ui-btn ui-btn--primary" disabled={busy}>
          <SlidersHorizontal size={15} aria-hidden="true" />
          {t('op.players.correction.submit')}
        </button>
      </form>
    </PanelModal>
  );
}
