import type { CashOperationReportResultDto, CashOperationReportRowDto, ShiftDto } from '../operatorApiClients';

/**
 * Настоящие DTO для проверок кассы.
 *
 * Раньше проверки собирали огрызки — «строка операции» из трёх полей, «закрытая смена» из
 * `countedCash` и `difference`, — и экран под ними выглядел рабочим ровно до тех пор, пока сервер
 * не присылал остальное. Типы теперь настоящие, и огрызок не собирается: фикстура несёт весь
 * контракт, а проверка меняет в нём только то, что проверяет.
 */
export function cashOperationRow(overrides: Partial<CashOperationReportRowDto> = {}): CashOperationReportRowDto {
  return {
    operationId: 'operation-1',
    organizationId: 'org-1',
    branchId: 'branch-1',
    shiftId: 'shift-1',
    createdByStaffUserId: 'staff-1',
    sourceType: 'cash_movement',
    operationType: 'cash_in',
    cashImpact: { currencyCode: 'TJS', minorUnits: 1000 },
    reason: 'Разменный фонд',
    createdAtUtc: '2026-09-15T10:00:00Z',
    createdByDisplayName: 'Мадина',
    ...overrides
  };
}

export function cashOperationReport(rows: CashOperationReportRowDto[] = []): CashOperationReportResultDto {
  const sum = (sign: 1 | -1) => rows
    .filter((row) => Math.sign(row.cashImpact.minorUnits) === sign)
    .reduce((total, row) => total + row.cashImpact.minorUnits, 0);
  return {
    rows,
    limit: rows.length,
    cashInTotal: { currencyCode: 'TJS', minorUnits: sum(1) },
    cashOutTotal: { currencyCode: 'TJS', minorUnits: sum(-1) },
    netCashTotal: { currencyCode: 'TJS', minorUnits: sum(1) + sum(-1) }
  };
}

export function shiftDto(overrides: Partial<ShiftDto> = {}): ShiftDto {
  return {
    shiftId: 'shift-1',
    organizationId: 'org-1',
    branchId: 'branch-1',
    openedByStaffUserId: 'staff-1',
    closedByStaffUserId: null,
    state: 'open',
    startingCash: { currencyCode: 'TJS', minorUnits: 10_000 },
    countedCash: null,
    expectedCash: null,
    difference: null,
    openingNote: '',
    closingNote: '',
    openedAtUtc: '2026-09-15T08:00:00Z',
    closedAtUtc: null,
    managerSignOffStaffUserId: null,
    signOffReason: null,
    ...overrides
  };
}
