import { clearStaffSession, staffSessionFromSignInResponse, writeStaffSession, type StaffSession } from '../auth/staffTokenStore';
import type { AcceptOwnerInviteRequest, FetchLike, StaffSignInClubChoice, StaffSignInResponse } from './types';
import { PlatformApiError } from './platformApi';

export class StaffSignInChooseClubError extends Error {
  public readonly clubs: StaffSignInClubChoice[];

  public constructor(clubs: StaffSignInClubChoice[]) {
    super('Multiple clubs match this login.');
    this.name = 'StaffSignInChooseClubError';
    this.clubs = clubs;
  }
}

export interface StaffAuthApiClientOptions {
  baseUrl: string;
  fetchImpl?: FetchLike;
  session: StaffSession | null;
  onSessionChanged: (session: StaffSession | null) => void;
}

export class StaffAuthApiClient {
  private session: StaffSession | null;
  private readonly baseUrl: string;
  private readonly fetchImpl: FetchLike;
  private readonly onSessionChanged: (session: StaffSession | null) => void;

  public constructor(options: StaffAuthApiClientOptions) {
    this.baseUrl = options.baseUrl.replace(/\/+$/u, '');
    this.fetchImpl = options.fetchImpl ?? fetch.bind(globalThis);
    this.session = options.session;
    this.onSessionChanged = options.onSessionChanged;
  }

  public getSession(): StaffSession | null {
    return this.session;
  }

  public async acceptInvite(request: AcceptOwnerInviteRequest): Promise<StaffSession> {
    const response = await this.fetchImpl(`${this.baseUrl}/api/platform/owner-invites/accept`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request)
    });
    return this.readAndApplySession(response, 'Setup code acceptance failed.');
  }

  public async signInByLogin(login: string, password: string): Promise<StaffSession> {
    const response = await this.fetchImpl(`${this.baseUrl}/api/auth/staff/sign-in-by-login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ login, password })
    });
    if (response.status === 409) {
      const body = (await response.json()) as { clubs: StaffSignInClubChoice[] };
      throw new StaffSignInChooseClubError(body.clubs);
    }
    return this.readAndApplySession(response, 'Sign-in failed.');
  }

  public async signInToClub(organizationId: string, login: string, password: string): Promise<StaffSession> {
    const response = await this.fetchImpl(`${this.baseUrl}/api/auth/staff/sign-in`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ organizationId, userName: login, password })
    });
    return this.readAndApplySession(response, 'Sign-in failed.');
  }

  public async forgotPasswordByEmail(userNameOrEmail: string): Promise<void> {
    const response = await this.fetchImpl(`${this.baseUrl}/api/auth/staff/forgot-password`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ userNameOrEmail })
    });
    if (!response.ok) {
      throw await toApiError(response, 'Reset request failed.');
    }
  }

  public async resetPasswordByToken(token: string, newPassword: string): Promise<void> {
    const response = await this.fetchImpl(`${this.baseUrl}/api/auth/staff/reset-password`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ token, newPassword })
    });
    if (!response.ok) {
      throw await toApiError(response, 'Reset failed.');
    }
  }

  public async forgotPasswordByPhone(phoneNumber: string): Promise<void> {
    const response = await this.fetchImpl(`${this.baseUrl}/api/auth/staff/forgot-password-by-phone`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ phoneNumber })
    });
    if (!response.ok) {
      throw await toApiError(response, 'Reset request failed.');
    }
  }

  public async resetPasswordByPhone(phoneNumber: string, code: string, newPassword: string): Promise<void> {
    const response = await this.fetchImpl(`${this.baseUrl}/api/auth/staff/reset-password-by-phone`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ phoneNumber, code, newPassword })
    });
    if (!response.ok) {
      throw await toApiError(response, 'Reset failed.');
    }
  }

  public signOutLocal(): void {
    this.applySession(null);
  }

  private async readAndApplySession(response: Response, fallbackMessage: string): Promise<StaffSession> {
    if (!response.ok) {
      throw await toApiError(response, fallbackMessage);
    }
    const body = (await response.json()) as StaffSignInResponse;
    const session = staffSessionFromSignInResponse(body);
    this.applySession(session);
    return session;
  }

  private applySession(session: StaffSession | null): void {
    this.session = session;
    if (session === null) {
      clearStaffSession();
    } else {
      writeStaffSession(session);
    }
    this.onSessionChanged(session);
  }
}

async function toApiError(response: Response, fallbackMessage: string): Promise<PlatformApiError> {
  let message = fallbackMessage;
  let code: string | null = null;
  let remainingAttempts: number | null = null;
  try {
    const text = await response.text();
    if (text.length > 0) {
      const parsed = JSON.parse(text) as { error?: string; status?: string; remainingAttempts?: number };
      if (typeof parsed.error === 'string' && parsed.error.length > 0) {
        message = parsed.error;
        code = parsed.error;
      }
      if (typeof parsed.status === 'string' && parsed.status.length > 0) {
        message = `${message} (${parsed.status})`;
      }
      if (typeof parsed.remainingAttempts === 'number') {
        remainingAttempts = parsed.remainingAttempts;
      }
    }
  } catch {
    // Preserve the fallback when the API returns a non-JSON error body.
  }
  return new PlatformApiError(response.status, message, code, remainingAttempts);
}
