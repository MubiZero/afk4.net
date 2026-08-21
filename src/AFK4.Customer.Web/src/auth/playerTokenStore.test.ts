import { it, expect, beforeEach } from 'bun:test';
import {
  readPlayerSession, writePlayerSession, clearPlayerSession,
  playerSessionFromResponse, isPlayerAccessTokenExpired,
  type PlayerSession
} from './playerTokenStore';

function makeStorage(): Storage {
  const map = new Map<string, string>();
  return {
    getItem: (k: string) => map.get(k) ?? null,
    setItem: (k: string, v: string) => { map.set(k, v); },
    removeItem: (k: string) => { map.delete(k); },
    clear: () => map.clear(),
    key: () => null,
    length: 0
  } as unknown as Storage;
}

const sample = {
  platformPersonId: 'person1', playerAccountId: 'p1', organizationId: 'org1', displayName: 'Фёдор',
  phoneVerified: true, profileCompleted: true,
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

// Человек, зарегистрировавшийся дома, ещё не имеет счёта ни в одном клубе. Требовать клуб при
// чтении значило бы выкидывать его из приложения на первой же перезагрузке страницы.
it('сессия человека без клуба переживает перезагрузку', () => {
  const homeless: PlayerSession = { ...sample, playerAccountId: null, organizationId: null };
  writePlayerSession(homeless, storage);
  expect(readPlayerSession(storage)).toEqual(homeless);
});

it('maps a session response into a stored session', () => {
  const s = playerSessionFromResponse({
    playerAccountId: null, organizationId: null, displayName: '', phoneVerified: true,
    accessToken: 'a', accessTokenExpiresAtUtc: '2999-01-01T00:00:00Z',
    refreshToken: 'r', refreshTokenExpiresAtUtc: '2999-02-01T00:00:00Z',
    platformPersonId: 'person1', preferredLocale: null, profileCompleted: false
  });
  expect(s.platformPersonId).toBe('person1');
  expect(s.playerAccountId).toBeNull();
  expect(s.profileCompleted).toBe(false);
});

// Продление отдаёт то же тело, что и вход, но сохранённая раньше сессия может не нести признак
// «имя и язык спрошены». Считать его незаполненным значило бы гонять человека по экрану имени
// на каждом входе.
it('сессия без признака заполненности считается заполненной', () => {
  const { profileCompleted: _dropped, ...withoutFlag } = sample;
  storage.setItem('afk4.player.session', JSON.stringify(withoutFlag));
  expect(readPlayerSession(storage)?.profileCompleted).toBe(true);
});

it('detects an expired access token', () => {
  expect(isPlayerAccessTokenExpired({ ...sample, accessTokenExpiresAtUtc: '2000-01-01T00:00:00Z' })).toBe(true);
  expect(isPlayerAccessTokenExpired(sample)).toBe(false);
});
