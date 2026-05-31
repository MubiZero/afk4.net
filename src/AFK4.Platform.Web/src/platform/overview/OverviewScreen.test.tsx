import { describe, expect, it, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { OverviewScreen } from './OverviewScreen';
import type { TenantMetricsState } from './useTenantMetrics';
import type { BillingMetricsState } from '@/platform/billing/useBillingMetrics';

function wrap(state: TenantMetricsState, billing?: BillingMetricsState) {
  return render(<I18nProvider><OverviewScreen state={state} billing={billing} /></I18nProvider>);
}

const ready: TenantMetricsState = {
  status: 'ready', retry: vi.fn(),
  data: {
    kpis: { totalTenants: 5, activeTenants: 3, suspendedTenants: 1, trialTenants: 1, totalBranches: 9, newTenants30d: 2 },
    byPlan: [{ planCode: 'starter', count: 3 }, { planCode: 'growth', count: 1 }, { planCode: 'scale', count: 1 }],
    attention: [{ organizationId: 'b', name: 'Beta', reason: 'suspended' }]
  }
};

describe('platform OverviewScreen', () => {
  it('renders KPI values when ready', () => {
    wrap(ready);
    expect(screen.getByText('Всего тенантов')).toBeInTheDocument();
    expect(screen.getByText('5')).toBeInTheDocument();
    expect(screen.getByText('Beta')).toBeInTheDocument();
  });

  it('shows a loading skeleton', () => {
    wrap({ status: 'loading', retry: vi.fn() });
    expect(screen.getByTestId('platform-overview-loading')).toBeInTheDocument();
  });

  it('shows an error with a working retry', () => {
    const retry = vi.fn();
    wrap({ status: 'error', message: 'x', retry });
    fireEvent.click(screen.getByText('Повторить'));
    expect(retry).toHaveBeenCalled();
  });

  it('shows the empty attention message when nothing needs attention', () => {
    wrap({ ...ready, data: { ...ready.data!, attention: [] } } as TenantMetricsState);
    expect(screen.getByText('Все тенанты в норме.')).toBeInTheDocument();
  });

  it('renders billing KPI tiles when billing is ready', () => {
    const billing: BillingMetricsState = {
      status: 'ready',
      data: { mrrMinorUnits: 580000, currencyCode: 'RUB', activeSubscriptions: 2, outstandingMinorUnits: 0, outstandingCount: 0, overdueMinorUnits: 0, overdueCount: 0 },
      retry: vi.fn()
    };
    wrap(ready, billing);
    expect(screen.getByText('MRR')).toBeInTheDocument();
  });
});
