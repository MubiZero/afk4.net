import { useState, type FormEvent } from 'react';
import { AlertTriangle, ArrowRight, Eye, EyeOff, Loader2 } from 'lucide-react';
import { PlatformApiClient } from '../api/platformApi';
import { describeApiError } from '../api/describeApiError';
import { useI18n } from '../i18n/I18nProvider';
import { BrandLogo } from './shell/BrandLogo';
import { TwoFactorChallenge } from './TwoFactorChallenge';
import { TwoFactorSetup } from './TwoFactorSetup';

export interface SignInProps {
  client: PlatformApiClient;
  onSignedIn: () => void;
}

// Password alone never opens the panel anymore (see PlatformTransport.signIn): it only earns a
// short-lived challenge token, and the next step depends on whether the account already has an
// authenticator configured. `expiresAtUtc` rides along so the 2FA screens can bounce back here on
// their own once the window runs out (see useChallengeExpiry) instead of surfacing a misleading
// "invalid code" for a challenge that simply died of old age.
type Step =
  | { kind: 'password' }
  | { kind: 'challenge'; challengeToken: string; expiresAtUtc: string }
  | { kind: 'setup'; challengeToken: string; expiresAtUtc: string };

// Тот же экран входа, что в Organization Admin и мастере установки: знак над заголовком,
// панель .auth-panel, показ пароля, ошибка полосой с красной кромкой. Раньше панель рисовала
// собственную голую карточку — и вход в платформу выглядел чужим приложением.
export function SignIn({ client, onSignedIn }: SignInProps) {
  const { t } = useI18n();
  const [userName, setUserName] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setSubmitting] = useState(false);
  const [step, setStep] = useState<Step>({ kind: 'password' });

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitting(true);
    setError(null);
    try {
      const outcome = await client.signIn(userName.trim(), password);
      setStep(outcome.twoFactorConfigured
        ? { kind: 'challenge', challengeToken: outcome.challengeToken, expiresAtUtc: outcome.expiresAtUtc }
        : { kind: 'setup', challengeToken: outcome.challengeToken, expiresAtUtc: outcome.expiresAtUtc });
    } catch (cause) {
      setError(describeApiError(cause, t, { 401: 'auth.error.invalid' }));
    } finally {
      setSubmitting(false);
    }
  }

  function backToPassword() {
    setPassword('');
    setStep({ kind: 'password' });
  }

  // The 2-minute challenge window ran out while the person was still on a 2FA screen. Their code
  // may well be correct — the window is just dead — so this is deliberately a different message
  // from "invalid code", with a clear instruction (sign in again) rather than a dead-end retry.
  function handleChallengeExpired() {
    setPassword('');
    setStep({ kind: 'password' });
    setError(t('auth.twoFactor.error.expired'));
  }

  if (step.kind === 'challenge') {
    return (
      <div className="pc-auth-shell">
        <header className="top-command auth-top-command">
          <div className="brand-block">
            <BrandLogo className="brand-logo" />
            <span>{t('shell.brand.section')}</span>
          </div>
        </header>
        <main className="auth-workspace">
          <TwoFactorChallenge
            onSubmit={async code => { await client.twoFactor.verify(step.challengeToken, code); onSignedIn(); }}
            onCancel={backToPassword}
            expiresAtUtc={step.expiresAtUtc}
            onExpired={handleChallengeExpired}
          />
        </main>
      </div>
    );
  }

  if (step.kind === 'setup') {
    return (
      <div className="pc-auth-shell">
        <header className="top-command auth-top-command">
          <div className="brand-block">
            <BrandLogo className="brand-logo" />
            <span>{t('shell.brand.section')}</span>
          </div>
        </header>
        <main className="auth-workspace">
          <TwoFactorSetup
            client={client.twoFactor}
            challengeToken={step.challengeToken}
            expiresAtUtc={step.expiresAtUtc}
            onExpired={handleChallengeExpired}
            onComplete={() => onSignedIn()}
            onCancel={backToPassword}
          />
        </main>
      </div>
    );
  }

  return (
    <div className="pc-auth-shell">
      <header className="top-command auth-top-command">
        <div className="brand-block">
          <BrandLogo className="brand-logo" />
          <span>{t('shell.brand.section')}</span>
        </div>
      </header>

      <main className="auth-workspace">
        <section className="auth-panel">
          <header className="auth-panel-head">
            <img className="auth-brand-mark" src="/favicon.svg" alt="" aria-hidden="true" />
            <h1>{t('auth.admin.title')}</h1>
            <p>{t('auth.admin.subtitle')}</p>
          </header>

          <form className="auth-form" onSubmit={handleSubmit} noValidate>
            {error !== null ? (
              <div className="auth-error" role="alert">
                <AlertTriangle size={16} aria-hidden="true" />
                <span>{error}</span>
              </div>
            ) : null}

            <div className="auth-field">
              <label className="auth-field-label" htmlFor="signin-username">{t('auth.field.login')}</label>
              <input
                id="signin-username"
                name="userName"
                type="text"
                autoComplete="username"
                value={userName}
                onChange={event => setUserName(event.target.value)}
                disabled={isSubmitting}
                required
              />
            </div>

            <div className="auth-field">
              <label className="auth-field-label" htmlFor="signin-password">{t('auth.field.password')}</label>
              <div className="auth-password">
                <input
                  id="signin-password"
                  name="password"
                  type={showPassword ? 'text' : 'password'}
                  autoComplete="current-password"
                  value={password}
                  onChange={event => setPassword(event.target.value)}
                  disabled={isSubmitting}
                  required
                />
                <button
                  type="button"
                  className="auth-password-toggle"
                  aria-label={t(showPassword ? 'auth.password.hide' : 'auth.password.show')}
                  onClick={() => setShowPassword(value => !value)}
                >
                  {showPassword ? <EyeOff size={16} aria-hidden="true" /> : <Eye size={16} aria-hidden="true" />}
                </button>
              </div>
            </div>

            <button type="submit" className="auth-primary" disabled={isSubmitting}>
              {isSubmitting ? <Loader2 className="auth-spinner" aria-hidden="true" /> : null}
              {isSubmitting ? t('auth.action.signingIn') : t('auth.action.signIn')}
              {isSubmitting ? null : <ArrowRight aria-hidden="true" />}
            </button>
          </form>
        </section>
      </main>
    </div>
  );
}
