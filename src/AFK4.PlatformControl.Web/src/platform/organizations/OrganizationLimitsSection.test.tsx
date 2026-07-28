import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { OrganizationLimitsSection } from './OrganizationLimitsSection';
import type { OrganizationDetail } from '@/api/types';

function detail(over: Partial<OrganizationDetail>): OrganizationDetail {
  return {
    organizationId: 'o1', slug: 'acme', name: 'Acme', status: 'active', statusReason: null,
    statusChangedAtUtc: null, planCode: 'starter', subscriptionStatus: 'active',
    limits: { maxBranches: 3, maxDevicesPerBranch: null, maxConcurrentSessions: null, maxStaffUsersPerBranch: null },
    branches: [], createdAtUtc: '2026-01-01T00:00:00Z', updatedAtUtc: '2026-01-01T00:00:00Z', ...over
  };
}

it('submits limits with blanks coerced to null', async () => {
  const client = { updateLimits: mock().mockResolvedValue(detail({})) } as any;
  const onUpdated = mock();
  render(
    <I18nProvider><ToastProvider>
      <OrganizationLimitsSection client={client} organization={detail({})} onUpdated={onUpdated} />
    </ToastProvider></I18nProvider>
  );
  fireEvent.click(screen.getByRole('button', { name: 'Применить лимиты' }));
  await waitFor(() => expect(client.updateLimits).toHaveBeenCalledWith('o1', {
    maxBranches: 3, maxDevicesPerBranch: null, maxConcurrentSessions: null, maxStaffUsersPerBranch: null
  }));
  expect(onUpdated).toHaveBeenCalled();
});
