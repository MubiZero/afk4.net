import { useState } from 'react';
import { ArrowRight, Briefcase, Check, Monitor } from 'lucide-react';
import { Button } from '@afk4/ui/react';
import { useI18n } from '@afk4/i18n';
import type { WizardRole } from './wizardApi';
import { handleRadioGroupKeys, radioTabIndex } from './radioGroup';
import { WizardStepLayout } from './WizardStepLayout';

// Порядок здесь и порядок кнопок ниже — одно и то же: по нему ходят стрелки.
const ROLES: readonly WizardRole[] = ['gaming_pc', 'manager_workstation'];

interface RoleScreenProps {
  /// Номер шага в ЭТОМ прогоне мастера: шаги пропускаются, зашитая цифра врала.
  stepNumber: number;
  ownerName: string;
  branchName: string;
  initialRole: WizardRole;
  onContinue(role: WizardRole): void;
  onBack(): void;
}

export function RoleScreen({
  stepNumber,
  ownerName,
  branchName,
  initialRole,
  onContinue,
  onBack,
}: RoleScreenProps) {
  const { t } = useI18n();
  const [role, setRole] = useState<WizardRole>(initialRole);

  return (
    <WizardStepLayout
      stepNumber={stepNumber}
      context={`${ownerName} · ${branchName}`}
      title={t('setup.wizard.role.title')}
      subtitle={t('setup.wizard.role.subtitle')}
      onBack={onBack}
      primary={(
        <Button variant="primary" onClick={() => onContinue(role)}>
          {t('setup.wizard.common.continue')}
          <ArrowRight size={16} aria-hidden />
        </Button>
      )}
    >
      <div
        className="wizard-segment wizard-segment-stack"
        role="radiogroup"
        aria-label={t('setup.wizard.role.aria')}
        onKeyDown={(event) => handleRadioGroupKeys(event, ROLES, role, (next) => setRole(next as WizardRole))}
      >
        <button
          type="button"
          role="radio"
          className="wizard-segment-button"
          aria-checked={role === 'gaming_pc'}
          tabIndex={radioTabIndex(0, ROLES.indexOf(role))}
          onClick={() => setRole('gaming_pc')}
        >
          <span className="wizard-segment-icon">
            <Monitor size={20} aria-hidden />
          </span>
          <span className="wizard-segment-body">
            <strong>{t('setup.wizard.role.gamingPc.title')}</strong>
            <span>{t('setup.wizard.role.gamingPc.body')}</span>
          </span>
          <span className="wizard-segment-check" aria-hidden>
            <Check size={14} strokeWidth={3} />
          </span>
        </button>
        <button
          type="button"
          role="radio"
          className="wizard-segment-button"
          aria-checked={role === 'manager_workstation'}
          tabIndex={radioTabIndex(1, ROLES.indexOf(role))}
          onClick={() => setRole('manager_workstation')}
        >
          <span className="wizard-segment-icon">
            <Briefcase size={20} aria-hidden />
          </span>
          <span className="wizard-segment-body">
            <strong>{t('setup.wizard.role.managerWorkstation.title')}</strong>
            <span>{t('setup.wizard.role.managerWorkstation.body')}</span>
          </span>
          <span className="wizard-segment-check" aria-hidden>
            <Check size={14} strokeWidth={3} />
          </span>
        </button>
      </div>

    </WizardStepLayout>
  );
}
