import { useState } from 'react';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Select } from '@/components/ui/select';
import { Table, TableHeader, TableRow, TableHead, TableBody, TableCell } from '@/components/ui/table';
import { LoadingCards, ErrorState, EmptyState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import { minorToMajor } from '@/lib/money';
import type { SubscriptionsApi } from '@/api/platformClients/subscriptions';
import { useSubscriptions } from './useSubscriptions';
import {
  filterSubscriptions, SUBSCRIPTION_STATUS_VARIANT, SUBSCRIPTION_STATUS_LABEL,
  INTERVAL_LABEL, SUBSCRIPTION_STATUS_FILTERS
} from './billingModel';

export function SubscriptionsTab({ client }: { client: SubscriptionsApi }) {
  const { t, formatCurrency, formatDate } = useI18n();
  const state = useSubscriptions(client);
  const [query, setQuery] = useState('');
  const [status, setStatus] = useState('all');

  if (state.status === 'loading') return <LoadingCards count={2} />;
  if (state.status === 'error') return <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={state.retry} />;

  const rows = filterSubscriptions(state.data, { query, status });

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
          {SUBSCRIPTION_STATUS_FILTERS.map(value => (
            <option key={value} value={value}>
              {value === 'all' ? t('platform.billing.filter.allStatuses') : t(SUBSCRIPTION_STATUS_LABEL[value])}
            </option>
          ))}
        </Select>
      </div>

      {rows.length === 0 ? (
        <EmptyState message={t('platform.billing.empty.subscriptions')} />
      ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('platform.billing.column.organization')}</TableHead>
              <TableHead>{t('platform.billing.column.plan')}</TableHead>
              <TableHead>{t('platform.billing.column.status')}</TableHead>
              <TableHead className="pc-num">{t('platform.billing.column.amount')}</TableHead>
              <TableHead>{t('platform.billing.column.interval')}</TableHead>
              <TableHead>{t('platform.billing.column.periodEnd')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {rows.map(subscription => (
              <TableRow key={subscription.organizationSubscriptionId}>
                <TableCell>{subscription.organizationName}</TableCell>
                <TableCell>{subscription.planCode}</TableCell>
                <TableCell>
                  <Badge variant={SUBSCRIPTION_STATUS_VARIANT[subscription.status] ?? 'outline'}>
                    {SUBSCRIPTION_STATUS_LABEL[subscription.status] !== undefined ? t(SUBSCRIPTION_STATUS_LABEL[subscription.status]) : subscription.status}
                  </Badge>
                </TableCell>
                <TableCell className="pc-num">{formatCurrency(minorToMajor(subscription.amountMinorUnits), subscription.currencyCode)}</TableCell>
                <TableCell>{INTERVAL_LABEL[subscription.billingInterval] !== undefined ? t(INTERVAL_LABEL[subscription.billingInterval]) : subscription.billingInterval}</TableCell>
                <TableCell>{formatDate(subscription.currentPeriodEndUtc)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </>
  );
}
