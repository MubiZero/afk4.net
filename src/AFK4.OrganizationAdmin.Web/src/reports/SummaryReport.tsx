import { useCallback, useEffect, useState, type JSX } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen } from '../management/ManagementScreen';
import { formatMinorUnits } from '../operatorHelpers';
import { projectOperatorError } from '../apiErrors';
import type { OrganizationAdminSummaryReportDto } from '../api/clients/reports';
import type { OperatorBackendContext, WorkspaceId } from '../operatorTypes';
import { ReportRangeControls } from './ReportRangeControls';
import { todayReportRange, toReportQuery, type ReportDateRange } from './reportRange';
import { createReportClients } from './reportClient';

// Валюта не приходит пропом: каждая сумма приезжает с сервера вместе со своей валютой,
// и брать её из соседнего места значило бы подписать чужие деньги знаком клуба.
export function SummaryReport({ backend, onNavigate }: { backend: OperatorBackendContext | null; onNavigate: (workspace: WorkspaceId) => void }): JSX.Element {
  const { t, formatDate } = useI18n();
  const [range, setRange] = useState<ReportDateRange>(() => todayReportRange());
  const [data, setData] = useState<OrganizationAdminSummaryReportDto | null>(null);
  const [state, setState] = useState<'loading' | 'ready' | 'error'>('loading');
  const [error, setError] = useState('');

  const load = useCallback(async () => {
    if (!backend) { setState('error'); setError(t('op.reports.backendRequired')); return; }
    setState('loading');
    try {
      const reports = createReportClients(backend);
      const query = toReportQuery(range);
      setData(await reports.getWorkspaceSummary(backend.branchId, query));
      setState('ready');
    } catch (reason) {
      setError(projectOperatorError(reason, t).detail);
      setState('error');
    }
  }, [backend, range, t]);

  useEffect(() => { void load(); }, [load]);
  return (
    <ManagementScreen title={t('op.reports.summary.title')} subtitle={t('op.reports.summary.subtitle')} contentWidth="full" state={state} errorDetail={error} onRetry={() => void load()}>
      <ReportRangeControls range={range} onChange={setRange} onRefresh={() => void load()} />
      {data ? <div className="reports-summary">
        <section className={`reports-day-state ${data.attentionTotalCount ? 'warning' : 'ok'}`}>
          <h2>{data.attentionTotalCount ? t('op.reports.summary.attention', { count: data.attentionTotalCount }) : t('op.reports.summary.ok')}</h2>
          {data.attentionItems.length ? <div className="reports-attention-list">{data.attentionItems.map((item) => <button key={`${item.kind}-${item.targetId}`} type="button" onClick={() => onNavigate('cash')}><span>{item.title}</span><strong>{item.amount ? formatMinorUnits(item.amount.minorUnits, item.amount.currencyCode) : item.detail}</strong></button>)}</div> : null}
          {data.attentionTotalCount > data.attentionItems.length ? <p>{t('op.reports.summary.more', { count: data.attentionTotalCount - data.attentionItems.length })}</p> : null}
        </section>
        <dl className="reports-figures">
          <div><dt>{t('op.reports.summary.netRevenue')}</dt><dd>{formatMinorUnits(data.figures.netRevenue.minorUnits, data.figures.netRevenue.currencyCode)}</dd></div>
          <div><dt>{t('op.reports.summary.gameplay')}</dt><dd>{formatMinorUnits(data.figures.gameplayRevenue.minorUnits, data.figures.gameplayRevenue.currencyCode)}</dd></div>
          <div><dt>{t('op.reports.summary.pos')}</dt><dd>{formatMinorUnits(data.figures.posNetSales.minorUnits, data.figures.posNetSales.currencyCode)}</dd></div>
        </dl>
        <section className="reports-trend"><div><strong>{t('op.reports.summary.trend')}</strong><span>{data.period.timeZone}</span></div><div className="reports-trend-points">{data.trend.map((point) => <div key={point.date}><span>{formatDate(`${point.date}T00:00:00Z`)}</span><strong>{formatMinorUnits(point.netRevenue.minorUnits, point.netRevenue.currencyCode)}</strong></div>)}</div></section>
        {data.activeShift ? <section className="reports-active-shift"><div><span>{t('op.reports.summary.provisional')}</span><strong>{t('op.reports.summary.activeShift')}</strong><small>{formatDate(data.activeShift.openedAtUtc)}</small></div><button type="button" className="ui-btn" onClick={() => onNavigate('cash')}>{t('op.reports.summary.openShift')}</button></section> : null}
      </div> : null}
    </ManagementScreen>
  );
}
