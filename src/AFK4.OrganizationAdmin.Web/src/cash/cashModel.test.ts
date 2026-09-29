import { describe, it, expect } from 'bun:test';
import { buildCashHeader, visibleCashTabs } from './cashModel';
import type { ShiftRevenueDto } from '../operatorApiClients';

function m(minorUnits: number) {
  return { currencyCode: 'TJS', minorUnits };
}

function openShift(overrides: Partial<ShiftRevenueDto> = {}): ShiftRevenueDto {
  return {
    shiftId: 's1', organizationId: 'o', branchId: 'b',
    openedByStaffUserId: 'u1', closedByStaffUserId: null, state: 'open',
    earned: { time: m(1000), goods: m(500), noShow: m(0), total: m(1500) },
    inflow: { cash: m(0), nonCash: m(0), walletTopUps: m(0), directTotal: m(0) },
    cash: { starting: m(10000), expected: m(11500), counted: null, difference: null },
    openedAtUtc: '2026-06-24T08:00:00Z', closedAtUtc: null,
    ...overrides
  };
}

describe('buildCashHeader', () => {
  it('открытая смена → isOpen + касса (expected) + выручка (earned.total)', () => {
    const s = buildCashHeader(openShift());
    expect(s.isOpen).toBe(true);
    expect(s.openedAtUtc).toBe('2026-06-24T08:00:00Z');
    expect(s.cashInHand?.minorUnits).toBe(11500);
    expect(s.revenueTotal?.minorUnits).toBe(1500);
  });

  it('null → закрыто, всё пусто', () => {
    const s = buildCashHeader(null);
    expect(s.isOpen).toBe(false);
    expect(s.openedAtUtc).toBeNull();
    expect(s.cashInHand).toBeNull();
    expect(s.revenueTotal).toBeNull();
  });

  it('state !== open (closed) → закрыто', () => {
    const s = buildCashHeader(openShift({ state: 'closed' }));
    expect(s.isOpen).toBe(false);
  });
});

function session(permissions: string[]) {
  return { permissions } as unknown as import('../authClient').OperatorAuthSession;
}

// Бывший «Журнал кассы» разложен на три вкладки первого уровня — у каждой свои права, как были у
// его сегментов: сотрудник не видит вкладку, к которой у него нет доступа.
describe('visibleCashTabs (per-tab гранулярность прав)', () => {
  it('только право продаж → только Продажи', () => {
    expect(visibleCashTabs(session(['organization.pos.sales.create']))).toEqual(['sales']);
  });
  it('reports.view → Смена + Кассовые операции', () => {
    expect(visibleCashTabs(session(['organization.reports.view']))).toEqual(['shift', 'ops']);
  });
  it('shifts.view → Смена + Кассовые операции', () => {
    expect(visibleCashTabs(session(['organization.shifts.view']))).toEqual(['shift', 'ops']);
  });
  it('только approveMoneyAction → Согласования', () => {
    expect(visibleCashTabs(session(['organization.billing.money_action.approve']))).toEqual(['review']);
  });
  it('только receipts.view → Чеки', () => {
    expect(visibleCashTabs(session(['organization.receipts.view']))).toEqual(['receipts']);
  });
  it('только pos.sales.refund → Продажи + Чеки', () => {
    expect(visibleCashTabs(session(['organization.pos.sales.refund']))).toEqual(['sales', 'receipts']);
  });
  it('null сессия → пусто', () => {
    expect(visibleCashTabs(null)).toEqual([]);
  });
  it('все права → все вкладки по порядку', () => {
    expect(visibleCashTabs(session([
      'organization.pos.sales.create', 'organization.pos.sales.pay', 'organization.pos.sales.refund', 'organization.pos.sales.void',
      'organization.shifts.view', 'organization.shifts.open', 'organization.reports.view', 'organization.billing.money_action.approve',
      'organization.billing.wallet.top_up'
    ]))).toEqual(['sales', 'shift', 'topups', 'ops', 'receipts', 'review']);
  });
});
