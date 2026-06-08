import { useState, type FormEvent } from 'react';
import { PlatformApiError } from '../api/platformApi';
import type { StaffAuthApiClient } from '../api/staffAuthApi';
import { useI18n, type MessageKey } from '../i18n/I18nProvider';
import { ErrorBanner, Field } from './ui';

type Channel = 'email' | 'phone';
type Step = 'request' | 'verify' | 'done';

export interface ForgotPasswordProps {
  client: Pick<
    StaffAuthApiClient,
    'forgotPasswordByEmail' | 'resetPasswordByEmail' | 'forgotPasswordByPhone' | 'resetPasswordByPhone'
  >;
  onBackToSignIn: () => void;
}

// Both channels follow the same shape: request a 6-digit code, then enter it with a new password.
// Email mails the code; SMS texts it — the verify step is identical from here on.
export function ForgotPassword({ client, onBackToSignIn }: ForgotPasswordProps) {
  const { t } = useI18n();
  const [channel, setChannel] = useState<Channel>('email');
  const [emailLogin, setEmailLogin] = useState('');
  const [phone, setPhone] = useState('');
  const [step, setStep] = useState<Step>('request');
  const [code, setCode] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setSubmitting] = useState(false);

  function selectChannel(next: Channel) {
    setChannel(next);
    setStep('request');
    setCode('');
    setNewPassword('');
    setError(null);
  }

  async function submitRequest(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const recipient = channel === 'email' ? emailLogin.trim() : phone.trim();
    if (recipient.length === 0) {
      setError(t('auth.error.required'));
      return;
    }
    setSubmitting(true);
    setError(null);
    try {
      if (channel === 'email') {
        await client.forgotPasswordByEmail(recipient);
      } else {
        await client.forgotPasswordByPhone(recipient);
      }
      setStep('verify');
    } catch (cause) {
      setError(channel === 'email' ? t('auth.forgot.email.error') : projectResetError(cause, t));
    } finally {
      setSubmitting(false);
    }
  }

  async function submitReset(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (code.trim().length === 0 || newPassword.length < 8) {
      setError(t('auth.forgot.phone.error.fields'));
      return;
    }
    setSubmitting(true);
    setError(null);
    try {
      if (channel === 'email') {
        await client.resetPasswordByEmail(emailLogin.trim(), code.trim(), newPassword);
      } else {
        await client.resetPasswordByPhone(phone.trim(), code.trim(), newPassword);
      }
      setStep('done');
    } catch (cause) {
      setError(projectResetError(cause, t));
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

      {step === 'done' ? (
        <section className="section">
          <p>{t('auth.forgot.phone.done')}</p>
          <button type="button" className="primary" onClick={onBackToSignIn}>{t('auth.forgot.phone.toSignIn')}</button>
        </section>
      ) : step === 'verify' ? (
        <form className="form" onSubmit={(event) => void submitReset(event)}>
          <p className="muted">{channel === 'email' ? t('auth.forgot.email.sent') : t('auth.forgot.phone.sent')}</p>
          <Field label={channel === 'email' ? t('auth.reset.field.token') : t('auth.forgot.phone.codeField')} htmlFor="forgot-code">
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
      ) : channel === 'email' ? (
        <form className="form" onSubmit={(event) => void submitRequest(event)}>
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
      ) : (
        <form className="form" onSubmit={(event) => void submitRequest(event)}>
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
      )}

      <button type="button" className="linklike" onClick={onBackToSignIn}>{t('auth.forgot.back')}</button>
    </div>
  );
}

function projectResetError(cause: unknown, t: (key: MessageKey) => string): string {
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
