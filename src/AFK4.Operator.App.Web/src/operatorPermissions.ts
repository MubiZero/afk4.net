import type { OperatorAuthSession } from './authClient';
import type { WorkspaceId } from './operatorTypes';

export const workspaceIds: WorkspaceId[] = ['map', 'dashboard', 'booking', 'cash', 'players', 'payment_cards', 'logs', 'settings', 'loyalty', 'news', 'stock'];

export const permissionNames = {
  viewFloorMap: 'floor_map.view',
  startSession: 'sessions.start',
  extendSession: 'sessions.extend',
  transferSession: 'sessions.transfer',
  endSession: 'sessions.end',
  viewSessions: 'sessions.view',
  viewPlayers: 'players.view',
  createPlayerAccount: 'players.create',
  viewBilling: 'billing.view',
  topUpWallet: 'billing.wallet.top_up',
  payDebt: 'billing.debt.pay',
  viewPackages: 'packages.view',
  managePackages: 'packages.manage',
  purchasePackage: 'packages.purchase',
  viewShift: 'shifts.view',
  openShift: 'shifts.open',
  closeShift: 'shifts.close',
  manageShiftCash: 'shifts.cash.manage',
  viewReports: 'reports.view',
  viewReservations: 'reservations.view',
  manageReservations: 'reservations.manage',
  createPosSale: 'pos.sales.create',
  payPosSale: 'pos.sales.pay',
  refundPosSale: 'pos.sales.refund',
  voidPosSale: 'pos.sales.void',
  viewInventory: 'inventory.view',
  manageInventoryStock: 'inventory.stock.manage',
  managePosCatalog: 'pos.catalog.manage',
  viewReceipt: 'receipts.view',
  viewDiagnostics: 'diagnostics.view',
  manageBranchStaff: 'identity.branch_staff.manage',
  manageRoles: 'identity.roles.manage',
  manageLayout: 'layout.manage',
  manageBranchSettings: 'branches.settings.manage',
  createDeviceEnrollmentCode: 'devices.enrollment_codes.create',
  assignDeviceSeat: 'devices.seat_assignment.assign',
  viewDeviceDetail: 'devices.detail.view',
  dispatchDeviceCommand: 'devices.commands.dispatch',
  rotateDeviceCredential: 'devices.credentials.rotate',
  revokeDeviceCredential: 'devices.credentials.revoke',
  manageTariffs: 'tariffs.manage',
  viewTariffs: 'tariffs.view',
  viewUpdateStatus: 'updates.status.view',
  manageUpdatePackages: 'updates.packages.manage',
  manageUpdateRollouts: 'updates.rollouts.manage',
  viewDeviceCommandStatus: 'devices.commands.status.view',
  viewAudit: 'audit.view',
  approveMoneyAction: 'billing.money_action.approve',
  manualCorrection: 'billing.manual_correction',
  refundLedgerEntry: 'billing.refund',
  managePaymentGateways: 'payments.gateways.manage',
  manageLoyaltySettings: 'loyalty.settings.manage',
  manageNews: 'news.manage'
} as const;

export const staffRoleOptions = ['cashier_operator', 'shift_supervisor', 'branch_manager', 'technician', 'accountant_auditor'] as const;

export const workspacePermissionRules: Record<WorkspaceId, readonly string[]> = {
  map: [permissionNames.viewFloorMap],
  dashboard: [permissionNames.viewReports],
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
  payment_cards: [permissionNames.managePaymentGateways],
  logs: [permissionNames.viewAudit, permissionNames.viewDiagnostics],
  settings: [
    permissionNames.manageBranchStaff,
    permissionNames.manageLayout,
    permissionNames.createDeviceEnrollmentCode,
    permissionNames.assignDeviceSeat,
    permissionNames.rotateDeviceCredential,
    permissionNames.revokeDeviceCredential,
    permissionNames.manageInventoryStock,
    permissionNames.managePosCatalog,
    permissionNames.managePackages,
    permissionNames.manageUpdatePackages,
    permissionNames.manageUpdateRollouts,
    permissionNames.manageTariffs
  ],
  loyalty: [permissionNames.manageLoyaltySettings],
  news: [permissionNames.manageNews],
  stock: [permissionNames.viewInventory, permissionNames.manageInventoryStock]
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
