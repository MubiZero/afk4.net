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
  createdAtUtc: string;
  lastActivityAtUtc: string | null;
  activePackageName: string | null;
  activePackageRemainingMinutes: number;
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

// Зеркало AFK4.Shared.Contracts.Common.CursorPage<T> (camelCase): страница + курсор следующей
// страницы (null = больше нет).
export interface CursorPageDto<T> {
  items: T[];
  nextCursor: string | null;
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

export interface ManualLedgerCorrectionRequest {
  organizationId: Guid;
  accountType: string;
  amount: MoneyDto;
  quantitySeconds: number;
  reason: string;
  idempotencyKey: string;
}

export interface RefundLedgerEntryRequest {
  organizationId: Guid;
  ledgerEntryId: Guid;
  amount: MoneyDto;
  reason: string;
  idempotencyKey: string;
}

export interface UpdatePlayerAccountRequest {
  organizationId: Guid;
  displayName: string;
  phoneNumber: string | null;
}

export interface SetPlayerActiveStateRequest {
  organizationId: Guid;
  isActive: boolean;
}

export function createPlayerClient(api: PlatformApiClient) {
  return {
    searchPlayers(branchId: Guid, query: string, limit: number, includeInactive = false): Promise<PlayerSearchResultDto[]> {
      const params: Record<string, string | number> = { query, limit };
      if (includeInactive) params.includeInactive = 'true';
      return api.get<PlayerSearchResultDto[]>(`branches/${branchId}/players`, params);
    },
    createPlayer(branchId: Guid, request: CreatePlayerAccountRequest): Promise<PlayerAccountDto> {
      return api.post<PlayerAccountDto, CreatePlayerAccountRequest>(`branches/${branchId}/players`, request);
    },
    getWalletSummary(playerAccountId: Guid): Promise<WalletSummaryDto> {
      return api.get<WalletSummaryDto>(`players/${playerAccountId}/wallet-summary`);
    },
    getLedger(
      playerAccountId: Guid,
      params: { entryType?: string; accountType?: string; cursor?: string; limit?: number } = {}
    ): Promise<CursorPageDto<LedgerEntryDto>> {
      const query: Record<string, string | number> = {};
      if (params.entryType) query.entryType = params.entryType;
      if (params.accountType) query.accountType = params.accountType;
      if (params.cursor) query.before = params.cursor; // курсор уходит на бэк как `before`
      if (params.limit !== undefined) query.limit = params.limit;
      return api.get<CursorPageDto<LedgerEntryDto>>(`players/${playerAccountId}/ledger`, query);
    },
    getPlayerPackages(playerAccountId: Guid): Promise<PlayerPackageDto[]> {
      return api.get<PlayerPackageDto[]>(`players/${playerAccountId}/packages`);
    },
    purchasePackage(playerAccountId: Guid, request: PurchasePackageRequest): Promise<PlayerPackageDto> {
      return api.post<PlayerPackageDto, PurchasePackageRequest>(`players/${playerAccountId}/packages/purchases`, request);
    },
    topUpWallet(playerAccountId: Guid, request: TopUpWalletRequest): Promise<WalletSummaryDto> {
      return api.post<WalletSummaryDto, TopUpWalletRequest>(`players/${playerAccountId}/wallet/top-ups`, request);
    },
    payDebt(playerAccountId: Guid, request: PayDebtRequest): Promise<WalletSummaryDto> {
      return api.post<WalletSummaryDto, PayDebtRequest>(`players/${playerAccountId}/debts/payments`, request);
    },
    manualCorrection(playerAccountId: Guid, request: ManualLedgerCorrectionRequest): Promise<WalletSummaryDto> {
      return api.post<WalletSummaryDto, ManualLedgerCorrectionRequest>(`players/${playerAccountId}/ledger/manual-corrections`, request);
    },
    refundLedgerEntry(playerAccountId: Guid, ledgerEntryId: Guid, request: RefundLedgerEntryRequest): Promise<LedgerEntryDto> {
      return api.post<LedgerEntryDto, RefundLedgerEntryRequest>(`players/${playerAccountId}/ledger/${ledgerEntryId}/refunds`, request);
    },
    updateProfile(branchId: Guid, playerAccountId: Guid, request: UpdatePlayerAccountRequest): Promise<PlayerAccountDto> {
      return api.patch<PlayerAccountDto, UpdatePlayerAccountRequest>(`branches/${branchId}/players/${playerAccountId}`, request);
    },
    setActiveState(branchId: Guid, playerAccountId: Guid, request: SetPlayerActiveStateRequest): Promise<PlayerAccountDto> {
      return api.post<PlayerAccountDto, SetPlayerActiveStateRequest>(`branches/${branchId}/players/${playerAccountId}/active-state`, request);
    }
  };
}
