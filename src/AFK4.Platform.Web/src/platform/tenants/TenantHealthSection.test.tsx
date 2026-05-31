import { render, screen } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { TenantHealthSection } from './TenantHealthSection';
import type { TenantHealth } from '@/api/types';

const health: TenantHealth = {
  organizationId: 'o1', status: 'active', branchCount: 3, deviceCount: 12,
  activeStaffUserCount: 5, latestStaffSignInAtUtc: '2026-05-01T00:00:00Z',
  latestMigration: '20260501_Init', recentErrorCount: 1,
  recentErrors: [{ createdAtUtc: '2026-05-02T00:00:00Z', source: 'auth', action: 'sign_in', outcome: 'denied', message: 'bad creds' }]
};

it('renders health metrics and the recent-errors row', async () => {
  const client = { getHealth: vi.fn().mockResolvedValue(health) };
  render(<I18nProvider><TenantHealthSection client={client} organizationId="o1" /></I18nProvider>);
  expect(await screen.findByText('active')).toBeInTheDocument();
  expect(screen.getByText('bad creds')).toBeInTheDocument();
});

it('shows an error state with a retry button', async () => {
  const client = { getHealth: vi.fn().mockRejectedValue(new Error('boom')) };
  render(<I18nProvider><TenantHealthSection client={client} organizationId="o1" /></I18nProvider>);
  expect(await screen.findByText('Повторить')).toBeInTheDocument();
});
