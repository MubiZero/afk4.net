import {
  clearSession,
  sessionFromSignInResponse,
  writeSession,
  type PlatformAdminSession
} from '../auth/tokenStore';
import type {
  FetchLike,
  PlatformAdminSignInChallengeResponse,
  PlatformAdminSignInResponse,
  TwoFactorSetupConfirmResponse,
  TwoFactorSetupResponse
} from './types';

// Result of step 1 of sign-in: password alone never yields a working session anymore, only a
// short-lived challenge token that must be redeemed via completeTwoFactorSetup/completeTwoFactor.
export type SignInOutcome = {
  kind: 'challenge';
  challengeToken: string;
  twoFactorConfigured: boolean;
};

export class PlatformApiError extends Error {
  public readonly status: number;
  public readonly errorCode: string | null;
  public readonly remainingAttempts: number | null;

  public constructor(
    status: number,
    message: string,
    errorCode: string | null = null,
    remainingAttempts: number | null = null
  ) {
    super(message);
    this.status = status;
    this.errorCode = errorCode;
    this.remainingAttempts = remainingAttempts;
  }
}

export interface PlatformTransportOptions {
  baseUrl: string;
  fetchImpl?: FetchLike;
  session: PlatformAdminSession | null;
  onSessionChanged: (session: PlatformAdminSession | null) => void;
}

/**
 * Shared HTTP plumbing for the platform-admin API: session lifecycle
 * (sign-in/out, single-flight refresh on 401), header building, and JSON/error
 * decoding. Domain clients compose an instance instead of duplicating it.
 */
export class PlatformTransport {
  private session: PlatformAdminSession | null;
  private readonly baseUrl: string;
  private readonly fetchImpl: FetchLike;
  private readonly onSessionChanged: (session: PlatformAdminSession | null) => void;
  private inflightRefresh: Promise<PlatformAdminSession | null> | null = null;

  public constructor(options: PlatformTransportOptions) {
    this.baseUrl = options.baseUrl.replace(/\/+$/u, '');
    this.fetchImpl = options.fetchImpl ?? fetch.bind(globalThis);
    this.session = options.session;
    this.onSessionChanged = options.onSessionChanged;
  }

  public getSession(): PlatformAdminSession | null {
    return this.session;
  }

  // Step 1: password only ever earns a challenge token now — no session is applied here. The
  // caller decides where to go next from `twoFactorConfigured`: an already-configured admin goes
  // to completeTwoFactor (verify), a first-timer goes to beginTwoFactorSetup.
  public async signIn(userName: string, password: string): Promise<SignInOutcome> {
    const response = await this.fetchImpl(`${this.baseUrl}/api/platform/auth/sign-in`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ userName, password })
    });
    if (!response.ok) {
      throw await PlatformTransport.toError(response, 'Sign-in failed.');
    }
    const body = (await response.json()) as PlatformAdminSignInChallengeResponse;
    return { kind: 'challenge', challengeToken: body.challengeToken, twoFactorConfigured: body.twoFactorConfigured };
  }

  // Step 2a (first-time setup): returns the TOTP secret/QR link for a challenge that hasn't
  // configured 2FA yet. No session change — the admin hasn't proven a code yet.
  public async beginTwoFactorSetup(challengeToken: string): Promise<TwoFactorSetupResponse> {
    const response = await this.fetchImpl(`${this.baseUrl}/api/platform/auth/2fa/setup`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ challengeToken })
    });
    if (!response.ok) {
      throw await PlatformTransport.toError(response, 'Two-factor setup failed.');
    }
    return (await response.json()) as TwoFactorSetupResponse;
  }

  // Step 2a confirm: proves the first TOTP code and issues the real session plus one-time
  // recovery codes. This is the only place a first-time setup applies a session.
  public async completeTwoFactorSetup(challengeToken: string, code: string): Promise<TwoFactorSetupConfirmResponse> {
    const response = await this.fetchImpl(`${this.baseUrl}/api/platform/auth/2fa/setup/confirm`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ challengeToken, code })
    });
    if (!response.ok) {
      throw await PlatformTransport.toError(response, 'Two-factor setup confirmation failed.');
    }
    const body = (await response.json()) as TwoFactorSetupConfirmResponse;
    this.applySession(sessionFromSignInResponse(body.session));
    return body;
  }

  // Step 2b: for an already-configured admin, verifies either a TOTP code or a recovery code and
  // issues the working session.
  public async completeTwoFactor(challengeToken: string, code: string): Promise<PlatformAdminSession> {
    const response = await this.fetchImpl(`${this.baseUrl}/api/platform/auth/2fa/verify`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ challengeToken, code })
    });
    if (!response.ok) {
      throw await PlatformTransport.toError(response, 'Two-factor verification failed.');
    }
    const body = (await response.json()) as PlatformAdminSignInResponse;
    const session = sessionFromSignInResponse(body);
    this.applySession(session);
    return session;
  }

  public async signOut(): Promise<void> {
    if (this.session === null) {
      return;
    }
    try {
      await this.fetchImpl(`${this.baseUrl}/api/platform/auth/sign-out`, {
        method: 'POST',
        headers: this.buildHeaders(),
        body: JSON.stringify({ refreshToken: this.session.refreshToken })
      });
    } finally {
      this.applySession(null);
    }
  }

  public async send<T>(method: string, path: string, body?: unknown, signal?: AbortSignal): Promise<T> {
    let response = await this.dispatch(method, path, body, undefined, signal);
    if (response.status === 401 && this.session !== null) {
      const refreshed = await this.refreshTokenOnce();
      if (refreshed !== null) {
        response = await this.dispatch(method, path, body, undefined, signal);
      }
    }
    if (!response.ok) {
      throw await PlatformTransport.toError(response);
    }
    return PlatformTransport.readBody<T>(response);
  }

  public async sendIdempotent<T>(method: string, path: string, body: unknown | undefined): Promise<T> {
    const idempotencyKey = crypto.randomUUID();
    let response = await this.dispatch(method, path, body, { 'Idempotency-Key': idempotencyKey });
    if (response.status === 401 && this.session !== null) {
      const refreshed = await this.refreshTokenOnce();
      if (refreshed !== null) {
        response = await this.dispatch(method, path, body, { 'Idempotency-Key': idempotencyKey });
      }
    }
    if (!response.ok) {
      throw await PlatformTransport.toError(response);
    }
    return PlatformTransport.readBody<T>(response);
  }

  private static async readBody<T>(response: Response): Promise<T> {
    if (response.status === 204) {
      return undefined as unknown as T;
    }
    const text = await response.text();
    return text.length === 0 ? (undefined as unknown as T) : (JSON.parse(text) as T);
  }

  private dispatch(
    method: string,
    path: string,
    body: unknown | undefined,
    extraHeaders?: Record<string, string>,
    signal?: AbortSignal
  ): Promise<Response> {
    const headers = this.buildHeaders();
    if (extraHeaders !== undefined) {
      for (const [k, v] of Object.entries(extraHeaders)) {
        headers[k] = v;
      }
    }
    const init: RequestInit = { method, headers, signal };
    if (body !== undefined) {
      init.body = JSON.stringify(body);
    }
    return this.fetchImpl(`${this.baseUrl}${path}`, init);
  }

  private buildHeaders(): Record<string, string> {
    const headers: Record<string, string> = { 'Content-Type': 'application/json' };
    if (this.session !== null && this.session.accessToken.length > 0) {
      headers['Authorization'] = `Bearer ${this.session.accessToken}`;
    }
    return headers;
  }

  private async refreshTokenOnce(): Promise<PlatformAdminSession | null> {
    if (this.inflightRefresh !== null) {
      return this.inflightRefresh;
    }
    this.inflightRefresh = (async (): Promise<PlatformAdminSession | null> => {
      if (this.session === null) {
        return null;
      }
      try {
        const response = await this.fetchImpl(`${this.baseUrl}/api/platform/auth/refresh`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ refreshToken: this.session.refreshToken })
        });
        if (!response.ok) {
          this.applySession(null);
          return null;
        }
        const body = (await response.json()) as PlatformAdminSignInResponse;
        const refreshed = sessionFromSignInResponse(body);
        this.applySession(refreshed);
        return refreshed;
      } finally {
        this.inflightRefresh = null;
      }
    })();
    return this.inflightRefresh;
  }

  private applySession(session: PlatformAdminSession | null): void {
    this.session = session;
    if (session === null) {
      clearSession();
    } else {
      writeSession(session);
    }
    this.onSessionChanged(session);
  }

  private static async toError(response: Response, fallbackMessage = 'Platform API call failed.'): Promise<PlatformApiError> {
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
      // Body wasn't JSON or wasn't readable; fall back to status text.
    }
    return new PlatformApiError(response.status, message, code, remainingAttempts);
  }
}
