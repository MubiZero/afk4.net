import { describe, it, expect, mock, afterEach } from 'bun:test';
import { render, screen, cleanup, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';

afterEach(() => cleanup());

mock.module('../../operatorHelpers', () => ({
  createAuthenticatedOperatorClients: () => ({
    orgBilling: {
      getSubscription: mock(async () => ({
        planCode: 'PRO',
        status: 'active',
        currentPeriodStartUtc: '2026-07-01T00:00:00Z',
        currentPeriodEndUtc: '2026-07-31T00:00:00Z',
        nextInvoiceUtc: '2026-08-01T00:00:00Z',
        amountMinorUnits: 120000,
        currencyCode: 'TJS',
        cancelAtPeriodEnd: false
      })),
      listInvoices: mock(async () => [
        {
          invoiceId: 'i1',
          number: 42,
          issuedAtUtc: '2026-07-01T00:00:00Z',
          dueAtUtc: '2026-07-10T00:00:00Z',
          amountMinorUnits: 120000,
          currencyCode: 'TJS',
          status: 'paid'
        }
      ])
    }
  })
}));

const backend = {
  config: { platformBaseUrl: 'x', currencyCode: 'TJS' },
  session: { organizationId: 'org', accessToken: 't' },
  branchId: 'b1'
};

describe('BillingDestination', () => {
  it('renders plan code, subscription status and an invoice row', async () => {
    const { BillingDestination } = await import('./BillingDestination');
    render(
      <I18nProvider initialLocale="ru">
        <BillingDestination backend={backend as never} />
      </I18nProvider>
    );
    await waitFor(() => expect(screen.getByText('PRO')).toBeInTheDocument());
    expect(screen.getByText('42')).toBeInTheDocument();
    expect(screen.getByText('Активна')).toBeInTheDocument();
    expect(screen.getByText('Оплачен')).toBeInTheDocument();
  });
});
