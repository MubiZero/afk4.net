import { it, expect } from 'bun:test';
import { formatMinorUnits } from './currencyFormat';
import { formatMoney } from './operatorHelpers';

it('formats minor units with the localized currency sign, not the ISO code', () => {
  expect(formatMinorUnits(1200, 'TJS')).toBe('12 с.');
  expect(formatMinorUnits(5400, 'TJS')).toBe('54 с.');
  expect(formatMinorUnits(2250, 'TJS')).toBe('22,5 с.');
  expect(formatMinorUnits(0, 'TJS')).toBe('0 с.');
});

it('uses the sign for other currencies and falls back to the code when unknown', () => {
  expect(formatMinorUnits(1000, 'USD')).toBe('10 $');
  expect(formatMinorUnits(1000, 'GBP')).toBe('10 GBP');
});

it('formatMoney treats null/missing as zero in the fallback currency', () => {
  expect(formatMoney(null, 'TJS')).toBe('0 с.');
  expect(formatMoney({ currencyCode: 'TJS', minorUnits: 1200 }, 'USD')).toBe('12 с.');
});
