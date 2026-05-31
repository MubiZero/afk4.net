import { beforeEach, describe, expect, it, mock } from 'bun:test';
import { PlatformApiClient, PlatformApiError } from './platformApi';
import type { PlatformAdminSession } from '../auth/tokenStore';

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' }
  });
}

function emptyResponse(status: number): Response {
  return new Response('', { status });
}

function buildSession(overrides: Partial<PlatformAdminSession> = {}): PlatformAdminSession {
  return {
    platformAdminId: 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee',
    userName: 'admin@platform.test',
    displayName: 'Platform Owner',
    roles: ['platform_owner'],
    permissions: ['platform.tenants.view'],
    accessToken: 'initial-access',
    accessTokenExpiresAtUtc: '2030-01-01T00:00:00Z',
    refreshToken: 'initial-refresh',
    refreshTokenExpiresAtUtc: '2030-02-01T00:00:00Z',
    ...overrides
  };
}

describe('PlatformApiClient', () => {
  beforeEach(() => {
    if (typeof globalThis.sessionStorage !== 'undefined') {
      globalThis.sessionStorage.clear();
    }
  });

  it('signs in and stores the returned session', async () => {
    const fetchImpl = mock(async () =>
      jsonResponse(200, {
        platformAdminId: 'a',
        userName: 'u',
        displayName: 'd',
        accessToken: 't',
        accessTokenExpiresAtUtc: '2030-01-01T00:00:00Z',
        refreshToken: 'r',
        refreshTokenExpiresAtUtc: '2030-02-01T00:00:00Z',
        roles: [],
        permissions: []
      })
    );
    let observed: PlatformAdminSession | null = null;
    const client = new PlatformApiClient({
      baseUrl: 'http://localhost:5000',
      fetchImpl: fetchImpl as unknown as typeof fetch,
      session: null,
      onSessionChanged: next => {
        observed = next;
      }
    });

    const session = await client.signIn('u', 'p');

    expect(session.accessToken).toBe('t');
    expect(observed).not.toBeNull();
    expect(fetchImpl).toHaveBeenCalledWith(
      'http://localhost:5000/api/platform/auth/sign-in',
      expect.objectContaining({ method: 'POST' })
    );
  });

  it('throws PlatformApiError with parsed body on failure', async () => {
    const fetchImpl = mock(async () => jsonResponse(400, { error: 'Slug too short' }));
    const client = new PlatformApiClient({
      baseUrl: 'http://localhost',
      fetchImpl: fetchImpl as unknown as typeof fetch,
      session: buildSession(),
      onSessionChanged: () => {}
    });

    await expect(client.listTenants()).rejects.toMatchObject({
      status: 400,
      message: 'Slug too short'
    });
  });

  it('refreshes the token once on 401 then retries the original call', async () => {
    const responses: Response[] = [
      emptyResponse(401),
      jsonResponse(200, {
        platformAdminId: 'a',
        userName: 'u',
        displayName: 'd',
        accessToken: 'fresh-access',
        accessTokenExpiresAtUtc: '2030-01-01T00:00:00Z',
        refreshToken: 'fresh-refresh',
        refreshTokenExpiresAtUtc: '2030-02-01T00:00:00Z',
        roles: [],
        permissions: []
      }),
      jsonResponse(200, [])
    ];
    const fetchImpl = mock(async () => responses.shift() as Response);
    const onSessionChanged = mock();
    const client = new PlatformApiClient({
      baseUrl: 'http://localhost',
      fetchImpl: fetchImpl as unknown as typeof fetch,
      session: buildSession(),
      onSessionChanged
    });

    const result = await client.listTenants();

    expect(result).toEqual([]);
    expect(fetchImpl).toHaveBeenCalledTimes(3);
    expect(client.getSession()?.accessToken).toBe('fresh-access');
    expect(onSessionChanged).toHaveBeenLastCalledWith(
      expect.objectContaining({ accessToken: 'fresh-access' })
    );
  });

  it('signs out and clears the session when the refresh attempt fails', async () => {
    const fetchImpl = mock(async () => emptyResponse(401));
    const onSessionChanged = mock();
    const client = new PlatformApiClient({
      baseUrl: 'http://localhost',
      fetchImpl: fetchImpl as unknown as typeof fetch,
      session: buildSession(),
      onSessionChanged
    });

    await expect(client.listTenants()).rejects.toBeInstanceOf(PlatformApiError);
    expect(client.getSession()).toBeNull();
    expect(onSessionChanged).toHaveBeenCalledWith(null);
  });

  it('lists owner invites for a tenant and returns the masked summary payload', async () => {
    const listBody = [
      {
        ownerInviteId: '11111111-1111-1111-1111-111111111111',
        organizationId: '22222222-2222-2222-2222-222222222222',
        branchId: '33333333-3333-3333-3333-333333333333',
        codeSuffix: 'ka9p',
        status: 'pending',
        ownerUserName: 'owner@demo.test',
        ownerDisplayName: 'Demo Owner',
        expiresAtUtc: '2026-06-01T00:00:00Z',
        acceptedAtUtc: null,
        revokedAtUtc: null,
        revokedReason: null,
        createdAtUtc: '2026-05-23T00:00:00Z'
      }
    ];
    const fetchImpl = mock(async () => jsonResponse(200, listBody));
    const client = new PlatformApiClient({
      baseUrl: 'http://localhost',
      fetchImpl: fetchImpl as unknown as typeof fetch,
      session: buildSession(),
      onSessionChanged: () => {}
    });

    const invites = await client.listOwnerInvites('22222222-2222-2222-2222-222222222222');

    expect(invites).toHaveLength(1);
    expect(invites[0].codeSuffix).toBe('ka9p');
    const lastCall = fetchImpl.mock.calls[0] as unknown as [string, RequestInit];
    expect(lastCall[0]).toBe('http://localhost/api/platform/tenants/22222222-2222-2222-2222-222222222222/owner-invites');
    expect(lastCall[1].method).toBe('GET');
  });

  it('attaches Bearer token on calls that have a session', async () => {
    const fetchImpl = mock(async () => jsonResponse(200, []));
    const client = new PlatformApiClient({
      baseUrl: 'http://localhost',
      fetchImpl: fetchImpl as unknown as typeof fetch,
      session: buildSession({ accessToken: 'bearer-1' }),
      onSessionChanged: () => {}
    });

    await client.listTenants();

    const lastCall = fetchImpl.mock.calls[0] as unknown as [string, RequestInit];
    const headers = lastCall[1].headers as Record<string, string>;
    expect(headers.Authorization).toBe('Bearer bearer-1');
  });
});
