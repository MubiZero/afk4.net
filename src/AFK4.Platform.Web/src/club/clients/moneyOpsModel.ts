import type {
  ManualLedgerCorrectionRequest, RefundLedgerEntryRequest, TopUpWalletRequest
} from '@/api/types';
import { majorToMinor } from '../money';

/** Shared shape of TopUpWalletRequest and PayDebtRequest (structurally identical). */
export function buildAmountReasonRequest(
  organizationId: string,
  currencyCode: string,
  amountMajor: number,
  reason: string,
  idempotencyKey: string
): TopUpWalletRequest {
  return {
    organizationId,
    amount: { currencyCode, minorUnits: majorToMinor(amountMajor) },
    reason: reason.trim(),
    idempotencyKey
  };
}

export function buildManualCorrectionRequest(
  organizationId: string,
  accountType: string,
  currencyCode: string,
  amountMajor: number,
  minutes: number,
  reason: string,
  idempotencyKey: string
): ManualLedgerCorrectionRequest {
  return {
    organizationId,
    accountType,
    amount: { currencyCode, minorUnits: majorToMinor(amountMajor) },
    quantitySeconds: Math.round(minutes * 60),
    reason: reason.trim(),
    idempotencyKey
  };
}

export function buildRefundRequest(
  organizationId: string,
  ledgerEntryId: string,
  currencyCode: string,
  amountMajor: number,
  reason: string,
  idempotencyKey: string
): RefundLedgerEntryRequest {
  return {
    organizationId,
    ledgerEntryId,
    amount: { currencyCode, minorUnits: majorToMinor(amountMajor) },
    reason: reason.trim(),
    idempotencyKey
  };
}
