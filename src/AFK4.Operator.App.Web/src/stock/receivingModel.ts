import type { PosProductDto } from '../operatorApiClients';
import { formatMoneyInputMinorUnits, parseNonNegativeMoneyInputMinorUnits, readNumber, readString } from '../operatorHelpers';

// Строка документа прихода. Себестоимость — сырой текст (свободный ввод без переформатирования
// на keystroke; minor-значение парсится по требованию). fresh — только что добавлена/инкрементирована.
export interface ReceiptLine {
  productId: string;
  name: string;
  sku: string;
  quantity: number;       // > 0
  unitCostText: string;   // редактируемый текст, парсится в minor units по требованию
  fresh: boolean;
}

// Преподстановка себестоимости = средневзвешенная закупочная товара (S0, плоское число) как текст.
export function prefillUnitCostText(product: PosProductDto): string {
  return formatMoneyInputMinorUnits(Math.max(readNumber(product, 'avgCostMinorUnits', 0), 0));
}

// Себестоимость строки в minor units (невалидный/пустой текст → 0).
export function lineUnitCostMinorUnits(line: ReceiptLine): number {
  return Math.max(parseNonNegativeMoneyInputMinorUnits(line.unitCostText) ?? 0, 0);
}

// Добавить товар в накладную: уже есть строкой → +1 к количеству (накопление, как сканер),
// иначе новая строка qty=1 с преподставленной себестоимостью. Возвращает НОВЫЙ массив.
export function addOrAccumulate(lines: ReceiptLine[], product: PosProductDto): ReceiptLine[] {
  const productId = readString(product, 'productId');
  if (lines.some((line) => line.productId === productId)) {
    return lines.map((line) =>
      line.productId === productId
        ? { ...line, quantity: line.quantity + 1, fresh: true }
        : { ...line, fresh: false });
  }
  const line: ReceiptLine = {
    productId,
    name: readString(product, 'name'),
    sku: readString(product, 'sku'),
    quantity: 1,
    unitCostText: prefillUnitCostText(product),
    fresh: true,
  };
  return [...lines.map((existing) => ({ ...existing, fresh: false })), line];
}

export function setQuantity(lines: ReceiptLine[], productId: string, quantity: number): ReceiptLine[] {
  return lines.map((line) =>
    line.productId === productId ? { ...line, quantity: Math.max(1, Math.trunc(quantity)), fresh: false } : line);
}

export function setUnitCostText(lines: ReceiptLine[], productId: string, text: string): ReceiptLine[] {
  return lines.map((line) =>
    line.productId === productId ? { ...line, unitCostText: text, fresh: false } : line);
}

export function removeLine(lines: ReceiptLine[], productId: string): ReceiptLine[] {
  return lines.filter((line) => line.productId !== productId);
}

export function lineSubtotalMinorUnits(line: ReceiptLine): number {
  return line.quantity * lineUnitCostMinorUnits(line);
}

export interface ReceiptTotals {
  positions: number;
  units: number;
  sumMinorUnits: number;
}

export function receiptTotals(lines: ReceiptLine[]): ReceiptTotals {
  return {
    positions: lines.length,
    units: lines.reduce((acc, line) => acc + line.quantity, 0),
    sumMinorUnits: lines.reduce((acc, line) => acc + lineSubtotalMinorUnits(line), 0),
  };
}

// «Накладная» (поставщик/№) кодируется в Reason движения — сущности поставщика нет (YAGNI).
export function receiptReason(baseLabel: string, supplier: string, invoiceNo: string): string {
  const parts = [baseLabel];
  const trimmedSupplier = supplier.trim();
  const trimmedInvoice = invoiceNo.trim();
  if (trimmedSupplier) parts.push(trimmedSupplier);
  if (trimmedInvoice) parts.push(`№${trimmedInvoice}`);
  return parts.join(' · ');
}
