import { PlatformApiClient } from '../../platformApi';
import type { Guid, ReportQuery } from '../types';
import { normalizeReportQuery } from '../queryHelpers';
import type {
  CashMovementDto,
  CashOperationReportResultDto,
  CloseShiftRequest,
  GameplayTimeReportResultDto,
  OpenShiftRequest,
  OperatorActionReportResultDto,
  RecordCashMovementRequest,
  SalesReportResultDto,
  ShiftDto,
  ShiftReportResultDto,
} from '@afk4/contracts';
export type {
  CashMovementDto,
  CashOperationReportResultDto,
  CashOperationReportRowDto,
  CloseShiftRequest,
  GameplayTimeReportResultDto,
  GameplayTimeReportRowDto,
  OpenShiftRequest,
  OperatorActionReportResultDto,
  OperatorActionReportRowDto,
  RecordCashMovementRequest,
  SalesReportResultDto,
  SalesReportRowDto,
  ShiftDto,
  ShiftReportResultDto,
  ShiftReportRowDto,
} from '@afk4/contracts';

/*
 * Отчёты смены. Раньше все пять маршрутов были одним `ReportResultDto = Record<string, unknown>` —
 * именем, которого на сервере нет вовсе. У каждого отчёта свой ответ и своя строка, и все десять
 * приезжают из `@afk4/contracts` — сверять больше нечего, форма одна.
 */

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
    getShiftReport(branchId: Guid, query?: ReportQuery): Promise<ShiftReportResultDto> {
      return api.get<ShiftReportResultDto>(`branches/${branchId}/reports/shifts`, normalizeReportQuery(query));
    },
    getSalesReport(branchId: Guid, query?: ReportQuery): Promise<SalesReportResultDto> {
      return api.get<SalesReportResultDto>(`branches/${branchId}/reports/sales`, normalizeReportQuery(query));
    },
    getGameplayTimeReport(branchId: Guid, query?: ReportQuery): Promise<GameplayTimeReportResultDto> {
      return api.get<GameplayTimeReportResultDto>(`branches/${branchId}/reports/gameplay-time`, normalizeReportQuery(query));
    },
    getCashOperationReport(branchId: Guid, query?: ReportQuery): Promise<CashOperationReportResultDto> {
      return api.get<CashOperationReportResultDto>(`branches/${branchId}/reports/cash-operations`, normalizeReportQuery(query));
    },
    getOperatorActionReport(branchId: Guid, query?: ReportQuery): Promise<OperatorActionReportResultDto> {
      return api.get<OperatorActionReportResultDto>(`branches/${branchId}/reports/operator-actions`, normalizeReportQuery(query));
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
