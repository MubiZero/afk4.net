import { useState, type JSX } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen } from '../management/ManagementScreen';
import { downloadTextFile, formatMinorUnits } from '../operatorHelpers';
import { EmptyState } from '../operatorPrimitives';
import type { ReportShiftRowDto } from '../api/clients/reports';
import type { OperatorBackendContext } from '../operatorTypes';
import { ReportBody, ReportRangeControls } from './ReportRangeControls';
import { SkeletonLine } from '../LoadingSkeleton';
import { todayReportRange, toReportQuery, type ReportDateRange } from './reportRange';
import { createReportClients } from './reportClient';
import { useReportData } from './useReportData';

export function ShiftCashReport({ backend, currencyCode }: { backend: OperatorBackendContext | null; currencyCode: string }): JSX.Element {
  const { t, formatDate } = useI18n();
  const [range, setRange] = useState<ReportDateRange>(() => todayReportRange());
  const [chosenId, setChosenId] = useState<string | null>(null);
  const { state, data, refreshing, error, reload } = useReportData(
    backend ? () => createReportClients(backend).getWorkspaceShiftCash(backend.branchId, toReportQuery(range)) : null,
    [backend, range]
  );
  // Выбранная смена переживает смену периода, если она есть и в новом ответе; иначе — первая.
  const selectedId = data?.shifts.some((row) => row.shiftId === chosenId) ? chosenId : data?.shifts[0]?.shiftId ?? null;
  const selected: ReportShiftRowDto | undefined = data?.shifts.find((row) => row.shiftId === selectedId);
  async function exportCsv() {
    if (!backend) return;
    const reports = createReportClients(backend);
    const csv = await reports.exportWorkspaceShiftCash(backend.branchId, toReportQuery(range));
    downloadTextFile(`shifts-cash-${range.from}-${range.to}.csv`, csv);
  }
  return <ManagementScreen title={t('op.reports.shifts.title')} subtitle={t('op.reports.shifts.subtitle')} contentWidth="full" state={state} skeleton={<ShiftCashSkeleton />} failure={error} onRetry={reload}
    controls={<ReportRangeControls range={range} onChange={setRange} onRefresh={reload} onExport={() => void exportCsv()} refreshing={refreshing} />}>
    <ReportBody refreshing={refreshing}>{data ? <div className="reports-master-detail"><div className="reports-shift-list">{data.shifts.length ? data.shifts.map((shift) => <button key={shift.shiftId} type="button" className={shift.shiftId === selectedId ? 'active' : undefined} onClick={() => setChosenId(shift.shiftId)}><span>{formatDate(shift.openedAtUtc)}</span><strong>{shift.state === 'open' ? t('op.reports.shifts.open') : formatMinorUnits(shift.difference?.minorUnits ?? 0, shift.difference?.currencyCode ?? currencyCode)}</strong></button>) : <EmptyState inline title={t('op.reports.empty')} next={{ kind: 'elsewhere', hint: t('op.reports.emptyHint') }} />}</div>{selected ? <aside className="reports-shift-inspector"><h2>{t('op.reports.shifts.inspector')}</h2><dl><div><dt>{t('op.reports.col.expectedCash')}</dt><dd>{formatMinorUnits(selected.expectedCash.minorUnits, selected.expectedCash.currencyCode)}</dd></div><div><dt>{t('op.reports.col.countedCash')}</dt><dd>{selected.countedCash ? formatMinorUnits(selected.countedCash.minorUnits, selected.countedCash.currencyCode) : t('op.reports.shifts.provisional')}</dd></div><div><dt>{t('op.reports.col.difference')}</dt><dd>{selected.difference ? formatMinorUnits(selected.difference.minorUnits, selected.difference.currencyCode) : t('op.reports.shifts.provisional')}</dd></div></dl><h3>{t('op.reports.shifts.cashOperations')}</h3><ul>{data.cashOperations.filter((row) => row.shiftId === selected.shiftId).map((row) => <li key={row.operationId}><span>{row.reason || row.operationType}</span><strong>{formatMinorUnits(row.cashImpact.minorUnits, row.cashImpact.currencyCode)}</strong></li>)}</ul></aside> : null}</div> : null}</ReportBody>
  </ManagementScreen>;
}

// Список смен слева и карточка выбранной справа — в тех же блоках, что и настоящий отчёт.
function ShiftCashSkeleton(): JSX.Element {
  return (
    <div className="reports-master-detail" data-skeleton="list" aria-hidden="true">
      <div className="reports-shift-list">
        {Array.from({ length: 5 }, (_, row) => (
          <button key={row} type="button" tabIndex={-1}><span><SkeletonLine width="8em" /></span><strong><SkeletonLine width="5em" /></strong></button>
        ))}
      </div>
      <aside className="reports-shift-inspector">
        <h2><SkeletonLine width="9em" /></h2>
        <dl>
          {[0, 1, 2].map((row) => <div key={row}><dt><SkeletonLine width="9em" /></dt><dd><SkeletonLine width="5em" /></dd></div>)}
        </dl>
      </aside>
    </div>
  );
}
