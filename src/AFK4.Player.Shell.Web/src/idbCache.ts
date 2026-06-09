export interface KeyValueStore {
  get(key: string): Promise<unknown>;
  set(key: string, value: unknown): Promise<void>;
}

/** Try the network loader; on failure fall back to the last cached value; cache fresh successes (best-effort). */
export function createCachedLoader<T>(store: KeyValueStore, key: string, loader: () => Promise<T>): () => Promise<T> {
  return async () => {
    try {
      const fresh = await loader();
      try {
        await store.set(key, fresh);
      } catch {
        // best-effort cache write; never fail the request because caching failed
      }
      return fresh;
    } catch (error) {
      let cached: unknown;
      try {
        cached = await store.get(key);
      } catch {
        cached = undefined;
      }
      if (cached !== undefined) return cached as T;
      throw error;
    }
  };
}

/** Thin IndexedDB-backed KeyValueStore (real binding; verified on device, not in unit tests). */
export function indexedDbStore(dbName = 'afk4-player-shell', storeName = 'cache'): KeyValueStore {
  function open(): Promise<IDBDatabase> {
    return new Promise((resolve, reject) => {
      const req = indexedDB.open(dbName, 1);
      req.onupgradeneeded = () => req.result.createObjectStore(storeName);
      req.onsuccess = () => resolve(req.result);
      req.onerror = () => reject(req.error);
    });
  }
  async function tx<R>(mode: IDBTransactionMode, fn: (s: IDBObjectStore) => IDBRequest): Promise<R> {
    const db = await open();
    return new Promise<R>((resolve, reject) => {
      const request = fn(db.transaction(storeName, mode).objectStore(storeName));
      request.onsuccess = () => resolve(request.result as R);
      request.onerror = () => reject(request.error);
    });
  }
  return {
    get: (key) => tx('readonly', (s) => s.get(key)),
    set: async (key, value) => { await tx('readwrite', (s) => s.put(value, key)); }
  };
}
