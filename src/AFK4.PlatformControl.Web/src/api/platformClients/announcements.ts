import type { PlatformTransport } from '../platformTransport';
import type { PlatformAnnouncement } from '../types';

export interface SaveAnnouncementRequest {
  title: string;
  body: string;
  severity: string;
  showFromUtc: string;
  showUntilUtc: string;
  audienceKind: string;
  audiencePlanCodes: string[];
  audienceOrganizationIds: string[];
}

export class AnnouncementsApi {
  public constructor(private readonly transport: PlatformTransport) {}

  public listAnnouncements(): Promise<PlatformAnnouncement[]> {
    return this.transport.send<PlatformAnnouncement[]>('GET', '/api/platform/announcements');
  }

  public createAnnouncement(request: SaveAnnouncementRequest): Promise<PlatformAnnouncement> {
    return this.transport.send<PlatformAnnouncement>('POST', '/api/platform/announcements', request);
  }

  public updateAnnouncement(announcementId: string, request: SaveAnnouncementRequest): Promise<PlatformAnnouncement> {
    return this.transport.send<PlatformAnnouncement>(
      'PUT', `/api/platform/announcements/${encodeURIComponent(announcementId)}`, request);
  }

  public publishAnnouncement(announcementId: string): Promise<PlatformAnnouncement> {
    return this.transport.send<PlatformAnnouncement>(
      'POST', `/api/platform/announcements/${encodeURIComponent(announcementId)}/publish`);
  }

  public withdrawAnnouncement(announcementId: string): Promise<PlatformAnnouncement> {
    return this.transport.send<PlatformAnnouncement>(
      'POST', `/api/platform/announcements/${encodeURIComponent(announcementId)}/withdraw`);
  }
}
