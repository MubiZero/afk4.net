import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { OrganizationsScreen } from './OrganizationsScreen';
import type { OrganizationSummary } from '@/api/types';

function summary(over: Partial<OrganizationSummary>): OrganizationSummary {
  return {
    organizationId: 'o1', slug: 'acme', name: 'Acme', status: 'active', planCode: 'starter',
    subscriptionStatus: 'active', branchCount: 1, createdAtUtc: '2026-01-01T00:00:00Z',
    updatedAtUtc: '2026-01-01T00:00:00Z', ...over
  };
}
function client() {
  return {
    organizations: {
      listOrganizations: mock().mockResolvedValue([summary({ organizationId: 'o1', name: 'Acme' }), summary({ organizationId: 'o2', name: 'Globex', slug: 'globex' })]),
      getOrganization: mock().mockResolvedValue({
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
    invoices: { listOrganizationInvoices: mock().mockResolvedValue([]), generateInvoice: mock() },
    organizationOwnerInvites: { listOrganizationOwnerInvites: mock().mockResolvedValue([]) },
    supportNotes: { listSupportNotes: mock().mockResolvedValue([]) }
  } as never;
}

function setup(props: Partial<Parameters<typeof OrganizationsScreen>[0]> = {}) {
  return render(
    <I18nProvider><ToastProvider>
      <OrganizationsScreen client={client()} selectedOrganizationId={null} initialInvite={null}
        onOpenOrganization={() => {}} onCloseOrganization={() => {}} onCreateOrganization={() => {}} {...props} />
    </ToastProvider></I18nProvider>
  );
}

it('renders the organization rows', async () => {
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

it('fires onOpenOrganization when a row is clicked', async () => {
  const onOpenOrganization = mock();
  setup({ onOpenOrganization });
  await waitFor(() => expect(screen.getByText('Acme')).toBeInTheDocument());
  fireEvent.click(screen.getByText('Acme'));
  expect(onOpenOrganization).toHaveBeenCalledWith('o1');
});

it('opens the drawer when selectedOrganizationId is set', async () => {
  setup({ selectedOrganizationId: 'o1' });
  await waitFor(() => expect(screen.getByText('Подписка')).toBeInTheDocument());
});
