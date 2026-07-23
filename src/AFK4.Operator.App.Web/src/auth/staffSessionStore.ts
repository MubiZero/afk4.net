import type { OperatorAuthSession } from '../authClient';
import type { StaffSignInResponse } from './staffAuthApi';

const KEY = 'afk4.staff.session';

export function readStoredSession(): OperatorAuthSession | null {
  const raw = sessionStorage.getItem(KEY);
  if (!raw) return null;
  try {
    const s = JSON.parse(raw) as OperatorAuthSession;
    if (!s.accessToken || !s.organizationId) return null;
    return s;
  } catch {
    return null;
  }
}

export function writeStoredSession(session: OperatorAuthSession): void {
  sessionStorage.setItem(KEY, JSON.stringify(session));
}

export function clearStoredSession(): void {
  sessionStorage.removeItem(KEY);
}

export function sessionFromSignInResponse(r: StaffSignInResponse): OperatorAuthSession {
  return {
    staffUserId: r.staffUserId,
    organizationId: r.organizationId,
    displayName: r.displayName,
    accessToken: r.accessToken,
    accessTokenExpiresAtUtc: r.accessTokenExpiresAtUtc,
    refreshToken: r.refreshToken,
    refreshTokenExpiresAtUtc: r.refreshTokenExpiresAtUtc,
    branchIds: r.branchIds,
    permissions: r.permissions,
    roleNames: r.roleNames ?? []
  };
}

export function isAccessTokenExpired(session: OperatorAuthSession, nowMs: number): boolean {
  return Date.parse(session.accessTokenExpiresAtUtc) <= nowMs;
}
