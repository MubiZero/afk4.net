import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { RenameBranchDialog } from './RenameBranchDialog';

function setup(client: { updateBranchProfile: ReturnType<typeof mock> }, onDone = mock(), onOpenChange = mock()) {
  render(
    <I18nProvider><ToastProvider>
      <RenameBranchDialog open branchId="b1" organizationId="org" initialName="Центр" initialCity="Москва"
        client={client as never} onOpenChange={onOpenChange} onDone={onDone} />
    </ToastProvider></I18nProvider>
  );
  return { client, onDone, onOpenChange };
}

it('saves the trimmed name and city, then closes', async () => {
  const client = { updateBranchProfile: mock().mockResolvedValue({}) };
  const { onDone, onOpenChange } = setup(client);
  fireEvent.change(screen.getByLabelText('Название филиала'), { target: { value: ' Новый центр ' } });
  fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));
  await waitFor(() => expect(client.updateBranchProfile).toHaveBeenCalledWith('b1', { organizationId: 'org', name: 'Новый центр', city: 'Москва' }));
  await waitFor(() => expect(onDone).toHaveBeenCalled());
  await waitFor(() => expect(onOpenChange).toHaveBeenCalledWith(false));
});

it('does not call onDone when the save fails', async () => {
  const client = { updateBranchProfile: mock().mockRejectedValue(new Error('boom')) };
  const { onDone } = setup(client);
  fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));
  await waitFor(() => expect(client.updateBranchProfile).toHaveBeenCalled());
  expect(onDone).not.toHaveBeenCalled();
});
