import type { ShiftRevenueDto } from '../operatorApiClients';
import type { OperatorAuthSession } from '../authClient';
import { hasAnyPermission, permissionNames } from '../operatorPermissions';
import type { CashTab } from './CashTabBar';

type Money = ShiftRevenueDto['earned']['total'];

export interface CashHeaderState {
  isOpen: boolean;
  openedAtUtc: string | null;
  cashInHand: Money | null;
  revenueTotal: Money | null;
}

// Права под-вкладок «Кассы» = права соответствующих старых отдельных воркспейсов. Так
// сохраняется гранулярность IA: оператор не видит вкладки, к которым у него нет доступа.
const CASH_TAB_PERMISSIONS: Record<CashTab, readonly string[]> = {
  sales: [permissionNames.createPosSale, permissionNames.payPosSale, permissionNames.refundPosSale, permissionNames.voidPosSale],
  orders: [permissionNames.createPosSale],
  payments: [permissionNames.viewShift, permissionNames.openShift, permissionNames.viewReports],
  shifts: [permissionNames.viewReports],
  review: [permissionNames.approveMoneyAction]
};

const CASH_TAB_ORDER: CashTab[] = ['sales', 'orders', 'payments', 'shifts', 'review'];

export function visibleCashTabs(session: OperatorAuthSession | null): CashTab[] {
  return CASH_TAB_ORDER.filter((id) => hasAnyPermission(session, CASH_TAB_PERMISSIONS[id]));
}

// Текущая смена для шапки-якоря. Открыта = state==='open'; всё остальное (null/closed) — касса закрыта.
export function buildCashHeader(revenue: ShiftRevenueDto | null): CashHeaderState {
  if (revenue === null || revenue.state !== 'open') {
    return { isOpen: false, openedAtUtc: null, cashInHand: null, revenueTotal: null };
  }
  return {
    isOpen: true,
    openedAtUtc: revenue.openedAtUtc,
    cashInHand: revenue.cash.expected,
    revenueTotal: revenue.earned.total
  };
}
