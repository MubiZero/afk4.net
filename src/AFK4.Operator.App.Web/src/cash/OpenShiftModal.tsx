import { useI18n } from '@afk4/i18n';
import { Unlock } from 'lucide-react';
import { PanelModal } from '../PanelModal';

// Презентационная модалка открытия смены: старт наличных + комментарий. Реальный вызов
// shifts.openShift — в оркестраторе (CashShiftCommandBar).
export function OpenShiftModal({
  startingCash,
  note,
  onChangeStartingCash,
  onChangeNote,
  onClose,
  onSubmit,
  busy
}: {
  startingCash: string;
  note: string;
  onChangeStartingCash: (value: string) => void;
  onChangeNote: (value: string) => void;
  onClose: () => void;
  onSubmit: () => void;
  busy: boolean;
}) {
  const { t } = useI18n();
  return (
    <PanelModal title={t('op.cash.open.title')} subtitle={t('op.cash.open.subtitle')} onClose={onClose}>
      <form
        className="cash-shift-form"
        onSubmit={(event) => {
          event.preventDefault();
          onSubmit();
        }}
      >
        <label htmlFor="open-shift-cash">{t('op.cash.open.startingCashLabel')}</label>
        <input
          id="open-shift-cash"
          inputMode="decimal"
          value={startingCash}
          disabled={busy}
          onChange={(event) => onChangeStartingCash(event.currentTarget.value)}
        />
        <label htmlFor="open-shift-note">{t('op.cash.open.noteLabel')}</label>
        <input
          id="open-shift-note"
          value={note}
          disabled={busy}
          onChange={(event) => onChangeNote(event.currentTarget.value)}
        />
        <button type="submit" className="cash-primary-action" disabled={busy}>
          <Unlock size={15} aria-hidden="true" />
          {t('op.cash.open.submit')}
        </button>
      </form>
    </PanelModal>
  );
}
