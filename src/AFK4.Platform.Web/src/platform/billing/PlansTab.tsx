import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Table, TableHeader, TableRow, TableHead, TableBody, TableCell } from '@/components/ui/table';
import { LoadingCards, ErrorState, EmptyState } from '@/components/ui/states';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import { minorToMajor } from '@/club/money';
import type { PlansApi } from '@/api/platformClients/plans';
import type { SubscriptionPlan } from '@/api/types';
import { usePlans } from './usePlans';
import { PlanFormDialog } from './PlanFormDialog';
import { emptyPlanForm, planToForm, planFormToCreateRequest, planFormToUpdateRequest, INTERVAL_LABEL, type PlanForm } from './billingModel';

export function PlansTab({ client }: { client: PlansApi }) {
  const { t, formatCurrency } = useI18n();
  const { toast } = useToast();
  const state = usePlans(client);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [mode, setMode] = useState<'create' | 'edit'>('create');
  const [form, setForm] = useState<PlanForm>(emptyPlanForm());
  const [pending, setPending] = useState(false);

  function openCreate() { setMode('create'); setForm(emptyPlanForm()); setDialogOpen(true); }
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
    } catch {
      toast({ title: t('platform.billing.action.error'), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  if (state.status === 'loading') return <LoadingCards count={2} />;
  if (state.status === 'error') return <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={state.retry} />;

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between">
        <CardTitle>{t('platform.billing.tab.plans')}</CardTitle>
        <Button onClick={openCreate}>{t('platform.billing.plans.create')}</Button>
      </CardHeader>
      <CardContent>
        {state.data.length === 0 ? (
          <EmptyState message={t('platform.billing.empty.plans')} />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('platform.billing.column.plan')}</TableHead>
                <TableHead>{t('platform.billing.plans.column.price')}</TableHead>
                <TableHead>{t('platform.billing.column.interval')}</TableHead>
                <TableHead>{t('platform.billing.plans.column.active')}</TableHead>
                <TableHead>{t('platform.billing.column.actions')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {state.data.map(plan => (
                <TableRow key={plan.planCode}>
                  <TableCell><span className="font-medium">{plan.name}</span> <code className="text-xs text-muted-foreground">{plan.planCode}</code></TableCell>
                  <TableCell className="tabular-nums">{formatCurrency(minorToMajor(plan.priceMinorUnits), plan.currencyCode)}</TableCell>
                  <TableCell>{INTERVAL_LABEL[plan.billingInterval] ? t(INTERVAL_LABEL[plan.billingInterval]) : plan.billingInterval}</TableCell>
                  <TableCell>{plan.isActive ? <Badge variant="success">●</Badge> : <Badge variant="outline">—</Badge>}</TableCell>
                  <TableCell><Button variant="outline" onClick={() => openEdit(plan)}>{t('platform.billing.plans.edit')}</Button></TableCell>
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
