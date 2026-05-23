import {
  clearSession,
  isAccessTokenExpired,
  sessionFromSignInResponse,
  writeSession,
  type PlatformAdminSession
} from '../auth/tokenStore';
import type {
  CreateTenantRequest,
  CreateTenantResponse,
  OwnerInvite,
  OwnerInviteSummary,
  PlatformAdminSignInResponse,
  TenantDetail,
  TenantHealth,
  TenantSummary,
  TenantSupportNote
} from './types';

export class PlatformApiError extends Error {
  public readonly status: number;
  public readonly errorCode: string | null;

  public constructor(status: number, message: string, errorCode: string | null = null) {
    super(message);
    this.status = status;
    this.errorCode = errorCode;
  }
}

export interface PlatformApiClientOptions {
  baseUrl: string;
  fetchImpl?: typeof fetch;
  session: PlatformAdminSession | null;
  onSessionChanged: (session: PlatformAdminSession | null) => void;
}

export class PlatformApiClient {
  private session: PlatformAdminSession | null;
  private readonly baseUrl: string;
  private readonly fetchImpl: typeof fetch;
  private readonly onSessionChanged: (session: PlatformAdminSession | null) => void;
  private inflightRefresh: Promise<PlatformAdminSession | null> | null = null;

  public constructor(options: PlatformApiClientOptions) {
    this.baseUrl = options.baseUrl.replace(/\/+$/u, '');
    this.fetchImpl = options.fetchImpl ?? fetch.bind(globalThis);
    this.session = options.session;
    this.onSessionChanged = options.onSessionChanged;
  }

  public getSession(): PlatformAdminSession | null {
    return this.session;
  }

  public async signIn(userName: string, password: string): Promise<PlatformAdminSession> {
    const response = await this.fetchImpl(`${this.baseUrl}/api/platform/auth/sign-in`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ userName, password })
    });
    if (!response.ok) {
      throw await PlatformApiClient.toError(response, 'Sign-in failed.');
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
        headers: this.buildHeaders(true),
        body: JSON.stringify({ refreshToken: this.session.refreshToken })
      });
    } finally {
      this.applySession(null);
    }
  }

  public listTenants(): Promise<TenantSummary[]> {
    return this.send<TenantSummary[]>('GET', '/api/platform/tenants');
  }

  public getTenant(organizationId: string): Promise<TenantDetail> {
    return this.send<TenantDetail>('GET', `/api/platform/tenants/${organizationId}`);
  }

  public createTenant(request: CreateTenantRequest): Promise<CreateTenantResponse> {
    return this.send<CreateTenantResponse>('POST', '/api/platform/tenants', request);
  }

  public updateStatus(
    organizationId: string,
    status: string,
    reason: string
  ): Promise<TenantDetail> {
    return this.send<TenantDetail>(
      'PATCH',
      `/api/platform/tenants/${organizationId}/status`,
      { status, reason }
    );
  }

  public updatePlan(
    organizationId: string,
    planCode: string,
    subscriptionStatus: string
  ): Promise<TenantDetail> {
    return this.send<TenantDetail>(
      'PATCH',
      `/api/platform/tenants/${organizationId}/plan`,
      { planCode, subscriptionStatus }
    );
  }

  public updateLimits(organizationId: string, limits: CreateTenantRequest['limits']): Promise<TenantDetail> {
    return this.send<TenantDetail>(
      'PATCH',
      `/api/platform/tenants/${organizationId}/limits`,
      { limits }
    );
  }

  public createOwnerInvite(
    organizationId: string,
    branchId: string,
    ownerUserName: string | null,
    ownerDisplayName: string | null,
    lifetime: string | null
  ): Promise<OwnerInvite> {
    return this.send<OwnerInvite>(
      'POST',
      `/api/platform/tenants/${organizationId}/owner-invites`,
      { branchId, ownerUserName, ownerDisplayName, lifetime }
    );
  }

  public listOwnerInvites(organizationId: string): Promise<OwnerInviteSummary[]> {
    return this.send<OwnerInviteSummary[]>(
      'GET',
      `/api/platform/tenants/${organizationId}/owner-invites`
    );
  }

  public revokeOwnerInvite(ownerInviteId: string, reason: string): Promise<OwnerInvite> {
    return this.send<OwnerInvite>(
      'POST',
      `/api/platform/owner-invites/${ownerInviteId}/revoke`,
      { reason }
    );
  }

  public listSupportNotes(organizationId: string): Promise<TenantSupportNote[]> {
    return this.send<TenantSupportNote[]>(
      'GET',
      `/api/platform/tenants/${organizationId}/support-notes`
    );
  }

  public createSupportNote(organizationId: string, body: string): Promise<TenantSupportNote> {
    return this.send<TenantSupportNote>(
      'POST',
      `/api/platform/tenants/${organizationId}/support-notes`,
      { body }
    );
  }

  public updateSupportNote(
    organizationId: string,
    tenantSupportNoteId: string,
    body: string
  ): Promise<TenantSupportNote> {
    return this.send<TenantSupportNote>(
      'PATCH',
      `/api/platform/tenants/${organizationId}/support-notes/${tenantSupportNoteId}`,
      { body }
    );
  }

  public getHealth(organizationId: string): Promise<TenantHealth> {
    return this.send<TenantHealth>('GET', `/api/platform/tenants/${organizationId}/health`);
  }

  private async send<T>(method: string, path: string, body?: unknown): Promise<T> {
    let response = await this.dispatch(method, path, body, true);
    if (response.status === 401 && this.session !== null) {
      const refreshed = await this.refreshTokenOnce();
      if (refreshed !== null) {
        response = await this.dispatch(method, path, body, true);
      }
    }
    if (!response.ok) {
      throw await PlatformApiClient.toError(response);
    }
    if (response.status === 204) {
      return undefined as unknown as T;
    }
    const text = await response.text();
    return text.length === 0 ? (undefined as unknown as T) : (JSON.parse(text) as T);
  }

  private dispatch(method: string, path: string, body: unknown | undefined, includeAuth: boolean): Promise<Response> {
    const init: RequestInit = {
      method,
      headers: this.buildHeaders(includeAuth && body !== undefined)
    };
    if (body !== undefined) {
      init.body = JSON.stringify(body);
    }
    return this.fetchImpl(`${this.baseUrl}${path}`, init);
  }

  private buildHeaders(includeContentType: boolean): Record<string, string> {
    const headers: Record<string, string> = {};
    if (includeContentType) {
      headers['Content-Type'] = 'application/json';
    } else {
      headers['Content-Type'] = 'application/json';
    }
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
      // Body wasn't JSON or wasn't readable; fall back to status text.
    }
    return new PlatformApiError(response.status, message, code);
  }
}

export function isExpiredOrMissing(session: PlatformAdminSession | null, now: Date = new Date()): boolean {
  if (session === null) {
    return true;
  }
  return isAccessTokenExpired(session, now);
}
