import { render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { OrganizationClubsTab } from './OrganizationClubsTab';
import type { OrganizationBranch, OrganizationLimits } from '@/api/types';

function branch(overrides: Partial<OrganizationBranch> = {}): OrganizationBranch {
  return {
    branchId: 'branch-1',
    slug: 'main',
    name: 'Главный клуб',
    city: 'Душанбе',
    createdAtUtc: '2026-01-01T00:00:00Z',
    ...overrides
  };
}

function limits(overrides: Partial<OrganizationLimits> = {}): OrganizationLimits {
  return { maxBranches: null, maxDevicesPerBranch: null, maxConcurrentSessions: null, maxStaffUsersPerBranch: null, ...overrides };
}

function pulseClient() {
  return { getPulse: mock().mockResolvedValue({ generatedAtUtc: '2026-01-01T00:00:00Z', organizations: [] }) };
}

function organizationsClient() {
  return { createBranch: mock() };
}

describe('OrganizationClubsTab', () => {
  it('показывает занятость лимита филиалов', async () => {
    const branches = [branch({ branchId: 'branch-1' }), branch({ branchId: 'branch-2', slug: 'north' })];
    render(
      <I18nProvider>
        <OrganizationClubsTab
          client={pulseClient()}
          organizationsClient={organizationsClient()}
          organizationId="org-1"
          branches={branches}
          limits={limits({ maxBranches: 3 })}
          onBranchCreated={mock()}
        />
      </I18nProvider>
    );

    await waitFor(() => expect(screen.getByText('Филиалов: 2 из 3')).toBeInTheDocument());
  });

  it('не показывает счётчик, если лимит филиалов не задан', async () => {
    const branches = [branch()];
    render(
      <I18nProvider>
        <OrganizationClubsTab
          client={pulseClient()}
          organizationsClient={organizationsClient()}
          organizationId="org-1"
          branches={branches}
          limits={limits({ maxBranches: null })}
          onBranchCreated={mock()}
        />
      </I18nProvider>
    );

    await waitFor(() => expect(screen.getByText('Главный клуб')).toBeInTheDocument());
    expect(screen.queryByText(/^Филиалов:/)).not.toBeInTheDocument();
  });
});
