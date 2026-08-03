import { describe, expect, it } from 'bun:test';
import { buildOrganizationRows, INVITE_STATUS_VARIANT, INVITE_STATUS_LABEL, type OrganizationsFilter } from './organizationsModel';
import type { OrganizationSummary } from '@/api/types';

function organization(over: Partial<OrganizationSummary>): OrganizationSummary {
  return {
    organizationId: 'o1', slug: 'acme', name: 'Acme', status: 'active',
    planCode: 'starter', subscriptionStatus: 'active', branchCount: 1,
    createdAtUtc: '2026-01-01T00:00:00Z', updatedAtUtc: '2026-01-01T00:00:00Z', ...over
  };
}
const ALL: OrganizationsFilter = { query: '', status: 'all', plan: 'all', sort: 'attention' };

describe('buildOrganizationRows', () => {
  it('returns all organizations sorted by updatedAtUtc descending', () => {
    const rows = buildOrganizationRows(
      [organization({ organizationId: 'a', updatedAtUtc: '2026-01-01T00:00:00Z' }),
       organization({ organizationId: 'b', updatedAtUtc: '2026-03-01T00:00:00Z' })],
      ALL
    );
    expect(rows.map(r => r.organizationId)).toEqual(['b', 'a']);
  });

  it('filters by query over name and slug (case-insensitive)', () => {
    const rows = buildOrganizationRows(
      [organization({ organizationId: 'a', name: 'Globex', slug: 'globex' }),
       organization({ organizationId: 'b', name: 'Acme', slug: 'acme-key' })],
      { ...ALL, query: 'ACME' }
    );
    expect(rows.map(r => r.organizationId)).toEqual(['b']);
  });

  it('filters by status and plan', () => {
    const rows = buildOrganizationRows(
      [organization({ organizationId: 'a', status: 'suspended', planCode: 'scale' }),
       organization({ organizationId: 'b', status: 'active', planCode: 'scale' })],
      { ...ALL, status: 'active', plan: 'scale' }
    );
    expect(rows.map(r => r.organizationId)).toEqual(['b']);
  });

  it('sorts organizations by name when requested', () => {
    const rows = buildOrganizationRows([
      organization({ organizationId: 'z', name: 'Zeta' }),
      organization({ organizationId: 'a', name: 'Alpha' })
    ], { ...ALL, sort: 'name' });
    expect(rows.map(row => row.organizationId)).toEqual(['a', 'z']);
  });

  it('maps every invite status to a variant and label', () => {
    for (const s of ['pending', 'accepted', 'revoked', 'expired']) {
      expect(INVITE_STATUS_VARIANT[s]).toBeTruthy();
      expect(INVITE_STATUS_LABEL[s]).toContain('platform.organization.invites.status.');
    }
  });
});
