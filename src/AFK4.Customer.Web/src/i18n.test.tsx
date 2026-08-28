import { it, expect, mock } from 'bun:test';
import { render, screen } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { DashboardScreen } from './screens/dashboard/DashboardScreen';
import { ToastProvider } from './components/ui/toast';
import type { PlayerApiClient } from './api/playerApi';
import { branchChoice } from './branch/branchChoice';

it('renders the dashboard in English under the en locale', async () => {
  const api = {
    getDashboard: mock().mockResolvedValue({ walletBalance: { currencyCode: 'TJS', minorUnits: 0 }, heldBalance: { currencyCode: 'TJS', minorUnits: 0 }, debtBalance: { currencyCode: 'TJS', minorUnits: 0 }, activeSession: null }),
    getTopUpIntents: mock().mockResolvedValue([])
  } as unknown as PlayerApiClient;
  render(
    <I18nProvider initialLocale="en">
      <ToastProvider>
        <DashboardScreen branch={branchChoice([], null, 'b1')} onChooseBranch={() => {}} api={api} displayName="Fedor" phoneVerified={false} />
      </ToastProvider>
    </I18nProvider>
  );
  expect(await screen.findByText('Wallet balance')).toBeInTheDocument();
});
