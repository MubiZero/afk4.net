import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { useI18n } from '@/i18n/I18nProvider';
import { minorToMajor } from '@/lib/money';
import type { MessageKey } from '@/i18n/messages';
import type { OrganizationMetricsState } from './useOrganizationMetrics';
import type { BillingMetricsState } from '@/platform/billing/useBillingMetrics';
import { AttentionQueue } from './AttentionQueue';
import { PartialFailure } from '@/components/ui/states';

const PLAN_LABEL: Record<string, MessageKey> = {
  starter: 'platform.plan.starter',
  growth: 'platform.plan.growth',
  scale: 'platform.plan.scale'
};

export function OverviewScreen({ state, billing }: { state: OrganizationMetricsState; billing?: BillingMetricsState }) {
  const { t, formatNumber, formatCurrency } = useI18n();

  if (state.status === 'loading') {
    return (
      <div data-testid="platform-overview-loading" className="grid grid-cols-1 gap-4 md:grid-cols-3 lg:grid-cols-6">
        {[0, 1, 2, 3, 4, 5].map(i => <Skeleton key={i} className="h-24 w-full rounded-lg" />)}
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

  const { kpis, byPlan, attention } = state.data;
  return (
    <div className="mx-auto flex w-full max-w-[1600px] flex-col gap-5">
      <div className="grid grid-cols-1 gap-5 xl:grid-cols-[minmax(0,2fr)_minmax(18rem,1fr)]">
        <Card className="min-w-0">
          <CardHeader><CardTitle>{t('platform.overview.attention.title')}</CardTitle></CardHeader>
          <CardContent><AttentionQueue rows={attention} /></CardContent>
        </Card>
        <Card>
          <CardHeader><CardTitle>{t('platform.overview.byPlan.title')}</CardTitle></CardHeader>
          <CardContent className="flex flex-col gap-2">
            {byPlan.map(p => <div key={p.planCode} className="flex items-center justify-between border-b border-border py-2 last:border-0"><span className="text-sm font-medium">{PLAN_LABEL[p.planCode] ? t(PLAN_LABEL[p.planCode]) : p.planCode}</span><span className="text-sm tabular-nums">{formatNumber(p.count)}</span></div>)}
          </CardContent>
        </Card>
      </div>
      <div className="grid grid-cols-2 overflow-hidden rounded-lg border border-border bg-card md:grid-cols-4">
        <CompactMetric label={t('platform.overview.kpi.organizations')} value={formatNumber(kpis.totalOrganizations)} />
        <CompactMetric label={t('platform.overview.kpi.active')} value={formatNumber(kpis.activeOrganizations)} />
        <CompactMetric label={t('platform.overview.kpi.suspended')} value={formatNumber(kpis.suspendedOrganizations)} />
        <CompactMetric label={t('platform.overview.kpi.branches')} value={formatNumber(kpis.totalBranches)} />
      </div>
      {billing !== undefined && billing.status === 'ready' ? <div className="grid grid-cols-1 gap-3 md:grid-cols-3"><Kpi label={t('platform.overview.kpi.mrr')} value={formatCurrency(minorToMajor(billing.data.mrrMinorUnits), billing.data.currencyCode)} /><Kpi label={t('platform.overview.kpi.outstanding')} value={formatCurrency(minorToMajor(billing.data.outstandingMinorUnits), billing.data.currencyCode)} /><Kpi label={t('platform.overview.kpi.overdue')} value={formatCurrency(minorToMajor(billing.data.overdueMinorUnits), billing.data.currencyCode)} /></div> : null}
      {billing !== undefined && billing.status === 'error' ? <PartialFailure title={t('platform.overview.billingUnavailable')} retryLabel={t('state.retry')} onRetry={billing.retry} /> : null}
    </div>
  );
}

function CompactMetric({ label, value }: { label: string; value: string }) {
  return <div className="border-b border-r border-border p-4 last:border-r-0 md:border-b-0"><div className="text-xs text-muted-foreground">{label}</div><div className="mt-1 text-xl font-bold tabular-nums">{value}</div></div>;
}

function Kpi({ label, value }: { label: string; value: string }) {
  return (
    <Card><CardContent className="py-4">
      <div className="text-xs font-medium text-muted">{label}</div>
      <div className="mt-2 text-2xl font-bold tabular-nums">{value}</div>
    </CardContent></Card>
  );
}
