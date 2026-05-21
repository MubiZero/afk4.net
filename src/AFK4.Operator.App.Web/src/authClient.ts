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
  return postHostRequest<OperatorAuthSession | null>('auth:loadToken');
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
