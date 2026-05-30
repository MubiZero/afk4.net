import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { CreateClientDialog } from './CreateClientDialog';

function setup() {
  const client = { createPlayer: vi.fn(async () => ({ playerAccountId: 'p2' })) };
  const onDone = vi.fn();
  render(
    <I18nProvider><ToastProvider>
      <CreateClientDialog
        open branchId="b1" organizationId="org" client={client as never}
        onOpenChange={() => {}} onDone={onDone}
      />
    </ToastProvider></I18nProvider>
  );
  return { client, onDone };
}

it('disables submit until a name is entered', () => {
  setup();
  expect(screen.getByRole('button', { name: 'Создать' })).toBeDisabled();
  fireEvent.change(screen.getByLabelText('Имя'), { target: { value: 'Иван' } });
  expect(screen.getByRole('button', { name: 'Создать' })).toBeEnabled();
});

it('creates a player and reports done', async () => {
  const { client, onDone } = setup();
  fireEvent.change(screen.getByLabelText('Имя'), { target: { value: 'Иван' } });
  fireEvent.change(screen.getByLabelText('Телефон'), { target: { value: '+992900' } });
  fireEvent.click(screen.getByRole('button', { name: 'Создать' }));
  await waitFor(() => expect(client.createPlayer).toHaveBeenCalled());
  expect(client.createPlayer.mock.calls[0][0]).toBe('b1');
  expect(client.createPlayer.mock.calls[0][1]).toMatchObject({
    organizationId: 'org', displayName: 'Иван', phoneNumber: '+992900'
  });
  await waitFor(() => expect(onDone).toHaveBeenCalled());
});
