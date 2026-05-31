import { it, expect, mock } from 'bun:test';
import { ClubApiClient } from './clubApi';

function jsonResponse(body: unknown): Response {
  return new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } });
}
function makeClient(fetchImpl: typeof fetch): ClubApiClient {
  return new ClubApiClient({ baseUrl: 'https://api.test', fetchImpl, session: null, onSessionChanged: () => {} });
}

it('getCatalog GETs the branch catalog route', async () => {
  const fetchImpl = mock(async () => jsonResponse([])) as unknown as typeof fetch;
  await makeClient(fetchImpl).getCatalog('b1');
  expect(fetchImpl).toHaveBeenCalledWith('https://api.test/api/branches/b1/pos/catalog', expect.objectContaining({ method: 'GET' }));
});

it('createProductCategory POSTs to the categories route', async () => {
  const fetchImpl = mock(async () => jsonResponse({ categoryId: 'c1' })) as unknown as typeof fetch;
  await makeClient(fetchImpl).createProductCategory('b1', { organizationId: 'org', name: 'Drinks', idempotencyKey: 'k1' });
  const call = (fetchImpl as unknown as { mock: { calls: [string, RequestInit][] } }).mock.calls[0];
  expect(call[0]).toBe('https://api.test/api/branches/b1/pos/categories');
  expect(call[1].method).toBe('POST');
  expect(JSON.parse(call[1].body as string)).toEqual({ organizationId: 'org', name: 'Drinks', idempotencyKey: 'k1' });
});

it('createProduct POSTs to the products route', async () => {
  const fetchImpl = mock(async () => jsonResponse({ productId: 'p1' })) as unknown as typeof fetch;
  await makeClient(fetchImpl).createProduct('b1', {
    organizationId: 'org', categoryId: 'c1', name: 'Cola', sku: 'SKU1',
    price: { currencyCode: 'RUB', minorUnits: 150 }, trackStock: false, allowNegativeStock: false, idempotencyKey: 'k2'
  });
  const call = (fetchImpl as unknown as { mock: { calls: [string, RequestInit][] } }).mock.calls[0];
  expect(call[0]).toBe('https://api.test/api/branches/b1/pos/products');
  expect(call[1].method).toBe('POST');
});

it('updateProduct PATCHes the product route', async () => {
  const fetchImpl = mock(async () => jsonResponse({ productId: 'p1' })) as unknown as typeof fetch;
  await makeClient(fetchImpl).updateProduct('b1', 'p1', {
    organizationId: 'org', categoryId: 'c1', name: 'Cola', sku: 'SKU1',
    price: { currencyCode: 'RUB', minorUnits: 200 }, trackStock: false, allowNegativeStock: false, isActive: true
  });
  const call = (fetchImpl as unknown as { mock: { calls: [string, RequestInit][] } }).mock.calls[0];
  expect(call[0]).toBe('https://api.test/api/branches/b1/pos/products/p1');
  expect(call[1].method).toBe('PATCH');
});
