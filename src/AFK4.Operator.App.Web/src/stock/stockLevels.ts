import { readString, readNumber, readBoolean } from '../operatorHelpers';
import type { PosProductDto } from '../api/clients/pos';

export interface StockItem {
  productId: string;
  name: string;
  sku: string;
  category: string;
  stockOnHand: number;
  reorderThreshold: number;
  avgCostMinorUnits: number;
  priceMinorUnits: number;
}

export type StockStatus = 'ok' | 'low' | 'out';

export function stockStatus(item: StockItem): StockStatus {
  if (item.stockOnHand <= 0) return 'out';
  if (item.reorderThreshold > 0 && item.stockOnHand <= item.reorderThreshold) return 'low';
  return 'ok';
}

export function marginPercent(priceMinorUnits: number, avgCostMinorUnits: number): number | null {
  if (priceMinorUnits <= 0) return null;
  return Math.round(((priceMinorUnits - avgCostMinorUnits) / priceMinorUnits) * 100);
}

export function stockValueMinorUnits(item: StockItem): number {
  return Math.max(item.stockOnHand, 0) * item.avgCostMinorUnits;
}

export function mapCatalogToStock(catalog: PosProductDto[]): StockItem[] {
  return catalog
    .filter((product) => readBoolean(product, 'trackStock'))
    .map((product) => ({
      productId: readString(product, 'productId'),
      name: readString(product, 'name'),
      sku: readString(product, 'sku', ''),
      category: readString(product, 'categoryName', ''),
      stockOnHand: readNumber(product, 'stockOnHand', 0),
      reorderThreshold: readNumber(product, 'reorderThreshold', 0),
      avgCostMinorUnits: readNumber(product, 'avgCostMinorUnits', 0),
      priceMinorUnits: readNumber(product, 'priceMinorUnits', 0),
    }));
}

export function summarize(items: StockItem[]): {
  totalValueMinorUnits: number;
  lowCount: number;
  outCount: number;
} {
  let total = 0;
  let low = 0;
  let out = 0;
  for (const it of items) {
    total += stockValueMinorUnits(it);
    const status = stockStatus(it);
    if (status === 'out') out += 1;
    else if (status === 'low') low += 1;
  }
  return { totalValueMinorUnits: total, lowCount: low, outCount: out };
}
