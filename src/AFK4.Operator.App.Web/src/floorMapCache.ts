import type { FloorMapDto } from './operatorApiClients';

// Last-known-good floor map snapshot persisted per branch (spec §6.5). Lets the operator surface keep
// rendering seats — read-only, visibly stale — when the platform is unreachable. localStorage mirror of
// the WPF FileFloorMapCache: corrupt entries self-heal to "no cache" rather than throwing.

export interface FloorMapCacheEntry {
  floorMap: FloorMapDto;
  cachedAtMs: number;
}

const keyPrefix = 'afk4.operator.floorMapCache.';

export function floorMapCacheStorageKey(branchId: string): string {
  return `${keyPrefix}${branchId}`;
}

export function saveFloorMapCache(branchId: string, floorMap: FloorMapDto, cachedAtMs: number): void {
  const entry: FloorMapCacheEntry = { floorMap, cachedAtMs };
  try {
    localStorage.setItem(floorMapCacheStorageKey(branchId), JSON.stringify(entry));
  } catch {
    // Storage unavailable or quota exceeded — the mirror simply has no cache to fall back on.
  }
}

export function loadFloorMapCache(branchId: string): FloorMapCacheEntry | null {
  const key = floorMapCacheStorageKey(branchId);
  const raw = readItem(key);
  if (raw === null) {
    return null;
  }

  try {
    const entry = JSON.parse(raw) as FloorMapCacheEntry;
    if (!entry || typeof entry.cachedAtMs !== 'number' || !entry.floorMap) {
      removeItem(key);
      return null;
    }

    return entry;
  } catch {
    removeItem(key);
    return null;
  }
}

function readItem(key: string): string | null {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

function removeItem(key: string): void {
  try {
    localStorage.removeItem(key);
  } catch {
    // Best-effort cleanup; ignore storage failures.
  }
}
