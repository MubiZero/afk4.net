import { describe, expect, it, mock } from 'bun:test';
import { render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { TenantSubscriptionSection } from './TenantSubscriptionSection';
import type { TenantSubscription } from '@/api/types';

const sub: TenantSubscription = {
  tenantSubscriptionId: 's', organizationId: 'o', planCode: 'starter', status: 'active',
  currentPeriodStartUtc: '2026-05-01T00:00:00Z', currentPeriodEndUtc: '2026-06-01T00:00:00Z',
  nextInvoiceUtc: '2026-06-01T00:00:00Z', amountMinorUnits: 290000, currencyCode: 'RUB',
  billingInterval: 'monthly', cancelAtPeriodEnd: false, createdAtUtc: '2026-05-01T00:00:00Z', updatedAtUtc: '2026-05-01T00:00:00Z'
};

function fakeClient(over: Record<string, unknown> = {}) {
  return {
    getSubscription: mock().mockResolvedValue(sub),
    updateSubscription: mock().mockResolvedValue({ ...sub, planCode: 'growth' }),
    listPlans: mock().mockResolvedValue([
      { planCode: 'starter', name: 'Starter', priceMinorUnits: 290000, currencyCode: 'RUB', billingInterval: 'monthly', maxBranches: null, maxDevicesPerBranch: null, maxConcurrentSessions: null, maxStaffUsersPerBranch: null, isActive: true, sortOrder: 0 },
      { planCode: 'growth', name: 'Growth', priceMinorUnits: 790000, currencyCode: 'RUB', billingInterval: 'monthly', maxBranches: null, maxDevicesPerBranch: null, maxConcurrentSessions: null, maxStaffUsersPerBranch: null, isActive: true, sortOrder: 1 }
    ]),
    ...over
  } as never;
}

describe('TenantSubscriptionSection', () => {
  it('loads and shows the current plan', async () => {
    render(<I18nProvider><ToastProvider><TenantSubscriptionSection client={fakeClient()} plans={fakeClient()} organizationId="o" /></ToastProvider></I18nProvider>);
    await waitFor(() => expect(screen.getByText('Подписка')).toBeInTheDocument());
  });
});
