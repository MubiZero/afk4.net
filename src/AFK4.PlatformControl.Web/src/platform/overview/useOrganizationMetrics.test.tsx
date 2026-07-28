import { describe, expect, it, mock } from 'bun:test';
import { renderHook, waitFor } from '@testing-library/react';
import { useOrganizationMetrics } from './useOrganizationMetrics';

const okOrganizations = [
  { organizationId: 'a', slug: 'a', name: 'Alpha', status: 'active', planCode: 'starter',
    subscriptionStatus: 'active', branchCount: 2, createdAtUtc: '2026-01-01T00:00:00Z', updatedAtUtc: '2026-01-01T00:00:00Z' }
];

function fakeClient(over: Partial<Record<'listOrganizations', unknown>> = {}) {
  return {
    listOrganizations: mock().mockResolvedValue(okOrganizations),
    ...over
  } as never;
}

describe('useOrganizationMetrics', () => {
  it('reaches ready with a view-model', async () => {
    const { result } = renderHook(() => useOrganizationMetrics(fakeClient()));
    expect(result.current.status).toBe('loading');
    await waitFor(() => expect(result.current.status).toBe('ready'));
    if (result.current.status === 'ready') {
      expect(result.current.data.kpis.totalOrganizations).toBe(1);
      expect(result.current.data.kpis.totalBranches).toBe(2);
    }
  });

  it('surfaces an error state and supports retry', async () => {
    const failing = fakeClient({ listOrganizations: mock().mockRejectedValue(new Error('boom')) });
    const { result } = renderHook(() => useOrganizationMetrics(failing));
    await waitFor(() => expect(result.current.status).toBe('error'));
    (failing as { listOrganizations: ReturnType<typeof mock> }).listOrganizations.mockResolvedValue(okOrganizations);
    result.current.retry();
    await waitFor(() => expect(result.current.status).toBe('ready'));
  });
});
