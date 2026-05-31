import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { RefundDialog } from './RefundDialog';

function setup() {
  const client = {
    refundLedgerEntry: mock<(id: string, lid: string, req: object) => Promise<object>>(async () => ({ ledgerEntryId: 'l9' }))
  };
  const onDone = mock();
  render(
    <I18nProvider><ToastProvider>
      <RefundDialog
        open client={client as never} playerAccountId="p1" organizationId="org"
        entry={{ ledgerEntryId: 'l1', amountMajor: 50, currencyCode: 'TJS' }}
        onOpenChange={() => {}} onDone={onDone}
      />
    </ToastProvider></I18nProvider>
  );
  return { client, onDone };
}

it('pre-fills the amount and refunds the entry', async () => {
  const { client, onDone } = setup();
  expect((screen.getByLabelText('Сумма') as HTMLInputElement).value).toBe('50');
  fireEvent.change(screen.getByLabelText('Причина'), { target: { value: 'брак' } });
  fireEvent.click(screen.getByRole('button', { name: 'Подтвердить' }));
  await waitFor(() => expect(client.refundLedgerEntry).toHaveBeenCalled());
  expect(client.refundLedgerEntry.mock.calls[0][0]).toBe('p1');
  expect(client.refundLedgerEntry.mock.calls[0][1]).toBe('l1');
  expect(client.refundLedgerEntry.mock.calls[0][2]).toMatchObject({
    organizationId: 'org', ledgerEntryId: 'l1', amount: { currencyCode: 'TJS', minorUnits: 5000 }, reason: 'брак'
  });
  await waitFor(() => expect(onDone).toHaveBeenCalled());
});
