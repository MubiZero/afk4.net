import type { ShiftRevenueDto } from '../operatorApiClients';

type Money = ShiftRevenueDto['earned']['total'];

export interface CashHeaderState {
  isOpen: boolean;
  openedAtUtc: string | null;
  cashInHand: Money | null;
  revenueTotal: Money | null;
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
