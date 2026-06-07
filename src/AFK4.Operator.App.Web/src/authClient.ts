import { postHostRequest } from './hostBridge';

export interface OperatorAuthSession {
  staffUserId: string;
  organizationId: string;
  displayName: string;
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshTokenExpiresAtUtc: string;
  branchIds: string[];
  activeBranchId?: string;
  permissions: string[];
}

export interface OperatorSignInRequest {
  organizationId: string;
  userName: string;
  password: string;
}

export function loadOperatorSession(): Promise<OperatorAuthSession | null> {
  return postHostRequest<OperatorAuthSession | null | undefined>('auth:loadToken')
    .then((session) => session ?? null);
}

export function signInOperator(request: OperatorSignInRequest): Promise<OperatorAuthSession> {
  return postHostRequest<OperatorAuthSession>('auth:signIn', request);
}

export function refreshOperatorSession(): Promise<OperatorAuthSession> {
  return postHostRequest<OperatorAuthSession>('auth:refresh');
}

export function signOutOperator(): Promise<{ signedOut: boolean }> {
  return postHostRequest<{ signedOut: boolean }>('auth:signOut');
}

export function forgotPasswordByEmail(userNameOrEmail: string): Promise<void> {
  return postHostRequest<void>('auth:forgotByEmail', { userNameOrEmail });
}

export function resetPasswordByEmail(token: string, newPassword: string): Promise<void> {
  return postHostRequest<void>('auth:resetByEmail', { token, newPassword });
}

export function forgotPasswordByPhone(phoneNumber: string): Promise<void> {
  return postHostRequest<void>('auth:forgotByPhone', { phoneNumber });
}

export function resetPasswordByPhone(phoneNumber: string, code: string, newPassword: string): Promise<void> {
  return postHostRequest<void>('auth:resetByPhone', { phoneNumber, code, newPassword });
}
