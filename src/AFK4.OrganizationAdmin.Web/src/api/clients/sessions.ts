import { PlatformApiClient } from '../../platformApi';
import type { Guid, MoneyDto, ReportQuery } from '../types';
import { normalizeReportQuery } from '../queryHelpers';

export interface StartGuestSessionRequest {
  organizationId: Guid;
  seatId: Guid;
  // "open" (open tab, no end boundary) or "fixed" (reserves durationMinutes up front).
  durationMode?: string;
  durationMinutes?: number | null;
  tariffRuleVersionId: string;
  idempotencyKey: string;
  playerAccountId?: Guid | null;
  billingMode?: string;
  tariffVersionId?: Guid | null;
  playerPackageId?: Guid | null;
  isComp?: boolean;
  compReason?: string | null;
}

export interface ExtendSessionRequest {
  additionalMinutes: number;
  tariffRuleVersionId: string;
  idempotencyKey: string;
  playerAccountId?: Guid | null;
  billingMode?: string;
  tariffVersionId?: Guid | null;
  playerPackageId?: Guid | null;
}

export interface TransferSessionRequest {
  targetSeatId: Guid;
  idempotencyKey: string;
}

export interface EndSessionRequest {
  reason: string;
  idempotencyKey: string;
}

export interface SessionCommandResponse {
  idempotencyKey: string;
  session: Record<string, unknown>;
  deviceCommands: Array<Record<string, unknown>>;
}

export interface PaymentPartDto {
  paymentMethod: string;
  amount: MoneyDto;
}

export interface SessionCheckoutRequest {
  organizationId: Guid;
  payments: PaymentPartDto[];
  idempotencyKey: string;
}

export interface SessionCheckoutResponse {
  idempotencyKey: string;
  sessionId: Guid;
  timeCharge: MoneyDto;
  posTotal: MoneyDto;
  grandTotal: MoneyDto;
  payments: PaymentPartDto[];
  receipt: Record<string, unknown>;
  session: Record<string, unknown>;
  deviceCommands: Array<Record<string, unknown>>;
}

export interface SessionCheckoutQuoteResponse {
  sessionId: Guid;
  timeCharge: MoneyDto;
  posTotal: MoneyDto;
  grandTotal: MoneyDto;
  billableSeconds: number;
  playerAccountId?: Guid | null;
  walletBalance?: MoneyDto | null;
}

// Сессия для таймлайна броней: старт + плановый/фактический конец. Открытый таб — оба конца null.
export interface SessionTimelineItemDto {
  sessionId: Guid;
  seatId: Guid;
  seatName: string;
  zoneId: Guid;
  zoneName: string;
  state: string;
  playerAccountId: Guid | null;
  playerDisplayName: string | null;
  tariffName: string | null;
  startedAtUtc: string;
  endsAtUtc: string | null;
  endedAtUtc: string | null;
}

export interface SessionTimelineResult {
  sessions: SessionTimelineItemDto[];
}

export function createSessionClient(api: PlatformApiClient) {
  return {
    timeline(branchId: Guid, query?: ReportQuery): Promise<SessionTimelineResult> {
      return api.get<SessionTimelineResult>(`branches/${branchId}/sessions`, normalizeReportQuery(query));
    },
    startGuestSession(branchId: Guid, request: StartGuestSessionRequest): Promise<SessionCommandResponse> {
      return api.post<SessionCommandResponse, StartGuestSessionRequest>(`branches/${branchId}/sessions/start`, request);
    },
    extendSession(sessionId: Guid, request: ExtendSessionRequest): Promise<SessionCommandResponse> {
      return api.post<SessionCommandResponse, ExtendSessionRequest>(`sessions/${sessionId}/extend`, request);
    },
    transferSession(sessionId: Guid, request: TransferSessionRequest): Promise<SessionCommandResponse> {
      return api.post<SessionCommandResponse, TransferSessionRequest>(`sessions/${sessionId}/transfer`, request);
    },
    endSession(sessionId: Guid, request: EndSessionRequest): Promise<SessionCommandResponse> {
      return api.post<SessionCommandResponse, EndSessionRequest>(`sessions/${sessionId}/end`, request);
    },
    checkoutSession(sessionId: Guid, request: SessionCheckoutRequest): Promise<SessionCheckoutResponse> {
      return api.post<SessionCheckoutResponse, SessionCheckoutRequest>(`sessions/${sessionId}/checkout`, request);
    },
    getCheckoutQuote(sessionId: Guid): Promise<SessionCheckoutQuoteResponse> {
      return api.get<SessionCheckoutQuoteResponse>(`sessions/${sessionId}/checkout/quote`);
    }
  };
}
