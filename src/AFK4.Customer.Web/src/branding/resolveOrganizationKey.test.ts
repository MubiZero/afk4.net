import { it, expect } from 'bun:test';
import { resolveOrganizationKey } from './resolveOrganizationKey';

function makeStorage(seed?: Record<string, string>): Storage {
  const map = new Map<string, string>(Object.entries(seed ?? {}));
  return {
    getItem: (k: string) => map.get(k) ?? null,
    setItem: (k: string, v: string) => { map.set(k, v); },
    removeItem: (k: string) => { map.delete(k); },
    clear: () => map.clear(), key: () => null, length: 0
  } as unknown as Storage;
}

it('prefers the ?organization= query override and caches it', () => {
  const storage = makeStorage();
  expect(resolveOrganizationKey('club.portal.afk4.net', '?organization=override', storage)).toBe('override');
  expect(storage.getItem('afk4.player.organizationKey')).toBe('override');
});

it('derives the key from a subdomain', () => {
  expect(resolveOrganizationKey('cyberx.portal.afk4.net', '', makeStorage())).toBe('cyberx');
});

it('falls back to the cached key when nothing else is present', () => {
  expect(resolveOrganizationKey('localhost', '', makeStorage({ 'afk4.player.organizationKey': 'demo' }))).toBe('demo');
});

it('returns null when there is nothing to resolve', () => {
  expect(resolveOrganizationKey('localhost', '', makeStorage())).toBeNull();
});
