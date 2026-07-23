import { test, expect } from 'bun:test';
import { StaffAuthApi, ChooseClubError, StaffAuthApiError, isUnauthorizedStaffAuthError } from './staffAuthApi';

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
}

test('signInByLogin returns session on 200', async () => {
  const api = new StaffAuthApi('http://x/', async (url, init) => {
    expect(String(url)).toBe('http://x/api/auth/staff/sign-in-by-login');
    expect(JSON.parse(String(init?.body))).toEqual({ login: 'u', password: 'p' });
    return jsonResponse(200, { staffUserId: 's', organizationId: 'o', displayName: 'D',
      accessToken: 'a', accessTokenExpiresAtUtc: 'x', refreshToken: 'r', refreshTokenExpiresAtUtc: 'y',
      branchIds: ['b1'], permissions: [], roleNames: [] });
  });
  const s = await api.signInByLogin('u', 'p');
  expect(s.branchIds).toEqual(['b1']);
});

test('signInByLogin throws ChooseClubError on 409', async () => {
  const api = new StaffAuthApi('http://x/', async () =>
    jsonResponse(409, { clubs: [{ organizationId: 'o1', name: 'Club 1' }, { organizationId: 'o2', name: 'Club 2' }] }));
  const err = await api.signInByLogin('u', 'p').catch((e) => e);
  expect(err).toBeInstanceOf(ChooseClubError);
  expect((err as ChooseClubError).clubs).toHaveLength(2);
});

test('signInByLogin throws on 401', async () => {
  const api = new StaffAuthApi('http://x/', async () => jsonResponse(401, {}));
  await expect(api.signInByLogin('u', 'p')).rejects.toThrow();
});

test('signInByLogin throws a StaffAuthApiError with the status and parsed body on 401', async () => {
  const api = new StaffAuthApi('http://x/', async () => jsonResponse(401, { error: 'invalid_credentials' }));
  const err = await api.signInByLogin('u', 'p').catch((e) => e);
  expect(err).toBeInstanceOf(StaffAuthApiError);
  expect((err as StaffAuthApiError).status).toBe(401);
  expect((err as StaffAuthApiError).body).toEqual({ error: 'invalid_credentials' });
  expect(isUnauthorizedStaffAuthError(err)).toBe(true);
});

test('refresh throws a non-401 StaffAuthApiError on a server error, without flagging it as unauthorized', async () => {
  const api = new StaffAuthApi('http://x/', async () => jsonResponse(500, { error: 'internal_error' }));
  const err = await api.refresh('o', 'r').catch((e) => e);
  expect(err).toBeInstanceOf(StaffAuthApiError);
  expect((err as StaffAuthApiError).status).toBe(500);
  expect(isUnauthorizedStaffAuthError(err)).toBe(false);
});

test('StaffAuthApiError tolerates a non-JSON error body', async () => {
  const api = new StaffAuthApi('http://x/', async () => new Response('Gateway Timeout', { status: 504 }));
  const err = await api.refresh('o', 'r').catch((e) => e);
  expect(err).toBeInstanceOf(StaffAuthApiError);
  expect((err as StaffAuthApiError).status).toBe(504);
  expect((err as StaffAuthApiError).body).toBeNull();
});

test('refresh posts organizationId + refreshToken', async () => {
  const api = new StaffAuthApi('http://x/', async (url, init) => {
    expect(String(url)).toBe('http://x/api/auth/staff/refresh');
    expect(JSON.parse(String(init?.body))).toEqual({ organizationId: 'o', refreshToken: 'r' });
    return jsonResponse(200, { staffUserId: 's', organizationId: 'o', displayName: 'D',
      accessToken: 'a2', accessTokenExpiresAtUtc: 'x', refreshToken: 'r2', refreshTokenExpiresAtUtc: 'y',
      branchIds: ['b1'], permissions: [], roleNames: [] });
  });
  const s = await api.refresh('o', 'r');
  expect(s.accessToken).toBe('a2');
});
