import { readString, readNumber, readBoolean, readMoney } from '../operatorHelpers';
import type { PosProductDto } from '../api/clients/pos';
import type { PosCategoryDirectory } from '../posCategoryDirectory';

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

// Справочник необязателен: сводке в шапке имена категорий не нужны, и лишний запрос ради них
// был бы платой ни за что. Ленте они нужны — она его и передаёт.
export function mapCatalogToStock(catalog: PosProductDto[], categories?: PosCategoryDirectory): StockItem[] {
  return catalog
    .filter((product) => readBoolean(product, 'trackStock'))
    .map((product) => ({
      productId: readString(product, 'productId'),
      name: readString(product, 'name'),
      sku: readString(product, 'sku', ''),
      category: categories?.get(readString(product, 'categoryId'))?.name ?? '',
      stockOnHand: readNumber(product, 'stockOnHand', 0),
      reorderThreshold: readNumber(product, 'reorderThreshold', 0),
      avgCostMinorUnits: readNumber(product, 'avgCostMinorUnits', 0),
      priceMinorUnits: readMoney(product, 'price')?.minorUnits ?? 0,
    }));
}

// Стоимость склада неизвестна (`null`), пока ни у одного товара на полке не заведена
// себестоимость: «0 с.» читалось как «склад ничего не стоит». Себестоимость есть лишь у части —
// сумма по тем, у кого она есть.
// ponytail: частичная сумма не помечена как частичная; пометка — когда себестоимость станет обязательной не везде.
export function summarize(items: StockItem[]): {
  totalValueMinorUnits: number | null;
  lowCount: number;
  outCount: number;
} {
  let total = 0;
  let costKnown = false;
  let low = 0;
  let out = 0;
  for (const it of items) {
    total += stockValueMinorUnits(it);
    if (it.stockOnHand > 0 && it.avgCostMinorUnits > 0) costKnown = true;
    const status = stockStatus(it);
    if (status === 'out') out += 1;
    else if (status === 'low') low += 1;
  }
  return { totalValueMinorUnits: costKnown ? total : null, lowCount: low, outCount: out };
}
