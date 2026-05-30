import { it, expect } from 'vitest';
import type {
  ShiftReport, SalesReport, GameplayTimeReport, CashOperationReport, OperatorActionReport
} from '@/api/types';
import {
  presetRange, isoToDateInput, dateInputToFromUtc, dateInputToToUtc,
  buildShiftReport, buildSalesReport, buildGameplayReport, buildCashReport, buildOperatorActionReport,
  type ReportFormatters
} from './reportsModel';

const fmt: ReportFormatters = {
  formatCurrency: (a, c) => `${a.toFixed(2)} ${c}`,
  formatNumber: n => String(n),
  formatDate: iso => iso.slice(0, 10)
};

it('presetRange today spans the full UTC day', () => {
  const r = presetRange('today', new Date('2026-05-30T15:00:00.000Z'));
  expect(r.fromUtc).toBe('2026-05-30T00:00:00.000Z');
  expect(r.toUtc).toBe('2026-05-30T23:59:59.000Z');
});

it('presetRange 7d starts six days earlier', () => {
  const r = presetRange('7d', new Date('2026-05-30T15:00:00.000Z'));
  expect(r.fromUtc).toBe('2026-05-24T00:00:00.000Z');
});

it('date input helpers round-trip', () => {
  expect(isoToDateInput('2026-05-30T23:59:59.000Z')).toBe('2026-05-30');
  expect(dateInputToFromUtc('2026-05-30')).toBe('2026-05-30T00:00:00.000Z');
  expect(dateInputToToUtc('2026-05-30')).toBe('2026-05-30T23:59:59.000Z');
});

it('buildSalesReport produces summary cards and formatted rows', () => {
  const report: SalesReport = {
    limit: 100,
    grossSalesTotal: { currencyCode: 'RUB', minorUnits: 150000 },
    refundsTotal: { currencyCode: 'RUB', minorUnits: 0 },
    netSalesTotal: { currencyCode: 'RUB', minorUnits: 150000 },
    rows: [{
      posSaleId: 's1', organizationId: 'o', branchId: 'b', shiftId: 'sh', createdByStaffUserId: 'u',
      state: 'Paid', total: { currencyCode: 'RUB', minorUnits: 150000 },
      paidAmount: { currencyCode: 'RUB', minorUnits: 150000 }, refundAmount: { currencyCode: 'RUB', minorUnits: 0 },
      lineCount: 2, itemQuantity: 3, createdAtUtc: '2026-05-30T10:00:00.000Z',
      paidAtUtc: '2026-05-30T10:01:00.000Z', refundedAtUtc: null, voidedAtUtc: null
    }]
  };
  const view = buildSalesReport(report, fmt);
  expect(view.summaryCards).toEqual([
    { labelKey: 'reports.sum.gross', value: '1500.00 RUB' },
    { labelKey: 'reports.sum.refunds', value: '0.00 RUB' },
    { labelKey: 'reports.sum.net', value: '1500.00 RUB' }
  ]);
  expect(view.rows[0].total).toBe('1500.00 RUB');
  expect(view.rows[0].qty).toBe('3');
});

it('buildGameplayReport converts seconds to minutes', () => {
  const report: GameplayTimeReport = {
    limit: 100, totalDurationSeconds: 3600, totalPackageSeconds: 1800, totalBonusSeconds: 600,
    gameplayRevenueTotal: { currencyCode: 'RUB', minorUnits: 50000 },
    rows: [{
      sessionId: 'g1', organizationId: 'o', branchId: 'b', seatId: 'seat', deviceId: 'dev',
      createdByStaffUserId: 'u', playerKind: 'Member', playerAccountId: 'p', state: 'Ended',
      durationSeconds: 3600, packageSeconds: 1800, bonusSeconds: 600,
      gameplayRevenue: { currencyCode: 'RUB', minorUnits: 50000 },
      startedAtUtc: '2026-05-30T09:00:00.000Z', endedAtUtc: '2026-05-30T10:00:00.000Z', endsAtUtc: null
    }]
  };
  const view = buildGameplayReport(report, fmt);
  expect(view.summaryCards[0]).toEqual({ labelKey: 'reports.sum.duration', value: '60' });
  expect(view.rows[0].duration).toBe('60');
});

it('buildShiftReport has no summary cards and renders optional money as dash', () => {
  const report: ShiftReport = {
    limit: 100,
    rows: [{
      shiftId: 'sh1', organizationId: 'o', branchId: 'b', openedByStaffUserId: 'u', closedByStaffUserId: null,
      state: 'Open', startingCash: { currencyCode: 'RUB', minorUnits: 0 },
      cashMovementsTotal: { currencyCode: 'RUB', minorUnits: 1000 },
      posCashPaymentsTotal: { currencyCode: 'RUB', minorUnits: 0 },
      posRefundsTotal: { currencyCode: 'RUB', minorUnits: 0 },
      billingCashImpactTotal: { currencyCode: 'RUB', minorUnits: 0 },
      expectedCash: { currencyCode: 'RUB', minorUnits: 1000 },
      countedCash: null, difference: null,
      openedAtUtc: '2026-05-30T08:00:00.000Z', closedAtUtc: null
    }]
  };
  const view = buildShiftReport(report, fmt);
  expect(view.summaryCards).toEqual([]);
  expect(view.rows[0].counted).toBe('—');
  expect(view.rows[0].closed).toBe('—');
});

it('buildCashReport and buildOperatorActionReport build totals', () => {
  const cash: CashOperationReport = {
    limit: 100,
    cashInTotal: { currencyCode: 'RUB', minorUnits: 200000 },
    cashOutTotal: { currencyCode: 'RUB', minorUnits: 50000 },
    netCashTotal: { currencyCode: 'RUB', minorUnits: 150000 },
    rows: [{
      operationId: 'c1', organizationId: 'o', branchId: 'b', shiftId: null, createdByStaffUserId: 'u',
      sourceType: 'Shift', operationType: 'Deposit', cashImpact: { currencyCode: 'RUB', minorUnits: 200000 },
      reason: 'open', createdAtUtc: '2026-05-30T08:00:00.000Z'
    }]
  };
  expect(buildCashReport(cash, fmt).summaryCards[2]).toEqual({ labelKey: 'reports.sum.netCash', value: '1500.00 RUB' });

  const actions: OperatorActionReport = {
    limit: 100, totalActionCount: 42,
    rows: [{
      actorStaffUserId: 'u1', actorDisplayName: 'Иван', action: 'session.start', outcome: 'Succeeded',
      count: 42, firstAtUtc: '2026-05-30T08:00:00.000Z', lastAtUtc: '2026-05-30T20:00:00.000Z'
    }]
  };
  const view = buildOperatorActionReport(actions, fmt);
  expect(view.summaryCards).toEqual([{ labelKey: 'reports.sum.actions', value: '42' }]);
  expect(view.rows[0].operator).toBe('Иван');
});
