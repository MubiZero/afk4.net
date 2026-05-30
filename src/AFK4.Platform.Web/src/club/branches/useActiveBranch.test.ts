import { it, expect, beforeEach } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { useActiveBranch } from './useActiveBranch';

beforeEach(() => { localStorage.clear(); });

it('defaults to the first branch when nothing is stored', () => {
  const { result } = renderHook(() => useActiveBranch(['a', 'b']));
  expect(result.current.activeBranchId).toBe('a');
});

it('restores a stored branch that is still available', () => {
  localStorage.setItem('afk4.club.activeBranchId', 'b');
  const { result } = renderHook(() => useActiveBranch(['a', 'b']));
  expect(result.current.activeBranchId).toBe('b');
});

it('ignores a stored branch that is no longer available', () => {
  localStorage.setItem('afk4.club.activeBranchId', 'z');
  const { result } = renderHook(() => useActiveBranch(['a', 'b']));
  expect(result.current.activeBranchId).toBe('a');
});

it('select changes the active branch and persists it', () => {
  const { result } = renderHook(() => useActiveBranch(['a', 'b']));
  act(() => result.current.select('b'));
  expect(result.current.activeBranchId).toBe('b');
  expect(localStorage.getItem('afk4.club.activeBranchId')).toBe('b');
});

it('select ignores a branch that is not in the list', () => {
  const { result } = renderHook(() => useActiveBranch(['a', 'b']));
  act(() => result.current.select('z'));
  expect(result.current.activeBranchId).toBe('a');
});
