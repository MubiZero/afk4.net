import { describe, it, expect } from 'bun:test';
import { allowedManagementDestinations, managementDestinations, managementGroups } from './managementNav';
import { permissionNames } from '../operatorPermissions';

const sessionWith = (perms: string[]) => ({ permissions: perms }) as never;

describe('managementNav', () => {
  // Меню сгруппировано (решение владельца 29.09): Клуб · ПК · Деньги · Люди. Порядок массива — порядок
  // пунктов, и группы в нём идут подряд, иначе одна группа рисовалась бы двумя кусками.
  it('lists the twelve destinations grouped as club · PCs · money · people', () => {
    expect(managementGroups.map((g) => g.id)).toEqual(['club', 'pcs', 'money', 'people']);
    expect(managementDestinations.map((d) => `${d.group}:${d.id}`)).toEqual([
      'club:club', 'club:booking', 'club:news', 'club:events', 'club:reviews',
      'pcs:halls', 'pcs:protection', 'pcs:games',
      'money:tariffs', 'money:goods', 'money:payments',
      'people:staff'
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
