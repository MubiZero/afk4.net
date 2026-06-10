import type { ApiTransport } from '../apiTransport';
import type {
  CreatePlayerAccountRequest,
  LedgerEntry,
  ManualLedgerCorrectionRequest,
  PayDebtRequest,
  PlayerAccount,
  PlayerPackage,
  PlayerSearchResult,
  PurchasePackageRequest,
  RefundLedgerEntryRequest,
  TopUpWalletRequest,
  WalletSummary
} from '../types';

export class PlayersApi {
  public constructor(private readonly transport: ApiTransport) {}

  public searchPlayers(branchId: string, query: string, limit = 20): Promise<PlayerSearchResult[]> {
    const qs = new URLSearchParams({ query, limit: String(limit) });
    return this.transport.send<PlayerSearchResult[]>(
      'GET',
      `/api/branches/${encodeURIComponent(branchId)}/players?${qs.toString()}`
    );
  }

  public createPlayer(branchId: string, request: CreatePlayerAccountRequest): Promise<PlayerAccount> {
    return this.transport.send<PlayerAccount>('POST', `/api/branches/${encodeURIComponent(branchId)}/players`, request);
  }

  public getWalletSummary(playerAccountId: string): Promise<WalletSummary> {
    return this.transport.send<WalletSummary>('GET', `/api/players/${encodeURIComponent(playerAccountId)}/wallet-summary`);
  }

  public getPlayerPackages(playerAccountId: string): Promise<PlayerPackage[]> {
    return this.transport.send<PlayerPackage[]>('GET', `/api/players/${encodeURIComponent(playerAccountId)}/packages`);
  }

  public topUpWallet(playerAccountId: string, request: TopUpWalletRequest): Promise<LedgerEntry> {
    return this.transport.send<LedgerEntry>('POST', `/api/players/${encodeURIComponent(playerAccountId)}/wallet/top-ups`, request);
  }

  public payDebt(playerAccountId: string, request: PayDebtRequest): Promise<LedgerEntry> {
    return this.transport.send<LedgerEntry>('POST', `/api/players/${encodeURIComponent(playerAccountId)}/debts/payments`, request);
  }

  public createManualCorrection(playerAccountId: string, request: ManualLedgerCorrectionRequest): Promise<LedgerEntry> {
    return this.transport.send<LedgerEntry>('POST', `/api/players/${encodeURIComponent(playerAccountId)}/ledger/manual-corrections`, request);
  }

  public refundLedgerEntry(playerAccountId: string, ledgerEntryId: string, request: RefundLedgerEntryRequest): Promise<LedgerEntry> {
    return this.transport.send<LedgerEntry>(
      'POST',
      `/api/players/${encodeURIComponent(playerAccountId)}/ledger/${encodeURIComponent(ledgerEntryId)}/refunds`,
      request
    );
  }

  public purchasePackage(playerAccountId: string, request: PurchasePackageRequest): Promise<PlayerPackage> {
    return this.transport.send<PlayerPackage>('POST', `/api/players/${encodeURIComponent(playerAccountId)}/packages/purchases`, request);
  }
}
