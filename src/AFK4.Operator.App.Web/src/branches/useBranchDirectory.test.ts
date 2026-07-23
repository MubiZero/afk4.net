import { test, expect, mock } from 'bun:test';
import { renderHook, waitFor } from '@testing-library/react';
import { useBranchDirectory } from './useBranchDirectory';

test('loads branch names in parallel', async () => {
  const getProfile = mock((branchId: string) => Promise.resolve({ name: `Name-${branchId}` }));
  const { result } = renderHook(() => useBranchDirectory(getProfile, ['b1', 'b2']));

  await waitFor(() => expect(Object.keys(result.current)).toHaveLength(2));

  expect(result.current.b1).toEqual({ name: 'Name-b1' });
  expect(result.current.b2).toEqual({ name: 'Name-b2' });
  expect(getProfile).toHaveBeenCalledTimes(2);
});

test('swallows a failed profile fetch and falls back to the branch id', async () => {
  const getProfile = mock((branchId: string) =>
    branchId === 'bad' ? Promise.reject(new Error('boom')) : Promise.resolve({ name: 'Center' })
  );
  const { result } = renderHook(() => useBranchDirectory(getProfile, ['b1', 'bad']));

  await waitFor(() => expect(Object.keys(result.current)).toHaveLength(2));

  expect(result.current.b1).toEqual({ name: 'Center' });
  expect(result.current.bad).toEqual({ name: 'bad' });
});

test('returns an empty directory for an empty branch list', async () => {
  const getProfile = mock((_branchId: string) => Promise.resolve({ name: 'unused' }));
  const { result } = renderHook(() => useBranchDirectory(getProfile, []));

  await waitFor(() => expect(result.current).toEqual({}));
  expect(getProfile).not.toHaveBeenCalled();
});
