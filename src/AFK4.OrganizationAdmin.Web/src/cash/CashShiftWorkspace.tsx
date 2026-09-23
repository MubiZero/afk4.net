import { useEffect, useMemo, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { hasPermission, permissionNames } from '../operatorPermissions';
import { ArrowRightLeft, Banknote, ChevronRight, Clock3, MoreHorizontal, ReceiptText } from 'lucide-react';
import {
  cashOperationTypeLabel,
  createAuthenticatedOperatorClients,
  downloadTextFile,
  formatTime
} from '../operatorHelpers';
import { projectOperatorError, type OperatorErrorProjection } from '../apiErrors';
import { EmptyState, Money, PartialLoadFailure } from '../operatorPrimitives';
import type { OperatorBackendContext } from '../operatorTypes';
import type { OperatorAuthSession } from '../authClient';
import type {
  CashOperationReportResultDto,
  CashOperationReportRowDto,
  ShiftRevenueDto
} from '../operatorApiClients';
import { CashRegisterRows } from './CashTerminalFrame';
import { CashShiftCommandBar } from './CashShiftCommandBar';

interface ShiftCockpitClient {
  current(branchId: string): Promise<ShiftRevenueDto | null>;
  history(branchId: string, limit?: number): Promise<{ shifts: ShiftRevenueDto[]; limit: number }>;
}

interface ShiftCockpitReports {
  getCashOperationReport(branchId: string, query?: { limit?: number }): Promise<CashOperationReportResultDto>;
}

function formatElapsed(openedAtUtc: string): string {
  const openedAt = Date.parse(openedAtUtc);
  if (!Number.isFinite(openedAt)) return '—';
  const totalMinutes = Math.max(0, Math.floor((Date.now() - openedAt) / 60_000));
  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;
  return hours > 0 ? `${hours} ч ${minutes} мин` : `${minutes} мин`;
}

function formatOpenedAt(openedAtUtc: string, todayLabel: string): string {
  const opened = new Date(openedAtUtc);
  if (Number.isNaN(opened.getTime())) return '—';
  const now = new Date();
  const sameDay = opened.getFullYear() === now.getFullYear()
    && opened.getMonth() === now.getMonth()
    && opened.getDate() === now.getDate();
  const time = opened.toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit' });
  return sameDay ? `${todayLabel}, ${time}` : `${opened.toLocaleDateString('ru-RU')}, ${time}`;
}

function closedShifts(shifts: ShiftRevenueDto[]): ShiftRevenueDto[] {
  return shifts.filter((shift) => shift.state === 'closed');
}

export function CashShiftWorkspace({
  backend,
  branchId,
  currencyCode,
  session = backend?.session ?? null,
  shiftNonce = 0,
  onShiftChanged = () => {},
  revenueClient: injectedRevenue,
  reports: injectedReports
}: {
  backend: OperatorBackendContext | null;
  branchId: string;
  currencyCode: string;
  session?: OperatorAuthSession | null;
  shiftNonce?: number;
  onShiftChanged?: () => void;
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
  const [cashRows, setCashRows] = useState<CashOperationReportRowDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [historyError, setHistoryError] = useState<OperatorErrorProjection | null>(null);
  const [cashRowsError, setCashRowsError] = useState<OperatorErrorProjection | null>(null);
  const [exportError, setExportError] = useState<string | null>(null);
  const [selectedShiftId, setSelectedShiftId] = useState('');

  useEffect(() => {
    if (revenueClient === null || reports === null) return undefined;
    let active = true;
    setLoading(true);
    setLoadError(null);
    setHistoryError(null);
    setCashRowsError(null);
    // Три запроса — три панели. Без текущей смены экран не знает, открыта ли она, и не может
    // предложить ни открыть, ни закрыть: её отказ по-прежнему занимает весь экран. Прошлые смены
    // и движение наличных — справка рядом, и их отказ не должен стирать смену со сверкой.
    Promise.allSettled([
      revenueClient.current(branchId),
      revenueClient.history(branchId, 20),
      reports.getCashOperationReport(branchId, { limit: 8 })
    ])
      .then(([cur, hist, cash]) => {
        if (!active) return;
        if (cur.status === 'fulfilled') setCurrent(cur.value);
        else setLoadError(projectOperatorError(cur.reason, t).detail);
        if (hist.status === 'fulfilled') setHistory(closedShifts(hist.value.shifts));
        else setHistoryError(projectOperatorError(hist.reason, t));
        if (cash.status === 'fulfilled') setCashRows(cash.value.rows);
        else setCashRowsError(projectOperatorError(cash.reason, t));
      })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [revenueClient, reports, branchId, shiftNonce]);

  const retryHistory = () => {
    if (revenueClient === null) return;
    setHistoryError(null);
    revenueClient.history(branchId, 20)
      .then((hist) => setHistory(closedShifts(hist.shifts)))
      .catch((error) => setHistoryError(projectOperatorError(error, t)));
  };

  const retryCashRows = () => {
    if (reports === null) return;
    setCashRowsError(null);
    reports.getCashOperationReport(branchId, { limit: 8 })
      .then((cash) => setCashRows(cash.rows))
      .catch((error) => setCashRowsError(projectOperatorError(error, t)));
  };

  const exportCsv = async (kind: 'shifts' | 'cash' | 'sales') => {
    if (backend === null) return;
    try {
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

  if (loading) return <main className="workspace-screen cash-shift-screen"><p className="workspace-loading">{t('op.shifts.loading')}</p></main>;
  if (loadError) return <main className="workspace-screen cash-shift-screen"><p className="ui-alert ui-alert--spaced" role="alert">{loadError}</p></main>;

  const selectedShift = history.find((shift) => shift.shiftId === selectedShiftId)
    ?? (current === null ? history[0] : null);
  const operatorName = backend?.session.displayName?.trim() || t('op.cash.shift.operatorFallback');
  const movementTotal = cashRows.reduce((total, row) => total + row.cashImpact.minorUnits, 0);
  const earnedTotal = current?.earned.total.minorUnits ?? 0;
  const percent = (value: number) => earnedTotal > 0 ? Math.round(value / earnedTotal * 100) : 0;

  const exportMenu = (
    <details className="cash-shift-export-menu">
      <summary aria-label={t('op.cash.shift.exportTitle')}><MoreHorizontal size={18} aria-hidden="true" /><span>{t('op.cash.shift.exportTitle')}</span></summary>
      <div className="cash-shift-export-popover">
        <button type="button" onClick={() => void exportCsv('shifts')}><ReceiptText size={15} aria-hidden="true" />{t('op.cash.shift.exportShiftSummary')}</button>
        <button type="button" onClick={() => void exportCsv('cash')}><Banknote size={15} aria-hidden="true" />{t('op.cash.shift.exportCashMovements')}</button>
        <button type="button" onClick={() => void exportCsv('sales')}><ArrowRightLeft size={15} aria-hidden="true" />{t('op.cash.shift.exportReceipts')}</button>
        {exportError && <p className="cash-export-error" role="alert">{exportError}</p>}
      </div>
    </details>
  );

  return (
    <main className="workspace-screen cash-shift-screen">
      {current ? (
        <>
          <section className="cash-shift-status-card" aria-label={t('op.cash.shift.statusAria')}>
            <div className="cash-shift-status-block cash-shift-status-lead">
              <Clock3 size={25} aria-hidden="true" />
              <span>{t('op.cash.shift.openStatus')}</span>
              <strong>{formatElapsed(current.openedAtUtc)}</strong>
            </div>
            <div className="cash-shift-status-block"><span>{t('op.cash.shift.cashier')}</span><strong>{operatorName}</strong></div>
            <div className="cash-shift-status-block"><span>{t('op.cash.shift.opened')}</span><strong>{formatOpenedAt(current.openedAtUtc, t('op.cash.shift.today'))}</strong></div>
            <div className="cash-shift-status-block"><span>{t('op.cash.shift.starting')}</span><strong><Money minorUnits={current.cash.starting.minorUnits} currencyCode={currencyCode} /></strong></div>
            <div className="cash-shift-status-actions">{exportMenu}<CashShiftCommandBar backend={backend} session={session} shiftId={current.shiftId} isOpen openedByStaffUserId={current.openedByStaffUserId} expectedCash={current.cash.expected} currencyCode={currencyCode} revenue={current} onShiftChanged={onShiftChanged} /></div>
          </section>

          <section className="cash-shift-reconcile-band" aria-label={t('op.cash.shift.reconcileTitle')}>
            <div><span>{t('op.cash.shift.expectedDrawer')}</span><strong><Money minorUnits={current.cash.expected.minorUnits} currencyCode={currencyCode} /></strong><small>{t('op.cash.shift.expectedHint')}</small></div>
            <div className="cash-shift-reconcile-pending"><span>{t('op.cash.shift.actualDrawer')}</span><strong>{t('op.cash.shift.notEntered')}</strong><small>{t('op.cash.shift.actualHint')}</small></div>
            <div><span>{t('op.cash.shift.difference')}</span><strong>{current.cash.difference ? <Money minorUnits={current.cash.difference.minorUnits} currencyCode={currencyCode} /> : '—'}</strong><small>{t('op.cash.shift.differenceHint')}</small></div>
          </section>

          <div className="cash-shift-main-grid">
            <div className="cash-shift-main-column">
              <section className="cash-shift-revenue-strip">
                <div className="cash-shift-revenue-total"><span>{t('op.cash.shift.revenueTitle')}</span><strong><Money minorUnits={current.earned.total.minorUnits} currencyCode={currencyCode} /></strong></div>
                <div><span>{t('op.shifts.time')}</span><strong><Money minorUnits={current.earned.time.minorUnits} currencyCode={currencyCode} /></strong><small>{percent(current.earned.time.minorUnits)}%</small></div>
                <div><span>{t('op.shifts.goods')}</span><strong><Money minorUnits={current.earned.goods.minorUnits} currencyCode={currencyCode} /></strong><small>{percent(current.earned.goods.minorUnits)}%</small></div>
                {/* Удержания за неявку показываем, только когда они есть: у филиала, который
                    предоплату не удерживает, это вечный ноль, а полоса выручки — беглый взгляд,
                    не документ. В Z-отчёте строка стоит всегда — там итог обязан раскладываться. */}
                {current.earned.noShow.minorUnits > 0 && (
                  <div><span>{t('op.shifts.noShow')}</span><strong><Money minorUnits={current.earned.noShow.minorUnits} currencyCode={currencyCode} /></strong><small>{percent(current.earned.noShow.minorUnits)}%</small></div>
                )}
                <div><span>{t('op.shifts.cash')}</span><strong><Money minorUnits={current.inflow.cash.minorUnits} currencyCode={currencyCode} /></strong><small>{percent(current.inflow.cash.minorUnits)}%</small></div>
                <div><span>{t('op.shifts.nonCash')}</span><strong><Money minorUnits={current.inflow.nonCash.minorUnits} currencyCode={currencyCode} /></strong><small>{percent(current.inflow.nonCash.minorUnits)}%</small></div>
                <div><span>{t('op.shifts.walletTopUps')}</span><strong><Money minorUnits={current.inflow.walletTopUps.minorUnits} currencyCode={currencyCode} /></strong></div>
              </section>

              <section className="cash-shift-movement-ledger">
                <header><h2>{t('op.cash.shift.movementsTitle')}</h2>{cashRowsError === null && <span>{cashRows.length}</span>}</header>
                {cashRowsError !== null ? <PartialLoadFailure text={t('op.cash.shift.movementsFailed', { reason: cashRowsError.detail })} failure={cashRowsError} onRetry={retryCashRows} /> : <>
                <div className="cash-shift-movement-head" aria-hidden="true"><span>{t('op.cash.shift.timeColumn')}</span><span>{t('op.cash.shift.operationColumn')}</span><span>{t('op.cash.shift.reasonColumn')}</span><span>{t('op.cash.shift.operatorColumn')}</span><span>{t('op.cash.shift.amountColumn')}</span></div>
                {cashRows.length === 0 ? <EmptyState inline className="cash-shift-empty-note" title={t('op.cash.shift.movementsEmpty')} next={{ kind: 'calm', hint: t('op.cash.shift.movementsEmptyHint') }} /> : <ul className="cash-shift-movements">
                  {cashRows.slice(0, 8).map((row) => {
                    const negative = row.cashImpact.minorUnits < 0;
                    return <li key={row.operationId} className={negative ? 'out' : 'in'}><span>{formatTime(row.createdAtUtc)}</span><strong>{cashOperationTypeLabel(row.operationType || 'cash', t)}</strong><em>{row.reason || '—'}</em><span>{row.createdByDisplayName || '—'}</span><b><Money minorUnits={row.cashImpact.minorUnits} currencyCode={currencyCode} signed /></b></li>;
                  })}
                </ul>}
                <footer><span>{t('op.cash.shift.movementTotal')}</span><strong><Money minorUnits={movementTotal} currencyCode={currencyCode} signed /></strong></footer>
                </>}
              </section>
            </div>

            <section className="cash-shift-history-panel">
              <header><h2>{t('op.cash.shift.pastShifts')}</h2>{historyError === null && <span>{history.length}</span>}</header>
              {historyError !== null ? <PartialLoadFailure text={t('op.cash.shift.historyFailed', { reason: historyError.detail })} failure={historyError} onRetry={retryHistory} /> : history.length === 0 ? <EmptyState inline className="cash-shift-empty-note" title={t('op.cash.shift.historyEmpty')} next={{ kind: 'calm', hint: t('op.cash.shift.historyEmptyHint') }} /> : <CashRegisterRows rows={history.slice(0, 8)} selectedId={selectedShift?.shiftId ?? ''} getId={(shift) => shift.shiftId} onSelect={setSelectedShiftId} ariaLabel={t('op.cash.shift.pastShifts')} renderRow={(shift) => <div className="cash-shift-history-row">
                <span>{new Date(shift.openedAtUtc).toLocaleDateString('ru-RU')}</span>
                <span><small>{t('op.shifts.earned')}</small><strong><Money minorUnits={shift.earned.total.minorUnits} currencyCode={currencyCode} /></strong></span>
                <span className={shift.cash.difference?.minorUnits ? 'attention' : ''}><small>{t('op.cash.shift.difference')}</small><strong>{shift.cash.difference ? <Money minorUnits={shift.cash.difference.minorUnits} currencyCode={currencyCode} /> : '—'}</strong></span>
                <ChevronRight size={16} aria-hidden="true" />
              </div>} />}
              {selectedShift ? <div className="cash-shift-history-detail">
                <span>{t('op.cash.shift.selectedClosed')}</span>
                <strong><Money minorUnits={selectedShift.earned.total.minorUnits} currencyCode={currencyCode} /></strong>
                <dl>
                  <div><dt>{t('op.cash.shift.expected')}</dt><dd><Money minorUnits={selectedShift.cash.expected.minorUnits} currencyCode={currencyCode} /></dd></div>
                  <div><dt>{t('op.cash.shift.counted')}</dt><dd>{selectedShift.cash.counted ? <Money minorUnits={selectedShift.cash.counted.minorUnits} currencyCode={currencyCode} /> : '—'}</dd></div>
                  <div><dt>{t('op.cash.shift.difference')}</dt><dd>{selectedShift.cash.difference ? <Money minorUnits={selectedShift.cash.difference.minorUnits} currencyCode={currencyCode} /> : '—'}</dd></div>
                </dl>
              </div> : null}
            </section>
          </div>
        </>
      ) : (
        <section className="cash-shift-no-open">
          {/* Подсказка «откройте смену в панели кассы» звала к кнопке, которой у этого
              сотрудника нет: кнопка живёт за правом «открыть смену». Без права правильный
              следующий шаг другой — позвать того, у кого оно есть. */}
          <span>{t('op.cash.shift.empty')}</span><h2>{t('op.cash.shift.noOpenNow')}</h2><p>{
            !hasPermission(session, permissionNames.openShift)
              ? t('op.cash.shift.noOpenAskManager')
              : history.length || historyError !== null ? t('op.cash.shift.noOpenHint') : t('op.cash.shift.historyEmpty')
          }</p>
          {historyError !== null && <PartialLoadFailure text={t('op.cash.shift.historyFailed', { reason: historyError.detail })} failure={historyError} onRetry={retryHistory} />}
          <div className="cash-shift-no-open-actions">{exportMenu}<CashShiftCommandBar backend={backend} session={session} shiftId={null} isOpen={false} expectedCash={null} currencyCode={currencyCode} onShiftChanged={onShiftChanged} /></div>
          {selectedShift ? <div className="cash-shift-last-closed"><span>{t('op.cash.shift.lastClosed')}</span><strong>{new Date(selectedShift.openedAtUtc).toLocaleDateString('ru-RU')}</strong><b><Money minorUnits={selectedShift.earned.total.minorUnits} currencyCode={currencyCode} /></b></div> : null}
        </section>
      )}
    </main>
  );
}
