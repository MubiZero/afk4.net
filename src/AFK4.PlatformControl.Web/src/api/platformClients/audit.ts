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

  public search(filters: { organizationId?: string; action?: string; outcome?: string; fromUtc?: string; toUtc?: string; limit?: number }): Promise<AuditSearchResult> {
    const query = new URLSearchParams();
    if (filters.organizationId) query.set('organizationId', filters.organizationId);
    if (filters.action) query.set('action', filters.action);
    if (filters.outcome) query.set('outcome', filters.outcome);
    if (filters.fromUtc) query.set('fromUtc', filters.fromUtc);
    if (filters.toUtc) query.set('toUtc', filters.toUtc);
    query.set('limit', String(filters.limit ?? 100));
    return this.transport.send<AuditSearchResult>('GET', `/api/platform/audit?${query}`);
  }
}
