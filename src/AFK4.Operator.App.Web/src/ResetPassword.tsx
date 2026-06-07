import { useState, type FormEvent } from 'react';
import { AlertTriangle } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import { resetPasswordByEmail } from './authClient';

export function ResetPassword({ onBackToSignIn }: { onBackToSignIn: () => void }) {
  const { t } = useI18n();
  const [token, setToken] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [done, setDone] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isBusy, setIsBusy] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!token.trim() || newPassword.length < 8) { setError(t('auth.reset.error.fields')); return; }
    setIsBusy(true); setError(null);
    try {
      await resetPasswordByEmail(token.trim(), newPassword);
      setDone(true);
    } catch {
      setError(t('auth.reset.error.invalid'));
    } finally {
      setIsBusy(false);
    }
  }

  return (
    <div className="operator-shell auth-shell">
      <main className="auth-workspace">
        <section className="auth-panel">
          <header>
            <span>AFK4.NET {t('op.auth.operator')}</span>
            <h1>{t('auth.reset.title')}</h1>
            <p>{t('auth.reset.subtitle')}</p>
          </header>

          {done ? (
            <section className="auth-confirm">
              <p>{t('auth.reset.success')}</p>
              <button type="button" className="primary-wide" onClick={onBackToSignIn}>{t('auth.reset.toSignIn')}</button>
            </section>
          ) : (
            <form className="auth-form" onSubmit={handleSubmit}>
              <label>
                {t('auth.reset.field.token')}
                <input value={token} onChange={(e) => setToken(e.currentTarget.value)} autoFocus disabled={isBusy} />
              </label>
              <label>
                {t('auth.reset.field.newPassword')}
                <input type="password" value={newPassword} onChange={(e) => setNewPassword(e.currentTarget.value)} autoComplete="new-password" disabled={isBusy} />
              </label>
              <button type="submit" className="primary-wide" disabled={isBusy}>
                {isBusy ? t('auth.reset.action.submitting') : t('auth.reset.action.submit')}
              </button>
            </form>
          )}

          {error && (
            <div className="auth-error" role="alert">
              <AlertTriangle size={16} />
              <span>{error}</span>
            </div>
          )}

          <button type="button" className="auth-link" onClick={onBackToSignIn}>{t('auth.reset.back')}</button>
        </section>
      </main>
    </div>
  );
}
