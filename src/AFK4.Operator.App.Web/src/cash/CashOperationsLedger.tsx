import { useEffect, useMemo, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { Download, Search } from 'lucide-react';
import {
  cashOperationTypeLabel,
  createAuthenticatedOperatorClients,
  downloadTextFile,
  formatMoney,
  formatTime,
  readArray,
  readMoney,
  readString
} from '../operatorHelpers';
import { projectOperatorError } from '../apiErrors';
import type { OperatorBackendContext } from '../operatorTypes';

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
  }, [reports, branchId, shiftNonce]);

  const rows = readArray<Record<string, unknown>>(report, 'rows');
  const needle = query.trim().toLowerCase();
  const filtered = needle === ''
    ? rows
    : rows.filter((row) => {
        const type = cashOperationTypeLabel(readString(row, 'operationType', 'cash'), t).toLowerCase();
        return readString(row, 'reason').toLowerCase().includes(needle) || type.includes(needle);
      });

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
  if (loadError) return <p className="workspace-error" role="alert">{loadError}</p>;

  return (
    <section className="cash-ledger">
      <div className="cash-ledger-summary">
        <span><b>{t('op.cash.journal.cashIn')}</b> {formatMoney(readMoney(report, 'cashInTotal'), currencyCode)}</span>
        <span><b>{t('op.cash.journal.cashOut')}</b> {formatMoney(readMoney(report, 'cashOutTotal'), currencyCode)}</span>
        <span><b>{t('op.cash.journal.net')}</b> {formatMoney(readMoney(report, 'netCashTotal'), currencyCode)}</span>
      </div>
      <div className="cash-ledger-search">
        <Search size={14} aria-hidden="true" />
        <input
          value={query}
          onChange={(event) => setQuery(event.currentTarget.value)}
          placeholder={t('op.cash.journal.searchPlaceholder')}
          aria-label={t('op.cash.journal.searchPlaceholder')}
        />
        <button type="button" className="cash-ledger-export" onClick={() => void exportCsv()}>
          <Download size={14} aria-hidden="true" />{t('op.cash.journal.export')}
        </button>
      </div>
      {exportError && <p className="cash-export-error" role="alert">{exportError}</p>}
      {filtered.length === 0 ? (
        <p className="cash-shift-empty-note">{rows.length === 0 ? t('op.cash.journal.empty') : t('op.cash.journal.noMatch')}</p>
      ) : (
        <ul className="cash-ledger-list">
          {filtered.map((row) => {
            const impact = readMoney(row, 'cashImpact');
            const negative = impact !== null && impact.minorUnits < 0;
            return (
              <li key={readString(row, 'operationId')} className={`cash-ledger-row${negative ? ' out' : ' in'}`}>
                <span className="cash-ledger-time">{formatTime(readString(row, 'createdAtUtc'))}</span>
                <strong>{cashOperationTypeLabel(readString(row, 'operationType', 'cash'), t)}</strong>
                <em>{readString(row, 'reason')}</em>
                <b>{formatMoney(impact, currencyCode)}</b>
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}
