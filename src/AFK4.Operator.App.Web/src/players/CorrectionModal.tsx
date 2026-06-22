import { useI18n } from '@afk4/i18n';
import { SlidersHorizontal } from 'lucide-react';
import { PanelModal } from '../PanelModal';

export type CorrectionAccount = 'wallet' | 'debt';
export type CorrectionDirection = 'credit' | 'debit';

// Ручная денежная корректировка (wallet/debt). Презентационный компонент: реальный вызов —
// в оркестраторе. Знак суммы задаёт направление (credit=+, debit=−); бонус-время не трогаем.
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

        <label htmlFor="correction-amount">{t('op.players.correction.amountLabel')}</label>
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

        <button type="submit" className="clients-primary-action" disabled={busy}>
          <SlidersHorizontal size={15} aria-hidden="true" />
          {t('op.players.correction.submit')}
        </button>
      </form>
    </PanelModal>
  );
}
