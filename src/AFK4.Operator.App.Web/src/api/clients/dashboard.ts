import { PlatformApiClient } from '../../platformApi';
import type { Guid, ReportQuery } from '../types';
import { normalizeReportQuery } from '../queryHelpers';

export type OperatorDashboardSummaryDto = Record<string, unknown>;

export type DashboardSummaryQuery = ReportQuery;

export function createDashboardClient(api: PlatformApiClient) {
  return {
    getSummary(branchId: Guid, query?: DashboardSummaryQuery): Promise<OperatorDashboardSummaryDto> {
      return api.get<OperatorDashboardSummaryDto>(`branches/${branchId}/dashboard/summary`, normalizeReportQuery(query));
    }
  };
}
