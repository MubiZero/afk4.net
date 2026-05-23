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
