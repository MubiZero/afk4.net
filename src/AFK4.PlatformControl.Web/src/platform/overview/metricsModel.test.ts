import { describe, expect, it } from 'bun:test';
import { buildOrganizationMetrics } from './metricsModel';
import type { OrganizationSummary } from '@/api/types';

function organization(p: Partial<OrganizationSummary>): OrganizationSummary {
  return {
    organizationId: 'o', slug: 's', name: 'Club', status: 'active', planCode: 'starter',
    subscriptionStatus: 'active', branchCount: 1, createdAtUtc: '2026-01-01T00:00:00Z',
    updatedAtUtc: '2026-01-01T00:00:00Z', ...p
  };
}

const NOW = '2026-05-31T00:00:00Z';

describe('buildOrganizationMetrics', () => {
  it('counts organizations by status, subscription and sums branches', () => {
    const vm = buildOrganizationMetrics([
      organization({ organizationId: 'a', status: 'active', subscriptionStatus: 'active', branchCount: 2 }),
      organization({ organizationId: 'b', status: 'suspended', subscriptionStatus: 'past_due', branchCount: 3 }),
      organization({ organizationId: 'c', status: 'active', subscriptionStatus: 'trial', branchCount: 1 })
    ], NOW);
    expect(vm.kpis.totalOrganizations).toBe(3);
    expect(vm.kpis.activeOrganizations).toBe(2);
    expect(vm.kpis.suspendedOrganizations).toBe(1);
    expect(vm.kpis.trialOrganizations).toBe(1);
    expect(vm.kpis.totalBranches).toBe(6);
  });

  it('counts organizations created within the last 30 days', () => {
    const vm = buildOrganizationMetrics([
      organization({ organizationId: 'old', createdAtUtc: '2026-01-01T00:00:00Z' }),
      organization({ organizationId: 'new', createdAtUtc: '2026-05-20T00:00:00Z' })
    ], NOW);
    expect(vm.kpis.newOrganizations30d).toBe(1);
  });

  it('groups counts by plan in catalog order', () => {
    const vm = buildOrganizationMetrics([
      organization({ organizationId: 'a', planCode: 'scale' }),
      organization({ organizationId: 'b', planCode: 'starter' }),
      organization({ organizationId: 'c', planCode: 'starter' })
    ], NOW);
    expect(vm.byPlan).toEqual([
      { planCode: 'starter', count: 2 },
      { planCode: 'growth', count: 0 },
      { planCode: 'scale', count: 1 }
    ]);
  });

  it('lists suspended and past-due organizations in the attention feed', () => {
    const vm = buildOrganizationMetrics([
      organization({ organizationId: 'a', name: 'Alpha', status: 'active', subscriptionStatus: 'active' }),
      organization({ organizationId: 'b', name: 'Beta', status: 'suspended', subscriptionStatus: 'active' }),
      organization({ organizationId: 'c', name: 'Gamma', status: 'active', subscriptionStatus: 'past_due' })
    ], NOW);
    const ids = vm.attention.map(a => a.organizationId);
    expect(ids).toEqual(expect.arrayContaining(['b', 'c']));
    expect(ids).not.toContain('a');
    expect(vm.attention.find(a => a.organizationId === 'b')?.reason).toBe('suspended');
    expect(vm.attention.find(a => a.organizationId === 'c')?.reason).toBe('past_due');
  });

  it('lists health errors, expiring owner invites and rollout attention', () => {
    const vm = buildOrganizationMetrics([organization({
      organizationId: 'ops', name: 'Operational Club', recentErrorCount: 2,
      expiringOwnerInviteCount: 1, rolloutAttentionCount: 1
    })], NOW);

    expect(vm.attention.map(item => item.reason)).toEqual([
      'health_errors', 'expiring_invite', 'rollout_attention'
    ]);
  });
});
