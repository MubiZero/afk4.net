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
        'organization.branches.view',
        'organization.billing.subscription.view',
        'organization.ads.view',
        'organization.devices.install',
        'organization.audit.view',
        'organization.audit.organization.view'
      ])
    ).map((d) => d.id);
    expect(ids).toEqual(['branches', 'billing', 'ads', 'install', 'journal']);
  });

  it('a session with only audit.view (branch-scoped, not owner-only audit.organization.view) no longer sees journal', () => {
    const ids = allowedNetworkDestinations(sessionWith(['organization.audit.view'])).map((d) => d.id);
    expect(ids).toEqual([]);
  });

  // Управляющий отвечает за рекламу на ПК перед проверяющим (ст. 25), но счета ему видеть незачем.
  it('a manager sees the ads without the subscription', () => {
    const ids = allowedNetworkDestinations(sessionWith(['organization.ads.view'])).map((d) => d.id);
    expect(ids).toEqual(['ads']);
  });

  it('a session with no org perms sees nothing', () => {
    expect(allowedNetworkDestinations(sessionWith(['organization.sessions.start']))).toEqual([]);
  });
});
