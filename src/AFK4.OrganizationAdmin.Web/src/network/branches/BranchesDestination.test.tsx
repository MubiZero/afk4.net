import { describe, it, expect, mock, afterEach } from 'bun:test';
import { render, screen, cleanup, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';

afterEach(() => cleanup());

// MoneyDto over the wire is { currencyCode, minorUnits } — see branchRollupModel.test.ts note.
const summary = {
  utilization: { onlineDevices: 2, offlineDevices: 0, activeSessions: 1 },
  revenue: { totalRevenue: { minorUnits: 5000, currencyCode: 'TJS' } },
  alertPressure: { totalAlerts: 0 }
};

mock.module('../../operatorHelpers', () => ({
  createAuthenticatedOperatorClients: () => ({
    orgBranches: { getOwnerBranches: mock(async () => [{ branchId: 'b1', name: 'Центр' }]) },
    settings: { getBranchProfile: mock(async () => ({ name: 'Центр', city: 'Душанбе' })) },
    dashboard: { getSummary: mock(async () => summary) }
  }),
  dashboardRangeQuery: (from: string, to: string) => ({ fromUtc: from, toUtc: to, limit: 8 }),
  toDateInputValue: () => '2026-07-24'
}));

const backend = { config: { platformBaseUrl: 'x', currencyCode: 'TJS' }, session: { organizationId: 'org', accessToken: 't' }, branchId: 'b1' };

describe('BranchesDestination', () => {
  it('renders branch cards with the branch name', async () => {
    const { BranchesDestination } = await import('./BranchesDestination');
    render(
      <I18nProvider initialLocale="ru">
        <BranchesDestination backend={backend as never} />
      </I18nProvider>
    );
    await waitFor(() => expect(screen.getByText('Душанбе')).toBeInTheDocument());
    expect(screen.getByText('Центр')).toBeInTheDocument();
  });
});
