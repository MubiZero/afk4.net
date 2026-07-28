import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, beforeAll, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { OrganizationStatusSection } from './OrganizationStatusSection';
import type { OrganizationDetail } from '@/api/types';

// Radix Select requires these to work in jsdom
beforeAll(() => {
  window.HTMLElement.prototype.hasPointerCapture = () => false;
  window.HTMLElement.prototype.scrollIntoView = () => {};
  window.HTMLElement.prototype.releasePointerCapture = () => {};
});

function detail(over: Partial<OrganizationDetail>): OrganizationDetail {
  return {
    organizationId: 'o1', slug: 'acme', name: 'Acme', status: 'active', statusReason: null,
    statusChangedAtUtc: null, planCode: 'starter', subscriptionStatus: 'active',
    limits: { maxBranches: null, maxDevicesPerBranch: null, maxConcurrentSessions: null, maxStaffUsersPerBranch: null },
    branches: [], createdAtUtc: '2026-01-01T00:00:00Z', updatedAtUtc: '2026-01-01T00:00:00Z', ...over
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

  // 1) drive the Radix Select from 'active' to 'suspended'
  //    Open by firing pointerDown on the trigger (Radix's pointer-based open logic)
  const trigger = screen.getByRole('combobox', { name: 'Новый статус' });
  fireEvent.pointerDown(trigger, { button: 0, ctrlKey: false });
  fireEvent.click(trigger);

  // Wait for the listbox / option to appear, then click it
  const option = await screen.findByRole('option', { name: 'Приостановлена' });
  fireEvent.click(option);

  // 2) the "Изменить статус" button is now enabled — click it
  const applyBtn = screen.getByRole('button', { name: 'Изменить статус' });
  await waitFor(() => expect(applyBtn).toBeEnabled());
  fireEvent.click(applyBtn);

  // 3) confirm in the dialog
  fireEvent.click(await screen.findByRole('button', { name: 'Применить' }));
  await waitFor(() => expect(client.updateStatus).toHaveBeenCalledWith('o1', 'suspended', expect.any(String)));
  expect(onUpdated).toHaveBeenCalledWith(next);
});
