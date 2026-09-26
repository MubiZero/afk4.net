import { GameLaunchKindNames } from '@afk4/contracts';
import type { MessageKey } from '@afk4/i18n';
import type { BranchGameDto, CatalogGameDto, UpsertBranchGameRequest } from '../../../api/clients/games';

export const launchKinds = [
  GameLaunchKindNames.Steam,
  GameLaunchKindNames.Epic,
  GameLaunchKindNames.Riot,
  GameLaunchKindNames.BattleNet,
  GameLaunchKindNames.Executable
] as const;

export type LaunchKind = (typeof launchKinds)[number];

export const launchKindLabel: Record<LaunchKind, MessageKey> = {
  steam: 'op.games.kind.steam',
  epic: 'op.games.kind.epic',
  riot: 'op.games.kind.riot',
  battlenet: 'op.games.kind.battlenet',
  exe: 'op.games.kind.exe'
};

/** Что спросить в поле цели запуска — у каждого лаунчера своё имя игры. */
export const launchTargetLabel: Record<LaunchKind, MessageKey> = {
  steam: 'op.games.target.steam',
  epic: 'op.games.target.epic',
  riot: 'op.games.target.riot',
  battlenet: 'op.games.target.battlenet',
  exe: 'op.games.target.exe'
};

export interface GameForm {
  id: string | null;
  catalogGameId: string | null;
  name: string;
  genre: string;
  minAge: string;
  launchKind: LaunchKind;
  launchTarget: string;
  executablePath: string;
  arguments: string;
  availableWithoutSession: boolean;
  isEnabled: boolean;
  launchOnSessionStart: boolean;
  /** Обложка и возраст из каталога — только показываются: их обновляет платформа. */
  catalogCoverUrl: string | null;
}

export const emptyGameForm: GameForm = {
  id: null,
  catalogGameId: null,
  name: '',
  genre: '',
  minAge: '',
  launchKind: GameLaunchKindNames.Steam,
  launchTarget: '',
  executablePath: '',
  arguments: '',
  availableWithoutSession: false,
  isEnabled: true,
  launchOnSessionStart: false,
  catalogCoverUrl: null
};

export function formFromCatalog(game: CatalogGameDto): GameForm {
  return {
    ...emptyGameForm,
    catalogGameId: game.catalogGameId,
    name: game.name,
    genre: game.genre ?? '',
    minAge: game.minAge === null ? '' : String(game.minAge),
    launchKind: asKind(game.launchKind),
    launchTarget: game.launchKind === GameLaunchKindNames.Executable ? '' : (game.launchTarget ?? ''),
    executablePath: game.launchKind === GameLaunchKindNames.Executable ? (game.launchTarget ?? '') : '',
    catalogCoverUrl: game.coverUrl
  };
}

export function formFromGame(game: BranchGameDto): GameForm {
  return {
    id: game.branchGameId,
    catalogGameId: game.catalogGameId,
    name: game.name,
    genre: game.genre ?? '',
    minAge: game.minAge === null ? '' : String(game.minAge),
    launchKind: asKind(game.launchKind),
    launchTarget: game.launchTarget ?? '',
    executablePath: game.executablePath ?? '',
    arguments: game.arguments ?? '',
    availableWithoutSession: game.availableWithoutSession,
    isEnabled: game.isEnabled,
    launchOnSessionStart: game.launchOnSessionStart ?? false,
    catalogCoverUrl: game.catalogGameId ? game.coverUrl : null
  };
}

function asKind(kind: string): LaunchKind {
  return (launchKinds as readonly string[]).includes(kind) ? (kind as LaunchKind) : GameLaunchKindNames.Executable;
}

const launcherTarget = /^[A-Za-z0-9_.:-]{1,200}$/;
const steamAppId = /^[0-9]{1,10}$/;
const windowsExe = /^[A-Za-z]:\\[^"<>|?*\r\n]+\.exe$/i;

/** Та же проверка, что на сервере, — чтобы человек узнал об ошибке у поля, а не отказом после. */
export function validateGame(form: GameForm): MessageKey | null {
  if (!form.name.trim()) return 'op.games.error.name';
  if (form.minAge !== '' && !/^\d{1,2}$/.test(form.minAge)) return 'op.games.error.age';
  const path = form.executablePath.trim();
  if (path && !windowsExe.test(path)) return 'op.games.error.path';
  if (form.launchKind === GameLaunchKindNames.Executable) {
    return path ? null : 'op.games.error.pathRequired';
  }

  if (path) return null;
  const target = form.launchTarget.trim();
  if (form.launchKind === GameLaunchKindNames.Steam) return steamAppId.test(target) ? null : 'op.games.error.steam';
  return launcherTarget.test(target) ? null : 'op.games.error.target';
}

export function buildGameRequest(organizationId: string, form: GameForm): UpsertBranchGameRequest {
  const blank = (value: string) => (value.trim() === '' ? null : value.trim());
  return {
    organizationId,
    catalogGameId: form.catalogGameId,
    name: form.name.trim(),
    genre: blank(form.genre),
    minAge: form.minAge === '' ? null : Number(form.minAge),
    launchKind: form.launchKind,
    launchTarget: form.launchKind === GameLaunchKindNames.Executable ? null : blank(form.launchTarget),
    executablePath: blank(form.executablePath),
    arguments: blank(form.arguments),
    availableWithoutSession: form.availableWithoutSession,
    isEnabled: form.isEnabled,
    launchOnSessionStart: form.launchOnSessionStart
  };
}

/** «Steam · 730», «Свой exe · C:\Games\…». */
export function launchSummary(game: BranchGameDto, kindLabel: (kind: LaunchKind) => string): string {
  const kind = asKind(game.launchKind);
  const target = game.executablePath ?? game.launchTarget;
  return target ? `${kindLabel(kind)} · ${target}` : kindLabel(kind);
}

export function moved(ids: string[], id: string, delta: -1 | 1): string[] | null {
  const index = ids.indexOf(id);
  const next = index + delta;
  if (index < 0 || next < 0 || next >= ids.length) return null;
  const result = [...ids];
  [result[index], result[next]] = [result[next], result[index]];
  return result;
}
