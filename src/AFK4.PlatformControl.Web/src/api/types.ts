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

export interface AccountActivationRequest {
  code: string;
  userName: string;
  displayName: string;
  password: string;
}

export interface OrganizationSummary {
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

export interface OrganizationBranch {
  branchId: string;
  slug: string;
  name: string;
  city: string;
  createdAtUtc: string;
}

export interface OrganizationLimits {
  maxBranches: number | null;
  maxDevicesPerBranch: number | null;
  maxConcurrentSessions: number | null;
  maxStaffUsersPerBranch: number | null;
}

export interface OrganizationDetail {
  organizationId: string;
  slug: string;
  name: string;
  status: string;
  statusReason: string | null;
  statusChangedAtUtc: string | null;
  planCode: string;
  subscriptionStatus: string;
  limits: OrganizationLimits;
  branches: OrganizationBranch[];
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CreateOrganizationRequest {
  organizationSlug: string;
  organizationName: string;
  branchSlug: string;
  branchName: string;
  branchCity: string;
  planCode: string;
  subscriptionStatus: string;
  limits: OrganizationLimits | null;
  ownerUserName: string | null;
  ownerDisplayName: string | null;
  organizationOwnerInviteLifetime: string | null;
}

export interface OrganizationOwnerInvite {
  organizationOwnerInviteId: string;
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

export interface OrganizationOwnerInviteSummary {
  organizationOwnerInviteId: string;
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

export interface CreateOrganizationResponse {
  organization: OrganizationDetail;
  organizationOwnerInvite: OrganizationOwnerInvite;
}

export interface OrganizationSupportNote {
  organizationSupportNoteId: string;
  organizationId: string;
  authorPlatformAdminId: string;
  authorDisplayName: string;
  body: string;
  createdAtUtc: string;
}

export interface OrganizationHealthError {
  createdAtUtc: string;
  source: string;
  action: string;
  outcome: string;
  message: string | null;
}

export interface OrganizationHealth {
  organizationId: string;
  status: string;
  branchCount: number;
  deviceCount: number;
  activeStaffUserCount: number;
  latestStaffSignInAtUtc: string | null;
  latestMigration: string | null;
  recentErrorCount: number;
  recentErrors: OrganizationHealthError[];
}

export const OrganizationStatus = {
  Active: 'active',
  Suspended: 'suspended',
  DeletionPending: 'deletion_pending'
} as const;
export type OrganizationStatusValue = (typeof OrganizationStatus)[keyof typeof OrganizationStatus];

export const OrganizationPlanCode = {
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

export interface OrganizationSubscription {
  organizationSubscriptionId: string;
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
  organizationSubscriptionId: string;
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
