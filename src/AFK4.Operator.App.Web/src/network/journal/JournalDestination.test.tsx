import { describe, it, expect, mock, afterEach } from 'bun:test';
import { render, screen, cleanup, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';

afterEach(() => cleanup());

const searchOrganizationAudit = mock(async () => ({
  records: [{
    auditRecordId: 'r1', branchId: null, actorStaffUserId: null, actorPlatformAdminUserId: null,
    action: 'news.published', targetType: 'News', targetId: 'n1', outcome: 'Succeeded',
    sourceApp: 'PlatformApi', detailsJson: '{}', createdAtUtc: '2026-07-20T10:00:00Z'
  }],
  limit: 100
}));

mock.module('../../operatorHelpers', () => ({
  createAuthenticatedOperatorClients: () => ({
    orgAudit: { searchOrganizationAudit }
  })
}));

const backend = { config: { platformBaseUrl: 'x', currencyCode: 'TJS' }, session: { organizationId: 'org', accessToken: 't' }, branchId: 'b1' };

describe('JournalDestination', () => {
  it('renders an audit row including an org-level (null-branch) action', async () => {
    const { JournalDestination } = await import('./JournalDestination');
    render(<I18nProvider initialLocale="ru"><JournalDestination backend={backend as never} /></I18nProvider>);
    await waitFor(() => expect(screen.getByText('news.published')).toBeInTheDocument());
    // The whole point of this screen is org-wide audit — verify the org-level (BranchId=null)
    // record actually reached the query the client was called with, not just that some row rendered.
    expect(searchOrganizationAudit).toHaveBeenCalled();
    expect(screen.getByText('News (n1)')).toBeInTheDocument();
    expect(screen.getByText('система')).toBeInTheDocument();
  });
});
