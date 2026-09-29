import { useEffect, useState } from 'react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { projectOperatorError, type OperatorErrorProjection } from './apiErrors';
import type { AuditRecordDto, AuditSearchResultDto, MoneyActionRequestDto } from './operatorApiClients';
import type { Feedback, OperatorBackendContext } from './operatorTypes';
import {
  auditActionLabel,
  auditActorLabel,
  createAuthenticatedOperatorClients,
  emptyFeedback,
  formatTime,
  operatorDisplayNameLabel,
  readArray,
  readString,
  requireBackend
} from './operatorHelpers';
import { useFeedbackToasts } from './useFeedbackToasts';
import { CashRegisterRows, CashTerminalSplit } from './cash/CashTerminalFrame';
import { EmptyState, Money, PartialLoadFailure } from './operatorPrimitives';
import { Button, Tabs } from '@afk4/ui/react';

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

// Вкладка «Согласования» кассы: очередь заявок, история решений и журнал денежных действий.
export function ReviewWorkspace({ currencyCode, backend }: { currencyCode: string; backend: OperatorBackendContext | null }) {
  const { t } = useI18n();
  const [activeSegment, setActiveSegment] = useState<ReviewSegment>('queue');
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  useFeedbackToasts(feedback);
  const [loadError, setLoadError] = useState<string | null>(null);

  const [requests, setRequests] = useState<MoneyActionRequestDto[]>([]);
  const [staffNames, setStaffNames] = useState<Record<string, string>>({});
  const [staffLoadError, setStaffLoadError] = useState<OperatorErrorProjection | null>(null);
  const [selectedRequestId, setSelectedRequestId] = useState('');
  const [rejectingId, setRejectingId] = useState('');
  const [decisionReason, setDecisionReason] = useState('');

  const [auditResult, setAuditResult] = useState<AuditSearchResultDto | null>(null);
  const [auditActor, setAuditActor] = useState('');
  const [auditMinAmount, setAuditMinAmount] = useState('');
  const [auditMaxAmount, setAuditMaxAmount] = useState('');

  const resolveStaffName = (staffUserId: string) =>
    staffNames[staffUserId.toLowerCase()] ?? `${staffUserId.slice(0, 8)}…`;

  const reviewAuditActorLabel = (record: AuditRecordDto) => {
    const actorStaffUserId = record.actorStaffUserId ?? '';
    const resolved = actorStaffUserId ? staffNames[actorStaffUserId.toLowerCase()] : '';
    return resolved || auditActorLabel(record, backend, t);
  };

  const loadQueue = async (nextBackend = backend, shared?: ReturnType<typeof createAuthenticatedOperatorClients>) => {
    setLoadError(null);
    if (nextBackend === null) return;
    try {
      const apiClients = shared ?? createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const feed = await apiClients.moneyActions.listPending(nextBackend.branchId);
      setRequests(readArray<MoneyActionRequestDto>(feed, 'requests'));
    } catch (error) {
      const detail = projectOperatorError(error, t).detail;
      setLoadError(detail);
      setFeedback({ label: t('op.review.feedbackLoad'), state: 'failed', detail });
    }
  };

  // Имена сотрудников — подпись к заявке, а не сама заявка, и грузятся отдельно от очереди. Их
  // отказ (например, у проверяющего нет права видеть список сотрудников) раньше стирал всю
  // очередь; теперь заявки остаются, вместо имени видно начало номера, а повтор спрашивает
  // только имена.
  const loadStaffNames = async (nextBackend = backend, shared?: ReturnType<typeof createAuthenticatedOperatorClients>) => {
    if (nextBackend === null) {
      setStaffLoadError(null);
      return;
    }
    setStaffLoadError(null);
    try {
      const apiClients = shared ?? createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const staff = await apiClients.settings.getStaffUsers(nextBackend.branchId);
      const names: Record<string, string> = {};
      for (const user of staff) {
        names[readString(user, 'staffUserId').toLowerCase()] = operatorDisplayNameLabel(readString(user, 'displayName'), t);
      }
      setStaffNames(names);
    } catch (error) {
      setStaffLoadError(projectOperatorError(error, t));
    }
  };

  useEffect(() => {
    // Один набор клиентов на оба запроса: у каждого набора свой продлеватель сессии.
    const shared = backend === null ? undefined : createAuthenticatedOperatorClients(backend.config, backend.session);
    void loadQueue(backend, shared);
    void loadStaffNames(backend, shared);
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken]);

  // Решение по заявке — одно нажатие. Второй клик раньше уходил вторым запросом: сервер
  // схлопывает повтор в тот же итог, но оператор получал 409 и противоречивое «не удалось»
  // поверх уже принятого решения.
  const deciding = feedback?.state === 'pending';

  const approveRequest = async (request: MoneyActionRequestDto) => {
    if (deciding) return;
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
    if (deciding) return;
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

  const auditFiltered = auditActor !== '' || auditMinAmount.trim() !== '' || auditMaxAmount.trim() !== '';
  const resetAuditFilter = () => {
    setAuditActor('');
    setAuditMinAmount('');
    setAuditMaxAmount('');
    void applyAuditSearch({ actor: '', min: '', max: '' });
  };

  // Фильтр можно передать явно: сброс перечитывает журнал без отбора сразу, не дожидаясь, пока
  // очищенные поля доедут до состояния.
  const applyAuditSearch = async (filter = { actor: auditActor, min: auditMinAmount, max: auditMaxAmount }) => {
    setFeedback({ label: t('op.review.feedbackAudit'), state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const parsedMin = filter.min.trim() === '' ? null : Number(filter.min);
      const parsedMax = filter.max.trim() === '' ? null : Number(filter.max);
      const maxAmount = parsedMax !== null && Number.isFinite(parsedMax) ? parsedMax : null;
      // Default to amount-bearing (money / high-risk) records when no amount bound is set:
      // the audit query drops null-amount rows once a bound is present (§5.5).
      const minAmount = parsedMin !== null && Number.isFinite(parsedMin)
        ? parsedMin
        : (maxAmount === null ? 0 : null);
      const result = await apiClients.audit.search({
        branchId: nextBackend.branchId,
        actorStaffUserId: filter.actor.trim() === '' ? null : filter.actor.trim(),
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

  const auditRecords = readArray<AuditRecordDto>(auditResult, 'records');
  const decisionRecords = auditRecords.filter((record) => /approv|reject|money.action/i.test(record.action));
  const staffOptions = Object.entries(staffNames);
  const selectedRequest = requests.find((request) => request.moneyActionRequestId === selectedRequestId) ?? null;
  // Число заявок — на вкладке очереди, а «истекает скоро» и «просрочена» — меткой на самой заявке.
  // Три карточки «Заявки 1 · Истекают 1 · Просрочены 0» над одной заявкой повторяли её же трижды.
  const selectSegment = (segment: ReviewSegment) => {
    setActiveSegment(segment);
    if (segment === 'history' || (segment === 'audit' && auditResult === null)) void applyAuditSearch();
  };

  return (
    <section className="review-embed">

      {staffLoadError !== null && (
        <PartialLoadFailure text={t('op.review.staffNamesFailed', { reason: staffLoadError.detail })} failure={staffLoadError} onRetry={() => void loadStaffNames()} />
      )}

      <Tabs
        className="review-segments"
        label={t('op.cash.journal.segReview')}
        value={activeSegment}
        onChange={selectSegment}
        items={[
          { value: 'queue', label: t('op.review.tabQueue'), count: requests.length > 0 ? requests.length : undefined },
          { value: 'history', label: t('op.review.tabHistory') },
          { value: 'audit', label: t('op.review.tabAudit') }
        ]}
      />

      {activeSegment === 'queue' && (
        <CashTerminalSplit
          inspectorLabel={t('op.cash.inspector.aria')}
          inspectorOpen={selectedRequest !== null}
          closeLabel={t('common.close')}
          onCloseInspector={() => { setSelectedRequestId(''); setRejectingId(''); setDecisionReason(''); }}
          register={requests.length === 0 ? (loadError !== null
            ? <p className="review-empty">{loadError}</p>
            : <EmptyState inline className="review-empty" title={t('op.review.emptyQueue')} next={{ kind: 'calm', hint: t('op.review.emptyQueueHint') }} />) : <CashRegisterRows rows={requests} selectedId={selectedRequestId} getId={(request) => request.moneyActionRequestId} onSelect={setSelectedRequestId} ariaLabel={t('op.review.queueAria')} renderRow={(request) => {
            const expiryBadge = reviewExpiryBadge(request.expiresAtUtc, Date.now(), t);
            return <div className="review-approval-row"><span>{reviewActionTypeLabel(request.actionType, t)}</span><strong><Money minorUnits={request.amountMinorUnits} currencyCode={request.currencyCode || currencyCode} /></strong><em>{request.reason}</em><small>{resolveStaffName(request.requestedByStaffUserId)}</small>{expiryBadge ? <b className={expiryBadge.tone}>{expiryBadge.label}</b> : null}</div>;
          }} />}
          inspector={selectedRequest ? <div className="review-approval-inspector">
            <p>{reviewActionTypeLabel(selectedRequest.actionType, t)}</p>
            <h2>{selectedRequest.reason}</h2>
            <strong><Money minorUnits={selectedRequest.amountMinorUnits} currencyCode={selectedRequest.currencyCode || currencyCode} /></strong>
            <dl><div><dt>{t('op.review.requestedByLabel')}</dt><dd>{resolveStaffName(selectedRequest.requestedByStaffUserId)}</dd></div><div><dt>{t('op.review.createdLabel')}</dt><dd>{formatTime(selectedRequest.createdAtUtc)}</dd></div><div><dt>{t('op.review.expiresLabel')}</dt><dd>{formatTime(selectedRequest.expiresAtUtc)} {reviewExpiryBadge(selectedRequest.expiresAtUtc, Date.now(), t)?.label ?? ''}</dd></div></dl>
            {rejectingId === selectedRequest.moneyActionRequestId ? <div className="review-reject-form"><label>{t('op.review.rejectReasonLabel')}<input value={decisionReason} onChange={(event) => setDecisionReason(event.currentTarget.value)} placeholder={t('op.review.rejectReasonPlaceholder')} /></label><div className="review-request-actions"><Button variant="primary" disabled={deciding} onClick={() => void confirmReject(selectedRequest)}>{t('op.review.confirmRejectBtn')}</Button><Button variant="ghost" onClick={() => { setRejectingId(''); setDecisionReason(''); }}>{t('common.cancel')}</Button></div></div> : <div className="review-request-actions"><Button variant="primary" disabled={deciding} onClick={() => void approveRequest(selectedRequest)}>{t('op.review.approveBtn')}</Button><Button disabled={deciding} onClick={() => { setRejectingId(selectedRequest.moneyActionRequestId); setDecisionReason(''); }}>{t('devices.action.reject')}</Button></div>}
          </div> : <p className="cash-inspector-empty">{t('op.review.selectHint')}</p>}
        />
      )}

      {activeSegment === 'history' && <section className="review-panel review-history-panel">{decisionRecords.length === 0 ? <EmptyState inline className="review-empty" title={t('op.review.emptyHistory')} next={{ kind: 'calm', hint: t('op.review.emptyHistoryHint') }} /> : <div className="review-audit-list">{decisionRecords.map((record) => <article key={record.auditRecordId} className="review-audit-row"><span>{formatTime(record.createdAtUtc)}</span><strong>{reviewAuditActorLabel(record)}</strong><em>{auditActionLabel(record.action, t)}</em><b><Money minorUnits={record.amountMinorUnits || null} currencyCode={currencyCode} /></b></article>)}</div>}</section>}

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
            <Button onClick={() => void applyAuditSearch()}>{t('op.review.applyFilter')}</Button>
          </div>
          <div className="review-audit-list">
            {auditRecords.length === 0 ? (
              <EmptyState
                inline
                className="review-empty"
                title={t('op.review.emptyAudit')}
                next={auditFiltered
                  ? { kind: 'action', label: t('op.empty.resetFilter'), onClick: resetAuditFilter }
                  : { kind: 'calm', hint: t('op.review.emptyAuditHint') }}
              />
            ) : (
              auditRecords.map((record) => (
                <article key={record.auditRecordId} className="review-audit-row">
                  <span>{formatTime(record.createdAtUtc)}</span>
                  <strong>{reviewAuditActorLabel(record)}</strong>
                  <em>{auditActionLabel(record.action, t)}</em>
                  <b><Money minorUnits={(record.amountMinorUnits ?? 0) > 0 ? record.amountMinorUnits ?? 0 : null} currencyCode={currencyCode} /></b>
                </article>
              ))
            )}
          </div>
        </section>
      )}
    </section>
  );
}
