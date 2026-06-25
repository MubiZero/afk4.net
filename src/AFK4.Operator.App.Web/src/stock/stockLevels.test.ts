import { describe, it, expect } from 'bun:test';
import { stockStatus, marginPercent, stockValueMinorUnits, summarize, type StockItem } from './stockLevels';

const item = (over: Partial<StockItem>): StockItem => ({
  productId: 'p', name: 'X', sku: 'X', category: '', stockOnHand: 10,
  reorderThreshold: 5, avgCostMinorUnits: 400, priceMinorUnits: 1000, ...over
});

describe('stockStatus', () => {
  it('out при нулевом остатке', () => expect(stockStatus(item({ stockOnHand: 0 }))).toBe('out'));
  it('low при остатке <= порога', () => expect(stockStatus(item({ stockOnHand: 5, reorderThreshold: 6 }))).toBe('low'));
  it('ok выше порога', () => expect(stockStatus(item({ stockOnHand: 12, reorderThreshold: 6 }))).toBe('ok'));
  it('порог 0 = без low (только out)', () => expect(stockStatus(item({ stockOnHand: 1, reorderThreshold: 0 }))).toBe('ok'));
});

describe('marginPercent', () => {
  it('60% при 400/1000', () => expect(marginPercent(1000, 400)).toBe(60));
  it('null при нулевой цене', () => expect(marginPercent(0, 400)).toBeNull());
});

describe('stockValueMinorUnits', () => {
  it('остаток × средняя', () => expect(stockValueMinorUnits(item({ stockOnHand: 12, avgCostMinorUnits: 400 }))).toBe(4800));
});

describe('summarize', () => {
  it('считает low/out и стоимость', () => {
    const r = summarize([
      item({ stockOnHand: 0, reorderThreshold: 5, avgCostMinorUnits: 500 }),
      item({ stockOnHand: 2, reorderThreshold: 6, avgCostMinorUnits: 600 }),
      item({ stockOnHand: 12, reorderThreshold: 6, avgCostMinorUnits: 400 })
    ]);
    expect(r.outCount).toBe(1);
    expect(r.lowCount).toBe(1);
    expect(r.totalValueMinorUnits).toBe(0 * 500 + 2 * 600 + 12 * 400);
  });
});
