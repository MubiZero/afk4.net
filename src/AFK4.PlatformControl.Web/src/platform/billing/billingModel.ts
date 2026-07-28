import type {
  CreatePlanRequest,
  InvoiceListItem,
  SubscriptionListItem,
  SubscriptionPlan,
  UpdatePlanRequest
} from '@/api/types';
import type { MessageKey } from '@/i18n/messages';
import type { BadgeVariant } from '@/components/ui/badge';

export interface ListFilter {
  query: string;
  status: string; // 'all' | concrete status
}

export function filterSubscriptions(rows: SubscriptionListItem[], filter: ListFilter): SubscriptionListItem[] {
  const q = filter.query.trim().toLowerCase();
  return rows
    .filter(r => filter.status === 'all' || r.status === filter.status)
    .filter(r => q === '' || r.organizationName.toLowerCase().includes(q) || r.organizationSlug.toLowerCase().includes(q));
}

export function filterInvoices(rows: InvoiceListItem[], filter: ListFilter): InvoiceListItem[] {
  const q = filter.query.trim().toLowerCase();
  return rows
    .filter(r => filter.status === 'all' || r.status === filter.status)
    .filter(r => q === '' || r.organizationName.toLowerCase().includes(q) || r.organizationSlug.toLowerCase().includes(q));
}

export const INVOICE_STATUS_VARIANT: Record<string, BadgeVariant> = {
  issued: 'secondary',
  paid: 'success',
  void: 'outline',
  overdue: 'destructive'
};

export const INVOICE_STATUS_LABEL: Record<string, MessageKey> = {
  issued: 'platform.billing.invoiceStatus.issued',
  paid: 'platform.billing.invoiceStatus.paid',
  void: 'platform.billing.invoiceStatus.void',
  overdue: 'platform.billing.invoiceStatus.overdue'
};

export const INVOICE_KIND_LABEL: Record<string, MessageKey> = {
  subscription: 'platform.billing.invoiceKind.subscription',
  proration: 'platform.billing.invoiceKind.proration'
};

export const SUBSCRIPTION_STATUS_VARIANT: Record<string, BadgeVariant> = {
  trial: 'secondary',
  active: 'success',
  past_due: 'destructive',
  cancelled: 'outline'
};

export const SUBSCRIPTION_STATUS_LABEL: Record<string, MessageKey> = {
  trial: 'platform.organization.subscription.trial',
  active: 'platform.organization.subscription.active',
  past_due: 'platform.organization.subscription.pastDue',
  cancelled: 'platform.organization.subscription.cancelled'
};

export const INTERVAL_LABEL: Record<string, MessageKey> = {
  monthly: 'platform.billing.interval.monthly',
  yearly: 'platform.billing.interval.yearly'
};

export const INVOICE_STATUS_FILTERS = ['all', 'issued', 'paid', 'void', 'overdue'] as const;
export const SUBSCRIPTION_STATUS_FILTERS = ['all', 'trial', 'active', 'past_due', 'cancelled'] as const;

export interface PlanForm {
  planCode: string;
  name: string;
  priceMinorUnits: number;
  currencyCode: string;
  billingInterval: string;
  maxBranches: number | null;
  maxDevicesPerBranch: number | null;
  maxConcurrentSessions: number | null;
  maxStaffUsersPerBranch: number | null;
  isActive: boolean;
  sortOrder: number;
}

export function emptyPlanForm(): PlanForm {
  return {
    planCode: '',
    name: '',
    priceMinorUnits: 0,
    currencyCode: 'RUB',
    billingInterval: 'monthly',
    maxBranches: null,
    maxDevicesPerBranch: null,
    maxConcurrentSessions: null,
    maxStaffUsersPerBranch: null,
    isActive: true,
    sortOrder: 0
  };
}

export function planToForm(plan: SubscriptionPlan): PlanForm {
  return {
    planCode: plan.planCode,
    name: plan.name,
    priceMinorUnits: plan.priceMinorUnits,
    currencyCode: plan.currencyCode,
    billingInterval: plan.billingInterval,
    maxBranches: plan.maxBranches,
    maxDevicesPerBranch: plan.maxDevicesPerBranch,
    maxConcurrentSessions: plan.maxConcurrentSessions,
    maxStaffUsersPerBranch: plan.maxStaffUsersPerBranch,
    isActive: plan.isActive,
    sortOrder: plan.sortOrder
  };
}

export function validatePlanForm(form: PlanForm): boolean {
  if (form.planCode.trim() === '') return false;
  if (form.name.trim() === '') return false;
  if (!Number.isFinite(form.priceMinorUnits) || form.priceMinorUnits < 0) return false;
  return true;
}

export function planFormToCreateRequest(form: PlanForm): CreatePlanRequest {
  return {
    planCode: form.planCode.trim(),
    name: form.name.trim(),
    priceMinorUnits: form.priceMinorUnits,
    currencyCode: form.currencyCode,
    billingInterval: form.billingInterval,
    maxBranches: form.maxBranches,
    maxDevicesPerBranch: form.maxDevicesPerBranch,
    maxConcurrentSessions: form.maxConcurrentSessions,
    maxStaffUsersPerBranch: form.maxStaffUsersPerBranch,
    sortOrder: form.sortOrder
  };
}

export function planFormToUpdateRequest(form: PlanForm): UpdatePlanRequest {
  return {
    name: form.name.trim(),
    priceMinorUnits: form.priceMinorUnits,
    currencyCode: form.currencyCode,
    billingInterval: form.billingInterval,
    maxBranches: form.maxBranches,
    maxDevicesPerBranch: form.maxDevicesPerBranch,
    maxConcurrentSessions: form.maxConcurrentSessions,
    maxStaffUsersPerBranch: form.maxStaffUsersPerBranch,
    isActive: form.isActive,
    sortOrder: form.sortOrder
  };
}
