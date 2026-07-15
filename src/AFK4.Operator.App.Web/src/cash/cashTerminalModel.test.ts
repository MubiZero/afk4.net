import { describe, expect, it } from 'bun:test';
import type { OperatorAuthSession } from '../authClient';
import {
  filterCashOperationRows,
  resolveRegisterSelection,
  visibleCashJournalSegments
} from './cashTerminalModel';

function session(permissions: string[]): OperatorAuthSession {
  return { permissions } as OperatorAuthSession;
}

describe('visibleCashJournalSegments', () => {
  it('receipt-only staff sees only receipts', () => {
    expect(visibleCashJournalSegments(session(['receipts.view']))).toEqual(['receipts']);
  });

  it('preserves terminal order for a fully permitted manager', () => {
    expect(visibleCashJournalSegments(session([
      'reports.view',
      'receipts.view',
      'billing.money_action.approve'
    ]))).toEqual(['ops', 'receipts', 'review']);
  });
});

describe('resolveRegisterSelection', () => {
  const rows = [{ operationId: 'a' }, { operationId: 'b' }];

  it('keeps an existing selection', () => {
    expect(resolveRegisterSelection(rows, 'b', 'operationId')).toBe('b');
  });

  it('falls back to the first visible row', () => {
    expect(resolveRegisterSelection(rows, 'missing', 'operationId')).toBe('a');
  });

  it('clears selection for an empty register', () => {
    expect(resolveRegisterSelection([], 'a', 'operationId')).toBe('');
  });
});

describe('filterCashOperationRows', () => {
  const rows = [
    { operationId: 'a', operationType: 'cash_in', reason: 'Разменный фонд' },
    { operationId: 'b', operationType: 'cash_out', reason: 'Инкассация' }
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
