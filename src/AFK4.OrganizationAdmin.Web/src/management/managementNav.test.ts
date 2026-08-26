import { describe, it, expect } from 'bun:test';
import { allowedManagementDestinations, managementDestinations } from './managementNav';
import { permissionNames } from '../operatorPermissions';

const sessionWith = (perms: string[]) => ({ permissions: perms }) as never;

describe('managementNav', () => {
  it('lists exactly the nine destinations in order', () => {
    expect(managementDestinations.map((d) => d.id)).toEqual([
      'club', 'booking', 'halls', 'tariffs', 'staff', 'goods', 'payments', 'news', 'events'
    ]);
  });

  // «Клуб» и «Приём броней» ходят под одним правом (ManageBranchSettings на сервере), поэтому
  // роль с настройками филиала видит оба раздела, а роль без него — ни одного.
  it('branch-settings permission opens both club and booking intake', () => {
    const settingsOnly = allowedManagementDestinations(sessionWith([permissionNames.manageBranchSettings]));
    expect(settingsOnly.map((d) => d.id)).toEqual(['club', 'booking']);
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
