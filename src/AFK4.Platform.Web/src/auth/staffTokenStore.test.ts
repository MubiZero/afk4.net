import { describe, expect, it } from 'bun:test';
import {
  clearStaffSession,
  isStaffAccessTokenExpired,
  readStaffSession,
  staffSessionFromSignInResponse,
  writeStaffSession
} from './staffTokenStore';
import type { SessionStorageLike } from './tokenStore';

function buildStorage(): SessionStorageLike & { store: Map<string, string> } {
  const store = new Map<string, string>();
  return {
    store,
    getItem: key => (store.has(key) ? (store.get(key) as string) : null),
    setItem: (key, value) => {
      store.set(key, value);
    },
    removeItem: key => {
      store.delete(key);
    }
  };
}

describe('staffTokenStore', () => {
  it('round-trips a written staff session', () => {
    const storage = buildStorage();
    const session = staffSessionFromSignInResponse({
      staffUserId: '11111111-1111-1111-1111-111111111111',
      organizationId: '22222222-2222-2222-2222-222222222222',
      displayName: 'Owner',
      accessToken: 'access',
      accessTokenExpiresAtUtc: '2030-01-01T00:00:00Z',
      refreshToken: 'refresh',
      refreshTokenExpiresAtUtc: '2030-02-01T00:00:00Z',
      branchIds: ['33333333-3333-3333-3333-333333333333'],
      permissions: ['layout.manage'],
      roleNames: ['cashier_operator']
    });

    expect(session.roleNames).toEqual(['cashier_operator']);
    writeStaffSession(session, storage);
    expect(readStaffSession(storage)).toEqual(session);
  });

  it('returns null when the stored JSON is malformed or incomplete', () => {
    const storage = buildStorage();
    storage.setItem('afk4.staff.session', '{not-json');
    expect(readStaffSession(storage)).toBeNull();

    storage.setItem('afk4.staff.session', JSON.stringify({ accessToken: '' }));
    expect(readStaffSession(storage)).toBeNull();

    storage.setItem('afk4.staff.session', JSON.stringify({ accessToken: 'a', organizationId: '' }));
    expect(readStaffSession(storage)).toBeNull();
  });

  it('clears the stored staff session', () => {
    const storage = buildStorage();
    storage.setItem('afk4.staff.session', JSON.stringify({ accessToken: 'a', organizationId: 'org' }));
    clearStaffSession(storage);
    expect(readStaffSession(storage)).toBeNull();
  });

  it('detects expired and unexpired staff access tokens', () => {
    const session = staffSessionFromSignInResponse({
      staffUserId: '11111111-1111-1111-1111-111111111111',
      organizationId: '22222222-2222-2222-2222-222222222222',
      displayName: 'Owner',
      accessToken: 'access',
      accessTokenExpiresAtUtc: '2030-01-01T00:00:00Z',
      refreshToken: 'refresh',
      refreshTokenExpiresAtUtc: '2030-02-01T00:00:00Z',
      branchIds: [],
      permissions: []
    });

    expect(isStaffAccessTokenExpired(session, new Date('2029-01-01T00:00:00Z'))).toBe(false);
    expect(isStaffAccessTokenExpired(session, new Date('2031-01-01T00:00:00Z'))).toBe(true);
  });
});
