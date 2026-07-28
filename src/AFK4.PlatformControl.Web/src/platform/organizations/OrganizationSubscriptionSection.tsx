import { useEffect, useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Switch } from '@/components/ui/switch';
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select';
import { LoadingCards, ErrorState } from '@/components/ui/states';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import { minorToMajor } from '@/lib/money';
import type { SubscriptionsApi } from '@/api/platformClients/subscriptions';
import type { PlansApi } from '@/api/platformClients/plans';
import type { SubscriptionPlan, OrganizationSubscription } from '@/api/types';
import { SUBSCRIPTION_STATUS_LABEL } from '@/platform/billing/billingModel';

type Client = Pick<SubscriptionsApi, 'getSubscription' | 'updateSubscription'>;

const STATUS_OPTIONS = ['trial', 'active', 'past_due', 'cancelled'] as const;
const INTERVAL_OPTIONS = ['monthly', 'yearly'] as const;

export function OrganizationSubscriptionSection({ client, plans: plansApi, organizationId }: { client: Client; plans: Pick<PlansApi, 'listPlans'>; organizationId: string }) {
  const { t, formatCurrency, formatDate } = useI18n();
  const { toast } = useToast();
  const [tick, setTick] = useState(0);
  const [sub, setSub] = useState<OrganizationSubscription | null>(null);
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
    Promise.all([client.getSubscription(organizationId), plansApi.listPlans(true)])
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
      toast({ title: t('platform.organization.subscriptionForm.updated'), variant: 'success' });
    } catch {
      toast({ title: t('platform.organization.action.error'), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  if (error) return <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={() => setTick(n => n + 1)} />;
  if (sub === null) return <LoadingCards count={1} />;

  const dirty = planCode !== sub.planCode || interval !== sub.billingInterval || status !== sub.status || cancelAtPeriodEnd !== sub.cancelAtPeriodEnd;

  return (
    <Card>
      <CardHeader><CardTitle>{t('platform.organization.section.subscription')}</CardTitle></CardHeader>
      <CardContent className="flex flex-col gap-3 text-sm">
        <label className="block">
          <span className="mb-1 block text-muted-foreground">{t('platform.organization.subscriptionForm.plan')}</span>
          <Select value={planCode} onValueChange={setPlanCode}>
            <SelectTrigger aria-label={t('platform.organization.subscriptionForm.plan')}><SelectValue /></SelectTrigger>
            <SelectContent>
              {plans.map(p => <SelectItem key={p.planCode} value={p.planCode}>{p.name} ({formatCurrency(minorToMajor(p.priceMinorUnits), p.currencyCode)})</SelectItem>)}
            </SelectContent>
          </Select>
        </label>
        <label className="block">
          <span className="mb-1 block text-muted-foreground">{t('platform.organization.subscriptionForm.interval')}</span>
          <Select value={interval} onValueChange={setInterval}>
            <SelectTrigger aria-label={t('platform.organization.subscriptionForm.interval')}><SelectValue /></SelectTrigger>
            <SelectContent>
              {INTERVAL_OPTIONS.map(i => <SelectItem key={i} value={i}>{t(i === 'monthly' ? 'platform.billing.interval.monthly' : 'platform.billing.interval.yearly')}</SelectItem>)}
            </SelectContent>
          </Select>
        </label>
        <label className="block">
          <span className="mb-1 block text-muted-foreground">{t('platform.organization.subscriptionForm.status')}</span>
          <Select value={status} onValueChange={setStatus}>
            <SelectTrigger aria-label={t('platform.organization.subscriptionForm.status')}><SelectValue /></SelectTrigger>
            <SelectContent>
              {STATUS_OPTIONS.map(s => <SelectItem key={s} value={s}>{t(SUBSCRIPTION_STATUS_LABEL[s])}</SelectItem>)}
            </SelectContent>
          </Select>
        </label>
        <label className="flex items-center justify-between">
          <span className="text-muted-foreground">{t('platform.organization.subscriptionForm.cancelAtPeriodEnd')}</span>
          <Switch checked={cancelAtPeriodEnd} onCheckedChange={setCancelAtPeriodEnd} />
        </label>
        <div className="flex justify-between text-muted-foreground"><span>{t('platform.organization.subscriptionForm.amount')}</span><span className="tabular-nums">{formatCurrency(minorToMajor(sub.amountMinorUnits), sub.currencyCode)}</span></div>
        <div className="flex justify-between text-muted-foreground"><span>{t('platform.organization.subscriptionForm.currentPeriod')}</span><span>{formatDate(sub.currentPeriodStartUtc)} – {formatDate(sub.currentPeriodEndUtc)}</span></div>
        <div className="flex justify-between text-muted-foreground"><span>{t('platform.organization.subscriptionForm.nextInvoice')}</span><span>{sub.nextInvoiceUtc !== null ? formatDate(sub.nextInvoiceUtc) : '—'}</span></div>
        <div><Button onClick={() => void submit()} disabled={pending || !dirty}>{t('platform.organization.subscriptionForm.apply')}</Button></div>
      </CardContent>
    </Card>
  );
}
