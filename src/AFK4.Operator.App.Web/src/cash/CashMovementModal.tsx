import { useI18n } from '@afk4/i18n';
import { ArrowDownToLine, ArrowUpFromLine } from 'lucide-react';
import { PanelModal } from '../PanelModal';

// Презентационная модалка кассового движения. Направление (внесение/изъятие) задаётся снаружи
// кнопкой и неизменно внутри модалки. Реальный вызов shifts.recordCashMovement — в оркестраторе.
export function CashMovementModal({
  movementType,
  amount,
  reason,
  onChangeAmount,
  onChangeReason,
  onClose,
  onSubmit,
  busy
}: {
  movementType: 'cash_in' | 'cash_out';
  amount: string;
  reason: string;
  onChangeAmount: (value: string) => void;
  onChangeReason: (value: string) => void;
  onClose: () => void;
  onSubmit: () => void;
  busy: boolean;
}) {
  const { t } = useI18n();
  const isIn = movementType === 'cash_in';
  const Icon = isIn ? ArrowDownToLine : ArrowUpFromLine;
  return (
    <PanelModal
      title={isIn ? t('op.cash.movement.titleIn') : t('op.cash.movement.titleOut')}
      subtitle={t('op.cash.movement.subtitle')}
      onClose={onClose}
    >
      <form
        className="cash-shift-form"
        onSubmit={(event) => {
          event.preventDefault();
          onSubmit();
        }}
      >
        <label htmlFor="cash-movement-amount">{t('op.cash.movement.amountLabel')}</label>
        <input
          id="cash-movement-amount"
          inputMode="decimal"
          value={amount}
          disabled={busy}
          onChange={(event) => onChangeAmount(event.currentTarget.value)}
        />
        <label htmlFor="cash-movement-reason">{t('op.cash.movement.reasonLabel')}</label>
        <input
          id="cash-movement-reason"
          value={reason}
          disabled={busy}
          onChange={(event) => onChangeReason(event.currentTarget.value)}
        />
        <button type="submit" className="cash-primary-action" disabled={busy}>
          <Icon size={15} aria-hidden="true" />
          {t('op.cash.movement.submit')}
        </button>
      </form>
    </PanelModal>
  );
}
