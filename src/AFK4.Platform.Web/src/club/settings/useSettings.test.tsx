// src/club/settings/useSettings.test.tsx
import { renderHook, waitFor } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { useSettings } from './useSettings';
import type { BranchProfile, BranchSettings, StaffUser } from '@/api/types';

const profile: BranchProfile = { organizationId: 'org', branchId: 'b1', name: 'Центр', city: 'Москва', createdAtUtc: '' };
const settings: BranchSettings = { organizationId: 'org', branchId: 'b1', requireManualDeviceApproval: false };
const staff: StaffUser[] = [];

it('loads profile, settings, and staff into a ready state', async () => {
  const client = {
    getBranchProfile: vi.fn().mockResolvedValue(profile),
    getBranchSettings: vi.fn().mockResolvedValue(settings),
    listStaff: vi.fn().mockResolvedValue(staff)
  };
  const { result } = renderHook(() => useSettings(client as never, 'b1'));
  expect(result.current.status).toBe('loading');
  await waitFor(() => expect(result.current.status).toBe('ready'));
  if (result.current.status === 'ready') {
    expect(result.current.data.profile.name).toBe('Центр');
  }
});

it('surfaces an error state when a call rejects', async () => {
  const client = {
    getBranchProfile: vi.fn().mockRejectedValue(new Error('boom')),
    getBranchSettings: vi.fn().mockResolvedValue(settings),
    listStaff: vi.fn().mockResolvedValue(staff)
  };
  const { result } = renderHook(() => useSettings(client as never, 'b1'));
  await waitFor(() => expect(result.current.status).toBe('error'));
});
