import { describe, it, expect, mock, afterEach } from 'bun:test';
import { render, screen, cleanup, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';

afterEach(() => cleanup());

const search = mock(async () => ({
  records: [{
    auditRecordId: 'r1', branchId: 'b1', actorStaffUserId: 'staff-1', actorPlatformAdminUserId: null,
    action: 'shift.opened', targetType: 'Shift', targetId: 's1', outcome: 'Succeeded',
    sourceApp: 'OrganizationAdmin', detailsJson: '{}', createdAtUtc: '2026-07-20T10:00:00Z'
  }]
}));

mock.module('../../operatorHelpers', () => ({
  createAuthenticatedOperatorClients: () => ({ audit: { search } }),
  readArray: (rec: Record<string, unknown>, key: string) => (Array.isArray(rec?.[key]) ? (rec[key] as unknown[]) : []),
  readRecord: (v: unknown) => (v && typeof v === 'object' ? (v as Record<string, unknown>) : {}),
  readString: (rec: Record<string, unknown>, key: string) => (typeof rec?.[key] === 'string' ? (rec[key] as string) : '')
}));

const backend = { config: { platformBaseUrl: 'x', currencyCode: 'TJS' }, session: { organizationId: 'org', accessToken: 't' }, branchId: 'b1' };

describe('BranchJournalDestination', () => {
  it('searches BRANCH audit (not org) and renders a row', async () => {
    const { BranchJournalDestination } = await import('./BranchJournalDestination');
    render(<I18nProvider initialLocale="ru"><BranchJournalDestination backend={backend as never} /></I18nProvider>);
    await waitFor(() => expect(screen.getByText('shift.opened')).toBeInTheDocument());
    // Верифицируем именно branch-запрос: search вызван с branchId 'b1' первым аргументом.
    expect(search).toHaveBeenCalled();
    const firstArg = (search.mock.calls[0] as unknown[])[0];
    expect(firstArg).toMatchObject({ branchId: 'b1' });
    expect(screen.getByText('Shift (s1)')).toBeInTheDocument();
  });
});
