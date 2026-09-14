import { Check } from 'lucide-react';
import { Fragment } from 'react';
import { useI18n, type MessageKey } from '@afk4/i18n';

export type WizardStep =
  | 'phoneLogin'
  | 'forgotPassword'
  | 'branchSelection'
  | 'role'
  | 'branding'
  | 'staff'
  | 'hall'
  | 'tariff'
  | 'device'
  | 'finished';

const STEP_LABELS: Record<WizardStep, MessageKey> = {
  phoneLogin: 'setup.wizard.stepper.signIn',
  // Сброс пароля — ответвление от входа, своей позиции в степпере у него нет.
  forgotPassword: 'setup.wizard.stepper.signIn',
  branchSelection: 'setup.wizard.stepper.branch',
  role: 'setup.wizard.stepper.role',
  branding: 'setup.wizard.stepper.branding',
  staff: 'setup.wizard.stepper.staff',
  hall: 'setup.wizard.stepper.hall',
  tariff: 'setup.wizard.stepper.tariff',
  device: 'setup.wizard.stepper.device',
  finished: 'setup.wizard.stepper.done',
};

interface StepperProps {
  /// Шаги ИМЕННО ЭТОГО прогона, по порядку — см. visibleSteps. Раньше здесь был зашитый список
  /// из девяти позиций: на игровом ПК четыре из них не показывались никогда, а нумерация всё
  /// равно доходила до девяти.
  steps: readonly WizardStep[];
  current: WizardStep;
}

export function Stepper({ steps, current }: StepperProps) {
  const { t } = useI18n();
  // Сброс пароля живёт на позиции входа: экран есть, отдельного шага нет.
  const resolved = current === 'forgotPassword' ? 'phoneLogin' : current;
  const currentIndex = steps.indexOf(resolved);

  return (
    <ol className="wizard-stepper" aria-label={t('setup.wizard.stepper.label')}>
      {steps.map((step, index) => {
        const status = stateForIndex(index, currentIndex);
        const isLast = index === steps.length - 1;
        return (
          <Fragment key={step}>
            <li
              className={`wizard-stepper-item ${status}`}
              aria-current={status === 'active' ? 'step' : undefined}
            >
              <span className="wizard-stepper-dot" aria-hidden>
                {status === 'done' ? <Check size={14} strokeWidth={3} /> : index + 1}
              </span>
              <span className="wizard-stepper-label">{t(STEP_LABELS[step])}</span>
            </li>
            {!isLast && <span className="wizard-stepper-separator" aria-hidden />}
          </Fragment>
        );
      })}
    </ol>
  );
}

function stateForIndex(index: number, currentIndex: number): 'done' | 'active' | 'pending' {
  if (index < currentIndex) {
    return 'done';
  }
  if (index === currentIndex) {
    return 'active';
  }
  return 'pending';
}
