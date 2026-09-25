import { describe, it, expect, mock, afterEach } from 'bun:test';
import { render, screen, cleanup, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { BillingDestination } from './BillingDestination';
import type { BillingClient } from './useBilling';
import type { ClubPlanDto } from '@afk4/contracts';

afterEach(() => cleanup());

// Клиент передаётся экрану, а не подменяется через mock.module: подмена общего фабричного хелпера
// в bun переживает файл и доставалась соседним наборам.
const client = {
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
} as unknown as BillingClient;

const backend = {
  config: { platformBaseUrl: 'x', currencyCode: 'TJS' },
  session: { organizationId: 'org', accessToken: 't' },
  branchId: 'b1'
};

const plan = (overrides: Partial<ClubPlanDto> = {}): ClubPlanDto => ({
  planCode: 'per_pc', kind: 'per_pc', devices: 14, includedDevices: 10, billableDevices: 4,
  pricePerDevice: { currencyCode: 'TJS', minorUnits: 1000 }, estimatedMonthly: { currencyCode: 'TJS', minorUnits: 4000 },
  trialEndsAtUtc: null, trialAvailable: false, canSwitchToPerPc: false, promisedPaymentAvailable: false,
  promisedPaymentUntilUtc: null, overdue: null, ...overrides
});

function planClient(first: ReturnType<typeof plan>, afterAction?: ReturnType<typeof plan>) {
  return {
    getPlan: mock(async () => first),
    startTrial: mock(async () => afterAction ?? first),
    switchToPerPc: mock(async () => afterAction ?? first),
    promisePayment: mock(async () => afterAction ?? first)
  };
}

describe('BillingDestination', () => {
  it('shows the plan in words, subscription status and an invoice row — without the raw subscription price', async () => {
    render(
      <I18nProvider initialLocale="ru">
        <BillingDestination backend={backend as never} client={client} planClient={planClient(plan())} />
      </I18nProvider>
    );
    await waitFor(() => expect(screen.getByText('Тариф за ПК')).toBeInTheDocument());
    expect(screen.getByText('14')).toBeInTheDocument();
    expect(screen.getByText(/^40\s?с\.$/)).toBeInTheDocument();
    // 1 200 с. — и у подписки, и у счёта; на экране остаётся только строка счёта: сумму подписки
    // прежней сетки экран больше не выводит.
    expect(screen.getAllByText(/^1\s200\s?с\.$/)).toHaveLength(1);
    expect(screen.getByText('42')).toBeInTheDocument();
    expect(screen.getByText('Активна')).toBeInTheDocument();
    expect(screen.getByText('Оплачен')).toBeInTheDocument();
  });
});

describe('тариф клуба', () => {
  it('владелец бесплатного клуба сам начинает пробный период', async () => {
    const free = plan({ planCode: 'free', kind: 'free', trialAvailable: true, canSwitchToPerPc: true, estimatedMonthly: { currencyCode: 'TJS', minorUnits: 0 } });
    const trial = plan({ planCode: 'per_pc', kind: 'trial', trialEndsAtUtc: '2026-10-25T00:00:00Z' });
    const plans = planClient(free, trial);
    const owner = { ...backend, session: { ...backend.session, permissions: ['organization.billing.subscription.manage'] } };
    render(
      <I18nProvider initialLocale="ru">
        <BillingDestination backend={owner as never} client={client} planClient={plans} />
      </I18nProvider>
    );

    const start = await screen.findByRole('button', { name: 'Попробовать 30 дней без ограничений' });
    start.click();
    await waitFor(() => expect(plans.startTrial).toHaveBeenCalledTimes(1));
    expect(await screen.findByText('Пробный период')).toBeInTheDocument();
  });

  it('без права владельца кнопок нет', async () => {
    render(
      <I18nProvider initialLocale="ru">
        <BillingDestination backend={backend as never} client={client} planClient={planClient(plan({ kind: 'free', trialAvailable: true }))} />
      </I18nProvider>
    );
    await screen.findByText('Бесплатный тариф');
    expect(screen.queryByRole('button', { name: /Попробовать/ })).toBeNull();
  });
});

describe('приведи клуб', () => {
  it('показывает код клуба и накопленные месяцы', async () => {
    render(
      <I18nProvider initialLocale="ru">
        <BillingDestination backend={backend as never} client={client} planClient={planClient(plan({ referralCode: 'AFK-7Q2MXR', referredClubs: 1, freeMonths: 1 }))} />
      </I18nProvider>
    );

    expect(await screen.findByText('AFK-7Q2MXR')).toBeInTheDocument();
    expect(screen.getByText('Приведено клубов: 1 · бесплатных месяцев впереди: 1')).toBeInTheDocument();
  });
});

