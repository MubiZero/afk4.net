import { PlatformApiClient } from '../../platformApi';
import type { Guid, ReportQuery } from '../types';
import { normalizeReportQuery } from '../queryHelpers';
import type {
  EndSessionRequest,
  ExtendSessionRequest,
  SessionCheckoutQuoteResponse,
  SessionCheckoutRequest,
  SessionCheckoutResponse,
  SessionCommandResponse,
  SessionTimelineResult,
  StartGuestSessionRequest,
  TransferSessionRequest,
} from '@afk4/contracts';
export type {
  EndSessionRequest,
  ExtendSessionRequest,
  PaymentPartDto,
  SessionCheckoutQuoteResponse,
  SessionCheckoutRequest,
  SessionCheckoutResponse,
  SessionCommandResponse,
  SessionDto,
  SessionLeaseDto,
  SessionTimelineItemDto,
  SessionTimelineResult,
  StartGuestSessionRequest,
  TransferSessionRequest,
} from '@afk4/contracts';

/**
 * Ответ на любое действие со сессией на карте мест: расчёт отдаёт чек, остальные — нет, но
 * сессия и посланные машине команды есть у обоих.
 */
export type SessionActionResponse = SessionCommandResponse | SessionCheckoutResponse;

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
