import { describe, expect, it } from 'bun:test';
import {
  clearSession,
  isAccessTokenExpired,
  readSession,
  sessionFromSignInResponse,
  writeSession,
  type SessionStorageLike
} from './tokenStore';

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

describe('tokenStore', () => {
  it('round-trips a written session', () => {
    const storage = buildStorage();
    const session = sessionFromSignInResponse({
      platformAdminId: 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee',
      userName: 'admin@platform.test',
      displayName: 'Admin',
      accessToken: 'access',
      accessTokenExpiresAtUtc: '2030-01-01T00:00:00Z',
      refreshToken: 'refresh',
      refreshTokenExpiresAtUtc: '2030-02-01T00:00:00Z',
      roles: ['platform_owner'],
      permissions: ['platform.tenants.view']
    });

    writeSession(session, storage);
    expect(readSession(storage)).toEqual(session);
  });

  it('returns null when storage is empty', () => {
    const storage = buildStorage();
    expect(readSession(storage)).toBeNull();
  });

  it('returns null when stored JSON is malformed', () => {
    const storage = buildStorage();
    storage.setItem('afk4.platform.session', '{not-json');
    expect(readSession(storage)).toBeNull();
  });

  it('treats sessions without access tokens as missing', () => {
    const storage = buildStorage();
    storage.setItem('afk4.platform.session', JSON.stringify({ accessToken: '' }));
    expect(readSession(storage)).toBeNull();
  });

  it('clears the stored session', () => {
    const storage = buildStorage();
    storage.setItem('afk4.platform.session', JSON.stringify({ accessToken: 'a' }));
    clearSession(storage);
    expect(readSession(storage)).toBeNull();
  });

  it('detects expired tokens', () => {
    const session = sessionFromSignInResponse({
      platformAdminId: '00000000-0000-0000-0000-000000000000',
      userName: 'a',
      displayName: 'a',
      accessToken: 'access',
      accessTokenExpiresAtUtc: '2020-01-01T00:00:00Z',
      refreshToken: 'refresh',
      refreshTokenExpiresAtUtc: '2020-02-01T00:00:00Z',
      roles: [],
      permissions: []
    });
    expect(isAccessTokenExpired(session, new Date('2026-01-01T00:00:00Z'))).toBe(true);
  });

  it('reports unexpired tokens', () => {
    const session = sessionFromSignInResponse({
      platformAdminId: '00000000-0000-0000-0000-000000000000',
      userName: 'a',
      displayName: 'a',
      accessToken: 'access',
      accessTokenExpiresAtUtc: '2030-01-01T00:00:00Z',
      refreshToken: 'refresh',
      refreshTokenExpiresAtUtc: '2030-02-01T00:00:00Z',
      roles: [],
      permissions: []
    });
    expect(isAccessTokenExpired(session, new Date('2026-01-01T00:00:00Z'))).toBe(false);
  });
});
