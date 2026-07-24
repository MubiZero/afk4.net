import type { MessageKey } from '@afk4/i18n';
import { createAuthenticatedOperatorClients } from '../../operatorHelpers';
import type { DateRange } from '../../network/journal/dateRange';
import type { ReportFormatters, ReportView } from './reportModel';
import {
  buildCashOperationReportView,
  buildGameplayTimeReportView,
  buildOperatorActionReportView,
  buildShiftReportView
} from './reportModel';

export type OperatorClients = ReturnType<typeof createAuthenticatedOperatorClients>;
export type HistoryReportId = 'shifts' | 'cashOperations' | 'gameplayTime' | 'operatorActions';

export interface HistoryReportSpec {
  id: HistoryReportId;
  labelKey: MessageKey;
  load: (clients: OperatorClients, branchId: string, range: DateRange) => Promise<Record<string, unknown>>;
  build: (result: Record<string, unknown>, fmt: ReportFormatters) => ReportView;
  exportCsv: (clients: OperatorClients, branchId: string, range: DateRange) => Promise<string>;
  csvName: string;
}

const q = (range: DateRange) => ({ fromUtc: range.fromUtc, toUtc: range.toUtc });

export const historyReports: readonly HistoryReportSpec[] = [
  {
    id: 'shifts',
    labelKey: 'op.reports.history.tab.shifts',
    load: (c, b, r) => c.shifts.getShiftReport(b, q(r)),
    build: buildShiftReportView,
    exportCsv: (c, b, r) => c.shifts.exportShiftReportCsv(b, q(r)),
    csvName: 'shifts'
  },
  {
    id: 'cashOperations',
    labelKey: 'op.reports.history.tab.cash',
    load: (c, b, r) => c.shifts.getCashOperationReport(b, q(r)),
    build: buildCashOperationReportView,
    exportCsv: (c, b, r) => c.shifts.exportCashOperationReportCsv(b, q(r)),
    csvName: 'cash-operations'
  },
  {
    id: 'gameplayTime',
    labelKey: 'op.reports.history.tab.gameplay',
    load: (c, b, r) => c.shifts.getGameplayTimeReport(b, q(r)),
    build: buildGameplayTimeReportView,
    exportCsv: (c, b, r) => c.shifts.exportGameplayTimeReportCsv(b, q(r)),
    csvName: 'gameplay-time'
  },
  {
    id: 'operatorActions',
    labelKey: 'op.reports.history.tab.actions',
    load: (c, b, r) => c.shifts.getOperatorActionReport(b, q(r)),
    build: buildOperatorActionReportView,
    exportCsv: (c, b, r) => c.shifts.exportOperatorActionReportCsv(b, q(r)),
    csvName: 'operator-actions'
  }
];
