import { beforeEach, describe, expect, it, mock } from 'bun:test';
import { readStaffSession, type StaffSession } from '../auth/staffTokenStore';
import { StaffAuthApiClient, StaffSignInChooseClubError } from './staffAuthApi';
import { PlatformApiError } from './platformApi';

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });
}

function buildResponse() {
  return {
    staffUserId: '11111111-1111-1111-1111-111111111111',
    organizationId: '22222222-2222-2222-2222-222222222222',
    displayName: 'Demo Owner',
    accessToken: 'staff-access',
    accessTokenExpiresAtUtc: '2030-01-01T00:00:00Z',
    refreshToken: 'staff-refresh',
    refreshTokenExpiresAtUtc: '2030-02-01T00:00:00Z',
    branchIds: ['33333333-3333-3333-3333-333333333333'],
    permissions: ['layout.manage']
  };
}

describe('StaffAuthApiClient', () => {
  beforeEach(() => {
    if (typeof globalThis.sessionStorage !== 'undefined') {
      globalThis.sessionStorage.clear();
    }
  });

  it('accepts an invite and stores the staff session', async () => {
    const fetchImpl = mock(async () => jsonResponse(200, buildResponse()));
    const onSessionChanged = mock();
    const client = new StaffAuthApiClient({
      baseUrl: 'http://localhost:5000/',
      fetchImpl: fetchImpl as unknown as typeof fetch,
      session: null,
      onSessionChanged
    });

    const session = await client.acceptInvite({
      code: 'setup-code',
      userName: 'owner@demo.test',
      displayName: 'Demo Owner',
      password: 'Passw0rd!Real'
    });

    expect(session.accessToken).toBe('staff-access');
    expect(readStaffSession()?.staffUserId).toBe('11111111-1111-1111-1111-111111111111');
    expect(onSessionChanged).toHaveBeenCalledWith(expect.objectContaining({ displayName: 'Demo Owner' }));
    expect(fetchImpl).toHaveBeenCalledWith(
      'http://localhost:5000/api/platform/owner-invites/accept',
      expect.objectContaining({ method: 'POST' })
    );
  });

  it('signs in by login through the sign-in-by-login endpoint', async () => {
    const fetchImpl = mock(async () => jsonResponse(200, buildResponse()));
    const client = new StaffAuthApiClient({
      baseUrl: 'http://localhost',
      fetchImpl: fetchImpl as unknown as typeof fetch,
      session: null,
      onSessionChanged: () => {}
    });

    await client.signInByLogin('owner@demo.test', 'Passw0rd!Real');

    const call = fetchImpl.mock.calls[0] as unknown as [string, RequestInit];
    expect(call[0]).toBe('http://localhost/api/auth/staff/sign-in-by-login');
    expect(JSON.parse(call[1].body as string)).toEqual({
      login: 'owner@demo.test',
      password: 'Passw0rd!Real'
    });
  });

  it('throws StaffSignInChooseClubError on a 409 with clubs', async () => {
    const clubs = [
      { organizationId: 'org-a', name: 'Club A' },
      { organizationId: 'org-b', name: 'Club B' }
    ];
    const fetchImpl = mock(async () => jsonResponse(409, { clubs }));
    const client = new StaffAuthApiClient({
      baseUrl: 'http://localhost',
      fetchImpl: fetchImpl as unknown as typeof fetch,
      session: null,
      onSessionChanged: () => {}
    });

    await expect(client.signInByLogin('shared@demo.test', 'pw'))
      .rejects.toMatchObject({ clubs });
  });

  it('signs in to a chosen club through the org-scoped endpoint', async () => {
    const fetchImpl = mock(async () => jsonResponse(200, buildResponse()));
    const client = new StaffAuthApiClient({
      baseUrl: 'http://localhost',
      fetchImpl: fetchImpl as unknown as typeof fetch,
      session: null,
      onSessionChanged: () => {}
    });

    await client.signInToClub('org-b', 'shared@demo.test', 'pw');

    const call = fetchImpl.mock.calls[0] as unknown as [string, RequestInit];
    expect(call[0]).toBe('http://localhost/api/auth/staff/sign-in');
    expect(JSON.parse(call[1].body as string)).toEqual({
      organizationId: 'org-b',
      userName: 'shared@demo.test',
      password: 'pw'
    });
  });

  it('clears the local staff session on local sign-out', () => {
    const session: StaffSession = {
      staffUserId: '11111111-1111-1111-1111-111111111111',
      organizationId: '22222222-2222-2222-2222-222222222222',
      displayName: 'Demo Owner',
      branchIds: [],
      permissions: [],
      accessToken: 'staff-access',
      accessTokenExpiresAtUtc: '2030-01-01T00:00:00Z',
      refreshToken: 'staff-refresh',
      refreshTokenExpiresAtUtc: '2030-02-01T00:00:00Z'
    };
    const onSessionChanged = mock();
    const client = new StaffAuthApiClient({
      baseUrl: 'http://localhost',
      fetchImpl: mock() as unknown as typeof fetch,
      session,
      onSessionChanged
    });

    client.signOutLocal();

    expect(client.getSession()).toBeNull();
    expect(readStaffSession()).toBeNull();
    expect(onSessionChanged).toHaveBeenCalledWith(null);
  });

  it('throws a parsed PlatformApiError when login sign-in fails', async () => {
    const fetchImpl = mock(async () => jsonResponse(401, { error: 'Bad credentials' }));
    const client = new StaffAuthApiClient({
      baseUrl: 'http://localhost',
      fetchImpl: fetchImpl as unknown as typeof fetch,
      session: null,
      onSessionChanged: () => {}
    });

    await expect(client.signInByLogin('owner', 'wrong')).rejects.toMatchObject({
      status: 401,
      message: 'Bad credentials'
    });
    await expect(client.signInByLogin('owner', 'wrong')).rejects.toBeInstanceOf(PlatformApiError);
  });
});
