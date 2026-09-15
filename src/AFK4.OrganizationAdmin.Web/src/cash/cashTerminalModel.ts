import type { OperatorAuthSession } from '../authClient';
import { hasAnyPermission, hasPermission, permissionNames } from '../operatorPermissions';
import type { CashOperationReportRowDto } from '../operatorApiClients';

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

export function filterCashOperationRows(
  rows: readonly CashOperationReportRowDto[],
  query: string,
  operationType: string,
  operationTypeLabel: (operationType: string) => string = (value) => value
): CashOperationReportRowDto[] {
  const needle = query.trim().toLocaleLowerCase();
  return rows.filter((row) => {
    const type = row.operationType;
    return (operationType === 'all' || type === operationType)
      && (needle === '' || `${type} ${operationTypeLabel(type)} ${row.reason}`.toLocaleLowerCase().includes(needle));
  });
}
