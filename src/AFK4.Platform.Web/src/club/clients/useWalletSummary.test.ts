import { it, expect, mock } from 'bun:test';
import { renderHook, waitFor } from '@testing-library/react';
import type { WalletSummary } from '@/api/types';
import { useWalletSummary } from './useWalletSummary';

const summary: WalletSummary = {
  playerAccountId: 'p1',
  walletBalance: { currencyCode: 'TJS', minorUnits: 50000 },
  debtBalance: { currencyCode: 'TJS', minorUnits: 0 },
  recentEntries: [{
    ledgerEntryId: 'l1', organizationId: 'org', branchId: 'b1', playerAccountId: 'p1',
    sessionId: null, playerPackageId: null, entryType: 'top_up', accountType: 'wallet',
    amount: { currencyCode: 'TJS', minorUnits: 50000 }, quantitySeconds: 0,
    description: 'd', reason: 'Касса', reversesLedgerEntryId: null,
    createdByStaffUserId: 's1', createdAtUtc: '2026-05-30T10:00:00.000Z'
  }]
};

it('loads a wallet summary into balance and ledger rows', async () => {
  const client = { getWalletSummary: mock(async () => summary) };
  const { result } = renderHook(() => useWalletSummary(client as never, 'p1'));
  await waitFor(() => expect(result.current.status).toBe('ready'));
  if (result.current.status !== 'ready') throw new Error('not ready');
  expect(result.current.balance.walletMajor).toBe(500);
  expect(result.current.ledger.map(r => r.entryType)).toEqual(['top_up']);
  expect(client.getWalletSummary).toHaveBeenCalledWith('p1');
});

it('reports an error when the load fails', async () => {
  const client = { getWalletSummary: mock(async () => { throw new Error('boom'); }) };
  const { result } = renderHook(() => useWalletSummary(client as never, 'p1'));
  await waitFor(() => expect(result.current.status).toBe('error'));
});
