import { useState, type JSX } from 'react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { ManagementScreen } from '../management/ManagementScreen';
import { MgmtTable } from '../management/kit/MgmtTable';
import { downloadTextFile, operatorDisplayNameLabel } from '../operatorHelpers';
import type { OperatorActionReportResultDto } from '../api/clients/shifts';
import type { OperatorBackendContext } from '../operatorTypes';
import { ReportBody, ReportFiguresSkeleton, ReportRangeControls } from './ReportRangeControls';
import { SkeletonTable } from '../LoadingSkeleton';
import { todayReportRange, toReportInstantQuery, type ReportDateRange } from './reportRange';
import { createDetailReportClients } from './reportClient';
import { useReportData } from './useReportData';

/**
 * Кто из сотрудников что делал за период.
 *
 * Как и «Время игры», этот отчёт можно было заказать письмом, но открыть в кабинете — нет.
 * Владельцу он нужен ровно тогда, когда что-то разошлось: видно, кто отменял брони, кому чаще
 * всего отказывали в правах и в какие часы это происходило.
 */
const ACTIONS_GRID = 'minmax(160px, 1fr) minmax(200px, 1.4fr) 140px 120px 160px';

export function OperatorActionsReport({ backend }: { backend: OperatorBackendContext | null }): JSX.Element {
  const { t, formatDate, formatNumber } = useI18n();
  const [range, setRange] = useState<ReportDateRange>(() => todayReportRange());
  const { state, data, refreshing, error, reload } = useReportData(
    backend ? () => createDetailReportClients(backend).shifts.getOperatorActionReport(backend.branchId, toReportInstantQuery(range)) : null,
    [backend, range]
  );

  async function exportCsv() {
    if (!backend) return;
    const clients = createDetailReportClients(backend);
    const csv = await clients.shifts.exportOperatorActionReportCsv(backend.branchId, toReportInstantQuery(range));
    downloadTextFile(`operator-actions-${range.from}-${range.to}.csv`, csv);
  }

  return (
    <ManagementScreen
      title={t('op.reports.actions.title')}
      subtitle={t('op.reports.actions.subtitle')}
      contentWidth="full"
      state={state}
      skeleton={
        <>
          <ReportFiguresSkeleton count={1} />
          <SkeletonTable gridTemplate={ACTIONS_GRID} />
        </>
      }
      failure={error}
      onRetry={reload}
      controls={<ReportRangeControls range={range} onChange={setRange} onRefresh={reload} onExport={() => void exportCsv()} refreshing={refreshing} />}
    >
      {data ? (
        <ReportBody refreshing={refreshing}>
          <dl className="reports-figures">
            <div><dt>{t('op.reports.actions.total')}</dt><dd>{formatNumber(data.totalActionCount)}</dd></div>
          </dl>
          <MgmtTable<OperatorActionReportResultDto['rows'][number]>
            columns={[
              { key: 'staff', header: t('op.reports.actions.col.staff'), render: (row) => operatorDisplayNameLabel(row.actorDisplayName, t) },
              { key: 'action', header: t('op.reports.actions.col.action'), render: (row) => row.action },
              { key: 'outcome', header: t('op.reports.actions.col.outcome'), render: (row) => t(outcomeKey(row.outcome)) },
              { key: 'count', header: t('op.reports.actions.col.count'), align: 'end', render: (row) => formatNumber(row.count) },
              { key: 'last', header: t('op.reports.actions.col.last'), align: 'end', render: (row) => formatDate(row.lastAtUtc) }
            ]}
            rows={data.rows}
            rowKey={(row) => `${row.actorStaffUserId ?? 'system'}-${row.action}-${row.outcome}`}
            gridTemplate={ACTIONS_GRID}
            empty={{ title: t('op.reports.empty'), next: { kind: 'elsewhere', hint: t('op.reports.emptyHint') } }}
          />
          {data.rows.length >= data.limit
            ? <p className="mgmt-drawer-hint">{t('op.reports.truncated', { count: data.limit })}</p>
            : null}
        </ReportBody>
      ) : null}
    </ManagementScreen>
  );
}

function outcomeKey(outcome: string): MessageKey {
  const normalized = outcome.toLowerCase();
  if (normalized === 'denied') return 'op.reports.actions.outcome.denied';
  if (normalized === 'failed') return 'op.reports.actions.outcome.failed';
  return 'op.reports.actions.outcome.succeeded';
}
