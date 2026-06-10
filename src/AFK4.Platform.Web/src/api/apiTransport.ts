import {
  clearStaffSession,
  staffSessionFromSignInResponse,
  writeStaffSession,
  type StaffSession
} from '../auth/staffTokenStore';
import { PlatformApiError } from './platformApi';
import type { FetchLike, StaffSignInResponse } from './types';

export interface ApiTransportOptions {
  baseUrl: string;
  fetchImpl?: FetchLike;
  session: StaffSession | null;
  onSessionChanged: (session: StaffSession | null) => void;
}

/**
 * Shared HTTP plumbing for the staff-facing API: auth header injection,
 * single-flight token refresh on 401, and JSON/error decoding. Domain clients
 * compose an instance of this instead of re-implementing the transport.
 */
export class ApiTransport {
  private session: StaffSession | null;
  private readonly baseUrl: string;
  private readonly fetchImpl: FetchLike;
  private readonly onSessionChanged: (session: StaffSession | null) => void;
  private inflightRefresh: Promise<StaffSession | null> | null = null;

  public constructor(options: ApiTransportOptions) {
    this.baseUrl = options.baseUrl.replace(/\/+$/u, '');
    this.fetchImpl = options.fetchImpl ?? fetch.bind(globalThis);
    this.session = options.session;
    this.onSessionChanged = options.onSessionChanged;
  }

  public getSession(): StaffSession | null {
    return this.session;
  }

  public async send<T>(
    method: string,
    path: string,
    body?: unknown,
    extraHeaders?: Record<string, string>
  ): Promise<T> {
    const response = await this.sendRaw(method, path, body, extraHeaders);
    return this.readJson<T>(response);
  }

  public async sendRaw(
    method: string,
    path: string,
    body?: unknown,
    extraHeaders?: Record<string, string>
  ): Promise<Response> {
    let response = await this.dispatch(method, path, body, extraHeaders);
    if (response.status === 401 && this.session !== null) {
      const refreshed = await this.refreshTokenOnce();
      if (refreshed !== null) {
        response = await this.dispatch(method, path, body, extraHeaders);
      }
    }
    if (!response.ok) {
      throw await toApiError(response);
    }
    return response;
  }

  public async readJson<T>(response: Response): Promise<T> {
    if (response.status === 204) {
      return undefined as unknown as T;
    }
    const text = await response.text();
    return text.length === 0 ? (undefined as unknown as T) : (JSON.parse(text) as T);
  }

  private dispatch(
    method: string,
    path: string,
    body?: unknown,
    extraHeaders?: Record<string, string>
  ): Promise<Response> {
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
      ...extraHeaders
    };
    if (this.session !== null && this.session.accessToken.length > 0) {
      headers.Authorization = `Bearer ${this.session.accessToken}`;
    }
    const init: RequestInit = { method, headers };
    if (body !== undefined) {
      init.body = JSON.stringify(body);
    }
    return this.fetchImpl(`${this.baseUrl}${path}`, init);
  }

  private async refreshTokenOnce(): Promise<StaffSession | null> {
    if (this.inflightRefresh !== null) {
      return this.inflightRefresh;
    }
    this.inflightRefresh = (async (): Promise<StaffSession | null> => {
      if (this.session === null) {
        return null;
      }
      try {
        const response = await this.fetchImpl(`${this.baseUrl}/api/auth/staff/refresh`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ refreshToken: this.session.refreshToken })
        });
        if (!response.ok) {
          this.applySession(null);
          return null;
        }
        const body = (await response.json()) as StaffSignInResponse;
        const refreshed = staffSessionFromSignInResponse(body);
        this.applySession(refreshed);
        return refreshed;
      } finally {
        this.inflightRefresh = null;
      }
    })();
    return this.inflightRefresh;
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

async function toApiError(response: Response): Promise<PlatformApiError> {
  let message = 'Club API call failed.';
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
    // Keep the fallback when the API returns non-JSON content.
  }
  return new PlatformApiError(response.status, message, code, remainingAttempts);
}
