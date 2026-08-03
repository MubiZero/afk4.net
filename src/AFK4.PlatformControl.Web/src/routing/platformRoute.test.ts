import { describe, expect, it } from 'bun:test';
import { pathForPlatformRoute, resolvePlatformRoute } from './platformRoute';

describe('platformRoute', () => {
  it('resolves a canonical organization tab', () => {
    expect(resolvePlatformRoute('/admin/organizations/org-1', '?tab=access')).toEqual({
      kind: 'organization',
      organizationId: 'org-1',
      tab: 'access'
    });
  });

  it('falls back to clubs for an unknown organization tab', () => {
    expect(resolvePlatformRoute('/admin/organizations/org-1', '?tab=unknown')).toEqual({
      kind: 'organization',
      organizationId: 'org-1',
      tab: 'clubs'
    });
  });

  it('round-trips the fleet pulse view through the URL', () => {
    expect(pathForPlatformRoute({ kind: 'overview', view: 'now' })).toBe('/admin');
    expect(pathForPlatformRoute({ kind: 'overview', view: 'debt' })).toBe('/admin?view=debt');
    expect(resolvePlatformRoute('/admin', '?view=debt')).toEqual({ kind: 'overview', view: 'debt' });
    expect(resolvePlatformRoute('/admin', '?view=all')).toEqual({ kind: 'overview', view: 'all' });
  });

  it('falls back to the default view for an unknown or absent view param', () => {
    expect(resolvePlatformRoute('/admin')).toEqual({ kind: 'overview', view: 'now' });
    expect(resolvePlatformRoute('/admin', '?view=bogus')).toEqual({ kind: 'overview', view: 'now' });
  });

  it('redirects the retired organization-list bookmark to the fleet pulse screen', () => {
    expect(resolvePlatformRoute('/admin/organizations')).toEqual({ kind: 'overview', view: 'now' });
    expect(resolvePlatformRoute('/admin/organizations', '?view=all')).toEqual({ kind: 'overview', view: 'all' });
  });

  it('does not retain legacy organization routes', () => {
    expect(resolvePlatformRoute('/organizations/org-1')).toEqual({
      kind: 'notFound',
      path: '/organizations/org-1'
    });
  });

  it('round-trips global workspace tabs and audit filters', () => {
    expect(resolvePlatformRoute('/admin/money', '?tab=invoices')).toEqual({ kind: 'billing', tab: 'invoices' });
    expect(resolvePlatformRoute('/admin/updates', '?tab=rollouts')).toEqual({ kind: 'updates', tab: 'rollouts' });
    const audit = { kind: 'audit', organizationId: 'org-1', action: 'updates.rollout.create', outcome: 'succeeded', from: '2026-07-01', to: '2026-07-30' } as const;
    expect(resolvePlatformRoute('/admin/journal', pathForPlatformRoute(audit).split('?')[1])).toEqual(audit);
  });

  it('has one canonical result for every supported authenticated route', () => {
    expect([
      resolvePlatformRoute('/admin').kind,
      resolvePlatformRoute('/admin/organizations/new').kind,
      resolvePlatformRoute('/admin/organizations/org-1').kind,
      resolvePlatformRoute('/admin/money').kind,
      resolvePlatformRoute('/admin/updates').kind,
      resolvePlatformRoute('/admin/journal').kind,
      resolvePlatformRoute('/admin/settings').kind,
      resolvePlatformRoute('/admin/profile').kind
    ]).toEqual(['overview', 'organizationNew', 'organization', 'billing', 'updates', 'audit', 'settings', 'profile']);
    expect(resolvePlatformRoute('/organizations').kind).toBe('notFound');
    expect(resolvePlatformRoute('/club').kind).toBe('notFound');
    expect(resolvePlatformRoute('/auth/sign-in').kind).toBe('notFound');
  });
});
