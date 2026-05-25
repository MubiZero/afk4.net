import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import App, { resolvePlatformRoute } from './App';
import { clearStaffSession, readStaffSession } from './auth/staffTokenStore';
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
    permissions: ['layout.manage']
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
    clearStaffSession();
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

  it('resolves public auth URLs without requiring an admin route', () => {
    expect(resolvePlatformRoute('/auth')).toMatchObject({
      redirectTo: '/auth/sign-in',
      route: { kind: 'staffSignIn' }
    });
    expect(resolvePlatformRoute('/auth/accept-invite', null, '?code=setup-123')).toMatchObject({
      route: { kind: 'acceptInvite', code: 'setup-123' }
    });
    expect(resolvePlatformRoute('/auth/sign-in', null, '?organizationId=22222222-2222-2222-2222-222222222222')).toMatchObject({
      route: {
        kind: 'staffSignIn',
        organizationId: '22222222-2222-2222-2222-222222222222'
      }
    });
    expect(resolvePlatformRoute('/auth/forgot-password')).toMatchObject({
      route: { kind: 'forgotPassword' }
    });
    expect(resolvePlatformRoute('/auth/reset-password')).toMatchObject({
      route: { kind: 'resetPassword' }
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

  it('accepts a setup code, stores the staff session, and redirects to /club', async () => {
    window.history.replaceState(null, '', '/auth/accept-invite?code=setup-code-1');
    const fetchMock = vi.fn(async () => jsonResponse(200, buildStaffSignInResponse()));
    vi.stubGlobal('fetch', fetchMock);

    render(<App apiBaseUrl="http://localhost" />);

    expect(screen.getByRole('heading', { name: 'Accept setup code' })).toBeInTheDocument();
    expect(screen.getByLabelText('Setup code')).toHaveValue('setup-code-1');

    fireEvent.change(screen.getByLabelText('User name'), { target: { value: 'owner@demo.test' } });
    fireEvent.change(screen.getByLabelText('Display name'), { target: { value: 'Demo Owner' } });
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'Passw0rd!Real' } });
    fireEvent.change(screen.getByLabelText('Confirm password'), { target: { value: 'Passw0rd!Real' } });
    fireEvent.click(screen.getByRole('button', { name: 'Accept and open club' }));

    await waitFor(() => expect(window.location.pathname).toBe('/club'));
    expect(screen.getByRole('heading', { name: 'Club dashboard' })).toBeInTheDocument();
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

  it('signs in a staff user and redirects to /club', async () => {
    window.history.replaceState(
      null,
      '',
      '/auth/sign-in?organizationId=22222222-2222-2222-2222-222222222222'
    );
    const fetchMock = vi.fn(async () => jsonResponse(200, buildStaffSignInResponse()));
    vi.stubGlobal('fetch', fetchMock);

    render(<App apiBaseUrl="http://localhost" />);

    expect(screen.getByRole('heading', { name: 'Club sign in' })).toBeInTheDocument();
    expect(screen.getByLabelText('Organization')).toHaveValue('22222222-2222-2222-2222-222222222222');

    fireEvent.change(screen.getByLabelText('User name'), { target: { value: 'owner@demo.test' } });
    fireEvent.change(screen.getByLabelText('Password'), { target: { value: 'Passw0rd!Real' } });
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));

    await waitFor(() => expect(window.location.pathname).toBe('/club'));
    expect(screen.getByRole('heading', { name: 'Club dashboard' })).toBeInTheDocument();

    const call = fetchMock.mock.calls[0] as unknown as [string, RequestInit];
    expect(call[0]).toBe('http://localhost/api/auth/staff/sign-in');
    expect(JSON.parse(call[1].body as string)).toEqual({
      organizationId: '22222222-2222-2222-2222-222222222222',
      userName: 'owner@demo.test',
      password: 'Passw0rd!Real'
    });
  });
});
