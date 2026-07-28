import type { PlatformAdminSignInResponse } from '../api/types';

const STORAGE_KEY = 'afk4.platform.session';

export interface PlatformAdminSession {
  platformAdminId: string;
  userName: string;
  displayName: string;
  roles: string[];
  permissions: string[];
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
  refreshTokenExpiresAtUtc: string;
}

export interface SessionStorageLike {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
  removeItem(key: string): void;
}

function getStorage(): SessionStorageLike | null {
  if (typeof globalThis === 'undefined') {
    return null;
  }
  const candidate = (globalThis as { sessionStorage?: SessionStorageLike }).sessionStorage;
  return candidate ?? null;
}

export function readSession(storage: SessionStorageLike | null = getStorage()): PlatformAdminSession | null {
  if (storage === null) {
    return null;
  }
  const raw = storage.getItem(STORAGE_KEY);
  if (raw === null || raw === '') {
    return null;
  }
  try {
    const parsed = JSON.parse(raw) as PlatformAdminSession;
    if (typeof parsed.accessToken !== 'string' || parsed.accessToken.length === 0) {
      return null;
    }
    return parsed;
  } catch {
    return null;
  }
}

export function writeSession(
  session: PlatformAdminSession,
  storage: SessionStorageLike | null = getStorage()
): void {
  storage?.setItem(STORAGE_KEY, JSON.stringify(session));
}

export function clearSession(storage: SessionStorageLike | null = getStorage()): void {
  storage?.removeItem(STORAGE_KEY);
}

export function sessionFromSignInResponse(response: PlatformAdminSignInResponse): PlatformAdminSession {
  return {
    platformAdminId: response.platformAdminId,
    userName: response.userName,
    displayName: response.displayName,
    roles: response.roles,
    permissions: response.permissions,
    accessToken: response.accessToken,
    accessTokenExpiresAtUtc: response.accessTokenExpiresAtUtc,
    refreshToken: response.refreshToken,
    refreshTokenExpiresAtUtc: response.refreshTokenExpiresAtUtc
  };
}

export function isAccessTokenExpired(session: PlatformAdminSession, now: Date = new Date()): boolean {
  const expiresAt = Date.parse(session.accessTokenExpiresAtUtc);
  if (Number.isNaN(expiresAt)) {
    return true;
  }
  return expiresAt <= now.getTime();
}
