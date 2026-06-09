import { describe, expect, it } from 'bun:test';
import { toPaymentStatus } from './paymentStatus';

const base = {
  paymentIntentId: 'p', amountMinorUnits: 5000, currencyCode: 'TJS', purpose: 'wallet_topup',
  method: 'dcgate', createdAtUtc: '', fulfilledAtUtc: null, payUrl: 'pay.dc.tj/x', comment: '123', gatewayExpiresAtUtc: null
};

describe('toPaymentStatus', () => {
  it('pending while awaiting confirmation', () => {
    expect(toPaymentStatus({ ...base, state: 'pending', isExpired: false })).toBe('pending');
  });
  it('fulfilled when server confirms', () => {
    expect(toPaymentStatus({ ...base, state: 'fulfilled', isExpired: false })).toBe('fulfilled');
  });
  it('expired by state', () => {
    expect(toPaymentStatus({ ...base, state: 'expired', isExpired: false })).toBe('expired');
  });
  it('expired by isExpired flag even if still pending', () => {
    expect(toPaymentStatus({ ...base, state: 'pending', isExpired: true })).toBe('expired');
  });
});
