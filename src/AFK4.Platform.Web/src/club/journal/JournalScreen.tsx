import { useState } from 'react';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';
import { LoadingCards, ErrorState, EmptyState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import type { AuditApi } from '@/api/clients/audit';
import type { AuditSearchQuery } from '@/api/types';
import { AuditFilters, type AuditDraft } from './AuditFilters';
import { useAudit } from './useAudit';
import { toAuditRows } from './auditModel';
import { presetRange, type DateRange } from '../reports/reportsModel';

type Client = Pick<AuditApi, 'searchAudit'>;

const DEFAULT_LIMIT = 100;

function buildQuery(range: DateRange, draft: AuditDraft): AuditSearchQuery {
  const query: AuditSearchQuery = { fromUtc: range.fromUtc, toUtc: range.toUtc, limit: DEFAULT_LIMIT };
  if (draft.action.length > 0) query.action = draft.action;
  if (draft.targetType.length > 0) query.targetType = draft.targetType;
  if (draft.outcome !== 'all') query.outcome = draft.outcome;
  return query;
}

export function JournalScreen({ client, branchId }: { client: Client; branchId: string }) {
  const { t, formatDate } = useI18n();
  const [range, setRange] = useState<DateRange>(() => presetRange('today', new Date()));
  const [query, setQuery] = useState<AuditSearchQuery>(() => buildQuery(presetRange('today', new Date()), { action: '', outcome: 'all', targetType: '' }));
  const state = useAudit(client, branchId, query);

  function handleRangeChange(next: DateRange) {
    setRange(next);
    setQuery(prev => ({ ...prev, fromUtc: next.fromUtc, toUtc: next.toUtc }));
  }

  return (
    <div className="flex flex-col gap-4">
      <AuditFilters
        range={range}
        onRangeChange={handleRangeChange}
        onApply={draft => setQuery(buildQuery(range, draft))}
        onReset={() => setQuery(buildQuery(range, { action: '', outcome: 'all', targetType: '' }))}
      />

      {state.status === 'loading' ? (
        <LoadingCards count={3} />
      ) : state.status === 'error' ? (
        <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={state.retry} />
      ) : (
        <JournalTable
          rows={toAuditRows({ records: state.records, limit: 0 }, { formatDate }, t('journal.actor.system'))}
        />
      )}

      <p className="text-xs text-muted-foreground">{t('journal.limitNote')}</p>
    </div>
  );
}

function JournalTable({ rows }: { rows: ReturnType<typeof toAuditRows> }) {
  const { t } = useI18n();
  if (rows.length === 0) return <EmptyState message={t('journal.empty')} />;
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>{t('journal.col.date')}</TableHead>
          <TableHead>{t('journal.col.actor')}</TableHead>
          <TableHead>{t('journal.col.action')}</TableHead>
          <TableHead>{t('journal.col.target')}</TableHead>
          <TableHead>{t('journal.col.outcome')}</TableHead>
          <TableHead>{t('journal.col.source')}</TableHead>
          <TableHead>{t('journal.col.details')}</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {rows.map(row => (
          <TableRow key={row.id}>
            <TableCell className="tabular-nums">{row.date}</TableCell>
            <TableCell>{row.actor}</TableCell>
            <TableCell className="font-medium">{row.action}</TableCell>
            <TableCell>{row.target}</TableCell>
            <TableCell><Badge variant={row.outcomeVariant}>{row.outcome}</Badge></TableCell>
            <TableCell>{row.source}</TableCell>
            <TableCell className="max-w-xs truncate font-mono text-xs" title={row.details}>{row.details}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}
