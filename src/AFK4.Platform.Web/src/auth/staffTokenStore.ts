import type { StaffSignInResponse } from '../api/types';
import type { SessionStorageLike } from './tokenStore';

const STAFF_STORAGE_KEY = 'afk4.staff.session';

export interface StaffSession {
  staffUserId: string;
  organizationId: string;
  displayName: string;
  branchIds: string[];
  permissions: string[];
  roleNames: string[];
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
  refreshTokenExpiresAtUtc: string;
}

function getStorage(): SessionStorageLike | null {
  if (typeof globalThis === 'undefined') {
    return null;
  }
  const candidate = (globalThis as { sessionStorage?: SessionStorageLike }).sessionStorage;
  return candidate ?? null;
}

export function readStaffSession(storage: SessionStorageLike | null = getStorage()): StaffSession | null {
  if (storage === null) {
    return null;
  }
  const raw = storage.getItem(STAFF_STORAGE_KEY);
  if (raw === null || raw === '') {
    return null;
  }
  try {
    const parsed = JSON.parse(raw) as StaffSession;
    if (typeof parsed.accessToken !== 'string' || parsed.accessToken.length === 0) {
      return null;
    }
    if (typeof parsed.organizationId !== 'string' || parsed.organizationId.length === 0) {
      return null;
    }
    return parsed;
  } catch {
    return null;
  }
}

export function writeStaffSession(
  session: StaffSession,
  storage: SessionStorageLike | null = getStorage()
): void {
  storage?.setItem(STAFF_STORAGE_KEY, JSON.stringify(session));
}

export function clearStaffSession(storage: SessionStorageLike | null = getStorage()): void {
  storage?.removeItem(STAFF_STORAGE_KEY);
}

export function staffSessionFromSignInResponse(response: StaffSignInResponse): StaffSession {
  return {
    staffUserId: response.staffUserId,
    organizationId: response.organizationId,
    displayName: response.displayName,
    branchIds: response.branchIds,
    permissions: response.permissions,
    roleNames: response.roleNames ?? [],
    accessToken: response.accessToken,
    accessTokenExpiresAtUtc: response.accessTokenExpiresAtUtc,
    refreshToken: response.refreshToken,
    refreshTokenExpiresAtUtc: response.refreshTokenExpiresAtUtc
  };
}

export function isStaffAccessTokenExpired(session: StaffSession, now: Date = new Date()): boolean {
  const expiresAt = Date.parse(session.accessTokenExpiresAtUtc);
  if (Number.isNaN(expiresAt)) {
    return true;
  }
  return expiresAt <= now.getTime();
}
