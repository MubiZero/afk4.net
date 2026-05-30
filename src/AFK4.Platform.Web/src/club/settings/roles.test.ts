// src/club/settings/roles.test.ts
import { it, expect } from 'vitest';
import { ASSIGNABLE_ROLES, roleLabelKey } from './roles';

it('exposes the five assignable branch-staff roles and excludes owner', () => {
  expect(ASSIGNABLE_ROLES).toEqual([
    'branch_manager', 'shift_supervisor', 'cashier_operator', 'technician', 'accountant_auditor'
  ]);
  expect(ASSIGNABLE_ROLES).not.toContain('owner');
});

it('maps known roles to label keys and falls back to roles.unknown', () => {
  expect(roleLabelKey('branch_manager')).toBe('roles.branch_manager');
  expect(roleLabelKey('owner')).toBe('roles.owner');
  expect(roleLabelKey('something_else')).toBe('roles.unknown');
});
