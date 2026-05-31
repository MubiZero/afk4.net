import { describe, it, expect } from 'vitest';
import {
  subscriptionStatusLabelKey,
  subscriptionStatusVariant,
  invoiceStatusLabelKey,
  invoiceStatusVariant,
  buildInvoiceRows
} from './billingModel';
import type { Invoice } from '../../api/types';

describe('billingModel', () => {
  it('maps known subscription statuses to label keys and variants', () => {
    expect(subscriptionStatusLabelKey('active')).toBe('club.billing.subStatus.active');
    expect(subscriptionStatusVariant('past_due')).toBe('secondary');
    expect(subscriptionStatusVariant('cancelled')).toBe('outline');
  });

  it('falls back to null/outline for unknown subscription statuses', () => {
    expect(subscriptionStatusLabelKey('weird')).toBeNull();
    expect(subscriptionStatusVariant('weird')).toBe('outline');
  });

  it('maps invoice statuses', () => {
    expect(invoiceStatusLabelKey('paid')).toBe('club.billing.invStatus.paid');
    expect(invoiceStatusVariant('overdue')).toBe('destructive');
  });

  it('builds invoice rows newest amount intact (minor units preserved)', () => {
    const invoices: Invoice[] = [
      {
        invoiceId: 'i1', organizationId: 'o1', number: 12, kind: 'subscription',
        periodStartUtc: '2026-04-01T00:00:00Z', periodEndUtc: '2026-05-01T00:00:00Z',
        issuedAtUtc: '2026-05-01T00:00:00Z', dueAtUtc: '2026-05-08T00:00:00Z',
        amountMinorUnits: 290000, currencyCode: 'RUB', status: 'issued',
        paidAtUtc: null, voidedAtUtc: null, voidReason: null, description: 'x'
      }
    ];
    const rows = buildInvoiceRows(invoices);
    expect(rows).toHaveLength(1);
    expect(rows[0].number).toBe(12);
    expect(rows[0].amountMinorUnits).toBe(290000);
    expect(rows[0].currencyCode).toBe('RUB');
  });
});
