import { PlatformApiClient } from '../../platformApi';
import type { Guid } from '../types';
import type {
  InvoiceDto,
  OrganizationBillingStatusDto,
  OrganizationSubscriptionDto,
} from '@afk4/contracts';
export type {
  InvoiceDto,
  OrganizationBillingStatusDto,
  OrganizationSubscriptionDto,
} from '@afk4/contracts';

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
