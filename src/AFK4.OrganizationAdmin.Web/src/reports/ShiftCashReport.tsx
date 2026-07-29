import { useCallback, useEffect, useState, type JSX } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen } from '../management/ManagementScreen';
import { downloadTextFile, formatMinorUnits } from '../operatorHelpers';
import { projectOperatorError } from '../apiErrors';
import type { OrganizationAdminShiftCashReportDto, ReportShiftRowDto } from '../api/clients/reports';
import type { OperatorBackendContext } from '../operatorTypes';
import { ReportRangeControls } from './ReportRangeControls';
import { todayReportRange, toReportQuery, type ReportDateRange } from './reportRange';
import { createReportClients } from './reportClient';

export function ShiftCashReport({ backend, currencyCode }: { backend: OperatorBackendContext | null; currencyCode: string }): JSX.Element {
  const { t, formatDate } = useI18n();
  const [range, setRange] = useState<ReportDateRange>(() => todayReportRange());
  const [data, setData] = useState<OrganizationAdminShiftCashReportDto | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [state, setState] = useState<'loading' | 'ready' | 'error'>('loading');
  const [error, setError] = useState('');
  const load = useCallback(async () => {
    if (!backend) { setState('error'); setError(t('op.reports.backendRequired')); return; }
    setState('loading');
    try {
      const reports = createReportClients(backend);
      const query = toReportQuery(range);
      const result = await reports.getWorkspaceShiftCash(backend.branchId, query);
      setData(result);
      setSelectedId((current) => result.shifts.some((row) => row.shiftId === current) ? current : result.shifts[0]?.shiftId ?? null);
      setState('ready');
    } catch (reason) { setError(projectOperatorError(reason, t).detail); setState('error'); }
  }, [backend, range, t]);
  useEffect(() => { void load(); }, [load]);
  const selected: ReportShiftRowDto | undefined = data?.shifts.find((row) => row.shiftId === selectedId);
  async function exportCsv() {
    if (!backend) return;
    const reports = createReportClients(backend);
    const csv = await reports.exportWorkspaceShiftCash(backend.branchId, toReportQuery(range));
    downloadTextFile(`shifts-cash-${range.from}-${range.to}.csv`, csv);
  }
  return <ManagementScreen title={t('op.reports.shifts.title')} subtitle={t('op.reports.shifts.subtitle')} contentWidth="full" state={state} errorDetail={error} onRetry={() => void load()}>
    <ReportRangeControls range={range} onChange={setRange} onRefresh={() => void load()} onExport={() => void exportCsv()} />
    {data ? <div className="reports-master-detail"><div className="reports-shift-list">{data.shifts.length ? data.shifts.map((shift) => <button key={shift.shiftId} type="button" className={shift.shiftId === selectedId ? 'active' : undefined} onClick={() => setSelectedId(shift.shiftId)}><span>{formatDate(shift.openedAtUtc)}</span><strong>{shift.state === 'open' ? t('op.reports.shifts.open') : formatMinorUnits(shift.difference?.minorUnits ?? 0, shift.difference?.currencyCode ?? currencyCode)}</strong></button>) : <p>{t('op.reports.empty')}</p>}</div>{selected ? <aside className="reports-shift-inspector"><h2>{t('op.reports.shifts.inspector')}</h2><dl><div><dt>{t('op.reports.col.expectedCash')}</dt><dd>{formatMinorUnits(selected.expectedCash.minorUnits, selected.expectedCash.currencyCode)}</dd></div><div><dt>{t('op.reports.col.countedCash')}</dt><dd>{selected.countedCash ? formatMinorUnits(selected.countedCash.minorUnits, selected.countedCash.currencyCode) : t('op.reports.shifts.provisional')}</dd></div><div><dt>{t('op.reports.col.difference')}</dt><dd>{selected.difference ? formatMinorUnits(selected.difference.minorUnits, selected.difference.currencyCode) : t('op.reports.shifts.provisional')}</dd></div></dl><h3>{t('op.reports.shifts.cashOperations')}</h3><ul>{data.cashOperations.filter((row) => row.shiftId === selected.shiftId).map((row) => <li key={row.operationId}><span>{row.reason || row.operationType}</span><strong>{formatMinorUnits(row.cashImpact.minorUnits, row.cashImpact.currencyCode)}</strong></li>)}</ul></aside> : null}</div> : null}
  </ManagementScreen>;
}
