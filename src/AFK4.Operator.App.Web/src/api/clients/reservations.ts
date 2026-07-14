import { PlatformApiClient } from '../../platformApi';
import type { Guid, ReportQuery } from '../types';
import { normalizeReportQuery } from '../queryHelpers';
import type { SessionCommandResponse } from './sessions';

export type ReservationDto = Record<string, unknown>;
export type ReservationSearchResultDto = Record<string, unknown>;

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

export interface CancelReservationRequest {
  organizationId: Guid;
  reason: string;
  expectedVersion: number;
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
      return api.get<ReservationSearchResultDto>(`/api/branches/${branchId}/reservations`, normalizeReportQuery(query));
    },
    create(branchId: Guid, request: CreateReservationRequest): Promise<ReservationDto> {
      return api.post<ReservationDto, CreateReservationRequest>(`/api/branches/${branchId}/reservations`, request);
    },
    createGroup(branchId: Guid, request: CreateReservationGroupRequest): Promise<ReservationGroupResultDto> {
      return api.post<ReservationGroupResultDto, CreateReservationGroupRequest>(`/api/branches/${branchId}/reservations/group`, request);
    },
    update(reservationId: Guid, request: UpdateReservationRequest): Promise<ReservationDto> {
      return api.patch<ReservationDto, UpdateReservationRequest>(`/api/reservations/${reservationId}`, request);
    },
    confirm(reservationId: Guid, request: ConfirmReservationRequest): Promise<ReservationDto> {
      return api.post<ReservationDto, ConfirmReservationRequest>(`/api/reservations/${reservationId}/confirm`, request);
    },
    seat(reservationId: Guid, request: SeatReservationRequest): Promise<ReservationDto> {
      return api.post<ReservationDto, SeatReservationRequest>(`/api/reservations/${reservationId}/seat`, request);
    },
    cancel(reservationId: Guid, request: CancelReservationRequest): Promise<ReservationDto> {
      return api.post<ReservationDto, CancelReservationRequest>(`/api/reservations/${reservationId}/cancel`, request);
    },
    startSession(
      reservationId: Guid,
      request: StartReservationSessionRequest
    ): Promise<StartReservationSessionResponse> {
      return api.post<StartReservationSessionResponse, StartReservationSessionRequest>(
        `/api/reservations/${reservationId}/start-session`,
        request
      );
    }
  };
}
