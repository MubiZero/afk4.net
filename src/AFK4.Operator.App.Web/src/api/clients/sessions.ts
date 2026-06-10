import { PlatformApiClient } from '../../platformApi';
import type { Guid, MoneyDto } from '../types';

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

export function createSessionClient(api: PlatformApiClient) {
  return {
    startGuestSession(branchId: Guid, request: StartGuestSessionRequest): Promise<SessionCommandResponse> {
      return api.post<SessionCommandResponse, StartGuestSessionRequest>(`/api/branches/${branchId}/sessions/start`, request);
    },
    extendSession(sessionId: Guid, request: ExtendSessionRequest): Promise<SessionCommandResponse> {
      return api.post<SessionCommandResponse, ExtendSessionRequest>(`/api/sessions/${sessionId}/extend`, request);
    },
    transferSession(sessionId: Guid, request: TransferSessionRequest): Promise<SessionCommandResponse> {
      return api.post<SessionCommandResponse, TransferSessionRequest>(`/api/sessions/${sessionId}/transfer`, request);
    },
    endSession(sessionId: Guid, request: EndSessionRequest): Promise<SessionCommandResponse> {
      return api.post<SessionCommandResponse, EndSessionRequest>(`/api/sessions/${sessionId}/end`, request);
    },
    checkoutSession(sessionId: Guid, request: SessionCheckoutRequest): Promise<SessionCheckoutResponse> {
      return api.post<SessionCheckoutResponse, SessionCheckoutRequest>(`/api/sessions/${sessionId}/checkout`, request);
    },
    getCheckoutQuote(sessionId: Guid): Promise<SessionCheckoutQuoteResponse> {
      return api.get<SessionCheckoutQuoteResponse>(`/api/sessions/${sessionId}/checkout/quote`);
    }
  };
}
