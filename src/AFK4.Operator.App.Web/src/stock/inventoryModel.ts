import type { PosProductDto } from '../operatorApiClients';
import { readString, readNumber, readBoolean, readArray } from '../operatorHelpers';

// Строка пересчёта: один отслеживаемый товар. countedText — сырой ввод факта (пусто = не пересчитано),
// fresh — только что отсканировано (для подсветки/фокуса).
export interface CountLine {
  productId: string;
  name: string;
  sku: string;
  barcodes: string[];
  systemQty: number;          // учётный остаток на момент загрузки
  avgCostMinorUnits: number;  // средняя себестоимость — для суммы расхождения
  countedText: string;
  fresh: boolean;
}

const NON_NEGATIVE_INT = /^\d+$/;

// Строка факта → целое >= 0, либо null (пусто/невалид/отрицат).
export function parseCounted(text: string): number | null {
  const trimmed = text.trim();
  if (!NON_NEGATIVE_INT.test(trimmed)) return null;
  const value = Number.parseInt(trimmed, 10);
  return Number.isFinite(value) && value >= 0 ? value : null;
}

export function lineCounted(line: CountLine): number | null {
  return parseCounted(line.countedText);
}

// Расхождение факт − учёт; null пока не пересчитано.
export function lineDiff(line: CountLine): number | null {
  const counted = lineCounted(line);
  return counted === null ? null : counted - line.systemQty;
}

// Знаковая сумма расхождения по средней себестоимости; 0 если не пересчитано / нет расхождения.
export function lineDiffSumMinorUnits(line: CountLine): number {
  const diff = lineDiff(line);
  if (diff === null) return 0;
  const result = diff * Math.max(line.avgCostMinorUnits, 0);
  return result === 0 ? 0 : result;
}

export function mapCatalogToCountLines(catalog: PosProductDto[]): CountLine[] {
  return catalog
    .filter((product) => readBoolean(product, 'trackStock'))
    .map((product) => ({
      productId: readString(product, 'productId'),
      name: readString(product, 'name'),
      sku: readString(product, 'sku', ''),
      barcodes: readArray<string>(product, 'barcodes'),
      systemQty: readNumber(product, 'stockOnHand', 0),
      avgCostMinorUnits: readNumber(product, 'avgCostMinorUnits', 0),
      countedText: '',
      fresh: false,
    }));
}

function withFresh(lines: CountLine[], productId: string, patch: (line: CountLine) => CountLine): CountLine[] {
  return lines.map((line) =>
    line.productId === productId ? patch({ ...line, fresh: true })
      : line.fresh ? { ...line, fresh: false } : line
  );
}

// Записать факт для товара (помечает строку fresh, снимает fresh с прочих).
export function setCounted(lines: CountLine[], productId: string, countedText: string): CountLine[] {
  return withFresh(lines, productId, (line) => ({ ...line, countedText }));
}

// Подсветить только что отсканированную строку без изменения факта.
export function markFresh(lines: CountLine[], productId: string): CountLine[] {
  return withFresh(lines, productId, (line) => line);
}

// Сбросить весь пересчёт.
export function resetCounts(lines: CountLine[]): CountLine[] {
  return lines.map((line) => ({ ...line, countedText: '', fresh: false }));
}

// После успешного проведения строки: учётный := факт (расхождение → 0), чтобы ретрай при
// частичном сбое не провёл её повторно. Если факт невалиден — не трогаем.
export function markPosted(lines: CountLine[], productId: string): CountLine[] {
  return lines.map((line) => {
    if (line.productId !== productId) return line;
    const counted = lineCounted(line);
    return counted === null ? line : { ...line, systemQty: counted };
  });
}

export interface InventoryTotals {
  trackedCount: number;
  countedCount: number;
  discrepancies: number;
  shortageUnits: number;             // суммарная недостача в единицах (положительное)
  shortageSumMinorUnits: number;     // положительное
  surplusUnits: number;
  surplusSumMinorUnits: number;
  netSumMinorUnits: number;          // знаковый итог: излишек +, недостача −
}

export function inventoryTotals(lines: CountLine[]): InventoryTotals {
  let countedCount = 0, discrepancies = 0;
  let shortageUnits = 0, shortageSumMinorUnits = 0;
  let surplusUnits = 0, surplusSumMinorUnits = 0, netSumMinorUnits = 0;
  for (const line of lines) {
    const diff = lineDiff(line);
    if (diff === null) continue;
    countedCount += 1;
    if (diff === 0) continue;
    discrepancies += 1;
    const sum = lineDiffSumMinorUnits(line);
    netSumMinorUnits += sum;
    if (diff < 0) { shortageUnits += -diff; shortageSumMinorUnits += -sum; }
    else { surplusUnits += diff; surplusSumMinorUnits += sum; }
  }
  return {
    trackedCount: lines.length, countedCount, discrepancies,
    shortageUnits, shortageSumMinorUnits, surplusUnits, surplusSumMinorUnits, netSumMinorUnits,
  };
}

export interface InventoryAdjustment {
  productId: string;
  quantityDelta: number;        // знаковая, != 0
  unitCostMinorUnits: number;   // из средней себестоимости (>= 0)
}

// Корректировки к проведению: только пересчитанные строки с расхождением != 0.
export function inventoryAdjustments(lines: CountLine[]): InventoryAdjustment[] {
  const out: InventoryAdjustment[] = [];
  for (const line of lines) {
    const diff = lineDiff(line);
    if (diff === null || diff === 0) continue;
    out.push({ productId: line.productId, quantityDelta: diff, unitCostMinorUnits: Math.max(line.avgCostMinorUnits, 0) });
  }
  return out;
}
