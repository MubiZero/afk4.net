import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { expect, it, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { OrganizationPage } from './OrganizationPage';

function client() {
  return {
    organizations: {
      getOrganization: mock().mockResolvedValue({ organizationId: 'o1', slug: 'orion', name: 'Orion Gaming', status: 'active', statusReason: null, statusChangedAtUtc: null, planCode: 'growth', subscriptionStatus: 'active', limits: { maxBranches: 3, maxDevicesPerBranch: 80, maxConcurrentSessions: 80, maxStaffUsersPerBranch: 12 }, branches: [{ branchId: 'b1', slug: 'center', name: 'Orion Center', city: 'Tashkent', createdAtUtc: '2026-01-01T00:00:00Z' }], createdAtUtc: '2026-01-01T00:00:00Z', updatedAtUtc: '2026-01-02T00:00:00Z' }),
      getHealth: mock().mockResolvedValue({ organizationId: 'o1', status: 'healthy', branchCount: 1, deviceCount: 42, activeStaffUserCount: 5, latestStaffSignInAtUtc: null, latestMigration: '20260729', recentErrorCount: 0, recentErrors: [] }),
      updateStatus: mock(), updateLimits: mock()
    },
    subscriptions: { getSubscription: mock().mockResolvedValue({ organizationId: 'o1', planCode: 'growth', billingInterval: 'monthly', status: 'active', cancelAtPeriodEnd: false, amountMinorUnits: 1000, currencyCode: 'RUB', currentPeriodStartUtc: '2026-01-01T00:00:00Z', currentPeriodEndUtc: '2026-02-01T00:00:00Z', nextInvoiceUtc: null }), updateSubscription: mock() },
    plans: { listPlans: mock().mockResolvedValue([]) },
    invoices: { listOrganizationInvoices: mock().mockResolvedValue([]), generateInvoice: mock() },
    organizationOwnerInvites: { listOrganizationOwnerInvites: mock().mockResolvedValue([]) },
    supportNotes: { listSupportNotes: mock().mockResolvedValue([]) },
    audit: { listOrganizationHistory: mock().mockResolvedValue({ records: [], limit: 100 }) }
  } as never;
}

const allAccess = { canManageOrganization: true, canManageAccess: true, canViewSupport: true, canViewBilling: true, canViewAudit: true };

function setup(tab: Parameters<typeof OrganizationPage>[0]['tab'] = 'summary', onTabChange = mock(), access = allAccess) {
  render(<I18nProvider><ToastProvider><OrganizationPage client={client()} organizationId="o1" tab={tab} access={access} initialInvite={null} onTabChange={onTabChange} onChanged={() => {}} /></ToastProvider></I18nProvider>);
  return onTabChange;
}

it('renders the canonical organization heading and selected URL tab', async () => {
  setup('clubs');
  await waitFor(() => expect(screen.getByRole('heading', { name: 'Orion Gaming' })).toBeVisible());
  expect(screen.getByRole('tab', { name: 'Клубы' })).toHaveAttribute('aria-selected', 'true');
  expect(screen.getByText('Orion Center')).toBeVisible();
});

it('reports tab changes to the route owner', async () => {
  const onTabChange = setup('summary');
  await waitFor(() => expect(screen.getByRole('heading', { name: 'Orion Gaming' })).toBeVisible());
  const supportTab = screen.getByRole('tab', { name: 'Поддержка' });
  fireEvent.pointerDown(supportTab, { button: 0, ctrlKey: false });
  fireEvent.click(supportTab);
  await waitFor(() => expect(onTabChange).toHaveBeenCalledWith('support'));
});

it('keeps summary available when health fails', async () => {
  setup('summary');
  await waitFor(() => expect(screen.getByText('orion')).toBeVisible());
  expect(screen.getByRole('heading', { name: 'Orion Gaming' })).toBeVisible();
});

it('shows a forbidden state for a forbidden direct tab URL', async () => {
  const onTabChange = mock();
  setup('history', onTabChange, { canManageOrganization: false, canManageAccess: false, canViewSupport: false, canViewBilling: false, canViewAudit: false });
  await waitFor(() => expect(screen.getByRole('heading', { name: 'Нет доступа' })).toBeVisible());
  expect(screen.queryByRole('heading', { name: 'Orion Gaming' })).not.toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: 'Сводка' }));
  expect(onTabChange).toHaveBeenCalledWith('summary');
});
