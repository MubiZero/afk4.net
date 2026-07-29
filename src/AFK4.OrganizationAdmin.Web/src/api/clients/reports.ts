import { PlatformApiClient } from '../../platformApi';
import type { Guid, MoneyDto } from '../types';

export interface OrganizationAdminReportPeriodDto { fromDate: string; toDate: string; timeZone: string; fromUtc: string; toUtc: string }
export interface OrganizationAdminReportAttentionDto { kind: string; title: string; detail: string; targetId?: Guid | null; amount?: MoneyDto | null }
export interface OrganizationAdminReportFiguresDto { netRevenue: MoneyDto; gameplayRevenue: MoneyDto; posNetSales: MoneyDto; gameplaySeconds: number }
export interface OrganizationAdminRevenueTrendPointDto { date: string; netRevenue: MoneyDto }
export interface OrganizationAdminActiveShiftDto { shiftId: Guid; openedByStaffUserId: Guid; openedAtUtc: string; expectedCash: MoneyDto; isProvisional: boolean }
export interface OrganizationAdminSummaryReportDto { period: OrganizationAdminReportPeriodDto; attentionTotalCount: number; attentionItems: OrganizationAdminReportAttentionDto[]; figures: OrganizationAdminReportFiguresDto; trend: OrganizationAdminRevenueTrendPointDto[]; activeShift?: OrganizationAdminActiveShiftDto | null }
export interface OrganizationAdminShiftCashReportDto { period: OrganizationAdminReportPeriodDto; shifts: ReportShiftRowDto[]; cashOperations: CashOperationRowDto[]; cashInTotal: MoneyDto; cashOutTotal: MoneyDto; netCashTotal: MoneyDto }
export interface OrganizationAdminRevenueReportDto { period: OrganizationAdminReportPeriodDto; grossRevenue: MoneyDto; refunds: MoneyDto; netRevenue: MoneyDto; gameplayRevenue: MoneyDto; gameplaySeconds: number; posNetSales: MoneyDto; comparison: { previousNetRevenue: MoneyDto; differenceMinorUnits: number; changePercent?: number | null }; sources: Array<{ source: string; revenue: MoneyDto }>; paymentMethods: Array<{ key: string; label: string; revenue: MoneyDto }>; operators: Array<{ key: string; label: string; revenue: MoneyDto }> }
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
    getWorkspaceSummary: (branchId: Guid, query: OrganizationAdminReportQuery) => api.get<OrganizationAdminSummaryReportDto>(`branches/${branchId}/reports/workspace/summary`, query),
    getWorkspaceShiftCash: (branchId: Guid, query: OrganizationAdminReportQuery) => api.get<OrganizationAdminShiftCashReportDto>(`branches/${branchId}/reports/workspace/shifts-cash`, query),
    getWorkspaceRevenue: (branchId: Guid, query: OrganizationAdminReportQuery) => api.get<OrganizationAdminRevenueReportDto>(`branches/${branchId}/reports/workspace/revenue`, query),
    exportWorkspaceShiftCash: (branchId: Guid, query: OrganizationAdminReportQuery) => api.getText(`branches/${branchId}/reports/workspace/shifts-cash/export.csv`, query),
    exportWorkspaceRevenue: (branchId: Guid, query: OrganizationAdminReportQuery) => api.getText(`branches/${branchId}/reports/workspace/revenue/export.csv`, query)
  };
}
