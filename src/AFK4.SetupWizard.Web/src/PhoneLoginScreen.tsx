import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { ArrowRight, Loader2 } from 'lucide-react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import {
  discoverAuthenticated,
  signInByPhone,
  type WizardDiscoverResponse,
} from './wizardApi';
import { isHostBridgeUnavailableError } from './hostBridge';

interface PhoneLoginScreenProps {
  onDiscovered(response: WizardDiscoverResponse): void;
  onUseOwnerCode(): void;
}

type RequestState =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'error'; message: string };

// E.164-ish: 9–15 digits after stripping +, spaces and dashes.
function normalizePhone(value: string): string {
  return value.replace(/[\s\-()]/g, '').replace(/^\+/, '');
}

export function PhoneLoginScreen({ onDiscovered, onUseOwnerCode }: PhoneLoginScreenProps) {
  const { t } = useI18n();
  const [phone, setPhone] = useState('');
  const [password, setPassword] = useState('');
  const [touched, setTouched] = useState(false);
  const [request, setRequest] = useState<RequestState>({ kind: 'idle' });
  const [showSlowSkeleton, setShowSlowSkeleton] = useState(false);

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
  const canSubmit = phoneValid && password.length > 0 && request.kind !== 'loading';
  const showPhoneHint = touched && phone.length > 0 && !phoneValid;

  const submit = useCallback(
    async (event: FormEvent<HTMLFormElement>) => {
      event.preventDefault();
      setTouched(true);
      if (!phoneValid || password.length === 0 || request.kind === 'loading') {
        return;
      }
      setRequest({ kind: 'loading' });
      try {
        await signInByPhone(normalizedPhone, password);
        const response = await discoverAuthenticated();
        if (response.branches.length === 0) {
          setRequest({ kind: 'error', message: t('setup.wizard.phoneLogin.error.noBranches') });
          return;
        }
        onDiscovered(response);
      } catch (error) {
        setRequest({ kind: 'error', message: messageForError(error, t) });
      }
    },
    [normalizedPhone, onDiscovered, password, phoneValid, request.kind, t],
  );

  return (
    <section className="wizard-screen is-narrow is-static">
      <div className="wizard-screen-head">
        <span className="wizard-eyebrow">{t('setup.wizard.common.step')} 1</span>
        <h1>{t('setup.wizard.phoneLogin.title')}</h1>
        <p>{t('setup.wizard.phoneLogin.subtitle')}</p>
      </div>

      <form className="wizard-form" onSubmit={submit} noValidate>
        <label className="wizard-field">
          <span className="wizard-field-label">{t('setup.wizard.phoneLogin.field.phone')}</span>
          <input
            type="tel"
            inputMode="tel"
            autoComplete="tel"
            autoFocus
            spellCheck={false}
            value={phone}
            onChange={(event) => {
              setPhone(event.target.value);
              if (request.kind === 'error') setRequest({ kind: 'idle' });
            }}
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

        <label className="wizard-field">
          <span className="wizard-field-label">{t('setup.wizard.phoneLogin.field.password')}</span>
          <input
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={(event) => {
              setPassword(event.target.value);
              if (request.kind === 'error') setRequest({ kind: 'idle' });
            }}
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
