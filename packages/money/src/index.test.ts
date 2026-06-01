import { it, expect } from 'bun:test';
import { minorToMajor, majorToMinor } from './index';

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
