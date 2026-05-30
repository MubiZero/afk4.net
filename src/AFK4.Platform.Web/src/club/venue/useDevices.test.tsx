import { describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor, act } from '@testing-library/react';
import { useDevices } from './useDevices';
import type { FloorMap } from '@/api/types';

const floorMap: FloorMap = { branchId: 'b1', branchName: 'X', zones: [], seats: [] };

function fakeClient(overrides: Partial<Record<'listDevices' | 'listPendingDevices' | 'getFloorMap', unknown>> = {}) {
  return {
    listDevices: vi.fn().mockResolvedValue([]),
    listPendingDevices: vi.fn().mockResolvedValue([]),
    getFloorMap: vi.fn().mockResolvedValue({ etag: null, floorMap }),
    ...overrides
  } as never;
}

describe('useDevices', () => {
  it('reaches ready state with a view-model', async () => {
    const { result } = renderHook(() => useDevices(fakeClient(), 'b1'));
    await waitFor(() => expect(result.current.status).toBe('ready'));
    if (result.current.status === 'ready') expect(result.current.data.active).toEqual([]);
  });

  it('reaches error state and retry reloads', async () => {
    const client = fakeClient({ listDevices: vi.fn().mockRejectedValueOnce(new Error('boom')).mockResolvedValue([]) });
    const { result } = renderHook(() => useDevices(client, 'b1'));
    await waitFor(() => expect(result.current.status).toBe('error'));
    act(() => result.current.retry());
    await waitFor(() => expect(result.current.status).toBe('ready'));
  });
});
