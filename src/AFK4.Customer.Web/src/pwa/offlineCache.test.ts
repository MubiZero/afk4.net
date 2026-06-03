import { it, expect, mock } from 'bun:test';
import { isPlayerCacheName, clearPlayerCaches } from './offlineCache';

it('recognizes player data cache names', () => {
  expect(isPlayerCacheName('afk4-player-api')).toBe(true);
  expect(isPlayerCacheName('workbox-precache-v2')).toBe(false);
});

it('deletes only player caches on sign-out', async () => {
  const deleted: string[] = [];
  const fakeCaches = {
    keys: mock().mockResolvedValue(['afk4-player-api', 'workbox-precache-v2', 'afk4-player-shell']),
    delete: mock().mockImplementation((name: string) => { deleted.push(name); return Promise.resolve(true); })
  } as unknown as CacheStorage;
  await clearPlayerCaches(fakeCaches);
  expect(deleted).toEqual(['afk4-player-api', 'afk4-player-shell']);
});

it('is a no-op when the Cache API is unavailable', async () => {
  await clearPlayerCaches(undefined);
  expect(true).toBe(true);
});
