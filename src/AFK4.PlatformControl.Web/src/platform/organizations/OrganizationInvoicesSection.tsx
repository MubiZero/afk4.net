import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { ErrorState, EmptyState } from '@/components/ui/states';
import { Loading, SkeletonRows } from '@/components/ui/skeletons';
import { useToast } from '@/components/ui/toast';
import { describeApiError } from '@/api/describeApiError';
import { useAttemptKey } from '@/api/useAttemptKey';
import { useI18n } from '@/i18n/I18nProvider';
import { minorToMajor } from '@/lib/money';
import type { InvoicesApi } from '@/api/platformClients/invoices';
import type { Invoice } from '@/api/types';
import { INVOICE_STATUS_VARIANT, INVOICE_STATUS_LABEL } from '@/platform/billing/billingModel';
import { ConfirmDialog } from '@/components/shared/ConfirmDialog';
import { useLoadable } from '../useLoadable';
import { ManualInvoiceDialog } from './ManualInvoiceDialog';

type Client = Pick<
  InvoicesApi,
  'listOrganizationInvoices' | 'generateInvoice' | 'createInvoice' | 'markInvoicePaid' | 'voidInvoice'
>;

/// Что делают со счётом прямо здесь. Раньше карточка клиента показывала список без единой
/// кнопки: отметить оплаченным или аннулировать просроченный счёт можно было только уйдя в
/// «Деньги» и найдя там этот же клуб заново.
type InvoiceAction = { kind: 'markPaid' | 'void'; invoice: Invoice };

export function OrganizationInvoicesSection({ client, organizationId, canManage = true, canManageInvoices = false }: {
  client: Client;
  organizationId: string;
  canManage?: boolean;
  /// Отметить оплаченным и аннулировать сервер спрашивает по отдельному праву на счета, а не по
  /// общему «управлению деньгами».
  canManageInvoices?: boolean;
}) {
  const { t, formatCurrency, formatDate } = useI18n();
  const { toast } = useToast();
  const state = useLoadable(() => client.listOrganizationInvoices(organizationId), [organizationId]);
  const [pending, setPending] = useState(false);
  const [manualOpen, setManualOpen] = useState(false);
  const [action, setAction] = useState<InvoiceAction | null>(null);
  const attempt = useAttemptKey();

  async function generate() {
    setPending(true);
    try {
      await client.generateInvoice(organizationId, attempt.forSubject({ action: 'generate', organizationId }));
      attempt.done();
      toast({ title: t('platform.billing.generate.done'), variant: 'success' });
      state.retry();
    } catch (cause) {
      toast({ title: describeApiError(cause, t), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

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
      state.retry();
    } catch (cause) {
      toast({ title: describeApiError(cause, t), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  // Оплатить или аннулировать можно то, что ещё живо: у оплаченного и аннулированного счёта эти
  // кнопки только собирали бы отказ сервера.
  const actionable = (status: string) => status === 'issued' || status === 'overdue';

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('platform.organization.section.invoices')}</CardTitle>
        {canManage ? (
          <span className="pc-cell-actions">
            <Button variant="outline" disabled={pending} onClick={() => void generate()}>{t('platform.organization.invoices.generate')}</Button>
            <Button variant="outline" onClick={() => setManualOpen(true)}>{t('platform.organization.invoices.manual')}</Button>
          </span>
        ) : null}
      </CardHeader>
      <CardContent>
        {state.status === 'error' ? (
          <ErrorState message={state.message} retryLabel={state.canRetry ? t('state.retry') : undefined} onRetry={state.canRetry ? state.retry : undefined} />
        ) : state.status === 'loading' ? (
          <Loading><SkeletonRows rows={2} rowClassName="pc-list-row" trailing={canManageInvoices} /></Loading>
        ) : state.data.length === 0 ? (
          <EmptyState message={t('platform.organization.invoices.empty')} next="calm" />
        ) : (
          state.data.map(inv => (
            <div key={inv.invoiceId} className="pc-list-row">
              <span className="pc-num">#{inv.number} · {formatDate(inv.issuedAtUtc)}</span>
              <span className="pc-cell-actions">
                <span className="pc-num">{formatCurrency(minorToMajor(inv.amountMinorUnits), inv.currencyCode)}</span>
                <Badge variant={INVOICE_STATUS_VARIANT[inv.status] ?? 'outline'}>{INVOICE_STATUS_LABEL[inv.status] ? t(INVOICE_STATUS_LABEL[inv.status]) : inv.status}</Badge>
                {canManageInvoices && actionable(inv.status) ? (
                  <>
                    <Button variant="outline" size="sm" disabled={pending} onClick={() => setAction({ kind: 'markPaid', invoice: inv })}>
                      {t('platform.billing.action.markPaid')}
                    </Button>
                    <Button variant="destructive" size="sm" disabled={pending} onClick={() => setAction({ kind: 'void', invoice: inv })}>
                      {t('platform.billing.action.void')}
                    </Button>
                  </>
                ) : null}
              </span>
            </div>
          ))
        )}
      </CardContent>
      <ConfirmDialog
        open={action !== null}
        title={action?.kind === 'void' ? t('platform.billing.void.title') : t('platform.billing.markPaid.title')}
        confirmLabel={action?.kind === 'void' ? t('platform.billing.void.confirm') : t('platform.billing.markPaid.confirm')}
        cancelLabel={t('platform.billing.action.cancel')}
        reasonLabel={action?.kind === 'void' ? t('platform.billing.void.reason') : t('platform.billing.markPaid.reference')}
        // Причина аннулирования уходит в журнал и потому обязательна; референс платежа подписан
        // «необязательно» и требовать его нельзя.
        reasonRequired={action?.kind === 'void'}
        destructive={action?.kind === 'void'}
        pending={pending}
        onConfirm={reason => void confirm(reason)}
        onOpenChange={open => { if (!open) setAction(null); }}
      />
      {manualOpen ? (
        <ManualInvoiceDialog
          client={client}
          organizationId={organizationId}
          currencyCode={state.status === 'ready' ? state.data[0]?.currencyCode ?? null : null}
          onClose={() => setManualOpen(false)}
          onCreated={() => { setManualOpen(false); state.retry(); }}
        />
      ) : null}
    </Card>
  );
}
