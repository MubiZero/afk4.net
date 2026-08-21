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

describe('visibleCashTabs (per-tab гранулярность прав)', () => {
  it('только право продаж → только Продажи (sales — суперсет, orders-вкладка удалена)', () => {
    expect(visibleCashTabs(session(['organization.pos.sales.create']))).toEqual(['sales']);
  });
  it('reports.view → Смена + Журнал (суперсет прав)', () => {
    expect(visibleCashTabs(session(['organization.reports.view']))).toEqual(['shift', 'journal']);
  });
  it('shifts.view → Смена + Журнал (суперсет прав)', () => {
    expect(visibleCashTabs(session(['organization.shifts.view']))).toEqual(['shift', 'journal']);
  });
  it('только approveMoneyAction → Журнал', () => {
    expect(visibleCashTabs(session(['organization.billing.money_action.approve']))).toEqual(['journal']);
  });
  it('только receipts.view → Журнал', () => {
    expect(visibleCashTabs(session(['organization.receipts.view']))).toEqual(['journal']);
  });
  it('только pos.sales.refund → Журнал', () => {
    expect(visibleCashTabs(session(['organization.pos.sales.refund']))).toEqual(['sales', 'journal']);
  });
  it('только viewReports → Журнал виден (расширение доступа, не регрессия)', () => {
    expect(visibleCashTabs(session(['organization.reports.view']))).toContain('journal');
  });
  it('null сессия → пусто', () => {
    expect(visibleCashTabs(null)).toEqual([]);
  });
  it('все права → все 3 вкладки по порядку', () => {
    expect(visibleCashTabs(session([
      'organization.pos.sales.create', 'organization.pos.sales.pay', 'organization.pos.sales.refund', 'organization.pos.sales.void',
      'organization.shifts.view', 'organization.shifts.open', 'organization.reports.view', 'organization.billing.money_action.approve'
    ]))).toEqual(['sales', 'shift', 'journal']);
  });
});
