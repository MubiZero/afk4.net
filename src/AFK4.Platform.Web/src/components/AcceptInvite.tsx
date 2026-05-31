import { useEffect, useState, type FormEvent } from 'react';
import { PlatformApiError } from '../api/platformApi';
import type { StaffAuthApiClient } from '../api/staffAuthApi';
import { useI18n } from '../i18n/I18nProvider';
import { ErrorBanner, Field } from './ui';

export interface AcceptInviteProps {
  client: StaffAuthApiClient;
  initialCode: string | null;
  onAccepted: () => void;
  onOpenSignIn: () => void;
}

export function AcceptInvite({ client, initialCode, onAccepted, onOpenSignIn }: AcceptInviteProps) {
  const { t } = useI18n();
  const [code, setCode] = useState(initialCode ?? '');
  const [userName, setUserName] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setSubmitting] = useState(false);

  useEffect(() => {
    setCode(initialCode ?? '');
  }, [initialCode]);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const normalizedCode = code.trim();
    const normalizedUserName = userName.trim();
    if (normalizedCode.length === 0) {
      setError(t('auth.accept.error.codeRequired'));
      return;
    }
    if (normalizedUserName.length === 0) {
      setError(t('auth.accept.error.loginRequired'));
      return;
    }
    if (password.length < 8) {
      setError(t('auth.accept.error.passwordLength'));
      return;
    }
    if (password !== confirmPassword) {
      setError(t('auth.accept.error.passwordMismatch'));
      return;
    }

    setSubmitting(true);
    setError(null);
    try {
      await client.acceptInvite({
        code: normalizedCode,
        userName: normalizedUserName,
        displayName: '',
        password
      });
      onAccepted();
    } catch (cause) {
      setError(projectAcceptInviteError(cause, t));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="page page-narrow">
      <h1>{t('auth.accept.title')}</h1>
      <p className="muted">{t('auth.accept.subtitle')}</p>
      <form className="form" onSubmit={handleSubmit}>
        <ErrorBanner message={error} onDismiss={() => setError(null)} />
        <Field label={t('auth.accept.field.code')} htmlFor="accept-code">
          <input
            id="accept-code"
            name="code"
            type="text"
            autoComplete="one-time-code"
            value={code}
            onChange={event => setCode(event.target.value)}
            disabled={isSubmitting}
            required
          />
        </Field>
        <Field label={t('auth.field.login')} htmlFor="accept-username">
          <input
            id="accept-username"
            name="userName"
            type="text"
            autoComplete="username"
            value={userName}
            onChange={event => setUserName(event.target.value)}
            disabled={isSubmitting}
            required
          />
        </Field>
        <Field label={t('auth.field.password')} htmlFor="accept-password">
          <input
            id="accept-password"
            name="password"
            type="password"
            autoComplete="new-password"
            value={password}
            onChange={event => setPassword(event.target.value)}
            disabled={isSubmitting}
            required
          />
        </Field>
        <Field label={t('auth.accept.field.confirmPassword')} htmlFor="accept-confirm-password">
          <input
            id="accept-confirm-password"
            name="confirmPassword"
            type="password"
            autoComplete="new-password"
            value={confirmPassword}
            onChange={event => setConfirmPassword(event.target.value)}
            disabled={isSubmitting}
            required
          />
        </Field>
        <div className="actions">
          <button type="submit" className="primary" disabled={isSubmitting}>
            {isSubmitting ? t('auth.accept.action.submitting') : t('auth.accept.action.submit')}
          </button>
          <button type="button" onClick={onOpenSignIn} disabled={isSubmitting}>
            {t('auth.accept.action.signInInstead')}
          </button>
        </div>
      </form>
    </div>
  );
}

function projectAcceptInviteError(
  cause: unknown,
  t: (key: 'auth.accept.error.codeNotFound' | 'auth.accept.error.loginTaken' | 'auth.accept.error.generic') => string
): string {
  if (cause instanceof PlatformApiError) {
    if (cause.status === 404) {
      return t('auth.accept.error.codeNotFound');
    }
    if (cause.status === 409) {
      return t('auth.accept.error.loginTaken');
    }
    return cause.message;
  }
  if (cause instanceof Error) {
    return cause.message;
  }
  return t('auth.accept.error.generic');
}
