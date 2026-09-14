import { describe, expect, it } from 'bun:test';
import { canDisable, canChangeRole, platformAdminActivationUrl } from './adminsModel';

const admin = (id: string, role: string, isActive = true) => ({
  platformAdminUserId: id, userName: id, displayName: id, role, isActive,
  twoFactorEnabled: true, lastSignInAtUtc: null, createdAtUtc: '2026-08-01T00:00:00Z'
});

describe('adminsModel', () => {
  it('не даёт отключить самого себя', () => {
    const items = [admin('me', 'platform_admin'), admin('other', 'platform_admin')];
    expect(canDisable(items[0], 'me', items)).toBe(false);
  });

  it('не даёт отключить последнего активного полного админа', () => {
    const items = [admin('me', 'platform_admin'), admin('support', 'platform_support')];
    expect(canDisable(items[0], 'other', items)).toBe(false);
  });

  it('разрешает отключить поддержку', () => {
    const items = [admin('me', 'platform_admin'), admin('support', 'platform_support')];
    expect(canDisable(items[1], 'me', items)).toBe(true);
  });

  it('не даёт понизить самого себя', () => {
    const items = [admin('me', 'platform_admin'), admin('other', 'platform_admin')];
    expect(canChangeRole(items[0], 'me', items)).toBe(false);
  });
});

// Ссылка обязана нести вид приглашения: по самому коду его не определить, а без него активация
// уходит на путь владельца и получает «код не подошёл».
describe('platformAdminActivationUrl', () => {
  it('carries both the kind and the code', () => {
    expect(platformAdminActivationUrl('https://panel.afk4.net', 'abc123'))
      .toBe('https://panel.afk4.net/account-activation?kind=platform-admin&code=abc123');
  });

  it('escapes a code that would otherwise break the query', () => {
    expect(platformAdminActivationUrl('https://panel.afk4.net', 'a+b&c=d'))
      .toBe('https://panel.afk4.net/account-activation?kind=platform-admin&code=a%2Bb%26c%3Dd');
  });

  it('does not double the slash when the origin carries one', () => {
    expect(platformAdminActivationUrl('https://panel.afk4.net/', 'abc123'))
      .toBe('https://panel.afk4.net/account-activation?kind=platform-admin&code=abc123');
  });
});
