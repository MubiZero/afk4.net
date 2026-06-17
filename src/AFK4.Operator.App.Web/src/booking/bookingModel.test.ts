import { it, expect } from 'bun:test';
import type { SeatSummary } from '../operatorData';
import {
  mapReservationsToItems,
  computeAxis,
  buildSeatRows,
  unseatedOnlineRequests
} from './bookingModel';

const seat = (id: string, zone: string, name: string): SeatSummary => ({
  id, zone, name, tone: 'ready', command: '', remaining: '', status: '', deviceName: name,
  activeSessionId: null, remainingSeconds: null
} as unknown as SeatSummary);

const HOUR = 3_600_000;
const day = Date.UTC(2026, 5, 17, 0, 0, 0); // локальная полночь в тесте трактуется как старт оси-входа

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

it('mapReservationsToItems: пустое имя → гость', () => {
  const items = mapReservationsToItems([
    { reservationId: 'r1', startsAtUtc: '2026-06-17T14:00:00Z', durationMinutes: 30 }
  ], 'Гость');
  expect(items[0].customerName).toBe('Гость');
});

it('computeAxis: нет броней → окно вокруг now, минимум 6ч, в пределах суток', () => {
  const now = day + 12 * HOUR;
  const axis = computeAxis([], day, now);
  expect(axis.endMs - axis.startMs).toBeGreaterThanOrEqual(6 * HOUR);
  expect(axis.startMs).toBeGreaterThanOrEqual(day);
  expect(axis.endMs).toBeLessThanOrEqual(day + 24 * HOUR);
});

it('computeAxis: одна короткая бронь → диапазон расширен до минимума 6ч', () => {
  const items = mapReservationsToItems([
    { reservationId: 'r1', startsAtUtc: new Date(day + 14 * HOUR).toISOString(), durationMinutes: 30 }
  ], 'Гость');
  const axis = computeAxis(items, day, day + 10 * HOUR);
  expect(axis.endMs - axis.startMs).toBeGreaterThanOrEqual(6 * HOUR);
});

it('computeAxis: ticks стоят на часовых границах внутри окна', () => {
  const axis = computeAxis([], day, day + 12 * HOUR);
  for (const tick of axis.ticks) {
    expect(tick.ms).toBeGreaterThanOrEqual(axis.startMs);
    expect(tick.ms).toBeLessThanOrEqual(axis.endMs);
  }
  expect(axis.ticks.length).toBeGreaterThan(0);
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

it('unseatedOnlineRequests: только online+pending без места', () => {
  const items = mapReservationsToItems([
    { reservationId: 'r1', state: 'pending', source: 'online', startsAtUtc: new Date(day).toISOString(), durationMinutes: 30 },
    { reservationId: 'r2', state: 'pending', source: 'online', startsAtUtc: new Date(day).toISOString(), durationMinutes: 30, seatId: 'a1' },
    { reservationId: 'r3', state: 'confirmed', source: 'online', startsAtUtc: new Date(day).toISOString(), durationMinutes: 30 }
  ], 'Гость');
  const lane = unseatedOnlineRequests(items);
  expect(lane.map((i) => i.reservationId)).toEqual(['r1']);
});
