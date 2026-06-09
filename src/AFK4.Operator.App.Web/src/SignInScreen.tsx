import { AlertTriangle, Wifi } from 'lucide-react';
import { useEffect, useState, type FormEvent } from 'react';
import { useI18n } from '@afk4/i18n';
import { type OperatorSignInRequest } from './authClient';
import { getOperatorConfig } from './operatorConfig';
import type { AuthStatus } from './operatorTypes';
import { projectAuthHostError, shellModeLabel, isGuid } from './operatorHelpers';
import { WindowControls, WindowResizeHandles, handleWindowDragStart, handleWindowTitleDoubleClick } from './WindowChrome';

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
    <div className="operator-shell auth-shell">
      <WindowResizeHandles />
      <header className="top-command auth-top-command" onMouseDown={handleWindowDragStart} onDoubleClick={handleWindowTitleDoubleClick}>
        <div className="brand-block">
          <img className="brand-logo" src="/afk4-logo-horizontal.svg" alt="AFK4.NET" />
          <span>{t('op.auth.operator')}</span>
        </div>
        <div className="top-status">
          <span><Wifi size={14} />{isChecking ? t('op.shell.checkingAuth') : t('op.shell.secureAuth')}</span>
          <span>{config.platformBaseUrl}</span>
          <span>{shellModeLabel(config.shellMode, t)}</span>
        </div>
        <WindowControls />
      </header>

      <main className="auth-workspace">
        <section className="auth-panel">
          <header>
            <span>{t('op.shell.appName')}</span>
            <h1>{t('op.shell.signInTitle')}</h1>
            <p>{t('op.shell.storageNote')}</p>
          </header>

          <form className="auth-form" onSubmit={submit}>
            <label>
              {t('auth.field.login')}
              <input
                value={userName}
                onChange={(event) => setUserName(event.currentTarget.value)}
                autoComplete="username"
                autoFocus
              />
            </label>
            <label>
              {t('auth.field.password')}
              <input
                type="password"
                value={password}
                onChange={(event) => setPassword(event.currentTarget.value)}
                autoComplete="current-password"
              />
            </label>

            <button type="submit" className="primary-wide" disabled={isBusy || isChecking}>
              {isBusy ? t('auth.action.signingIn') : t('auth.action.signIn')}
            </button>
          </form>

          <button type="button" className="auth-link" onClick={onForgotPassword}>
            {t('auth.forgot.link')}
          </button>

          {error && (
            <div className="auth-error" role="alert">
              <AlertTriangle size={16} />
              <span>{error}</span>
            </div>
          )}
        </section>

        <aside className="auth-context-panel">
          <section>
            <span>{t('op.shell.platform')}</span>
            <strong>{config.platformBaseUrl}</strong>
          </section>
          <section>
            <span>{t('op.shell.currency')}</span>
            <strong>{config.currencyCode}</strong>
          </section>
          <section>
            <span>{t('op.shell.storage')}</span>
            <strong>{t('op.shell.secureStorage')}</strong>
          </section>
        </aside>
      </main>
    </div>
  );
}
