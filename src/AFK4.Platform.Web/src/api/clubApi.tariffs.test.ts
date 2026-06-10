import { it, expect, mock } from 'bun:test';
import { ClubApiClient } from './clubApi';

function jsonResponse(body: unknown): Response {
  return new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } });
}

function makeClient(fetchImpl: typeof fetch): ClubApiClient {
  return new ClubApiClient({ baseUrl: 'https://api.test', fetchImpl, session: null, onSessionChanged: () => {} });
}

it('getTariffOptions GETs the branch options route', async () => {
  const fetchImpl = mock(async () => jsonResponse([])) as unknown as typeof fetch;
  await makeClient(fetchImpl).tariffs.getTariffOptions('b1');
  expect(fetchImpl).toHaveBeenCalledWith('https://api.test/api/branches/b1/tariffs/options', expect.objectContaining({ method: 'GET' }));
});

it('createTariff POSTs the body to the tariffs route', async () => {
  const fetchImpl = mock(async () => jsonResponse({ tariffId: 't1' })) as unknown as typeof fetch;
  await makeClient(fetchImpl).tariffs.createTariff('b1', { organizationId: 'org', name: 'Day', idempotencyKey: 'k1' });
  const call = (fetchImpl as unknown as { mock: { calls: [string, RequestInit][] } }).mock.calls[0];
  expect(call[0]).toBe('https://api.test/api/branches/b1/tariffs');
  expect(call[1].method).toBe('POST');
  expect(JSON.parse(call[1].body as string)).toEqual({ organizationId: 'org', name: 'Day', idempotencyKey: 'k1' });
});

it('createTariffVersion POSTs to the versions route', async () => {
  const fetchImpl = mock(async () => jsonResponse({ tariffVersionId: 'v1' })) as unknown as typeof fetch;
  await makeClient(fetchImpl).tariffs.createTariffVersion('b1', 't1', {
    organizationId: 'org', tariffId: 't1', currencyCode: 'RUB', pricePerMinuteMinorUnits: 250,
    minimumBillableMinutes: 1, roundingIncrementMinutes: 1, effectiveFromUtc: '2026-01-01T00:00:00.000Z', idempotencyKey: 'k2'
  });
  const call = (fetchImpl as unknown as { mock: { calls: [string, RequestInit][] } }).mock.calls[0];
  expect(call[0]).toBe('https://api.test/api/branches/b1/tariffs/t1/versions');
  expect(call[1].method).toBe('POST');
});

it('updateTariffVersion PATCHes the version route', async () => {
  const fetchImpl = mock(async () => jsonResponse({ tariffVersionId: 'v1' })) as unknown as typeof fetch;
  await makeClient(fetchImpl).tariffs.updateTariffVersion('b1', 't1', 'v1', {
    organizationId: 'org', currencyCode: 'RUB', pricePerMinuteMinorUnits: 300,
    minimumBillableMinutes: 1, roundingIncrementMinutes: 1, effectiveFromUtc: '2026-01-01T00:00:00.000Z', isActive: true
  });
  const call = (fetchImpl as unknown as { mock: { calls: [string, RequestInit][] } }).mock.calls[0];
  expect(call[0]).toBe('https://api.test/api/branches/b1/tariffs/t1/versions/v1');
  expect(call[1].method).toBe('PATCH');
});
