import { it, expect, mock } from 'bun:test';
import { renderHook, waitFor } from '@testing-library/react';
import type { PlayerSearchResult } from '@/api/types';
import { useClientSearch } from './useClientSearch';

const result: PlayerSearchResult = {
  playerAccountId: 'p1', displayName: 'Иван', phoneNumber: '+992900',
  walletBalanceMinorUnits: 50000, debtBalanceMinorUnits: 0, activePackageCount: 0, isActive: true
};

it('loads search results into rows', async () => {
  const client = { searchPlayers: mock(async () => [result]) };
  const { result: hook } = renderHook(() => useClientSearch(client as never, 'b1', ''));
  await waitFor(() => expect(hook.current.status).toBe('ready'));
  if (hook.current.status !== 'ready') throw new Error('not ready');
  expect(hook.current.rows.map(r => r.displayName)).toEqual(['Иван']);
  expect(client.searchPlayers).toHaveBeenCalledWith('b1', '', 20);
});

it('passes the query through to the API', async () => {
  const client = { searchPlayers: mock(async () => []) };
  renderHook(() => useClientSearch(client as never, 'b1', 'иван'));
  await waitFor(() => expect(client.searchPlayers).toHaveBeenCalledWith('b1', 'иван', 20));
});

it('reports an error when the load fails', async () => {
  const client = { searchPlayers: mock(async () => { throw new Error('boom'); }) };
  const { result: hook } = renderHook(() => useClientSearch(client as never, 'b1', ''));
  await waitFor(() => expect(hook.current.status).toBe('error'));
});
