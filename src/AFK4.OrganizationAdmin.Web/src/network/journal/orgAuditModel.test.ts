import { describe, it, expect } from 'bun:test';
import { toAuditRows, outcomeChipTone } from './orgAuditModel';

const rec = {
  auditRecordId: 'r1', organizationId: 'o', organizationName: null, amountMinorUnits: null, actorDisplayName: null,
  branchId: null, actorStaffUserId: null, actorPlatformAdminUserId: null,
  action: 'news.published', targetType: 'News', targetId: 'n1', outcome: 'Succeeded',
  sourceApp: 'PlatformApi', detailsJson: '{"x":1}', createdAtUtc: '2026-07-20T10:00:00Z'
};

describe('orgAuditModel', () => {
  it('maps records to rows with a system actor fallback', () => {
    const rows = toAuditRows([rec], { formatDate: (s) => s }, 'система');
    expect(rows[0].actor).toBe('система');
    expect(rows[0].target).toBe('News (n1)');
    expect(rows[0].outcomeTone).toBe('is-live');
  });

  // Tones use the real .ui-chip--status modifiers from 02-ui-kit.css (is-live/is-neutral/
  // is-warning/is-danger) — same vocabulary as billingModel.ts/stock/journalModel.ts, not the
  // ok/danger/muted placeholder names from the original brief.
  it('marks denied outcome as danger', () => {
    expect(outcomeChipTone('Denied')).toBe('is-danger');
  });

  it('falls back to neutral for unknown outcomes', () => {
    expect(outcomeChipTone('Pending')).toBe('is-neutral');
  });

  it('renders the target type alone when there is no targetId', () => {
    const rows = toAuditRows([{ ...rec, targetType: 'Organization', targetId: null }], { formatDate: (s) => s }, 'система');
    expect(rows[0].target).toBe('Organization');
  });

  // targetId приходил undefined (ключ отсутствовал в ответе), а сравнение было строго с null —
  // строка выходила «shift (undefined)» вместо простого «shift».
  it('renders the target type alone when targetId is missing, not just null', () => {
    const rows = toAuditRows([{ ...rec, targetType: 'shift', targetId: undefined } as never], { formatDate: (s) => s }, 'система');
    expect(rows[0].target).toBe('shift');
  });

  // Журнал открывают, чтобы ответить «кто трогал подписку», — столбец GUID на это не отвечал.
  it('names the actor the server resolved', () => {
    const rows = toAuditRows([{ ...rec, actorStaffUserId: 'staff-1', actorDisplayName: 'Фаррух' }], { formatDate: (s) => s }, 'система');
    expect(rows[0].actor).toBe('Фаррух');
  });

  it('falls back to the start of the id when the name is gone', () => {
    const rows = toAuditRows([{ ...rec, actorStaffUserId: '3fa85f64-5717-4562-b3fc-2c963f66afa6' }], { formatDate: (s) => s }, 'система');
    expect(rows[0].actor).toBe('3fa85f64');
  });
});
