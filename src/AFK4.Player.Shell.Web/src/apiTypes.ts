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
