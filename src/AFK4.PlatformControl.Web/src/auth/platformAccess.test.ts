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

  // Раздел «Задолженность» (DebtSection) вызывает четыре разных бэкенд-права под четырьмя
  // разными кнопками — эти капабилити не должны совпадать с более широкими группами
  // (organizations.manage / billing.manage / support.manage), иначе право на одно действие
  // покажет активной кнопку для другого.
  it('scopes organizations.status.manage to exactly the status-update permission', () => {
    expect(can(session(['platform.organizations.status.update']), 'organizations.status.manage')).toBe(true);
    expect(can(session(['platform.organizations.create']), 'organizations.status.manage')).toBe(false);
  });

  it('scopes organizations.support_notes.manage to exactly the support-notes permission', () => {
    expect(can(session(['platform.organizations.support_notes.manage']), 'organizations.support_notes.manage')).toBe(true);
    expect(can(session(['platform.support.access']), 'organizations.support_notes.manage')).toBe(false);
  });

  it('scopes billing.invoices.manage to exactly the invoices permission', () => {
    expect(can(session(['platform.billing.invoices.manage']), 'billing.invoices.manage')).toBe(true);
    expect(can(session(['platform.billing.plans.manage']), 'billing.invoices.manage')).toBe(false);
  });

  it('scopes billing.subscriptions.manage to exactly the subscriptions permission', () => {
    expect(can(session(['platform.billing.subscriptions.manage']), 'billing.subscriptions.manage')).toBe(true);
    expect(can(session(['platform.billing.invoices.manage']), 'billing.subscriptions.manage')).toBe(false);
  });

  // Вкладка «Фичи» держит на виду переключатель, поле причины и «Применить»/«Вернуть как у
  // тарифа» под этим капабилити — без него поддержка видит вкладку целиком (чтение), но не эти
  // рычаги, потому что сервер режет мутацию ровно по этому праву и молча вернёт 403.
  it('scopes organizations.features.manage to exactly the feature-override permission', () => {
    expect(can(session(['platform.organizations.features.manage']), 'organizations.features.manage')).toBe(true);
    expect(can(session(['platform.organizations.manage']), 'organizations.features.manage')).toBe(false);
    expect(can(session([]), 'organizations.features.manage')).toBe(false);
  });
});
