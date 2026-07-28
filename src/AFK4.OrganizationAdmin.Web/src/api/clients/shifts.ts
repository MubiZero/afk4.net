import { PlatformApiClient } from '../../platformApi';
import type { Guid, MoneyDto, ReportQuery } from '../types';
import { normalizeReportQuery } from '../queryHelpers';

export type ShiftDto = Record<string, unknown>;
export type CashMovementDto = Record<string, unknown>;
export type ReportResultDto = Record<string, unknown>;

export interface OpenShiftRequest {
  organizationId: Guid;
  startingCash: MoneyDto;
  openingNote: string;
  idempotencyKey: string;
}

export interface RecordCashMovementRequest {
  organizationId: Guid;
  movementType: string;
  amount: MoneyDto;
  reason: string;
  idempotencyKey: string;
}

export interface CloseShiftRequest {
  organizationId: Guid;
  countedCash: MoneyDto;
  closingNote: string;
  idempotencyKey: string;
}

export function createShiftClient(api: PlatformApiClient) {
  return {
    openShift(branchId: Guid, request: OpenShiftRequest): Promise<ShiftDto> {
      return api.post<ShiftDto, OpenShiftRequest>(`branches/${branchId}/shifts/open`, request);
    },
    getCurrentShift(branchId: Guid): Promise<ShiftDto | null> {
      return api.getOptional<ShiftDto>(`branches/${branchId}/shifts/current`);
    },
    recordCashMovement(shiftId: Guid, request: RecordCashMovementRequest): Promise<CashMovementDto> {
      return api.post<CashMovementDto, RecordCashMovementRequest>(`shifts/${shiftId}/cash-movements`, request);
    },
    closeShift(shiftId: Guid, request: CloseShiftRequest): Promise<ShiftDto> {
      return api.post<ShiftDto, CloseShiftRequest>(`shifts/${shiftId}/close`, request);
    },
    getShiftReport(branchId: Guid, query?: ReportQuery): Promise<ReportResultDto> {
      return api.get<ReportResultDto>(`branches/${branchId}/reports/shifts`, normalizeReportQuery(query));
    },
    getSalesReport(branchId: Guid, query?: ReportQuery): Promise<ReportResultDto> {
      return api.get<ReportResultDto>(`branches/${branchId}/reports/sales`, normalizeReportQuery(query));
    },
    getGameplayTimeReport(branchId: Guid, query?: ReportQuery): Promise<ReportResultDto> {
      return api.get<ReportResultDto>(`branches/${branchId}/reports/gameplay-time`, normalizeReportQuery(query));
    },
    getCashOperationReport(branchId: Guid, query?: ReportQuery): Promise<ReportResultDto> {
      return api.get<ReportResultDto>(`branches/${branchId}/reports/cash-operations`, normalizeReportQuery(query));
    },
    getOperatorActionReport(branchId: Guid, query?: ReportQuery): Promise<ReportResultDto> {
      return api.get<ReportResultDto>(`branches/${branchId}/reports/operator-actions`, normalizeReportQuery(query));
    },
    exportShiftReportCsv(branchId: Guid, query?: ReportQuery): Promise<string> {
      return api.getText(`branches/${branchId}/reports/shifts/export.csv`, normalizeReportQuery(query));
    },
    exportSalesReportCsv(branchId: Guid, query?: ReportQuery): Promise<string> {
      return api.getText(`branches/${branchId}/reports/sales/export.csv`, normalizeReportQuery(query));
    },
    exportGameplayTimeReportCsv(branchId: Guid, query?: ReportQuery): Promise<string> {
      return api.getText(`branches/${branchId}/reports/gameplay-time/export.csv`, normalizeReportQuery(query));
    },
    exportCashOperationReportCsv(branchId: Guid, query?: ReportQuery): Promise<string> {
      return api.getText(`branches/${branchId}/reports/cash-operations/export.csv`, normalizeReportQuery(query));
    },
    exportOperatorActionReportCsv(branchId: Guid, query?: ReportQuery): Promise<string> {
      return api.getText(`branches/${branchId}/reports/operator-actions/export.csv`, normalizeReportQuery(query));
    }
  };
}
