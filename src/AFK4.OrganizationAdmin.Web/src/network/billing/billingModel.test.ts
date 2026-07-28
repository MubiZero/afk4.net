import { describe, it, expect } from 'bun:test';
import { subscriptionStatusLabelKey, invoiceStatusLabelKey, subscriptionStatusTone, invoiceStatusTone } from './billingModel';

describe('billingModel', () => {
  it('maps known subscription statuses to label keys', () => {
    expect(subscriptionStatusLabelKey('active')).toBe('op.network.billing.subStatus.active');
    expect(subscriptionStatusLabelKey('unknown')).toBeNull();
  });

  it('maps invoice statuses to label keys', () => {
    expect(invoiceStatusLabelKey('paid')).toBe('op.network.billing.invStatus.paid');
    expect(invoiceStatusLabelKey('unknown')).toBeNull();
  });

  // Tones use the real .ui-chip--status modifiers from 02-ui-kit.css (is-live/is-neutral/
  // is-warning/is-danger) — same vocabulary as stock/journalModel.ts's movementStatusTone,
  // not the ok/warning/muted/danger placeholder names from the original brief.
  it('maps subscription status to a chip tone', () => {
    expect(subscriptionStatusTone('active')).toBe('is-live');
    expect(subscriptionStatusTone('past_due')).toBe('is-warning');
    expect(subscriptionStatusTone('trial')).toBe('is-neutral');
    expect(subscriptionStatusTone('cancelled')).toBe('is-neutral');
    expect(subscriptionStatusTone('unknown')).toBe('is-neutral');
  });

  it('maps invoice status to a chip tone', () => {
    expect(invoiceStatusTone('paid')).toBe('is-live');
    expect(invoiceStatusTone('overdue')).toBe('is-danger');
    expect(invoiceStatusTone('issued')).toBe('is-neutral');
    expect(invoiceStatusTone('void')).toBe('is-neutral');
  });
});
