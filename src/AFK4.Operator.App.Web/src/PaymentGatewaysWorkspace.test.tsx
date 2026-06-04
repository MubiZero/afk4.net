import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';

// bun's mock.module is not hoisted above static imports, so register it before
// importing the component under test.
const listMock = mock(async () => ({
  gateways: [
    {
      branchPaymentGatewayId: 'g1',
      branchId: null,
      dcgateProjectId: 'p1',
      cardLast4: '4242',
      status: 'pending_telegram',
      createdAtUtc: '2026-06-04T00:00:00Z',
      updatedAtUtc: '2026-06-04T00:00:00Z'
    }
  ]
}));
const provisionMock = mock(async () => ({}));
const startMock = mock(async () => ({ loginAttemptId: 'att', state: 'code_required' }));

const actualClients = await import('./operatorApiClients');
mock.module('./operatorApiClients', () => ({
  ...actualClients,
  createOperatorApiClients: () => ({
    paymentGateways: {
      list: listMock,
      provision: provisionMock,
      telegramStart: startMock,
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

const { PaymentGatewaysWorkspace } = await import('./PaymentGatewaysWorkspace');

const backend = {
  config: { platformBaseUrl: 'http://test' },
  session: { accessToken: 't' },
  branchId: 'b1'
};

describe('PaymentGatewaysWorkspace', () => {
  afterEach(() => {
    cleanup();
  });

  it('lists existing gateways with a pending-telegram badge', async () => {
    render(<I18nProvider><PaymentGatewaysWorkspace backend={backend} /></I18nProvider>);
    expect(await screen.findByText(/4242/)).toBeInTheDocument();
    expect(listMock).toHaveBeenCalled();
  });

  it('starts telegram attach for a pending gateway', async () => {
    render(<I18nProvider><PaymentGatewaysWorkspace backend={backend} /></I18nProvider>);
    await screen.findByText(/4242/);
    const phone = screen.getByLabelText(/телефон|phone/i);
    fireEvent.change(phone, { target: { value: '+992900000000' } });
    fireEvent.click(screen.getByRole('button', { name: /код|code/i }));
    await waitFor(() => expect(startMock).toHaveBeenCalled());
  });
});
