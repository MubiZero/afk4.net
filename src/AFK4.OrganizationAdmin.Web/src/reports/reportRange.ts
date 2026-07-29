import { toDateInputValue } from '../operatorHelpers';

export interface ReportDateRange { from: string; to: string }

export function todayReportRange(now = new Date()): ReportDateRange {
  const value = toDateInputValue(now);
  return { from: value, to: value };
}

export function toReportQuery(range: ReportDateRange) {
  return { fromDate: range.from, toDate: range.to };
}
