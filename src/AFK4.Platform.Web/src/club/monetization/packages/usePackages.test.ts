import { it, expect, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import type { PackageOption } from '@/api/types';
import { usePackages } from './usePackages';

const option: PackageOption = {
  packageDefinitionId: 'pk1', name: 'Старт', currencyCode: 'RUB', priceMinorUnits: 50000,
  includedSeconds: 3600, bonusSeconds: 600, expiresAfterDays: 30
};

it('loads package options into rows', async () => {
  const client = { getPackageOptions: vi.fn(async () => [option]) };
  const { result } = renderHook(() => usePackages(client as never, 'b1'));
  await waitFor(() => expect(result.current.status).toBe('ready'));
  if (result.current.status !== 'ready') throw new Error('not ready');
  expect(result.current.rows.map(r => r.name)).toEqual(['Старт']);
  expect(result.current.rows[0].price).toBe(500);
  expect(result.current.rows[0].includedMinutes).toBe(60);
});

it('reports an error when the load fails', async () => {
  const client = { getPackageOptions: vi.fn(async () => { throw new Error('boom'); }) };
  const { result } = renderHook(() => usePackages(client as never, 'b1'));
  await waitFor(() => expect(result.current.status).toBe('error'));
});
