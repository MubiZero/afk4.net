import { it, expect, vi } from 'vitest';
import { waitFor, renderHook } from '@testing-library/react';
import { useBranchDirectory } from './useBranchDirectory';

it('builds a map of branch id to name and city', async () => {
  const client = {
    getBranchProfile: vi.fn(async (id: string) => ({ organizationId: 'org', branchId: id, name: id.toUpperCase(), city: 'Москва', createdAtUtc: '' }))
  };
  const { result } = renderHook(() => useBranchDirectory(client as never, ['a', 'b']));
  await waitFor(() => expect(Object.keys(result.current)).toHaveLength(2));
  expect(result.current.a).toEqual({ name: 'A', city: 'Москва' });
  expect(result.current.b).toEqual({ name: 'B', city: 'Москва' });
});

it('omits branches whose profile fails to load', async () => {
  const client = {
    getBranchProfile: vi.fn(async (id: string) => {
      if (id === 'b') throw new Error('boom');
      return { organizationId: 'org', branchId: id, name: 'A', city: 'Москва', createdAtUtc: '' };
    })
  };
  const { result } = renderHook(() => useBranchDirectory(client as never, ['a', 'b']));
  await waitFor(() => expect(result.current.a).toBeDefined());
  expect(result.current.b).toBeUndefined();
});
