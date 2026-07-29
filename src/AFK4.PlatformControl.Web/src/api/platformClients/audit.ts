import type { PlatformTransport } from '../platformTransport';
import type { AuditSearchResult } from '../types';

export class AuditApi {
  public constructor(private readonly transport: PlatformTransport) {}

  public listOrganizationHistory(organizationId: string, limit = 100): Promise<AuditSearchResult> {
    return this.transport.send<AuditSearchResult>(
      'GET',
      `/api/platform/organizations/${organizationId}/audit?limit=${limit}`
    );
  }
}
