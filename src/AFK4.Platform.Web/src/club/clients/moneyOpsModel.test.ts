import { it, expect } from 'vitest';
import { buildAmountReasonRequest, buildManualCorrectionRequest, buildRefundRequest } from './moneyOpsModel';

it('builds an amount+reason request (top-up / pay-debt shape): major to minor, trims reason', () => {
  expect(buildAmountReasonRequest('org', 'TJS', 50, '  касса  ', 'idem')).toEqual({
    organizationId: 'org', amount: { currencyCode: 'TJS', minorUnits: 5000 }, reason: 'касса', idempotencyKey: 'idem'
  });
});

it('builds a money-account correction: amount to minor, zero seconds', () => {
  expect(buildManualCorrectionRequest('org', 'wallet', 'TJS', -5, 0, 'правка', 'idem')).toEqual({
    organizationId: 'org', accountType: 'wallet', amount: { currencyCode: 'TJS', minorUnits: -500 },
    quantitySeconds: 0, reason: 'правка', idempotencyKey: 'idem'
  });
});

it('builds a time-account correction: minutes to seconds, zero amount', () => {
  expect(buildManualCorrectionRequest('org', 'package_time', 'TJS', 0, 30, 'бонус', 'idem')).toEqual({
    organizationId: 'org', accountType: 'package_time', amount: { currencyCode: 'TJS', minorUnits: 0 },
    quantitySeconds: 1800, reason: 'бонус', idempotencyKey: 'idem'
  });
});

it('builds a refund request for a ledger entry', () => {
  expect(buildRefundRequest('org', 'l1', 'TJS', 50, 'возврат', 'idem')).toEqual({
    organizationId: 'org', ledgerEntryId: 'l1', amount: { currencyCode: 'TJS', minorUnits: 5000 },
    reason: 'возврат', idempotencyKey: 'idem'
  });
});
