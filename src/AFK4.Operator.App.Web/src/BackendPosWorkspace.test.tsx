import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { BackendPosWorkspace } from './BackendPosWorkspace';
import { ToastProvider } from './operatorToast';

const originalFetch = globalThis.fetch;
const backendProduct = {
  productId: 'product-1',
  name: 'Cola',
  sku: 'COLA',
  categoryName: 'Напитки',
  price: { currencyCode: 'TJS', minorUnits: 10_000 },
  trackStock: true,
  stockOnHand: 4,
  reorderThreshold: 2,
  barcodes: []
};
const linkedPlayer = {
  playerAccountId: 'player-1',
  displayName: 'Амир Алиев',
  phoneNumber: '+992900000001',
  walletBalanceMinorUnits: 4_500,
  debtBalanceMinorUnits: 0,
  activePackageCount: 0,
  isActive: true,
  createdAtUtc: '2026-07-14T08:00:00Z',
  lastActivityAtUtc: null,
  activePackageName: null,
  activePackageRemainingMinutes: 0
};
const requestedUrls: string[] = [];
const requestedBodies: unknown[] = [];
let settlementFailuresRemaining = 0;
let settlementNetworkFailuresRemaining = 0;
const fetchBackend = mock(async (input: RequestInfo | URL, init?: RequestInit) => {
  const url = String(input);
  requestedUrls.push(url);
  if (init?.body) {
    requestedBodies.push(JSON.parse(String(init.body)));
  }
  if (url.endsWith('/api/branches/branch-1/pos/catalog')) {
    return jsonResponse([backendProduct]);
  }
  if (url.endsWith('/api/branches/branch-1/shifts/current')) {
    return jsonResponse({ shiftId: 'shift-1' });
  }
  if (url.includes('/api/branches/branch-1/players?')) {
    return jsonResponse([linkedPlayer]);
  }
  if (url.endsWith('/api/branches/branch-1/pos/sales') && init?.method === 'POST') {
    return jsonResponse({ posSaleId: 'sale-1', state: 'draft' });
  }
  if (url.endsWith('/api/pos/sales/sale-1/settlements') && init?.method === 'POST') {
    if (settlementNetworkFailuresRemaining > 0) {
      settlementNetworkFailuresRemaining -= 1;
      throw new TypeError('network connection dropped');
    }
    if (settlementFailuresRemaining > 0) {
      settlementFailuresRemaining -= 1;
      return new Response(JSON.stringify({ error: 'version_conflict' }), {
        status: 409,
        statusText: 'Conflict',
        headers: { 'Content-Type': 'application/json' }
      });
    }
    return jsonResponse({ posSaleId: 'sale-1', state: 'paid' });
  }

  return new Response('Not Found', { status: 404, statusText: 'Not Found' });
});

function jsonResponse(body: unknown) {
  return new Response(JSON.stringify(body), {
    status: 200,
    headers: { 'Content-Type': 'application/json' }
  });
}

afterEach(() => {
  cleanup();
  globalThis.fetch = originalFetch;
  fetchBackend.mockClear();
  requestedUrls.length = 0;
  requestedBodies.length = 0;
  settlementFailuresRemaining = 0;
  settlementNetworkFailuresRemaining = 0;
});

// backend=null → fixture-режим: каталог/корзина из заглушек, без сетевых запросов.
function renderPos(embedded: boolean) {
  render(
    <I18nProvider initialLocale="ru">
      <ToastProvider>
        <BackendPosWorkspace currencyCode="TJS" backend={null} embedded={embedded} />
      </ToastProvider>
    </I18nProvider>
  );
}

const backend = {
  config: { platformBaseUrl: 'http://test' },
  session: {
    accessToken: 'token',
    organizationId: 'organization-1',
    permissions: ['players.view', 'pos.sales.create', 'pos.sales.pay']
  },
  branchId: 'branch-1'
};

function renderBackendPos() {
  globalThis.fetch = fetchBackend as unknown as typeof fetch;
  render(
    <I18nProvider initialLocale="ru">
      <ToastProvider>
        <BackendPosWorkspace currencyCode="TJS" backend={backend as never} embedded />
      </ToastProvider>
    </I18nProvider>
  );
}

describe('BackendPosWorkspace', () => {
  it('standalone: рендерит шапку «Продажи» и панели каталог + продажа (корзина+оплата)', () => {
    renderPos(false);
    expect(screen.getByRole('heading', { name: /Продажи/ })).toBeInTheDocument();
    expect(document.querySelector('main.pos-screen')).not.toBeNull();
    expect(screen.getByText('Каталог')).toBeInTheDocument();
    expect(screen.getByText('Корзина')).toBeInTheDocument();
    // Оплата слита в колонку «продажи» — отдельной панели нет, остаётся действие.
    expect(document.querySelector('.pos-sale-panel')).not.toBeNull();
    expect(screen.getByRole('button', { name: /Принять оплату/ })).toBeInTheDocument();
  });

  it('embedded: без собственного <main>/heading, корень — section.pos-embed, панели на месте', () => {
    renderPos(true);
    // Заголовок «Продажи» даёт сегмент-вкладка, не сам POS → собственного heading нет.
    expect(screen.queryByRole('heading', { name: /Продажи/ })).toBeNull();
    expect(document.querySelector('main.pos-screen')).toBeNull();
    expect(document.querySelector('section.pos-embed')).not.toBeNull();
    expect(screen.getByText('Каталог')).toBeInTheDocument();
    expect(document.querySelector('.pos-sale-panel')).not.toBeNull();
  });

  it('labels backend product price and selected client balance', async () => {
    renderBackendPos();
    await waitFor(() => expect(requestedUrls.length).toBeGreaterThanOrEqual(2));
    expect(requestedUrls).toContain('http://test/api/branches/branch-1/pos/catalog');
    expect((await screen.findAllByText('Cola')).length).toBeGreaterThanOrEqual(1);

    expect(screen.getByText('Цена:')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Выбрать' }));
    fireEvent.change(screen.getByRole('textbox', { name: 'Клиент' }), { target: { value: 'Амир' } });
    await waitFor(() => expect(requestedUrls.some((url) => url.includes('/players?'))).toBe(true));
    fireEvent.click(await screen.findByRole('button', { name: /Амир Алиев/ }));

    expect(screen.getByText('Баланс:')).toBeInTheDocument();
  });

  it('keeps wallet, cash, cart, and client after a stable settlement failure', async () => {
    settlementFailuresRemaining = 1;
    renderBackendPos();
    await screen.findAllByText('Cola');

    fireEvent.click(screen.getByRole('button', { name: 'Выбрать' }));
    fireEvent.change(screen.getByRole('textbox', { name: 'Клиент' }), { target: { value: 'Амир' } });
    fireEvent.click(await screen.findByRole('button', { name: /Амир Алиев/ }));
    fireEvent.click(screen.getByRole('button', { name: /Принять оплату/ }));

    fireEvent.click(screen.getByRole('tab', { name: 'Смешанно' }));
    const methodSelects = screen.getAllByRole('combobox', { name: 'Способ оплаты' });
    fireEvent.change(methodSelects[0], { target: { value: 'wallet' } });
    fireEvent.change(screen.getByRole('textbox', { name: 'Сумма' }), { target: { value: '20.00' } });
    fireEvent.click(screen.getByRole('button', { name: /Ещё способ/ }));
    fireEvent.change(screen.getByRole('textbox', { name: 'Получено' }), { target: { value: '80.00' } });

    const confirm = screen.getByRole('button', { name: /Принять 100/ });
    fireEvent.click(confirm);
    await waitFor(() => expect(requestedUrls.filter((url) => url.endsWith('/settlements'))).toHaveLength(1));

    expect(screen.getByRole('dialog', { name: 'Оплата' })).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: 'Сумма' })).toHaveValue('20.00');
    expect(screen.getByRole('textbox', { name: 'Получено' })).toHaveValue('80.00');
    expect(screen.getAllByText('Амир Алиев').length).toBeGreaterThanOrEqual(1);
    expect(await screen.findByText('Данные продажи изменились. Проверьте корзину и повторите оплату.')).toBeInTheDocument();
  });

  it('replays an ambiguous multipart settlement once with the same idempotency key', async () => {
    settlementNetworkFailuresRemaining = 1;
    renderBackendPos();
    await screen.findAllByText('Cola');

    fireEvent.click(screen.getByRole('button', { name: 'Выбрать' }));
    fireEvent.change(screen.getByRole('textbox', { name: 'Клиент' }), { target: { value: 'Амир' } });
    fireEvent.click(await screen.findByRole('button', { name: /Амир Алиев/ }));
    fireEvent.click(screen.getByRole('button', { name: /Принять оплату/ }));
    fireEvent.click(screen.getByRole('tab', { name: 'Смешанно' }));
    fireEvent.change(screen.getByRole('combobox', { name: 'Способ оплаты' }), { target: { value: 'wallet' } });
    fireEvent.change(screen.getByRole('textbox', { name: 'Сумма' }), { target: { value: '20.00' } });
    fireEvent.click(screen.getByRole('button', { name: /Ещё способ/ }));
    fireEvent.change(screen.getByRole('textbox', { name: 'Получено' }), { target: { value: '80.00' } });
    fireEvent.click(screen.getByRole('button', { name: /Принять 100/ }));

    await waitFor(() => expect(requestedUrls.filter((url) => url.endsWith('/settlements'))).toHaveLength(2));
    const settlementBodies = requestedBodies.filter((body) =>
      typeof body === 'object' && body !== null && 'payments' in body) as Array<Record<string, unknown>>;
    expect(settlementBodies).toHaveLength(2);
    expect(settlementBodies[0].payments).toEqual([
      { paymentMethod: 'wallet', amount: { currencyCode: 'TJS', minorUnits: 2_000 } },
      { paymentMethod: 'cash', amount: { currencyCode: 'TJS', minorUnits: 8_000 } }
    ]);
    expect(settlementBodies[1]).toEqual(settlementBodies[0]);

    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Оплата' })).toBeNull());
    expect(screen.getByText('Корзина пуста')).toBeInTheDocument();
  });
});
