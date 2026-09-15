import { PlatformApiClient } from '../../platformApi';
import type { Guid, ReportQuery } from '../types';
import { normalizeReportQuery } from '../queryHelpers';
import type { SessionCommandResponse } from './sessions';

/**
 * Бронь, как её отдаёт сервер (ReservationDto). Совпадение полей проверяется
 * в `contractParity.test.ts`.
 *
 * Хвост необязательных полей повторяет необязательные параметры C#-записи: их дописывали
 * волнами (цена заявки, срок ответа, неявка, отказ), и старые вызовы сервера их не заполняли.
 */
export interface ReservationDto {
  reservationId: Guid;
  organizationId: Guid;
  branchId: Guid;
  playerAccountId: Guid | null;
  seatId: Guid | null;
  seatName: string | null;
  zoneName: string | null;
  customerName: string;
  phoneNumber: string | null;
  startsAtUtc: string;
  endsAtUtc: string;
  durationMinutes: number;
  state: string;
  source: string;
  note: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  cancelledAtUtc: string | null;
  cancelReason: string;
  reservationGroupId: Guid | null;
  version?: number;
  startedSessionId?: Guid | null;
  tariffVersionId?: Guid | null;
  tariffName?: string | null;
  estimatedCostMinorUnits?: number | null;
  currencyCode?: string | null;
  respondByUtc?: string | null;
  confirmedAtUtc?: string | null;
  platformPersonId?: Guid | null;
  noShowAtUtc?: string | null;
  // Пусто, а не ноль, когда за неявку не удерживали вовсе: ноль читался бы как «удержали нисколько».
  retainedAmountMinorUnits?: number | null;
  rejectedAtUtc?: string | null;
  rejectReasonCode?: string | null;
  rejectReasonNote?: string | null;
}

export interface ReservationSearchResultDto {
  reservations: ReservationDto[];
  limit: number;
}

export type ReservationSearchQuery = ReportQuery & {
  state?: string | null;
  source?: string | null;
  playerAccountId?: Guid | null;
};

export interface CreateReservationRequest extends Record<string, unknown> {
  organizationId: Guid;
}

export interface CreateReservationGroupRequest extends Record<string, unknown> {
  organizationId: Guid;
  playerAccountId?: Guid | null;
  seatIds: Guid[];
  customerName: string;
  phoneNumber?: string | null;
  startsAtUtc: string;
  durationMinutes: number;
  source: string;
  note?: string | null;
}

export interface ReservationGroupConflictDto {
  seatId: Guid;
  reason: string;
}

// На 409 это же тело приходит в PlatformApiError.body (массовая бронь — all-or-nothing).
export interface ReservationGroupResultDto {
  reservationGroupId: Guid | null;
  reservations: ReservationDto[];
  conflicts: ReservationGroupConflictDto[];
}

export interface UpdateReservationRequest extends Record<string, unknown> {
  organizationId: Guid;
  expectedVersion: number;
}

export interface ConfirmReservationRequest {
  organizationId: Guid;
  expectedVersion: number;
}

export interface SeatReservationRequest {
  organizationId: Guid;
  expectedVersion: number;
}

// Версия здесь необязательна, и это не небрежность: повторный клик по уже отмеченной неявке
// сервер отвечает той же бронью, а не конфликтом версий — удерживать за одну неявку дважды нельзя.
export interface MarkReservationNoShowRequest {
  organizationId: Guid;
  expectedVersion?: number | null;
}

export interface CancelReservationRequest {
  organizationId: Guid;
  reason: string;
  expectedVersion: number;
}

/**
 * Отказ клуба в заявке. Не отмена: игроку возвращаются деньги целиком, а причину он читает на
 * своём языке — поэтому код из справочника, а не свободный текст.
 */
export interface RejectReservationRequest {
  organizationId: Guid;
  reasonCode: string;
  note?: string | null;
  expectedVersion?: number;
}

export interface StartReservationSessionRequest {
  organizationId: Guid;
  expectedVersion: number;
  tariffRuleVersionId: string;
  idempotencyKey: string;
  durationMode?: string;
  durationMinutes?: number | null;
  billingMode?: string;
  tariffVersionId?: Guid | null;
  playerPackageId?: Guid | null;
  isComp?: boolean;
  compReason?: string | null;
}

export interface StartReservationSessionResponse {
  reservation: ReservationDto;
  session: SessionCommandResponse;
}

export function createReservationClient(api: PlatformApiClient) {
  return {
    search(branchId: Guid, query?: ReservationSearchQuery): Promise<ReservationSearchResultDto> {
      return api.get<ReservationSearchResultDto>(`branches/${branchId}/reservations`, normalizeReportQuery(query));
    },
    create(branchId: Guid, request: CreateReservationRequest): Promise<ReservationDto> {
      return api.post<ReservationDto, CreateReservationRequest>(`branches/${branchId}/reservations`, request);
    },
    createGroup(branchId: Guid, request: CreateReservationGroupRequest): Promise<ReservationGroupResultDto> {
      return api.post<ReservationGroupResultDto, CreateReservationGroupRequest>(`branches/${branchId}/reservations/group`, request);
    },
    update(reservationId: Guid, request: UpdateReservationRequest): Promise<ReservationDto> {
      return api.patch<ReservationDto, UpdateReservationRequest>(`reservations/${reservationId}`, request);
    },
    confirm(reservationId: Guid, request: ConfirmReservationRequest): Promise<ReservationDto> {
      return api.post<ReservationDto, ConfirmReservationRequest>(`reservations/${reservationId}/confirm`, request);
    },
    seat(reservationId: Guid, request: SeatReservationRequest): Promise<ReservationDto> {
      return api.post<ReservationDto, SeatReservationRequest>(`reservations/${reservationId}/seat`, request);
    },
    noShow(reservationId: Guid, request: MarkReservationNoShowRequest): Promise<ReservationDto> {
      return api.post<ReservationDto, MarkReservationNoShowRequest>(`reservations/${reservationId}/no-show`, request);
    },
    cancel(reservationId: Guid, request: CancelReservationRequest): Promise<ReservationDto> {
      return api.post<ReservationDto, CancelReservationRequest>(`reservations/${reservationId}/cancel`, request);
    },
    reject(reservationId: Guid, request: RejectReservationRequest): Promise<ReservationDto> {
      return api.post<ReservationDto, RejectReservationRequest>(`reservations/${reservationId}/reject`, request);
    },
    startSession(
      reservationId: Guid,
      request: StartReservationSessionRequest
    ): Promise<StartReservationSessionResponse> {
      return api.post<StartReservationSessionResponse, StartReservationSessionRequest>(
        `reservations/${reservationId}/start-session`,
        request
      );
    }
  };
}
