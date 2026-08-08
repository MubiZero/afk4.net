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

// First step of sign-in: password alone no longer issues a working session. The caller must
// present `challengeToken` to one of the /auth/2fa/* routes (setup or verify) to receive a real
// PlatformAdminSignInResponse. The token is short-lived (2 minutes) and opaque.
export interface PlatformAdminSignInChallengeResponse {
  challengeToken: string;
  expiresAtUtc: string;
  twoFactorConfigured: boolean;
}

export interface TwoFactorSetupResponse {
  secret: string;
  otpAuthUri: string;
}

export interface TwoFactorSetupConfirmResponse {
  session: PlatformAdminSignInResponse;
  // Shown to the admin exactly once — the server never returns them again after this response.
  recoveryCodes: string[];
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
  recentErrorCount?: number;
  expiringOwnerInviteCount?: number;
  rolloutAttentionCount?: number;
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
  contactEmail: string | null;
  contactPhone: string | null;
  legalDetails: string | null;
  updateChannel: string;
  pinnedClientVersion: string | null;
}

/** Mirrors `OrganizationFeatureStateDto`: state of one feature for a club plus WHAT decided
 * it — an override, the plan, or the default. The panel must show the decision, not just the
 * value: "off" alone can't answer "why doesn't this club have the shop". */
export interface OrganizationFeatureState {
  featureKey: string;
  name: string;
  description: string;
  isEnabled: boolean;
  decisionLevel: 'override' | 'plan' | 'default';
  overrideValue: boolean | null;
  overrideReason: string | null;
  overrideSetAtUtc: string | null;
  planValue: boolean | null;
  defaultValue: boolean;
}

export interface CreateBranchRequest {
  slug: string;
  name: string;
  city: string;
  preferredTimeZone: string | null;
}

export interface PlanLimitExceeded {
  code: string;
  limitName: string;
  limit: number;
  current: number;
  planCode: string;
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

export interface SupportAccessGrant {
  grantId: string;
  organizationId: string;
  reason: string;
  issuedAtUtc: string;
  expiresAtUtc: string;
  revokedAtUtc: string | null;
}

export interface SupportAccessGrantIssue {
  grant: SupportAccessGrant;
  ticket: string;
  adminUrl: string;
}

export interface PlatformUpdatePackage {
  updatePackageId: string;
  component: string;
  version: string;
  channel: string;
  artifactUri: string;
  sha256: string;
  signature: string;
  signatureAlgorithm: string;
  sizeBytes: number;
  state: string;
  releaseNotes: string;
  createdByPlatformAdminUserId: string;
  createdAtUtc: string;
  validatedByPlatformAdminUserId: string | null;
  validatedAtUtc: string | null;
  retiredAtUtc: string | null;
}

export interface PlatformUpdateRollout {
  updateRolloutId: string;
  updatePackageId: string;
  component: string;
  version: string;
  channel: string;
  state: string;
  targetKind: string;
  organizationIds: string[];
  branchIds: string[];
  deviceIds: string[];
  batchPercent: number;
  reason: string;
  createdByPlatformAdminUserId: string;
  createdAtUtc: string;
  startsAtUtc: string;
  completedAtUtc: string | null;
}

export interface CreatePlatformUpdatePackageRequest {
  component: string;
  version: string;
  channel: string;
  artifactUri: string;
  sha256: string;
  signature: string;
  signatureAlgorithm: string;
  sizeBytes: number;
  releaseNotes: string;
}

export interface CreatePlatformUpdateRolloutRequest {
  updatePackageId: string;
  channel: string;
  targetKind: string;
  organizationIds: string[];
  branchIds: string[];
  deviceIds: string[];
  batchPercent: number;
  startsAtUtc: string;
  reason: string;
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

export interface AuditRecord {
  auditRecordId: string;
  organizationId: string;
  branchId: string | null;
  actorStaffUserId: string | null;
  actorPlatformAdminUserId: string | null;
  action: string;
  targetType: string;
  targetId: string | null;
  outcome: string;
  sourceApp: string;
  detailsJson: string;
  createdAtUtc: string;
  amountMinorUnits: number | null;
}

export interface AuditSearchResult {
  records: AuditRecord[];
  limit: number;
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
  paymentGraceUntilUtc: string | null;
}

export interface UpdateSubscriptionRequest {
  planCode: string | null;
  billingInterval: string | null;
  status: string | null;
  cancelAtPeriodEnd: boolean | null;
  amountMinorUnits: number | null;
  currentPeriodEndUtc: string | null;
  paymentGraceUntilUtc: string | null;
  clearPaymentGrace: boolean | null;
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

/** One club that needs a money decision: either it owes money, or it settled but is still
 * suspended. Mirrors `DebtRowDto` — nothing auto-reactivates a suspended club, so this row is
 * the reminder that a human still owes the club a decision. */
export interface DebtRow {
  organizationId: string;
  organizationName: string;
  organizationSlug: string;
  organizationStatus: string;
  subscriptionStatus: string;
  outstandingMinorUnits: number;
  currencyCode: string;
  oldestOverdueInvoiceNumber: number | null;
  oldestOverdueInvoiceId: string | null;
  daysOverdue: number;
  dunningStage: number;
  graceUntilUtc: string | null;
  settledButSuspended: boolean;
}

/** One month of the analytics window. Mirrors `AnalyticsMonthDto`: year/month are numbers
 * because the month name depends on the viewer's language, which the server doesn't know. */
export interface AnalyticsMonth {
  year: number;
  month: number;
  recurringMinorUnits: number;
  oneOffMinorUnits: number;
  joined: number;
  left: number;
  payingAtMonthEnd: number;
}

export interface AnalyticsOverview {
  generatedAtUtc: string;
  currencyCode: string;
  months: AnalyticsMonth[];
  currentMrrMinorUnits: number;
  currentPayingClubs: number;
  averageRevenuePerClubMinorUnits: number;
  outstandingMinorUnits: number;
}

export interface Money {
  currencyCode: string;
  minorUnits: number;
}

/** One snapshotted day for a branch. Mirrors `BranchDynamicsDayDto`: `agentAlive === null`
 * means "no data" (usually our own outage), `false` means the club genuinely never checked
 * in — the two must never be collapsed into one "bad" bucket on screen. */
export interface BranchDynamicsDay {
  date: string;
  sessionCount: number;
  revenue: Money;
  shiftOpenedCount: number;
  agentAlive: boolean | null;
}

export interface BranchDynamics {
  organizationId: string;
  branchId: string;
  fromDate: string;
  toDate: string;
  totalRevenue: Money;
  totalSessionCount: number;
  daysWithoutAgent: number;
  daysWithUnknownAgent: number;
  /** Days in the window with no snapshot at all. Never backfilled with zeros. */
  missingDayCount: number;
  days: BranchDynamicsDay[];
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

export interface PlatformAdminListItem {
  platformAdminUserId: string;
  userName: string;
  displayName: string;
  role: string;
  isActive: boolean;
  twoFactorEnabled: boolean;
  lastSignInAtUtc: string | null;
  createdAtUtc: string;
}

export interface PlatformAdminInvitation {
  invitationId: string;
  role: string;
  status: string;
  expiresAtUtc: string;
  createdAtUtc: string;
}

export interface CreateInvitationResponse {
  invitation: PlatformAdminInvitation;
  code: string;
}

export type PulseAlertLevel = 'normal' | 'attention' | 'critical';

export interface PulseAlert {
  kind: string;
  level: PulseAlertLevel;
  /** The numeric figure behind an alert; meaning depends on `kind`: elapsed minutes since
   * last agent heartbeat for `agent_silent`, elapsed minutes since shift opened for
   * `shift_not_closed`, count of devices with a failed install for `rollout_failed`. Null
   * when there's no such figure (`payment_overdue` always; `agent_silent` when the device
   * has never reported; `rollout_failed` when manually flagged before any device report).
   * Never a pre-rendered string — build user-facing text from this via `pulseModel`. */
  detailValue: number | null;
}

export interface PulseClub {
  branchId: string;
  name: string;
  city: string;
  devicesOnline: number;
  devicesTotal: number;
  seatsOccupied: number;
  seatsTotal: number;
  shiftOpen: boolean;
  shiftOpenedAtUtc: string | null;
  lastHeartbeatAtUtc: string | null;
  alerts: PulseAlert[];
}

export interface PulseOrganization {
  organizationId: string;
  name: string;
  status: string;
  planCode: string;
  subscriptionStatus: string;
  alertLevel: PulseAlertLevel;
  outstandingMinorUnits: number;
  currencyCode: string;
  alerts: PulseAlert[];
  clubs: PulseClub[];
}

export interface PlatformPulse {
  generatedAtUtc: string;
  organizations: PulseOrganization[];
}

export interface JobHealth {
  jobName: string;
  lastRunAtUtc: string | null;
  lastSuccessAtUtc: string | null;
  lastOutcome: string | null;
  lastItemsProcessed: number | null;
  lastError: string | null;
  consecutiveFailures: number;
}

export interface QueueHealth {
  queueName: string;
  pendingCount: number;
  failedCount: number;
  stuckCount: number;
}

export type IncidentSeverity = 'warning' | 'critical';

export interface Incident {
  incidentId: string;
  kind: string;
  dedupKey: string;
  severity: IncidentSeverity;
  detailsJson: string | null;
  openedAtUtc: string;
  lastSeenAtUtc: string;
}

export interface HealthOverview {
  generatedAtUtc: string;
  jobs: JobHealth[];
  queues: QueueHealth[];
  openIncidents: Incident[];
}
