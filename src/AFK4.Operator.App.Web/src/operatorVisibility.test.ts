import { describe, expect, it } from 'bun:test';
import { navSections } from './operatorData';
import { canOpenWorkspace } from './operatorPermissions';
import type { OperatorAuthSession } from './authClient';

// Canonical role → permission map. MUST mirror PermissionCatalog.cs (backend source of truth)
// and the §2 visibility contract in docs/superpowers/specs/2026-06-14-operator-foundation-design.md.
// Changing a role's permissions on the backend REQUIRES updating this fixture and the expected
// sections below in the same change. (#37 honest, trackable contract — no silent drift.)
const rolePermissions: Record<string, string[]> = {
  cashier_operator: [
    'floor_map.view', 'sessions.start', 'sessions.extend', 'sessions.transfer', 'sessions.end',
    'sessions.view', 'players.create', 'players.view', 'billing.view', 'billing.wallet.top_up',
    'billing.debt.pay', 'tariffs.view', 'packages.view', 'packages.purchase', 'shifts.open',
    'shifts.view', 'reservations.view', 'reservations.manage', 'pos.sales.create', 'pos.sales.pay',
    'receipts.view'
  ],
  shift_supervisor: [
    'devices.commands.status.view', 'devices.detail.view', 'floor_map.view', 'sessions.start',
    'sessions.extend', 'sessions.transfer', 'sessions.end', 'sessions.view', 'players.create',
    'players.view', 'billing.view', 'billing.wallet.top_up', 'billing.refund',
    'billing.manual_correction', 'billing.debt.pay', 'billing.money_action.approve', 'tariffs.view',
    'packages.view', 'packages.purchase', 'shifts.open', 'shifts.close', 'shifts.view',
    'shifts.cash.manage', 'reports.view', 'reservations.view', 'reservations.manage',
    'pos.sales.create', 'pos.sales.pay', 'pos.sales.refund', 'pos.sales.void', 'inventory.view',
    'receipts.view', 'updates.status.view'
  ],
  branch_manager: [
    'devices.enrollment_codes.create', 'devices.commands.dispatch', 'devices.commands.status.view',
    'devices.credentials.rotate', 'devices.credentials.revoke', 'devices.seat_assignment.assign',
    'devices.detail.view', 'devices.install', 'floor_map.view', 'layout.manage', 'sessions.start',
    'sessions.extend', 'sessions.transfer', 'sessions.end', 'sessions.view', 'players.create',
    'players.view', 'billing.view', 'billing.wallet.top_up', 'billing.refund',
    'billing.manual_correction', 'billing.debt.pay', 'billing.money_action.approve', 'tariffs.manage',
    'tariffs.view', 'packages.manage', 'packages.view', 'packages.purchase', 'shifts.open',
    'shifts.close', 'shifts.view', 'shifts.cash.manage', 'reports.view', 'reservations.view',
    'reservations.manage', 'pos.catalog.manage', 'shop.orders.manage', 'pos.sales.create',
    'pos.sales.pay', 'pos.sales.refund', 'pos.sales.void', 'inventory.stock.manage', 'inventory.view',
    'receipts.view', 'updates.packages.manage', 'updates.rollouts.manage', 'updates.status.view',
    'diagnostics.view', 'identity.branch_staff.manage', 'audit.view', 'branches.settings.manage'
  ],
  technician: [
    'devices.enrollment_codes.create', 'devices.commands.dispatch', 'devices.commands.status.view',
    'devices.credentials.rotate', 'devices.credentials.revoke', 'devices.seat_assignment.assign',
    'devices.detail.view', 'devices.install', 'floor_map.view', 'inventory.view',
    'updates.packages.manage', 'updates.rollouts.manage', 'updates.status.view', 'diagnostics.view'
  ],
  accountant_auditor: [
    'sessions.view', 'players.view', 'billing.view', 'tariffs.view', 'packages.view', 'shifts.view',
    'reports.view', 'reservations.view', 'inventory.view', 'receipts.view', 'updates.status.view',
    'diagnostics.view', 'audit.view'
  ]
};

// §2 contract: which rail sections (navSections[].key) each role may see.
const expectedSections: Record<string, string[]> = {
  cashier_operator: ['map', 'booking', 'players', 'cashier'],
  shift_supervisor: ['map', 'booking', 'players', 'cashier', 'reports', 'stock'],
  branch_manager: ['map', 'booking', 'players', 'cashier', 'reports', 'admin', 'stock'],
  technician: ['map', 'admin', 'stock'],
  accountant_auditor: ['booking', 'players', 'cashier', 'reports', 'admin', 'stock']
};

function visibleSections(permissions: string[]): string[] {
  const session = { permissions } as OperatorAuthSession;
  return navSections
    .filter((section) => section.items.some((item) => canOpenWorkspace(session, item.id)))
    .map((section) => section.key)
    .sort();
}

describe('role → visible rail sections (Этап 0 §2 contract)', () => {
  for (const role of Object.keys(expectedSections)) {
    it(`${role} sees exactly the contracted sections`, () => {
      expect(visibleSections(rolePermissions[role])).toEqual([...expectedSections[role]].sort());
    });
  }
});

describe('stock workspace visibility', () => {
  it('inventory.view grants access to stock workspace', () => {
    const session = { permissions: ['inventory.view'] } as OperatorAuthSession;
    expect(canOpenWorkspace(session, 'stock')).toBe(true);
  });

  it('inventory.stock.manage grants access to stock workspace', () => {
    const session = { permissions: ['inventory.stock.manage'] } as OperatorAuthSession;
    expect(canOpenWorkspace(session, 'stock')).toBe(true);
  });

  it('session without inventory permissions cannot open stock workspace', () => {
    const session = { permissions: ['floor_map.view', 'sessions.view'] } as OperatorAuthSession;
    expect(canOpenWorkspace(session, 'stock')).toBe(false);
  });
});
