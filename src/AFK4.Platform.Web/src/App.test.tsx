import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import type { ReactElement } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import App, { readPlatformWebAudience, resolvePlatformRoute } from './App';
import { clearStaffSession, readStaffSession, writeStaffSession, type StaffSession } from './auth/staffTokenStore';
import { clearSession, writeSession, type PlatformAdminSession } from './auth/tokenStore';
import { ThemeProvider } from './theme/ThemeProvider';
import { I18nProvider } from './i18n/I18nProvider';
import { ToastProvider } from './components/ui/toast';

// Signed-in club screens now render inside the new AppShell, whose components
// consume the theme + i18n + toast contexts (mounted in main.tsx in production).
// Tests that reach a club screen must provide them, mirroring the real app tree.
function renderWithProviders(ui: ReactElement) {
  return render(
    <ThemeProvider>
      <I18nProvider>
        <ToastProvider>{ui}</ToastProvider>
      </I18nProvider>
    </ThemeProvider>
  );
}

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });
}

function buildSession(): PlatformAdminSession {
  return {
    platformAdminId: 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee',
    userName: 'admin@platform.test',
    displayName: 'Platform Owner',
    roles: ['platform_owner'],
    permissions: ['platform.tenants.view'],
    accessToken: 'access-token',
    accessTokenExpiresAtUtc: '2030-01-01T00:00:00Z',
    refreshToken: 'refresh-token',
    refreshTokenExpiresAtUtc: '2030-02-01T00:00:00Z'
  };
}

function buildStaffSignInResponse() {
  return {
    staffUserId: '11111111-1111-1111-1111-111111111111',
    organizationId: '22222222-2222-2222-2222-222222222222',
    displayName: 'Demo Owner',
    accessToken: 'staff-access-token',
    accessTokenExpiresAtUtc: '2030-01-01T00:00:00Z',
    refreshToken: 'staff-refresh-token',
    refreshTokenExpiresAtUtc: '2030-02-01T00:00:00Z',
    branchIds: ['33333333-3333-3333-3333-333333333333'],
    permissions: [
      'floor_map.view',
      'layout.manage',
      'devices.detail.view',
      'devices.seat_assignment.assign',
      'devices.credentials.revoke',
      'identity.branch_staff.manage',
      'identity.owner_code.manage',
      'branches.settings.manage'
    ]
  };
}

function buildStaffSession(): StaffSession {
  return {
    staffUserId: '11111111-1111-1111-1111-111111111111',
    organizationId: '22222222-2222-2222-2222-222222222222',
    displayName: 'Demo Owner',
    branchIds: ['33333333-3333-3333-3333-333333333333'],
    permissions: buildStaffSignInResponse().permissions,
    accessToken: 'staff-access-token',
    accessTokenExpiresAtUtc: '2030-01-01T00:00:00Z',
    refreshToken: 'staff-refresh-token',
    refreshTokenExpiresAtUtc: '2030-02-01T00:00:00Z'
  };
}

function buildClubFetchMock() {
  return vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = new URL(String(input));
    const method = init?.method ?? 'GET';

    if (url.pathname === '/api/platform/owner-invites/accept' && method === 'POST') {
      return jsonResponse(200, buildStaffSignInResponse());
    }
    if (url.pathname === '/api/auth/staff/sign-in-by-tenant-key' && method === 'POST') {
      return jsonResponse(200, buildStaffSignInResponse());
    }
    if (url.pathname === '/api/auth/staff/sign-in-by-login' && method === 'POST') {
      return jsonResponse(200, buildStaffSignInResponse());
    }
    if (url.pathname === '/api/staff/me/owner-code' && method === 'GET') {
      return new Response(null, { status: 204 });
    }
    if (url.pathname === '/api/staff/me/owner-code/generate' && method === 'POST') {
      return jsonResponse(200, {
        ownerCode: '12345678',
        codeSuffix: '5678',
        expiresAtUtc: '2030-01-01T00:00:00Z'
      });
    }
    if (url.pathname === '/api/branches/33333333-3333-3333-3333-333333333333/profile') {
      return jsonResponse(200, {
        organizationId: '22222222-2222-2222-2222-222222222222',
        branchId: '33333333-3333-3333-3333-333333333333',
        name: 'Demo Branch',
        city: 'Dushanbe',
        createdAtUtc: '2026-05-24T00:00:00Z'
      });
    }
    if (url.pathname === '/api/branches/33333333-3333-3333-3333-333333333333/dashboard/summary') {
      return jsonResponse(200, {
        organizationId: '22222222-2222-2222-2222-222222222222',
        branchId: '33333333-3333-3333-3333-333333333333',
        fromUtc: '2026-05-25T00:00:00Z',
        toUtc: '2026-05-25T23:59:59Z',
        generatedAtUtc: '2026-05-25T12:00:00Z',
        utilization: {
          totalSeats: 5,
          activeSessions: 2,
          endingSessions: 0,
          onlineDevices: 3,
          offlineDevices: 1,
          sessionStarts: 4,
          utilizationPercent: 40
        },
        alertPressure: {
          pendingCommands: 0,
          failedCommands: 1,
          offlineDevices: 1,
          endingSessions: 0,
          totalAlerts: 2
        },
        revenue: {
          posNetSales: { amount: 0, currencyCode: 'TJS' },
          gameplayRevenue: { amount: 0, currencyCode: 'TJS' },
          totalRevenue: { amount: 0, currencyCode: 'TJS' },
          posCheckCount: 0,
          newPlayerCount: 0
        }
      });
    }
    if (url.pathname === '/api/branches/33333333-3333-3333-3333-333333333333/devices') {
      return jsonResponse(200, [
        {
          organizationId: '22222222-2222-2222-2222-222222222222',
          branchId: '33333333-3333-3333-3333-333333333333',
          deviceId: '44444444-4444-4444-4444-444444444444',
          machineName: 'PC-01',
          agentVersion: '0.1.0',
          shellVersion: '0.1.0',
          enrolledAtUtc: '2026-05-24T00:00:00Z',
          lastHeartbeatAtUtc: '2026-05-25T12:00:00Z',
          isOnline: true,
          isLocked: false,
          seatId: null,
          seatName: null,
          zoneId: null,
          zoneName: null,
          activeCredentialCount: 1,
          installedAppCount: 0,
          pendingCommandCount: 0,
          failedCommandCount: 0,
          displayName: 'PC-01',
          role: 'gaming_pc',
          enrollmentState: 'approved'
        }
      ]);
    }
    if (url.pathname === '/api/branches/33333333-3333-3333-3333-333333333333/devices/pending') {
      return jsonResponse(200, []);
    }
    if (url.pathname === '/api/branches/33333333-3333-3333-3333-333333333333/floor-map') {
      return jsonResponse(200, {
        branchId: '33333333-3333-3333-3333-333333333333',
        branchName: 'Demo Branch',
        zones: [],
        seats: []
      });
    }

    return jsonResponse(404, { error: `Unhandled ${method} ${url.pathname}` });
  });
}

describe('Platform Web routing', () => {
  beforeEach(() => {
    window.history.replaceState(null, '', '/');
    sessionStorage.clear();
    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: RequestInfo | URL) => {
        // The admin overview renders billing KPI tiles, which call
        // GET /api/platform/metrics. The real backend always returns a valid
        // PlatformBillingMetricsDto (non-empty currencyCode); mirror that here so
        // formatCurrency doesn't throw. All other endpoints keep the blanket [].
        if (new URL(String(input)).pathname === '/api/platform/metrics') {
          return jsonResponse(200, {
            mrrMinorUnits: 0,
            currencyCode: 'RUB',
            activeSubscriptions: 0,
            outstandingMinorUnits: 0,
            outstandingCount: 0,
            overdueMinorUnits: 0,
            overdueCount: 0
          });
        }
        return jsonResponse(200, []);
      })
    );
  });

  afterEach(() => {
    cleanup();
    clearSession();
    clearStaffSession();
    vi.unstubAllGlobals();
  });

  it('resolves root and legacy tenant URLs to admin routes', () => {
    expect(resolvePlatformRoute('/')).toMatchObject({
      redirectTo: '/admin',
      route: { kind: 'adminOverview' }
    });
    expect(resolvePlatformRoute('/tenants')).toMatchObject({
      redirectTo: '/admin/tenants',
      route: { kind: 'tenantList' }
    });
    expect(resolvePlatformRoute('/tenants/new')).toMatchObject({
      redirectTo: '/admin/tenants/new',
      route: { kind: 'newTenant' }
    });
    expect(resolvePlatformRoute('/tenants/org-1')).toMatchObject({
      redirectTo: '/admin/tenants/org-1',
      route: { kind: 'tenantDetail', organizationId: 'org-1' }
    });
  });

  it('resolves public auth URLs without requiring an admin route', () => {
    expect(resolvePlatformRoute('/auth')).toMatchObject({
      redirectTo: '/auth/sign-in',
      route: { kind: 'staffSignIn' }
    });
    expect(resolvePlatformRoute('/auth/accept-invite', null, '?code=setup-123')).toMatchObject({
      route: { kind: 'acceptInvite', code: 'setup-123' }
    });
    expect(resolvePlatformRoute('/auth/sign-in', null, '?tenantKey=demo-club')).toMatchObject({
      route: {
        kind: 'staffSignIn',
        tenantKey: 'demo-club'
      }
    });
    expect(resolvePlatformRoute('/auth/forgot-password')).toMatchObject({
      route: { kind: 'forgotPassword' }
    });
    expect(resolvePlatformRoute('/auth/reset-password')).toMatchObject({
      route: { kind: 'resetPassword' }
    });
  });

  it('resolves customer dashboard URLs under /club', () => {
    expect(resolvePlatformRoute('/club')).toMatchObject({
      route: { kind: 'clubDashboard' }
    });
    expect(resolvePlatformRoute('/club/install')).toMatchObject({
      route: { kind: 'clubInstall' }
    });
    expect(resolvePlatformRoute('/club/branches')).toMatchObject({
      route: { kind: 'clubBranches' }
    });
  });

  it('gates routes by the audience build flag', () => {
    expect(resolvePlatformRoute('/', null, '', 'admin')).toMatchObject({
      redirectTo: '/admin',
      route: { kind: 'adminOverview' }
    });
    expect(resolvePlatformRoute('/', null, '', 'club')).toMatchObject({
      redirectTo: '/club/install',
      route: { kind: 'clubInstall' }
    });
    expect(resolvePlatformRoute('/auth/sign-in', null, '', 'admin')).toMatchObject({
      route: { kind: 'staffSignIn' }
    });
    expect(resolvePlatformRoute('/auth/sign-in', null, '', 'club')).toMatchObject({
      route: { kind: 'staffSignIn' }
    });
    expect(resolvePlatformRoute('/club/install', null, '', 'admin')).toMatchObject({
      route: { kind: 'notFound', path: '/club/install' }
    });
    expect(resolvePlatformRoute('/admin/tenants', null, '', 'club')).toMatchObject({
      route: { kind: 'notFound', path: '/admin/tenants' }
    });
    expect(resolvePlatformRoute('/tenants/org-1', null, '', 'club')).toMatchObject({
      route: { kind: 'notFound', path: '/tenants/org-1' }
    });
  });

  it('normalizes the VITE_AUDIENCE build flag', () => {
    expect(readPlatformWebAudience({ VITE_AUDIENCE: 'admin' })).toBe('admin');
    expect(readPlatformWebAudience({ VITE_AUDIENCE: 'club' })).toBe('club');
    expect(readPlatformWebAudience({ VITE_AUDIENCE: 'ALL' })).toBe('all');
    expect(readPlatformWebAudience({ VITE_AUDIENCE: 'unexpected' })).toBe('all');
    expect(readPlatformWebAudience({})).toBe('all');
  });

  it('does not render customer screens in an admin audience build', () => {
    window.history.replaceState(null, '', '/club/install');
    writeStaffSession(buildStaffSession());

    render(<App apiBaseUrl="http://localhost" audience="admin" />);

    expect(screen.getByRole('heading', { name: 'Page not found' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Установка на ПК' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Open admin overview' })).toBeInTheDocument();
  });

  it('does not render platform-admin screens in a club audience build', () => {
    window.history.replaceState(null, '', '/admin/tenants');
    writeSession(buildSession());

    render(<App apiBaseUrl="http://localhost" audience="club" />);

    expect(screen.getByRole('heading', { name: 'Page not found' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Tenants' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Open club install' })).toBeInTheDocument();
  });

  it('redirects the old root bookmark to /admin for signed-in platform admins', async () => {
    writeSession(buildSession());
    renderWithProviders(<App apiBaseUrl="http://localhost" />);

    await waitFor(() => expect(window.location.pathname).toBe('/admin'));
    expect(await screen.findByText('Всего тенантов')).toBeInTheDocument();
  });

  it('redirects a legacy new-tenant bookmark to the admin-prefixed screen', async () => {
    window.history.replaceState(null, '', '/tenants/new');
    writeSession(buildSession());

    renderWithProviders(<App apiBaseUrl="http://localhost" />);

    await waitFor(() => expect(window.location.pathname).toBe('/admin/tenants/new'));
    expect(await screen.findByRole('button', { name: 'Создать тенант' })).toBeInTheDocument();
  });

  it('pushes admin-prefixed URLs for tenant list navigation', async () => {
    window.history.replaceState(null, '', '/admin/tenants');
    writeSession(buildSession());
    renderWithProviders(<App apiBaseUrl="http://localhost" />);

    expect(await screen.findByLabelText('Поиск по названию или ключу')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Новый тенант' }));

    expect(window.location.pathname).toBe('/admin/tenants/new');
    expect(await screen.findByRole('button', { name: 'Создать тенант' })).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Отмена' }));

    expect(window.location.pathname).toBe('/admin/tenants');
    expect(await screen.findByLabelText('Поиск по названию или ключу')).toBeInTheDocument();
  });

  it('accepts a setup code, stores the staff session, and redirects to /club/install', async () => {
    window.history.replaceState(null, '', '/auth/accept-invite?code=setup-code-1');
    const fetchMock = buildClubFetchMock();
    vi.stubGlobal('fetch', fetchMock);

    renderWithProviders(<App apiBaseUrl="http://localhost" />);

    expect(screen.getByRole('heading', { name: 'Accept setup code' })).toBeInTheDocument();
    expect(screen.getByLabelText('Setup code')).toHaveValue('setup-code-1');

    fireEvent.change(screen.getByLabelText('User name'), { target: { value: 'owner@demo.test' } });
    fireEvent.change(screen.getByLabelText('Display name'), { target: { value: 'Demo Owner' } });
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'Passw0rd!Real' } });
    fireEvent.change(screen.getByLabelText('Confirm password'), { target: { value: 'Passw0rd!Real' } });
    fireEvent.click(screen.getByRole('button', { name: 'Accept and open club' }));

    await waitFor(() => expect(window.location.pathname).toBe('/club/install'));
    expect(screen.getByRole('heading', { name: 'Установка на ПК' })).toBeInTheDocument();
    expect(readStaffSession()?.accessToken).toBe('staff-access-token');

    const call = fetchMock.mock.calls[0] as unknown as [string, RequestInit];
    expect(call[0]).toBe('http://localhost/api/platform/owner-invites/accept');
    expect(JSON.parse(call[1].body as string)).toEqual({
      code: 'setup-code-1',
      userName: 'owner@demo.test',
      displayName: 'Demo Owner',
      password: 'Passw0rd!Real'
    });
  });

  // TODO Task 4: replace with the new login-only sign-in test (no Club key field)
  it('signs in a staff user and redirects to /club/install', async () => {
    window.history.replaceState(
      null,
      '',
      '/auth/sign-in?tenantKey=demo-club'
    );
    const fetchMock = buildClubFetchMock();
    vi.stubGlobal('fetch', fetchMock);

    renderWithProviders(<App apiBaseUrl="http://localhost" />);

    expect(screen.getByRole('heading', { name: 'Club sign in' })).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText('User name'), { target: { value: 'owner@demo.test' } });
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'Passw0rd!Real' } });
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));

    await waitFor(() => expect(window.location.pathname).toBe('/club/install'));
    expect(screen.getByRole('heading', { name: 'Установка на ПК' })).toBeInTheDocument();

    const call = fetchMock.mock.calls[0] as unknown as [string, RequestInit];
    expect(call[0]).toBe('http://localhost/api/auth/staff/sign-in-by-login');
    expect(JSON.parse(call[1].body as string)).toEqual({
      login: 'owner@demo.test',
      password: 'Passw0rd!Real'
    });
  });

  it('renders the new AppShell with the Overview at /club', async () => {
    window.history.replaceState(null, '', '/club');
    writeStaffSession(buildStaffSession());
    vi.stubGlobal('fetch', buildClubFetchMock());

    renderWithProviders(<App apiBaseUrl="http://localhost" />);

    // Sidebar nav (new shell) renders the overview item by its i18n label.
    expect(screen.getByRole('button', { name: 'Обзор' })).toBeInTheDocument();

    // The Overview KPIs resolve from the branch dashboard summary API.
    await waitFor(() => expect(screen.getByText('Активные сессии')).toBeInTheDocument());
    expect(screen.getByText('Устройства онлайн')).toBeInTheDocument();
    // 'Выручка сегодня' is both a KPI label and the revenue card title.
    expect(screen.getAllByText('Выручка сегодня').length).toBeGreaterThan(0);
  });

  it('generates and reveals an owner code from /club/install', async () => {
    window.history.replaceState(null, '', '/club/install');
    writeStaffSession(buildStaffSession());
    const fetchMock = buildClubFetchMock();
    vi.stubGlobal('fetch', fetchMock);

    renderWithProviders(<App apiBaseUrl="http://localhost" />);

    expect(screen.getByRole('heading', { name: 'Установка на ПК' })).toBeInTheDocument();
    await waitFor(() => expect(screen.getByLabelText('Код владельца')).toHaveTextContent('Код не сгенерирован'));

    fireEvent.click(screen.getByRole('button', { name: 'Сгенерировать код' }));

    await waitFor(() => expect(screen.getByLabelText('Код владельца')).toHaveTextContent('12345678'));
    expect(fetchMock).toHaveBeenCalledWith(
      'http://localhost/api/staff/me/owner-code/generate',
      expect.objectContaining({ method: 'POST' })
    );
  });
});
