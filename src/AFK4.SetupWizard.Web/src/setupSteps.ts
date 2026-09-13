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

/// Форма конкретного прогона мастера: от неё зависит, какие шаги вообще будут показаны.
export interface WizardRunShape {
  /// Роль машины. `null` — пользователь ещё не дошёл до вопроса; тогда считаем по рабочему
  /// месту управляющего, то есть по самому длинному пути. Степпер, который после ответа
  /// становится короче, читается как «оказалось быстрее»; тот, который растёт, — как обман.
  role: WizardRole | null;
  /// Сколько филиалов у клуба: при одном выбирать не из чего и шаг не показывается.
  branchCount: number;
  setup: ClubSetupState;
}

/**
 * Шаги, которые в этом прогоне реально будут показаны, по порядку.
 *
 * Нужно затем, что степпер раньше всегда рисовал девять позиций с зашитыми номерами, включая
 * четыре, которых на игровом ПК не бывает никогда. Номера внутри экранов были зашиты отдельно и
 * с ним расходились: 'branding' и 'device' оба объявляли себя четвёртым шагом, потому что каждый
 * был пронумерован по своему пути.
 */
export function visibleSteps(shape: WizardRunShape): WizardStep[] {
  const steps: WizardStep[] = ['phoneLogin'];

  if (shape.branchCount > 1) {
    steps.push('branchSelection');
  }
  steps.push('role');

  if ((shape.role ?? 'manager_workstation') === 'manager_workstation') {
    for (const step of SETUP_STEPS) {
      if (!isDone(step, shape.setup)) {
        steps.push(step);
      }
    }
  }

  steps.push('device', 'finished');
  return steps;
}

/// Шаг, на который ведёт «Назад», либо null, если назад некуда. Раньше маршрут назад был
/// зашит парами ('hall' → 'staff' и так далее) и не знал о пропусках: в клубе, где сотрудники
/// уже заведены, «назад» с зала открывало пропущенный экран сотрудников.
export function previousVisibleStep(current: WizardStep, shape: WizardRunShape): WizardStep | null {
  const steps = visibleSteps(shape);
  const index = steps.indexOf(current);
  return index > 0 ? steps[index - 1] : null;
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
