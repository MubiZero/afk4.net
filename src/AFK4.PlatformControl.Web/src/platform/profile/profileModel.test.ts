import { describe, expect, it } from 'bun:test';
import { groupPermissions } from './profileModel';

describe('platform groupPermissions', () => {
  it('groups permissions by prefix and sorts within and across groups', () => {
    const groups = groupPermissions(['organizations.write', 'billing.refund', 'organizations.read']);
    expect(groups).toEqual([
      { key: 'billing', permissions: ['billing.refund'] },
      { key: 'organizations', permissions: ['organizations.read', 'organizations.write'] }
    ]);
  });

  it('uses the whole string as the key when there is no dot', () => {
    expect(groupPermissions(['superuser'])).toEqual([{ key: 'superuser', permissions: ['superuser'] }]);
  });
});
