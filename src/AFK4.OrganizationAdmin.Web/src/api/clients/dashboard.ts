import { PlatformApiClient } from '../../platformApi';
import type { Guid, MoneyDto, ReportQuery } from '../types';
import { normalizeReportQuery } from '../queryHelpers';

/** Сводка стойки (OperatorDashboardSummaryDto). Поля сверяются в `contractParity.test.ts`. */
export interface OperatorDashboardSummaryDto {
  organizationId: Guid;
  branchId: Guid;
  fromUtc: string;
  toUtc: string;
  generatedAtUtc: string;
  shift: OperatorDashboardShiftSummaryDto;
  revenue: OperatorDashboardRevenueSummaryDto;
  utilization: OperatorDashboardUtilizationSummaryDto;
  alertPressure: OperatorDashboardAlertPressureDto;
  reservations: OperatorDashboardReservationSummaryDto;
  focusQueue: OperatorDashboardQueueItemDto[];
  recentPayments: OperatorDashboardRecentPaymentDto[];
}

export interface OperatorDashboardShiftSummaryDto {
  shiftId: Guid | null;
  state: string;
  openedAtUtc: string | null;
  openedByStaffUserId: Guid | null;
  expectedCash: MoneyDto;
}

export interface OperatorDashboardRevenueSummaryDto {
  posNetSales: MoneyDto;
  gameplayRevenue: MoneyDto;
  totalRevenue: MoneyDto;
  posCheckCount: number;
  newPlayerCount: number;
}

export interface OperatorDashboardUtilizationSummaryDto {
  totalSeats: number;
  activeSessions: number;
  endingSessions: number;
  onlineDevices: number;
  offlineDevices: number;
  sessionStarts: number;
  utilizationPercent: number;
}

export interface OperatorDashboardAlertPressureDto {
  pendingCommands: number;
  failedCommands: number;
  offlineDevices: number;
  endingSessions: number;
  totalAlerts: number;
}

export interface OperatorDashboardReservationSummaryDto {
  activeReservations: number;
  availableSlots: number;
  source: string;
}

export interface OperatorDashboardQueueItemDto {
  tone: string;
  target: string;
  title: string;
  detail: string;
  seatId: Guid | null;
  deviceId: Guid | null;
  createdAtUtc: string;
  sourceType: string;
}

export interface OperatorDashboardRecentPaymentDto {
  paymentId: Guid;
  posSaleId: Guid | null;
  shiftId: Guid;
  createdByStaffUserId: Guid;
  paymentKind: string;
  paymentMethod: string;
  amount: MoneyDto;
  createdAtUtc: string;
  sessionId?: Guid | null;
}

export type DashboardSummaryQuery = ReportQuery;

export function createDashboardClient(api: PlatformApiClient) {
  return {
    getSummary(branchId: Guid, query?: DashboardSummaryQuery): Promise<OperatorDashboardSummaryDto> {
      return api.get<OperatorDashboardSummaryDto>(`branches/${branchId}/dashboard/summary`, normalizeReportQuery(query));
    }
  };
}
