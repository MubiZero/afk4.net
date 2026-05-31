import { describe, expect, it, mock } from 'bun:test';
import { renderHook, waitFor } from '@testing-library/react';
import { useTenantMetrics } from './useTenantMetrics';

const okTenants = [
  { organizationId: 'a', slug: 'a', name: 'Alpha', status: 'active', planCode: 'starter',
    subscriptionStatus: 'active', branchCount: 2, createdAtUtc: '2026-01-01T00:00:00Z', updatedAtUtc: '2026-01-01T00:00:00Z' }
];

function fakeClient(over: Partial<Record<'listTenants', unknown>> = {}) {
  return {
    listTenants: mock().mockResolvedValue(okTenants),
    ...over
  } as never;
}

describe('useTenantMetrics', () => {
  it('reaches ready with a view-model', async () => {
    const { result } = renderHook(() => useTenantMetrics(fakeClient()));
    expect(result.current.status).toBe('loading');
    await waitFor(() => expect(result.current.status).toBe('ready'));
    if (result.current.status === 'ready') {
      expect(result.current.data.kpis.totalTenants).toBe(1);
      expect(result.current.data.kpis.totalBranches).toBe(2);
    }
  });

  it('surfaces an error state and supports retry', async () => {
    const failing = fakeClient({ listTenants: mock().mockRejectedValue(new Error('boom')) });
    const { result } = renderHook(() => useTenantMetrics(failing));
    await waitFor(() => expect(result.current.status).toBe('error'));
    (failing as { listTenants: ReturnType<typeof mock> }).listTenants.mockResolvedValue(okTenants);
    result.current.retry();
    await waitFor(() => expect(result.current.status).toBe('ready'));
  });
});
