import { useState, type FormEvent } from 'react';
import { AlertTriangle, ArrowRight, Eye, EyeOff, Loader2 } from 'lucide-react';
import { PlatformApiClient, PlatformApiError } from '../api/platformApi';
import { useI18n } from '../i18n/I18nProvider';
import { BrandLogo } from './shell/BrandLogo';

export interface SignInProps {
  client: PlatformApiClient;
  onSignedIn: () => void;
}

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

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitting(true);
    setError(null);
    try {
      await client.signIn(userName.trim(), password);
      onSignedIn();
    } catch (cause) {
      if (cause instanceof PlatformApiError) {
        setError(cause.status === 401 ? t('auth.error.invalid') : cause.message);
      } else if (cause instanceof Error) {
        setError(cause.message);
      } else {
        setError(t('auth.error.generic'));
      }
    } finally {
      setSubmitting(false);
    }
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
