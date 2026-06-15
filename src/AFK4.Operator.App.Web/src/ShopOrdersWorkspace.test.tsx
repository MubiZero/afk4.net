import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterAll, afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from './operatorToast';

// bun's mock.module is not hoisted above static imports, so register it before
// importing the component under test.
const listQueue = mock(async () => ([
  {
    id: 'o1', branchId: 'b1', seatId: 's1', playerAccountId: 'pl', playerDisplayName: 'Alex',
    status: 'placed', total: { currencyCode: 'TJS', minorUnits: 1500 }, lines: [],
    placedAtUtc: '', acceptedAtUtc: null, deliveredAtUtc: null, cancelledAtUtc: null, version: 1
  }
]));
const accept = mock(async () => ({ id: 'o1', status: 'accepted', version: 2 }));
const deliver = mock(async () => ({}));
const cancel = mock(async () => ({}));

const actualHelpers = await import('./operatorHelpers');
mock.module('./operatorHelpers', () => ({
  ...actualHelpers,
  createAuthenticatedOperatorClients: () => ({ shopOrders: { listQueue, accept, deliver, cancel } })
}));

const actualRealtime = await import('./operatorRealtime');
mock.module('./operatorRealtime', () => ({
  ...actualRealtime,
  createOperatorRealtimeClient: () => ({ async start() {}, async stop() {} })
}));

const { ShopOrdersWorkspace } = await import('./ShopOrdersWorkspace');

// Restore the real modules (snapshotted in the preload) once this file is done — bun keeps
// mock.module registrations for the rest of the run and mutates the shared namespace, which
// would otherwise break App.test.tsx and the operator helper/realtime tests.
afterAll(() => {
  mock.module('./operatorHelpers', () => (globalThis as typeof globalThis & {
    __afk4RealOperatorHelpers: typeof import('./operatorHelpers');
  }).__afk4RealOperatorHelpers);
  mock.module('./operatorRealtime', () => (globalThis as typeof globalThis & {
    __afk4RealOperatorRealtime: typeof import('./operatorRealtime');
  }).__afk4RealOperatorRealtime);
});

const backend = {
  config: { platformBaseUrl: 'http://test' },
  session: { accessToken: 't', organizationId: 'org' },
  branchId: 'b1'
};

describe('ShopOrdersWorkspace', () => {
  afterEach(() => {
    cleanup();
    mock.restore();
  });

  it('lists open orders and accepts one', async () => {
    render(<I18nProvider><ToastProvider><ShopOrdersWorkspace backend={backend as never} /></ToastProvider></I18nProvider>);
    expect(await screen.findByText('Alex')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /принять|accept/i }));
    await waitFor(() => expect(accept).toHaveBeenCalledWith('b1', 'o1', 1));
    await waitFor(() => expect(screen.queryByRole('button', { name: /принять|accept/i })).toBeNull());
    expect(screen.getByRole('button', { name: /выдать|deliver/i })).toBeInTheDocument();
  });

  it('confirms an accepted order with a toast', async () => {
    render(
      <I18nProvider>
        <ToastProvider>
          <ShopOrdersWorkspace backend={backend as never} />
        </ToastProvider>
      </I18nProvider>
    );
    const acceptButton = await screen.findByRole('button', { name: /принять|accept/i });
    fireEvent.click(acceptButton);
    await waitFor(() => expect(accept).toHaveBeenCalled());
    await waitFor(() => expect(screen.getByText('Заказ принят')).toBeInTheDocument());
  });

  it('shows an error toast when an order action fails', async () => {
    accept.mockImplementationOnce(() => Promise.reject(new Error('network')));
    render(<I18nProvider><ToastProvider><ShopOrdersWorkspace backend={backend as never} /></ToastProvider></I18nProvider>);
    fireEvent.click(await screen.findByRole('button', { name: /принять|accept/i }));
    await waitFor(() => expect(screen.getByRole('alert')).toBeInTheDocument());
  });

  it('surfaces an error instead of silently showing an empty queue', async () => {
    listQueue.mockImplementationOnce(() => Promise.reject(new Error('network')));
    render(<I18nProvider><ToastProvider><ShopOrdersWorkspace backend={backend as never} /></ToastProvider></I18nProvider>);
    expect(await screen.findByText(/ошибка|error|network/i)).toBeInTheDocument();
    expect(screen.queryByText(/активных заказов нет/i)).toBeNull();
  });
});
