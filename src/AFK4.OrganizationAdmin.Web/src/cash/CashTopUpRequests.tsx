import { useEffect, useMemo, useState } from 'react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { createAuthenticatedOperatorClients, formatTime } from '../operatorHelpers';
import { projectOperatorError, type OperatorErrorProjection } from '../apiErrors';
import { EmptyState, Money, PartialLoadFailure } from '../operatorPrimitives';
import type { Feedback, OperatorBackendContext } from '../operatorTypes';
import type { OperatorTopUpIntentDto } from '../operatorApiClients';
import { DeferredSkeleton, SkeletonControl, SkeletonLine } from '../LoadingSkeleton';

interface TopUpQueueClient {
  listPending(branchId: string): Promise<OperatorTopUpIntentDto[]>;
  confirm(intentId: string): Promise<unknown>;
}

const METHOD_LABELS: Record<string, MessageKey> = {
  counter: 'op.cash.topups.method.counter',
  dc: 'op.cash.topups.method.dc',
  eskhata: 'op.cash.topups.method.eskhata'
};

// Онлайн-оплату подтверждает банк своим колбэком. Кнопка «принять оплату» рядом с такой заявкой
// зачислила бы деньги, которых клуб не получил, — поэтому её там нет вовсе.
const BANK_CONFIRMED_METHODS = new Set(['eskhata']);

/**
 * Очередь заявок на пополнение: игрок просит из приложения, кассир видит здесь и принимает деньги.
 * До этого экрана заявку было видно только тому, кто знает её идентификатор.
 */
export function CashTopUpRequests({
  backend,
  branchId,
  currencyCode,
  onFeedback,
  client: injectedClient
}: {
  backend: OperatorBackendContext | null;
  branchId: string;
  currencyCode: string;
  onFeedback?: (feedback: Feedback) => void;
  client?: TopUpQueueClient;
}) {
  const { t } = useI18n();
  const client = useMemo(
    () => injectedClient ?? (backend ? createAuthenticatedOperatorClients(backend.config, backend.session).dcTopUps : null),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [backend?.config, backend?.session, injectedClient]
  );

  const [requests, setRequests] = useState<OperatorTopUpIntentDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<OperatorErrorProjection | null>(null);
  const [confirmingId, setConfirmingId] = useState<string | null>(null);
  const [reloadNonce, setReloadNonce] = useState(0);

  useEffect(() => {
    if (client === null) return undefined;
    let active = true;
    setLoading(true);
    setLoadError(null);
    client.listPending(branchId)
      .then((rows) => { if (active) setRequests(rows); })
      .catch((error) => { if (active) setLoadError(projectOperatorError(error, t)); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [client, branchId, reloadNonce]);

  const confirm = async (intent: OperatorTopUpIntentDto) => {
    if (client === null) return;
    setConfirmingId(intent.paymentIntentId);
    try {
      await client.confirm(intent.paymentIntentId);
      onFeedback?.({
        label: t('op.cash.topups.feedbackLabel'),
        state: 'confirmed',
        detail: `${t('op.cash.topups.confirmed')} · ${intent.displayName}`
      });
      setReloadNonce((value) => value + 1);
    } catch (error) {
      onFeedback?.({
        label: t('op.cash.topups.feedbackLabel'),
        state: 'failed',
        detail: projectOperatorError(error, t).detail
      });
    } finally {
      setConfirmingId(null);
    }
  };

  if (loading) return <DeferredSkeleton><TopUpQueueSkeleton /></DeferredSkeleton>;
  if (loadError !== null) {
    return (
      <section className="cash-ledger-failure">
        <PartialLoadFailure text={loadError.detail} failure={loadError} onRetry={() => setReloadNonce((value) => value + 1)} />
      </section>
    );
  }

  if (requests.length === 0) {
    return (
      <section className="cash-topups">
        <EmptyState title={t('op.cash.topups.empty')} next={{ kind: 'calm', hint: t('op.cash.topups.emptyHint') }} />
      </section>
    );
  }

  return (
    <section className="cash-topups" aria-label={t('op.cash.topups.listAria')}>
      {requests.map((intent) => {
        const bankConfirmed = BANK_CONFIRMED_METHODS.has(intent.method);
        const methodKey = METHOD_LABELS[intent.method];
        return (
          <article key={intent.paymentIntentId} className="ui-ledger-row cash-topup-row">
            <span className="ui-ledger-time">{formatTime(intent.createdAtUtc)}</span>
            <div className="ui-ledger-body">
              <span className="ui-ledger-title">{intent.displayName}</span>
              <span className="ui-ledger-detail">
                {intent.seatName ?? t('op.cash.topups.seatUnknown')}
                {' · '}
                {methodKey ? t(methodKey) : intent.method}
              </span>
            </div>
            <span className="ui-ledger-aside">
              <Money minorUnits={intent.amountMinorUnits} currencyCode={intent.currencyCode || currencyCode} />
            </span>
            {bankConfirmed ? (
              <span className="cash-topup-waiting" title={t('op.cash.topups.waitingBankHint')}>
                {t('op.cash.topups.waitingBank')}
              </span>
            ) : (
              <button
                type="button"
                className="ui-btn ui-btn--primary ui-btn--sm cash-topup-confirm"
                disabled={confirmingId !== null}
                onClick={() => void confirm(intent)}
              >
                {t('op.cash.topups.confirm')}
              </button>
            )}
          </article>
        );
      })}
    </section>
  );
}

// Очередь заявок — теми же строками ленты: время, кто и откуда, сумма и кнопка приёма.
function TopUpQueueSkeleton() {
  return (
    <section className="cash-topups" data-skeleton="list" aria-hidden="true">
      {Array.from({ length: 3 }, (_, row) => (
        <article key={row} className="ui-ledger-row cash-topup-row">
          <span className="ui-ledger-time"><SkeletonLine width="3em" /></span>
          <div className="ui-ledger-body"><span className="ui-ledger-title"><SkeletonLine width="8em" /></span><span className="ui-ledger-detail"><SkeletonLine width="11em" /></span></div>
          <span className="ui-ledger-aside"><SkeletonLine width="4em" /></span>
          <SkeletonControl width="8rem" size="sm" />
        </article>
      ))}
    </section>
  );
}
