import { beforeEach, describe, expect, it, vi } from 'vitest';
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
    const fetchImpl = vi.fn(async () =>
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
    const fetchImpl = vi.fn(async () => jsonResponse(400, { error: 'Slug too short' }));
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
    const fetchImpl = vi.fn(async () => responses.shift() as Response);
    const onSessionChanged = vi.fn();
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
    const fetchImpl = vi.fn(async () => emptyResponse(401));
    const onSessionChanged = vi.fn();
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

  it('attaches Bearer token on calls that have a session', async () => {
    const fetchImpl = vi.fn(async () => jsonResponse(200, []));
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
