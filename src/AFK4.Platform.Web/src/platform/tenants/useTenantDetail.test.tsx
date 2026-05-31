import { describe, expect, it, mock } from 'bun:test';
import { renderHook, waitFor, act } from '@testing-library/react';
import { useTenantDetail } from './useTenantDetail';
import type { TenantDetail } from '@/api/types';

function detail(over: Partial<TenantDetail>): TenantDetail {
  return {
    organizationId: 'o1', slug: 'acme', name: 'Acme', status: 'active', statusReason: null,
    statusChangedAtUtc: null, planCode: 'starter', subscriptionStatus: 'active',
    limits: { maxBranches: null, maxDevicesPerBranch: null, maxConcurrentSessions: null, maxStaffUsersPerBranch: null },
    branches: [], createdAtUtc: '2026-01-01T00:00:00Z', updatedAtUtc: '2026-01-01T00:00:00Z', ...over
  };
}
function fakeClient(over: Partial<Record<'getTenant', unknown>> = {}) {
  return { getTenant: mock().mockResolvedValue(detail({})), ...over } as never;
}

describe('useTenantDetail', () => {
  it('reaches ready and apply swaps the detail in place', async () => {
    const { result } = renderHook(() => useTenantDetail(fakeClient(), 'o1'));
    await waitFor(() => expect(result.current.status).toBe('ready'));
    act(() => { if (result.current.status === 'ready') result.current.apply(detail({ name: 'Renamed' })); });
    if (result.current.status === 'ready') expect(result.current.data.name).toBe('Renamed');
  });

  it('reaches error and retry reloads', async () => {
    const client = fakeClient({ getTenant: mock().mockRejectedValueOnce(new Error('boom')).mockResolvedValue(detail({})) });
    const { result } = renderHook(() => useTenantDetail(client, 'o1'));
    await waitFor(() => expect(result.current.status).toBe('error'));
    act(() => result.current.retry());
    await waitFor(() => expect(result.current.status).toBe('ready'));
  });
});
