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
