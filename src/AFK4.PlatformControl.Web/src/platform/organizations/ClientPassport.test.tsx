import { render, screen, waitFor } from '@testing-library/react';
import { expect, it, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import type { OrganizationDetail } from '@/api/types';
import { ClientPassport, type ClientPassportClients } from './ClientPassport';
import type { OrganizationPageAccess } from './OrganizationPage';

function organization(overrides: Partial<OrganizationDetail> = {}): OrganizationDetail {
  return {
    organizationId: 'o1',
    slug: 'orion',
    name: 'Orion Gaming',
    status: 'active',
    statusReason: null,
    statusChangedAtUtc: null,
    planCode: 'growth',
    subscriptionStatus: 'active',
    limits: { maxBranches: 3, maxDevicesPerBranch: 80, maxConcurrentSessions: 80, maxStaffUsersPerBranch: 12 },
    branches: [{ branchId: 'b1', slug: 'center', name: 'Orion Center', city: 'Tashkent', createdAtUtc: '2026-01-01T00:00:00Z' }],
    createdAtUtc: '2026-01-01T00:00:00Z',
    updatedAtUtc: '2026-01-02T00:00:00Z',
    contactEmail: null,
    contactPhone: null,
    legalDetails: null,
    updateChannel: 'stable',
    pinnedClientVersion: null,
    ...overrides
  };
}

function client(): ClientPassportClients {
  return {
    organizations: { updateProfile: mock(), updateStatus: mock(), updateUpdateChannel: mock(), transferOwner: mock() },
    subscriptions: {
      getSubscription: mock().mockResolvedValue({
        organizationSubscriptionId: 's1', organizationId: 'o1', planCode: 'growth', status: 'active',
        currentPeriodStartUtc: '2026-01-01T00:00:00Z', currentPeriodEndUtc: '2026-02-01T00:00:00Z',
        nextInvoiceUtc: '2026-02-01T00:00:00Z', amountMinorUnits: 150000, currencyCode: 'RUB',
        billingInterval: 'monthly', cancelAtPeriodEnd: false, createdAtUtc: '2026-01-01T00:00:00Z',
        updatedAtUtc: '2026-01-01T00:00:00Z', paymentGraceUntilUtc: null
      }),
      updateSubscription: mock()
    },
    invoices: { generateInvoice: mock() },
    organizationOwnerInvites: {
      listOrganizationOwnerInvites: mock().mockResolvedValue([
        { organizationOwnerInviteId: 'i1', organizationId: 'o1', branchId: 'b1', codeSuffix: '1234', status: 'accepted', ownerUserName: 'owner@orion.tj', ownerDisplayName: 'Alice Owner', expiresAtUtc: '2026-02-01T00:00:00Z', acceptedAtUtc: '2026-01-05T00:00:00Z', revokedAtUtc: null, revokedReason: null, createdAtUtc: '2026-01-01T00:00:00Z' }
      ])
    },
    debt: { listDebt: mock().mockResolvedValue([]) }
  };
}

const fullAccess: OrganizationPageAccess = {
  canManageOrganization: true,
  canManageAccess: true,
  canViewSupport: true,
  canViewBilling: true,
  canManageBilling: true,
  canManageProfile: true,
  canManageUpdateChannel: true,
  canTransferOwner: true,
  canViewAudit: true,
  canManageFeatures: true,
    canManageOffboarding: true
};

function setup(access: OrganizationPageAccess = fullAccess, orgOverrides: Partial<OrganizationDetail> = {}) {
  const c = client();
  render(
    <I18nProvider>
      <ToastProvider>
        <ClientPassport client={c} organization={organization(orgOverrides)} access={access} onUpdated={() => {}} />
      </ToastProvider>
    </I18nProvider>
  );
  return c;
}

it('shows the name, plan, price, next invoice, owner and update channel', async () => {
  setup();
  // Заголовок h1 принадлежит экрану, паспорт повторяет имя как якорь личности, а не как второй заголовок.
  expect(screen.getByText('Orion Gaming')).toBeVisible();
  expect(screen.getByText('Growth')).toBeVisible();
  expect(screen.getByText('stable')).toBeVisible();
  await waitFor(() => expect(screen.getByText(/1.?500/u)).toBeVisible());
  await waitFor(() => expect(screen.getByText('Alice Owner')).toBeVisible());
});

it('shows a debt chip when the subscription is past due', () => {
  setup(fullAccess, { subscriptionStatus: 'past_due' });
  expect(screen.getByText('Просрочен платёж')).toBeVisible();
});

it('does not show a debt chip for an active subscription', () => {
  setup(fullAccess, { subscriptionStatus: 'active' });
  expect(screen.queryByText('Просрочен платёж')).not.toBeInTheDocument();
});

it('does not claim the club has no debt when the staffer lacks billing.view (the endpoint would 403)', async () => {
  const c = client();
  // /api/platform/debt requires platform.billing.view; the endpoint would reject the request,
  // so the effect must not even call it — a support staffer without billing access must see an
  // honest "unknown", never the confident "Долгов нет" the passport used to show by swallowing
  // the 403.
  c.debt.listDebt = mock().mockResolvedValue([]);
  render(
    <I18nProvider>
      <ToastProvider>
        <ClientPassport client={c} organization={organization()} access={{ ...fullAccess, canViewBilling: false }} onUpdated={() => {}} />
      </ToastProvider>
    </I18nProvider>
  );
  const block = await screen.findByTestId('passport-debt');
  expect(block.textContent).toContain('Неизвестно');
  expect(block.textContent).not.toContain('Долгов нет');
  expect(c.debt.listDebt).not.toHaveBeenCalled();
});

it('does not claim the club has no debt when the debt request fails over a flaky network', async () => {
  const c = client();
  c.debt.listDebt = mock().mockRejectedValue(new Error('network blip'));
  render(
    <I18nProvider>
      <ToastProvider>
        <ClientPassport client={c} organization={organization()} access={fullAccess} onUpdated={() => {}} />
      </ToastProvider>
    </I18nProvider>
  );
  const block = await screen.findByTestId('passport-debt');
  await waitFor(() => expect(c.debt.listDebt).toHaveBeenCalled());
  expect(block.textContent).toContain('Неизвестно');
  expect(block.textContent).not.toContain('Долгов нет');
});

it('hides the alarming past-due chip once an active payment grace is confirmed', async () => {
  const c = client();
  c.debt.listDebt = mock().mockResolvedValue([
    {
      organizationId: 'o1', organizationName: 'Orion Gaming', organizationSlug: 'orion', organizationStatus: 'active',
      subscriptionStatus: 'past_due', outstandingMinorUnits: 290000, currencyCode: 'TJS',
      oldestOverdueInvoiceNumber: 42, oldestOverdueInvoiceId: 'i1', daysOverdue: 10, dunningStage: 3,
      graceUntilUtc: '2026-09-01T00:00:00Z', settledButSuspended: false
    }
  ]);
  render(
    <I18nProvider>
      <ToastProvider>
        <ClientPassport client={c} organization={organization({ subscriptionStatus: 'past_due' })} access={fullAccess} onUpdated={() => {}} />
      </ToastProvider>
    </I18nProvider>
  );
  await screen.findByTestId('passport-debt-stage');
  // Отсрочка не откатывает past_due (§7/§8): чип должен погаснуть, только когда мы точно знаем
  // про активную отсрочку, а не одновременно с ней противоречить.
  expect(screen.queryByText('Просрочен платёж')).not.toBeInTheDocument();
});

it('shows the outstanding debt amount from the debt queue without leaving the passport', async () => {
  const c = client();
  c.debt.listDebt = mock().mockResolvedValue([
    {
      organizationId: 'o1', organizationName: 'Orion Gaming', organizationSlug: 'orion', organizationStatus: 'active',
      subscriptionStatus: 'past_due', outstandingMinorUnits: 290000, currencyCode: 'TJS',
      oldestOverdueInvoiceNumber: 42, oldestOverdueInvoiceId: 'i1', daysOverdue: 10, dunningStage: 3,
      graceUntilUtc: null, settledButSuspended: false
    }
  ]);
  render(
    <I18nProvider>
      <ToastProvider>
        <ClientPassport client={c} organization={organization()} access={fullAccess} onUpdated={() => {}} />
      </ToastProvider>
    </I18nProvider>
  );
  const block = await screen.findByTestId('passport-debt');
  await waitFor(() => expect(block.textContent).toContain('42'));
  expect(block.textContent).toContain('10');
});

it('hides billing and organization-management levers without the matching rights', () => {
  setup({
    canManageOrganization: false,
    canManageAccess: false,
    canViewSupport: false,
    canViewBilling: false,
    canManageBilling: false,
    canManageProfile: false,
    canManageUpdateChannel: false,
    canTransferOwner: false,
    canViewAudit: false,
    canManageFeatures: false,
    canManageOffboarding: false
  });
  expect(screen.queryByRole('button', { name: 'Изменить подписку' })).not.toBeInTheDocument();
  expect(screen.queryByRole('button', { name: 'Выставить счёт' })).not.toBeInTheDocument();
  expect(screen.queryByRole('button', { name: 'Отсрочка' })).not.toBeInTheDocument();
  expect(screen.queryByRole('button', { name: 'Править профиль' })).not.toBeInTheDocument();
  expect(screen.queryByRole('button', { name: 'Приостановить' })).not.toBeInTheDocument();
  expect(screen.queryByRole('button', { name: 'Передать' })).not.toBeInTheDocument();
});

it('shows billing and organization-management levers with the matching rights', () => {
  setup();
  expect(screen.getByRole('button', { name: 'Изменить подписку' })).toBeVisible();
  expect(screen.getByRole('button', { name: 'Выставить счёт' })).toBeVisible();
  expect(screen.getByRole('button', { name: 'Отсрочка' })).toBeVisible();
  expect(screen.getByRole('button', { name: 'Править профиль' })).toBeVisible();
  expect(screen.getByRole('button', { name: 'Приостановить' })).toBeVisible();
  expect(screen.getByRole('button', { name: 'Передать' })).toBeVisible();
});
