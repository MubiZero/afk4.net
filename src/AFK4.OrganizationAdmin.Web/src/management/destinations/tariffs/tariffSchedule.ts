// Расписание тарифа в форме кабинета: маска дней и окно местного времени.
//
// Пустое окно означает «круглосуточно», пустая маска — «каждый день». Это же значение стоит у
// всех тарифов, заведённых до расписаний, поэтому клуб, который ничего не настраивал, ничего и
// не замечает.

export const EVERY_DAY_MASK = 0;

export const ALL_DAYS_MASK = 0b111_1111;

/** Понедельник первым: так неделю видят и клуб, и его посетители. */
export const DAY_BITS = [1 << 0, 1 << 1, 1 << 2, 1 << 3, 1 << 4, 1 << 5, 1 << 6] as const;

export const DAY_LABEL_KEYS = [
  'op.management.tariffs.schedule.day.mon',
  'op.management.tariffs.schedule.day.tue',
  'op.management.tariffs.schedule.day.wed',
  'op.management.tariffs.schedule.day.thu',
  'op.management.tariffs.schedule.day.fri',
  'op.management.tariffs.schedule.day.sat',
  'op.management.tariffs.schedule.day.sun'
] as const;

export interface TariffScheduleForm {
  daysMask: number;
  /** '' означает «круглосуточно»; иначе 'ЧЧ:ММ'. */
  from: string;
  to: string;
}

export const ALL_HOURS: TariffScheduleForm = { daysMask: EVERY_DAY_MASK, from: '', to: '' };

export function minutesToTimeInput(minutes: number | null): string {
  if (minutes === null || !Number.isInteger(minutes) || minutes < 0 || minutes >= 24 * 60) return '';
  return `${String(Math.floor(minutes / 60)).padStart(2, '0')}:${String(minutes % 60).padStart(2, '0')}`;
}

export function timeInputToMinutes(value: string): number | null {
  const match = /^(\d{1,2}):(\d{2})$/.exec(value.trim());
  if (!match) return null;
  const hours = Number(match[1]);
  const minutes = Number(match[2]);
  if (hours > 23 || minutes > 59) return null;
  return hours * 60 + minutes;
}

export function toggleDay(daysMask: number, index: number): number {
  return daysMask ^ DAY_BITS[index];
}

export function isDaySelected(daysMask: number, index: number): boolean {
  return daysMask === EVERY_DAY_MASK || (daysMask & DAY_BITS[index]) !== 0;
}

/** Окно, у которого начало позже конца, переходит через полночь — ночной тариф иначе не задать. */
export function isOvernight(form: TariffScheduleForm): boolean {
  const from = timeInputToMinutes(form.from);
  const to = timeInputToMinutes(form.to);
  return from !== null && to !== null && from > to;
}

export interface TariffSchedulePayload {
  appliesOnDaysMask: number;
  appliesFromMinuteOfDay: number | null;
  appliesToMinuteOfDay: number | null;
}

/**
 * Переводит форму в запрос. Возвращает <c>null</c>, если владелец заполнил только одну половину
 * окна или задал нулевое: догадываться за него о цене его же часов нельзя, и сервер такое всё
 * равно не примет.
 */
export function toSchedulePayload(form: TariffScheduleForm): TariffSchedulePayload | null {
  const hasFrom = form.from.trim().length > 0;
  const hasTo = form.to.trim().length > 0;
  if (!hasFrom && !hasTo) {
    return { appliesOnDaysMask: form.daysMask, appliesFromMinuteOfDay: null, appliesToMinuteOfDay: null };
  }
  if (hasFrom !== hasTo) return null;

  const from = timeInputToMinutes(form.from);
  const to = timeInputToMinutes(form.to);
  if (from === null || to === null || from === to) return null;

  return { appliesOnDaysMask: form.daysMask, appliesFromMinuteOfDay: from, appliesToMinuteOfDay: to };
}

/**
 * Человеческое описание расписания для таблицы: «Круглосуточно», «08:00–16:00», «Пн-Пт 08:00–16:00».
 */
export function describeSchedule(
  daysMask: number,
  fromMinuteOfDay: number | null,
  toMinuteOfDay: number | null,
  labels: { dayNames: readonly string[]; always: string }
): string {
  const hours = fromMinuteOfDay !== null && toMinuteOfDay !== null && fromMinuteOfDay !== toMinuteOfDay
    ? `${minutesToTimeInput(fromMinuteOfDay)}–${minutesToTimeInput(toMinuteOfDay)}`
    : '';
  const days = daysMask === EVERY_DAY_MASK || daysMask === ALL_DAYS_MASK
    ? ''
    : labels.dayNames.filter((_, index) => (daysMask & DAY_BITS[index]) !== 0).join(' ');

  if (!hours && !days) return labels.always;
  return [days, hours].filter(Boolean).join(' ');
}

export function scheduleFromOption(
  daysMask: number,
  fromMinuteOfDay: number | null,
  toMinuteOfDay: number | null
): TariffScheduleForm {
  return {
    daysMask,
    from: minutesToTimeInput(fromMinuteOfDay),
    to: minutesToTimeInput(toMinuteOfDay)
  };
}
