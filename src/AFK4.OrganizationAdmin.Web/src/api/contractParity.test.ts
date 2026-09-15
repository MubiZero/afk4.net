import { describe, expect, it } from 'bun:test';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';

/**
 * Поля ответа сервера и поля клиентского типа — одно и то же множество.
 *
 * Генератора TS из C# в проекте нет, поэтому типы написаны руками и расходятся молча: так из
 * `ShopOrderDto` пропал `posSaleId`, и связь «заказ в баре → чек» была невидима оператору,
 * притом что сервер отдавал её с самого начала. Сверять две рукописные копии друг с другом
 * бессмысленно — читается исходник контрактов, и он здесь единственный источник правды.
 *
 * Список проверяемых типов неполон: часть клиентских DTO всё ещё объявлена как
 * `Record<string, unknown>` — контракта у них нет вовсе, и сверять там нечего. Каждый, кто
 * доводит такой тип до настоящего, добавляет его сюда.
 */
const repoRoot = join(import.meta.dir, '..', '..', '..');
const contractsRoot = join(repoRoot, 'AFK4.Shared.Contracts');

interface Pair {
  /// Имя записи в C#.
  record: string;
  /// Файл контракта относительно AFK4.Shared.Contracts.
  contract: string;
  /// Файл клиентского типа относительно этой папки.
  client: string;
  /// Имя интерфейса в TS.
  type: string;
}

const PAIRS: Pair[] = [
  { record: 'ShopOrderDto', contract: 'Shop/ShopOrderDto.cs', client: 'clients/shopOrders.ts', type: 'ShopOrderDto' },
  { record: 'ShopOrderLineDto', contract: 'Shop/ShopOrderLineDto.cs', client: 'clients/shopOrders.ts', type: 'ShopOrderLineDto' },
  { record: 'ShiftDto', contract: 'Shifts/ShiftDto.cs', client: 'clients/shifts.ts', type: 'ShiftDto' },
  { record: 'CashMovementDto', contract: 'Shifts/CashMovementDto.cs', client: 'clients/shifts.ts', type: 'CashMovementDto' },
  {
    record: 'DeviceInventoryItemDto',
    contract: 'Devices/DeviceInventoryItemDto.cs',
    client: 'clients/devices.ts',
    type: 'DeviceInventoryItemDto'
  },
  { record: 'ReservationDto', contract: 'Reservations/ReservationDto.cs', client: 'clients/reservations.ts', type: 'ReservationDto' },
  {
    record: 'ReservationSearchResultDto',
    contract: 'Reservations/ReservationDto.cs',
    client: 'clients/reservations.ts',
    type: 'ReservationSearchResultDto'
  },
  { record: 'StaffUserDto', contract: 'Identity/StaffUserDto.cs', client: 'clients/settings.ts', type: 'StaffUserDto' },
  { record: 'BranchProfileDto', contract: 'Branches/BranchProfileDto.cs', client: 'clients/settings.ts', type: 'BranchProfileDto' },
  { record: 'BranchPhotoDto', contract: 'Branches/BranchPhotoDto.cs', client: 'clients/settings.ts', type: 'BranchPhotoDto' },
  {
    record: 'BranchWorkingHoursDayDto',
    contract: 'Branches/BranchWorkingHoursDayDto.cs',
    client: 'clients/settings.ts',
    type: 'BranchWorkingHoursDay'
  },
  { record: 'ZoneDto', contract: 'Layout/ZoneDto.cs', client: 'clients/settings.ts', type: 'ZoneDto' },
  { record: 'SeatDto', contract: 'Layout/SeatDto.cs', client: 'clients/settings.ts', type: 'SeatDto' },
  { record: 'TariffDto', contract: 'Tariffs/TariffDto.cs', client: 'clients/settings.ts', type: 'TariffDto' },
  { record: 'TariffVersionDto', contract: 'Tariffs/TariffVersionDto.cs', client: 'clients/settings.ts', type: 'TariffVersionDto' },
  { record: 'TariffOptionDto', contract: 'Operator/TariffOptionDto.cs', client: 'clients/settings.ts', type: 'TariffOptionDto' },
  {
    record: 'PackageDefinitionDto',
    contract: 'Packages/PackageDefinitionDto.cs',
    client: 'clients/settings.ts',
    type: 'PackageDefinitionDto'
  },
  {
    record: 'DeviceSeatAssignmentDto',
    contract: 'Devices/DeviceSeatAssignmentDto.cs',
    client: 'clients/settings.ts',
    type: 'DeviceSeatAssignmentDto'
  },
  { record: 'StockMovementDto', contract: 'Inventory/StockMovementDto.cs', client: 'clients/inventory.ts', type: 'StockMovementDto' },
  { record: 'DeviceCommandDto', contract: 'Devices/DeviceCommandDto.cs', client: 'clients/devices.ts', type: 'DeviceCommandDto' },
  {
    record: 'DeviceCommandStatusDto',
    contract: 'Devices/DeviceCommandStatusDto.cs',
    client: 'clients/devices.ts',
    type: 'DeviceCommandStatusDto'
  },
  { record: 'DeviceDetailDto', contract: 'Devices/DeviceDetailDto.cs', client: 'clients/devices.ts', type: 'DeviceDetailDto' },
  {
    record: 'DeviceEnrollmentCodeDto',
    contract: 'Devices/DeviceEnrollmentCodeDto.cs',
    client: 'clients/devices.ts',
    type: 'DeviceEnrollmentCodeDto'
  },
  {
    record: 'RotateDeviceCredentialResponse',
    contract: 'Devices/RotateDeviceCredentialResponse.cs',
    client: 'clients/devices.ts',
    type: 'RotateDeviceCredentialResponse'
  },
  {
    record: 'RevokeDeviceCredentialResponse',
    contract: 'Devices/RevokeDeviceCredentialResponse.cs',
    client: 'clients/devices.ts',
    type: 'RevokeDeviceCredentialResponse'
  },
  {
    record: 'BranchDiagnosticsDto',
    contract: 'Diagnostics/BranchDiagnosticsDto.cs',
    client: 'clients/diagnostics.ts',
    type: 'BranchDiagnosticsDto'
  },
  {
    record: 'DeviceDiagnosticsSummaryDto',
    contract: 'Diagnostics/BranchDiagnosticsDto.cs',
    client: 'clients/diagnostics.ts',
    type: 'DeviceDiagnosticsSummaryDto'
  },
  {
    record: 'CommandDiagnosticsSummaryDto',
    contract: 'Diagnostics/BranchDiagnosticsDto.cs',
    client: 'clients/diagnostics.ts',
    type: 'CommandDiagnosticsSummaryDto'
  },
  {
    record: 'UpdateDiagnosticsSummaryDto',
    contract: 'Diagnostics/BranchDiagnosticsDto.cs',
    client: 'clients/diagnostics.ts',
    type: 'UpdateDiagnosticsSummaryDto'
  },
  {
    record: 'StaleDeviceDiagnosticsDto',
    contract: 'Diagnostics/BranchDiagnosticsDto.cs',
    client: 'clients/diagnostics.ts',
    type: 'StaleDeviceDiagnosticsDto'
  },
  {
    record: 'FailedCommandDiagnosticsDto',
    contract: 'Diagnostics/BranchDiagnosticsDto.cs',
    client: 'clients/diagnostics.ts',
    type: 'FailedCommandDiagnosticsDto'
  },
  {
    record: 'FailedUpdateDiagnosticsDto',
    contract: 'Diagnostics/BranchDiagnosticsDto.cs',
    client: 'clients/diagnostics.ts',
    type: 'FailedUpdateDiagnosticsDto'
  },
  {
    record: 'OperatorDashboardSummaryDto',
    contract: 'Dashboard/OperatorDashboardSummaryDto.cs',
    client: 'clients/dashboard.ts',
    type: 'OperatorDashboardSummaryDto'
  },
  {
    record: 'OperatorDashboardShiftSummaryDto',
    contract: 'Dashboard/OperatorDashboardSummaryDto.cs',
    client: 'clients/dashboard.ts',
    type: 'OperatorDashboardShiftSummaryDto'
  },
  {
    record: 'OperatorDashboardRevenueSummaryDto',
    contract: 'Dashboard/OperatorDashboardSummaryDto.cs',
    client: 'clients/dashboard.ts',
    type: 'OperatorDashboardRevenueSummaryDto'
  },
  {
    record: 'OperatorDashboardUtilizationSummaryDto',
    contract: 'Dashboard/OperatorDashboardSummaryDto.cs',
    client: 'clients/dashboard.ts',
    type: 'OperatorDashboardUtilizationSummaryDto'
  },
  {
    record: 'OperatorDashboardAlertPressureDto',
    contract: 'Dashboard/OperatorDashboardSummaryDto.cs',
    client: 'clients/dashboard.ts',
    type: 'OperatorDashboardAlertPressureDto'
  },
  {
    record: 'OperatorDashboardReservationSummaryDto',
    contract: 'Dashboard/OperatorDashboardSummaryDto.cs',
    client: 'clients/dashboard.ts',
    type: 'OperatorDashboardReservationSummaryDto'
  },
  {
    record: 'OperatorDashboardQueueItemDto',
    contract: 'Dashboard/OperatorDashboardSummaryDto.cs',
    client: 'clients/dashboard.ts',
    type: 'OperatorDashboardQueueItemDto'
  },
  {
    record: 'OperatorDashboardRecentPaymentDto',
    contract: 'Dashboard/OperatorDashboardSummaryDto.cs',
    client: 'clients/dashboard.ts',
    type: 'OperatorDashboardRecentPaymentDto'
  },
  { record: 'AuditSearchResultDto', contract: 'Audit/AuditSearchResultDto.cs', client: 'clients/audit.ts', type: 'AuditSearchResultDto' },
  { record: 'PosSaleDto', contract: 'Pos/PosSaleDto.cs', client: 'clients/pos.ts', type: 'PosSaleDto' },
  { record: 'PosSaleLineDto', contract: 'Pos/PosSaleLineDto.cs', client: 'clients/pos.ts', type: 'PosSaleLineDto' },
  { record: 'ReceiptDto', contract: 'Receipts/ReceiptDto.cs', client: 'clients/pos.ts', type: 'ReceiptDto' },
  {
    record: 'PosProductCategoryDto',
    contract: 'Pos/PosProductCategoryDto.cs',
    client: 'clients/pos.ts',
    type: 'PosProductCategoryDto'
  },
  {
    record: 'MoneyActionSubmitResponse',
    contract: 'Billing/MoneyActionContracts.cs',
    client: 'clients/moneyActions.ts',
    type: 'MoneyActionSubmitResponse'
  },
  {
    record: 'MoneyActionRequestDto',
    contract: 'Billing/MoneyActionContracts.cs',
    client: 'clients/moneyActions.ts',
    type: 'MoneyActionRequestDto'
  }
];

/** Имена параметров записи C# в том виде, в каком их отдаёт сериализатор: с маленькой буквы. */
function serverFields(source: string, record: string): string[] {
  const start = source.indexOf(`record ${record}(`);
  if (start < 0) throw new Error(`Запись ${record} не найдена в контракте.`);
  const open = source.indexOf('(', start);
  const close = source.indexOf(');', open);
  const body = source
    .slice(open + 1, close)
    // Комментарии внутри списка параметров — обычное дело в этих файлах.
    .replace(/\/\/[^\n]*/g, '')
    .replace(/\/\*[\s\S]*?\*\//g, '');

  const fields: string[] = [];
  // Тип может нести запятую внутри скобок (`IReadOnlyList<Guid>` не может, а `Dictionary<,>` да),
  // поэтому режем по запятым верхнего уровня.
  let depth = 0;
  let current = '';
  for (const character of `${body},`) {
    if (character === '<' || character === '(') depth += 1;
    if (character === '>' || character === ')') depth -= 1;
    if (character === ',' && depth === 0) {
      const parameter = current.trim();
      current = '';
      if (parameter.length === 0) continue;
      // «Тип Имя» или «Тип Имя = значение».
      const name = parameter.split('=')[0].trim().split(/\s+/).pop()!;
      fields.push(name.charAt(0).toLowerCase() + name.slice(1));
      continue;
    }
    current += character;
  }
  return fields;
}

/** Имена полей TS-интерфейса. */
function clientFields(source: string, type: string): string[] {
  const start = source.indexOf(`interface ${type} {`);
  if (start < 0) throw new Error(`Интерфейс ${type} не найден у клиента.`);
  const open = source.indexOf('{', start);
  let depth = 0;
  let end = open;
  for (let index = open; index < source.length; index += 1) {
    if (source[index] === '{') depth += 1;
    if (source[index] === '}') {
      depth -= 1;
      if (depth === 0) {
        end = index;
        break;
      }
    }
  }
  const body = source
    .slice(open + 1, end)
    .replace(/\/\/\/?[^\n]*/g, '')
    .replace(/\/\*[\s\S]*?\*\//g, '');
  return [...body.matchAll(/^\s*([a-zA-Z0-9_]+)\??\s*:/gm)].map((match) => match[1]);
}

describe('поля клиентских типов совпадают с контрактом сервера', () => {
  it('проверяет не пустой список типов', () => {
    expect(PAIRS.length).toBeGreaterThan(3);
  });

  for (const pair of PAIRS) {
    it(`${pair.type} несёт ровно те поля, что отдаёт ${pair.record}`, () => {
      const server = serverFields(readFileSync(join(contractsRoot, pair.contract), 'utf8'), pair.record);
      const client = clientFields(readFileSync(join(import.meta.dir, pair.client), 'utf8'), pair.type);

      expect(server.length).toBeGreaterThan(0);
      expect([...client].sort()).toEqual([...server].sort());
    });
  }
});
