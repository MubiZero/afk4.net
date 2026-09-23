import { useState, type JSX } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen } from '../management/ManagementScreen';
import { downloadTextFile, formatMinorUnits } from '../operatorHelpers';
import type { OperatorBackendContext } from '../operatorTypes';
import { ReportBody, ReportFiguresSkeleton, ReportRangeControls } from './ReportRangeControls';
import { SkeletonLine } from '../LoadingSkeleton';
import { todayReportRange, toReportQuery, type ReportDateRange } from './reportRange';
import { createReportClients } from './reportClient';
import { useReportData } from './useReportData';

export function RevenueReport({ backend }: { backend: OperatorBackendContext | null; currencyCode: string }): JSX.Element {
  const { t, formatNumber } = useI18n();
  const [range, setRange] = useState<ReportDateRange>(() => todayReportRange());
  const { state, data, refreshing, error, reload } = useReportData(
    backend ? () => createReportClients(backend).getWorkspaceRevenue(backend.branchId, toReportQuery(range)) : null,
    [backend, range]
  );
  async function exportCsv() {
    if (!backend) return;
    const reports = createReportClients(backend);
    const csv = await reports.exportWorkspaceRevenue(backend.branchId, toReportQuery(range));
    downloadTextFile(`revenue-${range.from}-${range.to}.csv`, csv);
  }
  return <ManagementScreen title={t('op.reports.revenue.title')} subtitle={t('op.reports.revenue.subtitle')} contentWidth="full" state={state} skeleton={<RevenueSkeleton />} failure={error} onRetry={reload}
    controls={<ReportRangeControls range={range} onChange={setRange} onRefresh={reload} onExport={() => void exportCsv()} refreshing={refreshing} />}>
    <ReportBody refreshing={refreshing}>{data ? <div className="reports-revenue"><dl className="reports-figures"><div><dt>{t('op.reports.revenue.total')}</dt><dd>{formatMinorUnits(data.netRevenue.minorUnits, data.netRevenue.currencyCode)}</dd></div><div><dt>{t('op.reports.revenue.refunds')}</dt><dd>{formatMinorUnits(data.refunds.minorUnits, data.refunds.currencyCode)}</dd></div><div><dt>{t('op.reports.revenue.gameplayHours')}</dt><dd>{formatNumber(Math.round(data.gameplaySeconds / 360) / 10)}</dd></div></dl><section className="reports-comparison"><span>{t('op.reports.revenue.comparison')}</span><strong>{data.comparison.changePercent == null ? '—' : `${data.comparison.changePercent >= 0 ? '+' : ''}${formatNumber(data.comparison.changePercent)}%`}</strong><small>{formatMinorUnits(data.comparison.previousNetRevenue.minorUnits, data.comparison.previousNetRevenue.currencyCode)}</small></section><div className="reports-source-split">{data.sources.map((source) => <section key={source.source}><span>{source.source === 'gameplay' ? t('op.reports.revenue.gameplay') : t('op.reports.revenue.pos')}</span><strong>{formatMinorUnits(source.revenue.minorUnits, source.revenue.currencyCode)}</strong></section>)}</div><div className="reports-breakdowns"><Breakdown title={t('op.reports.revenue.paymentMethods')} rows={data.paymentMethods} /><Breakdown title={t('op.reports.revenue.operators')} rows={data.operators} /></div></div> : null}</ReportBody>
  </ManagementScreen>;
}

function Breakdown({ title, rows }: { title: string; rows: Array<{ key: string; label: string; revenue: { currencyCode: string; minorUnits: number } }> }) {
  return <section><h2>{title}</h2>{rows.length ? <dl>{rows.map((row) => <div key={row.key}><dt>{row.label}</dt><dd>{formatMinorUnits(row.revenue.minorUnits, row.revenue.currencyCode)}</dd></div>)}</dl> : <p>—</p>}</section>;
}

// Цифры, сравнение, два источника и две разбивки — в тех же блоках, что и настоящий отчёт.
function RevenueSkeleton(): JSX.Element {
  return (
    <div className="reports-revenue" aria-hidden="true">
      <ReportFiguresSkeleton count={3} />
      <section className="reports-comparison"><span><SkeletonLine width="10em" /></span><strong><SkeletonLine width="4em" /></strong></section>
      <div className="reports-source-split">
        <section><span><SkeletonLine width="6em" /></span><strong><SkeletonLine width="5em" /></strong></section>
        <section><span><SkeletonLine width="6em" /></span><strong><SkeletonLine width="5em" /></strong></section>
      </div>
      <div className="reports-breakdowns">
        {[0, 1].map((breakdown) => (
          <section key={breakdown}>
            <h2><SkeletonLine width="9em" /></h2>
            <dl>
              {[0, 1, 2].map((row) => <div key={row}><dt><SkeletonLine width="8em" /></dt><dd><SkeletonLine width="5em" /></dd></div>)}
            </dl>
          </section>
        ))}
      </div>
    </div>
  );
}
