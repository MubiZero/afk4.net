import type { ApiTransport } from '../apiTransport';
import type { Invoice, TenantSubscription } from '../types';

export class BillingApi {
  public constructor(private readonly transport: ApiTransport) {}

  public getSubscription(organizationId: string): Promise<TenantSubscription> {
    return this.transport.send<TenantSubscription>('GET', `/api/organizations/${encodeURIComponent(organizationId)}/subscription`);
  }

  public listInvoices(organizationId: string): Promise<Invoice[]> {
    return this.transport.send<Invoice[]>('GET', `/api/organizations/${encodeURIComponent(organizationId)}/invoices`);
  }
}
