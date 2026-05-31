import { describe, expect, it } from 'vitest';
import { groupPermissions } from './profileModel';

describe('platform groupPermissions', () => {
  it('groups permissions by prefix and sorts within and across groups', () => {
    const groups = groupPermissions(['tenants.write', 'billing.refund', 'tenants.read']);
    expect(groups).toEqual([
      { key: 'billing', permissions: ['billing.refund'] },
      { key: 'tenants', permissions: ['tenants.read', 'tenants.write'] }
    ]);
  });

  it('uses the whole string as the key when there is no dot', () => {
    expect(groupPermissions(['superuser'])).toEqual([{ key: 'superuser', permissions: ['superuser'] }]);
  });
});
