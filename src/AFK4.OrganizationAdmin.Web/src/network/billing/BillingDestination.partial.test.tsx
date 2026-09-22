import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { PlatformApiError } from '../../platformApi';
import { BillingDestination } from './BillingDestination';
import type { BillingClient } from './useBilling';

afterEach(() => cleanup());

const subscription = {
  planCode: 'PRO',
  status: 'active',
  currentPeriodStartUtc: '2026-07-01T00:00:00Z',
  currentPeriodEndUtc: '2026-07-31T00:00:00Z',
  nextInvoiceUtc: '2026-08-01T00:00:00Z',
  amountMinorUnits: 120000,
  currencyCode: 'TJS',
  cancelAtPeriodEnd: false
};

const invoice = {
  invoiceId: 'i1',
  number: 42,
  issuedAtUtc: '2026-07-01T00:00:00Z',
  dueAtUtc: '2026-07-10T00:00:00Z',
  amountMinorUnits: 120000,
  currencyCode: 'TJS',
  status: 'paid'
};

const backend = {
  config: { platformBaseUrl: 'x', currencyCode: 'TJS' },
  session: { organizationId: 'org', accessToken: 't' },
  branchId: 'b1'
};

// Отдельный файл, а не соседний BillingDestination.test.tsx: тот подменяет фабрику клиентов
// через mock.module, а здесь клиент приходит пропом и подмена не нужна.
describe('BillingDestination — одна секция не пришла', () => {
  it('отказ счетов не стирает подписку, называет причину и повторяет только счета', async () => {
    const getSubscription = mock(async () => subscription);
    const listInvoices = mock()
      .mockRejectedValueOnce(new PlatformApiError('boom', 500, 'Internal Server Error', ''))
      .mockResolvedValue([invoice]);
    const client = { getSubscription, listInvoices } as unknown as BillingClient;

    render(
      <I18nProvider initialLocale="ru">
        <BillingDestination backend={backend as never} client={client} />
      </I18nProvider>
    );

    expect(await screen.findByText('PRO')).toBeInTheDocument();
    expect(screen.getByText('Не удалось загрузить счета')).toBeInTheDocument();
    expect(screen.getByText('Сервер вернул ошибку. Повторите позже.')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Повторить' }));

    expect(await screen.findByText('42')).toBeInTheDocument();
    await waitFor(() => expect(listInvoices).toHaveBeenCalledTimes(2));
    expect(getSubscription).toHaveBeenCalledTimes(1);
    expect(screen.getByText('PRO')).toBeInTheDocument();
  });
});
