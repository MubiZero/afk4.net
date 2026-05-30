import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { PurchasePackageDialog } from './PurchasePackageDialog';

function setup(choices = [{ packageDefinitionId: 'pd1', name: 'Старт' }]) {
  const client = {
    purchasePackage: vi.fn<(id: string, req: object) => Promise<object>>(async () => ({ playerPackageId: 'pp9' }))
  };
  const onDone = vi.fn();
  render(
    <I18nProvider><ToastProvider>
      <PurchasePackageDialog
        open client={client as never} playerAccountId="p1" organizationId="org"
        choices={choices} onOpenChange={() => {}} onDone={onDone}
      />
    </ToastProvider></I18nProvider>
  );
  return { client, onDone };
}

it('purchases the default-selected package', async () => {
  const { client, onDone } = setup();
  fireEvent.click(screen.getByRole('button', { name: 'Купить' }));
  await waitFor(() => expect(client.purchasePackage).toHaveBeenCalled());
  expect(client.purchasePackage.mock.calls[0][0]).toBe('p1');
  expect(client.purchasePackage.mock.calls[0][1]).toMatchObject({
    organizationId: 'org', packageDefinitionId: 'pd1'
  });
  await waitFor(() => expect(onDone).toHaveBeenCalled());
});

it('disables submit and shows a note when there are no choices', () => {
  setup([]);
  expect(screen.getByText('Нет доступных пакетов для покупки.')).toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'Купить' })).toBeDisabled();
});
