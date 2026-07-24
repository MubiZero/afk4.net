import { useMemo, useState } from 'react';
import type { JSX } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen } from '../../management/ManagementScreen';
import { EmptyState } from '../../operatorPrimitives';
import { createAuthenticatedOperatorClients, readArray, readString } from '../../operatorHelpers';
import type { OperatorBackendContext } from '../../operatorTypes';
import type { OrgAuditRecordDto } from '../../api/clients/orgAudit';
import { presetRange, type DateRange } from '../../network/journal/dateRange';
import { OrgAuditFilters, type AuditDraft } from '../../network/journal/OrgAuditFilters';
import { toAuditRows } from '../../network/journal/orgAuditModel';
import { useBranchAudit, type BranchAuditClient, type BranchAuditQuery } from './useBranchAudit';

const DEFAULT_LIMIT = 100;
const GRID = '1.2fr 1fr 1.4fr 1.2fr 0.8fr 0.8fr 1.4fr';

function mapRecords(result: Record<string, unknown>): OrgAuditRecordDto[] {
  // Каждый элемент `records` — уже объект-запись; читаем поля прямо (readArray<Record> + readString).
  return readArray<Record<string, unknown>>(result, 'records').map((r) => {
    const str = (key: string) => readString(r, key);
    const nullable = (key: string) => (str(key) === '' ? null : str(key));
    return {
      auditRecordId: str('auditRecordId'),
      branchId: nullable('branchId'),
      actorStaffUserId: nullable('actorStaffUserId'),
      actorPlatformAdminUserId: nullable('actorPlatformAdminUserId'),
      action: str('action'),
      targetType: str('targetType'),
      targetId: nullable('targetId'),
      outcome: str('outcome'),
      sourceApp: str('sourceApp'),
      detailsJson: str('detailsJson'),
      createdAtUtc: str('createdAtUtc')
    };
  });
}

function buildQuery(range: DateRange, draft: AuditDraft): BranchAuditQuery {
  const q: BranchAuditQuery = { fromUtc: range.fromUtc, toUtc: range.toUtc, limit: DEFAULT_LIMIT };
  if (draft.action.length > 0) q.action = draft.action;
  if (draft.targetType.length > 0) q.targetType = draft.targetType;
  if (draft.outcome !== 'all') q.outcome = draft.outcome;
  return q;
}

// Аудит филиала (Отчёты → Журнал): менеджерский branch-scoped журнал. Endpoint
// /api/branches/{id}/audit идёт через RequireBranchPermissionAsync (фундамент №1) — per-branch,
// утечки чужих филиалов нет (в отличие от org-журнала в Сеть→Журнал). Переиспользует фильтры и
// модель строк из network/journal (та же форма записи).
export function BranchJournalDestination({ backend }: { backend: OperatorBackendContext | null }): JSX.Element {
  const { t, formatDate } = useI18n();
  const [range, setRange] = useState<DateRange>(() => presetRange('today', new Date()));
  const [query, setQuery] = useState<BranchAuditQuery>(() =>
    buildQuery(presetRange('today', new Date()), { action: '', outcome: 'all', targetType: '' })
  );

  const client = useMemo<BranchAuditClient | null>(() => {
    if (backend === null) return null;
    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    return {
      search: (branchId, q) =>
        clients.audit.search({ branchId, action: q.action, outcome: q.outcome, targetType: q.targetType, fromUtc: q.fromUtc, toUtc: q.toUtc, limit: q.limit }).then(mapRecords)
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [backend?.config.platformBaseUrl, backend?.session.accessToken]);

  const state = useBranchAudit(
    client ?? { search: async () => [] },
    backend?.branchId ?? '',
    query
  );

  const rows = state.status === 'ready' ? toAuditRows(state.records, { formatDate }, t('op.reports.journal.actor.system')) : [];

  function handleRange(next: DateRange) {
    setRange(next);
    setQuery((prev) => ({ ...prev, fromUtc: next.fromUtc, toUtc: next.toUtc }));
  }

  const screenState = backend === null ? 'loading' : state.status === 'error' ? 'error' : 'ready';

  return (
    <ManagementScreen
      title={t('op.reports.journal.title')}
      subtitle={t('op.reports.journal.subtitle')}
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
        />

        {state.status === 'loading' ? (
          <div className="management-skeleton" aria-hidden="true">
            <div className="management-skeleton-line" />
            <div className="management-skeleton-line" />
            <div className="management-skeleton-line" />
          </div>
        ) : rows.length === 0 ? (
          <EmptyState title={t('op.reports.journal.empty')} />
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
      </div>
    </ManagementScreen>
  );
}
