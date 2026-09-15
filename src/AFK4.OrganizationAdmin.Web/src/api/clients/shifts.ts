import { PlatformApiClient } from '../../platformApi';
import type { Guid, MoneyDto, ReportQuery } from '../types';
import { normalizeReportQuery } from '../queryHelpers';

/**
 * Смена, как её отдаёт сервер (ShiftDto). Поля перечислены поимённо, а не спрятаны за
 * `Record<string, unknown>`: пока стояла заглушка, клиент не знал о смене ничего, и опечатка в
 * имени поля не отличалась от правильного имени ничем.
 *
 * Совпадение с сервером проверяется, а не обещается: см. `contractParity.test.ts`.
 */
export interface ShiftDto {
  shiftId: Guid;
  organizationId: Guid;
  branchId: Guid;
  openedByStaffUserId: Guid;
  closedByStaffUserId: Guid | null;
  state: string;
  startingCash: MoneyDto;
  countedCash: MoneyDto | null;
  expectedCash: MoneyDto | null;
  difference: MoneyDto | null;
  openingNote: string;
  closingNote: string;
  openedAtUtc: string;
  closedAtUtc: string | null;
  managerSignOffStaffUserId: Guid | null;
  signOffReason: string | null;
}

export interface CashMovementDto {
  cashMovementId: Guid;
  organizationId: Guid;
  branchId: Guid;
  shiftId: Guid;
  createdByStaffUserId: Guid;
  movementType: string;
  amount: MoneyDto;
  reason: string;
  createdAtUtc: string;
}
/*
 * Отчёты смены. Раньше все пять маршрутов были одним `ReportResultDto = Record<string, unknown>` —
 * именем, которого на сервере нет вовсе. У каждого отчёта свой ответ и своя строка, и все десять
 * стоят под проверкой паритета в `contractParity.test.ts`.
 */

export interface ShiftReportRowDto {
  shiftId: Guid;
  organizationId: Guid;
  branchId: Guid;
  openedByStaffUserId: Guid;
  closedByStaffUserId: Guid | null;
  state: string;
  startingCash: MoneyDto;
  cashMovementsTotal: MoneyDto;
  posCashPaymentsTotal: MoneyDto;
  posRefundsTotal: MoneyDto;
  billingCashImpactTotal: MoneyDto;
  expectedCash: MoneyDto;
  countedCash: MoneyDto | null;
  difference: MoneyDto | null;
  openedAtUtc: string;
  closedAtUtc: string | null;
}

export interface ShiftReportResultDto {
  rows: ShiftReportRowDto[];
  limit: number;
}

export interface SalesReportRowDto {
  posSaleId: Guid;
  organizationId: Guid;
  branchId: Guid;
  shiftId: Guid;
  createdByStaffUserId: Guid;
  state: string;
  total: MoneyDto;
  paidAmount: MoneyDto;
  refundAmount: MoneyDto;
  lineCount: number;
  itemQuantity: number;
  createdAtUtc: string;
  paidAtUtc: string | null;
  refundedAtUtc: string | null;
  voidedAtUtc: string | null;
  grossCostOfGoods: MoneyDto;
  refundedCostOfGoods: MoneyDto;
  netCostOfGoods: MoneyDto;
}

export interface SalesReportResultDto {
  rows: SalesReportRowDto[];
  limit: number;
  grossSalesTotal: MoneyDto;
  refundsTotal: MoneyDto;
  netSalesTotal: MoneyDto;
  grossCostOfGoodsTotal: MoneyDto;
  refundedCostOfGoodsTotal: MoneyDto;
  netCostOfGoodsTotal: MoneyDto;
}

export interface GameplayTimeReportRowDto {
  sessionId: Guid;
  organizationId: Guid;
  branchId: Guid;
  seatId: Guid;
  deviceId: Guid;
  createdByStaffUserId: Guid;
  playerKind: string;
  playerAccountId: Guid | null;
  state: string;
  durationSeconds: number;
  packageSeconds: number;
  bonusSeconds: number;
  gameplayRevenue: MoneyDto;
  startedAtUtc: string | null;
  endedAtUtc: string | null;
  endsAtUtc: string | null;
}

export interface GameplayTimeReportResultDto {
  rows: GameplayTimeReportRowDto[];
  limit: number;
  totalDurationSeconds: number;
  totalPackageSeconds: number;
  totalBonusSeconds: number;
  gameplayRevenueTotal: MoneyDto;
}

export interface CashOperationReportRowDto {
  operationId: Guid;
  organizationId: Guid;
  branchId: Guid;
  shiftId: Guid | null;
  createdByStaffUserId: Guid;
  sourceType: string;
  operationType: string;
  cashImpact: MoneyDto;
  reason: string;
  createdAtUtc: string;
  /** Кто провёл операцию — имя, а не идентификатор. */
  createdByDisplayName: string;
}

export interface CashOperationReportResultDto {
  rows: CashOperationReportRowDto[];
  limit: number;
  cashInTotal: MoneyDto;
  cashOutTotal: MoneyDto;
  netCashTotal: MoneyDto;
}

export interface OperatorActionReportRowDto {
  actorStaffUserId: Guid | null;
  actorDisplayName: string;
  action: string;
  outcome: string;
  count: number;
  firstAtUtc: string;
  lastAtUtc: string;
}

export interface OperatorActionReportResultDto {
  rows: OperatorActionReportRowDto[];
  limit: number;
  totalActionCount: number;
}

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
