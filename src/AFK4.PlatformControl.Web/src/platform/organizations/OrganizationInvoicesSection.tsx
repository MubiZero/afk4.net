import { useEffect, useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { LoadingCards, ErrorState, EmptyState } from '@/components/ui/states';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import { minorToMajor } from '@/lib/money';
import type { InvoicesApi } from '@/api/platformClients/invoices';
import type { Invoice } from '@/api/types';
import { INVOICE_STATUS_VARIANT, INVOICE_STATUS_LABEL } from '@/platform/billing/billingModel';

type Client = Pick<InvoicesApi, 'listOrganizationInvoices' | 'generateInvoice'>;

export function OrganizationInvoicesSection({ client, organizationId }: { client: Client; organizationId: string }) {
  const { t, formatCurrency, formatDate } = useI18n();
  const { toast } = useToast();
  const [tick, setTick] = useState(0);
  const [invoices, setInvoices] = useState<Invoice[] | null>(null);
  const [error, setError] = useState(false);
  const [pending, setPending] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setInvoices(null); setError(false);
    client.listOrganizationInvoices(organizationId)
      .then(rows => { if (!cancelled) setInvoices(rows); })
      .catch(() => { if (!cancelled) setError(true); });
    return () => { cancelled = true; };
  }, [client, organizationId, tick]);

  async function generate() {
    setPending(true);
    try {
      await client.generateInvoice(organizationId);
      toast({ title: t('platform.billing.generate.done'), variant: 'success' });
      setTick(n => n + 1);
    } catch {
      toast({ title: t('platform.billing.action.error'), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('platform.organization.section.invoices')}</CardTitle>
        <Button variant="outline" disabled={pending} onClick={() => void generate()}>{t('platform.organization.invoices.generate')}</Button>
      </CardHeader>
      <CardContent>
        {error ? (
          <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={() => setTick(n => n + 1)} />
        ) : invoices === null ? (
          <LoadingCards count={1} />
        ) : invoices.length === 0 ? (
          <EmptyState message={t('platform.organization.invoices.empty')} />
        ) : (
          invoices.map(inv => (
            <div key={inv.invoiceId} className="pc-list-row">
              <span className="pc-num">#{inv.number} · {formatDate(inv.issuedAtUtc)}</span>
              <span className="pc-cell-actions">
                <span className="pc-num">{formatCurrency(minorToMajor(inv.amountMinorUnits), inv.currencyCode)}</span>
                <Badge variant={INVOICE_STATUS_VARIANT[inv.status] ?? 'outline'}>{INVOICE_STATUS_LABEL[inv.status] ? t(INVOICE_STATUS_LABEL[inv.status]) : inv.status}</Badge>
              </span>
            </div>
          ))
        )}
      </CardContent>
    </Card>
  );
}
