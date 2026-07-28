import { useEffect, useState } from 'react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { projectOperatorError } from './apiErrors';
import type { AuditSearchResultDto, MoneyActionRequestDto } from './operatorApiClients';
import type { Feedback, LoadStatus, OperatorBackendContext } from './operatorTypes';
import {
  auditActionLabel,
  auditActorLabel,
  createAuthenticatedOperatorClients,
  emptyFeedback,
  formatMinorUnits,
  formatTime,
  operatorDisplayNameLabel,
  readArray,
  readNumber,
  readString,
  requireBackend,
  workspaceLoadStatusLabel
} from './operatorHelpers';
import { useFeedbackToasts } from './useFeedbackToasts';
import { CashMetricStrip, CashRegisterRows, CashTerminalSplit } from './cash/CashTerminalFrame';

type ReviewSegment = 'queue' | 'history' | 'audit';

function reviewActionTypeLabel(actionType: string, t: (key: MessageKey) => string): string {
  switch (actionType) {
    case 'refund':
      return t('money.refund');
    case 'manual_correction':
      return t('money.correction');
    case 'debt_write_off':
      return t('op.review.actionDebtWriteOff');
    default:
      return actionType;
  }
}

function reviewExpiryBadge(expiresAtUtc: string, nowMs: number, t: (key: MessageKey) => string): { label: string; tone: 'overdue' | 'soon' } | null {
  const expiresMs = Date.parse(expiresAtUtc);
  if (!Number.isFinite(expiresMs)) {
    return null;
  }
  const remainingMs = expiresMs - nowMs;
  if (remainingMs <= 0) {
    return { label: t('op.review.expiryOverdue'), tone: 'overdue' };
  }
  if (remainingMs <= 2 * 60 * 60 * 1000) {
    return { label: t('op.review.expirySoon'), tone: 'soon' };
  }
  return null;
}

export function ReviewWorkspace({ currencyCode, backend, embedded = false }: { currencyCode: string; backend: OperatorBackendContext | null; embedded?: boolean }) {
  const { t } = useI18n();
  const [activeSegment, setActiveSegment] = useState<ReviewSegment>('queue');
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  useFeedbackToasts(feedback);
  const [loadStatus, setLoadStatus] = useState<LoadStatus>('fixture');
  const [loadError, setLoadError] = useState<string | null>(null);

  const [requests, setRequests] = useState<MoneyActionRequestDto[]>([]);
  const [staffNames, setStaffNames] = useState<Record<string, string>>({});
  const [selectedRequestId, setSelectedRequestId] = useState('');
  const [rejectingId, setRejectingId] = useState('');
  const [decisionReason, setDecisionReason] = useState('');

  const [auditResult, setAuditResult] = useState<AuditSearchResultDto | null>(null);
  const [auditActor, setAuditActor] = useState('');
  const [auditMinAmount, setAuditMinAmount] = useState('');
  const [auditMaxAmount, setAuditMaxAmount] = useState('');

  const resolveStaffName = (staffUserId: string) =>
    staffNames[staffUserId.toLowerCase()] ?? `${staffUserId.slice(0, 8)}…`;

  const reviewAuditActorLabel = (record: Record<string, unknown>) => {
    const actorStaffUserId = readString(record, 'actorStaffUserId');
    const resolved = actorStaffUserId ? staffNames[actorStaffUserId.toLowerCase()] : '';
    return resolved || auditActorLabel(record, backend, t);
  };

  const loadQueue = async (nextBackend = backend) => {
    if (nextBackend === null) {
      setLoadStatus('fixture');
      setLoadError(null);
      return;
    }
    setLoadStatus('loading');
    setLoadError(null);
    try {
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const [feed, staff] = await Promise.all([
        apiClients.moneyActions.listPending(nextBackend.branchId),
        apiClients.settings.getStaffUsers(nextBackend.branchId)
      ]);
      setRequests(readArray<MoneyActionRequestDto>(feed, 'requests'));
      const names: Record<string, string> = {};
      for (const user of staff) {
        names[readString(user, 'staffUserId').toLowerCase()] = operatorDisplayNameLabel(readString(user, 'displayName'), t);
      }
      setStaffNames(names);
      setLoadStatus('backend');
    } catch (error) {
      const detail = projectOperatorError(error, t).detail;
      setLoadStatus('failed');
      setLoadError(detail);
      setFeedback({ label: t('op.review.feedbackLoad'), state: 'failed', detail });
    }
  };

  useEffect(() => {
    void loadQueue();
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken]);

  const approveRequest = async (request: MoneyActionRequestDto) => {
    setFeedback({ label: t('op.review.feedbackApprove'), state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      await apiClients.moneyActions.approve(nextBackend.branchId, request.moneyActionRequestId, { decisionReason: null });
      setFeedback({ label: t('op.review.feedbackApprove'), state: 'confirmed' });
      await loadQueue();
    } catch (error) {
      setFeedback({ label: t('op.review.feedbackApprove'), state: 'failed', detail: projectOperatorError(error, t).detail });
    }
  };

  const confirmReject = async (request: MoneyActionRequestDto) => {
    const reason = decisionReason.trim();
    if (reason.length === 0) {
      setFeedback({ label: t('op.review.feedbackReject'), state: 'failed', detail: t('op.review.rejectReasonRequired') });
      return;
    }
    setFeedback({ label: t('op.review.feedbackReject'), state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      await apiClients.moneyActions.reject(nextBackend.branchId, request.moneyActionRequestId, { decisionReason: reason });
      setRejectingId('');
      setDecisionReason('');
      setFeedback({ label: t('op.review.feedbackReject'), state: 'confirmed' });
      await loadQueue();
    } catch (error) {
      setFeedback({ label: t('op.review.feedbackReject'), state: 'failed', detail: projectOperatorError(error, t).detail });
    }
  };

  const applyAuditSearch = async () => {
    setFeedback({ label: t('op.review.feedbackAudit'), state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const parsedMin = auditMinAmount.trim() === '' ? null : Number(auditMinAmount);
      const parsedMax = auditMaxAmount.trim() === '' ? null : Number(auditMaxAmount);
      const maxAmount = parsedMax !== null && Number.isFinite(parsedMax) ? parsedMax : null;
      // Default to amount-bearing (money / high-risk) records when no amount bound is set:
      // the audit query drops null-amount rows once a bound is present (§5.5).
      const minAmount = parsedMin !== null && Number.isFinite(parsedMin)
        ? parsedMin
        : (maxAmount === null ? 0 : null);
      const result = await apiClients.audit.search({
        branchId: nextBackend.branchId,
        actorStaffUserId: auditActor.trim() === '' ? null : auditActor.trim(),
        minAmount,
        maxAmount,
        limit: 50
      });
      setAuditResult(result);
      setFeedback({ label: t('op.review.feedbackAudit'), state: 'confirmed' });
    } catch (error) {
      setFeedback({ label: t('op.review.feedbackAudit'), state: 'failed', detail: projectOperatorError(error, t).detail });
    }
  };

  const auditRecords = readArray<Record<string, unknown>>(auditResult, 'records');
  const decisionRecords = auditRecords.filter((record) => /approv|reject|money.action/i.test(readString(record, 'action')));
  const staffOptions = Object.entries(staffNames);
  const selectedRequest = requests.find((request) => request.moneyActionRequestId === selectedRequestId) ?? null;
  const expiringCount = requests.filter((request) => reviewExpiryBadge(request.expiresAtUtc, Date.now(), t)?.tone === 'soon').length;
  const overdueCount = requests.filter((request) => reviewExpiryBadge(request.expiresAtUtc, Date.now(), t)?.tone === 'overdue').length;

  const body = (
    <>
      {!embedded && (
        <section className="screen-head review-head">
          <div>
            <span>{t('op.review.title')}</span>
            <h1>{t('op.review.heading')}</h1>
          </div>
          <div className="screen-actions">
            <span className={`map-load-state ${loadStatus === 'backend' ? 'ready' : loadStatus}`}>{workspaceLoadStatusLabel(loadStatus, t('op.review.loadedLabel'), t)}</span>
          </div>
        </section>
      )}

      <CashMetricStrip ariaLabel={t('op.review.summaryLabel')} items={[
        { label: t('op.review.flagRequests'), value: requests.length, tone: requests.length ? 'attention' : 'default' },
        { label: t('op.review.expiringCount'), value: expiringCount, tone: expiringCount ? 'attention' : 'default' },
        { label: t('op.review.overdueCount'), value: overdueCount, tone: overdueCount ? 'danger' : 'default' }
      ]} />

      <div className="review-segments" role="tablist">
        <button type="button" role="tab" aria-selected={activeSegment === 'queue'} className={activeSegment === 'queue' ? 'active' : undefined} onClick={() => setActiveSegment('queue')}>{t('op.review.tabQueue')}</button>
        <button type="button" role="tab" aria-selected={activeSegment === 'history'} className={activeSegment === 'history' ? 'active' : undefined} onClick={() => { setActiveSegment('history'); void applyAuditSearch(); }}>{t('op.review.tabHistory')}</button>
        <button type="button" role="tab" aria-selected={activeSegment === 'audit'} className={activeSegment === 'audit' ? 'active' : undefined} onClick={() => { setActiveSegment('audit'); if (auditResult === null) void applyAuditSearch(); }}>{t('op.review.tabAudit')}</button>
      </div>

      {activeSegment === 'queue' && (
        <CashTerminalSplit
          inspectorOpen={selectedRequest !== null}
          closeLabel={t('common.close')}
          onCloseInspector={() => { setSelectedRequestId(''); setRejectingId(''); setDecisionReason(''); }}
          register={requests.length === 0 ? <p className="review-empty">{loadError ?? t('op.review.emptyQueue')}</p> : <CashRegisterRows rows={requests} selectedId={selectedRequestId} getId={(request) => request.moneyActionRequestId} onSelect={setSelectedRequestId} ariaLabel={t('op.review.queueAria')} renderRow={(request) => {
            const expiryBadge = reviewExpiryBadge(request.expiresAtUtc, Date.now(), t);
            return <div className="review-approval-row"><span>{reviewActionTypeLabel(request.actionType, t)}</span><strong>{formatMinorUnits(request.amountMinorUnits, request.currencyCode || currencyCode)}</strong><em>{request.reason}</em><small>{resolveStaffName(request.requestedByStaffUserId)}</small>{expiryBadge ? <b className={expiryBadge.tone}>{expiryBadge.label}</b> : null}</div>;
          }} />}
          inspector={selectedRequest ? <div className="review-approval-inspector">
            <p>{reviewActionTypeLabel(selectedRequest.actionType, t)}</p>
            <h2>{selectedRequest.reason}</h2>
            <strong>{formatMinorUnits(selectedRequest.amountMinorUnits, selectedRequest.currencyCode || currencyCode)}</strong>
            <dl><div><dt>{t('op.review.requestedByLabel')}</dt><dd>{resolveStaffName(selectedRequest.requestedByStaffUserId)}</dd></div><div><dt>{t('op.review.createdLabel')}</dt><dd>{formatTime(selectedRequest.createdAtUtc)}</dd></div><div><dt>{t('op.review.expiresLabel')}</dt><dd>{formatTime(selectedRequest.expiresAtUtc)} {reviewExpiryBadge(selectedRequest.expiresAtUtc, Date.now(), t)?.label ?? ''}</dd></div></dl>
            {rejectingId === selectedRequest.moneyActionRequestId ? <div className="review-reject-form"><label>{t('op.review.rejectReasonLabel')}<input value={decisionReason} onChange={(event) => setDecisionReason(event.currentTarget.value)} placeholder={t('op.review.rejectReasonPlaceholder')} /></label><div className="review-request-actions"><button type="button" onClick={() => void confirmReject(selectedRequest)}>{t('op.review.confirmRejectBtn')}</button><button type="button" onClick={() => { setRejectingId(''); setDecisionReason(''); }}>{t('common.cancel')}</button></div></div> : <div className="review-request-actions"><button type="button" onClick={() => void approveRequest(selectedRequest)}>{t('op.review.approveBtn')}</button><button type="button" onClick={() => { setRejectingId(selectedRequest.moneyActionRequestId); setDecisionReason(''); }}>{t('devices.action.reject')}</button></div>}
          </div> : <p className="cash-inspector-empty">{t('op.review.selectHint')}</p>}
        />
      )}

      {activeSegment === 'history' && <section className="review-panel review-history-panel">{decisionRecords.length === 0 ? <p className="review-empty">{t('op.review.emptyHistory')}</p> : <div className="review-audit-list">{decisionRecords.map((record) => <article key={readString(record, 'auditRecordId')} className="review-audit-row"><span>{formatTime(readString(record, 'createdAtUtc'))}</span><strong>{reviewAuditActorLabel(record)}</strong><em>{auditActionLabel(readString(record, 'action'), t)}</em><b>{readNumber(record, 'amountMinorUnits', 0) ? formatMinorUnits(readNumber(record, 'amountMinorUnits', 0), currencyCode) : '—'}</b></article>)}</div>}</section>}

      {activeSegment === 'audit' && (
        <section className="review-panel review-audit-panel">
          <div className="review-audit-filters">
            <label>
              {t('operators.col.name')}
              <select value={auditActor} onChange={(event) => setAuditActor(event.currentTarget.value)}>
                <option value="">{t('op.review.allStaff')}</option>
                {staffOptions.map(([staffUserId, name]) => (
                  <option key={staffUserId} value={staffUserId}>{name}</option>
                ))}
              </select>
            </label>
            <label>{t('op.review.amountFrom')}<input inputMode="numeric" value={auditMinAmount} onChange={(event) => setAuditMinAmount(event.currentTarget.value)} placeholder={t('op.review.amountMin')} /></label>
            <label>{t('op.review.amountTo')}<input inputMode="numeric" value={auditMaxAmount} onChange={(event) => setAuditMaxAmount(event.currentTarget.value)} placeholder={t('op.review.amountMax')} /></label>
            <button type="button" onClick={() => void applyAuditSearch()}>{t('op.review.applyFilter')}</button>
          </div>
          <div className="review-audit-list">
            {auditRecords.length === 0 ? (
              <p className="review-empty">{t('op.review.emptyAudit')}</p>
            ) : (
              auditRecords.map((record) => (
                <article key={readString(record, 'auditRecordId')} className="review-audit-row">
                  <span>{formatTime(readString(record, 'createdAtUtc'))}</span>
                  <strong>{reviewAuditActorLabel(record)}</strong>
                  <em>{auditActionLabel(readString(record, 'action'), t)}</em>
                  <b>{readNumber(record, 'amountMinorUnits', 0) > 0 ? formatMinorUnits(readNumber(record, 'amountMinorUnits', 0), currencyCode) : '—'}</b>
                </article>
              ))
            )}
          </div>
        </section>
      )}
    </>
  );
  return embedded ? <section className="review-embed">{body}</section> : <main className="workspace-screen review-screen">{body}</main>;
}
