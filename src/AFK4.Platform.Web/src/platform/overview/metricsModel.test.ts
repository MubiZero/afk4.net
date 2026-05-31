import { describe, expect, it } from 'bun:test';
import { buildTenantMetrics } from './metricsModel';
import type { TenantSummary } from '@/api/types';

function tenant(p: Partial<TenantSummary>): TenantSummary {
  return {
    organizationId: 'o', slug: 's', name: 'Club', status: 'active', planCode: 'starter',
    subscriptionStatus: 'active', branchCount: 1, createdAtUtc: '2026-01-01T00:00:00Z',
    updatedAtUtc: '2026-01-01T00:00:00Z', ...p
  };
}

const NOW = '2026-05-31T00:00:00Z';

describe('buildTenantMetrics', () => {
  it('counts tenants by status, subscription and sums branches', () => {
    const vm = buildTenantMetrics([
      tenant({ organizationId: 'a', status: 'active', subscriptionStatus: 'active', branchCount: 2 }),
      tenant({ organizationId: 'b', status: 'suspended', subscriptionStatus: 'past_due', branchCount: 3 }),
      tenant({ organizationId: 'c', status: 'active', subscriptionStatus: 'trial', branchCount: 1 })
    ], NOW);
    expect(vm.kpis.totalTenants).toBe(3);
    expect(vm.kpis.activeTenants).toBe(2);
    expect(vm.kpis.suspendedTenants).toBe(1);
    expect(vm.kpis.trialTenants).toBe(1);
    expect(vm.kpis.totalBranches).toBe(6);
  });

  it('counts tenants created within the last 30 days', () => {
    const vm = buildTenantMetrics([
      tenant({ organizationId: 'old', createdAtUtc: '2026-01-01T00:00:00Z' }),
      tenant({ organizationId: 'new', createdAtUtc: '2026-05-20T00:00:00Z' })
    ], NOW);
    expect(vm.kpis.newTenants30d).toBe(1);
  });

  it('groups counts by plan in catalog order', () => {
    const vm = buildTenantMetrics([
      tenant({ organizationId: 'a', planCode: 'scale' }),
      tenant({ organizationId: 'b', planCode: 'starter' }),
      tenant({ organizationId: 'c', planCode: 'starter' })
    ], NOW);
    expect(vm.byPlan).toEqual([
      { planCode: 'starter', count: 2 },
      { planCode: 'growth', count: 0 },
      { planCode: 'scale', count: 1 }
    ]);
  });

  it('lists suspended and past-due tenants in the attention feed', () => {
    const vm = buildTenantMetrics([
      tenant({ organizationId: 'a', name: 'Alpha', status: 'active', subscriptionStatus: 'active' }),
      tenant({ organizationId: 'b', name: 'Beta', status: 'suspended', subscriptionStatus: 'active' }),
      tenant({ organizationId: 'c', name: 'Gamma', status: 'active', subscriptionStatus: 'past_due' })
    ], NOW);
    const ids = vm.attention.map(a => a.organizationId);
    expect(ids).toEqual(expect.arrayContaining(['b', 'c']));
    expect(ids).not.toContain('a');
    expect(vm.attention.find(a => a.organizationId === 'b')?.reason).toBe('suspended');
    expect(vm.attention.find(a => a.organizationId === 'c')?.reason).toBe('past_due');
  });
});
