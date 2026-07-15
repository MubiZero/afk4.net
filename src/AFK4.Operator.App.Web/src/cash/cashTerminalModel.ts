import type { OperatorAuthSession } from '../authClient';
import { hasAnyPermission, hasPermission, permissionNames } from '../operatorPermissions';
import { readString } from '../operatorHelpers';

export type CashJournalSegment = 'ops' | 'receipts' | 'review';

export function visibleCashJournalSegments(session: OperatorAuthSession | null): CashJournalSegment[] {
  const result: CashJournalSegment[] = [];
  if (hasAnyPermission(session, [permissionNames.viewReports, permissionNames.viewShift, permissionNames.manageShiftCash])) {
    result.push('ops');
  }
  if (hasAnyPermission(session, [permissionNames.viewReceipt, permissionNames.refundPosSale])) {
    result.push('receipts');
  }
  if (hasPermission(session, permissionNames.approveMoneyAction)) {
    result.push('review');
  }
  return result;
}

export function resolveRegisterSelection(
  rows: Record<string, unknown>[],
  selectedId: string,
  idKey: string
): string {
  if (rows.some((row) => readString(row, idKey) === selectedId)) {
    return selectedId;
  }
  return rows.length === 0 ? '' : readString(rows[0], idKey);
}

export function filterCashOperationRows(
  rows: Record<string, unknown>[],
  query: string,
  operationType: string
): Record<string, unknown>[] {
  const needle = query.trim().toLocaleLowerCase();
  return rows.filter((row) => {
    const type = readString(row, 'operationType');
    return (operationType === 'all' || type === operationType)
      && (needle === '' || `${type} ${readString(row, 'reason')}`.toLocaleLowerCase().includes(needle));
  });
}
