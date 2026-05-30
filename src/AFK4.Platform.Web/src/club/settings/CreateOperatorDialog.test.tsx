// src/club/settings/CreateOperatorDialog.test.tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { CreateOperatorDialog } from './CreateOperatorDialog';

function setup(client: { createStaff: ReturnType<typeof vi.fn> }, onDone = vi.fn(), onOpenChange = vi.fn()) {
  render(
    <I18nProvider><ToastProvider>
      <CreateOperatorDialog open branchId="b1" organizationId="org" client={client as never} onOpenChange={onOpenChange} onDone={onDone} />
    </ToastProvider></I18nProvider>
  );
  return { client, onDone, onOpenChange };
}

it('keeps submit disabled until all fields and a role are valid', () => {
  setup({ createStaff: vi.fn() });
  const submit = screen.getByRole('button', { name: 'Создать' });
  expect(submit).toBeDisabled();
  fireEvent.change(screen.getByLabelText('Логин'), { target: { value: 'newop' } });
  fireEvent.change(screen.getByLabelText('Отображаемое имя'), { target: { value: 'Новый' } });
  fireEvent.change(screen.getByLabelText('Пароль'), { target: { value: 'longenough' } });
  fireEvent.click(screen.getByRole('checkbox', { name: 'Кассир-оператор' }));
  expect(submit).toBeEnabled();
});

it('creates the operator with trimmed values and selected roles', async () => {
  const client = { createStaff: vi.fn().mockResolvedValue({}) };
  const { onDone, onOpenChange } = setup(client);
  fireEvent.change(screen.getByLabelText('Логин'), { target: { value: ' newop ' } });
  fireEvent.change(screen.getByLabelText('Отображаемое имя'), { target: { value: 'Новый' } });
  fireEvent.change(screen.getByLabelText('Пароль'), { target: { value: 'longenough' } });
  fireEvent.click(screen.getByRole('checkbox', { name: 'Кассир-оператор' }));
  fireEvent.click(screen.getByRole('button', { name: 'Создать' }));
  await waitFor(() => expect(client.createStaff).toHaveBeenCalledWith('b1', {
    organizationId: 'org', userName: 'newop', displayName: 'Новый', password: 'longenough', roleNames: ['cashier_operator']
  }));
  await waitFor(() => expect(onDone).toHaveBeenCalled());
  await waitFor(() => expect(onOpenChange).toHaveBeenCalledWith(false));
});
