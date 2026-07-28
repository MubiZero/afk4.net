import { test, expect, beforeEach } from 'bun:test';
import { renderHook, act } from '@testing-library/react';
import { useActiveBranch } from './useActiveBranch';

beforeEach(() => localStorage.clear());

test('defaults to first branch, select persists', () => {
  const { result } = renderHook(() => useActiveBranch(['b1', 'b2']));
  expect(result.current.activeBranchId).toBe('b1');
  act(() => result.current.select('b2'));
  expect(result.current.activeBranchId).toBe('b2');
  expect(localStorage.getItem('afk4.organization-admin.activeBranchId')).toBe('b2');
});

test('ignores select outside list', () => {
  const { result } = renderHook(() => useActiveBranch(['b1']));
  act(() => result.current.select('zzz'));
  expect(result.current.activeBranchId).toBe('b1');
});
