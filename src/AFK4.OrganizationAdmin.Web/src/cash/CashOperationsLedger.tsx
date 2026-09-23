import { useEffect, useMemo, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { Download, Search } from 'lucide-react';
import {
  cashOperationTypeLabel,
  createAuthenticatedOperatorClients,
  downloadTextFile,
  formatTime
} from '../operatorHelpers';
import { projectOperatorError, type OperatorErrorProjection } from '../apiErrors';
import { EmptyState, Money, PartialLoadFailure } from '../operatorPrimitives';
import type { OperatorBackendContext } from '../operatorTypes';
import type { CashOperationReportResultDto, CashOperationReportRowDto } from '../operatorApiClients';
import { CashMetricStrip, CashRegisterRows, CashTerminalSkeleton, CashTerminalSplit } from './CashTerminalFrame';
import { DeferredSkeleton, SkeletonLine } from '../LoadingSkeleton';
import { filterCashOperationRows } from './cashTerminalModel';

interface LedgerReports {
  getCashOperationReport(branchId: string, query?: { limit?: number }): Promise<CashOperationReportResultDto>;
}

// Поисковая лента приходно-расходных кассовых операций (cash_in/cash_out) поверх getCashOperationReport.
// Сетку методов оплаты НЕ дублируем — она в кокпите «Смена» (inflow). Действия (внести/изъять) — в шапке.
export function CashOperationsLedger({
  backend,
  branchId,
  currencyCode,
  shiftNonce = 0,
  reports: injectedReports
}: {
  backend: OperatorBackendContext | null;
  branchId: string;
  currencyCode: string;
  shiftNonce?: number;
  reports?: LedgerReports;
}) {
  const { t } = useI18n();
  const reports = useMemo(
    () => injectedReports ?? (backend ? createAuthenticatedOperatorClients(backend.config, backend.session).shifts : null),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [backend?.config, backend?.session, injectedReports]
  );

  const [report, setReport] = useState<CashOperationReportResultDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<OperatorErrorProjection | null>(null);
  const [exportError, setExportError] = useState<string | null>(null);
  const [query, setQuery] = useState('');
  const [operationType, setOperationType] = useState('all');
  const [selectedId, setSelectedId] = useState('');
  const [reloadNonce, setReloadNonce] = useState(0);

  useEffect(() => {
    if (reports === null) return undefined;
    let active = true;
    setLoading(true);
    setLoadError(null);
    reports.getCashOperationReport(branchId, { limit: 50 })
      .then((result) => { if (active) setReport(result); })
      .catch((error) => { if (active) setLoadError(projectOperatorError(error, t)); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [reports, branchId, shiftNonce, reloadNonce]);

  const rows: CashOperationReportRowDto[] = report?.rows ?? [];
  const filtered = filterCashOperationRows(rows, query, operationType, (type) => cashOperationTypeLabel(type, t));
  const selected = filtered.find((row) => row.operationId === selectedId) ?? null;

  const exportCsv = async () => {
    if (backend === null) return;
    try {
      const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
      const stamp = new Date().toISOString().replace(/[:.]/g, '-');
      downloadTextFile(`afk4-cash-operations-${stamp}.csv`, await clients.shifts.exportCashOperationReportCsv(branchId, { limit: 200 }), 'text/csv;charset=utf-8');
      setExportError(null);
    } catch (error) {
      setExportError(projectOperatorError(error, t).detail);
    }
  };

  if (loading) {
    return (
      <DeferredSkeleton>
        <CashTerminalSkeleton
          className="cash-operations-terminal"
          metrics={3}
          search
          inspectorHint={t('op.cash.journal.selectHint')}
          row={
            <div className="ui-ledger-row cash-operation-row">
              <span className="ui-ledger-time"><SkeletonLine width="3em" /></span>
              <div className="ui-ledger-body"><span className="ui-ledger-title"><SkeletonLine width="7em" /></span><span className="ui-ledger-detail"><SkeletonLine width="12em" /></span></div>
              <span className="ui-ledger-aside"><SkeletonLine width="4em" /></span>
            </div>
          }
        />
      </DeferredSkeleton>
    );
  }
  if (loadError) return <section className="cash-ledger-failure"><PartialLoadFailure text={loadError.detail} failure={loadError} onRetry={() => setReloadNonce((value) => value + 1)} /></section>;

  return (
    <section className="cash-operations-terminal">
      <CashMetricStrip ariaLabel={t('op.cash.journal.metricsAria')} items={[
        { label: t('op.cash.journal.net'), value: <Money minorUnits={report?.netCashTotal.minorUnits ?? 0} currencyCode={currencyCode} /> },
        { label: t('op.cash.journal.cashIn'), value: <Money minorUnits={report?.cashInTotal.minorUnits ?? 0} currencyCode={currencyCode} />, tone: 'positive' },
        { label: t('op.cash.journal.cashOut'), value: <Money minorUnits={report?.cashOutTotal.minorUnits ?? 0} currencyCode={currencyCode} />, tone: 'danger' }
      ]} />
      <CashTerminalSplit
          inspectorLabel={t('op.cash.inspector.aria')}
        inspectorOpen={selected !== null}
        closeLabel={t('common.close')}
        onCloseInspector={() => setSelectedId('')}
        register={<>
          <div className="cash-ledger-search">
            <Search size={14} aria-hidden="true" />
            <input value={query} onChange={(event) => setQuery(event.currentTarget.value)} placeholder={t('op.cash.journal.searchPlaceholder')} aria-label={t('op.cash.journal.searchPlaceholder')} />
            <select value={operationType} onChange={(event) => setOperationType(event.currentTarget.value)} aria-label={t('op.cash.journal.typeFilter')}>
              <option value="all">{t('op.cash.journal.typeAll')}</option>
              <option value="cash_in">{t('op.cash.journal.cashIn')}</option>
              <option value="cash_out">{t('op.cash.journal.cashOut')}</option>
            </select>
            <span className="cash-ledger-result-count">{filtered.length} {filtered.length === 1 ? t('op.cash.journal.operationOne') : t('op.cash.journal.operationMany')}</span>
            <button type="button" className="cash-ledger-export" onClick={() => void exportCsv()}><Download size={14} aria-hidden="true" />{t('op.cash.journal.export')}</button>
          </div>
          {exportError && <p className="cash-export-error" role="alert">{exportError}</p>}
          {filtered.length === 0 ? (rows.length === 0
            ? <EmptyState inline className="cash-shift-empty-note cash-ledger-empty" title={t('op.cash.journal.empty')} next={{ kind: 'calm', hint: t('op.cash.journal.emptyHint') }} />
            : <EmptyState inline className="cash-shift-empty-note cash-ledger-empty" title={t('op.cash.journal.noMatch')} next={{ kind: 'action', label: t('op.empty.resetFilter'), onClick: () => { setQuery(''); setOperationType('all'); } }} />) : <CashRegisterRows rows={filtered} selectedId={selectedId} getId={(row) => row.operationId} onSelect={setSelectedId} ariaLabel={t('op.cash.journal.registerAria')} renderRow={(row) => {
            return <div className="ui-ledger-row cash-operation-row">
                <span className="ui-ledger-time">{formatTime(row.createdAtUtc)}</span>
                <div className="ui-ledger-body">
                  <span className="ui-ledger-title">{cashOperationTypeLabel(row.operationType || 'cash', t)}</span>
                  <span className="ui-ledger-detail">{row.reason}</span>
                </div>
                <span className="ui-ledger-aside">
                  <Money minorUnits={row.cashImpact.minorUnits} currencyCode={currencyCode} signed />
                </span>
              </div>;
          }} />}
        </>}
        inspector={selected ? <div className="cash-operation-inspector">
          <p>{cashOperationTypeLabel(selected.operationType || 'cash', t)}</p>
          <h2>{selected.reason || '—'}</h2>
          <strong><Money minorUnits={selected.cashImpact.minorUnits} currencyCode={currencyCode} signed /></strong>
          <dl>
            <div><dt>{t('op.cash.journal.operationId')}</dt><dd>{selected.operationId}</dd></div>
            <div><dt>{t('op.cash.journal.createdAt')}</dt><dd>{new Date(selected.createdAtUtc).toLocaleString('ru-RU')}</dd></div>
            <div><dt>{t('op.cash.journal.actor')}</dt><dd>{selected.createdByDisplayName || '—'}</dd></div>
            {selected.sourceType ? <div><dt>{t('op.cash.journal.source')}</dt><dd>{selected.sourceType}</dd></div> : null}
          </dl>
        </div> : <p className="cash-inspector-empty">{t('op.cash.journal.selectHint')}</p>}
      />
    </section>
  );
}
