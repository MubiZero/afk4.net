import { useState, type FormEvent } from 'react';
import { AlertTriangle, ArrowRight, Loader2 } from 'lucide-react';
import { describeTwoFactorError } from '../api/describeApiError';
import { useI18n } from '../i18n/I18nProvider';
import { useChallengeExpiry } from './useChallengeExpiry';

// The second step of sign-in for an admin who already has 2FA configured (see SignIn.tsx). The
// same field accepts a TOTP code from an authenticator app or a one-time recovery code — the
// server's /2fa/verify route tells them apart, this form doesn't need to.
//
// `expiresAtUtc`/`onExpired` are optional so the component keeps working standalone (and in the
// brief's own test) without a real challenge window; SignIn.tsx always supplies both in practice.
export function TwoFactorChallenge({ onSubmit, onCancel, expiresAtUtc, onExpired }: {
  onSubmit: (code: string) => Promise<void>;
  onCancel: () => void;
  expiresAtUtc?: string;
  onExpired?: () => void;
}) {
  const { t, formatDate } = useI18n();
  const [code, setCode] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setSubmitting] = useState(false);

  useChallengeExpiry(
    expiresAtUtc ?? '',
    () => onExpired?.(),
    expiresAtUtc !== undefined && onExpired !== undefined
  );

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitting(true);
    setError(null);
    try {
      await onSubmit(code.trim());
    } catch (cause) {
      // A dead challenge (server says so via two_factor_challenge_expired) gets the exact same
      // treatment as the client-side countdown running out: back to the password screen, not a
      // dead-end "invalid code" on a form that can never succeed again. If nothing is listening
      // for that (no onExpired wired up), say so inline instead of doing nothing.
      const outcome = describeTwoFactorError(cause, t, formatDate);
      if (outcome.kind === 'expired') {
        if (onExpired !== undefined) onExpired();
        else setError(t('auth.twoFactor.error.expired'));
      } else {
        setError(outcome.text);
      }
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
          <div className="ui-alert" role="alert">
            <AlertTriangle size={16} aria-hidden="true" />
            <span>{error}</span>
          </div>
        ) : null}

        <div className="ui-field">
          <label className="ui-field-label" htmlFor="two-factor-code">{t('auth.twoFactor.field.code')}</label>
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

        <button type="submit" className="ui-btn ui-btn--primary ui-btn--block" disabled={isSubmitting || code.trim().length === 0}>
          {isSubmitting ? <Loader2 className="ui-spinner" aria-hidden="true" /> : null}
          {isSubmitting ? t('auth.twoFactor.action.confirming') : t('auth.twoFactor.action.confirm')}
          {isSubmitting ? null : <ArrowRight aria-hidden="true" />}
        </button>

        <button type="button" className="ui-password-toggle" onClick={onCancel} disabled={isSubmitting}>
          {t('auth.twoFactor.action.back')}
        </button>
      </form>
    </section>
  );
}
