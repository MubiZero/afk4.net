import { useMemo } from 'react';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import { toDateTimeInputValue } from '../operatorHelpers';
import { PanelSelect } from '../PanelSelect';

const LOCALE_TAG: Record<string, string> = { ru: 'ru-RU', en: 'en-US', tg: 'tg' };
const MINUTE_STEPS = [0, 15, 30, 45];

const pad = (n: number) => String(n).padStart(2, '0');
const capitalize = (s: string) => (s ? s[0].toUpperCase() + s.slice(1) : s);

function parse(value: string): Date {
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? new Date() : d;
}
function closestStep(minute: number): number {
  return MINUTE_STEPS.reduce((best, step) => (Math.abs(step - minute) < Math.abs(best - minute) ? step : best), MINUTE_STEPS[0]);
}

/**
 * Компактный выбор даты+времени вместо нативного datetime-local: дата стрелками ‹ › (как навигатор
 * дат вверху экрана), время — два дропдауна часы:минуты (PanelSelect, шаг минут 15 в тон таймлайну).
 */
export function DateTimePicker({
  value,
  disabled = false,
  ariaLabel,
  onChange
}: {
  value: string;
  disabled?: boolean;
  ariaLabel: string;
  onChange: (value: string) => void;
}) {
  const { t, locale } = useI18n();
  const tag = LOCALE_TAG[locale] ?? 'ru-RU';
  const selected = parse(value);
  const dateFmt = useMemo(() => new Intl.DateTimeFormat(tag, { day: 'numeric', month: 'long' }), [tag]);

  const hourOptions = useMemo(() => Array.from({ length: 24 }, (_, hour) => ({ value: String(hour), label: pad(hour) })), []);
  const minuteOptions = useMemo(() => MINUTE_STEPS.map((minute) => ({ value: String(minute), label: pad(minute) })), []);

  function emit(next: Date) {
    onChange(toDateTimeInputValue(next));
  }
  function shiftDay(delta: number) {
    emit(new Date(selected.getFullYear(), selected.getMonth(), selected.getDate() + delta, selected.getHours(), selected.getMinutes()));
  }
  function setHour(hour: number) {
    emit(new Date(selected.getFullYear(), selected.getMonth(), selected.getDate(), hour, selected.getMinutes()));
  }
  function setMinute(minute: number) {
    emit(new Date(selected.getFullYear(), selected.getMonth(), selected.getDate(), selected.getHours(), minute));
  }

  return (
    <div className="booking-dtp" aria-label={ariaLabel}>
      <div className="booking-dtp-date">
        <button type="button" aria-label={t('op.booking.dateNav.prev')} disabled={disabled} onClick={() => shiftDay(-1)}><ChevronLeft size={15} /></button>
        <strong>{capitalize(dateFmt.format(selected))}</strong>
        <button type="button" aria-label={t('op.booking.dateNav.next')} disabled={disabled} onClick={() => shiftDay(1)}><ChevronRight size={15} /></button>
      </div>
      <div className="booking-dtp-time">
        <PanelSelect
          className="booking-dtp-select"
          ariaLabel={t('op.booking.dtp.hours')}
          value={String(selected.getHours())}
          options={hourOptions}
          disabled={disabled}
          onChange={(hour) => setHour(Number(hour))}
        />
        <span className="booking-dtp-colon" aria-hidden="true">:</span>
        <PanelSelect
          className="booking-dtp-select"
          ariaLabel={t('op.booking.dtp.minutes')}
          value={String(closestStep(selected.getMinutes()))}
          options={minuteOptions}
          disabled={disabled}
          onChange={(minute) => setMinute(Number(minute))}
        />
      </div>
    </div>
  );
}
