import type { PlatformTransport } from '../platformTransport';
import type { PlatformMediaPurposeName, PlatformMediaUploadedDto } from '../types';

/** Маршрут загрузки — зеркало `PlatformMediaRoutes.Upload` (статические маршруты кодоген не переносит). */
export const PLATFORM_MEDIA_UPLOAD = '/api/platform/media';

/** Картинка платформы в общее хранилище (MinIO): обложка каталога или картинка рекламы. */
export function uploadPlatformImage(
  transport: PlatformTransport,
  purpose: PlatformMediaPurposeName,
  file: File
): Promise<PlatformMediaUploadedDto> {
  const form = new FormData();
  form.append('purpose', purpose);
  form.append('file', file);
  return transport.send<PlatformMediaUploadedDto>('POST', PLATFORM_MEDIA_UPLOAD, form);
}
