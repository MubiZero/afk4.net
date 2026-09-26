import { PlatformApiClient } from '../../platformApi';
import type { Guid } from '../types';
import type { BranchReviewsPageDto } from '@afk4/contracts';
export type { BranchReviewDto, BranchReviewsPageDto } from '@afk4/contracts';

export interface BranchReviewsQuery {
  rating?: number | null;
  withComment?: boolean;
  before?: string | null;
}

// Отзывы игроков о филиале: итог, разбивка по звёздам и страница с ПК, за которым сидели.
export function createReviewsClient(api: PlatformApiClient) {
  return {
    list(branchId: Guid, query: BranchReviewsQuery = {}): Promise<BranchReviewsPageDto> {
      const params: Record<string, string> = {};
      if (query.rating) params.rating = String(query.rating);
      if (query.withComment) params.withComment = 'true';
      if (query.before) params.before = query.before;
      return api.get<BranchReviewsPageDto>(`branches/${branchId}/reviews`, params);
    }
  };
}
