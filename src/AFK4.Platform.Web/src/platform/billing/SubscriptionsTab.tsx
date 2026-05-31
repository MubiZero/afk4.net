import { useState } from 'react';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select';
import { Table, TableHeader, TableRow, TableHead, TableBody, TableCell } from '@/components/ui/table';
import { LoadingCards, ErrorState, EmptyState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import type { PlatformApiClient } from '@/api/platformApi';
import { useSubscriptions } from './useSubscriptions';
import {
  filterSubscriptions, SUBSCRIPTION_STATUS_VARIANT, SUBSCRIPTION_STATUS_LABEL,
  INTERVAL_LABEL, SUBSCRIPTION_STATUS_FILTERS
} from './billingModel';

export function SubscriptionsTab({ client }: { client: PlatformApiClient }) {
  const { t, formatCurrency, formatDate } = useI18n();
  const state = useSubscriptions(client);
  const [query, setQuery] = useState('');
  const [status, setStatus] = useState('all');

  if (state.status === 'loading') return <LoadingCards count={2} />;
  if (state.status === 'error') return <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={state.retry} />;

  const rows = filterSubscriptions(state.data, { query, status });

  return (
    <Card>
      <CardContent className="flex flex-col gap-3 pt-6">
        <div className="flex flex-wrap gap-2">
          <Input className="max-w-xs" placeholder={t('platform.billing.search.placeholder')} value={query} onChange={e => setQuery(e.target.value)} />
          <Select value={status} onValueChange={setStatus}>
            <SelectTrigger className="max-w-[200px]" aria-label={t('platform.billing.column.status')}><SelectValue /></SelectTrigger>
            <SelectContent>
              {SUBSCRIPTION_STATUS_FILTERS.map(s => (
                <SelectItem key={s} value={s}>{s === 'all' ? t('platform.billing.filter.allStatuses') : t(SUBSCRIPTION_STATUS_LABEL[s])}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        {rows.length === 0 ? (
          <EmptyState message={t('platform.billing.empty.subscriptions')} />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('platform.billing.column.tenant')}</TableHead>
                <TableHead>{t('platform.billing.column.plan')}</TableHead>
                <TableHead>{t('platform.billing.column.status')}</TableHead>
                <TableHead>{t('platform.billing.column.amount')}</TableHead>
                <TableHead>{t('platform.billing.column.interval')}</TableHead>
                <TableHead>{t('platform.billing.column.periodEnd')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.map(r => (
                <TableRow key={r.tenantSubscriptionId}>
                  <TableCell><span className="font-medium">{r.organizationName}</span> <code className="text-xs text-muted-foreground">{r.organizationSlug}</code></TableCell>
                  <TableCell>{r.planCode}</TableCell>
                  <TableCell><Badge variant={SUBSCRIPTION_STATUS_VARIANT[r.status] ?? 'outline'}>{SUBSCRIPTION_STATUS_LABEL[r.status] ? t(SUBSCRIPTION_STATUS_LABEL[r.status]) : r.status}</Badge></TableCell>
                  <TableCell className="tabular-nums">{formatCurrency(r.amountMinorUnits, r.currencyCode)}</TableCell>
                  <TableCell>{INTERVAL_LABEL[r.billingInterval] ? t(INTERVAL_LABEL[r.billingInterval]) : r.billingInterval}</TableCell>
                  <TableCell>{formatDate(r.currentPeriodEndUtc)}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </CardContent>
    </Card>
  );
}
