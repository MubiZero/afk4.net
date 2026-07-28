import { PlatformApiClient } from '../../platformApi';
import type { Guid } from '../types';

// TenantSubscriptionDto / InvoiceDto — AFK4.Shared.Contracts/Platform/Billing/*.cs. Both records
// carry a flat `AmountMinorUnits`/`CurrencyCode` pair over the wire (not a nested MoneyDto), so
// this DTO's field names match the backend record verbatim — unlike dashboard-summary's MoneyDto
// (see branchRollupModel.ts), there's no shape mismatch to normalize here.
export interface TenantSubscriptionDto {
  planCode: string;
  status: string;
  currentPeriodStartUtc: string;
  currentPeriodEndUtc: string;
  nextInvoiceUtc: string | null;
  amountMinorUnits: number;
  currencyCode: string;
  cancelAtPeriodEnd: boolean;
}

export interface InvoiceDto {
  invoiceId: string;
  number: number;
  issuedAtUtc: string;
  dueAtUtc: string;
  amountMinorUnits: number;
  currencyCode: string;
  status: string;
}

// Read-only org billing screen (Сеть → Подписка) — no plan-management actions by design.
export function createOrgBillingClient(api: PlatformApiClient) {
  return {
    getSubscription(organizationId: Guid): Promise<TenantSubscriptionDto> {
      return api.get<TenantSubscriptionDto>('subscription');
    },
    listInvoices(organizationId: Guid): Promise<InvoiceDto[]> {
      return api.get<InvoiceDto[]>('invoices');
    }
  };
}
