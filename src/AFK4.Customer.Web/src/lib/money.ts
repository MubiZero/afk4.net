import { minorToMajor } from '@afk4/money';

// TODO(Plan 2): thread the active locale from @afk4/i18n instead of hard-coding ru-RU.
export function formatMoney(minorUnits: number, currencyCode: string): string {
  const major = minorToMajor(minorUnits);
  return `${major.toLocaleString('ru-RU', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} ${currencyCode}`;
}
