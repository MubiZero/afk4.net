import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { OrganizationStatusSection } from './OrganizationStatusSection';
import type { OrganizationDetail } from '@/api/types';

function detail(over: Partial<OrganizationDetail>): OrganizationDetail {
  return {
    organizationId: 'o1', slug: 'acme', name: 'Acme', status: 'active', statusReason: null,
    statusChangedAtUtc: null, planCode: 'starter', subscriptionStatus: 'active',
    limits: { maxBranches: null, maxDevicesPerBranch: null, maxConcurrentSessions: null, maxStaffUsersPerBranch: null },
    branches: [], createdAtUtc: '2026-01-01T00:00:00Z', updatedAtUtc: '2026-01-01T00:00:00Z',
    contactEmail: null, contactPhone: null, legalDetails: null, updateChannel: 'stable', pinnedClientVersion: null, ...over
  };
}

it('confirms a status change and calls updateStatus then onUpdated', async () => {
  const next = detail({ status: 'suspended' });
  const client = { updateStatus: mock().mockResolvedValue(next) } as any;
  const onUpdated = mock();
  render(
    <I18nProvider><ToastProvider>
      <OrganizationStatusSection client={client} organization={detail({})} onUpdated={onUpdated} />
    </ToastProvider></I18nProvider>
  );

  // 1) выбираем новый статус в нативном селекте
  fireEvent.change(screen.getByLabelText('Новый статус'), { target: { value: 'suspended' } });

  // 2) кнопка «Изменить статус» разблокировалась — жмём
  const applyBtn = screen.getByRole('button', { name: 'Изменить статус' });
  await waitFor(() => expect(applyBtn).toBeEnabled());
  fireEvent.click(applyBtn);

  // 3) confirm in the dialog
  fireEvent.change(await screen.findByLabelText('Причина'), { target: { value: 'Нарушение условий обслуживания' } });
  fireEvent.click(await screen.findByRole('button', { name: 'Применить' }));
  await waitFor(() => expect(client.updateStatus).toHaveBeenCalledWith('o1', 'suspended', 'Нарушение условий обслуживания'));
  expect(onUpdated).toHaveBeenCalledWith(next);
});
