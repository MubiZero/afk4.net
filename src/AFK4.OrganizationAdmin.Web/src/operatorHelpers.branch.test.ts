import { test, expect } from 'bun:test';
import { resolveActiveBranchId } from './operatorHelpers';

const session = { branchIds: ['b1', 'b2'], activeBranchId: undefined } as never;

test('chosen branch wins when valid', () => {
  expect(resolveActiveBranchId(session, 'b1', 'b2')).toBe('b2');
});

test('invalid chosen falls through to machine pin', () => {
  expect(resolveActiveBranchId(session, 'b1', 'zzz')).toBe('b1');
});

test('no chosen → pin → first', () => {
  expect(resolveActiveBranchId(session, undefined, undefined)).toBe('b1');
});

// Филиал, которого нет в сессии, — не активный, а чужой: сервер отказывает в каждом запросе к нему.
// Привязка ПК (config.branchId) и session.activeBranchId раньше проходили без проверки, и
// сотрудник без филиала попадал в чужой филиал вместо честного «нет активного филиала».
test('machine pin outside the session branches falls through to the first own branch', () => {
  expect(resolveActiveBranchId(session, 'b9', undefined)).toBe('b1');
});

test('no own branches → null even with a machine pin', () => {
  const noBranches = { branchIds: [], activeBranchId: undefined } as never;
  expect(resolveActiveBranchId(noBranches, 'b1', undefined)).toBeNull();
});

test('session.activeBranchId outside the session branches is ignored', () => {
  const stale = { branchIds: ['b2'], activeBranchId: 'b1' } as never;
  expect(resolveActiveBranchId(stale, undefined, undefined)).toBe('b2');
});
