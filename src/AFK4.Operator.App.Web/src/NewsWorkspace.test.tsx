import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { NewsWorkspace } from './NewsWorkspace';
import type { NewsItemDto, NewsItemInput, OwnerBranchSummaryDto } from './operatorApiClients';

function client(initial: NewsItemDto[] = []) {
  const created: NewsItemInput[] = [];
  const removed: string[] = [];
  let store = [...initial];
  return {
    created,
    removed,
    list: async () => store,
    listBranches: async (): Promise<OwnerBranchSummaryDto[]> => [{ branchId: 'b1', name: 'Центр' }],
    create: async (req: NewsItemInput) => {
      created.push(req);
      const dto: NewsItemDto = {
        id: 'new', branchId: req.branchId, title: req.title, body: req.body, imageUrl: req.imageUrl,
        isPublished: req.isPublished, publishAtUtc: req.publishAtUtc, expiresAtUtc: req.expiresAtUtc,
        createdAtUtc: '2026-06-10T00:00:00Z', updatedAtUtc: '2026-06-10T00:00:00Z'
      };
      store = [dto, ...store];
      return dto;
    },
    update: async (_id: string, req: NewsItemInput) => ({
      id: _id, branchId: req.branchId, title: req.title, body: req.body, imageUrl: req.imageUrl,
      isPublished: req.isPublished, publishAtUtc: req.publishAtUtc, expiresAtUtc: req.expiresAtUtc,
      createdAtUtc: '2026-06-10T00:00:00Z', updatedAtUtc: '2026-06-10T00:00:00Z'
    }),
    remove: async (id: string) => { removed.push(id); store = store.filter((n) => n.id !== id); }
  };
}

function renderWorkspace(c: ReturnType<typeof client>) {
  render(<I18nProvider><NewsWorkspace backend={null} client={c as never} /></I18nProvider>);
}

describe('NewsWorkspace', () => {
  afterEach(() => cleanup());

  it('creates a news item from the form', async () => {
    const c = client();
    renderWorkspace(c);
    await waitFor(() => screen.getByLabelText(/заголовок/i));
    fireEvent.change(screen.getByLabelText(/заголовок/i), { target: { value: 'Турнир' } });
    fireEvent.change(screen.getByLabelText(/текст/i), { target: { value: 'В субботу' } });
    fireEvent.click(screen.getByRole('button', { name: /сохранить/i }));
    await waitFor(() => expect(c.created).toHaveLength(1));
    expect(c.created[0].title).toBe('Турнир');
  });

  it('rejects an empty title', async () => {
    const c = client();
    renderWorkspace(c);
    await waitFor(() => screen.getByLabelText(/заголовок/i));
    fireEvent.click(screen.getByRole('button', { name: /сохранить/i }));
    await waitFor(() => screen.getByText(/заголовок и текст обязательны/i));
    expect(c.created).toHaveLength(0);
  });

  it('lists existing items and deletes one', async () => {
    const c = client([{
      id: 'x1', branchId: null, title: 'Старая', body: 'B', imageUrl: null,
      isPublished: true, publishAtUtc: null, expiresAtUtc: null,
      createdAtUtc: '2026-06-01T00:00:00Z', updatedAtUtc: '2026-06-01T00:00:00Z'
    }]);
    renderWorkspace(c);
    await waitFor(() => screen.getByText(/Старая/));
    fireEvent.click(screen.getByRole('button', { name: /удалить/i }));
    await waitFor(() => expect(c.removed).toEqual(['x1']));
  });
});
