import { useI18n } from '@afk4/i18n';
import type { BranchWorkingHoursDay } from '../../api/clients/settings';
import { WEEKDAY_KEY } from './workingHours';

interface WorkingHoursEditorProps {
  value: BranchWorkingHoursDay[];
  onChange: (days: BranchWorkingHoursDay[]) => void;
  disabled?: boolean;
}

export function WorkingHoursEditor({ value, onChange, disabled }: WorkingHoursEditorProps) {
  const { t } = useI18n();

  const patchDay = (dayOfWeek: number, patch: Partial<BranchWorkingHoursDay>) => {
    onChange(value.map((day) => (day.dayOfWeek === dayOfWeek ? { ...day, ...patch } : day)));
  };

  return (
    <div className="club-hours">
      {value.map((day) => (
        <div className="club-hours-row" key={day.dayOfWeek}>
          <span className="club-hours-day">{t(WEEKDAY_KEY[day.dayOfWeek])}</span>
          <label className="mgmt-check">
            <input
              type="checkbox"
              checked={day.isClosed}
              disabled={disabled}
              onChange={(event) => patchDay(day.dayOfWeek, { isClosed: event.currentTarget.checked })}
            />
            {t('op.club.hours.closed')}
          </label>
          <label className="club-hours-time">
            <span className="club-hours-time-label">{t('op.club.hours.open')}</span>
            <input
              type="time"
              value={day.openTime ?? ''}
              disabled={disabled || day.isClosed}
              onChange={(event) => patchDay(day.dayOfWeek, { openTime: event.currentTarget.value })}
            />
          </label>
          <label className="club-hours-time">
            <span className="club-hours-time-label">{t('op.club.hours.close')}</span>
            <input
              type="time"
              value={day.closeTime ?? ''}
              disabled={disabled || day.isClosed}
              onChange={(event) => patchDay(day.dayOfWeek, { closeTime: event.currentTarget.value })}
            />
          </label>
        </div>
      ))}
    </div>
  );
}
