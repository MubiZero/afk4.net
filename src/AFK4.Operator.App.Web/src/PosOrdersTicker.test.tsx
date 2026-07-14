import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterAll, afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from './operatorToast';

// bun's mock.module is not hoisted above static imports, so register it before
// importing the component under test.
const m = (minorUnits: number) => ({ currencyCode: 'TJS', minorUnits });
const listQueue = mock(async () => ([
  {
    id: 'o1', branchId: 'b1', seatId: 'PC-01', playerAccountId: 'pl', playerDisplayName: 'Alex',
    status: 'placed', total: m(8500),
    // 3 позиции: чип покажет «3 поз · 85 с.», сами товары — только в поповере.
    lines: [
      { productId: 'p1', name: 'Cola 0.5', unitPrice: m(1500), quantity: 1, lineTotal: m(1500) },
      { productId: 'p2', name: 'Хот-дог', unitPrice: m(2800), quantity: 2, lineTotal: m(5600) },
      { productId: 'p3', name: 'Чипсы Lays', unitPrice: m(1400), quantity: 1, lineTotal: m(1400) }
    ],
    placedAtUtc: '2026-06-25T08:00:00Z', acceptedAtUtc: null, deliveredAtUtc: null, cancelledAtUtc: null, version: 1
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
const createLiveRealtime = mock(() => ({ async start() {}, async stop() {} }));
const createPreviewRealtime = mock(() => ({ async start() {}, async stop() {} }));
mock.module('./operatorRealtime', () => ({
  ...actualRealtime,
  createOperatorRealtimeClient: createLiveRealtime,
  createPreviewOperatorRealtimeClient: createPreviewRealtime
}));

const { PosOrdersTicker } = await import('./PosOrdersTicker');

// Restore the real modules once this file is done — bun keeps mock.module registrations for the
// rest of the run, which would otherwise leak into App.test.tsx and helper/realtime tests.
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

function renderTicker(b: unknown = backend) {
  render(<I18nProvider><ToastProvider><PosOrdersTicker backend={b as never} /></ToastProvider></I18nProvider>);
}

describe('PosOrdersTicker', () => {
  afterEach(() => {
    cleanup();
    mock.restore();
  });

  it('принимает заказ из поповера: на чипе быстрых действий нет', async () => {
    renderTicker();
    // Чип показывает место (откуда) и сводку «N поз · сумма», без имени заказчика.
    expect(await screen.findByText('PC-01')).toBeInTheDocument();
    const summary = screen.getByText(/3 поз/);
    expect(summary).toHaveTextContent('85'); // 3 позиции · 85 с. (total 8500 minor)
    expect(screen.queryByText('Alex')).toBeNull();
    // На чипе нет быстрых действий — решение принимается в поповере, видя состав.
    expect(screen.queryByRole('button', { name: /^принять$|^accept$/i })).toBeNull();
    fireEvent.click(screen.getByText('PC-01')); // клик по телу чипа открывает поповер
    const dialog = await screen.findByRole('dialog');
    // Новый заказ → в поповере есть «Принять», но ещё нет «Выдать».
    expect(within(dialog).getByRole('button', { name: /принять|accept/i })).toBeInTheDocument();
    expect(within(dialog).queryByRole('button', { name: /выдать|deliver/i })).toBeNull();
    fireEvent.click(within(dialog).getByRole('button', { name: /принять|accept/i }));
    await waitFor(() => expect(accept).toHaveBeenCalledWith('b1', 'o1', 1));
  });

  it('uses an icon-only chevron whose direction follows disclosure state', async () => {
    renderTicker();

    const disclosure = await screen.findByRole('button', { expanded: false });
    expect(disclosure).not.toHaveTextContent(/Подробнее|Details|Тафсилот/);

    fireEvent.click(disclosure);

    expect(disclosure).toHaveAttribute('aria-expanded', 'true');
    expect(disclosure.querySelector('.pos-order-chevron')).toHaveClass('is-expanded');
  });

  it('подтверждает приём заказа тостом', async () => {
    renderTicker();
    fireEvent.click(await screen.findByText('PC-01'));
    const dialog = await screen.findByRole('dialog');
    fireEvent.click(within(dialog).getByRole('button', { name: /принять|accept/i }));
    await waitFor(() => expect(accept).toHaveBeenCalled());
    await waitFor(() => expect(screen.getByText('Заказ принят')).toBeInTheDocument());
  });

  it('показывает тост ошибки, когда действие падает', async () => {
    accept.mockImplementationOnce(() => Promise.reject(new Error('network')));
    renderTicker();
    fireEvent.click(await screen.findByText('PC-01'));
    const dialog = await screen.findByRole('dialog');
    fireEvent.click(within(dialog).getByRole('button', { name: /принять|accept/i }));
    await waitFor(() => expect(screen.getByRole('alert')).toBeInTheDocument());
  });

  it('ошибку загрузки показывает явно, а не тихим «нет заказов»', async () => {
    listQueue.mockImplementationOnce(() => Promise.reject(new Error('network')));
    renderTicker();
    await waitFor(() => expect(screen.getByRole('alert')).toBeInTheDocument());
    expect(screen.queryByText(/активных заказов нет/i)).toBeNull();
  });

  it('без бэкенда — пустая лента («Активных заказов нет»)', async () => {
    renderTicker(null);
    expect(await screen.findByText(/активных заказов нет/i)).toBeInTheDocument();
  });

  it('в browser preview не открывает настоящий SignalR transport', async () => {
    const liveCallsBefore = createLiveRealtime.mock.calls.length;
    const previewCallsBefore = createPreviewRealtime.mock.calls.length;
    renderTicker({
      ...backend,
      config: { ...backend.config, shellMode: 'vite-dev-preview' }
    });

    await screen.findByText('PC-01');
    expect(createPreviewRealtime).toHaveBeenCalledTimes(previewCallsBefore + 1);
    expect(createLiveRealtime).toHaveBeenCalledTimes(liveCallsBefore);
  });

  it('клик по чипу раскрывает весь состав в поповере (в чипе — только кол-во + сумма)', async () => {
    renderTicker();
    await screen.findByText('PC-01');
    // В чипе состава нет вовсе (только сводка) — товары видны лишь в поповере.
    expect(screen.queryByText('Чипсы Lays')).toBeNull();
    fireEvent.click(screen.getByText('PC-01')); // клик по телу чипа
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText('Cola 0.5')).toBeInTheDocument();
    expect(within(dialog).getByText('Хот-дог')).toBeInTheDocument();
    expect(within(dialog).getByText('Чипсы Lays')).toBeInTheDocument();
  });
});
