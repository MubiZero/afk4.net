import { it, expect, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import type { PosProduct } from '@/api/types';
import { useCatalog } from './useCatalog';

const product: PosProduct = {
  productId: 'p1', organizationId: 'org', branchId: 'b1', categoryId: 'c1', name: 'Кола', sku: 'SKU1',
  price: { currencyCode: 'RUB', minorUnits: 150 }, trackStock: false, allowNegativeStock: false,
  isActive: true, stockOnHand: 10, createdAtUtc: '2026-01-01T00:00:00.000Z'
};

it('loads the catalog into product rows', async () => {
  const client = { getCatalog: vi.fn(async () => [product]) };
  const { result } = renderHook(() => useCatalog(client as never, 'b1'));
  await waitFor(() => expect(result.current.status).toBe('ready'));
  if (result.current.status !== 'ready') throw new Error('not ready');
  expect(result.current.rows.map(r => r.name)).toEqual(['Кола']);
  expect(result.current.rows[0].price).toBe(1.5);
});

it('reports an error when the load fails', async () => {
  const client = { getCatalog: vi.fn(async () => { throw new Error('boom'); }) };
  const { result } = renderHook(() => useCatalog(client as never, 'b1'));
  await waitFor(() => expect(result.current.status).toBe('error'));
});
