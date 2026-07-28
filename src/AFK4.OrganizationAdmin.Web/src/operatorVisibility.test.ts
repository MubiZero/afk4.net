import { describe, expect, it } from 'bun:test';
import { navSections } from './operatorData';
import { canOpenWorkspace } from './operatorPermissions';
import type { OperatorAuthSession } from './authClient';

// Canonical role → permission map. MUST mirror OrganizationPermissionCatalog.cs (backend source of truth)
// and the §2 visibility contract in docs/superpowers/specs/2026-06-14-operator-foundation-design.md.
// Changing a role's permissions on the backend REQUIRES updating this fixture and the expected
// sections below in the same change. (#37 honest, trackable contract — no silent drift.)
const rolePermissions: Record<string, string[]> = {
  operator: [
    'organization.floor_map.view', 'organization.sessions.start', 'organization.sessions.extend', 'organization.sessions.transfer', 'organization.sessions.end',
    'organization.sessions.view', 'organization.players.create', 'organization.players.view', 'organization.billing.view', 'organization.billing.wallet.top_up',
    'organization.billing.debt.pay', 'organization.tariffs.view', 'organization.packages.view', 'organization.packages.purchase', 'organization.shifts.open',
    'organization.shifts.view', 'organization.reservations.view', 'organization.reservations.manage', 'organization.pos.sales.create', 'organization.pos.sales.pay',
    'organization.receipts.view'
  ],
  shift_supervisor: [
    'organization.devices.commands.status.view', 'organization.devices.detail.view', 'organization.floor_map.view', 'organization.sessions.start',
    'organization.sessions.extend', 'organization.sessions.transfer', 'organization.sessions.end', 'organization.sessions.view', 'organization.players.create',
    'organization.players.view', 'organization.billing.view', 'organization.billing.wallet.top_up', 'organization.billing.refund',
    'organization.billing.manual_correction', 'organization.billing.debt.pay', 'organization.billing.money_action.approve', 'organization.tariffs.view',
    'organization.packages.view', 'organization.packages.purchase', 'organization.shifts.open', 'organization.shifts.close', 'organization.shifts.view',
    'organization.shifts.cash.manage', 'organization.reports.view', 'organization.reservations.view', 'organization.reservations.manage',
    'organization.pos.sales.create', 'organization.pos.sales.pay', 'organization.pos.sales.refund', 'organization.pos.sales.void', 'organization.inventory.view',
    'organization.receipts.view', 'organization.updates.status.view'
  ],
  branch_manager: [
    'organization.devices.enrollment_codes.create', 'organization.devices.commands.dispatch', 'organization.devices.commands.status.view',
    'organization.devices.credentials.rotate', 'organization.devices.credentials.revoke', 'organization.devices.seat_assignment.assign',
    'organization.devices.detail.view', 'organization.devices.install', 'organization.floor_map.view', 'organization.layout.manage', 'organization.sessions.start',
    'organization.sessions.extend', 'organization.sessions.transfer', 'organization.sessions.end', 'organization.sessions.view', 'organization.players.create',
    'organization.players.view', 'organization.billing.view', 'organization.billing.wallet.top_up', 'organization.billing.refund',
    'organization.billing.manual_correction', 'organization.billing.debt.pay', 'organization.billing.money_action.approve', 'organization.tariffs.manage',
    'organization.tariffs.view', 'organization.packages.manage', 'organization.packages.view', 'organization.packages.purchase', 'organization.shifts.open',
    'organization.shifts.close', 'organization.shifts.view', 'organization.shifts.cash.manage', 'organization.reports.view', 'organization.reservations.view',
    'organization.reservations.manage', 'organization.pos.catalog.manage', 'organization.shop.orders.manage', 'organization.pos.sales.create',
    'organization.pos.sales.pay', 'organization.pos.sales.refund', 'organization.pos.sales.void', 'organization.inventory.stock.manage', 'organization.inventory.view',
    'organization.receipts.view', 'organization.updates.packages.manage', 'organization.updates.rollouts.manage', 'organization.updates.status.view',
    'organization.diagnostics.view', 'organization.identity.branch_staff.manage', 'organization.audit.view', 'organization.branches.settings.manage'
  ],
  technician: [
    'organization.devices.enrollment_codes.create', 'organization.devices.commands.dispatch', 'organization.devices.commands.status.view',
    'organization.devices.credentials.rotate', 'organization.devices.credentials.revoke', 'organization.devices.seat_assignment.assign',
    'organization.devices.detail.view', 'organization.devices.install', 'organization.floor_map.view', 'organization.inventory.view',
    'organization.updates.packages.manage', 'organization.updates.rollouts.manage', 'organization.updates.status.view', 'organization.diagnostics.view'
  ],
  accountant: [
    'organization.sessions.view', 'organization.players.view', 'organization.billing.view', 'organization.tariffs.view', 'organization.packages.view', 'organization.shifts.view',
    'organization.reports.view', 'organization.reservations.view', 'organization.inventory.view', 'organization.receipts.view', 'organization.updates.status.view',
    'organization.diagnostics.view', 'organization.audit.view'
  ]
};

// §2 contract: which rail sections (navSections[].key) each role may see.
// NOTE (Управление redesign, 2026-07-16 design doc): the admin section's items collapsed to a
// single `management` workspace, and audit/logs left `Управление` (tracked separately as its own
// future rail section, not restored here). Admin-section visibility is now gated solely by the
// `management` rule (union of the eight management-destination permission sets), not by
// audit.view/diagnostics.view. The "Залы и ПК" (halls) destination's permission set is device
// MANAGEMENT perms only (enrollment/dispatch/assign/rotate/revoke + layout.manage) — read-only
// devices.detail.view/devices.commands.status.view were dropped from it (Task 1.5 fix) so that
// merely being able to *view* device status doesn't unlock the whole Управление section. As a
// result, relative to the pre-redesign admin section:
//   - shift_supervisor does NOT gain 'admin': its only device-related permissions are the
//     read-only devices.detail.view/devices.commands.status.view, which no longer qualify for
//     any management destination.
//   - branch_manager and technician keep 'admin': both hold real device-management permissions
//     (layout.manage for branch_manager; devices.enrollment_codes.create/commands.dispatch/
//     credentials.rotate/revoke/seat_assignment.assign for technician), so they qualify via
//     the "Залы и ПК" destination regardless of the read-only-perms fix.
//   - accountant loses 'admin': its permissions are all *.view and match none of the eight
//     management-destination permission sets (it previously saw 'admin' only via logs/audit).
//
// `network` (Task 4, gated by branches.view/billing.subscription.view/devices.install/
// audit.organization.view — networkNav.ts) is visible to more than just the owner, because
// OrganizationPermissionCatalog.cs already grants one of those four permissions to other roles for
// unrelated reasons. The Journal destination's gate was audit.view until the org-audit IDOR
// fix (org-audit leaked cross-branch data to any audit.view holder); it is now the owner-only
// audit.organization.view, so audit.view alone no longer unlocks `network`:
//   - branch_manager holds devices.install (but NOT audit.organization.view) → sees `network`
//     (Install only; Journal/Branches/Billing stay hidden).
//   - technician holds devices.install (field provisioning) → sees `network` (Install only).
//   - accountant holds only audit.view (no devices.install/branches.view/
//     billing.subscription.view/audit.organization.view) → `network` is now hidden entirely.
//   - operator and shift_supervisor hold none of the four → `network` stays hidden.
const expectedSections: Record<string, string[]> = {
  operator: ['map', 'booking', 'players', 'cashier'],
  shift_supervisor: ['map', 'booking', 'players', 'cashier', 'reports', 'stock'],
  branch_manager: ['map', 'booking', 'players', 'cashier', 'reports', 'admin', 'stock', 'network'],
  technician: ['map', 'admin', 'stock', 'network'],
  accountant: ['booking', 'players', 'cashier', 'reports', 'stock']
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

describe('management workspace visibility', () => {
  it('opens management for a role with any management permission', () => {
    const session = { permissions: rolePermissions.branch_manager } as OperatorAuthSession;
    expect(canOpenWorkspace(session, 'management')).toBe(true);
  });

  it('hides management for a role with no management permission', () => {
    const session = { permissions: rolePermissions.operator } as OperatorAuthSession;
    expect(canOpenWorkspace(session, 'management')).toBe(false);
  });
});

describe('stock workspace visibility', () => {
  it('inventory.view grants access to stock workspace', () => {
    const session = { permissions: ['organization.inventory.view'] } as OperatorAuthSession;
    expect(canOpenWorkspace(session, 'stock')).toBe(true);
  });

  it('inventory.stock.manage grants access to stock workspace', () => {
    const session = { permissions: ['organization.inventory.stock.manage'] } as OperatorAuthSession;
    expect(canOpenWorkspace(session, 'stock')).toBe(true);
  });

  it('session without inventory permissions cannot open stock workspace', () => {
    const session = { permissions: ['organization.floor_map.view', 'organization.sessions.view'] } as OperatorAuthSession;
    expect(canOpenWorkspace(session, 'stock')).toBe(false);
  });
});

describe('reports workspace visibility', () => {
  // dashboard rule is a union (reports.view OR audit.view) so a manager-auditor without
  // reports.view still opens «Отчёты» — landing on Journal (the only destination audit.view
  // unlocks in reportsNav.ts), instead of being shut out of the whole section.
  it('reports.view alone opens the dashboard workspace', () => {
    const session = { permissions: ['organization.reports.view'] } as OperatorAuthSession;
    expect(canOpenWorkspace(session, 'dashboard')).toBe(true);
  });

  it('audit.view alone opens the dashboard workspace', () => {
    const session = { permissions: ['organization.audit.view'] } as OperatorAuthSession;
    expect(canOpenWorkspace(session, 'dashboard')).toBe(true);
  });

  it('hides the dashboard workspace without reports.view or audit.view', () => {
    const session = { permissions: ['organization.floor_map.view'] } as OperatorAuthSession;
    expect(canOpenWorkspace(session, 'dashboard')).toBe(false);
  });
});

describe('network workspace visibility', () => {
  it('opens network for an owner-permission session', () => {
    const session = {
      permissions: [
        'organization.branches.view',
        'organization.billing.subscription.view',
        'organization.devices.install',
        'organization.audit.organization.view'
      ]
    } as OperatorAuthSession;
    expect(canOpenWorkspace(session, 'network')).toBe(true);
  });

  it('hides network for a cashier session', () => {
    const session = { permissions: rolePermissions.operator } as OperatorAuthSession;
    expect(canOpenWorkspace(session, 'network')).toBe(false);
  });

  // Regression: branch-scoped audit.view (BranchManager/AccountantAuditor) must not unlock
  // `network` on its own — only the owner-only audit.organization.view (Journal) or another
  // network-destination permission (devices.install/branches.view/billing.subscription.view)
  // should. Confirms the org-audit IDOR fix is reflected in nav visibility.
  it('hides network for a session with only branch-scoped audit.view', () => {
    const session = { permissions: ['organization.audit.view'] } as OperatorAuthSession;
    expect(canOpenWorkspace(session, 'network')).toBe(false);
  });
});
