import { useState, type FormEvent } from 'react';
import { ArrowLeft } from 'lucide-react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import {
  forgotPasswordByEmail,
  forgotPasswordByPhone,
  resetPasswordByEmail,
  resetPasswordByPhone,
} from './wizardApi';
import { HostBridgeRequestError, isHostBridgeUnavailableError } from './hostBridge';

interface ForgotPasswordScreenProps {
  onBack(): void;
}

type Channel = 'email' | 'phone';
type Step = 'request' | 'verify' | 'done';

// Both channels follow the same shape: request a 6-digit code, then enter it with a new password.
// Email mails the code; SMS texts it — the verify step is identical from there on.
export function ForgotPasswordScreen({ onBack }: ForgotPasswordScreenProps) {
  const { t } = useI18n();
  const [channel, setChannel] = useState<Channel>('phone');
  const [emailLogin, setEmailLogin] = useState('');
  const [phone, setPhone] = useState('');
  const [step, setStep] = useState<Step>('request');
  const [code, setCode] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isBusy, setIsBusy] = useState(false);

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
    if (!recipient) {
      setError(t('auth.error.required'));
      return;
    }
    setIsBusy(true);
    setError(null);
    try {
      if (channel === 'email') {
        await forgotPasswordByEmail(recipient);
      } else {
        await forgotPasswordByPhone(recipient);
      }
      setStep('verify');
    } catch (cause) {
      setError(requestError(channel, cause, t));
    } finally {
      setIsBusy(false);
    }
  }

  async function submitReset(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!code.trim() || newPassword.length < 8) {
      setError(t('auth.forgot.phone.error.fields'));
      return;
    }
    setIsBusy(true);
    setError(null);
    try {
      if (channel === 'email') {
        await resetPasswordByEmail(emailLogin.trim(), code.trim(), newPassword);
      } else {
        await resetPasswordByPhone(phone.trim(), code.trim(), newPassword);
      }
      setStep('done');
    } catch (cause) {
      setError(projectResetError(cause, t));
    } finally {
      setIsBusy(false);
    }
  }

  return (
    <section className="wizard-screen is-narrow is-static">
      <div className="wizard-screen-head">
        <h1>{t('auth.forgot.title')}</h1>
        <p>{t('auth.forgot.subtitle')}</p>
      </div>

      <div className="wizard-segment" role="radiogroup" aria-label={t('auth.forgot.subtitle')}>
        <button
          type="button"
          role="radio"
          className="wizard-segment-button"
          aria-checked={channel === 'phone'}
          aria-pressed={channel === 'phone'}
          onClick={() => selectChannel('phone')}
        >
          <span className="wizard-segment-body">
            <strong>{t('auth.forgot.channel.phone')}</strong>
          </span>
        </button>
        <button
          type="button"
          role="radio"
          className="wizard-segment-button"
          aria-checked={channel === 'email'}
          aria-pressed={channel === 'email'}
          onClick={() => selectChannel('email')}
        >
          <span className="wizard-segment-body">
            <strong>{t('auth.forgot.channel.email')}</strong>
          </span>
        </button>
      </div>

      {step === 'done' ? (
        <div className="wizard-confirm">
          <p>{t('auth.forgot.phone.done')}</p>
        </div>
      ) : step === 'verify' ? (
        <form className="wizard-form" onSubmit={submitReset} noValidate>
          <p className="wizard-field-hint">{channel === 'email' ? t('auth.forgot.email.sent') : t('auth.forgot.phone.sent')}</p>
          <label className="wizard-field">
            <span className="wizard-field-label">{channel === 'email' ? t('auth.reset.field.token') : t('auth.forgot.phone.codeField')}</span>
            <input
              value={code}
              onChange={(event) => setCode(event.target.value)}
              inputMode="numeric"
              autoComplete="one-time-code"
              disabled={isBusy}
            />
          </label>
          <label className="wizard-field">
            <span className="wizard-field-label">{t('auth.forgot.phone.newPassword')}</span>
            <input
              type="password"
              value={newPassword}
              onChange={(event) => setNewPassword(event.target.value)}
              autoComplete="new-password"
              disabled={isBusy}
            />
          </label>
          <button type="submit" className="wizard-primary" disabled={isBusy}>
            {isBusy ? t('auth.forgot.phone.resetting') : t('auth.forgot.phone.reset')}
          </button>
        </form>
      ) : channel === 'email' ? (
        <form className="wizard-form" onSubmit={submitRequest} noValidate>
          <label className="wizard-field">
            <span className="wizard-field-label">{t('auth.forgot.email.field')}</span>
            <input
              value={emailLogin}
              onChange={(event) => setEmailLogin(event.target.value)}
              autoComplete="username"
              disabled={isBusy}
              autoFocus
            />
          </label>
          <button type="submit" className="wizard-primary" disabled={isBusy}>
            {isBusy ? t('auth.forgot.email.submitting') : t('auth.forgot.email.submit')}
          </button>
        </form>
      ) : (
        <form className="wizard-form" onSubmit={submitRequest} noValidate>
          <label className="wizard-field">
            <span className="wizard-field-label">{t('auth.forgot.phone.field')}</span>
            <input
              type="tel"
              value={phone}
              onChange={(event) => setPhone(event.target.value)}
              inputMode="tel"
              autoComplete="tel"
              placeholder="+992 93 738-00-70"
              disabled={isBusy}
              autoFocus
            />
          </label>
          <button type="submit" className="wizard-primary" disabled={isBusy}>
            {isBusy ? t('auth.forgot.phone.submitting') : t('auth.forgot.phone.submit')}
          </button>
        </form>
      )}

      {error && (
        <div role="alert" className="wizard-alert">
          {error}
        </div>
      )}

      <button type="button" className="wizard-link-action wizard-fallback-link" onClick={onBack}>
        <ArrowLeft aria-hidden />
        <span>{t('auth.forgot.back')}</span>
      </button>
    </section>
  );
}

function requestError(channel: Channel, cause: unknown, t: (key: MessageKey) => string): string {
  if (channel === 'email' && !isHostBridgeUnavailableError(cause)) {
    return t('auth.forgot.email.error');
  }
  return projectResetError(cause, t);
}

function projectResetError(cause: unknown, t: (key: MessageKey) => string): string {
  if (isHostBridgeUnavailableError(cause)) {
    return t('setup.wizard.phoneLogin.error.bridgeMissing');
  }
  if (cause instanceof HostBridgeRequestError) {
    switch (cause.code) {
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
