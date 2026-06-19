import type { SeatSummary } from '../operatorData';
import { readString, readNumber } from '../operatorHelpers';

export type BookingTone = 'confirmed' | 'online' | 'pending' | 'seated' | 'cancelled';

export interface BookingItem {
  reservationId: string;
  reservationGroupId: string; // '' = одиночная бронь; общий id связывает блоки одной группы
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
  ticks: { ms: number; label: string; major: boolean }[];
}

export interface BookingBlock {
  item: BookingItem;
  leftPct: number;
  widthPct: number;
}

// Игровая сессия на месте: открытая (endMs=null — конца нет, занято «неограниченно») либо
// ограниченная дедлайном (фикс/предоплата). В отличие от брони — это факт занятости ПК сейчас.
export interface SessionItem {
  sessionId: string;
  seatId: string;
  startMs: number;
  endMs: number | null;
  open: boolean;
  playerName: string;
  tariffName: string | null;
}

export interface SessionBlock {
  item: SessionItem;
  leftPct: number;
  widthPct: number;
  open: boolean;
}

export interface SeatRow {
  seat: SeatSummary;
  blocks: BookingBlock[];
  sessions: SessionBlock[];
}

export interface ZoneRowGroup {
  zone: string;
  rows: SeatRow[];
}

const HOUR_MS = 3_600_000;

function bookingTone(state: string, source: string): BookingTone {
  if (state === 'cancelled') return 'cancelled';
  if (state === 'seated') return 'seated';
  if (state === 'confirmed') return 'confirmed';
  return source === 'online' ? 'online' : 'pending';
}

// i18n-ключ человекочитаемого состояния брони. Состояние приходит с бэка строкой
// (confirmed/pending/seated/cancelled) — её нельзя показывать оператору как есть.
type BookingStateKey =
  | 'op.booking.state.confirmed'
  | 'op.booking.state.pending'
  | 'op.booking.state.seated'
  | 'op.booking.state.cancelled'
  | 'op.booking.state.unknown';

export function bookingStateLabelKey(state: string): BookingStateKey {
  switch (state) {
    case 'confirmed': return 'op.booking.state.confirmed';
    case 'pending': return 'op.booking.state.pending';
    case 'seated': return 'op.booking.state.seated';
    case 'cancelled': return 'op.booking.state.cancelled';
    default: return 'op.booking.state.unknown';
  }
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
      reservationGroupId: readString(reservation, 'reservationGroupId'),
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

// Засечка на каждый час (ровная сетка), но подпись HH:00 ставится не на каждой, а раз в
// LABEL_STEP_HOURS — иначе на узкой оси 24 метки наезжают друг на друга. Промежуточные часы
// остаются «минорными» (мелкая риска без текста) — масштаб виден, подписи не слипаются.
const LABEL_STEP_HOURS = 3;

function buildTicks(startMs: number, endMs: number): TimelineAxis['ticks'] {
  const ticks: TimelineAxis['ticks'] = [];
  const first = new Date(startMs);
  first.setMinutes(0, 0, 0);
  if (first.getTime() < startMs) first.setHours(first.getHours() + 1);
  for (let ms = first.getTime(); ms <= endMs; ms += HOUR_MS) {
    const d = new Date(ms);
    const hours = d.getHours();
    ticks.push({ ms, label: `${String(hours).padStart(2, '0')}:00`, major: hours % LABEL_STEP_HOURS === 0 });
  }
  return ticks;
}

// Таймлайн всегда показывает полные сутки 00:00–24:00 по часам — стабильно и независимо от броней.
// (items/nowMs больше не влияют на ось; параметры сохранены для совместимости вызова.)
export function computeAxis(_items: BookingItem[], dayStartMs: number, _nowMs: number): TimelineAxis {
  const startMs = dayStartMs;
  const endMs = dayStartMs + 24 * HOUR_MS;
  // endMs - 1, чтобы последняя засечка была 23:00, а не дублирующая 00:00 следующих суток у края.
  return { startMs, endMs, spanMs: endMs - startMs, ticks: buildTicks(startMs, endMs - 1) };
}

function toBlock(item: BookingItem, axis: TimelineAxis): BookingBlock {
  const rawLeft = ((item.startMs - axis.startMs) / axis.spanMs) * 100;
  const rawWidth = ((item.endMs - item.startMs) / axis.spanMs) * 100;
  const leftPct = Math.min(100, Math.max(0, rawLeft));
  const widthPct = Math.min(100 - leftPct, Math.max(1, rawWidth));
  return { item, leftPct, widthPct };
}

// Открытая сессия тянется до правого края оси (конца нет); ограниченная — до дедлайна.
function toSessionBlock(item: SessionItem, axis: TimelineAxis): SessionBlock {
  const endMs = item.open ? axis.endMs : (item.endMs ?? axis.endMs);
  const rawLeft = ((item.startMs - axis.startMs) / axis.spanMs) * 100;
  const rawWidth = ((endMs - item.startMs) / axis.spanMs) * 100;
  const leftPct = Math.min(100, Math.max(0, rawLeft));
  const widthPct = Math.min(100 - leftPct, Math.max(0.6, rawWidth));
  return { item, leftPct, widthPct, open: item.open };
}

// Структурная форма DTO сессии с бэкенда (см. SessionTimelineItemDto) — модель не зависит от клиента.
export interface SessionDtoLike {
  sessionId: string;
  seatId: string;
  state: string;
  playerDisplayName: string | null;
  tariffName: string | null;
  startedAtUtc: string;
  endsAtUtc: string | null;
  endedAtUtc: string | null;
}

// DTO сессий (активных и завершённых) → элементы таймлайна. Конец = фактический (endedAtUtc) или
// плановый (endsAtUtc); если обоих нет — открытый таб, рисуется без конца.
export function mapSessionDtosToItems(dtos: SessionDtoLike[]): SessionItem[] {
  const out: SessionItem[] = [];
  for (const dto of dtos) {
    const startMs = new Date(dto.startedAtUtc).getTime();
    if (Number.isNaN(startMs)) continue;
    const endIso = dto.endedAtUtc ?? dto.endsAtUtc ?? null;
    const endMs = endIso ? new Date(endIso).getTime() : Number.NaN;
    const open = endIso === null || Number.isNaN(endMs);
    out.push({
      sessionId: dto.sessionId,
      seatId: dto.seatId,
      startMs,
      endMs: open ? null : endMs,
      open,
      playerName: dto.playerDisplayName ?? '',
      tariffName: dto.tariffName ?? null
    });
  }
  return out;
}

export function buildSeatRows(
  seats: SeatSummary[],
  items: BookingItem[],
  axis: TimelineAxis,
  sessions: SessionItem[] = []
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

  const sessionsBySeat = new Map<string, SessionBlock[]>();
  for (const session of sessions) {
    if (!seatIds.has(session.seatId)) continue;
    const list = sessionsBySeat.get(session.seatId) ?? [];
    list.push(toSessionBlock(session, axis));
    sessionsBySeat.set(session.seatId, list);
  }

  const groups: ZoneRowGroup[] = [];
  const indexByZone = new Map<string, number>();
  for (const seat of seats) {
    const row: SeatRow = { seat, blocks: bySeat.get(seat.id) ?? [], sessions: sessionsBySeat.get(seat.id) ?? [] };
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
