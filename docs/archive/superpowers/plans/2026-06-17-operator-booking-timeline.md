# Редизайн «Брони» → таймлайн-гант — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **UI-качество:** перед версткой компонентов (Tasks 4–7) вызови навык `interface-limb` — он даёт планку дизайна (контраст WCAG, размеры целей, состояния, движение). Логика/позиционирование в плане конкретны; визуальную полировку (отступы, тени) бери из существующих токенов проекта.

**Goal:** Переделать вкладку «Брони» Operator.App из 4-квадрантной сетки в таймлайн-гант (строки по местам, ось времени, боковой drawer), починив навигацию по датам и выбор места при создании/переносе.

**Architecture:** Чистый фронт (бэкенд API уже всё умеет). Контейнер `BackendBookingWorkspace` грузит брони за выбранную дату, собирает модель через чистый модуль `bookingModel.ts` и раздаёт её презентационным компонентам `BookingTimeline` / `BookingRequestsLane` / `BookingDrawer`. Состояния (загрузка/пусто/ошибка) — через примитивы Карты.

**Tech Stack:** React 19, TypeScript, Vite, bun test (happy-dom), `@afk4/i18n` (locales JSON + codegen), lucide-react.

## Global Constraints

- **Рабочая директория веб-приложения:** `src/AFK4.Operator.App.Web`. Все относительные пути ниже — от неё, если не указано иное.
- **bun:** полный путь `/home/fedya/.bun/bin/bun` (в PATH может не быть).
- **i18n — НЕ редактировать `packages/i18n/src/messages.ts`** (авто-генерится). Менять `locales/{ru,en,tg}.json` (в корне репозитория) + `cd packages/i18n && /home/fedya/.bun/bin/bun run gen`.
- **Парити локалей:** ключ обязан быть в ru, en И tg с настоящим переводом. tg===ru допустим только для заимствований/брендов/символов и тогда добавляется в `TG_IDENTICAL_TO_RU_ALLOWED` в `packages/i18n/src/messages.test.ts`. Никакого фейк-копирования ru в tg (lens #37).
- **Никаких AI-подписей** в коммитах (WORKING-STYLE #24): без `Co-Authored-By`/`Generated with`.
- **Стиль кода:** как в соседних файлах (`MapWorkspace.tsx`, `operatorPrimitives.tsx`). Маленькие файлы, одна ответственность.
- **Переиспользовать примитивы:** `EmptyState`, `Skeleton`, `FeedbackNotice`, `StateFlag` из `operatorPrimitives.tsx`; `useDeferredFlag`; хелперы из `operatorHelpers.ts`.

## File Structure

- Create: `src/booking/bookingModel.ts` — чистые типы и функции (ось, блоки, группировка, тоны).
- Create: `src/booking/bookingModel.test.ts` — юнит-тесты модели.
- Create: `src/booking/BookingTimeline.tsx` — гант (жёлоб + строки + ось + «сейчас» + блоки).
- Create: `src/booking/BookingRequestsLane.tsx` — полоса онлайн-заявок без места.
- Create: `src/booking/BookingDrawer.tsx` — drawer (деталь + создание, с выбором места).
- Modify: `src/BackendBookingWorkspace.tsx` — контейнер: дата, загрузка, сборка модели, рендер компонентов.
- Rewrite: `src/styles/10-booking.css` — стили ганта/drawer/лейна.
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json` (корень репо) — новые ключи.
- Modify (если нужно): `src/App.test.tsx` — поправить ассерты на старый DOM броней.

---

### Task 1: i18n-ключи для нового экрана

**Files:**
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json`
- Modify (если tg===ru легитимен): `packages/i18n/src/messages.test.ts`
- Regen: `packages/i18n/src/messages.ts` (через `bun run gen`)

**Interfaces:**
- Produces: новые `MessageKey` — `op.booking.dateNav.prev`, `op.booking.dateNav.next`, `op.booking.dateNav.today`, `op.booking.axis.now`, `op.booking.timeline.aria`, `op.booking.requests.laneTitle`, `op.booking.create.seat`, `op.booking.create.seatNone`, `op.booking.move.seat`, `op.booking.empty.dayTitle`, `op.booking.empty.dayHint`, `op.booking.drawer.detailTitle`, `op.booking.drawer.createTitle`, `op.booking.cellCreateAria`.

- [ ] **Step 1: Добавить ключи в `locales/ru.json`**

Найти блок `"op.booking.*"` и добавить рядом (значения — настоящие RU):

```json
"op.booking.dateNav.prev": "Предыдущий день",
"op.booking.dateNav.next": "Следующий день",
"op.booking.dateNav.today": "Сегодня",
"op.booking.axis.now": "Сейчас",
"op.booking.timeline.aria": "Таймлайн броней по местам",
"op.booking.requests.laneTitle": "Онлайн-заявки без места",
"op.booking.create.seat": "Место",
"op.booking.create.seatNone": "Нет свободных мест",
"op.booking.move.seat": "Перенести на место",
"op.booking.empty.dayTitle": "На этот день броней нет",
"op.booking.empty.dayHint": "Создайте бронь или выберите другой день.",
"op.booking.drawer.detailTitle": "Бронь",
"op.booking.drawer.createTitle": "Новая бронь",
"op.booking.cellCreateAria": "Создать бронь на это место и время"
```

- [ ] **Step 2: Добавить те же ключи в `locales/en.json`** (настоящий EN):

```json
"op.booking.dateNav.prev": "Previous day",
"op.booking.dateNav.next": "Next day",
"op.booking.dateNav.today": "Today",
"op.booking.axis.now": "Now",
"op.booking.timeline.aria": "Reservation timeline by seat",
"op.booking.requests.laneTitle": "Online requests without a seat",
"op.booking.create.seat": "Seat",
"op.booking.create.seatNone": "No free seats",
"op.booking.move.seat": "Move to seat",
"op.booking.empty.dayTitle": "No reservations for this day",
"op.booking.empty.dayHint": "Create a reservation or pick another day.",
"op.booking.drawer.detailTitle": "Reservation",
"op.booking.drawer.createTitle": "New reservation",
"op.booking.cellCreateAria": "Create a reservation for this seat and time"
```

- [ ] **Step 3: Добавить те же ключи в `locales/tg.json`** (настоящий TG):

```json
"op.booking.dateNav.prev": "Рӯзи гузашта",
"op.booking.dateNav.next": "Рӯзи оянда",
"op.booking.dateNav.today": "Имрӯз",
"op.booking.axis.now": "Ҳозир",
"op.booking.timeline.aria": "Хатти вақти бандҳо аз рӯи ҷойҳо",
"op.booking.requests.laneTitle": "Дархостҳои онлайн бе ҷой",
"op.booking.create.seat": "Ҷой",
"op.booking.create.seatNone": "Ҷойҳои холӣ нестанд",
"op.booking.move.seat": "Гузарондан ба ҷой",
"op.booking.empty.dayTitle": "Барои ин рӯз бандҳо нестанд",
"op.booking.empty.dayHint": "Бандеро созед ё рӯзи дигарро интихоб кунед.",
"op.booking.drawer.detailTitle": "Банд",
"op.booking.drawer.createTitle": "Банди нав",
"op.booking.cellCreateAria": "Барои ин ҷой ва вақт банд созед"
```

> Примечание: TG проверит носитель (memory: настоящий таджикский уже катился). Если по какому-то ключу не уверен — НЕ копируй ru, оставь лучший перевод и пометь в PR-описании на ревью.

- [ ] **Step 4: Перегенерировать messages.ts**

Run: `cd /home/fedya/projects/afk4.net/packages/i18n && /home/fedya/.bun/bin/bun run gen`
Expected: `generated .../messages.ts from 3 locales`

- [ ] **Step 5: Проверить парити-тест**

Run: `cd /home/fedya/projects/afk4.net/packages/i18n && /home/fedya/.bun/bin/bun test src/messages.test.ts`
Expected: PASS (ru/en/tg одинаковые наборы ключей).

- [ ] **Step 6: Commit**

```bash
cd /home/fedya/projects/afk4.net
git add locales/ru.json locales/en.json locales/tg.json packages/i18n/src/messages.ts
git commit -m "i18n(operator-booking): ключи таймлайна, навигации дат и drawer"
```

---

### Task 2: Чистая модель `bookingModel.ts` (TDD)

**Files:**
- Create: `src/booking/bookingModel.ts`
- Test: `src/booking/bookingModel.test.ts`

**Interfaces:**
- Consumes: `SeatSummary` из `../operatorData`; `readArray/readString/readNumber` из `../operatorHelpers`.
- Produces:
  - `interface BookingItem { reservationId; state; source; startMs; endMs; durationMinutes; customerName; phoneNumber; note; seatId; seatName; zoneName; tone }`
  - `type BookingTone = 'confirmed' | 'online' | 'pending' | 'seated' | 'cancelled'`
  - `interface TimelineAxis { startMs; endMs; spanMs; ticks: { ms: number; label: string }[] }`
  - `interface BookingBlock { item: BookingItem; leftPct: number; widthPct: number }`
  - `interface SeatRow { seat: SeatSummary; blocks: BookingBlock[] }`
  - `interface ZoneRowGroup { zone: string; rows: SeatRow[] }`
  - `mapReservationsToItems(reservations: Record<string, unknown>[], guestName: string): BookingItem[]`
  - `computeAxis(items: BookingItem[], dayStartMs: number, nowMs: number): TimelineAxis`
  - `buildSeatRows(seats: SeatSummary[], items: BookingItem[], axis: TimelineAxis): { groups: ZoneRowGroup[]; unplaced: BookingItem[] }`
  - `unseatedOnlineRequests(items: BookingItem[]): BookingItem[]`
  - `onlineRequestCount(items: BookingItem[]): number`

- [ ] **Step 1: Написать падающий тест `bookingModel.test.ts`**

```ts
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
```

- [ ] **Step 2: Запустить тест — убедиться, что падает**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/booking/bookingModel.test.ts`
Expected: FAIL (модуль/функции не найдены).

- [ ] **Step 3: Реализовать `bookingModel.ts`**

```ts
import type { SeatSummary } from '../operatorData';
import { readArray, readString, readNumber } from '../operatorHelpers';

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
```

- [ ] **Step 4: Запустить тест — убедиться, что проходит**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/booking/bookingModel.test.ts`
Expected: PASS (все кейсы).

- [ ] **Step 5: Commit**

```bash
cd /home/fedya/projects/afk4.net
git add src/AFK4.Operator.App.Web/src/booking/bookingModel.ts src/AFK4.Operator.App.Web/src/booking/bookingModel.test.ts
git commit -m "feat(operator-booking): чистая модель таймлайна (ось, блоки, группировка)"
```

---

### Task 3: Презентационные компоненты (Timeline + RequestsLane + Drawer)

> Перед версткой вызови `interface-limb`. Логика и пропсы ниже конкретны; визуальные значения берём из существующих CSS-токенов (как в `MapWorkspace`/`SeatTile`).

**Files:**
- Create: `src/booking/BookingTimeline.tsx`
- Create: `src/booking/BookingRequestsLane.tsx`
- Create: `src/booking/BookingDrawer.tsx`

**Interfaces:**
- Consumes из `bookingModel`: `ZoneRowGroup`, `TimelineAxis`, `BookingItem`, `BookingBlock`.
- Consumes из `../operatorData`: `SeatSummary`. Из `../operatorPrimitives`: `EmptyState`, `Skeleton`, `FeedbackNotice`. Из `../operatorTypes`: `Feedback`.
- Produces (пропсы, на которые опирается контейнер в Task 4):
  - `BookingTimeline({ groups, axis, nowMs, loading, showSkeleton, selectedReservationId, branchName, onSelectBlock, onCellCreate }: { groups: ZoneRowGroup[]; axis: TimelineAxis; nowMs: number; loading: boolean; showSkeleton: boolean; selectedReservationId: string; branchName: string; onSelectBlock: (item: BookingItem) => void; onCellCreate: (seat: SeatSummary, startMs: number) => void })`
  - `BookingRequestsLane({ requests, busy, canManage, onAccept, onClarify }: { requests: BookingItem[]; busy: boolean; canManage: boolean; onAccept: (item: BookingItem) => void; onClarify: (item: BookingItem) => void })` — рендерит `null`, если `requests.length === 0`.
  - `BookingDrawer({ mode, selected, freeSeats, draft, feedback, busy, canManage, onClose, onChangeDraft, onSelectSeat, onCreate, onSeat, onMove, onCancel, onConfirm, onOpenMap }: BookingDrawerProps)` — см. тип ниже.

- [ ] **Step 1: `BookingTimeline.tsx`**

```tsx
import { useI18n } from '@afk4/i18n';
import type { SeatSummary } from '../operatorData';
import { zoneLabel } from '../operatorHelpers';
import { EmptyState, Skeleton } from '../operatorPrimitives';
import type { BookingItem, TimelineAxis, ZoneRowGroup } from './bookingModel';

export function BookingTimeline({
  groups, axis, nowMs, loading, showSkeleton, selectedReservationId, branchName, onSelectBlock, onCellCreate
}: {
  groups: ZoneRowGroup[];
  axis: TimelineAxis;
  nowMs: number;
  loading: boolean;
  showSkeleton: boolean;
  selectedReservationId: string;
  branchName: string;
  onSelectBlock: (item: BookingItem) => void;
  onCellCreate: (seat: SeatSummary, startMs: number) => void;
}) {
  const { t } = useI18n();
  const hasRows = groups.some((g) => g.rows.length > 0);
  const nowPct = nowMs >= axis.startMs && nowMs <= axis.endMs
    ? ((nowMs - axis.startMs) / axis.spanMs) * 100
    : null;

  if (loading) {
    return showSkeleton ? (
      <div className="booking-grid" role="status" aria-label={t('op.booking.load.loading')}>
        {Array.from({ length: 8 }).map((_, i) => (
          <div className="booking-grid-row" key={i}>
            <Skeleton className="booking-row-label-skel" />
            <Skeleton className="booking-row-track-skel" />
          </div>
        ))}
      </div>
    ) : null;
  }

  if (!hasRows) {
    return <EmptyState title={t('op.booking.empty.dayTitle')} description={t('op.booking.empty.dayHint')} className="booking-empty" />;
  }

  // Перевод клика по дорожке в момент времени на оси.
  const cellStartMs = (event: React.MouseEvent<HTMLButtonElement>): number => {
    const rect = event.currentTarget.getBoundingClientRect();
    const ratio = Math.min(1, Math.max(0, (event.clientX - rect.left) / rect.width));
    return axis.startMs + ratio * axis.spanMs;
  };

  return (
    <div className="booking-grid" aria-label={t('op.booking.timeline.aria')}>
      <div className="booking-grid-axis">
        <div className="booking-axis-gutter" />
        <div className="booking-axis-track">
          {axis.ticks.map((tick) => (
            <span
              key={tick.ms}
              className="booking-axis-tick"
              style={{ left: `${((tick.ms - axis.startMs) / axis.spanMs) * 100}%` }}
            >{tick.label}</span>
          ))}
        </div>
      </div>

      <div className="booking-grid-body">
        {nowPct !== null && (
          <div className="booking-now-line" style={{ left: `calc(var(--booking-gutter) + (100% - var(--booking-gutter)) * ${nowPct / 100})` }} aria-label={t('op.booking.axis.now')} />
        )}
        {groups.map((group) => (
          <section className="booking-zone" key={group.zone}>
            <header className="booking-zone-head"><span>{zoneLabel(group.zone, t)}</span><strong>{group.rows.length}</strong></header>
            {group.rows.map((row) => (
              <div className="booking-grid-row" key={row.seat.id}>
                <span className="booking-row-label" title={row.seat.name}>{row.seat.name}</span>
                <button
                  type="button"
                  className="booking-row-track"
                  aria-label={t('op.booking.cellCreateAria')}
                  onClick={(event) => onCellCreate(row.seat, cellStartMs(event))}
                >
                  {row.blocks.map((block) => (
                    <span
                      key={block.item.reservationId}
                      role="button"
                      tabIndex={0}
                      className={`booking-block ${block.item.tone}${block.item.reservationId === selectedReservationId ? ' active' : ''}`}
                      style={{ left: `${block.leftPct}%`, width: `${block.widthPct}%` }}
                      onClick={(event) => { event.stopPropagation(); onSelectBlock(block.item); }}
                      onKeyDown={(event) => { if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); onSelectBlock(block.item); } }}
                    >
                      <b>{block.item.customerName}</b>
                    </span>
                  ))}
                </button>
              </div>
            ))}
          </section>
        ))}
      </div>
    </div>
  );
}
```

- [ ] **Step 2: `BookingRequestsLane.tsx`**

```tsx
import { useI18n } from '@afk4/i18n';
import { formatTime } from '../operatorHelpers';
import type { BookingItem } from './bookingModel';

export function BookingRequestsLane({
  requests, busy, canManage, onAccept, onClarify
}: {
  requests: BookingItem[];
  busy: boolean;
  canManage: boolean;
  onAccept: (item: BookingItem) => void;
  onClarify: (item: BookingItem) => void;
}) {
  const { t } = useI18n();
  if (requests.length === 0) return null;

  return (
    <section className="booking-requests-lane" aria-label={t('op.booking.requests.laneTitle')}>
      <header className="booking-lane-title"><span>{t('op.booking.requests.laneTitle')}</span><strong>{requests.length}</strong></header>
      <div className="booking-lane-cards">
        {requests.map((request) => (
          <article key={request.reservationId} className="booking-lane-card">
            <span className="booking-lane-time">{formatTime(new Date(request.startMs).toISOString())}</span>
            <strong>{request.customerName}</strong>
            <em>{request.note || t('op.booking.noComment')}</em>
            <div>
              <button type="button" disabled={!canManage || busy} onClick={() => onAccept(request)}>{t('op.booking.requests.accept')}</button>
              <button type="button" onClick={() => onClarify(request)}>{t('op.booking.requests.clarify')}</button>
            </div>
          </article>
        ))}
      </div>
    </section>
  );
}
```

- [ ] **Step 3: `BookingDrawer.tsx`** (деталь + создание; выбор места в обоих режимах)

```tsx
import { ArrowRightLeft, MonitorCheck, Plus, Square, UserRoundPlus, X } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import type { SeatSummary } from '../operatorData';
import type { Feedback } from '../operatorTypes';
import { formatTime, zoneLabel } from '../operatorHelpers';
import { FeedbackNotice } from '../operatorPrimitives';
import type { BookingItem } from './bookingModel';

export interface BookingDraft {
  customerName: string;
  phoneNumber: string;
  startsAt: string;       // datetime-local
  durationMinutes: number;
  seatId: string;
}

export interface BookingDrawerProps {
  mode: 'detail' | 'create';
  selected: BookingItem | null;
  freeSeats: SeatSummary[];
  draft: BookingDraft;
  feedback: Feedback;
  busy: boolean;
  canManage: boolean;
  onClose: () => void;
  onChangeDraft: (patch: Partial<BookingDraft>) => void;
  onCreate: () => void;
  onSeat: () => void;
  onMove: (targetSeatId: string) => void;
  onCancel: () => void;
  onConfirm: (item: BookingItem) => void;
  onOpenMap: (seatId: string) => void;
}

export function BookingDrawer(props: BookingDrawerProps) {
  const { t } = useI18n();
  const { mode, selected, freeSeats, draft, feedback, busy, canManage } = props;
  const title = mode === 'create' ? t('op.booking.drawer.createTitle') : t('op.booking.drawer.detailTitle');

  return (
    <aside className="booking-drawer" role="dialog" aria-label={title}>
      <header className="booking-drawer-head">
        <strong>{title}</strong>
        <button type="button" className="booking-drawer-close" aria-label={t('common.cancel')} onClick={props.onClose}><X size={16} /></button>
      </header>

      {mode === 'create' ? (
        <div className="booking-drawer-body">
          <label className="booking-field">{t('op.booking.create.seat')}
            <select value={draft.seatId} disabled={busy || freeSeats.length === 0} onChange={(e) => props.onChangeDraft({ seatId: e.target.value })}>
              {freeSeats.length === 0 && <option value="">{t('op.booking.create.seatNone')}</option>}
              {freeSeats.map((seat) => (
                <option key={seat.id} value={seat.id}>{zoneLabel(seat.zone, t)} · {seat.name}</option>
              ))}
            </select>
          </label>
          <label className="booking-field">{t('op.booking.client')}
            <input value={draft.customerName} disabled={busy} onChange={(e) => props.onChangeDraft({ customerName: e.target.value })} />
          </label>
          <label className="booking-field">{t('clients.field.phone')}
            <input value={draft.phoneNumber} disabled={busy} onChange={(e) => props.onChangeDraft({ phoneNumber: e.target.value })} />
          </label>
          <label className="booking-field">{t('op.booking.create.start')}
            <input type="datetime-local" value={draft.startsAt} disabled={busy} onChange={(e) => props.onChangeDraft({ startsAt: e.target.value })} />
          </label>
          <label className="booking-field">{t('op.booking.create.duration')}
            <input type="number" min={15} step={15} value={draft.durationMinutes} disabled={busy} onChange={(e) => props.onChangeDraft({ durationMinutes: Number(e.target.value) || 60 })} />
          </label>
          <FeedbackNotice feedback={feedback} />
          <button type="button" className="booking-primary-action" disabled={!canManage || busy || freeSeats.length === 0 || !draft.seatId} onClick={props.onCreate}>{t('op.booking.create.submit')}</button>
        </div>
      ) : selected && (
        <div className="booking-drawer-body">
          <div className={`booking-status-card ${selected.tone}`}>
            <span>{selected.state}</span>
            <strong>{formatTime(new Date(selected.startMs).toISOString())}</strong>
            <em>{selected.seatName ? `${zoneLabel(selected.zoneName, t)} · ${selected.seatName}` : zoneLabel(selected.zoneName, t)} · {t('op.booking.durationMin', { count: selected.durationMinutes })}</em>
          </div>

          <div className="booking-action-grid">
            <button type="button" disabled={!selected.seatId || busy} onClick={() => props.onOpenMap(selected.seatId)}><MonitorCheck size={15} />{t('op.booking.actions.openMap')}</button>
            <button type="button" disabled={!canManage || busy || !selected.seatId} onClick={props.onSeat}><UserRoundPlus size={15} />{t('op.booking.actions.seat')}</button>
            {selected.source === 'online' && selected.state === 'pending' && (
              <button type="button" disabled={!canManage || busy} onClick={() => props.onConfirm(selected)}><Plus size={15} />{t('op.booking.requests.accept')}</button>
            )}
            <button type="button" className="danger" disabled={!canManage || busy} onClick={props.onCancel}><Square size={15} />{t('op.booking.actions.cancel')}</button>
          </div>

          <label className="booking-field">{t('op.booking.move.seat')}
            <select value="" disabled={!canManage || busy || freeSeats.length === 0}
              onChange={(e) => { if (e.target.value) props.onMove(e.target.value); }}>
              <option value="">{freeSeats.length === 0 ? t('op.booking.create.seatNone') : '—'}</option>
              {freeSeats.filter((s) => s.id !== selected.seatId).map((seat) => (
                <option key={seat.id} value={seat.id}>{zoneLabel(seat.zone, t)} · {seat.name}</option>
              ))}
            </select>
          </label>

          <FeedbackNotice feedback={feedback} />

          <div className="booking-detail-list">
            <div><span>{t('op.booking.client')}</span><strong>{selected.customerName}</strong></div>
            <div><span>{t('op.booking.detail.comment')}</span><strong>{selected.note || t('op.booking.noComment')}</strong></div>
            <div><span>{t('op.booking.detail.source')}</span><strong>{selected.source === 'online' ? t('op.booking.source.online') : t('op.booking.source.operator')}</strong></div>
          </div>
        </div>
      )}
    </aside>
  );
}
```

- [ ] **Step 4: Проверить типы/сборку (компоненты ещё не подключены к контейнеру)**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run build 2>&1 | tail -20`
Expected: возможна ошибка «модуль не используется» из других мест нет; ошибки только если есть опечатки типов в трёх файлах. (Контейнер подключим в Task 4 — если сборка ругается лишь на неиспользуемые экспорты, это ок; на ошибки типов внутри файлов — чинить.)

- [ ] **Step 5: Commit**

```bash
cd /home/fedya/projects/afk4.net
git add src/AFK4.Operator.App.Web/src/booking/BookingTimeline.tsx src/AFK4.Operator.App.Web/src/booking/BookingRequestsLane.tsx src/AFK4.Operator.App.Web/src/booking/BookingDrawer.tsx
git commit -m "feat(operator-booking): компоненты таймлайна, лейна заявок и drawer"
```

---

### Task 4: Контейнер `BackendBookingWorkspace` — дата, загрузка, сборка модели, действия

**Files:**
- Modify (переписать тело): `src/BackendBookingWorkspace.tsx`

**Interfaces:**
- Consumes: всё из Task 2/3 + существующие `createAuthenticatedOperatorClients`, `requireBackend`, `hasPermission`, `permissionNames`, `addMinutes`, `toDateInputValue`, `toDateTimeInputValue`, `addDays`, `formatDateTime`.
- Сигнатура компонента НЕ меняется: `BackendBookingWorkspace({ floorMap, backend, onOpenSeat })`.

- [ ] **Step 1: Переписать контейнер**

Ключевые изменения относительно текущего файла:
1. Добавить стейт `const [selectedDate, setSelectedDate] = useState(() => new Date())` (нормализуем к локальной полуночи при вычислении границ).
2. `bookingFromUtc/ToUtc` считать из `selectedDate` через `toDateInputValue(selectedDate)` (а не `today`).
3. После загрузки: `const items = mapReservationsToItems(readArray(reservationResult, 'reservations'), t('op.booking.guest'))`.
4. `const dayStartMs = new Date(`${toDateInputValue(selectedDate)}T00:00:00`).getTime()`.
5. `const nowMs = Date.now()`; `const axis = useMemo(() => computeAxis(items, dayStartMs, nowMs), [items, dayStartMs, nowMs])`.
6. `const { groups, unplaced } = useMemo(() => buildSeatRows(floorMap.seats, items, axis), [floorMap.seats, items, axis])`.
7. `const requests = unseatedOnlineRequests(items)` для лейна; `onlineRequestCount(items)` — для StateFlag.
8. Стейты drawer: `const [drawerMode, setDrawerMode] = useState<'detail'|'create'|null>(null)`, `const [selectedReservationId, setSelectedReservationId] = useState('')`, `draft` (BookingDraft).
9. `readySeats` = как раньше (`tone==='ready' && !activeSessionId`) — это `freeSeats` для drawer.
10. Действия (`createReservation`, `seatReservation`, `moveReservation`, `cancelReservation`, `confirmReservation`) — перенести существующую логику `runReservationAction`, но:
    - `create` берёт `draft.seatId` (а не первое свободное);
    - `move(targetSeatId)` принимает явный id из drawer-селекта (а не `readySeats.find(...)`).
11. Обработчики UI:
    - `onSelectBlock(item)` → `setSelectedReservationId(item.reservationId); setDrawerMode('detail')`.
    - `onCellCreate(seat, startMs)` → заполнить `draft` (`seatId: seat.id`, `startsAt: toDateTimeInputValue(new Date(startMs))`), `setDrawerMode('create')`.
    - кнопка `+ Бронь` в шапке → `draft` с дефолтами (`seatId: readySeats[0]?.id ?? ''`, `startsAt` = now+15м), `setDrawerMode('create')`.
    - `onAccept(item)` (из лейна) → `confirmReservation(item.reservationId, ...)`.
    - `onClarify(item)` → `setSelectedReservationId(item.reservationId); setDrawerMode('detail')`.
12. Дата-навигатор в `screen-actions`: кнопки «‹» (`setSelectedDate(d => addDays(d, -1))`), подпись (`formatDateTime`-усечённо или `toDateInputValue` + «Сегодня», если совпадает), «›».

Полный JSX `return` (заменяет текущий, строки 251–379):

```tsx
  const selectedItem = items.find((i) => i.reservationId === selectedReservationId) ?? null;
  const dateLabel = toDateInputValue(selectedDate) === toDateInputValue(new Date())
    ? t('op.booking.dateNav.today')
    : new Date(selectedDate).toLocaleDateString('ru-RU', { day: '2-digit', month: 'long' });

  return (
    <main className="workspace-screen booking-screen">
      <section className="screen-head booking-head">
        <div>
          <span>{t('op.booking.eyebrow')}</span>
          <h1>{t('op.booking.title')}</h1>
        </div>
        <div className="screen-actions">
          <div className="booking-date-nav">
            <button type="button" aria-label={t('op.booking.dateNav.prev')} onClick={() => setSelectedDate((d) => addDays(d, -1))}>‹</button>
            <strong>{dateLabel}</strong>
            <button type="button" aria-label={t('op.booking.dateNav.next')} onClick={() => setSelectedDate((d) => addDays(d, 1))}>›</button>
          </div>
          <span className={`map-load-state ${loadStatus === 'backend' ? 'ready' : loadStatus}`}>{loadLabel}</span>
          <button type="button" className="booking-create-action" disabled={!canManageReservations} onClick={openCreateDrawer}><Plus size={14} />{t('op.booking.createBtn')}</button>
        </div>
      </section>

      <section className="state-strip booking-state-strip">
        <StateFlag label={t('op.booking.strip.free')} value={String(readySeats.length)} />
        <StateFlag label={t('op.booking.strip.busy')} value={String(activeSeats.length)} />
        <StateFlag label={t('op.booking.strip.problems')} value={String(problemSeats.length)} critical={problemSeats.length > 0} />
        <StateFlag label={t('op.booking.strip.bookings')} value={String(items.length)} critical={loadStatus === 'failed'} />
        <StateFlag label={t('op.booking.strip.requests')} value={String(onlineRequestCount(items))} critical={onlineRequestCount(items) > 0} />
      </section>

      {loadStatus === 'failed' && (
        <FeedbackNotice feedback={{ label: t('op.booking.eyebrow'), state: 'failed', detail: loadError ?? t('op.booking.load.failed') }} />
      )}

      <BookingRequestsLane
        requests={requests}
        busy={reservationBusy}
        canManage={canManageReservations}
        onAccept={(item) => confirmReservation(item.reservationId, t('op.booking.requests.acceptLabel', { client: item.customerName }))}
        onClarify={(item) => { setSelectedReservationId(item.reservationId); setDrawerMode('detail'); }}
      />

      <section className={`booking-layout${drawerMode ? ' with-drawer' : ''}`}>
        <BookingTimeline
          groups={groups}
          axis={axis}
          nowMs={nowMs}
          loading={loadStatus === 'loading'}
          showSkeleton={showSkeleton}
          selectedReservationId={selectedReservationId}
          branchName={floorMap.branchName}
          onSelectBlock={(item) => { setSelectedReservationId(item.reservationId); setDrawerMode('detail'); }}
          onCellCreate={openCreateDrawerForCell}
        />

        {drawerMode && (
          <BookingDrawer
            mode={drawerMode}
            selected={selectedItem}
            freeSeats={readySeats}
            draft={draft}
            feedback={feedback}
            busy={reservationBusy}
            canManage={canManageReservations}
            onClose={() => setDrawerMode(null)}
            onChangeDraft={(patch) => setDraft((d) => ({ ...d, ...patch }))}
            onCreate={createReservation}
            onSeat={seatReservation}
            onMove={moveReservation}
            onCancel={cancelReservation}
            onConfirm={(item) => confirmReservation(item.reservationId, t('op.booking.requests.acceptLabel', { client: item.customerName }))}
            onOpenMap={onOpenSeat}
          />
        )}
      </section>
    </main>
  );
```

Где `showSkeleton = useDeferredFlag(loadStatus === 'loading')`, `openCreateDrawer` и `openCreateDrawerForCell(seat, startMs)` — хелперы, заполняющие `draft` и ставящие `drawerMode='create'`. `createReservation` использует `draft.seatId`; `moveReservation(targetSeatId)` использует переданный id.

- [ ] **Step 2: Сборка типов**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run build 2>&1 | tail -20`
Expected: PASS (`tsc -b` без ошибок, vite build собирается). Чинить любые type-ошибки до зелёного.

- [ ] **Step 3: Commit**

```bash
cd /home/fedya/projects/afk4.net
git add src/AFK4.Operator.App.Web/src/BackendBookingWorkspace.tsx
git commit -m "feat(operator-booking): контейнер на таймлайн — даты, выбор места, drawer"
```

---

### Task 5: CSS ганта/drawer/лейна

**Files:**
- Rewrite: `src/styles/10-booking.css`

> Вызови `interface-limb` для проверки контраста/состояний. Использовать существующие токены (`--surface-*`, `--border-*`, `--text-*`, `--accent*`, `--success`, `--warning`, `--danger-strong`), как в `06-map-grid.css`/`07-map-sidepanel.css`.

- [ ] **Step 1: Переписать `10-booking.css`** под новую разметку. Минимум классов: `.booking-layout`(grid: track + опц. drawer), `.booking-layout.with-drawer`(`grid-template-columns: minmax(0,1fr) 360px`), `.booking-date-nav`, `.booking-requests-lane`/`.booking-lane-*`, `.booking-grid`/`.booking-grid-axis`/`.booking-axis-track`/`.booking-axis-tick`, `.booking-grid-body`, `.booking-zone`/`.booking-zone-head`, `.booking-grid-row`(grid: `var(--booking-gutter) 1fr`), `.booking-row-label`, `.booking-row-track`(position: relative, clickable), `.booking-block`(position:absolute; тоны: `.confirmed/.online/.pending/.seated`), `.booking-now-line`, `.booking-drawer`/`.booking-drawer-head/body`, `.booking-field`(label+input/select), сохранить `.booking-status-card`/`.booking-action-grid`/`.booking-detail-list`/`.booking-primary-action` (переиспользуются drawer-ом). Объявить `--booking-gutter: 96px` на `.booking-grid`.

Высоту ганта считать как у старого `.booking-layout` (формула `calc(100vh - ...)`), чтобы вертикальный скролл был внутри `.booking-grid-body` (`overflow-y: auto`).

- [ ] **Step 2: Поднять dev-превью и глазами проверить** (sandbox off для chromium не нужен — открыть вручную):

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run dev` (фоном), открыть URL, вкладка «Брони».
Проверить визуально: ось с засечками, строки по местам, блок брони PC-02 на своей строке, drawer по клику, навигатор дат переключает день (на пустой день — EmptyState), «сейчас» вертикаль.

- [ ] **Step 3: Commit**

```bash
cd /home/fedya/projects/afk4.net
git add src/AFK4.Operator.App.Web/src/styles/10-booking.css
git commit -m "style(operator-booking): стили таймлайна, drawer и лейна заявок"
```

---

### Task 6: Починить существующие тесты и финальная проверка

**Files:**
- Modify (если падает): `src/App.test.tsx` (и/или `CommandPalette.test.tsx`, `QuickActionsMenu.test.tsx`)

**Interfaces:**
- Consumes: ничего нового.

- [ ] **Step 1: Прогнать весь тест-сьют веб-приложения**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test 2>&1 | tail -30`
Expected: смотрим, какие тесты броней упали из-за смены DOM.

- [ ] **Step 2: Поправить упавшие ассерты на новый DOM**

Любой тест, который кликал старые карточки/панели броней (напр. `getByText('Лента броней')`, селекторы `.booking-card`), переписать на новые узлы: вкладку открывает `getByTitle('Брони')`; присутствие экрана проверять по заголовку `getByRole('heading', { name: ... })` или по `aria-label={t('op.booking.timeline.aria')}`. Логику проверки (что экран рендерится, действия доступны) сохранить — менять только селекторы под новую структуру. **Не ослаблять тесты ради зелёного** (lens #37): если тест проверял доступность кнопки «Принять» для заявки — оставить эту проверку, указав на новый узел (лейн или drawer).

- [ ] **Step 3: Прогнать тесты повторно**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test 2>&1 | tail -15`
Expected: PASS (все).

- [ ] **Step 4: Финальная сборка**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run build 2>&1 | tail -5`
Expected: PASS.

- [ ] **Step 5: Прогнать i18n-тесты (парити не сломали)**

Run: `cd /home/fedya/projects/afk4.net/packages/i18n && /home/fedya/.bun/bin/bun test 2>&1 | tail -10`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
cd /home/fedya/projects/afk4.net
git add -A
git commit -m "test(operator-booking): привести тесты к новому DOM таймлайна"
```

---

## Self-Review

**1. Spec coverage:**
- Навигация дат → Task 4 (selectedDate, date-nav). ✓
- Таймлайн-гант (ось авто-подгон, строки по местам, «сейчас», клик по ячейке) → Task 2 (`computeAxis`/`buildSeatRows`) + Task 3 (`BookingTimeline`) + Task 5 (CSS). ✓
- Drawer (деталь + создание, выбор места, реальный перенос) → Task 3 (`BookingDrawer`) + Task 4 (действия). ✓
- Лейн онлайн-заявок без места → Task 2 (`unseatedOnlineRequests`) + Task 3 (`BookingRequestsLane`). ✓
- Состояния (skeleton/empty/error) → Task 3 (Timeline) + Task 4 (FeedbackNotice ошибки). ✓
- Файловая декомпозиция → Tasks 2–5 создают `booking/*`. ✓
- i18n реальными переводами во все локали → Task 1. ✓
- Тесты модели + зелёные существующие → Task 2 + Task 6. ✓
- За рамками (drag, неделя, саб-лейны) → не запланировано (намеренно). ✓

**2. Placeholder scan:** код модели и компонентов полный; CSS-задача описывает классы и значения-источники (токены) без «add styles here» — конкретные правила пишутся по списку классов. Тестовый Task 6 даёт стратегию правок, а не готовый текст (старый DOM правок заранее неизвестен — это честно помечено).

**3. Type consistency:** `BookingItem/TimelineAxis/ZoneRowGroup/SeatRow/BookingBlock/BookingDraft/BookingDrawerProps` и сигнатуры `mapReservationsToItems/computeAxis/buildSeatRows/unseatedOnlineRequests/onlineRequestCount` совпадают между Task 2 (определение), Task 3 (потребление в пропсах) и Task 4 (контейнер). `moveReservation(targetSeatId)` принимает id — совпадает с `onMove` в `BookingDrawerProps`.
