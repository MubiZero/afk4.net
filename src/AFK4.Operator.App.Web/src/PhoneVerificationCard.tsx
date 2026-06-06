import { useEffect, useMemo, useState } from 'react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { PlatformApiClient, PlatformApiError } from './platformApi';
import { createOperatorApiClients } from './operatorApiClients';

// Structurally compatible with App.tsx's backend context (config + session);
// declared locally to avoid a circular import (App.tsx imports this file).
export interface PhoneVerificationBackend {
  config: { platformBaseUrl: string };
  session: { accessToken: string };
}

type Phase = 'loading' | 'idle' | 'code' | 'verified' | 'load_error';

const ERROR_KEYS: Record<string, MessageKey> = {
  invalid_phone: 'account.phone.err.invalid_phone',
  cooldown_active: 'account.phone.err.cooldown',
  rate_limited: 'account.phone.err.rate_limited',
  sms_unavailable: 'account.phone.err.sms_unavailable',
  invalid_code: 'account.phone.err.invalid_code',
  code_expired: 'account.phone.err.expired',
  no_active_code: 'account.phone.err.expired',
  too_many_attempts: 'account.phone.err.too_many',
  phone_already_in_use: 'account.phone.err.in_use'
};

// PlatformApiError.body is the raw response text, so parse it for the error code
// and the invalid_code "remainingAttempts" detail (t() has no interpolation).
function describeError(err: unknown, t: (k: MessageKey) => string): string {
  if (err instanceof PlatformApiError) {
    try {
      const body = JSON.parse(err.body) as { error?: string; remainingAttempts?: number };
      if (body.error === 'invalid_code' && typeof body.remainingAttempts === 'number') {
        return `${t('account.phone.invalidCodeAttempts')} ${body.remainingAttempts}`;
      }
      if (typeof body.error === 'string' && body.error in ERROR_KEYS) {
        return t(ERROR_KEYS[body.error]);
      }
    } catch {
      // non-JSON body → fall through to generic
    }
  }
  return t('account.phone.err.generic');
}

export function PhoneVerificationCard({ backend }: { backend: PhoneVerificationBackend }) {
  const { t } = useI18n();
  const api = useMemo(
    () => createOperatorApiClients(new PlatformApiClient({
      baseUrl: backend.config.platformBaseUrl,
      getAccessToken: () => backend.session.accessToken
    })).account,
    [backend.config.platformBaseUrl, backend.session.accessToken]
  );

  const [phase, setPhase] = useState<Phase>('loading');
  const [currentPhone, setCurrentPhone] = useState<string | null>(null);
  const [phone, setPhone] = useState('');
  const [code, setCode] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    let disposed = false;
    setPhase('loading');
    setError(null);
    void (async () => {
      try {
        const status = await api.getMyPhone();
        if (disposed) return;
        if (status.phoneVerifiedAtUtc !== null) {
          setCurrentPhone(status.phone);
          setPhase('verified');
        } else {
          setPhase('idle');
        }
      } catch {
        if (!disposed) setPhase('load_error');
      }
    })();
    return () => { disposed = true; };
  }, [api, reloadKey]);

  const sendCode = async () => {
    setBusy(true);
    setError(null);
    try {
      await api.startPhoneVerification({ phone: phone.trim() });
      setCode('');
      setPhase('code');
    } catch (err) {
      setError(describeError(err, t));
    } finally {
      setBusy(false);
    }
  };

  const confirm = async () => {
    setBusy(true);
    setError(null);
    try {
      const result = await api.confirmPhoneVerification({ code: code.trim() });
      setCurrentPhone(result.phone);
      setPhase('verified');
    } catch (err) {
      setError(describeError(err, t));
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="account-phone">
      <h3>{t('account.phone.title')}</h3>
      {error !== null && <p className="account-phone-error" role="alert">{error}</p>}

      {phase === 'loading' && <p className="account-phone-hint">{t('account.phone.loading')}</p>}

      {phase === 'load_error' && (
        <div className="account-phone-load-error">
          <p className="account-phone-error">{t('account.phone.err.generic')}</p>
          <button type="button" onClick={() => setReloadKey((k) => k + 1)}>{t('state.retry')}</button>
        </div>
      )}

      {phase === 'idle' && (
        <div className="account-phone-form">
          <label>{t('account.phone.field')}
            <input
              inputMode="tel"
              placeholder={t('account.phone.placeholder')}
              value={phone}
              onChange={(e) => setPhone(e.currentTarget.value)}
              disabled={busy}
            />
          </label>
          <button type="button" disabled={busy || phone.trim().length === 0} onClick={() => void sendCode()}>
            {t('account.phone.sendCode')}
          </button>
        </div>
      )}

      {phase === 'code' && (
        <div className="account-phone-form">
          <label>{t('account.phone.codeField')}
            <input
              inputMode="numeric"
              value={code}
              onChange={(e) => setCode(e.currentTarget.value)}
              disabled={busy}
            />
          </label>
          <div className="account-phone-actions">
            <button type="button" disabled={busy || code.trim().length === 0} onClick={() => void confirm()}>
              {t('account.phone.confirm')}
            </button>
            <button type="button" className="secondary" disabled={busy} onClick={() => void sendCode()}>
              {t('account.phone.resend')}
            </button>
          </div>
        </div>
      )}

      {phase === 'verified' && (
        <div className="account-phone-verified">
          <strong>{currentPhone}</strong>
          <span className="account-phone-badge">{t('account.phone.verifiedBadge')}</span>
          <button type="button" className="secondary" disabled={busy} onClick={() => { setPhone(''); setError(null); setPhase('idle'); }}>
            {t('account.phone.change')}
          </button>
        </div>
      )}
    </section>
  );
}
