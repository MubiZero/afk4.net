import { useI18n } from '@afk4/i18n';
import { Lock } from 'lucide-react';
import { PanelModal } from '../PanelModal';
import { formatMoney, parseMoneyInputMinorUnits } from '../operatorHelpers';

// Презентационная модалка закрытия смены со сверкой. Превью расхождения = факт − ожидается,
// считается живо при валидном вводе. Критичное действие (tone="danger"). Реальный вызов — снаружи.
export function CloseShiftModal({
  expectedCash,
  counted,
  note,
  currencyCode,
  onChangeCounted,
  onChangeNote,
  onClose,
  onSubmit,
  busy
}: {
  expectedCash: { currencyCode: string; minorUnits: number } | null;
  counted: string;
  note: string;
  currencyCode: string;
  onChangeCounted: (value: string) => void;
  onChangeNote: (value: string) => void;
  onClose: () => void;
  onSubmit: () => void;
  busy: boolean;
}) {
  const { t } = useI18n();
  const countedMinor = parseMoneyInputMinorUnits(counted);
  const difference =
    countedMinor === null || expectedCash === null
      ? null
      : { currencyCode, minorUnits: countedMinor - expectedCash.minorUnits };

  return (
    <PanelModal title={t('op.cash.close.title')} subtitle={t('op.cash.close.subtitle')} onClose={onClose} tone="danger">
      <form
        className="cash-shift-form"
        onSubmit={(event) => {
          event.preventDefault();
          onSubmit();
        }}
      >
        <div className="cash-close-reconcile">
          <div><span>{t('op.cash.close.expected')}</span><strong>{formatMoney(expectedCash, currencyCode)}</strong></div>
          <div className={difference && difference.minorUnits !== 0 ? 'attention' : undefined}>
            <span>{t('op.cash.close.difference')}</span>
            <strong>{formatMoney(difference, currencyCode)}</strong>
          </div>
        </div>
        <label htmlFor="close-shift-counted">{t('op.cash.close.countedLabel')}</label>
        <input
          id="close-shift-counted"
          inputMode="decimal"
          value={counted}
          disabled={busy}
          onChange={(event) => onChangeCounted(event.currentTarget.value)}
        />
        <label htmlFor="close-shift-note">{t('op.cash.close.noteLabel')}</label>
        <input
          id="close-shift-note"
          value={note}
          disabled={busy}
          onChange={(event) => onChangeNote(event.currentTarget.value)}
        />
        <p className="cash-close-impact">{t('op.cash.close.impact')}</p>
        <button type="submit" className="cash-primary-action danger" disabled={busy}>
          <Lock size={15} aria-hidden="true" />
          {t('op.cash.close.submit')}
        </button>
      </form>
    </PanelModal>
  );
}
