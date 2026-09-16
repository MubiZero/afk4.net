import { describe, expect, it, mock } from 'bun:test';
import userEvent from '@testing-library/user-event';
import { PlatformApiError } from '@/api/platformTransport';
import { render, screen, waitFor, within } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { InvoicesTab } from './InvoicesTab';
import type { InvoiceListItem } from '@/api/types';

function invoice(p: Partial<InvoiceListItem>): InvoiceListItem {
  return {
    invoiceId: 'inv-1', organizationId: 'o', organizationName: 'Acme', organizationSlug: 'acme',
    number: 7, kind: 'subscription', issuedAtUtc: '2026-05-01T00:00:00Z', dueAtUtc: '2026-05-08T00:00:00Z',
    amountMinorUnits: 290000, currencyCode: 'RUB', status: 'issued', ...p
  };
}

function fakeClient() {
  return {
    listInvoices: mock().mockResolvedValue([invoice({})]),
    markInvoicePaid: mock().mockResolvedValue(invoice({ status: 'paid' })),
    voidInvoice: mock().mockResolvedValue(invoice({ status: 'void' }))
  } as never;
}

describe('InvoicesTab', () => {
  it('renders invoice rows after load', async () => {
    render(
      <I18nProvider><ToastProvider><InvoicesTab client={fakeClient()} /></ToastProvider></I18nProvider>
    );
    await waitFor(() => expect(screen.getByText('Acme')).toBeInTheDocument());

    // amountMinorUnits: 290000 must render as MAJOR units (2900), not 290000.
    const row = screen.getByText('Acme').closest('tr');
    expect(row).not.toBeNull();
    const cells = within(row as HTMLElement).getAllByRole('cell');
    const digitTexts = cells.map(c => (c.textContent ?? '').replace(/\D/g, ''));
    expect(digitTexts).toContain('2900');
    expect(digitTexts).not.toContain('290000');
  });

  // Счёт, который кто-то уже отметил оплаченным, — обычное дело при двух руках на одной панели.
  // Раньше на это отвечало «Не удалось выполнить операцию», и человек жал кнопку ещё раз.
  it('называет причину отказа словами сервера, а не общим «не удалось»', async () => {
    const client = fakeClient() as unknown as {
      listInvoices: ReturnType<typeof mock>;
      markInvoicePaid: ReturnType<typeof mock>;
      voidInvoice: ReturnType<typeof mock>;
    };
    client.markInvoicePaid = mock().mockRejectedValue(
      new PlatformApiError(409, 'Invoice is already paid.', 'invoice_already_paid')
    );

    render(
      <I18nProvider><ToastProvider><InvoicesTab client={client as never} /></ToastProvider></I18nProvider>
    );
    await waitFor(() => expect(screen.getByText('Acme')).toBeInTheDocument());

    await userEvent.click(screen.getAllByRole('button', { name: 'Отметить оплаченным' })[0]!);
    const confirm = screen.getAllByRole('button', { name: 'Отметить оплаченным' }).at(-1)!;
    // Референс платежа подписан «необязательно» — кнопка обязана быть нажимаемой без него.
    expect(confirm).toBeEnabled();
    await userEvent.click(confirm);

    await waitFor(() => expect(screen.getByText('Счёт уже оплачен.')).toBeInTheDocument());
  });
});
