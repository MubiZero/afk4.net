import { PlatformApiClient } from '../../platformApi';
import type { Guid, MoneyDto } from '../types';

// Зеркала контрактов AFK4.Shared.Contracts (camelCase).
export interface PlayerSearchResultDto {
  playerAccountId: Guid;
  displayName: string;
  phoneNumber: string | null;
  walletBalanceMinorUnits: number;
  debtBalanceMinorUnits: number;
  activePackageCount: number;
  isActive: boolean;
}

export interface PlayerAccountDto {
  playerAccountId: Guid;
  organizationId: Guid;
  homeBranchId: Guid;
  displayName: string;
  phoneNumber: string | null;
  isActive: boolean;
  createdAtUtc: string;
}

export interface LedgerEntryDto {
  ledgerEntryId: Guid;
  organizationId: Guid;
  branchId: Guid;
  playerAccountId: Guid;
  sessionId: Guid | null;
  playerPackageId: Guid | null;
  entryType: string;
  accountType: string;
  amount: MoneyDto;
  quantitySeconds: number;
  description: string;
  reason: string;
  reversesLedgerEntryId: Guid | null;
  createdByStaffUserId: Guid;
  createdAtUtc: string;
}

export interface WalletSummaryDto {
  playerAccountId: Guid;
  walletBalance: MoneyDto;
  debtBalance: MoneyDto;
  recentEntries: LedgerEntryDto[];
}

export interface PlayerPackageDto {
  playerPackageId: Guid;
  packageDefinitionId: Guid;
  playerAccountId: Guid;
  name: string;
  purchasedPrice: MoneyDto;
  includedSeconds: number;
  bonusSeconds: number;
  remainingIncludedSeconds: number;
  remainingBonusSeconds: number;
  purchasedAtUtc: string;
  expiresAtUtc: string | null;
}

export interface CreatePlayerAccountRequest {
  organizationId: Guid;
  displayName: string;
  phoneNumber: string | null;
  idempotencyKey: string;
}

export interface TopUpWalletRequest {
  organizationId: Guid;
  amount: MoneyDto;
  reason: string;
  idempotencyKey: string;
}

export interface PayDebtRequest {
  organizationId: Guid;
  amount: MoneyDto;
  reason: string;
  idempotencyKey: string;
}

export interface PurchasePackageRequest {
  organizationId: Guid;
  packageDefinitionId: Guid;
  idempotencyKey: string;
}

export function createPlayerClient(api: PlatformApiClient) {
  return {
    searchPlayers(branchId: Guid, query: string, limit: number): Promise<PlayerSearchResultDto[]> {
      return api.get<PlayerSearchResultDto[]>(`/api/branches/${branchId}/players`, { query, limit });
    },
    createPlayer(branchId: Guid, request: CreatePlayerAccountRequest): Promise<PlayerAccountDto> {
      return api.post<PlayerAccountDto, CreatePlayerAccountRequest>(`/api/branches/${branchId}/players`, request);
    },
    getWalletSummary(playerAccountId: Guid): Promise<WalletSummaryDto> {
      return api.get<WalletSummaryDto>(`/api/players/${playerAccountId}/wallet-summary`);
    },
    getPlayerPackages(playerAccountId: Guid): Promise<PlayerPackageDto[]> {
      return api.get<PlayerPackageDto[]>(`/api/players/${playerAccountId}/packages`);
    },
    purchasePackage(playerAccountId: Guid, request: PurchasePackageRequest): Promise<PlayerPackageDto> {
      return api.post<PlayerPackageDto, PurchasePackageRequest>(`/api/players/${playerAccountId}/packages/purchases`, request);
    },
    topUpWallet(playerAccountId: Guid, request: TopUpWalletRequest): Promise<WalletSummaryDto> {
      return api.post<WalletSummaryDto, TopUpWalletRequest>(`/api/players/${playerAccountId}/wallet/top-ups`, request);
    },
    payDebt(playerAccountId: Guid, request: PayDebtRequest): Promise<WalletSummaryDto> {
      return api.post<WalletSummaryDto, PayDebtRequest>(`/api/players/${playerAccountId}/debts/payments`, request);
    }
  };
}
