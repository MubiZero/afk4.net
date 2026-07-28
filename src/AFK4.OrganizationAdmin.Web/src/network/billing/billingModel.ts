import type { MessageKey } from '@afk4/i18n';

// Chip tone vocabulary matches .ui-chip--status's real modifiers in 02-ui-kit.css (see also
// stock/journalModel.ts's movementStatusTone) — is-booking is unused here, no billing status maps
// to it.
export type ChipTone = 'is-live' | 'is-neutral' | 'is-booking' | 'is-warning' | 'is-danger';

const SUB_STATUS_LABEL: Record<string, MessageKey> = {
  trial: 'op.network.billing.subStatus.trial',
  active: 'op.network.billing.subStatus.active',
  past_due: 'op.network.billing.subStatus.pastDue',
  cancelled: 'op.network.billing.subStatus.cancelled'
};

const SUB_STATUS_TONE: Record<string, ChipTone> = {
  trial: 'is-neutral',
  active: 'is-live',
  past_due: 'is-warning',
  cancelled: 'is-neutral'
};

const INV_STATUS_LABEL: Record<string, MessageKey> = {
  issued: 'op.network.billing.invStatus.issued',
  paid: 'op.network.billing.invStatus.paid',
  void: 'op.network.billing.invStatus.void',
  overdue: 'op.network.billing.invStatus.overdue'
};

const INV_STATUS_TONE: Record<string, ChipTone> = {
  issued: 'is-neutral',
  paid: 'is-live',
  void: 'is-neutral',
  overdue: 'is-danger'
};

export function subscriptionStatusLabelKey(status: string): MessageKey | null {
  return SUB_STATUS_LABEL[status] ?? null;
}

export function subscriptionStatusTone(status: string): ChipTone {
  return SUB_STATUS_TONE[status] ?? 'is-neutral';
}

export function invoiceStatusLabelKey(status: string): MessageKey | null {
  return INV_STATUS_LABEL[status] ?? null;
}

export function invoiceStatusTone(status: string): ChipTone {
  return INV_STATUS_TONE[status] ?? 'is-neutral';
}
