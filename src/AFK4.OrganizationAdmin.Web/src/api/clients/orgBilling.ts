import { PlatformApiClient } from '../../platformApi';
import type { Guid } from '../types';

// OrganizationSubscriptionDto / InvoiceDto — AFK4.Shared.Contracts/Platform/Billing/*.cs. Both records
// carry a flat `AmountMinorUnits`/`CurrencyCode` pair over the wire (not a nested MoneyDto), so
// this DTO's field names match the backend record verbatim — unlike dashboard-summary's MoneyDto
// (see branchRollupModel.ts), there's no shape mismatch to normalize here.
export interface OrganizationSubscriptionDto {
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

// AFK4.Shared.Contracts/Platform/Billing/OrganizationBillingStatusDto.cs — compact arrears summary
// for the shell banner (BillingStatusBanner), deliberately smaller than the invoice list above so
// every page load doesn't pull the whole billing history just to know whether to warn the club.
export interface OrganizationBillingStatusDto {
  inArrears: boolean;
  outstandingMinorUnits: number;
  currencyCode: string;
  oldestOverdueInvoiceNumber: number | null;
  daysOverdue: number;
  graceUntilUtc: string | null;
}

// Read-only org billing screen (Сеть → Подписка) — no plan-management actions by design.
export function createOrgBillingClient(api: PlatformApiClient) {
  return {
    getSubscription(_organizationId: Guid): Promise<OrganizationSubscriptionDto> {
      return api.get<OrganizationSubscriptionDto>('subscription');
    },
    listInvoices(_organizationId: Guid): Promise<InvoiceDto[]> {
      return api.get<InvoiceDto[]>('invoices');
    },
    getBillingStatus(_organizationId: Guid): Promise<OrganizationBillingStatusDto> {
      return api.get<OrganizationBillingStatusDto>('billing/status');
    }
  };
}
