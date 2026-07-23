import { test, expect } from 'bun:test';
import { StaffAuthApi, ChooseClubError } from './staffAuthApi';

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
