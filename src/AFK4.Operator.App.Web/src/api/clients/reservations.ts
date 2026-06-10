import { PlatformApiClient } from '../../platformApi';
import type { Guid, ReportQuery } from '../types';
import { normalizeReportQuery } from '../queryHelpers';

export type ReservationDto = Record<string, unknown>;
export type ReservationSearchResultDto = Record<string, unknown>;

export type ReservationSearchQuery = ReportQuery & {
  state?: string | null;
  source?: string | null;
};

export interface CreateReservationRequest extends Record<string, unknown> {
  organizationId: Guid;
}

export interface UpdateReservationRequest extends Record<string, unknown> {
  organizationId: Guid;
}

export interface ConfirmReservationRequest {
  organizationId: Guid;
}

export interface SeatReservationRequest {
  organizationId: Guid;
}

export interface CancelReservationRequest {
  organizationId: Guid;
  reason: string;
}

export function createReservationClient(api: PlatformApiClient) {
  return {
    search(branchId: Guid, query?: ReservationSearchQuery): Promise<ReservationSearchResultDto> {
      return api.get<ReservationSearchResultDto>(`/api/branches/${branchId}/reservations`, normalizeReportQuery(query));
    },
    create(branchId: Guid, request: CreateReservationRequest): Promise<ReservationDto> {
      return api.post<ReservationDto, CreateReservationRequest>(`/api/branches/${branchId}/reservations`, request);
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
    }
  };
}
