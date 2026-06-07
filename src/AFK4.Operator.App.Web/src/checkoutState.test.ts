import { describe, expect, it } from 'bun:test';
import { createTranslator } from '@afk4/i18n';
import {
  buildCheckoutPayments,
  formatBilledDuration,
  initialCheckoutDrafts,
  parseCheckoutAmount,
  validateCheckoutPayments,
  type CheckoutPaymentDraft
} from './checkoutState';

const t = createTranslator('ru');

describe('parseCheckoutAmount', () => {
  it('parses major units to minor units and accepts a comma decimal', () => {
    expect(parseCheckoutAmount('22.50')).toBe(2250);
    expect(parseCheckoutAmount('22,50')).toBe(2250);
    expect(parseCheckoutAmount(' 5 ')).toBe(500);
  });

  it('rejects blank or malformed input', () => {
    expect(parseCheckoutAmount('')).toBeNull();
    expect(parseCheckoutAmount('abc')).toBeNull();
    expect(parseCheckoutAmount('1.234')).toBeNull();
  });
});

describe('formatBilledDuration', () => {
  it('formats minutes-only and hours+minutes', () => {
    expect(formatBilledDuration(2700, t)).toBe('45м');
    expect(formatBilledDuration(5400, t)).toBe('1ч 30м');
    expect(formatBilledDuration(3600, t)).toBe('1ч 0м');
  });
});

describe('validateCheckoutPayments', () => {
  it('accepts a single cash part equal to the grand total', () => {
    const result = validateCheckoutPayments([{ method: 'cash', amountText: '22.50' }], 2250, null, t);
    expect(result.canSubmit).toBe(true);
    expect(result.error).toBeNull();
    expect(result.remainingMinorUnits).toBe(0);
  });

  it('reports the shortfall when payments under-cover the bill', () => {
    const result = validateCheckoutPayments([{ method: 'cash', amountText: '10.00' }], 2250, null, t);
    expect(result.canSubmit).toBe(false);
    expect(result.remainingMinorUnits).toBe(1250);
    expect(result.error).toContain('Не хватает');
  });

  it('reports overpayment', () => {
    const result = validateCheckoutPayments([{ method: 'cash', amountText: '30.00' }], 2250, null, t);
    expect(result.canSubmit).toBe(false);
    expect(result.error).toContain('Превышение');
  });

  it('accepts a split of wallet + cash within the wallet balance', () => {
    const drafts: CheckoutPaymentDraft[] = [
      { method: 'wallet', amountText: '20.00' },
      { method: 'cash', amountText: '2.50' }
    ];
    const result = validateCheckoutPayments(drafts, 2250, 5000, t);
    expect(result.canSubmit).toBe(true);
    expect(result.walletMinorUnits).toBe(2000);
  });

  it('rejects a wallet part that exceeds the wallet balance', () => {
    const result = validateCheckoutPayments([{ method: 'wallet', amountText: '22.50' }], 2250, 1000, t);
    expect(result.canSubmit).toBe(false);
    expect(result.walletWithinBalance).toBe(false);
    expect(result.error).toContain('депозита');
  });

  it('flags a malformed amount', () => {
    const result = validateCheckoutPayments([{ method: 'cash', amountText: 'xx' }], 2250, null, t);
    expect(result.hasInvalidAmount).toBe(true);
    expect(result.canSubmit).toBe(false);
  });

  it('settles a zero-total bill with no payment rows', () => {
    const result = validateCheckoutPayments([{ method: 'cash', amountText: '' }], 0, null, t);
    expect(result.canSubmit).toBe(true);
    expect(result.paidMinorUnits).toBe(0);
  });
});

describe('buildCheckoutPayments', () => {
  it('builds parts in minor units and drops blank/zero rows', () => {
    const drafts: CheckoutPaymentDraft[] = [
      { method: 'wallet', amountText: '20.00' },
      { method: 'cash', amountText: '' },
      { method: 'card_manual', amountText: '2.50' }
    ];
    const payments = buildCheckoutPayments(drafts, 'TJS');
    expect(payments).toEqual([
      { paymentMethod: 'wallet', amount: { currencyCode: 'TJS', minorUnits: 2000 } },
      { paymentMethod: 'card_manual', amount: { currencyCode: 'TJS', minorUnits: 250 } }
    ]);
  });
});

describe('initialCheckoutDrafts', () => {
  it('pre-fills one cash row with the whole grand total', () => {
    expect(initialCheckoutDrafts(2250)).toEqual([{ method: 'cash', amountText: '22.50' }]);
  });

  it('leaves the row blank for a zero bill', () => {
    expect(initialCheckoutDrafts(0)).toEqual([{ method: 'cash', amountText: '' }]);
  });
});
