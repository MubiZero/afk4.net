// Hand-mirrored from AFK4.Shared.Contracts; no codegen exists — keep in sync.
// Property names are camelCase (JsonSerializerDefaults.Web); string VALUES mirror the wire.

export interface MoneyDto { currencyCode: string; minorUnits: number; }

export interface TariffOptionDto {
  tariffId: string; tariffVersionId: string; name: string; tariffRuleVersionId: string;
  versionNumber: number; currencyCode: string; pricePerMinuteMinorUnits: number;
  minimumBillableMinutes: number; roundingIncrementMinutes: number; effectiveFromUtc: string;
}

export interface PackageOptionDto {
  packageDefinitionId: string; name: string; currencyCode: string; priceMinorUnits: number;
  includedSeconds: number; bonusSeconds: number; expiresAfterDays: number;
}

export interface PlayerTopUpIntentDto {
  paymentIntentId: string; amountMinorUnits: number; currencyCode: string; state: string;
  purpose: string; method: string; createdAtUtc: string; fulfilledAtUtc: string | null;
  isExpired: boolean; payUrl: string | null; comment: string | null; gatewayExpiresAtUtc: string | null;
}

export interface ExtendSessionRequest {
  additionalMinutes: number; tariffRuleVersionId: string; idempotencyKey: string;
}

export interface ShopCatalogItemDto {
  productId: string; name: string; sku: string; price: MoneyDto; stockOnHand: number;
}

export interface ShopOrderLineDto {
  productId: string; name: string; unitPrice: MoneyDto; quantity: number; lineTotal: MoneyDto;
}

export interface ShopOrderDto {
  id: string; branchId: string; seatId: string; playerAccountId: string; playerDisplayName: string;
  status: string; total: MoneyDto; lines: ShopOrderLineDto[];
  placedAtUtc: string; acceptedAtUtc: string | null; deliveredAtUtc: string | null;
  cancelledAtUtc: string | null; version: number;
}

export interface ShopOrderLineInput { productId: string; quantity: number; }

export interface CashbackEntryDto {
  amountMinorUnits: number;
  currencyCode: string;
  reason: string;
  createdAtUtc: string;
}

export interface PlayerLoyaltyDto {
  topUpEnabled: boolean;
  topUpPercentBasisPoints: number;
  shopEnabled: boolean;
  shopPercentBasisPoints: number;
  totalEarned: MoneyDto;
  recent: CashbackEntryDto[];
}
