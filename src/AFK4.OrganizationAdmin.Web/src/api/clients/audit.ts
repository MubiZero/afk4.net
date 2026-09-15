import { PlatformApiClient } from '../../platformApi';
import type { Guid } from '../types';
import { normalizeReportQuery } from '../queryHelpers';

/**
 * Запись журнала действий (AuditRecordDto). Поля сверяются в `contractParity.test.ts`, включая
 * два последних — они объявлены в C# не параметрами записи, а свойствами `{ get; init; }`.
 */
export interface AuditRecordDto {
  auditRecordId: Guid;
  organizationId: Guid;
  branchId: Guid | null;
  actorStaffUserId: Guid | null;
  action: string;
  targetType: string;
  targetId: string | null;
  outcome: string;
  sourceApp: string;
  detailsJson: string;
  createdAtUtc: string;
  actorPlatformAdminUserId: Guid | null;
  amountMinorUnits: number | null;
}

export interface AuditSearchResultDto {
  records: AuditRecordDto[];
  limit: number;
}

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
      return api.get<AuditSearchResultDto>(`branches/${branchId}/audit`, normalizeReportQuery(query));
    }
  };
}
