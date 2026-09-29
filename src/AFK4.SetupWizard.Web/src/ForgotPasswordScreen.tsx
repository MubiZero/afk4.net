import { useState, type FormEvent } from 'react';
import { Loader2 } from 'lucide-react';
import { Button } from '@afk4/ui/react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { PIN_LENGTH, isWellFormedPin, keepPinDigits } from '@afk4/contracts';
import {
  forgotPasswordByEmail,
  forgotPasswordByPhone,
  resetPasswordByEmail,
  resetPasswordByPhone,
} from './wizardApi';
import { HostBridgeRequestError, isHostBridgeUnavailableError } from './hostBridge';
import { localPhoneDigits, formatLocal, fullPhoneDigits } from '@afk4/formatting';
import { WizardStepLayout } from './WizardStepLayout';

export type ResetChannel = 'email' | 'phone';

export interface SignInPrefill {
  channel: ResetChannel;
  identity: string;
}

interface ForgotPasswordScreenProps {
  onBack(prefill?: SignInPrefill): void;
}

type Step = 'request' | 'verify' | 'done';

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

  // For phone we send the full E.164 digits (992 + 9 local), mirroring the sign-in screen; the field
  // itself holds only the masked local part. For email it's the typed login/email as-is.
  const identity = channel === 'email' ? emailLogin.trim() : fullPhoneDigits(phone);
  const phoneComplete = localPhoneDigits(phone).length === 9;
  // Don't accuse the user while they're still typing — only after they leave the field (rule #41).
  const showPhoneHint = channel === 'phone' && phoneTouched && phone.length > 0 && !phoneComplete;
  const canRequest = (channel === 'phone' ? phoneComplete : emailLogin.trim().length > 0) && !isBusy;
  const canReset = code.trim().length > 0 && isWellFormedPin(newPassword) && !isBusy;

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
    // Only hand an identity back if the user actually typed one — `identity` for an empty phone
    // would still be "992" (the bare country code), which isn't a real prefill.
    const hasInput = channel === 'email' ? emailLogin.trim().length > 0 : localPhoneDigits(phone).length > 0;
    onBack(hasInput ? { channel, identity } : undefined);
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
        await resetPasswordByPhone(fullPhoneDigits(phone), code.trim(), newPassword);
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
      <WizardStepLayout
        title={t('auth.reset.title')}
        subtitle={t('auth.forgot.phone.done')}
        primary={(
          <Button variant="primary" onClick={() => onBack(identity ? { channel, identity } : undefined)}>
            {t('auth.forgot.phone.toSignIn')}
          </Button>
        )}
      />
    );
  }

  // Сброс ПИН-кода — ответвление от входа: номера шага у него нет, а «Назад» — там же, где на
  // любом шаге мастера, внизу слева.
  return (
    <WizardStepLayout
      title={t('auth.forgot.title')}
      subtitle={step === 'verify' ? t('auth.forgot.verify.subtitle') : t('auth.forgot.subtitle')}
      onBack={handleBack}
      backLabel={step === 'verify' ? undefined : t('auth.forgot.back')}
    >

      {step === 'verify' ? (
        <form className="wizard-form" onSubmit={submitReset} noValidate>
          <p className="ui-field-hint">
            {channel === 'email' ? t('auth.forgot.email.sent') : t('auth.forgot.phone.sent')}
          </p>
          <label className="ui-field">
            <span className="ui-field-label">
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
            <span id="reset-code-hint" className="ui-field-hint">{t('auth.forgot.code.hint', { min: PIN_LENGTH })}</span>
          </label>
          <label className="ui-field">
            <span className="ui-field-label">{t('auth.forgot.phone.newPassword')}</span>
            <input
              type="password"
              value={newPassword}
              onChange={(event) => { setNewPassword(keepPinDigits(event.target.value)); clearError(); }}
              inputMode="numeric"
              maxLength={PIN_LENGTH}
              autoComplete="new-password"
              disabled={isBusy}
              aria-describedby="reset-password-hint"
            />
            <span id="reset-password-hint" className="ui-field-hint">{t('auth.forgot.newPassword.hint', { min: PIN_LENGTH })}</span>
          </label>
          <button type="submit" className="ui-btn ui-btn--primary" disabled={!canReset}>
            {isBusy ? (
              <>
                <Loader2 className="ui-spinner" aria-hidden />
                <span>{t('auth.forgot.phone.resetting')}</span>
              </>
            ) : (
              <span>{t('auth.forgot.phone.reset')}</span>
            )}
          </button>
        </form>
      ) : channel === 'email' ? (
        <form className="wizard-form" onSubmit={submitRequest} noValidate>
          <label className="ui-field">
            <span className="ui-field-label">{t('auth.forgot.email.field')}</span>
            <input
              value={emailLogin}
              onChange={(event) => { setEmailLogin(event.target.value); clearError(); }}
              autoComplete="username"
              spellCheck={false}
              disabled={isBusy}
              autoFocus
            />
          </label>
          <button type="submit" className="ui-btn ui-btn--primary" disabled={!canRequest}>
            {isBusy ? (
              <>
                <Loader2 className="ui-spinner" aria-hidden />
                <span>{t('auth.forgot.email.submitting')}</span>
              </>
            ) : (
              <span>{t('auth.forgot.email.submit')}</span>
            )}
          </button>
        </form>
      ) : (
        <form className="wizard-form" onSubmit={submitRequest} noValidate>
          <label className="ui-field">
            <span className="ui-field-label">{t('auth.forgot.phone.field')}</span>
            <div className="ui-phone-field">
              <span className="ui-phone-prefix" aria-hidden>+992</span>
              <input
                className="ui-phone-input"
                type="tel"
                value={phone}
                onChange={(event) => { setPhone(formatLocal(event.target.value)); clearError(); }}
                onBlur={() => setPhoneTouched(true)}
                inputMode="tel"
                autoComplete="tel"
                spellCheck={false}
                placeholder="93 738 00 70"
                disabled={isBusy}
                autoFocus
                aria-invalid={showPhoneHint}
                aria-describedby={showPhoneHint ? 'reset-phone-hint' : undefined}
              />
            </div>
            {showPhoneHint && (
              <span id="reset-phone-hint" className="ui-field-hint">
                {t('auth.forgot.phone.error.invalidPhone')}
              </span>
            )}
          </label>
          <button type="submit" className="ui-btn ui-btn--primary" disabled={!canRequest}>
            {isBusy ? (
              <>
                <Loader2 className="ui-spinner" aria-hidden />
                <span>{t('auth.forgot.phone.submitting')}</span>
              </>
            ) : (
              <span>{t('auth.forgot.phone.submit')}</span>
            )}
          </button>
        </form>
      )}

      {step === 'request' && (
        <button
          type="button"
          className="wizard-link-inline wizard-mode-switch"
          onClick={() => selectChannel(channel === 'phone' ? 'email' : 'phone')}
        >
          {channel === 'phone' ? t('auth.forgot.switchToEmail') : t('auth.forgot.switchToSms')}
        </button>
      )}

      {error && (
        <div role="alert" className="ui-alert">
          {error}
        </div>
      )}

    </WizardStepLayout>
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
