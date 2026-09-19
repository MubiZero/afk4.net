import type { PlatformTransport } from '../platformTransport';
import type { Invoice, InvoiceListItem } from '../types';

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

  /**
   * Счёт вне подписки: разовая услуга или кредит-нота. Сумма кредит-ноты отрицательная —
   * этим она и уменьшает долг; сервер отказывает, если знак не совпал с видом счёта.
   */
  public createInvoice(
    organizationId: string,
    request: { kind: string; amountMinorUnits: number; description: string; dueAtUtc: string | null },
    attemptKey?: string
  ): Promise<Invoice> {
    return this.transport.sendIdempotent<Invoice>('POST', `/api/platform/organizations/${organizationId}/invoices`, request, attemptKey);
  }

  public generateInvoice(organizationId: string, attemptKey?: string): Promise<Invoice> {
    return this.transport.sendIdempotent<Invoice>('POST', `/api/platform/organizations/${organizationId}/invoices/generate`, undefined, attemptKey);
  }

  public markInvoicePaid(invoiceId: string, reference: string | null, attemptKey?: string): Promise<Invoice> {
    return this.transport.sendIdempotent<Invoice>('POST', `/api/platform/invoices/${invoiceId}/mark-paid`, { reference }, attemptKey);
  }

  public voidInvoice(invoiceId: string, reason: string, attemptKey?: string): Promise<Invoice> {
    return this.transport.sendIdempotent<Invoice>('POST', `/api/platform/invoices/${invoiceId}/void`, { reason }, attemptKey);
  }
}
