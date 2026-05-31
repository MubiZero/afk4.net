import { useState, type FormEvent } from 'react';
import { PlatformApiError } from '../api/platformApi';
import { StaffSignInChooseClubError, type StaffAuthApiClient } from '../api/staffAuthApi';
import type { StaffSignInClubChoice } from '../api/types';
import { useI18n } from '../i18n/I18nProvider';
import { ErrorBanner, Field } from './ui';

export interface StaffSignInProps {
  client: StaffAuthApiClient;
  onSignedIn: () => void;
  onOpenAcceptInvite: () => void;
}

export function StaffSignIn({ client, onSignedIn, onOpenAcceptInvite }: StaffSignInProps) {
  const { t } = useI18n();
  const [login, setLogin] = useState('');
  const [password, setPassword] = useState('');
  const [clubChoices, setClubChoices] = useState<StaffSignInClubChoice[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const normalizedLogin = login.trim();
    if (normalizedLogin.length === 0 || password.length === 0) {
      setError(t('auth.error.required'));
      return;
    }
    setSubmitting(true);
    setError(null);
    try {
      await client.signInByLogin(normalizedLogin, password);
      onSignedIn();
    } catch (cause) {
      if (cause instanceof StaffSignInChooseClubError) {
        setClubChoices(cause.clubs);
      } else {
        setError(projectSignInError(cause, t));
      }
    } finally {
      setSubmitting(false);
    }
  }

  async function handleChooseClub(organizationId: string) {
    setSubmitting(true);
    setError(null);
    try {
      await client.signInToClub(organizationId, login.trim(), password);
      onSignedIn();
    } catch (cause) {
      setClubChoices(null);
      setError(projectSignInError(cause, t));
    } finally {
      setSubmitting(false);
    }
  }

  if (clubChoices !== null) {
    return (
      <div className="page page-narrow">
        <h1>{t('auth.chooseClub.title')}</h1>
        <p className="muted">{t('auth.chooseClub.subtitle')}</p>
        <ErrorBanner message={error} onDismiss={() => setError(null)} />
        <div className="actions actions-stack">
          {clubChoices.map(choice => (
            <button
              key={choice.organizationId}
              type="button"
              className="primary"
              disabled={isSubmitting}
              onClick={() => void handleChooseClub(choice.organizationId)}
            >
              {choice.name}
            </button>
          ))}
          <button type="button" disabled={isSubmitting} onClick={() => setClubChoices(null)}>
            {t('auth.chooseClub.back')}
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="page page-narrow">
      <h1>{t('auth.club.title')}</h1>
      <p className="muted">{t('auth.club.subtitle')}</p>
      <form className="form" onSubmit={handleSubmit}>
        <ErrorBanner message={error} onDismiss={() => setError(null)} />
        <Field label={t('auth.field.login')} htmlFor="staff-login">
          <input
            id="staff-login"
            name="login"
            type="text"
            autoComplete="username"
            value={login}
            onChange={event => setLogin(event.target.value)}
            disabled={isSubmitting}
            required
          />
        </Field>
        <Field label={t('auth.field.password')} htmlFor="staff-password">
          <input
            id="staff-password"
            name="password"
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={event => setPassword(event.target.value)}
            disabled={isSubmitting}
            required
          />
        </Field>
        <button type="submit" className="primary" disabled={isSubmitting}>
          {isSubmitting ? t('auth.action.signingIn') : t('auth.action.signIn')}
        </button>
      </form>
      <button type="button" className="linklike" onClick={onOpenAcceptInvite}>
        {t('auth.haveCode')}
      </button>
    </div>
  );
}

function projectSignInError(cause: unknown, t: (key: 'auth.error.invalid' | 'auth.error.generic') => string): string {
  if (cause instanceof PlatformApiError) {
    return cause.status === 401 ? t('auth.error.invalid') : cause.message;
  }
  if (cause instanceof Error) {
    return cause.message;
  }
  return t('auth.error.generic');
}
