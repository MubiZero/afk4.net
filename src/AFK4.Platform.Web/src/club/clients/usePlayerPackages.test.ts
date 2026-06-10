import { it, expect, mock } from 'bun:test';
import { renderHook, waitFor } from '@testing-library/react';
import type { PackageOption, PlayerPackage } from '@/api/types';
import { usePlayerPackages } from './usePlayerPackages';

const pkg: PlayerPackage = {
  playerPackageId: 'pp1', packageDefinitionId: 'pd1', playerAccountId: 'p1', name: 'Старт',
  purchasedPrice: { currencyCode: 'TJS', minorUnits: 50000 },
  includedSeconds: 3600, bonusSeconds: 600, remainingIncludedSeconds: 1800, remainingBonusSeconds: 300,
  purchasedAtUtc: '2026-05-01T00:00:00.000Z', expiresAtUtc: null
};

const option: PackageOption = {
  packageDefinitionId: 'pd1', name: 'Старт', currencyCode: 'TJS', priceMinorUnits: 50000,
  includedSeconds: 3600, bonusSeconds: 600, expiresAfterDays: 30
};

it('loads player packages and purchase choices', async () => {
  const client = {
    getPlayerPackages: mock(async () => [pkg]),
    getPackageOptions: mock(async () => [option])
  };
  const { result } = renderHook(() => usePlayerPackages({ players: client, packages: client } as never, 'p1', 'b1'));
  await waitFor(() => expect(result.current.status).toBe('ready'));
  if (result.current.status !== 'ready') throw new Error('not ready');
  expect(result.current.rows.map(r => r.name)).toEqual(['Старт']);
  expect(result.current.choices).toEqual([{ packageDefinitionId: 'pd1', name: 'Старт' }]);
  expect(client.getPlayerPackages).toHaveBeenCalledWith('p1');
  expect(client.getPackageOptions).toHaveBeenCalledWith('b1');
});

it('reports an error when a load fails', async () => {
  const client = {
    getPlayerPackages: mock(async () => { throw new Error('boom'); }),
    getPackageOptions: mock(async () => [option])
  };
  const { result } = renderHook(() => usePlayerPackages({ players: client, packages: client } as never, 'p1', 'b1'));
  await waitFor(() => expect(result.current.status).toBe('error'));
});
