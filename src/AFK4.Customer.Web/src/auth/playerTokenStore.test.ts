import { it, expect, beforeEach } from 'bun:test';
import {
  readPlayerSession, writePlayerSession, clearPlayerSession,
  playerSessionFromSignInResponse, isPlayerAccessTokenExpired,
  type PlayerSession
} from './playerTokenStore';

function makeStorage(): Storage {
  const map = new Map<string, string>();
  return {
    getItem: (k) => map.get(k) ?? null,
    setItem: (k, v) => { map.set(k, v); },
    removeItem: (k) => { map.delete(k); },
    clear: () => map.clear(),
    key: () => null,
    length: 0
  } as unknown as Storage;
}

const sample = {
  playerAccountId: 'p1', organizationId: 'org1', displayName: 'Фёдор',
  phoneVerified: true,
  accessToken: 'a', accessTokenExpiresAtUtc: '2999-01-01T00:00:00Z',
  refreshToken: 'r', refreshTokenExpiresAtUtc: '2999-02-01T00:00:00Z'
} satisfies PlayerSession;

let storage: Storage;
beforeEach(() => { storage = makeStorage(); });

it('round-trips a session through storage', () => {
  writePlayerSession(sample, storage);
  expect(readPlayerSession(storage)).toEqual(sample);
});

it('clear removes the session', () => {
  writePlayerSession(sample, storage);
  clearPlayerSession(storage);
  expect(readPlayerSession(storage)).toBeNull();
});

it('reads null when accessToken is missing', () => {
  storage.setItem('afk4.player.session', JSON.stringify({ ...sample, accessToken: '' }));
  expect(readPlayerSession(storage)).toBeNull();
});

it('maps a sign-in response into a session', () => {
  const s = playerSessionFromSignInResponse({
    playerAccountId: 'p1', organizationId: 'org1', displayName: 'Фёдор', phoneVerified: false,
    accessToken: 'a', accessTokenExpiresAtUtc: '2999-01-01T00:00:00Z',
    refreshToken: 'r', refreshTokenExpiresAtUtc: '2999-02-01T00:00:00Z'
  });
  expect(s.playerAccountId).toBe('p1');
  expect(s.phoneVerified).toBe(false);
});

it('detects an expired access token', () => {
  expect(isPlayerAccessTokenExpired({ ...sample, accessTokenExpiresAtUtc: '2000-01-01T00:00:00Z' })).toBe(true);
  expect(isPlayerAccessTokenExpired(sample)).toBe(false);
});
