import { describe, expect, it } from 'vitest';
import {
  filterInvoices,
  filterSubscriptions,
  validatePlanForm,
  INVOICE_STATUS_VARIANT,
  SUBSCRIPTION_STATUS_VARIANT,
  emptyPlanForm,
  planFormToCreateRequest
} from './billingModel';
import type { InvoiceListItem, SubscriptionListItem } from '@/api/types';

function sub(p: Partial<SubscriptionListItem>): SubscriptionListItem {
  return {
    tenantSubscriptionId: 't', organizationId: 'o', organizationName: 'Acme', organizationSlug: 'acme',
    planCode: 'starter', status: 'active', billingInterval: 'monthly', amountMinorUnits: 290000,
    currencyCode: 'RUB', currentPeriodEndUtc: '2026-06-30T00:00:00Z', nextInvoiceUtc: null,
    cancelAtPeriodEnd: false, ...p
  };
}

function inv(p: Partial<InvoiceListItem>): InvoiceListItem {
  return {
    invoiceId: 'i', organizationId: 'o', organizationName: 'Acme', organizationSlug: 'acme',
    number: 1, kind: 'subscription', issuedAtUtc: '2026-05-01T00:00:00Z', dueAtUtc: '2026-05-08T00:00:00Z',
    amountMinorUnits: 290000, currencyCode: 'RUB', status: 'issued', ...p
  };
}

describe('filterSubscriptions', () => {
  it('filters by status and query (name/slug)', () => {
    const rows = [
      sub({ organizationSlug: 'acme', organizationName: 'Acme', status: 'active' }),
      sub({ organizationSlug: 'beta', organizationName: 'Beta', status: 'cancelled' })
    ];
    expect(filterSubscriptions(rows, { query: '', status: 'all' })).toHaveLength(2);
    expect(filterSubscriptions(rows, { query: '', status: 'cancelled' })).toHaveLength(1);
    expect(filterSubscriptions(rows, { query: 'acm', status: 'all' })).toHaveLength(1);
  });
});

describe('filterInvoices', () => {
  it('filters by status and query', () => {
    const rows = [
      inv({ organizationSlug: 'acme', status: 'issued' }),
      inv({ organizationSlug: 'beta', status: 'paid' })
    ];
    expect(filterInvoices(rows, { query: '', status: 'all' })).toHaveLength(2);
    expect(filterInvoices(rows, { query: '', status: 'paid' })).toHaveLength(1);
    expect(filterInvoices(rows, { query: 'bet', status: 'all' })).toHaveLength(1);
  });
});

describe('status variant maps', () => {
  it('maps each known status to a badge variant', () => {
    expect(INVOICE_STATUS_VARIANT.paid).toBeDefined();
    expect(INVOICE_STATUS_VARIANT.overdue).toBe('destructive');
    expect(SUBSCRIPTION_STATUS_VARIANT.active).toBeDefined();
  });
});

describe('validatePlanForm', () => {
  it('rejects blank code/name and negative price', () => {
    expect(validatePlanForm({ ...emptyPlanForm(), planCode: '', name: 'X' })).toBe(false);
    expect(validatePlanForm({ ...emptyPlanForm(), planCode: 'x', name: '' })).toBe(false);
    expect(validatePlanForm({ ...emptyPlanForm(), planCode: 'x', name: 'X', priceMinorUnits: -1 })).toBe(false);
    expect(validatePlanForm({ ...emptyPlanForm(), planCode: 'x', name: 'X', priceMinorUnits: 0 })).toBe(true);
  });

  it('converts a form to a create request', () => {
    const req = planFormToCreateRequest({ ...emptyPlanForm(), planCode: 'pro', name: 'Pro', priceMinorUnits: 100 });
    expect(req.planCode).toBe('pro');
    expect(req.priceMinorUnits).toBe(100);
    expect(req.billingInterval).toBe('monthly');
  });
});
