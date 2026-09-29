import type { CashOperationReportRowDto } from '../operatorApiClients';

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
