import { useRef } from 'react';
import type { InvoiceDto, OrganizationSubscriptionDto } from '../../api/clients/orgBilling';
import { useSection, type Section } from '../useSection';

export interface BillingClient {
  getSubscription(organizationId: string): Promise<OrganizationSubscriptionDto>;
  listInvoices(organizationId: string): Promise<InvoiceDto[]>;
}

export interface BillingState {
  subscription: Section<OrganizationSubscriptionDto>;
  invoices: Section<InvoiceDto[]>;
}

// Подписка и счета — два независимых запроса и две панели экрана. Раньше они грузились одним
// ожиданием, и отказ списка счетов стирал с экрана тариф и статус подписки, которые уже пришли.
// Теперь каждая панель живёт своей загрузкой, а повтор перезапрашивает только то, что не пришло.
export function useBilling(client: BillingClient, organizationId: string): BillingState {
  const clientRef = useRef(client);
  clientRef.current = client;
  const subscription = useSection(() => clientRef.current.getSubscription(organizationId), organizationId);
  const invoices = useSection(() => clientRef.current.listInvoices(organizationId), organizationId);
  return { subscription, invoices };
}
