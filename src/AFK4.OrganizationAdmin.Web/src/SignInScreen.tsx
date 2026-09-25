import { AlertTriangle, ArrowLeft, ArrowRight, Loader2 } from 'lucide-react';
import { useEffect, useState, type FormEvent, type ReactNode } from 'react';
import { useI18n } from '@afk4/i18n';
import { PIN_LENGTH, StaffSignInStepNames, type StaffSignInStepName } from '@afk4/contracts';
import type { ClubChoice } from './authClient';
import { getOperatorConfig } from './operatorConfig';
import type { AuthStatus } from './operatorTypes';
import { isGuid } from './operatorHelpers';
import { AuthFrame } from './AuthFrame';
import { PinBoxes } from './auth/PinBoxes';
import { projectFirstSignInError, projectPinSignInError, projectNextStepError } from './auth/signInErrors';
import { localPhoneDigits, formatLocal, fullPhoneDigits } from './phoneFormat';

/**
 * Вход в два шага: сначала номер, потом шесть клеток.
 *
 * Что во втором шаге, решает сервер по номеру: у кого есть ПИН-код — ПИН-код; кого руководитель
 * только что добавил — код первого входа, потом новый ПИН-код дважды, и человек сразу внутри.
 * Код нужен, потому что номер не секрет: без него ПИН-код новому сотруднику назначил бы любой, кто
 * знает его номер. Руководитель видит код в Панели, когда добавляет сотрудника; SMS его дублирует.
 *
 * Вход по логину или почте — запасной путь для учётных записей без номера, например владельца,
 * заведённого по почте.
 */
type Step =
  | { kind: 'phone' }
  | { kind: 'login' }
  | { kind: 'pin'; via: 'phone' | 'login' }
  | { kind: 'inviteCode' }
  | { kind: 'newPin'; code: string }
  | { kind: 'repeatPin'; code: string; pin: string };

const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export interface SignInActions {
  nextStep: (phoneNumber: string) => Promise<StaffSignInStepName>;
  signInByPhone: (phoneNumber: string, pin: string) => Promise<void>;
  signInByLogin: (login: string, pin: string) => Promise<void>;
  checkInviteCode: (phoneNumber: string, code: string) => Promise<void>;
  acceptInvite: (phoneNumber: string, code: string, pin: string) => Promise<void>;
}

export function SignInScreen({
  config,
  authStatus,
  hostError,
  chooseClub,
  actions,
  onChooseClub,
  onCancelChooseClub,
  onForgotPassword
}: {
  config: ReturnType<typeof getOperatorConfig>;
  authStatus: AuthStatus;
  hostError: string | null;
  chooseClub: { clubs: ClubChoice[] } | null;
  actions: SignInActions;
  onChooseClub: (organizationId: string) => Promise<void>;
  onCancelChooseClub: () => void;
  /** Номер уже набран на первом шаге — восстановление открывается сразу по нему. */
  onForgotPassword: (localPhone: string | null) => void;
}) {
  const { t } = useI18n();
  const [step, setStep] = useState<Step>({ kind: 'phone' });
  const [phone, setPhone] = useState('');
  const [login, setLogin] = useState('');
  const [digits, setDigits] = useState('');
  const [touched, setTouched] = useState(false);
  const [isBusy, setIsBusy] = useState(false);
  const [error, setError] = useState<string | null>(hostError);
  const isChecking = authStatus === 'checking';

  useEffect(() => {
    setError(hostError);
  }, [hostError]);

  const phoneComplete = localPhoneDigits(phone).length === 9;
  const phoneNumber = fullPhoneDigits(phone);
  const trimmedLogin = login.trim();
  const loginLooksLikeBadEmail = trimmedLogin.includes('@') && !EMAIL_RE.test(trimmedLogin);
  const identityHint =
    step.kind === 'phone' && touched && phone.trim().length > 0 && !phoneComplete
      ? t('op.auth.hint.phone')
      : step.kind === 'login' && touched && loginLooksLikeBadEmail
        ? t('op.auth.hint.email')
        : null;

  const goTo = (next: Step) => {
    setStep(next);
    setDigits('');
    setError(null);
  };

  const backToIdentity = () => goTo(step.kind === 'pin' && step.via === 'login' ? { kind: 'login' } : { kind: 'phone' });

  // Панель, стоящая в клубе, входит только после подключения к клубу. В браузере клуба нет по
  // построению — его находит сервер по номеру или логину.
  const connectionReady = config.runtime === 'browser' || isGuid(config.organizationId?.trim() ?? '');

  const run = async (work: () => Promise<void>, onError: (cause: unknown) => void) => {
    setIsBusy(true);
    setError(null);
    try {
      await work();
    } catch (cause) {
      onError(cause);
    } finally {
      setIsBusy(false);
    }
  };

  const submitPhone = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setTouched(true);
    if (!connectionReady) {
      setError(t('op.auth.connectionMissing'));
      return;
    }
    if (!phoneComplete) {
      return; // подсказка под полем уже говорит, что номер неполный
    }

    void run(async () => {
      const next = await actions.nextStep(phoneNumber);
      if (next === StaffSignInStepNames.Pin) {
        goTo({ kind: 'pin', via: 'phone' });
      } else if (next === StaffSignInStepNames.InviteCode) {
        goTo({ kind: 'inviteCode' });
      } else {
        setError(projectNextStepError(next, t));
      }
    }, (cause) => setError(projectPinSignInError(cause, 'phone', t)));
  };

  const submitLogin = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setTouched(true);
    if (!connectionReady) {
      setError(t('op.auth.connectionMissing'));
      return;
    }
    if (trimmedLogin.length > 0 && !loginLooksLikeBadEmail) {
      goTo({ kind: 'pin', via: 'login' });
    }
  };

  const failPin = (message: string) => {
    setDigits('');
    setError(message);
  };

  const submitPin = (pin: string) => {
    if (step.kind !== 'pin') return;
    const via = step.via;
    void run(
      async () => {
        if (via === 'phone') {
          await actions.signInByPhone(phoneNumber, pin);
        } else {
          await actions.signInByLogin(trimmedLogin, pin);
          // Логин нашёлся в нескольких клубах — впереди выбор клуба; вернувшийся оттуда видит пустые клетки.
          setDigits('');
        }
      },
      (cause) => failPin(projectPinSignInError(cause, via, t))
    );
  };

  const submitInviteCode = (code: string) => {
    void run(
      async () => {
        await actions.checkInviteCode(phoneNumber, code);
        goTo({ kind: 'newPin', code });
      },
      (cause) => failPin(projectFirstSignInError(cause, t))
    );
  };

  const submitNewPin = (pin: string) => {
    if (step.kind !== 'newPin') return;
    goTo({ kind: 'repeatPin', code: step.code, pin });
  };

  const submitRepeatPin = (pin: string) => {
    if (step.kind !== 'repeatPin') return;
    if (pin !== step.pin) {
      setStep({ kind: 'newPin', code: step.code });
      failPin(t('op.auth.repeatPin.mismatch'));
      return;
    }
    const code = step.code;
    void run(
      () => actions.acceptInvite(phoneNumber, code, pin),
      (cause) => failPin(projectFirstSignInError(cause, t))
    );
  };

  const [isChoosingClub, setIsChoosingClub] = useState(false);
  const [chooseClubError, setChooseClubError] = useState<string | null>(null);

  const submitChooseClub = async (organizationId: string) => {
    setChooseClubError(null);
    setIsChoosingClub(true);
    try {
      await onChooseClub(organizationId);
    } catch (nextError) {
      setChooseClubError(projectPinSignInError(nextError, 'login', t));
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

          <ErrorAlert message={chooseClubError} />

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

  if (step.kind === 'phone' || step.kind === 'login') {
    const byPhone = step.kind === 'phone';
    return (
      <AuthPanel title={t('op.shell.signInTitle')} subtitle={t('op.auth.signInSubtitle')}>
        <form className="auth-form" onSubmit={byPhone ? submitPhone : submitLogin} noValidate>
          <label className="ui-field">
            <span className="ui-field-label">{byPhone ? t('op.auth.field.phone') : t('op.auth.field.credentials')}</span>
            <div className={byPhone ? 'ui-phone-field' : undefined}>
              {byPhone && <span className="ui-phone-prefix" aria-hidden>+992</span>}
              <input
                // key пересоздаёт поле на смене способа — сбрасывает автозаполнение и маску.
                key={step.kind}
                className={byPhone ? 'ui-phone-input' : undefined}
                type={byPhone ? 'tel' : 'text'}
                inputMode={byPhone ? 'tel' : undefined}
                value={byPhone ? phone : login}
                onChange={(event) => {
                  const value = event.currentTarget.value;
                  if (byPhone) setPhone(formatLocal(value));
                  else setLogin(value);
                  setError(null);
                }}
                onBlur={() => setTouched(true)}
                placeholder={byPhone ? '93 738 00 70' : 'name@example.com'}
                autoComplete="username"
                spellCheck={false}
                autoFocus
                disabled={isBusy}
                aria-invalid={identityHint !== null}
                aria-describedby={identityHint !== null ? 'operator-identity-hint' : undefined}
              />
            </div>
            {identityHint !== null && (
              <span id="operator-identity-hint" className="ui-field-hint">{identityHint}</span>
            )}
          </label>

          <ErrorAlert message={error} />

          <button type="submit" className="ui-btn ui-btn--primary ui-btn--block" disabled={isBusy || isChecking}>
            {isBusy ? <Loader2 className="ui-spinner" size={18} aria-hidden /> : null}
            <span>{t('op.auth.next')}</span>
            {isBusy ? null : <ArrowRight size={18} aria-hidden />}
          </button>

          {/* Запасной путь — тихая ссылка, а не вторая кнопка: основной вход по номеру. */}
          <button
            type="button"
            className="auth-link-inline auth-mode-switch"
            onClick={() => {
              setTouched(false);
              goTo(byPhone ? { kind: 'login' } : { kind: 'phone' });
            }}
            disabled={isBusy}
          >
            {byPhone ? t('op.auth.useCredentials') : t('op.auth.usePhone')}
          </button>
        </form>
      </AuthPanel>
    );
  }

  const identityLabel = step.kind === 'pin' && step.via === 'login' ? trimmedLogin : `+992 ${formatLocal(phone)}`;
  const screen = digitStepCopy(step, t);
  const errorId = 'operator-pin-error';

  return (
    <AuthPanel title={screen.title} subtitle={screen.subtitle}>
      <button type="button" className="auth-step-back" onClick={backToIdentity} disabled={isBusy}>
        <ArrowLeft size={14} aria-hidden />
        <span>{identityLabel}</span>
        <span className="auth-step-back-action">{t('op.auth.changeIdentity')}</span>
      </button>

      <form
        className="auth-form"
        noValidate
        onSubmit={(event) => {
          event.preventDefault();
          if (digits.length === PIN_LENGTH) screen.submit(digits);
        }}
      >
        <PinBoxes
          // Новый шаг — новое поле: менеджер паролей и автозаполнение не переносят цифры между шагами.
          key={step.kind}
          id={`operator-${step.kind}`}
          label={screen.title}
          value={digits}
          onChange={(value) => {
            setDigits(value);
            setError(null);
          }}
          onComplete={screen.submit}
          secret={step.kind !== 'inviteCode'}
          autoComplete={screen.autoComplete}
          invalid={error !== null}
          disabled={isBusy}
          describedBy={error !== null ? errorId : undefined}
        />

        <p className="auth-pin-status" aria-live="polite">
          {isBusy && (
            <>
              <Loader2 className="ui-spinner" size={16} aria-hidden />
              <span>{screen.busyLabel}</span>
            </>
          )}
        </p>

        <ErrorAlert id={errorId} message={error} />

        {step.kind === 'pin' && (
          <button
            type="button"
            className="auth-link-inline auth-mode-switch"
            onClick={() => onForgotPassword(step.via === 'phone' ? phone : null)}
            disabled={isBusy}
          >
            {t('auth.forgot.link')}
          </button>
        )}
      </form>
    </AuthPanel>
  );

  function digitStepCopy(current: Exclude<Step, { kind: 'phone' } | { kind: 'login' }>, translate: typeof t) {
    switch (current.kind) {
      case 'pin':
        return {
          title: translate('op.auth.pin.title'),
          subtitle: null,
          autoComplete: 'current-password' as const,
          busyLabel: translate('auth.action.signingIn'),
          submit: submitPin
        };
      case 'inviteCode':
        return {
          title: translate('op.auth.firstSignIn.codeTitle'),
          subtitle: translate('op.auth.firstSignIn.codeHint'),
          autoComplete: 'one-time-code' as const,
          busyLabel: translate('op.auth.firstSignIn.checking'),
          submit: submitInviteCode
        };
      case 'newPin':
        return {
          title: translate('op.auth.newPin.title'),
          subtitle: translate('op.auth.newPin.hint', { count: PIN_LENGTH }),
          autoComplete: 'new-password' as const,
          busyLabel: '',
          submit: submitNewPin
        };
      case 'repeatPin':
        return {
          title: translate('op.auth.repeatPin.title'),
          subtitle: translate('op.auth.repeatPin.hint'),
          autoComplete: 'new-password' as const,
          busyLabel: translate('auth.action.signingIn'),
          submit: submitRepeatPin
        };
    }
  }
}

function AuthPanel({ title, subtitle, children }: { title: string; subtitle: string | null; children: ReactNode }) {
  return (
    <AuthFrame>
      <section className="auth-panel">
        <header className="auth-panel-head">
          <img className="auth-brand-mark" src="/favicon.svg" alt="" aria-hidden />
          <h1>{title}</h1>
          {subtitle && <p>{subtitle}</p>}
        </header>
        {children}
      </section>
    </AuthFrame>
  );
}

function ErrorAlert({ id, message }: { id?: string; message: string | null }) {
  if (message === null) return null;
  return (
    <div id={id} className="ui-alert" role="alert">
      <AlertTriangle size={16} aria-hidden />
      <span>{message}</span>
    </div>
  );
}
