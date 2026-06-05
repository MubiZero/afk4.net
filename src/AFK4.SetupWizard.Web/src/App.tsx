import { useState } from 'react';
import { BrandMark } from './BrandMark';
import { OwnerCodeScreen } from './OwnerCodeScreen';
import { getBootstrapConfig, type WizardDiscoverResponse } from './wizardApi';

type WizardStep = 'ownerCode' | 'branchSelection' | 'seatSelection' | 'roleSelection' | 'finished';

interface WizardState {
  step: WizardStep;
  discover?: WizardDiscoverResponse;
}

export function App() {
  const [state, setState] = useState<WizardState>({ step: 'ownerCode' });
  const config = getBootstrapConfig();

  const handleDiscovered = (discover: WizardDiscoverResponse) => {
    setState({ step: 'branchSelection', discover });
  };

  return (
    <div className="wizard-shell">
      <header className="wizard-header">
        <div className="wizard-brand">
          <BrandMark className="wizard-brand-logo" />
          <div className="wizard-brand-text">
            <strong>
              AFK4<span className="wizard-brand-accent">.NET</span> Setup Wizard
            </strong>
            {config && <span className="wizard-brand-host">{config.platformBaseUrl}</span>}
          </div>
        </div>
        <div className="wizard-step-indicator" role="status">
          <span>Step</span>
          <strong>{stepNumber(state.step)} of 4</strong>
        </div>
      </header>

      <main className="wizard-body">
        {state.step === 'ownerCode' && <OwnerCodeScreen onDiscovered={handleDiscovered} />}
        {state.step !== 'ownerCode' && (
          <section className="wizard-placeholder">
            <h2>Step {stepNumber(state.step)}: {stepLabel(state.step)}</h2>
            <p>Этот шаг ещё не перенесён на React. Скоро будет.</p>
          </section>
        )}
      </main>
    </div>
  );
}

function stepNumber(step: WizardStep): number {
  switch (step) {
    case 'ownerCode':
      return 1;
    case 'branchSelection':
      return 2;
    case 'seatSelection':
      return 3;
    case 'roleSelection':
      return 4;
    case 'finished':
      return 4;
  }
}

function stepLabel(step: WizardStep): string {
  switch (step) {
    case 'ownerCode':
      return 'Owner code';
    case 'branchSelection':
      return 'Branch';
    case 'seatSelection':
      return 'Seat';
    case 'roleSelection':
      return 'Role';
    case 'finished':
      return 'Done';
  }
}
