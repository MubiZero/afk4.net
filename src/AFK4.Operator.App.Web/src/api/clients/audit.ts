import { PlatformApiClient } from '../../platformApi';
import type { Guid } from '../types';
import { normalizeReportQuery } from '../queryHelpers';

export type AuditSearchResultDto = Record<string, unknown>;

export interface AuditSearchRequest {
  branchId: Guid;
  action?: string | null;
  outcome?: string | null;
  targetType?: string | null;
  fromUtc?: string | Date | null;
  toUtc?: string | Date | null;
  actorStaffUserId?: string | null;
  minAmount?: number | null;
  maxAmount?: number | null;
  limit?: number | null;
}

export function createAuditClient(api: PlatformApiClient) {
  return {
    search(request: AuditSearchRequest): Promise<AuditSearchResultDto> {
      const { branchId, ...query } = request;
      return api.get<AuditSearchResultDto>(`/api/branches/${branchId}/audit`, normalizeReportQuery(query));
    }
  };
}
