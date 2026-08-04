import { useEffect, useState, type ReactNode } from 'react';
import type { AuditApi } from '@/api/platformClients/audit';
import type { AuditSearchResult } from '@/api/types';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { EmptyState, ErrorState, LoadingCards } from '@/components/ui/states';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { useI18n } from '@/i18n/I18nProvider';

export interface AuditFilters { organizationId: string; action: string; outcome: string; from: string; to: string }

export function AuditScreen({ client, filters, onFiltersChange }: {
  client: Pick<AuditApi, 'search'>;
  filters: AuditFilters;
  onFiltersChange: (filters: AuditFilters) => void;
}) {
  const { t, formatDate } = useI18n();
  const [draft, setDraft] = useState(filters);
  const [revision, setRevision] = useState(0);
  const [result, setResult] = useState<AuditSearchResult | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => setDraft(filters), [filters]);
  useEffect(() => {
    let cancelled = false;
    setResult(null); setFailed(false);
    client.search({
      organizationId: filters.organizationId || undefined,
      action: filters.action || undefined,
      outcome: filters.outcome || undefined,
      fromUtc: filters.from ? new Date(`${filters.from}T00:00:00Z`).toISOString() : undefined,
      toUtc: filters.to ? new Date(`${filters.to}T23:59:59.999Z`).toISOString() : undefined,
      limit: 100
    }).then(value => { if (!cancelled) setResult(value); }).catch(() => { if (!cancelled) setFailed(true); });
    return () => { cancelled = true; };
  }, [client, filters, revision]);

  return <div>
    <form className="pc-filters" onSubmit={event => { event.preventDefault(); onFiltersChange(draft); }}>
      <Filter label={t('platform.audit.organization')}><Input value={draft.organizationId} onChange={e => setDraft({ ...draft, organizationId: e.target.value.trim() })} /></Filter>
      <Filter label={t('platform.audit.action')}><Input value={draft.action} onChange={e => setDraft({ ...draft, action: e.target.value })} /></Filter>
      <Filter label={t('platform.audit.outcome')}><Input value={draft.outcome} onChange={e => setDraft({ ...draft, outcome: e.target.value })} /></Filter>
      <Filter label={t('platform.audit.from')}><Input type="date" value={draft.from} onChange={e => setDraft({ ...draft, from: e.target.value })} /></Filter>
      <Filter label={t('platform.audit.to')}><Input type="date" value={draft.to} onChange={e => setDraft({ ...draft, to: e.target.value })} /></Filter>
      <div><Button type="submit">{t('platform.audit.apply')}</Button></div>
    </form>
    {failed ? <ErrorState message={t('platform.audit.error')} retryLabel={t('state.retry')} onRetry={() => setRevision(value => value + 1)} />
      : result === null ? <LoadingCards count={3} />
      : (result.records ?? []).length === 0 ? <EmptyState message={t('platform.audit.empty')} />
      : <div className="pc-table-panel"><Table><TableHeader><TableRow>
          <TableHead>{t('platform.audit.time')}</TableHead><TableHead>{t('platform.audit.organization')}</TableHead><TableHead>{t('platform.audit.action')}</TableHead><TableHead>{t('platform.audit.target')}</TableHead><TableHead>{t('platform.audit.outcome')}</TableHead><TableHead>{t('platform.audit.source')}</TableHead>
        </TableRow></TableHeader><TableBody>{(result.records ?? []).map(record => <TableRow key={record.auditRecordId}>
          <TableCell className="pc-num">{formatDate(record.createdAtUtc)}</TableCell><TableCell><code>{record.organizationId}</code></TableCell><TableCell><code>{record.action}</code></TableCell><TableCell>{record.targetType}{record.targetId ? ` · ${record.targetId}` : ''}</TableCell><TableCell><Badge variant="secondary">{record.outcome}</Badge></TableCell><TableCell>{record.sourceApp}</TableCell>
        </TableRow>)}</TableBody></Table></div>}
  </div>;
}

function Filter({ label, children }: { label: string; children: ReactNode }) {
  return <label><span>{label}</span>{children}</label>;
}
