import { describe, it, expect } from 'bun:test';
import {
  parseCounted, lineCounted, lineDiff, lineDiffSumMinorUnits,
  mapCatalogToCountLines, setCounted, markFresh, resetCounts, markPosted,
  inventoryTotals, inventoryAdjustments, type CountLine,
} from './inventoryModel';

const line = (over: Partial<CountLine> = {}): CountLine => ({
  productId: 'p1', name: 'Cola 0.5', sku: 'COLA-05', barcodes: ['111'],
  systemQty: 12, avgCostMinorUnits: 400, countedText: '', fresh: false, ...over,
});

const product = (over: Record<string, unknown> = {}) => ({
  productId: 'p1', name: 'Cola 0.5', sku: 'COLA-05', trackStock: true,
  stockOnHand: 12, avgCostMinorUnits: 400, barcodes: ['111'], ...over,
}) as never;

describe('inventoryModel', () => {
  it('parseCounted: целое >=0; пусто/невалид/отрицат → null', () => {
    expect(parseCounted('0')).toBe(0);
    expect(parseCounted('28')).toBe(28);
    expect(parseCounted(' 7 ')).toBe(7);
    expect(parseCounted('')).toBeNull();
    expect(parseCounted('  ')).toBeNull();
    expect(parseCounted('5.5')).toBeNull();
    expect(parseCounted('-3')).toBeNull();
    expect(parseCounted('abc')).toBeNull();
  });

  it('lineDiff: факт−учёт; пока не пересчитано → null', () => {
    expect(lineDiff(line({ countedText: '' }))).toBeNull();
    expect(lineDiff(line({ countedText: '12' }))).toBe(0);
    expect(lineDiff(line({ countedText: '10' }))).toBe(-2);
    expect(lineDiff(line({ countedText: '15' }))).toBe(3);
    expect(lineDiff(line({ countedText: '0' }))).toBe(-12);
  });

  it('lineCounted достаёт распарсенный факт', () => {
    expect(lineCounted(line({ countedText: '9' }))).toBe(9);
    expect(lineCounted(line({ countedText: '' }))).toBeNull();
  });

  it('lineDiffSumMinorUnits: знаковая сумма по средней себест.; null/0-расхождение → 0', () => {
    expect(lineDiffSumMinorUnits(line({ countedText: '' }))).toBe(0);
    expect(lineDiffSumMinorUnits(line({ countedText: '12' }))).toBe(0);
    expect(lineDiffSumMinorUnits(line({ countedText: '10' }))).toBe(-800); // -2 * 400
    expect(lineDiffSumMinorUnits(line({ countedText: '14' }))).toBe(800);  // +2 * 400
    expect(lineDiffSumMinorUnits(line({ countedText: '10', avgCostMinorUnits: -5 }))).toBe(0); // себест clamp 0
  });

  it('mapCatalogToCountLines: только trackStock, учётный=stockOnHand, факт пуст', () => {
    const lines = mapCatalogToCountLines([
      product(),
      product({ productId: 'p2', name: 'Время', trackStock: false }),
    ]);
    expect(lines).toHaveLength(1);
    expect(lines[0]).toMatchObject({ productId: 'p1', name: 'Cola 0.5', sku: 'COLA-05', systemQty: 12, avgCostMinorUnits: 400, countedText: '', fresh: false });
    expect(lines[0].barcodes).toEqual(['111']);
  });

  it('setCounted кладёт факт для товара, помечает fresh, снимает fresh с прочих', () => {
    const lines = [line({ productId: 'a', fresh: true }), line({ productId: 'b' })];
    const next = setCounted(lines, 'b', '5');
    expect(next.find((l) => l.productId === 'b')).toMatchObject({ countedText: '5', fresh: true });
    expect(next.find((l) => l.productId === 'a')!.fresh).toBe(false);
  });

  it('markFresh подсвечивает строку без изменения факта', () => {
    const lines = [line({ productId: 'a', fresh: true }), line({ productId: 'b', countedText: '3' })];
    const next = markFresh(lines, 'b');
    expect(next.find((l) => l.productId === 'b')).toMatchObject({ fresh: true, countedText: '3' });
    expect(next.find((l) => l.productId === 'a')!.fresh).toBe(false);
  });

  it('resetCounts очищает факт и подсветку всех строк', () => {
    const lines = [line({ countedText: '5', fresh: true }), line({ productId: 'b', countedText: '9' })];
    const next = resetCounts(lines);
    expect(next.every((l) => l.countedText === '' && !l.fresh)).toBe(true);
  });

  it('markPosted: учётный := факт (расхождение становится 0 — анти-двойное-проведение при ретрае)', () => {
    const lines = [line({ productId: 'a', countedText: '10' }), line({ productId: 'b', countedText: '9' })];
    const next = markPosted(lines, 'a');
    const a = next.find((l) => l.productId === 'a')!;
    expect(a.systemQty).toBe(10);
    expect(lineDiff(a)).toBe(0);
    expect(next.find((l) => l.productId === 'b')!.systemQty).toBe(12); // прочие не тронуты
  });

  it('inventoryTotals: пересчитано/расхождения/недостача/излишек/итог', () => {
    const lines = [
      line({ productId: 'a', systemQty: 12, countedText: '12' }), // совпало
      line({ productId: 'b', systemQty: 30, avgCostMinorUnits: 200, countedText: '28' }), // -2, -400
      line({ productId: 'c', systemQty: 6, avgCostMinorUnits: 700, countedText: '7' }),    // +1, +700
      line({ productId: 'd', systemQty: 2, countedText: '' }),     // не пересчитано
    ];
    const t = inventoryTotals(lines);
    expect(t.trackedCount).toBe(4);
    expect(t.countedCount).toBe(3);
    expect(t.discrepancies).toBe(2);
    expect(t.shortageUnits).toBe(2);
    expect(t.shortageSumMinorUnits).toBe(400);
    expect(t.surplusUnits).toBe(1);
    expect(t.surplusSumMinorUnits).toBe(700);
    expect(t.netSumMinorUnits).toBe(300); // -400 + 700
  });

  it('inventoryAdjustments: только пересчитанные с расхождением != 0; знаковая дельта, себест из avg', () => {
    const lines = [
      line({ productId: 'a', systemQty: 12, countedText: '12' }), // 0 → пропуск
      line({ productId: 'b', systemQty: 30, avgCostMinorUnits: 200, countedText: '28' }), // -2
      line({ productId: 'c', systemQty: 6, avgCostMinorUnits: 700, countedText: '7' }),    // +1
      line({ productId: 'd', systemQty: 2, countedText: '' }),     // null → пропуск
    ];
    expect(inventoryAdjustments(lines)).toEqual([
      { productId: 'b', quantityDelta: -2, unitCostMinorUnits: 200 },
      { productId: 'c', quantityDelta: 1, unitCostMinorUnits: 700 },
    ]);
  });

  it('масштаб: учётный 0 + факт 0 → нет расхождения; юникод-имя проходит', () => {
    expect(lineDiff(line({ systemQty: 0, countedText: '0' }))).toBe(0);
    const lines = mapCatalogToCountLines([product({ name: 'Шоколад «Алёнка» 🍫', stockOnHand: 0, avgCostMinorUnits: 0 })]);
    expect(lines[0].name).toBe('Шоколад «Алёнка» 🍫');
    expect(inventoryAdjustments(setCounted(lines, 'p1', '0'))).toHaveLength(0);
  });
});
