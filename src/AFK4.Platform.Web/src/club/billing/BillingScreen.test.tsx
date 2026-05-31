import { describe, it, expect, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '../../i18n/I18nProvider';
import { BillingScreen } from './BillingScreen';
import type { ClubApiClient } from '../../api/clubApi';
import type { Invoice, TenantSubscription } from '../../api/types';

function sub(): TenantSubscription {
  return {
    tenantSubscriptionId: 's1', organizationId: 'o1', planCode: 'starter', status: 'active',
    currentPeriodStartUtc: '2026-05-01T00:00:00Z', currentPeriodEndUtc: '2026-06-01T00:00:00Z',
    nextInvoiceUtc: '2026-06-01T00:00:00Z', amountMinorUnits: 290000, currencyCode: 'RUB',
    billingInterval: 'monthly', cancelAtPeriodEnd: false,
    createdAtUtc: '2026-05-01T00:00:00Z', updatedAtUtc: '2026-05-01T00:00:00Z'
  };
}

function client(invoices: Invoice[]): ClubApiClient {
  return {
    getSubscription: vi.fn().mockResolvedValue(sub()),
    listInvoices: vi.fn().mockResolvedValue(invoices)
  } as unknown as ClubApiClient;
}

function errorClient(): ClubApiClient {
  return {
    getSubscription: vi.fn().mockRejectedValue(new Error('network')),
    listInvoices: vi.fn().mockRejectedValue(new Error('network'))
  } as unknown as ClubApiClient;
}

function wrap(ui: React.ReactElement) {
  return render(<I18nProvider>{ui}</I18nProvider>);
}

describe('BillingScreen', () => {
  it('renders the current plan once loaded', async () => {
    wrap(<BillingScreen client={client([])} organizationId="o1" />);
    await waitFor(() => expect(screen.getByText('starter')).toBeInTheDocument());
  });

  it('shows the empty state when there are no invoices', async () => {
    wrap(<BillingScreen client={client([])} organizationId="o1" />);
    await waitFor(() => expect(screen.getByText(/Нет инвойсов|No invoices/)).toBeInTheDocument());
  });

  it('shows the error state with a retry button when loading fails', async () => {
    wrap(<BillingScreen client={errorClient()} organizationId="o1" />);
    await waitFor(() => expect(screen.getByText('Повторить')).toBeInTheDocument());
  });
});
