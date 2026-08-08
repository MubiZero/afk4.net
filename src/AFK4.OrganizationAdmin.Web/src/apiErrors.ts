import type { MessageKey } from '@afk4/i18n';
import { PlatformApiError } from './platformApi';

type TFn = (key: MessageKey, values?: Record<string, string | number>) => string;

export interface OperatorErrorProjection {
  title: string;
  detail: string;
}

const codeMessageKeys = {
  open_shift_required: 'op.error.code.openShiftRequired',
  invalid_payment_split: 'op.error.code.invalidPaymentSplit',
  mixed_currency: 'op.error.code.mixedCurrency',
  insufficient_funds: 'op.error.code.insufficientFunds',
  player_required_for_wallet: 'op.error.code.playerRequiredForWallet',
  out_of_stock: 'op.error.code.outOfStock',
  organization_mismatch: 'op.error.code.organizationMismatch',
  reservation_not_found: 'op.error.code.reservationNotFound',
  reservation_confirmation_required: 'op.error.code.reservationConfirmationRequired',
  reservation_already_started: 'op.error.code.reservationAlreadyStarted',
  reservation_expired: 'op.error.code.reservationExpired',
  seat_unavailable: 'op.error.code.seatUnavailable',
  version_conflict: 'op.error.code.versionConflict',
  idempotency_key_required: 'op.error.code.idempotencyKeyRequired',
  idempotency_conflict: 'op.error.code.idempotencyConflict',
  session_start_invalid: 'op.error.code.sessionStartInvalid',
  session_start_conflict: 'op.error.code.sessionStartConflict',
  plan_limit_reached: 'op.error.code.planLimitReached'
} as const satisfies Record<string, MessageKey>;

interface PlanLimitBody extends Record<string, number> {
  limit: number;
  current: number;
}

function readPlanLimit(body: string): PlanLimitBody | null {
  try {
    const parsed = JSON.parse(body) as { planLimit?: { limit?: unknown; current?: unknown } };
    const limit = parsed.planLimit?.limit;
    const current = parsed.planLimit?.current;
    return typeof limit === 'number' && typeof current === 'number' ? { limit, current } : null;
  } catch {
    return null;
  }
}

export function projectOperatorError(error: unknown, t: TFn): OperatorErrorProjection {
  const title = t('op.error.actionFailed.title');

  if (error instanceof PlatformApiError) {
    const code = readErrorCode(error.body);
    if (code !== null && code in codeMessageKeys) {
      const planLimit = code === 'plan_limit_reached' ? readPlanLimit(error.body) : null;
      return {
        title,
        detail: t(codeMessageKeys[code as keyof typeof codeMessageKeys], planLimit ?? undefined)
      };
    }
  }

  if (error instanceof Error && error.message.trim().length > 0) {
    return { title, detail: error.message };
  }

  if (typeof error === 'string' && error.trim().length > 0) {
    return { title, detail: error };
  }

  return { title, detail: t('op.error.actionFailed.noDetail') };
}

function readErrorCode(body: string): string | null {
  try {
    const parsed = JSON.parse(body) as { code?: unknown };
    return typeof parsed.code === 'string' ? parsed.code : null;
  } catch {
    return null;
  }
}
