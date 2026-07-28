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

export interface AcceptOwnerInviteRequest {
  code: string;
  userName: string;
  displayName: string;
  password: string;
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
