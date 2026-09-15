import { PlatformApiClient } from '../../platformApi';
import type { Guid, ReportQuery } from '../types';
import { normalizeReportQuery } from '../queryHelpers';
import type {
  CancelReservationRequest,
  ConfirmReservationRequest,
  CreateReservationGroupRequest,
  CreateReservationRequest,
  MarkReservationNoShowRequest,
  RejectReservationRequest,
  ReservationDto,
  ReservationGroupResultDto,
  ReservationSearchResultDto,
  SeatReservationRequest,
  StartReservationSessionRequest,
  StartReservationSessionResponse,
  UpdateReservationRequest,
} from '@afk4/contracts';
export type {
  CancelReservationRequest,
  ConfirmReservationRequest,
  CreateReservationGroupRequest,
  CreateReservationRequest,
  MarkReservationNoShowRequest,
  RejectReservationRequest,
  ReservationDto,
  ReservationGroupConflictDto,
  ReservationGroupResultDto,
  ReservationSearchResultDto,
  SeatReservationRequest,
  StartReservationSessionRequest,
  StartReservationSessionResponse,
  UpdateReservationRequest,
} from '@afk4/contracts';

export type ReservationSearchQuery = ReportQuery & {
  state?: string | null;
  source?: string | null;
  playerAccountId?: Guid | null;
};

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
