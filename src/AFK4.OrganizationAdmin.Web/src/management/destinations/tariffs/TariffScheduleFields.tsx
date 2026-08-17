import { useI18n } from '@afk4/i18n';

import {
  DAY_LABEL_KEYS,
  EVERY_DAY_MASK,
  isDaySelected,
  isOvernight,
  toggleDay,
  type TariffScheduleForm
} from './tariffSchedule';

/**
 * Часы тарифа в форме кабинета.
 *
 * Пустые часы означают «круглосуточно», а не «не заполнено»: клуб без дешёвого утра ничего здесь
 * не трогает и живёт как раньше. Переход через полночь не запрещается, а называется словами —
 * ночной тариф с 22:00 до 06:00 нужен клубу так же, как утренний, и молчаливое «конец раньше
 * начала» читалось бы как опечатка.
 */
export function TariffScheduleFields({
  value,
  disabled,
  onChange
}: {
  value: TariffScheduleForm;
  disabled?: boolean;
  onChange: (next: TariffScheduleForm) => void;
}) {
  const { t } = useI18n();

  return (
    <fieldset className="mgmt-schedule-set">
      <legend>{t('op.management.tariffs.schedule.legend')}</legend>

      <div className="mgmt-form-grid">
        <label>{t('op.management.tariffs.schedule.from')}
          <input
            type="time"
            value={value.from}
            disabled={disabled}
            onChange={(event) => onChange({ ...value, from: event.currentTarget.value })}
          />
        </label>
        <label>{t('op.management.tariffs.schedule.to')}
          <input
            type="time"
            value={value.to}
            disabled={disabled}
            onChange={(event) => onChange({ ...value, to: event.currentTarget.value })}
          />
        </label>
      </div>

      <p className="mgmt-schedule-hint">
        {isOvernight(value)
          ? t('op.management.tariffs.schedule.overnightNote')
          : t('op.management.tariffs.schedule.allDayHint')}
      </p>

      <div className="mgmt-day-toggles" role="group" aria-label={t('op.management.tariffs.schedule.days')}>
        {DAY_LABEL_KEYS.map((labelKey, index) => (
          <button
            key={labelKey}
            type="button"
            className={`ui-btn ui-btn--sm${isDaySelected(value.daysMask, index) ? ' is-selected' : ''}`}
            aria-pressed={isDaySelected(value.daysMask, index)}
            disabled={disabled}
            onClick={() => onChange({ ...value, daysMask: toggleDay(value.daysMask, index) })}
          >
            {t(labelKey)}
          </button>
        ))}
      </div>

      <p className="mgmt-schedule-hint">
        {value.daysMask === EVERY_DAY_MASK
          ? t('op.management.tariffs.schedule.everyDay')
          : t('op.management.tariffs.schedule.daysHint')}
      </p>
    </fieldset>
  );
}
