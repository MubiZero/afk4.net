import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { expect, it, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { AuditScreen } from './AuditScreen';

it('loads server-filtered audit and reports filters to the route owner', async () => {
  const search = mock().mockResolvedValue({ records: [], limit: 100 });
  const onFiltersChange = mock();
  render(<I18nProvider><AuditScreen client={{ search }} organizationsClient={{ listOrganizations: mock().mockResolvedValue([{ organizationId: 'o1', name: 'Orion Gaming' }]) }} filters={{ organizationId: '', action: '', outcome: '', from: '', to: '' }} onFiltersChange={onFiltersChange} /></I18nProvider>);
  await waitFor(() => expect(search).toHaveBeenCalled());
  // Текст действия уходит по Enter (или когда поле отпустили), а не на каждую букву.
  fireEvent.change(screen.getByLabelText('Действие'), { target: { value: 'updates.rollout.create' } });
  expect(onFiltersChange).not.toHaveBeenCalled();
  fireEvent.keyDown(screen.getByLabelText('Действие'), { key: 'Enter' });
  expect(onFiltersChange).toHaveBeenCalledWith(expect.objectContaining({ action: 'updates.rollout.create' }));
  // Выбор исхода применяется сразу.
  fireEvent.change(screen.getByLabelText('Результат'), { target: { value: 'Denied' } });
  expect(onFiltersChange).toHaveBeenLastCalledWith(expect.objectContaining({ outcome: 'Denied' }));
  expect(screen.queryByRole('button', { name: 'Применить фильтры' })).toBeNull();
  expect(screen.getByText('Журнал пока пуст. Сюда сами попадают действия сотрудников платформы и клубов.')).toBeVisible();
  expect(screen.queryByRole('button', { name: 'Сбросить фильтр' })).toBeNull();
});

// «Записей аудита не найдено» звучало одинаково и для пустого журнала, и для фильтра, под который
// ничего не подошло, — а это два разных ответа, и у второго есть выход.
it('пустой ответ под фильтром сбрасывается одной кнопкой', async () => {
  const search = mock().mockResolvedValue({ records: [], limit: 100 });
  const onFiltersChange = mock();
  render(<I18nProvider><AuditScreen client={{ search }} organizationsClient={{ listOrganizations: mock().mockResolvedValue([]) }} filters={{ organizationId: '', action: 'updates.rollout.create', outcome: 'Denied', from: '', to: '' }} onFiltersChange={onFiltersChange} /></I18nProvider>);

  expect(await screen.findByText('Под эти условия ничего не подошло.')).toBeVisible();
  fireEvent.click(screen.getByRole('button', { name: 'Сбросить фильтр' }));

  expect(onFiltersChange).toHaveBeenCalledWith({ organizationId: '', action: '', outcome: '', from: '', to: '' });
});

// Журнал читают, когда разбираются в жалобе клуба, и три колонки из четырёх были машинными
// именами сервера: «OrganizationOwnerInvite», «Denied», «PlatformApi».
it('объект, исход и источник записи названы словами', async () => {
  const search = mock().mockResolvedValue({
    records: [{
      auditRecordId: 'a1',
      organizationId: 'o1',
      organizationName: 'Orion Gaming',
      branchId: null,
      actorStaffUserId: null,
      actorPlatformAdminUserId: 'pa1',
      action: 'organizations.owner_invites.revoke',
      targetType: 'OrganizationOwnerInvite',
      targetId: 'inv-1',
      outcome: 'Denied',
      sourceApp: 'PlatformApi',
      detailsJson: '{}',
      amountMinorUnits: null,
      createdAtUtc: '2026-09-18T10:00:00Z'
    }],
    limit: 100
  });
  render(<I18nProvider><AuditScreen client={{ search }} organizationsClient={{ listOrganizations: mock().mockResolvedValue([{ organizationId: 'o1', name: 'Orion Gaming' }]) }} filters={{ organizationId: '', action: '', outcome: '', from: '', to: '' }} onFiltersChange={mock()} /></I18nProvider>);

  const table = within(await screen.findByRole('table'));
  // Объект — словами, без идентификатора записи.
  expect(table.getByText('Код приглашения владельца')).toBeVisible();
  expect(table.queryByText(/inv-1/)).toBeNull();
  expect(table.getByText('Отказано')).toBeVisible();
  expect(table.getByText('Сервер платформы')).toBeVisible();
  // Клуб назван именем: идентификатор в этой колонке опознать нечем.
  expect(table.getByText('Orion Gaming')).toBeVisible();
  // Незнакомое действие — кодом как есть: выдумывать за сервер название события нельзя.
  expect(table.getByText('organizations.owner_invites.revoke')).toBeVisible();
});

// Действие сотрудника платформы — словами, а код остаётся под ним: по коду ищут в фильтре.
it('называет действие платформы словами и оставляет код мелкой строкой', async () => {
  const search = mock().mockResolvedValue({
    records: [{
      auditRecordId: 'a2', organizationId: 'o1', organizationName: 'Orion Gaming', branchId: null,
      actorStaffUserId: null, actorPlatformAdminUserId: 'pa1', action: 'billing.invoice.mark_paid',
      targetType: 'Invoice', targetId: 'i-1', outcome: 'Succeeded', sourceApp: 'PlatformApi',
      detailsJson: '{}', amountMinorUnits: null, createdAtUtc: '2026-09-18T10:00:00Z'
    }],
    limit: 100
  });
  render(<I18nProvider><AuditScreen client={{ search }} organizationsClient={{ listOrganizations: mock().mockResolvedValue([]) }} filters={{ organizationId: '', action: '', outcome: '', from: '', to: '' }} onFiltersChange={mock()} /></I18nProvider>);

  const table = within(await screen.findByRole('table'));
  expect(table.getByText('Счёт отмечен оплаченным')).toBeVisible();
  expect(table.getByText('billing.invoice.mark_paid')).toBeVisible();
});
