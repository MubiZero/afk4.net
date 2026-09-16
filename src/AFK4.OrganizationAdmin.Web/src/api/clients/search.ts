import { PlatformApiClient } from '../../platformApi';
import type { Guid } from '../types';
import type { BranchSearchResultDto } from '@afk4/contracts';
export type { BranchSearchResultDto } from '@afk4/contracts';

export function createSearchClient(api: PlatformApiClient) {
  return {
    // Один запрос на все виды сразу: палитра спрашивает на каждое нажатие, и четыре похода в
    // сеть вместо одного она бы не пережила.
    searchBranch(branchId: Guid, query: string, limit: number): Promise<BranchSearchResultDto[]> {
      return api.get<BranchSearchResultDto[]>(`branches/${branchId}/search`, { query, limit });
    }
  };
}
