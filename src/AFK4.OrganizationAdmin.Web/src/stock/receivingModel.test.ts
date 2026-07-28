import { describe, it, expect } from 'bun:test';
import {
  addOrAccumulate, setQuantity, setUnitCostText, removeLine,
  lineSubtotalMinorUnits, lineUnitCostMinorUnits, receiptTotals, receiptReason, prefillUnitCostText,
  type ReceiptLine,
} from './receivingModel';

const product = (over: Record<string, unknown> = {}) => ({
  productId: 'p1', name: 'Cola 0.5', sku: 'COLA-05', avgCostMinorUnits: 400, ...over,
}) as never;

describe('receivingModel', () => {
  it('prefillUnitCostText форматирует avgCost, не ниже 0', () => {
    expect(prefillUnitCostText(product())).toBe('4.00');
    expect(prefillUnitCostText(product({ avgCostMinorUnits: -5 }))).toBe('0.00');
    expect(prefillUnitCostText(product({ avgCostMinorUnits: undefined }))).toBe('0.00');
  });

  it('lineUnitCostMinorUnits парсит текст, невалидный/пустой → 0', () => {
    expect(lineUnitCostMinorUnits({ unitCostText: '4.00' } as ReceiptLine)).toBe(400);
    expect(lineUnitCostMinorUnits({ unitCostText: '6.' } as ReceiptLine)).toBe(0);
    expect(lineUnitCostMinorUnits({ unitCostText: '' } as ReceiptLine)).toBe(0);
  });

  it('addOrAccumulate: новый товар → строка qty=1 с преподставленной себестоимостью, fresh=true', () => {
    const lines = addOrAccumulate([], product());
    expect(lines).toHaveLength(1);
    expect(lines[0]).toMatchObject({ productId: 'p1', name: 'Cola 0.5', sku: 'COLA-05', quantity: 1, unitCostText: '4.00', fresh: true });
  });

  it('addOrAccumulate: повтор того же товара → +1 к количеству, остальные fresh=false', () => {
    const first = addOrAccumulate([], product({ productId: 'a' }));
    const withSecond = addOrAccumulate(first, product({ productId: 'b', name: 'B' }));
    const accumulated = addOrAccumulate(withSecond, product({ productId: 'a' }));
    const a = accumulated.find((l) => l.productId === 'a')!;
    const b = accumulated.find((l) => l.productId === 'b')!;
    expect(a.quantity).toBe(2);
    expect(a.fresh).toBe(true);
    expect(b.fresh).toBe(false);
  });

  it('setQuantity не опускается ниже 1 и усекает дробь', () => {
    const lines = addOrAccumulate([], product());
    expect(setQuantity(lines, 'p1', 0)[0].quantity).toBe(1);
    expect(setQuantity(lines, 'p1', 7.9)[0].quantity).toBe(7);
  });

  it('setUnitCostText кладёт сырой текст; removeLine убирает строку', () => {
    const lines = addOrAccumulate([], product());
    expect(setUnitCostText(lines, 'p1', '6.5')[0].unitCostText).toBe('6.5');
    expect(removeLine(lines, 'p1')).toHaveLength(0);
  });

  it('lineSubtotalMinorUnits и receiptTotals считают позиции/единицы/сумму', () => {
    const line: ReceiptLine = { productId: 'p1', name: 'X', sku: 'S', quantity: 3, unitCostText: '6.00', fresh: false };
    expect(lineSubtotalMinorUnits(line)).toBe(1800);
    const totals = receiptTotals([line, { ...line, productId: 'p2', quantity: 2, unitCostText: '4.00' }]);
    expect(totals).toEqual({ positions: 2, units: 5, sumMinorUnits: 1800 + 800 });
  });

  it('receiptReason кодирует поставщика/№ накладной, пустые опускает', () => {
    expect(receiptReason('Приёмка', '  ООО Напитки ', ' 42 ')).toBe('Приёмка · ООО Напитки · №42');
    expect(receiptReason('Приёмка', '', '')).toBe('Приёмка');
    expect(receiptReason('Приёмка', 'Поставщик', '')).toBe('Приёмка · Поставщик');
  });
});
