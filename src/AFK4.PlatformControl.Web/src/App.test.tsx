import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, mock } from 'bun:test';
import App, { resolvePlatformRoute } from './App';
import { clearSession, writeSession, type PlatformAdminSession } from './auth/tokenStore';
import { ToastProvider } from './components/ui/toast';
import { I18nProvider } from './i18n/I18nProvider';
import { ThemeProvider } from './theme/ThemeProvider';

const originalFetch = globalThis.fetch;

function renderApp() {
  return render(
    <ThemeProvider>
      <I18nProvider>
        <ToastProvider><App apiBaseUrl="http://localhost" /></ToastProvider>
      </I18nProvider>
    </ThemeProvider>
  );
}

function buildSession(): PlatformAdminSession {
  return {
    platformAdminId: 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee',
    userName: 'admin@platform.test',
    displayName: 'Platform Owner',
    roles: ['platform_admin'],
    permissions: ['platform.organizations.view'],
    accessToken: 'access-token',
    accessTokenExpiresAtUtc: '2030-01-01T00:00:00Z',
    refreshToken: 'refresh-token',
    refreshTokenExpiresAtUtc: '2030-02-01T00:00:00Z'
  };
}

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
}

describe('Platform Control admin-only routing', () => {
  beforeEach(() => {
    window.history.replaceState(null, '', '/');
    sessionStorage.clear();
    globalThis.fetch = mock(async (input: RequestInfo | URL) => {
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
    }) as unknown as typeof fetch;
  });

  afterEach(() => {
    cleanup();
    clearSession();
    globalThis.fetch = originalFetch;
  });

  it('resolves root and legacy organization URLs to admin routes', () => {
    expect(resolvePlatformRoute('/')).toMatchObject({ redirectTo: '/admin', route: { kind: 'adminOverview' } });
    expect(resolvePlatformRoute('/organizations')).toMatchObject({ redirectTo: '/admin/organizations', route: { kind: 'organizationList' } });
    expect(resolvePlatformRoute('/organizations/new')).toMatchObject({ redirectTo: '/admin/organizations/new', route: { kind: 'newOrganization' } });
    expect(resolvePlatformRoute('/organizations/org-1')).toMatchObject({
      redirectTo: '/admin/organizations/org-1',
      route: { kind: 'organizationDetail', organizationId: 'org-1' }
    });
  });

  it('rejects removed club and staff sign-in routes while preserving owner onboarding', () => {
    expect(resolvePlatformRoute('/club').route).toEqual({ kind: 'notFound', path: '/club' });
    expect(resolvePlatformRoute('/club/install').route).toEqual({ kind: 'notFound', path: '/club/install' });
    expect(resolvePlatformRoute('/auth/sign-in').route).toEqual({ kind: 'notFound', path: '/auth/sign-in' });
    expect(resolvePlatformRoute('/account-activation', null, '?code=owner-code').route).toEqual({
      kind: 'accountActivation',
      code: 'owner-code'
    });
  });

  it('renders platform-admin sign-in for an admin route without a session', () => {
    window.history.replaceState(null, '', '/admin');
    renderApp();
    expect(screen.getByRole('heading', { name: 'Platform Control' })).toBeInTheDocument();
  });

  it('redirects the root bookmark and renders the signed-in Platform Control overview', async () => {
    writeSession(buildSession());
    renderApp();

    await waitFor(() => expect(window.location.pathname).toBe('/admin'));
    expect(screen.getByText('Platform Control')).toBeInTheDocument();
    expect(screen.getByText('Platform Owner')).toBeInTheDocument();
  });

  it('pushes the admin organization-list URL from navigation', async () => {
    window.history.replaceState(null, '', '/admin');
    writeSession(buildSession());
    renderApp();

    fireEvent.click(await screen.findByRole('button', { name: 'Организации' }));
    await waitFor(() => expect(window.location.pathname).toBe('/admin/organizations'));
  });
});
