import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { TenantSupportNotesSection } from './TenantSupportNotesSection';
import type { TenantSupportNote } from '@/api/types';

function note(over: Partial<TenantSupportNote>): TenantSupportNote {
  return {
    tenantSupportNoteId: 'n1', organizationId: 'o1', authorPlatformAdminId: 'a1',
    authorDisplayName: 'Admin', body: 'first note', createdAtUtc: '2026-01-01T00:00:00Z', ...over
  };
}

function renderSection(client: any) {
  return render(
    <I18nProvider><ToastProvider>
      <TenantSupportNotesSection client={client} organizationId="o1" />
    </ToastProvider></I18nProvider>
  );
}

it('lists existing notes', async () => {
  const client = { listSupportNotes: vi.fn().mockResolvedValue([note({})]), createSupportNote: vi.fn(), updateSupportNote: vi.fn() };
  renderSection(client);
  expect(await screen.findByText('first note')).toBeTruthy();
  expect(screen.getByText('Admin')).toBeTruthy();
});

it('creates a note from the draft', async () => {
  const client = {
    listSupportNotes: vi.fn().mockResolvedValue([]),
    createSupportNote: vi.fn().mockResolvedValue(note({ tenantSupportNoteId: 'n2', body: 'added' })),
    updateSupportNote: vi.fn()
  };
  renderSection(client);
  await screen.findByText('Заметок поддержки пока нет.');

  fireEvent.change(screen.getByRole('textbox', { name: 'Новая заметка' }), { target: { value: 'added' } });
  fireEvent.click(screen.getByRole('button', { name: 'Добавить заметку' }));
  await waitFor(() => expect(client.createSupportNote).toHaveBeenCalledWith('o1', 'added'));
});

it('edits a note inline', async () => {
  const client = {
    listSupportNotes: vi.fn().mockResolvedValue([note({})]),
    createSupportNote: vi.fn(),
    updateSupportNote: vi.fn().mockResolvedValue(note({ body: 'edited' }))
  };
  renderSection(client);
  fireEvent.click(await screen.findByRole('button', { name: 'Редактировать' }));

  const editor = screen.getByRole('textbox', { name: 'Редактировать заметку' });
  fireEvent.change(editor, { target: { value: 'edited' } });
  fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));
  await waitFor(() => expect(client.updateSupportNote).toHaveBeenCalledWith('o1', 'n1', 'edited'));
});
