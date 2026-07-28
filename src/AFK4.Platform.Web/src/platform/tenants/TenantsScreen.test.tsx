import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { TenantsScreen } from './TenantsScreen';
import type { TenantSummary } from '@/api/types';

function summary(over: Partial<TenantSummary>): TenantSummary {
  return {
    organizationId: 'o1', slug: 'acme', name: 'Acme', status: 'active', planCode: 'starter',
    subscriptionStatus: 'active', branchCount: 1, createdAtUtc: '2026-01-01T00:00:00Z',
    updatedAtUtc: '2026-01-01T00:00:00Z', ...over
  };
}
function client() {
  return {
    tenants: {
      listTenants: mock().mockResolvedValue([summary({ organizationId: 'o1', name: 'Acme' }), summary({ organizationId: 'o2', name: 'Globex', slug: 'globex' })]),
      getTenant: mock().mockResolvedValue({
        organizationId: 'o1', slug: 'acme', name: 'Acme', status: 'active', statusReason: null, statusChangedAtUtc: null,
        planCode: 'starter', subscriptionStatus: 'active',
        limits: { maxBranches: null, maxDevicesPerBranch: null, maxConcurrentSessions: null, maxStaffUsersPerBranch: null },
        branches: [], createdAtUtc: '2026-01-01T00:00:00Z', updatedAtUtc: '2026-01-01T00:00:00Z'
      }),
      updateStatus: mock(), updateLimits: mock(),
      getHealth: mock().mockResolvedValue({ organizationId: 'o1', status: 'active', branchCount: 0, deviceCount: 0, activeStaffUserCount: 0, latestStaffSignInAtUtc: null, latestMigration: null, recentErrorCount: 0, recentErrors: [] })
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
    organizationOwnerInvites: { listOrganizationOwnerInvites: mock().mockResolvedValue([]) },
    supportNotes: { listSupportNotes: mock().mockResolvedValue([]) }
  } as never;
}

function setup(props: Partial<Parameters<typeof TenantsScreen>[0]> = {}) {
  return render(
    <I18nProvider><ToastProvider>
      <TenantsScreen client={client()} selectedTenantId={null} initialInvite={null}
        onOpenTenant={() => {}} onCloseTenant={() => {}} onCreateTenant={() => {}} {...props} />
    </ToastProvider></I18nProvider>
  );
}

it('renders the tenant rows', async () => {
  setup();
  await waitFor(() => expect(screen.getByText('Acme')).toBeInTheDocument());
  expect(screen.getByText('Globex')).toBeInTheDocument();
});

it('filters rows by the search box', async () => {
  setup();
  await waitFor(() => expect(screen.getByText('Globex')).toBeInTheDocument());
  fireEvent.change(screen.getByLabelText('Поиск по названию или ключу'), { target: { value: 'globex' } });
  expect(screen.queryByText('Acme')).not.toBeInTheDocument();
  expect(screen.getByText('Globex')).toBeInTheDocument();
});

it('fires onOpenTenant when a row is clicked', async () => {
  const onOpenTenant = mock();
  setup({ onOpenTenant });
  await waitFor(() => expect(screen.getByText('Acme')).toBeInTheDocument());
  fireEvent.click(screen.getByText('Acme'));
  expect(onOpenTenant).toHaveBeenCalledWith('o1');
});

it('opens the drawer when selectedTenantId is set', async () => {
  setup({ selectedTenantId: 'o1' });
  await waitFor(() => expect(screen.getByText('Подписка')).toBeInTheDocument());
});
