import { getOperatorConfig } from './operatorConfig';
import type { StaffSignInResponse, StaffSignInStepName } from '@afk4/contracts';
import { StaffAuthApi, ChooseClubError, StaffAuthApiError, isUnauthorizedStaffAuthError, type ClubChoice } from './auth/staffAuthApi';
import {
  readStoredSession,
  writeStoredSession,
  clearStoredSession,
  sessionFromSignInResponse
} from './auth/staffSessionStore';

export { ChooseClubError, StaffAuthApiError, isUnauthorizedStaffAuthError };
export type { ClubChoice };

export interface OperatorAuthSession {
  staffUserId: string;
  organizationId: string;
  displayName: string;
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
  refreshTokenExpiresAtUtc: string;
  branchIds: string[];
  activeBranchId?: string;
  permissions: string[];
  roleNames?: string[];
  // True only for the adapted session support/supportSession.ts builds for a platform support
  // grant (see App.tsx). A few actions carry the same permission name as an area support IS
  // granted (e.g. password-reset shares ManageBranchStaff with profile/state edits) but are
  // deliberately NOT tagged with .AllowPlatformSupportAccess on the server — this flag lets those
  // screens gate on "is this actually a support session", not just the permission name, so the
  // button doesn't sit there enabled and then 403.
  isSupportSession?: boolean;
}

function api(): StaffAuthApi {
  return new StaffAuthApi(getOperatorConfig().platformBaseUrl);
}

export function loadOperatorSession(): Promise<OperatorAuthSession | null> {
  return Promise.resolve(readStoredSession());
}

function remember(response: StaffSignInResponse): OperatorAuthSession {
  const session = sessionFromSignInResponse(response);
  writeStoredSession(session);
  return session;
}

/**
 * Клуб, к которому подключена эта панель, или null в браузере. Подключённая спрашивает только свой
 * клуб; браузерная клуба не знает, и сервер находит его по номеру или логину. Раньше браузерная
 * панель без клуба бросала ошибку ещё до сети — и вход в ней не работал вовсе.
 */
export async function signInByPhoneOperator(
  organizationId: string | null, phoneNumber: string, password: string): Promise<OperatorAuthSession> {
  return remember(await api().signInByPhone(organizationId, phoneNumber, password));
}

export async function signInByLoginOperator(
  organizationId: string | null, login: string, password: string): Promise<OperatorAuthSession> {
  return remember(await api().signInByLogin(organizationId, login, password));
}

export function staffSignInNextStep(phoneNumber: string): Promise<StaffSignInStepName> {
  return api().nextStep(phoneNumber);
}

export function checkStaffInviteCode(phoneNumber: string, code: string): Promise<void> {
  return api().checkInvite(phoneNumber, code);
}

/** Первый вход: код от руководителя и новый ПИН — и человек сразу внутри. */
export async function acceptStaffInvite(phoneNumber: string, code: string, password: string): Promise<OperatorAuthSession> {
  return remember((await api().acceptInvite(phoneNumber, code, password)).signIn);
}

export async function signInToClubOperator(organizationId: string, login: string, password: string): Promise<OperatorAuthSession> {
  return remember(await api().signInToClub(organizationId, login, password));
}

export async function refreshOperatorSession(): Promise<OperatorAuthSession> {
  const current = readStoredSession();
  if (!current) {
    throw new Error('No session to refresh.');
  }

  const session = sessionFromSignInResponse(await api().refresh(current.organizationId, current.refreshToken));
  writeStoredSession(session);
  return session;
}

export async function signOutOperator(): Promise<{ signedOut: boolean }> {
  const current = readStoredSession();
  // Локальная сессия стирается в любом случае: человек нажал «Выйти», и экран обязан закрыться,
  // даже если сеть легла. Серверный отзыв — попытка: не дошла, токены доживут свой срок, но
  // машина уже чужая. Поэтому сначала зовём сервер, и только потом чистим.
  if (current) {
    try {
      await api().signOut(current.organizationId, current.refreshToken, current.accessToken);
    } catch {
      // Отзыв не дошёл — выход всё равно состоится локально.
    }
  }

  clearStoredSession();
  return { signedOut: true };
}

export function forgotPasswordByEmail(userNameOrEmail: string): Promise<void> {
  return api().forgotByEmail(userNameOrEmail);
}

export function resetPasswordByEmail(userNameOrEmail: string, code: string, newPassword: string): Promise<void> {
  return api().resetByEmail(userNameOrEmail, code, newPassword);
}

export function forgotPasswordByPhone(phoneNumber: string): Promise<void> {
  return api().forgotByPhone(phoneNumber);
}

export function resetPasswordByPhone(phoneNumber: string, code: string, newPassword: string): Promise<void> {
  return api().resetByPhone(phoneNumber, code, newPassword);
}
