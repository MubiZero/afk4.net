import { useState } from 'react';
import { ArrowLeft, ArrowRight, Briefcase, Check, Monitor } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import type { WizardRole } from './wizardApi';

interface RoleScreenProps {
  ownerName: string;
  branchName: string;
  initialRole: WizardRole;
  onContinue(role: WizardRole): void;
  onBack(): void;
}

export function RoleScreen({
  ownerName,
  branchName,
  initialRole,
  onContinue,
  onBack,
}: RoleScreenProps) {
  const { t } = useI18n();
  const [role, setRole] = useState<WizardRole>(initialRole);

  return (
    <section className="wizard-screen is-narrow">
      <div className="wizard-screen-head">
        <span className="wizard-screen-context">{ownerName} · {branchName}</span>
        <div className="wizard-screen-title-row">
          <span className="wizard-screen-step" aria-hidden>3</span>
          <h1>{t('setup.wizard.role.title')}</h1>
        </div>
        <p>{t('setup.wizard.role.subtitle')}</p>
      </div>

      <div
        className="wizard-segment wizard-segment-stack"
        role="radiogroup"
        aria-label={t('setup.wizard.role.aria')}
      >
        <button
          type="button"
          role="radio"
          className="wizard-segment-button"
          aria-checked={role === 'gaming_pc'}
          aria-pressed={role === 'gaming_pc'}
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
          aria-pressed={role === 'manager_workstation'}
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

      <div className="wizard-actions">
        <button type="button" className="wizard-secondary" onClick={onBack}>
          <ArrowLeft aria-hidden />
          <span>{t('setup.wizard.common.back')}</span>
        </button>
        <button
          type="button"
          className="wizard-primary"
          onClick={() => onContinue(role)}
        >
          <span>{t('setup.wizard.common.continue')}</span>
          <ArrowRight aria-hidden />
        </button>
      </div>
    </section>
  );
}
