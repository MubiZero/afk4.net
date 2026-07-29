import { useEffect, useState } from 'react';
import type { AuditApi } from '@/api/platformClients/audit';
import type { AuditSearchResult } from '@/api/types';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { EmptyState, ErrorState, LoadingCards } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';

export function OrganizationHistoryTab({ client, organizationId }: {
  client: Pick<AuditApi, 'listOrganizationHistory'>;
  organizationId: string;
}) {
  const { t, formatDate } = useI18n();
  const [revision, setRevision] = useState(0);
  const [result, setResult] = useState<AuditSearchResult | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setResult(null);
    setFailed(false);
    client.listOrganizationHistory(organizationId)
      .then(value => { if (!cancelled) setResult(value); })
      .catch(() => { if (!cancelled) setFailed(true); });
    return () => { cancelled = true; };
  }, [client, organizationId, revision]);

  if (failed) return <ErrorState message={t('platform.organization.history.error')} retryLabel={t('state.retry')} onRetry={() => setRevision(value => value + 1)} />;
  if (result === null) return <LoadingCards count={3} />;
  if (result.records.length === 0) return <EmptyState message={t('platform.organization.history.empty')} />;

  return <div className="overflow-hidden rounded-lg border border-border bg-card"><Table>
    <TableHeader><TableRow>
      <TableHead>{t('platform.organization.history.time')}</TableHead>
      <TableHead>{t('platform.organization.history.action')}</TableHead>
      <TableHead>{t('platform.organization.history.target')}</TableHead>
      <TableHead>{t('platform.organization.history.outcome')}</TableHead>
      <TableHead>{t('platform.organization.history.source')}</TableHead>
    </TableRow></TableHeader>
    <TableBody>{result.records.map(record => <TableRow key={record.auditRecordId}>
      <TableCell className="whitespace-nowrap tabular-nums">{formatDate(record.createdAtUtc)}</TableCell>
      <TableCell><code className="text-xs">{record.action}</code></TableCell>
      <TableCell>{record.targetType}{record.targetId !== null ? ` · ${record.targetId}` : ''}</TableCell>
      <TableCell><Badge variant="secondary">{record.outcome}</Badge></TableCell>
      <TableCell>{record.sourceApp}</TableCell>
    </TableRow>)}</TableBody>
  </Table></div>;
}
