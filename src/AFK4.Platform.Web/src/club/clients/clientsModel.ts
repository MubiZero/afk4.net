import type { CreatePlayerAccountRequest, LedgerEntry, PlayerSearchResult, WalletSummary } from '@/api/types';
import { minorToMajor } from '../money';

export interface PlayerRow {
  playerAccountId: string;
  displayName: string;
  phone: string;
  walletMajor: number;
  debtMajor: number;
  activePackageCount: number;
  isActive: boolean;
}

export interface BalanceView {
  walletMajor: number;
  walletCurrency: string;
  debtMajor: number;
  debtCurrency: string;
}

export interface LedgerRow {
  ledgerEntryId: string;
  createdAtUtc: string;
  entryType: string;
  accountType: string;
  amountMajor: number;
  currencyCode: string;
  quantityMinutes: number;
  reason: string;
}

export function toPlayerRows(results: PlayerSearchResult[]): PlayerRow[] {
  return results.map(r => ({
    playerAccountId: r.playerAccountId,
    displayName: r.displayName,
    phone: r.phoneNumber ?? '',
    walletMajor: minorToMajor(r.walletBalanceMinorUnits),
    debtMajor: minorToMajor(r.debtBalanceMinorUnits),
    activePackageCount: r.activePackageCount,
    isActive: r.isActive
  }));
}

export function toBalanceView(summary: WalletSummary): BalanceView {
  return {
    walletMajor: minorToMajor(summary.walletBalance.minorUnits),
    walletCurrency: summary.walletBalance.currencyCode,
    debtMajor: minorToMajor(summary.debtBalance.minorUnits),
    debtCurrency: summary.debtBalance.currencyCode
  };
}

export function toLedgerRows(entries: LedgerEntry[]): LedgerRow[] {
  return entries.map(e => ({
    ledgerEntryId: e.ledgerEntryId,
    createdAtUtc: e.createdAtUtc,
    entryType: e.entryType,
    accountType: e.accountType,
    amountMajor: minorToMajor(e.amount.minorUnits),
    currencyCode: e.amount.currencyCode,
    quantityMinutes: Math.round(e.quantitySeconds / 60),
    reason: e.reason
  }));
}

export function buildCreatePlayerRequest(
  organizationId: string,
  displayName: string,
  phoneNumber: string,
  idempotencyKey: string
): CreatePlayerAccountRequest {
  const trimmedPhone = phoneNumber.trim();
  return {
    organizationId,
    displayName: displayName.trim(),
    phoneNumber: trimmedPhone === '' ? null : trimmedPhone,
    idempotencyKey
  };
}
