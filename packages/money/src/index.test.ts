import { it, expect } from 'bun:test';
import { minorToMajor, majorToMinor, currencySymbol } from './index';

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
