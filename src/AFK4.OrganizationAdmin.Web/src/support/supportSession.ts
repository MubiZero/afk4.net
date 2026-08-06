import type { OperatorAuthSession } from '../authClient';
import { permissionNames } from '../permissionNames';

const KEY = 'afk4.support.session';

// Shared with platformApi.ts (which sets this header on every request while a support session is
// active) so the header name lives in exactly one place instead of two matching string literals.
export const SUPPORT_GRANT_HEADER_NAME = 'X-AFK4-Support-Access-Grant';

export interface SupportSessionBranch {
  branchId: string;
  name: string;
}

export interface SupportSession {
  sessionToken: string;
  organizationId: string;
  organizationName: string;
  reason: string;
  expiresAtUtc: string;
  writableAreas: string[];
  branches: SupportSessionBranch[];
}

// The organization-admin shell (App.tsx) is built entirely around OperatorAuthSession — floor map,
// realtime, shifts, permissions all key off it. A support session isn't a staff login, but the
// shell shouldn't grow a second parallel rendering path just to accommodate it: this is the one
// place that adapts a support grant into the same shape the shell already knows how to render.
//
// Every permission is granted (support sees what club staff sees — scoping to `writableAreas` is a
// follow-up task; the server already enforces the real boundary per endpoint regardless of what this
// client-side list says) with ONE deliberate exception: `openShift`. usePostAuthShiftGate uses it to
// decide whether to call GET .../shifts/current before letting the shell render at all, and that
// endpoint isn't tagged AllowPlatformSupportAccess server-side (see ShiftEndpoints.cs) — granting the
// permission would make the gate call it, get a 403, and permanently strand the tab on a "couldn't
// open shift" retry screen instead of the shell. Omitting it keeps the gate at 'not-required'.
const SUPPORT_PERMISSIONS = Object.values(permissionNames).filter(
  (permission) => permission !== permissionNames.openShift
);

export function supportOperatorSession(session: SupportSession): OperatorAuthSession {
  return {
    // Mirrors the platform admin's own StaffContext under a support grant (PlatformSupportSessionMiddleware
    // sets StaffUserId = Guid.Empty, DisplayName = "Поддержка платформы") — same identity, same string.
    staffUserId: '00000000-0000-0000-0000-000000000000',
    organizationId: session.organizationId,
    displayName: 'Поддержка платформы',
    // Never read: platformApi.ts prefers the support grant header unconditionally over Authorization
    // whenever a support session exists, so these never reach a real request.
    accessToken: '',
    accessTokenExpiresAtUtc: session.expiresAtUtc,
    refreshToken: '',
    refreshTokenExpiresAtUtc: session.expiresAtUtc,
    branchIds: session.branches.map((branch) => branch.branchId),
    permissions: SUPPORT_PERMISSIONS,
    roleNames: ['Поддержка платформы']
  };
}

export function readSupportSession(): SupportSession | null {
  const raw = sessionStorage.getItem(KEY);
  if (!raw) return null;
  try {
    const s = JSON.parse(raw) as SupportSession;
    if (!s.sessionToken || !s.organizationId) return null;
    return s;
  } catch {
    return null;
  }
}

export function writeSupportSession(session: SupportSession): void {
  sessionStorage.setItem(KEY, JSON.stringify(session));
}

export function clearSupportSession(): void {
  sessionStorage.removeItem(KEY);
}

// Garbage/unparseable expiry is treated as already-expired (fail-safe) — same convention as
// isAccessTokenExpired for the staff session.
export function isSupportSessionExpired(session: SupportSession, nowMs: number = Date.now()): boolean {
  const expiresAt = Date.parse(session.expiresAtUtc);
  return Number.isNaN(expiresAt) || expiresAt <= nowMs;
}

// Публичный обмен: у клиента ещё нет ни staff-токена, ни сессии поддержки, поэтому это простой
// fetch без PlatformApiClient (который требует токен на каждый вызов). Билет — секрет одноразового
// действия: не логируем его и не кладём в сообщение об ошибке.
export async function redeemSupportTicket(baseUrl: string, ticket: string): Promise<SupportSession> {
  const url = new URL('/api/public/support-access/sessions', baseUrl).toString();
  const response = await fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ ticket })
  });

  if (!response.ok) {
    throw new Error('Support ticket is invalid, already used, or expired.');
  }

  return await response.json() as SupportSession;
}

// Best-effort revoke of the grant itself (DELETE /api/support-access/session, see
// SupportAccessSessionEndpoints.cs) so the server closes the door immediately instead of waiting
// out the grant's TTL. Callers must still clear the local session in a `finally` regardless of
// whether this succeeds — a network hiccup here must never strand someone inside support mode.
export async function endSupportSession(baseUrl: string, sessionToken: string): Promise<void> {
  const url = new URL('/api/support-access/session', baseUrl).toString();
  const response = await fetch(url, {
    method: 'DELETE',
    headers: { [SUPPORT_GRANT_HEADER_NAME]: sessionToken }
  });

  if (!response.ok) {
    throw new Error('Failed to revoke support session.');
  }
}
