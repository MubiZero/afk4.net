import { formatMoney } from '@afk4/money';

/** Formats integer minor units as a human-facing money string with the localized
 * currency sign (e.g. 1200, 'TJS' -> '12 с.'). Whole amounts drop the fraction;
 * non-whole amounts keep up to 2 digits. The rule itself lives in @afk4/money so the
 * player's PC and the Panel show one amount the same way. */
export function formatMinorUnits(minorUnits: number, currencyCode: string): string {
  return formatMoney(minorUnits, currencyCode, 'ru-RU');
}
