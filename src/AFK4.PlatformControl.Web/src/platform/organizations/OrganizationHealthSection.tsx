import { useEffect, useState } from 'react';
import { RefreshCw } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { LoadingCards, ErrorState, EmptyState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import type { OrganizationsApi } from '@/api/platformClients/organizations';
import type { OrganizationHealth } from '@/api/types';

type Client = Pick<OrganizationsApi, 'getHealth'>;

interface Props {
  client: Client;
  organizationId: string;
}

// Состояние клиента: размер парка, живость персонала и недавние отказы.
//
// Внутренней телеметрии здесь намеренно нет: имя последней применённой миграции БД и дубль
// статуса из паспорта — данные для дежурного инженера, а не характеристика клиента. На
// бизнес-экране они читаются как случайный мусор и подрывают доверие ко всему остальному.
export function OrganizationHealthSection({ client, organizationId }: Props) {
  const { t, formatNumber, formatDate } = useI18n();
  const [tick, setTick] = useState(0);
  const [health, setHealth] = useState<OrganizationHealth | null>(null);
  const [error, setError] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setHealth(null); setError(false);
    client.getHealth(organizationId)
      .then(data => { if (!cancelled) setHealth(data); })
      .catch(() => { if (!cancelled) setError(true); });
    return () => { cancelled = true; };
  }, [client, organizationId, tick]);

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('platform.organization.section.health')}</CardTitle>
        <Button variant="ghost" size="icon-sm" aria-label={t('platform.organization.health.refresh')} onClick={() => setTick(value => value + 1)}>
          <RefreshCw size={14} aria-hidden="true" />
        </Button>
      </CardHeader>
      <CardContent>
        {error ? (
          <ErrorState message={t('platform.organization.health.error')} retryLabel={t('state.retry')} onRetry={() => setTick(value => value + 1)} />
        ) : health === null ? (
          <LoadingCards count={1} />
        ) : (
          <>
            <dl className="pc-facts">
              <Fact label={t('platform.organization.health.branches')} value={formatNumber(health.branchCount)} />
              <Fact label={t('platform.organization.health.devices')} value={formatNumber(health.deviceCount)} />
              <Fact label={t('platform.organization.health.activeStaff')} value={formatNumber(health.activeStaffUserCount)} />
              <Fact label={t('platform.organization.health.lastSignIn')} value={health.latestStaffSignInAtUtc !== null ? formatDate(health.latestStaffSignInAtUtc) : '—'} />
            </dl>

            {health.recentErrors.length === 0 ? (
              <EmptyState message={t('platform.organization.health.recentErrorsEmpty')} />
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>{t('platform.organization.health.col.time')}</TableHead>
                    <TableHead>{t('platform.organization.health.col.source')}</TableHead>
                    <TableHead>{t('platform.organization.health.col.action')}</TableHead>
                    <TableHead>{t('platform.organization.health.col.outcome')}</TableHead>
                    <TableHead>{t('platform.organization.health.col.message')}</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {health.recentErrors.map((entry, index) => (
                    <TableRow key={`${entry.createdAtUtc}-${index}`}>
                      <TableCell className="pc-num">{formatDate(entry.createdAtUtc)}</TableCell>
                      <TableCell>{entry.source}</TableCell>
                      <TableCell>{entry.action}</TableCell>
                      <TableCell>{entry.outcome}</TableCell>
                      <TableCell>{entry.message ?? ''}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}

function Fact({ label, value }: { label: string; value: string }) {
  return (
    <div className="pc-fact">
      <dt>{label}</dt>
      <dd>{value}</dd>
    </div>
  );
}
