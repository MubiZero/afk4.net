import { useEffect, useMemo, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { ReceiptText, Banknote, ArrowRightLeft } from 'lucide-react';
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
import type { ShiftRevenueDto } from '../operatorApiClients';
import { CashMetricStrip, CashRegisterRows, CashTerminalSplit } from './CashTerminalFrame';

interface ShiftCockpitClient {
  current(branchId: string): Promise<ShiftRevenueDto | null>;
  history(branchId: string, limit?: number): Promise<{ shifts: ShiftRevenueDto[]; limit: number }>;
}
interface ShiftCockpitReports {
  getCashOperationReport(branchId: string, query?: { limit?: number }): Promise<Record<string, unknown>>;
}

// Вкладка «Смена»: кокпит кассира — выручка + сверка из shiftRevenue, последние движения наличных,
// CSV-экспорты, история. Действия (открыть/закрыть/внести/изъять) — в шапке-якоре, не здесь.
// Полная поисковая лента операций + сетка методов отложены в S2 «Журнал кассы».
export function CashShiftWorkspace({
  backend,
  branchId,
  currencyCode,
  shiftNonce = 0,
  revenueClient: injectedRevenue,
  reports: injectedReports
}: {
  backend: OperatorBackendContext | null;
  branchId: string;
  currencyCode: string;
  shiftNonce?: number;
  revenueClient?: ShiftCockpitClient;
  reports?: ShiftCockpitReports;
}) {
  const { t } = useI18n();
  const built = useMemo(
    () => (backend && (!injectedRevenue || !injectedReports) ? createAuthenticatedOperatorClients(backend.config, backend.session) : null),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [backend?.config, backend?.session, injectedRevenue, injectedReports]
  );
  const revenueClient = injectedRevenue ?? built?.shiftRevenue ?? null;
  const reports = injectedReports ?? (built?.shifts ?? null);

  const [current, setCurrent] = useState<ShiftRevenueDto | null>(null);
  const [history, setHistory] = useState<ShiftRevenueDto[]>([]);
  const [cashRows, setCashRows] = useState<Record<string, unknown>[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [exportError, setExportError] = useState<string | null>(null);
  const [selectedShiftId, setSelectedShiftId] = useState('');

  useEffect(() => {
    if (revenueClient === null || reports === null) return undefined;
    let active = true;
    setLoading(true);
    setLoadError(null);
    Promise.all([
      revenueClient.current(branchId),
      revenueClient.history(branchId, 20),
      reports.getCashOperationReport(branchId, { limit: 6 })
    ])
      .then(([cur, hist, cash]) => {
        if (!active) return;
        setCurrent(cur);
        setHistory(hist.shifts.filter((s) => s.state === 'closed'));
        setCashRows(readArray<Record<string, unknown>>(cash, 'rows'));
      })
      .catch((error) => { if (active) setLoadError(projectOperatorError(error, t).detail); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [revenueClient, reports, branchId, shiftNonce]);

  const exportCsv = async (kind: 'shifts' | 'cash' | 'sales') => {
    if (backend === null) return;
    try {
      // Клиент строится lazy, только при клике — не на рендере.
      const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
      const stamp = new Date().toISOString().replace(/[:.]/g, '-');
      if (kind === 'shifts') {
        downloadTextFile(`afk4-shift-summary-${stamp}.csv`, await clients.shifts.exportShiftReportCsv(branchId, { limit: 50 }), 'text/csv;charset=utf-8');
      } else if (kind === 'cash') {
        downloadTextFile(`afk4-cash-movements-${stamp}.csv`, await clients.shifts.exportCashOperationReportCsv(branchId, { limit: 50 }), 'text/csv;charset=utf-8');
      } else {
        downloadTextFile(`afk4-check-list-${stamp}.csv`, await clients.shifts.exportSalesReportCsv(branchId, { limit: 50 }), 'text/csv;charset=utf-8');
      }
      setExportError(null);
    } catch (error) {
      setExportError(projectOperatorError(error, t).detail);
    }
  };

  if (loading) {
    return <main className="workspace-screen cash-shift-screen"><p className="workspace-loading">{t('op.shifts.loading')}</p></main>;
  }
  if (loadError) {
    return (
      <main className="workspace-screen cash-shift-screen">
        <p className="workspace-error" role="alert">{loadError}</p>
      </main>
    );
  }

  const selectedShift = history.find((shift) => shift.shiftId === selectedShiftId)
    ?? (current === null ? history[0] : null);
  const metricShift = current ?? selectedShift;
  const revenueMaximum = metricShift
    ? Math.max(metricShift.earned.time.minorUnits, metricShift.earned.goods.minorUnits, 1)
    : 1;

  const shiftInspector = selectedShift ? (
    <div className="cash-shift-inspector-content">
      <p className="cash-shift-inspector-kicker">{current === null ? t('op.cash.shift.lastClosed') : t('op.cash.shift.selectedClosed')}</p>
      <h2>{new Date(selectedShift.openedAtUtc).toLocaleDateString('ru-RU')}</h2>
      <div className="cash-shift-rows">
        <div className="cash-shift-row"><span>{t('op.shifts.earned')}</span><strong><Money minorUnits={selectedShift.earned.total.minorUnits} currencyCode={currencyCode} /></strong></div>
        <div className="cash-shift-row"><span>{t('op.cash.shift.expected')}</span><strong><Money minorUnits={selectedShift.cash.expected.minorUnits} currencyCode={currencyCode} /></strong></div>
        <div className="cash-shift-row"><span>{t('op.cash.shift.counted')}</span><strong>{selectedShift.cash.counted ? <Money minorUnits={selectedShift.cash.counted.minorUnits} currencyCode={currencyCode} /> : '—'}</strong></div>
        <div className={`cash-shift-row ${selectedShift.cash.difference?.minorUnits ? 'attention' : ''}`}><span>{t('op.cash.shift.difference')}</span><strong>{selectedShift.cash.difference ? <Money minorUnits={selectedShift.cash.difference.minorUnits} currencyCode={currencyCode} /> : '—'}</strong></div>
      </div>
    </div>
  ) : (
    <div className="cash-shift-inspector-content cash-shift-inspector-content--quiet">
      <p className="cash-shift-inspector-kicker">{t('op.cash.shift.exportTitle')}</p>
      <h2>{t('op.cash.shift.exportHint')}</h2>
      <div className="cash-shift-exports">
        <button type="button" onClick={() => void exportCsv('shifts')}><ReceiptText size={15} aria-hidden="true" />{t('op.cash.shift.exportShiftSummary')}</button>
        <button type="button" onClick={() => void exportCsv('cash')}><Banknote size={15} aria-hidden="true" />{t('op.cash.shift.exportCashMovements')}</button>
        <button type="button" onClick={() => void exportCsv('sales')}><ArrowRightLeft size={15} aria-hidden="true" />{t('op.cash.shift.exportReceipts')}</button>
      </div>
      {exportError && <p className="cash-export-error" role="alert">{exportError}</p>}
    </div>
  );

  return (
    <main className="workspace-screen cash-shift-screen">
      {metricShift ? <CashMetricStrip ariaLabel={t('op.cash.shift.metricsAria')} items={[
        { label: t('op.shifts.earned'), value: <Money minorUnits={metricShift.earned.total.minorUnits} currencyCode={currencyCode} />, tone: 'positive' },
        { label: t('op.shifts.cash'), value: <Money minorUnits={metricShift.inflow.cash.minorUnits} currencyCode={currencyCode} /> },
        { label: t('op.cash.shift.expectedMetric'), value: <Money minorUnits={metricShift.cash.expected.minorUnits} currencyCode={currencyCode} /> },
        { label: t('op.cash.shift.difference'), value: metricShift.cash.difference ? <Money minorUnits={metricShift.cash.difference.minorUnits} currencyCode={currencyCode} /> : t('op.cash.shift.notClosed'), tone: metricShift.cash.difference?.minorUnits ? 'danger' : 'default' }
      ]} /> : null}

      <CashTerminalSplit
        inspector={shiftInspector}
        inspectorOpen={selectedShift !== null}
        closeLabel={t('common.close')}
        onCloseInspector={() => setSelectedShiftId('')}
        register={<div className="cash-shift-register">
          {current ? <div className="cash-shift-primary-grid">
            <section className="cash-shift-panel">
              <div className="cash-shift-section-head"><h2>{t('op.cash.shift.revenueTitle')}</h2><strong><Money minorUnits={current.earned.total.minorUnits} currencyCode={currencyCode} /></strong></div>
              {[{ label: t('op.shifts.time'), amount: current.earned.time.minorUnits }, { label: t('op.shifts.goods'), amount: current.earned.goods.minorUnits }].map((part) => (
                <div className="cash-shift-revenue-row" key={part.label}><span>{part.label}</span><i><b style={{ width: `${Math.max(4, part.amount / revenueMaximum * 100)}%` }} /></i><strong><Money minorUnits={part.amount} currencyCode={currencyCode} /></strong></div>
              ))}
              <div className="cash-shift-methods">
                <span>{t('op.shifts.cash')} <b><Money minorUnits={current.inflow.cash.minorUnits} currencyCode={currencyCode} /></b></span>
                <span>{t('op.shifts.nonCash')} <b><Money minorUnits={current.inflow.nonCash.minorUnits} currencyCode={currencyCode} /></b></span>
                <span>{t('op.shifts.walletTopUps')} <b><Money minorUnits={current.inflow.walletTopUps.minorUnits} currencyCode={currencyCode} /></b></span>
              </div>
            </section>
            <section className="cash-shift-panel cash-shift-reconcile">
              <div className="cash-shift-section-head"><h2>{t('op.cash.shift.reconcileTitle')}</h2><strong className="muted">{t('op.cash.shift.notClosed')}</strong></div>
              <p className="cash-shift-formula">{t('op.cash.shift.formula')}</p>
              <div className="cash-shift-rows">
                <div className="cash-shift-row"><span>{t('op.cash.shift.starting')}</span><strong><Money minorUnits={current.cash.starting.minorUnits} currencyCode={currencyCode} /></strong></div>
                <div className="cash-shift-row"><span>{t('op.cash.shift.expected')}</span><strong><Money minorUnits={current.cash.expected.minorUnits} currencyCode={currencyCode} /></strong></div>
                <div className="cash-shift-row"><span>{t('op.cash.shift.counted')}</span><strong>{current.cash.counted ? <Money minorUnits={current.cash.counted.minorUnits} currencyCode={currencyCode} /> : t('op.cash.shift.notClosed')}</strong></div>
              </div>
            </section>
          </div> : <section className="cash-shift-no-open"><span>{t('op.cash.shift.empty')}</span><h2>{t('op.cash.shift.noOpenNow')}</h2><p>{history.length ? t('op.cash.shift.noOpenHint') : t('op.cash.shift.historyEmpty')}</p></section>}

          <section className="cash-shift-panel">
            <div className="cash-shift-section-head"><h2>{t('op.cash.shift.movementsTitle')}</h2><span>{cashRows.length}</span></div>
            {cashRows.length === 0 ? <p className="cash-shift-empty-note">{t('op.cash.shift.movementsEmpty')}</p> : <ul className="cash-shift-movements">
              {cashRows.slice(0, 6).map((row) => {
                const impact = readMoney(row, 'cashImpact');
                const negative = impact !== null && impact.minorUnits < 0;
                return <li key={readString(row, 'operationId')} className={negative ? 'out' : 'in'}><span>{formatTime(readString(row, 'createdAtUtc'))}</span><strong>{cashOperationTypeLabel(readString(row, 'operationType', 'cash'), t)}<small>{readString(row, 'reason', '—')}</small></strong><b><Money minorUnits={impact?.minorUnits ?? 0} currencyCode={currencyCode} /></b></li>;
              })}
            </ul>}
          </section>

          <section className="cash-shift-history-register">
            <div className="cash-shift-section-head"><h2>{t('op.cash.shift.historyTitle')}</h2><span>{history.length}</span></div>
            {history.length === 0 ? <p className="cash-shift-empty-note">{t('op.cash.shift.historyEmpty')}</p> : <CashRegisterRows rows={history} selectedId={selectedShift?.shiftId ?? ''} getId={(shift) => shift.shiftId} onSelect={setSelectedShiftId} ariaLabel={t('op.cash.shift.historyTitle')} renderRow={(shift) => <>
              <span className="cash-shift-hist-date">{new Date(shift.openedAtUtc).toLocaleDateString('ru-RU')}</span>
              <span className="cash-shift-hist-cell"><em>{t('op.shifts.earned')}</em><b><Money minorUnits={shift.earned.total.minorUnits} currencyCode={currencyCode} /></b></span>
              <span className={`cash-shift-hist-cell ${shift.cash.difference?.minorUnits ? 'attention' : ''}`}><em>{t('op.cash.shift.difference')}</em><b>{shift.cash.difference ? <Money minorUnits={shift.cash.difference.minorUnits} currencyCode={currencyCode} /> : '—'}</b></span>
            </>} />}
          </section>
        </div>}
      />
    </main>
  );
}
