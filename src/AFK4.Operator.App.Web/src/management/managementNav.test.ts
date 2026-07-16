import { describe, it, expect } from 'bun:test';
import { allowedManagementDestinations, managementDestinations } from './managementNav';
import { permissionNames } from '../operatorPermissions';

const sessionWith = (perms: string[]) => ({ permissions: perms }) as never;

describe('managementNav', () => {
  it('lists exactly the eight destinations in order', () => {
    expect(managementDestinations.map((d) => d.id)).toEqual([
      'club', 'halls', 'tariffs', 'staff', 'goods', 'payment', 'loyalty', 'news'
    ]);
  });

  it('hides destinations the session has no permission for', () => {
    const only = allowedManagementDestinations(sessionWith([permissionNames.manageLoyaltySettings]));
    expect(only.map((d) => d.id)).toEqual(['loyalty']);
  });

  it('returns nothing for a null session', () => {
    expect(allowedManagementDestinations(null)).toEqual([]);
  });
});
