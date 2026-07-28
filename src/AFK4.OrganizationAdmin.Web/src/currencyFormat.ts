import { minorToMajor, currencySymbol } from '@afk4/money';
import { formatNumber as formatLocaleNumber } from '@afk4/formatting';

/** Formats integer minor units as a human-facing money string with the localized
 * currency sign (e.g. 1200, 'TJS' -> '12 с.'). Whole amounts drop the fraction;
 * non-whole amounts keep up to 2 digits. Single source for all Operator money text. */
export function formatMinorUnits(minorUnits: number, currencyCode: string): string {
  const majorUnits = minorToMajor(minorUnits);
  const formatted = formatLocaleNumber(majorUnits, 'ru-RU', {
    maximumFractionDigits: Number.isInteger(majorUnits) ? 0 : 2,
    minimumFractionDigits: 0
  });

  return `${formatted} ${currencySymbol(currencyCode)}`;
}
