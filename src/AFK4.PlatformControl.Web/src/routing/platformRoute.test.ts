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
});
