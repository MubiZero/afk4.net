import { describe, expect, it, mock } from 'bun:test';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { PlatformApiError } from '@/api/platformTransport';
import type { CatalogGameDto } from '@/api/types';
import type { GamesApi } from '@/api/platformClients/games';
import { GamesScreen } from './GamesScreen';

type Client = Pick<GamesApi, 'listGames' | 'createGame' | 'updateGame' | 'steamCover' | 'uploadCover'>;

function game(overrides: Partial<CatalogGameDto> = {}): CatalogGameDto {
  return {
    catalogGameId: '11111111-1111-1111-1111-111111111111',
    name: 'Counter-Strike 2',
    description: 'Командный шутер',
    genre: 'Шутер',
    minAge: 16,
    launchKind: 'steam',
    launchTarget: '730',
    coverUrl: 'https://cdn.example.com/cs2.jpg',
    isPublished: true,
    updatedAtUtc: '2026-09-25T10:00:00Z',
    ...overrides
  };
}

const fortnite = game({
  catalogGameId: '22222222-2222-2222-2222-222222222222',
  name: 'Fortnite',
  description: null,
  genre: null,
  minAge: null,
  launchKind: 'epic',
  launchTarget: 'Fortnite',
  coverUrl: null,
  isPublished: false
});

function makeClient(overrides: Partial<Client> = {}): Client {
  return {
    listGames: mock(async () => [game(), fortnite]),
    createGame: mock(async () => game()),
    updateGame: mock(async () => game()),
    steamCover: mock(async (appId: string) => ({ url: `https://media.test/platform/catalog-cover/steam-${appId}.jpg` })),
    uploadCover: mock(async () => ({ url: 'https://media.test/platform/catalog-cover/own.png' })),
    ...overrides
  };
}

function renderScreen(client: Client) {
  return render(
    <I18nProvider>
      <ToastProvider>
        <GamesScreen client={client} />
      </ToastProvider>
    </I18nProvider>
  );
}

// Строка по названию в первой колонке: у Fortnite то же слово стоит и целью запуска Epic.
function rowOf(name: string): HTMLElement {
  const row = screen.getAllByRole('row').find(candidate => candidate.querySelector('td')?.textContent === name);
  if (row === undefined) throw new Error(`no row for ${name}`);
  return row;
}

describe('GamesScreen', () => {
  it('показывает игры каталога: жанр, возраст, запуск и состояние', async () => {
    renderScreen(makeClient());
    await screen.findByText('Counter-Strike 2');

    const cs2 = rowOf('Counter-Strike 2');
    expect(within(cs2).getByText('Шутер')).toBeInTheDocument();
    expect(within(cs2).getByText('16+')).toBeInTheDocument();
    expect(cs2).toHaveTextContent('Steam · 730');
    expect(within(cs2).getByText('Опубликована')).toBeInTheDocument();
    expect(cs2.querySelector('img')).toHaveAttribute('src', 'https://cdn.example.com/cs2.jpg');

    const draft = rowOf('Fortnite');
    expect(draft).toHaveTextContent('Epic Games · Fortnite');
    expect(within(draft).getByText('Черновик')).toBeInTheDocument();
    // Без обложки — пустая плашка того же размера, а не битая картинка.
    expect(draft.querySelector('img')).toBeNull();
  });

  it('пустой каталог объясняет, зачем он, и предлагает добавить игру', async () => {
    renderScreen(makeClient({ listGames: mock(async () => []) }));

    expect(await screen.findByText(/Из него клубы добавляют игры на свои ПК/)).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Добавить первую игру' }));
    expect(screen.getByRole('dialog', { name: 'Новая игра' })).toBeInTheDocument();
  });

  it('добавляет игру с тем, что ввёл человек', async () => {
    const client = makeClient();
    renderScreen(client);
    await screen.findByText('Counter-Strike 2');

    await userEvent.click(screen.getByRole('button', { name: 'Добавить игру' }));
    const dialog = screen.getByRole('dialog', { name: 'Новая игра' });
    await userEvent.type(within(dialog).getByLabelText('Название'), '  Dota 2 ');
    await userEvent.type(within(dialog).getByLabelText('Жанр'), 'MOBA');
    await userEvent.selectOptions(within(dialog).getByLabelText('Возраст'), '12');
    await userEvent.type(within(dialog).getByLabelText(/^AppID Steam/), '570');
    await userEvent.type(within(dialog).getByLabelText(/^Обложка/), 'https://cdn.example.com/dota2.jpg');
    await userEvent.click(within(dialog).getByRole('switch', { name: 'Опубликовать' }));

    // Предпросмотр — та самая картинка, что уедет на ПК.
    expect(within(dialog).getByAltText('Предпросмотр обложки')).toHaveAttribute('src', 'https://cdn.example.com/dota2.jpg');

    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    await waitFor(() => expect(client.createGame).toHaveBeenCalledWith({
      name: 'Dota 2',
      description: null,
      genre: 'MOBA',
      minAge: 12,
      launchKind: 'steam',
      launchTarget: '570',
      coverUrl: 'https://cdn.example.com/dota2.jpg',
      isPublished: true
    }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(client.listGames).toHaveBeenCalledTimes(2);
  });

  // Владелец, 26.09: «пусть подтягивается красивая картинка» — одна кнопка вместо поиска ссылки.
  it('подтягивает обложку из Steam по AppID и показывает её', async () => {
    const client = makeClient();
    renderScreen(client);
    await screen.findByText('Counter-Strike 2');

    await userEvent.click(screen.getByRole('button', { name: 'Добавить игру' }));
    const dialog = screen.getByRole('dialog', { name: 'Новая игра' });
    // Кнопки нет, пока не понятно, у какой игры Steam спрашивать.
    expect(within(dialog).queryByRole('button', { name: 'Подтянуть из Steam' })).toBeNull();
    await userEvent.type(within(dialog).getByLabelText(/^AppID Steam/), '570');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Подтянуть из Steam' }));

    await waitFor(() => expect(client.steamCover).toHaveBeenCalledWith('570'));
    expect(await within(dialog).findByAltText('Предпросмотр обложки'))
      .toHaveAttribute('src', 'https://media.test/platform/catalog-cover/steam-570.jpg');
  });

  it('говорит по-человечески, если у Steam картинки нет', async () => {
    const client = makeClient({
      steamCover: mock(async () => { throw new PlatformApiError(404, 'steam_cover_not_found', 'steam_cover_not_found'); })
    });
    renderScreen(client);
    await screen.findByText('Counter-Strike 2');

    await userEvent.click(screen.getByRole('button', { name: 'Добавить игру' }));
    const dialog = screen.getByRole('dialog', { name: 'Новая игра' });
    await userEvent.type(within(dialog).getByLabelText(/^AppID Steam/), '99999');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Подтянуть из Steam' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent('У Steam нет картинки для этого номера приложения');
  });

  it('загружает свою картинку в хранилище платформы', async () => {
    const client = makeClient();
    renderScreen(client);
    await screen.findByText('Counter-Strike 2');

    await userEvent.click(screen.getByRole('button', { name: 'Добавить игру' }));
    const dialog = screen.getByRole('dialog', { name: 'Новая игра' });
    const file = new File([new Uint8Array([0x89, 0x50, 0x4e, 0x47])], 'cover.png', { type: 'image/png' });
    await userEvent.upload(dialog.querySelector('input[type=file]') as HTMLInputElement, file);

    await waitFor(() => expect(client.uploadCover).toHaveBeenCalledWith(file));
    expect(await within(dialog).findByAltText('Предпросмотр обложки'))
      .toHaveAttribute('src', 'https://media.test/platform/catalog-cover/own.png');
  });

  it('не отправляет AppID Steam из букв и говорит, что не так, у самого поля', async () => {
    const client = makeClient();
    renderScreen(client);
    await screen.findByText('Counter-Strike 2');

    await userEvent.click(screen.getByRole('button', { name: 'Добавить игру' }));
    const dialog = screen.getByRole('dialog', { name: 'Новая игра' });
    await userEvent.type(within(dialog).getByLabelText('Название'), 'Dota 2');
    const target = within(dialog).getByLabelText(/^AppID Steam/);
    await userEvent.type(target, 'dota2');

    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    expect(client.createGame).not.toHaveBeenCalled();
    expect(within(dialog).getByText('AppID Steam — число до 10 цифр, например 730.')).toBeInTheDocument();
    expect(target).toHaveAttribute('aria-invalid', 'true');
    expect(target).toHaveFocus();
  });

  it('подпись и подсказка цели запуска меняются вместе со способом', async () => {
    renderScreen(makeClient());
    await screen.findByText('Counter-Strike 2');

    await userEvent.click(screen.getByRole('button', { name: 'Добавить игру' }));
    const dialog = screen.getByRole('dialog', { name: 'Новая игра' });
    await userEvent.selectOptions(within(dialog).getByLabelText(/^Способ запуска/), 'riot');

    expect(within(dialog).getByLabelText(/^Продукт Riot/)).toBeInTheDocument();
    expect(within(dialog).getByText(/league_of_legends или valorant/)).toBeInTheDocument();
    expect(within(dialog).queryByLabelText(/^AppID Steam/)).not.toBeInTheDocument();
  });

  it('правка открывает ту же форму с данными игры и сохраняет её по id', async () => {
    const client = makeClient();
    renderScreen(client);
    await screen.findByText('Counter-Strike 2');

    await userEvent.click(within(rowOf('Fortnite')).getByRole('button', { name: 'Изменить' }));
    const dialog = screen.getByRole('dialog', { name: 'Изменить игру' });
    expect(within(dialog).getByLabelText('Название')).toHaveValue('Fortnite');
    expect(within(dialog).getByLabelText(/^Способ запуска/)).toHaveValue('epic');
    expect(within(dialog).getByLabelText(/^Имя приложения Epic/)).toHaveValue('Fortnite');
    expect(within(dialog).getByLabelText('Возраст')).toHaveValue('');
    expect(within(dialog).getByRole('switch', { name: 'Опубликовать' })).not.toBeChecked();

    // Публикация — переключатель в той же форме: отдельной кнопки у строки нет.
    await userEvent.click(within(dialog).getByRole('switch', { name: 'Опубликовать' }));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    await waitFor(() => expect(client.updateGame).toHaveBeenCalledWith('22222222-2222-2222-2222-222222222222', {
      name: 'Fortnite',
      description: null,
      genre: null,
      minAge: null,
      launchKind: 'epic',
      launchTarget: 'Fortnite',
      coverUrl: null,
      isPublished: true
    }));
    expect(client.createGame).not.toHaveBeenCalled();
  });

  it('отказ сервера показывает своей фразой и не закрывает форму', async () => {
    const client = makeClient({
      updateGame: mock(async () => {
        throw new PlatformApiError(400, 'A Steam game needs its AppID (digits).', 'invalid_game');
      })
    });
    renderScreen(client);
    await screen.findByText('Counter-Strike 2');

    await userEvent.click(within(rowOf('Counter-Strike 2')).getByRole('button', { name: 'Изменить' }));
    const dialog = screen.getByRole('dialog', { name: 'Изменить игру' });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    const alert = await within(dialog).findByRole('alert');
    expect(alert).toHaveTextContent('Сервер не принял игру');
    expect(alert).not.toHaveTextContent('AppID (digits)');
    expect(within(dialog).getByLabelText('Название')).toHaveValue('Counter-Strike 2');
  });

  it('при сбое загрузки даёт повторить', async () => {
    let calls = 0;
    const client = makeClient({
      listGames: mock(async () => {
        calls += 1;
        if (calls === 1) throw new PlatformApiError(500, 'boom', null);
        return [game()];
      })
    });
    renderScreen(client);

    expect(await screen.findByText('Не удалось загрузить каталог игр')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Повторить' }));
    expect(await screen.findByText('Counter-Strike 2')).toBeInTheDocument();
  });
});
