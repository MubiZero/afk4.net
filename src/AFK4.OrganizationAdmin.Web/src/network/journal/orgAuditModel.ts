import type { OrgAuditRecordDto } from '../../api/clients/orgAudit';

// Chip tone vocabulary matches .ui-chip--status's real modifiers in 02-ui-kit.css (see also
// billingModel.ts / stock/journalModel.ts's movementStatusTone) — is-booking is unused here, no
// audit outcome maps to it.
export type OutcomeTone = 'is-live' | 'is-neutral' | 'is-booking' | 'is-warning' | 'is-danger';

export interface AuditRow {
  id: string;
  date: string;
  actor: string;
  action: string;
  target: string;
  outcome: string;
  outcomeTone: OutcomeTone;
  source: string;
  details: string;
}

export function outcomeChipTone(outcome: string): OutcomeTone {
  if (outcome === 'Succeeded') return 'is-live';
  if (outcome === 'Denied') return 'is-danger';
  if (outcome === 'Failed') return 'is-danger';
  return 'is-neutral';
}

/**
 * Строки журнала для человека. Исполнитель раньше был сырым GUID: владелец, открывший журнал
 * ответить «кто трогал подписку», получал столбец идентификаторов. Имя приносит сервер; без имени
 * — начало id, чтобы строку было с чем сверить.
 */
export function toAuditRows(
  records: readonly OrgAuditRecordDto[],
  fmt: { formatDate: (iso: string) => string },
  systemLabel: string
): AuditRow[] {
  return records.map((r) => ({
    id: r.auditRecordId,
    date: fmt.formatDate(r.createdAtUtc),
    actor: r.actorDisplayName ?? (r.actorStaffUserId ?? r.actorPlatformAdminUserId)?.slice(0, 8) ?? systemLabel,
    action: r.action,
    target: r.targetId === null ? r.targetType : `${r.targetType} (${r.targetId})`,
    outcome: r.outcome,
    outcomeTone: outcomeChipTone(r.outcome),
    source: r.sourceApp,
    details: r.detailsJson
  }));
}
