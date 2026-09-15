import { useI18n } from '@afk4/i18n';
import { Lock } from 'lucide-react';
import { PanelModal } from '../PanelModal';
import { parseNonNegativeMoneyInputMinorUnits } from '../operatorHelpers';
import { Money } from '../operatorPrimitives';

/**
 * Презентационная модалка закрытия смены со сверкой. Превью расхождения = факт − ожидается,
 * считается живо при валидном вводе (включая 0 — реально пустая касса валидна).
 * Критичное действие (tone="danger"). Реальный вызов — снаружи.
 *
 * Расхождение больше допуска филиала смену не закрывает: нужна подпись второго менеджера —
 * не того, кто смену открыл, и не того, кто закрывает (анти-фрод §5.7). Спросить её здесь,
 * до отправки, честнее, чем отказать после: раньше отправить подпись было нечем вовсе, и
 * смена с крупным расхождением не закрывалась никак.
 */
export function CloseShiftModal({
  expectedCash,
  counted,
  note,
  currencyCode,
  toleranceMinorUnits,
  signOffCandidates,
  signOffStaffUserId,
  signOffReason,
  onChangeCounted,
  onChangeNote,
  onChangeSignOffStaffUserId,
  onChangeSignOffReason,
  onClose,
  onSubmit,
  busy
}: {
  expectedCash: { currencyCode: string; minorUnits: number } | null;
  counted: string;
  note: string;
  currencyCode: string;
  /** Допуск филиала. null — пока не загружен: подпись тогда не требуем, решает сервер. */
  toleranceMinorUnits: number | null;
  /** Кто может подписать: уже без открывшего смену и без текущего оператора. */
  signOffCandidates: { staffUserId: string; displayName: string }[];
  signOffStaffUserId: string;
  signOffReason: string;
  onChangeCounted: (value: string) => void;
  onChangeNote: (value: string) => void;
  onChangeSignOffStaffUserId: (value: string) => void;
  onChangeSignOffReason: (value: string) => void;
  onClose: () => void;
  onSubmit: () => void;
  busy: boolean;
}) {
  const { t } = useI18n();
  const countedMinor = parseNonNegativeMoneyInputMinorUnits(counted);
  const difference =
    countedMinor === null || expectedCash === null
      ? null
      : { currencyCode, minorUnits: countedMinor - expectedCash.minorUnits };
  const needsSignOff =
    difference !== null && toleranceMinorUnits !== null && Math.abs(difference.minorUnits) > toleranceMinorUnits;
  const blocked = needsSignOff && signOffStaffUserId === '';

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
          <div><span>{t('op.cash.close.expected')}</span><strong><Money minorUnits={expectedCash?.minorUnits ?? 0} currencyCode={currencyCode} /></strong></div>
          <div className={difference && difference.minorUnits !== 0 ? 'attention' : undefined}>
            <span>{t('op.cash.close.difference')}</span>
            <strong>{difference === null ? '—' : <Money minorUnits={difference.minorUnits} currencyCode={currencyCode} />}</strong>
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
        {needsSignOff && (
          <section className="cash-close-signoff">
            <p className="ui-alert" role="alert">
              {t('op.cash.close.signOffRequired', { tolerance: (toleranceMinorUnits ?? 0) / 100 })}
            </p>
            <label htmlFor="close-shift-signoff">{t('op.cash.close.signOffLabel')}</label>
            <select
              id="close-shift-signoff"
              value={signOffStaffUserId}
              disabled={busy}
              onChange={(event) => onChangeSignOffStaffUserId(event.currentTarget.value)}
            >
              <option value="">{t('op.cash.close.signOffPlaceholder')}</option>
              {signOffCandidates.map((candidate) => (
                <option key={candidate.staffUserId} value={candidate.staffUserId}>{candidate.displayName}</option>
              ))}
            </select>
            {signOffCandidates.length === 0 && (
              <p className="ui-field-hint">{t('op.cash.close.signOffNobody')}</p>
            )}
            <label htmlFor="close-shift-signoff-reason">{t('op.cash.close.signOffReasonLabel')}</label>
            <input
              id="close-shift-signoff-reason"
              value={signOffReason}
              disabled={busy}
              onChange={(event) => onChangeSignOffReason(event.currentTarget.value)}
            />
          </section>
        )}
        <p className="cash-close-impact">{t('op.cash.close.impact')}</p>
        <button
          type="submit"
          className="ui-btn ui-btn--primary ui-btn--lg ui-btn--block ui-btn--danger cash-primary-action danger"
          disabled={busy || blocked}
        >
          <Lock size={15} aria-hidden="true" />
          {t('op.cash.close.submit')}
        </button>
      </form>
    </PanelModal>
  );
}
