import { describe, it, expect } from 'bun:test';
import { createTranslator } from '@afk4/i18n';
import { journalDate, toAuditRows, outcomeChipTone } from './orgAuditModel';

const t = createTranslator('ru');
const fmt = { formatDate: (s: string) => s, t };

const rec = {
  auditRecordId: 'r1', organizationId: 'o', organizationName: null, amountMinorUnits: null, actorDisplayName: null,
  branchId: null, actorStaffUserId: null, actorPlatformAdminUserId: null,
  action: 'news.create', targetType: 'NewsItem', targetId: 'n1', outcome: 'Succeeded',
  sourceApp: 'PlatformApi', detailsJson: '{"x":1}', createdAtUtc: '2026-07-20T10:00:00Z'
};

describe('orgAuditModel', () => {
  // Журнал раньше показывал «cash.shift.opened», «shift (3f2a…)», «success», «PlatformApi» и строку
  // JSON: всё верно, но читать это мог только разработчик.
  it('says in words what was done, to what, with what outcome and from where', () => {
    const [row] = toAuditRows([rec], fmt, 'система');
    expect(row.actor).toBe('система');
    expect(row.action).toBe('Новость создана');
    expect(row.actionIsCode).toBe(false);
    expect(row.target).toBe('Новость');
    expect(row.outcome).toBe('Успешно');
    expect(row.outcomeTone).toBe('is-live');
    expect(row.source).toBe('Сервер');
  });

  // Незнакомый словарю код не получает выдуманного названия — он идёт как есть и помечен кодом.
  it('keeps an unknown action and target as codes', () => {
    const [row] = toAuditRows([{ ...rec, action: 'something.new', targetType: 'Gizmo' }], fmt, 'система');
    expect(row.action).toBe('something.new');
    expect(row.actionIsCode).toBe(true);
    expect(row.target).toBe('Gizmo');
    expect(row.targetIsCode).toBe(true);
  });

  // Идентификатор объекта — не в колонке «Объект», а первой строкой подробностей, рядом с JSON.
  it('moves the target id and the JSON into readable details', () => {
    const [row] = toAuditRows([rec], fmt, 'система');
    expect(row.details).toBe('n1\n{\n  "x": 1\n}');
  });

  it('has no details for an empty record', () => {
    const [row] = toAuditRows([{ ...rec, targetId: null, detailsJson: '{}' }], fmt, 'система');
    expect(row.details).toBeNull();
  });

  // targetId приходил undefined (ключ отсутствовал в ответе), а сравнение было строго с null —
  // строка выходила «shift (undefined)».
  it('survives a missing targetId, not just null', () => {
    const [row] = toAuditRows([{ ...rec, targetType: 'Shift', targetId: undefined, detailsJson: '' } as never], fmt, 'система');
    expect(row.target).toBe('Смена');
    expect(row.details).toBeNull();
  });

  // Tones use the real .ui-chip--status modifiers (is-live/is-neutral/is-warning/is-danger).
  it('marks denied outcome as danger', () => {
    expect(outcomeChipTone('Denied')).toBe('is-danger');
  });

  it('falls back to neutral for unknown outcomes', () => {
    expect(outcomeChipTone('Pending')).toBe('is-neutral');
  });

  // Журнал открывают, чтобы ответить «кто трогал подписку», — столбец GUID на это не отвечал.
  it('names the actor the server resolved', () => {
    const rows = toAuditRows([{ ...rec, actorStaffUserId: 'staff-1', actorDisplayName: 'Фаррух' }], fmt, 'система');
    expect(rows[0].actor).toBe('Фаррух');
  });

  it('falls back to the start of the id when the name is gone', () => {
    const rows = toAuditRows([{ ...rec, actorStaffUserId: '3fa85f64-5717-4562-b3fc-2c963f66afa6' }], fmt, 'система');
    expect(rows[0].actor).toBe('3fa85f64');
  });

  // Год в каждой строке журнала за неделю — шум; он нужен, только когда запись не этого года.
  it('shows the year only when the record is not from this year', () => {
    const now = new Date('2026-09-29T12:00:00Z');
    expect(journalDate('2026-09-20T10:00:00Z', 'ru', now)).not.toContain('2026');
    expect(journalDate('2025-12-31T10:00:00Z', 'ru', now)).toContain('2025');
  });
});
