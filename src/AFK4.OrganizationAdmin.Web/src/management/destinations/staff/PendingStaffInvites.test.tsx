import { afterAll, afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import type { StaffInviteSummaryDto } from '@afk4/contracts';

const invite = (overrides: Partial<StaffInviteSummaryDto> = {}): StaffInviteSummaryDto => ({
  staffInviteId: 'i1',
  userName: '992930000010',
  displayName: 'Фарход',
  phoneNumber: '+992930000010',
  email: null,
  roleNames: ['operator'],
  createdAtUtc: '2026-09-25T08:00:00Z',
  expiresAtUtc: '2026-09-26T08:00:00Z',
  attemptsLeft: 3,
  status: 'pending',
  ...overrides
});

let stored: StaffInviteSummaryDto[] = [];
const listStaffInvites = mock(async () => stored);
const revokeStaffInvite = mock(async (_branchId: string, id: string) => { stored = stored.filter((item) => item.staffInviteId !== id); });
const createStaffInvite = mock(async () => {
  stored = [invite({ staffInviteId: 'i2' })];
  return { staffInviteId: 'i2', code: '482915', expiresAtUtc: '2026-09-26T09:00:00Z' };
});

const actual = (globalThis as Record<string, unknown>).__afk4RealOperatorHelpers as Record<string, unknown>;
mock.module('../../../operatorHelpers', () => ({
  ...actual,
  createAuthenticatedOperatorClients: () => ({ settings: { listStaffInvites, revokeStaffInvite, createStaffInvite } })
}));

const { PendingStaffInvites } = await import('./PendingStaffInvites');
const backend = { config: { platformBaseUrl: 'http://x' }, session: { accessToken: 't', organizationId: 'o1' }, branchId: 'b1' } as never;

function renderList() {
  return render(
    <I18nProvider initialLocale="ru">
      <PendingStaffInvites backend={backend} refreshKey={0} />
    </I18nProvider>
  );
}

afterEach(() => {
  stored = [];
  listStaffInvites.mockClear();
  revokeStaffInvite.mockClear();
  createStaffInvite.mockClear();
  cleanup();
});

afterAll(() => mock.module('../../../operatorHelpers', () => actual));

describe('PendingStaffInvites', () => {
  it('shows nothing when everyone added has signed in', async () => {
    const { container } = renderList();
    await waitFor(() => expect(listStaffInvites).toHaveBeenCalled());
    expect(container.querySelector('.staff-pending')).toBeNull();
  });

  it('says what happened to each code', async () => {
    stored = [invite(), invite({ staffInviteId: 'i3', displayName: 'Нигора', status: 'expired' }), invite({ staffInviteId: 'i4', displayName: 'Бахтиёр', status: 'exhausted', attemptsLeft: 0 })];
    renderList();

    expect(await screen.findByText('Ждут первого входа · 3')).toBeInTheDocument();
    expect(screen.getByText(/Код действует до/)).toBeInTheDocument();
    expect(screen.getByText('Код истёк — выдайте новый')).toBeInTheDocument();
    expect(screen.getByText('Три неверных кода — этот больше не пустит')).toBeInTheDocument();
  });

  // Новый код — то же добавление с теми же данными: сервер гасит старый код сам.
  it('issues a new code with the same person and shows it grouped for dictation', async () => {
    stored = [invite({ status: 'expired' })];
    renderList();

    fireEvent.click(await screen.findByRole('button', { name: 'Новый код' }));

    expect(await screen.findByText('Новый код: 482 915 · передайте сотруднику')).toBeInTheDocument();
    expect(createStaffInvite).toHaveBeenCalledWith('b1', {
      organizationId: 'o1',
      userName: '992930000010',
      displayName: 'Фарход',
      phoneNumber: '+992930000010',
      email: null,
      roleNames: ['operator']
    });
  });

  it('revokes a code and drops the row', async () => {
    stored = [invite()];
    const { container } = renderList();

    fireEvent.click(await screen.findByRole('button', { name: 'Отозвать' }));

    await waitFor(() => expect(revokeStaffInvite).toHaveBeenCalledWith('b1', 'i1'));
    await waitFor(() => expect(container.querySelector('.staff-pending')).toBeNull());
  });
});
