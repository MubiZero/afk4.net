import { describe, expect, it } from 'bun:test';
import { canDisable, canChangeRole } from './adminsModel';

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
