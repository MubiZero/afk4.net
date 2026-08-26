import { PlatformApiClient } from '../../platformApi';
import type { MoneyDto } from '../types';

export interface TournamentDto {
  tournamentId: string;
  branchId: string;
  title: string;
  description: string;
  discipline: string;
  startsAtUtc: string;
  entryFee: MoneyDto;
  capacity: number;
  state: string;
  registeredCount: number;
  createdAtUtc: string;
  updatedAtUtc: string;
  cancelledAtUtc: string | null;
  cancelReason: string;
}

export interface TournamentParticipantDto {
  tournamentRegistrationId: string;
  playerAccountId: string;
  displayName: string;
  phoneNumber: string | null;
  entryFeePaid: MoneyDto;
  registeredAtUtc: string;
}

export interface CreateTournamentRequest {
  branchId: string;
  title: string;
  description: string;
  discipline: string;
  startsAtUtc: string;
  entryFeeMinorUnits: number;
  capacity: number;
}

/// Незаполненное поле означает «оставить как было»: стойка правит одну строку, а не переписывает
/// событие целиком.
export interface UpdateTournamentRequest {
  title?: string;
  description?: string;
  discipline?: string;
  startsAtUtc?: string;
  entryFeeMinorUnits?: number;
  capacity?: number;
}

export function createTournamentClient(api: PlatformApiClient) {
  return {
    list(branchId: string): Promise<TournamentDto[]> {
      return api.get<TournamentDto[]>(`branches/${branchId}/tournaments`);
    },
    create(request: CreateTournamentRequest): Promise<TournamentDto> {
      return api.post<TournamentDto, CreateTournamentRequest>('tournaments', request);
    },
    update(tournamentId: string, request: UpdateTournamentRequest): Promise<TournamentDto> {
      return api.patch<TournamentDto, UpdateTournamentRequest>(`tournaments/${tournamentId}`, request);
    },
    publish(tournamentId: string): Promise<TournamentDto> {
      return api.post<TournamentDto, undefined>(`tournaments/${tournamentId}/publish`, undefined);
    },
    cancel(tournamentId: string, reason: string): Promise<TournamentDto> {
      return api.post<TournamentDto, { reason: string }>(`tournaments/${tournamentId}/cancel`, { reason });
    },
    participants(tournamentId: string): Promise<TournamentParticipantDto[]> {
      return api.get<TournamentParticipantDto[]>(`tournaments/${tournamentId}/participants`);
    }
  };
}
