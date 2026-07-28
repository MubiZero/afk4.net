import { describe, it, expect } from 'bun:test';
import { allowedManagementDestinations, managementDestinations } from './managementNav';
import { permissionNames } from '../operatorPermissions';

const sessionWith = (perms: string[]) => ({ permissions: perms }) as never;

describe('managementNav', () => {
  it('lists exactly the seven destinations in order', () => {
    expect(managementDestinations.map((d) => d.id)).toEqual([
      'club', 'halls', 'tariffs', 'staff', 'goods', 'payments', 'news'
    ]);
  });

  it('shows the merged payments section for either payment or loyalty permission', () => {
    const loyaltyOnly = allowedManagementDestinations(sessionWith([permissionNames.manageLoyaltySettings]));
    expect(loyaltyOnly.map((d) => d.id)).toEqual(['payments']);

    const gatewaysOnly = allowedManagementDestinations(sessionWith([permissionNames.managePaymentGateways]));
    expect(gatewaysOnly.map((d) => d.id)).toEqual(['payments']);
  });

  it('returns nothing for a null session', () => {
    expect(allowedManagementDestinations(null)).toEqual([]);
  });
});
