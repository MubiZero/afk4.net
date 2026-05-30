import { describe, expect, it } from 'vitest';
import { clubNav, roleFromPermissions, visibleNav } from './nav';

describe('club nav', () => {
  it('owner sees every item', () => {
    const groups = visibleNav('owner');
    const keys = groups.flatMap(g => g.items.map(i => i.key));
    expect(keys).toContain('settings');
    expect(keys).toContain('install');
    expect(keys).toContain('billing');
    expect(keys).toContain('profile');
  });

  it('manager does not see owner-only items', () => {
    const keys = visibleNav('manager').flatMap(g => g.items.map(i => i.key));
    expect(keys).toContain('overview');
    expect(keys).toContain('venue');
    expect(keys).not.toContain('settings');
    expect(keys).not.toContain('install');
    expect(keys).not.toContain('billing');
    expect(keys).not.toContain('profile');
  });

  it('derives owner role from the owner permission', () => {
    expect(roleFromPermissions(['identity.branch_staff.manage'])).toBe('owner');
    expect(roleFromPermissions(['sessions.start'])).toBe('manager');
  });

  it('config is internally consistent (every item has a path and label key)', () => {
    for (const g of clubNav) for (const i of g.items) {
      expect(i.path.startsWith('/club')).toBe(true);
      expect(i.labelKey.startsWith('nav.')).toBe(true);
    }
  });
});

it('exposes Зал и ПК as an active (non-soon) item', () => {
  const venue = clubNav.flatMap(g => g.items).find(i => i.key === 'venue');
  expect(venue?.soon).toBe(false);
});

it('owner sees the venue item', () => {
  const items = visibleNav('owner').flatMap(g => g.items).map(i => i.key);
  expect(items).toContain('venue');
});

it('exposes settings as a live owner-only branch item', () => {
  const settings = clubNav[0].items.find(i => i.key === 'settings');
  expect(settings).toBeDefined();
  expect(settings?.soon).toBe(false);
  expect(settings?.ownerOnly).toBe(true);
  expect(settings?.path).toBe('/club/settings');
});
