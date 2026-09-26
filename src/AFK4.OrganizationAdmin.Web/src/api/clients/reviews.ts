import { PlatformApiClient } from '../../platformApi';
import type { Guid } from '../types';
import type { BranchReviewsPageDto, HideReviewCommentRequest, ReplyToReviewRequest, ReviewHideReasonName } from '@afk4/contracts';
export type { BranchReviewDto, BranchReviewsPageDto, ReviewHideReasonName } from '@afk4/contracts';

export interface BranchReviewsQuery {
  rating?: number | null;
  withComment?: boolean;
  before?: string | null;
}

// Отзывы игроков о филиале: итог, разбивка по звёздам и страница с ПК, за которым сидели.
export function createReviewsClient(api: PlatformApiClient) {
  const review = (branchId: Guid, reviewId: Guid, action: string) => `branches/${branchId}/reviews/${reviewId}/${action}`;
  return {
    list(branchId: Guid, query: BranchReviewsQuery = {}): Promise<BranchReviewsPageDto> {
      const params: Record<string, string> = {};
      if (query.rating) params.rating = String(query.rating);
      if (query.withComment) params.withComment = 'true';
      if (query.before) params.before = query.before;
      return api.get<BranchReviewsPageDto>(`branches/${branchId}/reviews`, params);
    },
    /** Ответ клуба под отзывом; пустой снимает прежний. */
    reply(branchId: Guid, reviewId: Guid, reply: string): Promise<void> {
      return api.post<void, ReplyToReviewRequest>(review(branchId, reviewId, 'reply'), { reply });
    },
    /** Спрятать текст от игроков; звёзды остаются в оценке. */
    hideComment(branchId: Guid, reviewId: Guid, reason: ReviewHideReasonName): Promise<void> {
      return api.post<void, HideReviewCommentRequest>(review(branchId, reviewId, 'hide-comment'), { reason });
    },
    showComment(branchId: Guid, reviewId: Guid): Promise<void> {
      return api.post<void, undefined>(review(branchId, reviewId, 'show-comment'), undefined);
    }
  };
}
