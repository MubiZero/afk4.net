import { useState, type FormEvent } from 'react';
import { ArrowLeft, CheckCircle2, Loader2 } from 'lucide-react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import {
  forgotPasswordByEmail,
  forgotPasswordByPhone,
  resetPasswordByEmail,
  resetPasswordByPhone,
} from './wizardApi';
import { HostBridgeRequestError, isHostBridgeUnavailableError } from './hostBridge';

export type ResetChannel = 'email' | 'phone';

export interface SignInPrefill {
  channel: ResetChannel;
  identity: string;
}

interface ForgotPasswordScreenProps {
  onBack(prefill?: SignInPrefill): void;
}

type Step = 'request' | 'verify' | 'done';

const MIN_PASSWORD_LENGTH = 8;

// E.164-ish: 9–15 digits after stripping +, spaces and dashes — mirrors PhoneLoginScreen.
function normalizePhone(value: string): string {
  return value.replace(/[\s\-()]/g, '').replace(/^\+/, '');
}

// Both channels follow the same shape: request a 6-digit code, then enter it with a new password.
// Email mails the code; SMS texts it — the verify step is identical from there on.
export function ForgotPasswordScreen({ onBack }: ForgotPasswordScreenProps) {
  const { t } = useI18n();
  const [channel, setChannel] = useState<ResetChannel>('phone');
  const [emailLogin, setEmailLogin] = useState('');
  const [phone, setPhone] = useState('');
  const [phoneTouched, setPhoneTouched] = useState(false);
  const [step, setStep] = useState<Step>('request');
  const [code, setCode] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isBusy, setIsBusy] = useState(false);

  const identity = channel === 'email' ? emailLogin.trim() : phone.trim();
  const phoneValid = /^[0-9]{9,15}$/.test(normalizePhone(phone));
  // Don't accuse the user while they're still typing — only after they leave the field (rule #41).
  const showPhoneHint = channel === 'phone' && phoneTouched && phone.length > 0 && !phoneValid;
  const canRequest = (channel === 'phone' ? phoneValid : emailLogin.trim().length > 0) && !isBusy;
  const canReset = code.trim().length > 0 && newPassword.length >= MIN_PASSWORD_LENGTH && !isBusy;

  function selectChannel(next: ResetChannel) {
    setChannel(next);
    setStep('request');
    setCode('');
    setNewPassword('');
    setError(null);
  }

  function clearError() {
    if (error) setError(null);
  }

  function handleBack() {
    // From the verify step, "back" rewinds to re-enter the identity rather than abandoning the
    // whole reset; from the request step it leaves the screen and hands the typed identity back
    // to the sign-in form so the operator doesn't retype it.
    if (step === 'verify') {
      setStep('request');
      setCode('');
      setNewPassword('');
      setError(null);
      return;
    }
    onBack(identity ? { channel, identity } : undefined);
  }

  async function submitRequest(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!canRequest) return;
    setIsBusy(true);
    setError(null);
    try {
      if (channel === 'email') {
        await forgotPasswordByEmail(identity);
      } else {
        await forgotPasswordByPhone(identity);
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
    if (!canReset) return;
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

  if (step === 'done') {
    return (
      <section className="wizard-screen is-narrow is-static">
        <div className="wizard-finished-hero">
          <span className="wizard-finished-badge" aria-hidden>
            <CheckCircle2 size={32} />
          </span>
          <h1>{t('auth.reset.title')}</h1>
          <p>{t('auth.forgot.phone.done')}</p>
        </div>
        <div className="wizard-actions is-end">
          <button
            type="button"
            className="wizard-primary"
            onClick={() => onBack(identity ? { channel, identity } : undefined)}
          >
            <span>{t('auth.forgot.phone.toSignIn')}</span>
          </button>
        </div>
      </section>
    );
  }

  return (
    <section className="wizard-screen is-narrow is-static">
      <div className="wizard-screen-head">
        <h1>{t('auth.forgot.title')}</h1>
        <p>{step === 'verify' ? t('auth.forgot.verify.subtitle') : t('auth.forgot.subtitle')}</p>
      </div>

      {step === 'verify' ? (
        <form className="wizard-form" onSubmit={submitReset} noValidate>
          <p className="wizard-field-hint">
            {channel === 'email' ? t('auth.forgot.email.sent') : t('auth.forgot.phone.sent')}
          </p>
          <label className="wizard-field">
            <span className="wizard-field-label">
              {channel === 'email' ? t('auth.reset.field.token') : t('auth.forgot.phone.codeField')}
            </span>
            <input
              value={code}
              onChange={(event) => { setCode(event.target.value); clearError(); }}
              inputMode="numeric"
              autoComplete="one-time-code"
              autoFocus
              disabled={isBusy}
              aria-describedby="reset-code-hint"
            />
            <span id="reset-code-hint" className="wizard-field-hint">{t('auth.forgot.code.hint')}</span>
          </label>
          <label className="wizard-field">
            <span className="wizard-field-label">{t('auth.forgot.phone.newPassword')}</span>
            <input
              type="password"
              value={newPassword}
              onChange={(event) => { setNewPassword(event.target.value); clearError(); }}
              autoComplete="new-password"
              disabled={isBusy}
              aria-describedby="reset-password-hint"
            />
            <span id="reset-password-hint" className="wizard-field-hint">{t('auth.forgot.newPassword.hint')}</span>
          </label>
          <button type="submit" className="wizard-primary" disabled={!canReset}>
            {isBusy ? (
              <>
                <Loader2 className="wizard-spinner" aria-hidden />
                <span>{t('auth.forgot.phone.resetting')}</span>
              </>
            ) : (
              <span>{t('auth.forgot.phone.reset')}</span>
            )}
          </button>
        </form>
      ) : channel === 'email' ? (
        <form className="wizard-form" onSubmit={submitRequest} noValidate>
          <label className="wizard-field">
            <span className="wizard-field-label">{t('auth.forgot.email.field')}</span>
            <input
              value={emailLogin}
              onChange={(event) => { setEmailLogin(event.target.value); clearError(); }}
              autoComplete="username"
              spellCheck={false}
              disabled={isBusy}
              autoFocus
            />
          </label>
          <button type="submit" className="wizard-primary" disabled={!canRequest}>
            {isBusy ? (
              <>
                <Loader2 className="wizard-spinner" aria-hidden />
                <span>{t('auth.forgot.email.submitting')}</span>
              </>
            ) : (
              <span>{t('auth.forgot.email.submit')}</span>
            )}
          </button>
        </form>
      ) : (
        <form className="wizard-form" onSubmit={submitRequest} noValidate>
          <label className="wizard-field">
            <span className="wizard-field-label">{t('auth.forgot.phone.field')}</span>
            <input
              type="tel"
              value={phone}
              onChange={(event) => { setPhone(event.target.value); clearError(); }}
              onBlur={() => setPhoneTouched(true)}
              inputMode="tel"
              autoComplete="tel"
              spellCheck={false}
              placeholder="+992 93 738 00 70"
              disabled={isBusy}
              autoFocus
              aria-invalid={showPhoneHint}
              aria-describedby={showPhoneHint ? 'reset-phone-hint' : undefined}
            />
            {showPhoneHint && (
              <span id="reset-phone-hint" className="wizard-field-hint">
                {t('auth.forgot.phone.error.invalidPhone')}
              </span>
            )}
          </label>
          <button type="submit" className="wizard-primary" disabled={!canRequest}>
            {isBusy ? (
              <>
                <Loader2 className="wizard-spinner" aria-hidden />
                <span>{t('auth.forgot.phone.submitting')}</span>
              </>
            ) : (
              <span>{t('auth.forgot.phone.submit')}</span>
            )}
          </button>
        </form>
      )}

      {step === 'request' && (
        <div className="wizard-alt-actions">
          <button
            type="button"
            className="wizard-link-action"
            onClick={() => selectChannel(channel === 'phone' ? 'email' : 'phone')}
          >
            {channel === 'phone' ? t('auth.forgot.switchToEmail') : t('auth.forgot.switchToSms')}
          </button>
        </div>
      )}

      {error && (
        <div role="alert" className="wizard-alert">
          {error}
        </div>
      )}

      <button type="button" className="wizard-link-action wizard-fallback-link" onClick={handleBack}>
        <ArrowLeft aria-hidden />
        <span>{step === 'verify' ? t('setup.wizard.common.back') : t('auth.forgot.back')}</span>
      </button>
    </section>
  );
}

function requestError(channel: ResetChannel, cause: unknown, t: (key: MessageKey) => string): string {
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
