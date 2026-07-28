import { useState, type FormEvent } from 'react';
import { AlertTriangle } from 'lucide-react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { forgotPasswordByEmail, forgotPasswordByPhone, resetPasswordByEmail, resetPasswordByPhone, StaffAuthApiError } from './authClient';
import { AuthFrame } from './AuthFrame';
import { localPhoneDigits, formatLocal, fullPhoneDigits } from './phoneFormat';
import { isRecord } from './operatorHelpers';

type Channel = 'email' | 'phone';
type Step = 'request' | 'verify' | 'done';

// Both channels follow the same shape: request a 6-digit code, then enter it with a new password.
// Email mails the code; SMS texts it — the verify step is identical from there on.
export function ForgotPassword({ onBackToSignIn }: { onBackToSignIn: () => void }) {
  const { t } = useI18n();
  const [channel, setChannel] = useState<Channel>('email');
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
    setError(null);
    let recipient: string;
    if (channel === 'email') {
      recipient = emailLogin.trim();
      if (!recipient) { setError(t('auth.error.required')); return; }
    } else {
      if (localPhoneDigits(phone).length !== 9) { setError(t('op.auth.hint.phone')); return; }
      recipient = fullPhoneDigits(phone);
    }
    setIsBusy(true);
    try {
      if (channel === 'email') {
        await forgotPasswordByEmail(recipient);
      } else {
        await forgotPasswordByPhone(recipient);
      }
      setStep('verify');
    } catch (cause) {
      setError(channel === 'email' ? t('auth.forgot.email.error') : projectResetError(cause, t));
    } finally {
      setIsBusy(false);
    }
  }

  async function submitReset(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!code.trim() || newPassword.length < 8) { setError(t('auth.forgot.phone.error.fields')); return; }
    setIsBusy(true); setError(null);
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

  return (
    <AuthFrame>
      <section className="auth-panel">
        <header className="auth-panel-head">
          <img className="auth-brand-mark" src="/favicon.svg" alt="" aria-hidden />
          <h1>{t('auth.forgot.title')}</h1>
          <p>{t('auth.forgot.subtitle')}</p>
        </header>

        <div className="auth-channel-toggle" role="tablist" aria-label={t('auth.forgot.subtitle')}>
          <button type="button" className={channel === 'email' ? 'primary' : ''} aria-pressed={channel === 'email'} onClick={() => selectChannel('email')}>
            {t('auth.forgot.channel.email')}
          </button>
          <button type="button" className={channel === 'phone' ? 'primary' : ''} aria-pressed={channel === 'phone'} onClick={() => selectChannel('phone')}>
            {t('auth.forgot.channel.phone')}
          </button>
        </div>

        {step === 'done' ? (
          <section className="auth-confirm">
            <p>{t('auth.forgot.phone.done')}</p>
            <button type="button" className="auth-primary" onClick={onBackToSignIn}>{t('auth.forgot.phone.toSignIn')}</button>
          </section>
        ) : step === 'verify' ? (
          <form className="auth-form" onSubmit={submitReset}>
            <p className="auth-hint">{channel === 'email' ? t('auth.forgot.email.sent') : t('auth.forgot.phone.sent')}</p>
            <label className="auth-field">
              <span className="auth-field-label">{channel === 'email' ? t('auth.reset.field.token') : t('auth.forgot.phone.codeField')}</span>
              <input value={code} onChange={(e) => setCode(e.currentTarget.value)} inputMode="numeric" autoComplete="one-time-code" disabled={isBusy} />
            </label>
            <label className="auth-field">
              <span className="auth-field-label">{t('auth.forgot.phone.newPassword')}</span>
              <input type="password" value={newPassword} onChange={(e) => setNewPassword(e.currentTarget.value)} autoComplete="new-password" disabled={isBusy} />
            </label>
            <button type="submit" className="auth-primary" disabled={isBusy}>
              {isBusy ? t('auth.forgot.phone.resetting') : t('auth.forgot.phone.reset')}
            </button>
          </form>
        ) : channel === 'email' ? (
          <form className="auth-form" onSubmit={submitRequest}>
            <label className="auth-field">
              <span className="auth-field-label">{t('auth.forgot.email.field')}</span>
              <input value={emailLogin} onChange={(e) => setEmailLogin(e.currentTarget.value)} autoComplete="username" disabled={isBusy} autoFocus />
            </label>
            <button type="submit" className="auth-primary" disabled={isBusy}>
              {isBusy ? t('auth.forgot.email.submitting') : t('auth.forgot.email.submit')}
            </button>
          </form>
        ) : (
          <form className="auth-form" onSubmit={submitRequest}>
            <label className="auth-field">
              <span className="auth-field-label">{t('auth.forgot.phone.field')}</span>
              <div className="auth-phone-field">
                <span className="auth-phone-prefix" aria-hidden>+992</span>
                <input
                  className="auth-phone-input"
                  type="tel"
                  inputMode="tel"
                  value={phone}
                  onChange={(e) => setPhone(formatLocal(e.currentTarget.value))}
                  placeholder="93 738 00 70"
                  autoComplete="tel"
                  disabled={isBusy}
                  autoFocus
                />
              </div>
            </label>
            <button type="submit" className="auth-primary" disabled={isBusy}>
              {isBusy ? t('auth.forgot.phone.submitting') : t('auth.forgot.phone.submit')}
            </button>
          </form>
        )}

        {error && (
          <div className="auth-error" role="alert">
            <AlertTriangle size={16} aria-hidden />
            <span>{error}</span>
          </div>
        )}

        <button type="button" className="auth-link" onClick={onBackToSignIn}>{t('auth.forgot.back')}</button>
      </section>
    </AuthFrame>
  );
}

// Аутентификация идёт напрямую по HTTP (StaffAuthApi) — читаем разобранное тело ответа бэка
// (см. AuthEndpoints.cs: `{ error: 'invalid_code', remainingAttempts }` и т.п.), а не
// bridge-специфичный код нативного моста (тот путь тут больше не участвует).
function projectResetError(cause: unknown, t: (key: MessageKey) => string): string {
  if (cause instanceof StaffAuthApiError) {
    const body = cause.body;
    const code = isRecord(body) && typeof body.error === 'string' ? body.error : null;
    const remainingAttempts = isRecord(body) && typeof body.remainingAttempts === 'number' ? body.remainingAttempts : null;
    switch (code) {
      case 'invalid_phone':
        return t('auth.forgot.phone.error.invalidPhone');
      case 'invalid_code':
        return remainingAttempts === null
          ? t('auth.forgot.phone.error.invalidCode')
          : `${t('auth.forgot.phone.error.invalidCode')} ${t('auth.forgot.phone.error.remaining')}: ${remainingAttempts}`;
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
