# Operator «Карта» — редактор «План», ядро (расстановка мест) B2-3a Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Дать менеджеру (право `layout.manage`) режим редактирования вида «План»: расставлять уже существующие места зала на координатной сетке (drag+снап), задавать тип/поворот, убирать с плана и сохранять всю раскладку одной транзакцией.

**Architecture:** Чистый фронт (бэкенд-эндпоинт `PUT /api/branches/{id}/floor-map`, контракты и миграция уже в `main`/проде из B2-1). Редактор держит локальный `draft`, бэкенд дёргается только по «Сохранить». Сохранение — **full-replace**: сериализуем ВСЮ раскладку (все места — размещённые и «не на плане» — плюс все зоны и стены без изменений), иначе пропущенные сущности удалятся на сервере. Конкурентность — через ETag/If-Match.

**Tech Stack:** React + TypeScript (Vite), `bun test` + `@testing-library/react` (jsdom), `@afk4/i18n` (ICU-стиль ключи, источник — `locales/{ru,en,tg}.json` → `bun run gen`), существующий `PlatformApiClient`.

**Объём B2-3a:** места (place/move/snap/rotate/type/remove) + сохранение + права + пустое состояние. **Вне объёма (→ B2-3b, отдельный план):** геометрия зон (рамка/ресайз/цвет/тип/удаление), рисование/удаление стен. В B2-3a зоны и стены **сохраняются как есть** (передаются обратно без изменений), чтобы full-replace их не снёс.

**Ключевые факты по бэкенду (проверено в коде, НЕ меняем):**
- `PUT /api/branches/{branchId}/floor-map` принимает `FloorMapBulkUpdateRequest`, требует право `layout.manage`, требует заголовок `If-Match` с текущим ETag (иначе `428`), при устаревшем ETag → `412` (+ свежий ETag в заголовке ответа), при попытке удалить место с привязанным устройством/историей сессий → `409`, при нарушении валидации → `400`. Успех → `200` + `FloorMapBulkUpdateResponse { eTag, zones[], seats[] }` (маппинг ClientId→Id) + свежий ETag в заголовке.
- Семантика **full-replace**: зоны/места, чьих `Id` нет в запросе, удаляются; стены затираются целиком и пересоздаются из `Walls`. Требуется ≥1 зона; каждое место обязано ссылаться на зону через `ZoneClientId`, и эта зона должна быть в запросе.
- Стратегия ClientId для существующих сущностей: `ClientId == Id` (строка GUID). Так маппинг ответа тривиален и не плодит дублей.

**Контракт запроса (`src/AFK4.Shared.Contracts/FloorMap/FloorMapBulkUpdateRequest.cs`), camelCase в JSON:**
```
FloorMapBulkUpdateRequest { organizationId, zones[], seats[], walls?[] }
  zone: { zoneId?, clientId, name, sortOrder, geoX?, geoY?, geoWidth?, geoHeight?, color?, zoneType? }
  seat: { seatId?, clientId, zoneClientId, name, sortOrder, posX?, posY?, rotation=0, seatType="pc" }
  wall: { x1, y1, x2, y2 }
FloorMapBulkUpdateResponse { eTag, zones: [{clientId, zoneId}], seats: [{clientId, seatId}] }
```

---

## File Structure

**Сантехника (данные + API):**
- Modify `src/AFK4.Operator.App.Web/src/platformApi.ts` — добавить `getWithEtag` и `put` (с `If-Match`).
- Modify `src/AFK4.Operator.App.Web/src/api/clients/floorMap.ts` — добавить `getFloorMapWithEtag`, `updateFloorMap` + типы запроса/ответа.
- Modify `src/AFK4.Operator.App.Web/src/operatorData.ts` — `SeatSummary += zoneId`.
- Modify `src/AFK4.Operator.App.Web/src/floorMapState.ts` — `OperatorFloorMapState += etag`; `mapFloorMapSeat` несёт `zoneId`; конструкторы проставляют `etag`.
- Modify `src/AFK4.Operator.App.Web/src/operatorHelpers.ts` — `loadBackendFloorMapState` грузит через `getFloorMapWithEtag`.
- Modify `src/AFK4.Operator.App.Web/src/floorPlanGeometry.ts` — `pxToCell`, `isCellOccupied`.

**Черновик + сериализация:**
- Create `src/AFK4.Operator.App.Web/src/floorPlanDraft.ts` — тип `PlanDraft`, `createDraft`, мутации, `toBulkUpdateRequest`.

**UI редактора:**
- Modify `src/AFK4.Operator.App.Web/src/PlanSeat.tsx` — визуальный поворот + drag в режиме правки.
- Modify `src/AFK4.Operator.App.Web/src/FloorPlan.tsx` — проп `mode`, drag-места и подсветка коллизии в `edit`.
- Create `src/AFK4.Operator.App.Web/src/FloorInspector.tsx` — инспектор выбранного места.
- Create `src/AFK4.Operator.App.Web/src/FloorPalette.tsx` — стопка «не на плане».
- Create `src/AFK4.Operator.App.Web/src/FloorPlanEditor.tsx` — обёртка режима правки (draft + палитра + холст + инспектор + Сохранить/Отмена).
- Modify `src/AFK4.Operator.App.Web/src/MapWorkspace.tsx` — кнопка «Редактировать»/«Разметить план» под `manageLayout`, монтаж редактора, submit-сохранение.
- Modify `src/AFK4.Operator.App.Web/src/styles.css` — стили редактора (палитра/инспектор/панель действий/коллизия).
- Modify `locales/{ru,en,tg}.json` + `bun run gen` — ключи `op.map.plan.edit.*`.

**Тесты (рядом, `*.test.ts(x)`):** `floorPlanGeometry.test.ts`, `floorPlanDraft.test.ts`, `platformApi.test.ts` (или дополнить существующий), `PlanSeat.test.tsx`, `FloorPlan.test.tsx`, `FloorInspector.test.tsx`, `FloorPalette.test.tsx`, `FloorPlanEditor.test.tsx`, `MapWorkspace.test.tsx`.

**Команды проверки (память `afk4-env-quirks`):**
- Тесты воркспейса: `~/.bun/bin/bun test $(ls src/*.test.ts src/*.test.tsx | grep -v App.test) && ~/.bun/bin/bun test src/App.test.tsx` (из `src/AFK4.Operator.App.Web`).
- i18n: из `packages/i18n` → `~/.bun/bin/bun run gen` затем `~/.bun/bin/bun test`.
- Сборка: из `src/AFK4.Operator.App.Web` → `~/.bun/bin/bun run build`.

---

## Task 1: Сантехника данных — `zoneId` на месте и `etag` в состоянии

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/operatorData.ts` (интерфейс `SeatSummary`, ~строки 51-54)
- Modify: `src/AFK4.Operator.App.Web/src/floorMapState.ts` (интерфейс `OperatorFloorMapState` ~строка 15; конструкторы ~строки 34-76; `mapFloorMapSeat` ~строка 144)
- Test: `src/AFK4.Operator.App.Web/src/floorMapState.test.ts`

- [ ] **Step 1: Failing-тест — место несёт zoneId, состояние несёт etag**

Добавить в `floorMapState.test.ts` (рядом с существующими тестами `createFloorMapState`):

```ts
it('carries the zone id onto each seat and stores the etag', () => {
  const dto = {
    branchId: 'b1',
    branchName: 'AFK4 Dushanbe',
    zones: [],
    walls: [],
    seats: [{
      seatId: 's1', seatName: 'PC-01', zoneId: 'z1', zoneName: 'Зал A',
      sortOrder: 0, state: 'available', deviceId: null, deviceName: null,
      isDeviceOnline: null, isDeviceLocked: null, lastHeartbeatAtUtc: null,
      agentVersion: null, shellVersion: null, activeSessionId: null, remainingSeconds: null
    }]
  } as unknown as Parameters<typeof createFloorMapState>[0];

  const state = createFloorMapState(dto, translate, 'W/"abc"', 0);

  expect(state.etag).toBe('W/"abc"');
  expect(state.seats[0].zoneId).toBe('z1');
});
```

(Если у `createFloorMapState` сейчас иная сигнатура/имя translate-аргумента — выровнять под существующие тесты в этом же файле; `translate` уже определён в файле как тестовый `t`.)

- [ ] **Step 2: Запустить — упадёт**

Run: `~/.bun/bin/bun test src/floorMapState.test.ts` (из `src/AFK4.Operator.App.Web`)
Expected: FAIL — `etag` отсутствует в типе/значении, `zoneId` отсутствует на месте.

- [ ] **Step 3: Реализация**

В `operatorData.ts`, в `SeatSummary` рядом с гео-полями:

```ts
  // Floor-plan geometry (B2 DTO). Null/default until the seat is placed in the «План» editor.
  zoneId?: string;
  posX?: number | null;
```

В `floorMapState.ts`:

`OperatorFloorMapState` += поле (после `walls`):
```ts
  // Concurrency token from the floor-map GET; required as If-Match when saving layout (B2-3).
  // Null on fixtures and on the offline-cache mirror — the editor's save is disabled without it.
  etag: string | null;
```

`mapFloorMapSeat` — в возвращаемый объект добавить (рядом с `posX`):
```ts
    zoneId: dto.zoneId,
```

Конструкторы:
- `createFixtureFloorMapState`: добавить `etag: null` в возвращаемый объект.
- `createFloorMapState(floorMap, t, ...)`: добавить параметр `etag: string | null` и `loadedAtMs` оставить как есть; в объект добавить `etag`. Сигнатура становится `createFloorMapState(floorMap, t, etag, loadedAtMs = Date.now())`.
- `hydrateFloorMapStateFromCache(...)`: добавить `etag: null` (офлайн-снимок без токена — сохранять нельзя).

Найти всех вызывающих `createFloorMapState` и добавить аргумент `etag` (на этом шаге у бэкенд-загрузчика ещё нет etag — временно передать `null`, в Task 2 заменим на реальный). `rg -n "createFloorMapState\(" src` для списка.

- [ ] **Step 4: Запустить — пройдёт**

Run: `~/.bun/bin/bun test src/floorMapState.test.ts`
Expected: PASS. Затем полный прогон воркспейса (см. «Команды проверки») — зелёный.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/operatorData.ts src/AFK4.Operator.App.Web/src/floorMapState.ts src/AFK4.Operator.App.Web/src/floorMapState.test.ts
git commit -m "feat(operator-map): carry zoneId on seats and etag in floor-map state (B2-3a plumbing)"
```

---

## Task 2: API-клиент сохранения — `getWithEtag` + `put(If-Match)` + `updateFloorMap`

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/platformApi.ts`
- Modify: `src/AFK4.Operator.App.Web/src/api/clients/floorMap.ts`
- Modify: `src/AFK4.Operator.App.Web/src/operatorHelpers.ts` (`loadBackendFloorMapState`, ~строка 781)
- Test: `src/AFK4.Operator.App.Web/src/platformApi.test.ts` (создать, если нет)

- [ ] **Step 1: Failing-тест клиента API**

Создать/дополнить `platformApi.test.ts`:

```ts
import { describe, expect, it } from 'bun:test';
import { PlatformApiClient } from './platformApi';

function clientWith(fetchImpl: typeof fetch) {
  return new PlatformApiClient({ baseUrl: 'https://api.test/', getAccessToken: () => 'tok', fetchImpl });
}

describe('PlatformApiClient layout save support', () => {
  it('getWithEtag returns parsed body and the ETag header', async () => {
    const client = clientWith(async () =>
      new Response(JSON.stringify({ ok: true }), { status: 200, headers: { ETag: 'W/"v1"' } }));
    const result = await client.getWithEtag<{ ok: boolean }>('/x');
    expect(result.value.ok).toBe(true);
    expect(result.etag).toBe('W/"v1"');
  });

  it('put sends the If-Match header and JSON body', async () => {
    let seen: Request | null = null;
    const client = clientWith(async (input, init) => {
      seen = new Request(input, init);
      return new Response(JSON.stringify({ eTag: 'W/"v2"' }), { status: 200 });
    });
    const body = await client.put<{ eTag: string }, { a: number }>('/x', { a: 1 }, { ifMatch: 'W/"v1"' });
    expect(body.eTag).toBe('W/"v2"');
    expect(seen!.method).toBe('PUT');
    expect(seen!.headers.get('If-Match')).toBe('W/"v1"');
  });
});
```

- [ ] **Step 2: Запустить — упадёт**

Run: `~/.bun/bin/bun test src/platformApi.test.ts`
Expected: FAIL — методов `getWithEtag`/`put` нет.

- [ ] **Step 3: Реализация в `platformApi.ts`**

Рефактор: выделить публичный `put` и `getWithEtag`. Поскольку `fetchAuthorized` уже строит запрос с заголовками, добавить перегрузку с доп-заголовками. Конкретно:

В `fetchAuthorized` добавить параметр `extraHeaders?: Record<string, string>` и проставить их в `headers`:
```ts
  private async fetchAuthorized(
    method: string,
    path: string,
    body?: unknown,
    query?: QueryParams,
    extraHeaders?: Record<string, string>
  ): Promise<Response> {
    // ...существующее...
    const headers = new Headers({ Authorization: `Bearer ${accessToken}` });
    for (const [name, value] of Object.entries(extraHeaders ?? {})) {
      headers.set(name, value);
    }
    // ...существующее (Content-Type/body)...
  }
```

Добавить публичные методы (рядом с `get`/`post`):
```ts
  async getWithEtag<TResponse>(path: string, query?: QueryParams): Promise<{ value: TResponse; etag: string | null }> {
    const response = await this.fetchAuthorized('GET', path, undefined, query);
    await ensureSuccess(response);
    const etag = response.headers.get('ETag');
    const value = await response.json() as TResponse;
    return { value, etag };
  }

  async put<TResponse, TRequest = unknown>(
    path: string,
    body: TRequest,
    options?: { ifMatch?: string }
  ): Promise<TResponse> {
    const extraHeaders = options?.ifMatch ? { 'If-Match': options.ifMatch } : undefined;
    const response = await this.fetchAuthorized('PUT', path, body, undefined, extraHeaders);
    await ensureSuccess(response);
    if (response.status === 204) {
      return null as TResponse;
    }
    return await response.json() as TResponse;
  }
```

- [ ] **Step 4: Реализация в `floorMap.ts`**

Добавить типы запроса/ответа (camelCase, зеркало контракта) и методы клиента:

```ts
export interface FloorMapBulkZoneRequest {
  zoneId?: Guid | null;
  clientId: string;
  name: string;
  sortOrder: number;
  geoX?: number | null;
  geoY?: number | null;
  geoWidth?: number | null;
  geoHeight?: number | null;
  color?: string | null;
  zoneType?: string | null;
}

export interface FloorMapBulkSeatRequest {
  seatId?: Guid | null;
  clientId: string;
  zoneClientId: string;
  name: string;
  sortOrder: number;
  posX?: number | null;
  posY?: number | null;
  rotation?: number;
  seatType?: string;
}

export interface FloorMapBulkWallRequest { x1: number; y1: number; x2: number; y2: number; }

export interface FloorMapBulkUpdateRequest {
  organizationId: Guid;
  zones: FloorMapBulkZoneRequest[];
  seats: FloorMapBulkSeatRequest[];
  walls?: FloorMapBulkWallRequest[];
}

export interface FloorMapBulkUpdateResponse {
  eTag: string;
  zones: { clientId: string; zoneId: Guid }[];
  seats: { clientId: string; seatId: Guid }[];
}

export function createFloorMapClient(api: PlatformApiClient) {
  return {
    getFloorMap(branchId: Guid): Promise<FloorMapDto> {
      return api.get<FloorMapDto>(`/api/branches/${branchId}/floor-map`);
    },
    getFloorMapWithEtag(branchId: Guid): Promise<{ value: FloorMapDto; etag: string | null }> {
      return api.getWithEtag<FloorMapDto>(`/api/branches/${branchId}/floor-map`);
    },
    updateFloorMap(branchId: Guid, etag: string, request: FloorMapBulkUpdateRequest): Promise<FloorMapBulkUpdateResponse> {
      return api.put<FloorMapBulkUpdateResponse, FloorMapBulkUpdateRequest>(
        `/api/branches/${branchId}/floor-map`, request, { ifMatch: etag });
    }
  };
}
```

- [ ] **Step 5: Использовать etag при загрузке (`operatorHelpers.ts`)**

В `loadBackendFloorMapState` заменить:
```ts
  const floorMap = await clients.floorMap.getFloorMap(branchId);
```
на:
```ts
  const { value: floorMap, etag } = await clients.floorMap.getFloorMapWithEtag(branchId);
```
и при создании состояния передать `etag` в `createFloorMapState(floorMap, t, etag)` (найти точку создания состояния в функции; если она возвращает `createFloorMapState(...)` — добавить аргумент). Убрать временный `null`, заведённый в Task 1.

- [ ] **Step 6: Запустить — пройдёт + полный прогон**

Run: `~/.bun/bin/bun test src/platformApi.test.ts` → PASS
Затем полный прогон воркспейса → зелёный.

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/platformApi.ts src/AFK4.Operator.App.Web/src/platformApi.test.ts src/AFK4.Operator.App.Web/src/api/clients/floorMap.ts src/AFK4.Operator.App.Web/src/operatorHelpers.ts
git commit -m "feat(operator-map): floor-map save API client (getWithEtag + put If-Match)"
```

---

## Task 3: Геометрия — `pxToCell` (снап) + `isCellOccupied`

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/floorPlanGeometry.ts`
- Test: `src/AFK4.Operator.App.Web/src/floorPlanGeometry.test.ts`

- [ ] **Step 1: Failing-тест**

Добавить в `floorPlanGeometry.test.ts`:

```ts
import { pxToCell, isCellOccupied } from './floorPlanGeometry';

describe('pxToCell', () => {
  it('snaps a pixel offset to the nearest grid cell', () => {
    expect(pxToCell(0, 56)).toBe(0);
    expect(pxToCell(60, 56)).toBe(1);   // 60/56 = 1.07 → round → 1
    expect(pxToCell(83, 56)).toBe(1);   // 1.48 → 1
    expect(pxToCell(85, 56)).toBe(2);   // 1.52 → 2
  });
  it('never returns a negative cell', () => {
    expect(pxToCell(-10, 56)).toBe(0);
  });
});

describe('isCellOccupied', () => {
  const seats = [{ id: 'a', posX: 1, posY: 1 }, { id: 'b', posX: 3, posY: 2 }];
  it('reports a taken cell', () => {
    expect(isCellOccupied(seats, 1, 1)).toBe(true);
  });
  it('ignores the seat being moved', () => {
    expect(isCellOccupied(seats, 1, 1, 'a')).toBe(false);
  });
  it('reports a free cell', () => {
    expect(isCellOccupied(seats, 5, 5)).toBe(false);
  });
});
```

- [ ] **Step 2: Запустить — упадёт**

Run: `~/.bun/bin/bun test src/floorPlanGeometry.test.ts`
Expected: FAIL — функций нет.

- [ ] **Step 3: Реализация**

В `floorPlanGeometry.ts`:
```ts
// Snap a pixel offset (within the padded canvas, origin-normalized) back to an integer grid cell.
// Clamped at 0 so a drag toward the top/left edge never yields a negative coordinate.
export function pxToCell(px: number, cellSize: number = DEFAULT_CELL_SIZE): number {
  return Math.max(0, Math.round(px / cellSize));
}

// Is a grid cell already taken by a placed seat? `exceptId` excludes the seat currently being moved.
export function isCellOccupied(
  seats: { id: string; posX: number; posY: number }[],
  x: number,
  y: number,
  exceptId?: string
): boolean {
  return seats.some((seat) => seat.id !== exceptId && seat.posX === x && seat.posY === y);
}
```

- [ ] **Step 4: Запустить — пройдёт**

Run: `~/.bun/bin/bun test src/floorPlanGeometry.test.ts` → PASS

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/floorPlanGeometry.ts src/AFK4.Operator.App.Web/src/floorPlanGeometry.test.ts
git commit -m "feat(operator-map): pxToCell snap + cell-occupancy helpers"
```

---

## Task 4: Черновик раскладки + сериализатор `toBulkUpdateRequest`

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/floorPlanDraft.ts`
- Test: `src/AFK4.Operator.App.Web/src/floorPlanDraft.test.ts`

Цель: единственный источник правды режима правки. Несёт ВСЕ места (размещённые и нет), снимок зон и стен (в B2-3a — без изменений), и умеет собрать корректный `FloorMapBulkUpdateRequest`, который НЕ удаляет ничего лишнего.

- [ ] **Step 1: Failing-тест**

Создать `floorPlanDraft.test.ts`:

```ts
import { describe, expect, it } from 'bun:test';
import { createDraft, placeSeat, moveSeat, removeSeatFromPlan, rotateSeat, setSeatType, toBulkUpdateRequest } from './floorPlanDraft';
import type { OperatorFloorMapState } from './floorMapState';

function state(): OperatorFloorMapState {
  return {
    branchId: 'b1', branchName: 'AFK4', source: 'backend', loadStatus: 'ready',
    error: null, isOffline: false, etag: 'W/"v1"',
    zones: [{ zoneId: 'z1', name: 'Зал A', sortOrder: 0, geoX: null, geoY: null, geoWidth: null, geoHeight: null, color: null, zoneType: null }],
    walls: [{ wallId: 'w1', x1: 0, y1: 0, x2: 4, y2: 0 }],
    seats: [
      { id: 's1', zoneId: 'z1', name: 'PC-01', tone: 'ready', stateLabel: 'Свободно', sortOrder: 0, posX: 2, posY: 1, rotation: 0, seatType: 'pc' },
      { id: 's2', zoneId: 'z1', name: 'PC-02', tone: 'ready', stateLabel: 'Свободно', sortOrder: 1, posX: null, posY: null, rotation: 0, seatType: 'pc' }
    ]
  } as unknown as OperatorFloorMapState;
}

describe('floorPlanDraft', () => {
  it('serializes ALL seats and ALL zones (placed and unplaced) — never drops any', () => {
    const draft = createDraft(state());
    const req = toBulkUpdateRequest(draft, 'org-1');
    expect(req.organizationId).toBe('org-1');
    expect(req.zones).toHaveLength(1);
    expect(req.zones[0]).toMatchObject({ zoneId: 'z1', clientId: 'z1', name: 'Зал A', sortOrder: 0 });
    expect(req.seats).toHaveLength(2);
    const s1 = req.seats.find((s) => s.seatId === 's1')!;
    expect(s1).toMatchObject({ clientId: 's1', zoneClientId: 'z1', posX: 2, posY: 1, seatType: 'pc' });
    const s2 = req.seats.find((s) => s.seatId === 's2')!;
    expect(s2).toMatchObject({ posX: null, posY: null });        // unplaced seat still in payload
    expect(req.walls).toEqual([{ x1: 0, y1: 0, x2: 4, y2: 0 }]);  // walls preserved verbatim
  });

  it('placeSeat puts an unplaced seat at a cell; removeSeatFromPlan clears it', () => {
    let draft = createDraft(state());
    draft = placeSeat(draft, 's2', 5, 3);
    expect(draft.seats.find((s) => s.id === 's2')!.posX).toBe(5);
    draft = removeSeatFromPlan(draft, 's2');
    expect(draft.seats.find((s) => s.id === 's2')!.posX).toBeNull();
  });

  it('moveSeat / rotateSeat / setSeatType mutate only the target seat', () => {
    let draft = createDraft(state());
    draft = moveSeat(draft, 's1', 4, 4);
    draft = rotateSeat(draft, 's1', 90);
    draft = setSeatType(draft, 's1', 'console');
    const s1 = draft.seats.find((s) => s.id === 's1')!;
    expect(s1).toMatchObject({ posX: 4, posY: 4, rotation: 90, seatType: 'console' });
    expect(draft.seats.find((s) => s.id === 's2')!.rotation).toBe(0);
  });

  it('isDirty flips after a mutation', () => {
    const draft = createDraft(state());
    expect(draft.isDirty).toBe(false);
    expect(moveSeat(draft, 's1', 9, 9).isDirty).toBe(true);
  });
});
```

- [ ] **Step 2: Запустить — упадёт**

Run: `~/.bun/bin/bun test src/floorPlanDraft.test.ts`
Expected: FAIL — модуля нет.

- [ ] **Step 3: Реализация `floorPlanDraft.ts`**

```ts
import type { OperatorFloorMapState } from './floorMapState';
import type {
  FloorMapBulkUpdateRequest,
  FloorMapBulkSeatRequest,
  FloorMapBulkZoneRequest
} from './api/clients/floorMap';

// A seat as the editor tracks it: identity + zone + layout. Placed seats have posX/posY; unplaced
// seats keep them null but STAY in the draft so the full-replace save never deletes them.
export interface DraftSeat {
  id: string;
  zoneId: string;
  name: string;
  sortOrder: number;
  tone: string;
  stateLabel: string;
  seatType: string;
  rotation: number;
  posX: number | null;
  posY: number | null;
}

export interface DraftZone {
  zoneId: string;
  name: string;
  sortOrder: number;
  geoX: number | null;
  geoY: number | null;
  geoWidth: number | null;
  geoHeight: number | null;
  color: string | null;
  zoneType: string | null;
}

export interface DraftWall { x1: number; y1: number; x2: number; y2: number; }

export interface PlanDraft {
  etag: string | null;
  seats: DraftSeat[];
  zones: DraftZone[];
  walls: DraftWall[];
  isDirty: boolean;
}

export function createDraft(state: OperatorFloorMapState): PlanDraft {
  return {
    etag: state.etag,
    isDirty: false,
    seats: state.seats.map((seat) => ({
      id: seat.id,
      zoneId: seat.zoneId ?? '',
      name: seat.name,
      sortOrder: seat.sortOrder ?? 0,
      tone: seat.tone,
      stateLabel: seat.stateLabel,
      seatType: seat.seatType ?? 'pc',
      rotation: seat.rotation ?? 0,
      posX: seat.posX ?? null,
      posY: seat.posY ?? null
    })),
    zones: state.zones.map((zone) => ({
      zoneId: zone.zoneId,
      name: zone.name,
      sortOrder: zone.sortOrder,
      geoX: zone.geoX ?? null,
      geoY: zone.geoY ?? null,
      geoWidth: zone.geoWidth ?? null,
      geoHeight: zone.geoHeight ?? null,
      color: zone.color ?? null,
      zoneType: zone.zoneType ?? null
    })),
    walls: state.walls.map((wall) => ({ x1: wall.x1, y1: wall.y1, x2: wall.x2, y2: wall.y2 }))
  };
}

function mutateSeat(draft: PlanDraft, seatId: string, change: Partial<DraftSeat>): PlanDraft {
  return {
    ...draft,
    isDirty: true,
    seats: draft.seats.map((seat) => (seat.id === seatId ? { ...seat, ...change } : seat))
  };
}

export function placeSeat(draft: PlanDraft, seatId: string, posX: number, posY: number): PlanDraft {
  return mutateSeat(draft, seatId, { posX, posY });
}

export function moveSeat(draft: PlanDraft, seatId: string, posX: number, posY: number): PlanDraft {
  return mutateSeat(draft, seatId, { posX, posY });
}

export function removeSeatFromPlan(draft: PlanDraft, seatId: string): PlanDraft {
  return mutateSeat(draft, seatId, { posX: null, posY: null });
}

export function rotateSeat(draft: PlanDraft, seatId: string, rotation: number): PlanDraft {
  return mutateSeat(draft, seatId, { rotation });
}

export function setSeatType(draft: PlanDraft, seatId: string, seatType: string): PlanDraft {
  return mutateSeat(draft, seatId, { seatType });
}

// Serialize the ENTIRE layout. ClientId == existing Id so the server maps 1:1 and deletes nothing.
// Unplaced seats go in with null coords (still owned); zones and walls are echoed back unchanged
// (zone geometry + walls are edited in B2-3b, not here).
export function toBulkUpdateRequest(draft: PlanDraft, organizationId: string): FloorMapBulkUpdateRequest {
  const zones: FloorMapBulkZoneRequest[] = draft.zones.map((zone) => ({
    zoneId: zone.zoneId,
    clientId: zone.zoneId,
    name: zone.name,
    sortOrder: zone.sortOrder,
    geoX: zone.geoX,
    geoY: zone.geoY,
    geoWidth: zone.geoWidth,
    geoHeight: zone.geoHeight,
    color: zone.color,
    zoneType: zone.zoneType
  }));

  const seats: FloorMapBulkSeatRequest[] = draft.seats.map((seat) => ({
    seatId: seat.id,
    clientId: seat.id,
    zoneClientId: seat.zoneId,
    name: seat.name,
    sortOrder: seat.sortOrder,
    posX: seat.posX,
    posY: seat.posY,
    rotation: seat.rotation,
    seatType: seat.seatType
  }));

  return {
    organizationId,
    zones,
    seats,
    walls: draft.walls.map((wall) => ({ x1: wall.x1, y1: wall.y1, x2: wall.x2, y2: wall.y2 }))
  };
}
```

- [ ] **Step 4: Запустить — пройдёт**

Run: `~/.bun/bin/bun test src/floorPlanDraft.test.ts` → PASS

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/floorPlanDraft.ts src/AFK4.Operator.App.Web/src/floorPlanDraft.test.ts
git commit -m "feat(operator-map): plan editor draft model + full-layout serializer"
```

---

## Task 5: `PlanSeat` — визуальный поворот + drag в режиме правки

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/PlanSeat.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/styles.css` (`.plan-seat` — добавить `--seat-rotation`/draggable affordance)
- Test: `src/AFK4.Operator.App.Web/src/PlanSeat.test.tsx`

- [ ] **Step 1: Failing-тест**

Добавить в `PlanSeat.test.tsx`:

```ts
it('applies a visual rotation transform', () => {
  const { getByRole } = renderSeat(model({ rotation: 90 }));
  expect(getByRole('button').style.transform).toContain('rotate(90deg)');
});

it('fires onDragStart when draggable (edit mode)', () => {
  let started = '';
  const { getByRole } = render(
    <I18nProvider>
      <PlanSeat seat={model()} cellSize={56} onSelect={() => {}} draggable onSeatDragStart={(id) => { started = id; }} />
    </I18nProvider>
  );
  fireEvent.pointerDown(getByRole('button'));
  expect(started).toBe('pc-01');
});
```

- [ ] **Step 2: Запустить — упадёт**

Run: `~/.bun/bin/bun test src/PlanSeat.test.tsx`
Expected: FAIL — нет transform/нет `draggable`/`onSeatDragStart`.

- [ ] **Step 3: Реализация**

В `PlanSeat.tsx` расширить props: `draggable?: boolean; onSeatDragStart?: (seatId: string) => void;`. На корневом `<button>`:
- стиль: добавить `transform: \`rotate(${seat.rotation}deg)\`` (поворот вокруг центра маркера — задать `transform-origin: center` в CSS если нужно).
- если `draggable`: `onPointerDown={(e) => { e.stopPropagation(); onSeatDragStart?.(seat.id); }}` (stopPropagation, чтобы не стартовал пан холста). Класс `plan-seat--draggable` для `cursor: grab`.
- Сохранить существующее поведение `onSelect`/`onContextMenu`/aria.

В `styles.css`: `.plan-seat { transform-origin: center; } .plan-seat--draggable { cursor: grab; } .plan-seat--draggable:active { cursor: grabbing; }`.

- [ ] **Step 4: Запустить — пройдёт**

Run: `~/.bun/bin/bun test src/PlanSeat.test.tsx` → PASS (включая существующие тесты Task B2-2 — позиционирование/aria/контекст-меню).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/PlanSeat.tsx src/AFK4.Operator.App.Web/src/PlanSeat.test.tsx src/AFK4.Operator.App.Web/src/styles.css
git commit -m "feat(operator-map): PlanSeat visual rotation + edit-mode drag affordance"
```

---

## Task 6: `FloorPlan` режим `edit` — drag места со снапом и подсветкой коллизии

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/FloorPlan.tsx`
- Test: `src/AFK4.Operator.App.Web/src/FloorPlan.test.tsx`

`FloorPlan` получает `mode: 'view' | 'edit'` (по умолчанию `'view'`). В `view` — поведение B2-2 без изменений. В `edit` — перетаскивание места по холсту со снапом в ячейку через `pxToCell` и колбэком `onSeatMove(seatId, x, y)`; занятая ячейка под курсором не принимает дроп (колбэк не зовётся) и подсвечивается.

- [ ] **Step 1: Failing-тест**

Добавить в `FloorPlan.test.tsx`:

```ts
it('view mode keeps seats non-draggable (regression)', () => {
  const { container } = renderPlan({ mode: 'view' });
  expect(container.querySelector('.plan-seat--draggable')).toBeNull();
});

it('edit mode reports a snapped move to a free cell', () => {
  const moves: Array<[string, number, number]> = [];
  const { getByRole } = renderPlan({ mode: 'edit', onSeatMove: (id, x, y) => moves.push([id, x, y]) });
  const seat = getByRole('button', { name: /PC-01/ });
  fireEvent.pointerDown(seat);
  // переместить на ~2 ячейки вправо/вниз и отпустить над холстом
  fireEvent.pointerMove(window, { clientX: 200, clientY: 200 });
  fireEvent.pointerUp(window);
  expect(moves.length).toBe(1);
});
```

(Тест-хелпер `renderPlan(props)` — собрать `PlanModel` с 1-2 размещёнными местами и пробросить пропсы. Точные координаты подогнать под систему координат холста; если в jsdom `getBoundingClientRect` даёт нули — мокнуть/допустить, что снап считается от `clientX - canvasRect.left`. Главная проверка: в `view` нет draggable, в `edit` колбэк зовётся ровно один раз при валидном дропе.)

- [ ] **Step 2: Запустить — упадёт**

Run: `~/.bun/bin/bun test src/FloorPlan.test.tsx`
Expected: FAIL — нет `mode`/`onSeatMove`.

- [ ] **Step 3: Реализация**

В `FloorPlan.tsx`:
- Props += `mode?: 'view' | 'edit'`, `onSeatMove?: (seatId, x, y) => void`, `placedCells?` (берём из `model.placedSeats`).
- В `edit`: каждый `PlanSeat` рендерится с `draggable` и `onSeatDragStart`. Завести локальный стейт `dragSeat: { id; ... } | null`. На `onSeatDragStart` запомнить id; на глобальный `pointermove` (через `window.addEventListener` в эффекте, как уже сделано с wheel — non-passive не нужен) считать ячейку через `pxToCell` относительно холста (учесть `originX/originY`, `CANVAS_PADDING`, `scale`, `pan`); на `pointerup` — если ячейка свободна (`isCellOccupied(model.placedSeats, x, y, dragSeat.id) === false`) звать `onSeatMove(id, x, y)`, иначе ничего. Подсветка занятой ячейки — класс на ховер-ячейке/маркере (`.plan-seat--blocked` или оверлей `.plan-cell-blocked`).
- Пан холста в `edit` оставить только при старте на пустом месте (как в `view`); drag места перекрывает пан через `stopPropagation` в `PlanSeat` (Task 5).

⚠️ Координатная математика: учесть текущий `scale`. Смещение в px делить на `scale` перед `pxToCell` (иначе при зуме снап врёт). Прокомментировать.

- [ ] **Step 4: Запустить — пройдёт + полный прогон**

Run: `~/.bun/bin/bun test src/FloorPlan.test.tsx` → PASS, затем полный прогон воркспейса.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/FloorPlan.tsx src/AFK4.Operator.App.Web/src/FloorPlan.test.tsx
git commit -m "feat(operator-map): FloorPlan edit mode — drag seats with snap + collision guard"
```

---

## Task 7: `FloorPalette` (стопка «не на плане») + `FloorInspector` (тип/поворот/убрать)

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/FloorPalette.tsx`
- Create: `src/AFK4.Operator.App.Web/src/FloorInspector.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/styles.css`
- Test: `src/AFK4.Operator.App.Web/src/FloorPalette.test.tsx`, `src/AFK4.Operator.App.Web/src/FloorInspector.test.tsx`

`FloorPalette` — список мест без координат (из `draft.seats.filter(s => s.posX == null)`), сгруппированы по типу или просто списком; клик «разместить» → колбэк `onPlaceSeat(seatId)` (редактор положит в первую свободную ячейку). `FloorInspector` — свойства выбранного места: тип (`PanelSelect` из существующих), поворот (4 кнопки 0/90/180/270 или стрелки ±90), «убрать с плана». Использует существующий `PanelSelect.tsx`.

- [ ] **Step 1: Failing-тесты**

`FloorPalette.test.tsx`:
```ts
import { describe, expect, it } from 'bun:test';
import { render, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { FloorPalette } from './FloorPalette';

const unplaced = [{ id: 's2', name: 'PC-02', seatType: 'pc' }, { id: 's3', name: 'PS-01', seatType: 'console' }];

describe('FloorPalette', () => {
  it('lists unplaced seats and fires onPlaceSeat', () => {
    let placed = '';
    const { getByText } = render(
      <I18nProvider><FloorPalette unplaced={unplaced} onPlaceSeat={(id) => { placed = id; }} /></I18nProvider>);
    fireEvent.click(getByText('PC-02'));
    expect(placed).toBe('s2');
  });
  it('shows an empty hint when everything is placed', () => {
    const { container } = render(
      <I18nProvider><FloorPalette unplaced={[]} onPlaceSeat={() => {}} /></I18nProvider>);
    expect(container.querySelector('.floor-palette-empty')).not.toBeNull();
  });
});
```

`FloorInspector.test.tsx`:
```ts
import { describe, expect, it } from 'bun:test';
import { render, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { FloorInspector } from './FloorInspector';

const seat = { id: 's1', name: 'PC-01', seatType: 'pc', rotation: 0, posX: 2, posY: 1 };

describe('FloorInspector', () => {
  it('rotates and removes the selected seat', () => {
    const rotations: number[] = [];
    let removed = '';
    const { getByRole } = render(
      <I18nProvider>
        <FloorInspector seat={seat} onRotate={(r) => rotations.push(r)} onSetType={() => {}} onRemove={(id) => { removed = id; }} />
      </I18nProvider>);
    fireEvent.click(getByRole('button', { name: /повернуть|rotate/i }));
    expect(rotations[0]).toBe(90);
    fireEvent.click(getByRole('button', { name: /убрать|remove/i }));
    expect(removed).toBe('s1');
  });
});
```

(Имена кнопок — через i18n-ключи Task 8; на этом шаге допустимо ввести ключи заранее или временно матчить по тестовому aria. Согласовать с реализацией.)

- [ ] **Step 2: Запустить — упадёт**

Run: `~/.bun/bin/bun test src/FloorPalette.test.tsx src/FloorInspector.test.tsx`
Expected: FAIL — компонентов нет.

- [ ] **Step 3: Реализация**

`FloorPalette.tsx` — обёртка с заголовком `op.map.plan.edit.paletteTitle`, список кнопок мест (иконка типа + имя), пустой хинт `.floor-palette-empty` (`op.map.plan.edit.paletteEmpty`). Props: `unplaced: { id; name; seatType }[]`, `onPlaceSeat`.

`FloorInspector.tsx` — карточка свойств. Props: `seat`, `onRotate(next)`, `onSetType(type)`, `onRemove(id)`. Поворот: кнопка «повернуть на 90°» → `onRotate((seat.rotation + 90) % 360)`. Тип: `PanelSelect` со списком типов (`pc/console/vr/sim/billiard/boardgame/counter`, лейблы — i18n `op.map.plan.edit.seatType.*`). «Убрать с плана» — danger-кнопка.

`styles.css` — `.floor-palette`, `.floor-palette-empty`, `.floor-inspector`, раскладка трёх колонок редактора (см. Task 8).

- [ ] **Step 4: Запустить — пройдёт**

Run: `~/.bun/bin/bun test src/FloorPalette.test.tsx src/FloorInspector.test.tsx` → PASS

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/FloorPalette.tsx src/AFK4.Operator.App.Web/src/FloorInspector.tsx src/AFK4.Operator.App.Web/src/FloorPalette.test.tsx src/AFK4.Operator.App.Web/src/FloorInspector.test.tsx src/AFK4.Operator.App.Web/src/styles.css
git commit -m "feat(operator-map): plan editor palette (unplaced stack) + inspector"
```

---

## Task 8: `FloorPlanEditor` + интеграция в `MapWorkspace` + сохранение/ошибки + i18n

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/FloorPlanEditor.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/MapWorkspace.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/styles.css`
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json` (+ `bun run gen` в `packages/i18n`)
- Test: `src/AFK4.Operator.App.Web/src/FloorPlanEditor.test.tsx`, `src/AFK4.Operator.App.Web/src/MapWorkspace.test.tsx`

`FloorPlanEditor` — обёртка режима правки: держит `draft` (из `createDraft`), layout «палитра · холст(`mode=edit`) · инспектор», панель действий «Сохранить»/«Отмена». «Сохранить» → собрать `toBulkUpdateRequest(draft, orgId)` → вызвать переданный `onSave` → при успехе выйти в просмотр + `ActionFeedback`; при ошибке оставить черновик и показать конкретику. «Отмена» при `isDirty` → подтверждение.

Сохранение делает `MapWorkspace`/родитель: `updateFloorMap(branchId, etag, request)`, затем перезагрузка карты (как после seat-action). Маппинг ошибок: `412` → «Раскладка изменилась, обновите и повторите»; `409` → текст из тела (место с устройством/историей); `428` → «нет токена синхронизации, обновите страницу»; прочее → конкретика бэка.

- [ ] **Step 1: Failing-тесты**

`FloorPlanEditor.test.tsx` (ключевое — сборка корректного запроса по «Сохранить»):
```ts
import { describe, expect, it } from 'bun:test';
import { render, fireEvent, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { FloorPlanEditor } from './FloorPlanEditor';
import type { OperatorFloorMapState } from './floorMapState';

function state(): OperatorFloorMapState { /* как в floorPlanDraft.test.ts */ return /* ... */ {} as any; }

describe('FloorPlanEditor', () => {
  it('places an unplaced seat then Save submits the full layout', async () => {
    let submitted: any = null;
    const { getByText, getByRole } = render(
      <I18nProvider>
        <FloorPlanEditor floorMap={state()} organizationId="org-1"
          onSave={async (req) => { submitted = req; }} onExit={() => {}} />
      </I18nProvider>);
    fireEvent.click(getByText('PC-02'));                       // разместить из палитры
    fireEvent.click(getByRole('button', { name: /сохранить|save/i }));
    await waitFor(() => expect(submitted).not.toBeNull());
    expect(submitted.seats).toHaveLength(2);                   // оба места в запросе
    expect(submitted.seats.find((s: any) => s.seatId === 's2').posX).not.toBeNull();
  });

  it('Cancel with unsaved changes asks for confirmation', () => {
    const { getByText, getByRole, queryByText } = render(/* editor с одной правкой */ <I18nProvider>{/*...*/}</I18nProvider>);
    // ... сделать правку, нажать «Отмена», проверить появление подтверждения
  });
});
```

`MapWorkspace.test.tsx` — гейтинг права:
```ts
it('shows «Редактировать» only with layout.manage', () => { /* session с/без manageLayout → наличие кнопки */ });
it('empty plan shows «Разметить план» for a manager', () => { /* isEmpty + manageLayout */ });
```

- [ ] **Step 2: Запустить — упадёт**

Run: `~/.bun/bin/bun test src/FloorPlanEditor.test.tsx` → FAIL (нет компонента).

- [ ] **Step 3: i18n-ключи (СНАЧАЛА, источник — JSON)**

В `locales/ru.json`, `en.json`, `tg.json` добавить блок `op.map.plan.edit.*` (одинаковый набор ключей во всех трёх; tg — добросовестный перевод, НЕ копия ru). Набор:
```
op.map.plan.edit.enter            ru:"Редактировать"        en:"Edit"            tg:"Таҳрир"
op.map.plan.edit.arrange          ru:"Разметить план"       en:"Arrange plan"    tg:"Нақшаро тартиб диҳед"
op.map.plan.edit.save             ru:"Сохранить"            en:"Save"            tg:"Захира кардан"
op.map.plan.edit.cancel           ru:"Отмена"               en:"Cancel"          tg:"Бекор кардан"
op.map.plan.edit.paletteTitle     ru:"Не на плане"          en:"Not on plan"     tg:"Берун аз нақша"
op.map.plan.edit.paletteEmpty     ru:"Все места размещены"  en:"All seats placed" tg:"Ҳама ҷойҳо ҷойгир шуданд"
op.map.plan.edit.inspectorTitle   ru:"Свойства места"       en:"Seat properties" tg:"Хосиятҳои ҷой"
op.map.plan.edit.rotate           ru:"Повернуть на 90°"     en:"Rotate 90°"      tg:"Гардиш ба 90°"
op.map.plan.edit.removeFromPlan   ru:"Убрать с плана"       en:"Remove from plan" tg:"Аз нақша гирифтан"
op.map.plan.edit.seatTypeLabel    ru:"Тип"                  en:"Type"            tg:"Навъ"
op.map.plan.edit.seatType.pc        ru:"ПК"          en:"PC"        tg:"Компютер"
op.map.plan.edit.seatType.console   ru:"Консоль"     en:"Console"   tg:"Консол"
op.map.plan.edit.seatType.vr        ru:"VR"          en:"VR"        tg:"VR"
op.map.plan.edit.seatType.sim       ru:"Симулятор"   en:"Simulator" tg:"Симулятор"
op.map.plan.edit.seatType.billiard  ru:"Бильярд"     en:"Billiards" tg:"Биллиард"
op.map.plan.edit.seatType.boardgame ru:"Настолки"    en:"Board games" tg:"Бозиҳои мизӣ"
op.map.plan.edit.seatType.counter   ru:"Стойка"      en:"Counter"   tg:"Пешхон"
op.map.plan.edit.confirmDiscardTitle ru:"Отменить изменения?" en:"Discard changes?" tg:"Тағйиротро бекор кунед?"
op.map.plan.edit.confirmDiscardBody  ru:"Несохранённая расстановка будет потеряна." en:"Unsaved layout will be lost." tg:"Тартиби захиранашуда гум мешавад."
op.map.plan.edit.confirmDiscardYes   ru:"Отменить" en:"Discard" tg:"Бекор кардан"
op.map.plan.edit.confirmKeep         ru:"Продолжить правку" en:"Keep editing" tg:"Идомаи таҳрир"
op.map.plan.edit.saved            ru:"Расстановка сохранена" en:"Layout saved" tg:"Тартиб захира шуд"
op.map.plan.edit.saveFailed       ru:"Не удалось сохранить расстановку" en:"Could not save layout" tg:"Захираи тартиб нашуд"
op.map.plan.edit.conflict         ru:"Раскладка изменилась с другого устройства. Обновите и повторите." en:"Layout changed elsewhere. Refresh and retry." tg:"Тартиб аз ҷои дигар тағйир ёфт. Навсозӣ кунед ва такрор кунед."
op.map.plan.edit.noManagePermission ru:"Расстановку меняет менеджер зала." en:"Only a branch manager can arrange the plan." tg:"Танҳо мудири филиал нақшаро тартиб медиҳад."
```
Затем: из `packages/i18n` → `~/.bun/bin/bun run gen` (перегенерит `src/messages.ts` — НЕ редактировать руками), `~/.bun/bin/bun test` (parity зелёный).

- [ ] **Step 4: Реализация `FloorPlanEditor.tsx`**

```tsx
// Edit-mode wrapper for the «План» view: holds a local layout draft, lets a manager place/move/
// rotate/retype seats, and saves the whole layout in one transaction. Live statuses are frozen
// while editing (we are arranging, not monitoring).
```
- Стейт: `const [draft, setDraft] = useState(() => createDraft(floorMap))`, `selectedSeatId`, `feedback`, `saving`, `confirmDiscard`.
- `planModel` для холста собираем из draft (placed = `posX != null`); инспектор — по `selectedSeatId`; палитра — unplaced.
- Колбэки: `onSeatMove`→`moveSeat`, `onPlaceSeat`→первая свободная ячейка (через `isCellOccupied` сканом от 0,0) + `placeSeat`, `onRotate`→`rotateSeat`, `onSetType`→`setSeatType`, `onRemove`→`removeSeatFromPlan`.
- «Сохранить»: `setSaving(true)`; `await onSave(toBulkUpdateRequest(draft, organizationId))`; успех → `feedback=saved` + `onExit()`; ошибка → маппинг (см. выше) в `feedback`, черновик не трогаем. `finally setSaving(false)`.
- «Отмена»: если `draft.isDirty` → открыть `confirmDiscard`; иначе сразу `onExit()`.

- [ ] **Step 5: Интеграция в `MapWorkspace.tsx`**

- Проп/вычисление `canManageLayout = hasPermission(session, permissionNames.manageLayout)`.
- Локальный стейт `editing: boolean`.
- В ветке `viewMode === 'plan'`:
  - если `editing` → `<FloorPlanEditor floorMap={floorMap} organizationId={...} onSave={onSaveLayout} onExit={() => setEditing(false)} />`.
  - иначе просмотр (как в B2-2) + кнопка «Редактировать» (`op.map.plan.edit.enter`) в тулбаре вида **только при** `canManageLayout`.
  - пустой план: `EmptyState` + если `canManageLayout` кнопка «Разметить план» (`...edit.arrange`, открывает редактор), иначе подсказка `...edit.noManagePermission`.
- `onSaveLayout(request)` — новый проп `MapWorkspace`, прокинутый из `App.tsx`/`useFloorMap`: вызывает `clients.floorMap.updateFloorMap(branchId, floorMap.etag!, request)`, затем перезагружает карту (переиспользовать существующий путь обновления состояния, как в `handleSeatAction`). Реализовать в `useFloorMap` метод `handleSaveLayout(request)` и пробросить. Если `floorMap.etag == null` (офлайн/фикстура) — кнопка «Редактировать» скрыта/выключена (нельзя сохранить без токена).
- `organizationId` берётся из `session.organizationId`.

⚠️ Замораживание статусов: пока `editing`, не дёргать живые обновления карты в редактор — редактор работает со снимком `floorMap` на момент входа (`createDraft` один раз). Это ок: на время правки оператор не мониторит.

- [ ] **Step 6: Запустить — пройдёт + сборка**

Run: `~/.bun/bin/bun test src/FloorPlanEditor.test.tsx src/MapWorkspace.test.tsx` → PASS
Полный прогон воркспейса (incl. `App.test.tsx`) → зелёный.
`~/.bun/bin/bun run build` → без ошибок типов.
Из `packages/i18n`: `~/.bun/bin/bun test` → parity зелёный.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat(operator-map): «План» edit mode — editor, layout save, layout.manage gating (B2-3a)"
```

---

## Self-Review (выполнить после всех задач, перед finishing-branch)

1. **Покрытие спеки (B2-3a-часть):** drag+снап (Task 6), тип/поворот/убрать (Task 7), палитра «не на плане» (Task 7), сохранение одной транзакцией (Tasks 4+8), право `layout.manage` гейтит «Редактировать» (Task 8), «Разметить план» в пустом (Task 8), визуальный поворот (Task 5), pxToCell/снап (Task 3). ✔ Вне B2-3a (→ B2-3b): зоны-геометрия, стены — **намеренно** не делаем, помечено в шапке.
2. **Главный инвариант сохранения:** `toBulkUpdateRequest` шлёт ВСЕ места (placed+unplaced) и ВСЕ зоны и стены — проверено тестом Task 4. Это защита от full-replace-удаления. Перепроверить, что `MapWorkspace`/`useFloorMap` не фильтрует места перед передачей в редактор.
3. **ETag:** загрузка кладёт `etag` (Task 1/2), сохранение шлёт `If-Match` (Task 2/8), `412` маппится в понятную ошибку, черновик не теряется (Task 8). Без etag (офлайн) редактор недоступен.
4. **Типы согласованы:** `DraftSeat.zoneId` ← `SeatSummary.zoneId` ← `dto.zoneId`; имена методов (`moveSeat`/`placeSeat`/`removeSeatFromPlan`/`rotateSeat`/`setSeatType`) совпадают между `floorPlanDraft.ts`, тестами и `FloorPlanEditor`.
5. **i18n:** все новые ключи в трёх локалях, `messages.ts` сгенерён (не правлен руками), parity-тест зелёный, tg — перевод.
6. **Плейсхолдеры:** в плане нет «TODO/добавить обработку ошибок» без кода — маппинг ошибок расписан явно.

## Execution Handoff

После сохранения плана — выбор исполнения:
1. **Subagent-Driven (рекомендуется)** — свежий субагент на задачу + двухстадийное ревью.
2. **Inline Execution** — пакетно с чекпойнтами.
