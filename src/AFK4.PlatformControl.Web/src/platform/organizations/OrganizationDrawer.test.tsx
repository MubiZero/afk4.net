import { render, screen, waitFor } from '@testing-library/react';
import { it, expect, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { OrganizationDrawer } from './OrganizationDrawer';
import type { OrganizationDetail } from '@/api/types';

function detail(over: Partial<OrganizationDetail>): OrganizationDetail {
  return {
    organizationId: 'o1', slug: 'acme', name: 'Acme', status: 'active', statusReason: null,
    statusChangedAtUtc: null, planCode: 'starter', subscriptionStatus: 'active',
    limits: { maxBranches: null, maxDevicesPerBranch: null, maxConcurrentSessions: null, maxStaffUsersPerBranch: null },
    branches: [], createdAtUtc: '2026-01-01T00:00:00Z', updatedAtUtc: '2026-01-01T00:00:00Z', ...over
  };
}
function client() {
  return {
    organizations: {
      getOrganization: mock().mockResolvedValue(detail({})),
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
    invoices: { listOrganizationInvoices: mock().mockResolvedValue([]), generateInvoice: mock() },
    organizationOwnerInvites: { listOrganizationOwnerInvites: mock().mockResolvedValue([]) },
    supportNotes: { listSupportNotes: mock().mockResolvedValue([]) }
  } as never;
}

it('loads the organization and renders the section headers', async () => {
  render(
    <I18nProvider><ToastProvider>
      <OrganizationDrawer client={client()} organizationId="o1" initialInvite={null} onChanged={() => {}} />
    </ToastProvider></I18nProvider>
  );
  await waitFor(() => expect(screen.getByText('Подписка')).toBeInTheDocument());
  expect(screen.getByText('Лимиты')).toBeInTheDocument();
});

it('shows an error state when the organization fails to load', async () => {
  const base = client() as { organizations: Record<string, unknown> };
  base.organizations['getOrganization'] = mock().mockRejectedValue(new Error('boom'));
  const c = base as never;
  render(
    <I18nProvider><ToastProvider>
      <OrganizationDrawer client={c} organizationId="o1" initialInvite={null} onChanged={() => {}} />
    </ToastProvider></I18nProvider>
  );
  await waitFor(() => expect(screen.getByText('Не удалось загрузить организацию')).toBeInTheDocument());
});
