import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { OrganizationSupportNotesSection } from './OrganizationSupportNotesSection';
import type { OrganizationSupportNote } from '@/api/types';

function note(over: Partial<OrganizationSupportNote>): OrganizationSupportNote {
  return {
    organizationSupportNoteId: 'n1', organizationId: 'o1', authorPlatformAdminId: 'a1',
    authorDisplayName: 'Admin', body: 'first note', createdAtUtc: '2026-01-01T00:00:00Z', ...over
  };
}

function renderSection(client: any, canWrite = true) {
  return render(
    <I18nProvider><ToastProvider>
      <OrganizationSupportNotesSection client={client} organizationId="o1" canWrite={canWrite} />
    </ToastProvider></I18nProvider>
  );
}

it('lists existing notes', async () => {
  const client = { listSupportNotes: mock().mockResolvedValue([note({})]), createSupportNote: mock(), updateSupportNote: mock() };
  renderSection(client);
  expect(await screen.findByText('first note')).toBeTruthy();
  expect(screen.getByText('Admin')).toBeTruthy();
});

it('creates a note from the draft', async () => {
  const client = {
    listSupportNotes: mock().mockResolvedValue([]),
    createSupportNote: mock().mockResolvedValue(note({ organizationSupportNoteId: 'n2', body: 'added' })),
    updateSupportNote: mock()
  };
  renderSection(client);
  await screen.findByText('Заметок пока нет. Здесь поддержка записывает то, что следующему дежурному стоит знать об организации.');

  fireEvent.change(screen.getByRole('textbox', { name: 'Новая заметка' }), { target: { value: 'added' } });
  fireEvent.click(screen.getByRole('button', { name: 'Добавить заметку' }));
  await waitFor(() => expect(client.createSupportNote).toHaveBeenCalledWith('o1', 'added'));
});

it('edits a note inline', async () => {
  const client = {
    listSupportNotes: mock().mockResolvedValue([note({})]),
    createSupportNote: mock(),
    updateSupportNote: mock().mockResolvedValue(note({ body: 'edited' }))
  };
  renderSection(client);
  fireEvent.click(await screen.findByRole('button', { name: 'Редактировать' }));

  const editor = screen.getByRole('textbox', { name: 'Редактировать заметку' });
  fireEvent.change(editor, { target: { value: 'edited' } });
  fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));
  await waitFor(() => expect(client.updateSupportNote).toHaveBeenCalledWith('o1', 'n1', 'edited'));
});

// Смотреть заметки можно по праву на просмотр, писать — только по праву на правку. Форма и
// «Редактировать» у читающего обещали бы действие, на которое сервер ответит отказом.
it('shows notes without the form and the edit button to a read-only viewer', async () => {
  const client = { listSupportNotes: mock().mockResolvedValue([note({})]), createSupportNote: mock(), updateSupportNote: mock() };
  renderSection(client, false);
  expect(await screen.findByText('first note')).toBeTruthy();
  expect(screen.queryByRole('textbox', { name: 'Новая заметка' })).toBeNull();
  expect(screen.queryByRole('button', { name: 'Добавить заметку' })).toBeNull();
  expect(screen.queryByRole('button', { name: 'Редактировать' })).toBeNull();
});
