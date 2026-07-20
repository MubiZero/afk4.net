import { PlatformApiClient } from '../../platformApi';

export interface UploadedMediaDto {
  mediaId: string;
  url: string;
  contentType: string;
  sizeBytes: number;
}

export function createMediaClient(api: PlatformApiClient) {
  return {
    upload(branchId: string, purpose: string, file: File): Promise<UploadedMediaDto> {
      const form = new FormData();
      form.append('file', file);
      form.append('purpose', purpose);
      return api.postForm<UploadedMediaDto>(`/api/branches/${branchId}/media`, form);
    },
    remove(branchId: string, mediaId: string): Promise<void> {
      return api.delete<void>(`/api/branches/${branchId}/media/${mediaId}`);
    }
  };
}
