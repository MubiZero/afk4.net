import { useEffect, useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Switch } from '@/components/ui/switch';
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select';
import { LoadingCards, ErrorState } from '@/components/ui/states';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { PlatformApiClient } from '@/api/platformApi';
import type { SubscriptionPlan, TenantSubscription } from '@/api/types';
import { SUBSCRIPTION_STATUS_LABEL } from '@/platform/billing/billingModel';

type Client = Pick<PlatformApiClient, 'getSubscription' | 'updateSubscription' | 'listPlans'>;

const STATUS_OPTIONS = ['trial', 'active', 'past_due', 'cancelled'] as const;
const INTERVAL_OPTIONS = ['monthly', 'yearly'] as const;

export function TenantSubscriptionSection({ client, organizationId }: { client: Client; organizationId: string }) {
  const { t, formatCurrency, formatDate } = useI18n();
  const { toast } = useToast();
  const [tick, setTick] = useState(0);
  const [sub, setSub] = useState<TenantSubscription | null>(null);
  const [plans, setPlans] = useState<SubscriptionPlan[]>([]);
  const [error, setError] = useState(false);
  const [pending, setPending] = useState(false);
  const [planCode, setPlanCode] = useState('');
  const [interval, setInterval] = useState('');
  const [status, setStatus] = useState('');
  const [cancelAtPeriodEnd, setCancelAtPeriodEnd] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setSub(null); setError(false);
    Promise.all([client.getSubscription(organizationId), client.listPlans(true)])
      .then(([s, p]) => {
        if (cancelled) return;
        setSub(s); setPlans(p);
        setPlanCode(s.planCode); setInterval(s.billingInterval); setStatus(s.status); setCancelAtPeriodEnd(s.cancelAtPeriodEnd);
      })
      .catch(() => { if (!cancelled) setError(true); });
    return () => { cancelled = true; };
  }, [client, organizationId, tick]);

  async function submit() {
    if (sub === null) return;
    setPending(true);
    try {
      const next = await client.updateSubscription(organizationId, {
        planCode: planCode !== sub.planCode ? planCode : null,
        billingInterval: interval !== sub.billingInterval ? interval : null,
        status: status !== sub.status ? status : null,
        cancelAtPeriodEnd: cancelAtPeriodEnd !== sub.cancelAtPeriodEnd ? cancelAtPeriodEnd : null
      });
      setSub(next);
      toast({ title: t('platform.tenant.subscriptionForm.updated'), variant: 'success' });
    } catch {
      toast({ title: t('platform.tenant.action.error'), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  if (error) return <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={() => setTick(n => n + 1)} />;
  if (sub === null) return <LoadingCards count={1} />;

  const dirty = planCode !== sub.planCode || interval !== sub.billingInterval || status !== sub.status || cancelAtPeriodEnd !== sub.cancelAtPeriodEnd;

  return (
    <Card>
      <CardHeader><CardTitle>{t('platform.tenant.section.subscription')}</CardTitle></CardHeader>
      <CardContent className="flex flex-col gap-3 text-sm">
        <label className="block">
          <span className="mb-1 block text-muted-foreground">{t('platform.tenant.subscriptionForm.plan')}</span>
          <Select value={planCode} onValueChange={setPlanCode}>
            <SelectTrigger aria-label={t('platform.tenant.subscriptionForm.plan')}><SelectValue /></SelectTrigger>
            <SelectContent>
              {plans.map(p => <SelectItem key={p.planCode} value={p.planCode}>{p.name} ({formatCurrency(p.priceMinorUnits, p.currencyCode)})</SelectItem>)}
            </SelectContent>
          </Select>
        </label>
        <label className="block">
          <span className="mb-1 block text-muted-foreground">{t('platform.tenant.subscriptionForm.interval')}</span>
          <Select value={interval} onValueChange={setInterval}>
            <SelectTrigger aria-label={t('platform.tenant.subscriptionForm.interval')}><SelectValue /></SelectTrigger>
            <SelectContent>
              {INTERVAL_OPTIONS.map(i => <SelectItem key={i} value={i}>{t(i === 'monthly' ? 'platform.billing.interval.monthly' : 'platform.billing.interval.yearly')}</SelectItem>)}
            </SelectContent>
          </Select>
        </label>
        <label className="block">
          <span className="mb-1 block text-muted-foreground">{t('platform.tenant.subscriptionForm.status')}</span>
          <Select value={status} onValueChange={setStatus}>
            <SelectTrigger aria-label={t('platform.tenant.subscriptionForm.status')}><SelectValue /></SelectTrigger>
            <SelectContent>
              {STATUS_OPTIONS.map(s => <SelectItem key={s} value={s}>{t(SUBSCRIPTION_STATUS_LABEL[s])}</SelectItem>)}
            </SelectContent>
          </Select>
        </label>
        <label className="flex items-center justify-between">
          <span className="text-muted-foreground">{t('platform.tenant.subscriptionForm.cancelAtPeriodEnd')}</span>
          <Switch checked={cancelAtPeriodEnd} onCheckedChange={setCancelAtPeriodEnd} />
        </label>
        <div className="flex justify-between text-muted-foreground"><span>{t('platform.tenant.subscriptionForm.amount')}</span><span className="tabular-nums">{formatCurrency(sub.amountMinorUnits, sub.currencyCode)}</span></div>
        <div className="flex justify-between text-muted-foreground"><span>{t('platform.tenant.subscriptionForm.currentPeriod')}</span><span>{formatDate(sub.currentPeriodStartUtc)} – {formatDate(sub.currentPeriodEndUtc)}</span></div>
        <div className="flex justify-between text-muted-foreground"><span>{t('platform.tenant.subscriptionForm.nextInvoice')}</span><span>{sub.nextInvoiceUtc !== null ? formatDate(sub.nextInvoiceUtc) : '—'}</span></div>
        <div><Button onClick={() => void submit()} disabled={pending || !dirty}>{t('platform.tenant.subscriptionForm.apply')}</Button></div>
      </CardContent>
    </Card>
  );
}
