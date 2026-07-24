import { describe, it, expect } from 'bun:test';
import type { OperatorAuthSession } from '../authClient';
import { allowedNetworkDestinations } from './networkNav';

function sessionWith(permissions: string[]): OperatorAuthSession {
  return { permissions } as OperatorAuthSession;
}

describe('networkNav', () => {
  it('owner (all org perms, including owner-only audit.organization.view) sees all four destinations', () => {
    const ids = allowedNetworkDestinations(
      sessionWith([
        'branches.view',
        'billing.subscription.view',
        'devices.install',
        'audit.view',
        'audit.organization.view'
      ])
    ).map((d) => d.id);
    expect(ids).toEqual(['branches', 'billing', 'install', 'journal']);
  });

  it('a session with only audit.view (branch-scoped, not owner-only audit.organization.view) no longer sees journal', () => {
    const ids = allowedNetworkDestinations(sessionWith(['audit.view'])).map((d) => d.id);
    expect(ids).toEqual([]);
  });

  it('a session with no org perms sees nothing', () => {
    expect(allowedNetworkDestinations(sessionWith(['sessions.start']))).toEqual([]);
  });
});
