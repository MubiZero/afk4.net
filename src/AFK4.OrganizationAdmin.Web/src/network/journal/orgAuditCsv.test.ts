import { describe, expect, it } from 'bun:test';
import { toAuditCsv } from './orgAuditCsv';
import type { OrgAuditRecordDto } from '../../api/clients/orgAudit';

function record(overrides: Partial<OrgAuditRecordDto> = {}): OrgAuditRecordDto {
  return {
    auditRecordId: 'a1',
    branchId: 'b1',
    actorStaffUserId: 's1',
    actorPlatformAdminUserId: null,
    action: 'session.start',
    targetType: 'Session',
    targetId: 't1',
    outcome: 'Succeeded',
    sourceApp: 'operator',
    detailsJson: '{}',
    createdAtUtc: '2026-08-10T09:00:00Z',
    ...overrides
  };
}

describe('toAuditCsv', () => {
  it('пишет snake_case-шапку проектного экспортёра и CRLF', () => {
    const csv = toAuditCsv([record()]);
    const [header, row] = csv.split('\r\n');
    expect(header).toBe(
      'audit_record_id,created_at_utc,branch_id,actor_staff_user_id,actor_platform_admin_user_id,action,target_type,target_id,outcome,source_app,details_json'
    );
    expect(row).toBe('a1,2026-08-10T09:00:00Z,b1,s1,,session.start,Session,t1,Succeeded,operator,{}');
    expect(csv.endsWith('\r\n')).toBe(true);
  });

  it('отдаёт шапку даже без записей — пустой файл поддержке ничего не говорит', () => {
    expect(toAuditCsv([])).toBe(
      'audit_record_id,created_at_utc,branch_id,actor_staff_user_id,actor_platform_admin_user_id,action,target_type,target_id,outcome,source_app,details_json\r\n'
    );
  });

  it('экранирует запятые, кавычки и переводы строк удвоением кавычек', () => {
    const csv = toAuditCsv([record({ detailsJson: '{"reason":"нет денег, совсем"}' })]);
    expect(csv).toContain('"{""reason"":""нет денег, совсем""}"');
  });

  it('null-поля становятся пустыми ячейками, а не строкой null', () => {
    const csv = toAuditCsv([record({ branchId: null, actorStaffUserId: null, targetId: null })]);
    expect(csv.split('\r\n')[1]).toBe('a1,2026-08-10T09:00:00Z,,,,session.start,Session,,Succeeded,operator,{}');
  });

  it('org-level запись (branchId=null) выгружается наравне с филиальной — ради неё экран и существует', () => {
    const csv = toAuditCsv([record({ branchId: null, actorPlatformAdminUserId: 'p1', actorStaffUserId: null })]);
    expect(csv.split('\r\n')[1]).toContain(',,,p1,');
  });
});
