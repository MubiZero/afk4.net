import type { MessageKey } from '../../i18n/messages';
import type { Invoice } from '../../api/types';
import type { BadgeVariant as BadgeVariantBase } from '../../components/ui/badge';

// We use a subset of the real Badge variant union that semantically fits billing statuses.
// The Badge component has: default | secondary | success | destructive | outline | ghost | link
// Mapping: warning → secondary, danger → destructive, muted → outline
export type BadgeVariant = Extract<BadgeVariantBase, 'success' | 'secondary' | 'destructive' | 'outline'>;

const SUB_STATUS_LABEL: Record<string, MessageKey> = {
  trial: 'club.billing.subStatus.trial',
  active: 'club.billing.subStatus.active',
  past_due: 'club.billing.subStatus.pastDue',
  cancelled: 'club.billing.subStatus.cancelled'
};

const SUB_STATUS_VARIANT: Record<string, BadgeVariant> = {
  trial: 'secondary',
  active: 'success',
  past_due: 'secondary',
  cancelled: 'outline'
};

const INV_STATUS_LABEL: Record<string, MessageKey> = {
  issued: 'club.billing.invStatus.issued',
  paid: 'club.billing.invStatus.paid',
  void: 'club.billing.invStatus.void',
  overdue: 'club.billing.invStatus.overdue'
};

const INV_STATUS_VARIANT: Record<string, BadgeVariant> = {
  issued: 'secondary',
  paid: 'success',
  void: 'outline',
  overdue: 'destructive'
};

export function subscriptionStatusLabelKey(status: string): MessageKey | null {
  return SUB_STATUS_LABEL[status] ?? null;
}

export function subscriptionStatusVariant(status: string): BadgeVariant {
  return SUB_STATUS_VARIANT[status] ?? 'outline';
}

export function invoiceStatusLabelKey(status: string): MessageKey | null {
  return INV_STATUS_LABEL[status] ?? null;
}

export function invoiceStatusVariant(status: string): BadgeVariant {
  return INV_STATUS_VARIANT[status] ?? 'outline';
}

export interface InvoiceRow {
  invoiceId: string;
  number: number;
  kind: string;
  issuedAtUtc: string;
  dueAtUtc: string;
  amountMinorUnits: number;
  currencyCode: string;
  status: string;
}

export function buildInvoiceRows(invoices: Invoice[]): InvoiceRow[] {
  return invoices.map(i => ({
    invoiceId: i.invoiceId,
    number: i.number,
    kind: i.kind,
    issuedAtUtc: i.issuedAtUtc,
    dueAtUtc: i.dueAtUtc,
    amountMinorUnits: i.amountMinorUnits,
    currencyCode: i.currencyCode,
    status: i.status
  }));
}
