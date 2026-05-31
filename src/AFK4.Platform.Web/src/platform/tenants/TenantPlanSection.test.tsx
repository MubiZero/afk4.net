import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { TenantPlanSection } from './TenantPlanSection';
import type { TenantDetail } from '@/api/types';

function detail(over: Partial<TenantDetail>): TenantDetail {
  return {
    organizationId: 'o1', slug: 'acme', name: 'Acme', status: 'active', statusReason: null,
    statusChangedAtUtc: null, planCode: 'starter', subscriptionStatus: 'active',
    limits: { maxBranches: null, maxDevicesPerBranch: null, maxConcurrentSessions: null, maxStaffUsersPerBranch: null },
    branches: [], createdAtUtc: '2026-01-01T00:00:00Z', updatedAtUtc: '2026-01-01T00:00:00Z', ...over
  };
}

it('applies plan change and calls onUpdated', async () => {
  const next = detail({ planCode: 'scale' });
  const client = { updatePlan: vi.fn().mockResolvedValue(next) } as any;
  const onUpdated = vi.fn();
  render(
    <I18nProvider><ToastProvider>
      <TenantPlanSection client={client} tenant={detail({})} onUpdated={onUpdated} />
    </ToastProvider></I18nProvider>
  );
  fireEvent.click(screen.getByRole('button', { name: 'Применить' }));
  await waitFor(() => expect(client.updatePlan).toHaveBeenCalledWith('o1', 'starter', 'active'));
  expect(onUpdated).toHaveBeenCalledWith(next);
});
