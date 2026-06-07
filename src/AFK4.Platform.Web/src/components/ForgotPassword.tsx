import { useState, type FormEvent } from 'react';
import { PlatformApiError } from '../api/platformApi';
import type { StaffAuthApiClient } from '../api/staffAuthApi';
import { useI18n, type MessageKey } from '../i18n/I18nProvider';
import { ErrorBanner, Field } from './ui';

type Channel = 'email' | 'phone';
type PhoneStep = 'request' | 'verify' | 'done';

export interface ForgotPasswordProps {
  client: Pick<StaffAuthApiClient, 'forgotPasswordByEmail' | 'forgotPasswordByPhone' | 'resetPasswordByPhone'>;
  onBackToSignIn: () => void;
  onOpenReset: () => void;
}

export function ForgotPassword({ client, onBackToSignIn, onOpenReset }: ForgotPasswordProps) {
  const { t } = useI18n();
  const [channel, setChannel] = useState<Channel>('email');
  const [emailLogin, setEmailLogin] = useState('');
  const [emailSent, setEmailSent] = useState(false);
  const [phone, setPhone] = useState('');
  const [phoneStep, setPhoneStep] = useState<PhoneStep>('request');
  const [code, setCode] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setSubmitting] = useState(false);

  function selectChannel(next: Channel) {
    setChannel(next);
    setError(null);
    setPhoneStep('request');
    setEmailSent(false);
  }

  async function submitEmail(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (emailLogin.trim().length === 0) {
      setError(t('auth.error.required'));
      return;
    }
    setSubmitting(true);
    setError(null);
    try {
      await client.forgotPasswordByEmail(emailLogin.trim());
      setEmailSent(true);
    } catch {
      setError(t('auth.forgot.email.error'));
    } finally {
      setSubmitting(false);
    }
  }

  async function submitPhoneRequest(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (phone.trim().length === 0) {
      setError(t('auth.error.required'));
      return;
    }
    setSubmitting(true);
    setError(null);
    try {
      await client.forgotPasswordByPhone(phone.trim());
      setPhoneStep('verify');
    } catch (cause) {
      setError(projectPhoneError(cause, t));
    } finally {
      setSubmitting(false);
    }
  }

  async function submitPhoneReset(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (code.trim().length === 0 || newPassword.length < 8) {
      setError(t('auth.forgot.phone.error.fields'));
      return;
    }
    setSubmitting(true);
    setError(null);
    try {
      await client.resetPasswordByPhone(phone.trim(), code.trim(), newPassword);
      setPhoneStep('done');
    } catch (cause) {
      setError(projectPhoneError(cause, t));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="page page-narrow">
      <h1>{t('auth.forgot.title')}</h1>
      <p className="muted">{t('auth.forgot.subtitle')}</p>

      <div className="actions" role="group" aria-label={t('auth.forgot.subtitle')}>
        <button
          type="button"
          className={channel === 'email' ? 'primary' : ''}
          aria-pressed={channel === 'email'}
          onClick={() => selectChannel('email')}
        >
          {t('auth.forgot.channel.email')}
        </button>
        <button
          type="button"
          className={channel === 'phone' ? 'primary' : ''}
          aria-pressed={channel === 'phone'}
          onClick={() => selectChannel('phone')}
        >
          {t('auth.forgot.channel.phone')}
        </button>
      </div>

      <ErrorBanner message={error} onDismiss={() => setError(null)} />

      {channel === 'email' && (emailSent ? (
        <section className="section">
          <p>{t('auth.forgot.email.sent')}</p>
          <div className="actions actions-stack">
            <button type="button" className="primary" onClick={onOpenReset}>{t('auth.forgot.email.openReset')}</button>
          </div>
        </section>
      ) : (
        <form className="form" onSubmit={(e) => void submitEmail(e)}>
          <Field label={t('auth.forgot.email.field')} htmlFor="forgot-email">
            <input
              id="forgot-email"
              type="text"
              autoComplete="username"
              value={emailLogin}
              onChange={(event) => setEmailLogin(event.target.value)}
              disabled={isSubmitting}
              required
            />
          </Field>
          <button type="submit" className="primary" disabled={isSubmitting}>
            {isSubmitting ? t('auth.forgot.email.submitting') : t('auth.forgot.email.submit')}
          </button>
        </form>
      ))}

      {channel === 'phone' && (phoneStep === 'done' ? (
        <section className="section">
          <p>{t('auth.forgot.phone.done')}</p>
          <button type="button" className="primary" onClick={onBackToSignIn}>{t('auth.forgot.phone.toSignIn')}</button>
        </section>
      ) : phoneStep === 'verify' ? (
        <form className="form" onSubmit={(e) => void submitPhoneReset(e)}>
          <p className="muted">{t('auth.forgot.phone.sent')}</p>
          <Field label={t('auth.forgot.phone.codeField')} htmlFor="forgot-code">
            <input
              id="forgot-code"
              type="text"
              inputMode="numeric"
              autoComplete="one-time-code"
              value={code}
              onChange={(event) => setCode(event.target.value)}
              disabled={isSubmitting}
              required
            />
          </Field>
          <Field label={t('auth.forgot.phone.newPassword')} htmlFor="forgot-new-password">
            <input
              id="forgot-new-password"
              type="password"
              autoComplete="new-password"
              value={newPassword}
              onChange={(event) => setNewPassword(event.target.value)}
              disabled={isSubmitting}
              required
            />
          </Field>
          <button type="submit" className="primary" disabled={isSubmitting}>
            {isSubmitting ? t('auth.forgot.phone.resetting') : t('auth.forgot.phone.reset')}
          </button>
        </form>
      ) : (
        <form className="form" onSubmit={(e) => void submitPhoneRequest(e)}>
          <Field label={t('auth.forgot.phone.field')} htmlFor="forgot-phone">
            <input
              id="forgot-phone"
              type="tel"
              inputMode="tel"
              autoComplete="tel"
              value={phone}
              onChange={(event) => setPhone(event.target.value)}
              disabled={isSubmitting}
              required
            />
          </Field>
          <button type="submit" className="primary" disabled={isSubmitting}>
            {isSubmitting ? t('auth.forgot.phone.submitting') : t('auth.forgot.phone.submit')}
          </button>
        </form>
      ))}

      <button type="button" className="linklike" onClick={onBackToSignIn}>{t('auth.forgot.back')}</button>
    </div>
  );
}

function projectPhoneError(cause: unknown, t: (key: MessageKey) => string): string {
  if (cause instanceof PlatformApiError) {
    switch (cause.errorCode) {
      case 'invalid_phone':
        return t('auth.forgot.phone.error.invalidPhone');
      case 'invalid_code':
        return cause.remainingAttempts === null
          ? t('auth.forgot.phone.error.invalidCode')
          : `${t('auth.forgot.phone.error.invalidCode')} ${t('auth.forgot.phone.error.remaining')}: ${cause.remainingAttempts}`;
      case 'code_expired':
        return t('auth.forgot.phone.error.expired');
      case 'too_many_attempts':
        return t('auth.forgot.phone.error.tooMany');
      default:
        return t('auth.forgot.phone.error.generic');
    }
  }
  return t('auth.forgot.phone.error.generic');
}
