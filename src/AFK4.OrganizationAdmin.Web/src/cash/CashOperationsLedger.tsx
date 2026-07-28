import { useEffect, useMemo, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { Download, Search } from 'lucide-react';
import {
  cashOperationTypeLabel,
  createAuthenticatedOperatorClients,
  downloadTextFile,
  formatTime,
  readArray,
  readMoney,
  readString
} from '../operatorHelpers';
import { projectOperatorError } from '../apiErrors';
import { Money } from '../operatorPrimitives';
import type { OperatorBackendContext } from '../operatorTypes';
import { CashMetricStrip, CashRegisterRows, CashTerminalSplit } from './CashTerminalFrame';
import { filterCashOperationRows } from './cashTerminalModel';

interface LedgerReports {
  getCashOperationReport(branchId: string, query?: { limit?: number }): Promise<Record<string, unknown>>;
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

  const [report, setReport] = useState<Record<string, unknown> | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
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
      .catch((error) => { if (active) setLoadError(projectOperatorError(error, t).detail); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [reports, branchId, shiftNonce, reloadNonce]);

  const rows = readArray<Record<string, unknown>>(report, 'rows');
  const filtered = filterCashOperationRows(rows, query, operationType, (type) => cashOperationTypeLabel(type, t));
  const selected = filtered.find((row) => readString(row, 'operationId') === selectedId) ?? null;

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

  if (loading) return <p className="workspace-loading">{t('op.cash.journal.loading')}</p>;
  if (loadError) return <section className="cash-ledger-failure"><p className="workspace-error" role="alert">{loadError}</p><button type="button" onClick={() => setReloadNonce((value) => value + 1)}>{t('op.cash.journal.retry')}</button></section>;

  return (
    <section className="cash-operations-terminal">
      <CashMetricStrip ariaLabel={t('op.cash.journal.metricsAria')} items={[
        { label: t('op.cash.journal.net'), value: <Money minorUnits={readMoney(report, 'netCashTotal')?.minorUnits ?? 0} currencyCode={currencyCode} /> },
        { label: t('op.cash.journal.cashIn'), value: <Money minorUnits={readMoney(report, 'cashInTotal')?.minorUnits ?? 0} currencyCode={currencyCode} />, tone: 'positive' },
        { label: t('op.cash.journal.cashOut'), value: <Money minorUnits={readMoney(report, 'cashOutTotal')?.minorUnits ?? 0} currencyCode={currencyCode} />, tone: 'danger' }
      ]} />
      <CashTerminalSplit
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
          {filtered.length === 0 ? <p className="cash-shift-empty-note cash-ledger-empty">{rows.length === 0 ? t('op.cash.journal.empty') : t('op.cash.journal.noMatch')}</p> : <CashRegisterRows rows={filtered} selectedId={selectedId} getId={(row) => readString(row, 'operationId')} onSelect={setSelectedId} ariaLabel={t('op.cash.journal.registerAria')} renderRow={(row) => {
            const impact = readMoney(row, 'cashImpact');
            return <div className="ui-ledger-row cash-operation-row">
                <span className="ui-ledger-time">{formatTime(readString(row, 'createdAtUtc'))}</span>
                <div className="ui-ledger-body">
                  <span className="ui-ledger-title">{cashOperationTypeLabel(readString(row, 'operationType', 'cash'), t)}</span>
                  <span className="ui-ledger-detail">{readString(row, 'reason')}</span>
                </div>
                <span className="ui-ledger-aside">
                  <Money minorUnits={impact?.minorUnits ?? 0} currencyCode={currencyCode} signed />
                </span>
              </div>;
          }} />}
        </>}
        inspector={selected ? <div className="cash-operation-inspector">
          <p>{cashOperationTypeLabel(readString(selected, 'operationType', 'cash'), t)}</p>
          <h2>{readString(selected, 'reason', '—')}</h2>
          <strong><Money minorUnits={readMoney(selected, 'cashImpact')?.minorUnits ?? 0} currencyCode={currencyCode} signed /></strong>
          <dl>
            <div><dt>{t('op.cash.journal.operationId')}</dt><dd>{readString(selected, 'operationId')}</dd></div>
            <div><dt>{t('op.cash.journal.createdAt')}</dt><dd>{new Date(readString(selected, 'createdAtUtc')).toLocaleString('ru-RU')}</dd></div>
            {readString(selected, 'sourceType') ? <div><dt>{t('op.cash.journal.source')}</dt><dd>{readString(selected, 'sourceType')}</dd></div> : null}
            {readString(selected, 'actorName') ? <div><dt>{t('op.cash.journal.actor')}</dt><dd>{readString(selected, 'actorName')}</dd></div> : null}
            {readMoney(selected, 'runningBalance') ? <div><dt>{t('op.cash.journal.runningBalance')}</dt><dd><Money minorUnits={readMoney(selected, 'runningBalance')!.minorUnits} currencyCode={currencyCode} /></dd></div> : null}
          </dl>
        </div> : <p className="cash-inspector-empty">{t('op.cash.journal.selectHint')}</p>}
      />
    </section>
  );
}
