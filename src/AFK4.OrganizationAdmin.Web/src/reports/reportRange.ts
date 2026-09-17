import { toDateInputValue } from '../operatorHelpers';

export interface ReportDateRange { from: string; to: string }

export function todayReportRange(now = new Date()): ReportDateRange {
  const value = toDateInputValue(now);
  return { from: value, to: value };
}

export function toReportQuery(range: ReportDateRange) {
  return { fromDate: range.from, toDate: range.to };
}

/**
 * Тот же период, но мгновениями: подробные отчёты (время игры, действия сотрудников) спрашивают
 * fromUtc/toUtc, а не даты. Границы — сутки целиком, иначе отчёт за «сегодня» терял вечер.
 */
export function toReportInstantQuery(range: ReportDateRange) {
  return { fromUtc: `${range.from}T00:00:00.000Z`, toUtc: `${range.to}T23:59:59.999Z` };
}
