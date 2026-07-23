import { getOperatorConfig } from './operatorConfig';
import { StaffAuthApi, ChooseClubError, type ClubChoice } from './auth/staffAuthApi';
import {
  readStoredSession,
  writeStoredSession,
  clearStoredSession,
  sessionFromSignInResponse
} from './auth/staffSessionStore';

export { ChooseClubError };
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
}

function api(): StaffAuthApi {
  return new StaffAuthApi(getOperatorConfig().platformBaseUrl);
}

export function loadOperatorSession(): Promise<OperatorAuthSession | null> {
  return Promise.resolve(readStoredSession());
}

export async function signInByLoginOperator(login: string, password: string): Promise<OperatorAuthSession> {
  const session = sessionFromSignInResponse(await api().signInByLogin(login, password));
  writeStoredSession(session);
  return session;
}

export async function signInToClubOperator(organizationId: string, login: string, password: string): Promise<OperatorAuthSession> {
  const session = sessionFromSignInResponse(await api().signInToClub(organizationId, login, password));
  writeStoredSession(session);
  return session;
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

export function signOutOperator(): Promise<{ signedOut: boolean }> {
  // Серверного sign-out (токен revoke) пока нет — очищаем только локально сохранённую сессию.
  clearStoredSession();
  return Promise.resolve({ signedOut: true });
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
