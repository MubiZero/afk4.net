import type { AuditSearchResult } from '@/api/types';

export type OutcomeVariant = 'secondary' | 'destructive' | 'outline';

export interface AuditRow {
  id: string;
  date: string;
  actor: string;
  action: string;
  target: string;
  outcome: string;
  outcomeVariant: OutcomeVariant;
  source: string;
  details: string;
}

export function outcomeBadgeVariant(outcome: string): OutcomeVariant {
  if (outcome === 'Succeeded') return 'secondary';
  if (outcome === 'Denied') return 'destructive';
  return 'outline';
}

export function toAuditRows(
  result: AuditSearchResult,
  fmt: { formatDate: (iso: string) => string },
  systemLabel: string
): AuditRow[] {
  return result.records.map(record => ({
    id: record.auditRecordId,
    date: fmt.formatDate(record.createdAtUtc),
    actor: record.actorStaffUserId ?? record.actorPlatformAdminUserId ?? systemLabel,
    action: record.action,
    target: record.targetId === null ? record.targetType : `${record.targetType} (${record.targetId})`,
    outcome: record.outcome,
    outcomeVariant: outcomeBadgeVariant(record.outcome),
    source: record.sourceApp,
    details: record.detailsJson
  }));
}
