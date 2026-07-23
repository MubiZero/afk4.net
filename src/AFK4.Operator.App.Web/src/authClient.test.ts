import { afterEach, beforeEach, describe, expect, it, mock, type Mock } from 'bun:test';
import {
  ChooseClubError,
  forgotPasswordByEmail,
  loadOperatorSession,
  refreshOperatorSession,
  resetPasswordByPhone,
  signInByLoginOperator,
  signInToClubOperator,
  signOutOperator
} from './authClient';

// authClient.ts инстанцирует StaffAuthApi внутри (см. `function api()`) — снаружи нет способа
// впрыснуть свой fetchImpl, кроме подмены глобального fetch. Это тот же трюк, что использует
// App.test.tsx для платформенного API, и он не трогает модульный реестр (в отличие от
// mock.module), поэтому не течёт в соседние тестовые файлы (см. [[frontends-on-bun-test]]).
type FetchLike = (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>;

const originalFetch = globalThis.fetch;
let fetchMock: Mock<FetchLike>;

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
}

const sampleResponse = {
  staffUserId: 's', organizationId: 'o', displayName: 'D',
  accessToken: 'a', accessTokenExpiresAtUtc: '2999-01-01T00:00:00Z',
  refreshToken: 'r', refreshTokenExpiresAtUtc: '2999-01-01T00:00:00Z',
  branchIds: ['b1'], permissions: ['floor-map:view'], roleNames: []
};

beforeEach(() => {
  sessionStorage.clear();
  fetchMock = mock(async () => jsonResponse(200, sampleResponse)) as unknown as Mock<FetchLike>;
  globalThis.fetch = fetchMock as unknown as typeof fetch;
});

afterEach(() => {
  globalThis.fetch = originalFetch;
  sessionStorage.clear();
});

describe('signInByLoginOperator', () => {
  it('stores the session on success', async () => {
    const session = await signInByLoginOperator('u', 'p');

    expect(session.organizationId).toBe('o');
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(String(fetchMock.mock.calls[0]?.[0])).toContain('/api/auth/staff/sign-in-by-login');
    expect(sessionStorage.getItem('afk4.staff.session')).toContain('"accessToken":"a"');
  });

  it('throws ChooseClubError on a club collision (409) without storing a session', async () => {
    fetchMock.mockImplementation(async () => jsonResponse(409, {
      clubs: [{ organizationId: 'o1', name: 'Club 1' }, { organizationId: 'o2', name: 'Club 2' }]
    }));

    const error = await signInByLoginOperator('u', 'p').catch((cause) => cause);

    expect(error).toBeInstanceOf(ChooseClubError);
    expect((error as ChooseClubError).clubs).toHaveLength(2);
    expect(sessionStorage.getItem('afk4.staff.session')).toBeNull();
  });

  it('throws on invalid credentials (401) without storing a session', async () => {
    fetchMock.mockImplementation(async () => jsonResponse(401, {}));

    await expect(signInByLoginOperator('u', 'p')).rejects.toThrow();
    expect(sessionStorage.getItem('afk4.staff.session')).toBeNull();
  });
});

describe('signInToClubOperator', () => {
  it('posts the chosen club and stores the returned session', async () => {
    await signInToClubOperator('o', 'u', 'p');

    expect(String(fetchMock.mock.calls[0]?.[0])).toContain('/api/auth/staff/sign-in');
    expect(JSON.parse(String(fetchMock.mock.calls[0]?.[1]?.body))).toEqual({
      organizationId: 'o', userName: 'u', password: 'p'
    });
    expect(sessionStorage.getItem('afk4.staff.session')).toContain('"organizationId":"o"');
  });
});

describe('loadOperatorSession', () => {
  it('resolves null when nothing is stored', async () => {
    await expect(loadOperatorSession()).resolves.toBeNull();
  });

  it('resolves the stored session without a network call', async () => {
    await signInByLoginOperator('u', 'p');
    fetchMock.mockClear();

    const session = await loadOperatorSession();

    expect(session?.organizationId).toBe('o');
    expect(fetchMock).not.toHaveBeenCalled();
  });
});

describe('refreshOperatorSession', () => {
  it('throws when there is no session to refresh', async () => {
    await expect(refreshOperatorSession()).rejects.toThrow('No session to refresh.');
  });

  it('posts the stored organizationId + refreshToken and rewrites the session', async () => {
    await signInByLoginOperator('u', 'p');
    fetchMock.mockClear();
    fetchMock.mockImplementation(async () => jsonResponse(200, { ...sampleResponse, accessToken: 'a2' }));

    const session = await refreshOperatorSession();

    expect(session.accessToken).toBe('a2');
    expect(JSON.parse(String(fetchMock.mock.calls[0]?.[1]?.body))).toEqual({
      organizationId: 'o', refreshToken: 'r'
    });
    expect(sessionStorage.getItem('afk4.staff.session')).toContain('"accessToken":"a2"');
  });
});

describe('signOutOperator', () => {
  it('clears the stored session locally without a network call', async () => {
    await signInByLoginOperator('u', 'p');
    fetchMock.mockClear();

    await expect(signOutOperator()).resolves.toEqual({ signedOut: true });

    expect(fetchMock).not.toHaveBeenCalled();
    expect(sessionStorage.getItem('afk4.staff.session')).toBeNull();
  });
});

describe('forgot/reset password', () => {
  it('forgotPasswordByEmail posts to the email endpoint', async () => {
    fetchMock.mockImplementation(async () => new Response(null, { status: 204 }));
    await forgotPasswordByEmail('owner@demo.test');

    expect(String(fetchMock.mock.calls[0]?.[0])).toContain('/api/auth/staff/forgot-password');
    expect(JSON.parse(String(fetchMock.mock.calls[0]?.[1]?.body))).toEqual({ userNameOrEmail: 'owner@demo.test' });
  });

  it('resetPasswordByPhone posts to the phone reset endpoint', async () => {
    fetchMock.mockImplementation(async () => new Response(null, { status: 204 }));
    await resetPasswordByPhone('+992937380070', '123456', 'Passw0rd!New');

    expect(String(fetchMock.mock.calls[0]?.[0])).toContain('/api/auth/staff/reset-password-by-phone');
    expect(JSON.parse(String(fetchMock.mock.calls[0]?.[1]?.body))).toEqual({
      phoneNumber: '+992937380070', code: '123456', newPassword: 'Passw0rd!New'
    });
  });
});
