import { majorToMinor, minorToMajor } from '@afk4/money';
import { type MessageKey } from '@afk4/i18n';
import type { PaymentPartDto } from './operatorApiClients';

type TFn = (key: MessageKey, values?: Record<string, string | number>) => string;

/** The split-payment methods a unified checkout accepts. Mirrors the backend
 * `PaymentMethodNames` (cash, card_manual, wallet). */
export type CheckoutMethod = 'cash' | 'card_manual' | 'wallet';

export const checkoutMethods: readonly CheckoutMethod[] = ['cash', 'card_manual', 'wallet'];

export function checkoutMethodLabel(method: CheckoutMethod, t: TFn): string {
  switch (method) {
    case 'cash':
      return t('op.checkout.method.cash');
    case 'card_manual':
      return t('op.checkout.method.card');
    case 'wallet':
      return t('op.checkout.method.wallet');
  }
}

/** One editable payment row in the checkout dialog. `amountText` is the raw
 * major-unit string the operator typed; it is parsed to minor units on submit. */
export interface CheckoutPaymentDraft {
  method: CheckoutMethod;
  amountText: string;
}

/** Payment methods not already claimed by another split row. When rowIndex is
 * supplied its current method remains selectable, so editing a row never
 * invalidates its controlled value. */
export function getAvailableCheckoutMethods(
  drafts: readonly CheckoutPaymentDraft[],
  rowIndex?: number
): CheckoutMethod[] {
  const used = new Set(
    drafts
      .filter((_, index) => index !== rowIndex)
      .map((draft) => draft.method)
  );
  return checkoutMethods.filter((method) => !used.has(method));
}

export interface CheckoutValidation {
  /** Что реально идёт в счёт: карта+депозит целиком + наличные в пределах остатка. */
  paidMinorUnits: number;
  /** Наличные, протянутые оператором. Излишек над остатком — это сдача, не переплата. */
  cashTenderedMinorUnits: number;
  walletMinorUnits: number;
  /** Сдача наличными = протянуто − (остаток под наличные). 0, если наличных нет или недобор. */
  changeMinorUnits: number;
  /** grandTotal − paid; positive means still owed. */
  remainingMinorUnits: number;
  /** Карта+депозит уже больше счёта: их сдачи не бывает — это ошибка ввода. */
  cardOverMinorUnits: number;
  isBalanced: boolean;
  walletWithinBalance: boolean;
  hasInvalidAmount: boolean;
  canSubmit: boolean;
  error: string | null;
}

/** Parse a major-unit money string (e.g. "22.50" or "22,50") to minor units.
 * Returns null for blank or malformed input. */
export function parseCheckoutAmount(value: string): number | null {
  const normalized = value.trim().replace(',', '.');
  if (normalized === '') {
    return null;
  }
  if (!/^\d+(\.\d{1,2})?$/.test(normalized)) {
    return null;
  }
  const major = Number(normalized);
  return Number.isFinite(major) ? majorToMinor(major) : null;
}

/** Format minor units as a fixed 2-decimal major string for an input field. */
export function formatCheckoutAmount(minorUnits: number): string {
  return minorToMajor(minorUnits).toFixed(2);
}

/** "Наиграно" label from billed seconds, e.g. 5400 → "1ч 30м". */
export function formatBilledDuration(billableSeconds: number, t: TFn): string {
  const totalMinutes = Math.max(0, Math.round(billableSeconds / 60));
  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;
  if (hours === 0) {
    return t('op.checkout.duration.min', { minutes });
  }
  return t('op.checkout.duration.hourMin', { hours, minutes });
}

/** Validate the operator's split-payment entry against the quoted grand total
 * and (for wallet parts) the player's wallet balance. Pure — the dialog reads
 * `canSubmit`/`error` to drive the confirm button and inline message. */
export function validateCheckoutPayments(
  drafts: readonly CheckoutPaymentDraft[],
  grandTotalMinorUnits: number,
  walletBalanceMinorUnits: number | null,
  t: TFn
): CheckoutValidation {
  let cashTenderedMinorUnits = 0;
  let nonCashMinorUnits = 0;
  let walletMinorUnits = 0;
  let hasInvalidAmount = false;

  for (const draft of drafts) {
    const trimmed = draft.amountText.trim();
    if (trimmed === '') {
      // A blank row is ignored rather than treated as an error so the operator
      // can leave an unused method row in place.
      continue;
    }
    const minorUnits = parseCheckoutAmount(trimmed);
    if (minorUnits === null || minorUnits <= 0) {
      hasInvalidAmount = true;
      continue;
    }
    if (draft.method === 'cash') {
      // Наличные — это «получено»: оператор вводит купюру, а не точную сумму к счёту.
      cashTenderedMinorUnits += minorUnits;
    } else {
      nonCashMinorUnits += minorUnits;
      if (draft.method === 'wallet') {
        walletMinorUnits += minorUnits;
      }
    }
  }

  // Карта/депозит — безналичные: оператор сам задаёт точную сумму списания, сдачи с них не бывает.
  // Если они уже перекрыли счёт — это ошибка ввода, а не «лишние деньги».
  const cardOverMinorUnits = Math.max(0, nonCashMinorUnits - grandTotalMinorUnits);
  // Остаток, который должны закрыть наличными после безнала.
  const cashDueMinorUnits = Math.max(0, grandTotalMinorUnits - nonCashMinorUnits);
  const appliedCashMinorUnits = Math.min(cashTenderedMinorUnits, cashDueMinorUnits);
  // Сдача — только с наличных и только когда безнал не перекрыл счёт.
  const changeMinorUnits = cardOverMinorUnits > 0 ? 0 : Math.max(0, cashTenderedMinorUnits - cashDueMinorUnits);
  const paidMinorUnits = Math.min(nonCashMinorUnits, grandTotalMinorUnits) + appliedCashMinorUnits;
  const remainingMinorUnits = grandTotalMinorUnits - paidMinorUnits;
  const isBalanced = cardOverMinorUnits === 0 && remainingMinorUnits === 0;
  const walletWithinBalance = walletMinorUnits <= (walletBalanceMinorUnits ?? 0);

  // A zero-total bill settles with no payment parts at all.
  const canSubmit =
    !hasInvalidAmount &&
    walletWithinBalance &&
    cardOverMinorUnits === 0 &&
    (grandTotalMinorUnits === 0 ? nonCashMinorUnits === 0 && cashTenderedMinorUnits === 0 : isBalanced);

  let error: string | null = null;
  if (hasInvalidAmount) {
    error = t('op.checkout.error.invalidAmount');
  } else if (!walletWithinBalance) {
    error = t('op.checkout.error.walletExceeds');
  } else if (cardOverMinorUnits > 0) {
    error = t('op.checkout.error.cardOver', { amount: formatCheckoutAmount(cardOverMinorUnits) });
  } else if (remainingMinorUnits > 0) {
    error = t('op.checkout.error.short', { amount: formatCheckoutAmount(remainingMinorUnits) });
  }

  return {
    paidMinorUnits,
    cashTenderedMinorUnits,
    walletMinorUnits,
    changeMinorUnits,
    remainingMinorUnits,
    cardOverMinorUnits,
    isBalanced,
    walletWithinBalance,
    hasInvalidAmount,
    canSubmit,
    error
  };
}

/** Build the API payment parts from the drafts, dropping blank/zero rows and
 * converting to minor units in the bill currency. Non-cash parts are recorded as
 * entered; cash is recorded as the amount *applied* to the bill (tendered minus
 * change), so the till entry matches the bill while the operator still sees the
 * change to hand back. */
export function buildCheckoutPayments(
  drafts: readonly CheckoutPaymentDraft[],
  currencyCode: string,
  grandTotalMinorUnits: number
): PaymentPartDto[] {
  const payments: PaymentPartDto[] = [];
  let nonCashMinorUnits = 0;
  let cashTenderedMinorUnits = 0;
  for (const draft of drafts) {
    const minorUnits = parseCheckoutAmount(draft.amountText);
    if (minorUnits === null || minorUnits <= 0) {
      continue;
    }
    if (draft.method === 'cash') {
      cashTenderedMinorUnits += minorUnits;
      continue;
    }
    payments.push({ paymentMethod: draft.method, amount: { currencyCode, minorUnits } });
    nonCashMinorUnits += minorUnits;
  }

  const appliedCashMinorUnits = Math.min(cashTenderedMinorUnits, Math.max(0, grandTotalMinorUnits - nonCashMinorUnits));
  if (appliedCashMinorUnits > 0) {
    payments.push({ paymentMethod: 'cash', amount: { currencyCode, minorUnits: appliedCashMinorUnits } });
  }
  return payments;
}

/** The initial split: a single cash row pre-filled with the whole grand total,
 * the common case. The operator can re-split or switch to deposit from there. */
export function initialCheckoutDrafts(grandTotalMinorUnits: number): CheckoutPaymentDraft[] {
  return [{ method: 'cash', amountText: grandTotalMinorUnits > 0 ? formatCheckoutAmount(grandTotalMinorUnits) : '' }];
}
