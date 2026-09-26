import { PlatformApiClient } from '../../platformApi';
import type { Guid } from '../types';
import type {
  BranchGameDto,
  CatalogGameDto,
  ReorderBranchGamesRequest,
  UpsertBranchGameRequest
} from '@afk4/contracts';
export type { BranchGameDto, CatalogGameDto, ReorderBranchGamesRequest, UpsertBranchGameRequest } from '@afk4/contracts';

// Библиотека игр филиала и каталог платформы, из которого её собирают (спека оболочки, §6.6).
export function createGamesClient(api: PlatformApiClient) {
  return {
    list(branchId: Guid): Promise<BranchGameDto[]> {
      return api.get<BranchGameDto[]>(`branches/${branchId}/games`);
    },
    catalog(query: string): Promise<CatalogGameDto[]> {
      return api.get<CatalogGameDto[]>('game-catalog', query.trim() ? { query: query.trim() } : undefined);
    },
    add(branchId: Guid, request: UpsertBranchGameRequest): Promise<BranchGameDto> {
      return api.post<BranchGameDto, UpsertBranchGameRequest>(`branches/${branchId}/games`, request);
    },
    update(branchId: Guid, branchGameId: Guid, request: UpsertBranchGameRequest): Promise<BranchGameDto> {
      return api.put<BranchGameDto, UpsertBranchGameRequest>(`branches/${branchId}/games/${branchGameId}`, request);
    },
    remove(branchId: Guid, branchGameId: Guid): Promise<void> {
      return api.delete<void>(`branches/${branchId}/games/${branchGameId}`);
    },
    reorder(branchId: Guid, request: ReorderBranchGamesRequest): Promise<BranchGameDto[]> {
      return api.put<BranchGameDto[], ReorderBranchGamesRequest>(`branches/${branchId}/games/order`, request);
    }
  };
}
