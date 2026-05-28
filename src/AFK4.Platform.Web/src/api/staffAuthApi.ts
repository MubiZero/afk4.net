import { clearStaffSession, staffSessionFromSignInResponse, writeStaffSession, type StaffSession } from '../auth/staffTokenStore';
import type { AcceptOwnerInviteRequest, StaffSignInResponse } from './types';
import { PlatformApiError } from './platformApi';

export interface StaffAuthApiClientOptions {
  baseUrl: string;
  fetchImpl?: typeof fetch;
  session: StaffSession | null;
  onSessionChanged: (session: StaffSession | null) => void;
}

export class StaffAuthApiClient {
  private session: StaffSession | null;
  private readonly baseUrl: string;
  private readonly fetchImpl: typeof fetch;
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

  public async signIn(tenantKey: string, userName: string, password: string): Promise<StaffSession> {
    const response = await this.fetchImpl(`${this.baseUrl}/api/auth/staff/sign-in-by-tenant-key`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ tenantKey, userName, password })
    });
    return this.readAndApplySession(response, 'Sign-in failed.');
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
  try {
    const text = await response.text();
    if (text.length > 0) {
      const parsed = JSON.parse(text) as { error?: string; status?: string };
      if (typeof parsed.error === 'string' && parsed.error.length > 0) {
        message = parsed.error;
        code = parsed.error;
      }
      if (typeof parsed.status === 'string' && parsed.status.length > 0) {
        message = `${message} (${parsed.status})`;
      }
    }
  } catch {
    // Preserve the fallback when the API returns a non-JSON error body.
  }
  return new PlatformApiError(response.status, message, code);
}
