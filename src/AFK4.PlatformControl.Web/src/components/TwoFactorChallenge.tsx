import { useState, type FormEvent } from 'react';
import { AlertTriangle, ArrowRight, Loader2 } from 'lucide-react';
import { describeApiError } from '../api/describeApiError';
import { useI18n } from '../i18n/I18nProvider';

// The second step of sign-in for an admin who already has 2FA configured (see SignIn.tsx). The
// same field accepts a TOTP code from an authenticator app or a one-time recovery code — the
// server's /2fa/verify route tells them apart, this form doesn't need to.
export function TwoFactorChallenge({ onSubmit, onCancel }: {
  onSubmit: (code: string) => Promise<void>;
  onCancel: () => void;
}) {
  const { t } = useI18n();
  const [code, setCode] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitting(true);
    setError(null);
    try {
      await onSubmit(code.trim());
    } catch (cause) {
      setError(describeApiError(cause, t, {
        401: 'auth.twoFactor.error.invalidCode',
        429: 'auth.twoFactor.error.lockedOut'
      }));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <section className="auth-panel">
      <header className="auth-panel-head">
        <h1>{t('auth.twoFactor.challenge.title')}</h1>
        <p>{t('auth.twoFactor.challenge.subtitle')}</p>
      </header>

      <form className="auth-form" onSubmit={event => void handleSubmit(event)} noValidate>
        {error !== null ? (
          <div className="auth-error" role="alert">
            <AlertTriangle size={16} aria-hidden="true" />
            <span>{error}</span>
          </div>
        ) : null}

        <div className="auth-field">
          <label className="auth-field-label" htmlFor="two-factor-code">{t('auth.twoFactor.field.code')}</label>
          <input
            id="two-factor-code"
            name="code"
            type="text"
            inputMode="numeric"
            autoComplete="one-time-code"
            autoFocus
            value={code}
            onChange={event => setCode(event.target.value)}
            disabled={isSubmitting}
            required
          />
        </div>

        <button type="submit" className="auth-primary" disabled={isSubmitting || code.trim().length === 0}>
          {isSubmitting ? <Loader2 className="auth-spinner" aria-hidden="true" /> : null}
          {isSubmitting ? t('auth.twoFactor.action.confirming') : t('auth.twoFactor.action.confirm')}
          {isSubmitting ? null : <ArrowRight aria-hidden="true" />}
        </button>

        <button type="button" className="auth-password-toggle" onClick={onCancel} disabled={isSubmitting}>
          {t('auth.twoFactor.action.back')}
        </button>
      </form>
    </section>
  );
}
