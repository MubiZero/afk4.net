import { useEffect, useState } from 'react';
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
import { FeedbackNotice, StateFlag } from './operatorPrimitives';

type ReviewSegment = 'queue' | 'audit';

function reviewActionTypeLabel(actionType: string): string {
  switch (actionType) {
    case 'refund':
      return 'Возврат';
    case 'manual_correction':
      return 'Коррекция';
    case 'debt_write_off':
      return 'Списание долга';
    default:
      return actionType;
  }
}

function reviewExpiryBadge(expiresAtUtc: string, nowMs: number): { label: string; tone: 'overdue' | 'soon' } | null {
  const expiresMs = Date.parse(expiresAtUtc);
  if (!Number.isFinite(expiresMs)) {
    return null;
  }
  const remainingMs = expiresMs - nowMs;
  if (remainingMs <= 0) {
    return { label: 'Просрочена', tone: 'overdue' };
  }
  if (remainingMs <= 2 * 60 * 60 * 1000) {
    return { label: 'Истекает скоро', tone: 'soon' };
  }
  return null;
}

export function ReviewWorkspace({ currencyCode, backend }: { currencyCode: string; backend: OperatorBackendContext | null }) {
  const [activeSegment, setActiveSegment] = useState<ReviewSegment>('queue');
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const [loadStatus, setLoadStatus] = useState<LoadStatus>('fixture');
  const [loadError, setLoadError] = useState<string | null>(null);

  const [requests, setRequests] = useState<MoneyActionRequestDto[]>([]);
  const [staffNames, setStaffNames] = useState<Record<string, string>>({});
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
    return resolved || auditActorLabel(record, backend);
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
        names[readString(user, 'staffUserId').toLowerCase()] = operatorDisplayNameLabel(readString(user, 'displayName'));
      }
      setStaffNames(names);
      setLoadStatus('backend');
    } catch (error) {
      const detail = projectOperatorError(error).detail;
      setLoadStatus('failed');
      setLoadError(detail);
      setFeedback({ label: 'Проверка', state: 'failed', detail });
    }
  };

  useEffect(() => {
    void loadQueue();
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken]);

  const approveRequest = async (request: MoneyActionRequestDto) => {
    setFeedback({ label: 'Одобрение', state: 'pending' });
    try {
      const nextBackend = requireBackend(backend);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      await apiClients.moneyActions.approve(nextBackend.branchId, request.moneyActionRequestId, { decisionReason: null });
      setFeedback({ label: 'Одобрение', state: 'confirmed' });
      await loadQueue();
    } catch (error) {
      setFeedback({ label: 'Одобрение', state: 'failed', detail: projectOperatorError(error).detail });
    }
  };

  const confirmReject = async (request: MoneyActionRequestDto) => {
    const reason = decisionReason.trim();
    if (reason.length === 0) {
      setFeedback({ label: 'Отклонение', state: 'failed', detail: 'Укажите причину отклонения.' });
      return;
    }
    setFeedback({ label: 'Отклонение', state: 'pending' });
    try {
      const nextBackend = requireBackend(backend);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      await apiClients.moneyActions.reject(nextBackend.branchId, request.moneyActionRequestId, { decisionReason: reason });
      setRejectingId('');
      setDecisionReason('');
      setFeedback({ label: 'Отклонение', state: 'confirmed' });
      await loadQueue();
    } catch (error) {
      setFeedback({ label: 'Отклонение', state: 'failed', detail: projectOperatorError(error).detail });
    }
  };

  const applyAuditSearch = async () => {
    setFeedback({ label: 'Журнал', state: 'pending' });
    try {
      const nextBackend = requireBackend(backend);
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
      setFeedback({ label: 'Журнал', state: 'confirmed' });
    } catch (error) {
      setFeedback({ label: 'Журнал', state: 'failed', detail: projectOperatorError(error).detail });
    }
  };

  const auditRecords = readArray<Record<string, unknown>>(auditResult, 'records');
  const staffOptions = Object.entries(staffNames);

  return (
    <main className="workspace-screen review-screen">
      <section className="screen-head review-head">
        <div>
          <span>Проверка</span>
          <h1>Проверка · заявки и журнал</h1>
        </div>
        <div className="screen-actions">
          <span className={`map-load-state ${loadStatus === 'backend' ? 'ready' : loadStatus}`}>{workspaceLoadStatusLabel(loadStatus, 'Заявки загружены')}</span>
        </div>
      </section>

      <section className="state-strip review-state-strip" aria-label="Сводка проверки">
        <StateFlag label="Заявки" value={String(requests.length)} critical={requests.length > 0} />
        <StateFlag label="Источник" value={workspaceLoadStatusLabel(loadStatus, 'Платформа')} critical={loadStatus !== 'backend'} />
      </section>

      <div className="review-segments" role="tablist">
        <button type="button" role="tab" aria-selected={activeSegment === 'queue'} className={activeSegment === 'queue' ? 'active' : undefined} onClick={() => setActiveSegment('queue')}>Заявки на одобрение</button>
        <button type="button" role="tab" aria-selected={activeSegment === 'audit'} className={activeSegment === 'audit' ? 'active' : undefined} onClick={() => setActiveSegment('audit')}>Журнал операций</button>
      </div>

      {activeSegment === 'queue' && (
        <section className="review-panel review-queue-panel">
          {requests.length === 0 ? (
            <p className="review-empty">{loadError ?? 'Нет заявок на одобрение'}</p>
          ) : (
            requests.map((request) => {
              const expiryBadge = reviewExpiryBadge(request.expiresAtUtc, Date.now());
              return (
              <article key={request.moneyActionRequestId} className="review-request-row">
                <div className="review-request-head">
                  <strong>{reviewActionTypeLabel(request.actionType)}</strong>
                  <b>{formatMinorUnits(request.amountMinorUnits, request.currencyCode || currencyCode)}</b>
                </div>
                <em>{request.reason}</em>
                <div className="review-request-meta">
                  <span>Запросил: {resolveStaffName(request.requestedByStaffUserId)}</span>
                  <span>Создано: {formatTime(request.createdAtUtc)}</span>
                  <span>Истекает: {formatTime(request.expiresAtUtc)}</span>
                  {expiryBadge && <span className={`review-expiry-badge ${expiryBadge.tone}`}>{expiryBadge.label}</span>}
                </div>
                {rejectingId === request.moneyActionRequestId ? (
                  <div className="review-reject-form">
                    <label>
                      Причина отклонения
                      <input value={decisionReason} onChange={(event) => setDecisionReason(event.currentTarget.value)} placeholder="почему отклонено" />
                    </label>
                    <div className="review-request-actions">
                      <button type="button" onClick={() => void confirmReject(request)}>Подтвердить отклонение</button>
                      <button type="button" onClick={() => { setRejectingId(''); setDecisionReason(''); }}>Отмена</button>
                    </div>
                  </div>
                ) : (
                  <div className="review-request-actions">
                    <button type="button" onClick={() => void approveRequest(request)}>Одобрить</button>
                    <button type="button" onClick={() => { setRejectingId(request.moneyActionRequestId); setDecisionReason(''); }}>Отклонить</button>
                  </div>
                )}
              </article>
              );
            })
          )}
          <FeedbackNotice feedback={feedback} />
        </section>
      )}

      {activeSegment === 'audit' && (
        <section className="review-panel review-audit-panel">
          <div className="review-audit-filters">
            <label>
              Сотрудник
              <select value={auditActor} onChange={(event) => setAuditActor(event.currentTarget.value)}>
                <option value="">Все сотрудники</option>
                {staffOptions.map(([staffUserId, name]) => (
                  <option key={staffUserId} value={staffUserId}>{name}</option>
                ))}
              </select>
            </label>
            <label>Сумма от<input inputMode="numeric" value={auditMinAmount} onChange={(event) => setAuditMinAmount(event.currentTarget.value)} placeholder="мин" /></label>
            <label>Сумма до<input inputMode="numeric" value={auditMaxAmount} onChange={(event) => setAuditMaxAmount(event.currentTarget.value)} placeholder="макс" /></label>
            <button type="button" onClick={() => void applyAuditSearch()}>Применить фильтр</button>
          </div>
          <div className="review-audit-list">
            {auditRecords.length === 0 ? (
              <p className="review-empty">Записей нет — задайте фильтр</p>
            ) : (
              auditRecords.map((record) => (
                <article key={readString(record, 'auditRecordId')} className="review-audit-row">
                  <span>{formatTime(readString(record, 'createdAtUtc'))}</span>
                  <strong>{reviewAuditActorLabel(record)}</strong>
                  <em>{auditActionLabel(readString(record, 'action'))}</em>
                  <b>{readNumber(record, 'amountMinorUnits', 0) > 0 ? formatMinorUnits(readNumber(record, 'amountMinorUnits', 0), currencyCode) : '—'}</b>
                </article>
              ))
            )}
          </div>
          <FeedbackNotice feedback={feedback} />
        </section>
      )}
    </main>
  );
}
