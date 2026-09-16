import { useEffect, useMemo, useState } from 'react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { createAuthenticatedOperatorClients, formatTime } from '../operatorHelpers';
import { projectOperatorError } from '../apiErrors';
import { Money } from '../operatorPrimitives';
import type { Feedback, OperatorBackendContext } from '../operatorTypes';
import type { OperatorTopUpIntentDto } from '../operatorApiClients';

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
  const [loadError, setLoadError] = useState<string | null>(null);
  const [confirmingId, setConfirmingId] = useState<string | null>(null);
  const [reloadNonce, setReloadNonce] = useState(0);

  useEffect(() => {
    if (client === null) return undefined;
    let active = true;
    setLoading(true);
    setLoadError(null);
    client.listPending(branchId)
      .then((rows) => { if (active) setRequests(rows); })
      .catch((error) => { if (active) setLoadError(projectOperatorError(error, t).detail); })
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

  if (loading) return <p className="workspace-loading">{t('op.cash.topups.loading')}</p>;
  if (loadError !== null) {
    return (
      <section className="cash-ledger-failure">
        <p className="ui-alert ui-alert--spaced" role="alert">{loadError}</p>
        <button type="button" onClick={() => setReloadNonce((value) => value + 1)}>{t('op.cash.topups.retry')}</button>
      </section>
    );
  }

  if (requests.length === 0) {
    return (
      <section className="cash-topups">
        <p className="cash-shift-empty-note">{t('op.cash.topups.empty')}</p>
        <p className="cash-topups-hint">{t('op.cash.topups.emptyHint')}</p>
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
