import { useState } from 'react';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select';
import { Table, TableHeader, TableRow, TableHead, TableBody, TableCell } from '@/components/ui/table';
import { LoadingCards, ErrorState, EmptyState } from '@/components/ui/states';
import { ConfirmDialog } from '@/components/shared/ConfirmDialog';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { PlatformApiClient } from '@/api/platformApi';
import type { InvoiceListItem } from '@/api/types';
import { useInvoices } from './useInvoices';
import {
  filterInvoices, INVOICE_STATUS_VARIANT, INVOICE_STATUS_LABEL, INVOICE_KIND_LABEL, INVOICE_STATUS_FILTERS
} from './billingModel';

type Action = { kind: 'markPaid' | 'void'; invoice: InvoiceListItem };

export function InvoicesTab({ client }: { client: PlatformApiClient }) {
  const { t, formatCurrency, formatDate } = useI18n();
  const { toast } = useToast();
  const state = useInvoices(client);
  const [query, setQuery] = useState('');
  const [status, setStatus] = useState('all');
  const [action, setAction] = useState<Action | null>(null);
  const [pending, setPending] = useState(false);

  async function confirm(reason: string) {
    if (action === null) return;
    setPending(true);
    try {
      if (action.kind === 'markPaid') {
        await client.markInvoicePaid(action.invoice.invoiceId, reason.length > 0 ? reason : null);
        toast({ title: t('platform.billing.markPaid.done'), variant: 'success' });
      } else {
        await client.voidInvoice(action.invoice.invoiceId, reason);
        toast({ title: t('platform.billing.void.done'), variant: 'success' });
      }
      setAction(null);
      if (state.status === 'ready') state.retry();
    } catch {
      toast({ title: t('platform.billing.action.error'), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  if (state.status === 'loading') return <LoadingCards count={2} />;
  if (state.status === 'error') return <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={state.retry} />;

  const rows = filterInvoices(state.data, { query, status });
  const actionable = (s: string) => s === 'issued' || s === 'overdue';

  return (
    <Card>
      <CardContent className="flex flex-col gap-3 pt-6">
        <div className="flex flex-wrap gap-2">
          <Input className="max-w-xs" placeholder={t('platform.billing.search.placeholder')} value={query} onChange={e => setQuery(e.target.value)} />
          <Select value={status} onValueChange={setStatus}>
            <SelectTrigger className="max-w-[200px]" aria-label={t('platform.billing.column.status')}><SelectValue /></SelectTrigger>
            <SelectContent>
              {INVOICE_STATUS_FILTERS.map(s => (
                <SelectItem key={s} value={s}>{s === 'all' ? t('platform.billing.filter.allStatuses') : t(INVOICE_STATUS_LABEL[s])}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        {rows.length === 0 ? (
          <EmptyState message={t('platform.billing.empty.invoices')} />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('platform.billing.column.number')}</TableHead>
                <TableHead>{t('platform.billing.column.tenant')}</TableHead>
                <TableHead>{t('platform.billing.column.kind')}</TableHead>
                <TableHead>{t('platform.billing.column.amount')}</TableHead>
                <TableHead>{t('platform.billing.column.status')}</TableHead>
                <TableHead>{t('platform.billing.column.due')}</TableHead>
                <TableHead>{t('platform.billing.column.actions')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.map(r => (
                <TableRow key={r.invoiceId}>
                  <TableCell className="tabular-nums">#{r.number}</TableCell>
                  <TableCell><span className="font-medium">{r.organizationName}</span> <code className="text-xs text-muted-foreground">{r.organizationSlug}</code></TableCell>
                  <TableCell>{INVOICE_KIND_LABEL[r.kind] ? t(INVOICE_KIND_LABEL[r.kind]) : r.kind}</TableCell>
                  <TableCell className="tabular-nums">{formatCurrency(r.amountMinorUnits, r.currencyCode)}</TableCell>
                  <TableCell><Badge variant={INVOICE_STATUS_VARIANT[r.status] ?? 'outline'}>{INVOICE_STATUS_LABEL[r.status] ? t(INVOICE_STATUS_LABEL[r.status]) : r.status}</Badge></TableCell>
                  <TableCell>{formatDate(r.dueAtUtc)}</TableCell>
                  <TableCell className="flex gap-2">
                    {actionable(r.status) && (
                      <>
                        <Button variant="outline" onClick={() => setAction({ kind: 'markPaid', invoice: r })}>{t('platform.billing.action.markPaid')}</Button>
                        <Button variant="destructive" onClick={() => setAction({ kind: 'void', invoice: r })}>{t('platform.billing.action.void')}</Button>
                      </>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </CardContent>
      <ConfirmDialog
        open={action !== null}
        title={action?.kind === 'void' ? t('platform.billing.void.title') : t('platform.billing.markPaid.title')}
        confirmLabel={action?.kind === 'void' ? t('platform.billing.void.confirm') : t('platform.billing.markPaid.confirm')}
        cancelLabel={t('platform.billing.action.cancel')}
        reasonLabel={action?.kind === 'void' ? t('platform.billing.void.reason') : t('platform.billing.markPaid.reference')}
        destructive={action?.kind === 'void'}
        pending={pending}
        onConfirm={reason => void confirm(reason)}
        onOpenChange={open => { if (!open) setAction(null); }}
      />
    </Card>
  );
}
