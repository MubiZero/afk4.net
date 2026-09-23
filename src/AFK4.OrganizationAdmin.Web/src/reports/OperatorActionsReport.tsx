import { useCallback, useEffect, useState, type JSX } from 'react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { ManagementScreen } from '../management/ManagementScreen';
import { MgmtTable } from '../management/kit/MgmtTable';
import { downloadTextFile, operatorDisplayNameLabel } from '../operatorHelpers';
import { projectOperatorError, type OperatorErrorProjection } from '../apiErrors';
import type { OperatorActionReportResultDto } from '../api/clients/shifts';
import type { OperatorBackendContext } from '../operatorTypes';
import { ReportRangeControls } from './ReportRangeControls';
import { todayReportRange, toReportInstantQuery, type ReportDateRange } from './reportRange';
import { createDetailReportClients } from './reportClient';

/**
 * Кто из сотрудников что делал за период.
 *
 * Как и «Время игры», этот отчёт можно было заказать письмом, но открыть в кабинете — нет.
 * Владельцу он нужен ровно тогда, когда что-то разошлось: видно, кто отменял брони, кому чаще
 * всего отказывали в правах и в какие часы это происходило.
 */
export function OperatorActionsReport({ backend }: { backend: OperatorBackendContext | null }): JSX.Element {
  const { t, formatDate, formatNumber } = useI18n();
  const [range, setRange] = useState<ReportDateRange>(() => todayReportRange());
  const [data, setData] = useState<OperatorActionReportResultDto | null>(null);
  const [state, setState] = useState<'loading' | 'ready' | 'error'>('loading');
  const [error, setError] = useState<OperatorErrorProjection | undefined>();

  const load = useCallback(async () => {
    if (!backend) { setState('error'); setError(projectOperatorError(t('op.reports.backendRequired'), t)); return; }
    setState('loading');
    try {
      const clients = createDetailReportClients(backend);
      setData(await clients.shifts.getOperatorActionReport(backend.branchId, toReportInstantQuery(range)));
      setState('ready');
    } catch (reason) { setError(projectOperatorError(reason, t)); setState('error'); }
  }, [backend, range, t]);
  useEffect(() => { void load(); }, [load]);

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
      failure={error}
      onRetry={() => void load()}
    >
      <ReportRangeControls range={range} onChange={setRange} onRefresh={() => void load()} onExport={() => void exportCsv()} />
      {data ? (
        <>
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
            gridTemplate="minmax(160px, 1fr) minmax(200px, 1.4fr) 140px 120px 160px"
            empty={{ title: t('op.reports.empty'), next: { kind: 'elsewhere', hint: t('op.reports.emptyHint') } }}
          />
          {data.rows.length >= data.limit
            ? <p className="mgmt-drawer-hint">{t('op.reports.truncated', { count: data.limit })}</p>
            : null}
        </>
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
