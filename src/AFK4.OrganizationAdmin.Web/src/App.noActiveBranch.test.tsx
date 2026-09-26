import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterAll, afterEach, beforeEach, describe, expect, it, mock, type Mock } from 'bun:test';
import type { OperatorRealtimeOptions } from './operatorRealtime';

// Сотрудник без активного филиала. Всё рабочее в Панели — зал, касса, брони, настройки —
// живёт внутри филиала, поэтому оболочка называет причину один раз, своим экраном, а не
// оставляет формы гаснуть поодиночке (а «Платежи» — врать про подключение к серверу).

type FetchLike = (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>;

// bun's mock.module is NOT hoisted above static imports — register before importing App.
const actualRealtime = await import('./operatorRealtime');
mock.module('./operatorRealtime', () => ({
  ...actualRealtime,
  createOperatorRealtimeClient: mock((options: OperatorRealtimeOptions) => ({
    start: mock(async () => {
      options.onConnectionStateChanged?.('connected');
    }),
    stop: mock(async () => {})
  }))
}));

const { App } = await import('./App');

// Подмена модуля живёт дольше файла, если её не вернуть: в полном прогоне все файлы идут одним
// процессом, и следующий получил бы этот поддельный realtime (заслон — mockModule.guard.test.ts).
afterAll(() => mock.module('./operatorRealtime', () => actualRealtime));

const ORG_ID = '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08';
const BRANCH_A = 'acfc0212-967f-4d84-94be-9003387b09c2';

function createSession(overrides: Record<string, unknown> = {}) {
  return {
    staffUserId: '3db1367b-88c6-4b1c-99c3-bcbb5f4d5134',
    organizationId: ORG_ID,
    displayName: 'Cashier One',
    accessToken: 'access-token',
    accessTokenExpiresAtUtc: '2099-01-01T00:00:00Z',
    refreshToken: 'refresh-token',
    refreshTokenExpiresAtUtc: '2099-01-01T00:00:00Z',
    branchIds: [],
    // Права есть, филиала нет: именно так формы и гасли молча.
    permissions: ['organization.floor_map.view', 'organization.loyalty.settings.manage'],
    ...overrides
  };
}

function jsonResponse(body: unknown): Response {
  return new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } });
}

let refreshedSession: Record<string, unknown> = createSession();

function mockFetch(input: RequestInfo | URL): Promise<Response> {
  const pathname = new URL(String(input)).pathname;
  if (pathname.endsWith('/auth/staff/refresh')) {
    return Promise.resolve(jsonResponse(refreshedSession));
  }
  if (pathname.endsWith('/floor-map')) {
    return Promise.resolve(jsonResponse({ branchId: BRANCH_A, branchName: 'Центр', seats: [] }));
  }
  if (pathname.endsWith('/profile')) {
    return Promise.resolve(jsonResponse({ organizationId: ORG_ID, branchId: BRANCH_A, name: 'Центр', city: 'Dushanbe' }));
  }
  return Promise.resolve(new Response('not found', { status: 404 }));
}

const originalFetch = globalThis.fetch;
let fetchMock: Mock<FetchLike>;

function branchScopedRequests(): string[] {
  return fetchMock.mock.calls
    .map(([input]) => new URL(String(input)).pathname)
    .filter((pathname) => pathname.includes('/branches/'));
}

describe('App — no active branch', () => {
  beforeEach(() => {
    refreshedSession = createSession();
    fetchMock = mock(mockFetch) as unknown as Mock<FetchLike>;
    globalThis.fetch = fetchMock as unknown as typeof fetch;
    // ПК привязан к филиалу A, а у сотрудника филиалов нет: привязка ПК не делает филиал его.
    window.__AFK4_ORGANIZATION_ADMIN_CONFIG__ = {
      runtime: 'browser-dev',
      shellMode: 'vite-dev',
      platformBaseUrl: 'http://localhost:5074/',
      currencyCode: 'TJS',
      organizationId: ORG_ID,
      branchId: BRANCH_A
    };
  });

  afterEach(() => {
    cleanup();
    globalThis.fetch = originalFetch;
    delete window.__AFK4_ORGANIZATION_ADMIN_CONFIG__;
    localStorage.clear();
    sessionStorage.clear();
    mock.restore();
  });

  it('shows one shell-level screen instead of the workspaces, and asks nothing of a branch it does not have', async () => {
    sessionStorage.setItem('afk4.staff.session', JSON.stringify(createSession()));

    render(<App />);

    expect(await screen.findByRole('heading', { name: 'Нет активного филиала' })).toBeInTheDocument();
    expect(screen.queryByRole('navigation', { name: 'Рабочие места' })).not.toBeInTheDocument();
    expect(screen.queryByText('Нет подключения к серверу')).not.toBeInTheDocument();
    await new Promise((resolve) => setTimeout(resolve, 100));
    expect(branchScopedRequests()).toEqual([]);
  });

  it('opens the shell once a recheck finds the branch the staff member was just assigned to', async () => {
    sessionStorage.setItem('afk4.staff.session', JSON.stringify(createSession()));
    refreshedSession = createSession({ branchIds: [BRANCH_A] });

    render(<App />);

    fireEvent.click(await screen.findByRole('button', { name: 'Проверить снова' }));

    expect(await screen.findByRole('navigation', { name: 'Рабочие места' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Нет активного филиала' })).not.toBeInTheDocument();
  });

  it('lets the staff member sign out from the screen', async () => {
    sessionStorage.setItem('afk4.staff.session', JSON.stringify(createSession()));

    render(<App />);

    fireEvent.click(await screen.findByRole('button', { name: 'Выйти из аккаунта' }));

    expect(await screen.findByRole('heading', { name: 'Вход администратора' })).toBeInTheDocument();
    expect(sessionStorage.getItem('afk4.staff.session')).toBeNull();
  });
});
