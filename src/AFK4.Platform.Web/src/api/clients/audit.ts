import type { ApiTransport } from '../apiTransport';
import type { AuditSearchQuery, AuditSearchResult } from '../types';

export class AuditApi {
  public constructor(private readonly transport: ApiTransport) {}

  public searchAudit(branchId: string, query: AuditSearchQuery): Promise<AuditSearchResult> {
    const params = new URLSearchParams();
    if (query.action !== undefined && query.action.length > 0) params.set('action', query.action);
    if (query.outcome !== undefined && query.outcome.length > 0) params.set('outcome', query.outcome);
    if (query.targetType !== undefined && query.targetType.length > 0) params.set('targetType', query.targetType);
    if (query.fromUtc !== undefined) params.set('fromUtc', query.fromUtc);
    if (query.toUtc !== undefined) params.set('toUtc', query.toUtc);
    if (query.limit !== undefined) params.set('limit', String(query.limit));
    const qs = params.toString();
    return this.transport.send<AuditSearchResult>('GET', `/api/branches/${encodeURIComponent(branchId)}/audit${qs.length > 0 ? `?${qs}` : ''}`);
  }
}
