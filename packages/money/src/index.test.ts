import { it, expect } from 'bun:test';
import { minorToMajor, majorToMinor, currencySymbol, formatMoney } from './index';

it('converts minor units to major units', () => {
  expect(minorToMajor(12345)).toBe(123.45);
  expect(minorToMajor(250)).toBe(2.5);
  expect(minorToMajor(0)).toBe(0);
});

it('converts major units to minor units, rounding to the nearest minor unit', () => {
  expect(majorToMinor(99.99)).toBe(9999);
  expect(majorToMinor(2.5)).toBe(250);
  expect(majorToMinor(0)).toBe(0);
  expect(majorToMinor(1.005)).toBe(101);
});

it('maps known currency codes to short UI signs', () => {
  expect(currencySymbol('TJS')).toBe('с.');
  expect(currencySymbol('usd')).toBe('$');
});

it('falls back to the ISO code for unknown currencies', () => {
  expect(currencySymbol('GBP')).toBe('GBP');
});

it('formats money the way a person reads it: short sign, no fraction on whole amounts', () => {
  const nbsp = /\u00a0|\u202f/g;
  expect(formatMoney(1200, 'TJS').replace(nbsp, ' ')).toBe('12 с.');
  expect(formatMoney(1250, 'TJS').replace(nbsp, ' ')).toBe('12,5 с.');
  expect(formatMoney(120000, 'TJS').replace(nbsp, ' ')).toBe('1 200 с.');
  expect(formatMoney(-500, 'TJS').replace(nbsp, ' ')).toBe('-5 с.');
});
