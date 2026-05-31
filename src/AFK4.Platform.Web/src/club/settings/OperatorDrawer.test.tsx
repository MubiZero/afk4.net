// src/club/settings/OperatorDrawer.test.tsx
import { render, screen, fireEvent, waitFor, within } from '@testing-library/react';
import { it, expect, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { OperatorDrawer } from './OperatorDrawer';
import type { OperatorRow } from './settingsModel';

const active: OperatorRow = {
  staffUserId: 's1', organizationId: 'org', userName: 'ANN', displayName: 'Анна', isActive: true, roleNames: ['branch_manager']
};

function fakeClient() {
  return {
    updateStaffProfile: mock().mockResolvedValue({}),
    updateStaffRoles: mock().mockResolvedValue({}),
    updateStaffState: mock().mockResolvedValue({}),
    resetStaffPassword: mock().mockResolvedValue({})
  };
}

function setup(row: OperatorRow, currentStaffUserId = 'me', client = fakeClient(), onDone = mock()) {
  render(
    <I18nProvider><ToastProvider>
      <OperatorDrawer operator={row} branchId="b1" currentStaffUserId={currentStaffUserId} client={client as never} onDone={onDone} />
    </ToastProvider></I18nProvider>
  );
  return { client, onDone };
}

it('saves the operator profile', async () => {
  const { client } = setup(active);
  fireEvent.change(screen.getByLabelText('Отображаемое имя'), { target: { value: 'Анна Б.' } });
  fireEvent.click(screen.getByRole('button', { name: 'Сохранить профиль' }));
  await waitFor(() => expect(client.updateStaffProfile).toHaveBeenCalledWith('b1', 's1', { organizationId: 'org', userName: 'ANN', displayName: 'Анна Б.' }));
});

it('adds a role and saves the role set', async () => {
  const { client } = setup(active);
  fireEvent.click(screen.getByRole('checkbox', { name: 'Техник' }));
  fireEvent.click(screen.getByRole('button', { name: 'Сохранить роли' }));
  await waitFor(() => expect(client.updateStaffRoles).toHaveBeenCalledWith('b1', 's1', { organizationId: 'org', roleNames: ['branch_manager', 'technician'] }));
});

it('deactivates an active operator through the confirm dialog', async () => {
  const { client } = setup(active);
  fireEvent.click(screen.getByRole('button', { name: 'Деактивировать' }));
  const dialog = await waitFor(() => screen.getByRole('dialog'));
  fireEvent.click(within(dialog).getByRole('button', { name: 'Деактивировать' }));
  await waitFor(() => expect(client.updateStaffState).toHaveBeenCalledWith('b1', 's1', { organizationId: 'org', isActive: false }));
});

it('disables deactivation for the current account (self)', () => {
  setup(active, 's1');
  expect(screen.getByRole('button', { name: 'Деактивировать' })).toBeDisabled();
});

it('resets the password when the new password meets the length requirement', async () => {
  const { client } = setup(active);
  fireEvent.click(screen.getByRole('button', { name: 'Сбросить пароль' }));
  const dialog = await waitFor(() => screen.getByRole('dialog'));
  fireEvent.change(within(dialog).getByLabelText('Новый пароль'), { target: { value: 'longenough' } });
  fireEvent.click(within(dialog).getByRole('button', { name: 'Сбросить пароль' }));
  await waitFor(() => expect(client.resetStaffPassword).toHaveBeenCalledWith('b1', 's1', { organizationId: 'org', newPassword: 'longenough' }));
});

it('rejects a too-short password without calling the API', async () => {
  const { client } = setup(active);
  fireEvent.click(screen.getByRole('button', { name: 'Сбросить пароль' }));
  const dialog = await waitFor(() => screen.getByRole('dialog'));
  fireEvent.change(within(dialog).getByLabelText('Новый пароль'), { target: { value: 'short' } });
  fireEvent.click(within(dialog).getByRole('button', { name: 'Сбросить пароль' }));
  await waitFor(() => expect(screen.getByText('Пароль должен содержать не менее 8 символов')).toBeInTheDocument());
  expect(client.resetStaffPassword).not.toHaveBeenCalled();
});

it('shows an error toast and does not call onDone when a save fails', async () => {
  const client = { ...fakeClient(), updateStaffProfile: mock().mockRejectedValue(new Error('boom')) };
  const { onDone } = setup(active, 'me', client as never);
  fireEvent.click(screen.getByRole('button', { name: 'Сохранить профиль' }));
  await waitFor(() => expect(screen.getByText('Не удалось выполнить действие')).toBeInTheDocument());
  expect(onDone).not.toHaveBeenCalled();
});
