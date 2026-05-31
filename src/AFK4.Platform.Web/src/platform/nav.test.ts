import { describe, expect, it } from 'vitest';
import { platformNav } from './nav';

describe('platform nav', () => {
  it('exposes overview, tenants, billing and profile', () => {
    const keys = platformNav.flatMap(g => g.items.map(i => i.key));
    expect(keys).toContain('overview');
    expect(keys).toContain('tenants');
    expect(keys).toContain('billing');
    expect(keys).toContain('profile');
  });

  it('marks every platform nav item live', () => {
    const items = platformNav.flatMap(g => g.items);
    expect(items.find(i => i.key === 'overview')?.soon).toBe(false);
    expect(items.find(i => i.key === 'tenants')?.soon).toBe(false);
    expect(items.find(i => i.key === 'billing')?.soon).toBe(false);
    expect(items.find(i => i.key === 'profile')?.soon).toBe(false);
  });

  it('every item has an /admin path and a nav. label key', () => {
    for (const g of platformNav) for (const i of g.items) {
      expect(i.path.startsWith('/admin')).toBe(true);
      expect(i.labelKey.startsWith('nav.')).toBe(true);
    }
  });
});
