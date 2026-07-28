import { PlatformApiClient } from '../../platformApi';

export interface NewsItemDto {
  id: string;
  branchId: string | null;
  title: string;
  body: string;
  imageUrl: string | null;
  isPublished: boolean;
  publishAtUtc: string | null;
  expiresAtUtc: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface OwnerBranchSummaryDto {
  branchId: string;
  name: string;
}

export interface NewsItemInput {
  branchId: string | null;
  title: string;
  body: string;
  imageUrl: string | null;
  isPublished: boolean;
  publishAtUtc: string | null;
  expiresAtUtc: string | null;
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
