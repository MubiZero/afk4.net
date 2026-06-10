import { it, expect, mock } from 'bun:test';
import { ClubApiClient } from './clubApi';

function jsonResponse(body: unknown): Response {
  return new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } });
}
function makeClient(fetchImpl: typeof fetch): ClubApiClient {
  return new ClubApiClient({ baseUrl: 'https://api.test', fetchImpl, session: null, onSessionChanged: () => {} });
}

it('getPackageOptions GETs the branch options route', async () => {
  const fetchImpl = mock(async () => jsonResponse([])) as unknown as typeof fetch;
  await makeClient(fetchImpl).packages.getPackageOptions('b1');
  expect(fetchImpl).toHaveBeenCalledWith('https://api.test/api/branches/b1/packages/options', expect.objectContaining({ method: 'GET' }));
});

it('createPackageDefinition POSTs to the packages route', async () => {
  const fetchImpl = mock(async () => jsonResponse({ packageDefinitionId: 'pk1' })) as unknown as typeof fetch;
  await makeClient(fetchImpl).packages.createPackageDefinition('b1', {
    organizationId: 'org', name: 'Старт', price: { currencyCode: 'RUB', minorUnits: 50000 },
    includedSeconds: 3600, bonusSeconds: 0, expiresAfterDays: 30, idempotencyKey: 'k1'
  });
  const call = (fetchImpl as unknown as { mock: { calls: [string, RequestInit][] } }).mock.calls[0];
  expect(call[0]).toBe('https://api.test/api/branches/b1/packages');
  expect(call[1].method).toBe('POST');
  expect(JSON.parse(call[1].body as string)).toMatchObject({ organizationId: 'org', name: 'Старт', price: { currencyCode: 'RUB', minorUnits: 50000 } });
});

it('updatePackageDefinition PATCHes the package route', async () => {
  const fetchImpl = mock(async () => jsonResponse({ packageDefinitionId: 'pk1' })) as unknown as typeof fetch;
  await makeClient(fetchImpl).packages.updatePackageDefinition('b1', 'pk1', {
    organizationId: 'org', name: 'Старт', price: { currencyCode: 'RUB', minorUnits: 60000 },
    includedSeconds: 3600, bonusSeconds: 0, expiresAfterDays: 30, isActive: true
  });
  const call = (fetchImpl as unknown as { mock: { calls: [string, RequestInit][] } }).mock.calls[0];
  expect(call[0]).toBe('https://api.test/api/branches/b1/packages/pk1');
  expect(call[1].method).toBe('PATCH');
});
