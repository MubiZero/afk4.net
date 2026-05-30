// src/club/settings/SettingsScreen.test.tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { SettingsScreen } from './SettingsScreen';
import type { BranchProfile, BranchSettings, StaffUser } from '@/api/types';

const profile: BranchProfile = { organizationId: 'org', branchId: 'b1', name: 'Центр', city: 'Москва', createdAtUtc: '' };
const settings: BranchSettings = { organizationId: 'org', branchId: 'b1', requireManualDeviceApproval: false };
const staff: StaffUser[] = [
  { staffUserId: 's1', organizationId: 'org', userName: 'ANN', displayName: 'Анна', isActive: true, roleNames: ['branch_manager'], createdAtUtc: '' }
];

function fakeClient() {
  return {
    getBranchProfile: vi.fn().mockResolvedValue(profile),
    getBranchSettings: vi.fn().mockResolvedValue(settings),
    listStaff: vi.fn().mockResolvedValue(staff),
    updateBranchProfile: vi.fn(), updateBranchSettings: vi.fn(),
    updateStaffProfile: vi.fn(), updateStaffRoles: vi.fn(), updateStaffState: vi.fn(), resetStaffPassword: vi.fn(), createStaff: vi.fn()
  };
}

function setup(client = fakeClient()) {
  render(
    <I18nProvider><ToastProvider>
      <SettingsScreen client={client as never} branchId="b1" organizationId="org" currentStaffUserId="me" />
    </ToastProvider></I18nProvider>
  );
  return { client };
}

it('renders both tabs and shows the branch form by default', async () => {
  setup();
  expect(await screen.findByRole('tab', { name: 'Филиал' })).toBeInTheDocument();
  expect(screen.getByRole('tab', { name: 'Операторы и роли' })).toBeInTheDocument();
  expect(screen.getByLabelText('Название филиала')).toBeInTheDocument();
});

it('switches to the operators tab and opens the operator drawer on row click', async () => {
  setup();
  const opTab = await screen.findByRole('tab', { name: 'Операторы и роли' });
  fireEvent.mouseDown(opTab);
  fireEvent.click(opTab);
  fireEvent.click(await screen.findByText('Анна'));
  expect(await screen.findByRole('button', { name: 'Сохранить профиль' })).toBeInTheDocument();
});

it('shows the error state with retry when loading fails', async () => {
  const client = { ...fakeClient(), getBranchProfile: vi.fn().mockRejectedValue(new Error('boom')) };
  setup(client as never);
  expect(await screen.findByText('Не удалось загрузить данные.')).toBeInTheDocument();
});
