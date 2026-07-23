import { test, expect, beforeEach } from 'bun:test';
import { readStoredSession, writeStoredSession, clearStoredSession, isAccessTokenExpired } from './staffSessionStore';
import type { OperatorAuthSession } from '../authClient';

const sample: OperatorAuthSession = {
  staffUserId: 's1', organizationId: 'o1', displayName: 'Owner',
  accessToken: 'a.b', accessTokenExpiresAtUtc: '2999-01-01T00:00:00Z',
  refreshToken: 'r.b', refreshTokenExpiresAtUtc: '2999-01-01T00:00:00Z',
  branchIds: ['b1', 'b2'], permissions: ['pos.sell'], roleNames: ['branch_manager']
};

beforeEach(() => clearStoredSession());

test('write → read roundtrip', () => {
  writeStoredSession(sample);
  expect(readStoredSession()).toEqual(sample);
});

test('read returns null on empty / invalid', () => {
  expect(readStoredSession()).toBeNull();
  sessionStorage.setItem('afk4.staff.session', '{ broken');
  expect(readStoredSession()).toBeNull();
});

test('read rejects session without accessToken/organizationId', () => {
  sessionStorage.setItem('afk4.staff.session', JSON.stringify({ ...sample, accessToken: '' }));
  expect(readStoredSession()).toBeNull();
});

test('isAccessTokenExpired compares against expiry', () => {
  const soon = { ...sample, accessTokenExpiresAtUtc: '2000-01-01T00:00:00Z' };
  expect(isAccessTokenExpired(soon, Date.parse('2001-01-01T00:00:00Z'))).toBe(true);
  expect(isAccessTokenExpired(sample, Date.parse('2001-01-01T00:00:00Z'))).toBe(false);
});

test('isAccessTokenExpired returns true for malformed/empty expiry (fail-safe)', () => {
  const malformed = { ...sample, accessTokenExpiresAtUtc: '' };
  expect(isAccessTokenExpired(malformed, Date.now())).toBe(true);

  const broken = { ...sample, accessTokenExpiresAtUtc: 'not-a-date' };
  expect(isAccessTokenExpired(broken, Date.now())).toBe(true);
});
