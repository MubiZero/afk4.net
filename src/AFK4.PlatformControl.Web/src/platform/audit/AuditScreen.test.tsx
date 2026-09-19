import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { expect, it, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { AuditScreen } from './AuditScreen';

it('loads server-filtered audit and reports filters to the route owner', async () => {
  const search = mock().mockResolvedValue({ records: [], limit: 100 });
  const onFiltersChange = mock();
  render(<I18nProvider><AuditScreen client={{ search }} filters={{ organizationId: '', action: '', outcome: '', from: '', to: '' }} onFiltersChange={onFiltersChange} /></I18nProvider>);
  await waitFor(() => expect(search).toHaveBeenCalled());
  fireEvent.change(screen.getByLabelText('Действие'), { target: { value: 'updates.rollout.create' } });
  fireEvent.click(screen.getByRole('button', { name: 'Применить фильтры' }));
  expect(onFiltersChange).toHaveBeenCalledWith(expect.objectContaining({ action: 'updates.rollout.create' }));
  expect(screen.getByText('Записей аудита не найдено.')).toBeVisible();
});

// Журнал читают, когда разбираются в жалобе клуба, и три колонки из четырёх были машинными
// именами сервера: «OrganizationOwnerInvite», «Denied», «PlatformApi».
it('объект, исход и источник записи названы словами', async () => {
  const search = mock().mockResolvedValue({
    records: [{
      auditRecordId: 'a1',
      organizationId: 'o1',
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
  render(<I18nProvider><AuditScreen client={{ search }} filters={{ organizationId: '', action: '', outcome: '', from: '', to: '' }} onFiltersChange={mock()} /></I18nProvider>);

  expect(await screen.findByText('Код доступа владельца · inv-1')).toBeVisible();
  expect(screen.getByText('Отказано')).toBeVisible();
  expect(screen.getByText('Сервер платформы')).toBeVisible();
  // Действие остаётся машинным намеренно: по нему ищут и сверяются с логами.
  expect(screen.getByText('organizations.owner_invites.revoke')).toBeVisible();
});
