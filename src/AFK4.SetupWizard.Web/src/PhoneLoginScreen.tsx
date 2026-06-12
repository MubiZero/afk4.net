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

type RequestState =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'error'; message: string };

// Strip phone punctuation and the leading + so the digits can be sent to the backend.
function normalizePhone(value: string): string {
  return value.replace(/[\s\-()]/g, '').replace(/^\+/, '');
}

// Tajikistan numbers are country code 992 + 9 local digits. The wizard ships only to TJ clubs, so we
// pin +992 and only ever keep the 9 local digits — a country code typed by the user is dropped.
function localPhoneDigits(value: string): string {
  const digits = value.replace(/\D/g, '');
  // A leading "+" followed by only a partial/whole country code and nothing else is the prefix being
  // formed or deleted (e.g. backspacing "+992" down to "+99"). There are no local digits yet, so
  // don't resurrect the country-code digits as local ones — that was the "+992 99" bug.
  if (value.trimStart().startsWith('+') && digits.length <= 3 && '992'.startsWith(digits)) {
    return '';
  }
  return (digits.startsWith('992') ? digits.slice(3) : digits).slice(0, 9);
}

// Mask the local part as "93 738 00 70" (2-3-2-2) behind a fixed +992 prefix. With no local digits
// the field is empty (not a bare "+992"), so the prefix can never get stuck mid-deletion.
function formatPhone(value: string): string {
  const local = localPhoneDigits(value);
  const groups = [local.slice(0, 2), local.slice(2, 5), local.slice(5, 7), local.slice(7, 9)].filter(Boolean);
  return groups.length > 0 ? `+992 ${groups.join(' ')}` : '';
}

// One field accepts a phone, a login, or an email. We only mask as a phone while the value has no
// letters and no '@' — the moment a letter appears it's a login/email and masking stops, so a login
// is never mangled (and the user is never scolded for typing one).
function isPhoneInput(value: string): boolean {
  return /\d/.test(value) && !/[a-zA-Z@]/.test(value);
}

type IdentityKind = 'empty' | 'phone' | 'email' | 'login';

function classifyIdentity(value: string): IdentityKind {
  const trimmed = value.trim();
  if (trimmed.length === 0) return 'empty';
  if (trimmed.includes('@')) return 'email';
  if (/[a-zA-Z]/.test(trimmed)) return 'login';
  if (/\d/.test(trimmed)) return 'phone';
  return 'login';
}

const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

function maskIdentity(raw: string): string {
  return isPhoneInput(raw) ? formatPhone(raw) : raw;
}

export function PhoneLoginScreen({ onDiscovered, onForgotPassword, initialIdentity }: PhoneLoginScreenProps) {
  const { t } = useI18n();
  const [identity, setIdentity] = useState(() => maskIdentity(initialIdentity ?? ''));
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  // Подсказки и «Забыли пароль?» показываем только после первого взаимодействия/неудачи —
  // это пути восстановления, а не постоянные элементы, которые грузят экран.
  const [touched, setTouched] = useState(false);
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

  const kind = classifyIdentity(identity);
  // Ругаем только когда уверены в категории: незавершённый телефон или кривая почта.
  // Логин не валидируем вообще — у него нет «правильного формата».
  const showPhoneHint = kind === 'phone' && touched && localPhoneDigits(identity).length !== 9;
  const showEmailHint = kind === 'email' && touched && !EMAIL_RE.test(identity.trim());
  const canSubmit = identity.trim().length > 0 && password.length > 0 && request.kind !== 'loading';

  function clearError() {
    if (request.kind === 'error') setRequest({ kind: 'idle' });
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
      if (identity.trim().length === 0 || password.length === 0 || request.kind === 'loading') {
        return;
      }
      const category = classifyIdentity(identity);
      // Не дёргаем сеть на заведомо неверной почте/незавершённом номере — показываем подсказку.
      if (category === 'email' && !EMAIL_RE.test(identity.trim())) return;
      if (category === 'phone' && localPhoneDigits(identity).length !== 9) return;
      setRequest({ kind: 'loading' });
      try {
        if (category === 'phone') {
          await signInByPhone(normalizePhone(identity), password);
          await finishWithDiscovery();
          return;
        }
        const result = await signInByLogin(identity.trim(), password);
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
    [finishWithDiscovery, identity, password, request.kind, t],
  );

  const chooseClub = useCallback(
    async (organizationId: string) => {
      setRequest({ kind: 'loading' });
      try {
        await signInToClub(organizationId, identity.trim(), password);
        await finishWithDiscovery();
      } catch (error) {
        const { message, reason } = describeError(error, t);
        if (reason === 'auth') setAuthFailed(true);
        setClubChoices(null);
        setRequest({ kind: 'error', message });
      }
    },
    [finishWithDiscovery, identity, password, t],
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
      <div className="wizard-screen-head">
        <span className="wizard-eyebrow">
          {t('setup.wizard.common.step')} 1
          <span className="wizard-eyebrow-context">{t('setup.wizard.phoneLogin.subtitle')}</span>
        </span>
        <h1>{t('setup.wizard.phoneLogin.title')}</h1>
      </div>

      <form className="wizard-form" onSubmit={submit} noValidate>
        <label className="wizard-field">
          <span className="wizard-field-label">{t('setup.wizard.phoneLogin.field.phone')}</span>
          <input
            type="text"
            autoComplete="username"
            autoFocus
            spellCheck={false}
            value={identity}
            onChange={(event) => { setIdentity(maskIdentity(event.target.value)); clearError(); }}
            onBlur={() => setTouched(true)}
            placeholder="+992 93 738 00 70"
            aria-invalid={identityHint !== null}
            aria-describedby={identityHint !== null ? 'identity-hint' : undefined}
          />
          {identityHint !== null && (
            <span id="identity-hint" className="wizard-field-hint">{identityHint}</span>
          )}
        </label>

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
