import { beforeEach, describe, expect, it, mock } from 'bun:test';
import { PlatformTransport } from './platformTransport';

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
}

function emptyResponse(status: number): Response {
  return new Response('', { status });
}

function sessionBody(overrides: Record<string, unknown> = {}) {
  return {
    platformAdminId: 'a',
    userName: 'u',
    displayName: 'd',
    accessToken: 't',
    accessTokenExpiresAtUtc: '2030-01-01T00:00:00Z',
    refreshToken: 'r',
    refreshTokenExpiresAtUtc: '2030-02-01T00:00:00Z',
    roles: [],
    permissions: [],
    ...overrides
  };
}

describe('PlatformTransport — two-step sign-in', () => {
  beforeEach(() => {
    if (typeof globalThis.sessionStorage !== 'undefined') {
      globalThis.sessionStorage.clear();
    }
  });

  it('signIn returns a challenge and never applies a session', async () => {
    const fetchImpl = mock(async () =>
      jsonResponse(200, { challengeToken: 'chal-1', expiresAtUtc: '2030-01-01T00:02:00Z', twoFactorConfigured: true })
    );
    const onSessionChanged = mock();
    const transport = new PlatformTransport({
      baseUrl: 'http://localhost',
      fetchImpl: fetchImpl as unknown as typeof fetch,
      session: null,
      onSessionChanged
    });

    const outcome = await transport.signIn('u', 'p');

    expect(outcome).toEqual({
      kind: 'challenge',
      challengeToken: 'chal-1',
      twoFactorConfigured: true,
      expiresAtUtc: '2030-01-01T00:02:00Z'
    });
    expect(transport.getSession()).toBeNull();
    expect(onSessionChanged).not.toHaveBeenCalled();
  });

  it('beginTwoFactorSetup returns the secret and QR link without touching the session', async () => {
    const fetchImpl = mock(async () =>
      jsonResponse(200, { secret: 'ABCD1234', otpAuthUri: 'otpauth://totp/AFK4?secret=ABCD1234' })
    );
    const onSessionChanged = mock();
    const transport = new PlatformTransport({
      baseUrl: 'http://localhost',
      fetchImpl: fetchImpl as unknown as typeof fetch,
      session: null,
      onSessionChanged
    });

    const result = await transport.beginTwoFactorSetup('chal-1');

    expect(result).toEqual({ secret: 'ABCD1234', otpAuthUri: 'otpauth://totp/AFK4?secret=ABCD1234' });
    expect(onSessionChanged).not.toHaveBeenCalled();
  });

  it('completeTwoFactorSetup applies the returned session and surfaces recovery codes once', async () => {
    const fetchImpl = mock(async () =>
      jsonResponse(200, { session: sessionBody({ accessToken: 'fresh-access' }), recoveryCodes: ['code-1', 'code-2'] })
    );
    let observed: unknown = null;
    const transport = new PlatformTransport({
      baseUrl: 'http://localhost',
      fetchImpl: fetchImpl as unknown as typeof fetch,
      session: null,
      onSessionChanged: next => { observed = next; }
    });

    const result = await transport.completeTwoFactorSetup('chal-1', '123456');

    expect(result.recoveryCodes).toEqual(['code-1', 'code-2']);
    expect(transport.getSession()?.accessToken).toBe('fresh-access');
    expect(observed).not.toBeNull();
  });

  it('completeTwoFactor (verify) applies the returned session', async () => {
    const fetchImpl = mock(async () => jsonResponse(200, sessionBody({ accessToken: 'verified-access' })));
    const transport = new PlatformTransport({
      baseUrl: 'http://localhost',
      fetchImpl: fetchImpl as unknown as typeof fetch,
      session: null,
      onSessionChanged: () => {}
    });

    const session = await transport.completeTwoFactor('chal-1', '123456');

    expect(session.accessToken).toBe('verified-access');
    expect(transport.getSession()?.accessToken).toBe('verified-access');
  });

  it('rejects with PlatformApiError(429) on lockout and does not apply a session', async () => {
    const fetchImpl = mock(async () => emptyResponse(429));
    const transport = new PlatformTransport({
      baseUrl: 'http://localhost',
      fetchImpl: fetchImpl as unknown as typeof fetch,
      session: null,
      onSessionChanged: () => {}
    });

    await expect(transport.completeTwoFactor('chal-1', '000000')).rejects.toMatchObject({ status: 429 });
    expect(transport.getSession()).toBeNull();
  });
});
