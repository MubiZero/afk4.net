import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { PlatformApiClient } from '@/api/platformApi';
import type { TenantDetail } from '@/api/types';
import { PLAN_OPTIONS, PLAN_LABEL, SUBSCRIPTION_OPTIONS, SUBSCRIPTION_LABEL } from './tenantsModel';

type Updater = Pick<PlatformApiClient, 'updatePlan'>;

interface Props {
  client: Updater;
  tenant: TenantDetail;
  onUpdated: (next: TenantDetail) => void;
}

export function TenantPlanSection({ client, tenant, onUpdated }: Props) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [planCode, setPlanCode] = useState(tenant.planCode);
  const [subscription, setSubscription] = useState(tenant.subscriptionStatus);
  const [pending, setPending] = useState(false);

  async function submit() {
    setPending(true);
    try {
      const next = await client.updatePlan(tenant.organizationId, planCode, subscription);
      onUpdated(next);
      toast({ title: t('platform.tenant.planForm.updated'), variant: 'success' });
    } catch {
      toast({ title: t('platform.tenant.action.error'), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  return (
    <Card>
      <CardHeader><CardTitle>{t('platform.tenant.section.plan')}</CardTitle></CardHeader>
      <CardContent className="flex flex-col gap-3">
        <label className="block text-sm">
          <span className="mb-1 block text-muted-foreground">{t('platform.tenant.planForm.plan')}</span>
          <Select value={planCode} onValueChange={setPlanCode}>
            <SelectTrigger aria-label={t('platform.tenant.planForm.plan')}><SelectValue /></SelectTrigger>
            <SelectContent>
              {PLAN_OPTIONS.map(p => <SelectItem key={p} value={p}>{t(PLAN_LABEL[p])}</SelectItem>)}
            </SelectContent>
          </Select>
        </label>
        <label className="block text-sm">
          <span className="mb-1 block text-muted-foreground">{t('platform.tenant.planForm.subscription')}</span>
          <Select value={subscription} onValueChange={setSubscription}>
            <SelectTrigger aria-label={t('platform.tenant.planForm.subscription')}><SelectValue /></SelectTrigger>
            <SelectContent>
              {SUBSCRIPTION_OPTIONS.map(s => <SelectItem key={s} value={s}>{t(SUBSCRIPTION_LABEL[s])}</SelectItem>)}
            </SelectContent>
          </Select>
        </label>
        <div>
          <Button onClick={() => void submit()} disabled={pending}>{t('platform.tenant.planForm.apply')}</Button>
        </div>
      </CardContent>
    </Card>
  );
}
