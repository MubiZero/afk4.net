import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, beforeAll, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { PlatformApiError } from '@/api/platformTransport';
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

function renderSection(client: any, sectionBranches: OrganizationBranch[] = branches) {
  return render(
    <I18nProvider><ToastProvider>
      <OrganizationOwnerInvitesSection client={client} organizationId="o1" branches={sectionBranches} initialInvite={null} />
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

// Срок в фикстуре — заведомо далёкое будущее: «Отправить ещё раз» должна видеть только
// действующее приглашение, и тест не должен протухнуть вместе с датой в примере.
const activeInvite = summary({ expiresAtUtc: '2099-01-01T00:00:00Z' });

it('отправляет письмо повторно и показывает короткое подтверждение', async () => {
  const client = {
    listOrganizationOwnerInvites: mock().mockResolvedValue([activeInvite]),
    createOrganizationOwnerInvite: mock(),
    revokeOrganizationOwnerInvite: mock(),
    resendOrganizationOwnerInvite: mock().mockResolvedValue(activeInvite)
  };
  renderSection(client);

  fireEvent.click(await screen.findByRole('button', { name: 'Отправить ещё раз' }));

  await waitFor(() => expect(client.resendOrganizationOwnerInvite).toHaveBeenCalledWith('i1'));
  expect(await screen.findByText('Письмо отправлено повторно')).toBeTruthy();
});

it('отказ сервера при повторной отправке называет причину, а не общую фразу', async () => {
  const client = {
    listOrganizationOwnerInvites: mock().mockResolvedValue([activeInvite]),
    createOrganizationOwnerInvite: mock(),
    revokeOrganizationOwnerInvite: mock(),
    resendOrganizationOwnerInvite: mock().mockRejectedValue(new PlatformApiError(403, 'Forbidden', null))
  };
  renderSection(client);

  fireEvent.click(await screen.findByRole('button', { name: 'Отправить ещё раз' }));

  expect(await screen.findByText('Недостаточно прав для этого действия.')).toBeTruthy();
});

it('повторное нажатие во время запроса не отправляет письмо дважды', async () => {
  let resolveResend: (value: typeof activeInvite) => void = () => {};
  const client = {
    listOrganizationOwnerInvites: mock().mockResolvedValue([activeInvite]),
    createOrganizationOwnerInvite: mock(),
    revokeOrganizationOwnerInvite: mock(),
    resendOrganizationOwnerInvite: mock(() => new Promise<typeof activeInvite>(resolve => { resolveResend = resolve; }))
  };
  renderSection(client);

  const button = await screen.findByRole('button', { name: 'Отправить ещё раз' });
  fireEvent.click(button);
  fireEvent.click(button);
  fireEvent.click(button);

  expect(client.resendOrganizationOwnerInvite).toHaveBeenCalledTimes(1);
  resolveResend(activeInvite);
  await waitFor(() => expect(button).not.toBeDisabled());
});

it('не предлагает повтор для принятого, отозванного или просроченного приглашения', async () => {
  const client = {
    listOrganizationOwnerInvites: mock().mockResolvedValue([
      summary({ organizationOwnerInviteId: 'accepted', status: 'accepted' }),
      summary({ organizationOwnerInviteId: 'revoked', status: 'revoked' }),
      // Формально ещё "pending": сервер сам метку "истёк" не проставляет, пока приглашение не
      // попробуют принять, — поэтому просрочку панель считает по сроку, а не только по статусу.
      summary({ organizationOwnerInviteId: 'stale', status: 'pending', expiresAtUtc: '2000-01-01T00:00:00Z' })
    ]),
    createOrganizationOwnerInvite: mock(),
    revokeOrganizationOwnerInvite: mock(),
    resendOrganizationOwnerInvite: mock()
  };
  renderSection(client);

  await screen.findAllByText('owner@x.io');
  expect(screen.queryByRole('button', { name: 'Отправить ещё раз' })).toBeNull();
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

// Код выдаётся на филиал. У организации без филиала список пуст, кнопка серая — и раньше ни слова
// о том, что сначала нужен филиал и где его завести.
it('без филиала говорит, почему код не создать и где завести филиал', async () => {
  const client = { listOrganizationOwnerInvites: mock().mockResolvedValue([]), createOrganizationOwnerInvite: mock(), revokeOrganizationOwnerInvite: mock() };
  renderSection(client, []);

  const create = screen.getByRole('button', { name: 'Создать код' });
  expect(create).toBeDisabled();
  const reason = screen.getByText('Код приглашения выдаётся на филиал, а у организации их пока нет. Филиал добавляют на вкладке «Клубы».');
  expect(create.getAttribute('aria-describedby')).toBe(reason.id);
});

// Пустой список под серой кнопкой звал «Создайте код формой выше» — той самой формой, которая без
// филиала кода не создаст. Человек нажимал, ничего не происходило, и он шёл в поддержку.
it('без филиала пустой список не зовёт к форме, а говорит, что сначала нужен филиал', async () => {
  const client = { listOrganizationOwnerInvites: mock().mockResolvedValue([]), createOrganizationOwnerInvite: mock(), revokeOrganizationOwnerInvite: mock() };
  renderSection(client, []);

  await screen.findByText('Кодов пока нет. Сначала добавьте филиал на вкладке «Клубы» — после этого код создаётся формой выше.');
  expect(screen.queryByText(/Создайте код формой выше/)).toBeNull();
});

it('с филиалом причину не пишет', async () => {
  const client = { listOrganizationOwnerInvites: mock().mockResolvedValue([]), createOrganizationOwnerInvite: mock(), revokeOrganizationOwnerInvite: mock() };
  renderSection(client);

  const create = screen.getByRole('button', { name: 'Создать код' });
  expect(create).toBeEnabled();
  expect(create.getAttribute('aria-describedby')).toBeNull();
  expect(screen.queryByText(/Код приглашения выдаётся на филиал/)).toBeNull();
});
