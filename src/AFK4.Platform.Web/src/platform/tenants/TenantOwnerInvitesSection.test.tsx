import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, vi, beforeAll } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { TenantOwnerInvitesSection } from './TenantOwnerInvitesSection';
import type { OwnerInviteSummary, TenantBranch } from '@/api/types';

beforeAll(() => {
  window.HTMLElement.prototype.hasPointerCapture = () => false;
  window.HTMLElement.prototype.scrollIntoView = () => {};
  window.HTMLElement.prototype.releasePointerCapture = () => {};
});

const branches: TenantBranch[] = [
  { branchId: 'b1', slug: 'main', name: 'Main', city: 'Moscow', createdAtUtc: '2026-01-01T00:00:00Z' }
];

function summary(over: Partial<OwnerInviteSummary>): OwnerInviteSummary {
  return {
    ownerInviteId: 'i1', organizationId: 'o1', branchId: 'b1', codeSuffix: '1234',
    status: 'pending', ownerUserName: 'owner@x.io', ownerDisplayName: 'Owner',
    expiresAtUtc: '2026-02-01T00:00:00Z', acceptedAtUtc: null, revokedAtUtc: null,
    revokedReason: null, createdAtUtc: '2026-01-01T00:00:00Z', ...over
  };
}

function renderSection(client: any) {
  return render(
    <I18nProvider><ToastProvider>
      <TenantOwnerInvitesSection client={client} organizationId="o1" branches={branches} initialInvite={null} />
    </ToastProvider></I18nProvider>
  );
}

it('lists invites with a masked code', async () => {
  const client = { listOwnerInvites: vi.fn().mockResolvedValue([summary({})]), createOwnerInvite: vi.fn(), revokeOwnerInvite: vi.fn() };
  renderSection(client);
  expect(await screen.findByText('•••• 1234')).toBeTruthy();
  expect(screen.getByText('owner@x.io')).toBeTruthy();
});

it('creates a code and reveals the full code', async () => {
  const client = {
    listOwnerInvites: vi.fn(),
    createOwnerInvite: vi.fn().mockResolvedValue({
      ownerInviteId: 'i9', organizationId: 'o1', branchId: 'b1', code: 'FULL-CODE-9',
      status: 'pending', ownerUserName: null, ownerDisplayName: null,
      expiresAtUtc: '2026-02-01T00:00:00Z', acceptedAtUtc: null, revokedAtUtc: null,
      revokedReason: null, createdAtUtc: '2026-01-01T00:00:00Z'
    }),
    revokeOwnerInvite: vi.fn()
  };
  client.listOwnerInvites.mockResolvedValueOnce([]).mockResolvedValueOnce([summary({ ownerInviteId: 'i9', codeSuffix: 'DE-9', ownerUserName: null })]);
  renderSection(client);
  await screen.findByText('Кодов настройки пока нет.');

  fireEvent.click(screen.getByRole('button', { name: 'Создать код' }));
  await waitFor(() => expect(client.createOwnerInvite).toHaveBeenCalledWith('o1', 'b1', null, null, null));
  expect(await screen.findByText('FULL-CODE-9')).toBeTruthy();
});

it('revokes a pending invite with a reason', async () => {
  const client = {
    listOwnerInvites: vi.fn().mockResolvedValue([summary({})]),
    createOwnerInvite: vi.fn(),
    revokeOwnerInvite: vi.fn().mockResolvedValue(summary({ status: 'revoked' }))
  };
  renderSection(client);
  fireEvent.click(await screen.findByRole('button', { name: 'Отозвать' }));

  const reason = await screen.findByLabelText('Причина');
  fireEvent.change(reason, { target: { value: 'fraud' } });
  const confirmButtons = screen.getAllByRole('button', { name: 'Отозвать' });
  fireEvent.click(confirmButtons[confirmButtons.length - 1]);

  await waitFor(() => expect(client.revokeOwnerInvite).toHaveBeenCalledWith('i1', 'fraud'));
});
