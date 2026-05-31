import { useEffect, useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { LoadingCards, ErrorState, EmptyState } from '@/components/ui/states';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import { minorToMajor } from '@/club/money';
import type { PlatformApiClient } from '@/api/platformApi';
import type { Invoice } from '@/api/types';
import { INVOICE_STATUS_VARIANT, INVOICE_STATUS_LABEL } from '@/platform/billing/billingModel';

type Client = Pick<PlatformApiClient, 'listTenantInvoices' | 'generateInvoice'>;

export function TenantInvoicesSection({ client, organizationId }: { client: Client; organizationId: string }) {
  const { t, formatCurrency, formatDate } = useI18n();
  const { toast } = useToast();
  const [tick, setTick] = useState(0);
  const [invoices, setInvoices] = useState<Invoice[] | null>(null);
  const [error, setError] = useState(false);
  const [pending, setPending] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setInvoices(null); setError(false);
    client.listTenantInvoices(organizationId)
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
      <CardHeader className="flex flex-row items-center justify-between">
        <CardTitle>{t('platform.tenant.section.invoices')}</CardTitle>
        <Button variant="outline" disabled={pending} onClick={() => void generate()}>{t('platform.tenant.invoices.generate')}</Button>
      </CardHeader>
      <CardContent className="flex flex-col gap-2 text-sm">
        {error ? (
          <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={() => setTick(n => n + 1)} />
        ) : invoices === null ? (
          <LoadingCards count={1} />
        ) : invoices.length === 0 ? (
          <EmptyState message={t('platform.tenant.invoices.empty')} />
        ) : (
          invoices.map(inv => (
            <div key={inv.invoiceId} className="flex items-center justify-between border-b border-border py-2 last:border-0">
              <span className="tabular-nums">#{inv.number} · {formatDate(inv.issuedAtUtc)}</span>
              <span className="flex items-center gap-2">
                <span className="tabular-nums">{formatCurrency(minorToMajor(inv.amountMinorUnits), inv.currencyCode)}</span>
                <Badge variant={INVOICE_STATUS_VARIANT[inv.status] ?? 'outline'}>{INVOICE_STATUS_LABEL[inv.status] ? t(INVOICE_STATUS_LABEL[inv.status]) : inv.status}</Badge>
              </span>
            </div>
          ))
        )}
      </CardContent>
    </Card>
  );
}
