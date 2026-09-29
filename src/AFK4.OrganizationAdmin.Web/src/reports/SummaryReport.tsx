import { useState, type JSX } from 'react';
import { useI18n } from '@afk4/i18n';
import { formatDateParts } from '@afk4/formatting';
import { formatMinorUnits } from '../operatorHelpers';
import type { OperatorBackendContext, WorkspaceId } from '../operatorTypes';
import { ReportBody, ReportFiguresSkeleton, ReportRangeControls, ReportScreen } from './ReportRangeControls';
import { SkeletonLine } from '../LoadingSkeleton';
import { todayReportRange, toReportQuery, type ReportDateRange } from './reportRange';
import { createReportClients } from './reportClient';
import { useReportData } from './useReportData';

// Точка тренда несёт календарную дату (IsoDate), а не момент времени: разобрана и показана в UTC,
// без часового пояса зрителя, иначе дата "плывёт" на границе полуночи (23 сент. в поясе клуба
// становился 22 сент., 23:00 или 24 сент., 05:00 у зрителя — второй уже видел «завтра» с ярлыком
// «сегодня»). Год добавляется, только если отличается от текущего, — иначе он шум на каждой точке.
export function trendDayLabel(dateIso: string, locale: string): string {
  const date = new Date(`${dateIso}T00:00:00Z`);
  const showYear = date.getUTCFullYear() !== new Date().getUTCFullYear();
  return formatDateParts(date, locale, {
    day: 'numeric',
    month: 'short',
    ...(showYear ? { year: 'numeric' as const } : {}),
    timeZone: 'UTC'
  });
}

// Валюта не приходит пропом: каждая сумма приезжает с сервера вместе со своей валютой,
// и брать её из соседнего места значило бы подписать чужие деньги знаком клуба.
export function SummaryReport({ backend, onNavigate }: { backend: OperatorBackendContext | null; onNavigate: (workspace: WorkspaceId) => void }): JSX.Element {
  const { t, formatDate, locale } = useI18n();
  const [range, setRange] = useState<ReportDateRange>(() => todayReportRange());
  const { state, data, refreshing, error, reload } = useReportData(
    backend ? () => createReportClients(backend).getWorkspaceSummary(backend.branchId, toReportQuery(range)) : null,
    [backend, range]
  );
  return (
    <ReportScreen state={state} skeleton={<SummarySkeleton />} failure={error} onRetry={reload}
      controls={<ReportRangeControls range={range} onChange={setRange} onRefresh={reload} refreshing={refreshing} />}>
      <ReportBody refreshing={refreshing}>
        {data ? <div className="reports-summary">
          {/* Главное — выручка; что требует проверки — компактным списком под ней, а не крупным
              жёлтым заголовком громче денег. Деньги за игру подписаны как деньги: «Игровое время
              13 911,3 с.» читалось как время. Имена цифр — те же, что во вкладке «Выручка». */}
          <dl className="reports-figures">
            <div><dt>{t('op.reports.summary.netRevenue')}</dt><dd>{formatMinorUnits(data.figures.netRevenue.minorUnits, data.figures.netRevenue.currencyCode)}</dd></div>
            <div><dt>{t('op.reports.gameplay.revenue')}</dt><dd>{formatMinorUnits(data.figures.gameplayRevenue.minorUnits, data.figures.gameplayRevenue.currencyCode)}</dd></div>
            <div><dt>{t('op.reports.revenue.pos')}</dt><dd>{formatMinorUnits(data.figures.posNetSales.minorUnits, data.figures.posNetSales.currencyCode)}</dd></div>
          </dl>
          <section className={`reports-day-state ${data.attentionTotalCount ? 'warning' : 'ok'}`}>
            <h2>{data.attentionTotalCount ? t('op.reports.summary.attention', { count: data.attentionTotalCount }) : t('op.reports.summary.ok')}</h2>
            {data.attentionItems.length ? <div className="reports-attention-list">{data.attentionItems.map((item) => <button key={`${item.kind}-${item.targetId}`} type="button" onClick={() => onNavigate('cash')}><span>{item.title}</span><strong>{item.amount ? formatMinorUnits(item.amount.minorUnits, item.amount.currencyCode) : item.detail}</strong></button>)}</div> : null}
            {data.attentionTotalCount > data.attentionItems.length ? <p>{t('op.reports.summary.more', { count: data.attentionTotalCount - data.attentionItems.length })}</p> : null}
          </section>
          <section className="reports-trend"><div><strong>{t('op.reports.summary.trend')}</strong></div><div className="reports-trend-points">{data.trend.map((point) => <div key={point.date}><span>{trendDayLabel(point.date, locale)}</span><strong>{formatMinorUnits(point.netRevenue.minorUnits, point.netRevenue.currencyCode)}</strong></div>)}</div></section>
          {data.activeShift ? <section className="reports-active-shift"><div><span>{t('op.reports.summary.provisional')}</span><strong>{t('op.reports.summary.activeShift')}</strong><small>{formatDate(data.activeShift.openedAtUtc)}</small></div><button type="button" className="ui-btn" onClick={() => onNavigate('cash')}>{t('op.reports.summary.goToShift')}</button></section> : null}
        </div> : null}
      </ReportBody>
    </ReportScreen>
  );
}

// Итог дня, три цифры и неделя по дням — в тех же блоках, что и настоящая сводка.
function SummarySkeleton(): JSX.Element {
  return (
    <div className="reports-summary" aria-hidden="true">
      <ReportFiguresSkeleton count={3} />
      <section className="reports-day-state"><h2><SkeletonLine width="14em" /></h2></section>
      <section className="reports-trend">
        <div><strong><SkeletonLine width="8em" /></strong></div>
        <div className="reports-trend-points">
          {Array.from({ length: 7 }, (_, day) => (
            <div key={day}><span><SkeletonLine width="70%" /></span><strong><SkeletonLine width="50%" /></strong></div>
          ))}
        </div>
      </section>
    </div>
  );
}
