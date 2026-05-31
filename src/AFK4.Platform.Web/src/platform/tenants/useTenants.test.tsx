import { describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor, act } from '@testing-library/react';
import { useTenants } from './useTenants';
import type { TenantSummary } from '@/api/types';

function summary(over: Partial<TenantSummary>): TenantSummary {
  return {
    organizationId: 'o1', slug: 'acme', name: 'Acme', status: 'active', planCode: 'starter',
    subscriptionStatus: 'active', branchCount: 1, createdAtUtc: '2026-01-01T00:00:00Z',
    updatedAtUtc: '2026-01-01T00:00:00Z', ...over
  };
}
function fakeClient(over: Partial<Record<'listTenants', unknown>> = {}) {
  return { listTenants: vi.fn().mockResolvedValue([summary({})]), ...over } as never;
}

describe('useTenants', () => {
  it('reaches ready with tenant data', async () => {
    const { result } = renderHook(() => useTenants(fakeClient()));
    await waitFor(() => expect(result.current.status).toBe('ready'));
    if (result.current.status === 'ready') expect(result.current.data).toHaveLength(1);
  });

  it('reaches error and retry reloads', async () => {
    const client = fakeClient({ listTenants: vi.fn().mockRejectedValueOnce(new Error('boom')).mockResolvedValue([summary({})]) });
    const { result } = renderHook(() => useTenants(client));
    await waitFor(() => expect(result.current.status).toBe('error'));
    act(() => result.current.retry());
    await waitFor(() => expect(result.current.status).toBe('ready'));
  });
});
