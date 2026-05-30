import { it, expect } from 'vitest';
import { groupPermissions, resolveBranchNames } from './profileModel';

it('groups permissions by their prefix, sorted', () => {
  const groups = groupPermissions(['billing.refund', 'players.view', 'billing.wallet.top_up', 'players.create']);
  expect(groups).toEqual([
    { key: 'billing', permissions: ['billing.refund', 'billing.wallet.top_up'] },
    { key: 'players', permissions: ['players.create', 'players.view'] }
  ]);
});

it('uses the whole string as group when there is no dot', () => {
  expect(groupPermissions(['admin'])).toEqual([{ key: 'admin', permissions: ['admin'] }]);
});

it('resolves branch names with a fallback', () => {
  const names = resolveBranchNames(['b1', 'b2'], { b1: { name: 'Центр' } }, 'Без названия');
  expect(names).toEqual([{ branchId: 'b1', name: 'Центр' }, { branchId: 'b2', name: 'Без названия' }]);
});
