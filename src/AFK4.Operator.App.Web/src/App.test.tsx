import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { App } from './App';
import type { HostBridgeMessageEvent } from './hostBridge';

const realtimeMock = vi.hoisted(() => ({
  clients: [] as Array<{
    onConnectionStateChanged?: (state: string) => void;
    onDeviceStatusChanged: (status: unknown) => void;
    onDeviceCommandResult?: (result: unknown) => void;
  }>
}));

vi.mock('./operatorRealtime', async (importOriginal) => {
  const actual = await importOriginal<typeof import('./operatorRealtime')>();
  return {
    ...actual,
    createOperatorRealtimeClient: vi.fn((options: {
      onConnectionStateChanged?: (state: string) => void;
      onDeviceStatusChanged: (status: unknown) => void;
      onDeviceCommandResult?: (result: unknown) => void;
    }) => ({
      start: vi.fn(async () => {
        realtimeMock.clients.push(options);
        options.onConnectionStateChanged?.('connected');
      }),
      stop: vi.fn(async () => options.onConnectionStateChanged?.('disconnected'))
    }))
  };
});

describe('App', () => {
  beforeEach(() => {
    realtimeMock.clients.length = 0;
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
    vi.restoreAllMocks();
  });

  it('opens on the floor map operator workspace after native session restore', async () => {
    installSessionBridge();

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    expect((await screen.findAllByText(/\u041f\u043b\u0430\u0442\u0444\u043e\u0440\u043c\u0430 \u043f\u043e\u0434\u043a\u043b\u044e\u0447\u0435\u043d\u0430/)).length).toBeGreaterThan(0);
    expect(await screen.findByText(/Смена открыта/)).toBeInTheDocument();
    expect(await screen.findByText('POS: 1 чек сегодня')).toBeInTheDocument();
    expect(screen.queryByText(/Смена #24/)).not.toBeInTheDocument();
    expect(screen.queryByText(/неоплаченных чека/)).not.toBeInTheDocument();
    expect(screen.getByRole('navigation', { name: 'Рабочие места' })).toBeInTheDocument();
    expect(screen.getByLabelText('ПК зала')).toBeInTheDocument();
    expect(screen.getAllByText('Сессии').length).toBeGreaterThan(0);
    expect(screen.getByRole('button', { name: /Управление ПК/ })).toHaveAttribute(
      'title',
      'Команды для выбранного ПК: статус, блокировка, питание и сервисный доступ'
    );
    expect(screen.getByText('Сессия активна')).toBeInTheDocument();
    expect(screen.getAllByText('Сессия подтверждена').length).toBeGreaterThan(0);
    expect(await screen.findByRole('button', { name: /15 мин/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Свернуть' })).toBeInTheDocument();
    expect(screen.getByText(/Оператор смены/)).toBeInTheDocument();
  });

  it('exposes native window drag, maximize, and resize commands', async () => {
    const bridge = installSessionBridge();
    const { container } = render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();

    const topCommand = container.querySelector('.top-command');
    expect(topCommand).not.toBeNull();
    fireEvent.doubleClick(topCommand!);
    expect(bridge.requests).toContain('window:maximize');

    const resizeHandle = container.querySelector('.window-resize-handle.bottom-right');
    expect(resizeHandle).not.toBeNull();
    fireEvent.mouseDown(resizeHandle!, { button: 0 });
    expect(bridge.requests).toContain('window:resize');
  });

  it('opens selected PC control and runs backend device status/lock actions', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);
    fetchMock.mockImplementation((input, init) => {
      const pathname = new URL(String(input)).pathname;
      if (pathname.endsWith('/api/devices/11111111-1111-1111-1111-111111111111')) {
        return Promise.resolve(jsonResponse(createDeviceDetail({
          deviceId: '11111111-1111-1111-1111-111111111111',
          machineName: 'PC-01',
          agentVersion: '0.4',
          shellVersion: '0.4'
        })));
      }

      return mockPlatformFetch(input, init);
    });

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    expect((await screen.findAllByText(/\u041f\u043b\u0430\u0442\u0444\u043e\u0440\u043c\u0430 \u043f\u043e\u0434\u043a\u043b\u044e\u0447\u0435\u043d\u0430/)).length).toBeGreaterThan(0);
    fireEvent.click(screen.getByRole('button', { name: /Управление ПК/ }));

    expect(screen.getByRole('region', { name: 'Управление выбранным ПК' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Перезагрузить/ })).toBeEnabled();
    expect(screen.getByRole('button', { name: /Админ-режим/ })).toBeEnabled();
    fireEvent.click(screen.getByRole('button', { name: /Перезагрузить/ }));
    expect(screen.getByRole('status')).toHaveTextContent('Нужен Agent-контракт reboot');

    fireEvent.click(screen.getByRole('button', { name: /^Статус$/ }));

    await waitFor(() => expect(fetchMock.mock.calls.some(([input]) =>
      String(input).includes('/api/devices/11111111-1111-1111-1111-111111111111'))).toBe(true));
    await waitFor(() => expect(fetchMock.mock.calls.some(([input]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/diagnostics'))).toBe(true));
    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent('Агент 0.4'));

    fireEvent.click(screen.getByRole('button', { name: /Блокировать/ }));
    await waitFor(() => expect(fetchMock.mock.calls.some(([input, init]) =>
      String(input).endsWith('/api/devices/11111111-1111-1111-1111-111111111111/commands') &&
      init?.method === 'POST' &&
      String(init.body).includes('"type":"lock"'))).toBe(true));

    fireEvent.pointerDown(document.body);
    expect(screen.queryByRole('region', { name: 'Управление выбранным ПК' })).not.toBeInTheDocument();
  });

  it('filters the floor map and switches to table view', async () => {
    installSessionBridge();

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    expect((await screen.findAllByText(/\u041f\u043b\u0430\u0442\u0444\u043e\u0440\u043c\u0430 \u043f\u043e\u0434\u043a\u043b\u044e\u0447\u0435\u043d\u0430/)).length).toBeGreaterThan(0);
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
    expect(screen.getAllByText(/Депозит/).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/USD/).length).toBeGreaterThan(0);
  });

  it('signs in through the native bridge before showing operator workspaces', async () => {
    window.__AFK4_OPERATOR_CONFIG__ = {
      runtime: 'browser-dev',
      shellMode: 'vite-dev',
      platformBaseUrl: 'http://localhost:5074/',
      currencyCode: 'TJS',
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      branchId: 'acfc0212-967f-4d84-94be-9003387b09c2'
    };
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

  it('clears restored native session when token refresh is rejected', async () => {
    const bridge = installSessionBridge(createSession(), createSession(), {
      failedRequests: {
        'auth:refresh': 'Platform API returned 401 Unauthorized:'
      },
      loadConnection: buildStoredConnection()
    });
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: 'Вход оператора' })).toBeInTheDocument();
    await waitFor(() => expect(bridge.requests).toContain('auth:signOut'));
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([input]) => String(input).includes('/floor-map'))).toBe(false);
  });

  it('hides native bridge diagnostics in packaged WebView2 auth errors', async () => {
    seedStoredOperatorConnection();
    window.__AFK4_OPERATOR_CONFIG__ = {
      runtime: 'webview2',
      shellMode: 'vite-dist',
      platformBaseUrl: 'https://afk4.staging.mubi.dev/',
      currencyCode: 'TJS'
    };

    render(<App />);

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('приложения оператора');
    expect(alert).not.toHaveTextContent('Native host bridge is unavailable.');
  });

  it('shows the connection resolution screen when operator config has no organisation and no stored connection', async () => {
    installSessionBridge(null);

    render(<App />);

    expect(await screen.findByRole('heading', { name: 'Connect to your club' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Вход оператора' })).not.toBeInTheDocument();
  });

  it('skips the connection resolution screen when the native bridge returns a stored connection', async () => {
    installSessionBridge(null, null, { loadConnection: buildStoredConnection() });

    render(<App />);

    expect(await screen.findByRole('heading', { name: 'Вход оператора' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Connect to your club' })).not.toBeInTheDocument();
  });

  it('persists the resolved active connection via the native bridge and proceeds to the sign-in screen', async () => {
    const bridge = installSessionBridge(null);
    const fetchMock = vi.mocked(fetch);
    fetchMock.mockImplementation((input, init) => {
      const url = new URL(String(input));
      if (url.pathname.endsWith('/api/operator-connections/resolve') && init?.method === 'POST') {
        return Promise.resolve(jsonResponse({
          organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
          organizationSlug: 'afk4-dushanbe',
          organizationName: 'AFK4 Dushanbe',
          organizationStatus: 'active',
          organizationStatusReason: null,
          branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
          branchSlug: 'central',
          branchName: 'Central',
          branchCity: 'Dushanbe',
          source: 'slug'
        }));
      }
      return mockPlatformFetch(input, init);
    });

    render(<App />);

    expect(await screen.findByRole('heading', { name: 'Connect to your club' })).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('Club key'), { target: { value: 'afk4-dushanbe' } });
    fireEvent.change(screen.getByLabelText('Branch key'), { target: { value: 'central' } });
    fireEvent.click(screen.getByRole('button', { name: 'Continue' }));

    expect(await screen.findByRole('heading', { name: 'Вход оператора' })).toBeInTheDocument();
    expect(localStorage.getItem('afk4.operator.connection')).toBeNull();
    expect(bridge.connectionSaves.length).toBe(1);
    expect(bridge.connectionSaves[0].organizationId).toBe('0c04d6c0-bfa8-4e26-9263-fc0d307d0f08');
    expect(bridge.connectionSaves[0].branchId).toBe('acfc0212-967f-4d84-94be-9003387b09c2');
    expect(bridge.connectionSaves[0].branchSlug).toBe('central');
  });

  it('shows blocked-state copy and does not persist the connection when the resolved tenant is suspended', async () => {
    const bridge = installSessionBridge(null);
    const fetchMock = vi.mocked(fetch);
    fetchMock.mockImplementation((input, init) => {
      const url = new URL(String(input));
      if (url.pathname.endsWith('/api/operator-connections/resolve') && init?.method === 'POST') {
        return Promise.resolve(jsonResponse({
          organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
          organizationSlug: 'afk4-dushanbe',
          organizationName: 'AFK4 Dushanbe',
          organizationStatus: 'suspended',
          organizationStatusReason: 'Не оплачена подписка за май.',
          branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
          branchSlug: 'central',
          branchName: 'Central',
          branchCity: 'Dushanbe',
          source: 'slug'
        }));
      }
      return mockPlatformFetch(input, init);
    });

    render(<App />);

    expect(await screen.findByRole('heading', { name: 'Connect to your club' })).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('Club key'), { target: { value: 'afk4-dushanbe' } });
    fireEvent.change(screen.getByLabelText('Branch key'), { target: { value: 'central' } });
    fireEvent.click(screen.getByRole('button', { name: 'Continue' }));

    expect(await screen.findByRole('heading', { name: 'Подписка приостановлена' })).toBeInTheDocument();
    expect(screen.getByText('Не оплачена подписка за май.')).toBeInTheDocument();
    expect(localStorage.getItem('afk4.operator.connection')).toBeNull();
    expect(bridge.connectionSaves.length).toBe(0);
    expect(bridge.requests).toContain('connection:clearConnection');

    fireEvent.click(screen.getByRole('button', { name: 'Сменить подключение' }));
    expect(await screen.findByRole('heading', { name: 'Connect to your club' })).toBeInTheDocument();
    expect(bridge.requests.filter((type) => type === 'connection:clearConnection').length).toBeGreaterThanOrEqual(2);
  });

  it('ends the selected active session through the backend before confirming the UI action', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    expect((await screen.findAllByText(/\u041f\u043b\u0430\u0442\u0444\u043e\u0440\u043c\u0430 \u043f\u043e\u0434\u043a\u043b\u044e\u0447\u0435\u043d\u0430/)).length).toBeGreaterThan(0);
    fireEvent.click(await screen.findByRole('button', { name: /^Стоп$/ }));

    expect(await screen.findByRole('alertdialog', { name: 'Подтвердите остановку сессии' })).toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([input, init]) =>
      String(input).includes('/api/sessions/22222222-2222-2222-2222-222222222222/end') &&
      init?.method === 'POST')).toBe(false);
    fireEvent.click(screen.getByRole('button', { name: 'Подтвердить стоп' }));

    expect((await screen.findAllByText(/\u0421\u0442\u043e\u043f: \u0411\u043b\u043e\u043a\u0438\u0440\u043e\u0432\u043a\u0430: \u043e\u0436\u0438\u0434\u0430\u0435\u0442 \u0432\u044b\u043f\u043e\u043b\u043d\u0435\u043d\u0438\u044f/)).length).toBeGreaterThan(0);
    const postCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/sessions/22222222-2222-2222-2222-222222222222/end') &&
      init?.method === 'POST');
    expect(postCall).toBeDefined();
    const body = JSON.parse(String(postCall?.[1]?.body));
    expect(body.reason).toBe('operator');
    expect(body.idempotencyKey).toMatch(/^session-end-/);
  });

  it('refreshes the selected seat when a session command result arrives over realtime', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);
    let commandResultReported = false;
    let floorMapRequestCount = 0;
    fetchMock.mockImplementation((input, init) => {
      const pathname = new URL(String(input)).pathname;
      if (pathname.endsWith('/floor-map')) {
        floorMapRequestCount += 1;
        if (commandResultReported) {
          return Promise.resolve(jsonResponse(createFloorMapWithPc01({
            state: 'Locked',
            isDeviceLocked: true,
            activeSessionId: null,
            remainingSeconds: null
          })));
        }

        if (floorMapRequestCount > 1) {
          return Promise.resolve(jsonResponse(createFloorMapWithPc01({
            state: 'Ending',
            isDeviceLocked: false,
            activeSessionId: '22222222-2222-2222-2222-222222222222',
            remainingSeconds: 3600
          })));
        }
      }

      return mockPlatformFetch(input, init);
    });

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    await waitFor(() => expect(realtimeMock.clients).toHaveLength(1));
    fireEvent.click(await screen.findByRole('button', { name: /\u0421\u0442\u043e\u043f/ }));
    expect(await screen.findByRole('alertdialog', { name: 'Подтвердите остановку сессии' })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Подтвердить стоп' }));
    await waitFor(() => expect(floorMapRequestCount).toBeGreaterThanOrEqual(2));

    commandResultReported = true;
    realtimeMock.clients.at(-1)?.onDeviceCommandResult?.({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
      deviceId: '11111111-1111-1111-1111-111111111111',
      commandId: '44444444-4444-4444-4444-444444444444',
      status: 'accepted',
      message: 'Agent accepted lock',
      observedAtUtc: '2026-05-21T10:00:01Z'
    });

    await waitFor(() => expect(floorMapRequestCount).toBeGreaterThanOrEqual(3));
    expect(await screen.findByRole('button', { name: /\u0421\u0442\u0430\u0440\u0442 60 \u043c\u0438\u043d/ })).toBeEnabled();
    expect(screen.queryByRole('button', { name: /\u0421\u0442\u043e\u043f/ })).not.toBeInTheDocument();
  });

  it('starts a ready seat as a fast guest session through the backend', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    expect((await screen.findAllByText(/\u041f\u043b\u0430\u0442\u0444\u043e\u0440\u043c\u0430 \u043f\u043e\u0434\u043a\u043b\u044e\u0447\u0435\u043d\u0430/)).length).toBeGreaterThan(0);
    fireEvent.click(await screen.findByRole('button', { name: /PC-02/ }));
    const startButton = await screen.findByRole('button', { name: /Старт 60 мин/ });
    expect(startButton).toBeEnabled();
    fireEvent.click(startButton);

    expect((await screen.findAllByText(/\u0421\u0442\u0430\u0440\u0442 60 \u043c\u0438\u043d: \u0420\u0430\u0437\u0431\u043b\u043e\u043a\u0438\u0440\u043e\u0432\u043a\u0430: \u043e\u0436\u0438\u0434\u0430\u0435\u0442 \u0432\u044b\u043f\u043e\u043b\u043d\u0435\u043d\u0438\u044f/)).length).toBeGreaterThan(0);
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
    expect((await screen.findAllByText(/\u041f\u043b\u0430\u0442\u0444\u043e\u0440\u043c\u0430 \u043f\u043e\u0434\u043a\u043b\u044e\u0447\u0435\u043d\u0430/)).length).toBeGreaterThan(0);
    fireEvent.click(await screen.findByRole('button', { name: /PC-02/ }));
    fireEvent.click(await screen.findByRole('button', { name: /Депозит/ }));
    fireEvent.change(screen.getByLabelText('Игрок для биллинга'), { target: { value: 'Madina' } });
    fireEvent.click(await screen.findByRole('button', { name: /Madina S\./ }));
    expect(await screen.findByText('Депозит готов')).toBeInTheDocument();

    const startButton = await screen.findByRole('button', { name: /Старт 60 мин/ });
    expect(startButton).toBeEnabled();
    fireEvent.click(startButton);

    expect((await screen.findAllByText(/\u0421\u0442\u0430\u0440\u0442 60 \u043c\u0438\u043d: \u0420\u0430\u0437\u0431\u043b\u043e\u043a\u0438\u0440\u043e\u0432\u043a\u0430: \u043e\u0436\u0438\u0434\u0430\u0435\u0442 \u0432\u044b\u043f\u043e\u043b\u043d\u0435\u043d\u0438\u044f/)).length).toBeGreaterThan(0);
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
    expect((await screen.findAllByText(/\u041f\u043b\u0430\u0442\u0444\u043e\u0440\u043c\u0430 \u043f\u043e\u0434\u043a\u043b\u044e\u0447\u0435\u043d\u0430/)).length).toBeGreaterThan(0);
    expect(screen.getByTitle('POS')).toHaveAttribute('aria-disabled', 'true');
    expect(screen.getByTitle('Брони')).toHaveAttribute('aria-disabled', 'true');
    fireEvent.click(screen.getByTitle('Брони'));
    expect(await screen.findByText(/Нет прав на раздел/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /15 мин/ })).toBeDisabled();
    expect(screen.getByRole('button', { name: /Стоп/ })).toBeDisabled();
    expect(screen.getByText('Нет прав на действия с сессией')).toBeInTheDocument();
  });

  it('opens workspace rail entries with partial role permissions', async () => {
    installSessionBridge(createSession({
      permissions: [
        'floor_map.view',
        'pos.sales.create',
        'pos.sales.pay',
        'shifts.view',
        'shifts.open',
        'receipts.view',
        'devices.detail.view'
      ]
    }));

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    const posButton = screen.getByTitle('POS');
    expect(posButton).toBeEnabled();

    fireEvent.click(posButton);
    expect(screen.getByRole('heading', { name: /POS/ })).toBeInTheDocument();

    const workspaceButtons = within(screen.getByRole('navigation')).getAllByRole('button');
    const settingsButton = workspaceButtons.at(-1);
    expect(settingsButton).toBeDefined();
    expect(settingsButton).toBeEnabled();

    fireEvent.click(settingsButton!);
    expect(settingsButton).toHaveClass('active');
  });

  it('refreshes restored native permissions before gating workspace rail entries', async () => {
    installSessionBridge(
      createSession({ permissions: ['floor_map.view'] }),
      createSession({ permissions: ['floor_map.view', 'reservations.view'] }));

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    const workspaceButtons = within(screen.getByRole('navigation')).getAllByRole('button');
    const bookingButton = workspaceButtons[2];
    expect(bookingButton).toHaveAttribute('aria-disabled', 'false');
    expect(bookingButton).toBeEnabled();

    fireEvent.click(bookingButton);
    expect(bookingButton).toHaveClass('active');
  });

  it('switches to SmartShell-like booking, POS, and logs workspaces', async () => {
    installSessionBridge();

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Дашборд'));
    expect(screen.getByRole('heading', { name: /Что требует внимания/ })).toBeInTheDocument();
    expect(screen.getByText('Главный фокус')).toBeInTheDocument();
    expect((await screen.findAllByText(/Блокировка/)).length).toBeGreaterThan(0);
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
    const logsHead = screen.getByRole('heading', { name: /Журнал/ }).closest('.screen-head');
    expect(logsHead).toBeInTheDocument();
    expect(logsHead).not.toHaveTextContent('Смена');
    expect(logsHead).not.toHaveTextContent('Ошибки');
    expect(logsHead).not.toHaveTextContent('Экспорт');
    expect(screen.getByText('Журнал событий')).toBeInTheDocument();
    expect(screen.getByText('Детали события')).toBeInTheDocument();
    expect(screen.getByText('Фильтры')).toBeInTheDocument();
    expect(screen.getByText('Операции смены')).toBeInTheDocument();
    expect(screen.getByText('Источники')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Полный журнал/ })).toBeInTheDocument();

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

  it('downloads the Dashboard export CSV', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);
    const createObjectUrl = vi.fn(() => 'blob:dashboard');
    const revokeObjectUrl = vi.fn();
    Object.defineProperty(window.URL, 'createObjectURL', { value: createObjectUrl, configurable: true });
    Object.defineProperty(window.URL, 'revokeObjectURL', { value: revokeObjectUrl, configurable: true });
    const downloads: string[] = [];
    const createElement = document.createElement.bind(document);
    const createElementSpy = vi.spyOn(document, 'createElement').mockImplementation((tagName: string) => {
      const element = createElement(tagName);
      if (tagName.toLowerCase() === 'a') {
        Object.defineProperty(element, 'click', {
          value: () => downloads.push((element as HTMLAnchorElement).download),
          configurable: true
        });
      }

      return element;
    });

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Дашборд'));
    fireEvent.click(screen.getByRole('button', { name: /Экспорт дашборда за/ }));

    expect(await screen.findByText('Экспорт: подтверждено')).toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([input]) => String(input).includes('/reports/sales/export.csv'))).toBe(true);
    expect(downloads.some((download) => download.startsWith('afk4-dashboard-sales-') && download.endsWith('.csv'))).toBe(true);
    expect(createObjectUrl).toHaveBeenCalledWith(expect.any(Blob));
    expect(revokeObjectUrl).toHaveBeenCalledWith('blob:dashboard');
    createElementSpy.mockRestore();
  });

  it('runs backend audit search filters from Logs', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Логи'));
    expect(await screen.findByText('\u0416\u0443\u0440\u043d\u0430\u043b \u0437\u0430\u0433\u0440\u0443\u0436\u0435\u043d')).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('Событие'), { target: { value: 'sessions.start' } });
    fireEvent.change(screen.getByLabelText('Результат'), { target: { value: 'succeeded' } });
    fireEvent.change(screen.getByLabelText('Раздел'), { target: { value: 'Session' } });
    fireEvent.change(screen.getByLabelText('С'), { target: { value: '2026-05-21T00:00:00Z' } });
    fireEvent.change(screen.getByLabelText('До'), { target: { value: '2026-05-21T23:59:59Z' } });
    fireEvent.change(screen.getByLabelText('Записей'), { target: { value: '12' } });
    fireEvent.click(screen.getByRole('button', { name: 'Применить фильтр' }));

    expect(await screen.findByText('Применить фильтр: подтверждено')).toBeInTheDocument();
    const auditCalls = fetchMock.mock.calls.filter(([input]) => String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/audit'));
    const auditCall = auditCalls[auditCalls.length - 1];
    expect(auditCall).toBeDefined();
    const url = new URL(String(auditCall[0]));
    expect(url.searchParams.get('action')).toBe('sessions.start');
    expect(url.searchParams.get('outcome')).toBe('succeeded');
    expect(url.searchParams.get('targetType')).toBe('Session');
    expect(url.searchParams.get('fromUtc')).toBe('2026-05-21T00:00:00Z');
    expect(url.searchParams.get('toUtc')).toBe('2026-05-21T23:59:59Z');
    expect(url.searchParams.get('limit')).toBe('12');

    const beforePreset = new Date();
    fireEvent.click(screen.getByRole('button', { name: 'Сегодня' }));

    expect(await screen.findByText('Сегодня: подтверждено')).toBeInTheDocument();
    const presetCalls = fetchMock.mock.calls.filter(([input]) => String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/audit'));
    const presetUrl = new URL(String(presetCalls[presetCalls.length - 1][0]));
    const expectedFromUtc = new Date(Date.UTC(beforePreset.getUTCFullYear(), beforePreset.getUTCMonth(), beforePreset.getUTCDate())).toISOString();
    expect(presetUrl.searchParams.get('fromUtc')).toBe(expectedFromUtc);
    expect(Date.parse(presetUrl.searchParams.get('toUtc') ?? '')).toBeGreaterThanOrEqual(beforePreset.getTime());
    expect(presetUrl.searchParams.get('limit')).toBe('50');
  });

  it('shows backend audit record detail in Logs', async () => {
    installSessionBridge();

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Логи'));
    expect(await screen.findByText('\u0416\u0443\u0440\u043d\u0430\u043b \u0437\u0430\u0433\u0440\u0443\u0436\u0435\u043d')).toBeInTheDocument();

    const detailPanel = document.querySelector('.logs-detail-panel') as HTMLElement;
    expect(detailPanel).toHaveTextContent('Продажа создана');
    expect(detailPanel).toHaveTextContent('Чек');
    expect(detailPanel).toHaveTextContent('успешно');
    expect(detailPanel).toHaveTextContent('Оператор смены');
    expect(detailPanel).toHaveTextContent('Платформа');
    expect(detailPanel).not.toHaveTextContent('ID аудита');
    expect(detailPanel).not.toHaveTextContent('18181818');
    expect(detailPanel).not.toHaveTextContent('pos.sale.create');
    expect(detailPanel).not.toHaveTextContent('PosSale');
    expect(detailPanel).not.toHaveTextContent('99999999');
    expect(detailPanel).not.toHaveTextContent('PlatformApi');
  });

  it('shows backend diagnostics failure detail in Logs', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);
    fetchMock.mockImplementation((input, init) => {
      const pathname = new URL(String(input)).pathname;
      if (pathname.endsWith('/diagnostics')) {
        return Promise.resolve(jsonResponse(createDiagnostics({
          commandSummary: {
            pendingCommands: 0,
            failedCommands: 1,
            recentFailures: [
              {
                deviceId: '33333333-3333-3333-3333-333333333333',
                machineName: 'PC-03',
                commandId: '44444444-4444-4444-4444-444444444444',
                type: 'unlock',
                status: 'Failed',
                message: 'timeout waiting for Agent',
                updatedAtUtc: '2026-05-21T10:10:00Z'
              }
            ]
          }
        })));
      }

      return mockPlatformFetch(input, init);
    });

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Логи'));
    expect(await screen.findByText('\u0416\u0443\u0440\u043d\u0430\u043b \u0437\u0430\u0433\u0440\u0443\u0436\u0435\u043d')).toBeInTheDocument();
    expect(screen.getAllByText('PC-03 Разблокировка').length).toBeGreaterThan(0);

    const detailPanel = document.querySelector('.logs-detail-panel') as HTMLElement;
    expect(detailPanel).toHaveTextContent('Устройство');
    expect(detailPanel).toHaveTextContent('PC-03');
    expect(detailPanel).toHaveTextContent('Разблокировка');
    expect(detailPanel).toHaveTextContent('не выполнена');
    expect(detailPanel).toHaveTextContent('Агент не ответил вовремя');
    expect(detailPanel).not.toHaveTextContent('33333333');
    expect(detailPanel).not.toHaveTextContent('44444444');

    const sourcePanel = document.querySelector('.logs-sources-panel') as HTMLElement;
    fireEvent.click(within(sourcePanel).getByRole('button', { name: /Касса/ }));
    expect(screen.queryByText('PC-03 Разблокировка')).not.toBeInTheDocument();
    expect(screen.getAllByText('Продажа создана').length).toBeGreaterThan(0);
    expect(screen.queryByText('pos.sale.create')).not.toBeInTheDocument();

    fireEvent.click(within(sourcePanel).getByRole('button', { name: /Агент/ }));
    expect(screen.getAllByText('PC-03 Разблокировка').length).toBeGreaterThan(0);
    expect(detailPanel).not.toHaveTextContent('pos.sale.create');
  });

  it('downloads Logs CSV and audit trail exports', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);
    const createObjectUrl = vi.fn(() => 'blob:logs');
    const revokeObjectUrl = vi.fn();
    Object.defineProperty(window.URL, 'createObjectURL', { value: createObjectUrl, configurable: true });
    Object.defineProperty(window.URL, 'revokeObjectURL', { value: revokeObjectUrl, configurable: true });
    const downloads: string[] = [];
    const createElement = document.createElement.bind(document);
    const createElementSpy = vi.spyOn(document, 'createElement').mockImplementation((tagName: string) => {
      const element = createElement(tagName);
      if (tagName.toLowerCase() === 'a') {
        Object.defineProperty(element, 'click', {
          value: () => downloads.push((element as HTMLAnchorElement).download),
          configurable: true
        });
      }

      return element;
    });

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Логи'));
    expect(await screen.findByText('\u0416\u0443\u0440\u043d\u0430\u043b \u0437\u0430\u0433\u0440\u0443\u0436\u0435\u043d')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Таблица' }));

    expect(await screen.findByText('Таблица: подтверждено')).toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([input]) => String(input).includes('/reports/operator-actions/export.csv'))).toBe(true);
    expect(downloads.some((download) => download.startsWith('afk4-operator-actions-') && download.endsWith('.csv'))).toBe(true);

    fireEvent.click(screen.getByRole('button', { name: /Полный журнал/ }));
    expect(await screen.findByText('Полный журнал: подтверждено')).toBeInTheDocument();
    expect(createObjectUrl).toHaveBeenCalledTimes(2);
    expect(revokeObjectUrl).toHaveBeenCalledWith('blob:logs');
    expect(downloads.some((download) => download.startsWith('afk4-audit-trail-') && download.endsWith('.json'))).toBe(true);
    createElementSpy.mockRestore();
  });

  it('shows successful empty Logs results without backend-empty copy', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);
    fetchMock.mockImplementation((input, init) => {
      const pathname = new URL(String(input)).pathname;
      if (pathname.endsWith('/audit')) {
        return Promise.resolve(jsonResponse({ records: [], limit: 30 }));
      }

      return mockPlatformFetch(input, init);
    });

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Логи'));
    expect(await screen.findByText('\u0416\u0443\u0440\u043d\u0430\u043b \u0437\u0430\u0433\u0440\u0443\u0436\u0435\u043d')).toBeInTheDocument();

    expect((await screen.findAllByText('Событий за период нет')).length).toBeGreaterThan(0);
    expect(screen.queryByText('Нет backend событий')).not.toBeInTheDocument();
  });

  it('confirms POS payment only after backend sale and manual payment calls resolve', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('POS'));
    await waitFor(() => expect(screen.getByLabelText('\u0422\u043e\u0432\u0430\u0440 \u0434\u043b\u044f \u0441\u043f\u0438\u0441\u0430\u043d\u0438\u044f POS')).toBeEnabled());
    fireEvent.click(screen.getByRole('button', { name: /Принять оплату/ }));

    expect(await screen.findByText('Оплата: подтверждено')).toBeInTheDocument();
    const saleCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/pos/sales') &&
      init?.method === 'POST');
    expect(saleCall).toBeDefined();
    const saleBody = JSON.parse(String(saleCall?.[1]?.body));
    expect(saleBody).toMatchObject({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      shiftId: '66666666-6666-6666-6666-666666666666',
      playerAccountId: null
    });
    const paymentCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/pos/sales/99999999-9999-9999-9999-999999999999/payments/manual') &&
      init?.method === 'POST');
    expect(paymentCall).toBeDefined();
    const paymentBody = JSON.parse(String(paymentCall?.[1]?.body));
    expect(paymentBody).toMatchObject({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      paymentMethod: 'cash'
    });
  });

  it('attaches the selected backend client to POS sale checkout', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('POS'));
    expect((await screen.findAllByText(/\u041f\u043b\u0430\u0442\u0444\u043e\u0440\u043c\u0430 \u043f\u043e\u0434\u043a\u043b\u044e\u0447\u0435\u043d\u0430/)).length).toBeGreaterThan(0);
    fireEvent.change(screen.getByLabelText('Клиент POS'), { target: { value: 'Madina' } });
    fireEvent.click(await screen.findByRole('button', { name: /Madina S\./ }));
    const cardPaymentButton = screen.getAllByRole('button', { name: 'Карта' })
      .find((button) => button.closest('.pos-payment-methods'));
    expect(cardPaymentButton).toBeDefined();
    fireEvent.click(cardPaymentButton!);
    fireEvent.click(screen.getByRole('button', { name: /Принять оплату/ }));

    expect(await screen.findByText('Оплата: подтверждено')).toBeInTheDocument();
    const saleCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/pos/sales') &&
      init?.method === 'POST');
    expect(saleCall).toBeDefined();
    const saleBody = JSON.parse(String(saleCall?.[1]?.body));
    expect(saleBody).toMatchObject({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      shiftId: '66666666-6666-6666-6666-666666666666',
      playerAccountId: '12121212-1212-1212-1212-121212121212'
    });

    const paymentCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/pos/sales/99999999-9999-9999-9999-999999999999/payments/manual') &&
      init?.method === 'POST');
    expect(paymentCall).toBeDefined();
    const paymentBody = JSON.parse(String(paymentCall?.[1]?.body));
    expect(paymentBody).toMatchObject({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      paymentMethod: 'card_manual'
    });
  });

  it('tops up the selected POS client wallet from the cart total', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('POS'));
    expect((await screen.findAllByText(/\u041f\u043b\u0430\u0442\u0444\u043e\u0440\u043c\u0430 \u043f\u043e\u0434\u043a\u043b\u044e\u0447\u0435\u043d\u0430/)).length).toBeGreaterThan(0);
    fireEvent.change(screen.getByLabelText('Клиент POS'), { target: { value: 'Madina' } });
    fireEvent.click(await screen.findByRole('button', { name: /Madina S\./ }));
    fireEvent.click(screen.getByRole('button', { name: /Пополнить депозит/ }));

    expect(await screen.findByText('Пополнить депозит: 12 TJS')).toBeInTheDocument();
    const topUpCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/players/12121212-1212-1212-1212-121212121212/wallet/top-ups') &&
      init?.method === 'POST');
    expect(topUpCall).toBeDefined();
    const body = JSON.parse(String(topUpCall?.[1]?.body));
    expect(body).toMatchObject({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      amount: { currencyCode: 'TJS', minorUnits: 1200 },
      reason: 'operator POS wallet top-up'
    });
    expect(body.idempotencyKey).toMatch(/^wallet-top-up-/);
  });

  it('creates a POS customer card from the cart and attaches it to checkout', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('POS'));
    expect((await screen.findAllByText(/\u041f\u043b\u0430\u0442\u0444\u043e\u0440\u043c\u0430 \u043f\u043e\u0434\u043a\u043b\u044e\u0447\u0435\u043d\u0430/)).length).toBeGreaterThan(0);
    fireEvent.change(screen.getByLabelText('Имя клиента POS'), { target: { value: 'Zarina N.' } });
    fireEvent.change(screen.getByLabelText('Телефон клиента POS'), { target: { value: '+992 90 777 88 99' } });
    fireEvent.click(screen.getByRole('button', { name: 'Создать' }));

    expect(await screen.findByText('Новая карта: подтверждено')).toBeInTheDocument();
    expect(screen.getByText('Zarina N.')).toBeInTheDocument();
    const createPlayerCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/players') &&
      init?.method === 'POST');
    expect(createPlayerCall).toBeDefined();
    const createPlayerBody = JSON.parse(String(createPlayerCall?.[1]?.body));
    expect(createPlayerBody).toMatchObject({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      displayName: 'Zarina N.',
      phoneNumber: '+992 90 777 88 99'
    });
    expect(createPlayerBody.idempotencyKey).toMatch(/^player-create-/);

    fireEvent.click(screen.getByRole('button', { name: /Принять оплату/ }));

    expect(await screen.findByText('Оплата: подтверждено')).toBeInTheDocument();
    const saleCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/pos/sales') &&
      init?.method === 'POST');
    expect(saleCall).toBeDefined();
    const saleBody = JSON.parse(String(saleCall?.[1]?.body));
    expect(saleBody).toMatchObject({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      playerAccountId: '45454545-4545-4545-4545-454545454545'
    });
  });

  it('refunds the latest backend POS sale from quick operations', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('POS'));
    expect((await screen.findAllByText(/\u041f\u043b\u0430\u0442\u0444\u043e\u0440\u043c\u0430 \u043f\u043e\u0434\u043a\u043b\u044e\u0447\u0435\u043d\u0430/)).length).toBeGreaterThan(0);
    fireEvent.click(screen.getByRole('button', { name: /Возврат по чеку/ }));

    expect(await screen.findByRole('alertdialog', { name: 'Подтвердите возврат POS' })).toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([input, init]) =>
      String(input).includes('/api/pos/sales/99999999-9999-9999-9999-999999999999/refunds') &&
      init?.method === 'POST')).toBe(false);
    fireEvent.change(screen.getByLabelText('Причина возврата'), { target: { value: 'Клиент вернул товар' } });
    fireEvent.click(screen.getByRole('button', { name: 'Подтвердить возврат' }));

    expect(await screen.findByText('Возврат по чеку: подтверждено')).toBeInTheDocument();
    const refundCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/pos/sales/99999999-9999-9999-9999-999999999999/refunds') &&
      init?.method === 'POST');
    expect(refundCall).toBeDefined();
    const body = JSON.parse(String(refundCall?.[1]?.body));
    expect(body).toMatchObject({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      reason: 'Клиент вернул товар'
    });
    expect(body.idempotencyKey).toMatch(/^pos-refund-/);
  });

  it('refunds the selected backend POS sale from quick operations', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('POS'));
    expect((await screen.findAllByText(/\u041f\u043b\u0430\u0442\u0444\u043e\u0440\u043c\u0430 \u043f\u043e\u0434\u043a\u043b\u044e\u0447\u0435\u043d\u0430/)).length).toBeGreaterThan(0);
    fireEvent.click(screen.getByRole('button', { name: /25 TJS/ }));
    expect(await screen.findByText('Детали чека: подтверждено')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /Возврат по чеку/ }));
    expect(await screen.findByRole('alertdialog', { name: 'Подтвердите возврат POS' })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Подтвердить возврат' }));

    expect(await screen.findByText('Возврат по чеку: подтверждено')).toBeInTheDocument();
    const refundCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/pos/sales/88888888-8888-8888-8888-888888888888/refunds') &&
      init?.method === 'POST');
    expect(refundCall).toBeDefined();
  });

  it('loads backend POS sale details from the recent receipt list', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('POS'));
    expect((await screen.findAllByText(/\u041f\u043b\u0430\u0442\u0444\u043e\u0440\u043c\u0430 \u043f\u043e\u0434\u043a\u043b\u044e\u0447\u0435\u043d\u0430/)).length).toBeGreaterThan(0);
    fireEvent.click(screen.getAllByRole('button', { name: /paid/ })[0]);

    expect(await screen.findByText('Детали чека: подтверждено')).toBeInTheDocument();
    expect(screen.getAllByText(/Cola 0.5/).length).toBeGreaterThan(0);
    expect(await screen.findByText('POS-20260521-0001')).toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([input, init]) =>
      String(input).includes('/api/pos/sales/99999999-9999-9999-9999-999999999999') &&
      init?.method !== 'POST')).toBe(true);
    expect(fetchMock.mock.calls.some(([input, init]) =>
      String(input).includes('/api/receipts/11111111-1111-1111-1111-111111111111') &&
      init?.method !== 'POST')).toBe(true);
  });

  it('prints and exports the loaded backend POS receipt', async () => {
    installSessionBridge();
    const writeMock = vi.fn();
    const printMock = vi.fn();
    const openSpy = vi.spyOn(window, 'open').mockReturnValue({
      document: {
        write: writeMock,
        close: vi.fn()
      },
      focus: vi.fn(),
      print: printMock
    } as unknown as Window);
    const createObjectUrl = vi.fn(() => 'blob:receipt');
    const revokeObjectUrl = vi.fn();
    Object.defineProperty(window.URL, 'createObjectURL', { value: createObjectUrl, configurable: true });
    Object.defineProperty(window.URL, 'revokeObjectURL', { value: revokeObjectUrl, configurable: true });
    const linkClick = vi.fn();
    const createElement = document.createElement.bind(document);
    const createElementSpy = vi.spyOn(document, 'createElement').mockImplementation((tagName: string) => {
      const element = createElement(tagName);
      if (tagName.toLowerCase() === 'a') {
        Object.defineProperty(element, 'click', { value: linkClick, configurable: true });
      }

      return element;
    });

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('POS'));
    expect((await screen.findAllByText(/\u041f\u043b\u0430\u0442\u0444\u043e\u0440\u043c\u0430 \u043f\u043e\u0434\u043a\u043b\u044e\u0447\u0435\u043d\u0430/)).length).toBeGreaterThan(0);
    fireEvent.click(screen.getAllByRole('button', { name: /paid/ })[0]);
    expect(await screen.findByText('POS-20260521-0001')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Печать' }));
    expect(await screen.findByText('Печать чека: подтверждено')).toBeInTheDocument();
    expect(openSpy).toHaveBeenCalled();
    expect(writeMock.mock.calls[0]?.[0]).toContain('POS-20260521-0001');
    expect(writeMock.mock.calls[0]?.[0]).toContain('Cola 0.5');
    expect(printMock).toHaveBeenCalled();

    fireEvent.click(screen.getByRole('button', { name: 'Экспорт' }));
    expect(await screen.findByText('Экспорт чека: подтверждено')).toBeInTheDocument();
    expect(createObjectUrl).toHaveBeenCalled();
    expect(linkClick).toHaveBeenCalled();
    expect(revokeObjectUrl).toHaveBeenCalledWith('blob:receipt');
    createElementSpy.mockRestore();
  });

  it('voids a backend POS draft from the current cart', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('POS'));
    expect((await screen.findAllByText(/\u041f\u043b\u0430\u0442\u0444\u043e\u0440\u043c\u0430 \u043f\u043e\u0434\u043a\u043b\u044e\u0447\u0435\u043d\u0430/)).length).toBeGreaterThan(0);
    fireEvent.click(screen.getByRole('button', { name: /Аннулировать черновик/ }));

    expect(await screen.findByRole('alertdialog', { name: 'Подтвердите аннулирование' })).toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([input, init]) =>
      String(input).includes('/api/pos/sales/99999999-9999-9999-9999-999999999999/void') &&
      init?.method === 'POST')).toBe(false);
    fireEvent.change(screen.getByLabelText('Причина аннулирования'), { target: { value: 'Ошибочная корзина' } });
    fireEvent.click(screen.getByRole('button', { name: 'Подтвердить аннулирование' }));

    expect(await screen.findByText('Аннулировать черновик: подтверждено')).toBeInTheDocument();
    const saleCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/pos/sales') &&
      init?.method === 'POST');
    expect(saleCall).toBeDefined();
    const saleBody = JSON.parse(String(saleCall?.[1]?.body));
    expect(saleBody).toMatchObject({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      shiftId: '66666666-6666-6666-6666-666666666666'
    });
    expect(saleBody.lines).toHaveLength(1);
    expect(saleBody.idempotencyKey).toMatch(/^pos-sale-draft-/);

    const voidCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/pos/sales/99999999-9999-9999-9999-999999999999/void') &&
      init?.method === 'POST');
    expect(voidCall).toBeDefined();
    const voidBody = JSON.parse(String(voidCall?.[1]?.body));
    expect(voidBody).toMatchObject({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      reason: 'Ошибочная корзина'
    });
    expect(voidBody.idempotencyKey).toMatch(/^pos-void-/);
  });

  it('records a POS stock write-off from quick operations', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('POS'));
    expect((await screen.findAllByText(/\u041f\u043b\u0430\u0442\u0444\u043e\u0440\u043c\u0430 \u043f\u043e\u0434\u043a\u043b\u044e\u0447\u0435\u043d\u0430/)).length).toBeGreaterThan(0);
    fireEvent.change(screen.getByLabelText('Товар для списания POS'), {
      target: { value: '77777777-7777-7777-7777-777777777777' }
    });
    fireEvent.change(screen.getByLabelText('Количество списания POS'), { target: { value: '3' } });
    fireEvent.change(screen.getByLabelText('Причина списания POS'), { target: { value: 'broken bottle' } });
    fireEvent.click(screen.getByRole('button', { name: 'Списать' }));

    expect(await screen.findByText('Списание склада: подтверждено')).toBeInTheDocument();
    const stockCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/inventory/stock-movements') &&
      init?.method === 'POST');
    expect(stockCall).toBeDefined();
    const body = JSON.parse(String(stockCall?.[1]?.body));
    expect(body).toMatchObject({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      productId: '77777777-7777-7777-7777-777777777777',
      movementType: 'adjustment',
      quantityDelta: -3,
      unitCost: { currencyCode: 'TJS', minorUnits: 0 },
      reason: 'broken bottle'
    });
    expect(body.idempotencyKey).toMatch(/^stock-write-off-/);
  });

  it('opens a shift from Payments when no current shift exists', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);
    let shiftOpened = false;
    fetchMock.mockImplementation((input, init) => {
      const url = new URL(String(input));
      if (url.pathname.endsWith('/shifts/current') && !shiftOpened) {
        return Promise.resolve(new Response('', { status: 404, statusText: 'Not Found' }));
      }

      if (url.pathname.endsWith('/shifts/open') && init?.method === 'POST') {
        shiftOpened = true;
      }

      return mockPlatformFetch(input, init);
    });

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Платежи'));
    expect(await screen.findByText('\u041e\u0442\u0447\u0451\u0442\u044b \u0437\u0430\u0433\u0440\u0443\u0436\u0435\u043d\u044b')).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('Старт cash'), { target: { value: '150.00' } });
    fireEvent.change(screen.getByLabelText('Открытие'), { target: { value: 'Утренняя смена' } });
    fireEvent.click(screen.getByRole('button', { name: 'Открыть смену' }));

    expect(await screen.findByText('Открыть смену: подтверждено')).toBeInTheDocument();
    const openCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/shifts/open') &&
      init?.method === 'POST');
    expect(openCall).toBeDefined();
    const body = JSON.parse(String(openCall?.[1]?.body));
    expect(body).toMatchObject({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      startingCash: { currencyCode: 'TJS', minorUnits: 15000 },
      openingNote: 'Утренняя смена'
    });
    expect(body.idempotencyKey).toMatch(/^shift-open-/);
  });

  it('shows successful empty Payments reports without backend-empty copy', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);
    const zero = { currencyCode: 'TJS', minorUnits: 0 };
    fetchMock.mockImplementation((input, init) => {
      const pathname = new URL(String(input)).pathname;
      if (pathname.endsWith('/reports/sales')) {
        return Promise.resolve(jsonResponse({
          ...createSalesReport(),
          rows: [],
          grossSalesTotal: zero,
          refundsTotal: zero,
          netSalesTotal: zero
        }));
      }

      if (pathname.endsWith('/reports/cash-operations')) {
        return Promise.resolve(jsonResponse({
          ...createCashReport(),
          rows: [],
          cashInTotal: zero,
          cashOutTotal: zero,
          netCashTotal: zero
        }));
      }

      if (pathname.endsWith('/reports/shifts')) {
        return Promise.resolve(jsonResponse({
          ...createShiftReport(),
          rows: []
        }));
      }

      return mockPlatformFetch(input, init);
    });

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Платежи'));
    expect(await screen.findByText('\u041e\u0442\u0447\u0451\u0442\u044b \u0437\u0430\u0433\u0440\u0443\u0436\u0435\u043d\u044b')).toBeInTheDocument();

    expect(await screen.findByText('Операций за период нет')).toBeInTheDocument();
    expect(screen.queryByText('Нет backend операций')).not.toBeInTheDocument();
  });

  it('does not replace an empty backend POS catalog with demo products', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);
    fetchMock.mockImplementation((input, init) => {
      const pathname = new URL(String(input)).pathname;
      if (pathname.endsWith('/pos/catalog')) {
        return Promise.resolve(jsonResponse([]));
      }

      return mockPlatformFetch(input, init);
    });

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('POS'));

    expect(await screen.findByText('Каталог POS пуст')).toBeInTheDocument();
    expect(screen.getByText('Корзина пуста')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Кола 0\.5/ })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Принять оплату/ })).toBeDisabled();
  });

  it('does not select a demo client when backend player search is empty', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);
    fetchMock.mockImplementation((input, init) => {
      const pathname = new URL(String(input)).pathname;
      if (pathname.endsWith('/players') && init?.method !== 'POST') {
        return Promise.resolve(jsonResponse([]));
      }

      return mockPlatformFetch(input, init);
    });

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Клиенты'));

    expect(await screen.findByText('Клиенты не найдены')).toBeInTheDocument();
    expect(screen.getByText('Нет выбранного клиента')).toBeInTheDocument();
    expect(screen.getByText('backend-данные не подменяются демо-карточкой')).toBeInTheDocument();
    expect(screen.queryByText('Madina S.')).not.toBeInTheDocument();
  });

  it('shows backend detail for the selected Payments operation', async () => {
    installSessionBridge();

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Платежи'));
    expect(await screen.findByText('\u041e\u0442\u0447\u0451\u0442\u044b \u0437\u0430\u0433\u0440\u0443\u0436\u0435\u043d\u044b')).toBeInTheDocument();

    expect(screen.getAllByText('99999999').length).toBeGreaterThan(1);
    expect(screen.getByText('1 строк · 1 шт.')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /opening/ }));
    expect((await screen.findAllByText('aaaaaaaa')).length).toBeGreaterThan(0);
    expect(screen.getAllByText('test').length).toBeGreaterThan(1);
  });

  it('downloads Payments report exports', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);
    const createObjectUrl = vi.fn(() => 'blob:payments');
    const revokeObjectUrl = vi.fn();
    Object.defineProperty(window.URL, 'createObjectURL', { value: createObjectUrl, configurable: true });
    Object.defineProperty(window.URL, 'revokeObjectURL', { value: revokeObjectUrl, configurable: true });
    const downloads: string[] = [];
    const createElement = document.createElement.bind(document);
    const createElementSpy = vi.spyOn(document, 'createElement').mockImplementation((tagName: string) => {
      const element = createElement(tagName);
      if (tagName.toLowerCase() === 'a') {
        Object.defineProperty(element, 'click', {
          value: () => downloads.push((element as HTMLAnchorElement).download),
          configurable: true
        });
      }

      return element;
    });

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Платежи'));
    expect(await screen.findByText('\u041e\u0442\u0447\u0451\u0442\u044b \u0437\u0430\u0433\u0440\u0443\u0436\u0435\u043d\u044b')).toBeInTheDocument();
    const exportPanel = document.querySelector('.payments-export-panel') as HTMLElement;
    fireEvent.click(within(exportPanel).getByRole('button', { name: /Экспорт CSV/ }));

    expect(await screen.findByText('Экспорт CSV: подтверждено')).toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([input]) => String(input).includes('/reports/sales/export.csv'))).toBe(true);
    expect(downloads.some((download) => download.startsWith('afk4-sales-report-') && download.endsWith('.csv'))).toBe(true);

    fireEvent.click(within(exportPanel).getByRole('button', { name: /Кассовый отчёт/ }));
    expect(await screen.findByText('Кассовый отчёт: подтверждено')).toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([input]) => String(input).includes('/reports/cash-operations/export.csv'))).toBe(true);
    expect(downloads.some((download) => download.startsWith('afk4-cash-report-') && download.endsWith('.csv'))).toBe(true);

    fireEvent.click(within(exportPanel).getByRole('button', { name: /Расхождения/ }));
    expect(await screen.findByText('Расхождения: подтверждено')).toBeInTheDocument();
    expect(downloads.some((download) => download.startsWith('afk4-shift-discrepancies-') && download.endsWith('.json'))).toBe(true);
    expect(createObjectUrl).toHaveBeenCalledTimes(3);
    expect(revokeObjectUrl).toHaveBeenCalledWith('blob:payments');
    createElementSpy.mockRestore();
  });

  it('closes the current shift from Payments through the backend', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Платежи'));
    expect(await screen.findByText('\u041e\u0442\u0447\u0451\u0442\u044b \u0437\u0430\u0433\u0440\u0443\u0436\u0435\u043d\u044b')).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('Факт в кассе'), { target: { value: '1120.00' } });
    fireEvent.change(screen.getByLabelText('Комментарий'), { target: { value: 'Смена закрыта оператором' } });
    fireEvent.click(screen.getByRole('button', { name: /Подготовить закрытие/ }));

    expect(await screen.findByRole('alertdialog', { name: 'Подтвердите закрытие смены' })).toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([input, init]) =>
      String(input).includes('/api/shifts/66666666-6666-6666-6666-666666666666/close') &&
      init?.method === 'POST')).toBe(false);
    fireEvent.click(screen.getByRole('button', { name: 'Закрыть смену' }));

    expect(await screen.findByText('Подготовить закрытие: подтверждено')).toBeInTheDocument();
    const closeCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/shifts/66666666-6666-6666-6666-666666666666/close') &&
      init?.method === 'POST');
    expect(closeCall).toBeDefined();
    const body = JSON.parse(String(closeCall?.[1]?.body));
    expect(body).toMatchObject({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      countedCash: { currencyCode: 'TJS', minorUnits: 112000 },
      closingNote: 'Смена закрыта оператором'
    });
    expect(body.idempotencyKey).toMatch(/^shift-close-/);
  });

  it('records a cash movement from Payments through the backend', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Платежи'));
    expect(await screen.findByText('\u041e\u0442\u0447\u0451\u0442\u044b \u0437\u0430\u0433\u0440\u0443\u0436\u0435\u043d\u044b')).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('Сумма'), { target: { value: '25.50' } });
    fireEvent.change(screen.getByLabelText('Причина'), { target: { value: 'Размен перед турниром' } });
    fireEvent.click(screen.getByRole('button', { name: 'Добавить движение' }));

    expect(await screen.findByText('Добавить движение: подтверждено')).toBeInTheDocument();
    const movementCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/shifts/66666666-6666-6666-6666-666666666666/cash-movements') &&
      init?.method === 'POST');
    expect(movementCall).toBeDefined();
    const body = JSON.parse(String(movementCall?.[1]?.body));
    expect(body).toMatchObject({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      movementType: 'cash_in',
      amount: { currencyCode: 'TJS', minorUnits: 2550 },
      reason: 'Размен перед турниром'
    });
    expect(body.idempotencyKey).toMatch(/^shift-cash-movement-/);
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
    expect((await screen.findAllByText(/\u041f\u043b\u0430\u0442\u0444\u043e\u0440\u043c\u0430 \u043f\u043e\u0434\u043a\u043b\u044e\u0447\u0435\u043d\u0430/)).length).toBeGreaterThan(0);
    expect((await screen.findAllByText('Madina S.')).length).toBeGreaterThan(0);
    const createReservationButton = screen.getByRole('button', { name: /Создать бронь/ });
    await waitFor(() => expect(createReservationButton).toBeEnabled());
    fireEvent.click(createReservationButton);

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

  it('keeps client reservation creation disabled without reservation manage permission', async () => {
    installSessionBridge(createSession({
      permissions: allOperatorPermissions.filter((permission) => permission !== 'reservations.manage')
    }));

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Клиенты'));
    expect((await screen.findAllByText(/\u041f\u043b\u0430\u0442\u0444\u043e\u0440\u043c\u0430 \u043f\u043e\u0434\u043a\u043b\u044e\u0447\u0435\u043d\u0430/)).length).toBeGreaterThan(0);

    expect(screen.getByRole('button', { name: /Создать бронь/ })).toBeDisabled();
  });

  it('purchases a backend package from the selected client card', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Клиенты'));
    expect((await screen.findAllByText(/\u041f\u043b\u0430\u0442\u0444\u043e\u0440\u043c\u0430 \u043f\u043e\u0434\u043a\u043b\u044e\u0447\u0435\u043d\u0430/)).length).toBeGreaterThan(0);
    await waitFor(() => expect(screen.getByRole('button', { name: /Купить пакет/ })).toBeEnabled());
    fireEvent.change(await screen.findByLabelText('Пакет для покупки'), { target: { value: 'cdcdcdcd-cdcd-cdcd-cdcd-cdcdcdcdcdcd' } });
    const purchasePackageButton = await screen.findByRole('button', { name: /Купить пакет/ });
    await waitFor(() => expect(purchasePackageButton).toBeEnabled());
    fireEvent.click(purchasePackageButton);

    expect(await screen.findByText('Купить пакет: подтверждено')).toBeInTheDocument();
    const purchaseCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/players/12121212-1212-1212-1212-121212121212/packages/purchases') &&
      init?.method === 'POST');
    expect(purchaseCall).toBeDefined();
    const body = JSON.parse(String(purchaseCall?.[1]?.body));
    expect(body).toMatchObject({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      packageDefinitionId: 'cdcdcdcd-cdcd-cdcd-cdcd-cdcdcdcdcdcd'
    });
    expect(body.idempotencyKey).toMatch(/^package-purchase-/);
  });

  it('shows active backend packages on the selected client profile', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Клиенты'));
    expect((await screen.findAllByText(/\u041f\u043b\u0430\u0442\u0444\u043e\u0440\u043c\u0430 \u043f\u043e\u0434\u043a\u043b\u044e\u0447\u0435\u043d\u0430/)).length).toBeGreaterThan(0);

    expect(await screen.findByText(/180 мин/)).toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([input, init]) =>
      String(input).includes('/api/players/12121212-1212-1212-1212-121212121212/packages') &&
      init?.method !== 'POST')).toBe(true);
  });

  it('tops up the selected client wallet from the Clients money form', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Клиенты'));
    expect((await screen.findAllByText(/\u041f\u043b\u0430\u0442\u0444\u043e\u0440\u043c\u0430 \u043f\u043e\u0434\u043a\u043b\u044e\u0447\u0435\u043d\u0430/)).length).toBeGreaterThan(0);
    fireEvent.change(screen.getByLabelText('Сумма пополнения'), { target: { value: '123.45' } });
    fireEvent.change(screen.getByLabelText('Причина пополнения'), { target: { value: 'cash desk deposit' } });
    const topUpWalletButton = screen.getByRole('button', { name: /Пополнить депозит/ });
    await waitFor(() => expect(topUpWalletButton).toBeEnabled());
    fireEvent.click(topUpWalletButton);

    expect(await screen.findByText('Пополнить депозит: подтверждено')).toBeInTheDocument();
    const topUpCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/players/12121212-1212-1212-1212-121212121212/wallet/top-ups') &&
      init?.method === 'POST');
    expect(topUpCall).toBeDefined();
    const body = JSON.parse(String(topUpCall?.[1]?.body));
    expect(body).toMatchObject({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      amount: { currencyCode: 'TJS', minorUnits: 12345 },
      reason: 'cash desk deposit'
    });
    expect(body.idempotencyKey).toMatch(/^wallet-top-up-/);
  });

  it('pays debt for a selected client from the Clients money form', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Клиенты'));
    expect((await screen.findAllByText(/\u041f\u043b\u0430\u0442\u0444\u043e\u0440\u043c\u0430 \u043f\u043e\u0434\u043a\u043b\u044e\u0447\u0435\u043d\u0430/)).length).toBeGreaterThan(0);
    await waitFor(() => expect(screen.getByRole('button', { name: /Пополнить депозит/ })).toBeEnabled());
    fireEvent.click(await screen.findByRole('button', { name: /Olim K\./ }));
    fireEvent.change(await screen.findByLabelText('Сумма долга'), { target: { value: '20.00' } });
    fireEvent.change(screen.getByLabelText('Причина долга'), { target: { value: 'cash debt payment' } });
    const payDebtButton = screen.getByRole('button', { name: /Списать долг/ });
    await waitFor(() => expect(payDebtButton).toBeEnabled());
    fireEvent.click(payDebtButton);

    expect(await screen.findByText('Списать долг: подтверждено')).toBeInTheDocument();
    const debtCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/players/34343434-3434-3434-3434-343434343434/debts/payments') &&
      init?.method === 'POST');
    expect(debtCall).toBeDefined();
    const body = JSON.parse(String(debtCall?.[1]?.body));
    expect(body).toMatchObject({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      amount: { currencyCode: 'TJS', minorUnits: 2000 },
      reason: 'cash debt payment'
    });
    expect(body.idempotencyKey).toMatch(/^debt-payment-/);
  });

  it('creates a backend player from the Clients new card form', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Клиенты'));
    expect((await screen.findAllByText(/\u041f\u043b\u0430\u0442\u0444\u043e\u0440\u043c\u0430 \u043f\u043e\u0434\u043a\u043b\u044e\u0447\u0435\u043d\u0430/)).length).toBeGreaterThan(0);
    fireEvent.change(screen.getByLabelText('Имя нового клиента'), { target: { value: 'Zarina N.' } });
    fireEvent.change(screen.getByLabelText('Телефон нового клиента'), { target: { value: '+992 90 777 88 99' } });
    fireEvent.click(screen.getByRole('button', { name: /Новая карта/ }));

    expect(await screen.findByText('Новая карта: подтверждено')).toBeInTheDocument();
    const createPlayerCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/players') &&
      init?.method === 'POST');
    expect(createPlayerCall).toBeDefined();
    const body = JSON.parse(String(createPlayerCall?.[1]?.body));
    expect(body).toMatchObject({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      displayName: 'Zarina N.',
      phoneNumber: '+992 90 777 88 99'
    });
    expect(body.idempotencyKey).toMatch(/^player-create-/);
  });

  it('creates a staff user from the Settings personnel form', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Настройки'));
    expect(await screen.findByText('\u041d\u0430\u0441\u0442\u0440\u043e\u0439\u043a\u0438 \u0437\u0430\u0433\u0440\u0443\u0436\u0435\u043d\u044b')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /Персонал/ }));
    fireEvent.change(screen.getByLabelText('Логин'), { target: { value: 'manager2' } });
    fireEvent.change(screen.getByLabelText('Имя'), { target: { value: 'Manager Two' } });
    fireEvent.change(screen.getByLabelText('Временный пароль'), { target: { value: 'Secret123!' } });
    fireEvent.change(screen.getByLabelText('Роль'), { target: { value: 'branch_manager' } });
    fireEvent.click(screen.getByRole('button', { name: 'Создать сотрудника' }));

    expect(await screen.findByText('Пригласить сотрудника: подтверждено')).toBeInTheDocument();
    const staffCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/staff') &&
      init?.method === 'POST');
    expect(staffCall).toBeDefined();
    const body = JSON.parse(String(staffCall?.[1]?.body));
    expect(body).toMatchObject({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      userName: 'manager2',
      displayName: 'Manager Two',
      password: 'Secret123!',
      roleNames: ['branch_manager']
    });
  });

  it('updates the selected staff user role from Settings personnel', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Настройки'));
    expect(await screen.findByText('\u041d\u0430\u0441\u0442\u0440\u043e\u0439\u043a\u0438 \u0437\u0430\u0433\u0440\u0443\u0436\u0435\u043d\u044b')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /Персонал/ }));
    fireEvent.click(screen.getByRole('button', { name: /Оператор смены/ }));
    fireEvent.change(screen.getByLabelText('Роль сотрудника'), { target: { value: 'technician' } });
    fireEvent.click(screen.getByRole('button', { name: 'Обновить роль' }));

    expect(await screen.findByText('Обновить роль: подтверждено')).toBeInTheDocument();
    const roleCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/staff/3db1367b-88c6-4b1c-99c3-bcbb5f4d5134/roles') &&
      init?.method === 'PATCH');
    expect(roleCall).toBeDefined();
    const body = JSON.parse(String(roleCall?.[1]?.body));
    expect(body).toEqual({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      roleNames: ['technician']
    });
  });

  it('updates the selected staff profile from Settings personnel', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Настройки'));
    expect(await screen.findByText('\u041d\u0430\u0441\u0442\u0440\u043e\u0439\u043a\u0438 \u0437\u0430\u0433\u0440\u0443\u0436\u0435\u043d\u044b')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /Персонал/ }));
    fireEvent.click(screen.getByRole('button', { name: /Оператор смены/ }));
    fireEvent.change(screen.getByLabelText('Логин профиля'), { target: { value: 'cashier.renamed' } });
    fireEvent.change(screen.getByLabelText('Имя профиля'), { target: { value: 'Кассир смены' } });
    fireEvent.click(screen.getByRole('button', { name: 'Обновить профиль' }));

    expect(await screen.findByText('Обновить профиль сотрудника: подтверждено')).toBeInTheDocument();
    expect(await screen.findByRole('button', { name: /Кассир смены/ })).toBeInTheDocument();
    const profileCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/staff/3db1367b-88c6-4b1c-99c3-bcbb5f4d5134/profile') &&
      init?.method === 'PATCH');
    expect(profileCall).toBeDefined();
    const body = JSON.parse(String(profileCall?.[1]?.body));
    expect(body).toEqual({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      userName: 'cashier.renamed',
      displayName: 'Кассир смены'
    });
  });

  it('deactivates the selected staff user from Settings personnel', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Настройки'));
    expect(await screen.findByText('\u041d\u0430\u0441\u0442\u0440\u043e\u0439\u043a\u0438 \u0437\u0430\u0433\u0440\u0443\u0436\u0435\u043d\u044b')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /Персонал/ }));
    fireEvent.click(screen.getByRole('button', { name: /Оператор смены/ }));
    fireEvent.click(screen.getByRole('button', { name: 'Отключить сотрудника' }));

    expect(await screen.findByText('Отключить сотрудника: подтверждено')).toBeInTheDocument();
    const stateCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/staff/3db1367b-88c6-4b1c-99c3-bcbb5f4d5134/state') &&
      init?.method === 'PATCH');
    expect(stateCall).toBeDefined();
    const body = JSON.parse(String(stateCall?.[1]?.body));
    expect(body).toEqual({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      isActive: false
    });
  });

  it('resets the selected staff password from Settings personnel', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Настройки'));
    expect(await screen.findByText('\u041d\u0430\u0441\u0442\u0440\u043e\u0439\u043a\u0438 \u0437\u0430\u0433\u0440\u0443\u0436\u0435\u043d\u044b')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /Персонал/ }));
    fireEvent.click(screen.getByRole('button', { name: /Оператор смены/ }));
    fireEvent.change(screen.getByLabelText('Новый пароль'), { target: { value: 'Reset123!' } });
    fireEvent.click(screen.getByRole('button', { name: 'Сбросить пароль' }));

    expect(await screen.findByText('Сбросить пароль: подтверждено')).toBeInTheDocument();
    const resetCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/staff/3db1367b-88c6-4b1c-99c3-bcbb5f4d5134/password-reset') &&
      init?.method === 'POST');
    expect(resetCall).toBeDefined();
    const body = JSON.parse(String(resetCall?.[1]?.body));
    expect(body).toEqual({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      newPassword: 'Reset123!'
    });
  });

  it('keeps staff role update disabled without role management permission', async () => {
    installSessionBridge(createSession({
      permissions: allOperatorPermissions.filter((permission) => permission !== 'identity.roles.manage')
    }));

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Настройки'));
    expect(await screen.findByText('\u041d\u0430\u0441\u0442\u0440\u043e\u0439\u043a\u0438 \u0437\u0430\u0433\u0440\u0443\u0436\u0435\u043d\u044b')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /Персонал/ }));

    expect(screen.getByRole('button', { name: 'Обновить роль' })).toBeDisabled();
  });

  it('saves the Settings branch profile through the backend', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Настройки'));
    expect(await screen.findByText('\u041d\u0430\u0441\u0442\u0440\u043e\u0439\u043a\u0438 \u0437\u0430\u0433\u0440\u0443\u0436\u0435\u043d\u044b')).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('Название клуба'), { target: { value: 'AFK4 Pilot' } });
    fireEvent.change(screen.getByLabelText('Город'), { target: { value: 'Khujand' } });
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    expect(await screen.findByText('Профиль клуба: подтверждено')).toBeInTheDocument();
    const profileCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/profile') &&
      init?.method === 'PATCH');
    expect(profileCall).toBeDefined();
    const body = JSON.parse(String(profileCall?.[1]?.body));
    expect(body).toMatchObject({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      name: 'AFK4 Pilot',
      city: 'Khujand'
    });
  });

  it('creates layout zones and seats from Settings layout form', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Настройки'));
    expect(await screen.findByText('\u041d\u0430\u0441\u0442\u0440\u043e\u0439\u043a\u0438 \u0437\u0430\u0433\u0440\u0443\u0436\u0435\u043d\u044b')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /Залы и ПК/ }));
    fireEvent.change(screen.getByLabelText('Название зала'), { target: { value: 'VIP Hall' } });
    fireEvent.change(screen.getByLabelText('Порядок зала'), { target: { value: '30' } });
    fireEvent.click(screen.getByRole('button', { name: 'Создать зал' }));

    expect(await screen.findByText('Добавить зал: подтверждено')).toBeInTheDocument();
    const zoneCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/layout/zones') &&
      init?.method === 'POST');
    expect(zoneCall).toBeDefined();
    expect(JSON.parse(String(zoneCall?.[1]?.body))).toMatchObject({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      name: 'VIP Hall',
      sortOrder: 30
    });

    fireEvent.change(screen.getByLabelText('Название ПК'), { target: { value: 'VIP-01' } });
    fireEvent.change(screen.getByLabelText('Порядок ПК'), { target: { value: '40' } });
    fireEvent.click(screen.getByRole('button', { name: 'Создать ПК' }));

    expect(await screen.findByText('Добавить ПК: подтверждено')).toBeInTheDocument();
    const seatCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/layout/seats') &&
      init?.method === 'POST');
    expect(seatCall).toBeDefined();
    expect(JSON.parse(String(seatCall?.[1]?.body))).toMatchObject({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      zoneId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
      name: 'VIP-01',
      sortOrder: 40
    });
  });

  it('updates selected layout zones and seats from Settings layout form', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Настройки'));
    expect(await screen.findByText('\u041d\u0430\u0441\u0442\u0440\u043e\u0439\u043a\u0438 \u0437\u0430\u0433\u0440\u0443\u0436\u0435\u043d\u044b')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /Залы и ПК/ }));
    fireEvent.click(screen.getAllByRole('button', { name: /Зал A/ })[0]);
    fireEvent.change(screen.getByLabelText('Название зала'), { target: { value: 'VIP Hall' } });
    fireEvent.change(screen.getByLabelText('Порядок зала'), { target: { value: '30' } });
    fireEvent.click(screen.getByRole('button', { name: 'Обновить зал' }));

    expect(await screen.findByText('Обновить зал: подтверждено')).toBeInTheDocument();
    const zoneCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/layout/zones/bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb') &&
      init?.method === 'PATCH');
    expect(zoneCall).toBeDefined();
    expect(JSON.parse(String(zoneCall?.[1]?.body))).toMatchObject({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      name: 'VIP Hall',
      sortOrder: 30
    });

    fireEvent.click(screen.getByRole('button', { name: /PC-01/ }));
    fireEvent.change(screen.getByLabelText('Название ПК'), { target: { value: 'VIP-01' } });
    fireEvent.change(screen.getByLabelText('Порядок ПК'), { target: { value: '40' } });
    fireEvent.click(screen.getByRole('button', { name: 'Обновить ПК' }));

    expect(await screen.findByText('Обновить ПК: подтверждено')).toBeInTheDocument();
    const seatCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/layout/seats/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa') &&
      init?.method === 'PATCH');
    expect(seatCall).toBeDefined();
    expect(JSON.parse(String(seatCall?.[1]?.body))).toMatchObject({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      zoneId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
      name: 'VIP-01',
      sortOrder: 40
    });
  });

  it('deletes selected layout seats and empty zones from Settings layout form', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Настройки'));
    expect(await screen.findByText('\u041d\u0430\u0441\u0442\u0440\u043e\u0439\u043a\u0438 \u0437\u0430\u0433\u0440\u0443\u0436\u0435\u043d\u044b')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /Залы и ПК/ }));
    fireEvent.click(screen.getByRole('button', { name: /PC-01/ }));
    fireEvent.click(screen.getByRole('button', { name: 'Удалить ПК' }));

    const seatDeleteDialog = await screen.findByRole('alertdialog', { name: 'Подтвердите удаление ПК' });
    expect(fetchMock.mock.calls.some(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/layout/seats/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa') &&
      init?.method === 'DELETE')).toBe(false);
    fireEvent.click(within(seatDeleteDialog).getByRole('button', { name: 'Подтвердить удаление ПК' }));

    expect(await screen.findByText('Удалить ПК: подтверждено')).toBeInTheDocument();
    const seatDeleteCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/layout/seats/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa?organizationId=0c04d6c0-bfa8-4e26-9263-fc0d307d0f08') &&
      init?.method === 'DELETE');
    expect(seatDeleteCall).toBeDefined();

    fireEvent.click(screen.getAllByRole('button', { name: /Зал A/ })[0]);
    fireEvent.click(screen.getByRole('button', { name: 'Удалить зал' }));

    const zoneDeleteDialog = await screen.findByRole('alertdialog', { name: 'Подтвердите удаление зала' });
    expect(fetchMock.mock.calls.some(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/layout/zones/bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb') &&
      init?.method === 'DELETE')).toBe(false);
    fireEvent.click(within(zoneDeleteDialog).getByRole('button', { name: 'Подтвердить удаление зала' }));

    expect(await screen.findByText('Удалить зал: подтверждено')).toBeInTheDocument();
    const zoneDeleteCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/layout/zones/bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb?organizationId=0c04d6c0-bfa8-4e26-9263-fc0d307d0f08') &&
      init?.method === 'DELETE');
    expect(zoneDeleteCall).toBeDefined();
  });

  it('creates device enrollment codes and assigns devices to seats from Settings', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Настройки'));
    expect(await screen.findByText('\u041d\u0430\u0441\u0442\u0440\u043e\u0439\u043a\u0438 \u0437\u0430\u0433\u0440\u0443\u0436\u0435\u043d\u044b')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /Залы и ПК/ }));
    fireEvent.change(screen.getByLabelText('Срок кода, сек'), { target: { value: '600' } });
    fireEvent.click(screen.getByRole('button', { name: 'Создать код подключения' }));

    expect(await screen.findByText('Создать код подключения: подтверждено')).toBeInTheDocument();
    expect(screen.getByDisplayValue('AFK4-DEVICE-1234')).toBeInTheDocument();
    const enrollmentCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/device-enrollment-codes') &&
      init?.method === 'POST');
    expect(enrollmentCall).toBeDefined();
    expect(JSON.parse(String(enrollmentCall?.[1]?.body))).toMatchObject({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      expiresInSeconds: 600
    });

    fireEvent.change(screen.getByLabelText('Устройство'), { target: { value: '33333333-3333-3333-3333-333333333333' } });
    fireEvent.click(screen.getByRole('button', { name: 'Назначить устройство' }));

    expect(await screen.findByText('Назначить устройство: подтверждено')).toBeInTheDocument();
    const assignmentCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/devices/33333333-3333-3333-3333-333333333333/seat-assignment') &&
      init?.method === 'POST');
    expect(assignmentCall).toBeDefined();
    expect(JSON.parse(String(assignmentCall?.[1]?.body))).toMatchObject({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      seatId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
    });
    expect(fetchMock.mock.calls.some(([input, init]) =>
      String(input).includes('/api/devices/33333333-3333-3333-3333-333333333333') &&
      init?.method === 'GET')).toBe(true);
    expect(screen.getByDisplayValue('PC-02 · PC-01')).toBeInTheDocument();
    expect(screen.getByText('Агент')).toBeInTheDocument();
    expect(screen.getAllByText('0.1.14').length).toBeGreaterThan(0);
    expect(screen.getByText('не выполнена')).toBeInTheDocument();
    expect(screen.getAllByText('Агент не ответил').length).toBeGreaterThan(0);
    expect(fetchMock.mock.calls.some(([input, init]) =>
      String(input).includes('/api/devices/33333333-3333-3333-3333-333333333333/commands?limit=25') &&
      init?.method === 'GET')).toBe(true);

    fireEvent.click(screen.getByRole('button', { name: 'Сменить ключ' }));
    expect(await screen.findByText('Сменить ключ: подтверждено')).toBeInTheDocument();
    expect(screen.getByDisplayValue('готов к отзыву для PC-02')).toBeInTheDocument();
    expect(screen.getByDisplayValue('создан')).toBeInTheDocument();
    expect(screen.getByDisplayValue('device-secret-after-rotation')).toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([input, init]) =>
      String(input).includes('/api/devices/33333333-3333-3333-3333-333333333333/credentials/rotate') &&
      init?.method === 'POST')).toBe(true);

    fireEvent.click(screen.getByRole('button', { name: 'Отозвать ключ' }));
    const revokeDialog = await screen.findByRole('alertdialog', { name: 'Подтвердите отзыв ключа' });
    expect(revokeDialog).toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([input, init]) =>
      String(input).includes('/api/devices/33333333-3333-3333-3333-333333333333/credentials/23232323-2323-2323-2323-232323232323/revoke') &&
      init?.method === 'POST')).toBe(false);
    fireEvent.click(within(revokeDialog).getByRole('button', { name: 'Отозвать ключ' }));
    expect(await screen.findByText('Отозвать ключ: подтверждено')).toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([input, init]) =>
      String(input).includes('/api/devices/33333333-3333-3333-3333-333333333333/credentials/23232323-2323-2323-2323-232323232323/revoke') &&
      init?.method === 'POST')).toBe(true);
  });

  it('loads branch device inventory in Settings and opens a selected device card', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Настройки'));
    expect(await screen.findByText('\u041d\u0430\u0441\u0442\u0440\u043e\u0439\u043a\u0438 \u0437\u0430\u0433\u0440\u0443\u0436\u0435\u043d\u044b')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /Залы и ПК/ }));

    const branchHistory = await screen.findByLabelText('История команд филиала');
    expect(branchHistory).toBeInTheDocument();
    expect(within(branchHistory).getByText('PC-03')).toBeInTheDocument();
    expect(within(branchHistory).getByText(/Обновление сессии/)).toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/device-commands?limit=50') &&
      init?.method === 'GET')).toBe(true);
    fireEvent.click(screen.getByRole('button', { name: 'Обновить историю команд' }));
    expect(await screen.findByText('Обновить историю команд: подтверждено')).toBeInTheDocument();

    expect(await screen.findByRole('button', { name: /PC-03/ })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /PC-03/ }));
    expect(screen.getByLabelText('Устройство')).toHaveValue('44444444-4444-4444-8444-444444444444');
    expect(screen.getByText(/в работе 1/)).toBeInTheDocument();
    expect(screen.getByText(/ошибок 1/)).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Открыть карточку устройства' }));

    expect(await screen.findByText('Открыть карточку устройства: подтверждено')).toBeInTheDocument();
    expect(screen.getByDisplayValue('PC-03 · PC-01')).toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/devices') &&
      init?.method === 'GET')).toBe(true);
    expect(fetchMock.mock.calls.some(([input, init]) =>
      String(input).includes('/api/devices/44444444-4444-4444-8444-444444444444') &&
      init?.method === 'GET')).toBe(true);
    expect(fetchMock.mock.calls.some(([input, init]) =>
      String(input).includes('/api/devices/44444444-4444-4444-8444-444444444444/commands?limit=25') &&
      init?.method === 'GET')).toBe(true);
  });

  it('dispatches device commands from Settings device tools', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Настройки'));
    expect(await screen.findByText('\u041d\u0430\u0441\u0442\u0440\u043e\u0439\u043a\u0438 \u0437\u0430\u0433\u0440\u0443\u0436\u0435\u043d\u044b')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /Залы и ПК/ }));
    fireEvent.change(screen.getByLabelText('Устройство'), { target: { value: '33333333-3333-3333-3333-333333333333' } });
    fireEvent.change(screen.getByLabelText('Команда'), { target: { value: 'unlock' } });
    fireEvent.change(screen.getByLabelText('Причина команды'), { target: { value: 'manual unlock check' } });
    fireEvent.click(screen.getByRole('button', { name: 'Отправить команду' }));

    expect(await screen.findByText('Отправить команду: подтверждено')).toBeInTheDocument();
    const commandCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/devices/33333333-3333-3333-3333-333333333333/commands') &&
      init?.method === 'POST');
    expect(commandCall).toBeDefined();
    expect(JSON.parse(String(commandCall?.[1]?.body))).toMatchObject({
      type: 'unlock',
      payload: { reason: 'manual unlock check', source: 'operator-settings' }
    });
    expect(screen.getByDisplayValue('Разблокировка · отправлена')).toBeInTheDocument();
  });

  it('creates a POS category and product from Settings', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Настройки'));
    expect(await screen.findByText('\u041d\u0430\u0441\u0442\u0440\u043e\u0439\u043a\u0438 \u0437\u0430\u0433\u0440\u0443\u0436\u0435\u043d\u044b')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /POS и склад/ }));
    fireEvent.change(screen.getByLabelText('Категория'), { target: { value: 'Snacks' } });
    fireEvent.change(screen.getByLabelText('Товар'), { target: { value: 'Energy Bar' } });
    fireEvent.change(screen.getByLabelText('SKU'), { target: { value: 'BAR-01' } });
    fireEvent.change(screen.getByLabelText('Цена'), { target: { value: '35.50' } });
    fireEvent.click(screen.getByRole('button', { name: 'Создать товар' }));

    expect(await screen.findByText('Создать товар: подтверждено')).toBeInTheDocument();
    const categoryCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/pos/categories') &&
      init?.method === 'POST');
    expect(categoryCall).toBeDefined();
    const categoryBody = JSON.parse(String(categoryCall?.[1]?.body));
    expect(categoryBody).toMatchObject({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      name: 'Snacks'
    });
    expect(categoryBody.idempotencyKey).toMatch(/^pos-category-create-/);

    const productCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/pos/products') &&
      init?.method === 'POST');
    expect(productCall).toBeDefined();
    const productBody = JSON.parse(String(productCall?.[1]?.body));
    expect(productBody).toMatchObject({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      categoryId: '88888888-8888-8888-8888-888888888888',
      name: 'Energy Bar',
      sku: 'BAR-01',
      price: { currencyCode: 'TJS', minorUnits: 3550 },
      trackStock: true,
      allowNegativeStock: false
    });
    expect(productBody.idempotencyKey).toMatch(/^pos-product-create-/);
  });

  it('updates and deactivates a POS product from Settings', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Настройки'));
    expect(await screen.findByText('\u041d\u0430\u0441\u0442\u0440\u043e\u0439\u043a\u0438 \u0437\u0430\u0433\u0440\u0443\u0436\u0435\u043d\u044b')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /^POS и склад/ }));
    fireEvent.click(screen.getAllByRole('button', { name: /Cola 0.5/ })[0]);
    fireEvent.change(screen.getByLabelText('Товар'), { target: { value: 'Cola Zero 0.5' } });
    fireEvent.change(screen.getByLabelText('SKU'), { target: { value: 'COLA-ZERO-05' } });
    fireEvent.change(screen.getByLabelText('Цена'), { target: { value: '13.00' } });
    fireEvent.change(screen.getByLabelText('Минусовой остаток'), { target: { value: 'yes' } });
    fireEvent.click(screen.getByRole('button', { name: 'Обновить товар' }));

    expect(await screen.findByText('Обновить товар: подтверждено')).toBeInTheDocument();
    const updateCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/pos/products/77777777-7777-7777-7777-777777777777') &&
      init?.method === 'PATCH');
    expect(updateCall).toBeDefined();
    const updateBody = JSON.parse(String(updateCall?.[1]?.body));
    expect(updateBody).toMatchObject({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      categoryId: '88888888-8888-8888-8888-888888888888',
      name: 'Cola Zero 0.5',
      sku: 'COLA-ZERO-05',
      price: { currencyCode: 'TJS', minorUnits: 1300 },
      trackStock: true,
      allowNegativeStock: true,
      isActive: true
    });

    fireEvent.click(screen.getByRole('button', { name: 'Снять с продажи' }));
    expect(await screen.findByText('Снять с продажи: подтверждено')).toBeInTheDocument();
    const deactivateCall = fetchMock.mock.calls.filter(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/pos/products/77777777-7777-7777-7777-777777777777') &&
      init?.method === 'PATCH').at(-1);
    expect(deactivateCall).toBeDefined();
    const deactivateBody = JSON.parse(String(deactivateCall?.[1]?.body));
    expect(deactivateBody).toMatchObject({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      name: 'Cola Zero 0.5',
      sku: 'COLA-ZERO-05',
      isActive: false
    });
  });

  it('records an inventory stock movement from Settings POS and stock', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Настройки'));
    expect(await screen.findByText('\u041d\u0430\u0441\u0442\u0440\u043e\u0439\u043a\u0438 \u0437\u0430\u0433\u0440\u0443\u0436\u0435\u043d\u044b')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /^POS и склад/ }));
    expect(await screen.findByText(/operator stock count correction/)).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('Тип'), { target: { value: 'adjustment' } });
    fireEvent.change(screen.getByLabelText('Кол-во'), { target: { value: '-3' } });
    fireEvent.change(screen.getByLabelText('Себестоимость'), { target: { value: '0.00' } });
    fireEvent.change(screen.getByLabelText('Причина'), { target: { value: 'operator stock count correction' } });
    fireEvent.click(screen.getByRole('button', { name: 'Записать движение' }));

    expect(await screen.findByText('Записать движение: подтверждено')).toBeInTheDocument();
    const stockCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/inventory/stock-movements') &&
      init?.method === 'POST');
    expect(stockCall).toBeDefined();
    const body = JSON.parse(String(stockCall?.[1]?.body));
    expect(body).toMatchObject({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      productId: '77777777-7777-7777-7777-777777777777',
      movementType: 'adjustment',
      quantityDelta: -3,
      unitCost: { currencyCode: 'TJS', minorUnits: 0 },
      reason: 'operator stock count correction'
    });
    expect(body.idempotencyKey).toMatch(/^stock-movement-create-/);
    expect(fetchMock.mock.calls.some(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/inventory/stock-movements?limit=8') &&
      init?.method !== 'POST')).toBe(true);
  });

  it('creates a package definition from Settings tariffs', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Настройки'));
    expect(await screen.findByText('\u041d\u0430\u0441\u0442\u0440\u043e\u0439\u043a\u0438 \u0437\u0430\u0433\u0440\u0443\u0436\u0435\u043d\u044b')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /^Тарифы/ }));
    fireEvent.change(screen.getByLabelText('Пакет'), { target: { value: 'Weekend 10h' } });
    fireEvent.change(screen.getByLabelText('Цена'), { target: { value: '320.00' } });
    fireEvent.change(screen.getByLabelText('Минуты'), { target: { value: '600' } });
    fireEvent.change(screen.getByLabelText('Бонус'), { target: { value: '60' } });
    fireEvent.change(screen.getByLabelText('Дней'), { target: { value: '45' } });
    fireEvent.click(screen.getByRole('button', { name: 'Создать пакет' }));

    expect(await screen.findByText('Создать пакет: подтверждено')).toBeInTheDocument();
    const packageCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/packages') &&
      init?.method === 'POST');
    expect(packageCall).toBeDefined();
    const body = JSON.parse(String(packageCall?.[1]?.body));
    expect(body).toMatchObject({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      name: 'Weekend 10h',
      price: { currencyCode: 'TJS', minorUnits: 32000 },
      includedSeconds: 36000,
      bonusSeconds: 3600,
      expiresAfterDays: 45
    });
    expect(body.idempotencyKey).toMatch(/^package-definition-create-/);
  });

  it('updates and deactivates a package definition from Settings tariffs', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Настройки'));
    expect(await screen.findByText('\u041d\u0430\u0441\u0442\u0440\u043e\u0439\u043a\u0438 \u0437\u0430\u0433\u0440\u0443\u0436\u0435\u043d\u044b')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /^Тарифы/ }));
    fireEvent.click(screen.getByRole('button', { name: /Night 5h/ }));
    fireEvent.change(screen.getByLabelText('Пакет'), { target: { value: 'Night 6h' } });
    fireEvent.change(screen.getByLabelText('Цена'), { target: { value: '300.00' } });
    fireEvent.change(screen.getByLabelText('Минуты'), { target: { value: '360' } });
    fireEvent.change(screen.getByLabelText('Бонус'), { target: { value: '40' } });
    fireEvent.change(screen.getByLabelText('Дней'), { target: { value: '45' } });
    fireEvent.click(screen.getByRole('button', { name: 'Обновить пакет' }));

    expect(await screen.findByText('Обновить пакет: подтверждено')).toBeInTheDocument();
    const updateCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/packages/abababab-abab-abab-abab-abababababab') &&
      init?.method === 'PATCH');
    expect(updateCall).toBeDefined();
    const updateBody = JSON.parse(String(updateCall?.[1]?.body));
    expect(updateBody).toMatchObject({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      name: 'Night 6h',
      price: { currencyCode: 'TJS', minorUnits: 30000 },
      includedSeconds: 21600,
      bonusSeconds: 2400,
      expiresAfterDays: 45,
      isActive: true
    });

    fireEvent.click(screen.getByRole('button', { name: 'Снять пакет' }));
    expect(await screen.findByText('Снять пакет: подтверждено')).toBeInTheDocument();
    const deactivateCall = fetchMock.mock.calls.filter(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/packages/abababab-abab-abab-abab-abababababab') &&
      init?.method === 'PATCH').at(-1);
    expect(deactivateCall).toBeDefined();
    const deactivateBody = JSON.parse(String(deactivateCall?.[1]?.body));
    expect(deactivateBody).toMatchObject({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      name: 'Night 6h',
      isActive: false
    });
  });

  it('creates a tariff and rule version from Settings tariffs', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Настройки'));
    expect(await screen.findByText('\u041d\u0430\u0441\u0442\u0440\u043e\u0439\u043a\u0438 \u0437\u0430\u0433\u0440\u0443\u0436\u0435\u043d\u044b')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /^Тарифы/ }));
    fireEvent.change(screen.getByLabelText('Название тарифа'), { target: { value: 'Morning Hour' } });
    fireEvent.change(screen.getByLabelText('Цена/час'), { target: { value: '96.00' } });
    fireEvent.change(screen.getByLabelText('Минимум мин'), { target: { value: '20' } });
    fireEvent.change(screen.getByLabelText('Округление мин'), { target: { value: '10' } });
    fireEvent.click(screen.getByRole('button', { name: 'Создать тариф' }));

    expect(await screen.findByText('Создать тариф: подтверждено')).toBeInTheDocument();
    const tariffCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).endsWith('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/tariffs') &&
      init?.method === 'POST');
    expect(tariffCall).toBeDefined();
    const tariffBody = JSON.parse(String(tariffCall?.[1]?.body));
    expect(tariffBody).toMatchObject({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      name: 'Morning Hour'
    });
    expect(tariffBody.idempotencyKey).toMatch(/^tariff-create-/);

    const versionCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/tariffs/25252525-2525-2525-2525-252525252525/versions') &&
      init?.method === 'POST');
    expect(versionCall).toBeDefined();
    const versionBody = JSON.parse(String(versionCall?.[1]?.body));
    expect(versionBody).toMatchObject({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      tariffId: '25252525-2525-2525-2525-252525252525',
      currencyCode: 'TJS',
      pricePerMinuteMinorUnits: 160,
      minimumBillableMinutes: 20,
      roundingIncrementMinutes: 10
    });
    expect(versionBody.effectiveFromUtc).toEqual(expect.any(String));
    expect(versionBody.idempotencyKey).toMatch(/^tariff-version-create-/);
  });

  it('updates and deactivates a selected tariff from Settings tariffs', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Настройки'));
    expect(await screen.findByText('\u041d\u0430\u0441\u0442\u0440\u043e\u0439\u043a\u0438 \u0437\u0430\u0433\u0440\u0443\u0436\u0435\u043d\u044b')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /^Тарифы/ }));
    fireEvent.click(screen.getByRole('button', { name: /Standard/ }));
    fireEvent.change(screen.getByLabelText('Название тарифа'), { target: { value: 'Standard Plus' } });
    fireEvent.change(screen.getByLabelText('Цена/час'), { target: { value: '120.00' } });
    fireEvent.change(screen.getByLabelText('Минимум мин'), { target: { value: '20' } });
    fireEvent.change(screen.getByLabelText('Округление мин'), { target: { value: '10' } });
    fireEvent.click(screen.getByRole('button', { name: 'Обновить тариф' }));

    expect(await screen.findByText('Обновить тариф: подтверждено')).toBeInTheDocument();
    const tariffCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/tariffs/16161616-1616-1616-1616-161616161616') &&
      !String(input).includes('/versions/') &&
      init?.method === 'PATCH');
    expect(tariffCall).toBeDefined();
    const tariffBody = JSON.parse(String(tariffCall?.[1]?.body));
    expect(tariffBody).toEqual({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      name: 'Standard Plus',
      isActive: true
    });

    const versionCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/tariffs/16161616-1616-1616-1616-161616161616/versions/17171717-1717-1717-1717-171717171717') &&
      init?.method === 'PATCH');
    expect(versionCall).toBeDefined();
    const versionBody = JSON.parse(String(versionCall?.[1]?.body));
    expect(versionBody).toMatchObject({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      currencyCode: 'TJS',
      pricePerMinuteMinorUnits: 200,
      minimumBillableMinutes: 20,
      roundingIncrementMinutes: 10,
      isActive: true
    });

    fireEvent.click(screen.getByRole('button', { name: 'Снять тариф' }));
    expect(await screen.findByText('Снять тариф: подтверждено')).toBeInTheDocument();
    const deactivateCall = fetchMock.mock.calls.filter(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/tariffs/16161616-1616-1616-1616-161616161616/versions/17171717-1717-1717-1717-171717171717') &&
      init?.method === 'PATCH').at(-1);
    expect(deactivateCall).toBeDefined();
    const deactivateBody = JSON.parse(String(deactivateCall?.[1]?.body));
    expect(deactivateBody).toMatchObject({ isActive: false });
  });

  it('registers update packages and creates rollouts from Settings integrations', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Настройки'));
    expect(await screen.findByText('\u041d\u0430\u0441\u0442\u0440\u043e\u0439\u043a\u0438 \u0437\u0430\u0433\u0440\u0443\u0436\u0435\u043d\u044b')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /^Интеграции/ }));
    fireEvent.change(screen.getByLabelText('Версия'), { target: { value: '0.2.0' } });
    fireEvent.change(screen.getByLabelText('URL артефакта'), { target: { value: 'https://updates.afk4.test/operator-app/0.2.0/operator-app.msi' } });
    fireEvent.change(screen.getByLabelText('Подпись'), { target: { value: 'operator-package-signature' } });
    fireEvent.change(screen.getByLabelText('Размер, байты'), { target: { value: '4096' } });
    fireEvent.change(screen.getByLabelText('Описание релиза'), { target: { value: 'Пакет оператора 0.2.0.' } });
    fireEvent.click(screen.getByRole('button', { name: 'Зарегистрировать пакет' }));

    expect(await screen.findByText('Зарегистрировать пакет обновления: подтверждено')).toBeInTheDocument();
    const packageCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/updates/packages') &&
      init?.method === 'POST');
    expect(packageCall).toBeDefined();
    const packageBody = JSON.parse(String(packageCall?.[1]?.body));
    expect(packageBody).toMatchObject({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      component: 'operator-app',
      version: '0.2.0',
      channel: 'internal',
      artifactUri: 'https://updates.afk4.test/operator-app/0.2.0/operator-app.msi',
      signature: 'operator-package-signature',
      signatureAlgorithm: 'ECDSA-P256-SHA256-IEEE-P1363',
      sizeBytes: 4096,
      releaseNotes: 'Пакет оператора 0.2.0.'
    });

    fireEvent.change(screen.getByLabelText('Доля %'), { target: { value: '25' } });
    await waitFor(() => expect(screen.getByLabelText('Пакет для раскатки')).toHaveValue('19191919-1919-1919-1919-191919191919'));
    fireEvent.change(screen.getByLabelText('Старт UTC'), { target: { value: '2026-05-21T10:00:00Z' } });
    fireEvent.change(screen.getAllByLabelText('Причина раскатки')[0], { target: { value: 'Пилотная раскатка.' } });
    fireEvent.click(screen.getByRole('button', { name: 'Создать раскатку' }));

    expect(await screen.findByText('Создать раскатку обновления: подтверждено')).toBeInTheDocument();
    const rolloutCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/updates/rollouts') &&
      init?.method === 'POST');
    expect(rolloutCall).toBeDefined();
    const rolloutBody = JSON.parse(String(rolloutCall?.[1]?.body));
    expect(rolloutBody).toMatchObject({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      updatePackageId: '19191919-1919-1919-1919-191919191919',
      channel: 'internal',
      targetKind: 'branch',
      targetDeviceIds: [],
      batchPercent: 25,
      startsAtUtc: '2026-05-21T10:00:00.000Z',
      reason: 'Пилотная раскатка.'
    });
  });

  it('changes update package and rollout states from Settings integrations', async () => {
    installSessionBridge();
    const fetchMock = vi.mocked(fetch);

    render(<App />);

    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    fireEvent.click(screen.getByTitle('Настройки'));
    expect(await screen.findByText('\u041d\u0430\u0441\u0442\u0440\u043e\u0439\u043a\u0438 \u0437\u0430\u0433\u0440\u0443\u0436\u0435\u043d\u044b')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /^Интеграции/ }));
    expect(screen.getByText('цель достигнута')).toBeInTheDocument();
    expect(screen.getByText('Установлено')).toBeInTheDocument();
    expect(screen.getByText('PC-02')).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('Состояние пакета'), { target: { value: 'validated' } });
    fireEvent.change(screen.getByLabelText('Причина пакета'), { target: { value: 'Подпись проверена.' } });
    fireEvent.click(screen.getByRole('button', { name: 'Изменить состояние пакета' }));

    const packageStateDialog = await screen.findByRole('alertdialog', { name: 'Подтвердите состояние пакета' });
    expect(fetchMock.mock.calls.some(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/updates/packages/15151515-1515-1515-1515-151515151515/state') &&
      init?.method === 'POST')).toBe(false);
    fireEvent.click(within(packageStateDialog).getByRole('button', { name: 'Подтвердить состояние пакета' }));

    expect(await screen.findByText('Изменить состояние пакета: подтверждено')).toBeInTheDocument();
    const packageStateCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/updates/packages/15151515-1515-1515-1515-151515151515/state') &&
      init?.method === 'POST');
    expect(packageStateCall).toBeDefined();
    const packageStateBody = JSON.parse(String(packageStateCall?.[1]?.body));
    expect(packageStateBody).toMatchObject({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      state: 'validated',
      reason: 'Подпись проверена.'
    });

    fireEvent.change(screen.getByLabelText('Состояние раскатки'), { target: { value: 'paused' } });
    fireEvent.change(screen.getAllByLabelText('Причина раскатки')[1], { target: { value: 'Пауза для проверки ошибок.' } });
    fireEvent.click(screen.getByRole('button', { name: 'Изменить состояние раскатки' }));

    const rolloutStateDialog = await screen.findByRole('alertdialog', { name: 'Подтвердите состояние раскатки' });
    expect(fetchMock.mock.calls.some(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/updates/rollouts/14141414-1414-1414-1414-141414141414/state') &&
      init?.method === 'POST')).toBe(false);
    fireEvent.click(within(rolloutStateDialog).getByRole('button', { name: 'Подтвердить состояние раскатки' }));

    expect(await screen.findByText('Изменить состояние раскатки: подтверждено')).toBeInTheDocument();
    const rolloutStateCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/updates/rollouts/14141414-1414-1414-1414-141414141414/state') &&
      init?.method === 'POST');
    expect(rolloutStateCall).toBeDefined();
    const rolloutStateBody = JSON.parse(String(rolloutStateCall?.[1]?.body));
    expect(rolloutStateBody).toMatchObject({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      state: 'paused',
      reason: 'Пауза для проверки ошибок.'
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

  if (pathname.endsWith('/commands') && init?.method !== 'POST') {
    return jsonResponse(createDeviceCommandHistory());
  }

  if (pathname.endsWith('/device-commands') && pathname.includes('/api/branches/') && init?.method !== 'POST') {
    return jsonResponse(createBranchDeviceCommandHistory());
  }

  if (pathname.endsWith('/commands') && init?.method === 'POST') {
    const body = JSON.parse(String(init.body));
    return jsonResponse(createDeviceCommand(body.type, body.payload));
  }

  if (pathname.endsWith('/credentials/rotate') && init?.method === 'POST') {
    const parts = pathname.split('/');
    return jsonResponse(createRotatedDeviceCredential({ deviceId: parts[parts.length - 3] }));
  }

  if (pathname.includes('/credentials/') && pathname.endsWith('/revoke') && init?.method === 'POST') {
    const parts = pathname.split('/');
    return jsonResponse(createRevokedDeviceCredential({
      deviceId: parts[parts.length - 4],
      credentialId: parts[parts.length - 2]
    }));
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

  if (pathname.endsWith('/pos/categories') && init?.method === 'POST') {
    const body = JSON.parse(String(init.body));
    return jsonResponse(createPosCategory(body));
  }

  if (pathname.includes('/pos/products/') && init?.method === 'PATCH') {
    const body = JSON.parse(String(init.body));
    const productId = pathname.split('/').at(-1);
    return jsonResponse(createPosProduct({ ...body, productId }));
  }

  if (pathname.endsWith('/pos/products') && init?.method === 'POST') {
    const body = JSON.parse(String(init.body));
    return jsonResponse(createPosProduct(body));
  }

  if (pathname.endsWith('/pos/catalog')) {
    return jsonResponse(createPosCatalog());
  }

  if (pathname.endsWith('/inventory/stock-movements') && init?.method !== 'POST') {
    return jsonResponse(createStockMovements());
  }

  if (pathname.endsWith('/inventory/stock-movements') && init?.method === 'POST') {
    const body = JSON.parse(String(init.body));
    return jsonResponse(createStockMovement(body));
  }

  if (pathname.endsWith('/shifts/current')) {
    return jsonResponse(createCurrentShift());
  }

  if (pathname.endsWith('/shifts/open') && init?.method === 'POST') {
    return jsonResponse(createCurrentShift());
  }

  if (pathname.includes('/shifts/') && pathname.endsWith('/cash-movements') && init?.method === 'POST') {
    const body = JSON.parse(String(init.body));
    return jsonResponse(createCashMovement(body));
  }

  if (pathname.includes('/shifts/') && pathname.endsWith('/close') && init?.method === 'POST') {
    const body = JSON.parse(String(init.body));
    return jsonResponse(createClosedShift(body));
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

  if (pathname.includes('/receipts/')) {
    return jsonResponse(createReceipt());
  }

  if (pathname.endsWith('/pos/sales') && init?.method === 'POST') {
    return jsonResponse(createPosSale('draft'));
  }

  if (pathname.includes('/payments/manual')) {
    return jsonResponse(createPosSale('paid'));
  }

  if (pathname.includes('/pos/sales/') && pathname.endsWith('/refunds')) {
    return jsonResponse(createPosSale('refunded'));
  }

  if (pathname.includes('/pos/sales/') && pathname.endsWith('/void')) {
    return jsonResponse(createPosSale('voided'));
  }

  if (pathname.includes('/pos/sales/') && init?.method !== 'POST') {
    return jsonResponse(createPosSale('paid'));
  }

  if (pathname.endsWith('/players') && init?.method === 'POST') {
    const body = JSON.parse(String(init.body));
    return jsonResponse(createPlayerAccount(body));
  }

  if (pathname.endsWith('/players')) {
    return jsonResponse(createPlayers());
  }

  if (pathname.endsWith('/wallet-summary')) {
    if (pathname.includes('/players/34343434-3434-3434-3434-343434343434/')) {
      return jsonResponse(createWalletSummary({
        playerAccountId: '34343434-3434-3434-3434-343434343434',
        walletBalance: { currencyCode: 'TJS', minorUnits: 0 },
        debtBalance: { currencyCode: 'TJS', minorUnits: 3500 },
        recentEntries: []
      }));
    }

    return jsonResponse(createWalletSummary());
  }

  if (pathname.includes('/players/') && pathname.endsWith('/wallet/top-ups') && init?.method === 'POST') {
    const body = JSON.parse(String(init.body));
    return jsonResponse(createWalletSummary({
      walletBalance: body.amount
    }));
  }

  if (pathname.includes('/players/') && pathname.endsWith('/debts/payments') && init?.method === 'POST') {
    const body = JSON.parse(String(init.body));
    return jsonResponse(createWalletSummary({
      debtBalance: { currencyCode: body.amount.currencyCode, minorUnits: 0 }
    }));
  }

  if (pathname.endsWith('/profile') && init?.method === 'PATCH') {
    const body = JSON.parse(String(init.body));
    return jsonResponse(createBranchProfile(body));
  }

  if (pathname.endsWith('/profile')) {
    return jsonResponse(createBranchProfile());
  }

  if (pathname.includes('/players/') && pathname.endsWith('/packages/purchases')) {
    const body = JSON.parse(String(init?.body));
    return jsonResponse(createPlayerPackage(body));
  }

  if (pathname.includes('/branches/') && !pathname.includes('/updates/') && pathname.includes('/packages/') && init?.method === 'PATCH') {
    const body = JSON.parse(String(init.body));
    const packageDefinitionId = pathname.split('/').at(-1);
    return jsonResponse(createPackageDefinition({ ...body, packageDefinitionId }));
  }

  if (pathname.includes('/branches/') && !pathname.includes('/updates/') && pathname.endsWith('/packages') && init?.method === 'POST') {
    const body = JSON.parse(String(init.body));
    return jsonResponse(createPackageDefinition(body));
  }

  if (!pathname.includes('/updates/') && pathname.endsWith('/packages')) {
    return jsonResponse(createPlayerPackages());
  }

  if (pathname.endsWith('/staff') && init?.method === 'POST') {
    const body = JSON.parse(String(init.body));
    return jsonResponse(createStaffUser(body));
  }

  if (pathname.includes('/staff/') && pathname.endsWith('/profile') && init?.method === 'PATCH') {
    const body = JSON.parse(String(init.body));
    return jsonResponse(createStaffUser({
      staffUserId: pathname.split('/').at(-2),
      userName: body.userName,
      displayName: body.displayName
    }));
  }

  if (pathname.includes('/staff/') && pathname.endsWith('/state') && init?.method === 'PATCH') {
    const body = JSON.parse(String(init.body));
    return jsonResponse(createStaffUser({
      staffUserId: pathname.split('/').at(-2),
      isActive: body.isActive
    }));
  }

  if (pathname.includes('/staff/') && pathname.endsWith('/password-reset') && init?.method === 'POST') {
    return jsonResponse(createStaffUser({
      staffUserId: pathname.split('/').at(-2)
    }));
  }

  if (pathname.includes('/staff/') && pathname.endsWith('/roles') && init?.method === 'PATCH') {
    const body = JSON.parse(String(init.body));
    return jsonResponse(createStaffUser({
      staffUserId: pathname.split('/').at(-2),
      roleNames: body.roleNames
    }));
  }

  if (pathname.endsWith('/staff')) {
    return jsonResponse(createStaffUsers());
  }

  if (pathname.endsWith('/layout/zones') && init?.method === 'POST') {
    const body = JSON.parse(String(init.body));
    return jsonResponse(createZone(body));
  }

  if (pathname.includes('/layout/zones/') && init?.method === 'PATCH') {
    const body = JSON.parse(String(init.body));
    return jsonResponse(createZone({
      zoneId: pathname.split('/').at(-1),
      ...body
    }));
  }

  if (pathname.includes('/layout/zones/') && init?.method === 'DELETE') {
    return new Response(null, { status: 204 });
  }

  if (pathname.endsWith('/layout/seats') && init?.method === 'POST') {
    const body = JSON.parse(String(init.body));
    return jsonResponse(createSeat(body));
  }

  if (pathname.includes('/layout/seats/') && init?.method === 'PATCH') {
    const body = JSON.parse(String(init.body));
    return jsonResponse(createSeat({
      seatId: pathname.split('/').at(-1),
      ...body
    }));
  }

  if (pathname.includes('/layout/seats/') && init?.method === 'DELETE') {
    return new Response(null, { status: 204 });
  }

  if (pathname.endsWith('/layout/zones')) {
    return jsonResponse(createZones());
  }

  if (pathname.endsWith('/device-enrollment-codes') && init?.method === 'POST') {
    const body = JSON.parse(String(init.body));
    return jsonResponse(createDeviceEnrollmentCode(body));
  }

  if (pathname.endsWith('/seat-assignment') && init?.method === 'POST') {
    const body = JSON.parse(String(init.body));
    const parts = pathname.split('/');
    return jsonResponse(createDeviceSeatAssignment({
      deviceId: parts[parts.length - 2],
      seatId: body.seatId
    }));
  }

  if (pathname.endsWith('/devices') && pathname.includes('/api/branches/') && init?.method !== 'POST') {
    return jsonResponse(createDeviceInventory());
  }

  if (pathname.includes('/api/devices/') && !pathname.includes('/commands') && init?.method !== 'POST') {
    const parts = pathname.split('/');
    const deviceId = parts[parts.length - 1];
    return jsonResponse(createDeviceDetail({
      deviceId,
      machineName: deviceId === '44444444-4444-4444-8444-444444444444' ? 'PC-03' : 'PC-02'
    }));
  }

  if (pathname.endsWith('/diagnostics')) {
    return jsonResponse(createDiagnostics());
  }

  if (pathname.endsWith('/updates/packages') && init?.method === 'POST') {
    const body = JSON.parse(String(init.body));
    return jsonResponse(createUpdatePackage(body));
  }

  if (pathname.includes('/updates/packages/') && pathname.endsWith('/state') && init?.method === 'POST') {
    const body = JSON.parse(String(init.body));
    const parts = pathname.split('/');
    return jsonResponse(createUpdatePackage({
      updatePackageId: parts[parts.length - 2],
      state: body.state
    }));
  }

  if (pathname.endsWith('/updates/rollouts') && init?.method === 'POST') {
    const body = JSON.parse(String(init.body));
    return jsonResponse(createUpdateRollout(body));
  }

  if (pathname.includes('/updates/rollouts/') && pathname.endsWith('/state') && init?.method === 'POST') {
    const body = JSON.parse(String(init.body));
    const parts = pathname.split('/');
    return jsonResponse(createUpdateRollout({
      updateRolloutId: parts[parts.length - 2],
      state: body.state
    }));
  }

  if (pathname.endsWith('/updates/rollouts')) {
    return jsonResponse(createRollouts());
  }

  if (pathname.endsWith('/tariffs/options')) {
    return jsonResponse(createTariffs());
  }

  if (pathname.endsWith('/tariffs') && init?.method === 'POST') {
    const body = JSON.parse(String(init.body));
    return jsonResponse(createTariff(body));
  }

  if (pathname.includes('/tariffs/') && pathname.endsWith('/versions') && init?.method === 'POST') {
    const body = JSON.parse(String(init.body));
    const parts = pathname.split('/');
    return jsonResponse(createTariffVersion({
      ...body,
      tariffId: parts[parts.length - 2]
    }));
  }

  if (pathname.includes('/tariffs/') && pathname.includes('/versions/') && init?.method === 'PATCH') {
    const body = JSON.parse(String(init.body));
    const parts = pathname.split('/');
    return jsonResponse(createTariffVersion({
      ...body,
      tariffId: parts[parts.length - 3],
      tariffVersionId: parts.at(-1),
      retiredAtUtc: body.isActive === false ? '2026-05-22T10:00:00Z' : null
    }));
  }

  if (pathname.includes('/tariffs/') && init?.method === 'PATCH') {
    const body = JSON.parse(String(init.body));
    return jsonResponse(createTariff({
      ...body,
      tariffId: pathname.split('/').at(-1)
    }));
  }

  if (pathname.endsWith('/packages/options')) {
    return jsonResponse(createPackageOptions());
  }

  if (pathname.endsWith('/audit')) {
    return jsonResponse(createAudit());
  }

  return jsonResponse({ ok: true });
}

function seedStoredOperatorConnection(overrides: Record<string, unknown> = {}) {
  localStorage.setItem(
    'afk4.operator.connection',
    JSON.stringify({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      organizationSlug: 'afk4-dushanbe',
      organizationName: 'AFK4 Dushanbe',
      branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
      branchSlug: 'central',
      branchName: 'Central',
      branchCity: 'Dushanbe',
      storedAtUtc: '2026-05-23T10:00:00Z',
      ...overrides
    })
  );
}

function installSessionBridge(
  loadSession: ReturnType<typeof createSession> | null = createSession(),
  refreshSession: ReturnType<typeof createSession> | null = loadSession,
  options: {
    failedRequests?: Record<string, string>;
    loadConnection?: Record<string, unknown> | null;
  } = {}
) {
  const listeners = new Set<(event: HostBridgeMessageEvent) => void>();
  const requests: string[] = [];
  const connectionSaves: Array<Record<string, unknown>> = [];
  let connectionState: Record<string, unknown> | null = options.loadConnection ?? null;
  window.chrome = {
    webview: {
      postMessage: (message: unknown) => {
        const request = message as { type: string; requestId: string; payload?: unknown };
        requests.push(request.type);
        let payload: unknown = loadSession;

        if (request.type === 'auth:signIn') {
          payload = createSession();
        }

        if (request.type === 'auth:refresh') {
          payload = refreshSession;
        }

        if (request.type === 'auth:signOut') {
          payload = { signedOut: true };
        }

        if (request.type === 'connection:loadConnection') {
          payload = connectionState;
        }

        if (request.type === 'connection:saveConnection') {
          const incoming = (request.payload ?? {}) as Record<string, unknown>;
          connectionSaves.push(incoming);
          connectionState = incoming;
          payload = incoming;
        }

        if (request.type === 'connection:clearConnection') {
          connectionState = null;
          payload = { cleared: true };
        }

        queueMicrotask(() => {
          const failedMessage = options.failedRequests?.[request.type];
          for (const listener of listeners) {
            listener({
              data: {
                type: 'host:response',
                requestId: request.requestId,
                ok: failedMessage === undefined,
                payload: failedMessage === undefined ? payload : null,
                error: failedMessage === undefined
                  ? undefined
                  : { code: 'auth_failed', message: failedMessage }
              }
            });
          }
        });
      },
      addEventListener: (_type, listener) => listeners.add(listener),
      removeEventListener: (_type, listener) => listeners.delete(listener)
    }
  };

  return { requests, connectionSaves };
}

function buildStoredConnection(overrides: Record<string, unknown> = {}) {
  return {
    organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
    organizationSlug: 'afk4-dushanbe',
    organizationName: 'AFK4 Dushanbe',
    branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
    branchSlug: 'central',
    branchName: 'Central',
    branchCity: 'Dushanbe',
    storedAtUtc: '2026-05-23T10:00:00Z',
    ...overrides
  };
}

const allOperatorPermissions = [
  'floor_map.view',
  'sessions.start',
  'sessions.extend',
  'sessions.transfer',
  'sessions.end',
  'players.view',
  'players.create',
  'billing.view',
  'billing.wallet.top_up',
  'billing.debt.pay',
  'packages.view',
  'packages.manage',
  'packages.purchase',
  'shifts.view',
  'shifts.open',
  'shifts.close',
  'shifts.cash.manage',
  'reports.view',
  'reservations.view',
  'reservations.manage',
  'pos.sales.create',
  'pos.sales.pay',
  'pos.sales.refund',
  'pos.sales.void',
  'inventory.view',
  'inventory.stock.manage',
  'pos.catalog.manage',
  'receipts.view',
  'diagnostics.view',
  'identity.branch_staff.manage',
  'identity.roles.manage',
  'layout.manage',
  'devices.enrollment_codes.create',
  'devices.seat_assignment.assign',
  'devices.detail.view',
  'devices.commands.dispatch',
  'devices.credentials.rotate',
  'devices.credentials.revoke',
  'tariffs.manage',
  'tariffs.view',
  'updates.status.view',
  'updates.packages.manage',
  'updates.rollouts.manage',
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

function createFloorMapWithPc01(overrides: Partial<ReturnType<typeof createFloorMap>['seats'][number]>) {
  const floorMap = createFloorMap();
  return {
    ...floorMap,
    seats: [
      {
        ...floorMap.seats[0],
        ...overrides
      },
      ...floorMap.seats.slice(1)
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

function createDeviceCommand(type: string, payload: Record<string, string>) {
  return {
    commandId: '56565656-5656-5656-5656-565656565656',
    type,
    payload,
    createdAtUtc: '2026-05-21T10:00:00Z'
  };
}

function createDeviceCommandHistory() {
  return [
    {
      deviceId: '33333333-3333-3333-3333-333333333333',
      commandId: '78787878-7878-7878-8787-787878787878',
      type: 'lock',
      status: 'Failed',
      message: 'Agent timeout',
      createdAtUtc: '2026-05-21T09:50:00Z',
      updatedAtUtc: '2026-05-21T09:51:00Z'
    },
    {
      deviceId: '33333333-3333-3333-3333-333333333333',
      commandId: '79797979-7979-7979-8797-797979797979',
      type: 'unlock',
      status: 'Completed',
      message: 'Unlocked by operator',
      createdAtUtc: '2026-05-21T09:40:00Z',
      updatedAtUtc: '2026-05-21T09:41:00Z'
    }
  ];
}

function createBranchDeviceCommandHistory() {
  return [
    {
      deviceId: '44444444-4444-4444-8444-444444444444',
      commandId: '90909090-9090-9090-9090-909090909090',
      type: 'refresh-session-lease',
      status: 'Completed',
      message: 'Branch lease refreshed',
      createdAtUtc: '2026-05-21T09:55:00Z',
      updatedAtUtc: '2026-05-21T09:56:00Z'
    },
    ...createDeviceCommandHistory()
  ];
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
    createPosProduct()
  ];
}

function createPosCategory(overrides: Record<string, unknown> = {}) {
  return {
    categoryId: '88888888-8888-8888-8888-888888888888',
    organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
    branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
    name: 'Drinks',
    isActive: true,
    createdAtUtc: '2026-05-21T08:00:00Z',
    ...overrides
  };
}

function createPosProduct(overrides: Record<string, unknown> = {}) {
  return {
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
    createdAtUtc: '2026-05-21T08:00:00Z',
    ...overrides
  };
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

function createClosedShift(overrides: Record<string, unknown> = {}) {
  return {
    ...createCurrentShift(),
    state: 'closed',
    countedCash: { currencyCode: 'TJS', minorUnits: 112000 },
    difference: { currencyCode: 'TJS', minorUnits: 0 },
    closingNote: 'Смена закрыта оператором',
    closedAtUtc: '2026-05-21T18:00:00Z',
    ...overrides
  };
}

function createCashMovement(overrides: Record<string, unknown> = {}) {
  return {
    cashMovementId: 'bbbbbbbb-0000-0000-0000-000000000001',
    organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
    branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
    shiftId: '66666666-6666-6666-6666-666666666666',
    createdByStaffUserId: '3db1367b-88c6-4b1c-99c3-bcbb5f4d5134',
    movementType: 'cash_in',
    amount: { currencyCode: 'TJS', minorUnits: 1000 },
    reason: 'Размен кассы',
    createdAtUtc: '2026-05-21T11:00:00Z',
    ...overrides
  };
}

function createStockMovement(overrides: Record<string, unknown> = {}) {
  return {
    stockMovementId: 'cccccccc-0000-0000-0000-000000000001',
    organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
    branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
    productId: '77777777-7777-7777-7777-777777777777',
    movementType: 'adjustment',
    quantityDelta: -3,
    unitCost: { currencyCode: 'TJS', minorUnits: 0 },
    reason: 'operator stock count correction',
    createdByStaffUserId: '3db1367b-88c6-4b1c-99c3-bcbb5f4d5134',
    createdAtUtc: '2026-05-21T11:05:00Z',
    ...overrides
  };
}

function createStockMovements() {
  return [
    createStockMovement()
  ];
}

function createPosSale(state: string) {
  return {
    posSaleId: '99999999-9999-9999-9999-999999999999',
    organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
    branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
    shiftId: '66666666-6666-6666-6666-666666666666',
    state,
    lines: [
      {
        productId: '77777777-7777-7777-7777-777777777777',
        productName: 'Cola 0.5',
        quantity: 1,
        unitPrice: { currencyCode: 'TJS', minorUnits: 1200 },
        lineTotal: { currencyCode: 'TJS', minorUnits: 1200 }
      }
    ],
    total: { currencyCode: 'TJS', minorUnits: 1200 },
    createdByStaffUserId: '3db1367b-88c6-4b1c-99c3-bcbb5f4d5134',
    createdAtUtc: '2026-05-21T09:00:00Z',
    paidAtUtc: state === 'paid' ? '2026-05-21T09:01:00Z' : null,
    refundedAtUtc: state === 'refunded' ? '2026-05-21T09:05:00Z' : null,
    voidedAtUtc: state === 'voided' ? '2026-05-21T09:03:00Z' : null,
    latestReceipt: state === 'paid'
      ? createReceipt()
      : state === 'refunded'
        ? createReceipt({
          receiptId: '22222222-2222-2222-2222-222222222222',
          receiptNumber: 'REF-20260521-0001',
          receiptType: 'refund'
        })
        : null
  };
}

function createReceipt(overrides: Record<string, unknown> = {}) {
  return {
    receiptId: '11111111-1111-1111-1111-111111111111',
    organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
    branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
    posSaleId: '99999999-9999-9999-9999-999999999999',
    receiptNumber: 'POS-20260521-0001',
    receiptType: 'sale',
    total: { currencyCode: 'TJS', minorUnits: 1200 },
    createdAtUtc: '2026-05-21T09:01:00Z',
    ...overrides
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
      },
      {
        posSaleId: '88888888-8888-8888-8888-888888888888',
        organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
        branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
        shiftId: '66666666-6666-6666-6666-666666666666',
        createdByStaffUserId: '3db1367b-88c6-4b1c-99c3-bcbb5f4d5134',
        state: 'paid',
        total: { currencyCode: 'TJS', minorUnits: 2500 },
        paidAmount: { currencyCode: 'TJS', minorUnits: 2500 },
        refundAmount: { currencyCode: 'TJS', minorUnits: 0 },
        lineCount: 2,
        itemQuantity: 2,
        createdAtUtc: '2026-05-21T09:10:00Z',
        paidAtUtc: '2026-05-21T09:11:00Z',
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
    },
    {
      playerAccountId: '34343434-3434-3434-3434-343434343434',
      displayName: 'Olim K.',
      phoneNumber: '+992 90 111 22 33',
      walletBalanceMinorUnits: 0,
      debtBalanceMinorUnits: 3500,
      activePackageCount: 0,
      isActive: true
    }
  ];
}

function createWalletSummary(overrides: Record<string, unknown> = {}) {
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
    ],
    ...overrides
  };
}

function createPlayerPackages() {
  return [
    createPlayerPackage()
  ];
}

function createPlayerPackage(overrides: Record<string, unknown> = {}) {
  return {
    playerPackageId: '19191919-1919-1919-1919-191919191919',
    playerAccountId: '12121212-1212-1212-1212-121212121212',
    packageDefinitionId: 'abababab-abab-abab-abab-abababababab',
    name: 'Night 5h',
    remainingIncludedSeconds: 10800,
    remainingBonusSeconds: 0,
    state: 'active',
    expiresAtUtc: '2026-05-22T10:00:00Z',
    ...overrides
  };
}

function createPlayerAccount(overrides: Record<string, unknown> = {}) {
  return {
    playerAccountId: '45454545-4545-4545-4545-454545454545',
    organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
    branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
    displayName: 'New Client',
    phoneNumber: '+992 90 777 88 99',
    walletBalanceMinorUnits: 0,
    debtBalanceMinorUnits: 0,
    activePackageCount: 0,
    isActive: true,
    createdAtUtc: '2026-05-21T12:40:00Z',
    ...overrides
  };
}

function createPackageOptions() {
  return [
    {
      packageDefinitionId: 'abababab-abab-abab-abab-abababababab',
      name: 'Night 5h',
      currencyCode: 'TJS',
      priceMinorUnits: 25000,
      includedSeconds: 18000,
      bonusSeconds: 0,
      expiresAfterDays: 30
    },
    {
      packageDefinitionId: 'cdcdcdcd-cdcd-cdcd-cdcd-cdcdcdcdcdcd',
      name: 'Morning 2h',
      currencyCode: 'TJS',
      priceMinorUnits: 12000,
      includedSeconds: 7200,
      bonusSeconds: 1800,
      expiresAfterDays: 14
    }
  ];
}

function createPackageDefinition(overrides: Record<string, unknown> = {}) {
  return {
    packageDefinitionId: 'abababab-abab-abab-abab-abababababab',
    organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
    branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
    name: 'Night 5h',
    price: { currencyCode: 'TJS', minorUnits: 25000 },
    includedSeconds: 18000,
    bonusSeconds: 1800,
    expiresAfterDays: 30,
    isActive: true,
    createdAtUtc: '2026-05-21T12:00:00Z',
    ...overrides
  };
}

function createStaffUsers() {
  return [
    createStaffUser({
      staffUserId: '3db1367b-88c6-4b1c-99c3-bcbb5f4d5134',
      userName: 'cashier',
      displayName: 'Cashier One',
      roleNames: ['cashier_operator']
    })
  ];
}

function createBranchProfile(overrides: Record<string, unknown> = {}) {
  return {
    organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
    branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
    name: 'AFK4 Dushanbe',
    city: 'Dushanbe',
    createdAtUtc: '2026-05-21T08:00:00Z',
    ...overrides
  };
}

function createStaffUser(overrides: Record<string, unknown> = {}) {
  return {
    staffUserId: '3db1367b-88c6-4b1c-99c3-bcbb5f4d5134',
    organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
    userName: 'cashier',
    displayName: 'Cashier One',
    isActive: true,
    roleNames: ['cashier_operator'],
    createdAtUtc: '2026-05-21T08:00:00Z',
    ...overrides
  };
}

function createZone(overrides: Record<string, unknown> = {}) {
  return {
    zoneId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
    organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
    branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
    name: 'Зал A',
    sortOrder: 10,
    createdAtUtc: '2026-05-21T08:00:00Z',
    seats: [],
    ...overrides
  };
}

function createSeat(overrides: Record<string, unknown> = {}) {
  return {
    seatId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
    branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
    zoneId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
    name: 'PC-01',
    sortOrder: 10,
    createdAtUtc: '2026-05-21T08:00:00Z',
    ...overrides
  };
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

function createDeviceEnrollmentCode(overrides: Record<string, unknown> = {}) {
  return {
    organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
    branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
    code: 'AFK4-DEVICE-1234',
    expiresAtUtc: '2026-05-21T10:00:00Z',
    ...overrides
  };
}

function createDeviceSeatAssignment(overrides: Record<string, unknown> = {}) {
  return {
    deviceSeatAssignmentId: '21212121-2121-2121-2121-212121212121',
    organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
    branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
    seatId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    deviceId: '33333333-3333-3333-3333-333333333333',
    attachedAtUtc: '2026-05-21T09:30:00Z',
    detachedAtUtc: null,
    ...overrides
  };
}

function createDeviceInventory() {
  return [
    {
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
      deviceId: '33333333-3333-3333-3333-333333333333',
      machineName: 'PC-02',
      agentVersion: '0.1.14',
      shellVersion: '0.1.14',
      enrolledAtUtc: '2026-05-21T08:30:00Z',
      lastHeartbeatAtUtc: '2026-05-21T09:30:00Z',
      isOnline: true,
      isLocked: true,
      seatId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
      seatName: 'PC-01',
      zoneId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
      zoneName: 'Зал A',
      activeCredentialCount: 1,
      installedAppCount: 2,
      pendingCommandCount: 0,
      failedCommandCount: 0
    },
    {
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
      deviceId: '44444444-4444-4444-8444-444444444444',
      machineName: 'PC-03',
      agentVersion: '0.1.14',
      shellVersion: '0.1.13',
      enrolledAtUtc: '2026-05-21T08:40:00Z',
      lastHeartbeatAtUtc: '2026-05-21T09:20:00Z',
      isOnline: false,
      isLocked: true,
      seatId: null,
      seatName: null,
      zoneId: null,
      zoneName: null,
      activeCredentialCount: 1,
      installedAppCount: 1,
      pendingCommandCount: 1,
      failedCommandCount: 1
    }
  ];
}

function createDeviceDetail(overrides: Record<string, unknown> = {}) {
  return {
    organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
    branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
    deviceId: '33333333-3333-3333-3333-333333333333',
    machineName: 'PC-02',
    agentVersion: '0.1.14',
    shellVersion: '0.1.14',
    enrolledAtUtc: '2026-05-21T08:30:00Z',
    lastHeartbeatAtUtc: '2026-05-21T09:30:00Z',
    isOnline: true,
    isLocked: true,
    seatId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    seatName: 'PC-01',
    zoneId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
    zoneName: 'Зал A',
    activeCredentialCount: 1,
    installedAppCount: 2,
    recentCommands: [
      {
        deviceId: '33333333-3333-3333-3333-333333333333',
        commandId: '56565656-5656-5656-5656-565656565656',
        type: 'refresh-session-lease',
        status: 'acked',
        message: 'lease refreshed',
        createdAtUtc: '2026-05-21T09:20:00Z',
        updatedAtUtc: '2026-05-21T09:21:00Z'
      }
    ],
    ...overrides
  };
}

function createRotatedDeviceCredential(overrides: Record<string, unknown> = {}) {
  return {
    organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
    branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
    deviceId: '33333333-3333-3333-3333-333333333333',
    credentialId: '23232323-2323-2323-2323-232323232323',
    credentialSecret: 'device-secret-after-rotation',
    rotatedAtUtc: '2026-05-21T09:45:00Z',
    ...overrides
  };
}

function createRevokedDeviceCredential(overrides: Record<string, unknown> = {}) {
  return {
    organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
    branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
    deviceId: '33333333-3333-3333-3333-333333333333',
    credentialId: '23232323-2323-2323-2323-232323232323',
    revokedAtUtc: '2026-05-21T09:50:00Z',
    ...overrides
  };
}

function createDiagnostics(overrides: Record<string, unknown> = {}) {
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
    staleDevices: [],
    ...overrides
  };
}

function createRollouts() {
  return [
    {
      updateRolloutId: '14141414-1414-1414-1414-141414141414',
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
      updatePackageId: '15151515-1515-1515-1515-151515151515',
      component: 'agent-service',
      version: '0.1.14',
      channel: 'internal',
      state: 'active',
      targetKind: 'branch',
      targetDeviceIds: [],
      batchPercent: 100,
      createdAtUtc: '2026-05-21T08:00:00Z',
      startsAtUtc: '2026-05-21T08:00:00Z',
      completedAtUtc: null,
      deviceStatuses: [
        {
          deviceId: '33333333-3333-3333-3333-333333333333',
          updateRolloutId: '14141414-1414-1414-1414-141414141414',
          updatePackageId: '15151515-1515-1515-1515-151515151515',
          component: 'agent-service',
          installedVersion: '0.1.13',
          targetVersion: '0.1.14',
          status: 'installed',
          message: 'target reached',
          updatedAtUtc: '2026-05-21T08:30:00Z'
        }
      ]
    }
  ];
}

function createUpdatePackage(overrides: Record<string, unknown> = {}) {
  const updatePackageId = typeof overrides.updatePackageId === 'string' && overrides.updatePackageId.length > 0
    ? overrides.updatePackageId
    : '19191919-1919-1919-1919-191919191919';
  const { updatePackageId: _ignoredUpdatePackageId, ...remainingOverrides } = overrides;

  return {
    organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
    branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
    component: 'operator-app',
    version: '0.1.0',
    channel: 'internal',
    artifactUri: 'https://updates.afk4.staging.mubi.dev/operator-app/0.1.0/operator-app.msi',
    sha256: '0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef',
    signature: 'signed-update-package',
    signatureAlgorithm: 'ECDSA-P256-SHA256-IEEE-P1363',
    sizeBytes: 1048576,
    state: 'registered',
    releaseNotes: 'Operator App update package.',
    createdAtUtc: '2026-05-21T09:10:00Z',
    ...remainingOverrides,
    updatePackageId
  };
}

function createUpdateRollout(overrides: Record<string, unknown> = {}) {
  return {
    updateRolloutId: '20202020-2020-2020-2020-202020202020',
    organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
    branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
    updatePackageId: '19191919-1919-1919-1919-191919191919',
    component: 'operator-app',
    version: '0.1.0',
    channel: 'internal',
    state: 'active',
    targetKind: 'branch',
    targetDeviceIds: [],
    batchPercent: 100,
    createdAtUtc: '2026-05-21T09:15:00Z',
    startsAtUtc: '2026-05-21T10:00:00Z',
    completedAtUtc: null,
    deviceStatuses: [],
    ...overrides
  };
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
      roundingIncrementMinutes: 5,
      effectiveFromUtc: '2026-05-21T12:20:00Z'
    }
  ];
}

function createTariff(overrides: Record<string, unknown> = {}) {
  return {
    tariffId: '25252525-2525-2525-2525-252525252525',
    organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
    branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
    name: 'Morning Hour',
    isActive: true,
    createdAtUtc: '2026-05-21T12:20:00Z',
    ...overrides
  };
}

function createTariffVersion(overrides: Record<string, unknown> = {}) {
  return {
    tariffVersionId: '26262626-2626-2626-2626-262626262626',
    tariffId: '25252525-2525-2525-2525-252525252525',
    organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
    branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
    versionNumber: 2,
    currencyCode: 'TJS',
    pricePerMinuteMinorUnits: 160,
    minimumBillableMinutes: 20,
    roundingIncrementMinutes: 10,
    effectiveFromUtc: '2026-05-21T12:20:00Z',
    createdAtUtc: '2026-05-21T12:20:00Z',
    ...overrides
  };
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
