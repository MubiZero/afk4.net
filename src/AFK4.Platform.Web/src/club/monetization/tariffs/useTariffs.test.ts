import { it, expect, mock } from 'bun:test';
import { renderHook, waitFor } from '@testing-library/react';
import type { TariffOption } from '@/api/types';
import { useTariffs } from './useTariffs';

const option: TariffOption = {
  tariffId: 't1', tariffVersionId: 'v1', name: 'Дневной', tariffRuleVersionId: 'rv1', versionNumber: 1,
  currencyCode: 'RUB', pricePerMinuteMinorUnits: 250, minimumBillableMinutes: 1, roundingIncrementMinutes: 1,
  effectiveFromUtc: '2026-01-01T00:00:00.000Z'
};

it('loads tariff options into rows', async () => {
  const client = { getTariffOptions: mock(async () => [option]) };
  const { result } = renderHook(() => useTariffs(client as never, 'b1'));
  await waitFor(() => expect(result.current.status).toBe('ready'));
  if (result.current.status !== 'ready') throw new Error('not ready');
  expect(result.current.rows.map(r => r.name)).toEqual(['Дневной']);
  expect(result.current.rows[0].pricePerMinute).toBe(2.5);
});

it('reports an error when the load fails', async () => {
  const client = { getTariffOptions: mock(async () => { throw new Error('boom'); }) };
  const { result } = renderHook(() => useTariffs(client as never, 'b1'));
  await waitFor(() => expect(result.current.status).toBe('error'));
});
