import type { SeatSummary } from '../operatorData';
import { readString, readNumber } from '../operatorHelpers';

export type BookingTone = 'confirmed' | 'online' | 'pending' | 'seated' | 'cancelled';

export interface BookingItem {
  reservationId: string;
  state: string;
  source: string;
  startMs: number;
  endMs: number;
  durationMinutes: number;
  customerName: string;
  phoneNumber: string;
  note: string;
  seatId: string;
  seatName: string;
  zoneName: string;
  tone: BookingTone;
}

export interface TimelineAxis {
  startMs: number;
  endMs: number;
  spanMs: number;
  ticks: { ms: number; label: string }[];
}

export interface BookingBlock {
  item: BookingItem;
  leftPct: number;
  widthPct: number;
}

export interface SeatRow {
  seat: SeatSummary;
  blocks: BookingBlock[];
}

export interface ZoneRowGroup {
  zone: string;
  rows: SeatRow[];
}

const HOUR_MS = 3_600_000;
const MIN_SPAN_MS = 6 * HOUR_MS;
const PAD_MS = 30 * 60_000;

function bookingTone(state: string, source: string): BookingTone {
  if (state === 'cancelled') return 'cancelled';
  if (state === 'seated') return 'seated';
  if (state === 'confirmed') return 'confirmed';
  return source === 'online' ? 'online' : 'pending';
}

export function mapReservationsToItems(
  reservations: Record<string, unknown>[],
  guestName: string
): BookingItem[] {
  return reservations.map((reservation) => {
    const state = readString(reservation, 'state', 'pending');
    const source = readString(reservation, 'source', 'operator');
    const startsAtUtc = readString(reservation, 'startsAtUtc');
    const durationMinutes = readNumber(reservation, 'durationMinutes', 60);
    const startMs = new Date(startsAtUtc).getTime();
    const safeStart = Number.isNaN(startMs) ? 0 : startMs;
    return {
      reservationId: readString(reservation, 'reservationId'),
      state,
      source,
      startMs: safeStart,
      endMs: safeStart + durationMinutes * 60_000,
      durationMinutes,
      customerName: readString(reservation, 'customerName', guestName),
      phoneNumber: readString(reservation, 'phoneNumber'),
      note: readString(reservation, 'note', readString(reservation, 'phoneNumber')),
      seatId: readString(reservation, 'seatId'),
      seatName: readString(reservation, 'seatName'),
      zoneName: readString(reservation, 'zoneName'),
      tone: bookingTone(state, source)
    };
  });
}

function buildTicks(startMs: number, endMs: number): { ms: number; label: string }[] {
  const ticks: { ms: number; label: string }[] = [];
  const first = new Date(startMs);
  first.setMinutes(0, 0, 0);
  if (first.getTime() < startMs) first.setHours(first.getHours() + 1);
  for (let ms = first.getTime(); ms <= endMs; ms += HOUR_MS) {
    const d = new Date(ms);
    ticks.push({ ms, label: `${String(d.getHours()).padStart(2, '0')}:00` });
  }
  return ticks;
}

export function computeAxis(items: BookingItem[], dayStartMs: number, nowMs: number): TimelineAxis {
  const dayEndMs = dayStartMs + 24 * HOUR_MS;
  const placed = items.filter((i) => i.state !== 'cancelled' && i.startMs > 0);

  let start: number;
  let end: number;
  if (placed.length === 0) {
    const center = Math.min(Math.max(nowMs, dayStartMs), dayEndMs);
    start = center - HOUR_MS;
    end = center + 5 * HOUR_MS;
  } else {
    start = Math.min(...placed.map((i) => i.startMs)) - PAD_MS;
    end = Math.max(...placed.map((i) => i.endMs)) + PAD_MS;
  }

  start = Math.max(dayStartMs, start);
  end = Math.min(dayEndMs, end);

  if (end - start < MIN_SPAN_MS) {
    const deficit = MIN_SPAN_MS - (end - start);
    start = Math.max(dayStartMs, start - deficit / 2);
    end = Math.min(dayEndMs, start + MIN_SPAN_MS);
    start = Math.max(dayStartMs, end - MIN_SPAN_MS);
  }

  return { startMs: start, endMs: end, spanMs: end - start, ticks: buildTicks(start, end) };
}

function toBlock(item: BookingItem, axis: TimelineAxis): BookingBlock {
  const rawLeft = ((item.startMs - axis.startMs) / axis.spanMs) * 100;
  const rawWidth = ((item.endMs - item.startMs) / axis.spanMs) * 100;
  const leftPct = Math.min(100, Math.max(0, rawLeft));
  const widthPct = Math.min(100 - leftPct, Math.max(1, rawWidth));
  return { item, leftPct, widthPct };
}

export function buildSeatRows(
  seats: SeatSummary[],
  items: BookingItem[],
  axis: TimelineAxis
): { groups: ZoneRowGroup[]; unplaced: BookingItem[] } {
  const placeable = items.filter((i) => i.state !== 'cancelled' && i.seatId.length > 0);
  const bySeat = new Map<string, BookingBlock[]>();
  const seatIds = new Set(seats.map((s) => s.id));
  const unplaced: BookingItem[] = [];

  for (const item of placeable) {
    if (!seatIds.has(item.seatId)) {
      unplaced.push(item);
      continue;
    }
    const list = bySeat.get(item.seatId) ?? [];
    list.push(toBlock(item, axis));
    bySeat.set(item.seatId, list);
  }

  const groups: ZoneRowGroup[] = [];
  const indexByZone = new Map<string, number>();
  for (const seat of seats) {
    const row: SeatRow = { seat, blocks: bySeat.get(seat.id) ?? [] };
    const at = indexByZone.get(seat.zone);
    if (at === undefined) {
      indexByZone.set(seat.zone, groups.length);
      groups.push({ zone: seat.zone, rows: [row] });
    } else {
      groups[at].rows.push(row);
    }
  }

  return { groups, unplaced };
}

export function onlineRequestCount(items: BookingItem[]): number {
  return items.filter((i) => i.source === 'online' && i.state === 'pending').length;
}

export function unseatedOnlineRequests(items: BookingItem[]): BookingItem[] {
  return items.filter((i) => i.source === 'online' && i.state === 'pending' && i.seatId.length === 0);
}
