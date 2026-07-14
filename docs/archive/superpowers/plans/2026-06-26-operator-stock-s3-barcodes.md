# Operator Stock S3 — Штрих-коды Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Дать товару несколько штрих-кодов (модель + миграция + CRUD/эндпоинты), завести их в карточке товара (Управление→Товары), и подключить HID-сканер так, чтобы пик штриха клал товар в POS-чек и в строки приёмки (по уже загруженному каталогу, мгновенно).

**Architecture:** Новая таблица `product_barcodes` (голый `Guid ProductId` + индексы, без навигации/каскада — доминирующий паттерн репо). Штрихи едут **инлайн в каталоге** (`PosProductDto.Barcodes: string[]`, primary-first), грузятся одним словарём как `stockOnHand`. Уникальность кода в `(OrganizationId, BranchId, Code)` — enforce в **сервисе** (InMemory-провайдер тестов не enforce'ит индекс) + DB-индекс как backstop. **Lookup по штриху — клиентом** против загруженного каталога (мгновенно, без round-trip); серверный by-barcode-эндпоинт НЕ строим (см. Global Constraints). Сканер = чистый редьюсер (детектит быстрый HID-ввод + Enter, инжект `timeMs` для детерминизма тестов) + тонкий хук `useBarcodeScanner`.

**Tech Stack:** .NET 10 minimal API + EF Core (Postgres prod; InMemory в тестах; xUnit) · React 18 + TS + Vite · `bun test` (happy-dom + jest-dom) · `@afk4/i18n` (ICU) · `@afk4/tokens`.

## Global Constraints

- **Колонки БД — PascalCase** (нет snake_case-конвертера); snake_case ТОЛЬКО у имени таблицы через `entity.ToTable("product_barcodes")`. Колонки = имена свойств.
- **Связь сущности — голый `Guid ProductId` + индекс, БЕЗ навигации/FK/каскада** (доминирующий паттерн `PosProduct`/`StockMovement`/`ShopOrderLine`). Уникальный составной индекс: `entity.HasIndex(b => new { b.OrganizationId, b.BranchId, b.Code }).IsUnique();`.
- **Permission:** штрих-CRUD (создание/удаление) = `StaffPermissionNames.ManageInventoryStock` (= `"inventory.stock.manage"`, консистентно с product create/update). Чтение каталога (несёт штрихи) = `ViewInventory` (= `"inventory.view"`), без изменений. **Новых permission не заводим.**
- **Guard-паттерн эндпоинта** (как `PosEndpoints.cs:279-385`): `RequireBranchPermissionAsync(branchId, perm, ct)` → `!IsAuthenticated`→401 → `!IsAllowed`→403 (+ `WriteAuditAsync(... Denied)` на mutating) → `OrganizationId` ВСЕГДА из `authorization.StaffContext!.OrganizationId` (никогда из route/тела) → на mutating: IDOR-guard `request.OrganizationId != StaffContext.OrganizationId` → 400 → `WriteAuditAsync(...)` на успех.
- **Серверный by-barcode lookup-эндпоинт НЕ строим** (отклонение от спеки §6.1, утверждено): lookup идёт клиентом по `PosProductDto.Barcodes` загруженного каталога (мгновенный feedback на скан, каталог всегда загружен в POS и Приёмке — единственных местах скана; каталог филиала ограничен). Уникальность гарантирует бэк. Откатываемо.
- **Инвариант primary:** первый штрих товара → `IsPrimary=true`. Добавление нового primary демотит прочие. Удаление primary при наличии остатка → промоут старейшего оставшегося (всегда ровно один primary, если штрихов ≥1).
- **Деньги/контраст-инварианты склада** (из спеки §5): значения — `text-primary` яркие; заголовки/SKU ≥ `text-tertiary` (не `quaternary`); акцент emerald `var(--accent)` #2cc592 (НЕ синий); `--warning` только для предупреждений; `--danger` для «нет/опасно».
- **i18n:** локали `locales/{ru,en,tg}.json` (КОРЕНЬ репо), регенерация `cd packages/i18n && "$BUN" run gen`. Каждый UI-таск, вводящий `t('...')`-ключ, ОБЯЗАН добавить его во все три локали + регенерировать (иначе падает `i18nKeysExist.test.ts`). **tg — реальный таджикский** (анти `tg===ru` guard в `packages/i18n/src/messages.test.ts`); истинные заимствования/акронимы (EAN/SKU) → в whitelist `TG_IDENTICAL_TO_RU_ALLOWED`.
- **Тулчейн:** `BUN=/home/fedya/.bun/bin/bun`; `DOTNET=/home/fedya/.dotnet/dotnet`. Фронт-гейт = `bun test` (оба прогона) **И** `bun run build` (`tsc -b && vite build` — тайпчекает тест-файлы; типизируй bun-моки сигнатурой). Финальный гейт обязан включать `packages/i18n` тесты (tg-guard) + `dotnet test` бэка.
- **Миграция → деплой-гейт:** файл в `Data/Migrations/**` блокирует `Coolify Staging Deploy` workflow до ручного применения (см. afk4-env-quirks). Это POST-merge операционный шаг, НЕ часть PR-CI (InMemory тесты + build зелёные без применения миграции). Сурфейснуть пользователю после мержа.

---

## File Structure

**Бэкенд (создать):**
- `src/AFK4.Platform.Api/Data/ProductBarcodeEntity.cs` — сущность штриха.
- `src/AFK4.Platform.Api/Data/Migrations/<timestamp>_AddProductBarcodes.cs` (+`.Designer.cs`) — генерится `dotnet ef`.
- `src/AFK4.Shared.Contracts/Inventory/ProductBarcodeDto.cs` — DTO штриха.
- `src/AFK4.Shared.Contracts/Inventory/AddProductBarcodeRequest.cs` — тело POST.

**Бэкенд (изменить):**
- `src/AFK4.Platform.Api/Data/PlatformDbContext.cs` — DbSet + config `ProductBarcodeEntity`; обновится `PlatformDbContextModelSnapshot.cs` (ef-ом).
- `src/AFK4.Shared.Contracts/Pos/PosProductDto.cs` — `+ IReadOnlyList<string> Barcodes`.
- `src/AFK4.Platform.Api/Inventory/IInventoryService.cs` + `EfInventoryService.cs` — 3 метода штрихов + штрихи в каталоге + `ToDto`.
- `src/AFK4.Platform.Api/Endpoints/PosEndpoints.cs` — GET/POST/DELETE barcode-маршруты.

**Фронт (создать):**
- `src/AFK4.Operator.App.Web/src/barcodeScanner.ts` — чистый редьюсер сканера.
- `src/AFK4.Operator.App.Web/src/useBarcodeScanner.ts` — хук (window keydown → редьюсер → onScan).
- `src/AFK4.Operator.App.Web/src/settings/ProductBarcodesSection.tsx` — секция штрихов в карточке товара.

**Фронт (изменить):**
- `src/api/clients/settings.ts` — `getProductBarcodes`/`addProductBarcode`/`deleteProductBarcode` + типы.
- `src/AFK4.Operator.App.Web/src/settings/SettingsGoodsSection.tsx` — поле «Порог заказа» + монтаж `ProductBarcodesSection`.
- `src/AFK4.Operator.App.Web/src/BackendPosWorkspace.tsx` — бейдж сканера + lookup→корзина; dedup корзины по `productId`.
- `src/AFK4.Operator.App.Web/src/stock/ReceivingWorkspace.tsx` — бейдж + сканер→`addOrAccumulate`.
- `src/AFK4.Operator.App.Web/src/stock/receivingModel.ts` — хелпер `findByBarcode` (общий с POS).
- `src/AFK4.Operator.App.Web/src/styles/22-stock.css` (или `25-settings.css`) — чипы штрихов + бейдж сканера.
- `locales/{ru,en,tg}.json` + `packages/i18n/src/messages.test.ts` (whitelist) + регенерация `messages.ts`.

---

## Task 1: ProductBarcodeEntity + DbContext config + миграция

**Files:**
- Create: `src/AFK4.Platform.Api/Data/ProductBarcodeEntity.cs`
- Modify: `src/AFK4.Platform.Api/Data/PlatformDbContext.cs` (DbSet рядом с `:79-81`; config рядом с `PosProductEntity` config `:615-634`)
- Generated: `src/AFK4.Platform.Api/Data/Migrations/<ts>_AddProductBarcodes.cs` (+`.Designer.cs`), `PlatformDbContextModelSnapshot.cs`

**Interfaces:**
- Produces: `ProductBarcodeEntity { Guid BarcodeId (PK); Guid OrganizationId; Guid BranchId; Guid ProductId; string Code; bool IsPrimary; DateTimeOffset CreatedAtUtc; }`; `PlatformDbContext.ProductBarcodes` (`DbSet<ProductBarcodeEntity>`).

- [ ] **Step 1: Создать сущность**

`src/AFK4.Platform.Api/Data/ProductBarcodeEntity.cs`:
```csharp
namespace AFK4.Platform.Api.Data;

public sealed class ProductBarcodeEntity
{
    public Guid BarcodeId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid BranchId { get; set; }
    public Guid ProductId { get; set; }
    public string Code { get; set; } = "";
    public bool IsPrimary { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
```

- [ ] **Step 2: Зарегистрировать DbSet**

В `PlatformDbContext.cs` рядом с `:81` (`public DbSet<StockMovementEntity> StockMovements => Set<StockMovementEntity>();`) добавить:
```csharp
public DbSet<ProductBarcodeEntity> ProductBarcodes => Set<ProductBarcodeEntity>();
```

- [ ] **Step 3: Конфигурация в OnModelCreating**

В `PlatformDbContext.cs` сразу после блока `modelBuilder.Entity<PosProductEntity>(...)` (`:615-634`) добавить:
```csharp
modelBuilder.Entity<ProductBarcodeEntity>(entity =>
{
    entity.ToTable("product_barcodes");
    entity.HasKey(barcode => barcode.BarcodeId);
    entity.Property(barcode => barcode.Code).HasMaxLength(64).IsRequired();
    entity.HasIndex(barcode => new { barcode.OrganizationId, barcode.BranchId, barcode.Code }).IsUnique();
    entity.HasIndex(barcode => new { barcode.OrganizationId, barcode.BranchId, barcode.ProductId });
});
```

- [ ] **Step 4: Собрать API (свежий модель — иначе пустая миграция)**

Run:
```bash
cd /home/fedya/projects/afk4.net
/home/fedya/.dotnet/dotnet build src/AFK4.Platform.Api/AFK4.Platform.Api.csproj
```
Expected: `Build succeeded`. (Критично: `dotnet ef ... --no-build` берёт ПОСЛЕДНЮЮ сборку; без этого шага миграция выйдет пустой.)

- [ ] **Step 5: Сгенерировать миграцию**

Run:
```bash
cd /home/fedya/projects/afk4.net
/home/fedya/.dotnet/dotnet ef migrations add AddProductBarcodes \
  --project src/AFK4.Platform.Api \
  --output-dir Data/Migrations \
  --no-build
```
Expected: создаются `Data/Migrations/<ts>_AddProductBarcodes.cs` + `.Designer.cs`, меняется `PlatformDbContextModelSnapshot.cs`.

- [ ] **Step 6: Проверить, что миграция НЕ пустая**

Run: `sed -n '/protected override void Up/,/^    }/p' src/AFK4.Platform.Api/Data/Migrations/*_AddProductBarcodes.cs`
Expected: видны `migrationBuilder.CreateTable(name: "product_barcodes", ...)` с колонками `BarcodeId/OrganizationId/BranchId/ProductId/Code/IsPrimary/CreatedAtUtc`, `CreateIndex` уникальный по `(OrganizationId, BranchId, Code)` и обычный по `(OrganizationId, BranchId, ProductId)`; в `Down` — `DropTable`. Если `Up`/`Down` пустые — удалить оба `.cs`+`.Designer.cs` (НЕ `dotnet ef migrations remove` — он коннектится к БД), пересобрать (Step 4) и повторить Step 5.

- [ ] **Step 7: Собрать решение целиком (миграция компилируется)**

Run: `/home/fedya/.dotnet/dotnet build AFK4.sln`
Expected: `Build succeeded`.

- [ ] **Step 8: Commit**

```bash
git add src/AFK4.Platform.Api/Data/ProductBarcodeEntity.cs \
        src/AFK4.Platform.Api/Data/PlatformDbContext.cs \
        src/AFK4.Platform.Api/Data/Migrations/
git commit -m "feat(inventory): ProductBarcodeEntity + миграция AddProductBarcodes"
```

---

## Task 2: Контракты — ProductBarcodeDto, AddProductBarcodeRequest, PosProductDto.Barcodes

**Files:**
- Create: `src/AFK4.Shared.Contracts/Inventory/ProductBarcodeDto.cs`
- Create: `src/AFK4.Shared.Contracts/Inventory/AddProductBarcodeRequest.cs`
- Modify: `src/AFK4.Shared.Contracts/Pos/PosProductDto.cs:5-20`
- Test: `tests/AFK4.Shared.Contracts.Tests/InventoryContractSerializationTests.cs`

**Interfaces:**
- Consumes: `PosProductDto` (Task 1 не трогал). 
- Produces:
  - `ProductBarcodeDto(Guid BarcodeId, Guid ProductId, string Code, bool IsPrimary)`
  - `AddProductBarcodeRequest(Guid OrganizationId, string Code, bool IsPrimary = false)`
  - `PosProductDto` хвостовой параметр `IReadOnlyList<string> Barcodes` (default — пустой список).

- [ ] **Step 1: Написать падающие round-trip тесты**

В `tests/AFK4.Shared.Contracts.Tests/InventoryContractSerializationTests.cs` (паттерн как `PosProductDto_RoundTrips_AvgCostMinorUnits` `:93-104`) добавить:
```csharp
[Fact]
public void ProductBarcodeDto_RoundTrips()
{
    var dto = new ProductBarcodeDto(Guid.NewGuid(), Guid.NewGuid(), "4601234567890", IsPrimary: true);
    var back = JsonSerializer.Deserialize<ProductBarcodeDto>(JsonSerializer.Serialize(dto));
    Assert.Equal("4601234567890", back!.Code);
    Assert.True(back.IsPrimary);
}

[Fact]
public void PosProductDto_RoundTrips_Barcodes()
{
    var dto = new PosProductDto(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        "Snickers", "SNICKERS", new MoneyDto("TJS", 1000),
        true, false, true, 15, DateTimeOffset.UnixEpoch,
        ReorderThreshold: 10, AvailableInShell: false, AvgCostMinorUnits: 500,
        Barcodes: new[] { "4601234567890", "0000111122223" });
    var back = JsonSerializer.Deserialize<PosProductDto>(JsonSerializer.Serialize(dto));
    Assert.Equal(2, back!.Barcodes.Count);
    Assert.Equal("4601234567890", back.Barcodes[0]);
}

[Fact]
public void PosProductDto_Barcodes_DefaultsToEmpty()
{
    var dto = new PosProductDto(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        "Cola", "COLA", new MoneyDto("TJS", 500),
        true, false, true, 3, DateTimeOffset.UnixEpoch);
    Assert.NotNull(dto.Barcodes);
    Assert.Empty(dto.Barcodes);
}
```

- [ ] **Step 2: Прогнать — падает на «type not found»**

Run: `cd /home/fedya/projects/afk4.net && /home/fedya/.dotnet/dotnet test tests/AFK4.Shared.Contracts.Tests`
Expected: FAIL (компиляция — `ProductBarcodeDto`/`Barcodes` не существуют).

- [ ] **Step 3: Создать DTO**

`src/AFK4.Shared.Contracts/Inventory/ProductBarcodeDto.cs`:
```csharp
namespace AFK4.Shared.Contracts.Inventory;

public sealed record ProductBarcodeDto(Guid BarcodeId, Guid ProductId, string Code, bool IsPrimary);
```
`src/AFK4.Shared.Contracts/Inventory/AddProductBarcodeRequest.cs`:
```csharp
namespace AFK4.Shared.Contracts.Inventory;

public sealed record AddProductBarcodeRequest(Guid OrganizationId, string Code, bool IsPrimary = false);
```

- [ ] **Step 4: Дополнить PosProductDto**

`src/AFK4.Shared.Contracts/Pos/PosProductDto.cs` — добавить хвостовой параметр (после `AvgCostMinorUnits = 0`):
```csharp
public sealed record PosProductDto(
    Guid ProductId, Guid OrganizationId, Guid BranchId, Guid CategoryId,
    string Name, string Sku, MoneyDto Price,
    bool TrackStock, bool AllowNegativeStock, bool IsActive,
    int StockOnHand, DateTimeOffset CreatedAtUtc,
    int ReorderThreshold = 0,
    bool AvailableInShell = false,
    long AvgCostMinorUnits = 0,
    IReadOnlyList<string>? Barcodes = null)
{
    public IReadOnlyList<string> Barcodes { get; init; } = Barcodes ?? Array.Empty<string>();
}
```
(Nullable-параметр с дефолтом, чтобы старые сериализованные payload'ы без поля давали `[]`, а не `null`.)

- [ ] **Step 5: Прогнать — зелёные**

Run: `cd /home/fedya/projects/afk4.net && /home/fedya/.dotnet/dotnet test tests/AFK4.Shared.Contracts.Tests`
Expected: PASS (включая 3 новых).

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Shared.Contracts/Inventory/ProductBarcodeDto.cs \
        src/AFK4.Shared.Contracts/Inventory/AddProductBarcodeRequest.cs \
        src/AFK4.Shared.Contracts/Pos/PosProductDto.cs \
        tests/AFK4.Shared.Contracts.Tests/InventoryContractSerializationTests.cs
git commit -m "feat(inventory): контракты ProductBarcodeDto + PosProductDto.Barcodes"
```

---

## Task 3: Сервис — CRUD штрихов + штрихи в каталоге

**Files:**
- Modify: `src/AFK4.Platform.Api/Inventory/IInventoryService.cs:7-51`
- Modify: `src/AFK4.Platform.Api/Inventory/EfInventoryService.cs` (`GetCatalogAsync:407-436`, `ToDto:778-796`, + 3 новых метода)
- Test: `tests/AFK4.Platform.Api.Tests/Inventory/ProductBarcodeServiceTests.cs` (новый, папка `Inventory/` уже есть)

**Interfaces:**
- Consumes: `ProductBarcodeDto`, `AddProductBarcodeRequest` (Task 2); `BillingCommandServiceResult<T>`; `EfInventoryService(PlatformDbContext, TimeProvider, ILowStockNotifier?)`.
- Produces (новые методы `IInventoryService`):
  - `Task<BillingCommandServiceResult<IReadOnlyList<ProductBarcodeDto>>> GetProductBarcodesAsync(Guid organizationId, Guid branchId, Guid productId, CancellationToken ct);`
  - `Task<BillingCommandServiceResult<ProductBarcodeDto>> AddProductBarcodeAsync(Guid branchId, Guid actorStaffUserId, Guid productId, AddProductBarcodeRequest request, CancellationToken ct);`
  - `Task<BillingCommandServiceResult<ProductBarcodeDto>> DeleteProductBarcodeAsync(Guid organizationId, Guid branchId, Guid productId, Guid barcodeId, CancellationToken ct);`
  - `GetCatalogAsync` теперь наполняет `PosProductDto.Barcodes` (primary-first, затем по `CreatedAtUtc`).

- [ ] **Step 1: Написать падающие тесты сервиса**

`tests/AFK4.Platform.Api.Tests/Inventory/ProductBarcodeServiceTests.cs` (фабрика/хелперы как `EfInventoryServiceTests.cs:568-620` — `CreateDbContext`, `CreateService`, `CreateTrackedProductAsync`, `TestIds`):
```csharp
using AFK4.Platform.Api.Inventory;
using AFK4.Shared.Contracts.Inventory;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AFK4.Platform.Api.Tests.Inventory;

public sealed class ProductBarcodeServiceTests
{
    private const string Now = "2026-06-26T10:00:00Z";

    [Fact]
    public async Task AddFirstBarcode_IsPrimary()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var product = await CreateTrackedProductAsync(service);

        var result = await service.AddProductBarcodeAsync(
            TestIds.BranchId, ActorStaffUserId, product.ProductId,
            new AddProductBarcodeRequest(TestIds.OrganizationId, "4601234567890"),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.True(result.Response!.IsPrimary);
    }

    [Fact]
    public async Task AddDuplicateCode_FailsWithConflict()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var a = await CreateTrackedProductAsync(service);
        var b = await CreateTrackedProductAsync(service, sku: "SKU-B");
        await service.AddProductBarcodeAsync(TestIds.BranchId, ActorStaffUserId, a.ProductId,
            new AddProductBarcodeRequest(TestIds.OrganizationId, "111"), CancellationToken.None);

        var dup = await service.AddProductBarcodeAsync(TestIds.BranchId, ActorStaffUserId, b.ProductId,
            new AddProductBarcodeRequest(TestIds.OrganizationId, "111"), CancellationToken.None);

        Assert.False(dup.Succeeded);
        Assert.Single(await db.ProductBarcodes.ToListAsync());
    }

    [Fact]
    public async Task AddSecondPrimary_DemotesFirst()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var p = await CreateTrackedProductAsync(service);
        await service.AddProductBarcodeAsync(TestIds.BranchId, ActorStaffUserId, p.ProductId,
            new AddProductBarcodeRequest(TestIds.OrganizationId, "111"), CancellationToken.None);

        await service.AddProductBarcodeAsync(TestIds.BranchId, ActorStaffUserId, p.ProductId,
            new AddProductBarcodeRequest(TestIds.OrganizationId, "222", IsPrimary: true), CancellationToken.None);

        var list = (await service.GetProductBarcodesAsync(TestIds.OrganizationId, TestIds.BranchId, p.ProductId, CancellationToken.None)).Response!;
        Assert.Equal("222", list.Single(x => x.IsPrimary).Code);
        Assert.Single(list, x => x.IsPrimary);
    }

    [Fact]
    public async Task DeletePrimary_PromotesOldestRemaining()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var p = await CreateTrackedProductAsync(service);
        var first = (await service.AddProductBarcodeAsync(TestIds.BranchId, ActorStaffUserId, p.ProductId,
            new AddProductBarcodeRequest(TestIds.OrganizationId, "111"), CancellationToken.None)).Response!;
        await service.AddProductBarcodeAsync(TestIds.BranchId, ActorStaffUserId, p.ProductId,
            new AddProductBarcodeRequest(TestIds.OrganizationId, "222"), CancellationToken.None);

        await service.DeleteProductBarcodeAsync(TestIds.OrganizationId, TestIds.BranchId, p.ProductId, first.BarcodeId, CancellationToken.None);

        var list = (await service.GetProductBarcodesAsync(TestIds.OrganizationId, TestIds.BranchId, p.ProductId, CancellationToken.None)).Response!;
        Assert.Single(list);
        Assert.True(list[0].IsPrimary); // оставшийся промоутнут
    }

    [Fact]
    public async Task AddToForeignOrgProduct_Fails()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var p = await CreateTrackedProductAsync(service);

        var result = await service.AddProductBarcodeAsync(TestIds.BranchId, ActorStaffUserId, p.ProductId,
            new AddProductBarcodeRequest(Guid.NewGuid() /* чужая org */, "111"), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Empty(await db.ProductBarcodes.ToListAsync());
    }

    [Fact]
    public async Task Catalog_IncludesBarcodes_PrimaryFirst()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var p = await CreateTrackedProductAsync(service);
        await service.AddProductBarcodeAsync(TestIds.BranchId, ActorStaffUserId, p.ProductId,
            new AddProductBarcodeRequest(TestIds.OrganizationId, "AAA"), CancellationToken.None);
        await service.AddProductBarcodeAsync(TestIds.BranchId, ActorStaffUserId, p.ProductId,
            new AddProductBarcodeRequest(TestIds.OrganizationId, "BBB", IsPrimary: true), CancellationToken.None);

        var catalog = (await service.GetCatalogAsync(TestIds.OrganizationId, TestIds.BranchId, CancellationToken.None)).Response!;
        var dto = catalog.Single(x => x.ProductId == p.ProductId);
        Assert.Equal(new[] { "BBB", "AAA" }, dto.Barcodes);
    }

    // --- хелперы (зеркалят EfInventoryServiceTests) ---
    private static readonly Guid ActorStaffUserId = Guid.Parse("00000000-0000-0000-0000-0000000000aa");
    private static PlatformDbContext CreateDbContext() => new(new DbContextOptionsBuilder<PlatformDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
    private static EfInventoryService CreateService(PlatformDbContext db) =>
        new(db, new FixedTimeProvider(DateTimeOffset.Parse(Now)));
    private static async Task<PosProductDto> CreateTrackedProductAsync(EfInventoryService service, string sku = "SKU-A")
    {
        // ВНИМАНИЕ: переиспользуй существующие SeedOwnerAsync/CreateCategoryAsync/CreateTrackedProductAsync
        // из EfInventoryServiceTests — вынеси их в общий helper или продублируй сид здесь.
        throw new NotImplementedException("заменить на реальный сид как в EfInventoryServiceTests.cs:582-620");
    }
}
```
> Реализатору: НЕ оставляй `NotImplementedException` — переиспользуй сид-хелперы из `EfInventoryServiceTests.cs:582-620` (`SeedOwnerAsync`, `CreateCategoryAsync`, `CreateTrackedProductAsync`) — вынеси их в общий `InventoryTestFixtures` или скопируй рабочий сид. `FixedTimeProvider` — там же. `TestIds` — общий тестовый класс.

- [ ] **Step 2: Прогнать — падает**

Run: `cd /home/fedya/projects/afk4.net && /home/fedya/.dotnet/dotnet test tests/AFK4.Platform.Api.Tests --filter ProductBarcodeServiceTests`
Expected: FAIL (методы не существуют / `NotImplementedException` заменён, методы интерфейса отсутствуют).

- [ ] **Step 3: Расширить интерфейс**

В `IInventoryService.cs` добавить 3 сигнатуры из блока **Produces** выше.

- [ ] **Step 4: Реализовать методы в EfInventoryService**

Добавить в `EfInventoryService.cs`:
```csharp
public async Task<BillingCommandServiceResult<IReadOnlyList<ProductBarcodeDto>>> GetProductBarcodesAsync(
    Guid organizationId, Guid branchId, Guid productId, CancellationToken ct)
{
    if (organizationId == Guid.Empty)
        return BillingCommandServiceResult<IReadOnlyList<ProductBarcodeDto>>.Failure("Organization is required.");

    var rows = await dbContext.ProductBarcodes.AsNoTracking()
        .Where(b => b.OrganizationId == organizationId && b.BranchId == branchId && b.ProductId == productId)
        .OrderByDescending(b => b.IsPrimary).ThenBy(b => b.CreatedAtUtc)
        .ToListAsync(ct);

    IReadOnlyList<ProductBarcodeDto> dtos = rows
        .Select(b => new ProductBarcodeDto(b.BarcodeId, b.ProductId, b.Code, b.IsPrimary)).ToList();
    return BillingCommandServiceResult<IReadOnlyList<ProductBarcodeDto>>.Ok(dtos);
}

public async Task<BillingCommandServiceResult<ProductBarcodeDto>> AddProductBarcodeAsync(
    Guid branchId, Guid actorStaffUserId, Guid productId, AddProductBarcodeRequest request, CancellationToken ct)
{
    var code = (request.Code ?? "").Trim();
    if (code.Length == 0)
        return BillingCommandServiceResult<ProductBarcodeDto>.Failure("Barcode code is required.");
    if (code.Length > 64)
        return BillingCommandServiceResult<ProductBarcodeDto>.Failure("Barcode code is too long.");

    // товар существует в этой org/branch (IDOR + tenant guard)
    var product = await dbContext.PosProducts.AsNoTracking().FirstOrDefaultAsync(
        p => p.OrganizationId == request.OrganizationId && p.BranchId == branchId
          && p.ProductId == productId && p.IsActive, ct);
    if (product is null)
        return BillingCommandServiceResult<ProductBarcodeDto>.Failure("Product not found.");

    // уникальность кода в (org, branch) — сервисный enforce (InMemory не enforce'ит индекс)
    var clash = await dbContext.ProductBarcodes.AsNoTracking().AnyAsync(
        b => b.OrganizationId == request.OrganizationId && b.BranchId == branchId && b.Code == code, ct);
    if (clash)
        return BillingCommandServiceResult<ProductBarcodeDto>.Failure("Barcode is already bound to a product.");

    var existing = await dbContext.ProductBarcodes
        .Where(b => b.OrganizationId == request.OrganizationId && b.BranchId == branchId && b.ProductId == productId)
        .ToListAsync(ct);
    var makePrimary = request.IsPrimary || existing.Count == 0;
    if (makePrimary)
        foreach (var row in existing) row.IsPrimary = false;

    var entity = new ProductBarcodeEntity
    {
        BarcodeId = Guid.NewGuid(),
        OrganizationId = request.OrganizationId,
        BranchId = branchId,
        ProductId = productId,
        Code = code,
        IsPrimary = makePrimary,
        CreatedAtUtc = timeProvider.GetUtcNow(),
    };
    dbContext.ProductBarcodes.Add(entity);
    await dbContext.SaveChangesAsync(ct);
    return BillingCommandServiceResult<ProductBarcodeDto>.Ok(
        new ProductBarcodeDto(entity.BarcodeId, entity.ProductId, entity.Code, entity.IsPrimary));
}

public async Task<BillingCommandServiceResult<ProductBarcodeDto>> DeleteProductBarcodeAsync(
    Guid organizationId, Guid branchId, Guid productId, Guid barcodeId, CancellationToken ct)
{
    var target = await dbContext.ProductBarcodes.FirstOrDefaultAsync(
        b => b.OrganizationId == organizationId && b.BranchId == branchId
          && b.ProductId == productId && b.BarcodeId == barcodeId, ct);
    if (target is null)
        return BillingCommandServiceResult<ProductBarcodeDto>.Failure("Barcode not found.");

    dbContext.ProductBarcodes.Remove(target);

    if (target.IsPrimary)
    {
        var promote = await dbContext.ProductBarcodes
            .Where(b => b.OrganizationId == organizationId && b.BranchId == branchId
                     && b.ProductId == productId && b.BarcodeId != barcodeId)
            .OrderBy(b => b.CreatedAtUtc).FirstOrDefaultAsync(ct);
        if (promote is not null) promote.IsPrimary = true;
    }
    await dbContext.SaveChangesAsync(ct);
    return BillingCommandServiceResult<ProductBarcodeDto>.Ok(
        new ProductBarcodeDto(target.BarcodeId, target.ProductId, target.Code, target.IsPrimary));
}
```
> Реализатору: точные имена фабричных методов `BillingCommandServiceResult<T>.Ok(...)`/`.Failure(...)` и поле primary-ctor (`dbContext`/`timeProvider`) сверь с реальным `EfInventoryService.cs` — используй те же, что уже в файле (напр. как формируются провалы в `CreateStockMovementAsync:289-405`).

- [ ] **Step 5: Наполнить штрихами каталог**

В `GetCatalogAsync` (`:407-436`) — рядом с построением `stockByProductId` загрузить штрихи и передать в `ToDto`. Изменить сигнатуру `ToDto(PosProductEntity, int stockOnHand)` → `ToDto(PosProductEntity product, int stockOnHand, IReadOnlyList<string> barcodes)` и в конце маппинга (`:778-796`) добавить `barcodes` в хвост конструктора `PosProductDto`. Пример наполнения в `GetCatalogAsync`:
```csharp
var productIds = products.Select(p => p.ProductId).ToList();
var barcodeRows = await dbContext.ProductBarcodes.AsNoTracking()
    .Where(b => b.OrganizationId == organizationId && b.BranchId == branchId && productIds.Contains(b.ProductId))
    .OrderByDescending(b => b.IsPrimary).ThenBy(b => b.CreatedAtUtc)
    .ToListAsync(ct);
var barcodesByProduct = barcodeRows
    .GroupBy(b => b.ProductId)
    .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(b => b.Code).ToList());

// в Select(...): ToDto(product, stockByProductId.GetValueOrDefault(product.ProductId),
//                       barcodesByProduct.GetValueOrDefault(product.ProductId) ?? Array.Empty<string>())
```
> Прочие вызовы `ToDto` (create/update product `:161-177`/`:265-274`) передают `Array.Empty<string>()` (у только что созданного товара штрихов нет).

- [ ] **Step 6: Прогнать — зелёные**

Run: `cd /home/fedya/projects/afk4.net && /home/fedya/.dotnet/dotnet test tests/AFK4.Platform.Api.Tests --filter ProductBarcodeServiceTests`
Expected: PASS (6 тестов). Затем полный `/home/fedya/.dotnet/dotnet test tests/AFK4.Platform.Api.Tests` — без регрессий (особенно существующие каталог-тесты после смены сигнатуры `ToDto`).

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.Platform.Api/Inventory/ tests/AFK4.Platform.Api.Tests/Inventory/ProductBarcodeServiceTests.cs
git commit -m "feat(inventory): сервис CRUD штрихов + штрихи в каталоге (primary-first)"
```

---

## Task 4: Эндпоинты — barcode CRUD

**Files:**
- Modify: `src/AFK4.Platform.Api/Endpoints/PosEndpoints.cs` (рядом с product-маршрутами `:145-274`)
- Test: `tests/AFK4.Platform.Api.Tests/` — найти существующий тест эндпоинтов POS (если есть `PosEndpointsTests`/WebApplicationFactory); иначе пермишн-контракт покрыт Task 3 + ручной smoke. Реализатор: проверь наличие endpoint-теста и следуй паттерну; если эндпоинты в проекте не тестируются отдельно (тонкие pass-through) — добавить один happy-path + один 403 через существующий test-host паттерн, либо явно отметить отсутствие endpoint-харнеса в отчёте.

**Interfaces:**
- Consumes: `IInventoryService.GetProductBarcodesAsync/AddProductBarcodeAsync/DeleteProductBarcodeAsync` (Task 3); guard-хелперы `RequireBranchPermissionAsync`, `WriteAuditAsync`, `ToHttpResult` (в `PosEndpoints.cs`/`EndpointHelpers`).

- [ ] **Step 1: Добавить три маршрута** (в `MapPosEndpoints`, по образцу catalog `:279-306` и stock-movements `:341-385`)

```csharp
// GET список штрихов товара — ViewInventory
app.MapGet("/api/branches/{branchId:guid}/pos/products/{productId:guid}/barcodes", async (
    Guid branchId, Guid productId,
    StaffAuthorizationService authorizationService, IInventoryService inventoryService,
    CancellationToken ct) =>
{
    var auth = await authorizationService.RequireBranchPermissionAsync(branchId, StaffPermissionNames.ViewInventory, ct);
    if (!auth.IsAuthenticated) return Results.Unauthorized();
    if (!auth.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);
    var result = await inventoryService.GetProductBarcodesAsync(auth.StaffContext!.OrganizationId, branchId, productId, ct);
    return ToHttpResult(result);
});

// POST привязать штрих — ManageInventoryStock (+ IDOR + audit)
app.MapPost("/api/branches/{branchId:guid}/pos/products/{productId:guid}/barcodes", async (
    Guid branchId, Guid productId, AddProductBarcodeRequest request,
    StaffAuthorizationService authorizationService, IInventoryService inventoryService,
    /* IAuditWriter / тот же набор сервисов, что в POST stock-movements */ CancellationToken ct) =>
{
    var auth = await authorizationService.RequireBranchPermissionAsync(branchId, StaffPermissionNames.ManageInventoryStock, ct);
    if (!auth.IsAuthenticated) return Results.Unauthorized();
    if (!auth.IsAllowed) { /* WriteAuditAsync(... Denied ...) как в :341-385 */ return Results.StatusCode(StatusCodes.Status403Forbidden); }
    if (request.OrganizationId != auth.StaffContext!.OrganizationId)
        return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
    var result = await inventoryService.AddProductBarcodeAsync(branchId, auth.StaffContext.StaffUserId, productId, request, ct);
    /* WriteAuditAsync(... succeeded ...) как в :341-385 */
    return ToHttpResult(result);
});

// DELETE отвязать штрих — ManageInventoryStock (+ audit)
app.MapDelete("/api/branches/{branchId:guid}/pos/products/{productId:guid}/barcodes/{barcodeId:guid}", async (
    Guid branchId, Guid productId, Guid barcodeId,
    StaffAuthorizationService authorizationService, IInventoryService inventoryService,
    CancellationToken ct) =>
{
    var auth = await authorizationService.RequireBranchPermissionAsync(branchId, StaffPermissionNames.ManageInventoryStock, ct);
    if (!auth.IsAuthenticated) return Results.Unauthorized();
    if (!auth.IsAllowed) { /* WriteAuditAsync(... Denied ...) */ return Results.StatusCode(StatusCodes.Status403Forbidden); }
    var result = await inventoryService.DeleteProductBarcodeAsync(auth.StaffContext!.OrganizationId, branchId, productId, barcodeId, ct);
    /* WriteAuditAsync(...) */
    return ToHttpResult(result);
});
```
> Реализатору: точные имена audit-хелпера/его аргументов и DI-сервисов скопируй из POST `/inventory/stock-movements` (`PosEndpoints.cs:341-385`) — не выдумывай. Audit-action-строку назвать в стиле существующих (напр. `"inventory.barcode.add"` / `"inventory.barcode.delete"`).

- [ ] **Step 2: Собрать**

Run: `/home/fedya/.dotnet/dotnet build src/AFK4.Platform.Api/AFK4.Platform.Api.csproj`
Expected: `Build succeeded`.

- [ ] **Step 3: Тест(ы) эндпоинта по существующему харнесу** (если есть). Иначе — отметить в отчёте, что эндпоинты тонкие и логика покрыта Task 3; добавить минимум один happy-path, если test-host доступен.

- [ ] **Step 4: Прогнать бэк целиком**

Run: `cd /home/fedya/projects/afk4.net && /home/fedya/.dotnet/dotnet test tests/AFK4.Platform.Api.Tests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Api/Endpoints/PosEndpoints.cs tests/AFK4.Platform.Api.Tests/
git commit -m "feat(inventory): эндпоинты barcode CRUD (ViewInventory/ManageInventoryStock + IDOR + audit)"
```

---

## Task 5: Фронт — API-клиент штрихов + типы

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/api/clients/settings.ts` (рядом с `createProduct`/`updateProduct` `:204-209`, типы `:115-123`)
- Test: `src/AFK4.Operator.App.Web/src/api/clients/settings.barcodes.test.ts` (новый)

**Interfaces:**
- Consumes: `PlatformApiClient` verbs `get`/`post`/`delete` (`platformApi.ts`); `Guid = string`.
- Produces (на settings-клиенте):
  - `getProductBarcodes(branchId: Guid, productId: Guid): Promise<ProductBarcodeDto[]>` → GET `/api/branches/{branchId}/pos/products/{productId}/barcodes`
  - `addProductBarcode(branchId: Guid, productId: Guid, request: AddProductBarcodeRequest): Promise<ProductBarcodeDto>` → POST same path
  - `deleteProductBarcode(branchId: Guid, productId: Guid, barcodeId: Guid): Promise<void>` → DELETE `.../barcodes/{barcodeId}`
  - Типы `ProductBarcodeDto { barcodeId: Guid; productId: Guid; code: string; isPrimary: boolean }`, `AddProductBarcodeRequest extends Record<string, unknown> { organizationId: Guid; code: string; isPrimary?: boolean }`.

- [ ] **Step 1: Падающий тест клиента** (типизируй моки — `bun run build` тайпчекает тесты)

`settings.barcodes.test.ts`:
```ts
import { describe, it, expect, mock } from 'bun:test';
import { createSettingsClient } from './settings';

function fakeApi() {
  const calls: Array<{ method: string; path: string; body?: unknown }> = [];
  const api = {
    get: mock(async (path: string) => { calls.push({ method: 'GET', path }); return []; }),
    post: mock(async (path: string, body: Record<string, unknown>) => { calls.push({ method: 'POST', path, body }); return { barcodeId: 'b1', productId: 'p1', code: '111', isPrimary: true }; }),
    delete: mock(async (path: string) => { calls.push({ method: 'DELETE', path }); return null; }),
  };
  return { api, calls };
}

describe('settings barcode client', () => {
  it('GET barcodes hits the product barcodes path', async () => {
    const { api, calls } = fakeApi();
    const client = createSettingsClient(api as never);
    await client.getProductBarcodes('br1', 'p1');
    expect(calls[0]).toEqual({ method: 'GET', path: '/api/branches/br1/pos/products/p1/barcodes' });
  });

  it('POST barcode sends organizationId + code in body', async () => {
    const { api, calls } = fakeApi();
    const client = createSettingsClient(api as never);
    const res = await client.addProductBarcode('br1', 'p1', { organizationId: 'org1', code: '111', isPrimary: true });
    expect(calls[0].path).toBe('/api/branches/br1/pos/products/p1/barcodes');
    expect(calls[0].body).toMatchObject({ organizationId: 'org1', code: '111', isPrimary: true });
    expect(res.code).toBe('111');
  });

  it('DELETE barcode hits the barcode id path', async () => {
    const { api, calls } = fakeApi();
    const client = createSettingsClient(api as never);
    await client.deleteProductBarcode('br1', 'p1', 'b1');
    expect(calls[0]).toEqual({ method: 'DELETE', path: '/api/branches/br1/pos/products/p1/barcodes/b1' });
  });
});
```
> Реализатору: реальное имя фабрики (`createSettingsClient`) и форму `api`-параметра сверь с `settings.ts`. Если методы клиента группируются иначе — следуй файлу.

- [ ] **Step 2: Прогнать — падает**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/api/clients/settings.barcodes.test.ts`
Expected: FAIL (методы не существуют).

- [ ] **Step 3: Добавить типы и методы** в `settings.ts`:
```ts
export interface ProductBarcodeDto { barcodeId: Guid; productId: Guid; code: string; isPrimary: boolean }
export interface AddProductBarcodeRequest extends Record<string, unknown> {
  organizationId: Guid; code: string; isPrimary?: boolean;
}
// внутри createSettingsClient(api) — рядом с createProduct/updateProduct:
getProductBarcodes(branchId: Guid, productId: Guid): Promise<ProductBarcodeDto[]> {
  return api.get<ProductBarcodeDto[]>(`/api/branches/${branchId}/pos/products/${productId}/barcodes`);
},
addProductBarcode(branchId: Guid, productId: Guid, request: AddProductBarcodeRequest): Promise<ProductBarcodeDto> {
  return api.post<ProductBarcodeDto, AddProductBarcodeRequest>(`/api/branches/${branchId}/pos/products/${productId}/barcodes`, request);
},
deleteProductBarcode(branchId: Guid, productId: Guid, barcodeId: Guid): Promise<void> {
  return api.delete<void>(`/api/branches/${branchId}/pos/products/${productId}/barcodes/${barcodeId}`);
},
```

- [ ] **Step 4: Прогнать — зелёные** + типчек

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/api/clients/settings.barcodes.test.ts && /home/fedya/.bun/bin/bun run build`
Expected: PASS + `tsc -b` зелёный (моки типизированы).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/api/clients/settings.ts src/AFK4.Operator.App.Web/src/api/clients/settings.barcodes.test.ts
git commit -m "feat(operator): API-клиент штрихов товара (get/add/delete)"
```

---

## Task 6: useBarcodeScanner — чистый редьюсер + хук

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/barcodeScanner.ts` (чистая логика)
- Create: `src/AFK4.Operator.App.Web/src/useBarcodeScanner.ts` (хук)
- Test: `src/AFK4.Operator.App.Web/src/barcodeScanner.test.ts`

**Interfaces:**
- Produces:
  - `interface ScannerState { buffer: string; lastKeyMs: number }`
  - `const EMPTY_SCANNER: ScannerState = { buffer: '', lastKeyMs: 0 }`
  - `interface ScannerOptions { minLength?: number; maxInterKeyMs?: number }` (дефолты `MIN_CODE_LENGTH=3`, `MAX_INTER_KEY_MS=50`)
  - `interface ScannerStep { state: ScannerState; scanned?: string; capture: boolean }`
  - `function feedScanner(state: ScannerState, key: string, timeMs: number, opts?: ScannerOptions): ScannerStep`
  - Хук `function useBarcodeScanner(enabled: boolean, onScan: (code: string) => void, opts?: ScannerOptions): void`

- [ ] **Step 1: Падающие тесты редьюсера** (детерминизм через явный `timeMs`, без фейк-таймеров)

`barcodeScanner.test.ts`:
```ts
import { describe, it, expect } from 'bun:test';
import { feedScanner, EMPTY_SCANNER } from './barcodeScanner';

function run(keys: Array<[string, number]>) {
  let state = EMPTY_SCANNER;
  let scanned: string | undefined;
  let captures = 0;
  for (const [key, t] of keys) {
    const step = feedScanner(state, key, t);
    state = step.state;
    if (step.scanned) scanned = step.scanned;
    if (step.capture) captures++;
  }
  return { scanned, captures };
}

describe('feedScanner', () => {
  it('fast digits + Enter → scanned code', () => {
    const { scanned } = run([['4', 0], ['6', 10], ['0', 20], ['1', 30], ['Enter', 40]]);
    expect(scanned).toBe('4601');
  });

  it('slow human typing + Enter → no scan', () => {
    const { scanned } = run([['4', 0], ['6', 300], ['0', 600], ['1', 900], ['Enter', 1200]]);
    expect(scanned).toBeUndefined();
  });

  it('Enter alone → no scan', () => {
    const { scanned } = run([['Enter', 0]]);
    expect(scanned).toBeUndefined();
  });

  it('too short fast burst + Enter → no scan (below minLength)', () => {
    const { scanned } = run([['1', 0], ['2', 10], ['Enter', 20]]);
    expect(scanned).toBeUndefined();
  });

  it('captures fast keystrokes so they do not leak into focused fields', () => {
    const { captures } = run([['4', 0], ['6', 10], ['0', 20], ['1', 30], ['Enter', 40]]);
    expect(captures).toBeGreaterThan(0);
  });

  it('ignores modifier/navigation keys, keeps digit buffer', () => {
    const { scanned } = run([['4', 0], ['Shift', 10], ['6', 20], ['0', 30], ['1', 40], ['Enter', 50]]);
    expect(scanned).toBe('4601');
  });
});
```

- [ ] **Step 2: Прогнать — падает**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/barcodeScanner.test.ts`
Expected: FAIL (`feedScanner` не существует).

- [ ] **Step 3: Реализовать редьюсер**

`barcodeScanner.ts`:
```ts
export interface ScannerState { buffer: string; lastKeyMs: number }
export const EMPTY_SCANNER: ScannerState = { buffer: '', lastKeyMs: 0 };

export interface ScannerOptions { minLength?: number; maxInterKeyMs?: number }
export interface ScannerStep { state: ScannerState; scanned?: string; capture: boolean }

export const MIN_CODE_LENGTH = 3;
export const MAX_INTER_KEY_MS = 50;

const IGNORED = new Set(['Shift', 'Control', 'Alt', 'Meta', 'CapsLock', 'Tab', 'ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown']);

// HID-сканер «печатает» символы код очень быстро и завершает Enter'ом.
// Быстрый ввод (gap ≤ maxInterKeyMs) копим и помечаем capture=true (хук сделает preventDefault,
// чтобы цифры не утекли в сфокусированное поле). Медленный ввод человеком — отбрасываем.
export function feedScanner(state: ScannerState, key: string, timeMs: number, opts: ScannerOptions = {}): ScannerStep {
  const minLength = opts.minLength ?? MIN_CODE_LENGTH;
  const maxGap = opts.maxInterKeyMs ?? MAX_INTER_KEY_MS;

  if (key === 'Enter') {
    const code = state.buffer;
    const fastEnough = code.length >= minLength;
    if (fastEnough) return { state: EMPTY_SCANNER, scanned: code, capture: true };
    return { state: EMPTY_SCANNER, capture: false };
  }
  if (IGNORED.has(key)) return { state, capture: false };
  if (key.length !== 1) return { state: EMPTY_SCANNER, capture: false }; // неизвестная спец-клавиша → сброс

  const gap = timeMs - state.lastKeyMs;
  const continuing = state.buffer.length > 0 && gap <= maxGap;
  const buffer = continuing ? state.buffer + key : key;
  // capture только когда уверены, что это сканер: ≥2 быстрых символа подряд
  const capture = continuing;
  return { state: { buffer, lastKeyMs: timeMs }, capture };
}
```

- [ ] **Step 4: Прогнать редьюсер — зелёные**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/barcodeScanner.test.ts`
Expected: PASS (6 тестов).

- [ ] **Step 5: Реализовать хук** (`useHotkeys.ts:29-50` — образец; cleanup обязателен)

`useBarcodeScanner.ts`:
```ts
import { useEffect, useRef } from 'react';
import { feedScanner, EMPTY_SCANNER, type ScannerOptions, type ScannerState } from './barcodeScanner';

export function useBarcodeScanner(enabled: boolean, onScan: (code: string) => void, opts?: ScannerOptions): void {
  const stateRef = useRef<ScannerState>(EMPTY_SCANNER);
  const onScanRef = useRef(onScan);
  onScanRef.current = onScan;

  useEffect(() => {
    if (!enabled) { stateRef.current = EMPTY_SCANNER; return; }
    function handle(e: KeyboardEvent) {
      if (e.ctrlKey || e.metaKey || e.altKey) return;
      const step = feedScanner(stateRef.current, e.key, performance.now(), opts);
      stateRef.current = step.state;
      if (step.capture) e.preventDefault();
      if (step.scanned) onScanRef.current(step.scanned);
    }
    window.addEventListener('keydown', handle, true); // capture-фаза: перехватить до полей
    return () => window.removeEventListener('keydown', handle, true);
  }, [enabled, opts]);
}
```

- [ ] **Step 6: Build (тайпчек хука)**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run build`
Expected: `tsc -b` зелёный + vite build ок.

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/barcodeScanner.ts src/AFK4.Operator.App.Web/src/useBarcodeScanner.ts src/AFK4.Operator.App.Web/src/barcodeScanner.test.ts
git commit -m "feat(operator): useBarcodeScanner — HID-детект (чистый редьюсер + хук)"
```

---

## Task 7: Карточка товара — поле «Порог заказа» + секция «Штрих-коды»

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/settings/ProductBarcodesSection.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/settings/SettingsGoodsSection.tsx` (state `:50-62`, заполнение `:74-85`, разметка `:215-235`, сохранение `:115-159`)
- Modify: `locales/{ru,en,tg}.json` + `packages/i18n/src/messages.test.ts` (whitelist) + regen
- Modify: `src/AFK4.Operator.App.Web/src/styles/22-stock.css` (или `settings`-css) — чипы штрихов
- Test: `src/AFK4.Operator.App.Web/src/settings/ProductBarcodesSection.test.tsx`

**Interfaces:**
- Consumes: `settings`-клиент (Task 5) `getProductBarcodes/addProductBarcode/deleteProductBarcode`; `useBarcodeScanner` (Task 6); `ProductBarcodeDto`.
- Produces: `<ProductBarcodesSection productId backend organizationId canManage />` (рендерит чипы + ручной ввод + кнопку «Отсканировать»).

- [ ] **Step 1: Добавить i18n-ключи** (все три локали — иначе падает `i18nKeysExist.test.ts`)

В `locales/ru.json` (рядом с `op.settings.pos.*`):
```
"op.barcode.section.title": "Штрих-коды",
"op.barcode.empty": "Штрих-коды не привязаны",
"op.barcode.primary": "Основной",
"op.barcode.add": "Добавить",
"op.barcode.manualPlaceholder": "Введите или отсканируйте код",
"op.barcode.scan": "Отсканировать",
"op.barcode.scanning": "Ожидание скана…",
"op.barcode.remove": "Удалить штрих-код",
"op.barcode.duplicate": "Этот штрих-код уже привязан к товару",
"op.barcode.added": "Штрих-код привязан",
"op.settings.pos.reorderThreshold": "Порог заказа",
"op.settings.pos.reorderThresholdHint": "0 — без оповещения о низком остатке",
```
`locales/en.json` — англ. эквиваленты. `locales/tg.json` — **реальный таджикский** (напр. `"op.barcode.section.title": "Штрихкодҳо"`, `"op.barcode.primary": "Асосӣ"`, `"op.barcode.add": "Илова кардан"`, `"op.barcode.scan": "Сканер кардан"`, `"op.settings.pos.reorderThreshold": "Ҳадди фармоиш"`, …). Если какой-то ключ — истинный акроним/заимствование с `tg===ru` (напр. сам токен «EAN»), добавить его в `TG_IDENTICAL_TO_RU_ALLOWED` (`packages/i18n/src/messages.test.ts:24-177`). Затем:
```bash
cd /home/fedya/projects/afk4.net/packages/i18n && /home/fedya/.bun/bin/bun run gen
```

- [ ] **Step 2: Падающий тест секции**

`ProductBarcodesSection.test.tsx` (mock settings-клиента через возвращаемый объект; рендер + add/remove flow):
```tsx
import { describe, it, expect, mock, beforeEach } from 'bun:test';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ProductBarcodesSection } from './ProductBarcodesSection';

// замокать createAuthenticatedOperatorClients так, чтобы settings отдавал управляемые штрихи
// (следуй паттерну WriteOffDialog.test.tsx для мока backend-клиентов)

describe('ProductBarcodesSection', () => {
  it('renders empty state when no barcodes', async () => {
    // ... мок getProductBarcodes → []
    // render(<I18nProvider locale="ru"><ProductBarcodesSection .../></I18nProvider>)
    expect(await screen.findByText('Штрих-коды не привязаны')).toBeInTheDocument();
  });

  it('marks the primary barcode', async () => {
    // мок getProductBarcodes → [{barcodeId:'b1',code:'111',isPrimary:true}]
    expect(await screen.findByText('Основной')).toBeInTheDocument();
  });

  it('adds a barcode via manual input', async () => {
    // мок addProductBarcode; ввод в поле + клик «Добавить» → addProductBarcode вызван с {code:'222'}
  });
});
```
> Реализатору: точный способ мока backend-клиентов скопируй из `stock/WriteOffDialog.test.tsx`. Типизируй моки (build тайпчекает тесты).

- [ ] **Step 3: Прогнать — падает**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/settings/ProductBarcodesSection.test.tsx`
Expected: FAIL (компонент не существует).

- [ ] **Step 4: Реализовать ProductBarcodesSection**

Компонент: грузит штрихи (`getProductBarcodes`), рендерит чипы (`.barcode-chip`, primary помечен `.is-primary` + лейбл `t('op.barcode.primary')`), кнопка удаления на чипе (`deleteProductBarcode` → рефетч), поле ручного ввода + кнопка «Добавить» (`addProductBarcode` → рефетч; дубль → тост `t('op.barcode.duplicate')`), кнопка «Отсканировать» → `useBarcodeScanner(scanningEnabled, code => addCode(code))`. Пустое состояние `t('op.barcode.empty')`. При `!canManage` — только чтение (без ввода/удаления). Контраст: код штриха — `text-primary` mono; лейбл «Основной» — `--accent`.

- [ ] **Step 5: Поле «Порог заказа» в SettingsGoodsSection**

- State: рядом с `:50-56` добавить `const [productReorderThreshold, setProductReorderThreshold] = useState('0');`
- Разметка: в блоке инпутов товара (`:215-235`) добавить number-input с лейблом `t('op.settings.pos.reorderThreshold')` + подсказкой `t('op.settings.pos.reorderThresholdHint')`.
- Заполнение `selectCatalogProduct` (`:74-85`): `setProductReorderThreshold(String(readNumber(product, 'reorderThreshold', 0)))`.
- Сохранение create (`:115-125`) и update (`:149-159`): добавить в тело `reorderThreshold: Number(productReorderThreshold) || 0`.
- Смонтировать `<ProductBarcodesSection productId={selectedProductId} backend={backend} organizationId={...} canManage={canManageInventoryStock} />` под формой при выбранном существующем товаре (productId есть). Для нового (несохранённого) товара — показать подсказку «сохраните товар, затем добавьте штрихи» (штрих нельзя привязать к несуществующему productId).

- [ ] **Step 6: CSS чипов** в `22-stock.css` (или settings-css; образец `.client-chip` в `12-players.css:471-498`):
```css
.barcode-chips { display: flex; flex-wrap: wrap; gap: var(--space-2); }
.barcode-chip {
  display: inline-flex; align-items: center; gap: var(--space-2);
  border: 1px solid var(--border-default); border-radius: var(--radius-md);
  padding: var(--space-1) var(--space-2);
  font-family: var(--font-mono); color: var(--text-primary); background: var(--surface-elevated);
}
.barcode-chip.is-primary { border-color: var(--accent); }
.barcode-chip .primary-label { font-family: "Segoe UI"; font-size: 10px; color: var(--accent); }
```

- [ ] **Step 7: Прогнать тесты + build**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/settings/ && /home/fedya/.bun/bin/bun run build`
Expected: PASS + build зелёный.

- [ ] **Step 8: Commit**

```bash
git add locales/ packages/i18n/src/messages.ts packages/i18n/src/messages.test.ts \
        src/AFK4.Operator.App.Web/src/settings/ProductBarcodesSection.tsx \
        src/AFK4.Operator.App.Web/src/settings/SettingsGoodsSection.tsx \
        src/AFK4.Operator.App.Web/src/styles/22-stock.css \
        src/AFK4.Operator.App.Web/src/settings/ProductBarcodesSection.test.tsx
git commit -m "feat(operator): штрих-коды в карточке товара + поле порога заказа"
```

---

## Task 8: POS «Продажи» — сканер в чек

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/BackendPosWorkspace.tsx` (catalog load `:101-151`, `PosCatalogItem` `:30-38`, `projectPosProduct` `:57-72`, `addProduct` `:221-230`, search input `:334-341`, метрики каталога `:329-332`)
- Modify: `locales/{ru,en,tg}.json` + regen
- Test: `src/AFK4.Operator.App.Web/src/BackendPosWorkspace.test.tsx` (существующий — добавить кейсы; либо новый рядом)

**Interfaces:**
- Consumes: `useBarcodeScanner` (Task 6); `PosProductDto.barcodes` через `readArray` (Task 2/3 наполнили каталог).
- Produces: общий хелпер `findCatalogItemByBarcode(items, code)` (можно вынести в `barcodeScanner.ts` как `matchByBarcode` для переиспользования в Task 9).

- [ ] **Step 1: i18n-ключи** (три локали + regen):
```
"op.pos.scan.active": "Сканер активен",
"op.pos.scan.added": "{name} — в чек",
"op.pos.scan.unknown": "Штрих-код не привязан",
```
tg — реальный таджикский (напр. `"op.pos.scan.active": "Сканер фаъол"`, `"op.pos.scan.unknown": "Штрихкод пайваст нашудааст"`).

- [ ] **Step 2: Падающие тесты** (низкоуровневый dispatch `KeyboardEvent` как `useHotkeys.test.tsx:11-14`)

В тест POS-воркспейса добавить:
```tsx
function scan(code: string) {
  for (const ch of code) window.dispatchEvent(new KeyboardEvent('keydown', { key: ch, bubbles: true }));
  window.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
}
// тест: каталог с товаром {name:'Cola', barcodes:['111']} →
//   scan('111') (символы быстро, в тесте gap≈0) → 'Cola' появляется в корзине
//   scan('999') → тост 'Штрих-код не привязан'
//   scan('111') дважды → quantity 2
```
> Тесты гоняют символы синхронно (gap≈0 < 50ms) → детект срабатывает.

- [ ] **Step 3: Прогнать — падает**, затем реализовать:
- `PosCatalogItem` (`:30-38`) — добавить `productId` (уже есть) и `barcodes: string[]` (читать `readArray(product, 'barcodes')` в `projectPosProduct` `:57-72`).
- Состояние тоста скана + `useBarcodeScanner(true, onScan)` где `onScan(code)` = `matchByBarcode(products, code)` → если найден: `addProduct(item)` + тост `op.pos.scan.added`; иначе тост `op.pos.scan.unknown`.
- **Фикс дедупа корзины:** `addProduct` (`:221-230`) сейчас матчит по `item.name` (`:223`) — заменить на `item.productId` (иначе сканер двух одноимённых товаров склеит). Обновить затронутые существующие тесты.
- Бейдж «Сканер активен» (`op.pos.scan.active`, пульс) рядом с метриками каталога (`:329-332`).
- Поиск-поле (`:334-341`) уже принимает текст; скан идёт мимо поля (capture-фаза хука), поле не трогаем.

- [ ] **Step 4: Прогнать тесты + build**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test $(find src -name '*.test.ts' -o -name '*.test.tsx' | grep -v App.test) && /home/fedya/.bun/bin/bun test src/App.test.tsx && /home/fedya/.bun/bin/bun run build`
Expected: PASS (включая обновлённые dedup-тесты) + build зелёный.

- [ ] **Step 5: Commit**

```bash
git add locales/ packages/i18n/src/messages.ts src/AFK4.Operator.App.Web/src/BackendPosWorkspace.tsx src/AFK4.Operator.App.Web/src/barcodeScanner.ts src/AFK4.Operator.App.Web/src/BackendPosWorkspace.test.tsx
git commit -m "feat(operator): POS — сканер штрихов кладёт товар в чек (+ дедуп по productId)"
```

---

## Task 9: Приёмка — сканер в строки прихода + финальная уборка

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/stock/ReceivingWorkspace.tsx` (шов `:138-151`, `addProduct` `:90-94`, каталог `:51-58`)
- Modify: `src/AFK4.Operator.App.Web/src/stock/receivingModel.ts` (`addOrAccumulate:27-44` уже дедупит по productId — переиспользуем; добавить баркод-lookup если нужно)
- Modify: `locales/{ru,en,tg}.json` + regen (переиспользовать `op.pos.scan.*` где подходит, или `op.stock.receiving.scan*`)
- Test: `src/AFK4.Operator.App.Web/src/stock/ReceivingWorkspace.test.tsx` (существующий — добавить кейсы)

**Interfaces:**
- Consumes: `useBarcodeScanner` (Task 6); `matchByBarcode` (Task 8); `addOrAccumulate` (existing).

- [ ] **Step 1: i18n-ключи** (если новые; три локали + regen):
```
"op.stock.receiving.scanActive": "Сканер активен",
"op.stock.receiving.scanUnknown": "Штрих-код не привязан",
```
tg — реальный таджикский.

- [ ] **Step 2: Падающие тесты** (как Task 8 `scan()` helper):
```tsx
// каталог trackStock-товар {name:'Cola', barcodes:['111']} →
//   scan('111') → строка прихода 'Cola' добавлена (qty 1)
//   scan('111') снова → qty 2 (addOrAccumulate)
//   scan('999') → тост unknown, строк не прибавилось
```

- [ ] **Step 3: Прогнать — падает**, затем реализовать:
- `useBarcodeScanner(true, onScan)`; `onScan(code)` = `matchByBarcode(trackedCatalog, code)` → найден: `setLines(cur => addOrAccumulate(cur, product))`; иначе тост unknown.
- Бейдж «Сканер активен» в полосе `recv-add` (`:138-151`) вместо/рядом с комментарием-швом `{/* в S3 сюда подключится сканер */}` — заменить комментарий на реальный бейдж.
- Поиск (`:139-151`) оставить — ручной фоллбэк.

- [ ] **Step 4: Финальная уборка/контраст-пасс** (working style #33/#39 — класс, не один случай):
- Проверить, что нигде не осталось S3-плейсхолдеров/мёртвых комментариев про «будущий сканер».
- Сирот-ключей i18n нет (grep неиспользуемых `op.barcode.*`/`op.pos.scan.*`).
- Контраст таблиц/чипов: значения `text-primary`, не `quaternary`.

- [ ] **Step 5: Полный фронт-гейт**

Run:
```bash
cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web
/home/fedya/.bun/bin/bun test $(find src -name '*.test.ts' -o -name '*.test.tsx' | grep -v App.test) && /home/fedya/.bun/bin/bun test src/App.test.tsx
/home/fedya/.bun/bin/bun run build
cd /home/fedya/projects/afk4.net/packages/i18n && /home/fedya/.bun/bin/bun test
```
Expected: всё PASS (включая i18n tg-guard + key-existence) + build зелёный.

- [ ] **Step 6: Commit**

```bash
git add locales/ packages/i18n/src/messages.ts src/AFK4.Operator.App.Web/src/stock/ReceivingWorkspace.tsx src/AFK4.Operator.App.Web/src/stock/ReceivingWorkspace.test.tsx src/AFK4.Operator.App.Web/src/stock/receivingModel.ts
git commit -m "feat(operator): сканер в Приёмке + финальная уборка S3"
```

---

## Финальный гейт слайса (перед PR)

1. **Бэк:** `cd /home/fedya/projects/afk4.net && /home/fedya/.dotnet/dotnet test tests/AFK4.Platform.Api.Tests tests/AFK4.Shared.Contracts.Tests` — зелёные.
2. **Фронт:** оба `bun test`-прогона (не-App + App) + `bun run build` (`tsc -b` тайпчекает тесты) — зелёные.
3. **i18n:** `cd packages/i18n && bun test` — tg-guard (`tg≠ru`) + key-existence зелёные.
4. **Миграция:** `sln` собирается; миграция не пустая.
5. PR → дождаться зелёного CI → авто-мерж (auto-merge authorized).
6. **POST-merge (операционное, отдельно):** миграция блокирует Coolify Staging Deploy → применить по runbook (afk4-env-quirks) ИЛИ явно сообщить пользователю, что staging-деплой ждёт ручного применения. Не часть dev-branch completion.

---

## Self-Review (по спеке §6-8)

- **§6.1 штрих-модель** → Task 1 (entity+миграция), Task 2 (DTO), Task 3 (CRUD), Task 4 (эндпоинты). ✔ Уникальность `(Org,Branch,Code)` ✔. **Отклонение:** серверный by-barcode lookup НЕ строим (Global Constraints — обосновано: клиентский lookup по каталогу). 
- **§6.2 средневзвешенная** → уже в main (S0), не трогаем. ✔
- **§4 карточка товара со штрихами + поле порога** → Task 7. ✔
- **§4 POS-сканер (lookup→чек, +1, unknown-тост, поиск принимает скан)** → Task 8. ✔
- **§4 сканер в приёмке (+1 накопление)** → Task 9. ✔
- **§8 тесты:** guard на `useBarcodeScanner` (накопление, Enter-терминатор, дебаунс) → Task 6 ✔; per-product порог уже в S0; tg-honesty + key-existence → каждый UI-таск ✔; бэк (уникальность, lookup-в-каталоге, IDOR, primary-инвариант) → Task 3/4 ✔; масштаб (длинные имена, юникод, 13+ цифр) → покрыть в тестах Task 7/8.
- **Уборка:** дедуп корзины POS по productId (Task 8) — фикс латентного бага. Поле порога перенесено в карточку (Task 7) — закрывает долг из спеки §3.
- **Type consistency:** `feedScanner`/`EMPTY_SCANNER`/`ScannerStep` едины Task 6↔8↔9; `ProductBarcodeDto` поля (`barcodeId/productId/code/isPrimary`) едины бэк↔контракт↔фронт; `getProductBarcodes/addProductBarcode/deleteProductBarcode` едины Task 5↔7.
- **Open question §9 «кто видит маржу/себестоимость»** — вне скоупа S3 (экономколонки — бэклог эпика), не трогаем.
