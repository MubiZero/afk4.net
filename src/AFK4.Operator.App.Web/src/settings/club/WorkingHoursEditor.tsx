import { useI18n } from '@afk4/i18n';
import type { BranchWorkingHoursDay } from '../../api/clients/settings';
import { WEEKDAY_KEY } from './workingHours';

interface WorkingHoursEditorProps {
  value: BranchWorkingHoursDay[];
  onChange: (days: BranchWorkingHoursDay[]) => void;
  disabled?: boolean;
}

// Компактный недельный график: строка на день = тумблер «работает/выходной» + название + диапазон
// (у закрытого дня поля времени сворачиваются в лёгкую строку «Выходной»).
export function WorkingHoursEditor({ value, onChange, disabled }: WorkingHoursEditorProps) {
  const { t } = useI18n();

  const patchDay = (dayOfWeek: number, patch: Partial<BranchWorkingHoursDay>) => {
    onChange(value.map((day) => (day.dayOfWeek === dayOfWeek ? { ...day, ...patch } : day)));
  };

  return (
    <div className="club-hours">
      <div className="club-hours-grid">
        {value.map((day) => {
          const open = !day.isClosed;
          return (
            <div className={`club-hours-row${open ? '' : ' club-hours-row--closed'}`} key={day.dayOfWeek}>
              <label className="club-hours-toggle">
                <input
                  type="checkbox"
                  checked={open}
                  disabled={disabled}
                  aria-label={t(WEEKDAY_KEY[day.dayOfWeek])}
                  onChange={(event) => patchDay(day.dayOfWeek, { isClosed: !event.currentTarget.checked })}
                />
              </label>
              <span className="club-hours-day">{t(WEEKDAY_KEY[day.dayOfWeek])}</span>
              {open ? (
                <div className="club-hours-range">
                  <input
                    type="time"
                    className="club-hours-time"
                    aria-label={t('op.club.hours.open')}
                    value={day.openTime ?? ''}
                    disabled={disabled}
                    onChange={(event) => patchDay(day.dayOfWeek, { openTime: event.currentTarget.value })}
                  />
                  <span className="club-hours-sep" aria-hidden="true">–</span>
                  <input
                    type="time"
                    className="club-hours-time"
                    aria-label={t('op.club.hours.close')}
                    value={day.closeTime ?? ''}
                    disabled={disabled}
                    onChange={(event) => patchDay(day.dayOfWeek, { closeTime: event.currentTarget.value })}
                  />
                </div>
              ) : (
                <span className="club-hours-off">{t('op.club.hours.closed')}</span>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
}
