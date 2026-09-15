import { PlatformApiClient } from '../../platformApi';
import type { UploadedMediaDto } from '@afk4/contracts';
export type { UploadedMediaDto } from '@afk4/contracts';

export function createMediaClient(api: PlatformApiClient) {
  return {
    upload(branchId: string, purpose: string, file: File): Promise<UploadedMediaDto> {
      const form = new FormData();
      form.append('file', file);
      form.append('purpose', purpose);
      return api.postForm<UploadedMediaDto>(`branches/${branchId}/media`, form);
    },
    remove(branchId: string, mediaId: string): Promise<void> {
      return api.delete<void>(`branches/${branchId}/media/${mediaId}`);
    }
  };
}
