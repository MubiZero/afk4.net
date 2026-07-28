import { describe, it, expect } from 'bun:test';
import {
  buildShiftReportView,
  buildCashOperationReportView,
  buildGameplayTimeReportView,
  buildOperatorActionReportView,
  type ReportFormatters
} from './reportModel';

const fmt: ReportFormatters = {
  formatMinorUnits: (minor, code) => `${(minor / 100).toFixed(2)} ${code}`,
  formatNumber: (n) => String(n),
  formatDate: (iso) => iso.slice(0, 10)
};

const money = (minorUnits: number) => ({ minorUnits, currencyCode: 'TJS' });

describe('reportModel', () => {
  it('maps a shift report row incl. null counted/difference to «—»', () => {
    const view = buildShiftReportView({
      rows: [{
        state: 'Closed',
        openedAtUtc: '2026-07-01T08:00:00Z',
        closedAtUtc: null,
        cashMovementsTotal: money(5000),
        expectedCash: money(12000),
        countedCash: null,
        difference: null
      }]
    }, fmt);
    expect(view.columns.map((c) => c.key)).toEqual(['state', 'opened', 'closed', 'movements', 'expected', 'counted', 'difference']);
    expect(view.rows[0]).toMatchObject({
      state: 'Closed',
      opened: '2026-07-01',
      closed: '—',
      movements: '50.00 TJS',
      expected: '120.00 TJS',
      counted: '—',
      difference: '—'
    });
  });

  it('maps cash-operation summary + rows', () => {
    const view = buildCashOperationReportView({
      cashInTotal: money(30000),
      cashOutTotal: money(10000),
      netCashTotal: money(20000),
      rows: [{ sourceType: 'Shift', operationType: 'CashIn', cashImpact: money(30000), reason: 'float', createdAtUtc: '2026-07-01T09:00:00Z' }]
    }, fmt);
    expect(view.summaryCards.map((c) => c.value)).toEqual(['300.00 TJS', '100.00 TJS', '200.00 TJS']);
    expect(view.rows[0]).toMatchObject({ source: 'Shift', opType: 'CashIn', impact: '300.00 TJS', reason: 'float', created: '2026-07-01' });
  });

  it('maps gameplay-time seconds → minutes', () => {
    const view = buildGameplayTimeReportView({
      totalDurationSeconds: 3600,
      totalPackageSeconds: 1800,
      totalBonusSeconds: 0,
      gameplayRevenueTotal: money(45000),
      rows: [{ seatId: 'A1', deviceId: 'PC-1', playerKind: 'Guest', state: 'Ended', durationSeconds: 3600, gameplayRevenue: money(45000) }]
    }, fmt);
    expect(view.summaryCards[0].value).toBe('60'); // 3600s → 60 min
    expect(view.rows[0]).toMatchObject({ seat: 'A1', device: 'PC-1', playerKind: 'Guest', state: 'Ended', duration: '60', revenue: '450.00 TJS' });
  });

  it('maps operator-action counts', () => {
    const view = buildOperatorActionReportView({
      totalActionCount: 7,
      rows: [{ actorDisplayName: 'Иван', action: 'sale.pay', outcome: 'Succeeded', count: 5, firstAtUtc: '2026-07-01T09:00:00Z', lastAtUtc: '2026-07-01T18:00:00Z' }]
    }, fmt);
    expect(view.summaryCards[0].value).toBe('7');
    expect(view.rows[0]).toMatchObject({ operator: 'Иван', action: 'sale.pay', outcome: 'Succeeded', count: '5', first: '2026-07-01', last: '2026-07-01' });
  });

  it('empty rows → empty view rows', () => {
    expect(buildShiftReportView({ rows: [] }, fmt).rows).toEqual([]);
    expect(buildShiftReportView({}, fmt).rows).toEqual([]);
  });
});
