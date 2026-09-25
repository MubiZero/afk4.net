import type { MessageKey } from '@afk4/i18n';
import { GameLaunchKindNames, GameLibraryErrorCodeNames, type GameLaunchKindName } from '@afk4/contracts';
import { PlatformApiError } from '@/api/platformTransport';
import { describeApiError } from '@/api/describeApiError';
import type { CatalogGameDto, UpsertCatalogGameRequest } from '@/api/types';

type Translate = (key: MessageKey, values?: Record<string, string | number>) => string;

/** Порядок способов запуска в форме — тот, в котором их перечисляет контракт. */
export const LAUNCH_KINDS: readonly GameLaunchKindName[] = Object.values(GameLaunchKindNames);

/**
 * Возрастные отметки на выбор. Отметка только видна игроку — даты рождения у него нет, — поэтому
 * мелкая сетка не нужна. Сервер принимает 0–21: значение вне списка, заведённое раньше, форма
 * покажет как есть, а не подменит ближайшим.
 */
export const AGE_MARKS: readonly number[] = [0, 6, 12, 16, 18];

/**
 * Зеркало `GameLibraryLimits` и проверок `GameLibrary.Validate` на сервере. Форма ловит ошибку
 * до отправки и называет поле; сервер остаётся последним словом. Совпадение значений держит
 * тест, читающий C#.
 */
export const GAME_LIMITS = {
  maxNameLength: 120,
  maxDescriptionLength: 2000,
  maxGenreLength: 60,
  maxLaunchTargetLength: 200,
  maxPathLength: 512,
  maxCoverUrlLength: 1024,
  maxAge: 21
} as const;

export const STEAM_APP_ID_PATTERN = /^[0-9]{1,10}$/;
export const LAUNCHER_TARGET_PATTERN = /^[A-Za-z0-9_.:-]{1,200}$/;
export const WINDOWS_EXE_PATTERN = /^[A-Za-z]:\\[^"<>|?*\r\n]+\.exe$/i;

export interface GameForm {
  name: string;
  description: string;
  genre: string;
  /** '' — без отметки. */
  minAge: string;
  launchKind: GameLaunchKindName;
  launchTarget: string;
  coverUrl: string;
  isPublished: boolean;
}

export type GameFormField = 'name' | 'description' | 'genre' | 'launchTarget' | 'coverUrl';
export type GameFormErrors = Partial<Record<GameFormField, { key: MessageKey; values?: Record<string, number> }>>;

export function emptyGameForm(): GameForm {
  return {
    name: '',
    description: '',
    genre: '',
    minAge: '',
    launchKind: GameLaunchKindNames.Steam,
    launchTarget: '',
    coverUrl: '',
    isPublished: false
  };
}

export function formFromGame(game: CatalogGameDto): GameForm {
  return {
    name: game.name,
    description: game.description ?? '',
    genre: game.genre ?? '',
    minAge: game.minAge === null ? '' : String(game.minAge),
    // Как есть, без подмены: неизвестный способ, подменённый на exe, молча переписался бы при сохранении.
    launchKind: game.launchKind,
    launchTarget: game.launchTarget ?? '',
    coverUrl: game.coverUrl ?? '',
    isPublished: game.isPublished
  };
}

function isLaunchKind(value: string): value is GameLaunchKindName {
  return (LAUNCH_KINDS as readonly string[]).includes(value);
}

function blankToNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed === '' ? null : trimmed;
}

export function requestFromForm(form: GameForm): UpsertCatalogGameRequest {
  return {
    name: form.name.trim(),
    description: blankToNull(form.description),
    genre: blankToNull(form.genre),
    minAge: form.minAge === '' ? null : Number(form.minAge),
    launchKind: form.launchKind,
    launchTarget: blankToNull(form.launchTarget),
    coverUrl: blankToNull(form.coverUrl),
    isPublished: form.isPublished
  };
}

/** Обложка годится для показа: https-адрес, как требует сервер. */
export function isHttpsUrl(value: string): boolean {
  const trimmed = value.trim();
  if (trimmed === '' || trimmed.length > GAME_LIMITS.maxCoverUrlLength) return false;
  try {
    return new URL(trimmed).protocol === 'https:';
  } catch {
    return false;
  }
}

function targetError(kind: GameLaunchKindName, target: string): GameFormErrors['launchTarget'] {
  switch (kind) {
    case GameLaunchKindNames.Steam:
      return STEAM_APP_ID_PATTERN.test(target) ? undefined : { key: 'platform.games.error.steamTarget' };
    case GameLaunchKindNames.Executable:
      // Путь в каталоге необязателен: клуб укажет свой, когда добавит игру.
      if (target === '') return undefined;
      return target.length <= GAME_LIMITS.maxPathLength && WINDOWS_EXE_PATTERN.test(target)
        ? undefined
        : { key: 'platform.games.error.exePath', values: { max: GAME_LIMITS.maxPathLength } };
    default:
      return LAUNCHER_TARGET_PATTERN.test(target)
        ? undefined
        : { key: 'platform.games.error.launcherTarget', values: { max: GAME_LIMITS.maxLaunchTargetLength } };
  }
}

export function validateGameForm(form: GameForm): GameFormErrors {
  const errors: GameFormErrors = {};
  const name = form.name.trim();
  if (name === '') errors.name = { key: 'platform.games.error.nameRequired' };
  else if (name.length > GAME_LIMITS.maxNameLength) {
    errors.name = { key: 'platform.games.error.nameTooLong', values: { max: GAME_LIMITS.maxNameLength } };
  }
  if (form.description.trim().length > GAME_LIMITS.maxDescriptionLength) {
    errors.description = { key: 'platform.games.error.descriptionTooLong', values: { max: GAME_LIMITS.maxDescriptionLength } };
  }
  if (form.genre.trim().length > GAME_LIMITS.maxGenreLength) {
    errors.genre = { key: 'platform.games.error.genreTooLong', values: { max: GAME_LIMITS.maxGenreLength } };
  }
  const launchTarget = targetError(form.launchKind, form.launchTarget.trim());
  if (launchTarget !== undefined) errors.launchTarget = launchTarget;
  if (form.coverUrl.trim() !== '' && !isHttpsUrl(form.coverUrl)) {
    errors.coverUrl = { key: 'platform.games.error.coverUrl' };
  }
  return errors;
}

export function hasErrors(errors: GameFormErrors): boolean {
  return Object.keys(errors).length > 0;
}

const LAUNCH_KIND_LABEL_KEY: Record<GameLaunchKindName, MessageKey> = {
  steam: 'platform.games.kind.steam',
  epic: 'platform.games.kind.epic',
  riot: 'platform.games.kind.riot',
  battlenet: 'platform.games.kind.battlenet',
  exe: 'platform.games.kind.exe'
};

const LAUNCH_TARGET_LABEL_KEY: Record<GameLaunchKindName, MessageKey> = {
  steam: 'platform.games.target.steam',
  epic: 'platform.games.target.epic',
  riot: 'platform.games.target.riot',
  battlenet: 'platform.games.target.battlenet',
  exe: 'platform.games.target.exe'
};

const LAUNCH_TARGET_HINT_KEY: Record<GameLaunchKindName, MessageKey> = {
  steam: 'platform.games.target.steamHint',
  epic: 'platform.games.target.epicHint',
  riot: 'platform.games.target.riotHint',
  battlenet: 'platform.games.target.battlenetHint',
  exe: 'platform.games.target.exeHint'
};

/** Способ запуска, пришедший с сервера новее этого экрана, показывается своим машинным именем. */
export function describeLaunchKind(kind: string, t: Translate): string {
  return isLaunchKind(kind) ? t(LAUNCH_KIND_LABEL_KEY[kind]) : kind;
}

export function launchTargetLabelKey(kind: GameLaunchKindName): MessageKey {
  return LAUNCH_TARGET_LABEL_KEY[kind];
}

export function launchTargetHintKey(kind: GameLaunchKindName): MessageKey {
  return LAUNCH_TARGET_HINT_KEY[kind];
}

/** «16+»; без отметки — null. */
export function formatAgeMark(minAge: number | null): string | null {
  return minAge === null ? null : `${minAge}+`;
}

/** Отметки для выпадающего списка: стандартные и та, что уже стоит у игры, если она нестандартная. */
export function ageOptions(current: string): number[] {
  const value = current === '' ? null : Number(current);
  return value === null || AGE_MARKS.includes(value) ? [...AGE_MARKS] : [...AGE_MARKS, value].sort((a, b) => a - b);
}

// Отказ `invalid_game` приходит с английской фразой про поле. Форма проверяет те же правила до
// отправки, поэтому сюда доходит только расхождение с сервером — его и называем, а не «сбой».
const ERROR_CODE_KEY: Record<string, MessageKey> = {
  [GameLibraryErrorCodeNames.InvalidGame]: 'platform.games.error.invalid'
};

export function describeGameError(cause: unknown, t: Translate): string {
  if (cause instanceof PlatformApiError && cause.errorCode !== null) {
    const key = ERROR_CODE_KEY[cause.errorCode];
    if (key !== undefined) return t(key);
  }
  return describeApiError(cause, t);
}
