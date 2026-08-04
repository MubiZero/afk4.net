import { useState } from 'react';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Select } from '@/components/ui/select';
import { Table, TableHeader, TableRow, TableHead, TableBody, TableCell } from '@/components/ui/table';
import { LoadingCards, ErrorState, EmptyState } from '@/components/ui/states';
import { ConfirmDialog } from '@/components/shared/ConfirmDialog';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import { minorToMajor } from '@/lib/money';
import type { InvoicesApi } from '@/api/platformClients/invoices';
import type { InvoiceListItem } from '@/api/types';
import { useInvoices } from './useInvoices';
import {
  filterInvoices, INVOICE_STATUS_VARIANT, INVOICE_STATUS_LABEL, INVOICE_KIND_LABEL, INVOICE_STATUS_FILTERS
} from './billingModel';

type Action = { kind: 'markPaid' | 'void'; invoice: InvoiceListItem };

export function InvoicesTab({ client, canManage = true }: { client: InvoicesApi; canManage?: boolean }) {
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
  const actionable = (value: string) => value === 'issued' || value === 'overdue';

  return (
    <>
      <div className="pc-filters">
        <Input
          placeholder={t('platform.billing.search.placeholder')}
          aria-label={t('platform.billing.search.placeholder')}
          value={query}
          onChange={event => setQuery(event.target.value)}
        />
        <Select aria-label={t('platform.billing.column.status')} value={status} onChange={event => setStatus(event.target.value)}>
          {INVOICE_STATUS_FILTERS.map(value => (
            <option key={value} value={value}>
              {value === 'all' ? t('platform.billing.filter.allStatuses') : t(INVOICE_STATUS_LABEL[value])}
            </option>
          ))}
        </Select>
      </div>

      {rows.length === 0 ? (
        <EmptyState message={t('platform.billing.empty.invoices')} />
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('platform.billing.column.number')}</TableHead>
              <TableHead>{t('platform.billing.column.organization')}</TableHead>
              <TableHead>{t('platform.billing.column.kind')}</TableHead>
              <TableHead className="pc-num">{t('platform.billing.column.amount')}</TableHead>
              <TableHead>{t('platform.billing.column.status')}</TableHead>
              <TableHead>{t('platform.billing.column.due')}</TableHead>
              <TableHead>{t('platform.billing.column.actions')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {rows.map(invoice => (
              <TableRow key={invoice.invoiceId}>
                <TableCell className="pc-num">#{invoice.number}</TableCell>
                <TableCell>{invoice.organizationName}</TableCell>
                <TableCell>{INVOICE_KIND_LABEL[invoice.kind] !== undefined ? t(INVOICE_KIND_LABEL[invoice.kind]) : invoice.kind}</TableCell>
                <TableCell className="pc-num">{formatCurrency(minorToMajor(invoice.amountMinorUnits), invoice.currencyCode)}</TableCell>
                <TableCell>
                  <Badge variant={INVOICE_STATUS_VARIANT[invoice.status] ?? 'outline'}>
                    {INVOICE_STATUS_LABEL[invoice.status] !== undefined ? t(INVOICE_STATUS_LABEL[invoice.status]) : invoice.status}
                  </Badge>
                </TableCell>
                <TableCell>{formatDate(invoice.dueAtUtc)}</TableCell>
                <TableCell>
                  {canManage && actionable(invoice.status) ? (
                    <span className="pc-cell-actions">
                      <Button variant="outline" size="sm" onClick={() => setAction({ kind: 'markPaid', invoice })}>{t('platform.billing.action.markPaid')}</Button>
                      <Button variant="destructive" size="sm" onClick={() => setAction({ kind: 'void', invoice })}>{t('platform.billing.action.void')}</Button>
                    </span>
                  ) : null}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}

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
    </>
  );
}
