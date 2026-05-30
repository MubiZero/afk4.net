import type { MessageKey } from '@/i18n/messages';
import type {
  MoneyMinor, ShiftReport, SalesReport, GameplayTimeReport,
  CashOperationReport, OperatorActionReport
} from '@/api/types';
import { minorToMajor } from '../money';

export interface ReportFormatters {
  formatCurrency: (amountMajor: number, currencyCode: string) => string;
  formatNumber: (value: number) => string;
  formatDate: (iso: string) => string;
}

export interface SummaryCard { labelKey: MessageKey; value: string; }
export interface ReportColumn { key: string; labelKey: MessageKey; }
export interface ReportView {
  summaryCards: SummaryCard[];
  columns: ReportColumn[];
  rows: Record<string, string>[];
}

export interface DateRange { fromUtc: string; toUtc: string; }
export type RangePreset = 'today' | '7d' | '30d';

export function presetRange(preset: RangePreset, now: Date): DateRange {
  const y = now.getUTCFullYear();
  const m = now.getUTCMonth();
  const d = now.getUTCDate();
  const back = preset === 'today' ? 0 : preset === '7d' ? 6 : 29;
  const start = new Date(Date.UTC(y, m, d - back, 0, 0, 0));
  const end = new Date(Date.UTC(y, m, d, 23, 59, 59));
  return { fromUtc: start.toISOString(), toUtc: end.toISOString() };
}

export function isoToDateInput(iso: string): string { return iso.slice(0, 10); }
export function dateInputToFromUtc(date: string): string { return `${date}T00:00:00.000Z`; }
export function dateInputToToUtc(date: string): string { return `${date}T23:59:59.000Z`; }

function money(m: MoneyMinor, fmt: ReportFormatters): string {
  return fmt.formatCurrency(minorToMajor(m.minorUnits), m.currencyCode);
}
function optMoney(m: MoneyMinor | null, fmt: ReportFormatters): string {
  return m === null ? '—' : money(m, fmt);
}
function minutes(seconds: number, fmt: ReportFormatters): string {
  return fmt.formatNumber(Math.round(seconds / 60));
}
function optDate(iso: string | null, fmt: ReportFormatters): string {
  return iso === null ? '—' : fmt.formatDate(iso);
}

export function buildShiftReport(report: ShiftReport, fmt: ReportFormatters): ReportView {
  return {
    summaryCards: [],
    columns: [
      { key: 'state', labelKey: 'reports.col.state' },
      { key: 'opened', labelKey: 'reports.col.opened' },
      { key: 'closed', labelKey: 'reports.col.closed' },
      { key: 'movements', labelKey: 'reports.col.movements' },
      { key: 'expected', labelKey: 'reports.col.expectedCash' },
      { key: 'counted', labelKey: 'reports.col.countedCash' },
      { key: 'difference', labelKey: 'reports.col.difference' }
    ],
    rows: report.rows.map(r => ({
      state: r.state,
      opened: fmt.formatDate(r.openedAtUtc),
      closed: optDate(r.closedAtUtc, fmt),
      movements: money(r.cashMovementsTotal, fmt),
      expected: money(r.expectedCash, fmt),
      counted: optMoney(r.countedCash, fmt),
      difference: optMoney(r.difference, fmt)
    }))
  };
}

export function buildSalesReport(report: SalesReport, fmt: ReportFormatters): ReportView {
  return {
    summaryCards: [
      { labelKey: 'reports.sum.gross', value: money(report.grossSalesTotal, fmt) },
      { labelKey: 'reports.sum.refunds', value: money(report.refundsTotal, fmt) },
      { labelKey: 'reports.sum.net', value: money(report.netSalesTotal, fmt) }
    ],
    columns: [
      { key: 'state', labelKey: 'reports.col.state' },
      { key: 'total', labelKey: 'reports.col.total' },
      { key: 'paid', labelKey: 'reports.col.paid' },
      { key: 'refund', labelKey: 'reports.col.refund' },
      { key: 'lines', labelKey: 'reports.col.lines' },
      { key: 'qty', labelKey: 'reports.col.qty' },
      { key: 'created', labelKey: 'reports.col.created' },
      { key: 'paidAt', labelKey: 'reports.col.paidAt' }
    ],
    rows: report.rows.map(r => ({
      state: r.state,
      total: money(r.total, fmt),
      paid: money(r.paidAmount, fmt),
      refund: money(r.refundAmount, fmt),
      lines: fmt.formatNumber(r.lineCount),
      qty: fmt.formatNumber(r.itemQuantity),
      created: fmt.formatDate(r.createdAtUtc),
      paidAt: optDate(r.paidAtUtc, fmt)
    }))
  };
}

export function buildGameplayReport(report: GameplayTimeReport, fmt: ReportFormatters): ReportView {
  return {
    summaryCards: [
      { labelKey: 'reports.sum.duration', value: minutes(report.totalDurationSeconds, fmt) },
      { labelKey: 'reports.sum.package', value: minutes(report.totalPackageSeconds, fmt) },
      { labelKey: 'reports.sum.bonus', value: minutes(report.totalBonusSeconds, fmt) },
      { labelKey: 'reports.sum.revenue', value: money(report.gameplayRevenueTotal, fmt) }
    ],
    columns: [
      { key: 'seat', labelKey: 'reports.col.seat' },
      { key: 'device', labelKey: 'reports.col.device' },
      { key: 'playerKind', labelKey: 'reports.col.playerKind' },
      { key: 'state', labelKey: 'reports.col.state' },
      { key: 'duration', labelKey: 'reports.col.duration' },
      { key: 'revenue', labelKey: 'reports.col.revenue' }
    ],
    rows: report.rows.map(r => ({
      seat: r.seatId,
      device: r.deviceId,
      playerKind: r.playerKind,
      state: r.state,
      duration: minutes(r.durationSeconds, fmt),
      revenue: money(r.gameplayRevenue, fmt)
    }))
  };
}

export function buildCashReport(report: CashOperationReport, fmt: ReportFormatters): ReportView {
  return {
    summaryCards: [
      { labelKey: 'reports.sum.cashIn', value: money(report.cashInTotal, fmt) },
      { labelKey: 'reports.sum.cashOut', value: money(report.cashOutTotal, fmt) },
      { labelKey: 'reports.sum.netCash', value: money(report.netCashTotal, fmt) }
    ],
    columns: [
      { key: 'source', labelKey: 'reports.col.source' },
      { key: 'opType', labelKey: 'reports.col.opType' },
      { key: 'impact', labelKey: 'reports.col.impact' },
      { key: 'reason', labelKey: 'reports.col.reason' },
      { key: 'created', labelKey: 'reports.col.created' }
    ],
    rows: report.rows.map(r => ({
      source: r.sourceType,
      opType: r.operationType,
      impact: money(r.cashImpact, fmt),
      reason: r.reason,
      created: fmt.formatDate(r.createdAtUtc)
    }))
  };
}

export function buildOperatorActionReport(report: OperatorActionReport, fmt: ReportFormatters): ReportView {
  return {
    summaryCards: [
      { labelKey: 'reports.sum.actions', value: fmt.formatNumber(report.totalActionCount) }
    ],
    columns: [
      { key: 'operator', labelKey: 'reports.col.operator' },
      { key: 'action', labelKey: 'reports.col.action' },
      { key: 'outcome', labelKey: 'reports.col.outcome' },
      { key: 'count', labelKey: 'reports.col.count' },
      { key: 'first', labelKey: 'reports.col.first' },
      { key: 'last', labelKey: 'reports.col.last' }
    ],
    rows: report.rows.map(r => ({
      operator: r.actorDisplayName,
      action: r.action,
      outcome: r.outcome,
      count: fmt.formatNumber(r.count),
      first: fmt.formatDate(r.firstAtUtc),
      last: fmt.formatDate(r.lastAtUtc)
    }))
  };
}
