import { useCallback, useEffect, useState, type JSX } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen } from '../management/ManagementScreen';
import { downloadTextFile, formatMinorUnits } from '../operatorHelpers';
import { projectOperatorError } from '../apiErrors';
import type { OrganizationAdminRevenueReportDto } from '../api/clients/reports';
import type { OperatorBackendContext } from '../operatorTypes';
import { ReportRangeControls } from './ReportRangeControls';
import { todayReportRange, toReportQuery, type ReportDateRange } from './reportRange';
import { createReportClients } from './reportClient';

export function RevenueReport({ backend }: { backend: OperatorBackendContext | null; currencyCode: string }): JSX.Element {
  const { t, formatNumber } = useI18n();
  const [range, setRange] = useState<ReportDateRange>(() => todayReportRange());
  const [data, setData] = useState<OrganizationAdminRevenueReportDto | null>(null);
  const [state, setState] = useState<'loading' | 'ready' | 'error'>('loading');
  const [error, setError] = useState('');
  const load = useCallback(async () => {
    if (!backend) { setState('error'); setError(t('op.reports.backendRequired')); return; }
    setState('loading');
    try {
      const reports = createReportClients(backend);
      const query = toReportQuery(range);
      setData(await reports.getWorkspaceRevenue(backend.branchId, query)); setState('ready');
    } catch (reason) { setError(projectOperatorError(reason, t).detail); setState('error'); }
  }, [backend, range, t]);
  useEffect(() => { void load(); }, [load]);
  async function exportCsv() {
    if (!backend) return;
    const reports = createReportClients(backend);
    const csv = await reports.exportWorkspaceRevenue(backend.branchId, toReportQuery(range));
    downloadTextFile(`revenue-${range.from}-${range.to}.csv`, csv);
  }
  return <ManagementScreen title={t('op.reports.revenue.title')} subtitle={t('op.reports.revenue.subtitle')} contentWidth="full" state={state} errorDetail={error} onRetry={() => void load()}>
    <ReportRangeControls range={range} onChange={setRange} onRefresh={() => void load()} onExport={() => void exportCsv()} />
    {data ? <div className="reports-revenue"><dl className="reports-figures"><div><dt>{t('op.reports.revenue.total')}</dt><dd>{formatMinorUnits(data.netRevenue.minorUnits, data.netRevenue.currencyCode)}</dd></div><div><dt>{t('op.reports.revenue.refunds')}</dt><dd>{formatMinorUnits(data.refunds.minorUnits, data.refunds.currencyCode)}</dd></div><div><dt>{t('op.reports.revenue.gameplayHours')}</dt><dd>{formatNumber(Math.round(data.gameplaySeconds / 360) / 10)}</dd></div></dl><section className="reports-comparison"><span>{t('op.reports.revenue.comparison')}</span><strong>{data.comparison.changePercent == null ? '—' : `${data.comparison.changePercent >= 0 ? '+' : ''}${formatNumber(data.comparison.changePercent)}%`}</strong><small>{formatMinorUnits(data.comparison.previousNetRevenue.minorUnits, data.comparison.previousNetRevenue.currencyCode)}</small></section><div className="reports-source-split">{data.sources.map((source) => <section key={source.source}><span>{source.source === 'gameplay' ? t('op.reports.revenue.gameplay') : t('op.reports.revenue.pos')}</span><strong>{formatMinorUnits(source.revenue.minorUnits, source.revenue.currencyCode)}</strong></section>)}</div><div className="reports-breakdowns"><Breakdown title={t('op.reports.revenue.paymentMethods')} rows={data.paymentMethods} /><Breakdown title={t('op.reports.revenue.operators')} rows={data.operators} /></div></div> : null}
  </ManagementScreen>;
}

function Breakdown({ title, rows }: { title: string; rows: Array<{ key: string; label: string; revenue: { currencyCode: string; minorUnits: number } }> }) {
  return <section><h2>{title}</h2>{rows.length ? <dl>{rows.map((row) => <div key={row.key}><dt>{row.label}</dt><dd>{formatMinorUnits(row.revenue.minorUnits, row.revenue.currencyCode)}</dd></div>)}</dl> : <p>—</p>}</section>;
}
