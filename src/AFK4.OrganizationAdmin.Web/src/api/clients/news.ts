import { PlatformApiClient } from '../../platformApi';
import type { NewsItemDto, OwnerBranchSummaryDto } from '@afk4/contracts';
export type { NewsItemDto, OwnerBranchSummaryDto } from '@afk4/contracts';

export interface NewsItemInput {
  branchId: string | null;
  title: string;
  body: string;
  imageUrl: string | null;
  isPublished: boolean;
  publishAtUtc: string | null;
  expiresAtUtc: string | null;
  showOnPcs: boolean;
}

export function createNewsClient(api: PlatformApiClient) {
  return {
    list(): Promise<NewsItemDto[]> {
      return api.get<NewsItemDto[]>('news');
    },
    listBranches(): Promise<OwnerBranchSummaryDto[]> {
      return api.get<OwnerBranchSummaryDto[]>('branches');
    },
    create(request: NewsItemInput): Promise<NewsItemDto> {
      return api.post<NewsItemDto, NewsItemInput>('news', request);
    },
    update(id: string, request: NewsItemInput): Promise<NewsItemDto> {
      return api.patch<NewsItemDto, NewsItemInput>(`news/${id}`, request);
    },
    remove(id: string): Promise<void> {
      return api.delete<void>(`news/${id}`);
    }
  };
}
