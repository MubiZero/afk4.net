import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { App } from './App';
import type { HostBridgeMessageEvent } from './hostBridge';

vi.mock('./operatorRealtime', async (importOriginal) => {
  const actual = await importOriginal<typeof import('./operatorRealtime')>();
  return {
    ...actual,
    createOperatorRealtimeClient: vi.fn((options: {
      onConnectionStateChanged?: (state: string) => void;
      onDeviceStatusChanged: (status: unknown) => void;
    }) => ({
      start: vi.fn(async () => options.onConnectionStateChanged?.('connected')),
      stop: vi.fn(async () => options.onConnectionStateChanged?.('disconnected'))
    }))
  };
});

describe('App', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(mockPlatformFetch));
  });

  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
    delete window.chrome;
    delete window.__AFK4_OPERATOR_CONFIG__;
    localStorage.clear();
    sessionStorage.clear();
    vi.clearAllMocks();
  });

  it('opens on the floor map operator workspace after native session restore', async () => {
    installSessionBridge();

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    expect(await screen.findByText('Backend live')).toBeInTheDocument();
    expect(screen.getByRole('navigation', { name: 'Рабочие места' })).toBeInTheDocument();
    expect(screen.getByLabelText('ПК зала')).toBeInTheDocument();
    expect(screen.getAllByText('Сессии').length).toBeGreaterThan(0);
    expect(screen.getByRole('button', { name: /Техрежим/ })).toBeInTheDocument();
    expect(screen.getByText('Сессия активна')).toBeInTheDocument();
    expect(screen.getByText('Сессия подтверждена')).toBeInTheDocument();
    expect(await screen.findByRole('button', { name: /15 мин/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Свернуть' })).toBeInTheDocument();
    expect(screen.getByText(/Cashier One/)).toBeInTheDocument();
  });

  it('filters the floor map and switches to table view', async () => {
    installSessionBridge();

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    expect(await screen.findByText('Backend live')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /Свободно/ }));

    expect(await screen.findByRole('heading', { name: 'PC-02' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /PC-01/ })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Таблица' }));

    expect(await screen.findByRole('table', { name: 'Таблица ПК' })).toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: 'Команда' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'PC-02' })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /Сессии/ }));
    expect(await screen.findByRole('button', { name: 'PC-01' })).toBeInTheDocument();
  });

  it('uses the host currency in money surfaces', async () => {
    installSessionBridge();
    window.__AFK4_OPERATOR_CONFIG__ = {
      runtime: 'webview2',
      shellMode: 'vite-dist',
      platformBaseUrl: 'https://afk4.staging.mubi.dev/',
      currencyCode: 'USD'
    };

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    expect(screen.getByText('4 820 USD')).toBeInTheDocument();
    expect(screen.getAllByText(/Депозит/).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/USD/).length).toBeGreaterThan(0);
  });

  it('signs in through the native bridge before showing operator workspaces', async () => {
    installSessionBridge(null);

    render(<App />);

    expect(await screen.findByRole('heading', { name: 'Вход оператора' })).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('Пользователь'), { target: { value: 'cashier' } });
    fireEvent.change(screen.getByLabelText('Пароль'), { target: { value: 'password' } });
    fireEvent.click(screen.getByRole('button', { name: 'Войти' }));

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    expect(localStorage.length).toBe(0);
    expect(sessionStorage.length).toBe(0);
  });

  it('ends the selected active session through the backend before confirming the UI action', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    expect(await screen.findByText('Backend live')).toBeInTheDocument();
    fireEvent.click(await screen.findByRole('button', { name: /Стоп/ }));

    expect((await screen.findAllByText(/Стоп: lock: pending/)).length).toBeGreaterThan(0);
    const postCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/sessions/22222222-2222-2222-2222-222222222222/end') &&
      init?.method === 'POST');
    expect(postCall).toBeDefined();
    const body = JSON.parse(String(postCall?.[1]?.body));
    expect(body.reason).toBe('operator');
    expect(body.idempotencyKey).toMatch(/^session-end-/);
  });

  it('starts a ready seat as a fast guest session through the backend', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    expect(await screen.findByText('Backend live')).toBeInTheDocument();
    fireEvent.click(await screen.findByRole('button', { name: /PC-02/ }));
    const startButton = await screen.findByRole('button', { name: /Старт 60 мин/ });
    expect(startButton).toBeEnabled();
    fireEvent.click(startButton);

    expect((await screen.findAllByText(/Старт 60 мин: unlock: pending/)).length).toBeGreaterThan(0);
    const postCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/sessions/start') &&
      init?.method === 'POST');
    expect(postCall).toBeDefined();
    const body = JSON.parse(String(postCall?.[1]?.body));
    expect(body).toMatchObject({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      seatId: 'cccccccc-cccc-cccc-cccc-cccccccccccc',
      durationMinutes: 60,
      tariffRuleVersionId: 'manual-v1',
      billingMode: ''
    });
    expect(body.idempotencyKey).toMatch(/^session-start-/);
  });

  it('starts a ready seat with player billing metadata and command status', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    expect(await screen.findByText('Backend live')).toBeInTheDocument();
    fireEvent.click(await screen.findByRole('button', { name: /PC-02/ }));
    fireEvent.click(await screen.findByRole('button', { name: /Депозит/ }));
    fireEvent.change(screen.getByLabelText('Игрок для биллинга'), { target: { value: 'Madina' } });
    fireEvent.click(await screen.findByRole('button', { name: /Madina S\./ }));
    expect(await screen.findByText('Депозит готов')).toBeInTheDocument();

    const startButton = await screen.findByRole('button', { name: /Старт 60 мин/ });
    expect(startButton).toBeEnabled();
    fireEvent.click(startButton);

    expect((await screen.findAllByText(/Старт 60 мин: unlock: pending/)).length).toBeGreaterThan(0);
    const postCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/sessions/start') &&
      init?.method === 'POST');
    expect(postCall).toBeDefined();
    const body = JSON.parse(String(postCall?.[1]?.body));
    expect(body).toMatchObject({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      seatId: 'cccccccc-cccc-cccc-cccc-cccccccccccc',
      durationMinutes: 60,
      tariffRuleVersionId: 'standard-v1',
      billingMode: 'prepaid_wallet',
      playerAccountId: '12121212-1212-1212-1212-121212121212',
      tariffVersionId: '17171717-1717-1717-1717-171717171717'
    });
    expect(fetchMock.mock.calls.some(([input]) =>
      String(input).includes('/api/devices/33333333-3333-3333-3333-333333333333/commands/44444444-4444-4444-4444-444444444444/status'))).toBe(true);
  });

  it('disables unauthorized workspaces and selected-seat actions', async () => {
    installSessionBridge(createSession({ permissions: ['floor_map.view'] }));

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    expect(await screen.findByText('Backend live')).toBeInTheDocument();
    expect(screen.getByTitle('POS')).toBeDisabled();
    expect(screen.getByTitle('Брони')).toBeDisabled();
    expect(screen.getByRole('button', { name: /15 мин/ })).toBeDisabled();
    expect(screen.getByRole('button', { name: /Стоп/ })).toBeDisabled();
    expect(screen.getByText('Нет прав на действия с сессией')).toBeInTheDocument();
  });

  it('switches to SmartShell-like booking, POS, and logs workspaces', async () => {
    installSessionBridge();

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Дашборд'));
    expect(screen.getByRole('heading', { name: /Что требует внимания/ })).toBeInTheDocument();
    expect(screen.getByText('Главный фокус')).toBeInTheDocument();
    expect((await screen.findAllByText('lock Failed')).length).toBeGreaterThan(0);
    expect(screen.getByRole('button', { name: 'Сегодня' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Неделя' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Месяц' })).toBeInTheDocument();
    expect(screen.getByLabelText('Начало периода')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Экспорт дашборда за/ })).toBeInTheDocument();
    expect(screen.getByText('Пульс смены')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Неделя' }));
    expect(screen.getByText('1 чек')).toBeInTheDocument();

    fireEvent.click(screen.getByTitle('Брони'));
    const bookingHead = screen.getByRole('heading', { name: /Брони/ }).closest('.screen-head');
    expect(bookingHead).toBeInTheDocument();
    expect(bookingHead).not.toHaveTextContent('Сегодня');
    expect(bookingHead).not.toHaveTextContent('Завтра');
    expect(bookingHead).not.toHaveTextContent('Неделя');
    expect(screen.getByText('Лента броней')).toBeInTheDocument();
    expect(screen.getByText('Выбранная бронь')).toBeInTheDocument();
    expect(screen.getByText('Онлайн-заявки')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Открыть карту/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Создать бронь/ })).toBeInTheDocument();

    fireEvent.click(screen.getByTitle('POS'));
    const posHead = screen.getByRole('heading', { name: /POS/ }).closest('.screen-head');
    expect(posHead).toBeInTheDocument();
    expect(posHead).not.toHaveTextContent('Продажа');
    expect(posHead).not.toHaveTextContent('Возврат');
    expect(posHead).not.toHaveTextContent('Склад');
    expect(posHead).not.toHaveTextContent('История');
    expect(screen.getByText('Каталог')).toBeInTheDocument();
    expect(screen.getByText('Корзина')).toBeInTheDocument();
    expect(screen.getByText('Оплата')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Принять оплату/ })).toBeInTheDocument();
    expect(screen.getByText('Последние чеки')).toBeInTheDocument();
    expect(screen.getByText('Быстрые операции')).toBeInTheDocument();

    fireEvent.click(screen.getByTitle('Клиенты'));
    const clientsHead = screen.getByRole('heading', { name: /Клиенты/ }).closest('.screen-head');
    expect(clientsHead).toBeInTheDocument();
    expect(clientsHead).not.toHaveTextContent('Все');
    expect(clientsHead).not.toHaveTextContent('VIP');
    expect(clientsHead).not.toHaveTextContent('Долги');
    expect(screen.getByText('Список клиентов')).toBeInTheDocument();
    expect(screen.getByText('Карточка клиента')).toBeInTheDocument();
    expect(screen.getByText('Операции')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Пополнить депозит/ })).toBeInTheDocument();
    expect(screen.getByText('История клиента')).toBeInTheDocument();

    fireEvent.click(screen.getByTitle('Платежи'));
    const paymentsHead = screen.getByRole('heading', { name: /Платежи/ }).closest('.screen-head');
    expect(paymentsHead).toBeInTheDocument();
    expect(paymentsHead).not.toHaveTextContent('Смена');
    expect(paymentsHead).not.toHaveTextContent('Операции');
    expect(paymentsHead).not.toHaveTextContent('Сверка');
    expect(paymentsHead).not.toHaveTextContent('Экспорт');
    expect(screen.getByText('Операции смены')).toBeInTheDocument();
    expect(screen.getByText('Итоги смены')).toBeInTheDocument();
    expect(screen.getByText('Сверка кассы')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Подготовить закрытие/ })).toBeInTheDocument();
    expect(screen.getByText('Методы оплаты')).toBeInTheDocument();

    fireEvent.click(screen.getByTitle('Логи'));
    const logsHead = screen.getByRole('heading', { name: /Логи/ }).closest('.screen-head');
    expect(logsHead).toBeInTheDocument();
    expect(logsHead).not.toHaveTextContent('Смена');
    expect(logsHead).not.toHaveTextContent('Ошибки');
    expect(logsHead).not.toHaveTextContent('Аудит');
    expect(logsHead).not.toHaveTextContent('Экспорт');
    expect(screen.getByText('Журнал событий')).toBeInTheDocument();
    expect(screen.getByText('Детали события')).toBeInTheDocument();
    expect(screen.getByText('Фильтры')).toBeInTheDocument();
    expect(screen.getByText('Аудит смены')).toBeInTheDocument();
    expect(screen.getByText('Источники')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Audit trail/ })).toBeInTheDocument();

    fireEvent.click(screen.getByTitle('Настройки'));
    const settingsHead = screen.getByRole('heading', { name: /Настройки/ }).closest('.screen-head');
    expect(settingsHead).toBeInTheDocument();
    expect(settingsHead).not.toHaveTextContent('Основное');
    expect(settingsHead).not.toHaveTextContent('Залы и ПК');
    expect(screen.getAllByText('Профиль клуба').length).toBeGreaterThan(0);
    fireEvent.click(screen.getByRole('button', { name: /Залы и ПК/ }));
    expect(screen.getByText('Залы и рабочие места')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /^Тарифы/ }));
    expect(screen.getAllByText('Тарифы').length).toBeGreaterThan(0);
    expect(screen.getByText('Готовность клуба')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Пригласить сотрудника/ })).toBeInTheDocument();
  });

  it('confirms POS payment only after backend sale and manual payment calls resolve', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('POS'));
    expect(await screen.findByText('Backend live')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /Принять оплату/ }));

    expect(await screen.findByText('Оплата: подтверждено')).toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/pos/sales') &&
      init?.method === 'POST')).toBe(true);
    expect(fetchMock.mock.calls.some(([input, init]) =>
      String(input).includes('/api/pos/sales/99999999-9999-9999-9999-999999999999/payments/manual') &&
      init?.method === 'POST')).toBe(true);
  });

  it('confirms booking create and cancel only after reservation backend calls resolve', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Брони'));
    expect(await screen.findByText('Данные платформы')).toBeInTheDocument();
    expect(screen.getAllByText('Aziz P.').length).toBeGreaterThan(0);

    fireEvent.click(screen.getByRole('button', { name: /^Создать бронь$/ }));
    expect(await screen.findByText('Создать бронь: подтверждено')).toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/reservations') &&
      init?.method === 'POST')).toBe(true);

    fireEvent.click(screen.getByRole('button', { name: /Отменить/ }));
    expect(await screen.findByText('Отменить бронь: подтверждено')).toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([input, init]) =>
      String(input).includes('/api/reservations/99999999-9999-9999-9999-999999999999/cancel') &&
      init?.method === 'POST')).toBe(true);
  });

  it('keeps booking mutation controls disabled without reservation manage permission', async () => {
    installSessionBridge(createSession({ permissions: ['floor_map.view', 'reservations.view'] }));

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Брони'));
    expect(await screen.findByText('Данные платформы')).toBeInTheDocument();

    expect(screen.getByRole('button', { name: 'Создать' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Создать бронь' })).toBeDisabled();
    expect(screen.getByRole('button', { name: /Посадить/ })).toBeDisabled();
    expect(screen.getByRole('button', { name: /Перенести/ })).toBeDisabled();
    expect(screen.getByRole('button', { name: /Отменить/ })).toBeDisabled();
    expect(screen.getByRole('button', { name: /Принять/ })).toBeDisabled();
  });

  it('creates a reservation from the selected backend player card', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Клиенты'));
    expect(await screen.findByText('Backend live')).toBeInTheDocument();
    expect((await screen.findAllByText('Madina S.')).length).toBeGreaterThan(0);
    fireEvent.click(screen.getByRole('button', { name: /Создать бронь/ }));

    expect(await screen.findByText('Создать бронь: подтверждено')).toBeInTheDocument();
    const reservationCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/reservations') &&
      init?.method === 'POST');
    expect(reservationCall).toBeDefined();
    const body = JSON.parse(String(reservationCall?.[1]?.body));
    expect(body).toMatchObject({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      playerAccountId: '12121212-1212-1212-1212-121212121212',
      customerName: 'Madina S.',
      source: 'operator'
    });
  });
});

async function mockPlatformFetch(input: RequestInfo | URL, init?: RequestInit): Promise<Response> {
  const url = new URL(String(input));
  const pathname = url.pathname;

  if (pathname.endsWith('/floor-map')) {
    return jsonResponse(createFloorMap());
  }

  if (pathname.endsWith('/sessions/start') && init?.method === 'POST') {
    return jsonResponse(createSessionCommandResponse('unlock', '33333333-3333-3333-3333-333333333333'));
  }

  if (pathname.includes('/sessions/') && pathname.endsWith('/extend')) {
    return jsonResponse(createSessionCommandResponse('unlock', '11111111-1111-1111-1111-111111111111'));
  }

  if (pathname.includes('/sessions/') && pathname.endsWith('/transfer')) {
    return jsonResponse(createSessionCommandResponse('transfer', '11111111-1111-1111-1111-111111111111'));
  }

  if (pathname.includes('/sessions/') && pathname.endsWith('/end')) {
    return jsonResponse(createSessionCommandResponse('lock', '11111111-1111-1111-1111-111111111111'));
  }

  if (pathname.includes('/commands/') && pathname.endsWith('/status')) {
    const isLock = pathname.includes('11111111-1111-1111-1111-111111111111');
    return jsonResponse(createDeviceCommandStatus(isLock ? 'lock' : 'unlock', isLock
      ? 'Agent accepted lock'
      : 'Agent accepted unlock'));
  }

  if (pathname.endsWith('/dashboard/summary')) {
    return jsonResponse(createDashboardSummary());
  }

  if (pathname.endsWith('/reservations') && init?.method === 'POST') {
    return jsonResponse(createReservation({ state: 'confirmed', source: 'operator' }));
  }

  if (pathname.includes('/reservations/') && pathname.endsWith('/cancel')) {
    return jsonResponse(createReservation({ state: 'cancelled', source: 'online', cancelReason: 'Отменено оператором' }));
  }

  if (pathname.includes('/reservations/') && pathname.endsWith('/seat')) {
    return jsonResponse(createReservation({ state: 'seated', source: 'online' }));
  }

  if (pathname.includes('/reservations/') && pathname.endsWith('/confirm')) {
    return jsonResponse(createReservation({ state: 'confirmed', source: 'online' }));
  }

  if (pathname.includes('/reservations/') && init?.method === 'PATCH') {
    return jsonResponse(createReservation({ state: 'confirmed', source: 'operator', seatName: 'PC-02' }));
  }

  if (pathname.endsWith('/reservations')) {
    return jsonResponse(createReservationSearch());
  }

  if (pathname.endsWith('/pos/catalog')) {
    return jsonResponse(createPosCatalog());
  }

  if (pathname.endsWith('/shifts/current')) {
    return jsonResponse(createCurrentShift());
  }

  if (pathname.endsWith('/reports/sales')) {
    return jsonResponse(createSalesReport());
  }

  if (pathname.endsWith('/reports/cash-operations')) {
    return jsonResponse(createCashReport());
  }

  if (pathname.endsWith('/reports/shifts')) {
    return jsonResponse(createShiftReport());
  }

  if (pathname.endsWith('/reports/operator-actions')) {
    return jsonResponse({ rows: [], limit: 50 });
  }

  if (pathname.endsWith('.csv')) {
    return new Response('csv', { status: 200 });
  }

  if (pathname.endsWith('/pos/sales') && init?.method === 'POST') {
    return jsonResponse(createPosSale('draft'));
  }

  if (pathname.includes('/payments/manual')) {
    return jsonResponse(createPosSale('paid'));
  }

  if (pathname.endsWith('/players')) {
    return jsonResponse(createPlayers());
  }

  if (pathname.endsWith('/wallet-summary')) {
    return jsonResponse(createWalletSummary());
  }

  if (pathname.endsWith('/packages')) {
    return jsonResponse(createPlayerPackages());
  }

  if (pathname.endsWith('/staff')) {
    return jsonResponse(createStaffUsers());
  }

  if (pathname.endsWith('/layout/zones')) {
    return jsonResponse(createZones());
  }

  if (pathname.endsWith('/diagnostics')) {
    return jsonResponse(createDiagnostics());
  }

  if (pathname.endsWith('/updates/rollouts')) {
    return jsonResponse(createRollouts());
  }

  if (pathname.endsWith('/tariffs/options')) {
    return jsonResponse(createTariffs());
  }

  if (pathname.endsWith('/packages/options')) {
    return jsonResponse([]);
  }

  if (pathname.endsWith('/audit')) {
    return jsonResponse(createAudit());
  }

  return jsonResponse({ ok: true });
}

function installSessionBridge(loadSession: ReturnType<typeof createSession> | null = createSession()) {
  const listeners = new Set<(event: HostBridgeMessageEvent) => void>();
  window.chrome = {
    webview: {
      postMessage: (message: unknown) => {
        const request = message as { type: string; requestId: string };
        let payload: unknown = loadSession;

        if (request.type === 'auth:signIn') {
          payload = createSession();
        }

        if (request.type === 'auth:signOut') {
          payload = { signedOut: true };
        }

        queueMicrotask(() => {
          for (const listener of listeners) {
            listener({
              data: {
                type: 'host:response',
                requestId: request.requestId,
                ok: true,
                payload
              }
            });
          }
        });
      },
      addEventListener: (_type, listener) => listeners.add(listener),
      removeEventListener: (_type, listener) => listeners.delete(listener)
    }
  };
}

const allOperatorPermissions = [
  'floor_map.view',
  'sessions.start',
  'sessions.extend',
  'sessions.transfer',
  'sessions.end',
  'players.view',
  'billing.view',
  'shifts.view',
  'reports.view',
  'reservations.view',
  'reservations.manage',
  'pos.sales.create',
  'pos.sales.pay',
  'inventory.view',
  'receipts.view',
  'diagnostics.view',
  'identity.branch_staff.manage',
  'layout.manage',
  'tariffs.view',
  'updates.status.view',
  'devices.commands.status.view',
  'audit.view'
];

function createSession(overrides: Record<string, unknown> = {}) {
  return {
    staffUserId: '3db1367b-88c6-4b1c-99c3-bcbb5f4d5134',
    organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
    displayName: 'Cashier One',
    accessToken: 'access-token',
    accessTokenExpiresAtUtc: '2026-05-14T10:00:00Z',
    refreshTokenExpiresAtUtc: '2026-05-15T10:00:00Z',
    branchIds: ['acfc0212-967f-4d84-94be-9003387b09c2'],
    activeBranchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
    permissions: allOperatorPermissions,
    ...overrides
  };
}

function createFloorMap() {
  return {
    branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
    branchName: 'AFK4 Dushanbe · зал A',
    seats: [
      {
        seatId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        seatName: 'PC-01',
        zoneId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
        zoneName: 'Зал A',
        sortOrder: 10,
        state: 'Active',
        deviceId: '11111111-1111-1111-1111-111111111111',
        deviceName: 'PC-01',
        isDeviceOnline: true,
        isDeviceLocked: false,
        lastHeartbeatAtUtc: '2026-05-21T10:00:00Z',
        agentVersion: '0.4',
        shellVersion: '0.4',
        activeSessionId: '22222222-2222-2222-2222-222222222222',
        remainingSeconds: 2580
      },
      {
        seatId: 'cccccccc-cccc-cccc-cccc-cccccccccccc',
        seatName: 'PC-02',
        zoneId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
        zoneName: 'Зал A',
        sortOrder: 20,
        state: 'Locked',
        deviceId: '33333333-3333-3333-3333-333333333333',
        deviceName: 'PC-02',
        isDeviceOnline: true,
        isDeviceLocked: true,
        lastHeartbeatAtUtc: '2026-05-21T10:00:00Z',
        agentVersion: '0.4',
        shellVersion: '0.4',
        activeSessionId: null,
        remainingSeconds: null
      }
    ]
  };
}

function createSessionCommandResponse(type: string, deviceId: string) {
  return {
    idempotencyKey: 'test-idempotency-key',
    session: {
      sessionId: '22222222-2222-2222-2222-222222222222',
      deviceId
    },
    deviceCommands: [
      {
        commandId: '44444444-4444-4444-4444-444444444444',
        deviceId,
        type,
        status: 'pending',
        message: 'Queued for Agent',
        createdAtUtc: '2026-05-21T10:00:00Z',
        updatedAtUtc: '2026-05-21T10:00:00Z'
      }
    ]
  };
}

function createDeviceCommandStatus(type: string, message: string) {
  return {
    deviceId: type === 'lock'
      ? '11111111-1111-1111-1111-111111111111'
      : '33333333-3333-3333-3333-333333333333',
    commandId: '44444444-4444-4444-4444-444444444444',
    type,
    status: 'pending',
    message,
    createdAtUtc: '2026-05-21T10:00:00Z',
    updatedAtUtc: '2026-05-21T10:00:01Z'
  };
}

function createDashboardSummary() {
  return {
    organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
    branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
    fromUtc: '2026-05-21T00:00:00Z',
    toUtc: '2026-05-21T23:59:59Z',
    generatedAtUtc: '2026-05-21T12:00:00Z',
    shift: {
      shiftId: '66666666-6666-6666-6666-666666666666',
      state: 'open',
      openedAtUtc: '2026-05-21T08:00:00Z',
      openedByStaffUserId: '3db1367b-88c6-4b1c-99c3-bcbb5f4d5134',
      expectedCash: { currencyCode: 'TJS', minorUnits: 112000 }
    },
    revenue: {
      posNetSales: { currencyCode: 'TJS', minorUnits: 1200 },
      gameplayRevenue: { currencyCode: 'TJS', minorUnits: 4800 },
      totalRevenue: { currencyCode: 'TJS', minorUnits: 6000 },
      posCheckCount: 1,
      newPlayerCount: 1
    },
    utilization: {
      totalSeats: 2,
      activeSessions: 1,
      endingSessions: 0,
      onlineDevices: 2,
      offlineDevices: 0,
      sessionStarts: 2,
      utilizationPercent: 50
    },
    alertPressure: {
      pendingCommands: 0,
      failedCommands: 1,
      offlineDevices: 0,
      endingSessions: 0,
      totalAlerts: 1
    },
    reservations: {
      activeReservations: 0,
      availableSlots: 1,
      source: 'floor-map-availability'
    },
    focusQueue: [
      {
        tone: 'blocking',
        target: 'PC-02',
        title: 'lock Failed',
        detail: 'Agent did not confirm lock.',
        seatId: 'cccccccc-cccc-cccc-cccc-cccccccccccc',
        deviceId: '33333333-3333-3333-3333-333333333333',
        createdAtUtc: '2026-05-21T10:00:00Z',
        sourceType: 'device-command'
      }
    ],
    recentPayments: [
      {
        paymentId: '19191919-1919-1919-1919-191919191919',
        posSaleId: '99999999-9999-9999-9999-999999999999',
        shiftId: '66666666-6666-6666-6666-666666666666',
        createdByStaffUserId: '3db1367b-88c6-4b1c-99c3-bcbb5f4d5134',
        paymentKind: 'payment',
        paymentMethod: 'cash',
        amount: { currencyCode: 'TJS', minorUnits: 1200 },
        createdAtUtc: '2026-05-21T09:01:00Z'
      }
    ]
  };
}

function createReservationSearch() {
  return {
    reservations: [
      createReservation({ state: 'pending', source: 'online' })
    ],
    limit: 40
  };
}

function createReservation(overrides: Record<string, unknown> = {}) {
  return {
    reservationId: '99999999-9999-9999-9999-999999999999',
    organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
    branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
    playerAccountId: null,
    seatId: 'cccccccc-cccc-cccc-cccc-cccccccccccc',
    seatName: 'PC-01',
    zoneName: 'Main',
    customerName: 'Aziz P.',
    phoneNumber: '+992900000001',
    startsAtUtc: '2026-05-21T16:00:00Z',
    endsAtUtc: '2026-05-21T17:00:00Z',
    durationMinutes: 60,
    state: 'pending',
    source: 'online',
    note: 'online request',
    createdAtUtc: '2026-05-21T10:00:00Z',
    updatedAtUtc: '2026-05-21T10:00:00Z',
    cancelledAtUtc: null,
    cancelReason: '',
    ...overrides
  };
}

function createPosCatalog() {
  return [
    {
      productId: '77777777-7777-7777-7777-777777777777',
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
      categoryId: '88888888-8888-8888-8888-888888888888',
      name: 'Cola 0.5',
      sku: 'COLA-05',
      price: { currencyCode: 'TJS', minorUnits: 1200 },
      trackStock: true,
      allowNegativeStock: false,
      isActive: true,
      stockOnHand: 12,
      createdAtUtc: '2026-05-21T08:00:00Z'
    }
  ];
}

function createCurrentShift() {
  return {
    shiftId: '66666666-6666-6666-6666-666666666666',
    organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
    branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
    openedByStaffUserId: '3db1367b-88c6-4b1c-99c3-bcbb5f4d5134',
    closedByStaffUserId: null,
    state: 'open',
    startingCash: { currencyCode: 'TJS', minorUnits: 100000 },
    countedCash: null,
    expectedCash: { currencyCode: 'TJS', minorUnits: 112000 },
    difference: { currencyCode: 'TJS', minorUnits: 0 },
    openingNote: 'test',
    closingNote: '',
    openedAtUtc: '2026-05-21T08:00:00Z',
    closedAtUtc: null
  };
}

function createPosSale(state: string) {
  return {
    posSaleId: '99999999-9999-9999-9999-999999999999',
    organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
    branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
    shiftId: '66666666-6666-6666-6666-666666666666',
    state,
    lines: [],
    total: { currencyCode: 'TJS', minorUnits: 1200 },
    createdByStaffUserId: '3db1367b-88c6-4b1c-99c3-bcbb5f4d5134',
    createdAtUtc: '2026-05-21T09:00:00Z',
    paidAtUtc: state === 'paid' ? '2026-05-21T09:01:00Z' : null,
    refundedAtUtc: null,
    voidedAtUtc: null
  };
}

function createSalesReport() {
  return {
    rows: [
      {
        posSaleId: '99999999-9999-9999-9999-999999999999',
        organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
        branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
        shiftId: '66666666-6666-6666-6666-666666666666',
        createdByStaffUserId: '3db1367b-88c6-4b1c-99c3-bcbb5f4d5134',
        state: 'paid',
        total: { currencyCode: 'TJS', minorUnits: 1200 },
        paidAmount: { currencyCode: 'TJS', minorUnits: 1200 },
        refundAmount: { currencyCode: 'TJS', minorUnits: 0 },
        lineCount: 1,
        itemQuantity: 1,
        createdAtUtc: '2026-05-21T09:00:00Z',
        paidAtUtc: '2026-05-21T09:01:00Z',
        refundedAtUtc: null,
        voidedAtUtc: null
      }
    ],
    limit: 8,
    grossSalesTotal: { currencyCode: 'TJS', minorUnits: 1200 },
    refundsTotal: { currencyCode: 'TJS', minorUnits: 0 },
    netSalesTotal: { currencyCode: 'TJS', minorUnits: 1200 }
  };
}

function createCashReport() {
  return {
    rows: [
      {
        operationId: 'aaaaaaaa-0000-0000-0000-000000000001',
        organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
        branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
        shiftId: '66666666-6666-6666-6666-666666666666',
        createdByStaffUserId: '3db1367b-88c6-4b1c-99c3-bcbb5f4d5134',
        sourceType: 'shift',
        operationType: 'opening',
        cashImpact: { currencyCode: 'TJS', minorUnits: 100000 },
        reason: 'test',
        createdAtUtc: '2026-05-21T08:00:00Z'
      }
    ],
    limit: 8,
    cashInTotal: { currencyCode: 'TJS', minorUnits: 100000 },
    cashOutTotal: { currencyCode: 'TJS', minorUnits: 0 },
    netCashTotal: { currencyCode: 'TJS', minorUnits: 100000 }
  };
}

function createShiftReport() {
  return {
    rows: [
      {
        shiftId: '66666666-6666-6666-6666-666666666666',
        organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
        branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
        openedByStaffUserId: '3db1367b-88c6-4b1c-99c3-bcbb5f4d5134',
        closedByStaffUserId: null,
        state: 'open',
        startingCash: { currencyCode: 'TJS', minorUnits: 100000 },
        cashMovementsTotal: { currencyCode: 'TJS', minorUnits: 0 },
        posCashPaymentsTotal: { currencyCode: 'TJS', minorUnits: 1200 },
        posRefundsTotal: { currencyCode: 'TJS', minorUnits: 0 },
        billingCashImpactTotal: { currencyCode: 'TJS', minorUnits: 0 },
        expectedCash: { currencyCode: 'TJS', minorUnits: 112000 },
        countedCash: null,
        difference: { currencyCode: 'TJS', minorUnits: 0 },
        openedAtUtc: '2026-05-21T08:00:00Z',
        closedAtUtc: null
      }
    ],
    limit: 6
  };
}

function createPlayers() {
  return [
    {
      playerAccountId: '12121212-1212-1212-1212-121212121212',
      displayName: 'Madina S.',
      phoneNumber: '+992 90 555 22 11',
      walletBalanceMinorUnits: 46000,
      debtBalanceMinorUnits: 0,
      activePackageCount: 1,
      isActive: true
    }
  ];
}

function createWalletSummary() {
  return {
    playerAccountId: '12121212-1212-1212-1212-121212121212',
    walletBalance: { currencyCode: 'TJS', minorUnits: 46000 },
    debtBalance: { currencyCode: 'TJS', minorUnits: 0 },
    recentEntries: [
      {
        ledgerEntryId: '13131313-1313-1313-1313-131313131313',
        entryType: 'top_up',
        amount: { currencyCode: 'TJS', minorUnits: 20000 },
        createdAtUtc: '2026-05-21T09:00:00Z'
      }
    ]
  };
}

function createPlayerPackages() {
  return [
    {
      playerPackageId: '19191919-1919-1919-1919-191919191919',
      playerAccountId: '12121212-1212-1212-1212-121212121212',
      name: 'Night 5h',
      remainingIncludedSeconds: 10800,
      remainingBonusSeconds: 0,
      state: 'active',
      expiresAtUtc: '2026-05-22T10:00:00Z'
    }
  ];
}

function createStaffUsers() {
  return [
    {
      staffUserId: '3db1367b-88c6-4b1c-99c3-bcbb5f4d5134',
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      userName: 'cashier',
      displayName: 'Cashier One',
      isActive: true,
      roleNames: ['cashier'],
      createdAtUtc: '2026-05-21T08:00:00Z'
    }
  ];
}

function createZones() {
  return [
    {
      zoneId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
      name: 'Зал A',
      sortOrder: 10,
      createdAtUtc: '2026-05-21T08:00:00Z',
      seats: [
        {
          seatId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
          organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
          branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
          zoneId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
          name: 'PC-01',
          sortOrder: 10,
          createdAtUtc: '2026-05-21T08:00:00Z'
        }
      ]
    }
  ];
}

function createDiagnostics() {
  return {
    organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
    branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
    generatedAtUtc: '2026-05-21T09:00:00Z',
    deviceSummary: {
      totalDevices: 2,
      onlineDevices: 2,
      lockedDevices: 1,
      staleDevices: 0,
      staleThresholdSeconds: 120,
      newestHeartbeatAtUtc: '2026-05-21T09:00:00Z'
    },
    commandSummary: {
      pendingCommands: 0,
      failedCommands: 0,
      recentFailures: []
    },
    updateSummary: {
      activeRollouts: 1,
      installingDevices: 0,
      failedDevices: 0,
      rollbackDevices: 0,
      recentFailures: []
    },
    staleDevices: []
  };
}

function createRollouts() {
  return [
    {
      updateRolloutId: '14141414-1414-1414-1414-141414141414',
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
      updatePackageId: '15151515-1515-1515-1515-151515151515',
      component: 'agent',
      version: '0.1.14',
      channel: 'internal',
      state: 'active',
      targetKind: 'branch',
      targetDeviceIds: [],
      batchPercent: 100,
      createdAtUtc: '2026-05-21T08:00:00Z',
      startsAtUtc: '2026-05-21T08:00:00Z',
      completedAtUtc: null,
      deviceStatuses: []
    }
  ];
}

function createTariffs() {
  return [
    {
      tariffId: '16161616-1616-1616-1616-161616161616',
      tariffVersionId: '17171717-1717-1717-1717-171717171717',
      name: 'Standard',
      tariffRuleVersionId: 'standard-v1',
      versionNumber: 1,
      currencyCode: 'TJS',
      pricePerMinuteMinorUnits: 50,
      minimumBillableMinutes: 15,
      roundingIncrementMinutes: 5
    }
  ];
}

function createAudit() {
  return {
    records: [
      {
        auditRecordId: '18181818-1818-1818-1818-181818181818',
        organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
        branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
        actorStaffUserId: '3db1367b-88c6-4b1c-99c3-bcbb5f4d5134',
        action: 'pos.sale.create',
        targetType: 'PosSale',
        targetId: '99999999-9999-9999-9999-999999999999',
        outcome: 'succeeded',
        sourceApp: 'PlatformApi',
        detailsJson: '{}',
        createdAtUtc: '2026-05-21T09:00:00Z'
      }
    ],
    limit: 30
  };
}

function jsonResponse(body: unknown) {
  return new Response(JSON.stringify(body), {
    status: 200,
    headers: {
      'Content-Type': 'application/json'
    }
  });
}
