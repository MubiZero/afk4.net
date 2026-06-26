import { describe, it, expect } from 'bun:test';
import {
  mapMovementsToRows, filterByType, filterByPeriod, groupByDay, summarize, buildCsv,
  type JournalRow,
} from './journalModel';

const catalog = [
  { productId: 'p1', name: 'Cola 0.5', sku: 'COLA-05' },
  { productId: 'p2', name: 'Чипсы Lays', sku: 'CHIPS-LAYS' },
] as never[];

const mv = (over: Record<string, unknown>) => ({
  stockMovementId: `m-${over.reason ?? Math.abs(Number(over.quantityDelta) || 0)}`,
  productId: 'p1', movementType: 'purchase', quantityDelta: 10,
  unitCost: { currencyCode: 'TJS', minorUnits: 400 }, reason: 'Приёмка',
  createdByStaffUserId: 's1', createdByDisplayName: 'Олег С.', createdAtUtc: '2026-06-25T10:00:00Z',
  ...over,
}) as never;

describe('journalModel', () => {
  it('mapMovementsToRows резолвит имя/sku из каталога, считает сумму = |кол-во|×себест', () => {
    const rows = mapMovementsToRows([mv({ quantityDelta: -3, unitCost: { currencyCode: 'TJS', minorUnits: 500 } })], catalog);
    expect(rows[0]).toMatchObject({ name: 'Cola 0.5', sku: 'COLA-05', quantityDelta: -3, unitCostMinorUnits: 500, sumMinorUnits: 1500, who: 'Олег С.' });
  });

  it('mapMovementsToRows: товар вне каталога → name=fallback (productId), sku пустой', () => {
    const rows = mapMovementsToRows([mv({ productId: 'gone' })], catalog);
    expect(rows[0].name).toBe('gone');
    expect(rows[0].sku).toBe('');
  });

  it('filterByType оставляет только нужный movementType', () => {
    const rows = mapMovementsToRows([mv({ movementType: 'purchase' }), mv({ movementType: 'sale', quantityDelta: -1 })], catalog);
    expect(filterByType(rows, 'all')).toHaveLength(2);
    expect(filterByType(rows, 'sale').every((r) => r.type === 'sale')).toBe(true);
    expect(filterByType(rows, 'sale')).toHaveLength(1);
  });

  it('filterByPeriod today/week по UTC относительно nowMs', () => {
    const now = Date.parse('2026-06-25T12:00:00Z');
    const rows = mapMovementsToRows([
      mv({ reason: 'a', createdAtUtc: '2026-06-25T08:00:00Z' }), // сегодня
      mv({ reason: 'b', createdAtUtc: '2026-06-22T08:00:00Z' }), // 3 дня назад
      mv({ reason: 'c', createdAtUtc: '2026-06-10T08:00:00Z' }), // >7 дней
    ], catalog);
    expect(filterByPeriod(rows, 'today', now).map((r) => r.reason)).toEqual(['a']);
    expect(filterByPeriod(rows, 'week', now).map((r) => r.reason).sort()).toEqual(['a', 'b']);
    expect(filterByPeriod(rows, 'all', now)).toHaveLength(3);
  });

  it('groupByDay группирует по UTC-дню, новые дни первыми', () => {
    const rows = mapMovementsToRows([
      mv({ reason: 'a', createdAtUtc: '2026-06-25T08:00:00Z' }),
      mv({ reason: 'b', createdAtUtc: '2026-06-24T08:00:00Z' }),
      mv({ reason: 'c', createdAtUtc: '2026-06-25T20:00:00Z' }),
    ], catalog);
    const groups = groupByDay(rows);
    expect(groups[0].dayKey).toBe('2026-06-25');
    expect(groups[0].rows).toHaveLength(2);
    expect(groups[1].dayKey).toBe('2026-06-24');
  });

  it('summarize считает приход/продажи/списания/чистое движение', () => {
    const rows = mapMovementsToRows([
      mv({ movementType: 'purchase', quantityDelta: 12, unitCost: { currencyCode: 'TJS', minorUnits: 900 } }),
      mv({ movementType: 'sale', quantityDelta: -5 }),
      mv({ movementType: 'adjustment', quantityDelta: -3, unitCost: { currencyCode: 'TJS', minorUnits: 600 } }),
    ], catalog);
    const s = summarize(rows);
    expect(s.inboundQty).toBe(12);
    expect(s.inboundSumMinor).toBe(12 * 900);
    expect(s.soldQty).toBe(5);
    expect(s.writtenOffQty).toBe(3);
    expect(s.writtenOffSumMinor).toBe(3 * 600);
    expect(s.netQty).toBe(12 - 5 - 3);
  });

  it('buildCsv: заголовок + строки, экранирование запятых/кавычек', () => {
    const rows = mapMovementsToRows([mv({ quantityDelta: -2, reason: 'брак, упаковки', unitCost: { currencyCode: 'TJS', minorUnits: 600 } })], catalog);
    const csv = buildCsv(rows, {
      headers: ['Дата', 'Тип', 'Товар', 'SKU', 'Кол-во', 'Себест', 'Сумма', 'Причина', 'Кто'],
      typeLabel: (type) => type,
      formatMoney: (minor) => (minor / 100).toFixed(2),
      formatDateTime: (iso) => iso,
    });
    const lines = csv.trim().split('\n');
    expect(lines[0]).toContain('Дата');
    // запятая в причине → поле в кавычках
    expect(lines[1]).toContain('"брак, упаковки"');
    expect(lines[1]).toContain('-2');
  });
});
