import { describe, expect, it, mock } from 'bun:test';
import { render, screen, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { OverviewScreen } from './OverviewScreen';
import type { OrganizationMetricsState } from './useOrganizationMetrics';
import type { BillingMetricsState } from '@/platform/billing/useBillingMetrics';

function wrap(state: OrganizationMetricsState, billing?: BillingMetricsState) {
  return render(<I18nProvider><OverviewScreen state={state} billing={billing} /></I18nProvider>);
}

const ready: OrganizationMetricsState = {
  status: 'ready', retry: mock(),
  data: {
    kpis: { totalOrganizations: 5, activeOrganizations: 3, suspendedOrganizations: 1, trialOrganizations: 1, totalBranches: 9, newOrganizations30d: 2 },
    byPlan: [{ planCode: 'starter', count: 3 }, { planCode: 'growth', count: 1 }, { planCode: 'scale', count: 1 }],
    attention: [{ organizationId: 'b', name: 'Beta', reason: 'suspended' }]
  }
};

describe('platform OverviewScreen', () => {
  it('renders KPI values when ready', () => {
    wrap(ready);
    expect(screen.getByText('Всего организаций')).toBeInTheDocument();
    expect(screen.getByText('5')).toBeInTheDocument();
    expect(screen.getByText('Beta')).toBeInTheDocument();
  });

  it('links attention rows to the affected organization section', () => {
    wrap(ready);
    expect(screen.getByRole('link', { name: /Beta/i })).toHaveAttribute(
      'href', '/admin/organizations/b?tab=clubs'
    );
  });

  it('shows a loading skeleton', () => {
    wrap({ status: 'loading', retry: mock() });
    expect(screen.getByTestId('platform-overview-loading')).toBeInTheDocument();
  });

  it('shows an error with a working retry', () => {
    const retry = mock();
    wrap({ status: 'error', message: 'x', retry });
    fireEvent.click(screen.getByText('Повторить'));
    expect(retry).toHaveBeenCalled();
  });

  it('shows the empty attention message when nothing needs attention', () => {
    wrap({ ...ready, data: { ...ready.data!, attention: [] } } as OrganizationMetricsState);
    expect(screen.getByText('Все организации в норме.')).toBeInTheDocument();
  });

  it('renders billing KPI tiles when billing is ready', () => {
    const billing: BillingMetricsState = {
      status: 'ready',
      data: { mrrMinorUnits: 580000, currencyCode: 'RUB', activeSubscriptions: 2, outstandingMinorUnits: 0, outstandingCount: 0, overdueMinorUnits: 0, overdueCount: 0 },
      retry: mock()
    };
    wrap(ready, billing);
    expect(screen.getByText('MRR')).toBeInTheDocument();
  });

  it('keeps organization signals visible when billing metrics fail', () => {
    const retry = mock();
    wrap(ready, { status: 'error', message: 'billing failed', retry });

    expect(screen.getByText('Beta')).toBeVisible();
    expect(screen.getByRole('status')).toBeVisible();
    fireEvent.click(screen.getByRole('button', { name: 'Повторить' }));
    expect(retry).toHaveBeenCalled();
  });
});
