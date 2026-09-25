import { afterAll, afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import type { BranchGameDto, CatalogGameDto } from '../../../api/clients/games';

const organizationId = 'o1';

const catalogGame: CatalogGameDto = {
  catalogGameId: 'c1', name: 'Counter-Strike 2', description: null, genre: 'Шутер', minAge: 16,
  launchKind: 'steam', launchTarget: '730', coverUrl: null, isPublished: true, updatedAtUtc: '2026-09-25T10:00:00Z'
};

function libraryGame(overrides: Partial<BranchGameDto> = {}): BranchGameDto {
  return {
    branchGameId: 'g1', catalogGameId: null, name: 'Dota 2', genre: 'MOBA', minAge: 12, coverUrl: null,
    launchKind: 'steam', launchTarget: '570', executablePath: null, arguments: null,
    availableWithoutSession: false, isEnabled: true, sortOrder: 0, ...overrides
  };
}

let library: BranchGameDto[] = [];
const games = {
  list: mock(async () => library),
  catalog: mock(async () => [catalogGame]),
  add: mock(async (_branchId: string, request: Record<string, unknown>) => {
    const added = libraryGame({ branchGameId: 'g2', name: String(request.name), catalogGameId: (request.catalogGameId as string) ?? null });
    library = [...library, added];
    return added;
  }),
  update: mock(async () => libraryGame()),
  remove: mock(async () => { library = []; }),
  reorder: mock(async () => library)
};

const actual = (globalThis as Record<string, unknown>).__afk4RealOperatorHelpers as Record<string, unknown>;
mock.module('../../../operatorHelpers', () => ({
  ...actual,
  createAuthenticatedOperatorClients: () => ({ games })
}));

const { GamesDestination } = await import('./GamesDestination');
const backend = { config: { platformBaseUrl: 'http://x' }, session: { accessToken: 't', organizationId }, branchId: 'b1' } as never;

function renderScreen(permissions = ['organization.games.manage']) {
  return render(
    <I18nProvider initialLocale="ru">
      <GamesDestination backend={backend} session={{ permissions, organizationId } as never} currencyCode="TJS" />
    </I18nProvider>
  );
}

afterEach(() => {
  library = [];
  Object.values(games).forEach((fn) => fn.mockClear());
  cleanup();
});

afterAll(() => mock.module('../../../operatorHelpers', () => actual));

describe('GamesDestination', () => {
  it('an empty library says what the PCs show meanwhile', async () => {
    renderScreen();

    expect(await screen.findByText('Библиотека пуста')).toBeInTheDocument();
    expect(screen.getByText(/ПК показывают игры из своих настроек/)).toBeInTheDocument();
  });

  it('adds a game from the catalog, linked to it', async () => {
    renderScreen();
    await screen.findByText('Библиотека пуста');

    fireEvent.click(screen.getAllByRole('button', { name: 'Добавить из каталога' })[0]);
    fireEvent.click(await screen.findByRole('button', { name: /Counter-Strike 2/ }));
    expect(screen.getByDisplayValue('730')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    await waitFor(() => expect(games.add).toHaveBeenCalledTimes(1));
    expect(games.add.mock.calls[0][1]).toMatchObject({ organizationId, catalogGameId: 'c1', launchKind: 'steam', launchTarget: '730' });
    expect(await screen.findByText('Counter-Strike 2')).toBeInTheDocument();
  });

  it('refuses a Steam game without a numeric AppID before asking the server', async () => {
    renderScreen();
    await screen.findByText('Библиотека пуста');

    fireEvent.click(screen.getByRole('button', { name: 'Своя игра' }));
    fireEvent.change(screen.getByLabelText('Название на ПК'), { target: { value: 'Dota 2' } });
    fireEvent.change(screen.getByLabelText('AppID в Steam'), { target: { value: 'dota' } });
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    expect(await screen.findByText('AppID в Steam — только цифры.')).toBeInTheDocument();
    expect(games.add).not.toHaveBeenCalled();
  });

  it('shows the launch and whether the game waits for a session', async () => {
    library = [libraryGame(), libraryGame({ branchGameId: 'g2', name: 'Steam', availableWithoutSession: true, launchKind: 'exe', executablePath: 'C:\\Steam\\steam.exe', launchTarget: null })];
    renderScreen();

    expect(await screen.findByText('Steam · 570')).toBeInTheDocument();
    expect(screen.getByText('Свой exe · C:\\Steam\\steam.exe')).toBeInTheDocument();
    expect(screen.getByText('Всегда')).toBeInTheDocument();
  });

  it('without the right, nothing can be added', async () => {
    renderScreen([]);

    await screen.findByText('Библиотека пуста');
    expect(screen.queryByRole('button', { name: 'Своя игра' })).toBeNull();
  });
});
