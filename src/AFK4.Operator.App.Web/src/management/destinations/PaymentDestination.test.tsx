import { cleanup, render, screen } from '@testing-library/react';
import { afterAll, afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';

// bun's mock.module is not hoisted above static imports, so register it before importing the
// component under test — same pattern as PaymentGatewaysWorkspace.test.tsx, since
// PaymentDestination just wraps that workspace and inherits its client wiring.
const listMock = mock(async () => ({
  gateways: [
    {
      branchPaymentGatewayId: 'g1',
      branchId: null,
      dcgateProjectId: 'p1',
      cardLast4: '4242',
      status: 'active',
      createdAtUtc: '2026-06-04T00:00:00Z',
      updatedAtUtc: '2026-06-04T00:00:00Z'
    }
  ]
}));
const actualClients = await import('../../operatorApiClients');
mock.module('../../operatorApiClients', () => ({
  ...actualClients,
  createOperatorApiClients: () => ({
    paymentGateways: {
      list: listMock,
      provision: mock(async () => ({})),
      disable: mock(async () => ({})),
      telegramStart: mock(async () => ({ loginAttemptId: null, state: 'attached' })),
      telegramVerifyCode: mock(async () => ({ state: 'attached', gatewayStatus: 'active' })),
      telegramVerifyPassword: mock(async () => ({ state: 'attached', gatewayStatus: 'active' })),
      status: mock(async () => ({
        gatewayStatus: 'active',
        sessionHealth: 'online',
        lastConnectedAt: null,
        lastMessageAt: null,
        telegramMessagesCount: 0
      }))
    }
  })
}));

const { PaymentDestination } = await import('./PaymentDestination');

afterAll(() => {
  mock.module('../../operatorApiClients', () => (globalThis as typeof globalThis & {
    __afk4RealOperatorApiClients: typeof import('../../operatorApiClients');
  }).__afk4RealOperatorApiClients);
});

const backend = {
  config: { platformBaseUrl: 'http://test' },
  session: { accessToken: 't', organizationId: 'o1' },
  branchId: 'b1'
} as never;

const session = { permissions: [], organizationId: 'o1', displayName: 'x' } as never;

describe('PaymentDestination', () => {
  afterEach(() => {
    cleanup();
    mock.restore();
  });

  it('renders the ManagementScreen title and subtitle', async () => {
    const { container } = render(
      <I18nProvider initialLocale="ru">
        <PaymentDestination backend={backend} session={session} currencyCode="TJS" />
      </I18nProvider>
    );

    expect(screen.getByRole('heading', { name: 'Оплата' })).toBeTruthy();
    expect(container.querySelector('.management-screen-head')?.textContent).toContain('Приём онлайн-оплаты');

    // Provision form is present.
    expect(screen.getByRole('button', { name: 'Создать' })).toBeTruthy();

    // Concrete provider info renders, not a generic label: the last4 and status of the actual
    // gateway, via the workspace's own payment-card-* markup.
    const row = await screen.findByText(/4242/);
    expect(row).toBeTruthy();
    expect(container.querySelector('.payment-card-pan')?.textContent).toContain('4242');
    expect(container.querySelector('.payment-card-status')).toBeTruthy();
  });

  it('calls onDirtyChange(false) on mount since this destination has no save-bar', async () => {
    const onDirtyChange = mock(() => {});
    render(
      <I18nProvider initialLocale="ru">
        <PaymentDestination
          backend={backend}
          session={session}
          currencyCode="TJS"
          onDirtyChange={onDirtyChange}
        />
      </I18nProvider>
    );
    expect(onDirtyChange).toHaveBeenCalledWith(false);
    // Drain the workspace's gateway-list fetch so its state settles before the test ends
    // (otherwise the update lands after cleanup and React logs an act() warning).
    await screen.findByText(/4242/);
  });

  it('renders an honest empty state instead of crashing when backend is null', () => {
    const { container } = render(
      <I18nProvider initialLocale="ru">
        <PaymentDestination backend={null} session={session} currencyCode="TJS" />
      </I18nProvider>
    );

    expect(screen.getByRole('heading', { name: 'Оплата' })).toBeTruthy();
    expect(screen.getByText('Нет подключения к серверу')).toBeTruthy();
    // No gateway client is built, so none of the workspace's own markup renders.
    expect(container.querySelector('.payment-cards-provision')).toBeNull();
  });
});
