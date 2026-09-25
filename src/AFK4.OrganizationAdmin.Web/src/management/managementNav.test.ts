import { describe, it, expect } from 'bun:test';
import { allowedManagementDestinations, managementDestinations } from './managementNav';
import { permissionNames } from '../operatorPermissions';

const sessionWith = (perms: string[]) => ({ permissions: perms }) as never;

describe('managementNav', () => {
  it('lists exactly the twelve destinations in order', () => {
    expect(managementDestinations.map((d) => d.id)).toEqual([
      'club', 'booking', 'halls', 'protection', 'games', 'tariffs', 'staff', 'goods', 'payments', 'news', 'events', 'reviews'
    ]);
  });

  // Библиотеку игр собирает тот, кто ставит ПК, — отдельное право, не настройки филиала.
  it('the game-library permission opens only Games', () => {
    expect(allowedManagementDestinations(sessionWith([permissionNames.manageGameLibrary])).map((d) => d.id)).toEqual(['games']);
  });

  // «Клуб» и «Приём броней» ходят под одним правом (ManageBranchSettings на сервере), поэтому
  // роль с настройками филиала видит оба раздела, а роль без него — ни одного.
  it('branch-settings permission opens club, booking intake and PC protection', () => {
    const settingsOnly = allowedManagementDestinations(sessionWith([permissionNames.manageBranchSettings]));
    expect(settingsOnly.map((d) => d.id)).toEqual(['club', 'booking', 'protection']);
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
