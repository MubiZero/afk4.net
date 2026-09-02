import { useMemo, useState } from 'react';
import type { JSX } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen } from '../../management/ManagementScreen';
import { EmptyState } from '../../operatorPrimitives';
import { createAuthenticatedOperatorClients, downloadTextFile } from '../../operatorHelpers';
import { toAuditCsv } from './orgAuditCsv';
import type { OperatorBackendContext } from '../../operatorTypes';
import { presetRange, type DateRange } from './dateRange';
import { OrgAuditFilters, type AuditDraft } from './OrgAuditFilters';
import { useOrgAudit, type OrgAuditClient } from './useOrgAudit';
import { toAuditRows } from './orgAuditModel';
import type { OrgAuditQuery } from '../../api/clients/orgAudit';

const DEFAULT_LIMIT = 100;
const GRID = '1.2fr 1fr 1.4fr 1.2fr 0.8fr 0.8fr 1.4fr';

function buildQuery(range: DateRange, draft: AuditDraft): OrgAuditQuery {
  const q: OrgAuditQuery = { fromUtc: range.fromUtc, toUtc: range.toUtc, limit: DEFAULT_LIMIT };
  if (draft.action.length > 0) q.action = draft.action;
  if (draft.targetType.length > 0) q.targetType = draft.targetType;
  if (draft.outcome !== 'all') q.outcome = draft.outcome;
  return q;
}

// Org-wide аудит (Сеть → Журнал): все филиалы + org-level записи (BranchId=null) на одном
// экране — единственное место, где такие записи вообще видны оператору (branch-scoped
// audit.ts client никогда их не вернёт). Read-only, без пагинации (курсор — вне объёма, см. бриф).
export function JournalDestination({ backend }: { backend: OperatorBackendContext | null }): JSX.Element {
  const { t, formatDate } = useI18n();
  const [range, setRange] = useState<DateRange>(() => presetRange('today', new Date()));
  const [query, setQuery] = useState<OrgAuditQuery>(() =>
    buildQuery(presetRange('today', new Date()), { action: '', outcome: 'all', targetType: '' })
  );

  const client = useMemo<OrgAuditClient | null>(() => {
    if (backend === null) return null;
    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    return { searchOrganizationAudit: (id, q) => clients.orgAudit.searchOrganizationAudit(id, q) };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [backend?.config.platformBaseUrl, backend?.session.accessToken]);

  const state = useOrgAudit(
    client ?? { searchOrganizationAudit: async () => ({ records: [] }) },
    backend?.session.organizationId ?? '',
    query
  );

  const records = state.status === 'ready' ? state.records : [];
  const rows = state.status === 'ready' ? toAuditRows(records, { formatDate }, t('op.network.journal.actor.system')) : [];

  // Выгружается ровно то, что на экране: тот же фильтр, тот же лимит. Отдельного серверного
  // экспорта у org-аудита нет, а поддержке нужен файл, а не скриншот.
  function exportCsv() {
    const stamp = new Date().toISOString().replace(/[:.]/g, '-');
    downloadTextFile(`afk4-audit-journal-${stamp}.csv`, toAuditCsv(records), 'text/csv;charset=utf-8');
  }

  function handleRange(next: DateRange) {
    setRange(next);
    setQuery((prev) => ({ ...prev, fromUtc: next.fromUtc, toUtc: next.toUtc }));
  }

  const screenState = backend === null ? 'loading' : state.status === 'error' ? 'error' : 'ready';

  return (
    <ManagementScreen
      title={t('op.network.dest.journal')}
      subtitle={t('op.network.dest.journal.subtitle')}
      contentWidth="full"
      state={screenState}
      onRetry={state.status === 'error' ? state.retry : undefined}
    >
      <div className="network-journal">
        <OrgAuditFilters
          range={range}
          onRangeChange={handleRange}
          onApply={(draft) => setQuery(buildQuery(range, draft))}
          onReset={() => setQuery(buildQuery(range, { action: '', outcome: 'all', targetType: '' }))}
          onExport={exportCsv}
          exportDisabled={records.length === 0}
        />

        {state.status === 'loading' ? (
          <div className="management-skeleton" aria-hidden="true">
            <div className="management-skeleton-line" />
            <div className="management-skeleton-line" />
            <div className="management-skeleton-line" />
          </div>
        ) : rows.length === 0 ? (
          <EmptyState title={t('op.network.journal.empty')} />
        ) : (
          <div className="table-panel">
            <div className="ctable-head" style={{ gridTemplateColumns: GRID }} aria-hidden="true">
              <span>{t('op.network.journal.col.date')}</span>
              <span>{t('op.network.journal.col.actor')}</span>
              <span>{t('op.network.journal.col.action')}</span>
              <span>{t('op.network.journal.col.target')}</span>
              <span>{t('op.network.journal.col.outcome')}</span>
              <span>{t('op.network.journal.col.source')}</span>
              <span>{t('op.network.journal.col.details')}</span>
            </div>
            <div className="ctable-body">
              {rows.map((row) => (
                <div key={row.id} className="ctable-row" style={{ gridTemplateColumns: GRID }}>
                  <span>{row.date}</span>
                  <span>{row.actor}</span>
                  <span className="network-journal-action">{row.action}</span>
                  <span>{row.target}</span>
                  <span className={`ui-chip ui-chip--status ${row.outcomeTone}`}>{row.outcome}</span>
                  <span>{row.source}</span>
                  <span className="network-journal-details" title={row.details}>{row.details}</span>
                </div>
              ))}
            </div>
          </div>
        )}

        <p className="network-journal-limit-note">{t('op.network.journal.limitNote')}</p>
      </div>
    </ManagementScreen>
  );
}
