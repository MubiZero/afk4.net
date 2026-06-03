// Authenticated /api/me/* responses are cached by the service worker (NetworkFirst).
// On sign-out we must purge them so the next account on a shared device can't read them.
// Runtime cache names are prefixed "afk4-player-" (see vite.config.ts workbox config).
const PLAYER_CACHE_PREFIX = 'afk4-player-';

export function isPlayerCacheName(name: string): boolean {
  return name.startsWith(PLAYER_CACHE_PREFIX);
}

export async function clearPlayerCaches(cacheStorage: CacheStorage | undefined = globalThis.caches): Promise<void> {
  if (!cacheStorage) return;
  const names = await cacheStorage.keys();
  await Promise.all(names.filter(isPlayerCacheName).map((name) => cacheStorage.delete(name)));
}
