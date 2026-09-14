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
 * options. Returns the formatter output; callers handle empty/invalid inputs. */
export function formatDateParts(value: Date | string, locale: string, options: Intl.DateTimeFormatOptions): string {
  const date = typeof value === 'string' ? new Date(value) : value;
  return new Intl.DateTimeFormat(locale, options).format(date);
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
