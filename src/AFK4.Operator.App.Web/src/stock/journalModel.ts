import type { PosProductDto, StockMovementDto } from '../operatorApiClients';
import { readMoney, readNumber, readString } from '../operatorHelpers';

export type MovementType = 'purchase' | 'sale' | 'refund' | 'adjustment';
export type JournalTypeFilter = 'all' | MovementType;
export type JournalPeriod = 'today' | 'week' | 'all';

export interface JournalRow {
  id: string;
  productId: string;
  name: string;
  sku: string;
  type: string;
  quantityDelta: number;
  unitCostMinorUnits: number;
  sumMinorUnits: number;
  reason: string;
  who: string;
  createdAtUtc: string;
}

const DAY_MS = 86_400_000;

export function mapMovementsToRows(movements: StockMovementDto[], catalog: PosProductDto[]): JournalRow[] {
  const byId = new Map(catalog.map((product) => [readString(product, 'productId'), product]));
  return movements.map((movement) => {
    const productId = readString(movement, 'productId');
    const product = byId.get(productId);
    const quantityDelta = readNumber(movement, 'quantityDelta', 0);
    const unitCostMinorUnits = readMoney(movement, 'unitCost')?.minorUnits ?? 0;
    return {
      id: readString(movement, 'stockMovementId'),
      productId,
      name: product ? readString(product, 'name', productId) : productId,
      sku: product ? readString(product, 'sku') : '',
      type: readString(movement, 'movementType'),
      quantityDelta,
      unitCostMinorUnits,
      sumMinorUnits: Math.abs(quantityDelta) * unitCostMinorUnits,
      reason: readString(movement, 'reason'),
      who: readString(movement, 'createdByDisplayName'),
      createdAtUtc: readString(movement, 'createdAtUtc'),
    };
  });
}

export function filterByType(rows: JournalRow[], filter: JournalTypeFilter): JournalRow[] {
  return filter === 'all' ? rows : rows.filter((row) => row.type === filter);
}

export function filterByPeriod(rows: JournalRow[], period: JournalPeriod, nowMs: number): JournalRow[] {
  if (period === 'all') return rows;
  const startOfUtcDay = nowMs - (nowMs % DAY_MS);
  const threshold = period === 'today' ? startOfUtcDay : nowMs - 7 * DAY_MS;
  return rows.filter((row) => {
    const ms = Date.parse(row.createdAtUtc);
    return Number.isFinite(ms) && ms >= threshold;
  });
}

export interface DayGroup {
  dayKey: string;
  rows: JournalRow[];
}

// Группировка по UTC-календарному дню; дни — от новых к старым, строки внутри — как пришли (бэк отдаёт desc).
export function groupByDay(rows: JournalRow[]): DayGroup[] {
  const groups: DayGroup[] = [];
  const index = new Map<string, DayGroup>();
  for (const row of rows) {
    const dayKey = row.createdAtUtc.slice(0, 10);
    let group = index.get(dayKey);
    if (!group) {
      group = { dayKey, rows: [] };
      index.set(dayKey, group);
      groups.push(group);
    }
    group.rows.push(row);
  }
  return groups.sort((a, b) => (a.dayKey < b.dayKey ? 1 : a.dayKey > b.dayKey ? -1 : 0));
}

export interface JournalSummary {
  inboundQty: number;
  inboundSumMinor: number;
  soldQty: number;
  writtenOffQty: number;
  writtenOffSumMinor: number;
  netQty: number;
}

export function summarize(rows: JournalRow[]): JournalSummary {
  const summary: JournalSummary = { inboundQty: 0, inboundSumMinor: 0, soldQty: 0, writtenOffQty: 0, writtenOffSumMinor: 0, netQty: 0 };
  for (const row of rows) {
    summary.netQty += row.quantityDelta;
    if (row.type === 'purchase') {
      summary.inboundQty += row.quantityDelta;
      summary.inboundSumMinor += row.sumMinorUnits;
    } else if (row.type === 'sale') {
      summary.soldQty += Math.abs(row.quantityDelta);
    } else if (row.type === 'adjustment' && row.quantityDelta < 0) {
      summary.writtenOffQty += Math.abs(row.quantityDelta);
      summary.writtenOffSumMinor += row.sumMinorUnits;
    }
  }
  return summary;
}

function csvCell(value: string): string {
  return /[",\n]/.test(value) ? `"${value.replace(/"/g, '""')}"` : value;
}

export function buildCsv(
  rows: JournalRow[],
  opts: {
    headers: string[];
    typeLabel: (type: string) => string;
    formatMoney: (minorUnits: number) => string;
    formatDateTime: (iso: string) => string;
  }
): string {
  const lines = [opts.headers.map(csvCell).join(',')];
  for (const row of rows) {
    lines.push([
      opts.formatDateTime(row.createdAtUtc),
      opts.typeLabel(row.type),
      row.name,
      row.sku,
      String(row.quantityDelta),
      opts.formatMoney(row.unitCostMinorUnits),
      opts.formatMoney(row.sumMinorUnits),
      row.reason,
      row.who,
    ].map(csvCell).join(','));
  }
  return lines.join('\n');
}
