import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { ManualCorrectionDialog } from './ManualCorrectionDialog';

function setup() {
  const client = {
    createManualCorrection: vi.fn<(id: string, req: object) => Promise<object>>(async () => ({ ledgerEntryId: 'l9' }))
  };
  const onDone = vi.fn();
  render(
    <I18nProvider><ToastProvider>
      <ManualCorrectionDialog
        open client={client as never} playerAccountId="p1" organizationId="org"
        currencyCode="TJS" onOpenChange={() => {}} onDone={onDone}
      />
    </ToastProvider></I18nProvider>
  );
  return { client, onDone };
}

it('submits a wallet correction (default account) with minor units', async () => {
  const { client, onDone } = setup();
  fireEvent.change(screen.getByLabelText('Сумма'), { target: { value: '-5' } });
  fireEvent.change(screen.getByLabelText('Причина'), { target: { value: 'правка' } });
  fireEvent.click(screen.getByRole('button', { name: 'Подтвердить' }));
  await waitFor(() => expect(client.createManualCorrection).toHaveBeenCalled());
  expect(client.createManualCorrection.mock.calls[0][0]).toBe('p1');
  expect(client.createManualCorrection.mock.calls[0][1]).toMatchObject({
    organizationId: 'org', accountType: 'wallet',
    amount: { currencyCode: 'TJS', minorUnits: -500 }, quantitySeconds: 0, reason: 'правка'
  });
  await waitFor(() => expect(onDone).toHaveBeenCalled());
});

it('disables submit until amount and reason are set', () => {
  setup();
  expect(screen.getByRole('button', { name: 'Подтвердить' })).toBeDisabled();
  fireEvent.change(screen.getByLabelText('Сумма'), { target: { value: '5' } });
  fireEvent.change(screen.getByLabelText('Причина'), { target: { value: 'x' } });
  expect(screen.getByRole('button', { name: 'Подтвердить' })).toBeEnabled();
});
