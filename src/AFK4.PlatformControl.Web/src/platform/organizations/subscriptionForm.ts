import type { MessageKey } from '@afk4/i18n';
import { majorToMinor, minorToMajor } from '@/lib/money';
import type { OrganizationSubscription, UpdateSubscriptionRequest } from '@/api/types';

export const SUBSCRIPTION_STATUS_OPTIONS = ['trial', 'active', 'past_due', 'cancelled'] as const;
export const BILLING_INTERVAL_OPTIONS = ['monthly', 'yearly'] as const;

/// Скидку задают либо процентом, либо суммой — сервер отвергает обе сразу. Поэтому в форме это
/// один выбор, а не два независимых поля: иначе человек заполнит оба и получит отказ.
export type DiscountKind = 'none' | 'percent' | 'amount';

export interface SubscriptionForm {
  planCode: string;
  billingInterval: string;
  status: string;
  cancelAtPeriodEnd: boolean;
  amount: string;
  currentPeriodEnd: string;
  discountKind: DiscountKind;
  discountValue: string;
  discountUntil: string;
  discountReason: string;
}

export function subscriptionToForm(subscription: OrganizationSubscription): SubscriptionForm {
  const discountKind: DiscountKind = subscription.discountPercent !== null
    ? 'percent'
    : subscription.discountAmountMinorUnits !== null
      ? 'amount'
      : 'none';

  return {
    planCode: subscription.planCode,
    billingInterval: subscription.billingInterval,
    status: subscription.status,
    cancelAtPeriodEnd: subscription.cancelAtPeriodEnd,
    amount: String(minorToMajor(subscription.amountMinorUnits)),
    currentPeriodEnd: subscription.currentPeriodEndUtc.slice(0, 10),
    discountKind,
    discountValue: discountKind === 'percent'
      ? String(subscription.discountPercent)
      : discountKind === 'amount'
        ? String(minorToMajor(subscription.discountAmountMinorUnits ?? 0))
        : '',
    discountUntil: subscription.discountUntilUtc?.slice(0, 10) ?? '',
    discountReason: subscription.discountReason ?? ''
  };
}

/// Что не так с формой, или `null` — можно отправлять. Проверки повторяют серверные: отказ
/// сервера на языке его полей («DiscountPercent must be between 1 and 100») человеку за панелью
/// ничего не объясняет, а до отправки мы можем сказать то же самое по-человечески.
export function validateSubscriptionForm(form: SubscriptionForm): MessageKey | null {
  const amount = parseDecimal(form.amount);
  if (amount === null || amount < 0) {
    return 'platform.organization.subscriptionForm.error.amount';
  }

  if (form.currentPeriodEnd.length === 0) {
    return 'platform.organization.subscriptionForm.error.periodEnd';
  }

  if (form.status === 'trial' && form.currentPeriodEnd.length === 0) {
    return 'platform.organization.subscriptionForm.error.trialNeedsPeriodEnd';
  }

  if (form.discountKind === 'percent') {
    const percent = parseDecimal(form.discountValue);
    if (percent === null || !Number.isInteger(percent) || percent < 1 || percent > 100) {
      return 'platform.organization.subscriptionForm.error.discountPercent';
    }
  }

  if (form.discountKind === 'amount') {
    const value = parseDecimal(form.discountValue);
    if (value === null || value <= 0) {
      return 'platform.organization.subscriptionForm.error.discountAmount';
    }
  }

  return null;
}

/**
 * Запрос на сохранение. Отправляем только изменённое: сервер трактует `null` как «не трогать»,
 * и лишнее поле в запросе — это правка, которой человек не делал.
 */
export function subscriptionFormToRequest(
  form: SubscriptionForm,
  subscription: OrganizationSubscription
): UpdateSubscriptionRequest {
  const amountMinorUnits = majorToMinor(parseDecimal(form.amount) ?? 0);
  const hadDiscount = subscription.discountPercent !== null || subscription.discountAmountMinorUnits !== null;
  const discountValue = parseDecimal(form.discountValue);

  return {
    planCode: form.planCode !== subscription.planCode ? form.planCode : null,
    billingInterval: form.billingInterval !== subscription.billingInterval ? form.billingInterval : null,
    status: form.status !== subscription.status ? form.status : null,
    cancelAtPeriodEnd: form.cancelAtPeriodEnd !== subscription.cancelAtPeriodEnd ? form.cancelAtPeriodEnd : null,
    amountMinorUnits: amountMinorUnits !== subscription.amountMinorUnits ? amountMinorUnits : null,
    currentPeriodEndUtc: form.currentPeriodEnd !== subscription.currentPeriodEndUtc.slice(0, 10)
      ? new Date(`${form.currentPeriodEnd}T00:00:00Z`).toISOString()
      : null,
    paymentGraceUntilUtc: null,
    clearPaymentGrace: null,
    discountPercent: form.discountKind === 'percent' && discountValue !== null ? discountValue : null,
    discountAmountMinorUnits: form.discountKind === 'amount' && discountValue !== null ? majorToMinor(discountValue) : null,
    discountUntilUtc: form.discountKind !== 'none' && form.discountUntil.length > 0
      ? new Date(`${form.discountUntil}T00:00:00Z`).toISOString()
      : null,
    discountReason: form.discountKind !== 'none' && form.discountReason.trim().length > 0
      ? form.discountReason.trim()
      : null,
    // Снятие скидки — отдельный флаг, а не пустые поля: «ничего не прислали» для сервера значит
    // «оставь как было», и без него скидку нельзя было бы убрать вовсе.
    clearDiscount: form.discountKind === 'none' && hadDiscount ? true : null
  };
}

function parseDecimal(value: string): number | null {
  const parsed = Number.parseFloat(value.replace(',', '.'));
  return Number.isFinite(parsed) ? parsed : null;
}
