import { useEffect, useState, type ReactNode } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { LoadingCards, ErrorState, EmptyState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import type { PlatformApiClient } from '@/api/platformApi';
import type { TenantHealth } from '@/api/types';

type Client = Pick<PlatformApiClient, 'getHealth'>;

interface Props {
  client: Client;
  organizationId: string;
}

export function TenantHealthSection({ client, organizationId }: Props) {
  const { t, formatNumber, formatDate } = useI18n();
  const [tick, setTick] = useState(0);
  const [health, setHealth] = useState<TenantHealth | null>(null);
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
      <CardHeader className="flex flex-row items-center justify-between">
        <CardTitle>{t('platform.tenant.section.health')}</CardTitle>
        <Button variant="ghost" size="sm" onClick={() => setTick(n => n + 1)}>{t('platform.tenant.health.refresh')}</Button>
      </CardHeader>
      <CardContent className="flex flex-col gap-4 text-sm">
        {error ? (
          <ErrorState message={t('platform.tenant.health.error')} retryLabel={t('state.retry')} onRetry={() => setTick(n => n + 1)} />
        ) : health === null ? (
          <LoadingCards count={1} />
        ) : (
          <>
            <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
              <Row label={t('platform.tenant.health.status')}><Badge variant="secondary">{health.status}</Badge></Row>
              <Row label={t('platform.tenant.health.branches')}>{formatNumber(health.branchCount)}</Row>
              <Row label={t('platform.tenant.health.devices')}>{formatNumber(health.deviceCount)}</Row>
              <Row label={t('platform.tenant.health.activeStaff')}>{formatNumber(health.activeStaffUserCount)}</Row>
              <Row label={t('platform.tenant.health.lastSignIn')}>{health.latestStaffSignInAtUtc !== null ? formatDate(health.latestStaffSignInAtUtc) : '—'}</Row>
              <Row label={t('platform.tenant.health.latestMigration')}>{health.latestMigration ?? '—'}</Row>
              <Row label={t('platform.tenant.health.recentErrors')}>{formatNumber(health.recentErrorCount)}</Row>
            </div>

            {health.recentErrors.length === 0 ? (
              <EmptyState message={t('platform.tenant.health.recentErrorsEmpty')} />
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>{t('platform.tenant.health.col.time')}</TableHead>
                    <TableHead>{t('platform.tenant.health.col.source')}</TableHead>
                    <TableHead>{t('platform.tenant.health.col.action')}</TableHead>
                    <TableHead>{t('platform.tenant.health.col.outcome')}</TableHead>
                    <TableHead>{t('platform.tenant.health.col.message')}</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {health.recentErrors.map((entry, index) => (
                    <TableRow key={`${entry.createdAtUtc}-${index}`}>
                      <TableCell className="tabular-nums">{formatDate(entry.createdAtUtc)}</TableCell>
                      <TableCell>{entry.source}</TableCell>
                      <TableCell>{entry.action}</TableCell>
                      <TableCell>{entry.outcome}</TableCell>
                      <TableCell><code className="font-mono text-xs">{entry.message ?? ''}</code></TableCell>
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

function Row({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="flex justify-between gap-3">
      <span className="text-muted-foreground">{label}</span>
      <span className="text-right">{children}</span>
    </div>
  );
}
