import { useCallback, useEffect, useState, type JSX } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen } from '../management/ManagementScreen';
import { formatMinorUnits } from '../operatorHelpers';
import { projectOperatorError } from '../apiErrors';
import type { DashboardReportSummaryDto, ShiftReportResultDto } from '../api/clients/reports';
import type { OperatorBackendContext, WorkspaceId } from '../operatorTypes';
import { ReportRangeControls } from './ReportRangeControls';
import { todayReportRange, toReportQuery, type ReportDateRange } from './reportRange';
import { createReportClients } from './reportClient';

export function SummaryReport({ backend, currencyCode, onNavigate }: { backend: OperatorBackendContext | null; currencyCode: string; onNavigate: (workspace: WorkspaceId) => void }): JSX.Element {
  const { t, formatDate } = useI18n();
  const [range, setRange] = useState<ReportDateRange>(() => todayReportRange());
  const [data, setData] = useState<{ summary: DashboardReportSummaryDto; shifts: ShiftReportResultDto } | null>(null);
  const [state, setState] = useState<'loading' | 'ready' | 'error'>('loading');
  const [error, setError] = useState('');

  const load = useCallback(async () => {
    if (!backend) { setState('error'); setError(t('op.reports.backendRequired')); return; }
    setState('loading');
    try {
      const reports = createReportClients(backend);
      const query = toReportQuery(range);
      const [summary, shifts] = await Promise.all([
        reports.getSummary(backend.branchId, query),
        reports.getShifts(backend.branchId, query)
      ]);
      setData({ summary, shifts });
      setState('ready');
    } catch (reason) {
      setError(projectOperatorError(reason, t).detail);
      setState('error');
    }
  }, [backend, range, t]);

  useEffect(() => { void load(); }, [load]);
  const attention = (data?.shifts.rows ?? []).filter((shift) => shift.state === 'closed' && (shift.difference?.minorUnits ?? 0) !== 0);
  const visibleAttention = attention.slice(0, 3);

  return (
    <ManagementScreen title={t('op.reports.summary.title')} subtitle={t('op.reports.summary.subtitle')} contentWidth="full" state={state} errorDetail={error} onRetry={() => void load()}>
      <ReportRangeControls range={range} onChange={setRange} onRefresh={() => void load()} />
      {data ? <div className="reports-summary">
        <section className={`reports-day-state ${attention.length ? 'warning' : 'ok'}`}>
          <h2>{attention.length ? t('op.reports.summary.attention', { count: attention.length }) : t('op.reports.summary.ok')}</h2>
          {visibleAttention.length ? <div className="reports-attention-list">{visibleAttention.map((shift) => <button key={shift.shiftId} type="button" onClick={() => onNavigate('cash')}><span>{formatDate(shift.closedAtUtc ?? shift.openedAtUtc)}</span><strong>{formatMinorUnits(shift.difference?.minorUnits ?? 0, shift.difference?.currencyCode ?? currencyCode)}</strong></button>)}</div> : null}
          {attention.length > 3 ? <p>{t('op.reports.summary.more', { count: attention.length - 3 })}</p> : null}
        </section>
        <dl className="reports-figures">
          <div><dt>{t('op.reports.summary.netRevenue')}</dt><dd>{formatMinorUnits(data.summary.revenue.totalRevenue.minorUnits, data.summary.revenue.totalRevenue.currencyCode)}</dd></div>
          <div><dt>{t('op.reports.summary.gameplay')}</dt><dd>{formatMinorUnits(data.summary.revenue.gameplayRevenue.minorUnits, data.summary.revenue.gameplayRevenue.currencyCode)}</dd></div>
          <div><dt>{t('op.reports.summary.pos')}</dt><dd>{formatMinorUnits(data.summary.revenue.posNetSales.minorUnits, data.summary.revenue.posNetSales.currencyCode)}</dd></div>
        </dl>
        {data.summary.shift.shiftId ? <section className="reports-active-shift"><div><span>{t('op.reports.summary.provisional')}</span><strong>{t('op.reports.summary.activeShift')}</strong><small>{data.summary.shift.openedAtUtc ? formatDate(data.summary.shift.openedAtUtc) : ''}</small></div><button type="button" className="ui-btn" onClick={() => onNavigate('cash')}>{t('op.reports.summary.openShift')}</button></section> : null}
      </div> : null}
    </ManagementScreen>
  );
}
