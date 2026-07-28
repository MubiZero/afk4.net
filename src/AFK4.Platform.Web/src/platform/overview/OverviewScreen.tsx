import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { useI18n } from '@/i18n/I18nProvider';
import { minorToMajor } from '@/lib/money';
import type { MessageKey } from '@/i18n/messages';
import type { AttentionReason } from './metricsModel';
import type { TenantMetricsState } from './useTenantMetrics';
import type { BillingMetricsState } from '@/platform/billing/useBillingMetrics';

const ATTENTION_LABEL: Record<AttentionReason, MessageKey> = {
  suspended: 'platform.overview.attention.suspended',
  past_due: 'platform.overview.attention.pastDue'
};

const PLAN_LABEL: Record<string, MessageKey> = {
  starter: 'platform.plan.starter',
  growth: 'platform.plan.growth',
  scale: 'platform.plan.scale'
};

export function OverviewScreen({ state, billing }: { state: TenantMetricsState; billing?: BillingMetricsState }) {
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
    <div className="flex flex-col gap-4">
      <div className="grid grid-cols-1 gap-4 md:grid-cols-3 lg:grid-cols-6">
        <Kpi label={t('platform.overview.kpi.tenants')} value={formatNumber(kpis.totalTenants)} />
        <Kpi label={t('platform.overview.kpi.active')} value={formatNumber(kpis.activeTenants)} />
        <Kpi label={t('platform.overview.kpi.suspended')} value={formatNumber(kpis.suspendedTenants)} />
        <Kpi label={t('platform.overview.kpi.trial')} value={formatNumber(kpis.trialTenants)} />
        <Kpi label={t('platform.overview.kpi.branches')} value={formatNumber(kpis.totalBranches)} />
        <Kpi label={t('platform.overview.kpi.new30d')} value={formatNumber(kpis.newTenants30d)} />
      </div>

      {billing !== undefined && billing.status === 'ready' && (
        <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
          <Kpi label={t('platform.overview.kpi.mrr')} value={formatCurrency(minorToMajor(billing.data.mrrMinorUnits), billing.data.currencyCode)} />
          <Kpi label={t('platform.overview.kpi.outstanding')} value={formatCurrency(minorToMajor(billing.data.outstandingMinorUnits), billing.data.currencyCode)} />
          <Kpi label={t('platform.overview.kpi.overdue')} value={formatCurrency(minorToMajor(billing.data.overdueMinorUnits), billing.data.currencyCode)} />
        </div>
      )}

      <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
        <Card className="md:col-span-2">
          <CardHeader><CardTitle>{t('platform.overview.byPlan.title')}</CardTitle></CardHeader>
          <CardContent className="flex flex-col gap-2">
            {byPlan.map(p => (
              <div key={p.planCode} className="flex items-center justify-between border-b border-border py-2 last:border-0">
                <span className="text-sm font-medium">{PLAN_LABEL[p.planCode] ? t(PLAN_LABEL[p.planCode]) : p.planCode}</span>
                <span className="text-sm tabular-nums">{formatNumber(p.count)}</span>
              </div>
            ))}
          </CardContent>
        </Card>

        <Card>
          <CardHeader><CardTitle>{t('platform.overview.attention.title')}</CardTitle></CardHeader>
          <CardContent className="flex flex-col gap-2">
            {attention.length === 0 && <p className="text-sm text-muted">{t('platform.overview.attention.empty')}</p>}
            {attention.map(row => (
              <div key={row.organizationId} className="flex items-center justify-between border-b border-border py-2 last:border-0">
                <span className="text-sm font-medium">{row.name}</span>
                <Badge variant={row.reason === 'suspended' ? 'destructive' : 'secondary'}>{t(ATTENTION_LABEL[row.reason])}</Badge>
              </div>
            ))}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

function Kpi({ label, value }: { label: string; value: string }) {
  return (
    <Card><CardContent className="py-4">
      <div className="text-xs font-medium text-muted">{label}</div>
      <div className="mt-2 text-2xl font-bold tabular-nums">{value}</div>
    </CardContent></Card>
  );
}
