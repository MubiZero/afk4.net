import { describe, expect, it } from 'bun:test';
import type { PlatformAdminSession } from './tokenStore';
import { can } from './platformAccess';

function session(permissions: string[], roles: string[] = ['platform_support']): PlatformAdminSession {
  return {
    platformAdminId: 'admin-1',
    userName: 'support',
    displayName: 'Support',
    roles,
    permissions,
    accessToken: 'access',
    accessTokenExpiresAtUtc: '2099-01-01T00:00:00Z',
    refreshToken: 'refresh',
    refreshTokenExpiresAtUtc: '2099-01-02T00:00:00Z'
  };
}

describe('platformAccess', () => {
  it('derives organization read access from the backend permission', () => {
    expect(can(session(['platform.organizations.view']), 'organizations.read')).toBe(true);
    expect(can(session([]), 'organizations.read')).toBe(false);
  });

  it('does not grant billing management from a role name alone', () => {
    expect(can(session([], ['platform_admin']), 'billing.manage')).toBe(false);
    expect(can(session(['platform.billing.plans.manage'], ['platform_support']), 'billing.manage')).toBe(true);
  });

  it('requires an update management permission for release controls', () => {
    expect(can(session(['platform.updates.view']), 'updates.manage')).toBe(false);
    expect(can(session(['platform.updates.rollouts.manage']), 'updates.manage')).toBe(true);
  });
});
