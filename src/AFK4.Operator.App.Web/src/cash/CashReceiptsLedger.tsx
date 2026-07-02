import { useEffect, useMemo, useState } from 'react';
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
import { CriticalActionConfirmation, FeedbackNotice, Money } from '../operatorPrimitives';
import type { Feedback, OperatorBackendContext } from '../operatorTypes';
import type { OperatorAuthSession } from '../authClient';
import type { PosSaleDto, ReceiptDto } from '../operatorApiClients';

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
  const [criticalAction, setCriticalAction] = useState<'refund' | null>(null);
  const [refundReason, setRefundReason] = useState(() => t('op.pos.defaultRefundReason'));
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
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
  const refundable = rows.filter((row) => readString(row, 'state').toLowerCase() === 'paid');
  const selected = refundable.find((row) => readString(row, 'posSaleId') === selectedSaleId)
    ?? rows.find((row) => readString(row, 'posSaleId') === selectedSaleId)
    ?? refundable[0]
    ?? rows[0];
  const selectedId = readString(selected, 'posSaleId');
  const canView = backend !== null && hasPermission(session, permissionNames.viewReceipt);
  const canRefund = backend !== null
    && selectedId.length > 0
    && readString(selected, 'state').toLowerCase() === 'paid'
    && hasPermission(session, permissionNames.refundPosSale);

  const loadSaleDetail = async (saleId: string) => {
    setReceiptDetail(null);
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
      setSaleDetail(sale);
      setReceiptDetail(receipt);
      setFeedback({ label: t('op.pos.feedback.receiptDetails'), state: 'confirmed' });
    } catch (error) {
      setFeedback({ label: t('op.pos.feedback.receiptDetails'), state: 'failed', detail: projectOperatorError(error, t).detail });
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
    <section className="cash-receipts">
      <div className="cash-ledger-stats">
        <span className="cash-ledger-stat cash-ledger-stat--lead">
          <em>{t('op.pos.strip.sales')}</em><b>{rows.length} · <Money minorUnits={readMoney(report, 'grossSalesTotal')?.minorUnits ?? 0} currencyCode={currencyCode} /></b>
        </span>
        <span className="cash-ledger-stat cash-ledger-stat--out">
          <em>{t('op.pos.strip.refunds')}</em><b><Money minorUnits={readMoney(report, 'refundsTotal')?.minorUnits ?? 0} currencyCode={currencyCode} /></b>
        </span>
      </div>

      <div className="pos-receipt-list">
        {rows.length === 0 ? (
          <p className="cash-shift-empty-note">{t('op.pos.receipts.emptyPlatform')}</p>
        ) : (
          rows.slice(0, 30).map((row) => {
            const id = readString(row, 'posSaleId');
            return (
              <button
                key={id}
                type="button"
                className={`pos-receipt-row ${id === selectedId ? 'selected' : ''}`}
                disabled={!canView || feedback.state === 'pending'}
                onClick={() => { setSelectedSaleId(id); void loadSaleDetail(id); }}
              >
                <span>{formatTime(readString(row, 'createdAtUtc'))}</span>
                <strong>{posSaleStateLabel(readString(row, 'state', 'sale'), t)}</strong>
                <em>{posSaleLineSummary(row, t)}</em>
                <b><Money minorUnits={readMoney(row, 'total')?.minorUnits ?? 0} currencyCode={currencyCode} /></b>
              </button>
            );
          })
        )}
      </div>

      {saleDetail !== null && (
        <div className="pos-sale-detail">
          <div>
            <span>{t('op.pos.receipts.detailsTitle')}</span>
            <strong>{posSaleStateLabel(readString(saleDetail, 'state', 'sale'), t)}</strong>
            <b><Money minorUnits={readMoney(saleDetail, 'total')?.minorUnits ?? 0} currencyCode={currencyCode} /></b>
          </div>
          {readArray(saleDetail, 'lines').slice(0, 6).map((line) => (
            <p key={`${readString(line, 'productId')}-${readNumber(line, 'quantity', 0)}`}>
              {readString(line, 'productName', t('op.pos.receipts.productFallback'))} · {readNumber(line, 'quantity', 0)} × <Money minorUnits={readMoney(line, 'unitPrice')?.minorUnits ?? 0} currencyCode={currencyCode} />
            </p>
          ))}
          {receiptDetail !== null && (
            <div className="pos-receipt-detail">
              <span>{t('op.pos.receipts.platformReceipt')}</span>
              <strong>{readString(receiptDetail, 'receiptNumber', t('op.pos.receipts.receiptFallback'))}</strong>
              <b><Money minorUnits={readMoney(receiptDetail, 'total')?.minorUnits ?? 0} currencyCode={currencyCode} /></b>
              <p>{posReceiptTypeLabel(readString(receiptDetail, 'receiptType', 'sale'), t)}</p>
            </div>
          )}
          <div className="pos-receipt-actions">
            {canRefund && (
              <button type="button" disabled={feedback.state === 'pending'} onClick={() => { setFeedback(emptyFeedback); setCriticalAction('refund'); }}>
                <Undo2 size={13} aria-hidden="true" />{t('op.pos.quick.refundLabel')}
              </button>
            )}
            <button type="button" disabled={feedback.state === 'pending'} onClick={printReceipt}>
              <ReceiptText size={13} aria-hidden="true" />{t('op.pos.receipts.printBtn')}
            </button>
            <button type="button" disabled={feedback.state === 'pending'} onClick={exportReceipt}>
              <ArrowRightLeft size={13} aria-hidden="true" />{t('op.pos.receipts.exportBtn')}
            </button>
          </div>
        </div>
      )}

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

      <FeedbackNotice feedback={feedback} />
    </section>
  );
}
