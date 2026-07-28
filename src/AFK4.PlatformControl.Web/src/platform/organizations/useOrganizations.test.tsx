import { describe, expect, it, mock } from 'bun:test';
import { renderHook, waitFor, act } from '@testing-library/react';
import { useOrganizations } from './useOrganizations';
import type { OrganizationSummary } from '@/api/types';

function summary(over: Partial<OrganizationSummary>): OrganizationSummary {
  return {
    organizationId: 'o1', slug: 'acme', name: 'Acme', status: 'active', planCode: 'starter',
    subscriptionStatus: 'active', branchCount: 1, createdAtUtc: '2026-01-01T00:00:00Z',
    updatedAtUtc: '2026-01-01T00:00:00Z', ...over
  };
}
function fakeClient(over: Partial<Record<'listOrganizations', unknown>> = {}) {
  return { listOrganizations: mock().mockResolvedValue([summary({})]), ...over } as never;
}

describe('useOrganizations', () => {
  it('reaches ready with organization data', async () => {
    const { result } = renderHook(() => useOrganizations(fakeClient()));
    await waitFor(() => expect(result.current.status).toBe('ready'));
    if (result.current.status === 'ready') expect(result.current.data).toHaveLength(1);
  });

  it('reaches error and retry reloads', async () => {
    const client = fakeClient({ listOrganizations: mock().mockRejectedValueOnce(new Error('boom')).mockResolvedValue([summary({})]) });
    const { result } = renderHook(() => useOrganizations(client));
    await waitFor(() => expect(result.current.status).toBe('error'));
    act(() => result.current.retry());
    await waitFor(() => expect(result.current.status).toBe('ready'));
  });
});
