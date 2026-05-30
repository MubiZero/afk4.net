// src/club/settings/BranchProfileForm.test.tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { BranchProfileForm } from './BranchProfileForm';
import type { BranchProfileView } from './settingsModel';

const profile: BranchProfileView = { branchId: 'b1', organizationId: 'org', name: 'Центр', city: 'Москва' };

function setup(client: { updateBranchProfile: ReturnType<typeof vi.fn>; updateBranchSettings: ReturnType<typeof vi.fn> }, onDone = vi.fn()) {
  render(
    <I18nProvider><ToastProvider>
      <BranchProfileForm profile={profile} requireManualDeviceApproval={false} branchId="b1" client={client as never} onDone={onDone} />
    </ToastProvider></I18nProvider>
  );
  return { client, onDone };
}

it('saves the branch profile with trimmed values', async () => {
  const client = { updateBranchProfile: vi.fn().mockResolvedValue({}), updateBranchSettings: vi.fn() };
  const { onDone } = setup(client);
  fireEvent.change(screen.getByLabelText('Название филиала'), { target: { value: 'Север ' } });
  fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));
  await waitFor(() => expect(client.updateBranchProfile).toHaveBeenCalledWith('b1', { organizationId: 'org', name: 'Север', city: 'Москва' }));
  await waitFor(() => expect(onDone).toHaveBeenCalled());
});

it('persists the approval toggle when switched on', async () => {
  const client = { updateBranchProfile: vi.fn(), updateBranchSettings: vi.fn().mockResolvedValue({}) };
  setup(client);
  fireEvent.click(screen.getByRole('switch', { name: 'Ручное подтверждение устройств' }));
  await waitFor(() => expect(client.updateBranchSettings).toHaveBeenCalledWith('b1', { organizationId: 'org', requireManualDeviceApproval: true }));
});

it('reverts the toggle and shows an error toast when the settings call fails', async () => {
  const client = { updateBranchProfile: vi.fn(), updateBranchSettings: vi.fn().mockRejectedValue(new Error('boom')) };
  setup(client);
  const toggle = screen.getByRole('switch', { name: 'Ручное подтверждение устройств' });
  fireEvent.click(toggle);
  await waitFor(() => expect(screen.getByText('Не удалось выполнить действие')).toBeInTheDocument());
  expect(toggle).toHaveAttribute('aria-checked', 'false');
});
