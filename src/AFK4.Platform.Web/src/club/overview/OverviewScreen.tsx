import { Cell, Pie, PieChart, ResponsiveContainer, Tooltip } from 'recharts';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { useI18n } from '@/i18n/I18nProvider';
import type { MessageKey } from '@/i18n/messages';
import type { AttentionKind } from './overviewModel';
import type { OverviewState } from './useOverview';

const ATTENTION_LABEL: Record<AttentionKind, MessageKey> = {
  offline: 'overview.attention.offline',
  failed: 'overview.attention.failed',
  pending: 'overview.attention.pending'
};
const SLICE_COLOR: Record<string, string> = { gameplay: 'var(--primary)', pos: 'var(--success)' };

export function OverviewScreen({ state }: { state: OverviewState }) {
  const { t, formatNumber, formatCurrency } = useI18n();

  if (state.status === 'loading') {
    return (
      <div data-testid="overview-loading" className="grid grid-cols-1 gap-4 md:grid-cols-4">
        {[0, 1, 2, 3].map(i => <Skeleton key={i} className="h-24 w-full rounded-lg" />)}
      </div>
    );
  }

  if (state.status === 'error') {
    return (
      <Card><CardContent className="flex flex-col items-center gap-3 py-10">
        <p className="text-muted">{t('state.error')}</p>
        <Button onClick={state.retry}>{t('state.retry')}</Button>
      </CardContent></Card>
    );
  }

  const { kpis, revenueBreakdown, attention } = state.data;
  return (
    <div className="flex flex-col gap-4">
      <div className="grid grid-cols-1 gap-4 md:grid-cols-4">
        <Kpi label={t('overview.kpi.devicesOnline')} value={`${formatNumber(kpis.devicesOnline.online)} / ${formatNumber(kpis.devicesOnline.total)}`} />
        <Kpi label={t('overview.kpi.activeSessions')} value={formatNumber(kpis.activeSessions)} sub={`${kpis.utilizationPercent}%`} />
        <Kpi label={t('overview.kpi.revenueToday')} value={formatCurrency(kpis.revenueToday.amount, kpis.revenueToday.currencyCode)} />
        <Kpi label={t('overview.kpi.attention')} value={formatNumber(kpis.attention)} />
      </div>

      <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
        <Card className="md:col-span-2">
          <CardHeader><CardTitle>{t('overview.revenue.title')}</CardTitle></CardHeader>
          <CardContent>
            <div className="h-48">
              <ResponsiveContainer width="100%" height="100%">
                <PieChart>
                  <Pie data={revenueBreakdown} dataKey="amount" nameKey="key" innerRadius={50} outerRadius={75}>
                    {revenueBreakdown.map(s => <Cell key={s.key} fill={SLICE_COLOR[s.key]} />)}
                  </Pie>
                  <Tooltip />
                </PieChart>
              </ResponsiveContainer>
            </div>
            <div className="mt-3 flex gap-4 text-sm">
              <span><b>{t('overview.revenue.gameplay')}:</b> {formatCurrency(revenueBreakdown[0].amount, kpis.revenueToday.currencyCode)}</span>
              <span><b>{t('overview.revenue.pos')}:</b> {formatCurrency(revenueBreakdown[1].amount, kpis.revenueToday.currencyCode)}</span>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader><CardTitle>{t('overview.attention.title')}</CardTitle></CardHeader>
          <CardContent className="flex flex-col gap-2">
            {attention.length === 0 && <p className="text-sm text-muted">{t('overview.attention.empty')}</p>}
            {attention.map(row => (
              <div key={row.deviceId} className="flex items-center justify-between border-b border-border py-2 last:border-0">
                <span className="text-sm font-medium">{row.name}</span>
                <Badge variant={row.kind === 'offline' ? 'destructive' : 'secondary'}>{t(ATTENTION_LABEL[row.kind])}</Badge>
              </div>
            ))}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

function Kpi({ label, value, sub }: { label: string; value: string; sub?: string }) {
  return (
    <Card><CardContent className="py-4">
      <div className="text-xs font-medium text-muted">{label}</div>
      <div className="mt-2 text-2xl font-bold tabular-nums">{value}</div>
      {sub && <div className="mt-1 text-xs text-muted">{sub}</div>}
    </CardContent></Card>
  );
}
