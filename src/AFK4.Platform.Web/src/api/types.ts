// A browser-fetch-shaped injectable. We deliberately avoid `typeof fetch` here because
// @types/bun (pulled in for bun:test) redefines the global `fetch` type with a required
// `preconnect` member that mock implementations don't carry. The api clients only ever
// call this as a function, so this narrower contract is the correct one.
export type FetchLike = (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>;

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
  roleNames?: string[];
}

export interface StaffSignInClubChoice {
  organizationId: string;
  name: string;
}

export interface AcceptOwnerInviteRequest {
  code: string;
  userName: string;
  displayName: string;
  password: string;
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
  preferredLocale: string;
}

export interface UpdateBranchProfileRequest {
  organizationId: string;
  name: string;
  city: string;
}

export interface UpdateBranchSettingsRequest {
  organizationId: string;
  requireManualDeviceApproval: boolean;
  preferredLocale: string;
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

export interface CreateStaffInviteRequest {
  organizationId: string;
  userName: string;
  displayName: string;
  email: string;
  roleNames: string[];
}

export interface StaffInviteDto {
  staffInviteId: string;
  code: string;
  expiresAtUtc: string;
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

export interface PackageOption {
  packageDefinitionId: string;
  name: string;
  currencyCode: string;
  priceMinorUnits: number;
  includedSeconds: number;
  bonusSeconds: number;
  expiresAfterDays: number;
}

export interface PackageDefinition {
  packageDefinitionId: string;
  organizationId: string;
  branchId: string;
  name: string;
  price: MoneyMinor;
  includedSeconds: number;
  bonusSeconds: number;
  expiresAfterDays: number;
  isActive: boolean;
  createdAtUtc: string;
}

export interface CreatePackageDefinitionRequest {
  organizationId: string;
  name: string;
  price: MoneyMinor;
  includedSeconds: number;
  bonusSeconds: number;
  expiresAfterDays: number;
  idempotencyKey: string;
}

export interface UpdatePackageDefinitionRequest {
  organizationId: string;
  name: string;
  price: MoneyMinor;
  includedSeconds: number;
  bonusSeconds: number;
  expiresAfterDays: number;
  isActive: boolean;
}

export interface PlayerAccount {
  playerAccountId: string;
  organizationId: string;
  homeBranchId: string;
  displayName: string;
  phoneNumber: string | null;
  isActive: boolean;
  createdAtUtc: string;
}

export interface PlayerSearchResult {
  playerAccountId: string;
  displayName: string;
  phoneNumber: string | null;
  walletBalanceMinorUnits: number;
  debtBalanceMinorUnits: number;
  activePackageCount: number;
  isActive: boolean;
}

export interface LedgerEntry {
  ledgerEntryId: string;
  organizationId: string;
  branchId: string;
  playerAccountId: string;
  sessionId: string | null;
  playerPackageId: string | null;
  entryType: string;
  accountType: string;
  amount: MoneyMinor;
  quantitySeconds: number;
  description: string;
  reason: string;
  reversesLedgerEntryId: string | null;
  createdByStaffUserId: string;
  createdAtUtc: string;
}

export interface WalletSummary {
  playerAccountId: string;
  walletBalance: MoneyMinor;
  debtBalance: MoneyMinor;
  recentEntries: LedgerEntry[];
}

export interface PlayerPackage {
  playerPackageId: string;
  packageDefinitionId: string;
  playerAccountId: string;
  name: string;
  purchasedPrice: MoneyMinor;
  includedSeconds: number;
  bonusSeconds: number;
  remainingIncludedSeconds: number;
  remainingBonusSeconds: number;
  purchasedAtUtc: string;
  expiresAtUtc: string | null;
}

export interface CreatePlayerAccountRequest {
  organizationId: string;
  displayName: string;
  phoneNumber: string | null;
  idempotencyKey: string;
}

export interface TopUpWalletRequest {
  organizationId: string;
  amount: MoneyMinor;
  reason: string;
  idempotencyKey: string;
}

export interface PayDebtRequest {
  organizationId: string;
  amount: MoneyMinor;
  reason: string;
  idempotencyKey: string;
}

export interface ManualLedgerCorrectionRequest {
  organizationId: string;
  accountType: string;
  amount: MoneyMinor;
  quantitySeconds: number;
  reason: string;
  idempotencyKey: string;
}

export interface RefundLedgerEntryRequest {
  organizationId: string;
  ledgerEntryId: string;
  amount: MoneyMinor;
  reason: string;
  idempotencyKey: string;
}

export interface PurchasePackageRequest {
  organizationId: string;
  packageDefinitionId: string;
  idempotencyKey: string;
}

// --- Reports (block 7a) ---
export interface ShiftReportRow {
  shiftId: string;
  organizationId: string;
  branchId: string;
  openedByStaffUserId: string;
  closedByStaffUserId: string | null;
  state: string;
  startingCash: MoneyMinor;
  cashMovementsTotal: MoneyMinor;
  posCashPaymentsTotal: MoneyMinor;
  posRefundsTotal: MoneyMinor;
  billingCashImpactTotal: MoneyMinor;
  expectedCash: MoneyMinor;
  countedCash: MoneyMinor | null;
  difference: MoneyMinor | null;
  openedAtUtc: string;
  closedAtUtc: string | null;
}
export interface ShiftReport { rows: ShiftReportRow[]; limit: number; }

export interface SalesReportRow {
  posSaleId: string;
  organizationId: string;
  branchId: string;
  shiftId: string;
  createdByStaffUserId: string;
  state: string;
  total: MoneyMinor;
  paidAmount: MoneyMinor;
  refundAmount: MoneyMinor;
  grossCostOfGoods: MoneyMinor;
  refundedCostOfGoods: MoneyMinor;
  netCostOfGoods: MoneyMinor;
  lineCount: number;
  itemQuantity: number;
  createdAtUtc: string;
  paidAtUtc: string | null;
  refundedAtUtc: string | null;
  voidedAtUtc: string | null;
}
export interface SalesReport {
  rows: SalesReportRow[];
  limit: number;
  grossSalesTotal: MoneyMinor;
  refundsTotal: MoneyMinor;
  netSalesTotal: MoneyMinor;
  grossCostOfGoodsTotal: MoneyMinor;
  refundedCostOfGoodsTotal: MoneyMinor;
  netCostOfGoodsTotal: MoneyMinor;
}

export interface GameplayTimeReportRow {
  sessionId: string;
  organizationId: string;
  branchId: string;
  seatId: string;
  deviceId: string;
  createdByStaffUserId: string;
  playerKind: string;
  playerAccountId: string | null;
  state: string;
  durationSeconds: number;
  packageSeconds: number;
  bonusSeconds: number;
  gameplayRevenue: MoneyMinor;
  startedAtUtc: string | null;
  endedAtUtc: string | null;
  endsAtUtc: string | null;
}
export interface GameplayTimeReport {
  rows: GameplayTimeReportRow[];
  limit: number;
  totalDurationSeconds: number;
  totalPackageSeconds: number;
  totalBonusSeconds: number;
  gameplayRevenueTotal: MoneyMinor;
}

export interface CashOperationReportRow {
  operationId: string;
  organizationId: string;
  branchId: string;
  shiftId: string | null;
  createdByStaffUserId: string;
  sourceType: string;
  operationType: string;
  cashImpact: MoneyMinor;
  reason: string;
  createdAtUtc: string;
}
export interface CashOperationReport {
  rows: CashOperationReportRow[];
  limit: number;
  cashInTotal: MoneyMinor;
  cashOutTotal: MoneyMinor;
  netCashTotal: MoneyMinor;
}

export interface OperatorActionReportRow {
  actorStaffUserId: string | null;
  actorDisplayName: string;
  action: string;
  outcome: string;
  count: number;
  firstAtUtc: string;
  lastAtUtc: string;
}
export interface OperatorActionReport {
  rows: OperatorActionReportRow[];
  limit: number;
  totalActionCount: number;
}

// --- Audit (block 7a) ---
export interface AuditRecord {
  auditRecordId: string;
  organizationId: string;
  branchId: string | null;
  actorStaffUserId: string | null;
  action: string;
  targetType: string;
  targetId: string | null;
  outcome: string;
  sourceApp: string;
  detailsJson: string;
  createdAtUtc: string;
  actorPlatformAdminUserId: string | null;
}
export interface AuditSearchResult { records: AuditRecord[]; limit: number; }
export interface AuditSearchQuery {
  action?: string;
  outcome?: string;
  targetType?: string;
  fromUtc?: string;
  toUtc?: string;
  limit?: number;
}

// --- SaaS billing (SP3 Plan 4) ---
export const BillingInterval = {
  Monthly: 'monthly',
  Yearly: 'yearly'
} as const;

export const InvoiceStatus = {
  Issued: 'issued',
  Paid: 'paid',
  Void: 'void',
  Overdue: 'overdue'
} as const;

export const InvoiceKind = {
  Subscription: 'subscription',
  Proration: 'proration'
} as const;

export interface SubscriptionPlan {
  planCode: string;
  name: string;
  priceMinorUnits: number;
  currencyCode: string;
  billingInterval: string;
  maxBranches: number | null;
  maxDevicesPerBranch: number | null;
  maxConcurrentSessions: number | null;
  maxStaffUsersPerBranch: number | null;
  isActive: boolean;
  sortOrder: number;
}

export interface CreatePlanRequest {
  planCode: string;
  name: string;
  priceMinorUnits: number;
  currencyCode: string;
  billingInterval: string;
  maxBranches: number | null;
  maxDevicesPerBranch: number | null;
  maxConcurrentSessions: number | null;
  maxStaffUsersPerBranch: number | null;
  sortOrder: number;
}

export interface UpdatePlanRequest {
  name: string;
  priceMinorUnits: number;
  currencyCode: string;
  billingInterval: string;
  maxBranches: number | null;
  maxDevicesPerBranch: number | null;
  maxConcurrentSessions: number | null;
  maxStaffUsersPerBranch: number | null;
  isActive: boolean;
  sortOrder: number;
}

export interface TenantSubscription {
  tenantSubscriptionId: string;
  organizationId: string;
  planCode: string;
  status: string;
  currentPeriodStartUtc: string;
  currentPeriodEndUtc: string;
  nextInvoiceUtc: string | null;
  amountMinorUnits: number;
  currencyCode: string;
  billingInterval: string;
  cancelAtPeriodEnd: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface UpdateSubscriptionRequest {
  planCode: string | null;
  billingInterval: string | null;
  status: string | null;
  cancelAtPeriodEnd: boolean | null;
}

export interface Invoice {
  invoiceId: string;
  organizationId: string;
  number: number;
  kind: string;
  periodStartUtc: string;
  periodEndUtc: string;
  issuedAtUtc: string;
  dueAtUtc: string;
  amountMinorUnits: number;
  currencyCode: string;
  status: string;
  paidAtUtc: string | null;
  voidedAtUtc: string | null;
  voidReason: string | null;
  description: string;
}

export interface SubscriptionListItem {
  tenantSubscriptionId: string;
  organizationId: string;
  organizationName: string;
  organizationSlug: string;
  planCode: string;
  status: string;
  billingInterval: string;
  amountMinorUnits: number;
  currencyCode: string;
  currentPeriodEndUtc: string;
  nextInvoiceUtc: string | null;
  cancelAtPeriodEnd: boolean;
}

export interface InvoiceListItem {
  invoiceId: string;
  organizationId: string;
  organizationName: string;
  organizationSlug: string;
  number: number;
  kind: string;
  issuedAtUtc: string;
  dueAtUtc: string;
  amountMinorUnits: number;
  currencyCode: string;
  status: string;
}

export interface PlatformBillingMetrics {
  mrrMinorUnits: number;
  currencyCode: string;
  activeSubscriptions: number;
  outstandingMinorUnits: number;
  outstandingCount: number;
  overdueMinorUnits: number;
  overdueCount: number;
}
