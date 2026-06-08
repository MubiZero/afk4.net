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

type Mode = 'phone' | 'email';

interface PhoneLoginScreenProps {
  onDiscovered(response: WizardDiscoverResponse): void;
  onForgotPassword(): void;
  initialMode?: Mode;
  initialIdentity?: string;
}

type RequestState =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'error'; message: string };

// E.164-ish: 9–15 digits after stripping +, spaces and dashes.
function normalizePhone(value: string): string {
  return value.replace(/[\s\-()]/g, '').replace(/^\+/, '');
}

export function PhoneLoginScreen({ onDiscovered, onForgotPassword, initialMode, initialIdentity }: PhoneLoginScreenProps) {
  const { t } = useI18n();
  const [mode, setMode] = useState<Mode>(initialMode ?? 'phone');
  const [phone, setPhone] = useState(initialMode === 'phone' ? (initialIdentity ?? '') : '');
  const [login, setLogin] = useState(initialMode === 'email' ? (initialIdentity ?? '') : '');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [touched, setTouched] = useState(false);
  // «Забыли пароль?» показываем только после первой неудачной попытки входа —
  // это путь восстановления, а не постоянный элемент, который грузит экран.
  const [authFailed, setAuthFailed] = useState(false);
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

  const normalizedPhone = normalizePhone(phone);
  const phoneValid = /^[0-9]{9,15}$/.test(normalizedPhone);
  const showPhoneHint = touched && phone.length > 0 && !phoneValid;
  const canSubmit = mode === 'phone'
    ? phoneValid && password.length > 0 && request.kind !== 'loading'
    : login.trim().length > 0 && password.length > 0 && request.kind !== 'loading';

  function clearError() {
    if (request.kind === 'error') setRequest({ kind: 'idle' });
  }

  function switchMode(next: Mode) {
    setMode(next);
    setTouched(false);
    setAuthFailed(false);
    clearError();
  }

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
      if (!canSubmit) {
        return;
      }
      setRequest({ kind: 'loading' });
      try {
        if (mode === 'phone') {
          await signInByPhone(normalizedPhone, password);
          await finishWithDiscovery();
          return;
        }
        const result = await signInByLogin(login.trim(), password);
        if (result.requiresClubChoice) {
          setClubChoices(result.clubs);
          setRequest({ kind: 'idle' });
          return;
        }
        await finishWithDiscovery();
      } catch (error) {
        const { message, reason } = describeError(error, t);
        if (reason === 'auth') setAuthFailed(true);
        setRequest({ kind: 'error', message });
      }
    },
    [canSubmit, finishWithDiscovery, login, mode, normalizedPhone, password, t],
  );

  const chooseClub = useCallback(
    async (organizationId: string) => {
      setRequest({ kind: 'loading' });
      try {
        await signInToClub(organizationId, login.trim(), password);
        await finishWithDiscovery();
      } catch (error) {
        const { message, reason } = describeError(error, t);
        if (reason === 'auth') setAuthFailed(true);
        setClubChoices(null);
        setRequest({ kind: 'error', message });
      }
    },
    [finishWithDiscovery, login, password, t],
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

  return (
    <section className="wizard-screen is-narrow is-static">
      <div className="wizard-screen-head">
        <span className="wizard-eyebrow">
          {t('setup.wizard.common.step')} 1
          <span className="wizard-eyebrow-context">{t('setup.wizard.phoneLogin.subtitle')}</span>
        </span>
        <h1>{t('setup.wizard.phoneLogin.title')}</h1>
      </div>

      <form className="wizard-form" onSubmit={submit} noValidate>
        {mode === 'phone' ? (
          <label className="wizard-field">
            <span className="wizard-field-label">{t('setup.wizard.phoneLogin.field.phone')}</span>
            <input
              type="tel"
              inputMode="tel"
              autoComplete="tel"
              autoFocus
              spellCheck={false}
              value={phone}
              onChange={(event) => { setPhone(event.target.value); clearError(); }}
              onBlur={() => setTouched(true)}
              placeholder="+992 93 738 00-70"
              aria-invalid={showPhoneHint}
              aria-describedby={showPhoneHint ? 'phone-hint' : undefined}
            />
            {showPhoneHint && (
              <span id="phone-hint" className="wizard-field-hint">
                {t('setup.wizard.phoneLogin.error.invalidPhone')}
              </span>
            )}
          </label>
        ) : (
          <label className="wizard-field">
            <span className="wizard-field-label">{t('setup.wizard.phoneLogin.field.login')}</span>
            <input
              type="text"
              autoComplete="username"
              autoFocus
              spellCheck={false}
              value={login}
              onChange={(event) => { setLogin(event.target.value); clearError(); }}
            />
          </label>
        )}

        <div className="wizard-field">
          <div className="wizard-field-label wizard-label-with-action">
            <label htmlFor="wizard-password-input">{t('setup.wizard.phoneLogin.field.password')}</label>
            {authFailed && (
              <button type="button" className="wizard-link-inline" onClick={onForgotPassword}>
                {t('setup.wizard.phoneLogin.action.forgotPassword')}
              </button>
            )}
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
      </form>

      <div className="wizard-alt-actions">
        {mode === 'phone' ? (
          <button type="button" className="wizard-link-action" onClick={() => switchMode('email')}>
            {t('setup.wizard.phoneLogin.action.useEmail')}
          </button>
        ) : (
          <button type="button" className="wizard-link-action" onClick={() => switchMode('phone')}>
            {t('setup.wizard.phoneLogin.action.usePhone')}
          </button>
        )}
      </div>
    </section>
  );
}

function describeError(
  error: unknown,
  t: (key: MessageKey) => string,
): { message: string; reason: 'auth' | 'other' } {
  if (isHostBridgeUnavailableError(error)) {
    return { message: t('setup.wizard.phoneLogin.error.bridgeMissing'), reason: 'other' };
  }
  // Backend returns 401 with no detail (no user enumeration) → one combined, honest message.
  // reason 'auth' surfaces the "forgot password?" recovery link.
  return { message: t('setup.wizard.phoneLogin.error.signInFailed'), reason: 'auth' };
}
