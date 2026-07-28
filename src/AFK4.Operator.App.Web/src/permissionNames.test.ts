import { describe, expect, it } from 'bun:test';
import { permissionNames } from './permissionNames';

describe('organization permission names', () => {
  it('uses the organization namespace exactly once for every permission', () => {
    const values = Object.values(permissionNames);

    expect(values.length).toBeGreaterThan(0);
    expect(values.every((value) => value.startsWith('organization.'))).toBe(true);
    expect(values.some((value) => value.startsWith('organization.organization.'))).toBe(false);
    expect(new Set(values).size).toBe(values.length);
  });
});
