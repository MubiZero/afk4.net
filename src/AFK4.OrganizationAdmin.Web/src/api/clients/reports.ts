import { PlatformApiClient } from '../../platformApi';
import type { Guid, MoneyDto, ReportQuery } from '../types';
import { normalizeReportQuery } from '../queryHelpers';

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

export interface ShiftReportResultDto { rows: ReportShiftRowDto[]; limit: number }
export interface CashOperationRowDto { operationId: Guid; shiftId?: Guid | null; sourceType: string; operationType: string; cashImpact: MoneyDto; reason: string; createdAtUtc: string }
export interface CashOperationReportResultDto { rows: CashOperationRowDto[]; limit: number; cashInTotal: MoneyDto; cashOutTotal: MoneyDto; netCashTotal: MoneyDto }
export interface SalesReportResultDto { rows: unknown[]; limit: number; grossSalesTotal: MoneyDto; refundsTotal: MoneyDto; netSalesTotal: MoneyDto }
export interface GameplayTimeReportResultDto { rows: unknown[]; limit: number; totalDurationSeconds: number; totalPackageSeconds: number; totalBonusSeconds: number; gameplayRevenueTotal: MoneyDto }
export interface DashboardReportSummaryDto {
  fromUtc: string;
  toUtc: string;
  generatedAtUtc: string;
  shift: { shiftId?: Guid | null; state: string; openedAtUtc?: string | null; openedByStaffUserId?: Guid | null; expectedCash: MoneyDto };
  revenue: { posNetSales: MoneyDto; gameplayRevenue: MoneyDto; totalRevenue: MoneyDto; posCheckCount: number; newPlayerCount: number };
}

export function createReportsClient(api: PlatformApiClient) {
  return {
    getSummary: (branchId: Guid, query?: ReportQuery) => api.get<DashboardReportSummaryDto>(`branches/${branchId}/dashboard/summary`, normalizeReportQuery(query)),
    getShifts: (branchId: Guid, query?: ReportQuery) => api.get<ShiftReportResultDto>(`branches/${branchId}/reports/shifts`, normalizeReportQuery(query)),
    getCashOperations: (branchId: Guid, query?: ReportQuery) => api.get<CashOperationReportResultDto>(`branches/${branchId}/reports/cash-operations`, normalizeReportQuery(query)),
    getSales: (branchId: Guid, query?: ReportQuery) => api.get<SalesReportResultDto>(`branches/${branchId}/reports/sales`, normalizeReportQuery(query)),
    getGameplay: (branchId: Guid, query?: ReportQuery) => api.get<GameplayTimeReportResultDto>(`branches/${branchId}/reports/gameplay-time`, normalizeReportQuery(query)),
    exportShifts: (branchId: Guid, query?: ReportQuery) => api.getText(`branches/${branchId}/reports/shifts/export.csv`, normalizeReportQuery(query)),
    exportCashOperations: (branchId: Guid, query?: ReportQuery) => api.getText(`branches/${branchId}/reports/cash-operations/export.csv`, normalizeReportQuery(query)),
    exportSales: (branchId: Guid, query?: ReportQuery) => api.getText(`branches/${branchId}/reports/sales/export.csv`, normalizeReportQuery(query)),
    exportGameplay: (branchId: Guid, query?: ReportQuery) => api.getText(`branches/${branchId}/reports/gameplay-time/export.csv`, normalizeReportQuery(query))
  };
}
