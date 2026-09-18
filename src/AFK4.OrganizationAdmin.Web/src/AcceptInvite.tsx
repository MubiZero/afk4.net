import { useState, type FormEvent } from 'react';
import { AlertTriangle } from 'lucide-react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { PIN_LENGTH, isWellFormedPin, keepPinDigits } from '@afk4/contracts';
import { acceptStaffInvite, StaffAuthApiError } from './authClient';
import { AuthFrame } from './AuthFrame';
import { localPhoneDigits, formatLocal, fullPhoneDigits } from './phoneFormat';
import { isRecord } from './operatorHelpers';

/**
 * Приём приглашения: человека позвали работать, ему пришёл код, он придумывает себе ПИН-код.
 *
 * До этого экрана приглашение было тупиком: код выдавался, а принять его было негде, и клуб
 * заводил сотрудников скриптом с паролем, который знал не только их владелец.
 *
 * Форма в один шаг — код уже на руках, спрашивать его отдельным шагом незачем.
 */
export function AcceptInvite({ onBackToSignIn }: { onBackToSignIn: () => void }) {
  const { t } = useI18n();
  const [phone, setPhone] = useState('');
  const [code, setCode] = useState('');
  const [password, setPassword] = useState('');
  const [done, setDone] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isBusy, setIsBusy] = useState(false);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    if (localPhoneDigits(phone).length !== 9) { setError(t('op.auth.hint.phone')); return; }
    if (!code.trim() || !isWellFormedPin(password)) {
      setError(t('auth.invite.error.fields', { min: PIN_LENGTH }));
      return;
    }

    setIsBusy(true);
    try {
      await acceptStaffInvite(fullPhoneDigits(phone), code.trim(), password);
      setDone(true);
    } catch (cause) {
      setError(projectInviteError(cause, t));
    } finally {
      setIsBusy(false);
    }
  }

  return (
    <AuthFrame>
      <section className="auth-panel">
        <header className="auth-panel-head">
          <img className="auth-brand-mark" src="/favicon.svg" alt="" aria-hidden />
          <h1>{t('auth.invite.title')}</h1>
          <p>{t('auth.invite.subtitle')}</p>
        </header>

        {done ? (
          <section className="auth-confirm">
            <p>{t('auth.invite.done')}</p>
            <button type="button" className="ui-btn ui-btn--primary ui-btn--block" onClick={onBackToSignIn}>
              {t('auth.forgot.phone.toSignIn')}
            </button>
          </section>
        ) : (
          <form className="auth-form" onSubmit={submit}>
            <label className="ui-field">
              <span className="ui-field-label">{t('auth.forgot.phone.field')}</span>
              <div className="ui-phone-field">
                <span className="ui-phone-prefix" aria-hidden>+992</span>
                <input
                  className="ui-phone-input"
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
            <label className="ui-field">
              <span className="ui-field-label">{t('auth.invite.field.code')}</span>
              <input
                value={code}
                onChange={(e) => setCode(e.currentTarget.value)}
                inputMode="numeric"
                autoComplete="one-time-code"
                disabled={isBusy}
              />
            </label>
            <div className="ui-field">
              <label className="ui-field-label" htmlFor="invite-pin">{t('auth.invite.field.password')}</label>
              <input
                id="invite-pin"
                type="password"
                value={password}
                onChange={(e) => setPassword(keepPinDigits(e.currentTarget.value))}
                inputMode="numeric"
                maxLength={PIN_LENGTH}
                autoComplete="new-password"
                disabled={isBusy}
                aria-describedby="invite-pin-hint"
              />
              <span id="invite-pin-hint" className="ui-field-hint">{t('auth.forgot.newPassword.hint', { min: PIN_LENGTH })}</span>
            </div>
            <button type="submit" className="ui-btn ui-btn--primary ui-btn--block" disabled={isBusy}>
              {isBusy ? t('auth.invite.submitting') : t('auth.invite.submit')}
            </button>
          </form>
        )}

        {error && (
          <div className="ui-alert" role="alert">
            <AlertTriangle size={16} aria-hidden />
            <span>{error}</span>
          </div>
        )}

        <button type="button" className="auth-link" onClick={onBackToSignIn}>{t('auth.forgot.back')}</button>
      </section>
    </AuthFrame>
  );
}

// Ответы те же, что у восстановления пароля по телефону: тот же человек, те же слова.
function projectInviteError(cause: unknown, t: (key: MessageKey) => string): string {
  if (cause instanceof StaffAuthApiError) {
    const body = cause.body;
    const code = isRecord(body) && typeof body.error === 'string' ? body.error : null;
    const remainingAttempts = isRecord(body) && typeof body.remainingAttempts === 'number'
      ? body.remainingAttempts
      : null;
    switch (code) {
      case 'invalid_code':
        return remainingAttempts === null
          ? t('auth.forgot.phone.error.invalidCode')
          : `${t('auth.forgot.phone.error.invalidCode')} ${t('auth.forgot.phone.error.remaining')}: ${remainingAttempts}`;
      case 'code_expired':
        return t('auth.invite.error.expired');
      case 'too_many_attempts':
        return t('auth.forgot.phone.error.tooMany');
      default:
        return t('auth.invite.error.generic');
    }
  }
  return t('auth.invite.error.generic');
}
