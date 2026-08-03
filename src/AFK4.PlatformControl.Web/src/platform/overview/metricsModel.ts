import type { OrganizationSummary } from '@/api/types';

export type AttentionReason = 'suspended' | 'past_due' | 'health_errors' | 'expiring_invite' | 'rollout_attention';
export interface AttentionRow { organizationId: string; name: string; reason: AttentionReason; }
export interface PlanCount { planCode: string; count: number; }

export interface PlatformMetricsViewModel {
  kpis: {
    totalOrganizations: number;
    activeOrganizations: number;
    suspendedOrganizations: number;
    trialOrganizations: number;
    totalBranches: number;
    newOrganizations30d: number;
  };
  byPlan: PlanCount[];
  attention: AttentionRow[];
}

const PLAN_ORDER = ['starter', 'growth', 'scale'] as const;
const THIRTY_DAYS_MS = 30 * 24 * 60 * 60 * 1000;

export function buildOrganizationMetrics(organizations: OrganizationSummary[], nowIso: string): PlatformMetricsViewModel {
  const nowMs = Date.parse(nowIso);

  let activeOrganizations = 0;
  let suspendedOrganizations = 0;
  let trialOrganizations = 0;
  let totalBranches = 0;
  let newOrganizations30d = 0;
  const planCounts = new Map<string, number>();
  const attention: AttentionRow[] = [];

  for (const t of organizations) {
    if (t.status === 'active') activeOrganizations += 1;
    if (t.status === 'suspended') suspendedOrganizations += 1;
    if (t.subscriptionStatus === 'trial') trialOrganizations += 1;
    totalBranches += t.branchCount;

    const createdMs = Date.parse(t.createdAtUtc);
    if (!Number.isNaN(createdMs) && nowMs - createdMs <= THIRTY_DAYS_MS) newOrganizations30d += 1;

    planCounts.set(t.planCode, (planCounts.get(t.planCode) ?? 0) + 1);

    if (t.status === 'suspended') {
      attention.push({ organizationId: t.organizationId, name: t.name, reason: 'suspended' });
    } else if (t.subscriptionStatus === 'past_due') {
      attention.push({ organizationId: t.organizationId, name: t.name, reason: 'past_due' });
    }
    if ((t.recentErrorCount ?? 0) > 0) attention.push({ organizationId: t.organizationId, name: t.name, reason: 'health_errors' });
    if ((t.expiringOwnerInviteCount ?? 0) > 0) attention.push({ organizationId: t.organizationId, name: t.name, reason: 'expiring_invite' });
    if ((t.rolloutAttentionCount ?? 0) > 0) attention.push({ organizationId: t.organizationId, name: t.name, reason: 'rollout_attention' });
  }

  const byPlan: PlanCount[] = PLAN_ORDER.map(planCode => ({ planCode, count: planCounts.get(planCode) ?? 0 }));
  for (const [planCode, count] of planCounts) {
    if (!PLAN_ORDER.includes(planCode as (typeof PLAN_ORDER)[number])) byPlan.push({ planCode, count });
  }

  return {
    kpis: { totalOrganizations: organizations.length, activeOrganizations, suspendedOrganizations, trialOrganizations, totalBranches, newOrganizations30d },
    byPlan,
    attention
  };
}
