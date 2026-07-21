import { useCallback, useEffect, useMemo, useState } from 'react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { Plus } from 'lucide-react';
import { PlatformApiClient } from './platformApi';
import {
  createOperatorApiClients,
  type OwnerPaymentGatewayDto,
  type OwnerGatewayStatusResponse,
  type TelegramStartRequest
} from './operatorApiClients';
import { projectOperatorError } from './apiErrors';
import { CriticalActionConfirmation, EmptyState } from './operatorPrimitives';

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

// Приём по картам (dcgate): список карт спокойными строками с человеческим статусом, «Добавить
// карту» раскрывает форму, привязка Telegram идёт прямо в строке карты. Часть спокойного
// setup-экрана «Платежи и лояльность» — модель мгновенных действий, общего save-бара нет.
export function PaymentGatewaysWorkspace({ backend }: Props) {
  const { t, formatDate } = useI18n();
  const [gateways, setGateways] = useState<OwnerPaymentGatewayDto[]>([]);
  const [statuses, setStatuses] = useState<Record<string, OwnerGatewayStatusResponse>>({});
  const [loadError, setLoadError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [disableTarget, setDisableTarget] = useState<string | null>(null);

  // add-card form
  const [showAdd, setShowAdd] = useState(false);
  const [cardNumber, setCardNumber] = useState('');
  const [scopeBranch, setScopeBranch] = useState(false);

  // telegram attach state
  const [attachId, setAttachId] = useState<string | null>(null);
  const [attachPhase, setAttachPhase] = useState<AttachPhase>('idle');
  const [loginAttemptId, setLoginAttemptId] = useState('');
  const [phone, setPhone] = useState('');
  const [code, setCode] = useState('');
  const [password, setPassword] = useState('');

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
      setGateways(result.gateways ?? []);
      setLoadError(null);
    } catch (error) {
      setLoadError(projectOperatorError(error, t).detail);
    }
  }, [clients]);

  useEffect(() => {
    let disposed = false;
    void (async () => {
      try {
        const result = await clients.list();
        if (!disposed) { setGateways(result.gateways ?? []); setLoadError(null); }
      } catch (error) {
        if (!disposed) setLoadError(projectOperatorError(error, t).detail);
      } finally {
        if (!disposed) setLoading(false);
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
      setScopeBranch(false);
      setShowAdd(false);
      await reload();
    } catch (error) {
      setLoadError(projectOperatorError(error, t).detail);
    } finally {
      setBusy(false);
    }
  };

  const disable = async (id: string) => {
    setBusy(true);
    try {
      await clients.disable(id);
      await reload();
    } catch (error) {
      setLoadError(projectOperatorError(error, t).detail);
    } finally {
      setBusy(false);
    }
  };

  const startAttach = async (id: string) => {
    setBusy(true);
    try {
      const request: TelegramStartRequest = { phone: phone.trim() };
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
      setLoadError(projectOperatorError(error, t).detail);
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
      setLoadError(projectOperatorError(error, t).detail);
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
      setLoadError(projectOperatorError(error, t).detail);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div>
      {loadError && <p className="payset-inline-error" role="alert">{loadError}</p>}

      {loading ? (
        <div className="payset-loading" data-testid="gateways-skeleton" aria-hidden="true">
          <div className="management-skeleton-line" />
          <div className="management-skeleton-line" />
        </div>
      ) : gateways.length === 0 ? (
        <EmptyState title={t('payments_cards.empty')} description={t('op.payments.cards.emptyHint')} />
      ) : (
        <div className="payset-cards">
          {gateways.map((g) => {
            const live = statuses[g.branchPaymentGatewayId];
            const known = live && (live.sessionHealth === 'online' || live.sessionHealth === 'offline' || live.sessionHealth === 'configured');
            const statusTone = g.status === 'disabled' ? 'is-neutral' : g.status === 'pending_telegram' ? 'is-warning' : 'is-live';
            return (
              <article key={g.branchPaymentGatewayId} className="payset-card" data-status={g.status}>
                <span className="payset-card-pan">•••• {g.cardLast4}</span>
                <div className="payset-card-meta">
                  <div className="payset-card-tags">
                    <span className="ui-chip ui-chip--status ui-chip--xs is-neutral">
                      {g.branchId ? t('payments_cards.scope.branch') : t('payments_cards.scope.org')}
                    </span>
                    <span className={`ui-chip ui-chip--status ui-chip--xs ${statusTone}`}>
                      {t(`payments_cards.status.${g.status}` as MessageKey)}
                    </span>
                    {known && (
                      <span className={`ui-chip ui-chip--status ui-chip--xs ${live.sessionHealth === 'online' ? 'is-live' : 'is-neutral'}`}>
                        {t(`payments_cards.session.${live.sessionHealth}` as MessageKey)}
                      </span>
                    )}
                  </div>
                  {live?.lastMessageAt && (
                    <span className="payset-card-note">
                      {t('payments_cards.session.last_message')}: {formatDate(live.lastMessageAt)}
                    </span>
                  )}
                </div>

                {g.status !== 'disabled' && (
                  <button type="button" className="ui-btn ui-btn--sm ui-btn--danger"
                    disabled={busy} onClick={() => setDisableTarget(g.branchPaymentGatewayId)}>
                    {t('payments_cards.disable')}
                  </button>
                )}

                {g.status === 'pending_telegram' && (
                  <div className="payset-attach">
                    {(attachId !== g.branchPaymentGatewayId || attachPhase === 'idle') && (
                      <div className="payset-attach-row">
                        <label>{t('payments_cards.telegram.phone')}
                          <input aria-label="phone" value={phone} onChange={(e) => setPhone(e.currentTarget.value)} />
                        </label>
                        <button type="button" className="ui-btn ui-btn--primary"
                          disabled={busy || !phone.trim()} onClick={() => void startAttach(g.branchPaymentGatewayId)}>
                          {t('payments_cards.telegram.start')}
                        </button>
                      </div>
                    )}
                    {attachId === g.branchPaymentGatewayId && attachPhase === 'code_required' && (
                      <div className="payset-attach-row">
                        <label>{t('payments_cards.telegram.code')}
                          <input value={code} onChange={(e) => setCode(e.currentTarget.value)} inputMode="numeric" />
                        </label>
                        <button type="button" className="ui-btn ui-btn--primary" disabled={busy} onClick={() => void verifyCode()}>
                          {t('payments_cards.telegram.code_submit')}
                        </button>
                      </div>
                    )}
                    {attachId === g.branchPaymentGatewayId && attachPhase === 'password_required' && (
                      <div className="payset-attach-row">
                        <label>{t('payments_cards.telegram.password')}
                          <input type="password" value={password} onChange={(e) => setPassword(e.currentTarget.value)} />
                        </label>
                        <button type="button" className="ui-btn ui-btn--primary" disabled={busy} onClick={() => void verifyPassword()}>
                          {t('payments_cards.telegram.password_submit')}
                        </button>
                      </div>
                    )}
                    {attachId === g.branchPaymentGatewayId && attachPhase === 'attached' && (
                      <p className="payset-attach-done">{t('payments_cards.telegram.attached')}</p>
                    )}
                  </div>
                )}
              </article>
            );
          })}
        </div>
      )}

      {!loading && (
        showAdd ? (
          <div className="payset-reveal">
            <div className="mgmt-form">
              <div className="mgmt-form-grid">
                <label className="mgmt-form-wide">{t('payments_cards.card_number')}
                  <input value={cardNumber} onChange={(e) => setCardNumber(e.currentTarget.value)} inputMode="numeric" autoFocus />
                </label>
                <label className="mgmt-check">
                  <input type="checkbox" checked={scopeBranch} onChange={(e) => setScopeBranch(e.currentTarget.checked)} />
                  {scopeBranch ? t('payments_cards.scope.branch') : t('payments_cards.scope.org')}
                </label>
              </div>
              <div className="mgmt-form-actions">
                <button type="button" className="ui-btn" disabled={busy}
                  onClick={() => { setShowAdd(false); setCardNumber(''); setScopeBranch(false); }}>
                  {t('common.cancel')}
                </button>
                <button type="button" className="ui-btn ui-btn--primary"
                  disabled={busy || cardNumber.trim().length < 12} onClick={() => void provision()}>
                  {t('payments_cards.provision')}
                </button>
              </div>
            </div>
          </div>
        ) : (
          <button type="button" className="payset-add" style={{ marginTop: gateways.length ? 10 : 0 }}
            onClick={() => setShowAdd(true)}>
            <Plus size={18} strokeWidth={2} />
            {t('op.payments.cards.add')}
          </button>
        )
      )}

      {disableTarget && (
        <CriticalActionConfirmation
          title={t('op.payments.cards.disableTitle')}
          detail={`•••• ${gateways.find((g) => g.branchPaymentGatewayId === disableTarget)?.cardLast4 ?? ''}`}
          impact={t('op.payments.cards.disableImpact')}
          confirmLabel={t('payments_cards.disable')}
          disabled={busy}
          onCancel={() => setDisableTarget(null)}
          onConfirm={() => { const id = disableTarget; setDisableTarget(null); void disable(id); }}
        />
      )}
    </div>
  );
}
