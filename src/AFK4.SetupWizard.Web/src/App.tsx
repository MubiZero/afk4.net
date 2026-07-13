import { useCallback, useEffect, useMemo, useRef, useState, type PointerEvent } from 'react';
import { Moon, Sun } from 'lucide-react';
import { useI18n, type Locale, type MessageKey } from '@afk4/i18n';
import { CloseIcon, MaximizeIcon, MinimizeIcon, RestoreIcon } from './WindowIcons';
import { BrandMark } from './BrandMark';
import { BranchSelectionScreen } from './BranchSelectionScreen';
import { DeviceScreen } from './DeviceScreen';
import { FinishedScreen } from './FinishedScreen';
import { ForgotPasswordScreen, type SignInPrefill } from './ForgotPasswordScreen';
import { PhoneLoginScreen } from './PhoneLoginScreen';
import { RoleScreen } from './RoleScreen';
import { Stepper, type WizardStep } from './Stepper';
import { postHostWindowCommand, postHostWindowTheme } from './hostBridge';
import {
  authenticatedInstallClient,
  getBootstrapConfig,
  type WizardBranch,
  type WizardDiscoverResponse,
  type WizardEnrollResult,
  type WizardInstallClient,
  type WizardRole,
  type WizardSeat,
} from './wizardApi';

interface WizardState {
  step: WizardStep;
  ownerName: string;
  branches: WizardBranch[];
  branch: WizardBranch | null;
  role: WizardRole;
  enrollResult: WizardEnrollResult | null;
  selectedSeat: WizardSeat | null;
  signInPrefill: SignInPrefill | null;
}

const initialState: WizardState = {
  step: 'phoneLogin',
  ownerName: '',
  branches: [],
  branch: null,
  role: 'gaming_pc',
  enrollResult: null,
  selectedSeat: null,
  signInPrefill: null,
};

type Theme = 'light' | 'dark';

// Кнопка языка циклит по локалям и показывает текущий код. Тоҷикӣ — это «TJ» по ISO 639,
// но код локали у нас 'tg', поэтому в UI показываем «TJ» как привычную метку таджикского.
const LOCALE_CYCLE: Locale[] = ['ru', 'en', 'tg'];
const LOCALE_CODE: Record<Locale, string> = { ru: 'RU', en: 'EN', tg: 'TJ' };

// Линейная позиция шага в потоке — задаёт направление перехода (вперёд/назад) для анимации
// смены экрана. forgotPassword — ответвление от входа, поэтому стоит между входом и филиалом.
const STEP_POSITION: Record<WizardStep, number> = {
  phoneLogin: 0,
  forgotPassword: 0.5,
  branchSelection: 1,
  role: 2,
  device: 3,
  finished: 4,
};

const THEME_STORAGE_KEY = 'afk4.setupWizard.theme';

function readInitialTheme(): Theme {
  if (typeof window === 'undefined') return 'dark';
  try {
    const stored = window.localStorage.getItem(THEME_STORAGE_KEY);
    if (stored === 'light' || stored === 'dark') return stored;
  } catch {
    // localStorage недоступен — игнорим
  }
  // Setup wizard ставят в киберклубе, часто ночью (правило 30 — контекст использования):
  // тёмная тема — дефолтный baseline, чтобы не бить белым экраном по глазам.
  // Явный выбор пользователя (localStorage) выше по приоритету и сохраняется.
  return 'dark';
}

export function App() {
  const { t, locale, setLocale } = useI18n();
  const [state, setState] = useState<WizardState>(initialState);
  const [theme, setTheme] = useState<Theme>(readInitialTheme);
  // Реальное состояние окна приходит из хоста (StateChanged), а не угадывается — иначе кнопка
  // «развернуть/восстановить» рассинхронится при разворачивании двойным кликом / Win+↑.
  const [isMaximized, setIsMaximized] = useState(false);
  // Идёт установка (msiexec на хосте) — на это время бережём пользователя от случайного закрытия.
  const [installing, setInstalling] = useState(false);
  const [confirmClose, setConfirmClose] = useState(false);
  const config = useMemo(() => getBootstrapConfig(), []);
  const defaultDisplayName = config?.machineName ?? 'AFK4-PC';

  // Направление перехода: сравниваем позицию нового шага с предыдущей (ref обновляется в effect
  // уже после коммита, поэтому на рендере смены экрана он ещё держит старую позицию).
  const stepPosition = STEP_POSITION[state.step];
  const prevPositionRef = useRef(stepPosition);
  const navDirection = stepPosition < prevPositionRef.current ? 'back' : 'forward';
  useEffect(() => {
    prevPositionRef.current = stepPosition;
  }, [stepPosition]);

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme);
    postHostWindowTheme(theme);
  }, [theme]);

  // Хост сообщает текущее состояние окна (развёрнуто/нет) — слушаем, чтобы иконка и подсказка
  // кнопки maximize всегда отражали реальность.
  useEffect(() => {
    const webview = window.chrome?.webview;
    if (!webview?.addEventListener || !webview.removeEventListener) return;
    const onMessage = (event: { data: unknown }) => {
      const data = event.data as { type?: string; maximized?: boolean } | undefined;
      if (data?.type === 'window:state') {
        setIsMaximized(Boolean(data.maximized));
      }
    };
    webview.addEventListener('message', onMessage);
    return () => webview.removeEventListener?.('message', onMessage);
  }, []);

  const setThemeTo = useCallback((next: Theme) => {
    setTheme(next);
    // Персистим только осознанный выбор пользователя — дефолт (dark) остаётся
    // «невыбранным», чтобы будущая смена дефолта могла примениться.
    try {
      window.localStorage.setItem(THEME_STORAGE_KEY, next);
    } catch {
      // persistence — best-effort
    }
  }, []);

  const cycleLanguage = useCallback(() => {
    const next = LOCALE_CYCLE[(LOCALE_CYCLE.indexOf(locale) + 1) % LOCALE_CYCLE.length];
    setLocale(next);
  }, [locale, setLocale]);

  // Закрытие во время установки требует подтверждения: первый клик «вооружает» кнопку, второй —
  // действительно прерывает. Вне установки закрываем сразу.
  const requestClose = useCallback(() => {
    if (installing && !confirmClose) {
      setConfirmClose(true);
      return;
    }
    postHostWindowCommand('close');
  }, [installing, confirmClose]);

  // Снимаем «взвод» кнопки закрытия, если установка закончилась или пользователь передумал (таймаут).
  useEffect(() => {
    if (!confirmClose) return;
    const timer = setTimeout(() => setConfirmClose(false), 4000);
    return () => clearTimeout(timer);
  }, [confirmClose]);

  useEffect(() => {
    if (!installing) setConfirmClose(false);
  }, [installing]);

  const handlePhoneDiscovered = useCallback((response: WizardDiscoverResponse) => {
    const base = { ownerName: response.ownerName, branches: response.branches } as const;
    if (response.branches.length === 1) {
      setState((prev) => ({ ...prev, ...base, branch: response.branches[0], step: 'role' }));
      return;
    }
    setState((prev) => ({ ...prev, ...base, branch: null, step: 'branchSelection' }));
  }, []);

  const goToPhoneLogin = useCallback((prefill?: SignInPrefill) => {
    setState({ ...initialState, signInPrefill: prefill ?? null });
  }, []);

  const goToForgotPassword = useCallback(() => {
    setState((prev) => ({ ...prev, step: 'forgotPassword' }));
  }, []);

  const handleSelectBranch = useCallback((branch: WizardBranch) => {
    setState((prev) => ({ ...prev, branch, step: 'role' }));
  }, []);

  const handleRoleContinue = useCallback((role: WizardRole) => {
    setState((prev) => ({ ...prev, role, step: 'device' }));
  }, []);

  const handleEnrolled = useCallback(
    (result: WizardEnrollResult, selectedSeat: WizardSeat | null) => {
      setState((prev) => ({ ...prev, enrollResult: result, selectedSeat, step: 'finished' }));
    },
    [],
  );

  const backToReset = useCallback(() => {
    setState(initialState);
  }, []);

  const backFromRole = useCallback(() => {
    setState((prev) => ({
      ...prev,
      step: prev.branches.length > 1 ? 'branchSelection' : 'phoneLogin',
      branch: prev.branches.length > 1 ? null : prev.branch,
    }));
  }, []);

  const installClient = useMemo<WizardInstallClient>(() => authenticatedInstallClient(), []);

  const backToRole = useCallback(() => {
    setState((prev) => ({ ...prev, step: 'role' }));
  }, []);

  const handleHeaderPointerDown = useCallback((event: PointerEvent<HTMLElement>) => {
    if (event.button !== 0) {
      return;
    }
    const interactive = (event.target as HTMLElement).closest(
      'button, input, select, textarea, a, [data-no-drag]',
    );
    if (interactive) {
      return;
    }
    event.preventDefault();
    postHostWindowCommand('drag');
  }, []);

  const stepAnnouncement = useMemo(() => {
    const stepLabelKey: Record<typeof state.step, MessageKey> = {
      phoneLogin: 'setup.wizard.stepper.signIn',
      forgotPassword: 'setup.wizard.stepper.signIn',
      branchSelection: 'setup.wizard.stepper.branch',
      role: 'setup.wizard.stepper.role',
      device: 'setup.wizard.stepper.device',
      finished: 'setup.wizard.stepper.done',
    };
    const order = ['phoneLogin', 'branchSelection', 'role', 'device', 'finished'];
    const announceStep = state.step === 'forgotPassword' ? 'phoneLogin' : state.step;
    const stepNumber = order.indexOf(announceStep) + 1;
    return `${t('setup.wizard.common.step')} ${stepNumber}: ${t(stepLabelKey[state.step])}`;
  }, [state.step, t]);

  return (
    <div className="wizard-shell">
      <header className="wizard-titlebar" onPointerDown={handleHeaderPointerDown}>
        <div className="wizard-brand">
          <BrandMark className="wizard-brand-logo" />
          <div className="wizard-brand-text">
            <strong>
              AFK4<span className="wizard-brand-accent">.NET</span>
            </strong>
            <span className="wizard-brand-product">{t('setup.wizard.titlebar.product')}</span>
          </div>
        </div>
        <div className="wizard-titlebar-stepper" data-no-drag>
          <Stepper current={state.step} />
        </div>
        <div className="wizard-titlebar-controls" data-no-drag>
          <button
            type="button"
            className="wizard-toolbtn"
            aria-label={t('setup.wizard.titlebar.language.aria')}
            title={t('setup.wizard.titlebar.language.aria')}
            onClick={cycleLanguage}
          >
            {LOCALE_CODE[locale]}
          </button>

          <button
            type="button"
            className="wizard-toolbtn"
            aria-label={
              theme === 'dark'
                ? t('setup.wizard.titlebar.themeToLight')
                : t('setup.wizard.titlebar.themeToDark')
            }
            title={
              theme === 'dark'
                ? t('setup.wizard.titlebar.themeToLight')
                : t('setup.wizard.titlebar.themeToDark')
            }
            onClick={() => setThemeTo(theme === 'dark' ? 'light' : 'dark')}
          >
            {theme === 'dark' ? <Moon size={15} aria-hidden /> : <Sun size={15} aria-hidden />}
          </button>

          <span className="wizard-titlebar-divider" aria-hidden />

          <div className="wizard-window-controls">
            <button
              type="button"
              className="wizard-window-control"
              aria-label={t('setup.wizard.titlebar.minimize')}
              title={t('setup.wizard.titlebar.minimize')}
              onClick={() => postHostWindowCommand('minimize')}
            >
              <MinimizeIcon />
            </button>
            <button
              type="button"
              className="wizard-window-control"
              aria-label={
                isMaximized
                  ? t('setup.wizard.titlebar.restore')
                  : t('setup.wizard.titlebar.maximize')
              }
              title={
                isMaximized
                  ? t('setup.wizard.titlebar.restore')
                  : t('setup.wizard.titlebar.maximize')
              }
              onClick={() => postHostWindowCommand('toggleMaximize')}
            >
              {isMaximized ? <RestoreIcon /> : <MaximizeIcon />}
            </button>
            <button
              type="button"
              className={`wizard-window-control is-close${confirmClose ? ' is-armed' : ''}`}
              aria-label={
                confirmClose
                  ? t('setup.wizard.titlebar.close.confirm')
                  : t('setup.wizard.titlebar.close')
              }
              title={
                installing
                  ? confirmClose
                    ? t('setup.wizard.titlebar.close.confirm')
                    : t('setup.wizard.titlebar.close.installing')
                  : t('setup.wizard.titlebar.close')
              }
              onClick={requestClose}
            >
              <CloseIcon />
            </button>
          </div>
        </div>
      </header>

      <div role="status" aria-live="polite" className="wizard-sr-only">
        {stepAnnouncement}
      </div>

      <main className="wizard-body" data-nav-dir={navDirection}>
        {state.step === 'phoneLogin' && (
          <PhoneLoginScreen
            onDiscovered={handlePhoneDiscovered}
            onForgotPassword={goToForgotPassword}
            initialIdentity={state.signInPrefill?.identity}
          />
        )}

        {state.step === 'forgotPassword' && (
          <ForgotPasswordScreen onBack={goToPhoneLogin} />
        )}

        {state.step === 'branchSelection' && (
          <BranchSelectionScreen
            ownerName={state.ownerName}
            branches={state.branches}
            onSelect={handleSelectBranch}
            onBack={backToReset}
          />
        )}

        {state.step === 'role' && state.branch && (
          <RoleScreen
            ownerName={state.ownerName}
            branchName={state.branch.branchName}
            initialRole={state.role}
            onContinue={handleRoleContinue}
            onBack={backFromRole}
          />
        )}

        {state.step === 'device' && state.branch && (
          <DeviceScreen
            installClient={installClient}
            ownerName={state.ownerName}
            branch={state.branch}
            role={state.role}
            defaultDisplayName={defaultDisplayName}
            onEnrolled={handleEnrolled}
            onBusyChange={setInstalling}
            onBack={backToRole}
          />
        )}

        {state.step === 'finished' && state.enrollResult && state.branch && (
          <FinishedScreen
            result={state.enrollResult}
            branchName={state.branch.branchName}
            selectedSeat={state.selectedSeat}
          />
        )}
      </main>
    </div>
  );
}
