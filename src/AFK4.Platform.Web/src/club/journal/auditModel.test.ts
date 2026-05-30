import { it, expect } from 'vitest';
import type { AuditSearchResult } from '@/api/types';
import { toAuditRows, outcomeBadgeVariant } from './auditModel';

const result: AuditSearchResult = {
  limit: 100,
  records: [
    {
      auditRecordId: 'a1', organizationId: 'o', branchId: 'b', actorStaffUserId: 'staff-1',
      action: 'session.start', targetType: 'Session', targetId: 'sess-9', outcome: 'Succeeded',
      sourceApp: 'operator', detailsJson: '{"k":1}', createdAtUtc: '2026-05-30T10:00:00.000Z',
      actorPlatformAdminUserId: null
    },
    {
      auditRecordId: 'a2', organizationId: 'o', branchId: null, actorStaffUserId: null,
      action: 'login', targetType: 'Staff', targetId: null, outcome: 'Denied',
      sourceApp: 'web', detailsJson: '{}', createdAtUtc: '2026-05-30T11:00:00.000Z',
      actorPlatformAdminUserId: null
    }
  ]
};

it('builds rows with resolved actor and target', () => {
  const rows = toAuditRows(result, { formatDate: iso => iso.slice(0, 10) }, 'Система');
  expect(rows[0].actor).toBe('staff-1');
  expect(rows[0].target).toBe('Session (sess-9)');
  expect(rows[0].date).toBe('2026-05-30');
  expect(rows[1].actor).toBe('Система');
  expect(rows[1].target).toBe('Staff');
});

it('maps outcomes to badge variants', () => {
  expect(outcomeBadgeVariant('Succeeded')).toBe('secondary');
  expect(outcomeBadgeVariant('Denied')).toBe('destructive');
  expect(outcomeBadgeVariant('Other')).toBe('outline');
});
