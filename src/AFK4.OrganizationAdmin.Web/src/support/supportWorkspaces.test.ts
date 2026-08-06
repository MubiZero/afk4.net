import { it, expect } from 'bun:test';
import { supportVisibleWorkspaces, supportPermissions } from './supportWorkspaces';
import { permissionNames } from '../permissionNames';

it('supportVisibleWorkspaces: no areas → no workspaces unlocked', () => {
  expect(supportVisibleWorkspaces([])).toEqual(new Set());
});

it('supportVisibleWorkspaces: each area unlocks exactly its own workspace', () => {
  expect(supportVisibleWorkspaces(['floor-map'])).toEqual(new Set(['map']));
  expect(supportVisibleWorkspaces(['branch-settings'])).toEqual(new Set(['management']));
  expect(supportVisibleWorkspaces(['branch-profile'])).toEqual(new Set(['management']));
  expect(supportVisibleWorkspaces(['staff'])).toEqual(new Set(['management']));
  expect(supportVisibleWorkspaces(['devices'])).toEqual(new Set(['network']));
});

it('supportVisibleWorkspaces: full grant unlocks map/management/network and nothing money-related', () => {
  const visible = supportVisibleWorkspaces(['branch-settings', 'devices', 'staff', 'floor-map', 'branch-profile']);
  expect(visible).toEqual(new Set(['map', 'management', 'network']));
  for (const moneyWorkspace of ['cash', 'booking', 'players', 'stock', 'dashboard'] as const) {
    expect(visible.has(moneyWorkspace)).toBe(false);
  }
});

it('supportPermissions: no areas → only read permissions, no write permissions at all', () => {
  const permissions = supportPermissions([]);
  expect(permissions).toContain(permissionNames.viewFloorMap);
  expect(permissions).toContain(permissionNames.viewReports);
  expect(permissions.every((permission) => permission.endsWith('.view'))).toBe(true);
});

it('supportPermissions: a write permission only appears once its area is in the grant', () => {
  expect(supportPermissions([])).not.toContain(permissionNames.manageBranchSettings);
  expect(supportPermissions(['branch-settings'])).toContain(permissionNames.manageBranchSettings);
  expect(supportPermissions(['branch-profile'])).toContain(permissionNames.manageBranchSettings);

  expect(supportPermissions([])).not.toContain(permissionNames.manageLayout);
  expect(supportPermissions(['floor-map'])).toContain(permissionNames.manageLayout);

  expect(supportPermissions([])).not.toContain(permissionNames.manageBranchStaff);
  expect(supportPermissions([])).not.toContain(permissionNames.manageRoles);
  expect(supportPermissions(['staff'])).toContain(permissionNames.manageBranchStaff);
  expect(supportPermissions(['staff'])).toContain(permissionNames.manageRoles);

  expect(supportPermissions([])).not.toContain(permissionNames.assignDeviceSeat);
  expect(supportPermissions(['devices'])).toContain(permissionNames.assignDeviceSeat);
  expect(supportPermissions(['devices'])).toContain(permissionNames.rotateDeviceCredential);
  expect(supportPermissions(['devices'])).toContain(permissionNames.revokeDeviceCredential);
  expect(supportPermissions(['devices'])).toContain(permissionNames.dispatchDeviceCommand);
  expect(supportPermissions(['devices'])).toContain(permissionNames.createDeviceEnrollmentCode);
});

it('supportPermissions: permissions with no server-tagged write endpoint under support never appear, regardless of areas granted', () => {
  const allAreas = ['branch-settings', 'devices', 'staff', 'floor-map', 'branch-profile'];
  const permissions = supportPermissions(allAreas);

  expect(permissions).not.toContain(permissionNames.openShift);
  expect(permissions).not.toContain(permissionNames.manageTariffs);
  expect(permissions).not.toContain(permissionNames.managePackages);
  expect(permissions).not.toContain(permissionNames.managePosCatalog);
  expect(permissions).not.toContain(permissionNames.manageInventoryStock);
  expect(permissions).not.toContain(permissionNames.managePaymentGateways);
  expect(permissions).not.toContain(permissionNames.manageLoyaltySettings);
  expect(permissions).not.toContain(permissionNames.manageNews);
  expect(permissions).not.toContain(permissionNames.installDevice);
});

it('supportPermissions: an unknown area is ignored rather than throwing', () => {
  expect(() => supportPermissions(['some-future-area'])).not.toThrow();
  expect(supportPermissions(['some-future-area'])).toEqual(supportPermissions([]));
});
