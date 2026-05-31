import { it, expect, mock } from 'bun:test';
import { act, renderHook, waitFor } from '@testing-library/react';
import { PlatformApiError } from '@/api/platformApi';
import type { FloorMap } from '@/api/types';
import { useFloorMap } from './useFloorMap';

function floorMap(): FloorMap {
  return {
    branchId: 'b1', branchName: 'Центр',
    zones: [{ zoneId: 'z1', name: 'Zone A', sortOrder: 1 }],
    seats: [{ seatId: 's1', seatName: 'PC-1', zoneId: 'z1', zoneName: 'Zone A', sortOrder: 1, state: 'free', deviceId: null, deviceName: null, isDeviceOnline: null, isDeviceLocked: null, lastHeartbeatAtUtc: null, agentVersion: null, shellVersion: null, activeSessionId: null, remainingSeconds: null }]
  };
}

function client(overrides: Record<string, unknown> = {}) {
  return {
    getFloorMap: mock(async () => ({ etag: 'etag-1', floorMap: floorMap() })),
    updateFloorMap: mock(async () => ({ eTag: 'etag-2', zones: [], seats: [] })),
    ...overrides
  };
}

it('loads the floor map into editable zones with the branch name', async () => {
  const { result } = renderHook(() => useFloorMap(client() as never, 'b1', 'org'));
  await waitFor(() => expect(result.current.status).toBe('ready'));
  if (result.current.status !== 'ready') throw new Error('not ready');
  expect(result.current.branchName).toBe('Центр');
  expect(result.current.zones.map(z => z.name)).toEqual(['Zone A']);
});

it('save sends the bulk request with the current ETag and returns ok', async () => {
  const c = client();
  const { result } = renderHook(() => useFloorMap(c as never, 'b1', 'org'));
  await waitFor(() => expect(result.current.status).toBe('ready'));
  let outcome: string | undefined;
  await act(async () => { if (result.current.status === 'ready') outcome = await result.current.save(); });
  expect(outcome).toBe('ok');
  expect(c.updateFloorMap).toHaveBeenCalledWith('b1', expect.objectContaining({ organizationId: 'org' }), 'etag-1');
});

it('save surfaces a 412 as a conflict and keeps the staged zones', async () => {
  const c = client({ updateFloorMap: mock(async () => { throw new PlatformApiError(412, 'stale'); }) });
  const { result } = renderHook(() => useFloorMap(c as never, 'b1', 'org'));
  await waitFor(() => expect(result.current.status).toBe('ready'));
  let outcome: string | undefined;
  await act(async () => { if (result.current.status === 'ready') outcome = await result.current.save(); });
  expect(outcome).toBe('conflict');
  await waitFor(() => expect(result.current.status === 'ready' && result.current.conflict).toBe(true));
});

it('save surfaces a non-precondition failure as an error', async () => {
  const c = client({ updateFloorMap: mock(async () => { throw new PlatformApiError(409, 'busy'); }) });
  const { result } = renderHook(() => useFloorMap(c as never, 'b1', 'org'));
  await waitFor(() => expect(result.current.status).toBe('ready'));
  let outcome: string | undefined;
  await act(async () => { if (result.current.status === 'ready') outcome = await result.current.save(); });
  expect(outcome).toBe('error');
});
