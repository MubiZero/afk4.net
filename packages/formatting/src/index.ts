/** Shared `Intl` primitives so every surface constructs number/date formatters
 * the same way. These are low-level: callers pass the BCP-47 locale tag and the
 * `Intl` options that express their own presentation policy. Currency vs plain
 * number, fraction digits, date granularity — all stay the caller's choice; only
 * the `Intl.*Format` construction is centralised (and testable) here. */

/** Format a number with the given locale and options (default: locale defaults). */
export function formatNumber(value: number, locale: string, options?: Intl.NumberFormatOptions): string {
  return new Intl.NumberFormat(locale, options).format(value);
}

/** Format a major-unit amount as a localised currency string (`style: 'currency'`,
 * no fractional digits — the project convention shared by the platform i18n provider). */
export function formatCurrency(amount: number, currencyCode: string, locale: string): string {
  return new Intl.NumberFormat(locale, {
    style: 'currency',
    currency: currencyCode,
    maximumFractionDigits: 0,
  }).format(amount);
}

/** Format an ISO string or `Date` with the given locale and `Intl.DateTimeFormat`
 * options. Returns the formatter output; callers handle empty/invalid inputs.
 * Tajik (`tg`, `tg-TJ`) is formatted here, not by `Intl` — see {@link formatTajikDate}. */
export function formatDateParts(value: Date | string, locale: string, options: Intl.DateTimeFormatOptions): string {
  const date = typeof value === 'string' ? new Date(value) : value;
  return isTajikLocale(locale) ? formatTajikDate(date, options) : new Intl.DateTimeFormat(locale, options).format(date);
}

export function isTajikLocale(locale: string): boolean {
  return locale === 'tg' || locale.startsWith('tg-');
}

// Названия по-таджикски — в именительном падеже, как их пишут в датах: «31 октябр 2026».
const TAJIK_MONTHS = ['январ', 'феврал', 'март', 'апрел', 'май', 'июн', 'июл', 'август', 'сентябр', 'октябр', 'ноябр', 'декабр'];
// По порядку Date.getDay(): воскресенье первым.
const TAJIK_WEEKDAYS = ['якшанбе', 'душанбе', 'сешанбе', 'чоршанбе', 'панҷшанбе', 'ҷумъа', 'шанбе'];
const EN_WEEKDAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

/**
 * Дата по-таджикски. Браузеры таджикского не знают: `Intl` сводит `tg-TJ` к `en-US` — месяцы
 * выходят по-английски, а время — двенадцатичасовым с AM/PM. Раскладка берётся у `en-GB` (тот же
 * порядок «день месяц год» и 24 часа), названия месяцев и дней недели подставляются таджикские.
 * Всегда здесь, а не только когда `Intl` не знает таджикского, — чтобы на всех машинах одинаково.
 */
export function formatTajikDate(date: Date, options: Intl.DateTimeFormatOptions): string {
  const timeZone = options.timeZone;
  const monthIndex = Number(new Intl.DateTimeFormat('en-GB', { month: 'numeric', timeZone }).format(date)) - 1;
  const weekdayIndex = EN_WEEKDAYS.indexOf(new Intl.DateTimeFormat('en-US', { weekday: 'short', timeZone }).format(date));
  return new Intl.DateTimeFormat('en-GB', options)
    .formatToParts(date)
    .map((part) => {
      if (part.type === 'month' && !/^\d+$/.test(part.value)) return TAJIK_MONTHS[monthIndex] ?? part.value;
      if (part.type === 'weekday') return TAJIK_WEEKDAYS[weekdayIndex] ?? part.value;
      // «31 October 2026 at 14:00» у en-GB — по-таджикски через запятую; «31/10/2026» — через точку.
      if (part.type === 'literal') return part.value.replace(' at ', ', ').replace('/', '.');
      return part.value;
    })
    .join('');
}

/**
 * Телефон Таджикистана: код страны 992 и девять локальных цифр.
 *
 * Живёт здесь, а не в каждом приложении, потому что жило в двух: у админки клуба и у мастера
 * установки лежали побайтно одинаковые копии, различавшиеся только языком комментариев. Поле
 * телефона на входе, на сбросе пароля и в мастере — одно и то же поле, и вести себя оно обязано
 * одинаково; две копии расходятся на первой же правке маски.
 *
 * Это не Intl-примитив, а правило продукта: клубы пока только таджикские, и код страны
 * фиксирован. Когда появится второй код, отсюда и начнётся его добавление — в одном месте.
 */
export function localPhoneDigits(value: string): string {
  const digits = value.replace(/\D/g, '');
  return (digits.startsWith('992') ? digits.slice(3) : digits).slice(0, 9);
}

/** Маска локальной части «93 738 00 70» (2-3-2-2). Префикс +992 живёт вне поля ввода. */
export function formatLocal(value: string): string {
  const local = localPhoneDigits(value);
  const groups = [local.slice(0, 2), local.slice(2, 5), local.slice(5, 7), local.slice(7, 9)].filter(Boolean);
  return groups.join(' ');
}

/** Полные цифры на сервер: фиксированный код 992 и девять локальных. */
export function fullPhoneDigits(value: string): string {
  return `992${localPhoneDigits(value)}`;
}
