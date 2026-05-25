import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import App, { resolvePlatformRoute } from './App';
import { clearSession, writeSession, type PlatformAdminSession } from './auth/tokenStore';

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

describe('Platform Web routing', () => {
  beforeEach(() => {
    window.history.replaceState(null, '', '/');
    sessionStorage.clear();
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => jsonResponse(200, []))
    );
  });

  afterEach(() => {
    cleanup();
    clearSession();
    vi.unstubAllGlobals();
  });

  it('resolves root and legacy tenant URLs to admin routes', () => {
    expect(resolvePlatformRoute('/')).toMatchObject({
      redirectTo: '/admin',
      route: { kind: 'tenantList' }
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

  it('redirects the old root bookmark to /admin for signed-in platform admins', async () => {
    writeSession(buildSession());
    render(<App apiBaseUrl="http://localhost" />);

    await waitFor(() => expect(window.location.pathname).toBe('/admin'));
    expect(screen.getByRole('heading', { name: 'Tenants' })).toBeInTheDocument();
  });

  it('redirects a legacy new-tenant bookmark to the admin-prefixed screen', async () => {
    window.history.replaceState(null, '', '/tenants/new');
    writeSession(buildSession());

    render(<App apiBaseUrl="http://localhost" />);

    await waitFor(() => expect(window.location.pathname).toBe('/admin/tenants/new'));
    expect(screen.getByRole('heading', { name: 'New tenant' })).toBeInTheDocument();
  });

  it('pushes admin-prefixed URLs for tenant list navigation', async () => {
    window.history.replaceState(null, '', '/admin/tenants');
    writeSession(buildSession());
    render(<App apiBaseUrl="http://localhost" />);

    expect(screen.getByRole('heading', { name: 'Tenants' })).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'New tenant' }));

    expect(window.location.pathname).toBe('/admin/tenants/new');
    expect(screen.getByRole('heading', { name: 'New tenant' })).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));

    expect(window.location.pathname).toBe('/admin/tenants');
    expect(screen.getByRole('heading', { name: 'Tenants' })).toBeInTheDocument();
  });
});
