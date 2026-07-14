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
  price: { currencyCode: 'TJS', minorUnits: 1200 },
  trackStock: true,
  stockOnHand: 4,
  reorderThreshold: 2,
  barcodes: []
};
const linkedPlayer = {
  playerAccountId: 'player-1',
  displayName: 'Амир Алиев',
  phoneNumber: '+992900000001',
  walletBalanceMinorUnits: 25_000,
  debtBalanceMinorUnits: 0,
  activePackageCount: 0,
  isActive: true,
  createdAtUtc: '2026-07-14T08:00:00Z',
  lastActivityAtUtc: null,
  activePackageName: null,
  activePackageRemainingMinutes: 0
};
const requestedUrls: string[] = [];
const fetchBackend = mock(async (input: RequestInfo | URL) => {
  const url = String(input);
  requestedUrls.push(url);
  if (url.endsWith('/api/branches/branch-1/pos/catalog')) {
    return jsonResponse([backendProduct]);
  }
  if (url.endsWith('/api/branches/branch-1/shifts/current')) {
    return jsonResponse({ shiftId: 'shift-1' });
  }
  if (url.includes('/api/branches/branch-1/players?')) {
    return jsonResponse([linkedPlayer]);
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
    permissions: ['players.view']
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
});
