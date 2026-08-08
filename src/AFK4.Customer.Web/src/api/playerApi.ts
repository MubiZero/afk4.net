import type { PlayerSession } from '../auth/playerTokenStore';
import { playerSessionFromSignInResponse } from '../auth/playerTokenStore';
import type {
  PlayerSignInRequest, PlayerSignInResponse, PlayerDashboardDto,
  CursorPage, PlayerVisitDto, PlayerVisitReceiptDto, PlayerPurchaseDto,
  PlayerProfileDto, UpdatePlayerProfileRequest,
  PlayerTopUpIntentRequest, PlayerTopUpIntentDto,
  CreatePlayerReservationRequest, PlayerReservationDto
} from './types';

interface PlayerFeaturesResponse { features: string[]; }

export class PlayerApiError extends Error {
  constructor(public readonly status: number, message: string) {
    super(message);
    this.name = 'PlayerApiError';
  }
}

interface PlayerApiOptions {
  baseUrl: string;
  fetchImpl?: typeof fetch;
  session: PlayerSession | null;
  onSessionChanged: (session: PlayerSession | null) => void;
}

export class PlayerApiClient {
  private readonly baseUrl: string;
  private readonly fetchImpl: typeof fetch;
  private session: PlayerSession | null;
  private readonly onSessionChanged: (session: PlayerSession | null) => void;

  constructor(options: PlayerApiOptions) {
    this.baseUrl = options.baseUrl.replace(/\/$/, '');
    this.fetchImpl = options.fetchImpl ?? fetch;
    this.session = options.session;
    this.onSessionChanged = options.onSessionChanged;
  }

  signIn(request: PlayerSignInRequest): Promise<PlayerSignInResponse> {
    return this.publicPost<PlayerSignInResponse>('/api/public/player/sign-in', request);
  }

  getDashboard(): Promise<PlayerDashboardDto> {
    return this.authedGet<PlayerDashboardDto>('/api/me/dashboard');
  }

  async getFeatures(): Promise<string[]> {
    const response = await this.authedGet<PlayerFeaturesResponse>('/api/me/features');
    // A malformed body (missing/null/non-array `features` — version skew, a caching proxy, a
    // backend bug) must be treated the same as a failed request: throw so callers route it into
    // their "list unavailable, assume enabled" fallback instead of getting `undefined` downstream.
    if (!Array.isArray(response.features)) {
      throw new Error('Malformed /api/me/features response: features is not an array');
    }
    return response.features;
  }

  getVisits(cursor?: string): Promise<CursorPage<PlayerVisitDto>> {
    const query = cursor ? `?cursor=${encodeURIComponent(cursor)}` : '';
    return this.authedGet<CursorPage<PlayerVisitDto>>(`/api/me/visits${query}`);
  }

  getVisitReceipt(sessionId: string): Promise<PlayerVisitReceiptDto> {
    return this.authedGet<PlayerVisitReceiptDto>(`/api/me/visits/${encodeURIComponent(sessionId)}/receipt`);
  }

  getPurchases(cursor?: string): Promise<CursorPage<PlayerPurchaseDto>> {
    const query = cursor ? `?cursor=${encodeURIComponent(cursor)}` : '';
    return this.authedGet<CursorPage<PlayerPurchaseDto>>(`/api/me/purchases${query}`);
  }

  getProfile(): Promise<PlayerProfileDto> {
    return this.authedGet<PlayerProfileDto>('/api/me/profile');
  }

  updateProfile(request: UpdatePlayerProfileRequest): Promise<PlayerProfileDto> {
    return this.authedSend<PlayerProfileDto>('PATCH', '/api/me/profile', request);
  }

  createTopUpIntent(request: PlayerTopUpIntentRequest): Promise<PlayerTopUpIntentDto> {
    return this.authedSend<PlayerTopUpIntentDto>('POST', '/api/me/wallet/top-up-intent', request);
  }

  getTopUpIntents(): Promise<PlayerTopUpIntentDto[]> {
    return this.authedGet<PlayerTopUpIntentDto[]>('/api/me/wallet/top-up-intents');
  }

  getReservations(): Promise<PlayerReservationDto[]> {
    return this.authedGet<PlayerReservationDto[]>('/api/me/reservations');
  }

  createReservation(request: CreatePlayerReservationRequest): Promise<PlayerReservationDto> {
    return this.authedSend<PlayerReservationDto>('POST', '/api/me/reservations', request);
  }

  cancelReservation(reservationId: string): Promise<PlayerReservationDto> {
    return this.authedSend<PlayerReservationDto>('DELETE', `/api/me/reservations/${encodeURIComponent(reservationId)}`);
  }

  // Lets a long-lived client adopt a new session (e.g. after sign-in) without
  // being rebuilt, so consumers keep a stable client identity across auth changes.
  updateSession(session: PlayerSession | null): void {
    this.session = session;
  }

  private async publicPost<T>(path: string, body: unknown): Promise<T> {
    const response = await this.fetchImpl(`${this.baseUrl}${path}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body)
    });
    if (!response.ok) throw await PlayerApiClient.toError(response);
    return JSON.parse(await response.text()) as T;
  }

  private async authedGet<T>(path: string): Promise<T> {
    let response = await this.fetchImpl(`${this.baseUrl}${path}`, { method: 'GET', headers: this.buildHeaders() });
    if (response.status === 401 && (await this.refreshOnce())) {
      response = await this.fetchImpl(`${this.baseUrl}${path}`, { method: 'GET', headers: this.buildHeaders() });
    }
    if (!response.ok) throw await PlayerApiClient.toError(response);
    return JSON.parse(await response.text()) as T;
  }

  // Authenticated mutating request. Like authedGet, refreshes once on 401 and retries;
  // re-sends the same body with the refreshed token. Body omitted for verb-only calls (DELETE).
  private async authedSend<T>(method: string, path: string, body?: unknown): Promise<T> {
    const buildInit = (): RequestInit => {
      const headers = this.buildHeaders();
      if (body !== undefined) headers['Content-Type'] = 'application/json';
      return { method, headers, body: body !== undefined ? JSON.stringify(body) : undefined };
    };
    let response = await this.fetchImpl(`${this.baseUrl}${path}`, buildInit());
    if (response.status === 401 && (await this.refreshOnce())) {
      response = await this.fetchImpl(`${this.baseUrl}${path}`, buildInit());
    }
    if (!response.ok) throw await PlayerApiClient.toError(response);
    return JSON.parse(await response.text()) as T;
  }

  // GET requests carry no body, so no Content-Type — keeps them CORS-simple.
  private buildHeaders(): Record<string, string> {
    const headers: Record<string, string> = {};
    if (this.session) headers.Authorization = `Bearer ${this.session.accessToken}`;
    return headers;
  }

  private async refreshOnce(): Promise<boolean> {
    if (!this.session) return false;
    const response = await this.fetchImpl(`${this.baseUrl}/api/public/player/refresh`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken: this.session.refreshToken })
    });
    if (!response.ok) {
      this.session = null;
      this.onSessionChanged(null);
      return false;
    }
    const next = playerSessionFromSignInResponse(JSON.parse(await response.text()) as PlayerSignInResponse);
    this.session = next;
    this.onSessionChanged(next);
    return true;
  }

  private static async toError(response: Response): Promise<PlayerApiError> {
    let message = `Request failed with status ${response.status}`;
    try {
      const parsed = JSON.parse(await response.text()) as { error?: string };
      if (parsed.error) message = parsed.error;
    } catch { /* keep default */ }
    return new PlayerApiError(response.status, message);
  }
}
