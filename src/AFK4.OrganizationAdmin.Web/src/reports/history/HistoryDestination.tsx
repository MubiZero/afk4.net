import { useMemo, useState } from 'react';
import type { JSX } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen } from '../../management/ManagementScreen';
import { createAuthenticatedOperatorClients, downloadTextFile, formatMinorUnits } from '../../operatorHelpers';
import type { OperatorBackendContext } from '../../operatorTypes';
import { presetRange, isoToDateInput, dateInputToFromUtc, dateInputToToUtc, type DateRange } from '../../network/journal/dateRange';
import { historyReports, type HistoryReportId, type OperatorClients } from './reportTypes';
import { useReport } from './useReport';
import type { ReportFormatters } from './reportModel';
import { ReportTable } from './ReportTable';

export function HistoryDestination({ backend }: { backend: OperatorBackendContext | null }): JSX.Element {
  const { t, formatNumber, formatDate } = useI18n();
  const [activeId, setActiveId] = useState<HistoryReportId>('shifts');
  const [range, setRange] = useState<DateRange>(() => presetRange('30d', new Date()));

  const spec = historyReports.find((r) => r.id === activeId) ?? historyReports[0];

  const clients = useMemo<OperatorClients | null>(() => {
    if (backend === null) return null;
    return createAuthenticatedOperatorClients(backend.config, backend.session);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [backend?.config.platformBaseUrl, backend?.session.accessToken]);

  const fmt: ReportFormatters = { formatMinorUnits, formatNumber, formatDate };
  const branchId = backend?.branchId ?? '';

  const state = useReport(
    () => (clients === null ? Promise.resolve({}) : spec.load(clients, branchId, range)),
    spec.build,
    fmt,
    [activeId, branchId, range.fromUtc, range.toUtc, clients]
  );

  async function handleExport() {
    if (clients === null) return;
    const csv = await spec.exportCsv(clients, branchId, range);
    // downloadTextFile(fileName, contents) — имя первым (сверено в operatorHelpers.ts).
    downloadTextFile(`${spec.csvName}-${isoToDateInput(range.fromUtc)}_${isoToDateInput(range.toUtc)}.csv`, csv);
  }

  const screenState = backend === null ? 'loading' : state.status === 'error' ? 'error' : 'ready';

  return (
    <ManagementScreen
      title={t('op.reports.dest.history')}
      subtitle={t('op.reports.dest.history.subtitle')}
      contentWidth="full"
      state={screenState}
      onRetry={state.status === 'error' ? state.retry : undefined}
    >
      <div className="reports-history">
        <div className="reports-history-tabs">
          {historyReports.map((r) => (
            <button
              key={r.id}
              type="button"
              className={r.id === activeId ? 'ui-btn ui-btn--primary' : 'ui-btn'}
              onClick={() => setActiveId(r.id)}
            >
              {t(r.labelKey)}
            </button>
          ))}
        </div>

        <div className="reports-history-range mgmt-form">
          <label>
            {t('op.network.journal.range.from')}
            <input type="date" value={isoToDateInput(range.fromUtc)}
              onChange={(e) => setRange((prev) => ({ fromUtc: dateInputToFromUtc(e.currentTarget.value), toUtc: prev.toUtc }))} />
          </label>
          <label>
            {t('op.network.journal.range.to')}
            <input type="date" value={isoToDateInput(range.toUtc)}
              onChange={(e) => setRange((prev) => ({ fromUtc: prev.fromUtc, toUtc: dateInputToToUtc(e.currentTarget.value) }))} />
          </label>
        </div>

        {state.status === 'loading' ? (
          <div className="management-skeleton" aria-hidden="true">
            <div className="management-skeleton-line" />
            <div className="management-skeleton-line" />
            <div className="management-skeleton-line" />
          </div>
        ) : state.status === 'ready' ? (
          <ReportTable view={state.view} onExport={handleExport} />
        ) : null}
      </div>
    </ManagementScreen>
  );
}
