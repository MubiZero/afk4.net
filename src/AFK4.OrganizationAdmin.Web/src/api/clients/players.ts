import { PlatformApiClient } from '../../platformApi';
import type { Guid } from '../types';
import type { GuestImportRequest, GuestImportResultDto } from '@afk4/contracts';
import type {
  CreatePlayerAccountRequest,
  LedgerEntryDto,
  ManualLedgerCorrectionRequest,
  PayDebtRequest,
  PlayerAccountDto,
  PlayerPackageDto,
  PlayerReputationDto,
  PlayerReputationLookupRequest,
  PlayerSearchResultDto,
  PurchasePackageRequest,
  RefundLedgerEntryRequest,
  SetPlayerActiveStateRequest,
  TopUpWalletRequest,
  UpdatePlayerAccountRequest,
  WalletSummaryDto,
} from '@afk4/contracts';
export type {
  CreatePlayerAccountRequest,
  LedgerEntryDto,
  ManualLedgerCorrectionRequest,
  PayDebtRequest,
  PlayerAccountDto,
  PlayerPackageDto,
  PlayerReputationDto,
  PlayerReputationLookupRequest,
  PlayerSearchResultDto,
  PurchasePackageRequest,
  RefundLedgerEntryRequest,
  SetPlayerActiveStateRequest,
  TopUpWalletRequest,
  UpdatePlayerAccountRequest,
  WalletSummaryDto,
} from '@afk4/contracts';

// Зеркало AFK4.Shared.Contracts.Common.CursorPage<T> (camelCase): страница + курсор следующей
// страницы (null = больше нет).
export interface CursorPageDto<T> {
  items: T[];
  nextCursor: string | null;
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
    // Перенос гостей из прежней программы: пробный прогон (dryRun) и перенос одной ручкой.
    importGuests(branchId: Guid, request: GuestImportRequest): Promise<GuestImportResultDto> {
      return api.post<GuestImportResultDto, GuestImportRequest>(`branches/${branchId}/players/import`, request);
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
    },
    // Репутация человека по сети: спрос по точному номеру. Номер едет телом, а не в адресе —
    // адреса оседают в логах прокси, а это чужой телефон. Маршрут пишется в аудит на сам факт
    // чтения и стоит под лимитом 20/мин на сотрудника, поэтому зовём его только из карточки —
    // там, где администратор принимает решение, — и никогда построчно из списка.
    lookupReputation(branchId: Guid, phoneNumber: string): Promise<PlayerReputationDto> {
      return api.post<PlayerReputationDto, PlayerReputationLookupRequest>(
        `branches/${branchId}/players/reputation/lookup`,
        { phoneNumber }
      );
    },
    // То же самое про человека, у карточки которого номера нет: спрашиваем по личности
    // платформы, которую проекция клиента теперь называет. Без этого клиент без номера
    // оставался единственным, про кого сеть молчала, — а это ровно тот случай, когда стойке
    // важнее всего знать, чем человек кончил в соседнем клубе.
    reputationForPerson(branchId: Guid, platformPersonId: Guid): Promise<PlayerReputationDto> {
      return api.get<PlayerReputationDto>(
        `branches/${branchId}/players/reputation/${platformPersonId}`
      );
    }
  };
}
