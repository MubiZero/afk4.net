// src/club/settings/CreateOperatorDialog.test.tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { CreateOperatorDialog } from './CreateOperatorDialog';

function setup(client: { createStaffInvite: ReturnType<typeof mock> }, onDone = mock(), onOpenChange = mock()) {
  render(
    <I18nProvider><ToastProvider>
      <CreateOperatorDialog open branchId="b1" organizationId="org" client={client as never} onOpenChange={onOpenChange} onDone={onDone} />
    </ToastProvider></I18nProvider>
  );
  return { client, onDone, onOpenChange };
}

it('keeps submit disabled until all fields, a valid email and a role are set', () => {
  setup({ createStaffInvite: mock() });
  const submit = screen.getByRole('button', { name: 'Отправить приглашение' });
  expect(submit).toBeDisabled();
  fireEvent.change(screen.getByLabelText('Логин'), { target: { value: 'newop' } });
  fireEvent.change(screen.getByLabelText('Отображаемое имя'), { target: { value: 'Новый' } });
  fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'not-an-email' } });
  fireEvent.click(screen.getByRole('checkbox', { name: 'Кассир-оператор' }));
  expect(submit).toBeDisabled();
  fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'newop@club.example' } });
  expect(submit).toBeEnabled();
});

it('invites the operator with trimmed values and shows the returned code', async () => {
  const client = { createStaffInvite: mock().mockResolvedValue({ staffInviteId: 'i1', code: 'abc123', expiresAtUtc: '2026-06-08T00:00:00Z' }) };
  const { onDone } = setup(client);
  fireEvent.change(screen.getByLabelText('Логин'), { target: { value: ' newop ' } });
  fireEvent.change(screen.getByLabelText('Отображаемое имя'), { target: { value: 'Новый' } });
  fireEvent.change(screen.getByLabelText('Email'), { target: { value: ' newop@club.example ' } });
  fireEvent.click(screen.getByRole('checkbox', { name: 'Кассир-оператор' }));
  fireEvent.click(screen.getByRole('button', { name: 'Отправить приглашение' }));
  await waitFor(() => expect(client.createStaffInvite).toHaveBeenCalledWith('b1', {
    organizationId: 'org', userName: 'newop', displayName: 'Новый', email: 'newop@club.example', roleNames: ['cashier_operator']
  }));
  await waitFor(() => expect(onDone).toHaveBeenCalled());
  await waitFor(() => expect(screen.getByLabelText('Код приглашения')).toHaveTextContent('abc123'));
});
