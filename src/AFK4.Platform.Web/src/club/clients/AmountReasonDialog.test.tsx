import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { AmountReasonDialog } from './AmountReasonDialog';

function setup(kind: 'topUp' | 'payDebt') {
  const client = {
    topUpWallet: mock<(id: string, req: object) => Promise<object>>(async () => ({ ledgerEntryId: 'l9' })),
    payDebt: mock<(id: string, req: object) => Promise<object>>(async () => ({ ledgerEntryId: 'l9' }))
  };
  const onDone = mock();
  render(
    <I18nProvider><ToastProvider>
      <AmountReasonDialog
        open kind={kind} client={client as never} playerAccountId="p1" organizationId="org"
        currencyCode="TJS" onOpenChange={() => {}} onDone={onDone}
      />
    </ToastProvider></I18nProvider>
  );
  return { client, onDone };
}

it('disables submit until amount and reason are filled', () => {
  setup('topUp');
  expect(screen.getByRole('button', { name: 'Подтвердить' })).toBeDisabled();
  fireEvent.change(screen.getByLabelText('Сумма'), { target: { value: '50' } });
  fireEvent.change(screen.getByLabelText('Причина'), { target: { value: 'касса' } });
  expect(screen.getByRole('button', { name: 'Подтвердить' })).toBeEnabled();
});

it('tops up the wallet with minor units', async () => {
  const { client, onDone } = setup('topUp');
  fireEvent.change(screen.getByLabelText('Сумма'), { target: { value: '50' } });
  fireEvent.change(screen.getByLabelText('Причина'), { target: { value: 'касса' } });
  fireEvent.click(screen.getByRole('button', { name: 'Подтвердить' }));
  await waitFor(() => expect(client.topUpWallet).toHaveBeenCalled());
  expect(client.topUpWallet.mock.calls[0][0]).toBe('p1');
  expect(client.topUpWallet.mock.calls[0][1]).toMatchObject({
    organizationId: 'org', amount: { currencyCode: 'TJS', minorUnits: 5000 }, reason: 'касса'
  });
  await waitFor(() => expect(onDone).toHaveBeenCalled());
});

it('pays debt when kind is payDebt', async () => {
  const { client } = setup('payDebt');
  fireEvent.change(screen.getByLabelText('Сумма'), { target: { value: '15' } });
  fireEvent.change(screen.getByLabelText('Причина'), { target: { value: 'долг' } });
  fireEvent.click(screen.getByRole('button', { name: 'Подтвердить' }));
  await waitFor(() => expect(client.payDebt).toHaveBeenCalled());
  expect(client.payDebt.mock.calls[0][1]).toMatchObject({ amount: { currencyCode: 'TJS', minorUnits: 1500 } });
});
