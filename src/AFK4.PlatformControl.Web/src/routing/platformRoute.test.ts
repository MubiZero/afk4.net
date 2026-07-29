import { describe, expect, it } from 'bun:test';
import { pathForPlatformRoute, resolvePlatformRoute } from './platformRoute';

describe('platformRoute', () => {
  it('resolves a canonical organization tab', () => {
    expect(resolvePlatformRoute('/admin/organizations/org-1', '?tab=support')).toEqual({
      kind: 'organization',
      organizationId: 'org-1',
      tab: 'support'
    });
  });

  it('falls back to summary for an unknown organization tab', () => {
    expect(resolvePlatformRoute('/admin/organizations/org-1', '?tab=unknown')).toEqual({
      kind: 'organization',
      organizationId: 'org-1',
      tab: 'summary'
    });
  });

  it('round-trips organization-list state through the URL', () => {
    const route = {
      kind: 'organizations' as const,
      query: 'samarkand',
      status: 'active',
      plan: 'growth',
      sort: 'name'
    };

    const path = pathForPlatformRoute(route);

    expect(path).toBe('/admin/organizations?q=samarkand&status=active&plan=growth&sort=name');
    expect(resolvePlatformRoute('/admin/organizations', path.slice(path.indexOf('?')))).toEqual(route);
  });

  it('does not retain legacy organization routes', () => {
    expect(resolvePlatformRoute('/organizations/org-1')).toEqual({
      kind: 'notFound',
      path: '/organizations/org-1'
    });
  });

  it('round-trips global workspace tabs and audit filters', () => {
    expect(resolvePlatformRoute('/admin/billing', '?tab=invoices')).toEqual({ kind: 'billing', tab: 'invoices' });
    expect(resolvePlatformRoute('/admin/updates', '?tab=rollouts')).toEqual({ kind: 'updates', tab: 'rollouts' });
    const audit = { kind: 'audit', organizationId: 'org-1', action: 'updates.rollout.create', outcome: 'succeeded', from: '2026-07-01', to: '2026-07-30' } as const;
    expect(resolvePlatformRoute('/admin/audit', pathForPlatformRoute(audit).split('?')[1])).toEqual(audit);
  });
});
