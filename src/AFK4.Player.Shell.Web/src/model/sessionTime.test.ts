import { describe, expect, it } from 'bun:test';
import type { PlayerShellStateDto } from '@afk4/contracts';
import { sessionUntilUtc } from './sessionTime';

const base = {
  isOnline: true,
  isGraceMode: false,
  leaseExpiresAtUtc: '2026-09-27T10:15:00Z',
  sessionEndsAtUtc: '2026-09-27T12:00:00Z'
} as PlayerShellStateDto;

// Аренда — 15 минут и продлевается; отсчёт от неё прыгал между 5 и 15 минутами на двухчасовой сессии.
describe('sessionUntilUtc', () => {
  it('counts down to the session end, not to the lease, while online', () => {
    expect(sessionUntilUtc(base)).toBe('2026-09-27T12:00:00Z');
  });

  it('without the club the session lives no longer than its signed lease', () => {
    expect(sessionUntilUtc({ ...base, isOnline: false, isGraceMode: true })).toBe('2026-09-27T10:15:00Z');
  });

  it('an open tab has no end: the screen shows how long it runs', () => {
    expect(sessionUntilUtc({ ...base, sessionEndsAtUtc: null })).toBeNull();
    expect(sessionUntilUtc({ ...base, sessionEndsAtUtc: null, isOnline: false, isGraceMode: true })).toBe('2026-09-27T10:15:00Z');
  });
});
