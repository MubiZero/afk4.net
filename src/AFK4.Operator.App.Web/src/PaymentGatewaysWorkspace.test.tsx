import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterAll, afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import type { TelegramStartResponse } from './operatorApiClients';

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
const startMock = mock(async (): Promise<TelegramStartResponse> => ({ loginAttemptId: 'att', state: 'code_required' }));
const disableMock = mock(async () => ({
  branchPaymentGatewayId: 'g1',
  branchId: null,
  dcgateProjectId: 'p1',
  cardLast4: '4242',
  status: 'disabled',
  createdAtUtc: '2026-06-04T00:00:00Z',
  updatedAtUtc: '2026-06-04T00:00:00Z'
}));
const actualClients = await import('./operatorApiClients');
mock.module('./operatorApiClients', () => ({
  ...actualClients,
  createOperatorApiClients: () => ({
    paymentGateways: {
      list: listMock,
      provision: provisionMock,
      telegramStart: startMock,
      disable: disableMock,
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

// Restore the real './operatorApiClients' (snapshotted in the preload) once this file is done —
// bun keeps mock.module registrations for the rest of the run and mutates the shared namespace,
// which would break App.test.tsx and operatorApiClients.test.ts.
afterAll(() => {
  mock.module('./operatorApiClients', () => (globalThis as typeof globalThis & {
    __afk4RealOperatorApiClients: typeof import('./operatorApiClients');
  }).__afk4RealOperatorApiClients);
});

const backend = {
  config: { platformBaseUrl: 'http://test' },
  session: { accessToken: 't' },
  branchId: 'b1'
};

describe('PaymentGatewaysWorkspace', () => {
  afterEach(() => {
    cleanup();
    mock.restore();
  });

  it('lists existing gateways with a pending-telegram badge', async () => {
    render(<I18nProvider><PaymentGatewaysWorkspace backend={backend} /></I18nProvider>);
    expect(await screen.findByText(/4242/)).toBeInTheDocument();
    expect(listMock).toHaveBeenCalled();
  });

  it('renders the empty state instead of crashing when the list omits gateways', async () => {
    // The dev mock backend answers unknown routes with a bare [], so result.gateways is undefined.
    // The workspace must fall back to an empty list, not blow up on gateways.length.
    listMock.mockResolvedValueOnce({} as Awaited<ReturnType<typeof listMock>>);
    render(<I18nProvider><PaymentGatewaysWorkspace backend={backend} /></I18nProvider>);
    expect(await screen.findByText(/нет подключённых карт/i)).toBeInTheDocument();
  });

  it('shows the live telegram session badge for a listed gateway', async () => {
    render(<I18nProvider><PaymentGatewaysWorkspace backend={backend} /></I18nProvider>);
    await screen.findByText(/4242/);
    // mock status() returns sessionHealth: 'online' → ru "На связи"
    expect(await screen.findByText(/на связи/i)).toBeInTheDocument();
  });

  it('starts telegram attach with only the phone', async () => {
    render(<I18nProvider><PaymentGatewaysWorkspace backend={backend} /></I18nProvider>);
    await screen.findByText(/4242/);
    const phoneInput = screen.getByLabelText(/телефон|phone/i);
    fireEvent.change(phoneInput, { target: { value: '+992900000000' } });
    fireEvent.click(screen.getByRole('button', { name: /код|code/i }));
    await waitFor(() => expect(startMock).toHaveBeenCalledWith('g1', { phone: '+992900000000' }));
  });

  it('disables a card after confirmation', async () => {
    const originalConfirm = globalThis.confirm;
    globalThis.confirm = () => true;
    try {
      render(<I18nProvider><PaymentGatewaysWorkspace backend={backend} /></I18nProvider>);
      await screen.findByText(/4242/);
      fireEvent.click(screen.getByRole('button', { name: /отключить|disable/i }));
      await waitFor(() => expect(disableMock).toHaveBeenCalledWith('g1'));
    } finally {
      globalThis.confirm = originalConfirm;
    }
  });

  it('skips OTP when dcgate reports the phone already attached', async () => {
    startMock.mockResolvedValueOnce({ loginAttemptId: null, state: 'attached' });
    render(<I18nProvider><PaymentGatewaysWorkspace backend={backend} /></I18nProvider>);
    await screen.findByText(/4242/);
    const phoneInput = screen.getByLabelText(/телефон|phone/i);
    fireEvent.change(phoneInput, { target: { value: '+992900000000' } });
    fireEvent.click(screen.getByRole('button', { name: /код|code/i }));
    await waitFor(() => expect(startMock).toHaveBeenCalledWith('g1', { phone: '+992900000000' }));
    await screen.findByText(/карта активна|card active/i);
    expect(screen.queryByLabelText(/код из telegram|code from telegram/i)).toBeNull();
  });
});
