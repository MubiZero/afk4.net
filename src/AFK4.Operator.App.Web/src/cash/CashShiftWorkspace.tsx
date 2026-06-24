import { useEffect, useMemo, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { ReceiptText, Banknote, ArrowRightLeft } from 'lucide-react';
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
          <section className="cash-shift-card">
            <h2>{t('op.cash.shift.revenueTitle')}</h2>
            <div className="cash-shift-row"><span>{t('op.shifts.earned')}</span><strong>{formatMoney(current.earned.total, currencyCode)}</strong></div>
            <div className="cash-shift-row"><span>{t('op.shifts.time')}</span><strong>{formatMoney(current.earned.time, currencyCode)}</strong></div>
            <div className="cash-shift-row"><span>{t('op.shifts.goods')}</span><strong>{formatMoney(current.earned.goods, currencyCode)}</strong></div>
            <div className="cash-shift-row"><span>{t('op.shifts.cash')}</span><strong>{formatMoney(current.inflow.cash, currencyCode)}</strong></div>
            <div className="cash-shift-row"><span>{t('op.shifts.nonCash')}</span><strong>{formatMoney(current.inflow.nonCash, currencyCode)}</strong></div>
            <div className="cash-shift-row"><span>{t('op.shifts.walletTopUps')}</span><strong>{formatMoney(current.inflow.walletTopUps, currencyCode)}</strong></div>
          </section>

          <section className="cash-shift-card">
            <h2>{t('op.cash.shift.reconcileTitle')}</h2>
            <div className="cash-shift-row"><span>{t('op.cash.shift.starting')}</span><strong>{formatMoney(current.cash.starting, currencyCode)}</strong></div>
            <div className="cash-shift-row"><span>{t('op.cash.shift.expected')}</span><strong>{formatMoney(current.cash.expected, currencyCode)}</strong></div>
            <div className="cash-shift-row"><span>{t('op.cash.shift.counted')}</span><strong>{current.cash.counted ? formatMoney(current.cash.counted, currencyCode) : t('op.cash.shift.notClosed')}</strong></div>
            <div className={`cash-shift-row${current.cash.difference && current.cash.difference.minorUnits !== 0 ? ' attention' : ''}`}>
              <span>{t('op.cash.shift.difference')}</span><strong>{formatMoney(current.cash.difference, currencyCode)}</strong>
            </div>
          </section>

          <section className="cash-shift-card">
            <h2>{t('op.cash.shift.movementsTitle')}</h2>
            {cashRows.length === 0 ? (
              <p className="cash-shift-empty-note">{t('op.cash.shift.movementsEmpty')}</p>
            ) : (
              <ul className="cash-shift-movements">
                {cashRows.slice(0, 6).map((row) => (
                  <li key={readString(row, 'operationId')}>
                    <span>{formatTime(readString(row, 'createdAtUtc'))}</span>
                    <strong>{cashOperationTypeLabel(readString(row, 'operationType', 'cash'), t)}</strong>
                    <b>{formatMoney(readMoney(row, 'cashImpact'), currencyCode)}</b>
                  </li>
                ))}
              </ul>
            )}
          </section>

          <section className="cash-shift-card">
            <h2>{t('op.cash.shift.exportTitle')}</h2>
            <div className="cash-shift-exports">
              <button type="button" onClick={() => void exportCsv('shifts')}><ReceiptText size={15} aria-hidden="true" />{t('op.cash.shift.exportShiftSummary')}</button>
              <button type="button" onClick={() => void exportCsv('cash')}><Banknote size={15} aria-hidden="true" />{t('op.cash.shift.exportCashMovements')}</button>
              <button type="button" onClick={() => void exportCsv('sales')}><ArrowRightLeft size={15} aria-hidden="true" />{t('op.cash.shift.exportReceipts')}</button>
            </div>
          </section>
        </div>
      ) : (
        // op.cash.shift.empty = "Нет открытой смены" (единственное число, что ожидает тест)
        <section className="cash-shift-empty">{t('op.cash.shift.empty')}</section>
      )}

      <section className="cash-shift-history">
        <h2>{t('op.cash.shift.historyTitle')}</h2>
        {history.length === 0 ? (
          <p className="cash-shift-empty-note">{t('op.cash.shift.historyEmpty')}</p>
        ) : (
          <ul>
            {history.map((s) => (
              <li key={s.shiftId}>
                {new Date(s.openedAtUtc).toLocaleDateString('ru-RU')} · {t('op.shifts.earned')} {formatMoney(s.earned.total, currencyCode)}
                {s.cash.difference ? ` · ${t('op.shifts.cashDiff')} ${formatMoney(s.cash.difference, currencyCode)}` : ''}
              </li>
            ))}
          </ul>
        )}
      </section>
    </main>
  );
}
