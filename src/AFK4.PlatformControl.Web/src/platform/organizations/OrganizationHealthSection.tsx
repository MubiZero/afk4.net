import { RefreshCw } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { ErrorState, EmptyState } from '@/components/ui/states';
import { Loading, SkeletonTable, SkeletonTiles } from '@/components/ui/skeletons';
import { useI18n } from '@/i18n/I18nProvider';
import { useLoadable } from '../useLoadable';
import type { OrganizationsApi } from '@/api/platformClients/organizations';

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
  const state = useLoadable(() => client.getHealth(organizationId), [organizationId]);

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('platform.organization.section.health')}</CardTitle>
        <Button variant="ghost" size="icon-sm" aria-label={t('platform.organization.health.refresh')} onClick={state.retry}>
          <RefreshCw size={14} aria-hidden="true" />
        </Button>
      </CardHeader>
      <CardContent>
        {state.status === 'error' ? (
          <ErrorState title={t('platform.organization.health.error')} message={state.message} retryLabel={state.canRetry ? t('state.retry') : undefined} onRetry={state.canRetry ? state.retry : undefined} />
        ) : state.status === 'loading' ? (
          <Loading>
            <SkeletonTiles count={4} className="pc-facts" tileClassName="pc-fact" />
            <SkeletonTable columns={5} rows={2} />
          </Loading>
        ) : (
          <>
            <dl className="pc-facts">
              <Fact label={t('platform.organization.health.branches')} value={formatNumber(state.data.branchCount)} />
              <Fact label={t('platform.organization.health.devices')} value={formatNumber(state.data.deviceCount)} />
              <Fact label={t('platform.organization.health.activeStaff')} value={formatNumber(state.data.activeStaffUserCount)} />
              <Fact label={t('platform.organization.health.lastSignIn')} value={state.data.latestStaffSignInAtUtc !== null ? formatDate(state.data.latestStaffSignInAtUtc) : '—'} />
            </dl>

            {state.data.recentErrors.length === 0 ? (
              <EmptyState message={t('platform.organization.health.recentErrorsEmpty')} next="calm" />
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
                  {state.data.recentErrors.map((entry, index) => (
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
