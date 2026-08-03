import { describe, expect, it } from 'bun:test';
import type { PlatformAdminSession } from '@/auth/tokenStore';
import { buildPlatformNav } from './nav';

function session(permissions: string[]): PlatformAdminSession {
  return {
    platformAdminId: 'admin-1', userName: 'admin', displayName: 'Admin', roles: [], permissions,
    accessToken: 'access', accessTokenExpiresAtUtc: '2099-01-01T00:00:00Z',
    refreshToken: 'refresh', refreshTokenExpiresAtUtc: '2099-01-02T00:00:00Z'
  };
}

describe('platform nav', () => {
  it('exposes only destinations allowed by backend permissions', () => {
    const keys = buildPlatformNav(session([
      'platform.organizations.view',
      'platform.billing.view',
      'platform.audit.view'
    ])).flatMap(g => g.items.map(i => i.key));
    expect(keys).toContain('clubs');
    expect(keys).toContain('money');
    expect(keys).toContain('journal');
    expect(keys).toContain('profile');
    expect(keys).not.toContain('updates');
    expect(keys).not.toContain('settings');
    expect(keys).not.toContain('overview');
    expect(keys).not.toContain('organizations');
  });

  it('marks every platform nav item live', () => {
    const items = buildPlatformNav(session(['platform.organizations.view'])).flatMap(g => g.items);
    expect(items.length).toBeGreaterThan(0);
    expect(items.every(item => item.soon === false)).toBe(true);
  });

  it('every item has an /admin path and a nav. label key', () => {
    for (const g of buildPlatformNav(session(['platform.organizations.view']))) for (const i of g.items) {
      expect(i.path.startsWith('/admin')).toBe(true);
      expect(i.labelKey.startsWith('nav.')).toBe(true);
    }
  });
});
