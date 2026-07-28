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
