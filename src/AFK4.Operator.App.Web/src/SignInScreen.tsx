import { AlertTriangle, ArrowRight, Eye, EyeOff, Loader2 } from 'lucide-react';
import { useEffect, useState, type FormEvent } from 'react';
import { useI18n } from '@afk4/i18n';
import { type OperatorSignInRequest } from './authClient';
import { getOperatorConfig } from './operatorConfig';
import type { AuthStatus } from './operatorTypes';
import { projectAuthHostError, isGuid } from './operatorHelpers';
import { AuthFrame } from './AuthFrame';

export function SignInScreen({
  config,
  authStatus,
  hostError,
  onSignIn,
  onForgotPassword
}: {
  config: ReturnType<typeof getOperatorConfig>;
  authStatus: AuthStatus;
  hostError: string | null;
  onSignIn: (request: OperatorSignInRequest) => Promise<void>;
  onForgotPassword: () => void;
}) {
  const { t } = useI18n();
  const [userName, setUserName] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [isBusy, setIsBusy] = useState(false);
  const [error, setError] = useState<string | null>(hostError);
  const isChecking = authStatus === 'checking';

  useEffect(() => {
    setError(hostError);
  }, [hostError]);

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError(null);

    const organizationId = config.organizationId?.trim() ?? '';
    if (!isGuid(organizationId)) {
      setError(t('op.auth.connectionMissing'));
      return;
    }

    if (!userName.trim()) {
      setError(t('auth.error.required'));
      return;
    }

    if (!password) {
      setError(t('auth.error.required'));
      return;
    }

    setIsBusy(true);
    try {
      await onSignIn({
        organizationId: organizationId.trim(),
        userName: userName.trim(),
        password
      });
      setPassword('');
    } catch (nextError) {
      setError(projectAuthHostError(nextError, config, t));
    } finally {
      setIsBusy(false);
    }
  };

  return (
    <AuthFrame>
      <section className="auth-panel">
        <header>
          <h1>{t('op.shell.signInTitle')}</h1>
          <p>{t('op.auth.signInSubtitle')}</p>
        </header>

        <form className="auth-form" onSubmit={submit} noValidate>
          <label className="auth-field">
            <span className="auth-field-label">{t('auth.field.login')}</span>
            <input
              value={userName}
              onChange={(event) => setUserName(event.currentTarget.value)}
              autoComplete="username"
              spellCheck={false}
              autoFocus
            />
          </label>

          <div className="auth-field">
            <div className="auth-field-label auth-label-with-action">
              <label htmlFor="operator-password">{t('auth.field.password')}</label>
              <button type="button" className="auth-link-inline" onClick={onForgotPassword}>
                {t('auth.forgot.link')}
              </button>
            </div>
            <div className="auth-password">
              <input
                id="operator-password"
                type={showPassword ? 'text' : 'password'}
                value={password}
                onChange={(event) => setPassword(event.currentTarget.value)}
                autoComplete="current-password"
              />
              <button
                type="button"
                className="auth-password-toggle"
                aria-pressed={showPassword}
                aria-label={showPassword ? t('op.auth.hidePassword') : t('op.auth.showPassword')}
                onClick={() => setShowPassword((value) => !value)}
              >
                {showPassword ? <EyeOff size={16} aria-hidden /> : <Eye size={16} aria-hidden />}
              </button>
            </div>
          </div>

          {error && (
            <div className="auth-error" role="alert">
              <AlertTriangle size={16} aria-hidden />
              <span>{error}</span>
            </div>
          )}

          <button type="submit" className="auth-primary" disabled={isBusy || isChecking}>
            {isBusy ? (
              <>
                <Loader2 className="auth-spinner" size={18} aria-hidden />
                <span>{t('auth.action.signingIn')}</span>
              </>
            ) : (
              <>
                <span>{t('auth.action.signIn')}</span>
                <ArrowRight size={18} aria-hidden />
              </>
            )}
          </button>
        </form>
      </section>
    </AuthFrame>
  );
}
