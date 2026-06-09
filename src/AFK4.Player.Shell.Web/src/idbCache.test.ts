import { describe, expect, it } from 'bun:test';
import { createCachedLoader, type KeyValueStore } from './idbCache';

function memoryStore(): KeyValueStore {
  const m = new Map<string, unknown>();
  return { get: async (k) => m.get(k), set: async (k, v) => void m.set(k, v) };
}

describe('createCachedLoader', () => {
  it('returns fresh data and caches it', async () => {
    const store = memoryStore();
    const load = createCachedLoader(store, 'tariffs', async () => [{ name: 'A' }]);
    expect(await load()).toEqual([{ name: 'A' }]);
    expect(await store.get('tariffs')).toEqual([{ name: 'A' }]);
  });

  it('falls back to cache when the loader throws', async () => {
    const store = memoryStore();
    await store.set('tariffs', [{ name: 'cached' }]);
    const load = createCachedLoader(store, 'tariffs', async () => { throw new Error('offline'); });
    expect(await load()).toEqual([{ name: 'cached' }]);
  });

  it('rethrows when offline and nothing is cached', async () => {
    const load = createCachedLoader(memoryStore(), 'tariffs', async () => { throw new Error('offline'); });
    await expect(load()).rejects.toThrow('offline');
  });
});
