import { PlatformApiClient } from '../../platformApi';
import type { Guid, MoneyDto } from '../types';
import type {
  CreateReportScheduleRequest,
  OrganizationAdminRevenueReportDto,
  OrganizationAdminShiftCashReportDto,
  OrganizationAdminSummaryReportDto,
  ReportScheduleDto,
  UpdateReportScheduleRequest,
} from '@afk4/contracts';
export type {
  CreateReportScheduleRequest,
  OrganizationAdminActiveShiftDto,
  OrganizationAdminReportAttentionDto,
  OrganizationAdminReportFiguresDto,
  OrganizationAdminReportPeriodDto,
  OrganizationAdminRevenueReportDto,
  OrganizationAdminRevenueTrendPointDto,
  OrganizationAdminShiftCashReportDto,
  OrganizationAdminSummaryReportDto,
  ReportScheduleDto,
  UpdateReportScheduleRequest,
} from '@afk4/contracts';

export type OrganizationAdminReportQuery = Record<string, string> & { fromDate: string; toDate: string };

export interface ReportShiftRowDto {
  shiftId: Guid;
  openedByStaffUserId: Guid;
  closedByStaffUserId?: Guid | null;
  state: string;
  expectedCash: MoneyDto;
  countedCash?: MoneyDto | null;
  difference?: MoneyDto | null;
  cashMovementsTotal: MoneyDto;
  posCashPaymentsTotal: MoneyDto;
  posRefundsTotal: MoneyDto;
  billingCashImpactTotal: MoneyDto;
  openedAtUtc: string;
  closedAtUtc?: string | null;
}

export interface CashOperationRowDto { operationId: Guid; shiftId?: Guid | null; sourceType: string; operationType: string; cashImpact: MoneyDto; reason: string; createdAtUtc: string }

export function createReportsClient(api: PlatformApiClient) {
  return {
    listReportSchedules: (branchId: Guid) =>
      api.get<ReportScheduleDto[]>(`branches/${branchId}/report-schedules`),
    createReportSchedule: (branchId: Guid, request: CreateReportScheduleRequest) =>
      api.post<ReportScheduleDto, CreateReportScheduleRequest>(`branches/${branchId}/report-schedules`, request),
    updateReportSchedule: (branchId: Guid, scheduleId: Guid, request: UpdateReportScheduleRequest) =>
      api.patch<ReportScheduleDto, UpdateReportScheduleRequest>(`branches/${branchId}/report-schedules/${scheduleId}`, request),
    deleteReportSchedule: (branchId: Guid, scheduleId: Guid) =>
      api.delete<{ message: string }>(`branches/${branchId}/report-schedules/${scheduleId}`),
    getWorkspaceSummary: (branchId: Guid, query: OrganizationAdminReportQuery) => api.get<OrganizationAdminSummaryReportDto>(`branches/${branchId}/reports/workspace/summary`, query),
    getWorkspaceShiftCash: (branchId: Guid, query: OrganizationAdminReportQuery) => api.get<OrganizationAdminShiftCashReportDto>(`branches/${branchId}/reports/workspace/shifts-cash`, query),
    getWorkspaceRevenue: (branchId: Guid, query: OrganizationAdminReportQuery) => api.get<OrganizationAdminRevenueReportDto>(`branches/${branchId}/reports/workspace/revenue`, query),
    exportWorkspaceShiftCash: (branchId: Guid, query: OrganizationAdminReportQuery) => api.getText(`branches/${branchId}/reports/workspace/shifts-cash/export.csv`, query),
    exportWorkspaceRevenue: (branchId: Guid, query: OrganizationAdminReportQuery) => api.getText(`branches/${branchId}/reports/workspace/revenue/export.csv`, query)
  };
}
