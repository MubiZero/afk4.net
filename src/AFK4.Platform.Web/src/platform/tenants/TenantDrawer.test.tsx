import { render, screen, waitFor } from '@testing-library/react';
import { it, expect, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { TenantDrawer } from './TenantDrawer';
import type { TenantDetail } from '@/api/types';

function detail(over: Partial<TenantDetail>): TenantDetail {
  return {
    organizationId: 'o1', slug: 'acme', name: 'Acme', status: 'active', statusReason: null,
    statusChangedAtUtc: null, planCode: 'starter', subscriptionStatus: 'active',
    limits: { maxBranches: null, maxDevicesPerBranch: null, maxConcurrentSessions: null, maxStaffUsersPerBranch: null },
    branches: [], createdAtUtc: '2026-01-01T00:00:00Z', updatedAtUtc: '2026-01-01T00:00:00Z', ...over
  };
}
function client() {
  return {
    tenants: {
      getTenant: mock().mockResolvedValue(detail({})),
      updateStatus: mock(), updateLimits: mock(),
      getHealth: mock().mockResolvedValue({
        organizationId: 'o1', status: 'active', branchCount: 0, deviceCount: 0, activeStaffUserCount: 0,
        latestStaffSignInAtUtc: null, latestMigration: null, recentErrorCount: 0, recentErrors: []
      })
    },
    subscriptions: {
      getSubscription: mock().mockResolvedValue({
        organizationId: 'o1', planCode: 'starter', billingInterval: 'monthly', status: 'active',
        cancelAtPeriodEnd: false, amountMinorUnits: 1000, currencyCode: 'RUB',
        currentPeriodStartUtc: '2026-01-01T00:00:00Z', currentPeriodEndUtc: '2026-02-01T00:00:00Z',
        nextInvoiceUtc: null
      }),
      updateSubscription: mock()
    },
    plans: { listPlans: mock().mockResolvedValue([]) },
    invoices: { listTenantInvoices: mock().mockResolvedValue([]), generateInvoice: mock() },
    ownerInvites: { listOwnerInvites: mock().mockResolvedValue([]) },
    supportNotes: { listSupportNotes: mock().mockResolvedValue([]) }
  } as never;
}

it('loads the tenant and renders the section headers', async () => {
  render(
    <I18nProvider><ToastProvider>
      <TenantDrawer client={client()} organizationId="o1" initialInvite={null} onChanged={() => {}} />
    </ToastProvider></I18nProvider>
  );
  await waitFor(() => expect(screen.getByText('Подписка')).toBeInTheDocument());
  expect(screen.getByText('Лимиты')).toBeInTheDocument();
});

it('shows an error state when the tenant fails to load', async () => {
  const base = client() as { tenants: Record<string, unknown> };
  base.tenants['getTenant'] = mock().mockRejectedValue(new Error('boom'));
  const c = base as never;
  render(
    <I18nProvider><ToastProvider>
      <TenantDrawer client={c} organizationId="o1" initialInvite={null} onChanged={() => {}} />
    </ToastProvider></I18nProvider>
  );
  await waitFor(() => expect(screen.getByText('Не удалось загрузить тенанта')).toBeInTheDocument());
});
