import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Table, TableHeader, TableRow, TableHead, TableBody, TableCell } from '@/components/ui/table';
import { ErrorState, EmptyState } from '@/components/ui/states';
import { Loading, SkeletonCard, SkeletonTable } from '@/components/ui/skeletons';
import { useToast } from '@/components/ui/toast';
import { describeApiError } from '@/api/describeApiError';
import { useI18n } from '@/i18n/I18nProvider';
import { minorToMajor } from '@/lib/money';
import type { PlansApi } from '@/api/platformClients/plans';
import type { SubscriptionPlan } from '@/api/types';
import { usePlans } from './usePlans';
import { PlanFormDialog } from './PlanFormDialog';
import { BillingTermsCard } from './BillingTermsCard';
import { emptyPlanForm, planToForm, planFormToCreateRequest, planFormToUpdateRequest, INTERVAL_LABEL, type PlanForm } from './billingModel';

export function PlansTab({ client, canManage = true }: { client: PlansApi; canManage?: boolean }) {
  const { t, formatCurrency } = useI18n();
  const { toast } = useToast();
  const state = usePlans(client);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [mode, setMode] = useState<'create' | 'edit'>('create');
  const [form, setForm] = useState<PlanForm>(emptyPlanForm());
  const [pending, setPending] = useState(false);

  // Список функций платформы приходит с каждым тарифом — новому тарифу берём его у любого.
  function openCreate() {
    const features = state.status === 'ready' ? (state.data[0]?.features ?? []).map(({ featureKey, name }) => ({ featureKey, name })) : [];
    setMode('create');
    setForm(emptyPlanForm(features));
    setDialogOpen(true);
  }
  function openEdit(plan: SubscriptionPlan) { setMode('edit'); setForm(planToForm(plan)); setDialogOpen(true); }

  async function submit() {
    setPending(true);
    try {
      if (mode === 'create') {
        await client.createPlan(planFormToCreateRequest(form));
        toast({ title: t('platform.billing.planForm.created'), variant: 'success' });
      } else {
        await client.updatePlanCatalog(form.planCode, planFormToUpdateRequest(form));
        toast({ title: t('platform.billing.planForm.updated'), variant: 'success' });
      }
      setDialogOpen(false);
      if (state.status === 'ready') state.retry();
    } catch (cause) {
      toast({ title: describeApiError(cause, t), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  if (state.status === 'loading') return <Loading><SkeletonCard action={canManage}><SkeletonTable columns={5} /></SkeletonCard></Loading>;
  if (state.status === 'error') return <ErrorState message={state.message} retryLabel={state.canRetry ? t('state.retry') : undefined} onRetry={state.canRetry ? state.retry : undefined} />;

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('platform.billing.tab.plans')}</CardTitle>
        {canManage ? <Button onClick={openCreate}>{t('platform.billing.plans.create')}</Button> : null}
      </CardHeader>
      <CardContent>
        {state.data.length === 0 ? (
          <EmptyState
            message={t('platform.billing.empty.plans')}
            next={canManage
              ? { label: t('platform.billing.plans.createFirst'), onClick: openCreate }
              : { noPermission: t('state.empty.noPermission', { permission: t('platform.permission.billing.plans.manage') }) }}
          />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('platform.billing.column.plan')}</TableHead>
                <TableHead>{t('platform.billing.plans.column.price')}</TableHead>
                <TableHead>{t('platform.billing.plans.column.perDevice')}</TableHead>
                <TableHead>{t('platform.billing.plans.column.maxDevices')}</TableHead>
                <TableHead>{t('platform.billing.column.interval')}</TableHead>
                <TableHead>{t('platform.billing.plans.column.active')}</TableHead>
                <TableHead>{t('platform.billing.column.actions')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {state.data.map(plan => (
                <TableRow key={plan.planCode}>
                  <TableCell><span className="font-medium">{plan.name}</span> <code className="mgmt-drawer-hint">{plan.planCode}</code></TableCell>
                  <TableCell className="pc-num">{formatCurrency(minorToMajor(plan.priceMinorUnits), plan.currencyCode)}</TableCell>
                  <TableCell className="pc-num">
                    {(plan.pricePerDeviceMinorUnits ?? 0) > 0
                      ? t('platform.billing.plans.perDevice', {
                          price: formatCurrency(minorToMajor(plan.pricePerDeviceMinorUnits ?? 0), plan.currencyCode),
                          included: plan.includedDevices ?? 0
                        })
                      : '—'}
                  </TableCell>
                  <TableCell className="pc-num">{plan.maxDevices ?? '—'}</TableCell>
                  <TableCell>{INTERVAL_LABEL[plan.billingInterval] ? t(INTERVAL_LABEL[plan.billingInterval]) : plan.billingInterval}</TableCell>
                  {/* Точка и прочерк ничего не говорят ни человеку, ни зачитывающей экран программе:
                      скрытый тариф видно только по тому, что кружок другого цвета. */}
                  <TableCell>
                    {plan.isActive
                      ? <Badge variant="success">{t('platform.billing.plans.state.active')}</Badge>
                      : <Badge variant="outline">{t('platform.billing.plans.state.hidden')}</Badge>}
                  </TableCell>
                  <TableCell>{canManage ? <Button variant="outline" onClick={() => openEdit(plan)}>{t('platform.billing.plans.edit')}</Button> : null}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </CardContent>
      <PlanFormDialog open={dialogOpen} mode={mode} form={form} pending={pending} onChange={setForm} onSubmit={() => void submit()} onOpenChange={setDialogOpen} />
    </Card>
  );
}

/** Тарифы и условия оплаты — одна вкладка: и то и другое определяет, сколько и когда платит клуб. */
export function PlansAndTermsTab({ client, canManage = true }: { client: PlansApi; canManage?: boolean }) {
  return (
    <div className="pc-plans-stack">
      <PlansTab client={client} canManage={canManage} />
      <BillingTermsCard client={client} canManage={canManage} />
    </div>
  );
}
