import { useCallback, useEffect, useMemo, useState } from 'react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { PlatformApiClient } from './platformApi';
import { createOperatorApiClients, type OwnerPaymentGatewayDto } from './operatorApiClients';
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
  const { t } = useI18n();
  const [gateways, setGateways] = useState<OwnerPaymentGatewayDto[]>([]);
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

  const startAttach = async (id: string) => {
    setBusy(true);
    try {
      const result = await clients.telegramStart(id, { phone: phone.trim() });
      setAttachId(id);
      setLoginAttemptId(result.loginAttemptId);
      setAttachPhase('code_required');
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

            {g.status === 'pending_telegram' && (
              <div className="payment-card-attach">
                <h3>{t('payments_cards.telegram.title')}</h3>
                {(attachId !== g.branchPaymentGatewayId || attachPhase === 'idle') && (
                  <>
                    <label>{t('payments_cards.telegram.phone')}
                      <input value={phone} onChange={(e) => setPhone(e.currentTarget.value)} />
                    </label>
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
