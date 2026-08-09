import type { PlatformTransport } from '../platformTransport';
import type { OrganizationOffboarding, PurgeOrganizationResult } from '../types';

export class OffboardingApi {
  public constructor(private readonly transport: PlatformTransport) {}

  public getOffboarding(organizationId: string): Promise<OrganizationOffboarding> {
    return this.transport.send<OrganizationOffboarding>(
      'GET', `/api/platform/organizations/${encodeURIComponent(organizationId)}/offboarding`);
  }

  public purge(organizationId: string, slug: string): Promise<PurgeOrganizationResult> {
    return this.transport.send<PurgeOrganizationResult>(
      'POST', `/api/platform/organizations/${encodeURIComponent(organizationId)}/purge`, { slug });
  }

  /** Архив приходит байтами и с токеном в заголовке — простая ссылка получила бы 401. */
  public downloadExport(organizationId: string): Promise<Blob> {
    return this.transport.download(`/api/platform/organizations/${encodeURIComponent(organizationId)}/export`);
  }
}
