import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { ArrowRight, Eye, EyeOff, Loader2 } from 'lucide-react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import {
  discoverAuthenticated,
  signInByLogin,
  signInByPhone,
  signInToClub,
  type WizardClubChoice,
  type WizardDiscoverResponse,
} from './wizardApi';
import { isHostBridgeUnavailableError } from './hostBridge';

interface PhoneLoginScreenProps {
  onDiscovered(response: WizardDiscoverResponse): void;
  onForgotPassword(): void;
  initialIdentity?: string;
}

// 'phone' — основной вход по номеру (телефон-first). 'credentials' — запасной по логину/email,
// открывается кнопкой «Вход по логину или почте». Поле одно, но режим меняет и маску, и роутинг.
type Mode = 'phone' | 'credentials';

type RequestState =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'error'; message: string };

// Tajikistan numbers are country code 992 + 9 local digits. The wizard ships only to TJ clubs, so the
// +992 prefix is fixed (shown as a non-editable affix in the field) and the input holds ONLY the 9
// local digits — a country code typed/pasted by the user is dropped.
function localPhoneDigits(value: string): string {
  const digits = value.replace(/\D/g, '');
  return (digits.startsWith('992') ? digits.slice(3) : digits).slice(0, 9);
}

// Mask the local part as "93 738 00 70" (2-3-2-2). The +992 prefix lives outside the input, so the
// field value is the local part only (empty when nothing is typed).
function formatLocal(value: string): string {
  const local = localPhoneDigits(value);
  const groups = [local.slice(0, 2), local.slice(2, 5), local.slice(5, 7), local.slice(7, 9)].filter(Boolean);
  return groups.join(' ');
}

// Full dialable digits sent to the backend: fixed 992 country code + the 9 local digits.
function fullPhoneDigits(value: string): string {
  return `992${localPhoneDigits(value)}`;
}

const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

// Did the operator land here from "forgot password" with an email/login? Then open in credentials
// mode so the prefilled value isn't mangled by the phone mask.
function initialModeFor(identity: string | undefined): Mode {
  if (!identity) return 'phone';
  return /[a-zA-Z@]/.test(identity) ? 'credentials' : 'phone';
}

export function PhoneLoginScreen({ onDiscovered, onForgotPassword, initialIdentity }: PhoneLoginScreenProps) {
  const { t } = useI18n();
  const [mode, setMode] = useState<Mode>(() => initialModeFor(initialIdentity));
  const [identity, setIdentity] = useState(() =>
    initialModeFor(initialIdentity) === 'phone' ? formatLocal(initialIdentity ?? '') : (initialIdentity ?? ''),
  );
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [touched, setTouched] = useState(false);
  const [request, setRequest] = useState<RequestState>({ kind: 'idle' });
  const [showSlowSkeleton, setShowSlowSkeleton] = useState(false);
  const [clubChoices, setClubChoices] = useState<WizardClubChoice[] | null>(null);

  useEffect(() => {
    if (request.kind !== 'loading') {
      setShowSlowSkeleton(false);
      return;
    }
    const timer = setTimeout(() => setShowSlowSkeleton(true), 300);
    return () => clearTimeout(timer);
  }, [request.kind]);

  const trimmed = identity.trim();
  const phoneComplete = localPhoneDigits(identity).length === 9;
  const emailLike = trimmed.includes('@');
  // Ругаем только когда уверены в категории: незавершённый телефон или кривая почта.
  // Логин не валидируем — у него нет «правильного формата».
  const showPhoneHint = mode === 'phone' && touched && trimmed.length > 0 && !phoneComplete;
  const showEmailHint = mode === 'credentials' && emailLike && touched && !EMAIL_RE.test(trimmed);
  const identityReady = mode === 'phone' ? phoneComplete : trimmed.length > 0;
  const canSubmit = identityReady && password.length > 0 && request.kind !== 'loading';

  function clearError() {
    if (request.kind === 'error') setRequest({ kind: 'idle' });
  }

  const switchMode = useCallback((next: Mode) => {
    // Меняем маску/роутинг — старое значение не переносим, иначе телефон-маска «протекает»
    // в поле логина и наоборот. Поле очищаем, ошибки и подсказки сбрасываем.
    setMode(next);
    setIdentity('');
    setPassword('');
    setTouched(false);
    setRequest({ kind: 'idle' });
    setClubChoices(null);
  }, []);

  const finishWithDiscovery = useCallback(async () => {
    const response = await discoverAuthenticated();
    if (response.branches.length === 0) {
      setRequest({ kind: 'error', message: t('setup.wizard.phoneLogin.error.noBranches') });
      return;
    }
    onDiscovered(response);
  }, [onDiscovered, t]);

  const submit = useCallback(
    async (event: FormEvent<HTMLFormElement>) => {
      event.preventDefault();
      setTouched(true);
      if (!identityReady || password.length === 0 || request.kind === 'loading') {
        return;
      }
      // Не дёргаем сеть на заведомо неверной почте — показываем подсказку.
      if (mode === 'credentials' && emailLike && !EMAIL_RE.test(trimmed)) return;
      setRequest({ kind: 'loading' });
      try {
        if (mode === 'phone') {
          await signInByPhone(fullPhoneDigits(identity), password);
          await finishWithDiscovery();
          return;
        }
        const result = await signInByLogin(trimmed, password);
        if (result.requiresClubChoice) {
          setClubChoices(result.clubs);
          setRequest({ kind: 'idle' });
          return;
        }
        await finishWithDiscovery();
      } catch (error) {
        setRequest({ kind: 'error', message: describeError(error, t) });
      }
    },
    [emailLike, finishWithDiscovery, identity, identityReady, mode, password, request.kind, t, trimmed],
  );

  const chooseClub = useCallback(
    async (organizationId: string) => {
      setRequest({ kind: 'loading' });
      try {
        await signInToClub(organizationId, trimmed, password);
        await finishWithDiscovery();
      } catch (error) {
        setClubChoices(null);
        setRequest({ kind: 'error', message: describeError(error, t) });
      }
    },
    [finishWithDiscovery, password, t, trimmed],
  );

  if (clubChoices !== null) {
    return (
      <section className="wizard-screen is-narrow is-static">
        <div className="wizard-screen-head">
          <h1>{t('setup.wizard.phoneLogin.chooseClub.title')}</h1>
          <p>{t('setup.wizard.phoneLogin.chooseClub.subtitle')}</p>
        </div>

        {request.kind === 'error' && (
          <div role="alert" className="wizard-alert">{request.message}</div>
        )}

        <div className="wizard-segment wizard-segment-stack" role="group">
          {clubChoices.map((club) => (
            <button
              key={club.organizationId}
              type="button"
              className="wizard-segment-button"
              disabled={request.kind === 'loading'}
              onClick={() => void chooseClub(club.organizationId)}
            >
              <span className="wizard-segment-body"><strong>{club.name}</strong></span>
            </button>
          ))}
        </div>

        <button
          type="button"
          className="wizard-link-action wizard-fallback-link"
          onClick={() => { setClubChoices(null); setRequest({ kind: 'idle' }); }}
        >
          {t('setup.wizard.common.back')}
        </button>
      </section>
    );
  }

  const identityHint = showEmailHint
    ? t('setup.wizard.phoneLogin.hint.email')
    : showPhoneHint
      ? t('setup.wizard.phoneLogin.hint.phone')
      : null;

  return (
    <section className="wizard-screen is-narrow is-static">
      <div className="wizard-screen-head is-centered">
        <div className="wizard-screen-title-row">
          <span className="wizard-screen-step" aria-hidden>1</span>
          <h1>{t('setup.wizard.phoneLogin.title')}</h1>
        </div>
        <p>{t('setup.wizard.phoneLogin.subtitle')}</p>
      </div>

      <form className="wizard-form" onSubmit={submit} noValidate>
        <label className="wizard-field">
          <span className="wizard-field-label">
            {mode === 'phone'
              ? t('setup.wizard.phoneLogin.field.phone')
              : t('setup.wizard.phoneLogin.field.credentials')}
          </span>
          <div className={mode === 'phone' ? 'wizard-phone-field' : undefined}>
            {mode === 'phone' && (
              <span className="wizard-phone-prefix" aria-hidden>+992</span>
            )}
            <input
              // key forces a fresh input on mode switch — clears autofill/IME state cleanly.
              key={mode}
              className={mode === 'phone' ? 'wizard-phone-input' : undefined}
              type={mode === 'phone' ? 'tel' : 'text'}
              inputMode={mode === 'phone' ? 'tel' : undefined}
              autoComplete="username"
              autoFocus
              spellCheck={false}
              value={identity}
              onChange={(event) => {
                setIdentity(mode === 'phone' ? formatLocal(event.target.value) : event.target.value);
                clearError();
              }}
              onBlur={() => setTouched(true)}
              placeholder={mode === 'phone' ? '93 738 00 70' : 'name@example.com'}
              aria-invalid={identityHint !== null}
              aria-describedby={identityHint !== null ? 'identity-hint' : undefined}
            />
          </div>
          {identityHint !== null && (
            <span id="identity-hint" className="wizard-field-hint">{identityHint}</span>
          )}
        </label>

        <div className="wizard-field">
          <div className="wizard-field-label wizard-label-with-action">
            <label htmlFor="wizard-password-input">{t('setup.wizard.phoneLogin.field.password')}</label>
            <button type="button" className="wizard-link-inline" onClick={onForgotPassword}>
              {t('setup.wizard.phoneLogin.action.forgotPassword')}
            </button>
          </div>
          <div className="wizard-password">
            <input
              id="wizard-password-input"
              type={showPassword ? 'text' : 'password'}
              autoComplete="current-password"
              value={password}
              onChange={(event) => { setPassword(event.target.value); clearError(); }}
            />
            <button
              type="button"
              className="wizard-password-toggle"
              aria-pressed={showPassword}
              aria-label={showPassword
                ? t('setup.wizard.phoneLogin.action.hidePassword')
                : t('setup.wizard.phoneLogin.action.showPassword')}
              onClick={() => setShowPassword((v) => !v)}
            >
              {showPassword ? <EyeOff size={16} aria-hidden /> : <Eye size={16} aria-hidden />}
            </button>
          </div>
        </div>

        {showSlowSkeleton && (
          <div className="wizard-skeleton-list" aria-hidden>
            <div className="wizard-skeleton-card" />
            <div className="wizard-skeleton-card" />
          </div>
        )}

        {request.kind === 'error' && (
          <div role="alert" className="wizard-alert">
            {request.message}
          </div>
        )}

        <button type="submit" className="wizard-primary" disabled={!canSubmit}>
          {request.kind === 'loading' ? (
            <>
              <Loader2 className="wizard-spinner" aria-hidden />
              <span>{t('setup.wizard.phoneLogin.action.signingIn')}</span>
            </>
          ) : (
            <>
              <span>{t('setup.wizard.phoneLogin.action.signIn')}</span>
              <ArrowRight aria-hidden />
            </>
          )}
        </button>

        {/* Запасной способ входа — тихая текстовая ссылка под «Войти», как «Забыли пароль?».
            Без иконки и без веса secondary-кнопки: это второстепенный путь, а не вторая CTA. */}
        <button
          type="button"
          className="wizard-link-inline wizard-mode-switch"
          onClick={() => switchMode(mode === 'phone' ? 'credentials' : 'phone')}
          disabled={request.kind === 'loading'}
        >
          {mode === 'phone'
            ? t('setup.wizard.phoneLogin.action.useCredentials')
            : t('setup.wizard.phoneLogin.action.usePhone')}
        </button>
      </form>
    </section>
  );
}

function describeError(error: unknown, t: (key: MessageKey) => string): string {
  if (isHostBridgeUnavailableError(error)) {
    return t('setup.wizard.phoneLogin.error.bridgeMissing');
  }
  // Backend returns 401 with no detail (no user enumeration) → one combined, honest message.
  return t('setup.wizard.phoneLogin.error.signInFailed');
}
