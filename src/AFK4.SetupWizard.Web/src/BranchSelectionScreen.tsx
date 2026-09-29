import { ArrowRight } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import type { WizardBranch } from './wizardApi';
import { WizardStepLayout } from './WizardStepLayout';

interface BranchSelectionScreenProps {
  /// Номер шага в ЭТОМ прогоне мастера: шаги пропускаются, зашитая цифра врала.
  stepNumber: number;
  ownerName: string;
  branches: WizardBranch[];
  onSelect(branch: WizardBranch): void;
  onBack(): void;
}

export function BranchSelectionScreen({
  stepNumber,
  ownerName,
  branches,
  onSelect,
  onBack,
}: BranchSelectionScreenProps) {
  const { t } = useI18n();

  return (
    <WizardStepLayout
      stepNumber={stepNumber}
      context={ownerName}
      title={t('setup.wizard.branch.title')}
      subtitle={t('setup.wizard.branch.subtitle')}
      onBack={onBack}
    >
      {branches.length === 0 ? (
        <div className="wizard-empty">
          <strong>{t('setup.wizard.branch.empty.title')}</strong>
          <span>{t('setup.wizard.branch.empty.body')}</span>
        </div>
      ) : (
        <div className="wizard-branch-list">
          {branches.map((branch) => (
            <button
              key={branch.branchId}
              type="button"
              className="wizard-branch-card"
              onClick={() => onSelect(branch)}
            >
              {/* Короткое имя филиала («main») здесь было второй строкой: человеку у ПК оно
                  ничего не говорит, выбирают по названию и по тому, сколько мест свободно. */}
              <span className="wizard-branch-card-body">
                <strong>{branch.branchName}</strong>
                <span>
                  {branch.seats.length === 0
                    ? t('setup.wizard.branch.meta.noSeats')
                    : t('setup.wizard.branch.meta.free', { free: branch.freeSeatIds.length, total: branch.seats.length })}
                </span>
              </span>
              <ArrowRight size={18} aria-hidden className="wizard-branch-chevron" />
            </button>
          ))}
        </div>
      )}
    </WizardStepLayout>
  );
}
