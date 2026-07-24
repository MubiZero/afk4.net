import type { OperatorAuthSession } from './authClient';
import type { WorkspaceId } from './operatorTypes';
import { managementDestinations } from './management/managementNav';
import { networkDestinations } from './network/networkNav';
import { permissionNames } from './permissionNames';

export const workspaceIds: WorkspaceId[] = ['map', 'dashboard', 'booking', 'cash', 'players', 'management', 'stock', 'network'];

// Re-exported so existing `import { permissionNames } from './operatorPermissions'` call sites keep
// working; the catalog itself lives in permissionNames.ts to avoid an import cycle with
// management/managementNav.ts (see that file's comment).
export { permissionNames };

export const staffRoleOptions = ['cashier_operator', 'shift_supervisor', 'branch_manager', 'technician', 'accountant_auditor'] as const;

export const workspacePermissionRules: Record<WorkspaceId, readonly string[]> = {
  map: [permissionNames.viewFloorMap],
  dashboard: [permissionNames.viewReports, permissionNames.viewAudit],
  booking: [permissionNames.viewReservations],
  cash: [
    permissionNames.createPosSale,
    permissionNames.payPosSale,
    permissionNames.refundPosSale,
    permissionNames.voidPosSale,
    permissionNames.viewShift,
    permissionNames.openShift,
    permissionNames.closeShift,
    permissionNames.manageShiftCash,
    permissionNames.viewReports,
    permissionNames.viewReceipt,
    permissionNames.approveMoneyAction
  ],
  players: [
    permissionNames.viewPlayers,
    permissionNames.createPlayerAccount,
    permissionNames.viewBilling,
    permissionNames.topUpWallet,
    permissionNames.payDebt,
    permissionNames.viewPackages,
    permissionNames.purchasePackage
  ],
  // Union of the eight `Управление` destinations' permission sets (managementNav.ts), deduped —
  // canOpenWorkspace(session, 'management') is then equivalent to
  // allowedManagementDestinations(session).length > 0.
  management: [...new Set(managementDestinations.flatMap((destination) => destination.permissions))],
  stock: [permissionNames.viewInventory, permissionNames.manageInventoryStock],
  // Union of the four `Сеть` destinations' permission sets (networkNav.ts), deduped — same
  // equivalence as `management` above: canOpenWorkspace(session, 'network') iff
  // allowedNetworkDestinations(session).length > 0.
  network: [...new Set(networkDestinations.flatMap((destination) => destination.permissions))]
};

export function hasPermission(session: OperatorAuthSession | null, permission: string) {
  return session?.permissions?.some((candidate) => candidate.toLowerCase() === permission.toLowerCase()) ?? false;
}

export function hasAllPermissions(session: OperatorAuthSession | null, permissions: readonly string[]) {
  return permissions.every((permission) => hasPermission(session, permission));
}

export function hasAnyPermission(session: OperatorAuthSession | null, permissions: readonly string[]) {
  return permissions.some((permission) => hasPermission(session, permission));
}

export function canOpenWorkspace(session: OperatorAuthSession | null, workspaceId: WorkspaceId) {
  return hasAnyPermission(session, workspacePermissionRules[workspaceId]);
}

export function firstAllowedWorkspace(session: OperatorAuthSession | null) {
  return workspaceIds.find((workspaceId) => canOpenWorkspace(session, workspaceId)) ?? 'map';
}
