import { useEffect, useState } from 'react';
import { ArrowRightLeft, Banknote, ReceiptText, Search, ShieldAlert } from 'lucide-react';
import { projectOperatorError } from './apiErrors';
import type { ReportResultDto, ShiftDto } from './operatorApiClients';
import type { Feedback, LoadStatus, OperatorBackendContext } from './operatorTypes';
import { hasPermission, permissionNames } from './operatorPermissions';
import {
  cashOperationTypeLabel,
  createAuthenticatedOperatorClients,
  createIdempotencyKey,
  downloadTextFile,
  emptyFeedback,
  formatDateTime,
  formatMinorUnits,
  formatMoney,
  formatMoneyInputMinorUnits,
  formatTime,
  parseMoneyInputMinorUnits,
  parseNonNegativeMoneyInputMinorUnits,
  paymentSourceLabel,
  posSaleLineSummary,
  posSaleStateLabel,
  readArray,
  readMoney,
  readNumber,
  readString,
  requireBackend,
  shiftStateLabel,
  workspaceLoadStatusLabel
} from './operatorHelpers';
import { CriticalActionConfirmation, FeedbackNotice, StateFlag } from './operatorPrimitives';

type PaymentOperationItem = [string, string, string, string, string, string, Record<string, unknown> | null];

function paymentOperationPlaceholder(
  loadStatus: LoadStatus,
  currencyCode: string,
  loadError: string | null,
  hasSearchMiss: boolean
): PaymentOperationItem {
  if (hasSearchMiss) {
    return ['—', 'Нет совпадений', 'измените поиск или период', 'Поиск', `0 ${currencyCode}`, 'session', null];
  }

  if (loadStatus === 'loading') {
    return ['—', 'Загружаем операции', 'ждём отчёты', 'Отчёты', `0 ${currencyCode}`, 'session', null];
  }

  if (loadStatus === 'failed') {
    return ['—', 'Операции недоступны', loadError ?? 'повторите загрузку или проверьте связь', 'Отчёты', `0 ${currencyCode}`, 'refund', null];
  }

  if (loadStatus === 'backend') {
    return ['—', 'Операций за период нет', 'в отчёте пусто', 'Отчёты', `0 ${currencyCode}`, 'session', null];
  }

  return ['—', 'Локально: операций нет', 'локальные данные без платформы', 'локально', `0 ${currencyCode}`, 'session', null];
}

function buildShiftReconciliationExportJson(report: ReportResultDto, currentShift: ShiftDto | null, currencyCode: string): string {
  const rows = readArray<Record<string, unknown>>(report, 'rows');
  const latestRow = rows[0];
  const stateSource = currentShift ?? latestRow;
  const expectedCash = readMoney(currentShift, 'expectedCash') ?? readMoney(latestRow, 'expectedCash');
  const countedCash = readMoney(currentShift, 'countedCash') ?? readMoney(latestRow, 'countedCash');
  const difference = readMoney(currentShift, 'difference') ?? readMoney(latestRow, 'difference');

  return JSON.stringify({
    summary: {
      generatedAtUtc: new Date().toISOString(),
      shiftState: shiftStateLabel(readString(stateSource, 'state', 'unknown')),
      expectedCash: formatMoney(expectedCash, currencyCode),
      countedCash: countedCash ? formatMoney(countedCash, currencyCode) : 'Не указано',
      difference: formatMoney(difference, currencyCode),
      shiftCount: rows.length
    },
    shifts: rows.map((row, index) => ({
      label: `Смена ${index + 1}`,
      state: shiftStateLabel(readString(row, 'state', 'unknown')),
      openedAt: formatDateTime(readString(row, 'openedAtUtc')),
      closedAt: formatDateTime(readString(row, 'closedAtUtc')),
      startingCash: formatMoney(readMoney(row, 'startingCash'), currencyCode),
      cashMovements: formatMoney(readMoney(row, 'cashMovementsTotal'), currencyCode),
      cashSales: formatMoney(readMoney(row, 'posCashPaymentsTotal'), currencyCode),
      refunds: formatMoney(readMoney(row, 'posRefundsTotal'), currencyCode),
      walletCashImpact: formatMoney(readMoney(row, 'billingCashImpactTotal'), currencyCode),
      expectedCash: formatMoney(readMoney(row, 'expectedCash'), currencyCode),
      countedCash: readMoney(row, 'countedCash') ? formatMoney(readMoney(row, 'countedCash'), currencyCode) : 'Не указано',
      difference: formatMoney(readMoney(row, 'difference'), currencyCode)
    }))
  }, null, 2);
}

export function BackendPaymentsWorkspace({ currencyCode, backend }: { currencyCode: string; backend: OperatorBackendContext | null }) {
  const [paymentSearch, setPaymentSearch] = useState('');
  const [selectedOperationKey, setSelectedOperationKey] = useState('');
  const [selectedMethod, setSelectedMethod] = useState('Наличные');
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const [loadStatus, setLoadStatus] = useState<LoadStatus>('fixture');
  const [currentShift, setCurrentShift] = useState<ShiftDto | null>(null);
  const [salesReport, setSalesReport] = useState<ReportResultDto | null>(null);
  const [cashReport, setCashReport] = useState<ReportResultDto | null>(null);
  const [shiftReport, setShiftReport] = useState<ReportResultDto | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [openStartingCash, setOpenStartingCash] = useState('0.00');
  const [openingNote, setOpeningNote] = useState('Открытие смены');
  const [closeCountedCash, setCloseCountedCash] = useState('');
  const [closingNote, setClosingNote] = useState('Сверка оператором');
  const [cashMovementType, setCashMovementType] = useState('cash_in');
  const [cashMovementAmount, setCashMovementAmount] = useState('10.00');
  const [cashMovementReason, setCashMovementReason] = useState('Размен кассы');
  const [criticalAction, setCriticalAction] = useState<'close-shift' | null>(null);

  const loadPayments = async (nextBackend = backend) => {
    if (nextBackend === null) {
      setLoadStatus('fixture');
      setLoadError(null);
      return;
    }

    setLoadStatus('loading');
    setLoadError(null);
    try {
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const [shift, sales, cash, shifts] = await Promise.all([
        apiClients.shifts.getCurrentShift(nextBackend.branchId),
        apiClients.shifts.getSalesReport(nextBackend.branchId, { limit: 12 }),
        apiClients.shifts.getCashOperationReport(nextBackend.branchId, { limit: 12 }),
        apiClients.shifts.getShiftReport(nextBackend.branchId, { limit: 6 })
      ]);
      setCurrentShift(shift);
      setSalesReport(sales);
      setCashReport(cash);
      setShiftReport(shifts);
      const closeSeed = readMoney(shift, 'countedCash') ?? readMoney(shift, 'expectedCash');
      if (closeSeed !== null) {
        setCloseCountedCash(formatMoneyInputMinorUnits(closeSeed.minorUnits));
      }
      const note = readString(shift, 'closingNote');
      if (note) {
        setClosingNote(note);
      }
      setLoadError(null);
      setLoadStatus('backend');
    } catch (error) {
      const detail = projectOperatorError(error).detail;
      setLoadStatus('failed');
      setLoadError(detail);
      setFeedback({ label: 'Платежи', state: 'failed', detail });
    }
  };

  useEffect(() => {
    void loadPayments();
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken, currencyCode]);

  const salesRows = readArray<Record<string, unknown>>(salesReport, 'rows');
  const cashRows = readArray<Record<string, unknown>>(cashReport, 'rows');
  const shiftRows = readArray<Record<string, unknown>>(shiftReport, 'rows');
  const operations: PaymentOperationItem[] = [
    ...salesRows.map((row): PaymentOperationItem => [
      formatTime(readString(row, 'createdAtUtc')),
      posSaleStateLabel(readString(row, 'state', 'sale')),
      `Чек · ${posSaleLineSummary(row)}`,
      'Продажа',
      formatMoney(readMoney(row, 'total'), currencyCode),
      readString(row, 'state', 'sale').toLowerCase().includes('refund') ? 'refund' : 'sale',
      row
    ]),
    ...cashRows.map((row): PaymentOperationItem => [
      formatTime(readString(row, 'createdAtUtc')),
      cashOperationTypeLabel(readString(row, 'operationType', 'cash')),
      readString(row, 'reason', readString(row, 'sourceType', 'cash')),
      paymentSourceLabel(readString(row, 'sourceType', 'cash')),
      formatMoney(readMoney(row, 'cashImpact'), currencyCode),
      readNumber(readMoney(row, 'cashImpact'), 'minorUnits', 0) < 0 ? 'refund' : 'deposit',
      row
    ])
  ];
  const operationSearch = paymentSearch.trim().toLowerCase();
  const filteredOperations = operations.filter(([time, type, client, method, total]) => (
    `${time} ${type} ${client} ${method} ${total}`.toLowerCase().includes(paymentSearch.trim().toLowerCase())
  ));
  const visibleOperations = operations.length === 0
    ? [paymentOperationPlaceholder(loadStatus, currencyCode, loadError, false)]
    : filteredOperations.length > 0
      ? filteredOperations
      : [paymentOperationPlaceholder(loadStatus, currencyCode, loadError, operationSearch.length > 0)];
  const selectedOperation = visibleOperations.find(([time, type, client]) => `${time}-${type}-${client}` === selectedOperationKey) ?? visibleOperations[0];
  const selectedOperationSource = selectedOperation[6];
  const selectedOperationIsSale = selectedOperationSource !== null && readString(selectedOperationSource, 'posSaleId').length > 0;
  const selectedOperationScope = selectedOperationSource === null
    ? '—'
    : readString(selectedOperationSource, 'shiftId') ? 'В отчёте смены' : 'Без смены';
  const selectedOperationSourceLabel = selectedOperationSource === null
    ? selectedOperation[3]
    : selectedOperationIsSale
      ? 'Продажа'
      : 'Движение наличных';
  const selectedOperationDetail = selectedOperationIsSale
    ? posSaleLineSummary(selectedOperationSource)
    : readString(selectedOperationSource, 'reason', selectedOperation[2]);
  const grossSales = readMoney(salesReport, 'grossSalesTotal');
  const refunds = readMoney(salesReport, 'refundsTotal');
  const netSales = readMoney(salesReport, 'netSalesTotal');
  const cashIn = readMoney(cashReport, 'cashInTotal');
  const cashOut = readMoney(cashReport, 'cashOutTotal');
  const latestShiftRow = shiftRows[0];
  const expectedCash = readMoney(currentShift, 'expectedCash') ?? readMoney(latestShiftRow, 'expectedCash');
  const countedCash = readMoney(currentShift, 'countedCash') ?? readMoney(latestShiftRow, 'countedCash');
  const difference = readMoney(currentShift, 'difference') ?? readMoney(latestShiftRow, 'difference');
  const currentShiftId = readString(currentShift, 'shiftId');
  const currentShiftState = readString(currentShift, 'state');
  const canOpenShift = backend !== null
    && currentShiftId.length === 0
    && hasPermission(backend.session, permissionNames.openShift);
  const canCloseShift = backend !== null
    && currentShiftId.length > 0
    && currentShiftState === 'open'
    && hasPermission(backend.session, permissionNames.closeShift);
  const canRecordCashMovement = backend !== null
    && currentShiftId.length > 0
    && currentShiftState === 'open'
    && hasPermission(backend.session, permissionNames.manageShiftCash);
  const methods = [
    ['Наличные', cashIn ? formatMinorUnits(cashIn.minorUnits, cashIn.currencyCode) : `0 ${currencyCode}`, 'кассовый отчёт', `${cashRows.length} операций`],
    ['Карта', netSales ? formatMinorUnits(netSales.minorUnits, netSales.currencyCode) : `0 ${currencyCode}`, 'отчёт продаж', `${salesRows.length} чеков`],
    ['Возвраты', refunds ? formatMinorUnits(refunds.minorUnits, refunds.currencyCode) : `0 ${currencyCode}`, 'возвраты', 'по отчёту'],
    ['Расхождения', difference ? formatMinorUnits(difference.minorUnits, difference.currencyCode) : `0 ${currencyCode}`, 'закрытие смены', 'по сверке']
  ];

  const runReportAction = async (label: string) => {
    setCriticalAction(null);
    setFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const exportStamp = new Date().toISOString().replace(/[:.]/g, '-');
      if (label === 'Сводка смены') {
        const csv = await apiClients.shifts.exportShiftReportCsv(nextBackend.branchId, { limit: 50 });
        downloadTextFile(`afk4-shift-summary-${exportStamp}.csv`, csv, 'text/csv;charset=utf-8');
      } else if (label === 'Движение кассы') {
        const csv = await apiClients.shifts.exportCashOperationReportCsv(nextBackend.branchId, { limit: 50 });
        downloadTextFile(`afk4-cash-movements-${exportStamp}.csv`, csv, 'text/csv;charset=utf-8');
      } else if (label === 'Список чеков') {
        const csv = await apiClients.shifts.exportSalesReportCsv(nextBackend.branchId, { limit: 50 });
        downloadTextFile(`afk4-check-list-${exportStamp}.csv`, csv, 'text/csv;charset=utf-8');
      } else if (label === 'Открыть смену') {
        if (!hasPermission(nextBackend.session, permissionNames.openShift)) {
          throw new Error('Нет прав на открытие смены.');
        }

        if (currentShiftId) {
          throw new Error('Смена уже открыта.');
        }

        const startingCashMinorUnits = parseNonNegativeMoneyInputMinorUnits(openStartingCash);
        if (startingCashMinorUnits === null) {
          throw new Error('Введите стартовую сумму наличных не ниже нуля.');
        }

        const openedShift = await apiClients.shifts.openShift(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          startingCash: { currencyCode, minorUnits: startingCashMinorUnits },
          openingNote: openingNote.trim(),
          idempotencyKey: createIdempotencyKey('shift-open')
        });
        setCurrentShift(openedShift);
        await loadPayments(nextBackend);
      } else if (label === 'Добавить движение') {
        if (!hasPermission(nextBackend.session, permissionNames.manageShiftCash)) {
          throw new Error('Нет прав на движение наличных.');
        }

        if (!currentShiftId) {
          throw new Error('Нет открытой смены для движения наличных.');
        }

        const cashMovementMinorUnits = parseMoneyInputMinorUnits(cashMovementAmount);
        const reason = cashMovementReason.trim();
        if (cashMovementMinorUnits === null || !reason) {
          throw new Error('Введите сумму больше нуля и причину движения.');
        }

        await apiClients.shifts.recordCashMovement(currentShiftId, {
          organizationId: nextBackend.session.organizationId,
          movementType: cashMovementType,
          amount: { currencyCode, minorUnits: cashMovementMinorUnits },
          reason,
          idempotencyKey: createIdempotencyKey('shift-cash-movement')
        });
        setCashMovementAmount('10.00');
        setCashMovementReason('Размен кассы');
        await loadPayments(nextBackend);
      } else if (label === 'Подготовить закрытие') {
        if (!hasPermission(nextBackend.session, permissionNames.closeShift)) {
          throw new Error('Нет прав на закрытие смены.');
        }

        if (!currentShiftId) {
          throw new Error('Нет открытой смены для закрытия.');
        }

        const countedCashMinorUnits = parseMoneyInputMinorUnits(closeCountedCash);
        if (countedCashMinorUnits === null) {
          throw new Error('Введите фактическую сумму наличных больше нуля.');
        }

        const closedShift = await apiClients.shifts.closeShift(currentShiftId, {
          organizationId: nextBackend.session.organizationId,
          countedCash: { currencyCode, minorUnits: countedCashMinorUnits },
          closingNote: closingNote.trim(),
          idempotencyKey: createIdempotencyKey('shift-close')
        });
        setCurrentShift(closedShift);
      } else if (label === 'Сверка смены') {
        const report = await apiClients.shifts.getShiftReport(nextBackend.branchId, { limit: 20 });
        downloadTextFile(`afk4-shift-reconciliation-${exportStamp}.json`, buildShiftReconciliationExportJson(report, currentShift, currencyCode), 'application/json;charset=utf-8');
      } else {
        await apiClients.shifts.getShiftReport(nextBackend.branchId, { limit: 20 });
      }

      setFeedback({ label, state: 'confirmed' });
    } catch (error) {
      setFeedback({ label, state: 'failed', detail: projectOperatorError(error).detail });
    }
  };

  return (
    <main className="workspace-screen payments-screen">
      <section className="screen-head payments-head">
        <div>
          <span>Платежи</span>
          <h1>Платежи · касса смены и сверка</h1>
        </div>
        <div className="screen-actions">
          <span className={`map-load-state ${loadStatus === 'backend' ? 'ready' : loadStatus}`}>{workspaceLoadStatusLabel(loadStatus, 'Отчёты загружены')}</span>
        </div>
      </section>

      <section className="state-strip payments-state-strip" aria-label="Сводка платежей">
        <StateFlag label="Выручка" value={grossSales ? formatMinorUnits(grossSales.minorUnits, grossSales.currencyCode) : `0 ${currencyCode}`} />
        <StateFlag label="Наличные" value={cashIn ? formatMinorUnits(cashIn.minorUnits, cashIn.currencyCode) : `0 ${currencyCode}`} />
        <StateFlag label="Возвраты" value={refunds ? formatMinorUnits(refunds.minorUnits, refunds.currencyCode) : `0 ${currencyCode}`} critical={(refunds?.minorUnits ?? 0) > 0} />
        <StateFlag label="Смена" value={shiftStateLabel(readString(currentShift, 'state', 'нет смены'))} critical={currentShift === null} />
        <StateFlag label="К сверке" value={difference ? formatMinorUnits(difference.minorUnits, difference.currencyCode) : `0 ${currencyCode}`} critical={(difference?.minorUnits ?? 0) !== 0} />
      </section>

      <section className="payments-layout">
        <section className="payments-panel payments-ledger-panel">
          <header className="payments-panel-title">
            <span>Операции смены</span>
            <strong>продажи, возвраты и наличные</strong>
          </header>
          <label className="payments-search">
            <Search size={14} />
            <input
              placeholder="Клиент, чек, ПК, сумма"
              value={paymentSearch}
              onChange={(event) => setPaymentSearch(event.currentTarget.value)}
            />
          </label>
          <div className="payments-ledger-list">
            {visibleOperations.map(([time, type, client, method, total, tone]) => (
              <button
                key={`${time}-${type}-${client}`}
                type="button"
                className={`payment-operation-row ${tone}${`${time}-${type}-${client}` === selectedOperationKey ? ' active' : ''}`}
                onClick={() => setSelectedOperationKey(`${time}-${type}-${client}`)}
              >
                <span>{time}</span>
                <div>
                  <strong>{type}</strong>
                  <em>{client}</em>
                </div>
                <small>{method}</small>
                <b>{total}</b>
              </button>
            ))}
          </div>
        </section>

        <section className="payments-panel payments-summary-panel">
          <header className="payments-panel-title">
            <span>Итоги смены</span>
            <strong>выручка и выбранная операция</strong>
          </header>
          <div className="payments-total-card">
            <span>Всего · выбрано {selectedOperation[0]}</span>
            <strong>{netSales ? formatMinorUnits(netSales.minorUnits, netSales.currencyCode) : `0 ${currencyCode}`}</strong>
            <em>{selectedOperation[1]} · {selectedOperation[2]} · {selectedOperation[4]}</em>
          </div>
          <div className="payments-operation-detail" aria-label="Детали выбранной операции">
            <div><span>Операция</span><strong>{selectedOperation[1]}</strong></div>
            <div><span>Смена</span><strong>{selectedOperationScope}</strong></div>
            <div><span>Тип</span><strong>{selectedOperationSourceLabel}</strong></div>
            <div><span>Деталь</span><strong>{selectedOperationDetail}</strong></div>
          </div>
          <div className="payments-metric-grid">
            <div><span>Чеков</span><strong>{salesRows.length}</strong></div>
            <div><span>Наличные</span><strong>{cashRows.length}</strong></div>
            <div><span>Возвраты</span><strong>{refunds ? formatMinorUnits(refunds.minorUnits, refunds.currencyCode) : `0 ${currencyCode}`}</strong></div>
            <div><span>Смены</span><strong>{shiftRows.length}</strong></div>
          </div>
        </section>

        <section className="payments-panel payments-reconcile-panel">
          <header className="payments-panel-title">
            <span>Сверка кассы</span>
            <strong>проверка перед закрытием</strong>
          </header>
          <div className="payments-open-form">
            <label>Старт наличных<input inputMode="decimal" value={openStartingCash} disabled={!canOpenShift} onChange={(event) => setOpenStartingCash(event.currentTarget.value)} /></label>
            <label>Открытие<input value={openingNote} disabled={!canOpenShift} onChange={(event) => setOpeningNote(event.currentTarget.value)} /></label>
            <button type="button" disabled={!canOpenShift} onClick={() => runReportAction('Открыть смену')}>Открыть смену</button>
          </div>
          <div className="payments-reconcile-list">
            <div><span>Ожидается</span><strong>{expectedCash ? formatMinorUnits(expectedCash.minorUnits, expectedCash.currencyCode) : `0 ${currencyCode}`}</strong></div>
            <div><span>Посчитано</span><strong>{countedCash ? formatMinorUnits(countedCash.minorUnits, countedCash.currencyCode) : 'не закрыта'}</strong></div>
            <div className={(difference?.minorUnits ?? 0) !== 0 ? 'attention' : undefined}><span>Расхождение</span><strong>{difference ? formatMinorUnits(difference.minorUnits, difference.currencyCode) : `0 ${currencyCode}`}</strong></div>
          </div>
          <div className="payments-close-form">
            <label>Факт в кассе<input inputMode="decimal" value={closeCountedCash} disabled={!canCloseShift} onChange={(event) => setCloseCountedCash(event.currentTarget.value)} /></label>
            <label>Комментарий<input value={closingNote} disabled={!canCloseShift} onChange={(event) => setClosingNote(event.currentTarget.value)} /></label>
          </div>
          <button
            type="button"
            className="payments-primary-action"
            disabled={!canCloseShift || feedback.state === 'pending'}
            onClick={() => {
              setFeedback(emptyFeedback);
              setCriticalAction('close-shift');
            }}
          >
            Подготовить закрытие
          </button>
          {criticalAction === 'close-shift' && (
            <CriticalActionConfirmation
              title="Подтвердите закрытие смены"
              detail={`Факт ${closeCountedCash || '0'} ${currencyCode} · ожидается ${expectedCash ? formatMinorUnits(expectedCash.minorUnits, expectedCash.currencyCode) : `0 ${currencyCode}`}`}
              impact="После подтверждения смена будет закрыта, новые продажи потребуют открытия следующей смены."
              confirmLabel="Закрыть смену"
              disabled={feedback.state === 'pending'}
              onCancel={() => setCriticalAction(null)}
              onConfirm={() => void runReportAction('Подготовить закрытие')}
            />
          )}
          <FeedbackNotice feedback={feedback} />
        </section>

        <section className="payments-panel payments-methods-panel">
          <header className="payments-panel-title">
            <span>Методы оплаты</span>
            <strong>структура отчёта</strong>
          </header>
          <div className="payments-method-grid">
            {methods.map(([label, total, share, detail]) => (
              <button
                key={label}
                type="button"
                className={`payment-method-card${selectedMethod === label ? ' active' : ''}`}
                onClick={() => setSelectedMethod(label)}
              >
                <strong>{label}</strong>
                <b>{total}</b>
                <span>{share} · {detail}</span>
              </button>
            ))}
          </div>
        </section>

        <section className="payments-panel payments-cash-panel">
          <header className="payments-panel-title">
            <span>Движение наличных</span>
            <strong>кассовые движения</strong>
          </header>
          <div className="payments-cash-list">
            {cashRows.slice(0, 4).map((row) => (
              <article key={readString(row, 'operationId')} className="payment-cash-row">
                <span>{formatTime(readString(row, 'createdAtUtc'))}</span>
                <strong>{cashOperationTypeLabel(readString(row, 'operationType', 'cash'))}</strong>
                <b>{formatMoney(readMoney(row, 'cashImpact'), currencyCode)}</b>
              </article>
            ))}
          </div>
          <div className="payments-cash-form">
            <label>Тип
              <select value={cashMovementType} disabled={!canRecordCashMovement} onChange={(event) => setCashMovementType(event.currentTarget.value)}>
                <option value="cash_in">Внесение</option>
                <option value="cash_out">Изъятие</option>
              </select>
            </label>
            <label>Сумма<input inputMode="decimal" value={cashMovementAmount} disabled={!canRecordCashMovement} onChange={(event) => setCashMovementAmount(event.currentTarget.value)} /></label>
            <label className="payments-cash-reason">Причина<input value={cashMovementReason} disabled={!canRecordCashMovement} onChange={(event) => setCashMovementReason(event.currentTarget.value)} /></label>
            <button type="button" disabled={!canRecordCashMovement} onClick={() => runReportAction('Добавить движение')}>Добавить движение</button>
          </div>
        </section>

        <section className="payments-panel payments-export-panel">
          <header className="payments-panel-title">
            <span>Отчёты</span>
            <strong>файлы для сверки</strong>
          </header>
          <div className="payments-export-grid">
            {[
              ['Сводка смены', ReceiptText],
              ['Движение кассы', Banknote],
              ['Список чеков', ArrowRightLeft],
              ['Сверка смены', ShieldAlert]
            ].map(([label, Icon]) => (
              <button key={label as string} type="button" onClick={() => runReportAction(label as string)}><Icon size={16} />{label as string}</button>
            ))}
          </div>
        </section>
      </section>
    </main>
  );
}
