import { useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { ConfirmDialog } from '@/components/shared/ConfirmDialog';
import { ErrorState } from '@/components/ui/states';
import { Loading, SkeletonCard, SkeletonRows } from '@/components/ui/skeletons';
import { useToast } from '@/components/ui/toast';
import { describeApiError } from '@/api/describeApiError';
import { useAttemptKey } from '@/api/useAttemptKey';
import { useI18n } from '@/i18n/I18nProvider';
import { minorToMajor } from '@/lib/money';
import type { InvoicesApi } from '@/api/platformClients/invoices';
import type { InvoiceListItem } from '@/api/types';
import { useInvoices } from './useInvoices';
import { INVOICE_STATUS_LABEL, INVOICE_STATUS_VARIANT, queueTotals, selectPayableQueue } from './billingModel';

type Action = { kind: 'markPaid' | 'void'; invoice: InvoiceListItem };

// Очередь работы — первое, что видно в разделе «Деньги». Раньше экран открывался реестром
// подписок: он отвечал на вопрос «что вообще есть», а не на рабочий «кто не заплатил».
export function PayableQueue({ client, canManage }: { client: InvoicesApi; canManage: boolean }) {
  const { t, formatCurrency, formatDate } = useI18n();
  const { toast } = useToast();
  const state = useInvoices(client);
  const [action, setAction] = useState<Action | null>(null);
  const [pending, setPending] = useState(false);
  const attempt = useAttemptKey();

  async function confirm(reason: string) {
    if (action === null) return;
    setPending(true);
    try {
      const key = attempt.forSubject({ action: action.kind, invoiceId: action.invoice.invoiceId, reason });
      if (action.kind === 'markPaid') {
        await client.markInvoicePaid(action.invoice.invoiceId, reason.length > 0 ? reason : null, key);
        toast({ title: t('platform.billing.markPaid.done'), variant: 'success' });
      } else {
        await client.voidInvoice(action.invoice.invoiceId, reason, key);
        toast({ title: t('platform.billing.void.done'), variant: 'success' });
      }
      attempt.done();
      setAction(null);
      if (state.status === 'ready') state.retry();
    } catch (cause) {
      toast({ title: describeApiError(cause, t), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  if (state.status === 'loading') {
    return (
      <Loading>
        <SkeletonCard description>
          <SkeletonRows as="ul" rows={3} className="pc-queue" rowClassName="pc-queue-row" trailing={canManage} />
        </SkeletonCard>
      </Loading>
    );
  }
  if (state.status === 'error') return <ErrorState message={state.message} retryLabel={state.canRetry ? t('state.retry') : undefined} onRetry={state.canRetry ? state.retry : undefined} />;

  const queue = selectPayableQueue(state.data);
  const totals = queueTotals(queue);

  return (
    <Card>
      <CardHeader>
        <div>
          <CardTitle>{t('platform.billing.queue.title')}</CardTitle>
          <CardDescription>
            {queue.length === 0
              ? t('platform.billing.queue.empty')
              : totals.map(total => formatCurrency(minorToMajor(total.amountMinorUnits), total.currencyCode)).join(' · ')}
          </CardDescription>
        </div>
        {queue.length > 0 ? <Badge variant="warning">{t('platform.billing.queue.count', { count: queue.length })}</Badge> : null}
      </CardHeader>

      {queue.length > 0 ? (
        <CardContent>
          <ul className="pc-queue">
            {queue.map(invoice => (
              <li key={invoice.invoiceId} className="pc-queue-row" data-status={invoice.status}>
                <span className="pc-queue-id">
                  <strong>{invoice.organizationName}</strong>
                  <span>#{invoice.number} · {t('platform.billing.queue.due', { date: formatDate(invoice.dueAtUtc) })}</span>
                </span>
                <Badge variant={INVOICE_STATUS_VARIANT[invoice.status] ?? 'outline'}>
                  {INVOICE_STATUS_LABEL[invoice.status] !== undefined ? t(INVOICE_STATUS_LABEL[invoice.status]) : invoice.status}
                </Badge>
                <span className="pc-queue-amount ui-money">{formatCurrency(minorToMajor(invoice.amountMinorUnits), invoice.currencyCode)}</span>
                {canManage ? (
                  <span className="pc-cell-actions">
                    <Button size="sm" onClick={() => setAction({ kind: 'markPaid', invoice })}>{t('platform.billing.action.markPaid')}</Button>
                    <Button variant="destructive" size="sm" onClick={() => setAction({ kind: 'void', invoice })}>{t('platform.billing.action.void')}</Button>
                  </span>
                ) : null}
              </li>
            ))}
          </ul>
        </CardContent>
      ) : null}

      <ConfirmDialog
        open={action !== null}
        title={action?.kind === 'void' ? t('platform.billing.void.title') : t('platform.billing.markPaid.title')}
        confirmLabel={action?.kind === 'void' ? t('platform.billing.void.confirm') : t('platform.billing.markPaid.confirm')}
        cancelLabel={t('platform.billing.action.cancel')}
        reasonLabel={action?.kind === 'void' ? t('platform.billing.void.reason') : t('platform.billing.markPaid.reference')}
        // Причина аннулирования уходит в журнал и потому обязательна; референс платежа подписан
        // «необязательно» — требовать его значит обещать одно, а делать другое.
        reasonRequired={action?.kind === 'void'}
        destructive={action?.kind === 'void'}
        pending={pending}
        onConfirm={reason => void confirm(reason)}
        onOpenChange={open => { if (!open) setAction(null); }}
      />
    </Card>
  );
}
