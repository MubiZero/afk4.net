// src/club/settings/settingsModel.test.ts
import { it, expect } from 'bun:test';
import { buildSettings } from './settingsModel';
import type { BranchProfile, BranchSettings, StaffUser } from '@/api/types';

const profile: BranchProfile = {
  organizationId: 'org', branchId: 'b1', name: 'Центр', city: 'Москва', createdAtUtc: '2026-01-01T00:00:00Z'
};
const settings: BranchSettings = { organizationId: 'org', branchId: 'b1', requireManualDeviceApproval: true, preferredLocale: 'tg' };
const staff: StaffUser[] = [
  { staffUserId: 's2', organizationId: 'org', userName: 'BOB', displayName: 'Борис', isActive: true, roleNames: ['cashier_operator'], createdAtUtc: '2026-01-02T00:00:00Z' },
  { staffUserId: 's1', organizationId: 'org', userName: 'ANN', displayName: 'Анна', isActive: false, roleNames: ['branch_manager'], createdAtUtc: '2026-01-01T00:00:00Z' }
];

it('maps profile, settings flag, and sorts operators by display name', () => {
  const vm = buildSettings(profile, settings, staff);
  expect(vm.profile).toEqual({ branchId: 'b1', organizationId: 'org', name: 'Центр', city: 'Москва' });
  expect(vm.requireManualDeviceApproval).toBe(true);
  expect(vm.preferredLocale).toBe('tg');
  expect(vm.operators.map(o => o.displayName)).toEqual(['Анна', 'Борис']);
  expect(vm.operators[0]).toEqual({
    staffUserId: 's1', organizationId: 'org', userName: 'ANN', displayName: 'Анна', isActive: false, roleNames: ['branch_manager']
  });
});
