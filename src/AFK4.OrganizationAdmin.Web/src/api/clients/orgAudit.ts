import { PlatformApiClient } from '../../platformApi';
import type { Guid } from '../types';
import { normalizeReportQuery } from '../queryHelpers';

export interface OrgAuditRecordDto {
  auditRecordId: string;
  branchId: string | null;
  actorStaffUserId: string | null;
  actorPlatformAdminUserId: string | null;
  action: string;
  targetType: string;
  targetId: string | null;
  outcome: string;
  sourceApp: string;
  detailsJson: string;
  createdAtUtc: string;
}

export interface OrgAuditSearchResultDto {
  records: OrgAuditRecordDto[];
  limit: number;
}

export interface OrgAuditQuery {
  action?: string | null;
  outcome?: string | null;
  targetType?: string | null;
  fromUtc?: string | null;
  toUtc?: string | null;
  limit?: number | null;
}

export function createOrgAuditClient(api: PlatformApiClient) {
  return {
    searchOrganizationAudit(organizationId: Guid, query: OrgAuditQuery): Promise<OrgAuditSearchResultDto> {
      return api.get<OrgAuditSearchResultDto>('audit', normalizeReportQuery(query));
    }
  };
}
