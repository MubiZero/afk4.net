import type { PlatformTransport } from '../platformTransport';
import type { Invoice, InvoiceListItem, PlatformBillingMetrics } from '../types';

export class InvoicesApi {
  public constructor(private readonly transport: PlatformTransport) {}

  public listOrganizationInvoices(organizationId: string, status?: string): Promise<Invoice[]> {
    const query = status !== undefined && status.length > 0 ? `?status=${encodeURIComponent(status)}` : '';
    return this.transport.send<Invoice[]>('GET', `/api/platform/organizations/${organizationId}/invoices${query}`);
  }

  public listInvoices(status?: string): Promise<InvoiceListItem[]> {
    const query = status !== undefined && status.length > 0 ? `?status=${encodeURIComponent(status)}` : '';
    return this.transport.send<InvoiceListItem[]>('GET', `/api/platform/invoices${query}`);
  }

  public getBillingMetrics(): Promise<PlatformBillingMetrics> {
    return this.transport.send<PlatformBillingMetrics>('GET', '/api/platform/metrics');
  }

  /**
   * Счёт вне подписки: разовая услуга или кредит-нота. Сумма кредит-ноты отрицательная —
   * этим она и уменьшает долг; сервер отказывает, если знак не совпал с видом счёта.
   */
  public createInvoice(
    organizationId: string,
    request: { kind: string; amountMinorUnits: number; description: string; dueAtUtc: string | null }
  ): Promise<Invoice> {
    return this.transport.sendIdempotent<Invoice>('POST', `/api/platform/organizations/${organizationId}/invoices`, request);
  }

  public generateInvoice(organizationId: string): Promise<Invoice> {
    return this.transport.sendIdempotent<Invoice>('POST', `/api/platform/organizations/${organizationId}/invoices/generate`, undefined);
  }

  public markInvoicePaid(invoiceId: string, reference: string | null): Promise<Invoice> {
    return this.transport.sendIdempotent<Invoice>('POST', `/api/platform/invoices/${invoiceId}/mark-paid`, { reference });
  }

  public voidInvoice(invoiceId: string, reason: string): Promise<Invoice> {
    return this.transport.sendIdempotent<Invoice>('POST', `/api/platform/invoices/${invoiceId}/void`, { reason });
  }
}
