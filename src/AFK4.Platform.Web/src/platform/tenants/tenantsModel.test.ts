import { describe, expect, it } from 'bun:test';
import { buildTenantRows, INVITE_STATUS_VARIANT, INVITE_STATUS_LABEL, type TenantsFilter } from './tenantsModel';
import type { TenantSummary } from '@/api/types';

function tenant(over: Partial<TenantSummary>): TenantSummary {
  return {
    organizationId: 'o1', slug: 'acme', name: 'Acme', status: 'active',
    planCode: 'starter', subscriptionStatus: 'active', branchCount: 1,
    createdAtUtc: '2026-01-01T00:00:00Z', updatedAtUtc: '2026-01-01T00:00:00Z', ...over
  };
}
const ALL: TenantsFilter = { query: '', status: 'all', plan: 'all' };

describe('buildTenantRows', () => {
  it('returns all tenants sorted by updatedAtUtc descending', () => {
    const rows = buildTenantRows(
      [tenant({ organizationId: 'a', updatedAtUtc: '2026-01-01T00:00:00Z' }),
       tenant({ organizationId: 'b', updatedAtUtc: '2026-03-01T00:00:00Z' })],
      ALL
    );
    expect(rows.map(r => r.organizationId)).toEqual(['b', 'a']);
  });

  it('filters by query over name and slug (case-insensitive)', () => {
    const rows = buildTenantRows(
      [tenant({ organizationId: 'a', name: 'Globex', slug: 'globex' }),
       tenant({ organizationId: 'b', name: 'Acme', slug: 'acme-key' })],
      { ...ALL, query: 'ACME' }
    );
    expect(rows.map(r => r.organizationId)).toEqual(['b']);
  });

  it('filters by status and plan', () => {
    const rows = buildTenantRows(
      [tenant({ organizationId: 'a', status: 'suspended', planCode: 'scale' }),
       tenant({ organizationId: 'b', status: 'active', planCode: 'scale' })],
      { ...ALL, status: 'active', plan: 'scale' }
    );
    expect(rows.map(r => r.organizationId)).toEqual(['b']);
  });

  it('maps every invite status to a variant and label', () => {
    for (const s of ['pending', 'accepted', 'revoked', 'expired']) {
      expect(INVITE_STATUS_VARIANT[s]).toBeTruthy();
      expect(INVITE_STATUS_LABEL[s]).toContain('platform.tenant.invites.status.');
    }
  });
});
