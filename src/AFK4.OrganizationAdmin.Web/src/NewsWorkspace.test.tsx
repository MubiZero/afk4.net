import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { NewsWorkspace } from './NewsWorkspace';
import { ManagementScreen } from './management/ManagementScreen';
import type { NewsItemDto, NewsItemInput, NewsScopeDto } from './operatorApiClients';
import { PlatformApiError } from './platformApi';

function client(initial: NewsItemDto[] = []) {
  const created: NewsItemInput[] = [];
  const removed: string[] = [];
  let store = [...initial];
  return {
    created,
    removed,
    list: async () => store,
    scope: async (): Promise<NewsScopeDto> => ({ branches: [{ branchId: 'b1', name: 'Центр' }], canPublishToAllBranches: true }),
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

function renderWorkspace(c: ReturnType<typeof client>, canManage?: boolean) {
  // Кнопка «+ Новость» живёт в шапке экрана (ScreenAction), поэтому список рендерится внутри него.
  render(<I18nProvider><ManagementScreen title="Новости"><NewsWorkspace backend={null} canManage={canManage} client={c as never} /></ManagementScreen></I18nProvider>);
}

describe('NewsWorkspace', () => {
  afterEach(() => cleanup());

  it('creates a news item via the drawer', async () => {
    const c = client();
    renderWorkspace(c);
    await waitFor(() => screen.getAllByRole('button', { name: '+ Новость' }));
    fireEvent.click(screen.getAllByRole('button', { name: '+ Новость' })[0]);
    fireEvent.change(screen.getByLabelText(/заголовок/i), { target: { value: 'Турнир' } });
    fireEvent.change(screen.getByLabelText(/текст/i), { target: { value: 'В субботу' } });
    fireEvent.click(screen.getByRole('button', { name: /сохранить/i }));
    await waitFor(() => expect(c.created).toHaveLength(1));
    expect(c.created[0].title).toBe('Турнир');
  });

  it('marks the news for the PC screen', async () => {
    const c = client();
    renderWorkspace(c);
    await screen.findAllByRole('button', { name: '+ Новость' });
    fireEvent.click(screen.getAllByRole('button', { name: '+ Новость' })[0]);
    fireEvent.change(screen.getByLabelText(/заголовок/i), { target: { value: 'Турнир' } });
    fireEvent.change(screen.getByLabelText(/текст/i), { target: { value: 'В субботу' } });
    fireEvent.click(screen.getByLabelText('Показывать на экране ПК'));
    fireEvent.click(screen.getByRole('button', { name: /сохранить/i }));
    await waitFor(() => expect(c.created).toHaveLength(1));
    expect(c.created[0].showOnPcs).toBe(true);
  });

  it('rejects an empty title in the drawer', async () => {
    const c = client();
    renderWorkspace(c);
    await screen.findAllByRole('button', { name: '+ Новость' });
    fireEvent.click(screen.getAllByRole('button', { name: '+ Новость' })[0]);
    fireEvent.click(screen.getByRole('button', { name: /сохранить/i }));
    await waitFor(() => screen.getByText(/заголовок и текст обязательны/i));
    expect(c.created).toHaveLength(0);
  });

  it('lists items and deletes one via confirmation', async () => {
    const c = client([{
      id: 'x1', branchId: null, title: 'Старая', body: 'B', imageUrl: null,
      isPublished: true, publishAtUtc: null, expiresAtUtc: null,
      createdAtUtc: '2026-06-01T00:00:00Z', updatedAtUtc: '2026-06-01T00:00:00Z'
    }]);
    renderWorkspace(c);
    fireEvent.click(await screen.findByText('Старая'));            // строка → drawer
    fireEvent.click(screen.getByRole('button', { name: /удалить/i })); // футер drawer
    fireEvent.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: /удалить/i }));
    await waitFor(() => expect(c.removed).toEqual(['x1']));
  });

  it('hides create/save/delete entirely when canManage is false — read-only view, not a 403 trap', async () => {
    const c = client([{
      id: 'x1', branchId: null, title: 'Старая', body: 'B', imageUrl: null,
      isPublished: true, publishAtUtc: null, expiresAtUtc: null,
      createdAtUtc: '2026-06-01T00:00:00Z', updatedAtUtc: '2026-06-01T00:00:00Z'
    }]);
    renderWorkspace(c, false);

    await screen.findByText('Старая');
    expect(screen.queryByRole('button', { name: '+ Новость' })).not.toBeInTheDocument();

    fireEvent.click(screen.getByText('Старая'));
    expect(screen.queryByRole('button', { name: /сохранить/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /удалить/i })).not.toBeInTheDocument();
    expect(screen.getByLabelText(/заголовок/i)).toBeDisabled();
  });

  // Список филиалов нужен новостям только ради подписи «где показывается» и выбора в форме. Его
  // отказ раньше оставлял экран в вечной загрузке — ошибку никто не ловил, и сами новости так и не
  // появлялись. Теперь новости на месте, а повтор спрашивает только филиалы.
  it('отказ филиалов не прячет новости и повторяет только филиалы', async () => {
    const c = client([{
      id: 'x1', branchId: 'b1', title: 'Турнир', body: 'B', imageUrl: null,
      isPublished: true, publishAtUtc: null, expiresAtUtc: null,
      createdAtUtc: '2026-06-01T00:00:00Z', updatedAtUtc: '2026-06-01T00:00:00Z'
    }]);
    const list = mock(c.list);
    const scope = mock()
      .mockRejectedValueOnce(new PlatformApiError('boom', 500, 'Internal Server Error', ''))
      .mockResolvedValue({ branches: [{ branchId: 'b1', name: 'Центр' }], canPublishToAllBranches: true });
    renderWorkspace({ ...c, list, scope });

    expect(await screen.findByText('Турнир')).toBeInTheDocument();
    expect(screen.getByText(/Не удалось загрузить филиалы/)).toHaveTextContent('Сервер вернул ошибку. Повторите позже.');

    fireEvent.click(screen.getByRole('button', { name: 'Повторить' }));

    expect(await screen.findByText('Центр')).toBeInTheDocument();
    expect(scope).toHaveBeenCalledTimes(2);
    expect(list).toHaveBeenCalledTimes(1);
  });

  it('отказ самих новостей называет причину вместо вечной загрузки', async () => {
    const c = client();
    const list = mock()
      .mockRejectedValueOnce(new PlatformApiError('boom', 500, 'Internal Server Error', ''))
      .mockResolvedValue([]);
    renderWorkspace({ ...c, list });

    expect(await screen.findByText('Сервер вернул ошибку. Повторите позже.')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Повторить' }));

    expect(await screen.findByText('Новостей пока нет')).toBeInTheDocument();
    expect(list).toHaveBeenCalledTimes(2);
  });

  it('управляющему одного филиала «на всю сеть» не предлагается — новость сразу в его филиале', async () => {
    const c = client();
    renderWorkspace({
      ...c,
      scope: async () => ({ branches: [{ branchId: 'b1', name: 'Центр' }], canPublishToAllBranches: false })
    });
    await screen.findAllByRole('button', { name: '+ Новость' });
    fireEvent.click(screen.getAllByRole('button', { name: '+ Новость' })[0]);

    const where = screen.getByLabelText(/филиал/i) as HTMLSelectElement;
    expect(where.value).toBe('b1');
    expect(within(where).queryByText('Все филиалы')).toBeNull();
  });

  it('отказ сохранения — в форме, введённое не пропадает, двойной клик не делает двух новостей', async () => {
    const c = client();
    let release: () => void = () => {};
    const create = mock(() => new Promise<never>((_resolve, reject) => {
      release = () => reject(new PlatformApiError('boom', 500, 'Internal Server Error', ''));
    }));
    renderWorkspace({ ...c, create });
    await screen.findAllByRole('button', { name: '+ Новость' });
    fireEvent.click(screen.getAllByRole('button', { name: '+ Новость' })[0]);
    fireEvent.change(screen.getByLabelText(/заголовок/i), { target: { value: 'Турнир' } });
    fireEvent.change(screen.getByLabelText(/текст/i), { target: { value: 'В субботу' } });

    const save = screen.getByRole('button', { name: /сохранить/i });
    fireEvent.click(save);
    fireEvent.click(save);
    release();

    expect(await screen.findByText('Сервер вернул ошибку. Повторите позже.')).toBeInTheDocument();
    expect(create).toHaveBeenCalledTimes(1);
    expect((screen.getByLabelText(/заголовок/i) as HTMLInputElement).value).toBe('Турнир');
  });
});
