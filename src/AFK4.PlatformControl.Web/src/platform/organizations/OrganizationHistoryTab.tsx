import type { AuditApi } from '@/api/platformClients/audit';
import { Badge } from '@/components/ui/badge';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { EmptyState, ErrorState, LoadingCards } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import { useLoadable } from '../useLoadable';

export function OrganizationHistoryTab({ client, organizationId }: {
  client: Pick<AuditApi, 'listOrganizationHistory'>;
  organizationId: string;
}) {
  const { t, formatDate } = useI18n();
  const state = useLoadable(() => client.listOrganizationHistory(organizationId), [organizationId]);

  if (state.status === 'error') return <ErrorState title={t('platform.organization.history.error')} message={state.message} retryLabel={t('state.retry')} onRetry={state.retry} />;
  if (state.status === 'loading') return <LoadingCards count={3} />;
  if (state.data.records.length === 0) return <EmptyState message={t('platform.organization.history.empty')} />;

  return <div className="table-panel"><Table>
    <TableHeader><TableRow>
      <TableHead>{t('platform.organization.history.time')}</TableHead>
      <TableHead>{t('platform.organization.history.action')}</TableHead>
      <TableHead>{t('platform.organization.history.target')}</TableHead>
      <TableHead>{t('platform.organization.history.outcome')}</TableHead>
      <TableHead>{t('platform.organization.history.source')}</TableHead>
    </TableRow></TableHeader>
    <TableBody>{state.data.records.map(record => <TableRow key={record.auditRecordId}>
      <TableCell className="pc-num">{formatDate(record.createdAtUtc)}</TableCell>
      <TableCell><code>{record.action}</code></TableCell>
      <TableCell>{record.targetType}{record.targetId !== null ? ` · ${record.targetId}` : ''}</TableCell>
      <TableCell><Badge variant="secondary">{record.outcome}</Badge></TableCell>
      <TableCell>{record.sourceApp}</TableCell>
    </TableRow>)}</TableBody>
  </Table></div>;
}
