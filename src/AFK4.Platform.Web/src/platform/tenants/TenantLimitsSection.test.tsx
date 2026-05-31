import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { TenantLimitsSection } from './TenantLimitsSection';
import type { TenantDetail } from '@/api/types';

function detail(over: Partial<TenantDetail>): TenantDetail {
  return {
    organizationId: 'o1', slug: 'acme', name: 'Acme', status: 'active', statusReason: null,
    statusChangedAtUtc: null, planCode: 'starter', subscriptionStatus: 'active',
    limits: { maxBranches: 3, maxDevicesPerBranch: null, maxConcurrentSessions: null, maxStaffUsersPerBranch: null },
    branches: [], createdAtUtc: '2026-01-01T00:00:00Z', updatedAtUtc: '2026-01-01T00:00:00Z', ...over
  };
}

it('submits limits with blanks coerced to null', async () => {
  const client = { updateLimits: vi.fn().mockResolvedValue(detail({})) } as any;
  const onUpdated = vi.fn();
  render(
    <I18nProvider><ToastProvider>
      <TenantLimitsSection client={client} tenant={detail({})} onUpdated={onUpdated} />
    </ToastProvider></I18nProvider>
  );
  fireEvent.click(screen.getByRole('button', { name: 'Применить лимиты' }));
  await waitFor(() => expect(client.updateLimits).toHaveBeenCalledWith('o1', {
    maxBranches: 3, maxDevicesPerBranch: null, maxConcurrentSessions: null, maxStaffUsersPerBranch: null
  }));
  expect(onUpdated).toHaveBeenCalled();
});
