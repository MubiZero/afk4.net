import type { PlatformPersonSessionResponse } from '../api/types';

const PLAYER_STORAGE_KEY = 'afk4.player.session';

// Сессия принадлежит человеку, а не счёту в клубе: клуба может не быть вовсе — так выглядит тот,
// кто зарегистрировался дома и ещё никуда не заходил.
export interface PlayerSession {
  platformPersonId: string;
  playerAccountId: string | null;
  organizationId: string | null;
  displayName: string;
  phoneVerified: boolean;
  profileCompleted: boolean;
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
  refreshTokenExpiresAtUtc: string;
}

// Players are on personal devices: persist across launches (localStorage), unlike staff.
function getStorage(): Storage | null {
  if (typeof globalThis === 'undefined') return null;
  return (globalThis as { localStorage?: Storage }).localStorage ?? null;
}

export function readPlayerSession(storage: Storage | null = getStorage()): PlayerSession | null {
  if (storage === null) return null;
  const raw = storage.getItem(PLAYER_STORAGE_KEY);
  if (raw === null || raw === '') return null;
  try {
    const parsed = JSON.parse(raw) as PlayerSession;
    if (typeof parsed.accessToken !== 'string' || parsed.accessToken.length === 0) return null;
    // Клуб больше не обязателен: единственное, чем сессия обязана быть, — это живой токен.
    return { ...parsed, profileCompleted: parsed.profileCompleted !== false };
  } catch {
    return null;
  }
}

export function writePlayerSession(session: PlayerSession, storage: Storage | null = getStorage()): void {
  storage?.setItem(PLAYER_STORAGE_KEY, JSON.stringify(session));
}

export function clearPlayerSession(storage: Storage | null = getStorage()): void {
  storage?.removeItem(PLAYER_STORAGE_KEY);
}

export function playerSessionFromResponse(response: PlatformPersonSessionResponse): PlayerSession {
  return {
    platformPersonId: response.platformPersonId,
    playerAccountId: response.playerAccountId,
    organizationId: response.organizationId,
    displayName: response.displayName,
    phoneVerified: response.phoneVerified,
    profileCompleted: response.profileCompleted !== false,
    accessToken: response.accessToken,
    accessTokenExpiresAtUtc: response.accessTokenExpiresAtUtc,
    refreshToken: response.refreshToken,
    refreshTokenExpiresAtUtc: response.refreshTokenExpiresAtUtc
  };
}

export function isPlayerAccessTokenExpired(session: PlayerSession, now: Date = new Date()): boolean {
  const expiresAt = Date.parse(session.accessTokenExpiresAtUtc);
  if (Number.isNaN(expiresAt)) return true;
  return expiresAt <= now.getTime();
}
