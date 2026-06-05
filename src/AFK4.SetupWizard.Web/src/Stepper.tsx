import { Check } from 'lucide-react';
import { Fragment } from 'react';
import { useI18n, type MessageKey } from '@afk4/i18n';

export type WizardStep =
  | 'ownerCode'
  | 'branchSelection'
  | 'role'
  | 'device'
  | 'finished';

const STEPS: { id: WizardStep; index: number; labelKey: MessageKey }[] = [
  { id: 'ownerCode', index: 1, labelKey: 'setup.wizard.stepper.code' },
  { id: 'branchSelection', index: 2, labelKey: 'setup.wizard.stepper.branch' },
  { id: 'role', index: 3, labelKey: 'setup.wizard.stepper.role' },
  { id: 'device', index: 4, labelKey: 'setup.wizard.stepper.device' },
  { id: 'finished', index: 5, labelKey: 'setup.wizard.stepper.done' },
];

interface StepperProps {
  current: WizardStep;
}

export function Stepper({ current }: StepperProps) {
  const { t } = useI18n();
  const currentIndex = STEPS.findIndex((step) => step.id === current);

  return (
    <ol className="wizard-stepper" aria-label={t('setup.wizard.stepper.label')}>
      {STEPS.map((step, index) => {
        const status = stateForIndex(index, currentIndex);
        const isLast = index === STEPS.length - 1;
        return (
          <Fragment key={step.id}>
            <li
              className={`wizard-stepper-item ${status}`}
              aria-current={status === 'active' ? 'step' : undefined}
            >
              <span className="wizard-stepper-dot" aria-hidden>
                {status === 'done' ? <Check size={14} strokeWidth={3} /> : step.index}
              </span>
              <span className="wizard-stepper-label">{t(step.labelKey)}</span>
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
