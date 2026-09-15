import { PlatformApiClient } from '../../platformApi';
import type {
  CreateTournamentRequest,
  TournamentDto,
  TournamentParticipantDto,
  UpdateTournamentRequest,
} from '@afk4/contracts';
export type {
  CreateTournamentRequest,
  TournamentDto,
  TournamentParticipantDto,
  UpdateTournamentRequest,
} from '@afk4/contracts';

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
