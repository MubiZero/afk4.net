import { it, expect } from 'bun:test';
import type { LedgerEntry, PlayerSearchResult, WalletSummary } from '@/api/types';
import { toPlayerRows, toBalanceView, toLedgerRows, buildCreatePlayerRequest } from './clientsModel';

const result: PlayerSearchResult = {
  playerAccountId: 'p1', displayName: 'Иван', phoneNumber: '+992900',
  walletBalanceMinorUnits: 50000, debtBalanceMinorUnits: 1500, activePackageCount: 2, isActive: true
};

const entry: LedgerEntry = {
  ledgerEntryId: 'l1', organizationId: 'org', branchId: 'b1', playerAccountId: 'p1',
  sessionId: null, playerPackageId: null, entryType: 'top_up', accountType: 'wallet',
  amount: { currencyCode: 'TJS', minorUnits: 50000 }, quantitySeconds: 0,
  description: 'd', reason: 'Касса', reversesLedgerEntryId: null,
  createdByStaffUserId: 's1', createdAtUtc: '2026-05-30T10:00:00.000Z'
};

const summary: WalletSummary = {
  playerAccountId: 'p1',
  walletBalance: { currencyCode: 'TJS', minorUnits: 50000 },
  debtBalance: { currencyCode: 'TJS', minorUnits: 1500 },
  recentEntries: [entry]
};

it('maps search results to rows with major-unit balances and empty phone fallback', () => {
  expect(toPlayerRows([result])[0]).toEqual({
    playerAccountId: 'p1', displayName: 'Иван', phone: '+992900',
    walletMajor: 500, debtMajor: 15, activePackageCount: 2, isActive: true
  });
  const noPhone = toPlayerRows([{ ...result, phoneNumber: null }])[0];
  expect(noPhone.phone).toBe('');
});

it('maps a wallet summary to a balance view', () => {
  expect(toBalanceView(summary)).toEqual({
    walletMajor: 500, walletCurrency: 'TJS', debtMajor: 15, debtCurrency: 'TJS'
  });
});

it('maps ledger entries to rows: minor to major, seconds to minutes', () => {
  expect(toLedgerRows([{ ...entry, quantitySeconds: 1800 }])[0]).toEqual({
    ledgerEntryId: 'l1', createdAtUtc: '2026-05-30T10:00:00.000Z',
    entryType: 'top_up', accountType: 'wallet', amountMajor: 500, currencyCode: 'TJS',
    quantityMinutes: 30, reason: 'Касса'
  });
});

it('builds a create request: trims fields, empty phone becomes null', () => {
  expect(buildCreatePlayerRequest('org', '  Иван  ', '  +992900  ', 'idem')).toEqual({
    organizationId: 'org', displayName: 'Иван', phoneNumber: '+992900', idempotencyKey: 'idem'
  });
  expect(buildCreatePlayerRequest('org', 'Иван', '   ', 'idem').phoneNumber).toBeNull();
});
