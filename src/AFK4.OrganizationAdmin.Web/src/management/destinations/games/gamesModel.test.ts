import { describe, expect, it } from 'bun:test';
import { buildGameRequest, emptyGameForm, formFromCatalog, moved, validateGame } from './gamesModel';

const cs2 = {
  catalogGameId: 'c1',
  name: 'Counter-Strike 2',
  description: null,
  genre: 'Шутер',
  minAge: 16,
  launchKind: 'steam',
  launchTarget: '730',
  coverUrl: 'https://media.afk4.net/cs2.webp',
  isPublished: true,
  updatedAtUtc: '2026-09-25T10:00:00Z'
};

describe('gamesModel', () => {
  it('a catalog game arrives filled in, and goes out linked to the catalog', () => {
    const form = formFromCatalog(cs2);

    expect(validateGame(form)).toBeNull();
    expect(buildGameRequest('o1', form)).toEqual({
      organizationId: 'o1',
      catalogGameId: 'c1',
      name: 'Counter-Strike 2',
      genre: 'Шутер',
      minAge: 16,
      launchKind: 'steam',
      launchTarget: '730',
      executablePath: null,
      arguments: null,
      availableWithoutSession: false,
      isEnabled: true
    });
  });

  // Та же проверка, что на сервере: человек узнаёт об ошибке у поля.
  it('checks the launch the way the server does', () => {
    expect(validateGame({ ...emptyGameForm, name: 'Dota 2', launchTarget: 'dota' })).toBe('op.games.error.steam');
    expect(validateGame({ ...emptyGameForm, name: 'Dota 2', launchTarget: '570' })).toBeNull();
    expect(validateGame({ ...emptyGameForm, name: 'LoL', launchKind: 'riot', launchTarget: 'league of legends' })).toBe('op.games.error.target');
    expect(validateGame({ ...emptyGameForm, name: 'Своя', launchKind: 'exe' })).toBe('op.games.error.pathRequired');
    expect(validateGame({ ...emptyGameForm, name: 'Своя', launchKind: 'exe', executablePath: 'D:\\Games\\own.bat' })).toBe('op.games.error.path');
    expect(validateGame({ ...emptyGameForm, name: 'Своя', launchKind: 'exe', executablePath: 'D:\\Games\\own.exe' })).toBeNull();
    expect(validateGame({ ...emptyGameForm, name: ' ' })).toBe('op.games.error.name');
  });

  // Свой путь к exe заменяет лаунчер: цель лаунчера тогда не нужна.
  it('an own exe path overrides the launcher target', () => {
    expect(validateGame({ ...emptyGameForm, name: 'Dota 2', launchTarget: '', executablePath: 'E:\\Steam\\dota2.exe' })).toBeNull();
  });

  it('moves a game within the order and refuses to fall off the ends', () => {
    expect(moved(['a', 'b', 'c'], 'b', -1)).toEqual(['b', 'a', 'c']);
    expect(moved(['a', 'b', 'c'], 'c', 1)).toBeNull();
  });
});
