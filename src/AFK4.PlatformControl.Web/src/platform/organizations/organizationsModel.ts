import type { OrganizationSummary } from '@/api/types';
import type { MessageKey } from '@/i18n/messages';
import type { BadgeVariant } from '@/components/ui/badge';

export interface OrganizationRow {
  organizationId: string;
  name: string;
  slug: string;
  status: string;
  planCode: string;
  subscriptionStatus: string;
  branchCount: number;
  updatedAtUtc: string;
}

export interface OrganizationsFilter {
  query: string;
  status: string; // 'all' | OrganizationStatus value
  plan: string;   // 'all' | plan code
}

export function buildOrganizationRows(organizations: OrganizationSummary[], filter: OrganizationsFilter): OrganizationRow[] {
  const q = filter.query.trim().toLowerCase();
  return organizations
    .filter(t => filter.status === 'all' || t.status === filter.status)
    .filter(t => filter.plan === 'all' || t.planCode === filter.plan)
    .filter(t => q === '' || t.name.toLowerCase().includes(q) || t.slug.toLowerCase().includes(q))
    .map(t => ({
      organizationId: t.organizationId,
      name: t.name,
      slug: t.slug,
      status: t.status,
      planCode: t.planCode,
      subscriptionStatus: t.subscriptionStatus,
      branchCount: t.branchCount,
      updatedAtUtc: t.updatedAtUtc
    }))
    .sort((a, b) => b.updatedAtUtc.localeCompare(a.updatedAtUtc));
}

export const STATUS_VARIANT: Record<string, BadgeVariant> = {
  active: 'success',
  suspended: 'destructive',
  deletion_pending: 'outline'
};
export const STATUS_LABEL: Record<string, MessageKey> = {
  active: 'platform.organization.status.active',
  suspended: 'platform.organization.status.suspended',
  deletion_pending: 'platform.organization.status.deletionPending'
};

export const SUBSCRIPTION_VARIANT: Record<string, BadgeVariant> = {
  active: 'success',
  trial: 'secondary',
  past_due: 'destructive',
  cancelled: 'outline'
};
export const SUBSCRIPTION_LABEL: Record<string, MessageKey> = {
  trial: 'platform.organization.subscription.trial',
  active: 'platform.organization.subscription.active',
  past_due: 'platform.organization.subscription.pastDue',
  cancelled: 'platform.organization.subscription.cancelled'
};

export const PLAN_LABEL: Record<string, MessageKey> = {
  starter: 'platform.plan.starter',
  growth: 'platform.plan.growth',
  scale: 'platform.plan.scale'
};

export const INVITE_STATUS_VARIANT: Record<string, BadgeVariant> = {
  pending: 'secondary',
  accepted: 'success',
  revoked: 'outline',
  expired: 'outline'
};
export const INVITE_STATUS_LABEL: Record<string, MessageKey> = {
  pending: 'platform.organization.invites.status.pending',
  accepted: 'platform.organization.invites.status.accepted',
  revoked: 'platform.organization.invites.status.revoked',
  expired: 'platform.organization.invites.status.expired'
};

export const STATUS_OPTIONS = ['active', 'suspended', 'deletion_pending'] as const;
export const PLAN_OPTIONS = ['starter', 'growth', 'scale'] as const;
export const SUBSCRIPTION_OPTIONS = ['trial', 'active', 'past_due', 'cancelled'] as const;
