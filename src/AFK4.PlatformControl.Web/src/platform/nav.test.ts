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
    ])).map(item => item.key);
    expect(keys).toContain('clubs');
    expect(keys).toContain('money');
    expect(keys).toContain('journal');
    expect(keys).not.toContain('updates');
    expect(keys).not.toContain('settings');
    expect(keys).not.toContain('games');
    expect(keys).not.toContain('ads');
  });

  it('shows the game catalog only to those who may edit it', () => {
    const keys = buildPlatformNav(session(['platform.games.manage'])).map(item => item.key);
    expect(keys).toEqual(['games']);
    expect(buildPlatformNav(session(['platform.games.manage']))[0].path).toBe('/admin/games');
  });

  it('shows platform ads only to those who may run them', () => {
    const nav = buildPlatformNav(session(['platform.ads.manage']));
    expect(nav.map(item => item.key)).toEqual(['ads']);
    expect(nav[0].path).toBe('/admin/ads');
  });

  // Профиль переехал в меню аккаунта в подвале рейла: отдельного пункта навигации быть не должно,
  // иначе вернётся мёртвый экран со списком собственных прав.
  it('has no profile destination', () => {
    const keys = buildPlatformNav(session(['platform.organizations.view'])).map(item => item.key);
    expect(keys).not.toContain('profile');
  });

  it('every item has an /admin path, a nav. label key and an icon', () => {
    for (const item of buildPlatformNav(session(['platform.organizations.view']))) {
      expect(item.path.startsWith('/admin')).toBe(true);
      expect(item.labelKey.startsWith('nav.')).toBe(true);
      expect(item.icon).toBeDefined();
    }
  });
});
