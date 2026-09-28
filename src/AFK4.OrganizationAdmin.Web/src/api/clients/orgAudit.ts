import { PlatformApiClient } from '../../platformApi';
import type { Guid } from '../types';
import { normalizeReportQuery } from '../queryHelpers';

// Запись журнала — тип контракта, а не своё зеркало: своё отставало бы от сервера (так и было
// с именем исполнителя).
export type { AuditRecordDto as OrgAuditRecordDto, AuditSearchResultDto as OrgAuditSearchResultDto } from '@afk4/contracts';
import type { AuditSearchResultDto as OrgAuditSearchResultDto } from '@afk4/contracts';

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
    searchOrganizationAudit(_organizationId: Guid, query: OrgAuditQuery): Promise<OrgAuditSearchResultDto> {
      return api.get<OrgAuditSearchResultDto>('audit', normalizeReportQuery(query));
    }
  };
}
