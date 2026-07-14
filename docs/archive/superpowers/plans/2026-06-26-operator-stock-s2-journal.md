# Склад S2 — Журнал движений Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Дать складу прослеживаемость — вкладка «Журнал»: лента всех движений (время · тип · товар+контекст · ±кол-во · сумма · кто) с фильтром по типу и по периоду, итогами за период и экспортом CSV; перенести историю движений из `Управление → Товары` (дубль убрать).

**Architecture:** Почти чистый фронт. Одна маленькая бэк-правка: `StockMovementDto` получает `CreatedByDisplayName`, резолвимый сервером в `GetStockMovementsAsync` (справочник `/staff` закрыт правом `ManageBranchStaff` — оператор его не достанет, поэтому имя резолвит сервер). Период фильтруется КЛИЕНТОМ на окне последних ≤200 движений (эндпоинт даты игнорирует, режет до 200). Журнал — read-only.

**Tech Stack:** Бэк — .NET 10 + EF Core (xUnit, InMemory). Фронт — React 18 + TS, `@afk4/i18n` (ICU; `locales/{ru,en,tg}.json` → `bun run gen` в `packages/i18n`), `@afk4/tokens`, `bun test` (happy-dom + jest-dom), CSS в `src/styles/22-stock.css`.

## Global Constraints

- **Деньги — minor units** на проводе; UI форматирует `formatMinorUnits(minorUnits, currencyCode)` (из `../currencyFormat`). `unitCost` движения = вложенный `MoneyDto` (`readMoney(m,'unitCost')?.minorUnits ?? 0`). `quantityDelta` — плоское знаковое число.
- **`bun run build` тайпчекает тест-файлы** (`tsc -b`) — зелёный `bun test` ≠ зелёная сборка. Типизировать bun-моки: фабрика с сигнатурой `mock(async (_branchId: string, _q?: unknown) => …)`, тогда `.mock.calls`/`mockImplementation` типобезопасны. Финал слайса ОБЯЗАН включать `bun run build`.
- **Детерминизм времени в чистых функциях:** `Date.now()` НЕ вызывать внутри pure-логики — передавать `nowMs: number` параметром (тестируемость). Группировку по дню и период считать по UTC-календарю (TZ-независимо, детерминированно в тестах). Это осознанный компромисс (ранне-утренние локальные движения группируются в предыдущий UTC-день) — для S2 допустимо, зафиксировано в бэклоге.
- **Токены `@afk4/tokens`, тема dark.** Акцент — emerald `var(--accent)`. Деньги/числа — `text-strong`/`text-primary` моноширинные. **Янтарь (`--warning`) только для предупреждений** (списание/коррекция-минус), НЕ для сумм/приходов. Приход — `accent-text` (зелёный +). Контраст таблиц: значения `text-primary`, заголовки/SKU ≥ `text-tertiary`, границы `border-default`.
- **Права:** Журнал — read-only, видна при `viewInventory | manageInventoryStock` (как Остатки). Бэк GET `stock-movements` уже требует `ViewInventory`.
- **Никаких заглушек/мусора.** История из `Управление → Товары` убирается (не дублируется). Типы-фильтры = реальные `movementType` (purchase/sale/refund/adjustment) — мокапный сплит «Списания/Корректировки» НЕ подделывать (оба = adjustment).
- **i18n:** новые `t()`-ключи обязаны существовать (guard `i18nKeysExist.test.ts`); новые ru-ключи реально переведены на tg (анти `tg===ru` guard). Порядок: правка `locales/{ru,en,tg}.json` → `cd packages/i18n && bun run gen` → коммит `messages.ts` с локалями.
- **dotnet на Linux:** полный путь `/home/fedya/.dotnet/dotnet` (проверь `which dotnet`). Бэк-тесты: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj` и `tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj`.

---

### Task 1: Бэк — `StockMovementDto.CreatedByDisplayName` (резолв имени сервером)

**Files:**
- Modify: `src/AFK4.Shared.Contracts/Inventory/StockMovementDto.cs`
- Modify: `src/AFK4.Platform.Api/Inventory/EfInventoryService.cs` (`GetStockMovementsAsync` + `ToDto(StockMovementEntity)`)
- Modify: `tests/AFK4.Platform.Api.Tests/EfInventoryServiceTests.cs` (новый тест резолва имени)
- Modify: `tests/AFK4.Shared.Contracts.Tests/InventoryContractSerializationTests.cs` (round-trip нового поля)

**Interfaces:**
- Produces: `StockMovementDto` с трейлинг-полем `string? CreatedByDisplayName = null` (бэк-совместимо). `GetStockMovementsAsync` заполняет его именем `StaffUserEntity.DisplayName` по `CreatedByStaffUserId`; `CreateStockMovementAsync` оставляет `null` (имя нужно только Журналу на чтение).

- [ ] **Step 1: Падающий тест резолва имени**

В `tests/AFK4.Platform.Api.Tests/EfInventoryServiceTests.cs` добавить тест (рядом с `GetStockMovementsAsync_ReturnsRecentMovementsFilteredByProduct`). Он сидит staff-юзера с `StaffUserId = ActorStaffUserId` и именем, создаёт движение и движение от неизвестного актора, проверяет резолв:
```csharp
    [Fact]
    public async Task GetStockMovementsAsync_ResolvesCreatedByDisplayName()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var product = await CreateTrackedProductAsync(service);
        db.StaffUsers.Add(new StaffUserEntity
        {
            StaffUserId = ActorStaffUserId, OrganizationId = TestIds.OrganizationId, UserName = "oleg",
            NormalizedUserName = "OLEG", DisplayName = "Олег С.", PasswordHash = "x", IsActive = true, CreatedAtUtc = Now
        });
        await db.SaveChangesAsync();

        var unknownActor = Guid.NewGuid();
        await service.CreateStockMovementAsync(
            TestIds.BranchId, ActorStaffUserId,
            StockMovement(product.ProductId, StockMovementTypeNames.Purchase, 5, "stock-name-001"),
            CancellationToken.None);
        // движение от актора без записи в StaffUsers → имя не резолвится (null)
        await service.CreateStockMovementAsync(
            TestIds.BranchId, unknownActor,
            StockMovement(product.ProductId, StockMovementTypeNames.Adjustment, -1, "stock-name-002"),
            CancellationToken.None);

        var result = await service.GetStockMovementsAsync(
            TestIds.OrganizationId, TestIds.BranchId, product.ProductId, limit: 50, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);
        // Различаем движения по автору: StockMovement(...) хардкодит Reason, а idempotencyKey (4-й арг) ≠ Reason.
        var known = Assert.Single(result.Response, m => m.CreatedByStaffUserId == ActorStaffUserId);
        Assert.Equal("Олег С.", known.CreatedByDisplayName);
        var unknown = Assert.Single(result.Response, m => m.CreatedByStaffUserId == unknownActor);
        Assert.Null(unknown.CreatedByDisplayName);
    }
```

- [ ] **Step 2: Запустить — убедиться, что падает**

Run: `/home/fedya/.dotnet/dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~GetStockMovementsAsync_ResolvesCreatedByDisplayName"`
Expected: FAIL компиляции (нет поля `CreatedByDisplayName`).

- [ ] **Step 3: Добавить поле в контракт**

`src/AFK4.Shared.Contracts/Inventory/StockMovementDto.cs` — добавить трейлинг-параметр:
```csharp
public sealed record StockMovementDto(
    Guid StockMovementId,
    Guid OrganizationId,
    Guid BranchId,
    Guid ProductId,
    string MovementType,
    int QuantityDelta,
    MoneyDto UnitCost,
    string Reason,
    Guid CreatedByStaffUserId,
    DateTimeOffset CreatedAtUtc,
    string? CreatedByDisplayName = null);
```

- [ ] **Step 4: Резолв имени в сервисе**

В `EfInventoryService.cs`:

(а) `ToDto(StockMovementEntity)` — добавить опциональный параметр имени:
```csharp
    private static StockMovementDto ToDto(StockMovementEntity movement, string? createdByDisplayName = null)
    {
        return new StockMovementDto(
            movement.StockMovementId,
            movement.OrganizationId,
            movement.BranchId,
            movement.ProductId,
            movement.MovementType,
            movement.QuantityDelta,
            new MoneyDto(movement.CurrencyCode, movement.UnitCostMinorUnits),
            movement.Reason,
            movement.CreatedByStaffUserId,
            movement.CreatedAtUtc,
            createdByDisplayName);
    }
```
(Существующий вызов `var response = ToDto(movement);` в `CreateStockMovementAsync` остаётся — имя `null` по умолчанию.)

(б) В `GetStockMovementsAsync`, ПОСЛЕ получения `movements` (список) и ПЕРЕД `return`, заменить финальный `return` на резолв имён:
```csharp
        var movements = await query
            .OrderByDescending(movement => movement.CreatedAtUtc)
            .ThenByDescending(movement => movement.StockMovementId)
            .Take(Math.Min(limit, 200))
            .ToListAsync(cancellationToken);

        var staffIds = movements.Select(movement => movement.CreatedByStaffUserId).Distinct().ToList();
        var staffNames = await dbContext.StaffUsers
            .AsNoTracking()
            .Where(staff => staff.OrganizationId == organizationId && staffIds.Contains(staff.StaffUserId))
            .ToDictionaryAsync(staff => staff.StaffUserId, staff => staff.DisplayName, cancellationToken);

        return BillingCommandServiceResult<IReadOnlyList<StockMovementDto>>.Ok(
            movements.Select(movement => ToDto(movement, staffNames.GetValueOrDefault(movement.CreatedByStaffUserId))).ToList());
```
(Удалить прежний `return BillingCommandServiceResult<…>.Ok(movements.Select(ToDto).ToList());`.)

- [ ] **Step 5: Обновить контракт-тест round-trip**

В `tests/AFK4.Shared.Contracts.Tests/InventoryContractSerializationTests.cs`, в `StockMovementDto_RoundTrips`, добавить новый аргумент в конструктор и ассерт:
```csharp
        var movement = new StockMovementDto(
            // … существующие позиционные аргументы …
            CreatedByStaffUserId: Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134"),
            CreatedAtUtc: DateTimeOffset.Parse("2026-05-13T10:00:00Z"),
            CreatedByDisplayName: "Олег С.");
```
И после десериализации:
```csharp
        Assert.Equal("Олег С.", copy!.CreatedByDisplayName);
```

- [ ] **Step 6: Запустить тесты — зелено**

Run: `/home/fedya/.dotnet/dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~GetStockMovements" && /home/fedya/.dotnet/dotnet test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter "FullyQualifiedName~StockMovementDto_RoundTrips"`
Expected: PASS. (Если падают другие места, конструирующие `StockMovementDto` позиционно без нового поля — их не должно быть, поле опциональное; но если компилятор укажет — там просто добавить `CreatedByDisplayName` нет нужды, дефолт применится.)

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.Shared.Contracts/Inventory/StockMovementDto.cs src/AFK4.Platform.Api/Inventory/EfInventoryService.cs tests/AFK4.Platform.Api.Tests/EfInventoryServiceTests.cs tests/AFK4.Shared.Contracts.Tests/InventoryContractSerializationTests.cs
git commit -m "feat(inventory): StockMovementDto.CreatedByDisplayName — сервер резолвит имя автора движения"
```

---

### Task 2: Фронт — чистая логика журнала (`journalModel.ts`)

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/stock/journalModel.ts`
- Test: `src/AFK4.Operator.App.Web/src/stock/journalModel.test.ts`
- Modify: `src/AFK4.Operator.App.Web/src/operatorHelpers.ts` (добавить `refund`-кейс в `stockMovementTypeLabel`)
- Modify: `locales/{ru,en,tg}.json` (ключ `op.helper.stock.refund`, если его нет) + gen

**Interfaces:**
- Consumes: `PosProductDto`, `StockMovementDto` (из `../operatorApiClients`); `readMoney`/`readNumber`/`readString` (из `../operatorHelpers`).
- Produces:
  - `type MovementType = 'purchase' | 'sale' | 'refund' | 'adjustment'`
  - `type JournalTypeFilter = 'all' | MovementType`
  - `type JournalPeriod = 'today' | 'week' | 'all'`
  - `interface JournalRow { id; productId; name; sku; type; quantityDelta; unitCostMinorUnits; sumMinorUnits; reason; who; createdAtUtc }`
  - `mapMovementsToRows(movements: StockMovementDto[], catalog: PosProductDto[]): JournalRow[]`
  - `filterByType(rows, filter): JournalRow[]`
  - `filterByPeriod(rows, period, nowMs): JournalRow[]`
  - `interface DayGroup { dayKey: string; rows: JournalRow[] }` + `groupByDay(rows): DayGroup[]`
  - `interface JournalSummary { inboundQty; inboundSumMinor; soldQty; writtenOffQty; writtenOffSumMinor; netQty }` + `summarize(rows): JournalSummary`
  - `buildCsv(rows, opts: { headers: string[]; typeLabel: (type: string) => string; formatMoney: (minor: number) => string; formatDateTime: (iso: string) => string }): string`

- [ ] **Step 1: Добавить `refund` в `stockMovementTypeLabel` + ключ**

В `operatorHelpers.ts`, в `stockMovementTypeLabel`, добавить кейс перед `default`:
```ts
    case 'refund':
      return t('op.helper.stock.refund');
```
Проверить, есть ли ключ `op.helper.stock.refund` в `locales/ru.json` (grep). Если нет — добавить во все три локали рядом с `op.helper.stock.sale`:
```json
  "op.helper.stock.refund": "Возврат",
```
en: `"op.helper.stock.refund": "Refund",` · tg: `"op.helper.stock.refund": "Баргардонидан",`
Затем `cd packages/i18n && bun run gen && cd ../..`.

- [ ] **Step 2: Падающий тест модели**

`src/AFK4.Operator.App.Web/src/stock/journalModel.test.ts`:
```ts
import { describe, it, expect } from 'bun:test';
import {
  mapMovementsToRows, filterByType, filterByPeriod, groupByDay, summarize, buildCsv,
  type JournalRow,
} from './journalModel';

const catalog = [
  { productId: 'p1', name: 'Cola 0.5', sku: 'COLA-05' },
  { productId: 'p2', name: 'Чипсы Lays', sku: 'CHIPS-LAYS' },
] as never[];

const mv = (over: Record<string, unknown>) => ({
  stockMovementId: `m-${over.reason ?? Math.abs(Number(over.quantityDelta) || 0)}`,
  productId: 'p1', movementType: 'purchase', quantityDelta: 10,
  unitCost: { currencyCode: 'TJS', minorUnits: 400 }, reason: 'Приёмка',
  createdByStaffUserId: 's1', createdByDisplayName: 'Олег С.', createdAtUtc: '2026-06-25T10:00:00Z',
  ...over,
}) as never;

describe('journalModel', () => {
  it('mapMovementsToRows резолвит имя/sku из каталога, считает сумму = |кол-во|×себест', () => {
    const rows = mapMovementsToRows([mv({ quantityDelta: -3, unitCost: { currencyCode: 'TJS', minorUnits: 500 } })], catalog);
    expect(rows[0]).toMatchObject({ name: 'Cola 0.5', sku: 'COLA-05', quantityDelta: -3, unitCostMinorUnits: 500, sumMinorUnits: 1500, who: 'Олег С.' });
  });

  it('mapMovementsToRows: товар вне каталога → name=fallback (productId), sku пустой', () => {
    const rows = mapMovementsToRows([mv({ productId: 'gone' })], catalog);
    expect(rows[0].name).toBe('gone');
    expect(rows[0].sku).toBe('');
  });

  it('filterByType оставляет только нужный movementType', () => {
    const rows = mapMovementsToRows([mv({ movementType: 'purchase' }), mv({ movementType: 'sale', quantityDelta: -1 })], catalog);
    expect(filterByType(rows, 'all')).toHaveLength(2);
    expect(filterByType(rows, 'sale').every((r) => r.type === 'sale')).toBe(true);
    expect(filterByType(rows, 'sale')).toHaveLength(1);
  });

  it('filterByPeriod today/week по UTC относительно nowMs', () => {
    const now = Date.parse('2026-06-25T12:00:00Z');
    const rows = mapMovementsToRows([
      mv({ reason: 'a', createdAtUtc: '2026-06-25T08:00:00Z' }), // сегодня
      mv({ reason: 'b', createdAtUtc: '2026-06-22T08:00:00Z' }), // 3 дня назад
      mv({ reason: 'c', createdAtUtc: '2026-06-10T08:00:00Z' }), // >7 дней
    ], catalog);
    expect(filterByPeriod(rows, 'today', now).map((r) => r.reason)).toEqual(['a']);
    expect(filterByPeriod(rows, 'week', now).map((r) => r.reason).sort()).toEqual(['a', 'b']);
    expect(filterByPeriod(rows, 'all', now)).toHaveLength(3);
  });

  it('groupByDay группирует по UTC-дню, новые дни первыми', () => {
    const rows = mapMovementsToRows([
      mv({ reason: 'a', createdAtUtc: '2026-06-25T08:00:00Z' }),
      mv({ reason: 'b', createdAtUtc: '2026-06-24T08:00:00Z' }),
      mv({ reason: 'c', createdAtUtc: '2026-06-25T20:00:00Z' }),
    ], catalog);
    const groups = groupByDay(rows);
    expect(groups[0].dayKey).toBe('2026-06-25');
    expect(groups[0].rows).toHaveLength(2);
    expect(groups[1].dayKey).toBe('2026-06-24');
  });

  it('summarize считает приход/продажи/списания/чистое движение', () => {
    const rows = mapMovementsToRows([
      mv({ movementType: 'purchase', quantityDelta: 12, unitCost: { currencyCode: 'TJS', minorUnits: 900 } }),
      mv({ movementType: 'sale', quantityDelta: -5 }),
      mv({ movementType: 'adjustment', quantityDelta: -3, unitCost: { currencyCode: 'TJS', minorUnits: 600 } }),
    ], catalog);
    const s = summarize(rows);
    expect(s.inboundQty).toBe(12);
    expect(s.inboundSumMinor).toBe(12 * 900);
    expect(s.soldQty).toBe(5);
    expect(s.writtenOffQty).toBe(3);
    expect(s.writtenOffSumMinor).toBe(3 * 600);
    expect(s.netQty).toBe(12 - 5 - 3);
  });

  it('buildCsv: заголовок + строки, экранирование запятых/кавычек', () => {
    const rows = mapMovementsToRows([mv({ quantityDelta: -2, reason: 'брак, упаковки', unitCost: { currencyCode: 'TJS', minorUnits: 600 } })], catalog);
    const csv = buildCsv(rows, {
      headers: ['Дата', 'Тип', 'Товар', 'SKU', 'Кол-во', 'Себест', 'Сумма', 'Причина', 'Кто'],
      typeLabel: (type) => type,
      formatMoney: (minor) => (minor / 100).toFixed(2),
      formatDateTime: (iso) => iso,
    });
    const lines = csv.trim().split('\n');
    expect(lines[0]).toContain('Дата');
    // запятая в причине → поле в кавычках
    expect(lines[1]).toContain('"брак, упаковки"');
    expect(lines[1]).toContain('-2');
  });
});
```

- [ ] **Step 3: Запустить — падает**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && ~/.bun/bin/bun test src/stock/journalModel.test.ts`
Expected: FAIL (нет `./journalModel`).

- [ ] **Step 4: Реализация `journalModel.ts`**

```ts
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
```

- [ ] **Step 5: Запустить — зелено**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && ~/.bun/bin/bun test src/stock/journalModel.test.ts`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/stock/journalModel.ts src/AFK4.Operator.App.Web/src/stock/journalModel.test.ts src/AFK4.Operator.App.Web/src/operatorHelpers.ts locales/ packages/i18n/src/messages.ts
git commit -m "feat(stock): чистая логика журнала движений (map/filter/period/group/summary/csv)"
```

---

### Task 3: Фронт — вкладка «Журнал» в каркасе раздела

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/stock/stockModel.ts` (+ `'journal'`)
- Modify: `src/AFK4.Operator.App.Web/src/stock/StockWorkspace.tsx` (рендер `JournalWorkspace` под `journal`)
- Modify: `src/AFK4.Operator.App.Web/src/stock/stockModel.test.ts` (+ кейс)
- Modify: `locales/{ru,en,tg}.json` (`op.stock.tab.journal`) + gen
- Modify: `src/AFK4.Operator.App.Web/src/stock/StockWorkspace.test.tsx` (+ кейс на вкладку Журнал)

**Interfaces:**
- Produces: `StockTab = 'levels' | 'receiving' | 'journal'`; `STOCK_TAB_ORDER = ['levels','receiving','journal']`; `STOCK_TAB_PERMISSIONS.journal = [viewInventory, manageInventoryStock]`. `StockWorkspace` рендерит `JournalWorkspace` (создаётся в Task 4) под `journal`. **На время Task 3 `JournalWorkspace` ещё нет — рендерить временную заглушку `<div className="stock-journal-pending" />`, которую Task 4 заменит.**

- [ ] **Step 1: Ключ вкладки в локали + gen**

`locales/ru.json`: `"op.stock.tab.journal": "Журнал",` · en: `"op.stock.tab.journal": "Journal",` · tg: `"op.stock.tab.journal": "Журнали ҳаракат",`
Затем `cd packages/i18n && bun run gen && cd ../..`.

- [ ] **Step 2: Падающий тест**

В `src/AFK4.Operator.App.Web/src/stock/StockWorkspace.test.tsx` добавить кейс (мок `getStockMovements` уже понадобится — добавить в стаб inventory):
```ts
  it('вкладка «Журнал» видна и переключается', async () => {
    view(manageSession);
    const journalTab = screen.getByRole('tab', { name: 'Журнал' });
    fireEvent.click(journalTab);
    expect(journalTab).toHaveAttribute('aria-selected', 'true');
  });
```
(Если стаб `createAuthenticatedOperatorClients` в этом файле не отдаёт `inventory.getStockMovements` — добавить его как `mock(async () => [])`, чтобы заглушка Журнала не падала. На Task 3 рендерится `stock-journal-pending` — фетча ещё нет, но стаб не помешает.)

- [ ] **Step 3: Запустить — падает**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && ~/.bun/bin/bun test src/stock/StockWorkspace.test.tsx src/stock/stockModel.test.ts`
Expected: FAIL (нет вкладки Журнал).

- [ ] **Step 4: Расширить `stockModel.ts`**

```ts
export type StockTab = 'levels' | 'receiving' | 'journal';

export const STOCK_TAB_ORDER: readonly StockTab[] = ['levels', 'receiving', 'journal'];

export const STOCK_TAB_PERMISSIONS: Record<StockTab, readonly string[]> = {
  levels: [permissionNames.viewInventory, permissionNames.manageInventoryStock],
  receiving: [permissionNames.manageInventoryStock],
  journal: [permissionNames.viewInventory, permissionNames.manageInventoryStock],
};
```
Добавить кейс в `stockModel.test.ts`:
```ts
it('journal виден при праве просмотра инвентаря', () => {
  expect(visibleStockTabs({ permissions: ['inventory.view'] } as never)).toContain('journal');
});
```

- [ ] **Step 5: Подключить заглушку в `StockWorkspace.tsx`**

В `TAB_LABELS` добавить `journal: 'op.stock.tab.journal'`. В контент добавить:
```tsx
        {activeTab === 'journal' && (
          // Task 4 заменит заглушку на <JournalWorkspace …/>.
          <div className="stock-journal-pending" />
        )}
```

- [ ] **Step 6: Запустить — зелено**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && ~/.bun/bin/bun test src/stock/StockWorkspace.test.tsx src/stock/stockModel.test.ts`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/stock/ locales/ packages/i18n/src/messages.ts
git commit -m "feat(stock): вкладка «Журнал» в каркасе раздела (заглушка под экран)"
```

---

### Task 4: Фронт — экран «Журнал» (`JournalWorkspace`) + CSV-экспорт

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/stock/JournalWorkspace.tsx`
- Test: `src/AFK4.Operator.App.Web/src/stock/JournalWorkspace.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/stock/StockWorkspace.tsx` (заменить заглушку)
- Modify: `src/AFK4.Operator.App.Web/src/styles/22-stock.css` (стили журнала)
- Modify: `locales/{ru,en,tg}.json` (`op.stock.journal.*`) + gen
- Modify: `src/AFK4.Operator.App.Web/src/devMockBackend.ts` (GET stock-movements → пример движений)

**Interfaces:**
- Consumes: `journalModel` (Task 2), `stockMovementTypeLabel`/`createAuthenticatedOperatorClients`/`readString` (из `../operatorHelpers`), `formatMinorUnits` (из `../currencyFormat`), `projectOperatorError`, `hasPermission`/`permissionNames`.
- Produces: `JournalWorkspace` со пропсами `{ backend, currencyCode, session }`.

- [ ] **Step 1: Ключи `op.stock.journal.*` + gen**

`locales/ru.json` (en/tg — аналогично, tg НАСТОЯЩИЙ таджикский):
```json
  "op.stock.journal.title": "Журнал движений",
  "op.stock.journal.head": "Движения склада",
  "op.stock.journal.search": "Товар…",
  "op.stock.journal.export": "Экспорт CSV",
  "op.stock.journal.empty": "Движений по складу пока нет",
  "op.stock.journal.emptyFiltered": "Нет движений под фильтр",
  "op.stock.journal.loading": "Загрузка движений…",
  "op.stock.journal.noPermission": "Недостаточно прав для просмотра журнала",
  "op.stock.journal.capNote": "Показаны последние {count} движений",
  "op.stock.journal.filter.all": "Все",
  "op.stock.journal.unit": "шт",
  "op.stock.journal.period.title": "Период",
  "op.stock.journal.period.today": "Сегодня",
  "op.stock.journal.period.week": "7 дней",
  "op.stock.journal.period.all": "Все",
  "op.stock.journal.summary.title": "Итог за период",
  "op.stock.journal.summary.inbound": "Приход",
  "op.stock.journal.summary.sold": "Продажи",
  "op.stock.journal.summary.writtenOff": "Списания",
  "op.stock.journal.summary.net": "Чистое движение",
  "op.stock.journal.csv.dateTime": "Дата и время",
  "op.stock.journal.csv.type": "Тип",
  "op.stock.journal.csv.product": "Товар",
  "op.stock.journal.csv.sku": "SKU",
  "op.stock.journal.csv.qty": "Кол-во",
  "op.stock.journal.csv.unitCost": "Себестоимость ед.",
  "op.stock.journal.csv.sum": "Сумма",
  "op.stock.journal.csv.reason": "Причина",
  "op.stock.journal.csv.who": "Кто",
  "op.stock.journal.today": "Сегодня",
  "op.stock.journal.yesterday": "Вчера",
```
en — англ. эквиваленты; tg — таджикский (напр. `period.today`="Имрӯз", `period.week`="7 рӯз", `summary.inbound`="Воридот", `summary.sold`="Фурӯш", `summary.writtenOff`="Ҳисоббор", `summary.net`="Ҳаракати соф", `today`="Имрӯз", `yesterday`="Дирӯз", `export`="Содироти CSV", `head`="Ҳаракати анбор", `who`="Кӣ" и т.д. — переводить осмысленно, не копией ru).
Затем `cd packages/i18n && bun run gen && cd ../..`.

- [ ] **Step 2: Падающий тест экрана**

`src/AFK4.Operator.App.Web/src/stock/JournalWorkspace.test.tsx`:
```ts
import { describe, it, expect, mock, afterEach, afterAll } from 'bun:test';
import { render, screen, fireEvent, cleanup, within } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';

const getCatalog = mock(async (_branchId: string) => ([
  { productId: 'p1', name: 'Cola 0.5', sku: 'COLA-05', trackStock: true },
  { productId: 'p2', name: 'Чипсы Lays', sku: 'CHIPS-LAYS', trackStock: true },
]));
const getStockMovements = mock(async (_branchId: string, _query?: unknown) => ([
  { stockMovementId: 'm1', productId: 'p1', movementType: 'purchase', quantityDelta: 12, unitCost: { currencyCode: 'TJS', minorUnits: 400 }, reason: 'Приёмка · Напитки', createdByStaffUserId: 's1', createdByDisplayName: 'Олег С.', createdAtUtc: '2026-06-25T10:05:00Z' },
  { stockMovementId: 'm2', productId: 'p2', movementType: 'adjustment', quantityDelta: -2, unitCost: { currencyCode: 'TJS', minorUnits: 600 }, reason: 'брак', createdByStaffUserId: 's1', createdByDisplayName: 'Олег С.', createdAtUtc: '2026-06-25T13:30:00Z' },
  { stockMovementId: 'm3', productId: 'p1', movementType: 'sale', quantityDelta: -1, unitCost: { currencyCode: 'TJS', minorUnits: 400 }, reason: 'чек #1042', createdByStaffUserId: 's1', createdByDisplayName: 'Олег С.', createdAtUtc: '2026-06-25T13:42:00Z' },
]));
const actual = (globalThis as Record<string, unknown>).__afk4RealOperatorHelpers as Record<string, unknown>;
mock.module('../operatorHelpers', () => ({ ...actual, createAuthenticatedOperatorClients: () => ({ pos: { getCatalog }, inventory: { getStockMovements } }) }));

const { JournalWorkspace } = await import('./JournalWorkspace');

const backend = { config: { platformBaseUrl: 'http://x' }, session: { accessToken: 't', organizationId: 'o' }, branchId: 'b' } as never;
const session = { permissions: ['inventory.view'], organizationId: 'o' } as never;
const view = () => render(<I18nProvider initialLocale="ru"><JournalWorkspace backend={backend} currencyCode="TJS" session={session} /></I18nProvider>);

afterEach(() => { getCatalog.mockClear(); getStockMovements.mockClear(); cleanup(); });
afterAll(() => mock.restore());

describe('JournalWorkspace', () => {
  it('показывает движения с резолвом имени товара, типом и автором', async () => {
    view();
    await screen.findByText('Cola 0.5');
    expect(screen.getByText('Чипсы Lays')).toBeInTheDocument();
    // автор движения
    expect(screen.getAllByText('Олег С.').length).toBeGreaterThan(0);
  });

  it('фильтр по типу «Продажа» оставляет только sale', async () => {
    view();
    await screen.findByText('Cola 0.5');
    fireEvent.click(screen.getByRole('button', { name: /Продажа/ }));
    const list = screen.getByLabelText('Движения склада');
    expect(within(list).queryByText('Чипсы Lays')).not.toBeInTheDocument(); // adjustment скрыт
    expect(within(list).getByText('Cola 0.5')).toBeInTheDocument(); // sale остался
  });

  it('без права — экран отказа', () => {
    render(<I18nProvider initialLocale="ru"><JournalWorkspace backend={backend} currencyCode="TJS" session={{ permissions: [], organizationId: 'o' } as never} /></I18nProvider>);
    expect(screen.getByText('Недостаточно прав для просмотра журнала')).toBeInTheDocument();
  });

  it('кнопка «Экспорт CSV» доступна при наличии движений', async () => {
    view();
    await screen.findByText('Cola 0.5');
    expect(screen.getByRole('button', { name: 'Экспорт CSV' })).toBeEnabled();
  });
});
```

- [ ] **Step 3: Запустить — падает**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && ~/.bun/bin/bun test src/stock/JournalWorkspace.test.tsx`
Expected: FAIL (нет `./JournalWorkspace`).

- [ ] **Step 4: Реализация `JournalWorkspace.tsx`**

```tsx
import { useEffect, useMemo, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import type { MessageKey } from '@afk4/i18n';
import { ArrowDownToLine } from 'lucide-react';
import { createAuthenticatedOperatorClients, stockMovementTypeLabel } from '../operatorHelpers';
import { formatMinorUnits } from '../currencyFormat';
import { projectOperatorError } from '../apiErrors';
import { hasAnyPermission, permissionNames } from '../operatorPermissions';
import type { PosProductDto, StockMovementDto } from '../operatorApiClients';
import type { OperatorBackendContext } from '../operatorTypes';
import type { OperatorAuthSession } from '../authClient';
import {
  mapMovementsToRows, filterByType, filterByPeriod, groupByDay, summarize, buildCsv,
  type JournalRow, type JournalTypeFilter, type JournalPeriod,
} from './journalModel';

const TYPE_FILTERS: JournalTypeFilter[] = ['all', 'purchase', 'sale', 'refund', 'adjustment'];
const PERIODS: JournalPeriod[] = ['today', 'week', 'all'];
const PERIOD_LABEL_KEYS: Record<JournalPeriod, MessageKey> = {
  today: 'op.stock.journal.period.today',
  week: 'op.stock.journal.period.week',
  all: 'op.stock.journal.period.all',
};
const MOVEMENT_LIMIT = 200;

// Класс чипа типа для цвета: приход зелёный (+), списание/коррекция-минус янтарь, прочее нейтральное.
function rowTone(row: JournalRow): string {
  if (row.type === 'purchase' || (row.type === 'adjustment' && row.quantityDelta > 0)) return 'plus';
  if (row.type === 'adjustment' && row.quantityDelta < 0) return 'warn';
  return 'minus';
}

export function JournalWorkspace({
  backend,
  currencyCode,
  session,
}: {
  backend: OperatorBackendContext | null;
  currencyCode: string;
  session: OperatorAuthSession | null;
}) {
  const { t, locale } = useI18n();
  const canView = hasAnyPermission(session, [permissionNames.viewInventory, permissionNames.manageInventoryStock]);

  const clients = useMemo(
    () => (backend && canView ? createAuthenticatedOperatorClients(backend.config, backend.session) : null),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [backend?.config, backend?.session, canView]
  );

  const [movements, setMovements] = useState<StockMovementDto[]>([]);
  const [catalog, setCatalog] = useState<PosProductDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [typeFilter, setTypeFilter] = useState<JournalTypeFilter>('all');
  // Дефолт 'all' (последние ≤200) — всегда показывает свежую активность, без пустоты тихим утром
  // и без завязки тестов на текущую дату. Сегодня/7 дней — опциональное сужение.
  const [period, setPeriod] = useState<JournalPeriod>('all');
  const [search, setSearch] = useState('');

  useEffect(() => {
    if (!canView || clients === null || backend === null) { setLoading(false); return; }
    let alive = true;
    setLoading(true);
    setLoadError(null);
    Promise.all([
      clients.inventory.getStockMovements(backend.branchId, { limit: MOVEMENT_LIMIT }),
      clients.pos.getCatalog(backend.branchId),
    ])
      .then(([loadedMovements, loadedCatalog]) => {
        if (!alive) return;
        setMovements(Array.isArray(loadedMovements) ? loadedMovements as StockMovementDto[] : []);
        setCatalog(Array.isArray(loadedCatalog) ? loadedCatalog as PosProductDto[] : []);
      })
      .catch((error) => { if (alive) setLoadError(projectOperatorError(error, t).detail); })
      .finally(() => { if (alive) setLoading(false); });
    return () => { alive = false; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [clients, backend?.branchId, canView]);

  const dateTimeFmt = useMemo(() => new Intl.DateTimeFormat(locale, { hour: '2-digit', minute: '2-digit' }), [locale]);
  const dayFmt = useMemo(() => new Intl.DateTimeFormat(locale, { day: 'numeric', month: 'long' }), [locale]);

  if (!canView) {
    return <section className="stock-journal"><p className="workspace-error">{t('op.stock.journal.noPermission')}</p></section>;
  }
  if (loading) {
    return <div className="stock-layout"><section className="stock-journal"><p className="workspace-loading">{t('op.stock.journal.loading')}</p></section></div>;
  }
  if (loadError) {
    return <div className="stock-layout"><section className="stock-journal"><p className="workspace-error" role="alert">{loadError}</p></section></div>;
  }

  const allRows = mapMovementsToRows(movements, catalog);
  const nowMs = Date.now();
  const periodRows = filterByPeriod(allRows, period, nowMs);
  const query = search.trim().toLowerCase();
  const rows = filterByType(periodRows, typeFilter).filter((row) =>
    !query || row.name.toLowerCase().includes(query) || row.sku.toLowerCase().includes(query));
  const groups = groupByDay(rows);
  const summary = summarize(periodRows);

  const dayLabel = (dayKey: string): string => {
    const todayKey = new Date(nowMs).toISOString().slice(0, 10);
    const yesterdayKey = new Date(nowMs - 86_400_000).toISOString().slice(0, 10);
    if (dayKey === todayKey) return t('op.stock.journal.today');
    if (dayKey === yesterdayKey) return t('op.stock.journal.yesterday');
    return dayFmt.format(new Date(`${dayKey}T00:00:00Z`));
  };

  const exportCsv = () => {
    const csv = buildCsv(rows, {
      headers: [
        t('op.stock.journal.csv.dateTime'), t('op.stock.journal.csv.type'), t('op.stock.journal.csv.product'),
        t('op.stock.journal.csv.sku'), t('op.stock.journal.csv.qty'), t('op.stock.journal.csv.unitCost'),
        t('op.stock.journal.csv.sum'), t('op.stock.journal.csv.reason'), t('op.stock.journal.csv.who'),
      ],
      typeLabel: (type) => stockMovementTypeLabel(type, t),
      formatMoney: (minor) => (minor / 100).toFixed(2),
      formatDateTime: (iso) => new Date(iso).toISOString().replace('T', ' ').slice(0, 16),
    });
    const blob = new Blob([`﻿${csv}`], { type: 'text/csv;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `stock-journal-${new Date(nowMs).toISOString().slice(0, 10)}.csv`;
    anchor.click();
    URL.revokeObjectURL(url);
  };

  const capReached = movements.length >= MOVEMENT_LIMIT;

  return (
    <div className="stock-layout">
      <section className="stock-journal">
        <div className="ledger-head">
          <h2 id="journal-head">{t('op.stock.journal.head')}</h2>
          <div className="seg">
            {TYPE_FILTERS.map((filter) => (
              <button
                key={filter}
                type="button"
                className={typeFilter === filter ? 'on' : ''}
                aria-pressed={typeFilter === filter}
                onClick={() => setTypeFilter(filter)}
              >
                {filter === 'all' ? t('op.stock.journal.filter.all') : stockMovementTypeLabel(filter, t)}
              </button>
            ))}
          </div>
          <div className="panel-search">
            <input
              type="search"
              placeholder={t('op.stock.journal.search')}
              value={search}
              aria-label={t('op.stock.journal.search')}
              onChange={(event) => setSearch(event.currentTarget.value)}
            />
          </div>
          <button type="button" className="cash-command-btn journal-export" onClick={exportCsv} disabled={rows.length === 0}>
            <ArrowDownToLine size={14} aria-hidden="true" />
            {t('op.stock.journal.export')}
          </button>
        </div>

        {capReached && <p className="journal-cap">{t('op.stock.journal.capNote', { count: MOVEMENT_LIMIT })}</p>}

        {allRows.length === 0 ? (
          <p className="cash-shift-empty-note">{t('op.stock.journal.empty')}</p>
        ) : rows.length === 0 ? (
          <p className="cash-shift-empty-note">{t('op.stock.journal.emptyFiltered')}</p>
        ) : (
          <div className="jledger" aria-label={t('op.stock.journal.head')} aria-describedby="journal-head">
            {groups.map((group) => (
              <div key={group.dayKey}>
                <div className="daygroup">{dayLabel(group.dayKey)}</div>
                <ul className="jlist">
                  {group.rows.map((row) => {
                    const tone = rowTone(row);
                    return (
                      <li key={row.id} className="jrow">
                        <span className="jtime">{dateTimeFmt.format(new Date(row.createdAtUtc))}</span>
                        <span className={`jtype ${tone}`}>{stockMovementTypeLabel(row.type, t)}</span>
                        <div className="jname">
                          <strong>{row.name}</strong>
                          <em>{row.sku}{row.reason ? ` · ${row.reason}` : ''}</em>
                        </div>
                        <span className={`jqty ${tone}`}>
                          {row.quantityDelta > 0 ? '+' : ''}{row.quantityDelta} {t('op.stock.journal.unit')}
                        </span>
                        <span className="jsum">{row.sumMinorUnits > 0 ? formatMinorUnits(row.sumMinorUnits, currencyCode) : '—'}</span>
                        <span className="jwho">{row.who || '—'}</span>
                      </li>
                    );
                  })}
                </ul>
              </div>
            ))}
          </div>
        )}
      </section>

      <aside className="stock-summary">
        <div className="ctx-card">
          <h3 className="ctx-title">{t('op.stock.journal.period.title')}</h3>
          <div className="period">
            {PERIODS.map((value) => (
              <button
                key={value}
                type="button"
                className={period === value ? 'on' : ''}
                aria-pressed={period === value}
                onClick={() => setPeriod(value)}
              >
                {t(PERIOD_LABEL_KEYS[value])}
              </button>
            ))}
          </div>
        </div>

        <div className="ctx-card">
          <h3 className="ctx-title">{t('op.stock.journal.summary.title')}</h3>
          <div className="totrow"><span>{t('op.stock.journal.summary.inbound')}</span><b className="in">+{summary.inboundQty} · {formatMinorUnits(summary.inboundSumMinor, currencyCode)}</b></div>
          <div className="totrow"><span>{t('op.stock.journal.summary.sold')}</span><b>−{summary.soldQty}</b></div>
          <div className="totrow"><span>{t('op.stock.journal.summary.writtenOff')}</span><b className="wn">−{summary.writtenOffQty} · {formatMinorUnits(summary.writtenOffSumMinor, currencyCode)}</b></div>
          <div className="totrow net"><span>{t('op.stock.journal.summary.net')}</span><b>{summary.netQty > 0 ? '+' : ''}{summary.netQty}</b></div>
        </div>
      </aside>
    </div>
  );
}
```
(Если `useI18n()` не отдаёт `locale` — взять текущую локаль из публичного API i18n-пакета; свериться с тем, как другие экраны берут локаль для `Intl`. Если способа нет — использовать фиксированный `'ru'` для `Intl.DateTimeFormat` НЕ годится для en/tg; тогда взять `document.documentElement.lang` или импортировать хук локали. Сначала проверь сигнатуру `useI18n`.)

- [ ] **Step 5: Подключить экран в `StockWorkspace.tsx`**

Заменить заглушку из Task 3:
```tsx
import { JournalWorkspace } from './JournalWorkspace';
// …
{activeTab === 'journal' && (
  <JournalWorkspace backend={backend} currencyCode={currencyCode} session={session} />
)}
```

- [ ] **Step 6: Стили журнала в `22-stock.css`** (дописать в конец)

```css
/* ── Экран «Журнал движений» ── */
.stock-journal {
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  border: 1px solid var(--border-soft);
  border-radius: var(--radius-md);
  padding: var(--space-3);
  background: var(--surface-elevated);
  min-height: 0;
  overflow: auto;
}

.stock-journal .ledger-head {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  flex-wrap: wrap;
}

.stock-journal .ledger-head h2 {
  margin: 0;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: var(--text-tertiary);
  font-size: var(--text-xs);
  font-weight: 700;
}

.journal-export { margin-left: var(--space-2); }
.journal-export:disabled { opacity: 0.5; cursor: not-allowed; }

.journal-cap { margin: 0; color: var(--text-tertiary); font-size: 11px; }

.jledger { display: flex; flex-direction: column; gap: 2px; }

.daygroup {
  color: var(--text-quaternary);
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  margin: 8px 4px 2px;
}

.jlist { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: var(--space-1); }

.jrow {
  display: grid;
  grid-template-columns: 52px 116px minmax(160px, 1fr) 92px 96px 140px;
  align-items: center;
  gap: 12px;
  padding: var(--space-2);
  border: 1px solid var(--border-default);
  border-radius: var(--radius-sm);
  background: var(--surface-card);
}

.jrow:hover { border-color: var(--border-accent); }

.jtime {
  color: var(--text-secondary);
  font-family: var(--font-mono);
  font-variant-numeric: tabular-nums;
  font-size: 12px;
}

.jtype { font-size: 12px; font-weight: 600; }
.jtype.plus { color: var(--accent-text); }
.jtype.warn { color: var(--warning); }
.jtype.minus { color: var(--text-secondary); }

.jname { min-width: 0; }
.jname strong { display: block; color: var(--text-primary); font-size: var(--text-sm); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.jname em { color: var(--text-tertiary); font-family: var(--font-mono); font-size: 10px; font-style: normal; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; display: block; }

.jqty {
  text-align: right;
  font-family: var(--font-mono);
  font-variant-numeric: tabular-nums;
  font-size: var(--text-sm);
  font-weight: 600;
}
.jqty.plus { color: var(--accent-text); }
.jqty.warn { color: var(--warning); }
.jqty.minus { color: var(--text-strong); }

.jsum {
  text-align: right;
  color: var(--text-tertiary);
  font-family: var(--font-mono);
  font-variant-numeric: tabular-nums;
  font-size: 12px;
}

.jwho {
  color: var(--text-secondary);
  font-size: 12px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

/* Период (правая колонка) */
.period { display: flex; gap: 4px; }
.period button {
  flex: 1;
  border: 1px solid var(--border-soft);
  background: transparent;
  color: var(--text-tertiary);
  border-radius: 6px;
  padding: 6px 0;
  font-size: 12px;
  cursor: pointer;
}
.period button.on { color: var(--text-primary); border-color: var(--border-default); background: var(--surface-sunken); }
.period button:focus-visible { outline: 2px solid var(--accent); outline-offset: 2px; }

/* Итоги периода */
.stock-summary .totrow {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 7px 0;
  font-size: 13px;
  color: var(--text-secondary);
  border-bottom: 1px solid var(--border-soft);
}
.stock-summary .totrow:last-child { border-bottom: none; }
.stock-summary .totrow b { font-family: var(--font-mono); font-variant-numeric: tabular-nums; color: var(--text-primary); }
.stock-summary .totrow b.in { color: var(--accent-text); }
.stock-summary .totrow b.wn { color: var(--warning); }
.stock-summary .totrow.net b { font-weight: 700; }
```

- [ ] **Step 7: Мок движений для превью в `devMockBackend.ts`**

Заменить заглушку `if (pathname.endsWith('/inventory/stock-movements') && method === 'GET') return [];` на возврат примеров (чтобы Журнал в live-превью был не пустой). Использовать СТАТИЧЕСКИЕ ISO-даты (НЕ `new Date()` без аргумента нельзя в воркфлоу, но в обычном модуле можно — однако для стабильности возьми фиксированные строки):
```ts
  if (pathname.endsWith('/inventory/stock-movements') && method === 'GET') return stockMovementsFixture();
  if (pathname.endsWith('/inventory/stock-movements') && method === 'POST') return { stockMovementId: 'mock-movement' };
```
И добавить функцию-фикстуру рядом с прочими (например, около `posCatalog()`), с несколькими движениями (purchase/sale/adjustment), полями как в DTO (включая `createdByDisplayName`). **productId берём РЕАЛЬНЫЕ из `posCatalog()`: `prod-cola`, `prod-chips`, `prod-energy` (проверено — существуют), иначе имена не зарезолвятся.** Даты — через готовый хелпер `todayAtUtc(hour, minute)` (уже есть в файле, привязывает к локальному «сегодня»):
```ts
function stockMovementsFixture() {
  return [
    { stockMovementId: 'mv-1', productId: 'prod-cola', movementType: 'sale', quantityDelta: -1, unitCost: { currencyCode: 'TJS', minorUnits: 400 }, reason: 'чек #1042', createdByStaffUserId: 'staff-1', createdByDisplayName: 'Олег С.', createdAtUtc: todayAtUtc(13, 42) },
    { stockMovementId: 'mv-2', productId: 'prod-chips', movementType: 'adjustment', quantityDelta: -2, unitCost: { currencyCode: 'TJS', minorUnits: 600 }, reason: 'брак упаковки', createdByStaffUserId: 'staff-1', createdByDisplayName: 'Олег С.', createdAtUtc: todayAtUtc(13, 30) },
    { stockMovementId: 'mv-3', productId: 'prod-energy', movementType: 'purchase', quantityDelta: 24, unitCost: { currencyCode: 'TJS', minorUnits: 900 }, reason: 'Приёмка · Напитки Душанбе', createdByStaffUserId: 'staff-1', createdByDisplayName: 'Олег С.', createdAtUtc: todayAtUtc(10, 5) },
  ];
}
```

- [ ] **Step 8: Запустить тесты экрана**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && ~/.bun/bin/bun test src/stock/JournalWorkspace.test.tsx`
Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/stock/ src/AFK4.Operator.App.Web/src/styles/22-stock.css src/AFK4.Operator.App.Web/src/devMockBackend.ts locales/ packages/i18n/src/messages.ts
git commit -m "feat(stock): экран «Журнал движений» — лента, фильтры, период, итоги, экспорт CSV"
```

---

### Task 5: Уборка — убрать историю движений из `Управление → Товары`

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/settings/SettingsGoodsSection.tsx` (убрать блок истории + проп `stockMovements`)
- Modify: `src/AFK4.Operator.App.Web/src/BackendSettingsWorkspace.tsx` (убрать стейт/фетч/проп `stockMovements`)
- Modify: тесты, которые рендерят `SettingsGoodsSection` с `stockMovements` (`settingsSectionsSmoke.test.tsx` и любые в `SettingsGoodsSection`-смоуке) — убрать проп

**Interfaces:**
- `SettingsGoodsSection` больше НЕ принимает `stockMovements`. Форма «записать движение» ОСТАЁТСЯ (единственный путь для произвольных корректировок до S4). Удаляется только блок истории (`settings-stock-history` + заголовок `op.settings.stock.history.title`).

- [ ] **Step 1: Найти потребителей**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && grep -rn "stockMovements\|settings-stock-history\|op.settings.stock.history\|stockMovementTypeLabel" src --include='*.tsx' --include='*.ts'`
Зафиксировать все места, где `stockMovements` передаётся/используется (родитель, секция, тесты).

- [ ] **Step 2: Убрать блок истории из `SettingsGoodsSection.tsx`**

Удалить из пропсов `stockMovements` (и из деструктуризации). Удалить весь блок истории — заголовок `op.settings.stock.history.title` + `settings-config-grid settings-stock-history` со списком (это финальный `<div className="settings-section-title">…история…` и следующий `<div className="settings-config-grid settings-stock-history">…</div>`). Если после удаления `stockMovementTypeLabel`/`readMoney`/`readNumber` стали неиспользуемыми импортами — убрать их (проверь сборкой). Форму «записать движение» и историю-независимый код НЕ трогать.

- [ ] **Step 3: Убрать проводку в `BackendSettingsWorkspace.tsx`**

- Удалить `const [stockMovements, setStockMovements] = useState<StockMovementDto[]>([]);`
- В `Promise.all` убрать строку `apiClients.inventory.getStockMovements(nextBackend.branchId, { limit: 8 }).catch(() => []),` и соответствующую переменную деструктуризации (`stockMovementRows`); поправить порядок деструктуризации массива.
- Удалить `setStockMovements(...)`.
- В рендере `SettingsGoodsSection` убрать проп `stockMovements={stockMovements}`.
- Если импорт типа `StockMovementDto` стал неиспользуемым — убрать.

- [ ] **Step 4: Поправить тесты, рендерящие `SettingsGoodsSection`**

В `settingsSectionsSmoke.test.tsx` (и где ещё всплыло на Step 1) убрать передачу `stockMovements={…}` в `SettingsGoodsSection`. Если был ассерт на отображение истории движений в настройках — удалить его (история теперь в Журнале). Тесты не должны проверять отсутствующий блок.

- [ ] **Step 5: Запустить затронутые тесты + сборку**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && ~/.bun/bin/bun test src/settingsSectionsSmoke.test.tsx && ~/.bun/bin/bun run build`
Expected: тесты PASS, сборка зелёная (важно — `tsc` поймает неиспользуемые импорты/пропсы).

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Operator.App.Web/src
git commit -m "refactor(settings): убрать историю движений из «Товары» — теперь в Журнале склада"
```

---

### Task 6: Финальная проверка раздела + завершение

**Files:** проверка; фиксы при необходимости.

- [ ] **Step 1: Полный фронт-прогон**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && ~/.bun/bin/bun test` затем отдельным прогоном `~/.bun/bin/bun test src/App.test.tsx`
Expected: всё зелёное, включая `i18nKeysExist.test.ts` и tg≠ru guard.

- [ ] **Step 2: Сборка фронта**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && ~/.bun/bin/bun run build`
Expected: без ошибок типов/сборки.

- [ ] **Step 3: Бэк-прогон (затронут контракт + сервис)**

Run: `/home/fedya/.dotnet/dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj && /home/fedya/.dotnet/dotnet test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj`
Expected: всё зелёное.

- [ ] **Step 4: Чистка сирот**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && grep -rn "stock-journal-pending\|settings-stock-history\|op.settings.stock.history" src`
`stock-journal-pending` не должно остаться в коде (Task 4 заменил). Если ключи `op.settings.stock.history.*` стали сиротами (нигде не используются) — убрать из локалей всех трёх языков + `bun run gen` (мёртвый i18n-ключ не оставляем).

- [ ] **Step 5: Commit (если были фиксы)**

```bash
git add -A && git commit -m "chore(stock): финальная чистка слайса журнала"
```
