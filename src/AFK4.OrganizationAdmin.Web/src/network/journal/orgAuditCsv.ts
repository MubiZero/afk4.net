import type { OrgAuditRecordDto } from '../../api/clients/orgAudit';

// Выгрузка журнала для поддержки. Формат — как у серверного ReportCsvExporter (snake_case-шапка,
// сырые значения, CRLF, кавычки удвоением): файл читает инженер поддержки, а не оператор, поэтому
// шапка НЕ локализуется. Выгружаются сырые поля DTO, а не отображаемые строки таблицы: branchId и
// идентификаторы актора на экране свёрнуты, а поддержке именно они и нужны.
const HEADER = [
  'audit_record_id',
  'created_at_utc',
  'branch_id',
  'actor_staff_user_id',
  'actor_platform_admin_user_id',
  'action',
  'target_type',
  'target_id',
  'outcome',
  'source_app',
  'details_json'
] as const;

const NEW_LINE = '\r\n';

function escapeField(value: string | null): string {
  if (value === null) return '';
  if (!/[",\r\n]/.test(value)) return value;
  return `"${value.replaceAll('"', '""')}"`;
}

export function toAuditCsv(records: readonly OrgAuditRecordDto[]): string {
  const lines = [HEADER.join(',')];
  for (const r of records) {
    lines.push(
      [
        r.auditRecordId,
        r.createdAtUtc,
        r.branchId,
        r.actorStaffUserId,
        r.actorPlatformAdminUserId,
        r.action,
        r.targetType,
        r.targetId,
        r.outcome,
        r.sourceApp,
        r.detailsJson
      ]
        .map(escapeField)
        .join(',')
    );
  }
  return `${lines.join(NEW_LINE)}${NEW_LINE}`;
}
