import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { ArrowRight, Loader2 } from 'lucide-react';
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
  onUseOwnerCode(): void;
  onForgotPassword(): void;
}

type Mode = 'phone' | 'email';

type RequestState =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'error'; message: string };

// E.164-ish: 9–15 digits after stripping +, spaces and dashes.
function normalizePhone(value: string): string {
  return value.replace(/[\s\-()]/g, '').replace(/^\+/, '');
}

export function PhoneLoginScreen({ onDiscovered, onUseOwnerCode, onForgotPassword }: PhoneLoginScreenProps) {
  const { t } = useI18n();
  const [mode, setMode] = useState<Mode>('phone');
  const [phone, setPhone] = useState('');
  const [login, setLogin] = useState('');
  const [password, setPassword] = useState('');
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

  const normalizedPhone = normalizePhone(phone);
  const phoneValid = /^[0-9]{9,15}$/.test(normalizedPhone);
  const showPhoneHint = touched && phone.length > 0 && !phoneValid;
  const canSubmit = mode === 'phone'
    ? phoneValid && password.length > 0 && request.kind !== 'loading'
    : login.trim().length > 0 && password.length > 0 && request.kind !== 'loading';

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
        setRequest({ kind: 'error', message: messageForError(error, t) });
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
        setClubChoices(null);
        setRequest({ kind: 'error', message: messageForError(error, t) });
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
        <span className="wizard-eyebrow">{t('setup.wizard.common.step')} 1</span>
        <h1>{t('setup.wizard.phoneLogin.title')}</h1>
        <p>{t('setup.wizard.phoneLogin.subtitle')}</p>
      </div>

      <div className="wizard-segment" role="radiogroup" aria-label={t('setup.wizard.phoneLogin.title')}>
        <button
          type="button"
          role="radio"
          className="wizard-segment-button"
          aria-checked={mode === 'phone'}
          aria-pressed={mode === 'phone'}
          onClick={() => { setMode('phone'); clearError(); }}
        >
          <span className="wizard-segment-body"><strong>{t('setup.wizard.phoneLogin.mode.phone')}</strong></span>
        </button>
        <button
          type="button"
          role="radio"
          className="wizard-segment-button"
          aria-checked={mode === 'email'}
          aria-pressed={mode === 'email'}
          onClick={() => { setMode('email'); clearError(); }}
        >
          <span className="wizard-segment-body"><strong>{t('setup.wizard.phoneLogin.mode.email')}</strong></span>
        </button>
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
              placeholder="+992 93 738-00-70"
              aria-invalid={showPhoneHint}
              aria-describedby="phone-hint"
            />
            <span id="phone-hint" className="wizard-field-hint">
              {showPhoneHint
                ? t('setup.wizard.phoneLogin.error.invalidPhone')
                : t('setup.wizard.phoneLogin.hint.phone')}
            </span>
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

        <label className="wizard-field">
          <span className="wizard-field-label">{t('setup.wizard.phoneLogin.field.password')}</span>
          <input
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={(event) => { setPassword(event.target.value); clearError(); }}
            aria-describedby="password-hint"
          />
          <span id="password-hint" className="wizard-field-hint">
            {t('setup.wizard.phoneLogin.hint.password')}
          </span>
        </label>

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

        <button type="button" className="wizard-link-action" onClick={onForgotPassword}>
          {t('setup.wizard.phoneLogin.action.forgotPassword')}
        </button>

        <button type="button" className="wizard-link-action wizard-fallback-link" onClick={onUseOwnerCode}>
          {t('setup.wizard.phoneLogin.action.useCode')}
        </button>
      </form>
    </section>
  );
}

function messageForError(error: unknown, t: (key: MessageKey) => string): string {
  if (isHostBridgeUnavailableError(error)) {
    return t('setup.wizard.phoneLogin.error.bridgeMissing');
  }
  // Backend returns 401 with no detail (no user enumeration) → one combined, honest message.
  return t('setup.wizard.phoneLogin.error.signInFailed');
}
