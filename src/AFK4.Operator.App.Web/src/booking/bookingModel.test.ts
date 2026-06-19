import { it, expect } from 'bun:test';
import type { SeatSummary } from '../operatorData';
import {
  mapReservationsToItems,
  mapSessionDtosToItems,
  computeAxis,
  buildSeatRows,
  unseatedOnlineRequests,
  bookingStateLabelKey,
  type SessionDtoLike
} from './bookingModel';

const seat = (id: string, zone: string, name: string): SeatSummary => ({
  id, zone, name, tone: 'ready', command: '', remaining: '', status: '', deviceName: name,
  activeSessionId: null, remainingSeconds: null
} as unknown as SeatSummary);

const HOUR = 3_600_000;
const day = Date.UTC(2026, 5, 17, 0, 0, 0); // локальная полночь в тесте трактуется как старт оси-входа

it('bookingStateLabelKey: каждое известное состояние → свой i18n-ключ, неизвестное → unknown', () => {
  expect(bookingStateLabelKey('confirmed')).toBe('op.booking.state.confirmed');
  expect(bookingStateLabelKey('pending')).toBe('op.booking.state.pending');
  expect(bookingStateLabelKey('seated')).toBe('op.booking.state.seated');
  expect(bookingStateLabelKey('cancelled')).toBe('op.booking.state.cancelled');
  expect(bookingStateLabelKey('expired')).toBe('op.booking.state.unknown');
  expect(bookingStateLabelKey('')).toBe('op.booking.state.unknown');
});

it('mapReservationsToItems: парсит поля и считает endMs из длительности', () => {
  const items = mapReservationsToItems([
    { reservationId: 'r1', state: 'pending', source: 'online', startsAtUtc: '2026-06-17T14:00:00Z',
      durationMinutes: 60, customerName: 'Марат', phoneNumber: '+992', seatId: 'a1', seatName: 'PC-01', zoneName: 'Зал A' }
  ], 'Гость');
  expect(items).toHaveLength(1);
  expect(items[0].customerName).toBe('Марат');
  expect(items[0].endMs - items[0].startMs).toBe(60 * 60_000);
  expect(items[0].tone).toBe('online');
});

it('mapReservationsToItems: читает reservationGroupId (пусто если нет)', () => {
  const items = mapReservationsToItems([
    { reservationId: 'g1', startsAtUtc: '2026-06-17T14:00:00Z', durationMinutes: 60, reservationGroupId: 'grp-7' },
    { reservationId: 's1', startsAtUtc: '2026-06-17T14:00:00Z', durationMinutes: 60 }
  ], 'Гость');
  expect(items[0].reservationGroupId).toBe('grp-7');
  expect(items[1].reservationGroupId).toBe('');
});

it('mapReservationsToItems: пустое имя → гость', () => {
  const items = mapReservationsToItems([
    { reservationId: 'r1', startsAtUtc: '2026-06-17T14:00:00Z', durationMinutes: 30 }
  ], 'Гость');
  expect(items[0].customerName).toBe('Гость');
});

it('computeAxis: всегда полные сутки 00:00–24:00, независимо от броней', () => {
  const empty = computeAxis([], day, day + 12 * HOUR);
  expect(empty.startMs).toBe(day);
  expect(empty.endMs).toBe(day + 24 * HOUR);
  expect(empty.spanMs).toBe(24 * HOUR);

  // С бронями ось не меняется — те же сутки.
  const items = mapReservationsToItems([
    { reservationId: 'r1', startsAtUtc: new Date(day + 14 * HOUR).toISOString(), durationMinutes: 30 }
  ], 'Гость');
  const withItems = computeAxis(items, day, day + 10 * HOUR);
  expect(withItems.startMs).toBe(day);
  expect(withItems.endMs).toBe(day + 24 * HOUR);
});

it('computeAxis: часовые засечки на все сутки, шагом в час', () => {
  const axis = computeAxis([], day, day + 12 * HOUR);
  expect(axis.ticks.length).toBeGreaterThanOrEqual(23);
  for (const tick of axis.ticks) {
    expect(tick.ms).toBeGreaterThanOrEqual(axis.startMs);
    expect(tick.ms).toBeLessThanOrEqual(axis.endMs);
  }
  for (let i = 1; i < axis.ticks.length; i += 1) {
    expect(axis.ticks[i].ms - axis.ticks[i - 1].ms).toBe(HOUR);
  }
});

it('buildSeatRows: блок ложится на свою строку, %-позиция в [0,100]', () => {
  const seats = [seat('a1', 'Зал A', 'PC-01'), seat('a2', 'Зал A', 'PC-02')];
  const items = mapReservationsToItems([
    { reservationId: 'r1', state: 'confirmed', source: 'operator',
      startsAtUtc: new Date(day + 14 * HOUR).toISOString(), durationMinutes: 60, seatId: 'a1', seatName: 'PC-01', zoneName: 'Зал A' }
  ], 'Гость');
  const axis = computeAxis(items, day, day + 10 * HOUR);
  const { groups, unplaced } = buildSeatRows(seats, items, axis);
  expect(unplaced).toHaveLength(0);
  const rowA1 = groups[0].rows.find((r) => r.seat.id === 'a1')!;
  expect(rowA1.blocks).toHaveLength(1);
  expect(rowA1.blocks[0].leftPct).toBeGreaterThanOrEqual(0);
  expect(rowA1.blocks[0].leftPct + rowA1.blocks[0].widthPct).toBeLessThanOrEqual(100.0001);
});

it('buildSeatRows: бронь с неизвестным местом → unplaced', () => {
  const seats = [seat('a1', 'Зал A', 'PC-01')];
  const items = mapReservationsToItems([
    { reservationId: 'r1', state: 'pending', source: 'operator',
      startsAtUtc: new Date(day + 14 * HOUR).toISOString(), durationMinutes: 60, seatId: 'ZZZ' }
  ], 'Гость');
  const axis = computeAxis(items, day, day + 10 * HOUR);
  const { unplaced } = buildSeatRows(seats, items, axis);
  expect(unplaced).toHaveLength(1);
});

it('buildSeatRows: отменённые на грид не попадают', () => {
  const seats = [seat('a1', 'Зал A', 'PC-01')];
  const items = mapReservationsToItems([
    { reservationId: 'r1', state: 'cancelled', source: 'operator',
      startsAtUtc: new Date(day + 14 * HOUR).toISOString(), durationMinutes: 60, seatId: 'a1' }
  ], 'Гость');
  const axis = computeAxis(items, day, day + 10 * HOUR);
  const { groups } = buildSeatRows(seats, items, axis);
  expect(groups[0].rows[0].blocks).toHaveLength(0);
});

const sessionDto = (seatId: string, over: Partial<SessionDtoLike>): SessionDtoLike => ({
  sessionId: `s-${seatId}`, seatId, state: 'active', playerDisplayName: null, tariffName: null,
  startedAtUtc: new Date(day + 9 * HOUR).toISOString(), endsAtUtc: null, endedAtUtc: null, ...over
});

it('mapSessionDtosToItems: открытый таб (без конца) → open, endMs=null', () => {
  const sessions = mapSessionDtosToItems([
    sessionDto('a1', { playerDisplayName: 'Юсуф' })
  ]);
  expect(sessions).toHaveLength(1);
  expect(sessions[0].open).toBe(true);
  expect(sessions[0].endMs).toBeNull();
  expect(sessions[0].startMs).toBe(day + 9 * HOUR);
  expect(sessions[0].playerName).toBe('Юсуф');
});

it('mapSessionDtosToItems: завершённая (endedAtUtc) → ограниченная фактическим концом', () => {
  const ended = day + 5 * HOUR;
  const sessions = mapSessionDtosToItems([
    sessionDto('a1', { state: 'ended', startedAtUtc: new Date(day + 4 * HOUR).toISOString(), endedAtUtc: new Date(ended).toISOString() })
  ]);
  expect(sessions[0].open).toBe(false);
  expect(sessions[0].endMs).toBe(ended);
});

it('mapSessionDtosToItems: активная фикс (endsAtUtc) → ограниченная плановым дедлайном', () => {
  const deadline = day + 11 * HOUR;
  const sessions = mapSessionDtosToItems([
    sessionDto('a1', { endsAtUtc: new Date(deadline).toISOString() })
  ]);
  expect(sessions[0].open).toBe(false);
  expect(sessions[0].endMs).toBe(deadline);
});

it('mapSessionDtosToItems: битый старт → пропуск', () => {
  const sessions = mapSessionDtosToItems([
    sessionDto('a1', { startedAtUtc: 'не дата' })
  ]);
  expect(sessions).toHaveLength(0);
});

it('buildSeatRows: сессия ложится на строку своего места', () => {
  const seats = [seat('a1', 'Зал A', 'PC-01'), seat('a2', 'Зал A', 'PC-02')];
  const axis = computeAxis([], day, day + 10 * HOUR);
  const sessions = mapSessionDtosToItems([sessionDto('a1', {})]);
  const { groups } = buildSeatRows(seats, [], axis, sessions);
  const rowA1 = groups[0].rows.find((r) => r.seat.id === 'a1')!;
  const rowA2 = groups[0].rows.find((r) => r.seat.id === 'a2')!;
  expect(rowA1.sessions).toHaveLength(1);
  expect(rowA1.sessions[0].open).toBe(true);
  // открытая тянется до правого края оси
  expect(rowA1.sessions[0].leftPct + rowA1.sessions[0].widthPct).toBeCloseTo(100, 5);
  expect(rowA2.sessions).toHaveLength(0);
});

it('unseatedOnlineRequests: только online+pending без места', () => {
  const items = mapReservationsToItems([
    { reservationId: 'r1', state: 'pending', source: 'online', startsAtUtc: new Date(day).toISOString(), durationMinutes: 30 },
    { reservationId: 'r2', state: 'pending', source: 'online', startsAtUtc: new Date(day).toISOString(), durationMinutes: 30, seatId: 'a1' },
    { reservationId: 'r3', state: 'confirmed', source: 'online', startsAtUtc: new Date(day).toISOString(), durationMinutes: 30 }
  ], 'Гость');
  const lane = unseatedOnlineRequests(items);
  expect(lane.map((i) => i.reservationId)).toEqual(['r1']);
});
