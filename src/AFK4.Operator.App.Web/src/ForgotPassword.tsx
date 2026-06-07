import { useState, type FormEvent } from 'react';
import { AlertTriangle } from 'lucide-react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { forgotPasswordByEmail, forgotPasswordByPhone, resetPasswordByPhone } from './authClient';
import { HostBridgeRequestError } from './hostBridge';

type Channel = 'email' | 'phone';
type PhoneStep = 'request' | 'verify' | 'done';

export function ForgotPassword({
  onBackToSignIn,
  onOpenReset
}: {
  onBackToSignIn: () => void;
  onOpenReset: () => void;
}) {
  const { t } = useI18n();
  const [channel, setChannel] = useState<Channel>('email');
  const [emailLogin, setEmailLogin] = useState('');
  const [emailSent, setEmailSent] = useState(false);
  const [phone, setPhone] = useState('');
  const [phoneStep, setPhoneStep] = useState<PhoneStep>('request');
  const [code, setCode] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isBusy, setIsBusy] = useState(false);

  function selectChannel(next: Channel) {
    setChannel(next);
    setError(null);
  }

  async function submitEmail(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!emailLogin.trim()) { setError(t('auth.error.required')); return; }
    setIsBusy(true); setError(null);
    try {
      await forgotPasswordByEmail(emailLogin.trim());
      setEmailSent(true);
    } catch {
      setError(t('auth.forgot.email.error'));
    } finally {
      setIsBusy(false);
    }
  }

  async function submitPhoneRequest(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!phone.trim()) { setError(t('auth.error.required')); return; }
    setIsBusy(true); setError(null);
    try {
      await forgotPasswordByPhone(phone.trim());
      setPhoneStep('verify');
    } catch (cause) {
      setError(projectResetError(cause, t));
    } finally {
      setIsBusy(false);
    }
  }

  async function submitPhoneReset(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!code.trim() || newPassword.length < 8) { setError(t('auth.forgot.phone.error.fields')); return; }
    setIsBusy(true); setError(null);
    try {
      await resetPasswordByPhone(phone.trim(), code.trim(), newPassword);
      setPhoneStep('done');
    } catch (cause) {
      setError(projectResetError(cause, t));
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

          {channel === 'email' && (emailSent ? (
            <section className="auth-confirm">
              <p>{t('auth.forgot.email.sent')}</p>
              <button type="button" className="primary-wide" onClick={onOpenReset}>{t('auth.forgot.email.openReset')}</button>
              <button type="button" className="auth-link" onClick={onBackToSignIn}>{t('auth.forgot.back')}</button>
            </section>
          ) : (
            <form className="auth-form" onSubmit={submitEmail}>
              <label>
                {t('auth.forgot.email.field')}
                <input value={emailLogin} onChange={(e) => setEmailLogin(e.currentTarget.value)} autoComplete="username" disabled={isBusy} autoFocus />
              </label>
              <button type="submit" className="primary-wide" disabled={isBusy}>
                {isBusy ? t('auth.forgot.email.submitting') : t('auth.forgot.email.submit')}
              </button>
            </form>
          ))}

          {channel === 'phone' && (phoneStep === 'done' ? (
            <section className="auth-confirm">
              <p>{t('auth.forgot.phone.done')}</p>
              <button type="button" className="primary-wide" onClick={onBackToSignIn}>{t('auth.forgot.phone.toSignIn')}</button>
            </section>
          ) : phoneStep === 'verify' ? (
            <form className="auth-form" onSubmit={submitPhoneReset}>
              <p className="auth-hint">{t('auth.forgot.phone.sent')}</p>
              <label>
                {t('auth.forgot.phone.codeField')}
                <input value={code} onChange={(e) => setCode(e.currentTarget.value)} inputMode="numeric" autoComplete="one-time-code" disabled={isBusy} />
              </label>
              <label>
                {t('auth.forgot.phone.newPassword')}
                <input type="password" value={newPassword} onChange={(e) => setNewPassword(e.currentTarget.value)} autoComplete="new-password" disabled={isBusy} />
              </label>
              <button type="submit" className="primary-wide" disabled={isBusy}>
                {isBusy ? t('auth.forgot.phone.resetting') : t('auth.forgot.phone.reset')}
              </button>
            </form>
          ) : (
            <form className="auth-form" onSubmit={submitPhoneRequest}>
              <label>
                {t('auth.forgot.phone.field')}
                <input type="tel" value={phone} onChange={(e) => setPhone(e.currentTarget.value)} inputMode="tel" autoComplete="tel" disabled={isBusy} />
              </label>
              <button type="submit" className="primary-wide" disabled={isBusy}>
                {isBusy ? t('auth.forgot.phone.submitting') : t('auth.forgot.phone.submit')}
              </button>
            </form>
          ))}

          {error && (
            <div className="auth-error" role="alert">
              <AlertTriangle size={16} />
              <span>{error}</span>
            </div>
          )}

          <button type="button" className="auth-link" onClick={onBackToSignIn}>{t('auth.forgot.back')}</button>
        </section>
      </main>
    </div>
  );
}

function projectResetError(cause: unknown, t: (key: MessageKey) => string): string {
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
