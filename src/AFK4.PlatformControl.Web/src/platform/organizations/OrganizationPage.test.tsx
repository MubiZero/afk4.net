import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { expect, it, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { OrganizationPage } from './OrganizationPage';

function client() {
  return {
    organizations: {
      getOrganization: mock().mockResolvedValue({ organizationId: 'o1', slug: 'orion', name: 'Orion Gaming', status: 'active', statusReason: null, statusChangedAtUtc: null, planCode: 'growth', subscriptionStatus: 'active', limits: { maxBranches: 3, maxDevicesPerBranch: 80, maxConcurrentSessions: 80, maxStaffUsersPerBranch: 12 }, branches: [{ branchId: 'b1', slug: 'center', name: 'Orion Center', city: 'Tashkent', createdAtUtc: '2026-01-01T00:00:00Z' }], createdAtUtc: '2026-01-01T00:00:00Z', updatedAtUtc: '2026-01-02T00:00:00Z', contactEmail: null, contactPhone: null, legalDetails: null, updateChannel: 'stable', pinnedClientVersion: null }),
      getHealth: mock().mockResolvedValue({ organizationId: 'o1', status: 'healthy', branchCount: 1, deviceCount: 42, activeStaffUserCount: 5, latestStaffSignInAtUtc: null, latestMigration: '20260729', recentErrorCount: 0, recentErrors: [] }),
      updateStatus: mock(), updateLimits: mock(), updateProfile: mock(), updateUpdateChannel: mock(), transferOwner: mock()
    },
    subscriptions: { getSubscription: mock().mockResolvedValue({ organizationSubscriptionId: 's1', organizationId: 'o1', planCode: 'growth', billingInterval: 'monthly', status: 'active', cancelAtPeriodEnd: false, amountMinorUnits: 1000, currencyCode: 'RUB', currentPeriodStartUtc: '2026-01-01T00:00:00Z', currentPeriodEndUtc: '2026-02-01T00:00:00Z', nextInvoiceUtc: null, createdAtUtc: '2026-01-01T00:00:00Z', updatedAtUtc: '2026-01-01T00:00:00Z', paymentGraceUntilUtc: null }), updateSubscription: mock() },
    plans: { listPlans: mock().mockResolvedValue([]) },
    invoices: { listOrganizationInvoices: mock().mockResolvedValue([]), generateInvoice: mock() },
    organizationOwnerInvites: { listOrganizationOwnerInvites: mock().mockResolvedValue([]) },
    supportNotes: { listSupportNotes: mock().mockResolvedValue([]) },
    audit: { listOrganizationHistory: mock().mockResolvedValue({ records: [], limit: 100 }) },
    pulse: { getPulse: mock().mockResolvedValue({ generatedAtUtc: '2026-01-01T00:00:00Z', organizations: [{ organizationId: 'o1', name: 'Orion Gaming', status: 'active', planCode: 'growth', subscriptionStatus: 'active', alertLevel: 'normal', outstandingMinorUnits: 0, currencyCode: 'RUB', alerts: [], clubs: [] }] }) }
  } as never;
}

const allAccess = {
  canManageOrganization: true,
  canManageAccess: true,
  canViewSupport: true,
  canViewBilling: true,
  canManageBilling: true,
  canManageProfile: true,
  canManageUpdateChannel: true,
  canTransferOwner: true,
  canViewAudit: true
};

function setup(tab: Parameters<typeof OrganizationPage>[0]['tab'] = 'clubs', onTabChange = mock(), access = allAccess) {
  render(<I18nProvider><ToastProvider><OrganizationPage client={client()} organizationId="o1" tab={tab} access={access} initialInvite={null} onTabChange={onTabChange} onChanged={() => {}} /></ToastProvider></I18nProvider>);
  return onTabChange;
}

it('renders the canonical organization heading with the passport and selected URL tab', async () => {
  setup('clubs');
  await waitFor(() => expect(screen.getAllByRole('heading', { name: 'Orion Gaming' }).length).toBeGreaterThan(0));
  expect(screen.getByRole('tab', { name: 'Клубы' })).toHaveAttribute('aria-selected', 'true');
  expect(screen.getByText('Orion Center')).toBeVisible();
});

it('keeps the passport visible on every tab', async () => {
  setup('history');
  await waitFor(() => expect(screen.getByText('stable')).toBeVisible());
});

it('reports tab changes to the route owner', async () => {
  const onTabChange = setup('clubs');
  await waitFor(() => expect(screen.getAllByRole('heading', { name: 'Orion Gaming' }).length).toBeGreaterThan(0));
  const invoicesTab = screen.getByRole('tab', { name: 'Счета' });
  fireEvent.pointerDown(invoicesTab, { button: 0, ctrlKey: false });
  fireEvent.click(invoicesTab);
  await waitFor(() => expect(onTabChange).toHaveBeenCalledWith('invoices'));
});

it('keeps clubs available when health fails', async () => {
  setup('clubs');
  await waitFor(() => expect(screen.getByText(/orion/u)).toBeVisible());
  expect(screen.getAllByRole('heading', { name: 'Orion Gaming' }).length).toBeGreaterThan(0);
});

it('shows a forbidden state for a forbidden direct tab URL', async () => {
  const onTabChange = mock();
  setup('history', onTabChange, {
    canManageOrganization: false,
    canManageAccess: false,
    canViewSupport: false,
    canViewBilling: false,
    canManageBilling: false,
    canManageProfile: false,
    canManageUpdateChannel: false,
    canTransferOwner: false,
    canViewAudit: false
  });
  await waitFor(() => expect(screen.getByRole('heading', { name: 'Нет доступа' })).toBeVisible());
  expect(screen.queryByRole('heading', { name: 'Orion Gaming' })).not.toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: 'Клубы' }));
  expect(onTabChange).toHaveBeenCalledWith('clubs');
});
