import { PlatformApiClient } from '../../platformApi';
import type { Guid, MoneyDto, ReportQuery } from '../types';
import { normalizeReportQuery } from '../queryHelpers';
import type { DeviceCommandDto } from './devices';
import type { ReceiptDto } from './pos';

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

/**
 * Аренда места машиной (SessionLeaseDto): подписанный платформой пропуск, по которому
 * агент на ПК держит сессию открытой.
 */
export interface SessionLeaseDto {
  sessionId: Guid;
  organizationId: Guid;
  branchId: Guid;
  seatId: Guid;
  deviceId: Guid;
  state: string;
  sequence: number;
  issuedAtUtc: string;
  expiresAtUtc: string;
  signatureAlgorithm: string;
  signature: string;
}

/**
 * Сессия, как её отдаёт сервер (SessionDto). Раньше на этом месте стоял
 * `Record<string, unknown>`: ответ на старт, продление, перенос, завершение и расчёт — весь
 * денежный путь стойки — не имел ни одного названного поля, и опечатка в имени читалась как
 * пустое значение.
 *
 * Совпадение с сервером проверяется, а не обещается: см. `contractParity.test.ts`.
 */
export interface SessionDto {
  sessionId: Guid;
  organizationId: Guid;
  branchId: Guid;
  seatId: Guid;
  deviceId: Guid;
  state: string;
  tariffRuleVersionId: string;
  startedAtUtc: string | null;
  endsAtUtc: string | null;
  endedAtUtc: string | null;
  remainingSeconds: number | null;
  currentLease: SessionLeaseDto | null;
  /** Версия для оптимистичной блокировки — её же клиент возвращает следующей правкой. */
  version: number;
}

export interface SessionCommandResponse {
  idempotencyKey: string;
  session: SessionDto;
  deviceCommands: DeviceCommandDto[];
  /** Оценённая стоимость бесплатной сессии; null у обычного старта. */
  compValueMinorUnits: number | null;
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
  receipt: ReceiptDto;
  session: SessionDto;
  deviceCommands: DeviceCommandDto[];
}

/**
 * Ответ на любое действие со сессией на карте мест: расчёт отдаёт чек, остальные — нет, но
 * сессия и посланные машине команды есть у обоих.
 */
export type SessionActionResponse = SessionCommandResponse | SessionCheckoutResponse;

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
