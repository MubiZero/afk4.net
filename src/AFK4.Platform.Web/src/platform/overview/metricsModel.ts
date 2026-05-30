import type { TenantSummary } from '@/api/types';

export type AttentionReason = 'suspended' | 'past_due';
export interface AttentionRow { organizationId: string; name: string; reason: AttentionReason; }
export interface PlanCount { planCode: string; count: number; }

export interface PlatformMetricsViewModel {
  kpis: {
    totalTenants: number;
    activeTenants: number;
    suspendedTenants: number;
    trialTenants: number;
    totalBranches: number;
    newTenants30d: number;
  };
  byPlan: PlanCount[];
  attention: AttentionRow[];
}

const PLAN_ORDER = ['starter', 'growth', 'scale'] as const;
const THIRTY_DAYS_MS = 30 * 24 * 60 * 60 * 1000;

export function buildTenantMetrics(tenants: TenantSummary[], nowIso: string): PlatformMetricsViewModel {
  const nowMs = Date.parse(nowIso);

  let activeTenants = 0;
  let suspendedTenants = 0;
  let trialTenants = 0;
  let totalBranches = 0;
  let newTenants30d = 0;
  const planCounts = new Map<string, number>();
  const attention: AttentionRow[] = [];

  for (const t of tenants) {
    if (t.status === 'active') activeTenants += 1;
    if (t.status === 'suspended') suspendedTenants += 1;
    if (t.subscriptionStatus === 'trial') trialTenants += 1;
    totalBranches += t.branchCount;

    const createdMs = Date.parse(t.createdAtUtc);
    if (!Number.isNaN(createdMs) && nowMs - createdMs <= THIRTY_DAYS_MS) newTenants30d += 1;

    planCounts.set(t.planCode, (planCounts.get(t.planCode) ?? 0) + 1);

    if (t.status === 'suspended') {
      attention.push({ organizationId: t.organizationId, name: t.name, reason: 'suspended' });
    } else if (t.subscriptionStatus === 'past_due') {
      attention.push({ organizationId: t.organizationId, name: t.name, reason: 'past_due' });
    }
  }

  const byPlan: PlanCount[] = PLAN_ORDER.map(planCode => ({ planCode, count: planCounts.get(planCode) ?? 0 }));
  for (const [planCode, count] of planCounts) {
    if (!PLAN_ORDER.includes(planCode as (typeof PLAN_ORDER)[number])) byPlan.push({ planCode, count });
  }

  return {
    kpis: { totalTenants: tenants.length, activeTenants, suspendedTenants, trialTenants, totalBranches, newTenants30d },
    byPlan,
    attention
  };
}
