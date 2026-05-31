import { describe, expect, it, vi } from 'vitest';
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
    listInvoices: vi.fn().mockResolvedValue([invoice({})]),
    markInvoicePaid: vi.fn().mockResolvedValue(invoice({ status: 'paid' })),
    voidInvoice: vi.fn().mockResolvedValue(invoice({ status: 'void' }))
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
});
