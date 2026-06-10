import { useState } from 'react';
import { CheckCircle2 } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import { closeWizard, provisionShell, type WizardEnrollResult, type WizardSeat, type WizardShellOutcome } from './wizardApi';

interface FinishedScreenProps {
  result: WizardEnrollResult;
  branchName: string;
  selectedSeat: WizardSeat | null;
}

export function FinishedScreen({ result, branchName, selectedSeat }: FinishedScreenProps) {
  const { t } = useI18n();
  const isPending = result.enrollmentState.toLowerCase() === 'pending';
  const roleLabel = result.role === 'gaming_pc'
    ? t('setup.wizard.role.gamingPc.title')
    : t('setup.wizard.role.managerWorkstation.title');

  return (
    <section className="wizard-screen">
      <div className="wizard-finished">
        <div className="wizard-finished-hero">
          {/* Бейдж только для успеха — зелёная галка совпадает со смыслом «готово».
              Для pending кругляш с часами противоречил надписи внизу, поэтому его нет. */}
          {!isPending && (
            <span className="wizard-finished-badge" aria-hidden>
              <CheckCircle2 size={32} />
            </span>
          )}
          <span className="wizard-eyebrow">
            {t('setup.wizard.common.step')} 5 · {t('setup.wizard.finished.done')}
          </span>
          <h1>
            {isPending
              ? t('setup.wizard.finished.pending.title')
              : t('setup.wizard.finished.ok.title')}
          </h1>
          <p>
            {isPending
              ? t('setup.wizard.finished.pending.body')
              : t('setup.wizard.finished.ok.body')}
          </p>
        </div>

        <dl className="wizard-finished-summary">
          <div>
            <dt>{t('setup.wizard.finished.summary.branch')}</dt>
            <dd>{branchName}</dd>
          </div>
          <div>
            <dt>{t('setup.wizard.finished.summary.role')}</dt>
            <dd>{roleLabel}</dd>
          </div>
          {selectedSeat && (
            <div>
              <dt>{t('setup.wizard.finished.summary.seat')}</dt>
              <dd>{selectedSeat.pcName} · {selectedSeat.zoneName}</dd>
            </div>
          )}
          <div>
            <dt>{t('setup.wizard.finished.summary.name')}</dt>
            <dd>{result.displayName}</dd>
          </div>
          <div>
            <dt>{t('setup.wizard.finished.summary.channel')}</dt>
            <dd>{result.updateChannel}</dd>
          </div>
        </dl>

        {result.role === 'gaming_pc' && (
          <ShellStatusRow initial={result.shell} />
        )}

        {isPending && (
          <div className="wizard-pending-note" role="status">
            {t('setup.wizard.finished.pendingNote')}
          </div>
        )}

        <div className="wizard-actions is-end">
          <button type="button" className="wizard-primary wizard-finished-close" onClick={closeWizard}>
            <span>{t('setup.wizard.finished.close')}</span>
          </button>
        </div>
      </div>
    </section>
  );
}

function ShellStatusRow({ initial }: { initial: WizardShellOutcome }) {
  const { t } = useI18n();
  const [outcome, setOutcome] = useState(initial);
  const [busy, setBusy] = useState(false);

  if (outcome.status === 'installed' || outcome.status === 'already_present') {
    return (
      <div className="wizard-shell-status is-ok" role="status">
        {t('setup.wizard.finished.shell.ok')}
      </div>
    );
  }

  if (outcome.status === 'skipped') {
    return null;
  }

  return (
    <div className="wizard-shell-status is-error" role="alert">
      <span>
        {t('setup.wizard.finished.shell.failed')}
        {outcome.exitCode !== null ? ` (msiexec ${outcome.exitCode})` : ''}
      </span>
      <button
        type="button"
        className="wizard-secondary"
        disabled={busy}
        onClick={async () => {
          setBusy(true);
          try {
            setOutcome(await provisionShell());
          } finally {
            setBusy(false);
          }
        }}
      >
        {busy ? t('setup.wizard.finished.shell.installing') : t('setup.wizard.finished.shell.retry')}
      </button>
    </div>
  );
}
