import { PlatformApiClient } from '../../platformApi';
import type { Guid, ReportQuery } from '../types';
import { normalizeReportQuery } from '../queryHelpers';
import type { OperatorDashboardSummaryDto } from '@afk4/contracts';
export type {
  OperatorDashboardAlertPressureDto,
  OperatorDashboardQueueItemDto,
  OperatorDashboardRecentPaymentDto,
  OperatorDashboardReservationSummaryDto,
  OperatorDashboardRevenueSummaryDto,
  OperatorDashboardShiftSummaryDto,
  OperatorDashboardSummaryDto,
  OperatorDashboardUtilizationSummaryDto,
} from '@afk4/contracts';

export type DashboardSummaryQuery = ReportQuery;

export function createDashboardClient(api: PlatformApiClient) {
  return {
    getSummary(branchId: Guid, query?: DashboardSummaryQuery): Promise<OperatorDashboardSummaryDto> {
      return api.get<OperatorDashboardSummaryDto>(`branches/${branchId}/dashboard/summary`, normalizeReportQuery(query));
    }
  };
}
