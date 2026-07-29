import { useCallback, useEffect, useState, type JSX } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen } from '../management/ManagementScreen';
import { downloadTextFile, formatMinorUnits } from '../operatorHelpers';
import { projectOperatorError } from '../apiErrors';
import type { DashboardReportSummaryDto, GameplayTimeReportResultDto, SalesReportResultDto } from '../api/clients/reports';
import type { OperatorBackendContext } from '../operatorTypes';
import { ReportRangeControls } from './ReportRangeControls';
import { todayReportRange, toReportQuery, type ReportDateRange } from './reportRange';
import { createReportClients } from './reportClient';

export function RevenueReport({ backend }: { backend: OperatorBackendContext | null; currencyCode: string }): JSX.Element {
  const { t, formatNumber } = useI18n();
  const [range, setRange] = useState<ReportDateRange>(() => todayReportRange());
  const [data, setData] = useState<{ summary: DashboardReportSummaryDto; sales: SalesReportResultDto; gameplay: GameplayTimeReportResultDto } | null>(null);
  const [state, setState] = useState<'loading' | 'ready' | 'error'>('loading');
  const [error, setError] = useState('');
  const load = useCallback(async () => {
    if (!backend) { setState('error'); setError(t('op.reports.backendRequired')); return; }
    setState('loading');
    try {
      const reports = createReportClients(backend);
      const query = toReportQuery(range);
      const [summary, sales, gameplay] = await Promise.all([reports.getSummary(backend.branchId, query), reports.getSales(backend.branchId, query), reports.getGameplay(backend.branchId, query)]);
      setData({ summary, sales, gameplay }); setState('ready');
    } catch (reason) { setError(projectOperatorError(reason, t).detail); setState('error'); }
  }, [backend, range, t]);
  useEffect(() => { void load(); }, [load]);
  async function exportCsv() {
    if (!backend) return;
    const reports = createReportClients(backend);
    const [sales, gameplay] = await Promise.all([reports.exportSales(backend.branchId, toReportQuery(range)), reports.exportGameplay(backend.branchId, toReportQuery(range))]);
    downloadTextFile(`revenue-${range.from}-${range.to}.csv`, `${sales}\r\n${gameplay}`);
  }
  return <ManagementScreen title={t('op.reports.revenue.title')} subtitle={t('op.reports.revenue.subtitle')} contentWidth="full" state={state} errorDetail={error} onRetry={() => void load()}>
    <ReportRangeControls range={range} onChange={setRange} onRefresh={() => void load()} onExport={() => void exportCsv()} />
    {data ? <div className="reports-revenue"><dl className="reports-figures"><div><dt>{t('op.reports.revenue.total')}</dt><dd>{formatMinorUnits(data.summary.revenue.totalRevenue.minorUnits, data.summary.revenue.totalRevenue.currencyCode)}</dd></div><div><dt>{t('op.reports.revenue.refunds')}</dt><dd>{formatMinorUnits(data.sales.refundsTotal.minorUnits, data.sales.refundsTotal.currencyCode)}</dd></div><div><dt>{t('op.reports.revenue.gameplayHours')}</dt><dd>{formatNumber(Math.round(data.gameplay.totalDurationSeconds / 360) / 10)}</dd></div></dl><div className="reports-source-split"><section><span>{t('op.reports.revenue.gameplay')}</span><strong>{formatMinorUnits(data.summary.revenue.gameplayRevenue.minorUnits, data.summary.revenue.gameplayRevenue.currencyCode)}</strong></section><section><span>{t('op.reports.revenue.pos')}</span><strong>{formatMinorUnits(data.summary.revenue.posNetSales.minorUnits, data.summary.revenue.posNetSales.currencyCode)}</strong></section></div></div> : null}
  </ManagementScreen>;
}
