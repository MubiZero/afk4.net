import type { QueryParams } from '../platformApi';
import type { ReportQuery } from './types';

export function normalizeReportQuery<T extends ReportQuery>(query?: T): QueryParams | undefined {
  if (!query) {
    return undefined;
  }

  return {
    ...(query as unknown as QueryParams),
    fromUtc: query.fromUtc instanceof Date ? query.fromUtc.toISOString() : query.fromUtc,
    toUtc: query.toUtc instanceof Date ? query.toUtc.toISOString() : query.toUtc
  };
}

export function normalizeDeviceCommandQuery(query?: { limit?: number | null }): QueryParams | undefined {
  if (!query) {
    return undefined;
  }

  return { limit: query.limit };
}
