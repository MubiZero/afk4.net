import { useEffect, useMemo, useRef, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { ArrowRightLeft, Ban, ReceiptText, Undo2 } from 'lucide-react';
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
  requireBackend,
  safeReceiptFileName
} from '../operatorHelpers';
import { PermissionRefusal, projectOperatorError, type OperatorErrorProjection } from '../apiErrors';
import { canSelfVoidSale } from './selfVoid';
import { hasPermission, permissionNames } from '../operatorPermissions';
import { CriticalActionConfirmation, EmptyState, LoadFailureState, Money } from '../operatorPrimitives';
import type { Feedback, OperatorBackendContext } from '../operatorTypes';
import type { OperatorAuthSession } from '../authClient';
import type { PosSaleDto, ReceiptDto, SalesReportResultDto } from '../operatorApiClients';
import { useFeedbackToasts } from '../useFeedbackToasts';
import { CashMetricStrip, CashRegisterRows, CashTerminalSkeleton, CashTerminalSplit } from './CashTerminalFrame';
import { DeferredSkeleton, SkeletonControl, SkeletonLine } from '../LoadingSkeleton';
import { useShownFor } from '../useShownFor';

type ReceiptDetailState = {
  status: 'idle' | 'loading' | 'ready' | 'failed';
  saleId: string;
  // Чек, открытый не из ленты, а из палитры: повтор после сбоя должен знать, что перезапрашивать,
  // — по номеру чека продажи под рукой ещё нет.
  receiptId: string;
  error: OperatorErrorProjection | null;
};

// Сегмент «Чеки» в «Журнале кассы»: продажи смены + деталь чека + возврат (переехало из POS
// «Последние чеки»/«Быстрые операции»). Возврат — money-path, та же логика, что была в кассе.
export function CashReceiptsLedger({
  backend,
  branchId,
  currencyCode,
  session,
  openReceipt
}: {
  backend: OperatorBackendContext | null;
  branchId: string;
  currencyCode: string;
  session: OperatorAuthSession | null;
  // Чек, выбранный в командной палитре. Лента показывает последние 50 продаж смены, а с чеком
  // приходят и через неделю — такой чек открывается по идентификатору, мимо ленты.
  openReceipt?: { receiptId: string } | null;
}) {
  const { t } = useI18n();
  const clients = useMemo(
    () => (backend ? createAuthenticatedOperatorClients(backend.config, backend.session) : null),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [backend?.config, backend?.session]
  );

  const [report, setReport] = useState<SalesReportResultDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [selectedSaleId, setSelectedSaleId] = useState('');
  const [saleDetail, setSaleDetail] = useState<PosSaleDto | null>(null);
  const [receiptDetail, setReceiptDetail] = useState<ReceiptDto | null>(null);
  const [detailState, setDetailState] = useState<ReceiptDetailState>({ status: 'idle', saleId: '', receiptId: '', error: null });
  const detailRequest = useRef(0);
  const [criticalAction, setCriticalAction] = useState<'refund' | 'void' | null>(null);
  // Открытая смена филиала: без неё отменить свою продажу нельзя — закрытая смена уже сведена.
  const [openShiftId, setOpenShiftId] = useState<string | null>(null);
  const [refundReason, setRefundReason] = useState(() => t('op.pos.defaultRefundReason'));
  const [voidReason, setVoidReason] = useState('');
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  useFeedbackToasts(feedback);
  const [nonce, setNonce] = useState(0);
  const shown = useShownFor(branchId);

  useEffect(() => {
    if (clients === null) { setLoading(false); return undefined; }
    let active = true;
    if (!shown.isShown()) setLoading(true);
    clients.shifts.getSalesReport(branchId, { limit: 50 })
      .then((result) => { if (active) { setReport(result); setLoadError(null); } })
      .catch((error) => { if (active) setLoadError(projectOperatorError(error, t).detail); })
      .finally(() => { if (active) { setLoading(false); shown.markShown(); } });
    return () => { active = false; };
  }, [clients, branchId, nonce]);

  useEffect(() => {
    if (clients === null) { setOpenShiftId(null); return undefined; }
    let active = true;
    clients.shifts.getCurrentShift(branchId)
      .then((shift) => { if (active) setOpenShiftId(shift?.shiftId || null); })
      // Молча: без открытой смены отмена просто не предлагается, и отдельным отказом на экране
      // чеков это ничего не объясняет.
      .catch(() => { if (active) setOpenShiftId(null); });
    return () => { active = false; };
  }, [clients, branchId, nonce]);

  const rows = report?.rows ?? [];
  const selected = rows.find((row) => row.posSaleId === selectedSaleId) ?? null;
  // Продажа, открытая по чеку из палитры, в ленте смены может и не лежать — тогда её состояние
  // и сумму знает только сама загруженная продажа. Читаем сначала её: она же и авторитетнее.
  const selectedId = saleDetail?.posSaleId ?? selected?.posSaleId ?? '';
  const saleState = (saleDetail?.state ?? selected?.state ?? '').toLowerCase();
  const selectedTotal = saleDetail?.total ?? selected?.total ?? null;
  const canView = backend !== null && hasPermission(session, permissionNames.viewReceipt);
  const canRefund = backend !== null
    && selectedId.length > 0
    && saleState === 'paid'
    && hasPermission(session, permissionNames.refundPosSale);
  // Отмена вынимает деньги из смены. Широкое право отменяет что угодно; кассир — только свой
  // только что пробитый чек, и ровно это правило зеркалит selfVoid.ts (решает всё равно сервер).
  const saleIsVoidable = selectedId.length > 0 && ['sale', 'paid'].includes(saleState);
  const canVoidAny = backend !== null && saleIsVoidable && hasPermission(session, permissionNames.voidPosSale);
  const canSelfVoid = backend !== null
    && saleIsVoidable
    && !canVoidAny
    && session !== null
    && saleDetail !== null
    && hasPermission(session, permissionNames.voidOwnRecentPosSale)
    && canSelfVoidSale({
      createdByStaffUserId: saleDetail.createdByStaffUserId,
      createdAtUtc: saleDetail.createdAtUtc,
      saleShiftId: saleDetail.shiftId,
      actorStaffUserId: session.staffUserId,
      openShiftId,
      nowMs: Date.now()
    });
  const canVoid = canVoidAny || canSelfVoid;

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
    setDetailState({ status: 'loading', saleId, receiptId: '', error: null });
    setFeedback({ label: t('op.pos.feedback.receiptDetails'), state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      if (!hasPermission(nextBackend.session, permissionNames.viewReceipt)) {
        throw new PermissionRefusal(t('op.pos.error.noPermissionViewReceipts'));
      }
      if (!saleId) throw new Error(t('op.pos.error.selectReceiptFromList'));
      const built = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const sale = await built.pos.getSale(saleId);
      const receiptId = sale.latestReceipt?.receiptId ?? '';
      const receipt = receiptId ? await built.pos.getReceipt(receiptId) : null;
      if (request !== detailRequest.current) return;
      setSaleDetail(sale);
      setReceiptDetail(receipt);
      setDetailState({ status: 'ready', saleId, receiptId: receiptId, error: null });
      setFeedback({ label: t('op.pos.feedback.receiptDetails'), state: 'confirmed' });
    } catch (error) {
      if (request !== detailRequest.current) return;
      const failure = projectOperatorError(error, t);
      setDetailState({ status: 'failed', saleId, receiptId: '', error: failure });
      setFeedback({ label: t('op.pos.feedback.receiptDetails'), state: 'failed', detail: failure.detail });
    }
  };

  // Чек из палитры: продажи под рукой нет, зато есть чек — от него и пляшем. Сессионный чек
  // (закрытие сессии, а не продажа) продажи не имеет вовсе, и это нормальный случай, а не сбой.
  const loadReceiptDetail = async (receiptId: string) => {
    const request = ++detailRequest.current;
    setSaleDetail(null);
    setReceiptDetail(null);
    setDetailState({ status: 'loading', saleId: '', receiptId, error: null });
    setFeedback({ label: t('op.pos.feedback.receiptDetails'), state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      if (!hasPermission(nextBackend.session, permissionNames.viewReceipt)) {
        throw new PermissionRefusal(t('op.pos.error.noPermissionViewReceipts'));
      }
      const built = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const receipt = await built.pos.getReceipt(receiptId);
      const sale = receipt.posSaleId ? await built.pos.getSale(receipt.posSaleId) : null;
      if (request !== detailRequest.current) return;
      setSelectedSaleId(receipt.posSaleId ?? '');
      setSaleDetail(sale);
      setReceiptDetail(receipt);
      setDetailState({ status: 'ready', saleId: receipt.posSaleId ?? '', receiptId, error: null });
      setFeedback({ label: t('op.pos.feedback.receiptDetails'), state: 'confirmed' });
    } catch (error) {
      if (request !== detailRequest.current) return;
      const failure = projectOperatorError(error, t);
      setDetailState({ status: 'failed', saleId: '', receiptId, error: failure });
      setFeedback({ label: t('op.pos.feedback.receiptDetails'), state: 'failed', detail: failure.detail });
    }
  };

  useEffect(() => {
    if (!openReceipt || clients === null) return;
    void loadReceiptDetail(openReceipt.receiptId);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [openReceipt?.receiptId, clients]);

  // Отмена. Отдельно от возврата: возврат оставляет продажу в истории и заводит обратную
  // запись, отмена убирает саму продажу — исход другой, и подтверждение у него своё.
  const voidSelected = async () => {
    setCriticalAction(null);
    setFeedback({ label: t('op.pos.feedback.void'), state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      if (!canVoid) {
        throw new Error(t('op.pos.error.noPermissionVoid'));
      }
      if (!selectedId) throw new Error(t('op.pos.error.selectReceiptFromList'));
      const reason = voidReason.trim();
      if (!reason) throw new Error(t('op.pos.error.enterVoidReason'));
      await createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session).pos.voidSale(selectedId, {
        organizationId: nextBackend.session.organizationId,
        reason,
        idempotencyKey: createIdempotencyKey('pos-void')
      });
      setFeedback({ label: t('op.pos.feedback.void'), state: 'confirmed' });
      setVoidReason('');
      setNonce((value) => value + 1);
      void loadSaleDetail(selectedId);
    } catch (error) {
      setFeedback({ label: t('op.pos.feedback.void'), state: 'failed', detail: projectOperatorError(error, t).detail });
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
      setDetailState({ status: 'idle', saleId: '', receiptId: '', error: null });
      setNonce((value) => value + 1);
    } catch (error) {
      setFeedback({ label: t('op.pos.feedback.refund'), state: 'failed', detail: projectOperatorError(error, t).detail });
    }
  };

  const selectedReceiptRecord = receiptDetail ?? saleDetail?.latestReceipt ?? null;

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
      const receiptNumber = selectedReceiptRecord?.receiptNumber || 'receipt';
      downloadTextFile(`${safeReceiptFileName(receiptNumber)}.txt`, receiptText);
      setFeedback({ label: t('op.pos.feedback.export'), state: 'confirmed' });
    } catch (error) {
      setFeedback({ label: t('op.pos.feedback.export'), state: 'failed', detail: projectOperatorError(error, t).detail });
    }
  };

  if (loading) {
    return (
      <DeferredSkeleton>
        <CashTerminalSkeleton
          className="cash-receipts-terminal"
          metrics={3}
          inspectorHint={t('op.cash.receipts.selectHint')}
          row={<div className="cash-receipt-row"><span><SkeletonLine width="3em" /></span><strong><SkeletonLine width="5em" /></strong><em><SkeletonLine width="14em" /></em><b><SkeletonLine width="4em" /></b></div>}
        />
      </DeferredSkeleton>
    );
  }
  if (loadError) return <p className="ui-alert ui-alert--spaced" role="alert">{loadError}</p>;

  return (
    <section className="cash-receipts-terminal">
      <CashMetricStrip ariaLabel={t('op.cash.receipts.metricsAria')} items={[
        { label: t('op.pos.strip.sales'), value: rows.length },
        { label: t('op.cash.receipts.gross'), value: <Money minorUnits={report?.grossSalesTotal.minorUnits ?? 0} currencyCode={currencyCode} />, tone: 'positive' },
        { label: t('op.pos.strip.refunds'), value: <Money minorUnits={report?.refundsTotal.minorUnits ?? 0} currencyCode={currencyCode} />, tone: 'danger' }
      ]} />
      <CashTerminalSplit
          inspectorLabel={t('op.cash.inspector.aria')}
        inspectorOpen={selectedSaleId.length > 0 || detailState.status !== 'idle'}
        closeLabel={t('common.close')}
        onCloseInspector={() => { detailRequest.current += 1; setSelectedSaleId(''); setDetailState({ status: 'idle', saleId: '', receiptId: '', error: null }); }}
        register={rows.length === 0 ? <EmptyState inline className="cash-shift-empty-note cash-ledger-empty" title={t('op.cash.receipts.empty')} next={{ kind: 'calm', hint: t('op.cash.receipts.emptyHint') }} /> : <CashRegisterRows rows={rows.slice(0, 30)} selectedId={selectedSaleId} getId={(row) => row.posSaleId} ariaLabel={t('op.cash.receipts.registerAria')} onSelect={(id) => { if (canView) void loadSaleDetail(id); }} renderRow={(row) => <div className="cash-receipt-row">
          <span>{formatTime(row.createdAtUtc)}</span>
          <strong>{posSaleStateLabel(row.state || 'sale', t)}</strong>
          <em>{posSaleLineSummary(row, t)}</em>
          <b><Money minorUnits={row.total.minorUnits} currencyCode={currencyCode} /></b>
        </div>} />}
        inspector={detailState.status === 'loading' ? <DeferredSkeleton><ReceiptInspectorSkeleton /></DeferredSkeleton>
          : detailState.status === 'failed' ? <LoadFailureState title={t('op.cash.receipts.detailFailed')} failure={detailState.error ?? projectOperatorError(undefined, t)} onRetry={() => void (detailState.saleId ? loadSaleDetail(detailState.saleId) : loadReceiptDetail(detailState.receiptId))} />
          : detailState.status === 'ready' && (saleDetail !== null || receiptDetail !== null) ? <div className="cash-receipt-inspector">
            {/* Чек закрытия сессии продажи не имеет вовсе — тогда шапку и итог берём из самого
                чека, иначе найденный по номеру чек открывался бы в пустоту. */}
            <div className="cash-receipt-inspector-head"><span>{t('op.pos.receipts.detailsTitle')}</span><strong>{posSaleStateLabel(saleDetail?.state || receiptDetail?.receiptType || 'sale', t)}</strong><b><Money minorUnits={(saleDetail?.total ?? receiptDetail?.total)?.minorUnits ?? 0} currencyCode={currencyCode} /></b></div>
            {saleDetail !== null && <>
            <section><h3>{t('op.cash.receipts.lines')}</h3>{(saleDetail.lines ?? []).map((line) => <div className="cash-receipt-line" key={`${line.productId}-${line.quantity}`}><span>{line.productName || t('op.pos.receipts.productFallback')}<small>{line.quantity} × <Money minorUnits={line.unitPrice.minorUnits} currencyCode={currencyCode} /></small></span><strong><Money minorUnits={line.lineTotal.minorUnits} currencyCode={currencyCode} /></strong></div>)}</section>
            {/* Секция читала поле `payments`, которого в PosSaleDto не было, и потому всегда оставалась
                пустой. Поле добавлено в контракт: строки оплат в базе лежали всё это время. */}
            <section><h3>{t('op.cash.receipts.payments')}</h3>{(saleDetail.payments ?? []).map((payment, index) => <div className="cash-receipt-payment" key={`${payment.paymentMethod}-${index}`}><span>{paymentMethodLabel(payment.paymentMethod)}</span><strong><Money minorUnits={payment.amount.minorUnits} currencyCode={currencyCode} /></strong></div>)}</section>
            </>}
            {receiptDetail !== null ? <div className="pos-receipt-detail"><span>{t('op.pos.receipts.platformReceipt')}</span><strong>№ {receiptDetail.receiptNumber || t('op.pos.receipts.receiptFallback')}</strong><p>{posReceiptTypeLabel(receiptDetail.receiptType || 'sale', t)}</p></div> : null}
            <div className="pos-receipt-actions">
              {canRefund ? <button type="button" disabled={feedback.state === 'pending'} onClick={() => { setFeedback(emptyFeedback); setCriticalAction('refund'); }}><Undo2 size={13} aria-hidden="true" />{t('op.pos.quick.refundLabel')}</button> : null}
              {canVoid ? <button type="button" disabled={feedback.state === 'pending'} onClick={() => { setFeedback(emptyFeedback); setCriticalAction('void'); }}><Ban size={13} aria-hidden="true" />{t('op.pos.quick.voidLabel')}</button> : null}
              {saleDetail !== null && <button type="button" disabled={feedback.state === 'pending'} onClick={printReceipt}><ReceiptText size={13} aria-hidden="true" />{t('op.pos.receipts.printBtn')}</button>}
              {saleDetail !== null && <button type="button" disabled={feedback.state === 'pending'} onClick={exportReceipt}><ArrowRightLeft size={13} aria-hidden="true" />{t('op.pos.receipts.exportBtn')}</button>}
            </div>
          </div> : <p className="cash-inspector-empty">{t('op.cash.receipts.selectHint')}</p>}
      />

      {criticalAction === 'void' && (
        <CriticalActionConfirmation
          title={t('op.pos.quick.voidConfirmTitle')}
          detail={t('op.pos.quick.voidConfirmDetail', { amount: formatMoney(selectedTotal, currencyCode) })}
          impact={t('op.pos.quick.voidConfirmImpact')}
          confirmLabel={t('op.pos.quick.voidConfirmBtn')}
          disabled={feedback.state === 'pending'}
          onCancel={() => setCriticalAction(null)}
          onConfirm={() => void voidSelected()}
        >
          <label className="critical-confirmation-field">
            <span>{t('op.pos.quick.voidReasonLabel')}</span>
            <input value={voidReason} disabled={feedback.state === 'pending'} onChange={(event) => setVoidReason(event.currentTarget.value)} />
          </label>
        </CriticalActionConfirmation>
      )}

      {criticalAction === 'refund' && (
        <CriticalActionConfirmation
          title={t('op.pos.quick.refundConfirmTitle')}
          detail={t('op.pos.quick.refundConfirmDetail', { amount: formatMoney(selectedTotal, currencyCode) })}
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

// Карточка чека, пока она грузится: шапка с итогом, состав, оплата и кнопки — в тех же блоках.
// Заголовки разделов от ответа не зависят и стоят настоящим текстом.
function ReceiptInspectorSkeleton() {
  const { t } = useI18n();
  return (
    <div className="cash-receipt-inspector" data-skeleton="receipt" aria-hidden="true">
      <div className="cash-receipt-inspector-head"><span>{t('op.pos.receipts.detailsTitle')}</span><strong><SkeletonLine width="5em" /></strong><b><SkeletonLine width="4em" /></b></div>
      <section>
        <h3>{t('op.cash.receipts.lines')}</h3>
        {[0, 1].map((line) => (
          <div key={line} className="cash-receipt-line"><span><SkeletonLine width="10em" /><small><SkeletonLine width="6em" /></small></span><strong><SkeletonLine width="4em" /></strong></div>
        ))}
      </section>
      <section>
        <h3>{t('op.cash.receipts.payments')}</h3>
        <div className="cash-receipt-payment"><span><SkeletonLine width="7em" /></span><strong><SkeletonLine width="4em" /></strong></div>
      </section>
      {/* Кнопки чека стоят в строку; какие из них будут, решают права и состояние продажи. */}
      <div className="pos-receipt-actions"><SkeletonControl width="16rem" size="sm" /></div>
    </div>
  );
}
