import { describe, expect, it } from 'bun:test';
import { createTranslator } from '@afk4/i18n';
import { hydrateFloorMapStateFromCache, mapFloorMapDtoToState, offlineBannerText } from './floorMapState';
import type { FloorMapCacheEntry } from './floorMapCache';
import type { FloorMapDto } from './operatorApiClients';

const branchId = 'acfc0212-967f-4d84-94be-9003387b09c2';
const t = createTranslator('ru');

function floorMap(branchName: string): FloorMapDto {
  return {
    branchId,
    branchName,
    seats: [
      {
        seatId: '11111111-1111-4111-8111-111111111111',
        seatName: 'PC-010',
        zoneId: '22222222-2222-4222-8222-222222222222',
        zoneName: 'Main Hall',
        sortOrder: 1,
        state: 'Active',
        deviceId: '33333333-3333-4333-8333-333333333333',
        deviceName: 'PC-010',
        isDeviceOnline: true,
        isDeviceLocked: false,
        activeSessionId: '44444444-4444-4444-8444-444444444444',
        remainingSeconds: 1800
      }
    ]
  };
}

describe('offline mirror state', () => {
  it('maps a live DTO as online and fresh', () => {
    const state = mapFloorMapDtoToState(floorMap('Demo Branch'), t, 10_000);

    expect(state.isOffline).toBe(false);
    expect(state.cachedAtMs).toBe(10_000);
    expect(offlineBannerText(state, t, 10_000)).toBeNull();
  });

  it('hydrates a degraded read-only state from cache', () => {
    const entry: FloorMapCacheEntry = { floorMap: floorMap('Cached Branch'), cachedAtMs: 1_000 };

    const state = hydrateFloorMapStateFromCache(entry, branchId, t);

    expect(state.isOffline).toBe(true);
    expect(state.branchName).toBe('Cached Branch');
    expect(state.seats[0].name).toBe('PC-010');
    expect(state.loadStatus).toBe('ready');
    expect(state.cachedAtMs).toBe(1_000);
  });

  it('shows the offline banner when degraded', () => {
    const entry: FloorMapCacheEntry = { floorMap: floorMap('Cached Branch'), cachedAtMs: 1_000 };
    const state = hydrateFloorMapStateFromCache(entry, branchId, t);

    const banner = offlineBannerText(state, t, 2_000);
    expect(banner).not.toBeNull();
    expect(banner).toContain('Офлайн');
    expect(banner).toContain('только просмотр');
  });

  it('shows the banner once a fresh load ages past 30s (D8)', () => {
    const state = mapFloorMapDtoToState(floorMap('Demo Branch'), t, 0);

    expect(offlineBannerText(state, t, 20_000)).toBeNull();
    expect(offlineBannerText(state, t, 31_000)).not.toBeNull();
  });
});
