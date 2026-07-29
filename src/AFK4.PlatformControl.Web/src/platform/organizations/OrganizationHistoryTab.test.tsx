import { render, screen, waitFor } from '@testing-library/react';
import { expect, it, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { OrganizationHistoryTab } from './OrganizationHistoryTab';

it('renders the authoritative organization audit history', async () => {
  const client = {
    listOrganizationHistory: mock().mockResolvedValue({
      limit: 100,
      records: [{
        auditRecordId: 'a1', organizationId: 'o1', branchId: null,
        actorStaffUserId: null, actorPlatformAdminUserId: 'p1',
        action: 'tenancy.organization.status.update', targetType: 'Organization', targetId: 'o1',
        outcome: 'succeeded', sourceApp: 'PlatformApi', detailsJson: '{}',
        createdAtUtc: '2026-07-30T08:00:00Z', amountMinorUnits: null
      }]
    })
  };

  render(<I18nProvider><OrganizationHistoryTab client={client} organizationId="o1" /></I18nProvider>);

  await waitFor(() => expect(screen.getByText('tenancy.organization.status.update')).toBeVisible());
  expect(client.listOrganizationHistory).toHaveBeenCalledWith('o1');
  expect(screen.getByText('Organization · o1')).toBeVisible();
});
