export interface PlatformAdminSignInResponse {
  platformAdminId: string;
  userName: string;
  displayName: string;
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
  refreshTokenExpiresAtUtc: string;
  roles: string[];
  permissions: string[];
}

export interface StaffSignInResponse {
  staffUserId: string;
  organizationId: string;
  displayName: string;
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
  refreshTokenExpiresAtUtc: string;
  branchIds: string[];
  permissions: string[];
}

export interface AcceptOwnerInviteRequest {
  code: string;
  userName: string;
  displayName: string;
  password: string;
}

export interface OwnerCodeSummary {
  codeSuffix: string;
  expiresAtUtc: string;
  lastUsedAtUtc: string | null;
  failedAttemptCount: number;
}

export interface OwnerCodeIssued {
  ownerCode: string;
  codeSuffix: string;
  expiresAtUtc: string;
}

export interface BranchProfile {
  organizationId: string;
  branchId: string;
  name: string;
  city: string;
  createdAtUtc: string;
}

export interface BranchSettings {
  organizationId: string;
  branchId: string;
  requireManualDeviceApproval: boolean;
}

export interface UpdateBranchProfileRequest {
  organizationId: string;
  name: string;
  city: string;
}

export interface UpdateBranchSettingsRequest {
  organizationId: string;
  requireManualDeviceApproval: boolean;
}

export interface SeatStatus {
  seatId: string;
  seatName: string;
  zoneId: string;
  zoneName: string;
  sortOrder: number;
  state: string;
  deviceId: string | null;
  deviceName: string | null;
  isDeviceOnline: boolean | null;
  isDeviceLocked: boolean | null;
  lastHeartbeatAtUtc: string | null;
  agentVersion: string | null;
  shellVersion: string | null;
  activeSessionId: string | null;
  remainingSeconds: number | null;
}

export interface FloorMap {
  branchId: string;
  branchName: string;
  zones: FloorMapZone[];
  seats: SeatStatus[];
}

export interface FloorMapZone {
  zoneId: string;
  name: string;
  sortOrder: number;
}

export interface FloorMapRead {
  etag: string | null;
  floorMap: FloorMap;
}

export interface FloorMapBulkUpdateRequest {
  organizationId: string;
  zones: FloorMapBulkZoneRequest[];
  seats: FloorMapBulkSeatRequest[];
}

export interface FloorMapBulkZoneRequest {
  zoneId: string | null;
  clientId: string;
  name: string;
  sortOrder: number;
}

export interface FloorMapBulkSeatRequest {
  seatId: string | null;
  clientId: string;
  zoneClientId: string;
  name: string;
  sortOrder: number;
}

export interface FloorMapBulkUpdateResponse {
  eTag: string;
  zones: Array<{ clientId: string; zoneId: string }>;
  seats: Array<{ clientId: string; seatId: string }>;
}

export interface Money {
  amount: number;
  currencyCode: string;
}

export interface OperatorDashboardSummary {
  organizationId: string;
  branchId: string;
  fromUtc: string;
  toUtc: string;
  generatedAtUtc: string;
  utilization: {
    totalSeats: number;
    activeSessions: number;
    endingSessions: number;
    onlineDevices: number;
    offlineDevices: number;
    sessionStarts: number;
    utilizationPercent: number;
  };
  alertPressure: {
    pendingCommands: number;
    failedCommands: number;
    offlineDevices: number;
    endingSessions: number;
    totalAlerts: number;
  };
  revenue: {
    posNetSales: Money;
    gameplayRevenue: Money;
    totalRevenue: Money;
    posCheckCount: number;
    newPlayerCount: number;
  };
}

export interface DeviceInventoryItem {
  organizationId: string;
  branchId: string;
  deviceId: string;
  machineName: string;
  agentVersion: string;
  shellVersion: string;
  enrolledAtUtc: string;
  lastHeartbeatAtUtc: string | null;
  isOnline: boolean;
  isLocked: boolean;
  seatId: string | null;
  seatName: string | null;
  zoneId: string | null;
  zoneName: string | null;
  activeCredentialCount: number;
  installedAppCount: number;
  pendingCommandCount: number;
  failedCommandCount: number;
  displayName: string;
  role: string;
  enrollmentState: string;
}

export interface StaffUser {
  staffUserId: string;
  organizationId: string;
  userName: string;
  displayName: string;
  isActive: boolean;
  roleNames: string[];
  createdAtUtc: string;
}

export interface CreateStaffUserRequest {
  organizationId: string;
  userName: string;
  displayName: string;
  password: string;
  roleNames: string[];
}

export interface UpdateStaffUserRolesRequest {
  organizationId: string;
  roleNames: string[];
}

export interface UpdateStaffUserProfileRequest {
  organizationId: string;
  userName: string;
  displayName: string;
}

export interface UpdateStaffUserStateRequest {
  organizationId: string;
  isActive: boolean;
}

export interface ResetStaffUserPasswordRequest {
  organizationId: string;
  newPassword: string;
}

export interface TenantSummary {
  organizationId: string;
  slug: string;
  name: string;
  status: string;
  planCode: string;
  subscriptionStatus: string;
  branchCount: number;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface TenantBranch {
  branchId: string;
  slug: string;
  name: string;
  city: string;
  createdAtUtc: string;
}

export interface TenantLimits {
  maxBranches: number | null;
  maxDevicesPerBranch: number | null;
  maxConcurrentSessions: number | null;
  maxStaffUsersPerBranch: number | null;
}

export interface TenantDetail {
  organizationId: string;
  slug: string;
  name: string;
  status: string;
  statusReason: string | null;
  statusChangedAtUtc: string | null;
  planCode: string;
  subscriptionStatus: string;
  limits: TenantLimits;
  branches: TenantBranch[];
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CreateTenantRequest {
  organizationSlug: string;
  organizationName: string;
  branchSlug: string;
  branchName: string;
  branchCity: string;
  planCode: string;
  subscriptionStatus: string;
  limits: TenantLimits | null;
  ownerUserName: string | null;
  ownerDisplayName: string | null;
  ownerInviteLifetime: string | null;
}

export interface OwnerInvite {
  ownerInviteId: string;
  organizationId: string;
  branchId: string;
  code: string;
  status: string;
  ownerUserName: string | null;
  ownerDisplayName: string | null;
  expiresAtUtc: string;
  acceptedAtUtc: string | null;
  revokedAtUtc: string | null;
  revokedReason: string | null;
  createdAtUtc: string;
}

export interface OwnerInviteSummary {
  ownerInviteId: string;
  organizationId: string;
  branchId: string;
  codeSuffix: string;
  status: string;
  ownerUserName: string | null;
  ownerDisplayName: string | null;
  expiresAtUtc: string;
  acceptedAtUtc: string | null;
  revokedAtUtc: string | null;
  revokedReason: string | null;
  createdAtUtc: string;
}

export interface CreateTenantResponse {
  tenant: TenantDetail;
  ownerInvite: OwnerInvite;
}

export interface TenantSupportNote {
  tenantSupportNoteId: string;
  organizationId: string;
  authorPlatformAdminId: string;
  authorDisplayName: string;
  body: string;
  createdAtUtc: string;
}

export interface TenantHealthError {
  createdAtUtc: string;
  source: string;
  action: string;
  outcome: string;
  message: string | null;
}

export interface TenantHealth {
  organizationId: string;
  status: string;
  branchCount: number;
  deviceCount: number;
  activeStaffUserCount: number;
  latestStaffSignInAtUtc: string | null;
  latestMigration: string | null;
  recentErrorCount: number;
  recentErrors: TenantHealthError[];
}

export const TenantStatus = {
  Active: 'active',
  Suspended: 'suspended',
  DeletionPending: 'deletion_pending'
} as const;
export type TenantStatusValue = (typeof TenantStatus)[keyof typeof TenantStatus];

export const TenantPlanCode = {
  Starter: 'starter',
  Growth: 'growth',
  Scale: 'scale'
} as const;

export const SubscriptionStatus = {
  Trial: 'trial',
  Active: 'active',
  PastDue: 'past_due',
  Cancelled: 'cancelled'
} as const;

export interface TariffOption {
  tariffId: string;
  tariffVersionId: string;
  name: string;
  tariffRuleVersionId: string;
  versionNumber: number;
  currencyCode: string;
  pricePerMinuteMinorUnits: number;
  minimumBillableMinutes: number;
  roundingIncrementMinutes: number;
  effectiveFromUtc: string;
}

export interface Tariff {
  tariffId: string;
  organizationId: string;
  branchId: string;
  name: string;
  isActive: boolean;
  createdAtUtc: string;
}

export interface TariffVersion {
  tariffVersionId: string;
  tariffId: string;
  versionNumber: number;
  currencyCode: string;
  pricePerMinuteMinorUnits: number;
  minimumBillableMinutes: number;
  roundingIncrementMinutes: number;
  effectiveFromUtc: string;
  retiredAtUtc: string | null;
  createdAtUtc: string;
}

export interface CreateTariffRequest {
  organizationId: string;
  name: string;
  idempotencyKey: string;
}

export interface UpdateTariffRequest {
  organizationId: string;
  name: string;
  isActive: boolean;
}

export interface CreateTariffVersionRequest {
  organizationId: string;
  tariffId: string;
  currencyCode: string;
  pricePerMinuteMinorUnits: number;
  minimumBillableMinutes: number;
  roundingIncrementMinutes: number;
  effectiveFromUtc: string;
  idempotencyKey: string;
}

export interface UpdateTariffVersionRequest {
  organizationId: string;
  currencyCode: string;
  pricePerMinuteMinorUnits: number;
  minimumBillableMinutes: number;
  roundingIncrementMinutes: number;
  effectiveFromUtc: string;
  isActive: boolean;
}

export interface MoneyMinor {
  currencyCode: string;
  minorUnits: number;
}

export interface PosProduct {
  productId: string;
  organizationId: string;
  branchId: string;
  categoryId: string;
  name: string;
  sku: string;
  price: MoneyMinor;
  trackStock: boolean;
  allowNegativeStock: boolean;
  isActive: boolean;
  stockOnHand: number;
  createdAtUtc: string;
}

export interface PosProductCategory {
  categoryId: string;
  organizationId: string;
  branchId: string;
  name: string;
  isActive: boolean;
  createdAtUtc: string;
}

export interface CreateProductCategoryRequest {
  organizationId: string;
  name: string;
  idempotencyKey: string;
}

export interface CreateProductRequest {
  organizationId: string;
  categoryId: string;
  name: string;
  sku: string;
  price: MoneyMinor;
  trackStock: boolean;
  allowNegativeStock: boolean;
  idempotencyKey: string;
}

export interface UpdateProductRequest {
  organizationId: string;
  categoryId: string;
  name: string;
  sku: string;
  price: MoneyMinor;
  trackStock: boolean;
  allowNegativeStock: boolean;
  isActive: boolean;
}
