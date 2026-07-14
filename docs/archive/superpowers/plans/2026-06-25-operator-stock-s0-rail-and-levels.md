# Склад S0 — секция рейла + Остатки v2 — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Вынести склад из вкладки Кассы в новый корневой раздел рейла «Склад» и собрать экран «Остатки v2» (реальный per-product порог, себестоимость/маржа/стоимость остатка, статусы, поиск, фильтры, правая «Сводка»).

**Architecture:** Новый раздел регистрируется как `WorkspaceId 'stock'` (как `cash`/`players`). `StockWorkspace` владеет своим внутренним layout'ом в колонке 2 shell: грид «список + правая Сводка» (3-я колонка shell не задействуется — она привязала бы `App.tsx` к табам Склада). В S0 у раздела один таб «Остатки» (`StockLevelsWorkspace`); следующие табы добавят слайсы S1–S4. Бэк добавляет средневзвешенную себестоимость `AvgCostMinorUnits` на товар, пересчитываемую при `purchase`-движении, и отдаёт её в `PosProductDto`.

**Tech Stack:** React 18 + TypeScript (Vite, `bun test` + happy-dom + jest-dom); .NET 10 minimal API + EF Core (Postgres; тесты на InMemory, xUnit); `@afk4/i18n` (ICU, `locales/{ru,en,tg}.json` → `bun run gen`); `@afk4/tokens` (CSS-токены).

## Global Constraints

- **Тема/токены:** `@afk4/tokens`, dark; акцент = emerald `var(--accent)` `#2cc592` (НЕ синий); палитра нейтрально-серая.
- **Деньги:** белый моноширинный (`--font-mono`, `text-strong`/`text-primary`). **Янтарь (`--warning`) только для предупреждений** (низкий остаток), красный — «нет в наличии». Деньги в DTO — minor units; в major переводить на UI-границе.
- **Порог низкого остатка** — per-product `ReorderThreshold` из DTO; `0` = без алертинга. **Никакого хардкода** `LOW_STOCK_THRESHOLD`.
- **Контраст таблиц:** числа — `text-primary` (яркие); заголовки колонок и SKU — минимум `text-tertiary` (не `quaternary`); границы строк — `border-default`.
- **i18n:** новые ключи — во все три `locales/{ru,en,tg}.json`, затем `bun run gen` (в `packages/i18n`); `tg` — реальный таджикский (не копия `ru`); каждый используемый `t()`-ключ обязан существовать в каталоге.
- **IDOR:** org-id только из `StaffContext.OrganizationId`, не из тела.
- **Гейты:** фронт `bun test` зелёный + `tsc`+`vite build`; бэк `dotnet test tests/AFK4.Platform.Api.Tests` зелёный.
- **Мокап-референс вёрстки:** `.superpowers/brainstorm/3236934-1782385059/content/stock-shell-v5.html` (Остатки) — источник разметки/классов.

---

## File Structure

**Backend (create/modify):**
- Modify `src/AFK4.Platform.Api/Data/PosProductEntity.cs` — поле `AvgCostMinorUnits`.
- Modify `src/AFK4.Shared.Contracts/Pos/PosProductDto.cs` — трейлинг-параметр `AvgCostMinorUnits`.
- Modify `src/AFK4.Platform.Api/Inventory/EfInventoryService.cs` — `ToDto` + пересчёт средней в `CreateStockMovementAsync`.
- Create `src/AFK4.Platform.Api/Data/Migrations/<ts>_AddProductAvgCost.cs` (+ Designer) — через `dotnet ef`.
- Modify `tests/AFK4.Platform.Api.Tests/EfInventoryServiceTests.cs` — тесты средней.

**Frontend (create):**
- `src/stock/StockWorkspace.tsx` — корень раздела (layout + табы).
- `src/stock/StockTabBar.tsx` — полоска вкладок (шаблон `CashTabBar`).
- `src/stock/stockModel.ts` — `StockTab`, порядок/permissions, `visibleStockTabs`.
- `src/stock/StockLevelsWorkspace.tsx` — экран «Остатки» (список + Сводка).
- `src/stock/stockLevels.ts` — чистая логика: маппинг каталога → `StockItem`, статус по порогу, маржа, агрегаты Сводки.
- `src/styles/22-stock.css` — стили раздела.
- `src/stock/stockLevels.test.ts`, `src/stock/StockLevelsWorkspace.test.tsx`, `src/stock/stockModel.test.ts`.

**Frontend (modify):**
- `src/operatorTypes.ts` — `WorkspaceId` += `'stock'`.
- `src/operatorPermissions.ts` — `workspaceIds` += `'stock'`; `workspacePermissionRules.stock`.
- `src/operatorData.ts` — `NavSection` для склада (иконка + label).
- `src/WorkspaceRouter.tsx` — ветка `workspace === 'stock'`.
- `src/cash/CashTabBar.tsx`, `src/cash/cashModel.ts`, `src/cash/CashWorkspace.tsx` — убрать таб `'stock'` из Кассы.
- `src/main.tsx` или `src/styles/styles.css` — подключить `22-stock.css` (по тому же механизму, что прочие `NN-*.css`).
- `locales/{ru,en,tg}.json` — новые ключи; затем `bun run gen`.
- `src/App.test.tsx` — `TAB_SECTION`/навигация для новой секции.

---

## Task 1: Backend — поле AvgCostMinorUnits на товаре + в DTO

**Files:**
- Modify: `src/AFK4.Platform.Api/Data/PosProductEntity.cs`
- Modify: `src/AFK4.Shared.Contracts/Pos/PosProductDto.cs`
- Modify: `src/AFK4.Platform.Api/Inventory/EfInventoryService.cs:751-768` (метод `ToDto`)
- Test: `tests/AFK4.Shared.Contracts.Tests/InventoryContractSerializationTests.cs`

**Interfaces:**
- Produces: `PosProductDto.AvgCostMinorUnits` (`long`, default `0`); `PosProductEntity.AvgCostMinorUnits` (`long`).

- [ ] **Step 1: Добавить поле в entity**

В `PosProductEntity.cs` после `PriceMinorUnits`:
```csharp
    public long PriceMinorUnits { get; set; }

    /// <summary>Средневзвешенная закупочная себестоимость единицы (minor units). Пересчитывается при purchase-движении.</summary>
    public long AvgCostMinorUnits { get; set; }
```

- [ ] **Step 2: Добавить трейлинг-параметр в DTO**

В `PosProductDto.cs` — добавить после `AvailableInShell` (сохраняя позиционный порядок и значения по умолчанию):
```csharp
    int ReorderThreshold = 0,
    bool AvailableInShell = false,
    long AvgCostMinorUnits = 0);
```

- [ ] **Step 3: Прокинуть в ToDto**

В `EfInventoryService.cs`, метод `ToDto(PosProductEntity product, int stockOnHand)` — добавить последним аргументом:
```csharp
            product.ReorderThreshold,
            product.AvailableInShell,
            product.AvgCostMinorUnits);
```

- [ ] **Step 4: Написать падающий тест сериализации контракта**

В `InventoryContractSerializationTests.cs` добавить (проверяет, что новое поле сериализуется и дефолтится):
```csharp
[Fact]
public void PosProductDto_RoundTrips_AvgCostMinorUnits()
{
    var dto = new PosProductDto(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        "Snickers", "SNICKERS", new MoneyDto("TJS", 1000),
        true, false, true, 15, DateTimeOffset.UnixEpoch,
        ReorderThreshold: 10, AvailableInShell: false, AvgCostMinorUnits: 500);
    var json = JsonSerializer.Serialize(dto);
    var back = JsonSerializer.Deserialize<PosProductDto>(json);
    Assert.Equal(500, back!.AvgCostMinorUnits);
}
```

- [ ] **Step 5: Сборка + тест**

Run: `dotnet test tests/AFK4.Shared.Contracts.Tests --filter PosProductDto_RoundTrips_AvgCostMinorUnits`
Expected: PASS (solution компилируется со всеми тремя call-site `new PosProductDto(...)` — компилятор подтвердит, что трейлинг-дефолт не сломал существующие вызовы).

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Platform.Api/Data/PosProductEntity.cs src/AFK4.Shared.Contracts/Pos/PosProductDto.cs src/AFK4.Platform.Api/Inventory/EfInventoryService.cs tests/AFK4.Shared.Contracts.Tests/InventoryContractSerializationTests.cs
git commit -m "feat(inventory): AvgCostMinorUnits на товаре и в PosProductDto"
```

---

## Task 2: Backend — пересчёт средневзвешенной себестоимости при purchase

**Files:**
- Modify: `src/AFK4.Platform.Api/Inventory/EfInventoryService.cs` (`CreateStockMovementAsync`, транзакционный блок ~338-383)
- Test: `tests/AFK4.Platform.Api.Tests/EfInventoryServiceTests.cs`

**Interfaces:**
- Consumes: `PosProductEntity.AvgCostMinorUnits` (Task 1), `StockMovementTypeNames.Purchase`.
- Produces: после `purchase` на товаре обновлён `AvgCostMinorUnits = round((oldQty*oldAvg + inQty*inCost) / (oldQty + inQty))`. Прочие типы движений среднюю не двигают. При пустом/нулевом исходном остатке средняя = себестоимость прихода.

- [ ] **Step 1: Написать падающие тесты средней**

В `EfInventoryServiceTests.cs` добавить (опираясь на существующие хелперы `CreateService`, `CreateTrackedProductAsync`, `StockMovement`, `TestIds`, `ActorStaffUserId`):
```csharp
[Fact]
public async Task Purchase_SetsAvgCost_OnFirstReceipt()
{
    await using var db = CreateDbContext();
    var service = CreateService(db);
    var product = await CreateTrackedProductAsync(service);
    await service.CreateStockMovementAsync(TestIds.BranchId, ActorStaffUserId, new CreateStockMovementRequest(
        TestIds.OrganizationId, product.ProductId, StockMovementTypeNames.Purchase,
        10, new MoneyDto("TJS", 400), "поставка", "buy-1"), CancellationToken.None);
    var entity = await db.PosProducts.SingleAsync(p => p.ProductId == product.ProductId);
    Assert.Equal(400, entity.AvgCostMinorUnits);
}

[Fact]
public async Task Purchase_RecomputesWeightedAverage()
{
    await using var db = CreateDbContext();
    var service = CreateService(db);
    var product = await CreateTrackedProductAsync(service);
    // 10 @ 400  → avg 400
    await service.CreateStockMovementAsync(TestIds.BranchId, ActorStaffUserId, new CreateStockMovementRequest(
        TestIds.OrganizationId, product.ProductId, StockMovementTypeNames.Purchase, 10, new MoneyDto("TJS", 400), "p1", "buy-a"), CancellationToken.None);
    // + 30 @ 600 → (10*400 + 30*600)/40 = 550
    await service.CreateStockMovementAsync(TestIds.BranchId, ActorStaffUserId, new CreateStockMovementRequest(
        TestIds.OrganizationId, product.ProductId, StockMovementTypeNames.Purchase, 30, new MoneyDto("TJS", 600), "p2", "buy-b"), CancellationToken.None);
    var entity = await db.PosProducts.SingleAsync(p => p.ProductId == product.ProductId);
    Assert.Equal(550, entity.AvgCostMinorUnits);
}

[Fact]
public async Task Sale_DoesNotChangeAvgCost()
{
    await using var db = CreateDbContext();
    var service = CreateService(db);
    var product = await CreateTrackedProductAsync(service);
    await service.CreateStockMovementAsync(TestIds.BranchId, ActorStaffUserId, new CreateStockMovementRequest(
        TestIds.OrganizationId, product.ProductId, StockMovementTypeNames.Purchase, 10, new MoneyDto("TJS", 400), "p", "buy-c"), CancellationToken.None);
    await service.CreateStockMovementAsync(TestIds.BranchId, ActorStaffUserId, new CreateStockMovementRequest(
        TestIds.OrganizationId, product.ProductId, StockMovementTypeNames.Sale, -2, new MoneyDto("TJS", 0), "чек", "sale-c"), CancellationToken.None);
    var entity = await db.PosProducts.SingleAsync(p => p.ProductId == product.ProductId);
    Assert.Equal(400, entity.AvgCostMinorUnits);
}
```

- [ ] **Step 2: Запустить — падают**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter "Purchase_SetsAvgCost_OnFirstReceipt|Purchase_RecomputesWeightedAverage|Sale_DoesNotChangeAvgCost"`
Expected: FAIL (`Purchase_*` — средняя `0`; `Sale_` уже зелёный, но держим как регресс).

- [ ] **Step 3: Реализовать пересчёт в транзакции**

В `CreateStockMovementAsync`, внутри транзакционного блока, ПЕРЕД финальным `SaveChangesAsync` (после `dbContext.StockMovements.Add(movement)`), когда `product` уже загружен в tracking-контекст:
```csharp
            if (movement.MovementType == StockMovementTypeNames.Purchase)
            {
                // Stock-on-hand ДО этого прихода = сумма прежних дельт (movement ещё не учтён в БД-сумме на этот момент).
                var priorQuantity = await dbContext.StockMovements
                    .Where(existing =>
                        existing.OrganizationId == product.OrganizationId &&
                        existing.BranchId == product.BranchId &&
                        existing.ProductId == product.ProductId &&
                        existing.StockMovementId != movement.StockMovementId)
                    .SumAsync(existing => (int?)existing.QuantityDelta, cancellationToken) ?? 0;
                var basePrior = Math.Max(priorQuantity, 0);
                var inboundQty = movement.QuantityDelta; // purchase > 0 (validation отвергает 0)
                var denominator = basePrior + inboundQty;
                product.AvgCostMinorUnits = denominator <= 0
                    ? movement.UnitCostMinorUnits
                    : (long)Math.Round(
                        (basePrior * (double)product.AvgCostMinorUnits + inboundQty * (double)movement.UnitCostMinorUnits) / denominator,
                        MidpointRounding.AwayFromZero);
            }
```
(`product` уже tracked → `SaveChangesAsync` запишет изменённый `AvgCostMinorUnits`.)

- [ ] **Step 4: Запустить — зелёные**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter "Purchase_SetsAvgCost_OnFirstReceipt|Purchase_RecomputesWeightedAverage|Sale_DoesNotChangeAvgCost"`
Expected: PASS (3/3).

- [ ] **Step 5: Полный прогон проекта (регресс идемпотентности/IDOR)**

Run: `dotnet test tests/AFK4.Platform.Api.Tests`
Expected: PASS (включая существующие `CreateStockMovementAsync_ReplaysSameIdempotencyKeyAndRequest` и др.).

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Platform.Api/Inventory/EfInventoryService.cs tests/AFK4.Platform.Api.Tests/EfInventoryServiceTests.cs
git commit -m "feat(inventory): средневзвешенная себестоимость пересчитывается при приёмке"
```

---

## Task 3: Backend — миграция AddProductAvgCost

**Files:**
- Create: `src/AFK4.Platform.Api/Data/Migrations/<ts>_AddProductAvgCost.cs` (+ `.Designer.cs`), обновляет `PlatformDbContextModelSnapshot.cs`.

**Interfaces:**
- Produces: колонка `AvgCostMinorUnits` (`bigint`, `NOT NULL DEFAULT 0`) в `pos_products`.

> **Окружение:** `dotnet ef` запускать из **Windows PowerShell** (`AGENTS.md`), не из WSL. Процедура — из `docs/superpowers/plans/2026-06-16-operator-map-plan-b2-1-backend.md:980-1016`.

- [ ] **Step 1: Обновить модель сборкой**

Run (Windows PowerShell): `dotnet build src/AFK4.Platform.Api`
Expected: build succeeded (модель свежая — иначе `Up()` выйдет пустым).

- [ ] **Step 2: Сгенерировать миграцию**

Run: `dotnet ef migrations add AddProductAvgCost --project src/AFK4.Platform.Api --output-dir Data/Migrations --no-build`
Expected: созданы два файла + обновлён snapshot.

- [ ] **Step 3: Проверить Up/Down**

Открыть новый `<ts>_AddProductAvgCost.cs`; `Up()` должен содержать:
```csharp
migrationBuilder.AddColumn<long>(
    name: "AvgCostMinorUnits", table: "pos_products",
    type: "bigint", nullable: false, defaultValue: 0L);
```
`Down()` — `DropColumn`. Если `Up()/Down()` пустые → модель была устаревшей: удалить оба новых файла, повторить Step 1 → Step 2.

- [ ] **Step 4: Тест миграции/контекста**

Run: `dotnet test tests/AFK4.Platform.Api.Tests`
Expected: PASS (InMemory-тесты не применяют миграцию, но проверяют, что snapshot/контекст консистентны и сборка цела).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Api/Data/Migrations
git commit -m "feat(inventory): миграция AddProductAvgCost (pos_products.AvgCostMinorUnits)"
```

---

## Task 4: Frontend — регистрация раздела «Склад» (рейл + роутер заглушкой)

**Files:**
- Modify: `src/operatorTypes.ts` (`WorkspaceId`)
- Modify: `src/operatorPermissions.ts` (`workspaceIds`, `workspacePermissionRules`)
- Modify: `src/operatorData.ts` (`navSections`, импорт иконки)
- Modify: `src/WorkspaceRouter.tsx`
- Create: `src/stock/StockWorkspace.tsx` (минимальная заглушка — наполнится в Task 5)
- Modify: `locales/{ru,en,tg}.json` (+ `bun run gen`)
- Test: `src/operatorVisibility.test.ts`

**Interfaces:**
- Produces: `WorkspaceId` включает `'stock'`; раздел виден при `inventory.view` ИЛИ `inventory.stock.manage`; `<StockWorkspace currencyCode backend session />` смонтирован для `workspace === 'stock'`.

- [ ] **Step 1: Добавить id типа и permission-правило**

`src/operatorTypes.ts` — в union `WorkspaceId` добавить `| 'stock'`.
`src/operatorPermissions.ts` — в массив `workspaceIds` добавить `'stock'`; в `workspacePermissionRules`:
```ts
  stock: [permissionNames.viewInventory, permissionNames.manageInventoryStock],
```

- [ ] **Step 2: Добавить i18n-ключ названия раздела**

В каждый из `locales/ru.json` / `en.json` / `tg.json` добавить ключ `op.shell.navGroup.warehouse`:
- ru: `"Склад"`
- en: `"Stock"`
- tg: `"Анбор"`  (реальный таджикский «склад»)

Затем: `cd packages/i18n && bun run gen` (регенерит `messages.ts`).

- [ ] **Step 3: Добавить секцию в navSections**

`src/operatorData.ts` — импорт иконки из `lucide-react` (например `Boxes`) и новая секция (после `cashier`, перед `reports`):
```ts
  {
    key: 'stock',
    labelKey: 'op.shell.navGroup.warehouse',
    icon: Boxes,
    items: [{ id: 'stock', labelKey: 'op.shell.navGroup.warehouse' }]
  },
```

- [ ] **Step 4: Заглушка StockWorkspace + ветка роутера**

Create `src/stock/StockWorkspace.tsx`:
```tsx
import type { OperatorBackendContext } from '../operatorTypes';
import type { OperatorAuthSession } from '../authClient';

export function StockWorkspace(_props: {
  currencyCode: string;
  backend: OperatorBackendContext | null;
  session: OperatorAuthSession | null;
}) {
  return <main className="workspace-screen stock-screen" />;
}
```
`src/WorkspaceRouter.tsx` — рядом с веткой `cash` добавить:
```tsx
        {workspace === 'stock' && <StockWorkspace currencyCode={currencyCode} backend={backend} session={session} />}
```
(+ импорт `StockWorkspace`.)

- [ ] **Step 5: Тест видимости раздела**

В `src/operatorVisibility.test.ts` добавить кейс: сессия с `inventory.view` → `canOpenWorkspace(session, 'stock') === true`; сессия без inventory-прав → `false`. Использовать существующий хелпер построения сессии в файле.

- [ ] **Step 6: Запустить тесты + сборку**

Run: `bun test src/operatorVisibility.test.ts`
Expected: PASS.
Run: `bun run build` (tsc+vite) — Expected: компилируется (exhaustive `Record<WorkspaceId,…>` подтверждает, что `stock` добавлен везде).

- [ ] **Step 7: Commit**

```bash
git add src/operatorTypes.ts src/operatorPermissions.ts src/operatorData.ts src/WorkspaceRouter.tsx src/stock/StockWorkspace.tsx src/operatorVisibility.test.ts locales packages/i18n/src/messages.ts
git commit -m "feat(operator): раздел рейла «Склад» (регистрация + роутер-заглушка)"
```

---

## Task 5: Frontend — каркас StockWorkspace (табы) + отвязка stock-таба из Кассы

**Files:**
- Create: `src/stock/stockModel.ts`, `src/stock/StockTabBar.tsx`
- Modify: `src/stock/StockWorkspace.tsx`
- Modify: `src/cash/CashTabBar.tsx`, `src/cash/cashModel.ts`, `src/cash/CashWorkspace.tsx` (убрать `'stock'`)
- Test: `src/stock/stockModel.test.ts`

**Interfaces:**
- Produces: `type StockTab = 'levels'` (расширяется в S1–S4); `STOCK_TAB_ORDER`, `STOCK_TAB_PERMISSIONS`, `visibleStockTabs(session): StockTab[]`; `<StockTabBar tabs activeTab onSelect />`. `StockWorkspace` рендерит шапку-якорь + табы + активный таб.
- Consumes: `permissionNames.manageInventoryStock`, `permissionNames.viewInventory`.

- [ ] **Step 1: Написать падающий тест модели табов**

Create `src/stock/stockModel.test.ts`:
```ts
import { describe, it, expect } from 'bun:test';
import { visibleStockTabs, STOCK_TAB_ORDER } from './stockModel';

describe('stockModel', () => {
  it('levels виден при inventory.view', () => {
    const session = { permissions: ['inventory.view'] } as never;
    expect(visibleStockTabs(session)).toContain('levels');
  });
  it('пустые права → нет вкладок', () => {
    const session = { permissions: [] } as never;
    expect(visibleStockTabs(session)).toEqual([]);
  });
  it('порядок стабилен', () => {
    expect(STOCK_TAB_ORDER[0]).toBe('levels');
  });
});
```

- [ ] **Step 2: Запустить — падает**

Run: `bun test src/stock/stockModel.test.ts`
Expected: FAIL (модуль не найден).

- [ ] **Step 3: Реализовать stockModel**

Create `src/stock/stockModel.ts` (шаблон `cashModel.ts`):
```ts
import { hasAnyPermission, permissionNames } from '../operatorPermissions';
import type { OperatorAuthSession } from '../authClient';

export type StockTab = 'levels';

export const STOCK_TAB_ORDER: readonly StockTab[] = ['levels'];

export const STOCK_TAB_PERMISSIONS: Record<StockTab, readonly string[]> = {
  levels: [permissionNames.viewInventory, permissionNames.manageInventoryStock]
};

export function visibleStockTabs(session: OperatorAuthSession | null): StockTab[] {
  return STOCK_TAB_ORDER.filter((tab) => hasAnyPermission(session, STOCK_TAB_PERMISSIONS[tab]));
}
```

- [ ] **Step 4: Запустить — зелёный**

Run: `bun test src/stock/stockModel.test.ts`
Expected: PASS (3/3).

- [ ] **Step 5: StockTabBar + StockWorkspace**

Create `src/stock/StockTabBar.tsx` (шаблон `CashTabBar.tsx`, классы `cash-tabs`/`cash-tab` переиспользуем — единый язык вкладок раздела):
```tsx
import { useI18n } from '@afk4/i18n';
import type { MessageKey } from '@afk4/i18n';
import type { StockTab } from './stockModel';

export function StockTabBar({ tabs, activeTab, onSelect }: {
  tabs: { id: StockTab; labelKey: MessageKey }[];
  activeTab: StockTab;
  onSelect: (tab: StockTab) => void;
}) {
  const { t } = useI18n();
  return (
    <div className="cash-tabs" role="tablist">
      {tabs.map((tab) => (
        <button key={tab.id} type="button" role="tab" aria-selected={tab.id === activeTab}
          className={tab.id === activeTab ? 'cash-tab active' : 'cash-tab'}
          onClick={() => onSelect(tab.id)}>{t(tab.labelKey)}</button>
      ))}
    </div>
  );
}
```
Replace `src/stock/StockWorkspace.tsx`:
```tsx
import { useState } from 'react';
import { useI18n } from '@afk4/i18n';
import type { MessageKey } from '@afk4/i18n';
import type { OperatorBackendContext } from '../operatorTypes';
import type { OperatorAuthSession } from '../authClient';
import { StockTabBar } from './StockTabBar';
import { StockLevelsWorkspace } from './StockLevelsWorkspace';
import { visibleStockTabs, type StockTab } from './stockModel';

const TAB_LABELS: Record<StockTab, MessageKey> = { levels: 'op.stock.tab.levels' };

export function StockWorkspace({ currencyCode, backend, session }: {
  currencyCode: string;
  backend: OperatorBackendContext | null;
  session: OperatorAuthSession | null;
}) {
  const { t } = useI18n();
  const visible = visibleStockTabs(session);
  const [activeTab, setActiveTab] = useState<StockTab>(() => visible[0] ?? 'levels');
  const tabs = visible.map((id) => ({ id, labelKey: TAB_LABELS[id] }));

  return (
    <main className="workspace-screen stock-screen">
      <div className="cash-head">
        <h1><span className="cash-head-name">{t('op.stock.title')}</span></h1>
      </div>
      {tabs.length > 1 && <StockTabBar tabs={tabs} activeTab={activeTab} onSelect={setActiveTab} />}
      <div className="cash-tab-content">
        {activeTab === 'levels' && <StockLevelsWorkspace backend={backend} currencyCode={currencyCode} session={session} />}
      </div>
    </main>
  );
}
```
(`tabs.length > 1` — в S0 один таб, полоска скрыта; вернётся в S1.)

Add i18n keys (`op.stock.title`, `op.stock.tab.levels`) во все три locales + `bun run gen`.

- [ ] **Step 6: Отвязать stock-таб от Кассы**

- `src/cash/CashTabBar.tsx:1` — убрать `'stock'` из `CashTab`: `export type CashTab = 'sales' | 'shift' | 'journal';`
- `src/cash/cashModel.ts` — убрать `stock` из `CASH_TAB_PERMISSIONS` и `CASH_TAB_ORDER`.
- `src/cash/CashWorkspace.tsx` — убрать импорт `CashStockWorkspace`, строку таба `{ id: 'stock', label: t('op.cash.stock.tab') }` и ветку `{activeTab === 'stock' && …}`.
(`CashStockWorkspace.tsx` файл пока оставить — его логика списания переедет в S0 Task 6/последующие; удалить в конце слайса, чтобы не плодить мёртвый код — см. Task 8 Step 4.)

- [ ] **Step 7: Запустить тесты Кассы + сборку**

Run: `bun test src/cash/`
Expected: PASS (тесты Кассы не должны ссылаться на удалённый таб; при падении — поправить ссылки на `'stock'`).
Run: `bun run build` — Expected: компилируется.

- [ ] **Step 8: Commit**

```bash
git add src/stock src/cash locales packages/i18n/src/messages.ts
git commit -m "feat(stock): каркас StockWorkspace с табами; убран таб Склад из Кассы"
```

---

## Task 6: Frontend — чистая логика Остатков (stockLevels.ts)

**Files:**
- Create: `src/stock/stockLevels.ts`
- Test: `src/stock/stockLevels.test.ts`

**Interfaces:**
- Produces:
  - `interface StockItem { productId; name; sku; category; stockOnHand; reorderThreshold; avgCostMinorUnits; priceMinorUnits; }`
  - `type StockStatus = 'ok' | 'low' | 'out'`
  - `stockStatus(item): StockStatus` — `out` если `stockOnHand <= 0`; `low` если `reorderThreshold > 0 && stockOnHand <= reorderThreshold`; иначе `ok`.
  - `marginPercent(priceMinorUnits, avgCostMinorUnits): number | null` — `null` если цена `<= 0`; иначе `round((price - avg)/price * 100)`.
  - `stockValueMinorUnits(item): number` — `stockOnHand * avgCostMinorUnits`.
  - `mapCatalogToStock(catalog: PosProductDto[]): StockItem[]` — фильтр `trackStock`, маппинг полей (`category` — пока из `category` если есть в DTO, иначе пусто — см. ниже).
  - `summarize(items): { totalValueMinorUnits; lowCount; outCount; byCategory: {category; valueMinorUnits}[] }`
- Consumes: `PosProductDto` shape через `readString/readNumber/readMoney` helpers (`operatorHelpers`).

> Примечание: категория товара в `PosProductDto` отдаётся как `CategoryId` (Guid), не как имя. В S0 раздел Сводки по категориям может группировать по `categoryId` с отображением «—», либо опустить разбивку по категориям до доступности имени категории (бэк отдаёт категории отдельным эндпоинтом). **Решение S0:** Сводка показывает «Стоимость склада» (итого) + «Заказать» + «Движение за смену»; разбивку по категориям отложить в backlog (требует имя категории в каталоге). Убрать `byCategory` из этого интерфейса, если имя недоступно — оставить `{ totalValueMinorUnits; lowCount; outCount }`.

- [ ] **Step 1: Написать падающие тесты логики**

Create `src/stock/stockLevels.test.ts`:
```ts
import { describe, it, expect } from 'bun:test';
import { stockStatus, marginPercent, stockValueMinorUnits, summarize, type StockItem } from './stockLevels';

const item = (over: Partial<StockItem>): StockItem => ({
  productId: 'p', name: 'X', sku: 'X', category: '', stockOnHand: 10,
  reorderThreshold: 5, avgCostMinorUnits: 400, priceMinorUnits: 1000, ...over
});

describe('stockStatus', () => {
  it('out при нулевом остатке', () => expect(stockStatus(item({ stockOnHand: 0 }))).toBe('out'));
  it('low при остатке <= порога', () => expect(stockStatus(item({ stockOnHand: 5, reorderThreshold: 6 }))).toBe('low'));
  it('ok выше порога', () => expect(stockStatus(item({ stockOnHand: 12, reorderThreshold: 6 }))).toBe('ok'));
  it('порог 0 = без low (только out)', () => expect(stockStatus(item({ stockOnHand: 1, reorderThreshold: 0 }))).toBe('ok'));
});

describe('marginPercent', () => {
  it('60% при 400/1000', () => expect(marginPercent(1000, 400)).toBe(60));
  it('null при нулевой цене', () => expect(marginPercent(0, 400)).toBeNull());
});

describe('stockValueMinorUnits', () => {
  it('остаток × средняя', () => expect(stockValueMinorUnits(item({ stockOnHand: 12, avgCostMinorUnits: 400 }))).toBe(4800));
});

describe('summarize', () => {
  it('считает low/out и стоимость', () => {
    const r = summarize([
      item({ stockOnHand: 0, reorderThreshold: 5, avgCostMinorUnits: 500 }),
      item({ stockOnHand: 2, reorderThreshold: 6, avgCostMinorUnits: 600 }),
      item({ stockOnHand: 12, reorderThreshold: 6, avgCostMinorUnits: 400 })
    ]);
    expect(r.outCount).toBe(1);
    expect(r.lowCount).toBe(1);
    expect(r.totalValueMinorUnits).toBe(0 * 500 + 2 * 600 + 12 * 400);
  });
});
```

- [ ] **Step 2: Запустить — падает**

Run: `bun test src/stock/stockLevels.test.ts`
Expected: FAIL (модуль не найден).

- [ ] **Step 3: Реализовать stockLevels.ts**

Create `src/stock/stockLevels.ts`:
```ts
import { readString, readNumber, readMoney } from '../operatorHelpers';
import type { PosProductDto } from '../operatorApiClients';

export interface StockItem {
  productId: string;
  name: string;
  sku: string;
  category: string;
  stockOnHand: number;
  reorderThreshold: number;
  avgCostMinorUnits: number;
  priceMinorUnits: number;
}

export type StockStatus = 'ok' | 'low' | 'out';

export function stockStatus(item: StockItem): StockStatus {
  if (item.stockOnHand <= 0) return 'out';
  if (item.reorderThreshold > 0 && item.stockOnHand <= item.reorderThreshold) return 'low';
  return 'ok';
}

export function marginPercent(priceMinorUnits: number, avgCostMinorUnits: number): number | null {
  if (priceMinorUnits <= 0) return null;
  return Math.round(((priceMinorUnits - avgCostMinorUnits) / priceMinorUnits) * 100);
}

export function stockValueMinorUnits(item: StockItem): number {
  return Math.max(item.stockOnHand, 0) * item.avgCostMinorUnits;
}

export function mapCatalogToStock(catalog: PosProductDto[]): StockItem[] {
  return catalog
    .filter((product) => Boolean(readNumber(product, 'trackStock', 0)) || (product as Record<string, unknown>).trackStock === true)
    .filter((product) => (product as Record<string, unknown>).trackStock === true)
    .map((product) => ({
      productId: readString(product, 'productId'),
      name: readString(product, 'name'),
      sku: readString(product, 'sku', 'SKU'),
      category: readString(product, 'categoryName', ''),
      stockOnHand: readNumber(product, 'stockOnHand', 0),
      reorderThreshold: readNumber(product, 'reorderThreshold', 0),
      avgCostMinorUnits: readMoney(product, 'avgCost')?.minorUnits ?? readNumber(product, 'avgCostMinorUnits', 0),
      priceMinorUnits: readMoney(product, 'price')?.minorUnits ?? 0
    }));
}

export function summarize(items: StockItem[]): { totalValueMinorUnits: number; lowCount: number; outCount: number } {
  let total = 0, low = 0, out = 0;
  for (const item of items) {
    total += stockValueMinorUnits(item);
    const status = stockStatus(item);
    if (status === 'out') out += 1;
    else if (status === 'low') low += 1;
  }
  return { totalValueMinorUnits: total, lowCount: low, outCount: out };
}
```
> Уточнить при реализации: точное имя поля средней в DTO на проводе — `avgCostMinorUnits` (число) против `avgCost` (MoneyDto). По Task 1 это `long AvgCostMinorUnits` → JSON `avgCostMinorUnits` (число). Упростить `avgCostMinorUnits: readNumber(product, 'avgCostMinorUnits', 0)` и убрать `readMoney('avgCost')`. Аналогично `mapCatalogToStock`'s `trackStock`-фильтр свести к одной проверке `(product as …).trackStock === true` через имеющийся `readBoolean` helper, если он есть.

- [ ] **Step 4: Запустить — зелёный**

Run: `bun test src/stock/stockLevels.test.ts`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/stock/stockLevels.ts src/stock/stockLevels.test.ts
git commit -m "feat(stock): чистая логика остатков (статус по порогу, маржа, стоимость, сводка)"
```

---

## Task 7: Frontend — экран StockLevelsWorkspace (список + Сводка) + CSS

**Files:**
- Create: `src/stock/StockLevelsWorkspace.tsx`, `src/styles/22-stock.css`
- Modify: подключение `22-stock.css` (туда же, где импортируются прочие `NN-*.css`)
- Modify: `locales/{ru,en,tg}.json` (+ `bun run gen`)
- Test: `src/stock/StockLevelsWorkspace.test.tsx`

**Interfaces:**
- Consumes: `mapCatalogToStock`, `stockStatus`, `marginPercent`, `stockValueMinorUnits`, `summarize` (Task 6); `createAuthenticatedOperatorClients(...).pos.getCatalog(branchId)` (как в `CashStockWorkspace`); `minorToMajor`/`formatCurrency` (money helpers, UI-граница).
- Produces: рабочий экран Остатков с фильтрами (Все/На исходе/Нет), поиском, списком и правой Сводкой. Разметка/классы — из мокапа `stock-shell-v5.html` (`.cash-stock-row`-язык + `srow`/`metrics`).

- [ ] **Step 1: Написать падающий тест экрана**

Create `src/stock/StockLevelsWorkspace.test.tsx` (паттерн `CashStockWorkspace.test.tsx`: mock `operatorHelpers` ДО динамического импорта):
```tsx
import { describe, it, expect, mock, afterEach, afterAll } from 'bun:test';
import { render, screen, fireEvent, cleanup } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';

const getCatalog = mock(async () => ([
  { productId: 'p1', name: 'Энергетик Red Bull', sku: 'ENERGY-RB', trackStock: true, stockOnHand: 8, reorderThreshold: 10, avgCostMinorUnits: 900, price: { currencyCode: 'TJS', minorUnits: 1800 } },
  { productId: 'p2', name: 'Cola 0.5', sku: 'COLA-05', trackStock: true, stockOnHand: 12, reorderThreshold: 6, avgCostMinorUnits: 400, price: { currencyCode: 'TJS', minorUnits: 1000 } }
]));
const actual = (globalThis as Record<string, unknown>).__afk4RealOperatorHelpers as Record<string, unknown>;
mock.module('../operatorHelpers', () => ({ ...actual, createAuthenticatedOperatorClients: () => ({ pos: { getCatalog }, inventory: {} }) }));

const { StockLevelsWorkspace } = await import('./StockLevelsWorkspace');

const backend = { config: { platformBaseUrl: 'http://x' }, session: { accessToken: 't', organizationId: 'o' }, branchId: 'b' } as never;
const session = { permissions: ['inventory.view'], organizationId: 'o' } as never;
const view = () => render(<I18nProvider initialLocale="ru"><StockLevelsWorkspace backend={backend} currencyCode="TJS" session={session} /></I18nProvider>);

afterEach(() => cleanup());
afterAll(() => mock.restore());

describe('StockLevelsWorkspace', () => {
  it('показывает товары и помечает «на исходе» по per-product порогу', async () => {
    view();
    expect(await screen.findByText('Энергетик Red Bull')).toBeInTheDocument();
    // Red Bull 8 при пороге 10 → low; Cola 12 при пороге 6 → ok
    const lowTags = await screen.findAllByText(/на исходе/i);
    expect(lowTags.length).toBe(1);
  });

  it('фильтр «На исходе» оставляет только low/out', async () => {
    view();
    await screen.findByText('Cola 0.5');
    fireEvent.click(screen.getByRole('button', { name: /на исходе/i }));
    expect(screen.queryByText('Cola 0.5')).not.toBeInTheDocument();
    expect(screen.getByText('Энергетик Red Bull')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Запустить — падает**

Run: `bun test src/stock/StockLevelsWorkspace.test.tsx`
Expected: FAIL (модуль не найден).

- [ ] **Step 3: Реализовать StockLevelsWorkspace**

Create `src/stock/StockLevelsWorkspace.tsx`. Структура (разметка/классы — из мокапа `stock-shell-v5.html`; деньги — через `minorToMajor`+`formatCurrency`):
- Грид колонки 2: `.stock-layout { display:grid; grid-template-columns: minmax(0,1fr) 304px; gap:10px }` — слева `.cash-stock-levels` (поиск + сегменты Все/На исходе/Нет + список), справа `.stock-summary` (Сводка).
- Загрузка: `useEffect` → `createAuthenticatedOperatorClients(backend.config, backend.session).pos.getCatalog(backend.branchId)` → `mapCatalogToStock` → `useState<StockItem[]>`; loading/error состояния через `.workspace-loading`/`.workspace-error` (как `CashStockWorkspace`).
- Фильтр: `useState<'all'|'low'|'out'>('all')` + поиск `useState('')`; `filtered = items.filter(byStatus).filter(bySearch)`.
- Строка: иконка + имя/SKU/категория + плотная группа метрик (Остаток `qty` с классом `low`/`out`, Порог, Себест `minorToMajor(avgCost)`, Цена, Маржа `marginPercent(...)`, Стоимость `stockValueMinorUnits`), действия `＋`/`−` (в S0 — заглушки/disabled; реальные приёмка/списание в S1).
- Статус-тег: `stockStatus` → `t('op.stock.status.ok'|'.low'|'.out')`; класс `low`=warning, `out`=danger.
- Сводка справа: `summarize(items)` → «Стоимость склада» (`formatCurrency(minorToMajor(total))`), «На исходе» (lowCount), «Нет в наличии» (outCount); блок «Заказать» = список `low`+`out` (в S0 кнопка «Оформить приёмку» — заглушка/ведёт в S1).

Ключевая логика статуса/маржи/денег импортируется из `stockLevels.ts` (Task 6) — НЕ дублировать. Все строки — `t(...)`; новые ключи (`op.stock.title` есть из Task 5; добавить `op.stock.levels.*`, `op.stock.status.*`, `op.stock.filter.*`, `op.stock.summary.*`, `op.stock.col.*`) во все три locales + `bun run gen`.

- [ ] **Step 4: CSS раздела**

Create `src/styles/22-stock.css`: перенести/адаптировать stock-правила из `21-cash.css` (`.cash-stock-*`) + добавить `.stock-layout`, `.stock-summary` (карточки как `.cash-shift-card`), `.srow`/`.metrics` плотную группу числовых (из мокапа). **Контраст-инвариант:** числа `text-primary`, заголовки колонок/SKU `text-tertiary`, границы `border-default`. Подключить файл там же, где импортируются прочие `NN-*.css`.

- [ ] **Step 5: Запустить — зелёный**

Run: `bun test src/stock/StockLevelsWorkspace.test.tsx`
Expected: PASS (2/2).

- [ ] **Step 6: Commit**

```bash
git add src/stock/StockLevelsWorkspace.tsx src/styles/22-stock.css src/stock/StockLevelsWorkspace.test.tsx locales packages/i18n/src/messages.ts
git commit -m "feat(stock): экран Остатки v2 (реальный порог, себест/маржа/стоимость, фильтры, Сводка)"
```

---

## Task 8: Интеграция — навигация App.test, уборка, полный прогон

**Files:**
- Modify: `src/App.test.tsx` (`TAB_SECTION`/навигация)
- Delete: `src/cash/CashStockWorkspace.tsx` + `src/cash/CashStockWorkspace.test.tsx` (логика перенесена; мёртвый код убираем)
- Modify: `src/styles/21-cash.css` (убрать осиротевшие `.cash-stock-*`, если не используются)

**Interfaces:**
- Consumes: всё из Task 4–7.

- [ ] **Step 1: Навигация к разделу в App.test**

В `src/App.test.tsx` — раздел «Склад» открывается кнопкой рейла с label `Склад`. Добавить smoke-проверку: после `gotoWorkspace('Склад')` виден заголовок Остатков. Если `gotoWorkspace` ищет таб-секцию по `TAB_SECTION` — раздел одиночный (один таб), открывается прямой кнопкой рейла; убедиться, что хелпер это поддерживает (иначе использовать прямой клик по кнопке с `name: 'Склад'`).

- [ ] **Step 2: Прогнать App.test (отдельным процессом)**

Run: `bun test src/App.test.tsx`
Expected: PASS (рейл показывает «Склад»; Касса больше не показывает вкладку «Склад»).

- [ ] **Step 3: Полный прогон фронта + сборка**

Run: `bun test $(find src -name '*.test.ts' -o -name '*.test.tsx' | grep -v App.test)`
Expected: PASS.
Run: `bun run build`
Expected: tsc+vite зелёные.

- [ ] **Step 4: Удалить мёртвый CashStockWorkspace + осиротевший CSS**

```bash
git rm src/cash/CashStockWorkspace.tsx src/cash/CashStockWorkspace.test.tsx
```
В `21-cash.css` удалить `.cash-stock-*` правила, ПЕРЕЕХАВШИЕ в `22-stock.css` (оставить только то, что ещё используется Кассой — проверить `grep -rn "cash-stock" src`). Прогнать `bun run build` снова.

- [ ] **Step 5: Финальный полный прогон (фронт + бэк)**

Run: `bun test $(find src -name '*.test.ts' -o -name '*.test.tsx' | grep -v App.test) && bun test src/App.test.tsx`
Expected: PASS.
Run: `dotnet test tests/AFK4.Platform.Api.Tests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "chore(stock): App.test навигация раздела; удалён мёртвый CashStockWorkspace + осиротевший CSS"
```

---

## Self-Review (выполнено при написании)

- **Покрытие spec (S0):** секция рейла (Task 4), 1 таб Остатки (Task 5), Остатки v2 с реальным порогом/себест/маржой/стоимостью/статусами/поиском/фильтрами (Task 6–7), правая Сводка (Task 7), средневзвешенная себестоимость бэк (Task 1–3), отвязка таба из Кассы + уборка (Task 5, 8), контраст-инвариант (Task 7 Step 4, Global Constraints). ✓
- **Отложено явно (не S0):** реальные действия приёмки/списания на строке (S1), разбивка Сводки по категориям (нужно имя категории в каталоге — backlog), табы Приёмка/Журнал/Инвентаризация (S1–S4). Помечено в задачах.
- **Типы:** `StockTab`/`STOCK_TAB_ORDER`/`visibleStockTabs` (Task 5) согласованы с использованием в `StockWorkspace`; `StockItem`/`stockStatus`/`marginPercent`/`stockValueMinorUnits`/`summarize` (Task 6) — с потреблением в Task 7; `AvgCostMinorUnits` (Task 1) — с пересчётом (Task 2) и чтением на фронте (Task 6). ✓
- **Открытый вопрос реализации:** точное имя поля средней на проводе (`avgCostMinorUnits` число) — зафиксировано в Task 6 Step 3; кто видит маржу/себестоимость (permission-видимость экономических колонок) — в S0 показываем всем с `inventory.view`; ужесточение — backlog.
