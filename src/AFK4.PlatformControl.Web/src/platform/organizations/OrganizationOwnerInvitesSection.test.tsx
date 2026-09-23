import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, beforeAll, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { OrganizationOwnerInvitesSection } from './OrganizationOwnerInvitesSection';
import type { OrganizationOwnerInviteSummary, OrganizationBranch } from '@/api/types';

beforeAll(() => {
  window.HTMLElement.prototype.hasPointerCapture = () => false;
  window.HTMLElement.prototype.scrollIntoView = () => {};
  window.HTMLElement.prototype.releasePointerCapture = () => {};
});

const branches: OrganizationBranch[] = [
  { branchId: 'b1', slug: 'main', name: 'Main', city: 'Moscow', createdAtUtc: '2026-01-01T00:00:00Z' }
];

function summary(over: Partial<OrganizationOwnerInviteSummary>): OrganizationOwnerInviteSummary {
  return {
    organizationOwnerInviteId: 'i1', organizationId: 'o1', branchId: 'b1', codeSuffix: '1234',
    status: 'pending', ownerUserName: 'owner@x.io', ownerDisplayName: 'Owner',
    expiresAtUtc: '2026-02-01T00:00:00Z', acceptedAtUtc: null, revokedAtUtc: null,
    revokedReason: null, createdAtUtc: '2026-01-01T00:00:00Z', ...over
  };
}

function renderSection(client: any) {
  return render(
    <I18nProvider><ToastProvider>
      <OrganizationOwnerInvitesSection client={client} organizationId="o1" branches={branches} initialInvite={null} />
    </ToastProvider></I18nProvider>
  );
}

it('lists invites with a masked code', async () => {
  const client = { listOrganizationOwnerInvites: mock().mockResolvedValue([summary({})]), createOrganizationOwnerInvite: mock(), revokeOrganizationOwnerInvite: mock() };
  renderSection(client);
  expect(await screen.findByText('•••• 1234')).toBeTruthy();
  expect(screen.getByText('owner@x.io')).toBeTruthy();
});

it('creates a code and reveals the full code', async () => {
  const client = {
    listOrganizationOwnerInvites: mock(),
    createOrganizationOwnerInvite: mock().mockResolvedValue({
      organizationOwnerInviteId: 'i9', organizationId: 'o1', branchId: 'b1', code: 'FULL-CODE-9',
      status: 'pending', ownerUserName: null, ownerDisplayName: null,
      expiresAtUtc: '2026-02-01T00:00:00Z', acceptedAtUtc: null, revokedAtUtc: null,
      revokedReason: null, createdAtUtc: '2026-01-01T00:00:00Z'
    }),
    revokeOrganizationOwnerInvite: mock()
  };
  client.listOrganizationOwnerInvites.mockResolvedValueOnce([]).mockResolvedValueOnce([summary({ organizationOwnerInviteId: 'i9', codeSuffix: 'DE-9', ownerUserName: null })]);
  renderSection(client);
  await screen.findByText('Кодов пока нет. Создайте код формой выше — по нему владелец заведёт себе вход.');

  fireEvent.click(screen.getByRole('button', { name: 'Создать код' }));
  await waitFor(() => expect(client.createOrganizationOwnerInvite).toHaveBeenCalledWith('o1', 'b1', null, null, null, null));
  expect(await screen.findByText('FULL-CODE-9')).toBeTruthy();
});

it('revokes a pending invite with a reason', async () => {
  const client = {
    listOrganizationOwnerInvites: mock().mockResolvedValue([summary({})]),
    createOrganizationOwnerInvite: mock(),
    revokeOrganizationOwnerInvite: mock().mockResolvedValue(summary({ status: 'revoked' }))
  };
  renderSection(client);
  fireEvent.click(await screen.findByRole('button', { name: 'Отозвать' }));

  const reason = await screen.findByLabelText('Причина');
  fireEvent.change(reason, { target: { value: 'fraud' } });
  const confirmButtons = screen.getAllByRole('button', { name: 'Отозвать' });
  fireEvent.click(confirmButtons[confirmButtons.length - 1]);

  await waitFor(() => expect(client.revokeOrganizationOwnerInvite).toHaveBeenCalledWith('i1', 'fraud'));
});

// Без адреса сервер письма не шлёт, а код приходится диктовать голосом: поле почты — это и есть
// единственный путь, которым приглашение доезжает до владельца само.
it('sends the invite to the owner email typed in the form', async () => {
  const client = {
    listOrganizationOwnerInvites: mock().mockResolvedValue([]),
    createOrganizationOwnerInvite: mock().mockResolvedValue({
      organizationOwnerInviteId: 'i9', organizationId: 'o1', branchId: 'b1', code: 'FULL-CODE-9',
      status: 'pending', ownerUserName: null, ownerDisplayName: null,
      expiresAtUtc: '2026-02-01T00:00:00Z', acceptedAtUtc: null, revokedAtUtc: null,
      revokedReason: null, createdAtUtc: '2026-01-01T00:00:00Z'
    }),
    revokeOrganizationOwnerInvite: mock()
  };
  renderSection(client);
  await screen.findByText('Кодов пока нет. Создайте код формой выше — по нему владелец заведёт себе вход.');

  fireEvent.change(screen.getByLabelText('Почта владельца'), { target: { value: ' owner@club.tj ' } });
  fireEvent.click(screen.getByRole('button', { name: 'Создать код' }));

  await waitFor(() => expect(client.createOrganizationOwnerInvite)
    .toHaveBeenCalledWith('o1', 'b1', null, null, null, 'owner@club.tj'));
});

// Код владельца показывается ровно один раз, и его надо передать человеку целиком. Раньше он
// появлялся строкой в ячейке таблицы: выделить мышью, не промахнуться, никакой ссылки.
it('выданный код можно передать ссылкой, а не перепечатывать из таблицы', async () => {
  const client = {
    listOrganizationOwnerInvites: mock().mockResolvedValue([]),
    createOrganizationOwnerInvite: mock().mockResolvedValue({
      organizationOwnerInviteId: 'i1',
      organizationId: 'o1',
      branchId: 'b1',
      code: 'OWN-98765',
      codeSuffix: '8765',
      status: 'pending',
      ownerUserName: null,
      ownerDisplayName: null,
      expiresAtUtc: '2026-10-01T00:00:00Z',
      acceptedAtUtc: null,
      revokedAtUtc: null,
      revokedReason: null,
      createdAtUtc: '2026-09-18T00:00:00Z'
    }),
    revokeOrganizationOwnerInvite: mock()
  };
  render(
    <I18nProvider><ToastProvider>
      <OrganizationOwnerInvitesSection
        client={client as never}
        organizationId="o1"
        branches={[{ branchId: 'b1', slug: 'main', name: 'На Рудаки', city: 'Душанбе', createdAtUtc: '2026-01-01T00:00:00Z' }]}
      />
    </ToastProvider></I18nProvider>
  );

  fireEvent.click(await screen.findByRole('button', { name: 'Создать код' }));

  expect(await screen.findByLabelText('Код приглашения')).toHaveValue('OWN-98765');
  expect(screen.getByLabelText('Ссылка для активации')).toHaveValue(
    `${window.location.origin}/account-activation?code=OWN-98765`
  );
});
