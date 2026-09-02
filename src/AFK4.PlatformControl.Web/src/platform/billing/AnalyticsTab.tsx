import {
  Bar,
  BarChart,
  CartesianGrid,
  Legend,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis
} from 'recharts';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { ErrorState, LoadingCards } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import { minorToMajor } from '@/lib/money';
import type { AnalyticsApi } from '@/api/platformClients/analytics';
import type { MessageKey } from '@/i18n/messages';
import { isEmpty, toRevenueSeries } from './analyticsModel';
import { useAnalytics } from './useAnalytics';

// Каталог допускает только заранее известные ключи (MessageKey), а сервер отдаёт номер месяца
// числом — статический массив вместо шаблонной строки держит доступ к каталогу типобезопасным.
const MONTH_LABEL_KEY: readonly MessageKey[] = [
  'platform.analytics.month.1', 'platform.analytics.month.2', 'platform.analytics.month.3',
  'platform.analytics.month.4', 'platform.analytics.month.5', 'platform.analytics.month.6',
  'platform.analytics.month.7', 'platform.analytics.month.8', 'platform.analytics.month.9',
  'platform.analytics.month.10', 'platform.analytics.month.11', 'platform.analytics.month.12'
];

export function AnalyticsTab({ client }: { client: Pick<AnalyticsApi, 'getOverview'> }) {
  const { t, formatCurrency } = useI18n();
  const state = useAnalytics(client);

  if (state.status === 'loading') return <LoadingCards count={3} />;
  if (state.status === 'error') return <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={state.retry} />;

  const overview = state.data;
  const monthLabel = (month: number) => t(MONTH_LABEL_KEY[month - 1] ?? MONTH_LABEL_KEY[0]);
  const revenueSeries = toRevenueSeries(overview.months, monthLabel);
  const movementSeries = overview.months.map(month => ({
    label: monthLabel(month.month),
    joined: month.joined,
    left: month.left,
    paying: month.payingAtMonthEnd
  }));
  const dataIsEmpty = isEmpty(overview);

  return (
    <div className="pc-analytics">
      <div className="pc-analytics-summary">
        <SummaryTile
          label={t('platform.analytics.summary.mrr')}
          value={formatCurrency(minorToMajor(overview.currentMrrMinorUnits), overview.currencyCode)}
        />
        <SummaryTile
          label={t('platform.analytics.summary.clubsLabel')}
          value={t('platform.analytics.summary.clubs', { count: overview.currentPayingClubs })}
        />
        <SummaryTile
          label={t('platform.analytics.summary.average')}
          value={formatCurrency(minorToMajor(overview.averageRevenuePerClubMinorUnits), overview.currencyCode)}
        />
        <SummaryTile
          label={t('platform.analytics.summary.outstanding')}
          value={formatCurrency(minorToMajor(overview.outstandingMinorUnits), overview.currencyCode)}
        />
      </div>

      {dataIsEmpty ? (
        <Card>
          <CardHeader>
            <CardTitle>{t('platform.analytics.revenue.title')}</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="pc-analytics-empty">{t('platform.analytics.empty')}</p>
          </CardContent>
        </Card>
      ) : (
        <>
          <Card>
            <CardHeader>
              <CardTitle>{t('platform.analytics.revenue.title')}</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="pc-analytics-chart">
                <ResponsiveContainer width="100%" height={260}>
                  <BarChart data={revenueSeries}>
                    <CartesianGrid strokeDasharray="3 3" stroke="var(--border-soft)" />
                    <XAxis dataKey="label" stroke="var(--text-tertiary)" fontSize={12} />
                    <YAxis stroke="var(--text-tertiary)" fontSize={12} />
                    <Tooltip
                      formatter={value => formatCurrency(Number(value), overview.currencyCode)}
                      contentStyle={{ background: 'var(--surface-elevated)', border: '1px solid var(--border-default)', borderRadius: 'var(--radius-sm)' }}
                    />
                    <Legend
                      formatter={(value: string) => value === 'recurring' ? t('platform.analytics.revenue.recurring') : t('platform.analytics.revenue.oneOff')}
                    />
                    <Bar dataKey="recurring" stackId="revenue" fill="var(--accent)" name="recurring" />
                    <Bar dataKey="oneOff" stackId="revenue" fill="var(--text-tertiary)" name="oneOff" />
                  </BarChart>
                </ResponsiveContainer>
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>{t('platform.analytics.movement.title')}</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="pc-analytics-chart">
                <ResponsiveContainer width="100%" height={220}>
                  <LineChart data={movementSeries}>
                    <CartesianGrid strokeDasharray="3 3" stroke="var(--border-soft)" />
                    <XAxis dataKey="label" stroke="var(--text-tertiary)" fontSize={12} />
                    <YAxis stroke="var(--text-tertiary)" fontSize={12} allowDecimals={false} />
                    <Tooltip contentStyle={{ background: 'var(--surface-elevated)', border: '1px solid var(--border-default)', borderRadius: 'var(--radius-sm)' }} />
                    <Legend
                      formatter={(value: string) =>
                        value === 'joined' ? t('platform.analytics.movement.joined')
                          : value === 'left' ? t('platform.analytics.movement.left')
                            : t('platform.analytics.movement.paying')}
                    />
                    <Line type="monotone" dataKey="joined" stroke="var(--success)" name="joined" />
                    <Line type="monotone" dataKey="left" stroke="var(--danger)" name="left" />
                    <Line type="monotone" dataKey="paying" stroke="var(--accent)" name="paying" />
                  </LineChart>
                </ResponsiveContainer>
              </div>
              {/* Честная оговорка: при первом включении фичи истории снимков ещё нет, поэтому в
                  первом месяце окна все платящие клубы выглядят «пришедшими». Это не рост, а
                  старт учёта — молча показывать вертикальный взлёт как факт нельзя. */}
              <p className="pc-analytics-footnote">{t('platform.analytics.movement.footnote')}</p>
            </CardContent>
          </Card>
        </>
      )}
    </div>
  );
}

function SummaryTile({ label, value }: { label: string; value: string }) {
  return (
    <div className="pc-analytics-tile">
      <span className="pc-analytics-tile-label">{label}</span>
      <span className="pc-analytics-tile-value ui-money">{value}</span>
    </div>
  );
}
