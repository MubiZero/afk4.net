import type { MessageKey } from '@afk4/i18n';
import { readArray, readMoney, readNumber, readString } from '../../operatorHelpers';

export interface ReportFormatters {
  formatMinorUnits: (minorUnits: number, currencyCode: string) => string;
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

function rowsOf(result: Record<string, unknown>): Record<string, unknown>[] {
  return readArray<Record<string, unknown>>(result, 'rows');
}

function money(rec: Record<string, unknown>, key: string, fmt: ReportFormatters): string {
  const m = readMoney(rec, key);
  return m === null ? '—' : fmt.formatMinorUnits(m.minorUnits, m.currencyCode);
}

function dateOrDash(rec: Record<string, unknown>, key: string, fmt: ReportFormatters): string {
  const iso = readString(rec, key);
  return iso === '' ? '—' : fmt.formatDate(iso);
}

function minutes(rec: Record<string, unknown>, key: string, fmt: ReportFormatters): string {
  return fmt.formatNumber(Math.round(readNumber(rec, key) / 60));
}

function totalMinutes(result: Record<string, unknown>, key: string, fmt: ReportFormatters): string {
  return fmt.formatNumber(Math.round(readNumber(result, key) / 60));
}

export function buildShiftReportView(result: Record<string, unknown>, fmt: ReportFormatters): ReportView {
  return {
    summaryCards: [],
    columns: [
      { key: 'state', labelKey: 'op.reports.col.state' },
      { key: 'opened', labelKey: 'op.reports.col.opened' },
      { key: 'closed', labelKey: 'op.reports.col.closed' },
      { key: 'movements', labelKey: 'op.reports.col.movements' },
      { key: 'expected', labelKey: 'op.reports.col.expectedCash' },
      { key: 'counted', labelKey: 'op.reports.col.countedCash' },
      { key: 'difference', labelKey: 'op.reports.col.difference' }
    ],
    rows: rowsOf(result).map((r) => ({
      state: readString(r, 'state'),
      opened: dateOrDash(r, 'openedAtUtc', fmt),
      closed: dateOrDash(r, 'closedAtUtc', fmt),
      movements: money(r, 'cashMovementsTotal', fmt),
      expected: money(r, 'expectedCash', fmt),
      counted: money(r, 'countedCash', fmt),
      difference: money(r, 'difference', fmt)
    }))
  };
}

export function buildCashOperationReportView(result: Record<string, unknown>, fmt: ReportFormatters): ReportView {
  return {
    summaryCards: [
      { labelKey: 'op.reports.sum.cashIn', value: money(result, 'cashInTotal', fmt) },
      { labelKey: 'op.reports.sum.cashOut', value: money(result, 'cashOutTotal', fmt) },
      { labelKey: 'op.reports.sum.netCash', value: money(result, 'netCashTotal', fmt) }
    ],
    columns: [
      { key: 'source', labelKey: 'op.reports.col.source' },
      { key: 'opType', labelKey: 'op.reports.col.opType' },
      { key: 'impact', labelKey: 'op.reports.col.impact' },
      { key: 'reason', labelKey: 'op.reports.col.reason' },
      { key: 'created', labelKey: 'op.reports.col.created' }
    ],
    rows: rowsOf(result).map((r) => ({
      source: readString(r, 'sourceType'),
      opType: readString(r, 'operationType'),
      impact: money(r, 'cashImpact', fmt),
      reason: readString(r, 'reason'),
      created: dateOrDash(r, 'createdAtUtc', fmt)
    }))
  };
}

export function buildGameplayTimeReportView(result: Record<string, unknown>, fmt: ReportFormatters): ReportView {
  return {
    summaryCards: [
      { labelKey: 'op.reports.sum.duration', value: totalMinutes(result, 'totalDurationSeconds', fmt) },
      { labelKey: 'op.reports.sum.package', value: totalMinutes(result, 'totalPackageSeconds', fmt) },
      { labelKey: 'op.reports.sum.bonus', value: totalMinutes(result, 'totalBonusSeconds', fmt) },
      { labelKey: 'op.reports.sum.revenue', value: money(result, 'gameplayRevenueTotal', fmt) }
    ],
    columns: [
      { key: 'seat', labelKey: 'op.reports.col.seat' },
      { key: 'device', labelKey: 'op.reports.col.device' },
      { key: 'playerKind', labelKey: 'op.reports.col.playerKind' },
      { key: 'state', labelKey: 'op.reports.col.state' },
      { key: 'duration', labelKey: 'op.reports.col.duration' },
      { key: 'revenue', labelKey: 'op.reports.col.revenue' }
    ],
    rows: rowsOf(result).map((r) => ({
      seat: readString(r, 'seatId'),
      device: readString(r, 'deviceId'),
      playerKind: readString(r, 'playerKind'),
      state: readString(r, 'state'),
      duration: minutes(r, 'durationSeconds', fmt),
      revenue: money(r, 'gameplayRevenue', fmt)
    }))
  };
}

export function buildOperatorActionReportView(result: Record<string, unknown>, fmt: ReportFormatters): ReportView {
  return {
    summaryCards: [
      { labelKey: 'op.reports.sum.actions', value: fmt.formatNumber(readNumber(result, 'totalActionCount')) }
    ],
    columns: [
      { key: 'operator', labelKey: 'op.reports.col.operator' },
      { key: 'action', labelKey: 'op.reports.col.action' },
      { key: 'outcome', labelKey: 'op.reports.col.outcome' },
      { key: 'count', labelKey: 'op.reports.col.count' },
      { key: 'first', labelKey: 'op.reports.col.first' },
      { key: 'last', labelKey: 'op.reports.col.last' }
    ],
    rows: rowsOf(result).map((r) => ({
      operator: readString(r, 'actorDisplayName'),
      action: readString(r, 'action'),
      outcome: readString(r, 'outcome'),
      count: fmt.formatNumber(readNumber(r, 'count')),
      first: dateOrDash(r, 'firstAtUtc', fmt),
      last: dateOrDash(r, 'lastAtUtc', fmt)
    }))
  };
}
