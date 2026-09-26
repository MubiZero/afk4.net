import { describe, expect, it } from 'bun:test';
import { clockOffsetMs, formatDuration, secondsUntil } from './clock';

describe('часы платформы', () => {
  it('поправка — разница между временем платформы и часами ПК в момент получения', () => {
    const received = Date.parse('2026-09-24T20:00:00Z');
    // Часы ПК отстают на полторы минуты.
    expect(clockOffsetMs('2026-09-24T20:01:30Z', received)).toBe(90_000);
    expect(clockOffsetMs(null, received)).toBe(0);
    expect(clockOffsetMs('не время', received)).toBe(0);
  });

  it('остаток считается по времени платформы, а не по часам ПК', () => {
    const pcNow = Date.parse('2026-09-24T20:00:00Z');
    const offset = 90_000;
    expect(secondsUntil('2026-09-24T20:11:30Z', pcNow, offset)).toBe(600);
    expect(secondsUntil('2026-09-24T19:00:00Z', pcNow, offset)).toBe(0);
    expect(secondsUntil(null, pcNow, offset)).toBeNull();
  });

  it('часы появляются, только когда они есть', () => {
    expect(formatDuration(0)).toBe('00:00');
    expect(formatDuration(59)).toBe('00:59');
    expect(formatDuration(3_909)).toBe('1:05:09');
  });
});
