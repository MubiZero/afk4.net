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
let settlementResponseGate: Promise<Response> | null = null;
let saleReadState = 'draft';
let removeProductAfterSettlement = false;
const fetchBackend = mock(async (input: RequestInfo | URL, init?: RequestInit) => {
  const url = String(input);
  requestedUrls.push(url);
  if (init?.body) {
    requestedBodies.push(JSON.parse(String(init.body)));
  }
  if (url.endsWith('/api/branches/branch-1/pos/catalog')) {
    return jsonResponse(removeProductAfterSettlement && requestedUrls.some((item) => item.endsWith('/settlements'))
      ? []
      : [backendProduct]);
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
    if (settlementResponseGate !== null) {
      return settlementResponseGate;
    }
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
  if (url.endsWith('/api/pos/sales/sale-1') && (!init?.method || init.method === 'GET')) {
    return jsonResponse({ posSaleId: 'sale-1', state: saleReadState });
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
  settlementResponseGate = null;
  saleReadState = 'draft';
  removeProductAfterSettlement = false;
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
    accessTokenExpiresAtUtc: '2999-01-01T00:00:00Z',
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

    fireEvent.click(screen.getByRole('tab', { name: 'Карта' }));
    fireEvent.click(screen.getByRole('button', { name: /Принять 100/ }));
    await waitFor(() => expect(requestedUrls.filter((url) => url.endsWith('/settlements'))).toHaveLength(2));
    const settlementBodies = requestedBodies.filter((body) =>
      typeof body === 'object' && body !== null && 'payments' in body) as Array<Record<string, unknown>>;
    expect(settlementBodies[1].idempotencyKey).not.toBe(settlementBodies[0].idempotencyKey);
    expect(settlementBodies[1].payments).toEqual([
      { paymentMethod: 'card_manual', amount: { currencyCode: 'TJS', minorUnits: 10_000 } }
    ]);
    expect(requestedUrls.filter((url) => url.endsWith('/pos/sales'))).toHaveLength(1);
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

  it('blocks every close path while settlement is pending and reconciles an already-paid sale without a second charge', async () => {
    let releaseSettlement!: (response: Response) => void;
    settlementResponseGate = new Promise<Response>((resolve) => {
      releaseSettlement = resolve;
    });
    saleReadState = 'paid';
    renderBackendPos();
    await screen.findAllByText('Cola');

    fireEvent.click(screen.getByRole('button', { name: /Принять оплату/ }));
    fireEvent.click(screen.getByRole('button', { name: /Принять 100/ }));
    await waitFor(() => expect(requestedUrls.filter((url) => url.endsWith('/settlements'))).toHaveLength(1));

    const dialog = screen.getByRole('dialog', { name: 'Оплата' });
    const closeButton = dialog.querySelector<HTMLButtonElement>('.panel-modal-close');
    expect(closeButton).not.toBeNull();
    expect(closeButton).toBeDisabled();
    fireEvent.click(closeButton!);
    fireEvent.keyDown(document, { key: 'Escape' });
    fireEvent.pointerDown(dialog.parentElement!);
    expect(screen.getByRole('dialog', { name: 'Оплата' })).toBeInTheDocument();

    releaseSettlement(new Response(JSON.stringify({ error: 'version_conflict' }), {
      status: 409,
      statusText: 'Conflict',
      headers: { 'Content-Type': 'application/json' }
    }));

    await waitFor(() => expect(requestedUrls).toContain('http://test/api/pos/sales/sale-1'));
    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Оплата' })).toBeNull());
    expect(requestedUrls.filter((url) => url.endsWith('/pos/sales'))).toHaveLength(1);
    expect(requestedUrls.filter((url) => url.endsWith('/settlements'))).toHaveLength(1);
    expect(screen.getByText('Корзина пуста')).toBeInTheDocument();
  });

  it('keeps the cart snapshot when conflict refresh no longer contains its product', async () => {
    settlementFailuresRemaining = 1;
    removeProductAfterSettlement = true;
    renderBackendPos();
    await screen.findAllByText('Cola');

    fireEvent.click(screen.getByRole('button', { name: /Принять оплату/ }));
    fireEvent.click(screen.getByRole('button', { name: /Принять 100/ }));

    await waitFor(() => expect(requestedUrls).toContain('http://test/api/pos/sales/sale-1'));
    expect(screen.getByRole('dialog', { name: 'Оплата' })).toBeInTheDocument();
    expect(screen.getAllByText('Cola')).toHaveLength(2);
    expect(screen.getByRole('textbox', { name: 'Получено' })).toHaveValue('100.00');
    expect(await screen.findByText('Данные продажи изменились. Проверьте корзину и повторите оплату.')).toBeInTheDocument();
  });

  it('keeps an ambiguous attempt locked and retries the same sale and key after close attempts', async () => {
    settlementNetworkFailuresRemaining = 2;
    renderBackendPos();
    await screen.findAllByText('Cola');

    fireEvent.click(screen.getByRole('button', { name: /Принять оплату/ }));
    fireEvent.click(screen.getByRole('button', { name: /Принять 100/ }));
    await waitFor(() => expect(requestedUrls.filter((url) => url.endsWith('/settlements'))).toHaveLength(2));

    const dialog = screen.getByRole('dialog', { name: 'Оплата' });
    const closeButton = dialog.querySelector<HTMLButtonElement>('.panel-modal-close')!;
    expect(closeButton).toBeDisabled();
    const cancelButtons = screen.getAllByRole('button', { name: 'Отмена' });
    expect(cancelButtons).toHaveLength(2);
    expect(cancelButtons[1]).toBeDisabled();
    fireEvent.keyDown(document, { key: 'Escape' });
    fireEvent.pointerDown(dialog.parentElement!);
    expect(screen.getByRole('dialog', { name: 'Оплата' })).toBeInTheDocument();

    // The unresolved attempt owns an immutable cash snapshot. Even if the
    // operator tries to switch the visible draft to card, retry must not pair
    // a changed payload with the original idempotency key.
    const cardTab = screen.getByRole('tab', { name: 'Карта' });
    expect(cardTab).toBeDisabled();
    fireEvent.click(cardTab);
    fireEvent.click(screen.getByRole('button', { name: /Принять 100/ }));
    await waitFor(() => expect(requestedUrls.filter((url) => url.endsWith('/settlements'))).toHaveLength(3));
    const saleBodies = requestedBodies.filter((body) =>
      typeof body === 'object' && body !== null && 'lines' in body) as Array<Record<string, unknown>>;
    const settlementBodies = requestedBodies.filter((body) =>
      typeof body === 'object' && body !== null && 'payments' in body) as Array<Record<string, unknown>>;
    expect(saleBodies).toHaveLength(1);
    expect(settlementBodies).toHaveLength(3);
    expect(settlementBodies[1]).toEqual(settlementBodies[0]);
    expect(settlementBodies[2]).toEqual(settlementBodies[0]);
    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Оплата' })).toBeNull());
  });

  for (const terminalState of ['voided', 'refunded']) {
    it(`closes the ${terminalState} sale attempt after 409 reconciliation without a retry loop`, async () => {
      settlementFailuresRemaining = 1;
      saleReadState = terminalState;
      renderBackendPos();
      await screen.findAllByText('Cola');

      fireEvent.click(screen.getByRole('button', { name: /Принять оплату/ }));
      fireEvent.click(screen.getByRole('button', { name: /Принять 100/ }));

      await waitFor(() => expect(requestedUrls).toContain('http://test/api/pos/sales/sale-1'));
      await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Оплата' })).toBeNull());
      expect(requestedUrls.filter((url) => url.endsWith('/settlements'))).toHaveLength(1);
      expect(screen.getByText('Продажа уже завершена или отменена. Проверьте чек и создайте новую продажу.')).toBeInTheDocument();
      expect(screen.getAllByText('Cola').length).toBeGreaterThanOrEqual(1);

      fireEvent.click(screen.getByRole('button', { name: /Принять оплату/ }));
      fireEvent.click(screen.getByRole('button', { name: /Принять 100/ }));
      await waitFor(() => expect(requestedUrls.filter((url) => url.endsWith('/pos/sales'))).toHaveLength(2));
      const createBodies = requestedBodies.filter((body) =>
        typeof body === 'object' && body !== null && 'lines' in body) as Array<Record<string, unknown>>;
      expect(createBodies[1].idempotencyKey).not.toBe(createBodies[0].idempotencyKey);
    });
  }
});
