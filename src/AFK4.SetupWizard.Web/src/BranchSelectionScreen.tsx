import { ArrowLeft, ArrowRight, Building2 } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import type { WizardBranch } from './wizardApi';

interface BranchSelectionScreenProps {
  ownerName: string;
  branches: WizardBranch[];
  onSelect(branch: WizardBranch): void;
  onBack(): void;
}

export function BranchSelectionScreen({
  ownerName,
  branches,
  onSelect,
  onBack,
}: BranchSelectionScreenProps) {
  const { t } = useI18n();

  return (
    <section className="wizard-screen is-framed">
      <div className="wizard-screen-head">
        <span className="wizard-screen-context">{ownerName}</span>
        <div className="wizard-screen-title-row">
          <span className="wizard-screen-step" aria-hidden>2</span>
          <h1>{t('setup.wizard.branch.title')}</h1>
        </div>
        <p>{t('setup.wizard.branch.subtitle')}</p>
      </div>

      <div className="wizard-scroll">
      {branches.length === 0 ? (
        <div className="wizard-empty">
          <strong>{t('setup.wizard.branch.empty.title')}</strong>
          <span>{t('setup.wizard.branch.empty.body')}</span>
        </div>
      ) : (
        <div className="wizard-branch-list">
          {branches.map((branch) => {
            const seatCount = branch.seats.length;
            const freeCount = branch.freeSeatIds.length;
            return (
              <button
                key={branch.branchId}
                type="button"
                className="wizard-branch-card"
                onClick={() => onSelect(branch)}
              >
                <div className="wizard-branch-card-body">
                  <strong>{branch.branchName}</strong>
                  <span className="wizard-branch-slug">
                    <Building2 size={12} aria-hidden />
                    {branch.branchSlug}
                  </span>
                </div>
                <div className="wizard-branch-card-meta">
                  <div className="wizard-branch-stat">
                    <strong>{freeCount}</strong>
                    <span>
                      {t('setup.wizard.branch.meta.of')} {seatCount}{' '}
                      {t('setup.wizard.branch.meta.free')}
                    </span>
                  </div>
                  <ArrowRight size={18} aria-hidden className="wizard-branch-chevron" />
                </div>
              </button>
            );
          })}
        </div>
      )}
      </div>

      <div className="wizard-actions">
        <button type="button" className="wizard-secondary" onClick={onBack}>
          <ArrowLeft aria-hidden />
          <span>{t('setup.wizard.common.back')}</span>
        </button>
      </div>
    </section>
  );
}
