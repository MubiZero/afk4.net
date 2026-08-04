import { useEffect, useState, type FormEvent } from 'react';
import QRCode from 'qrcode';
import { AlertTriangle, ArrowRight, Check, Copy, Loader2 } from 'lucide-react';
import type { PlatformAdminSession } from '../auth/tokenStore';
import { describeApiError } from '../api/describeApiError';
import { useI18n } from '../i18n/I18nProvider';

export interface TwoFactorSetupClient {
  beginSetup(challengeToken: string): Promise<{ secret: string; otpAuthUri: string }>;
  completeSetup(challengeToken: string, code: string): Promise<{ session: PlatformAdminSession; recoveryCodes: string[] }>;
}

type Step =
  | { kind: 'loading' }
  | { kind: 'ready'; secret: string; otpAuthUri: string; qrDataUrl: string | null }
  | { kind: 'error'; message: string }
  | { kind: 'recoveryCodes'; codes: string[]; session: PlatformAdminSession };

// First-time 2FA setup, shown after a correct password when the account has no authenticator
// configured yet (SignIn.tsx routes here on `twoFactorConfigured === false`). Two stages: scan +
// confirm a code, then a one-time, unmissable display of recovery codes — there is no endpoint to
// fetch them again, so `onComplete` only fires once the admin explicitly acknowledges saving them.
export function TwoFactorSetup({ client, challengeToken, onComplete, onCancel }: {
  client: TwoFactorSetupClient;
  challengeToken: string;
  onComplete: (session: PlatformAdminSession) => void;
  onCancel: () => void;
}) {
  const { t } = useI18n();
  const [step, setStep] = useState<Step>({ kind: 'loading' });
  const [code, setCode] = useState('');
  const [confirmError, setConfirmError] = useState<string | null>(null);
  const [isConfirming, setConfirming] = useState(false);
  const [acknowledged, setAcknowledged] = useState(false);
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    let cancelled = false;
    client.beginSetup(challengeToken)
      .then(async ({ secret, otpAuthUri }) => {
        let qrDataUrl: string | null = null;
        try {
          qrDataUrl = await QRCode.toDataURL(otpAuthUri);
        } catch {
          qrDataUrl = null;
        }
        if (!cancelled) setStep({ kind: 'ready', secret, otpAuthUri, qrDataUrl });
      })
      .catch((cause: unknown) => {
        if (!cancelled) {
          setStep({
            kind: 'error',
            message: describeApiError(cause, t, { 409: 'auth.twoFactor.setup.error.alreadyConfigured' })
          });
        }
      });
    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [challengeToken]);

  async function handleConfirm(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setConfirming(true);
    setConfirmError(null);
    try {
      const { session, recoveryCodes } = await client.completeSetup(challengeToken, code.trim());
      setStep({ kind: 'recoveryCodes', codes: recoveryCodes, session });
    } catch (cause) {
      setConfirmError(describeApiError(cause, t, {
        401: 'auth.twoFactor.error.invalidCode',
        429: 'auth.twoFactor.error.lockedOut'
      }));
    } finally {
      setConfirming(false);
    }
  }

  function copyCodes(codes: string[]) {
    void navigator.clipboard?.writeText(codes.join('\n'));
    setCopied(true);
    setTimeout(() => setCopied(false), 1500);
  }

  if (step.kind === 'loading') {
    return (
      <section className="auth-panel">
        <p>{t('auth.twoFactor.setup.loading')}</p>
      </section>
    );
  }

  if (step.kind === 'error') {
    return (
      <section className="auth-panel">
        <div className="auth-error" role="alert">
          <AlertTriangle size={16} aria-hidden="true" />
          <span>{step.message}</span>
        </div>
        <button type="button" className="auth-password-toggle" onClick={onCancel}>
          {t('auth.twoFactor.action.back')}
        </button>
      </section>
    );
  }

  if (step.kind === 'recoveryCodes') {
    return (
      <section className="auth-panel">
        <header className="auth-panel-head">
          <h1>{t('auth.twoFactor.recovery.title')}</h1>
        </header>

        <div className="auth-error" role="alert">
          <AlertTriangle size={16} aria-hidden="true" />
          <span>{t('auth.twoFactor.recovery.warning')}</span>
        </div>

        <ul className="pc-mono" aria-label={t('auth.twoFactor.recovery.title')}>
          {step.codes.map(recoveryCode => <li key={recoveryCode}>{recoveryCode}</li>)}
        </ul>

        <button type="button" className="auth-password-toggle" onClick={() => copyCodes(step.codes)}>
          {copied ? <Check size={16} aria-hidden="true" /> : <Copy size={16} aria-hidden="true" />}
          {copied ? t('auth.twoFactor.recovery.copied') : t('auth.twoFactor.recovery.copy')}
        </button>

        <label className="auth-field">
          <span>
            <input
              type="checkbox"
              checked={acknowledged}
              onChange={event => setAcknowledged(event.target.checked)}
            />{' '}
            {t('auth.twoFactor.recovery.ack')}
          </span>
        </label>

        <button
          type="button"
          className="auth-primary"
          disabled={!acknowledged}
          onClick={() => onComplete(step.session)}
        >
          {t('auth.twoFactor.recovery.continue')}
          <ArrowRight aria-hidden="true" />
        </button>
      </section>
    );
  }

  return (
    <section className="auth-panel">
      <header className="auth-panel-head">
        <h1>{t('auth.twoFactor.setup.title')}</h1>
        <p>{t('auth.twoFactor.setup.subtitle')}</p>
      </header>

      {step.qrDataUrl !== null ? (
        <img src={step.qrDataUrl} alt={t('auth.twoFactor.setup.title')} width={200} height={200} />
      ) : null}

      <div className="auth-field">
        <span className="auth-field-label">{t('auth.twoFactor.setup.secretLabel')}</span>
        <code className="pc-mono">{step.secret}</code>
        <p>{t('auth.twoFactor.setup.secretHint')}</p>
      </div>

      <form className="auth-form" onSubmit={event => void handleConfirm(event)} noValidate>
        {confirmError !== null ? (
          <div className="auth-error" role="alert">
            <AlertTriangle size={16} aria-hidden="true" />
            <span>{confirmError}</span>
          </div>
        ) : null}

        <div className="auth-field">
          <label className="auth-field-label" htmlFor="two-factor-setup-code">{t('auth.twoFactor.setup.codeLabel')}</label>
          <input
            id="two-factor-setup-code"
            name="code"
            type="text"
            inputMode="numeric"
            autoComplete="one-time-code"
            value={code}
            onChange={event => setCode(event.target.value)}
            disabled={isConfirming}
            required
          />
        </div>

        <button type="submit" className="auth-primary" disabled={isConfirming || code.trim().length === 0}>
          {isConfirming ? <Loader2 className="auth-spinner" aria-hidden="true" /> : null}
          {t('auth.twoFactor.setup.confirm')}
          {isConfirming ? null : <ArrowRight aria-hidden="true" />}
        </button>

        <button type="button" className="auth-password-toggle" onClick={onCancel} disabled={isConfirming}>
          {t('auth.twoFactor.action.back')}
        </button>
      </form>
    </section>
  );
}
