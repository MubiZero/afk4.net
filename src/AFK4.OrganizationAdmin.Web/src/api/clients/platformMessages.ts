import { PlatformApiClient } from '../../platformApi';

/** Сообщение платформы клубу. Тело пишет человек и приходит одной строкой — переводу не подлежит. */
export interface PlatformMessageDto {
  announcementId: string;
  title: string;
  body: string;
  severity: string;
  showFromUtc: string;
  showUntilUtc: string;
  publishedAtUtc: string;
  isRead: boolean;
}

export function createPlatformMessagesClient(api: PlatformApiClient) {
  return {
    async list(): Promise<PlatformMessageDto[]> {
      const items = await api.get<PlatformMessageDto[]>('announcements');
      // Некорректная форма 200-ответа обязана уходить в тот же путь, что и отказ: иначе
      // `undefined` доезжает до `.map` в компоненте шелла и роняет весь Оператор в белое.
      return Array.isArray(items) ? items : [];
    },
    async markRead(announcementId: string): Promise<void> {
      await api.post<void, undefined>(`announcements/${encodeURIComponent(announcementId)}/read`, undefined);
    }
  };
}
