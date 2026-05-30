import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import type { AuditSearchResult } from '@/api/types';
import { JournalScreen } from './JournalScreen';

const record = {
  auditRecordId: 'a1', organizationId: 'o', branchId: 'b', actorStaffUserId: 'staff-1',
  action: 'session.start', targetType: 'Session', targetId: 'sess-9', outcome: 'Succeeded',
  sourceApp: 'operator', detailsJson: '{}', createdAtUtc: '2026-05-30T10:00:00.000Z',
  actorPlatformAdminUserId: null
};

function fakeClient() {
  return { searchAudit: vi.fn<() => Promise<AuditSearchResult>>(async () => ({ limit: 100, records: [record] })) };
}

it('renders audit rows', async () => {
  render(
    <I18nProvider><ToastProvider>
      <JournalScreen client={fakeClient() as never} branchId="b1" />
    </ToastProvider></I18nProvider>
  );
  expect(await screen.findByText('session.start')).toBeInTheDocument();
});

it('refetches with the applied action filter', async () => {
  const client = fakeClient();
  render(
    <I18nProvider><ToastProvider>
      <JournalScreen client={client as never} branchId="b1" />
    </ToastProvider></I18nProvider>
  );
  await screen.findByText('session.start');
  fireEvent.change(screen.getByLabelText('Действие'), { target: { value: 'login' } });
  fireEvent.click(screen.getByRole('button', { name: 'Применить' }));
  await waitFor(() =>
    expect(client.searchAudit.mock.calls.some(c => ((c as unknown[])[1] as { action?: string }).action === 'login')).toBe(true)
  );
});
