import { useEffect, useState, type ReactNode } from 'react';
import type { AuditApi } from '@/api/platformClients/audit';
import type { OrganizationsApi } from '@/api/platformClients/organizations';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { Badge } from '@/components/ui/badge';
import { EmptyState, ErrorState, LoadingCards } from '@/components/ui/states';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { useI18n } from '@/i18n/I18nProvider';
import { useLoadable } from '../useLoadable';
import { auditOutcomeLabel, auditOutcomeVariant, auditSourceLabel, auditTargetLabel } from '@/platform/audit/auditModel';

export interface AuditFilters { organizationId: string; action: string; outcome: string; from: string; to: string }

const OUTCOME_OPTIONS = ['Succeeded', 'Denied', 'Failed'] as const;

export function AuditScreen({ client, organizationsClient, filters, onFiltersChange }: {
  client: Pick<AuditApi, 'search'>;
  /// Список клубов — ради фильтра по клубу. Выбирать из имён, а не вводить идентификатор:
  /// журнал смотрят, когда разбираются с конкретным клубом, а его GUID никто не помнит.
  organizationsClient: Pick<OrganizationsApi, 'listOrganizations'>;
  filters: AuditFilters;
  onFiltersChange: (filters: AuditFilters) => void;
}) {
  const { t, formatDate } = useI18n();
  const [draft, setDraft] = useState(filters);
  const organizations = useLoadable(() => organizationsClient.listOrganizations());
  const state = useLoadable(() => client.search({
    organizationId: filters.organizationId || undefined,
    action: filters.action || undefined,
    outcome: filters.outcome || undefined,
    fromUtc: filters.from ? new Date(`${filters.from}T00:00:00Z`).toISOString() : undefined,
    toUtc: filters.to ? new Date(`${filters.to}T23:59:59.999Z`).toISOString() : undefined,
    limit: 100
  }), [filters.organizationId, filters.action, filters.outcome, filters.from, filters.to]);

  useEffect(() => setDraft(filters), [filters]);

  return <div>
    <form className="pc-filters" onSubmit={event => { event.preventDefault(); onFiltersChange(draft); }}>
      <Filter label={t('platform.audit.organization')}>
        {/* Пока список клубов в пути или не доехал, остаётся ввод идентификатора: фильтр по ссылке
            из адресной строки должен работать и без списка. */}
        {organizations.status === 'ready' ? (
          <Select value={draft.organizationId} onChange={e => setDraft({ ...draft, organizationId: e.target.value })}>
            <option value="">{t('platform.audit.organization.all')}</option>
            {organizations.data.map(organization => (
              <option key={organization.organizationId} value={organization.organizationId}>{organization.name}</option>
            ))}
          </Select>
        ) : (
          <Input value={draft.organizationId} onChange={e => setDraft({ ...draft, organizationId: e.target.value.trim() })} />
        )}
      </Filter>
      <Filter label={t('platform.audit.action')}><Input value={draft.action} onChange={e => setDraft({ ...draft, action: e.target.value })} /></Filter>
      {/* Исходов ровно три, и писались они руками по-английски: опечатка давала пустой журнал
          без единого намёка, что дело в фильтре. */}
      <Filter label={t('platform.audit.outcome')}>
        <Select value={draft.outcome} onChange={e => setDraft({ ...draft, outcome: e.target.value })}>
          <option value="">{t('journal.outcome.all')}</option>
          {OUTCOME_OPTIONS.map(outcome => (
            <option key={outcome} value={outcome}>{auditOutcomeLabel(outcome, t)}</option>
          ))}
        </Select>
      </Filter>
      <Filter label={t('platform.audit.from')}><Input type="date" value={draft.from} onChange={e => setDraft({ ...draft, from: e.target.value })} /></Filter>
      <Filter label={t('platform.audit.to')}><Input type="date" value={draft.to} onChange={e => setDraft({ ...draft, to: e.target.value })} /></Filter>
      <div><Button type="submit">{t('platform.audit.apply')}</Button></div>
    </form>
    {state.status === 'error' ? <ErrorState title={t('platform.audit.error')} message={state.message} retryLabel={t('state.retry')} onRetry={state.retry} />
      : state.status === 'loading' ? <LoadingCards count={3} />
      : (state.data.records ?? []).length === 0 ? <EmptyState message={t('platform.audit.empty')} />
      : <div className="table-panel"><Table><TableHeader><TableRow>
          <TableHead>{t('platform.audit.time')}</TableHead><TableHead>{t('platform.audit.organization')}</TableHead><TableHead>{t('platform.audit.action')}</TableHead><TableHead>{t('platform.audit.target')}</TableHead><TableHead>{t('platform.audit.outcome')}</TableHead><TableHead>{t('platform.audit.source')}</TableHead>
        </TableRow></TableHeader><TableBody>{(state.data.records ?? []).map(record => <TableRow key={record.auditRecordId}>
          <TableCell className="pc-num">{formatDate(record.createdAtUtc)}</TableCell><TableCell>{record.organizationName ?? <code>{record.organizationId}</code>}</TableCell><TableCell><code>{record.action}</code></TableCell><TableCell>{auditTargetLabel(record.targetType, t)}{record.targetId ? ` · ${record.targetId}` : ''}</TableCell><TableCell><Badge variant={auditOutcomeVariant(record.outcome)}>{auditOutcomeLabel(record.outcome, t)}</Badge></TableCell><TableCell>{auditSourceLabel(record.sourceApp, t)}</TableCell>
        </TableRow>)}</TableBody></Table></div>}
  </div>;
}

function Filter({ label, children }: { label: string; children: ReactNode }) {
  return <label><span>{label}</span>{children}</label>;
}
