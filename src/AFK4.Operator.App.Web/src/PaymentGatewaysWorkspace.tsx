import { useCallback, useEffect, useMemo, useState } from 'react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { PlatformApiClient } from './platformApi';
import {
  createOperatorApiClients,
  type OwnerPaymentGatewayDto,
  type OwnerGatewayStatusResponse,
  type TelegramStartRequest
} from './operatorApiClients';
import { projectOperatorError } from './apiErrors';

type AttachPhase = 'idle' | 'code_required' | 'password_required' | 'attached';

// Mirrors the inline OperatorBackendContext the other workspaces receive (config + session + branch).
// Keep structurally compatible with App.tsx's OperatorBackendContext — cannot import it directly
// because App.tsx imports this component (would be a circular dependency).
export interface PaymentGatewaysBackend {
  config: { platformBaseUrl: string };
  session: { accessToken: string };
  branchId: string;
}

interface Props {
  backend: PaymentGatewaysBackend;
}

export function PaymentGatewaysWorkspace({ backend }: Props) {
  const { t, formatDate } = useI18n();
  const [gateways, setGateways] = useState<OwnerPaymentGatewayDto[]>([]);
  const [statuses, setStatuses] = useState<Record<string, OwnerGatewayStatusResponse>>({});
  const [loadError, setLoadError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  // provision form
  const [cardNumber, setCardNumber] = useState('');
  const [scopeBranch, setScopeBranch] = useState(false);

  // telegram attach state
  const [attachId, setAttachId] = useState<string | null>(null);
  const [attachPhase, setAttachPhase] = useState<AttachPhase>('idle');
  const [loginAttemptId, setLoginAttemptId] = useState('');
  const [phone, setPhone] = useState('');
  const [code, setCode] = useState('');
  const [password, setPassword] = useState('');
  const [apiId, setApiId] = useState('');
  const [apiHash, setApiHash] = useState('');
  const [savedApiId, setSavedApiId] = useState<number | null>(null);
  const [hasSavedCreds, setHasSavedCreds] = useState(false);
  const [changeCreds, setChangeCreds] = useState(false);

  const clients = useMemo(
    () => createOperatorApiClients(new PlatformApiClient({
      baseUrl: backend.config.platformBaseUrl,
      getAccessToken: () => backend.session.accessToken
    })).paymentGateways,
    [backend.config.platformBaseUrl, backend.session.accessToken]
  );

  const reload = useCallback(async () => {
    try {
      const result = await clients.list();
      setGateways(result.gateways);
      setLoadError(null);
    } catch (error) {
      setLoadError(projectOperatorError(error).detail);
    }
  }, [clients]);

  useEffect(() => {
    let disposed = false;
    void (async () => {
      try {
        const result = await clients.list();
        if (!disposed) { setGateways(result.gateways); setLoadError(null); }
      } catch (error) {
        if (!disposed) setLoadError(projectOperatorError(error).detail);
      }
    })();
    return () => { disposed = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Live Telegram status per gateway. Fetched concurrently whenever the list
  // (re)loads — e.g. after a fresh attach — so each row can show its health.
  // A failed status fetch for one card is silently omitted, never breaking the list.
  useEffect(() => {
    let disposed = false;
    const ids = gateways.map((g) => g.branchPaymentGatewayId);
    if (ids.length === 0) { setStatuses({}); return; }
    void (async () => {
      const results = await Promise.allSettled(ids.map((id) => clients.status(id)));
      if (disposed) return;
      const next: Record<string, OwnerGatewayStatusResponse> = {};
      results.forEach((result, index) => {
        if (result.status === 'fulfilled') next[ids[index]] = result.value;
      });
      setStatuses(next);
    })();
    return () => { disposed = true; };
  }, [gateways, clients]);

  const provision = async () => {
    setBusy(true);
    try {
      await clients.provision({
        branchId: scopeBranch ? backend.branchId : null,
        cardNumber: cardNumber.trim()
      });
      setCardNumber('');
      await reload();
    } catch (error) {
      setLoadError(projectOperatorError(error).detail);
    } finally {
      setBusy(false);
    }
  };

  const disable = async (id: string) => {
    if (!window.confirm(t('payments_cards.disable_confirm'))) return;
    setBusy(true);
    try {
      await clients.disable(id);
      await reload();
    } catch (error) {
      setLoadError(projectOperatorError(error).detail);
    } finally {
      setBusy(false);
    }
  };

  const lookupCreds = async () => {
    const trimmed = phone.trim();
    if (!trimmed) return;
    try {
      const res = await clients.telegramCredentials(trimmed);
      setHasSavedCreds(res.hasCredentials);
      setSavedApiId(res.apiId);
      setChangeCreds(false);
    } catch { /* best-effort */ }
  };

  const startAttach = async (id: string) => {
    setBusy(true);
    try {
      const sendCreds = !hasSavedCreds || changeCreds;
      const request: TelegramStartRequest = sendCreds
        ? { phone: phone.trim(), apiId: Number(apiId), apiHash: apiHash.trim() }
        : { phone: phone.trim() };
      const result = await clients.telegramStart(id, request);
      setAttachId(id);
      if (result.state === 'attached') {
        setAttachPhase('attached');
        await reload();
      } else {
        setLoginAttemptId(result.loginAttemptId ?? '');
        setAttachPhase(result.state as AttachPhase);
      }
    } catch (error) {
      setLoadError(projectOperatorError(error).detail);
    } finally {
      setBusy(false);
    }
  };

  const verifyCode = async () => {
    if (!attachId) return;
    setBusy(true);
    try {
      const result = await clients.telegramVerifyCode(attachId, { loginAttemptId, code: code.trim() });
      if (result.state === 'password_required') {
        setAttachPhase('password_required');
      } else if (result.state === 'attached') {
        setAttachPhase('attached');
        await reload();
      } else {
        setLoadError(t('payments_cards.error.generic'));
      }
    } catch (error) {
      setLoadError(projectOperatorError(error).detail);
    } finally {
      setBusy(false);
    }
  };

  const verifyPassword = async () => {
    if (!attachId) return;
    setBusy(true);
    try {
      const result = await clients.telegramVerifyPassword(attachId, { loginAttemptId, password });
      if (result.state === 'attached') {
        setAttachPhase('attached');
        await reload();
      } else {
        setLoadError(t('payments_cards.error.generic'));
      }
    } catch (error) {
      setLoadError(projectOperatorError(error).detail);
    } finally {
      setBusy(false);
    }
  };

  return (
    <main className="workspace-screen payment-cards-screen">
      <section className="screen-head">
        <h1>{t('payments_cards.title')}</h1>
        <p>{t('payments_cards.subtitle')}</p>
      </section>

      {loadError && <p className="payment-cards-error" role="alert">{loadError}</p>}

      <section className="payment-cards-provision">
        <label>{t('payments_cards.card_number')}
          <input value={cardNumber} onChange={(e) => setCardNumber(e.currentTarget.value)} inputMode="numeric" />
        </label>
        <label>
          <input type="checkbox" checked={scopeBranch} onChange={(e) => setScopeBranch(e.currentTarget.checked)} />
          {scopeBranch ? t('payments_cards.scope.branch') : t('payments_cards.scope.org')}
        </label>
        <button type="button" disabled={busy || cardNumber.trim().length < 12} onClick={() => void provision()}>
          {t('payments_cards.provision')}
        </button>
      </section>

      <section className="payment-cards-list">
        {gateways.length === 0 && <p className="payment-cards-empty">{t('payments_cards.empty')}</p>}
        {gateways.map((g) => (
          <article key={g.branchPaymentGatewayId} className="payment-card-row" data-status={g.status}>
            <span className="payment-card-pan">•••• {g.cardLast4}</span>
            <span className="payment-card-scope">
              {g.branchId ? t('payments_cards.scope.branch') : t('payments_cards.scope.org')}
            </span>
            <span className="payment-card-status">{t(`payments_cards.status.${g.status}` as MessageKey)}</span>

            {g.status !== 'disabled' && (
              <button
                type="button"
                className="payment-card-disable"
                disabled={busy}
                onClick={() => void disable(g.branchPaymentGatewayId)}
              >
                {t('payments_cards.disable')}
              </button>
            )}

            {statuses[g.branchPaymentGatewayId] && (() => {
              const live = statuses[g.branchPaymentGatewayId];
              const known = live.sessionHealth === 'online'
                || live.sessionHealth === 'offline'
                || live.sessionHealth === 'configured';
              return (
                <div className="payment-card-session" data-health={live.sessionHealth}>
                  <span className="payment-card-session-badge">
                    {known
                      ? t(`payments_cards.session.${live.sessionHealth}` as MessageKey)
                      : live.sessionHealth}
                  </span>
                  {live.lastMessageAt && (
                    <span className="payment-card-session-last">
                      {t('payments_cards.session.last_message')}: {formatDate(live.lastMessageAt)}
                    </span>
                  )}
                </div>
              );
            })()}

            {g.status === 'pending_telegram' && (
              <div className="payment-card-attach">
                <h3>{t('payments_cards.telegram.title')}</h3>
                {(attachId !== g.branchPaymentGatewayId || attachPhase === 'idle') && (
                  <>
                    <label>{t('payments_cards.telegram.phone')}
                      <input
                        aria-label="phone"
                        value={phone}
                        onChange={(e) => setPhone(e.currentTarget.value)}
                        onBlur={() => void lookupCreds()}
                      />
                    </label>
                    {hasSavedCreds && !changeCreds ? (
                      <p className="payment-card-saved-creds">
                        {t('payments_cards.telegram.saved_creds' as MessageKey, { apiId: savedApiId ?? '' })}
                        <button type="button" onClick={() => setChangeCreds(true)}>
                          {t('payments_cards.telegram.change_creds' as MessageKey)}
                        </button>
                      </p>
                    ) : (
                      <>
                        <label>{t('payments_cards.telegram.api_id' as MessageKey)}
                          <input
                            aria-label="api_id"
                            inputMode="numeric"
                            value={apiId}
                            onChange={(e) => setApiId(e.currentTarget.value)}
                          />
                        </label>
                        <label>{t('payments_cards.telegram.api_hash' as MessageKey)}
                          <input
                            aria-label="api_hash"
                            type="password"
                            value={apiHash}
                            onChange={(e) => setApiHash(e.currentTarget.value)}
                          />
                        </label>
                        <p className="payment-card-api-help">{t('payments_cards.telegram.api_help' as MessageKey)}</p>
                      </>
                    )}
                    <button type="button" disabled={busy} onClick={() => void startAttach(g.branchPaymentGatewayId)}>
                      {t('payments_cards.telegram.start')}
                    </button>
                  </>
                )}
                {attachId === g.branchPaymentGatewayId && attachPhase === 'code_required' && (
                  <>
                    <label>{t('payments_cards.telegram.code')}
                      <input value={code} onChange={(e) => setCode(e.currentTarget.value)} inputMode="numeric" />
                    </label>
                    <button type="button" disabled={busy} onClick={() => void verifyCode()}>
                      {t('payments_cards.telegram.code_submit')}
                    </button>
                  </>
                )}
                {attachId === g.branchPaymentGatewayId && attachPhase === 'password_required' && (
                  <>
                    <label>{t('payments_cards.telegram.password')}
                      <input type="password" value={password} onChange={(e) => setPassword(e.currentTarget.value)} />
                    </label>
                    <button type="button" disabled={busy} onClick={() => void verifyPassword()}>
                      {t('payments_cards.telegram.password_submit')}
                    </button>
                  </>
                )}
                {attachId === g.branchPaymentGatewayId && attachPhase === 'attached' && (
                  <p className="payment-card-attached">{t('payments_cards.telegram.attached')}</p>
                )}
              </div>
            )}
          </article>
        ))}
      </section>
    </main>
  );
}
