import type { WizardBranch, WizardRole } from './wizardApi';
import type { WizardStep } from './Stepper';

/// Что в клубе уже настроено. Мастер ставится на каждый админский ПК, а настраивают клуб один раз:
/// на втором рабочем месте эти вопросы не помогают, а мешают — оформление можно затереть, а тариф
/// с тем же именем сервер вообще не примет, и человек увидит ошибку на ровном месте.
export interface ClubSetupState {
  brandingConfigured: boolean;
  branch: WizardBranch | null;
}

const SETUP_STEPS: readonly WizardStep[] = ['branding', 'staff', 'hall', 'tariff'];

function isDone(step: WizardStep, setup: ClubSetupState): boolean {
  switch (step) {
    case 'branding':
      return setup.brandingConfigured;
    case 'staff':
      return setup.branch?.hasStaffBesidesOwner === true;
    case 'hall':
      return (setup.branch?.seats.length ?? 0) > 0;
    case 'tariff':
      return setup.branch?.hasTariff === true;
    default:
      return true;
  }
}

/**
 * Следующий шаг настройки после `after`, либо 'device', когда настраивать больше нечего.
 * На игровом ПК настройка клуба не показывается вовсе: там ставят железо.
 */
export function nextSetupStep(after: WizardStep, role: WizardRole, setup: ClubSetupState): WizardStep {
  if (role !== 'manager_workstation') {
    return 'device';
  }

  const startIndex = after === 'role' ? 0 : SETUP_STEPS.indexOf(after) + 1;
  for (let index = startIndex; index < SETUP_STEPS.length; index += 1) {
    const step = SETUP_STEPS[index];
    if (!isDone(step, setup)) {
      return step;
    }
  }

  return 'device';
}
