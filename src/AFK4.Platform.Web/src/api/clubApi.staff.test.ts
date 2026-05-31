// src/api/clubApi.staff.test.ts
import { it, expect, mock } from 'bun:test';
import { ClubApiClient } from './clubApi';

function okResponse(body: unknown) {
  return { ok: true, status: 200, headers: new Map(), text: async () => JSON.stringify(body) } as unknown as Response;
}

function makeClient(fetchImpl: typeof fetch) {
  return new ClubApiClient({
    baseUrl: 'https://api.test',
    fetchImpl,
    session: {
      staffUserId: 'u1', organizationId: 'org1', displayName: 'D', branchIds: ['b1'], permissions: [],
      accessToken: 'tok', accessTokenExpiresAtUtc: '', refreshToken: 'r', refreshTokenExpiresAtUtc: ''
    },
    onSessionChanged: () => {}
  });
}

it('updateStaffRoles PATCHes the roles route with the role names', async () => {
  const fetchImpl = mock().mockResolvedValue(okResponse({ staffUserId: 's1' }));
  const client = makeClient(fetchImpl as unknown as typeof fetch);
  await client.updateStaffRoles('b1', 's1', { organizationId: 'org1', roleNames: ['branch_manager'] });
  const [url, init] = fetchImpl.mock.calls[0];
  expect(url).toBe('https://api.test/api/branches/b1/staff/s1/roles');
  expect(init.method).toBe('PATCH');
  expect(JSON.parse(init.body)).toEqual({ organizationId: 'org1', roleNames: ['branch_manager'] });
});

it('updateStaffState PATCHes the state route', async () => {
  const fetchImpl = mock().mockResolvedValue(okResponse({ staffUserId: 's1' }));
  const client = makeClient(fetchImpl as unknown as typeof fetch);
  await client.updateStaffState('b1', 's1', { organizationId: 'org1', isActive: false });
  const [url, init] = fetchImpl.mock.calls[0];
  expect(url).toBe('https://api.test/api/branches/b1/staff/s1/state');
  expect(init.method).toBe('PATCH');
  expect(JSON.parse(init.body)).toEqual({ organizationId: 'org1', isActive: false });
});

it('resetStaffPassword POSTs the password-reset route', async () => {
  const fetchImpl = mock().mockResolvedValue(okResponse({ staffUserId: 's1' }));
  const client = makeClient(fetchImpl as unknown as typeof fetch);
  await client.resetStaffPassword('b1', 's1', { organizationId: 'org1', newPassword: 'longenough' });
  const [url, init] = fetchImpl.mock.calls[0];
  expect(url).toBe('https://api.test/api/branches/b1/staff/s1/password-reset');
  expect(init.method).toBe('POST');
  expect(JSON.parse(init.body)).toEqual({ organizationId: 'org1', newPassword: 'longenough' });
});
