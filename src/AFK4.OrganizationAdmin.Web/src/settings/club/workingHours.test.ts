import { describe, expect, it } from 'bun:test';
import { defaultWorkingHours, normalizeWorkingHours } from './workingHours';

describe('workingHours model', () => {
  it('default has 7 days Mon..Sun, all open', () => {
    const days = defaultWorkingHours();
    expect(days.map((d) => d.dayOfWeek)).toEqual([1, 2, 3, 4, 5, 6, 7]);
    expect(days.every((d) => !d.isClosed)).toBe(true);
  });

  it('normalize null/undefined returns default 7 days', () => {
    expect(normalizeWorkingHours(undefined)).toHaveLength(7);
    expect(normalizeWorkingHours(null)).toHaveLength(7);
  });

  it('normalize fills missing days and keeps provided ones', () => {
    const days = normalizeWorkingHours([{ dayOfWeek: 3, isClosed: true, openTime: null, closeTime: null }]);
    expect(days).toHaveLength(7);
    expect(days.find((d) => d.dayOfWeek === 3)?.isClosed).toBe(true);
    expect(days.find((d) => d.dayOfWeek === 1)?.isClosed).toBe(false);
  });
});
