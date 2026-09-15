import { describe, expect, it } from 'bun:test';
import type { OperatorAuthSession } from '../authClient';
import type { CashOperationReportRowDto } from '../operatorApiClients';
import { cashOperationRow } from './cashFixtures';
import { filterCashOperationRows, visibleCashJournalSegments } from './cashTerminalModel';

function session(permissions: string[]): OperatorAuthSession {
  return { permissions } as OperatorAuthSession;
}

// Строка отчёта целиком, а не её огрызок: у функции теперь настоящий тип строки, и урезанный
// объект перестал бы собираться — как и должен, потому что сервер отдаёт все поля.
function row(operationId: string, operationType: string, reason: string): CashOperationReportRowDto {
  return cashOperationRow({ operationId, operationType, reason });
}

describe('visibleCashJournalSegments', () => {
  it('receipt-only staff sees only receipts', () => {
    expect(visibleCashJournalSegments(session(['organization.receipts.view']))).toEqual(['receipts']);
  });

  it('preserves terminal order for a fully permitted manager', () => {
    expect(visibleCashJournalSegments(session([
      'organization.reports.view',
      'organization.receipts.view',
      'organization.billing.money_action.approve'
    ]))).toEqual(['ops', 'receipts', 'review']);
  });
});

describe('filterCashOperationRows', () => {
  const rows = [
    row('a', 'cash_in', 'Разменный фонд'),
    row('b', 'cash_out', 'Инкассация')
  ];

  it('filters by normalized reason and exact operation type', () => {
    expect(filterCashOperationRows(rows, ' РАЗМЕН ', 'cash_in')).toEqual([rows[0]]);
    expect(filterCashOperationRows(rows, '', 'cash_out')).toEqual([rows[1]]);
  });

  it('keeps all rows for an empty query and all types', () => {
    expect(filterCashOperationRows(rows, '', 'all')).toEqual(rows);
  });

  it('can search by a localized operation label', () => {
    expect(filterCashOperationRows(rows, 'внесение', 'all', (type) => type === 'cash_in' ? 'Внесение' : 'Изъятие')).toEqual([rows[0]]);
  });
});
