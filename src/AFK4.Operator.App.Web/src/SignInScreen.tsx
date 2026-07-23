import { AlertTriangle, ArrowRight, Eye, EyeOff, Loader2 } from 'lucide-react';
import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { useI18n } from '@afk4/i18n';
import type { ClubChoice } from './authClient';
import { getOperatorConfig } from './operatorConfig';
import type { AuthStatus } from './operatorTypes';
import { projectAuthSignInError, isGuid } from './operatorHelpers';
import { AuthFrame } from './AuthFrame';
import { localPhoneDigits, formatLocal, fullPhoneDigits } from './phoneFormat';

// 'phone' — основной вход по номеру (телефон-first). 'credentials' — запасной по логину/email,
// открывается ссылкой «Вход по логину или почте». Поле одно, режим меняет маску и то, что
// уходит на бэк как userName. Логин сотрудника может совпасть в нескольких клубах — тогда бэк
// отвечает 409 (ChooseClubError) и форма уступает место списку клубов (см. `chooseClub` ниже).
type Mode = 'phone' | 'credentials';

const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export function SignInScreen({
  config,
  authStatus,
  hostError,
  chooseClub,
  onSignIn,
  onChooseClub,
  onCancelChooseClub,
  onForgotPassword
}: {
  config: ReturnType<typeof getOperatorConfig>;
  authStatus: AuthStatus;
  hostError: string | null;
  chooseClub: { clubs: ClubChoice[] } | null;
  onSignIn: (userName: string, password: string) => Promise<void>;
  onChooseClub: (organizationId: string) => Promise<void>;
  onCancelChooseClub: () => void;
  onForgotPassword: () => void;
}) {
  const { t } = useI18n();
  const [mode, setMode] = useState<Mode>('phone');
  const [identity, setIdentity] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [touched, setTouched] = useState(false);
  const [isBusy, setIsBusy] = useState(false);
  const [error, setError] = useState<string | null>(hostError);
  const isChecking = authStatus === 'checking';

  useEffect(() => {
    setError(hostError);
  }, [hostError]);

  const trimmed = identity.trim();
  const phoneComplete = localPhoneDigits(identity).length === 9;
  const emailLike = trimmed.includes('@');
  // Подсказку показываем только когда уверены в категории: незавершённый телефон или кривая почта.
  // Логин не валидируем — у него нет «правильного формата».
  const showPhoneHint = mode === 'phone' && touched && trimmed.length > 0 && !phoneComplete;
  const showEmailHint = mode === 'credentials' && emailLike && touched && !EMAIL_RE.test(trimmed);
  const identityHint = showEmailHint
    ? t('op.auth.hint.email')
    : showPhoneHint
      ? t('op.auth.hint.phone')
      : null;

  const switchMode = useCallback((next: Mode) => {
    // Меняем маску/роутинг — старое значение не переносим, иначе телефон-маска «протекает»
    // в поле логина и наоборот. Поле очищаем, ошибки и подсказки сбрасываем.
    setMode(next);
    setIdentity('');
    setTouched(false);
    setError(null);
  }, []);

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setTouched(true);
    setError(null);

    // Устройство должно быть привязано к клубу (сопряжение), прежде чем пробовать вход —
    // независимо от того, к каким клубам относится сам логин сотрудника (см. choose-club ниже).
    const organizationId = config.organizationId?.trim() ?? '';
    if (!isGuid(organizationId)) {
      setError(t('op.auth.connectionMissing'));
      return;
    }

    let userName: string;
    if (mode === 'phone') {
      if (!phoneComplete) {
        return; // подсказка под полем уже сообщает, что номер неполный — сеть не дёргаем
      }
      userName = fullPhoneDigits(identity);
    } else {
      if (!trimmed) {
        setError(t('auth.error.required'));
        return;
      }
      if (emailLike && !EMAIL_RE.test(trimmed)) {
        return; // кривая почта — показываем подсказку, не дёргаем сеть
      }
      userName = trimmed;
    }

    if (!password) {
      setError(t('auth.error.required'));
      return;
    }

    setIsBusy(true);
    try {
      await onSignIn(userName, password);
      setPassword('');
    } catch (nextError) {
      setError(projectAuthSignInError(nextError, t));
    } finally {
      setIsBusy(false);
    }
  };

  const [isChoosingClub, setIsChoosingClub] = useState(false);
  const [chooseClubError, setChooseClubError] = useState<string | null>(null);

  const submitChooseClub = async (organizationId: string) => {
    setChooseClubError(null);
    setIsChoosingClub(true);
    try {
      await onChooseClub(organizationId);
    } catch (nextError) {
      setChooseClubError(projectAuthSignInError(nextError, t));
    } finally {
      setIsChoosingClub(false);
    }
  };

  if (chooseClub !== null) {
    return (
      <AuthFrame>
        <section className="auth-panel">
          <header className="auth-panel-head">
            <img className="auth-brand-mark" src="/favicon.svg" alt="" aria-hidden />
            <h1>{t('auth.chooseClub.title')}</h1>
            <p>{t('auth.chooseClub.subtitle')}</p>
          </header>

          <div className="auth-club-list">
            {chooseClub.clubs.map((club) => (
              <button
                key={club.organizationId}
                type="button"
                className="auth-club-option"
                disabled={isChoosingClub}
                onClick={() => void submitChooseClub(club.organizationId)}
              >
                <span>{club.name}</span>
                <ArrowRight size={18} aria-hidden />
              </button>
            ))}
          </div>

          {chooseClubError && (
            <div className="auth-error" role="alert">
              <AlertTriangle size={16} aria-hidden />
              <span>{chooseClubError}</span>
            </div>
          )}

          <button
            type="button"
            className="auth-link-inline"
            disabled={isChoosingClub}
            onClick={() => {
              setChooseClubError(null);
              onCancelChooseClub();
            }}
          >
            {t('auth.chooseClub.back')}
          </button>
        </section>
      </AuthFrame>
    );
  }

  return (
    <AuthFrame>
      <section className="auth-panel">
        <header className="auth-panel-head">
          <img className="auth-brand-mark" src="/favicon.svg" alt="" aria-hidden />
          <h1>{t('op.shell.signInTitle')}</h1>
          <p>{t('op.auth.signInSubtitle')}</p>
        </header>

        <form className="auth-form" onSubmit={submit} noValidate>
          <label className="auth-field">
            <span className="auth-field-label">
              {mode === 'phone' ? t('op.auth.field.phone') : t('op.auth.field.credentials')}
            </span>
            <div className={mode === 'phone' ? 'auth-phone-field' : undefined}>
              {mode === 'phone' && <span className="auth-phone-prefix" aria-hidden>+992</span>}
              <input
                // key пересоздаёт инпут на смене режима — чисто сбрасывает autofill/IME-состояние.
                key={mode}
                className={mode === 'phone' ? 'auth-phone-input' : undefined}
                type={mode === 'phone' ? 'tel' : 'text'}
                inputMode={mode === 'phone' ? 'tel' : undefined}
                value={identity}
                onChange={(event) =>
                  setIdentity(mode === 'phone' ? formatLocal(event.currentTarget.value) : event.currentTarget.value)
                }
                onBlur={() => setTouched(true)}
                placeholder={mode === 'phone' ? '93 738 00 70' : 'name@example.com'}
                autoComplete="username"
                spellCheck={false}
                autoFocus
                aria-invalid={identityHint !== null}
                aria-describedby={identityHint !== null ? 'operator-identity-hint' : undefined}
              />
            </div>
            {identityHint !== null && (
              <span id="operator-identity-hint" className="auth-field-hint">{identityHint}</span>
            )}
          </label>

          <div className="auth-field">
            <div className="auth-field-label auth-label-with-action">
              <label htmlFor="operator-password">{t('auth.field.password')}</label>
              <button type="button" className="auth-link-inline" onClick={onForgotPassword}>
                {t('auth.forgot.link')}
              </button>
            </div>
            <div className="auth-password">
              <input
                id="operator-password"
                type={showPassword ? 'text' : 'password'}
                value={password}
                onChange={(event) => setPassword(event.currentTarget.value)}
                autoComplete="current-password"
              />
              <button
                type="button"
                className="auth-password-toggle"
                aria-pressed={showPassword}
                aria-label={showPassword ? t('op.auth.hidePassword') : t('op.auth.showPassword')}
                onClick={() => setShowPassword((value) => !value)}
              >
                {showPassword ? <EyeOff size={16} aria-hidden /> : <Eye size={16} aria-hidden />}
              </button>
            </div>
          </div>

          {error && (
            <div className="auth-error" role="alert">
              <AlertTriangle size={16} aria-hidden />
              <span>{error}</span>
            </div>
          )}

          <button type="submit" className="auth-primary" disabled={isBusy || isChecking}>
            {isBusy ? (
              <>
                <Loader2 className="auth-spinner" size={18} aria-hidden />
                <span>{t('auth.action.signingIn')}</span>
              </>
            ) : (
              <>
                <span>{t('auth.action.signIn')}</span>
                <ArrowRight size={18} aria-hidden />
              </>
            )}
          </button>

          {/* Запасной способ входа — тихая текстовая ссылка под «Войти», как «Забыли пароль?».
              Это второстепенный путь, а не вторая CTA, поэтому без иконки и веса secondary-кнопки. */}
          <button
            type="button"
            className="auth-link-inline auth-mode-switch"
            onClick={() => switchMode(mode === 'phone' ? 'credentials' : 'phone')}
            disabled={isBusy}
          >
            {mode === 'phone' ? t('op.auth.useCredentials') : t('op.auth.usePhone')}
          </button>
        </form>
      </section>
    </AuthFrame>
  );
}
