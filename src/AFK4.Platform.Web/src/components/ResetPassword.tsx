import { useState, type FormEvent } from 'react';
import type { StaffAuthApiClient } from '../api/staffAuthApi';
import { useI18n } from '../i18n/I18nProvider';
import { ErrorBanner, Field } from './ui';

export interface ResetPasswordProps {
  client: Pick<StaffAuthApiClient, 'resetPasswordByToken'>;
  initialToken: string | null;
  onBackToSignIn: () => void;
}

export function ResetPassword({ client, initialToken, onBackToSignIn }: ResetPasswordProps) {
  const { t } = useI18n();
  const [token, setToken] = useState(initialToken ?? '');
  const [newPassword, setNewPassword] = useState('');
  const [done, setDone] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (token.trim().length === 0 || newPassword.length < 8) {
      setError(t('auth.reset.error.fields'));
      return;
    }
    setSubmitting(true);
    setError(null);
    try {
      await client.resetPasswordByToken(token.trim(), newPassword);
      setDone(true);
    } catch {
      setError(t('auth.reset.error.invalid'));
    } finally {
      setSubmitting(false);
    }
  }

  if (done) {
    return (
      <div className="page page-narrow">
        <h1>{t('auth.reset.title')}</h1>
        <section className="section">
          <p>{t('auth.reset.success')}</p>
          <button type="button" className="primary" onClick={onBackToSignIn}>{t('auth.reset.toSignIn')}</button>
        </section>
      </div>
    );
  }

  return (
    <div className="page page-narrow">
      <h1>{t('auth.reset.title')}</h1>
      <p className="muted">{t('auth.reset.subtitle')}</p>
      <form className="form" onSubmit={(event) => void handleSubmit(event)}>
        <ErrorBanner message={error} onDismiss={() => setError(null)} />
        <Field label={t('auth.reset.field.token')} htmlFor="reset-token">
          <input
            id="reset-token"
            type="text"
            value={token}
            onChange={(event) => setToken(event.target.value)}
            disabled={isSubmitting}
            required
          />
        </Field>
        <Field label={t('auth.reset.field.newPassword')} htmlFor="reset-new-password">
          <input
            id="reset-new-password"
            type="password"
            autoComplete="new-password"
            value={newPassword}
            onChange={(event) => setNewPassword(event.target.value)}
            disabled={isSubmitting}
            required
          />
        </Field>
        <button type="submit" className="primary" disabled={isSubmitting}>
          {isSubmitting ? t('auth.reset.action.submitting') : t('auth.reset.action.submit')}
        </button>
      </form>
      <button type="button" className="linklike" onClick={onBackToSignIn}>{t('auth.reset.back')}</button>
    </div>
  );
}
