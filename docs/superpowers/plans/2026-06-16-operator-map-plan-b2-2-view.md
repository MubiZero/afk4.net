# План «План» (просмотр) — B2-2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Заменить вид «Таблица» в операторской Карте зала на новый вид **«План»** — плоскую top-down планировку с живыми статусами мест, рисующую реальную геометрию (места по координатам, зоны-прямоугольники, стены). Только просмотр; редактор расстановки приходит в B2-3.

**Architecture:** Бэкенд B2-1 уже отдаёт геометрию (`posX/posY/rotation/seatType` у мест, `geoX/geoY/geoWidth/geoHeight/color/zoneType` у зон, массив `walls`). Здесь мы: (1) дотягиваем фронт-контракты и состояние, чтобы геометрия не терялась; (2) пишем чистый геометрический модуль (ячейки→пиксели, bounding-box); (3) строим из состояния плоскую модель плана (`PlanModel`); (4) рисуем гибридом — SVG-подложка (сетка/зоны/стены) + DOM-слой мест поверх (переиспользует визуальный язык тонов плитки); (5) меняем переключатель видов `Сетка · План` и удаляем Таблицу.

**Tech Stack:** React + TypeScript (Vite), `bun test` + `@testing-library/react` (jsdom), `@afk4/i18n` (локали `locales/{ru,en,tg}.json`, авто-генерация `MessageKey`), обычный CSS (`src/styles.css`).

**Корень веб-приложения:** `src/AFK4.Operator.App.Web` (имя пакета `afk4-operator-app-web`). Все относительные пути ниже — от этого корня, если не сказано иное.

**Команды (важно — окружение):**
- Тесты воркспейса: из `src/AFK4.Operator.App.Web` запускать одиночный файл `bun test src/<file>.test.tsx`. Полный прогон: `bun test $(ls src/*.test.ts src/*.test.tsx | grep -v App.test) && bun test src/App.test.tsx`.
- `bun` в этом окружении может требовать полного пути (`~/.bun/bin/bun`) — см. память `afk4-env-quirks`.
- Сборка/типы: `bun run build` (`tsc -b && vite build`) из `src/AFK4.Operator.App.Web`.
- Регенерация i18n: из `packages/i18n` — `bun run gen` (генерит `src/messages.ts` из `locales/*.json`).

**Продуктовые решения (зафиксированы):**
- **«План» игнорирует фильтр карты.** Фильтры (`all/ready/active/...`) — инструмент триажа для Сетки. План показывает всю планировку зала целиком; фильтровать пространственный вид смысла нет.
- **Поворот (`rotation`) визуально пока не применяется** к маркеру места (чтобы не вращать подпись и не делать её нечитаемой). Значение хранится и доедет до редактора B2-3, который даст осмысленную индикацию направления.
- **Стопку «не на плане» в режиме просмотра не рисуем** — неразмещённые места видны в Сетке. Но под холстом показываем честный счётчик «Не на плане: N» (#34), чтобы оператор не думал, что место пропало.
- **Между B2-2 и B2-3** ни у одного места ещё нет координат (редактор не построен) → План у всех покажет пустое состояние «Зал ещё не размечен». Это ожидаемо и честно; Сетка остаётся полноценным рабочим видом. Кнопку «Разметить план» добавит B2-3 вместе с редактором.

---

## Структура файлов

**Создаются:**
- `src/floorPlanGeometry.ts` — чистые геометрические функции (`cellToPx`, `boundingBox`, константы). Юнит-тестируемо.
- `src/floorPlanGeometry.test.ts`
- `src/floorPlanState.ts` — типы плоской модели (`PlanSeat`/`PlanZone`/`Wall`/`PlanModel`) + `toPlanModel(state)` (раскладывает места на размещённые/неразмещённые, фильтрует зоны с геометрией, считает bbox).
- `src/floorPlanState.test.ts`
- `src/PlanSeat.tsx` — одно место на холсте (абсолютное позиционирование, тон-цвет места).
- `src/PlanSeat.test.tsx`
- `src/FloorPlan.tsx` — холст-вид: SVG-подложка (сетка/зоны/стены) + DOM-слой `PlanSeat`, обёртка зум/пан.
- `src/FloorPlan.test.tsx`

**Изменяются:**
- `src/api/clients/floorMap.ts` — геометрия в `SeatStatusDto`/`FloorMapZoneDto`, новый `FloorMapWallDto` + `walls` в `FloorMapDto`.
- `src/operatorData.ts` — геометрия в `SeatSummary`.
- `src/floorMapState.ts` — `OperatorFloorMapState` хранит `zones`/`walls`; маппинг переносит геометрию мест и сохраняет зоны/стены.
- `src/operatorTypes.ts` — `MapViewMode` `'grid'|'table'` → `'grid'|'plan'`.
- `src/MapWorkspace.tsx` — переключатель `Сетка/План`, ветка `plan` вместо `table`, удаление разметки таблицы и неиспользуемых импортов.
- `src/MapWorkspace.test.tsx` — **создаётся** (тест переключателя видов).
- `locales/ru.json`, `locales/en.json`, `locales/tg.json` — удалить ключи таблицы, добавить ключи плана.
- `packages/i18n/src/messages.ts` — регенерируется (не править руками).
- `src/styles.css` — удалить `.seat-table*`, добавить стили `.floor-plan*` / `.plan-seat*`.

---

## Task 1: Геометрия в фронт-контрактах и SeatSummary

**Files:**
- Modify: `src/api/clients/floorMap.ts`
- Modify: `src/operatorData.ts`

Чистое расширение типов (бэк уже отдаёт эти поля). Отдельного юнит-теста нет — проверяется компилятором; реальное поведение покроет Task 2.

- [ ] **Step 1: Добавить геометрию в DTO контракта**

В `src/api/clients/floorMap.ts` дополнить интерфейсы. В `SeatStatusDto` добавить в конец (после `sessionStartedAtUtc`):

```typescript
  // Floor-plan layout (B2): grid cell + orientation + host type. Null/default until the
  // branch is arranged in the «План» editor; the abstract grid view ignores these.
  posX?: number | null;
  posY?: number | null;
  rotation?: number;
  seatType?: string;
```

Заменить `FloorMapZoneDto` и `FloorMapDto` целиком на:

```typescript
export interface FloorMapDto {
  branchId: Guid;
  branchName: string;
  zones?: FloorMapZoneDto[];
  walls?: FloorMapWallDto[];
  seats: SeatStatusDto[];
}

export interface FloorMapZoneDto {
  zoneId: Guid;
  name: string;
  sortOrder: number;
  // Floor-plan rectangle in grid cells; null until arranged in the «План» editor (B2).
  geoX?: number | null;
  geoY?: number | null;
  geoWidth?: number | null;
  geoHeight?: number | null;
  color?: string | null;
  zoneType?: string | null;
}

export interface FloorMapWallDto {
  wallId: Guid;
  x1: number;
  y1: number;
  x2: number;
  y2: number;
}
```

- [ ] **Step 2: Добавить геометрию в SeatSummary**

В `src/operatorData.ts`, в интерфейс `SeatSummary` добавить в конец (после `sessionStartedAtUtc`):

```typescript
  // Floor-plan geometry (B2 DTO). Null/default until the seat is placed in the «План» editor.
  posX?: number | null;
  posY?: number | null;
  rotation?: number;
  seatType?: string;
```

- [ ] **Step 3: Проверить типы**

Run (из `src/AFK4.Operator.App.Web`): `bun run build`
Expected: сборка проходит без ошибок типов.

- [ ] **Step 4: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/api/clients/floorMap.ts src/AFK4.Operator.App.Web/src/operatorData.ts
git commit -m "feat(operator-map): add floor-plan geometry to frontend floor-map contracts"
```

---

## Task 2: Состояние карты хранит и переносит геометрию

**Files:**
- Modify: `src/floorMapState.ts`
- Test: `src/floorMapState.test.ts`

`OperatorFloorMapState` сейчас выбрасывает зоны и стены — для Плана их надо сохранить. Места уже трансформируются в `SeatSummary`; добавляем перенос геометрии. Зоны/стены храним в DTO-форме (статичная раскладка, реалтайм их не трогает).

- [ ] **Step 1: Написать падающий тест**

В конец `describe('floor-map state', ...)` в `src/floorMapState.test.ts` добавить:

```typescript
  it('carries seat geometry and keeps zones and walls for the plan view', () => {
    const state = mapFloorMapDtoToState({
      branchId,
      branchName: 'Demo Branch',
      zones: [
        { zoneId: '44444444-4444-4444-4444-444444444444', name: 'Зал A', sortOrder: 10, geoX: 0, geoY: 0, geoWidth: 4, geoHeight: 3, color: '#22d3ee', zoneType: 'hall' },
        { zoneId: '55555555-5555-5555-5555-555555555555', name: 'Без геометрии', sortOrder: 20 }
      ],
      walls: [
        { wallId: '66666666-6666-6666-6666-666666666666', x1: 0, y1: 0, x2: 4, y2: 0 }
      ],
      seats: [
        createSeat({ seatName: 'PC-01', sortOrder: 10, state: 'Locked', posX: 1, posY: 2, rotation: 90, seatType: 'console' })
      ]
    }, t);

    expect(state.zones).toHaveLength(2);
    expect(state.walls).toHaveLength(1);
    expect(state.walls[0]).toMatchObject({ x1: 0, y1: 0, x2: 4, y2: 0 });
    expect(state.seats[0]).toMatchObject({ posX: 1, posY: 2, rotation: 90, seatType: 'console' });
  });

  it('defaults zones and walls to empty arrays for fixtures', () => {
    const state = createFixtureFloorMapState();
    expect(state.zones).toEqual([]);
    expect(state.walls).toEqual([]);
  });
```

Проверь, что фабрика `createSeat` в этом файле принимает дополнительные поля (она строит `SeatStatusDto` из частичного объекта — поля `posX/posY/rotation/seatType` просто попадут в DTO). Если `createSeat` использует явный список полей, добавь в него проброс новых полей с дефолтами; ожидаемая текущая сигнатура — `(overrides: Partial<SeatStatusDto>) => SeatStatusDto`, тогда правки не нужны.

- [ ] **Step 2: Запустить тест — убедиться, что падает**

Run: `bun test src/floorMapState.test.ts`
Expected: FAIL — `state.zones`/`state.walls` отсутствуют в типе/значении; `state.seats[0]` без `posX`.

- [ ] **Step 3: Расширить тип состояния**

В `src/floorMapState.ts` импорт DTO дополнить типами зон/стен:

```typescript
import type { FloorMapDto, FloorMapZoneDto, FloorMapWallDto, SeatStatusDto } from './operatorApiClients';
```

В интерфейс `OperatorFloorMapState` добавить поля (после `seats: SeatSummary[];`):

```typescript
  // Static layout geometry for the «План» view (B2). Realtime seat updates never touch these.
  zones: FloorMapZoneDto[];
  walls: FloorMapWallDto[];
```

- [ ] **Step 4: Заполнить zones/walls во всех конструкторах состояния**

В `createFixtureFloorMapState()` в возвращаемый объект добавить:

```typescript
    zones: [],
    walls: [],
```

В `mapFloorMapDtoToState(...)` в возвращаемый объект добавить:

```typescript
    zones: floorMap.zones ?? [],
    walls: floorMap.walls ?? [],
```

В `hydrateFloorMapStateFromCache(...)` в возвращаемый объект добавить:

```typescript
    zones: entry.floorMap.zones ?? [],
    walls: entry.floorMap.walls ?? [],
```

- [ ] **Step 5: Перенести геометрию места в маппинге**

В функции `mapFloorMapSeat(dto, t, loadedAtMs)` в возвращаемый объект `SeatSummary` добавить (рядом с `sessionStartedAtUtc`):

```typescript
    posX: dto.posX ?? null,
    posY: dto.posY ?? null,
    rotation: dto.rotation ?? 0,
    seatType: dto.seatType ?? 'pc',
```

- [ ] **Step 6: Запустить тест — убедиться, что проходит**

Run: `bun test src/floorMapState.test.ts`
Expected: PASS (все тесты файла, включая существующие).

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/floorMapState.ts src/AFK4.Operator.App.Web/src/floorMapState.test.ts
git commit -m "feat(operator-map): keep zones/walls and seat geometry in floor-map state"
```

---

## Task 3: Чистый геометрический модуль

**Files:**
- Create: `src/floorPlanGeometry.ts`
- Test: `src/floorPlanGeometry.test.ts`

Координаты — целочисленные ячейки. Здесь только то, что нужно просмотру: ячейка→пиксели и bounding-box холста. Снап/пиксели→ячейку (`pxToCell`) появятся в B2-3 (drag), сейчас не нужны (YAGNI).

- [ ] **Step 1: Написать падающий тест**

Создать `src/floorPlanGeometry.test.ts`:

```typescript
import { describe, expect, it } from 'bun:test';
import { boundingBox, cellToPx, DEFAULT_CELL_SIZE } from './floorPlanGeometry';

describe('floor-plan geometry', () => {
  it('converts grid cells to pixels with the default cell size', () => {
    expect(cellToPx(0)).toBe(0);
    expect(cellToPx(3)).toBe(3 * DEFAULT_CELL_SIZE);
  });

  it('converts grid cells to pixels with a custom cell size', () => {
    expect(cellToPx(2, 40)).toBe(80);
  });

  it('returns null bounding box when there is nothing to draw', () => {
    expect(boundingBox({ seats: [], zones: [], walls: [] })).toBeNull();
  });

  it('computes a bounding box spanning seats, zones and walls (seats occupy one cell)', () => {
    const box = boundingBox({
      seats: [{ posX: 2, posY: 3 }],
      zones: [{ geoX: 0, geoY: 0, geoWidth: 4, geoHeight: 2 }],
      walls: [{ x1: 5, y1: 1, x2: 5, y2: 6 }]
    });
    expect(box).toEqual({ minX: 0, minY: 0, maxX: 5, maxY: 6 });
  });
});
```

- [ ] **Step 2: Запустить тест — убедиться, что падает**

Run: `bun test src/floorPlanGeometry.test.ts`
Expected: FAIL — модуль не существует.

- [ ] **Step 3: Реализовать модуль**

Создать `src/floorPlanGeometry.ts`:

```typescript
// Floor-plan coordinate system: integer grid cells. Pure helpers so the geometry is unit-testable
// and shared between the read-only «План» view (B2-2) and the editor (B2-3).

export const DEFAULT_CELL_SIZE = 56;
export const CANVAS_PADDING = 32;

export interface BoundingBox {
  minX: number;
  minY: number;
  maxX: number;
  maxY: number;
}

export function cellToPx(cell: number, cellSize: number = DEFAULT_CELL_SIZE): number {
  return cell * cellSize;
}

// Outer extent (in cells) of everything to draw. A seat occupies a single cell, so its right/bottom
// edge is +1. Returns null when there is nothing positioned — the caller shows an empty state.
export function boundingBox(inputs: {
  seats: { posX: number; posY: number }[];
  zones: { geoX: number; geoY: number; geoWidth: number; geoHeight: number }[];
  walls: { x1: number; y1: number; x2: number; y2: number }[];
}): BoundingBox | null {
  const xs: number[] = [];
  const ys: number[] = [];

  for (const seat of inputs.seats) {
    xs.push(seat.posX, seat.posX + 1);
    ys.push(seat.posY, seat.posY + 1);
  }
  for (const zone of inputs.zones) {
    xs.push(zone.geoX, zone.geoX + zone.geoWidth);
    ys.push(zone.geoY, zone.geoY + zone.geoHeight);
  }
  for (const wall of inputs.walls) {
    xs.push(wall.x1, wall.x2);
    ys.push(wall.y1, wall.y2);
  }

  if (xs.length === 0) {
    return null;
  }

  return {
    minX: Math.min(...xs),
    minY: Math.min(...ys),
    maxX: Math.max(...xs),
    maxY: Math.max(...ys)
  };
}
```

- [ ] **Step 4: Запустить тест — убедиться, что проходит**

Run: `bun test src/floorPlanGeometry.test.ts`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/floorPlanGeometry.ts src/AFK4.Operator.App.Web/src/floorPlanGeometry.test.ts
git commit -m "feat(operator-map): add pure floor-plan geometry helpers"
```

---

## Task 4: Плоская модель плана (`toPlanModel`)

**Files:**
- Create: `src/floorPlanState.ts`
- Test: `src/floorPlanState.test.ts`

Превращаем `OperatorFloorMapState` в готовую к рендеру модель: размещённые места (есть `posX`+`posY`), неразмещённые, зоны с геометрией, стены, bbox, флаг пустоты.

- [ ] **Step 1: Написать падающий тест**

Создать `src/floorPlanState.test.ts`:

```typescript
import { describe, expect, it } from 'bun:test';
import { createTranslator } from '@afk4/i18n';
import { mapFloorMapDtoToState } from './floorMapState';
import { toPlanModel } from './floorPlanState';

const branchId = 'acfc0212-967f-4d84-94be-9003387b09c2';
const t = createTranslator('ru');

function seat(overrides: Record<string, unknown>) {
  return {
    seatId: '00000000-0000-0000-0000-000000000000',
    seatName: 'PC',
    zoneId: '44444444-4444-4444-4444-444444444444',
    zoneName: 'Зал A',
    sortOrder: 10,
    state: 'Locked',
    ...overrides
  };
}

describe('floor-plan model', () => {
  it('splits seats into placed and unplaced, keeps geometric zones and walls', () => {
    const state = mapFloorMapDtoToState({
      branchId,
      branchName: 'Demo',
      zones: [
        { zoneId: '44444444-4444-4444-4444-444444444444', name: 'Зал A', sortOrder: 10, geoX: 0, geoY: 0, geoWidth: 4, geoHeight: 3, color: '#22d3ee', zoneType: 'hall' },
        { zoneId: '55555555-5555-5555-5555-555555555555', name: 'Без гео', sortOrder: 20 }
      ],
      walls: [{ wallId: '66666666-6666-6666-6666-666666666666', x1: 0, y1: 0, x2: 4, y2: 0 }],
      seats: [
        seat({ seatId: '11111111-1111-1111-1111-111111111111', seatName: 'PC-01', posX: 1, posY: 1, seatType: 'pc' }),
        seat({ seatId: '22222222-2222-2222-2222-222222222222', seatName: 'PC-02' })
      ]
    }, t);

    const model = toPlanModel(state);

    expect(model.placedSeats.map((s) => s.name)).toEqual(['PC-01']);
    expect(model.placedSeats[0]).toMatchObject({ posX: 1, posY: 1, seatType: 'pc' });
    expect(model.unplacedSeats.map((s) => s.name)).toEqual(['PC-02']);
    expect(model.zones).toHaveLength(1);
    expect(model.zones[0]).toMatchObject({ name: 'Зал A', geoWidth: 4, geoHeight: 3 });
    expect(model.walls).toHaveLength(1);
    expect(model.isEmpty).toBe(false);
    expect(model.bbox).not.toBeNull();
  });

  it('is empty when nothing has coordinates', () => {
    const state = mapFloorMapDtoToState({
      branchId,
      branchName: 'Demo',
      seats: [seat({ seatName: 'PC-01' })]
    }, t);

    const model = toPlanModel(state);

    expect(model.placedSeats).toEqual([]);
    expect(model.unplacedSeats).toHaveLength(1);
    expect(model.isEmpty).toBe(true);
    expect(model.bbox).toBeNull();
  });
});
```

- [ ] **Step 2: Запустить тест — убедиться, что падает**

Run: `bun test src/floorPlanState.test.ts`
Expected: FAIL — `toPlanModel` не существует.

- [ ] **Step 3: Реализовать модуль**

Создать `src/floorPlanState.ts`:

```typescript
import type { OperatorFloorMapState } from './floorMapState';
import { boundingBox, type BoundingBox } from './floorPlanGeometry';
import type { SeatSummary, SeatTone } from './operatorData';

// A seat positioned on the plan canvas. Carries just what the canvas needs to draw it.
export interface PlanSeat {
  id: string;
  name: string;
  tone: SeatTone;
  stateLabel: string;
  seatType: string;
  rotation: number;
  posX: number;
  posY: number;
}

export interface PlanZone {
  id: string;
  name: string;
  geoX: number;
  geoY: number;
  geoWidth: number;
  geoHeight: number;
  color: string | null;
  zoneType: string | null;
}

export interface Wall {
  id: string;
  x1: number;
  y1: number;
  x2: number;
  y2: number;
}

export interface PlanModel {
  placedSeats: PlanSeat[];
  unplacedSeats: SeatSummary[];
  zones: PlanZone[];
  walls: Wall[];
  bbox: BoundingBox | null;
  isEmpty: boolean;
}

// Project the live floor-map state into a flat, render-ready plan model. Seats without coordinates
// fall into `unplacedSeats` (still visible in the grid view); zones without a full rectangle are
// dropped from the canvas. `isEmpty` drives the «зал ещё не размечен» empty state.
export function toPlanModel(state: OperatorFloorMapState): PlanModel {
  const placedSeats: PlanSeat[] = [];
  const unplacedSeats: SeatSummary[] = [];

  for (const seat of state.seats) {
    if (seat.posX != null && seat.posY != null) {
      placedSeats.push({
        id: seat.id,
        name: seat.name,
        tone: seat.tone,
        stateLabel: seat.stateLabel,
        seatType: seat.seatType ?? 'pc',
        rotation: seat.rotation ?? 0,
        posX: seat.posX,
        posY: seat.posY
      });
    } else {
      unplacedSeats.push(seat);
    }
  }

  const zones: PlanZone[] = state.zones
    .filter((zone) => zone.geoX != null && zone.geoY != null && zone.geoWidth != null && zone.geoHeight != null)
    .map((zone) => ({
      id: zone.zoneId,
      name: zone.name,
      geoX: zone.geoX as number,
      geoY: zone.geoY as number,
      geoWidth: zone.geoWidth as number,
      geoHeight: zone.geoHeight as number,
      color: zone.color ?? null,
      zoneType: zone.zoneType ?? null
    }));

  const walls: Wall[] = state.walls.map((wall) => ({
    id: wall.wallId,
    x1: wall.x1,
    y1: wall.y1,
    x2: wall.x2,
    y2: wall.y2
  }));

  const bbox = boundingBox({ seats: placedSeats, zones, walls });

  return {
    placedSeats,
    unplacedSeats,
    zones,
    walls,
    bbox,
    isEmpty: bbox === null
  };
}
```

- [ ] **Step 4: Запустить тест — убедиться, что проходит**

Run: `bun test src/floorPlanState.test.ts`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/floorPlanState.ts src/AFK4.Operator.App.Web/src/floorPlanState.test.ts
git commit -m "feat(operator-map): build flat plan model from floor-map state"
```

---

## Task 5: Компонент места на холсте (`PlanSeat`)

**Files:**
- Create: `src/PlanSeat.tsx`
- Test: `src/PlanSeat.test.tsx`
- Modify: `src/styles.css`

Маркер места: абсолютное позиционирование по координатам, цвет по тону (переиспользуем `state-${tone}` визуальный язык), клик = выбор, правый клик = контекст-меню (паритет с Сеткой).

- [ ] **Step 1: Написать падающий тест**

Создать `src/PlanSeat.test.tsx`:

```typescript
import { describe, expect, it } from 'bun:test';
import { render, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { PlanSeat } from './PlanSeat';
import type { PlanSeat as PlanSeatModel } from './floorPlanState';

function model(overrides: Partial<PlanSeatModel> = {}): PlanSeatModel {
  return {
    id: 'pc-01',
    name: 'PC-01',
    tone: 'active',
    stateLabel: 'В сессии',
    seatType: 'pc',
    rotation: 0,
    posX: 2,
    posY: 3,
    ...overrides
  };
}

function renderSeat(seat: PlanSeatModel, props: { selected?: boolean; onSelect?: () => void } = {}) {
  return render(
    <I18nProvider>
      <PlanSeat seat={seat} cellSize={56} selected={props.selected} onSelect={props.onSelect ?? (() => {})} />
    </I18nProvider>
  );
}

describe('PlanSeat', () => {
  it('positions the seat by its grid cell and labels it by name and status', () => {
    const { getByRole } = renderSeat(model());
    const button = getByRole('button');
    expect(button.style.left).toBe(`${2 * 56}px`);
    expect(button.style.top).toBe(`${3 * 56}px`);
    expect(button.getAttribute('aria-label')).toBe('PC-01 В сессии');
    expect(button.className).toContain('state-active');
  });

  it('marks the selected seat and fires onSelect on click', () => {
    let clicked = false;
    const { getByRole } = renderSeat(model(), { selected: true, onSelect: () => { clicked = true; } });
    const button = getByRole('button');
    expect(button.className).toContain('selected');
    fireEvent.click(button);
    expect(clicked).toBe(true);
  });
});
```

- [ ] **Step 2: Запустить тест — убедиться, что падает**

Run: `bun test src/PlanSeat.test.tsx`
Expected: FAIL — `PlanSeat` не существует.

- [ ] **Step 3: Реализовать компонент**

Создать `src/PlanSeat.tsx`:

```tsx
import type { CSSProperties, MouseEvent as ReactMouseEvent } from 'react';
import { cellToPx } from './floorPlanGeometry';
import type { PlanSeat as PlanSeatModel } from './floorPlanState';

// One seat on the plan canvas. Reuses the tone language of the grid tile (`state-${tone}`), but is a
// compact positioned marker. Rotation is carried in the model but not applied visually here — that
// lands in the editor (B2-3), so the label stays upright and readable in the read-only view.
export function PlanSeat({
  seat,
  cellSize,
  selected,
  onSelect,
  onContextMenu
}: {
  seat: PlanSeatModel;
  cellSize: number;
  selected?: boolean;
  onSelect: () => void;
  onContextMenu?: (event: ReactMouseEvent) => void;
}) {
  const className = ['plan-seat', `state-${seat.tone}`, selected ? 'selected' : ''].filter(Boolean).join(' ');
  const style: CSSProperties = {
    left: `${cellToPx(seat.posX, cellSize)}px`,
    top: `${cellToPx(seat.posY, cellSize)}px`
  };

  return (
    <button
      type="button"
      className={className}
      style={style}
      aria-label={`${seat.name} ${seat.stateLabel}`}
      aria-pressed={selected}
      onClick={onSelect}
      onContextMenu={onContextMenu}
    >
      <span className="plan-seat-dot" aria-hidden="true" />
      <span className="plan-seat-name">{seat.name}</span>
    </button>
  );
}
```

- [ ] **Step 4: Запустить тест — убедиться, что проходит**

Run: `bun test src/PlanSeat.test.tsx`
Expected: PASS.

- [ ] **Step 5: Добавить стили `PlanSeat`**

В `src/styles.css` добавить блок (рядом с прочими стилями карты; цвет тона берётся из существующей переменной `--seat-color`, которую задают правила `.state-${tone}` — проверь, что `.plan-seat.state-*` их наследует; правила цвета привязаны к `.seat-tile.state-*`, поэтому продублируй привязку переменной для `.plan-seat`):

```css
/* ===== Floor plan: seat markers ===== */
.plan-seat {
  position: absolute;
  display: flex;
  align-items: center;
  gap: 6px;
  width: 48px;
  height: 48px;
  padding: 4px 6px;
  border: 1px solid color-mix(in srgb, var(--seat-color, var(--accent)) 60%, transparent);
  border-radius: 10px;
  background: color-mix(in srgb, var(--seat-color, var(--accent)) 16%, var(--surface, #0f172a));
  color: var(--text-primary, #e2e8f0);
  font: inherit;
  cursor: pointer;
  box-sizing: border-box;
  overflow: hidden;
}
.plan-seat.state-ready { --seat-color: var(--accent); }
.plan-seat.state-active { --seat-color: var(--text-tertiary, #94a3b8); }
.plan-seat.state-pending { --seat-color: color-mix(in srgb, var(--warning, #f59e0b) 70%, #fff); }
.plan-seat.state-blocking { --seat-color: color-mix(in srgb, var(--danger, #ef4444) 68%, #fff); }
.plan-seat.state-offline { --seat-color: #64748b; }
.plan-seat:hover,
.plan-seat:focus-visible {
  outline: 2px solid var(--seat-color, var(--accent));
  outline-offset: 1px;
}
.plan-seat.selected {
  outline: 2px solid var(--accent);
  outline-offset: 2px;
}
.plan-seat-dot {
  flex: 0 0 auto;
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: var(--seat-color, var(--accent));
}
.plan-seat-name {
  font-size: 11px;
  font-weight: 600;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
```

- [ ] **Step 6: Проверить сборку**

Run: `bun run build`
Expected: проходит.

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/PlanSeat.tsx src/AFK4.Operator.App.Web/src/PlanSeat.test.tsx src/AFK4.Operator.App.Web/src/styles.css
git commit -m "feat(operator-map): add positioned PlanSeat marker for the plan canvas"
```

---

## Task 6: Холст плана (`FloorPlan`)

**Files:**
- Create: `src/FloorPlan.tsx`
- Test: `src/FloorPlan.test.tsx`
- Modify: `src/styles.css`

Холст: SVG-подложка рисует сетку, зоны (rect + подпись) и стены (line); поверх — DOM-слой `PlanSeat`. Зум (колесо + кнопки) и пан (drag по пустому) через `transform` на обёртке. Холст предполагает непустую модель — пустое состояние решает `MapWorkspace`.

- [ ] **Step 1: Написать падающий тест**

Создать `src/FloorPlan.test.tsx`:

```typescript
import { describe, expect, it } from 'bun:test';
import { render, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { FloorPlan } from './FloorPlan';
import type { PlanModel } from './floorPlanState';

function nonEmptyModel(): PlanModel {
  return {
    placedSeats: [
      { id: 'pc-01', name: 'PC-01', tone: 'active', stateLabel: 'В сессии', seatType: 'pc', rotation: 0, posX: 1, posY: 1 },
      { id: 'pc-02', name: 'PC-02', tone: 'ready', stateLabel: 'Свободно', seatType: 'pc', rotation: 0, posX: 3, posY: 1 }
    ],
    unplacedSeats: [],
    zones: [
      { id: 'z1', name: 'Зал A', geoX: 0, geoY: 0, geoWidth: 5, geoHeight: 3, color: '#22d3ee', zoneType: 'hall' }
    ],
    walls: [{ id: 'w1', x1: 0, y1: 0, x2: 5, y2: 0 }],
    bbox: { minX: 0, minY: 0, maxX: 5, maxY: 3 },
    isEmpty: false
  };
}

function renderPlan(model: PlanModel, onSelectSeat: (id: string) => void = () => {}) {
  return render(
    <I18nProvider>
      <FloorPlan model={model} selectedSeatId="pc-01" onSelectSeat={onSelectSeat} />
    </I18nProvider>
  );
}

describe('FloorPlan', () => {
  it('renders a seat marker per placed seat and labels the zone', () => {
    const { getByRole, getByText } = renderPlan(nonEmptyModel());
    expect(getByRole('button', { name: 'PC-01 В сессии' })).not.toBeNull();
    expect(getByRole('button', { name: 'PC-02 Свободно' })).not.toBeNull();
    expect(getByText('Зал A')).not.toBeNull();
  });

  it('fires onSelectSeat with the seat id on click', () => {
    let picked = '';
    const { getByRole } = renderPlan(nonEmptyModel(), (id) => { picked = id; });
    fireEvent.click(getByRole('button', { name: 'PC-02 Свободно' }));
    expect(picked).toBe('pc-02');
  });

  it('marks the selected seat', () => {
    const { getByRole } = renderPlan(nonEmptyModel());
    expect(getByRole('button', { name: 'PC-01 В сессии' }).className).toContain('selected');
  });
});
```

- [ ] **Step 2: Запустить тест — убедиться, что падает**

Run: `bun test src/FloorPlan.test.tsx`
Expected: FAIL — `FloorPlan` не существует.

- [ ] **Step 3: Реализовать компонент**

Создать `src/FloorPlan.tsx`:

```tsx
import { useState } from 'react';
import type { MouseEvent as ReactMouseEvent, PointerEvent as ReactPointerEvent, WheelEvent as ReactWheelEvent } from 'react';
import { Minus, Plus } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import { cellToPx, CANVAS_PADDING, DEFAULT_CELL_SIZE } from './floorPlanGeometry';
import type { PlanModel } from './floorPlanState';
import { PlanSeat } from './PlanSeat';

const MIN_SCALE = 0.4;
const MAX_SCALE = 2.5;

// Top-down floor-plan canvas (read-only in B2-2): an SVG underlay (grid, zones, walls) with a DOM
// layer of seat markers on top. Pan by dragging empty space, zoom with the wheel or the +/- buttons.
// Assumes a non-empty model — the caller renders the «зал ещё не размечен» empty state instead.
export function FloorPlan({
  model,
  selectedSeatId,
  onSelectSeat,
  onSeatContextMenu
}: {
  model: PlanModel;
  selectedSeatId: string;
  onSelectSeat: (seatId: string) => void;
  onSeatContextMenu?: (seatId: string, event: ReactMouseEvent) => void;
}) {
  const { t } = useI18n();
  const [scale, setScale] = useState(1);
  const [pan, setPan] = useState({ x: 0, y: 0 });
  const [drag, setDrag] = useState<{ startX: number; startY: number; panX: number; panY: number } | null>(null);

  const bbox = model.bbox ?? { minX: 0, minY: 0, maxX: 1, maxY: 1 };
  const cell = DEFAULT_CELL_SIZE;
  const originX = bbox.minX;
  const originY = bbox.minY;
  const widthPx = cellToPx(bbox.maxX - originX, cell) + CANVAS_PADDING * 2;
  const heightPx = cellToPx(bbox.maxY - originY, cell) + CANVAS_PADDING * 2;
  // Map an absolute cell to a pixel inside the padded canvas (origin shifted so nothing is negative).
  const px = (c: number) => cellToPx(c - originX, cell) + CANVAS_PADDING;
  const py = (c: number) => cellToPx(c - originY, cell) + CANVAS_PADDING;

  const gridLines: { key: string; x1: number; y1: number; x2: number; y2: number }[] = [];
  for (let x = originX; x <= bbox.maxX; x += 1) {
    gridLines.push({ key: `v-${x}`, x1: px(x), y1: py(originY), x2: px(x), y2: py(bbox.maxY) });
  }
  for (let y = originY; y <= bbox.maxY; y += 1) {
    gridLines.push({ key: `h-${y}`, x1: px(originX), y1: py(y), x2: px(bbox.maxX), y2: py(y) });
  }

  const zoom = (delta: number) => setScale((s) => Math.min(MAX_SCALE, Math.max(MIN_SCALE, s + delta)));

  const onWheel = (event: ReactWheelEvent) => {
    event.preventDefault();
    zoom(event.deltaY < 0 ? 0.1 : -0.1);
  };

  // Pan only when the drag starts on empty canvas (not on a seat marker — those handle their own click).
  const onPointerDown = (event: ReactPointerEvent) => {
    if ((event.target as HTMLElement).closest('.plan-seat')) {
      return;
    }
    setDrag({ startX: event.clientX, startY: event.clientY, panX: pan.x, panY: pan.y });
  };
  const onPointerMove = (event: ReactPointerEvent) => {
    if (drag === null) {
      return;
    }
    setPan({ x: drag.panX + (event.clientX - drag.startX), y: drag.panY + (event.clientY - drag.startY) });
  };
  const endDrag = () => setDrag(null);

  return (
    <div className="floor-plan">
      <div className="floor-plan-toolbar">
        <button type="button" aria-label={t('op.map.plan.zoomOut')} onClick={() => zoom(-0.2)}><Minus size={16} /></button>
        <button type="button" aria-label={t('op.map.plan.zoomReset')} onClick={() => { setScale(1); setPan({ x: 0, y: 0 }); }}>{Math.round(scale * 100)}%</button>
        <button type="button" aria-label={t('op.map.plan.zoomIn')} onClick={() => zoom(0.2)}><Plus size={16} /></button>
      </div>
      <div
        className="floor-plan-viewport"
        role="application"
        aria-label={t('op.map.plan.canvasLabel')}
        onWheel={onWheel}
        onPointerDown={onPointerDown}
        onPointerMove={onPointerMove}
        onPointerUp={endDrag}
        onPointerLeave={endDrag}
      >
        <div
          className="floor-plan-canvas"
          style={{ width: `${widthPx}px`, height: `${heightPx}px`, transform: `translate(${pan.x}px, ${pan.y}px) scale(${scale})` }}
        >
          <svg className="floor-plan-underlay" width={widthPx} height={heightPx} aria-hidden="true">
            <g className="floor-plan-grid">
              {gridLines.map((line) => (
                <line key={line.key} x1={line.x1} y1={line.y1} x2={line.x2} y2={line.y2} />
              ))}
            </g>
            {model.zones.map((zone) => (
              <rect
                key={zone.id}
                className="floor-plan-zone"
                x={px(zone.geoX)}
                y={py(zone.geoY)}
                width={cellToPx(zone.geoWidth, cell)}
                height={cellToPx(zone.geoHeight, cell)}
                style={zone.color ? { stroke: zone.color, fill: `color-mix(in srgb, ${zone.color} 12%, transparent)` } : undefined}
              />
            ))}
            {model.walls.map((wall) => (
              <line key={wall.id} className="floor-plan-wall" x1={px(wall.x1)} y1={py(wall.y1)} x2={px(wall.x2)} y2={py(wall.y2)} />
            ))}
          </svg>
          {model.zones.map((zone) => (
            <span
              key={`label-${zone.id}`}
              className="floor-plan-zone-label"
              style={{ left: `${px(zone.geoX) + 6}px`, top: `${py(zone.geoY) + 4}px` }}
            >
              {zone.name}
            </span>
          ))}
          {model.placedSeats.map((seat) => (
            <PlanSeat
              key={seat.id}
              seat={{ ...seat, posX: seat.posX - originX, posY: seat.posY - originY }}
              cellSize={cell}
              selected={seat.id === selectedSeatId}
              onSelect={() => onSelectSeat(seat.id)}
              onContextMenu={onSeatContextMenu ? (event) => onSeatContextMenu(seat.id, event) : undefined}
            />
          ))}
        </div>
      </div>
    </div>
  );
}
```

Заметь: `PlanSeat` позиционирует по `cellToPx(posX)`, без отступа `CANVAS_PADDING`. Чтобы маркеры совпали с SVG-сеткой (которая рисуется с `CANVAS_PADDING`), оберни DOM-слой так, чтобы у него был тот же отступ. Сделай это стилем: `.floor-plan-canvas` позиционирует SVG в (0,0), а маркеры — абсолютные внутри канваса; добавь к каждому маркеру тот же `CANVAS_PADDING`. Проще всего — передавать в `PlanSeat` уже нормализованные координаты (`posX - originX`) и добавить `CANVAS_PADDING` отступом самого DOM-слоя. Реализация выше передаёт нормализованные координаты; компенсацию `CANVAS_PADDING` задай в CSS через `.floor-plan-canvas` (padding) либо оберни маркеры в отдельный слой со смещением. В тесте проверяется только наличие/клик/selected (не пиксельное совпадение), поэтому функционально тест пройдёт; визуальное совпадение проверь в Step 6.

- [ ] **Step 4: Запустить тест — убедиться, что проходит**

Run: `bun test src/FloorPlan.test.tsx`
Expected: PASS.

- [ ] **Step 5: Добавить стили холста**

В `src/styles.css` добавить:

```css
/* ===== Floor plan: canvas ===== */
.floor-plan {
  position: relative;
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 0;
}
.floor-plan-toolbar {
  display: flex;
  gap: 4px;
  align-self: flex-end;
  margin-bottom: 8px;
}
.floor-plan-toolbar button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 36px;
  height: 32px;
  padding: 0 8px;
  border: 1px solid var(--border, #1e293b);
  border-radius: 8px;
  background: var(--surface, #0f172a);
  color: var(--text-primary, #e2e8f0);
  cursor: pointer;
}
.floor-plan-viewport {
  position: relative;
  flex: 1;
  min-height: 0;
  overflow: hidden;
  border: 1px solid var(--border, #1e293b);
  border-radius: 12px;
  background: var(--surface-sunken, #0b1120);
  touch-action: none;
}
.floor-plan-canvas {
  position: absolute;
  top: 0;
  left: 0;
  transform-origin: 0 0;
}
.floor-plan-underlay {
  position: absolute;
  top: 0;
  left: 0;
}
.floor-plan-grid line {
  stroke: color-mix(in srgb, var(--border, #1e293b) 60%, transparent);
  stroke-width: 1;
}
.floor-plan-zone {
  fill: color-mix(in srgb, var(--accent, #22d3ee) 8%, transparent);
  stroke: color-mix(in srgb, var(--accent, #22d3ee) 40%, transparent);
  stroke-width: 1.5;
  rx: 8;
}
.floor-plan-wall {
  stroke: var(--text-primary, #e2e8f0);
  stroke-width: 4;
  stroke-linecap: round;
}
.floor-plan-zone-label {
  position: absolute;
  font-size: 11px;
  font-weight: 600;
  color: var(--text-secondary, #94a3b8);
  pointer-events: none;
}
```

- [ ] **Step 6: Визуальная проверка совмещения слоёв**

Run: `bun run dev`, открой Карту, переключись на «План» с тестовыми координатами (или временно подставь координаты в фикстуры `operatorData.ts` для проверки, затем откати).
Expected: маркеры мест стоят в узлах SVG-сетки; зоны/стены/подписи на местах; зум и пан работают. Если есть рассинхрон отступа `CANVAS_PADDING` между SVG и DOM-слоем — поправь в CSS (`.floor-plan-canvas` padding или смещение DOM-слоя), как описано в Step 3.

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/FloorPlan.tsx src/AFK4.Operator.App.Web/src/FloorPlan.test.tsx src/AFK4.Operator.App.Web/src/styles.css
git commit -m "feat(operator-map): add read-only top-down floor-plan canvas"
```

---

## Task 7: i18n — заменить ключи таблицы на ключи плана

**Files:**
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json` (от корня репо)
- Regenerate: `packages/i18n/src/messages.ts`

Удаляем ключи таблицы, добавляем ключи плана во всех трёх локалях (тест паритета `packages/i18n/src/messages.test.ts` требует одинакового набора ключей).

> ⚠️ **Таджикский:** строки ниже — добросовестный черновик, не ru→tg копипаст. Пользователь — носитель/доменный эксперт по таджикскому: после имплементации показать ему именно tg-значения на подтверждение (см. WORKING-STYLE #37/#38). Не помечать задачу «готово» как фейк-паритет — это реальные переводы, но требуют сверки носителем.

- [ ] **Step 1: Удалить ключи таблицы во всех трёх локалях**

В `locales/ru.json`, `locales/en.json`, `locales/tg.json` удалить строки с ключами:
`op.map.viewTable`, `op.map.tableLabel`, `op.map.colPc`, `op.map.colState`, `op.map.colPlayer`, `op.map.colRemaining`, `op.map.colDevice`, `op.map.colCommand`, `op.map.colBilling`.

Оставить `op.map.viewGrid` и `op.map.viewLabel`.

- [ ] **Step 2: Добавить ключи плана**

Рядом с `op.map.viewGrid` добавить в **`ru.json`**:

```json
  "op.map.viewPlan": "План",
  "op.map.plan.canvasLabel": "План зала",
  "op.map.plan.zoomIn": "Приблизить",
  "op.map.plan.zoomOut": "Отдалить",
  "op.map.plan.zoomReset": "Сбросить масштаб",
  "op.map.plan.emptyTitle": "Зал ещё не размечен",
  "op.map.plan.emptyHint": "Пока расстановка не задана, пользуйтесь видом «Карта». Разметку плана добавим в режиме редактирования.",
  "op.map.plan.unplacedNote": "Не на плане: {count}",
```

В **`en.json`**:

```json
  "op.map.viewPlan": "Plan",
  "op.map.plan.canvasLabel": "Floor plan",
  "op.map.plan.zoomIn": "Zoom in",
  "op.map.plan.zoomOut": "Zoom out",
  "op.map.plan.zoomReset": "Reset zoom",
  "op.map.plan.emptyTitle": "Floor not arranged yet",
  "op.map.plan.emptyHint": "Until the layout is set up, use the «Map» view. Plan arrangement is coming in edit mode.",
  "op.map.plan.unplacedNote": "Off plan: {count}",
```

В **`tg.json`** (черновик для сверки носителем):

```json
  "op.map.viewPlan": "Нақша",
  "op.map.plan.canvasLabel": "Нақшаи толор",
  "op.map.plan.zoomIn": "Калонтар",
  "op.map.plan.zoomOut": "Хурдтар",
  "op.map.plan.zoomReset": "Бозсозии андоза",
  "op.map.plan.emptyTitle": "Толор ҳанӯз ҷойгир нашудааст",
  "op.map.plan.emptyHint": "То он даме ки ҷойгиршавӣ муайян нашудааст, аз намуди «Харита» истифода баред. Ҷойгиркунии нақша дар реҷаи таҳрир илова мешавад.",
  "op.map.plan.unplacedNote": "Берун аз нақша: {count}",
```

- [ ] **Step 3: Регенерировать messages.ts**

Run (из `packages/i18n`): `bun run gen`
Expected: `packages/i18n/src/messages.ts` обновлён (новые ключи есть, ключи таблицы исчезли).

- [ ] **Step 4: Прогнать тест паритета i18n**

Run (из `packages/i18n`): `bun test src/messages.test.ts`
Expected: PASS — все три локали имеют одинаковый набор ключей, `messages.ts` совпадает с источником.

- [ ] **Step 5: Commit**

```bash
git add locales/ru.json locales/en.json locales/tg.json packages/i18n/src/messages.ts
git commit -m "i18n(operator-map): swap floor-map table keys for plan-view keys"
```

---

## Task 8: Подключить «План» в MapWorkspace и убрать Таблицу

**Files:**
- Modify: `src/operatorTypes.ts`
- Modify: `src/MapWorkspace.tsx`
- Create: `src/MapWorkspace.test.tsx`
- Modify: `src/styles.css`

Меняем тип вида, переключатель, ветку рендера; «План» игнорирует фильтр и решает пустое состояние через `planModel.isEmpty`. Удаляем разметку таблицы, неиспользуемые импорты и CSS таблицы.

- [ ] **Step 1: Сменить тип вида**

В `src/operatorTypes.ts` заменить:

```typescript
export type MapViewMode = 'grid' | 'table';
```

на:

```typescript
export type MapViewMode = 'grid' | 'plan';
```

- [ ] **Step 2: Написать падающий тест переключателя**

Создать `src/MapWorkspace.test.tsx`:

```typescript
import { describe, expect, it } from 'bun:test';
import { render, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { MapWorkspace } from './MapWorkspace';
import { createFixtureFloorMapState } from './floorMapState';

function renderWorkspace() {
  return render(
    <I18nProvider>
      <MapWorkspace
        floorMap={createFixtureFloorMapState()}
        session={null}
        actionsEnabled={false}
        selectedSeatId=""
        activeFilter="all"
        offlineActionAudit={[]}
        onSelectSeat={() => {}}
        onFilterChange={() => {}}
        onPcControlAction={async () => ({ detail: '' })}
        onSeatAction={async () => ({})}
      />
    </I18nProvider>
  );
}

describe('MapWorkspace view switch', () => {
  it('offers Grid and Plan views and no Table view', () => {
    const { getByText, queryByText } = renderWorkspace();
    expect(getByText('Карта')).not.toBeNull();
    expect(getByText('План')).not.toBeNull();
    expect(queryByText('Таблица')).toBeNull();
  });

  it('switches to the plan view and shows the not-arranged empty state for unplaced fixtures', () => {
    const { getByText } = renderWorkspace();
    fireEvent.click(getByText('План'));
    // Fixtures carry no coordinates → plan is empty.
    expect(getByText('Зал ещё не размечен')).not.toBeNull();
  });
});
```

- [ ] **Step 3: Запустить тест — убедиться, что падает**

Run: `bun test src/MapWorkspace.test.tsx`
Expected: FAIL — кнопки «План» нет, ветка plan не реализована.

- [ ] **Step 4: Обновить импорты MapWorkspace**

В `src/MapWorkspace.tsx` в импорте из `./operatorHelpers` удалить ставшие неиспользуемыми `billingLabel`, `commandLabel`, `deviceStatusLabel`, `toneLabel` (они остаются определёнными в `operatorHelpers.ts` и используются в `MapSidePanel`/`operatorHelpers` — удаляем только из этого файла). Оставить: `countByMapFilter`, `emptyFeedback`, `guestBillingSelection`, `mapFilterOptions`, `matchesMapFilter`, `projectOperatorFacingError`, `zoneLabel`.

Добавить импорты модулей плана (после `import { SeatTile } from './SeatTile';`):

```typescript
import { FloorPlan } from './FloorPlan';
import { toPlanModel } from './floorPlanState';
```

- [ ] **Step 5: Сменить переключатель видов**

Заменить блок переключателя (строки с `map-view-switch`):

```tsx
        <div className="filter-row map-view-switch" aria-label={t('op.map.viewLabel')}>
          <button type="button" className={viewMode === 'grid' ? 'active' : undefined} onClick={() => setViewMode('grid')}>{t('op.map.viewGrid')}</button>
          <button type="button" className={viewMode === 'table' ? 'active' : undefined} onClick={() => setViewMode('table')}>{t('op.map.viewTable')}</button>
        </div>
```

на:

```tsx
        <div className="filter-row map-view-switch" aria-label={t('op.map.viewLabel')}>
          <button type="button" className={viewMode === 'grid' ? 'active' : undefined} onClick={() => setViewMode('grid')}>{t('op.map.viewGrid')}</button>
          <button type="button" className={viewMode === 'plan' ? 'active' : undefined} onClick={() => setViewMode('plan')}>{t('op.map.viewPlan')}</button>
        </div>
```

- [ ] **Step 6: Построить модель плана**

После строки `const selectedSeat = floorMap.seats.find((seat) => seat.id === selectedSeatId) ?? null;` добавить:

```tsx
  const planModel = useMemo(() => toPlanModel(floorMap), [floorMap]);
```

- [ ] **Step 7: Сменить className доски**

Заменить:

```tsx
      <section className={`map-board ${viewMode === 'table' ? 'table-mode' : ''}`} aria-label={t('op.map.seatsLabel')}>
```

на:

```tsx
      <section className={`map-board ${viewMode === 'plan' ? 'plan-mode' : ''}`} aria-label={t('op.map.seatsLabel')}>
```

- [ ] **Step 8: Заменить ветку рендера table на plan**

Заменить весь условный блок рендера внутри `<section className="map-board ...">` (от `{isLoadingSeats ? (` до закрывающего `)}` перед `</section>`) на:

```tsx
        {isLoadingSeats ? (
          showSeatSkeleton ? (
            <div className="seat-grid" role="status" aria-label={t('op.map.loading')}>
              {Array.from({ length: 10 }).map((_, index) => (
                <Skeleton key={index} className="seat-skeleton" />
              ))}
            </div>
          ) : null
        ) : viewMode === 'plan' ? (
          planModel.isEmpty ? (
            <EmptyState title={t('op.map.plan.emptyTitle')} description={t('op.map.plan.emptyHint')} className="map-empty-state" />
          ) : (
            <>
              <FloorPlan
                model={planModel}
                selectedSeatId={selectedSeatId}
                onSelectSeat={onSelectSeat}
                onSeatContextMenu={(seatId, event) => {
                  const seat = floorMap.seats.find((candidate) => candidate.id === seatId);
                  if (seat) {
                    openSeatMenu(seat, event);
                  }
                }}
              />
              {planModel.unplacedSeats.length > 0 && (
                <p className="floor-plan-unplaced">{t('op.map.plan.unplacedNote', { count: planModel.unplacedSeats.length })}</p>
              )}
            </>
          )
        ) : visibleSeats.length === 0 ? (
          <EmptyState title={t('op.map.emptyTitle')} description={t('op.map.emptyHint')} className="map-empty-state" />
        ) : (
          <div className="seat-zones">
            {zoneGroups.map((group) => (
              <section className="zone-group" key={group.zone}>
                <header className="zone-group-head">
                  <span>{zoneLabel(group.zone, t)}</span>
                  <strong>{group.seats.length}</strong>
                </header>
                <div className="seat-grid">
                  {group.seats.map((seat) => (
                    <SeatTile
                      key={seat.id}
                      seat={seat}
                      selected={seat.id === selectedSeatId}
                      onSelect={() => onSelectSeat(seat.id)}
                      onContextMenu={(event) => openSeatMenu(seat, event)}
                    />
                  ))}
                </div>
              </section>
            ))}
          </div>
        )}
```

(Это убирает весь блок `<div className="seat-table-wrap">…</table></div>`.)

- [ ] **Step 9: Запустить тест — убедиться, что проходит**

Run: `bun test src/MapWorkspace.test.tsx`
Expected: PASS.

- [ ] **Step 10: Удалить CSS таблицы и добавить стиль счётчика «не на плане»**

В `src/styles.css` удалить все правила селекторов `.seat-table`, `.seat-table-wrap`, `.seat-table th`, `.seat-table td`, `.seat-table tr`, `.seat-table tr.selected td`, `.seat-table tr.state-*` и связанный с таблицей `.map-board.table-mode` (если есть). Добавить:

```css
.floor-plan-unplaced {
  margin: 8px 0 0;
  font-size: 12px;
  color: var(--text-secondary, #94a3b8);
}
```

Если был `.map-board.table-mode` — при необходимости добавь `.map-board.plan-mode { /* раскладка под холст: даём доске занять высоту */ display: flex; flex-direction: column; min-height: 0; }` (подгони под существующую раскладку `.map-board`).

- [ ] **Step 11: Полный прогон тестов воркспейса + сборка**

Run (из `src/AFK4.Operator.App.Web`):
`bun test $(ls src/*.test.ts src/*.test.tsx | grep -v App.test) && bun test src/App.test.tsx`
затем `bun run build`
Expected: все тесты зелёные; сборка проходит (tsc подтверждает, что не осталось ссылок на удалённые ключи/импорты/`'table'`).

- [ ] **Step 12: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/operatorTypes.ts src/AFK4.Operator.App.Web/src/MapWorkspace.tsx src/AFK4.Operator.App.Web/src/MapWorkspace.test.tsx src/AFK4.Operator.App.Web/src/styles.css
git commit -m "feat(operator-map): replace table view with the plan view in the map workspace"
```

---

## Финальная проверка (после всех задач)

- [ ] Полный прогон тестов воркспейса оператора зелёный (`bun test ...`).
- [ ] `bun run build` проходит (нет висящих ссылок на `'table'`, удалённые i18n-ключи, удалённые импорты).
- [ ] Тест паритета i18n зелёный (`packages/i18n` → `bun test src/messages.test.ts`).
- [ ] Визуально: переключатель `Карта · План`; План на фикстурах (без координат) показывает «Зал ещё не размечен»; при подставленных координатах рисует места/зоны/стены, клик открывает правую панель, зум/пан работают.
- [ ] tg-строки показаны пользователю на подтверждение (носитель языка).
- [ ] После B2-2: следующий PR — **B2-3 (редактор расстановки)**: палитра + инспектор + drag + сохранение `FloorMapBulkUpdateRequest`, право `layout.manage`, кнопка «Разметить план» в пустом состоянии, визуальная индикация `rotation`, стопка «не на плане», `pxToCell`/снап в `floorPlanGeometry`.

---

## Self-Review (выполнено при написании плана)

**Покрытие спеки B2-2:**
- Фронт-контракты геометрии → Task 1. ✓
- Состояние хранит zones/walls + геометрию мест → Task 2. ✓
- `floorPlanGeometry` (cellToPx/bbox) → Task 3 (`pxToCell`/снап явно отложены в B2-3 — drag там). ✓
- `floorPlanState`/`PlanModel` (DTO→модель, размещённые/неразмещённые) → Task 4. ✓
- Гибрид-рендер: SVG-подложка (сетка/зоны/стены) + DOM-слой мест (`PlanSeat` переиспользует тон-язык) → Task 5–6. ✓
- Замена `table`→`plan` в переключателе, удаление Таблицы, фолбэк/пустое/скелет, живые статусы (через `floorMap.seats` + реалтайм, уже работающий) → Task 8. ✓
- Тесты фронта (geometry, FloorPlan рендер + фолбэк-пустое, переключатель) → Task 3/4/6/8. ✓
- i18n новые ключи `op.map.plan.*` (ru/en/tg) → Task 7. ✓

**Осознанные отступления от спеки (зафиксированы выше):** План игнорирует фильтр; rotation без визуала; стопка «не на плане» — только счётчик (полная стопка в B2-3); кнопка «Разметить план» — в B2-3 (редактора ещё нет).

**Согласованность типов:** `PlanSeat`/`PlanZone`/`Wall`/`PlanModel` определены в Task 4 и используются единообразно в Task 5/6/8; `toPlanModel` — единая точка; `cellToPx`/`boundingBox`/`DEFAULT_CELL_SIZE`/`CANVAS_PADDING` из Task 3 используются в Task 6.
