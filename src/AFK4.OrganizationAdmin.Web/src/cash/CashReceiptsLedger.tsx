import { useEffect, useMemo, useRef, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { ArrowRightLeft, ReceiptText, Undo2 } from 'lucide-react';
import {
  buildPosReceiptText,
  createAuthenticatedOperatorClients,
  createIdempotencyKey,
  downloadTextFile,
  emptyFeedback,
  escapeHtml,
  formatMoney,
  formatTime,
  paymentSourceLabel,
  posReceiptTypeLabel,
  posSaleLineSummary,
  posSaleStateLabel,
  readArray,
  readMoney,
  readNumber,
  readRecord,
  readString,
  requireBackend,
  safeReceiptFileName
} from '../operatorHelpers';
import { projectOperatorError } from '../apiErrors';
import { hasPermission, permissionNames } from '../operatorPermissions';
import { CriticalActionConfirmation, Money } from '../operatorPrimitives';
import type { Feedback, OperatorBackendContext } from '../operatorTypes';
import type { OperatorAuthSession } from '../authClient';
import type { PosSaleDto, ReceiptDto } from '../operatorApiClients';
import { useFeedbackToasts } from '../useFeedbackToasts';
import { CashMetricStrip, CashRegisterRows, CashTerminalSplit } from './CashTerminalFrame';

type ReceiptDetailState = {
  status: 'idle' | 'loading' | 'ready' | 'failed';
  saleId: string;
  error: string | null;
};

// Сегмент «Чеки» в «Журнале кассы»: продажи смены + деталь чека + возврат (переехало из POS
// «Последние чеки»/«Быстрые операции»). Возврат — money-path, та же логика, что была в кассе.
export function CashReceiptsLedger({
  backend,
  branchId,
  currencyCode,
  session
}: {
  backend: OperatorBackendContext | null;
  branchId: string;
  currencyCode: string;
  session: OperatorAuthSession | null;
}) {
  const { t } = useI18n();
  const clients = useMemo(
    () => (backend ? createAuthenticatedOperatorClients(backend.config, backend.session) : null),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [backend?.config, backend?.session]
  );

  const [report, setReport] = useState<Record<string, unknown> | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [selectedSaleId, setSelectedSaleId] = useState('');
  const [saleDetail, setSaleDetail] = useState<PosSaleDto | null>(null);
  const [receiptDetail, setReceiptDetail] = useState<ReceiptDto | null>(null);
  const [detailState, setDetailState] = useState<ReceiptDetailState>({ status: 'idle', saleId: '', error: null });
  const detailRequest = useRef(0);
  const [criticalAction, setCriticalAction] = useState<'refund' | null>(null);
  const [refundReason, setRefundReason] = useState(() => t('op.pos.defaultRefundReason'));
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  useFeedbackToasts(feedback);
  const [nonce, setNonce] = useState(0);

  useEffect(() => {
    if (clients === null) { setLoading(false); return undefined; }
    let active = true;
    setLoading(true);
    setLoadError(null);
    clients.shifts.getSalesReport(branchId, { limit: 50 })
      .then((result) => { if (active) setReport(result); })
      .catch((error) => { if (active) setLoadError(projectOperatorError(error, t).detail); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [clients, branchId, nonce]);

  const rows = readArray<Record<string, unknown>>(report, 'rows');
  const selected = rows.find((row) => readString(row, 'posSaleId') === selectedSaleId);
  const selectedId = readString(selected, 'posSaleId');
  const canView = backend !== null && hasPermission(session, permissionNames.viewReceipt);
  const canRefund = backend !== null
    && selectedId.length > 0
    && readString(selected, 'state').toLowerCase() === 'paid'
    && hasPermission(session, permissionNames.refundPosSale);
  const paymentMethodLabel = (method: string) => {
    if (method.toLowerCase() === 'card') return t('op.checkout.method.card');
    if (['wallet', 'deposit'].includes(method.toLowerCase())) return t('op.checkout.method.wallet');
    return paymentSourceLabel(method, t);
  };

  const loadSaleDetail = async (saleId: string) => {
    const request = ++detailRequest.current;
    setSelectedSaleId(saleId);
    setSaleDetail(null);
    setReceiptDetail(null);
    setDetailState({ status: 'loading', saleId, error: null });
    setFeedback({ label: t('op.pos.feedback.receiptDetails'), state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      if (!hasPermission(nextBackend.session, permissionNames.viewReceipt)) {
        throw new Error(t('op.pos.error.noPermissionViewReceipts'));
      }
      if (!saleId) throw new Error(t('op.pos.error.selectReceiptFromList'));
      const built = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const sale = await built.pos.getSale(saleId);
      const latestReceipt = readRecord(sale, 'latestReceipt');
      const receiptId = readString(latestReceipt, 'receiptId');
      const receipt = receiptId ? await built.pos.getReceipt(receiptId) : null;
      if (request !== detailRequest.current) return;
      setSaleDetail(sale);
      setReceiptDetail(receipt);
      setDetailState({ status: 'ready', saleId, error: null });
      setFeedback({ label: t('op.pos.feedback.receiptDetails'), state: 'confirmed' });
    } catch (error) {
      if (request !== detailRequest.current) return;
      const detail = projectOperatorError(error, t).detail;
      setDetailState({ status: 'failed', saleId, error: detail });
      setFeedback({ label: t('op.pos.feedback.receiptDetails'), state: 'failed', detail });
    }
  };

  const refundSelected = async () => {
    setCriticalAction(null);
    setFeedback({ label: t('op.pos.feedback.refund'), state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      if (!hasPermission(nextBackend.session, permissionNames.refundPosSale)) {
        throw new Error(t('op.pos.error.noPermissionRefund'));
      }
      if (!selectedId) throw new Error(t('op.pos.error.selectReceiptForRefund'));
      const reason = refundReason.trim();
      if (!reason) throw new Error(t('op.pos.error.enterRefundReason'));
      await createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session).pos.refundSale(selectedId, {
        organizationId: nextBackend.session.organizationId,
        reason,
        idempotencyKey: createIdempotencyKey('pos-refund')
      });
      setFeedback({ label: t('op.pos.feedback.refund'), state: 'confirmed' });
      setSaleDetail(null);
      setDetailState({ status: 'idle', saleId: '', error: null });
      setNonce((value) => value + 1);
    } catch (error) {
      setFeedback({ label: t('op.pos.feedback.refund'), state: 'failed', detail: projectOperatorError(error, t).detail });
    }
  };

  const selectedReceiptRecord = receiptDetail ?? readRecord(saleDetail, 'latestReceipt');

  const printReceipt = () => {
    setFeedback({ label: t('op.pos.feedback.print'), state: 'pending' });
    try {
      if (saleDetail === null) throw new Error(t('op.pos.error.openReceiptFirst'));
      const receiptText = buildPosReceiptText(saleDetail, selectedReceiptRecord, currencyCode, t);
      const printWindow = window.open('', '_blank', 'width=360,height=640');
      if (printWindow === null) throw new Error(t('op.pos.error.printWindowFailed'));
      printWindow.document.write(`<pre style="font: 13px/1.45 monospace; white-space: pre-wrap;">${escapeHtml(receiptText)}</pre>`);
      printWindow.document.close();
      printWindow.focus();
      printWindow.print();
      setFeedback({ label: t('op.pos.feedback.print'), state: 'confirmed' });
    } catch (error) {
      setFeedback({ label: t('op.pos.feedback.print'), state: 'failed', detail: projectOperatorError(error, t).detail });
    }
  };

  const exportReceipt = () => {
    setFeedback({ label: t('op.pos.feedback.export'), state: 'pending' });
    try {
      if (saleDetail === null) throw new Error(t('op.pos.error.openReceiptFirstExport'));
      const receiptText = buildPosReceiptText(saleDetail, selectedReceiptRecord, currencyCode, t);
      const receiptNumber = readString(selectedReceiptRecord, 'receiptNumber', 'receipt');
      downloadTextFile(`${safeReceiptFileName(receiptNumber)}.txt`, receiptText);
      setFeedback({ label: t('op.pos.feedback.export'), state: 'confirmed' });
    } catch (error) {
      setFeedback({ label: t('op.pos.feedback.export'), state: 'failed', detail: projectOperatorError(error, t).detail });
    }
  };

  if (loading) return <p className="workspace-loading">{t('op.cash.journal.loading')}</p>;
  if (loadError) return <p className="workspace-error" role="alert">{loadError}</p>;

  return (
    <section className="cash-receipts-terminal">
      <CashMetricStrip ariaLabel={t('op.cash.receipts.metricsAria')} items={[
        { label: t('op.pos.strip.sales'), value: rows.length },
        { label: t('op.cash.receipts.gross'), value: <Money minorUnits={readMoney(report, 'grossSalesTotal')?.minorUnits ?? 0} currencyCode={currencyCode} />, tone: 'positive' },
        { label: t('op.pos.strip.refunds'), value: <Money minorUnits={readMoney(report, 'refundsTotal')?.minorUnits ?? 0} currencyCode={currencyCode} />, tone: 'danger' }
      ]} />
      <CashTerminalSplit
        inspectorOpen={selected !== undefined}
        closeLabel={t('common.close')}
        onCloseInspector={() => { detailRequest.current += 1; setSelectedSaleId(''); setDetailState({ status: 'idle', saleId: '', error: null }); }}
        register={rows.length === 0 ? <p className="cash-shift-empty-note cash-ledger-empty">{t('op.pos.receipts.emptyPlatform')}</p> : <CashRegisterRows rows={rows.slice(0, 30)} selectedId={selectedSaleId} getId={(row) => readString(row, 'posSaleId')} ariaLabel={t('op.cash.receipts.registerAria')} onSelect={(id) => { if (canView) void loadSaleDetail(id); }} renderRow={(row) => <div className="cash-receipt-row">
          <span>{formatTime(readString(row, 'createdAtUtc'))}</span>
          <strong>{posSaleStateLabel(readString(row, 'state', 'sale'), t)}</strong>
          <em>{posSaleLineSummary(row, t)}</em>
          <b><Money minorUnits={readMoney(row, 'total')?.minorUnits ?? 0} currencyCode={currencyCode} /></b>
        </div>} />}
        inspector={detailState.status === 'loading' ? <p className="cash-receipt-detail-state">{t('op.cash.receipts.detailLoading')}</p>
          : detailState.status === 'failed' ? <div className="cash-receipt-detail-state"><strong>{t('op.cash.receipts.detailFailed')}</strong><small>{detailState.error}</small><button type="button" onClick={() => void loadSaleDetail(detailState.saleId)}>{t('op.cash.journal.retry')}</button></div>
          : detailState.status === 'ready' && saleDetail !== null ? <div className="cash-receipt-inspector">
            <div className="cash-receipt-inspector-head"><span>{t('op.pos.receipts.detailsTitle')}</span><strong>{posSaleStateLabel(readString(saleDetail, 'state', 'sale'), t)}</strong><b>{readMoney(saleDetail, 'total') ? <Money minorUnits={readMoney(saleDetail, 'total')!.minorUnits} currencyCode={currencyCode} /> : '—'}</b></div>
            <section><h3>{t('op.cash.receipts.lines')}</h3>{readArray(saleDetail, 'lines').map((line) => <div className="cash-receipt-line" key={`${readString(line, 'productId')}-${readNumber(line, 'quantity', 0)}`}><span>{readString(line, 'productName', t('op.pos.receipts.productFallback'))}<small>{readNumber(line, 'quantity', 0)} × <Money minorUnits={readMoney(line, 'unitPrice')?.minorUnits ?? 0} currencyCode={currencyCode} /></small></span><strong><Money minorUnits={readMoney(line, 'lineTotal')?.minorUnits ?? (readMoney(line, 'unitPrice')?.minorUnits ?? 0) * readNumber(line, 'quantity', 0)} currencyCode={currencyCode} /></strong></div>)}</section>
            <section><h3>{t('op.cash.receipts.payments')}</h3>{readArray(saleDetail, 'payments').map((payment, index) => <div className="cash-receipt-payment" key={`${readString(payment, 'method', readString(payment, 'source'))}-${index}`}><span>{paymentMethodLabel(readString(payment, 'method', readString(payment, 'source')))}</span><strong><Money minorUnits={readMoney(payment, 'amount')?.minorUnits ?? 0} currencyCode={currencyCode} /></strong></div>)}</section>
            {receiptDetail !== null ? <div className="pos-receipt-detail"><span>{t('op.pos.receipts.platformReceipt')}</span><strong>№ {readString(receiptDetail, 'receiptNumber', t('op.pos.receipts.receiptFallback'))}</strong><p>{posReceiptTypeLabel(readString(receiptDetail, 'receiptType', 'sale'), t)}</p></div> : null}
            <div className="pos-receipt-actions">
              {canRefund ? <button type="button" disabled={feedback.state === 'pending'} onClick={() => { setFeedback(emptyFeedback); setCriticalAction('refund'); }}><Undo2 size={13} aria-hidden="true" />{t('op.pos.quick.refundLabel')}</button> : null}
              <button type="button" disabled={feedback.state === 'pending'} onClick={printReceipt}><ReceiptText size={13} aria-hidden="true" />{t('op.pos.receipts.printBtn')}</button>
              <button type="button" disabled={feedback.state === 'pending'} onClick={exportReceipt}><ArrowRightLeft size={13} aria-hidden="true" />{t('op.pos.receipts.exportBtn')}</button>
            </div>
          </div> : <p className="cash-inspector-empty">{t('op.cash.receipts.selectHint')}</p>}
      />

      {criticalAction === 'refund' && (
        <CriticalActionConfirmation
          title={t('op.pos.quick.refundConfirmTitle')}
          detail={t('op.pos.quick.refundConfirmDetail', { amount: formatMoney(readMoney(selected, 'total'), currencyCode) })}
          impact={t('op.pos.quick.refundConfirmImpact')}
          confirmLabel={t('op.pos.quick.refundConfirmBtn')}
          disabled={feedback.state === 'pending'}
          onCancel={() => setCriticalAction(null)}
          onConfirm={() => void refundSelected()}
        >
          <label className="critical-confirmation-field">
            <span>{t('op.pos.quick.refundReasonLabel')}</span>
            <input value={refundReason} disabled={feedback.state === 'pending'} onChange={(event) => setRefundReason(event.currentTarget.value)} />
          </label>
        </CriticalActionConfirmation>
      )}
    </section>
  );
}
