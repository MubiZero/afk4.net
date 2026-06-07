import { useEffect, useState } from 'react';
import { ArrowRightLeft, Banknote, ReceiptText, Search, ShieldAlert } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
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
  hasSearchMiss: boolean,
  t: ReturnType<typeof useI18n>['t']
): PaymentOperationItem {
  if (hasSearchMiss) {
    return ['—', t('op.payments.ph.noMatch.title'), t('op.payments.ph.noMatch.hint'), t('op.payments.ph.noMatch.source'), `0 ${currencyCode}`, 'session', null];
  }

  if (loadStatus === 'loading') {
    return ['—', t('op.payments.ph.loading.title'), t('op.payments.ph.loading.hint'), t('op.payments.ph.loading.source'), `0 ${currencyCode}`, 'session', null];
  }

  if (loadStatus === 'failed') {
    return ['—', t('op.payments.ph.failed.title'), loadError ?? t('op.payments.ph.failed.hint'), t('op.payments.ph.loading.source'), `0 ${currencyCode}`, 'refund', null];
  }

  if (loadStatus === 'backend') {
    return ['—', t('op.payments.ph.empty.title'), t('op.payments.ph.empty.hint'), t('op.payments.ph.loading.source'), `0 ${currencyCode}`, 'session', null];
  }

  return ['—', t('op.payments.ph.local.title'), t('op.payments.ph.local.hint'), t('op.payments.ph.local.source'), `0 ${currencyCode}`, 'session', null];
}

function buildShiftReconciliationExportJson(report: ReportResultDto, currentShift: ShiftDto | null, currencyCode: string, notIndicated: string): string {
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
      countedCash: countedCash ? formatMoney(countedCash, currencyCode) : notIndicated,
      difference: formatMoney(difference, currencyCode),
      shiftCount: rows.length
    },
    shifts: rows.map((row, index) => ({
      label: `Shift ${index + 1}`,
      state: shiftStateLabel(readString(row, 'state', 'unknown')),
      openedAt: formatDateTime(readString(row, 'openedAtUtc')),
      closedAt: formatDateTime(readString(row, 'closedAtUtc')),
      startingCash: formatMoney(readMoney(row, 'startingCash'), currencyCode),
      cashMovements: formatMoney(readMoney(row, 'cashMovementsTotal'), currencyCode),
      cashSales: formatMoney(readMoney(row, 'posCashPaymentsTotal'), currencyCode),
      refunds: formatMoney(readMoney(row, 'posRefundsTotal'), currencyCode),
      walletCashImpact: formatMoney(readMoney(row, 'billingCashImpactTotal'), currencyCode),
      expectedCash: formatMoney(readMoney(row, 'expectedCash'), currencyCode),
      countedCash: readMoney(row, 'countedCash') ? formatMoney(readMoney(row, 'countedCash'), currencyCode) : notIndicated,
      difference: formatMoney(readMoney(row, 'difference'), currencyCode)
    }))
  }, null, 2);
}

export function BackendPaymentsWorkspace({ currencyCode, backend }: { currencyCode: string; backend: OperatorBackendContext | null }) {
  const { t } = useI18n();
  const [paymentSearch, setPaymentSearch] = useState('');
  const [selectedOperationKey, setSelectedOperationKey] = useState('');
  const [selectedMethod, setSelectedMethod] = useState(t('op.payments.methods.cash'));
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const [loadStatus, setLoadStatus] = useState<LoadStatus>('fixture');
  const [currentShift, setCurrentShift] = useState<ShiftDto | null>(null);
  const [salesReport, setSalesReport] = useState<ReportResultDto | null>(null);
  const [cashReport, setCashReport] = useState<ReportResultDto | null>(null);
  const [shiftReport, setShiftReport] = useState<ReportResultDto | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [openStartingCash, setOpenStartingCash] = useState('0.00');
  const [openingNote, setOpeningNote] = useState(t('op.payments.default.openingNote'));
  const [closeCountedCash, setCloseCountedCash] = useState('');
  const [closingNote, setClosingNote] = useState(t('op.payments.default.closingNote'));
  const [cashMovementType, setCashMovementType] = useState('cash_in');
  const [cashMovementAmount, setCashMovementAmount] = useState('10.00');
  const [cashMovementReason, setCashMovementReason] = useState(t('op.payments.cash.defaultReason'));
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
      setFeedback({ label: t('op.payments.title'), state: 'failed', detail });
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
      `${t('op.pos.receipts.receiptFallback')} · ${posSaleLineSummary(row, t)}`,
      t('op.payments.ledger.typeSale'),
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
    ? [paymentOperationPlaceholder(loadStatus, currencyCode, loadError, false, t)]
    : filteredOperations.length > 0
      ? filteredOperations
      : [paymentOperationPlaceholder(loadStatus, currencyCode, loadError, operationSearch.length > 0, t)];
  const selectedOperation = visibleOperations.find(([time, type, client]) => `${time}-${type}-${client}` === selectedOperationKey) ?? visibleOperations[0];
  const selectedOperationSource = selectedOperation[6];
  const selectedOperationIsSale = selectedOperationSource !== null && readString(selectedOperationSource, 'posSaleId').length > 0;
  const selectedOperationScope = selectedOperationSource === null
    ? '—'
    : readString(selectedOperationSource, 'shiftId') ? t('op.payments.ledger.inShift') : t('op.payments.ledger.noShift');
  const selectedOperationSourceLabel = selectedOperationSource === null
    ? selectedOperation[3]
    : selectedOperationIsSale
      ? t('op.payments.ledger.typeSale')
      : t('op.payments.ledger.typeCash');
  const selectedOperationDetail = selectedOperationIsSale
    ? posSaleLineSummary(selectedOperationSource, t)
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
    [t('op.payments.methods.cash'), cashIn ? formatMinorUnits(cashIn.minorUnits, cashIn.currencyCode) : `0 ${currencyCode}`, t('op.payments.methods.cashShare'), t('op.payments.methods.cashOps', { count: cashRows.length })],
    [t('op.payments.methods.card'), netSales ? formatMinorUnits(netSales.minorUnits, netSales.currencyCode) : `0 ${currencyCode}`, t('op.payments.methods.cardShare'), t('op.payments.methods.receipts', { count: salesRows.length })],
    [t('op.payments.methods.refunds'), refunds ? formatMinorUnits(refunds.minorUnits, refunds.currencyCode) : `0 ${currencyCode}`, t('op.payments.methods.refundsShare'), t('op.payments.methods.byReport')],
    [t('op.payments.methods.difference'), difference ? formatMinorUnits(difference.minorUnits, difference.currencyCode) : `0 ${currencyCode}`, t('op.payments.methods.differenceShare'), t('op.payments.methods.byReconcile')]
  ];

  const openShiftActionKey = t('op.payments.reconcile.openShiftBtn');
  const addMovementActionKey = t('op.payments.cash.addBtn');
  const prepareCloseActionKey = t('op.payments.reconcile.prepareCloseBtn');
  const shiftSummaryActionKey = t('op.payments.export.shiftSummary');
  const cashMovementsActionKey = t('op.payments.export.cashMovements');
  const receiptListActionKey = t('op.payments.export.receiptList');
  const reconciliationActionKey = t('op.payments.export.reconciliation');

  const runReportAction = async (label: string) => {
    setCriticalAction(null);
    setFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const exportStamp = new Date().toISOString().replace(/[:.]/g, '-');
      if (label === shiftSummaryActionKey) {
        const csv = await apiClients.shifts.exportShiftReportCsv(nextBackend.branchId, { limit: 50 });
        downloadTextFile(`afk4-shift-summary-${exportStamp}.csv`, csv, 'text/csv;charset=utf-8');
      } else if (label === cashMovementsActionKey) {
        const csv = await apiClients.shifts.exportCashOperationReportCsv(nextBackend.branchId, { limit: 50 });
        downloadTextFile(`afk4-cash-movements-${exportStamp}.csv`, csv, 'text/csv;charset=utf-8');
      } else if (label === receiptListActionKey) {
        const csv = await apiClients.shifts.exportSalesReportCsv(nextBackend.branchId, { limit: 50 });
        downloadTextFile(`afk4-check-list-${exportStamp}.csv`, csv, 'text/csv;charset=utf-8');
      } else if (label === openShiftActionKey) {
        if (!hasPermission(nextBackend.session, permissionNames.openShift)) {
          throw new Error(t('op.payments.error.noPermOpenShift'));
        }

        if (currentShiftId) {
          throw new Error(t('op.payments.error.shiftAlreadyOpen'));
        }

        const startingCashMinorUnits = parseNonNegativeMoneyInputMinorUnits(openStartingCash);
        if (startingCashMinorUnits === null) {
          throw new Error(t('op.payments.error.invalidStartingCash'));
        }

        const openedShift = await apiClients.shifts.openShift(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          startingCash: { currencyCode, minorUnits: startingCashMinorUnits },
          openingNote: openingNote.trim(),
          idempotencyKey: createIdempotencyKey('shift-open')
        });
        setCurrentShift(openedShift);
        await loadPayments(nextBackend);
      } else if (label === addMovementActionKey) {
        if (!hasPermission(nextBackend.session, permissionNames.manageShiftCash)) {
          throw new Error(t('op.payments.error.noPermCash'));
        }

        if (!currentShiftId) {
          throw new Error(t('op.payments.error.noShiftForCash'));
        }

        const cashMovementMinorUnits = parseMoneyInputMinorUnits(cashMovementAmount);
        const reason = cashMovementReason.trim();
        if (cashMovementMinorUnits === null || !reason) {
          throw new Error(t('op.payments.error.invalidCashInput'));
        }

        await apiClients.shifts.recordCashMovement(currentShiftId, {
          organizationId: nextBackend.session.organizationId,
          movementType: cashMovementType,
          amount: { currencyCode, minorUnits: cashMovementMinorUnits },
          reason,
          idempotencyKey: createIdempotencyKey('shift-cash-movement')
        });
        setCashMovementAmount('10.00');
        setCashMovementReason(t('op.payments.cash.defaultReason'));
        await loadPayments(nextBackend);
      } else if (label === prepareCloseActionKey) {
        if (!hasPermission(nextBackend.session, permissionNames.closeShift)) {
          throw new Error(t('op.payments.error.noPermCloseShift'));
        }

        if (!currentShiftId) {
          throw new Error(t('op.payments.error.noShiftToClose'));
        }

        const countedCashMinorUnits = parseMoneyInputMinorUnits(closeCountedCash);
        if (countedCashMinorUnits === null) {
          throw new Error(t('op.payments.error.invalidCountedCash'));
        }

        const closedShift = await apiClients.shifts.closeShift(currentShiftId, {
          organizationId: nextBackend.session.organizationId,
          countedCash: { currencyCode, minorUnits: countedCashMinorUnits },
          closingNote: closingNote.trim(),
          idempotencyKey: createIdempotencyKey('shift-close')
        });
        setCurrentShift(closedShift);
      } else if (label === reconciliationActionKey) {
        const report = await apiClients.shifts.getShiftReport(nextBackend.branchId, { limit: 20 });
        downloadTextFile(`afk4-shift-reconciliation-${exportStamp}.json`, buildShiftReconciliationExportJson(report, currentShift, currencyCode, t('op.payments.reconcile.notIndicated')), 'application/json;charset=utf-8');
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
          <span>{t('op.payments.title')}</span>
          <h1>{t('op.payments.heading')}</h1>
        </div>
        <div className="screen-actions">
          <span className={`map-load-state ${loadStatus === 'backend' ? 'ready' : loadStatus}`}>{workspaceLoadStatusLabel(loadStatus, t('op.payments.loadedLabel'))}</span>
        </div>
      </section>

      <section className="state-strip payments-state-strip" aria-label={t('op.payments.stripLabel')}>
        <StateFlag label={t('op.payments.strip.revenue')} value={grossSales ? formatMinorUnits(grossSales.minorUnits, grossSales.currencyCode) : `0 ${currencyCode}`} />
        <StateFlag label={t('op.payments.strip.cash')} value={cashIn ? formatMinorUnits(cashIn.minorUnits, cashIn.currencyCode) : `0 ${currencyCode}`} />
        <StateFlag label={t('op.payments.strip.refunds')} value={refunds ? formatMinorUnits(refunds.minorUnits, refunds.currencyCode) : `0 ${currencyCode}`} critical={(refunds?.minorUnits ?? 0) > 0} />
        <StateFlag label={t('op.payments.strip.shift')} value={shiftStateLabel(readString(currentShift, 'state', 'нет смены'), t)} critical={currentShift === null} />
        <StateFlag label={t('op.payments.strip.reconcile')} value={difference ? formatMinorUnits(difference.minorUnits, difference.currencyCode) : `0 ${currencyCode}`} critical={(difference?.minorUnits ?? 0) !== 0} />
      </section>

      <section className="payments-layout">
        <section className="payments-panel payments-ledger-panel">
          <header className="payments-panel-title">
            <span>{t('op.payments.ledger.title')}</span>
            <strong>{t('op.payments.ledger.subtitle')}</strong>
          </header>
          <label className="payments-search">
            <Search size={14} />
            <input
              placeholder={t('op.payments.ledger.searchPlaceholder')}
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
            <span>{t('op.payments.summary.title')}</span>
            <strong>{t('op.payments.summary.subtitle')}</strong>
          </header>
          <div className="payments-total-card">
            <span>{t('op.payments.summary.selectedAt', { time: selectedOperation[0] })}</span>
            <strong>{netSales ? formatMinorUnits(netSales.minorUnits, netSales.currencyCode) : `0 ${currencyCode}`}</strong>
            <em>{selectedOperation[1]} · {selectedOperation[2]} · {selectedOperation[4]}</em>
          </div>
          <div className="payments-operation-detail" aria-label={t('op.payments.summary.detailLabel')}>
            <div><span>{t('op.payments.summary.fieldOp')}</span><strong>{selectedOperation[1]}</strong></div>
            <div><span>{t('op.payments.summary.fieldShift')}</span><strong>{selectedOperationScope}</strong></div>
            <div><span>{t('op.payments.summary.fieldType')}</span><strong>{selectedOperationSourceLabel}</strong></div>
            <div><span>{t('op.payments.summary.fieldDetail')}</span><strong>{selectedOperationDetail}</strong></div>
          </div>
          <div className="payments-metric-grid">
            <div><span>{t('op.payments.summary.metricReceipts')}</span><strong>{salesRows.length}</strong></div>
            <div><span>{t('op.payments.summary.metricCash')}</span><strong>{cashRows.length}</strong></div>
            <div><span>{t('op.payments.summary.metricRefunds')}</span><strong>{refunds ? formatMinorUnits(refunds.minorUnits, refunds.currencyCode) : `0 ${currencyCode}`}</strong></div>
            <div><span>{t('op.payments.summary.metricShifts')}</span><strong>{shiftRows.length}</strong></div>
          </div>
        </section>

        <section className="payments-panel payments-reconcile-panel">
          <header className="payments-panel-title">
            <span>{t('op.payments.reconcile.title')}</span>
            <strong>{t('op.payments.reconcile.subtitle')}</strong>
          </header>
          <div className="payments-open-form">
            <label>{t('op.payments.reconcile.startingCashLabel')}<input inputMode="decimal" value={openStartingCash} disabled={!canOpenShift} onChange={(event) => setOpenStartingCash(event.currentTarget.value)} /></label>
            <label>{t('op.payments.reconcile.openingNoteLabel')}<input value={openingNote} disabled={!canOpenShift} onChange={(event) => setOpeningNote(event.currentTarget.value)} /></label>
            <button type="button" disabled={!canOpenShift} onClick={() => runReportAction(openShiftActionKey)}>{t('op.payments.reconcile.openShiftBtn')}</button>
          </div>
          <div className="payments-reconcile-list">
            <div><span>{t('op.payments.reconcile.expected')}</span><strong>{expectedCash ? formatMinorUnits(expectedCash.minorUnits, expectedCash.currencyCode) : `0 ${currencyCode}`}</strong></div>
            <div><span>{t('op.payments.reconcile.counted')}</span><strong>{countedCash ? formatMinorUnits(countedCash.minorUnits, countedCash.currencyCode) : t('op.payments.reconcile.notClosed')}</strong></div>
            <div className={(difference?.minorUnits ?? 0) !== 0 ? 'attention' : undefined}><span>{t('op.payments.reconcile.difference')}</span><strong>{difference ? formatMinorUnits(difference.minorUnits, difference.currencyCode) : `0 ${currencyCode}`}</strong></div>
          </div>
          <div className="payments-close-form">
            <label>{t('op.payments.reconcile.countedCashLabel')}<input inputMode="decimal" value={closeCountedCash} disabled={!canCloseShift} onChange={(event) => setCloseCountedCash(event.currentTarget.value)} /></label>
            <label>{t('op.payments.reconcile.closingNoteLabel')}<input value={closingNote} disabled={!canCloseShift} onChange={(event) => setClosingNote(event.currentTarget.value)} /></label>
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
            {t('op.payments.reconcile.prepareCloseBtn')}
          </button>
          {criticalAction === 'close-shift' && (
            <CriticalActionConfirmation
              title={t('op.payments.reconcile.confirmTitle')}
              detail={t('op.payments.reconcile.confirmDetail', {
                counted: closeCountedCash || '0',
                currency: currencyCode,
                expected: expectedCash ? formatMinorUnits(expectedCash.minorUnits, expectedCash.currencyCode) : `0 ${currencyCode}`
              })}
              impact={t('op.payments.reconcile.confirmImpact')}
              confirmLabel={t('op.payments.reconcile.confirmBtn')}
              disabled={feedback.state === 'pending'}
              onCancel={() => setCriticalAction(null)}
              onConfirm={() => void runReportAction(prepareCloseActionKey)}
            />
          )}
          <FeedbackNotice feedback={feedback} />
        </section>

        <section className="payments-panel payments-methods-panel">
          <header className="payments-panel-title">
            <span>{t('op.payments.methods.title')}</span>
            <strong>{t('op.payments.methods.subtitle')}</strong>
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
            <span>{t('op.payments.cash.title')}</span>
            <strong>{t('op.payments.cash.subtitle')}</strong>
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
            <label>{t('op.payments.cash.typeLabel')}
              <select value={cashMovementType} disabled={!canRecordCashMovement} onChange={(event) => setCashMovementType(event.currentTarget.value)}>
                <option value="cash_in">{t('op.payments.cash.typeIn')}</option>
                <option value="cash_out">{t('op.payments.cash.typeOut')}</option>
              </select>
            </label>
            <label>{t('op.payments.cash.amountLabel')}<input inputMode="decimal" value={cashMovementAmount} disabled={!canRecordCashMovement} onChange={(event) => setCashMovementAmount(event.currentTarget.value)} /></label>
            <label className="payments-cash-reason">{t('op.payments.cash.reasonLabel')}<input value={cashMovementReason} disabled={!canRecordCashMovement} onChange={(event) => setCashMovementReason(event.currentTarget.value)} /></label>
            <button type="button" disabled={!canRecordCashMovement} onClick={() => runReportAction(addMovementActionKey)}>{t('op.payments.cash.addBtn')}</button>
          </div>
        </section>

        <section className="payments-panel payments-export-panel">
          <header className="payments-panel-title">
            <span>{t('op.payments.export.title')}</span>
            <strong>{t('op.payments.export.subtitle')}</strong>
          </header>
          <div className="payments-export-grid">
            {[
              [shiftSummaryActionKey, ReceiptText],
              [cashMovementsActionKey, Banknote],
              [receiptListActionKey, ArrowRightLeft],
              [reconciliationActionKey, ShieldAlert]
            ].map(([label, Icon]) => (
              <button key={label as string} type="button" onClick={() => runReportAction(label as string)}><Icon size={16} />{label as string}</button>
            ))}
          </div>
        </section>
      </section>
    </main>
  );
}
