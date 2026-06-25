import { describe, it, expect } from 'bun:test';
import { stockStatus, marginPercent, stockValueMinorUnits, summarize, mapCatalogToStock, type StockItem } from './stockLevels';

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

describe('mapCatalogToStock', () => {
  it('читает цену из nested MoneyDto, а avgCost — из плоского числа', () => {
    const catalog = [
      {
        productId: 'p1',
        name: 'Кола',
        sku: 'COLA',
        categoryName: 'Напитки',
        trackStock: true,
        stockOnHand: 5,
        reorderThreshold: 2,
        avgCostMinorUnits: 500,
        price: { currencyCode: 'TJS', minorUnits: 1000 },
      },
    ];
    const [mapped] = mapCatalogToStock(catalog as never);
    expect(mapped.priceMinorUnits).toBe(1000);
    expect(mapped.avgCostMinorUnits).toBe(500);
  });

  it('фильтрует товары без trackStock', () => {
    const catalog = [
      { productId: 'p1', name: 'A', sku: '', trackStock: true, stockOnHand: 0, reorderThreshold: 0, avgCostMinorUnits: 0, price: { currencyCode: 'TJS', minorUnits: 0 } },
      { productId: 'p2', name: 'B', sku: '', trackStock: false, stockOnHand: 10, reorderThreshold: 0, avgCostMinorUnits: 0, price: { currencyCode: 'TJS', minorUnits: 0 } },
    ];
    expect(mapCatalogToStock(catalog as never)).toHaveLength(1);
  });
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
