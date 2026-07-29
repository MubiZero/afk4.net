import { addDays, toDateInputValue } from '../operatorHelpers';

export interface ReportDateRange { from: string; to: string }

export function todayReportRange(now = new Date()): ReportDateRange {
  const value = toDateInputValue(now);
  return { from: value, to: value };
}

export function toReportQuery(range: ReportDateRange, limit = 200) {
  const from = new Date(`${range.from}T00:00:00`);
  const inclusiveTo = new Date(`${range.to}T00:00:00`);
  return { fromUtc: from.toISOString(), toUtc: addDays(inclusiveTo, 1).toISOString(), limit };
}
