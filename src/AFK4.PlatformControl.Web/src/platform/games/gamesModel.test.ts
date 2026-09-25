import { describe, expect, it } from 'bun:test';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { PlatformApiError } from '@/api/platformTransport';
import type { CatalogGameDto } from '@/api/types';
import {
  GAME_LIMITS,
  LAUNCHER_TARGET_PATTERN,
  STEAM_APP_ID_PATTERN,
  WINDOWS_EXE_PATTERN,
  ageOptions,
  describeGameError,
  emptyGameForm,
  formFromGame,
  formatAgeMark,
  requestFromForm,
  validateGameForm,
  type GameForm
} from './gamesModel';

const t = (key: string) => key;

function form(overrides: Partial<GameForm> = {}): GameForm {
  return { ...emptyGameForm(), name: 'Counter-Strike 2', launchTarget: '730', ...overrides };
}

function game(overrides: Partial<CatalogGameDto> = {}): CatalogGameDto {
  return {
    catalogGameId: '22222222-2222-2222-2222-222222222222',
    name: 'Fortnite',
    description: null,
    genre: 'Королевская битва',
    minAge: 12,
    launchKind: 'epic',
    launchTarget: 'Fortnite',
    coverUrl: null,
    isPublished: true,
    updatedAtUtc: '2026-09-25T10:00:00Z',
    ...overrides
  };
}

const repoSource = (...path: string[]) =>
  readFileSync(join(import.meta.dir, '..', '..', '..', '..', '..', 'src', ...path), 'utf8');

describe('проверка формы игры', () => {
  it('пропускает правильную игру Steam', () => {
    expect(validateGameForm(form())).toEqual({});
  });

  it('требует название', () => {
    expect(validateGameForm(form({ name: '   ' })).name?.key).toBe('platform.games.error.nameRequired');
  });

  it('у Steam — только цифры AppID, и пустым он быть не может', () => {
    expect(validateGameForm(form({ launchTarget: 'cs2' })).launchTarget?.key).toBe('platform.games.error.steamTarget');
    expect(validateGameForm(form({ launchTarget: '' })).launchTarget?.key).toBe('platform.games.error.steamTarget');
    expect(validateGameForm(form({ launchTarget: '12345678901' })).launchTarget?.key).toBe('platform.games.error.steamTarget');
  });

  it('у лаунчеров — имя без пробелов и лишних знаков', () => {
    expect(validateGameForm(form({ launchKind: 'epic', launchTarget: 'Fortnite' }))).toEqual({});
    expect(validateGameForm(form({ launchKind: 'riot', launchTarget: 'league_of_legends' }))).toEqual({});
    expect(validateGameForm(form({ launchKind: 'battlenet', launchTarget: 'Pro' }))).toEqual({});
    // Цель подставляется в командную строку лаунчера: пробел или кавычка сломали бы запуск на всём зале.
    expect(validateGameForm(form({ launchKind: 'epic', launchTarget: 'Fort nite' })).launchTarget?.key)
      .toBe('platform.games.error.launcherTarget');
    expect(validateGameForm(form({ launchKind: 'riot', launchTarget: '' })).launchTarget?.key)
      .toBe('platform.games.error.launcherTarget');
  });

  it('у своего exe путь необязателен, но если есть — полный путь Windows к .exe', () => {
    expect(validateGameForm(form({ launchKind: 'exe', launchTarget: '' }))).toEqual({});
    expect(validateGameForm(form({ launchKind: 'exe', launchTarget: 'C:\\Games\\Dota\\dota2.exe' }))).toEqual({});
    expect(validateGameForm(form({ launchKind: 'exe', launchTarget: 'dota2.exe' })).launchTarget?.key)
      .toBe('platform.games.error.exePath');
  });

  it('обложка — только https', () => {
    expect(validateGameForm(form({ coverUrl: 'https://cdn.example.com/cs2.jpg' }))).toEqual({});
    expect(validateGameForm(form({ coverUrl: 'http://cdn.example.com/cs2.jpg' })).coverUrl?.key).toBe('platform.games.error.coverUrl');
    expect(validateGameForm(form({ coverUrl: 'cs2.jpg' })).coverUrl?.key).toBe('platform.games.error.coverUrl');
  });

  it('называет предел длины в тексте ошибки', () => {
    const errors = validateGameForm(form({ name: 'x'.repeat(GAME_LIMITS.maxNameLength + 1), genre: 'x'.repeat(GAME_LIMITS.maxGenreLength + 1) }));
    expect(errors.name).toEqual({ key: 'platform.games.error.nameTooLong', values: { max: GAME_LIMITS.maxNameLength } });
    expect(errors.genre).toEqual({ key: 'platform.games.error.genreTooLong', values: { max: GAME_LIMITS.maxGenreLength } });
  });
});

describe('запрос из формы', () => {
  it('обрезает пробелы, пустое шлёт как null, возраст — числом', () => {
    expect(requestFromForm(form({
      name: '  Counter-Strike 2 ',
      description: '  ',
      genre: ' Шутер ',
      minAge: '16',
      launchTarget: ' 730 ',
      coverUrl: '',
      isPublished: true
    }))).toEqual({
      name: 'Counter-Strike 2',
      description: null,
      genre: 'Шутер',
      minAge: 16,
      launchKind: 'steam',
      launchTarget: '730',
      coverUrl: null,
      isPublished: true
    });
  });

  it('без отметки возраста шлёт null, а не ноль: «0+» — это тоже отметка', () => {
    expect(requestFromForm(form({ minAge: '' })).minAge).toBeNull();
    expect(requestFromForm(form({ minAge: '0' })).minAge).toBe(0);
  });

  it('форма правки повторяет игру из каталога', () => {
    const source = game();
    const { catalogGameId: _id, updatedAtUtc: _at, ...expected } = source;
    expect(requestFromForm(formFromGame(source))).toEqual(expected);
  });
});

describe('возраст', () => {
  it('пишется отметкой «N+», без отметки — пусто', () => {
    expect(formatAgeMark(16)).toBe('16+');
    expect(formatAgeMark(0)).toBe('0+');
    expect(formatAgeMark(null)).toBeNull();
  });

  it('нестандартную отметку, заведённую раньше, список не теряет', () => {
    expect(ageOptions('')).toEqual([0, 6, 12, 16, 18]);
    expect(ageOptions('21')).toEqual([0, 6, 12, 16, 18, 21]);
    expect(ageOptions('16')).toEqual([0, 6, 12, 16, 18]);
  });
});

describe('отказ сервера', () => {
  it('invalid_game называет своей фразой, а не английским текстом сервера', () => {
    const cause = new PlatformApiError(400, 'A Steam game needs its AppID (digits).', 'invalid_game');
    expect(describeGameError(cause, t)).toBe('platform.games.error.invalid');
  });

  it('нехватку прав отдаёт общему разбору', () => {
    expect(describeGameError(new PlatformApiError(403, 'Forbidden', null), t)).toBe('state.error.forbidden');
  });
});

// Форма проверяет то же, что сервер, чтобы назвать поле до отправки. Разойдись правила — форма
// либо пропустит то, что сервер отвергнет, либо не даст сохранить то, что он принял бы.
describe('совпадение с сервером', () => {
  it('пределы длины — те же, что GameLibraryLimits', () => {
    const source = repoSource('AFK4.Shared.Contracts', 'Games', 'GameLibraryContracts.cs');
    const limit = (name: string) => {
      const match = source.match(new RegExp(`public const int ${name}\\s*=\\s*(\\d+);`));
      expect(match).not.toBeNull();
      return Number(match![1]);
    };
    expect({ ...GAME_LIMITS } as Record<string, number>).toEqual({
      maxNameLength: limit('MaxNameLength'),
      maxDescriptionLength: limit('MaxDescriptionLength'),
      maxGenreLength: limit('MaxGenreLength'),
      maxLaunchTargetLength: limit('MaxLaunchTargetLength'),
      maxPathLength: limit('MaxPathLength'),
      maxCoverUrlLength: limit('MaxCoverUrlLength'),
      maxAge: limit('MaxAge')
    });
  });

  it('шаблоны целей запуска — те же, что в GameLibrary', () => {
    const source = repoSource('AFK4.Platform.Api', 'Games', 'GameLibrary.cs');
    const pattern = (method: string) => {
      const match = source.match(new RegExp(`\\[GeneratedRegex\\((@?)"((?:[^"]|"")*)"[^\\]]*\\]\\s*private static partial Regex ${method}\\(`));
      expect(match).not.toBeNull();
      // В дословной строке C# (@"...") кавычка удваивается — в регулярке JS она одна.
      return match![1] === '@' ? match![2].replace(/""/g, '"') : match![2];
    };
    expect(STEAM_APP_ID_PATTERN.source).toBe(pattern('SteamAppIdPattern'));
    expect(LAUNCHER_TARGET_PATTERN.source).toBe(pattern('LauncherTargetPattern'));
    expect(WINDOWS_EXE_PATTERN.source).toBe(pattern('WindowsExePattern'));
    expect(WINDOWS_EXE_PATTERN.flags).toContain('i');
  });
});
