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

  return (
    <main className="workspace-screen cash-shift-screen">
      {current ? (
        <div className="cash-shift-grid">
          {/* Выручка ведёт герой-тоталом смены — это главная цифра кокпита, не строка среди равных. */}
          <section className="ui-card ui-card--elevated cash-shift-card cash-shift-card--lead">
            <h2>{t('op.cash.shift.revenueTitle')}</h2>
            <strong className="cash-shift-hero"><Money minorUnits={current.earned.total.minorUnits} currencyCode={currencyCode} /></strong>
            <div className="cash-shift-rows">
              <div className="cash-shift-row"><span>{t('op.shifts.time')}</span><strong><Money minorUnits={current.earned.time.minorUnits} currencyCode={currencyCode} /></strong></div>
              <div className="cash-shift-row"><span>{t('op.shifts.goods')}</span><strong><Money minorUnits={current.earned.goods.minorUnits} currencyCode={currencyCode} /></strong></div>
              <div className="cash-shift-row"><span>{t('op.shifts.cash')}</span><strong><Money minorUnits={current.inflow.cash.minorUnits} currencyCode={currencyCode} /></strong></div>
              <div className="cash-shift-row"><span>{t('op.shifts.nonCash')}</span><strong><Money minorUnits={current.inflow.nonCash.minorUnits} currencyCode={currencyCode} /></strong></div>
              <div className="cash-shift-row"><span>{t('op.shifts.walletTopUps')}</span><strong><Money minorUnits={current.inflow.walletTopUps.minorUnits} currencyCode={currencyCode} /></strong></div>
            </div>
          </section>

          {/* Сверка ведёт расхождением — ответ кассира «сошлась ли касса». Красное если ≠0, спокойное если 0/не закрыта. */}
          <section className="ui-card ui-card--elevated cash-shift-card cash-shift-card--lead">
            <h2>{t('op.cash.shift.reconcileTitle')}</h2>
            <div className={`cash-shift-hero-block ${current.cash.difference === null ? 'muted' : current.cash.difference.minorUnits !== 0 ? 'attention' : 'ok'}`}>
              <span className="cash-shift-hero-label">{t('op.cash.shift.difference')}</span>
              <strong className="cash-shift-hero">{current.cash.difference === null ? t('op.cash.shift.notClosed') : <Money minorUnits={current.cash.difference.minorUnits} currencyCode={currencyCode} />}</strong>
            </div>
            <div className="cash-shift-rows">
              <div className="cash-shift-row"><span>{t('op.cash.shift.starting')}</span><strong><Money minorUnits={current.cash.starting.minorUnits} currencyCode={currencyCode} /></strong></div>
              <div className="cash-shift-row"><span>{t('op.cash.shift.expected')}</span><strong><Money minorUnits={current.cash.expected.minorUnits} currencyCode={currencyCode} /></strong></div>
              <div className="cash-shift-row"><span>{t('op.cash.shift.counted')}</span><strong>{current.cash.counted ? <Money minorUnits={current.cash.counted.minorUnits} currencyCode={currencyCode} /> : t('op.cash.shift.notClosed')}</strong></div>
            </div>
          </section>

          <section className="ui-card ui-card--elevated cash-shift-card">
            <h2>{t('op.cash.shift.movementsTitle')}</h2>
            {cashRows.length === 0 ? (
              <p className="cash-shift-empty-note">{t('op.cash.shift.movementsEmpty')}</p>
            ) : (
              <ul className="cash-shift-movements">
                {cashRows.slice(0, 6).map((row) => {
                  const impact = readMoney(row, 'cashImpact');
                  const negative = impact !== null && impact.minorUnits < 0;
                  return (
                    <li key={readString(row, 'operationId')} className={negative ? 'out' : 'in'}>
                      <span>{formatTime(readString(row, 'createdAtUtc'))}</span>
                      <strong>{cashOperationTypeLabel(readString(row, 'operationType', 'cash'), t)}</strong>
                      <b><Money minorUnits={impact?.minorUnits ?? 0} currencyCode={currencyCode} /></b>
                    </li>
                  );
                })}
              </ul>
            )}
          </section>

          <section className="ui-card ui-card--elevated cash-shift-card">
            <h2>{t('op.cash.shift.exportTitle')}</h2>
            <div className="cash-shift-exports">
              <button type="button" onClick={() => void exportCsv('shifts')}><ReceiptText size={15} aria-hidden="true" />{t('op.cash.shift.exportShiftSummary')}</button>
              <button type="button" onClick={() => void exportCsv('cash')}><Banknote size={15} aria-hidden="true" />{t('op.cash.shift.exportCashMovements')}</button>
              <button type="button" onClick={() => void exportCsv('sales')}><ArrowRightLeft size={15} aria-hidden="true" />{t('op.cash.shift.exportReceipts')}</button>
            </div>
            {exportError && <p className="cash-export-error" role="alert">{exportError}</p>}
          </section>
        </div>
      ) : (
        // op.cash.shift.empty = "Нет открытой смены" (единственное число, что ожидает тест)
        <section className="cash-shift-empty">{t('op.cash.shift.empty')}</section>
      )}

      <section className="ui-card ui-card--elevated cash-shift-card cash-shift-history">
        <h2>{t('op.cash.shift.historyTitle')}</h2>
        {history.length === 0 ? (
          <p className="cash-shift-empty-note">{t('op.cash.shift.historyEmpty')}</p>
        ) : (
          <ul className="cash-shift-hist-list">
            {history.map((s) => (
              <li key={s.shiftId} className="cash-shift-hist-row">
                <span className="cash-shift-hist-date">{new Date(s.openedAtUtc).toLocaleDateString('ru-RU')}</span>
                <span className="cash-shift-hist-cell"><em>{t('op.shifts.earned')}</em><b><Money minorUnits={s.earned.total.minorUnits} currencyCode={currencyCode} /></b></span>
                <span className={`cash-shift-hist-cell ${s.cash.difference !== null && s.cash.difference.minorUnits !== 0 ? 'attention' : ''}`}>
                  <em>{t('op.cash.shift.difference')}</em><b>{s.cash.difference === null ? '—' : <Money minorUnits={s.cash.difference.minorUnits} currencyCode={currencyCode} />}</b>
                </span>
              </li>
            ))}
          </ul>
        )}
      </section>
    </main>
  );
}
