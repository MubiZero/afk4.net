import type { ReservationDto } from '@afk4/contracts';
import type { SeatSummary } from '../operatorData';

export type BookingTone = 'confirmed' | 'online' | 'pending' | 'seated' | 'cancelled';

export interface BookingItem {
  reservationId: string;
  reservationGroupId: string; // '' = одиночная бронь; общий id связывает блоки одной группы
  version: number;
  state: string;
  source: string;
  startMs: number;
  endMs: number;
  durationMinutes: number;
  customerName: string;
  phoneNumber: string;
  note: string;
  playerAccountId: string;
  // Личность за счётом, с которого пришла заявка. Ею стойка и спрашивает сеть про гостя:
  // у заявки, записанной на стойке одним номером, счёта ещё нет — и спрашивать нечем.
  platformPersonId: string;
  seatId: string;
  seatName: string;
  zoneName: string;
  tone: BookingTone;
  startedSessionId: string;
  // Докуда клуб обещал ответить на заявку. Есть только у той, что ждёт решения стойки:
  // подтверждённую бронь по таймеру никто не снимает. null = срока нет.
  respondByMs: number | null;
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

function readTimestamp(raw: string | null | undefined): number | null {
  if (!raw) return null;
  const ms = new Date(raw).getTime();
  return Number.isNaN(ms) ? null : ms;
}

/**
 * Сколько осталось на ответ, в виде «4:12» (мм:сс) или «1:04:12» (ч:мм:сс) для длинных сроков.
 *
 * Срок показываем только заявке, которая действительно ждёт ответа: у подтверждённой брони
 * `respondByUtc` уже ничего не значит, и обратный отсчёт рядом с ней врал бы.
 * `overdue` — срок вышел, а заявка ещё здесь: фоновая задача снимет её в ближайший проход.
 */
export function respondCountdown(
  item: Pick<BookingItem, 'state' | 'respondByMs'>,
  nowMs: number
): { label: string; overdue: boolean } | null {
  if (item.state !== 'pending' || item.respondByMs === null) return null;

  const remainingSeconds = Math.floor((item.respondByMs - nowMs) / 1000);
  if (remainingSeconds <= 0) return { label: '0:00', overdue: true };

  const hours = Math.floor(remainingSeconds / 3600);
  const minutes = Math.floor((remainingSeconds % 3600) / 60);
  const seconds = remainingSeconds % 60;
  const pad = (value: number) => String(value).padStart(2, '0');
  return {
    label: hours > 0 ? `${hours}:${pad(minutes)}:${pad(seconds)}` : `${minutes}:${pad(seconds)}`,
    overdue: false
  };
}

function bookingTone(state: string, source: string): BookingTone {
  // Неявка терминальна так же, как отмена, и в полосе выглядит так же приглушённо. Отличает их
  // подпись: смешивать «передумал» и «не приехал» в одно слово нельзя, но и кричать о неявке
  // отдельным цветом не за чем — заявка уже закрыта.
  if (state === 'cancelled' || state === 'no_show' || state === 'rejected') return 'cancelled';
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
  | 'op.booking.state.noShow'
  | 'op.booking.state.rejected'
  | 'op.booking.state.unknown';

export function bookingStateLabelKey(state: string): BookingStateKey {
  switch (state) {
    case 'confirmed': return 'op.booking.state.confirmed';
    case 'pending': return 'op.booking.state.pending';
    case 'seated': return 'op.booking.state.seated';
    case 'cancelled': return 'op.booking.state.cancelled';
    case 'no_show': return 'op.booking.state.noShow';
    case 'rejected': return 'op.booking.state.rejected';
    default: return 'op.booking.state.unknown';
  }
}

// Отказать можно в заявке, которую ещё не приняли: подтверждённую бронь клуб отменяет, и это
// другой разговор с человеком, которому уже пообещали место.
//
// Неявка повторяет правило сервера (ReservationNoShow.WhyNot), а не смягчает его: не приехать
// можно только на подтверждённую бронь и только после её начала. Заявка, на которую клуб сам не
// ответил, — это молчание клуба, а не чужая неявка, и превращать одно в другое одним кликом
// нельзя. Не начавшаяся бронь — тем более: человек ещё не опоздал.
export function bookingDetailActions(
  state: string,
  startedByNow = false,
  hasStartedSession = false
): {
  canConfirm: boolean;
  canStart: boolean;
  canSeat: boolean;
  canReject: boolean;
  canMarkNoShow: boolean;
} {
  return {
    canConfirm: state === 'pending',
    // Посаженная бронь без запущенной сессии — это отмеченный приход: человек у стойки, машина ещё
    // не запущена. Забрать у него «Начать сессию» значило бы рвать связь брони с сессией.
    canStart: (state === 'confirmed' || state === 'seated') && !hasStartedSession,
    // Отметка прихода повторяет правило сервера (SeatAsync): усадить можно и заявку —
    // сама посадка и есть ответ клуба, и сервер проставляет подтверждение сам.
    canSeat: state === 'pending' || state === 'confirmed',
    canReject: state === 'pending',
    canMarkNoShow: state === 'confirmed' && startedByNow
  };
}

export function mapReservationsToItems(
  reservations: ReservationDto[],
  guestName: string
): BookingItem[] {
  return reservations.map((reservation) => {
    const startMs = new Date(reservation.startsAtUtc).getTime();
    const safeStart = Number.isNaN(startMs) ? 0 : startMs;
    const durationMinutes = reservation.durationMinutes;
    const phoneNumber = reservation.phoneNumber ?? '';
    return {
      reservationId: reservation.reservationId,
      reservationGroupId: reservation.reservationGroupId ?? '',
      version: reservation.version ?? 1,
      state: reservation.state,
      source: reservation.source,
      startMs: safeStart,
      endMs: safeStart + durationMinutes * 60_000,
      durationMinutes,
      customerName: reservation.customerName || guestName,
      phoneNumber,
      note: reservation.note || phoneNumber,
      playerAccountId: reservation.playerAccountId ?? '',
      platformPersonId: reservation.platformPersonId ?? '',
      seatId: reservation.seatId ?? '',
      seatName: reservation.seatName ?? '',
      zoneName: reservation.zoneName ?? '',
      tone: bookingTone(reservation.state, reservation.source),
      startedSessionId: reservation.startedSessionId ?? '',
      respondByMs: readTimestamp(reservation.respondByUtc)
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
