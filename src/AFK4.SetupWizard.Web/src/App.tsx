import { useCallback, useEffect, useMemo, useState, type PointerEvent } from 'react';
import { Minus, Moon, Square, Sun, X } from 'lucide-react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { BrandMark } from './BrandMark';
import { BranchSelectionScreen } from './BranchSelectionScreen';
import { DeviceScreen } from './DeviceScreen';
import { FinishedScreen } from './FinishedScreen';
import { OwnerCodeScreen } from './OwnerCodeScreen';
import { PhoneLoginScreen } from './PhoneLoginScreen';
import { RoleScreen } from './RoleScreen';
import { Stepper, type WizardStep } from './Stepper';
import { postHostWindowCommand } from './hostBridge';
import {
  authenticatedInstallClient,
  getBootstrapConfig,
  ownerCodeInstallClient,
  type WizardBranch,
  type WizardDiscoverResponse,
  type WizardEnrollResult,
  type WizardInstallClient,
  type WizardRole,
  type WizardSeat,
} from './wizardApi';

type AuthMode = 'phone' | 'code';

interface WizardState {
  step: WizardStep;
  authMode: AuthMode;
  ownerCode: string;
  ownerName: string;
  branches: WizardBranch[];
  branch: WizardBranch | null;
  role: WizardRole;
  enrollResult: WizardEnrollResult | null;
  selectedSeat: WizardSeat | null;
}

const initialState: WizardState = {
  step: 'phoneLogin',
  authMode: 'phone',
  ownerCode: '',
  ownerName: '',
  branches: [],
  branch: null,
  role: 'gaming_pc',
  enrollResult: null,
  selectedSeat: null,
};

type Theme = 'light' | 'dark';

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
  const { t } = useI18n();
  const [state, setState] = useState<WizardState>(initialState);
  const [theme, setTheme] = useState<Theme>(readInitialTheme);
  const config = useMemo(() => getBootstrapConfig(), []);
  const defaultDisplayName = config?.machineName ?? 'AFK4-PC';

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme);
  }, [theme]);

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

  const applyDiscovery = useCallback(
    (authMode: AuthMode, ownerCode: string, response: WizardDiscoverResponse) => {
      const base = {
        authMode,
        ownerCode,
        ownerName: response.ownerName,
        branches: response.branches,
      } as const;
      if (response.branches.length === 1) {
        setState((prev) => ({ ...prev, ...base, branch: response.branches[0], step: 'role' }));
        return;
      }
      setState((prev) => ({ ...prev, ...base, branch: null, step: 'branchSelection' }));
    },
    [],
  );

  const handleCodeDiscovered = useCallback(
    (ownerCode: string, response: WizardDiscoverResponse) => applyDiscovery('code', ownerCode, response),
    [applyDiscovery],
  );

  const handlePhoneDiscovered = useCallback(
    (response: WizardDiscoverResponse) => applyDiscovery('phone', '', response),
    [applyDiscovery],
  );

  const goToOwnerCode = useCallback(() => {
    setState((prev) => ({ ...prev, step: 'ownerCode', authMode: 'code' }));
  }, []);

  const goToPhoneLogin = useCallback(() => {
    setState(initialState);
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
      step:
        prev.branches.length > 1
          ? 'branchSelection'
          : prev.authMode === 'phone'
            ? 'phoneLogin'
            : 'ownerCode',
      branch: prev.branches.length > 1 ? null : prev.branch,
    }));
  }, []);

  const installClient = useMemo<WizardInstallClient>(
    () => (state.authMode === 'phone' ? authenticatedInstallClient() : ownerCodeInstallClient(state.ownerCode)),
    [state.authMode, state.ownerCode],
  );

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
      ownerCode: 'setup.wizard.stepper.signIn',
      branchSelection: 'setup.wizard.stepper.branch',
      role: 'setup.wizard.stepper.role',
      device: 'setup.wizard.stepper.device',
      finished: 'setup.wizard.stepper.done',
    };
    const order = ['phoneLogin', 'branchSelection', 'role', 'device', 'finished'];
    const announceStep = state.step === 'ownerCode' ? 'phoneLogin' : state.step;
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
          <div
            className="wizard-theme-slider"
            role="group"
            aria-label={t('setup.wizard.titlebar.theme.aria')}
          >
            <button
              type="button"
              className="wizard-theme-slider-option"
              aria-pressed={theme === 'light'}
              aria-label={t('setup.wizard.titlebar.theme.light')}
              title={t('setup.wizard.titlebar.theme.light')}
              onClick={() => setThemeTo('light')}
            >
              <Sun size={14} aria-hidden />
            </button>
            <button
              type="button"
              className="wizard-theme-slider-option"
              aria-pressed={theme === 'dark'}
              aria-label={t('setup.wizard.titlebar.theme.dark')}
              title={t('setup.wizard.titlebar.theme.dark')}
              onClick={() => setThemeTo('dark')}
            >
              <Moon size={14} aria-hidden />
            </button>
            <span className={`wizard-theme-slider-thumb is-${theme}`} aria-hidden />
          </div>

          <span className="wizard-titlebar-divider" aria-hidden />

          <div className="wizard-window-controls">
            <button
              type="button"
              className="wizard-window-control"
              aria-label={t('setup.wizard.titlebar.minimize')}
              onClick={() => postHostWindowCommand('minimize')}
            >
              <Minus size={16} aria-hidden />
            </button>
            <button
              type="button"
              className="wizard-window-control"
              aria-label={t('setup.wizard.titlebar.maximize')}
              onClick={() => postHostWindowCommand('toggleMaximize')}
            >
              <Square size={13} aria-hidden />
            </button>
            <button
              type="button"
              className="wizard-window-control is-close"
              aria-label={t('setup.wizard.titlebar.close')}
              onClick={() => postHostWindowCommand('close')}
            >
              <X size={16} aria-hidden />
            </button>
          </div>
        </div>
      </header>

      <div role="status" aria-live="polite" className="wizard-sr-only">
        {stepAnnouncement}
      </div>

      <main className="wizard-body">
        {state.step === 'phoneLogin' && (
          <PhoneLoginScreen onDiscovered={handlePhoneDiscovered} onUseOwnerCode={goToOwnerCode} />
        )}

        {state.step === 'ownerCode' && (
          <OwnerCodeScreen onDiscovered={handleCodeDiscovered} onUsePhone={goToPhoneLogin} />
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
