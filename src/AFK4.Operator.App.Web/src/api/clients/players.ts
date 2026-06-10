import { PlatformApiClient } from '../../platformApi';
import type { Guid, MoneyDto } from '../types';

export type PlayerSearchResultDto = Record<string, unknown>;
export type PlayerAccountDto = Record<string, unknown>;
export type WalletSummaryDto = Record<string, unknown>;
export type PlayerPackageDto = Record<string, unknown>;

export interface CreatePlayerAccountRequest extends Record<string, unknown> {
  organizationId: Guid;
}

export interface TopUpWalletRequest extends Record<string, unknown> {
  organizationId: Guid;
  amount: MoneyDto;
  idempotencyKey: string;
}

export interface PayDebtRequest extends Record<string, unknown> {
  organizationId: Guid;
  amount: MoneyDto;
  idempotencyKey: string;
}

export interface PurchasePackageRequest extends Record<string, unknown> {
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
