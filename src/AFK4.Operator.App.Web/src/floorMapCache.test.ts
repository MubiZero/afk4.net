import { afterEach, describe, expect, it } from 'bun:test';
import {
  floorMapCacheStorageKey,
  loadFloorMapCache,
  saveFloorMapCache
} from './floorMapCache';
import type { FloorMapDto } from './operatorApiClients';

const branchId = 'acfc0212-967f-4d84-94be-9003387b09c2';
const otherBranchId = 'bd0f1234-5678-4abc-9def-0123456789ab';

function floorMap(branchName: string, seatName: string): FloorMapDto {
  return {
    branchId,
    branchName,
    seats: [
      {
        seatId: '11111111-1111-4111-8111-111111111111',
        seatName,
        zoneId: '22222222-2222-4222-8222-222222222222',
        zoneName: 'Main Hall',
        sortOrder: 1,
        state: 'Active',
        deviceId: '33333333-3333-4333-8333-333333333333',
        deviceName: seatName,
        isDeviceOnline: true,
        isDeviceLocked: false,
        activeSessionId: '44444444-4444-4444-8444-444444444444',
        remainingSeconds: 1800
      }
    ]
  };
}

afterEach(() => {
  localStorage.clear();
});

describe('floorMapCache', () => {
  it('returns null when nothing is cached for the branch', () => {
    expect(loadFloorMapCache(branchId)).toBeNull();
  });

  it('round-trips the floor map and cached timestamp', () => {
    saveFloorMapCache(branchId, floorMap('Demo Branch', 'PC-010'), 1_000);

    const entry = loadFloorMapCache(branchId);
    expect(entry).not.toBeNull();
    expect(entry?.cachedAtMs).toBe(1_000);
    expect(entry?.floorMap.branchName).toBe('Demo Branch');
    expect(entry?.floorMap.seats[0].seatName).toBe('PC-010');
  });

  it('isolates entries per branch', () => {
    saveFloorMapCache(branchId, floorMap('Branch A', 'PC-001'), 1_000);

    expect(loadFloorMapCache(otherBranchId)).toBeNull();
    expect(loadFloorMapCache(branchId)).not.toBeNull();
  });

  it('replaces the previous entry for the same branch', () => {
    saveFloorMapCache(branchId, floorMap('Old', 'PC-001'), 1_000);
    saveFloorMapCache(branchId, floorMap('New', 'PC-002'), 5_000);

    const entry = loadFloorMapCache(branchId);
    expect(entry?.floorMap.branchName).toBe('New');
    expect(entry?.cachedAtMs).toBe(5_000);
  });

  it('returns null and self-heals when the cached value is corrupt', () => {
    localStorage.setItem(floorMapCacheStorageKey(branchId), '{ not json');

    expect(loadFloorMapCache(branchId)).toBeNull();
    expect(localStorage.getItem(floorMapCacheStorageKey(branchId))).toBeNull();
  });
});
