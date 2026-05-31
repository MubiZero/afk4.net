import { useI18n } from '@/i18n/I18nProvider';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { LoadingCards, ErrorState } from '@/components/ui/states';
import { minorToMajor } from '../money';
import type { ClubApiClient } from '@/api/clubApi';
import { useBilling } from './useBilling';
import {
  subscriptionStatusLabelKey,
  subscriptionStatusVariant,
  invoiceStatusLabelKey,
  invoiceStatusVariant,
  buildInvoiceRows
} from './billingModel';

export function BillingScreen({ client, organizationId }: { client: ClubApiClient; organizationId: string }) {
  const { t, formatCurrency, formatDate } = useI18n();
  const state = useBilling(client, organizationId);

  if (state.status === 'loading') return <LoadingCards count={2} />;
  if (state.status === 'error') return (
    <ErrorState
      message={t('state.error')}
      retryLabel={t('state.retry')}
      onRetry={state.retry}
    />
  );

  const { subscription, invoices } = state;
  const subLabelKey = subscriptionStatusLabelKey(subscription.status);
  const rows = buildInvoiceRows(invoices);

  return (
    <div className="space-y-6">
      <Card>
        <CardHeader>
          <CardTitle>{t('club.billing.subscription.title')}</CardTitle>
        </CardHeader>
        <CardContent>
          <dl className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <div>
              <dt className="text-xs text-muted-foreground">{t('club.billing.subscription.plan')}</dt>
              <dd className="mt-1 font-medium">{subscription.planCode}</dd>
            </div>
            <div>
              <dt className="text-xs text-muted-foreground">{t('club.billing.subscription.status')}</dt>
              <dd className="mt-1 flex flex-wrap gap-1">
                <Badge variant={subscriptionStatusVariant(subscription.status)}>
                  {subLabelKey ? t(subLabelKey) : subscription.status}
                </Badge>
                {subscription.cancelAtPeriodEnd && (
                  <Badge variant="secondary">{t('club.billing.subscription.cancelAtPeriodEnd')}</Badge>
                )}
              </dd>
            </div>
            <div>
              <dt className="text-xs text-muted-foreground">{t('club.billing.subscription.amount')}</dt>
              <dd className="mt-1 tabular-nums">
                {formatCurrency(minorToMajor(subscription.amountMinorUnits), subscription.currencyCode)}
              </dd>
            </div>
            <div>
              <dt className="text-xs text-muted-foreground">{t('club.billing.subscription.period')}</dt>
              <dd className="mt-1 tabular-nums">
                {formatDate(subscription.currentPeriodStartUtc)} — {formatDate(subscription.currentPeriodEndUtc)}
              </dd>
            </div>
            <div>
              <dt className="text-xs text-muted-foreground">{t('club.billing.subscription.nextInvoice')}</dt>
              <dd className="mt-1 tabular-nums">
                {subscription.nextInvoiceUtc ? formatDate(subscription.nextInvoiceUtc) : '—'}
              </dd>
            </div>
          </dl>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>{t('club.billing.invoices.title')}</CardTitle>
        </CardHeader>
        <CardContent>
          {rows.length === 0 ? (
            <p className="py-8 text-center text-sm text-muted-foreground">
              {t('club.billing.invoices.empty')}
            </p>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t('platform.billing.column.number')}</TableHead>
                  <TableHead>{t('platform.billing.column.issued')}</TableHead>
                  <TableHead>{t('platform.billing.column.due')}</TableHead>
                  <TableHead>{t('platform.billing.column.amount')}</TableHead>
                  <TableHead>{t('platform.billing.column.status')}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map(r => {
                  const invLabelKey = invoiceStatusLabelKey(r.status);
                  return (
                    <TableRow key={r.invoiceId}>
                      <TableCell className="tabular-nums">{r.number}</TableCell>
                      <TableCell className="tabular-nums">{formatDate(r.issuedAtUtc)}</TableCell>
                      <TableCell className="tabular-nums">{formatDate(r.dueAtUtc)}</TableCell>
                      <TableCell className="tabular-nums">
                        {formatCurrency(minorToMajor(r.amountMinorUnits), r.currencyCode)}
                      </TableCell>
                      <TableCell>
                        <Badge variant={invoiceStatusVariant(r.status)}>
                          {invLabelKey ? t(invLabelKey) : r.status}
                        </Badge>
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
