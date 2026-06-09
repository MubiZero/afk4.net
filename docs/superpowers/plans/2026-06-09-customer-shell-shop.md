# Shop (Unit F, cycle 1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a seated player order snacks/drinks from the WebView2 React shell, pay from their wallet balance, and have an operator receive and fulfil the order in a live React queue.

**Architecture:** Server (`AFK4.Platform.Api`) is authoritative — new `ShopOrder`/`ShopOrderLine` entities, a `ShopOrderService` that wraps wallet ledger debit + stock movement, player routes `/api/me/shop/*`, operator routes `/api/branches/{id}/shop/*`, and SignalR `DeviceHub` events. The player shell polls order status; the operator gets live SignalR push. Accounting is standalone (own ledger entries + own stock movements), not coupled to `PosSale`.

**Tech Stack:** .NET 10 minimal APIs + EF Core (PostgreSQL), xUnit + `Microsoft.EntityFrameworkCore.InMemory`; Vite + React 19 + TS with `bun test` + happy-dom + Testing Library; SignalR (`@microsoft/signalr`) in the operator web; `@afk4/i18n` for operator strings, raw Russian strings for the kiosk shell.

**Spec:** `docs/superpowers/specs/2026-06-09-customer-shell-unit-f-shop-design.md`

**Branch:** `feature/customer-shell-shop` (already created on top of `feature/customer-shell-webview2-pivot`).

**Conventions discovered (apply throughout):**
- Player-initiated ledger entries & stock movements use `CreatedByStaffUserId = Guid.Empty` (same as `PlayerSelfServiceEndpoints` passing `Guid.Empty`). Operator actions use the real `StaffUserId`.
- Wallet balance = `SUM(LedgerEntry.AmountMinorUnits WHERE AccountType == "wallet")`. Top-up is **positive**; a purchase debit is **negative**; a cancel reversal is **positive**.
- Stock on hand = `SUM(StockMovement.QuantityDelta)` per product. A sale is **negative** `QuantityDelta` (`MovementType = "sale"`); a restore is **positive** (`MovementType = "refund"`).
- The order stores `WalletLedgerEntryId` (the debit entry) so cancellation can write a linked reversal (`ReversesLedgerEntryId`).
- **Seat:** the order stores `SeatId` (Guid, copied from the player's active session). The operator UI resolves the human seat label from its floor-map cache (it already has seat labels); the server does not snapshot a label string. This refines the spec's "SeatLabel" to "SeatId" — cleaner, no stale label.
- `MoneyDto` is the existing type used by `CreateProductRequest` (namespace `AFK4.Shared.Contracts`).
- Migrations: `dotnet ef migrations add <Name> --project src/AFK4.Platform.Api` (output dir `src/AFK4.Platform.Api/Data/Migrations`).
- .NET tests: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~<Name>"`.
- Web tests: from the project dir, `/home/fedya/.bun/bin/bun test <path>`.

---

## File Structure

**Contracts (`src/AFK4.Shared.Contracts/Shop/`)** — new:
- `ShopCatalogItemDto.cs` — a player-orderable product (id, name, sku, price, stockOnHand).
- `ShopOrderLineInput.cs` — `(Guid ProductId, int Quantity)` request line.
- `PlaceShopOrderRequest.cs` — `(IReadOnlyList<ShopOrderLineInput> Lines)`.
- `ShopOrderLineDto.cs`, `ShopOrderDto.cs` — order projection (lines, status, seatId, total, version).
- `ShopOrderStatusNames.cs` — `placed|accepted|delivered|cancelled`.

**Contracts — modified:**
- `Identity/StaffPermissionNames.cs` — add `ManageShopOrders`.
- `Devices/DeviceRealtimeEvents.cs` — add `ShopOrderCreated`, `ShopOrderUpdated`.
- `Pos/CreateProductRequest.cs`, `Pos/UpdateProductRequest.cs` — add `AvailableInShell`.
- `Pos/PosProductDto.cs` — add `AvailableInShell` (so the catalog edit form can read it).

**Server (`src/AFK4.Platform.Api/`)** — new:
- `Data/ShopOrderEntity.cs`, `Data/ShopOrderLineEntity.cs`.
- `Shop/IShopOrderService.cs`, `Shop/EfShopOrderService.cs`, `Shop/ShopOrderActionResult.cs`, `Shop/ShopOrderProjection.cs` (entity→DTO).
- `Shop/IShopOrderNotifier.cs`, `Shop/SignalRShopOrderNotifier.cs`.
- `Endpoints/PlayerShopEndpoints.cs`, `Endpoints/ShopOrderEndpoints.cs` (operator).

**Server — modified:**
- `Data/PlatformDbContext.cs` — DbSets + `OnModelCreating` for both entities + `AvailableInShell` on `PosProductEntity`.
- `Data/PosProductEntity.cs` — add `AvailableInShell`.
- `Inventory/EfInventoryService.cs` — map `AvailableInShell` on create/update + DTO projection.
- `Endpoints/PosEndpoints.cs` — pass `AvailableInShell` through (it already binds the request record, so this is mostly DTO/service).
- `Program.cs` — register `IShopOrderService`, `IShopOrderNotifier`, `app.MapPlayerShopEndpoints()`, `app.MapShopOrderEndpoints()`.
- `Data/Migrations/` — one migration `AddShopOrders`.

**Player shell (`src/AFK4.Player.Shell.Web/src/`)** — new/modified:
- `apiTypes.ts` (+shop DTOs), `shellApi.ts` (+shop methods, +`code` on `ApiError`), `screens/ShopScreen.tsx` (+test), `screens/SelfServiceMenu.tsx` (wire in).

**Operator web (`src/AFK4.Operator.App.Web/src/`)** — new/modified:
- `operatorApiClients.ts` (+shop client+DTOs), `operatorRealtime.ts` + `useOperatorRealtime.ts` (+shop events), `ShopOrdersWorkspace.tsx` (+test), `App.tsx` + `operatorData.ts` (nav), `BackendPosWorkspace.tsx` (+`availableInShell` checkbox), `packages/i18n/src/messages.ts` (+`op.shopOrders.*` + nav key).

---

# Unit S-server

### Task 1: Shop contracts & shared constants

**Files:**
- Create: `src/AFK4.Shared.Contracts/Shop/ShopOrderStatusNames.cs`
- Create: `src/AFK4.Shared.Contracts/Shop/ShopCatalogItemDto.cs`
- Create: `src/AFK4.Shared.Contracts/Shop/ShopOrderLineInput.cs`
- Create: `src/AFK4.Shared.Contracts/Shop/PlaceShopOrderRequest.cs`
- Create: `src/AFK4.Shared.Contracts/Shop/ShopOrderLineDto.cs`
- Create: `src/AFK4.Shared.Contracts/Shop/ShopOrderDto.cs`
- Modify: `src/AFK4.Shared.Contracts/Identity/StaffPermissionNames.cs`
- Modify: `src/AFK4.Shared.Contracts/Devices/DeviceRealtimeEvents.cs`

This task has no behavior to test on its own; it defines shared types the rest depend on. Verify by compiling the contracts project.

- [ ] **Step 1: Create the contract types**

`ShopOrderStatusNames.cs`:
```csharp
namespace AFK4.Shared.Contracts.Shop;

public static class ShopOrderStatusNames
{
    public const string Placed = "placed";
    public const string Accepted = "accepted";
    public const string Delivered = "delivered";
    public const string Cancelled = "cancelled";
}
```

`ShopCatalogItemDto.cs`:
```csharp
namespace AFK4.Shared.Contracts.Shop;

public sealed record ShopCatalogItemDto(
    Guid ProductId,
    string Name,
    string Sku,
    MoneyDto Price,
    int StockOnHand);
```

`ShopOrderLineInput.cs`:
```csharp
namespace AFK4.Shared.Contracts.Shop;

public sealed record ShopOrderLineInput(Guid ProductId, int Quantity);
```

`PlaceShopOrderRequest.cs`:
```csharp
namespace AFK4.Shared.Contracts.Shop;

public sealed record PlaceShopOrderRequest(IReadOnlyList<ShopOrderLineInput> Lines);
```

`ShopOrderLineDto.cs`:
```csharp
namespace AFK4.Shared.Contracts.Shop;

public sealed record ShopOrderLineDto(
    Guid ProductId,
    string Name,
    MoneyDto UnitPrice,
    int Quantity,
    MoneyDto LineTotal);
```

`ShopOrderDto.cs`:
```csharp
namespace AFK4.Shared.Contracts.Shop;

public sealed record ShopOrderDto(
    Guid Id,
    Guid BranchId,
    Guid SeatId,
    Guid PlayerAccountId,
    string PlayerDisplayName,
    string Status,
    MoneyDto Total,
    IReadOnlyList<ShopOrderLineDto> Lines,
    DateTimeOffset PlacedAtUtc,
    DateTimeOffset? AcceptedAtUtc,
    DateTimeOffset? DeliveredAtUtc,
    DateTimeOffset? CancelledAtUtc,
    int Version);
```

- [ ] **Step 2: Add the permission and realtime-event constants**

In `StaffPermissionNames.cs`, add alongside the existing constants:
```csharp
    public const string ManageShopOrders = "shop.orders.manage";
```

In `DeviceRealtimeEvents.cs`, add alongside the existing constants:
```csharp
    public const string ShopOrderCreated = "shopOrderCreated";
    public const string ShopOrderUpdated = "shopOrderUpdated";
```

- [ ] **Step 3: Build the contracts project to verify it compiles**

Run: `dotnet build src/AFK4.Shared.Contracts/AFK4.Shared.Contracts.csproj`
Expected: Build succeeded, 0 errors.

> If `MoneyDto` is not found, confirm its namespace (`grep -rn "record MoneyDto" src/AFK4.Shared.Contracts`) and add the matching `using` to the new files. It is the same type `CreateProductRequest` uses.

- [ ] **Step 4: Commit**

```bash
git add src/AFK4.Shared.Contracts/Shop src/AFK4.Shared.Contracts/Identity/StaffPermissionNames.cs src/AFK4.Shared.Contracts/Devices/DeviceRealtimeEvents.cs
git commit -m "feat(shop): shared contracts, permission, realtime events"
```

---

### Task 2: Entities, AvailableInShell flag, DbContext config + migration

**Files:**
- Create: `src/AFK4.Platform.Api/Data/ShopOrderEntity.cs`
- Create: `src/AFK4.Platform.Api/Data/ShopOrderLineEntity.cs`
- Modify: `src/AFK4.Platform.Api/Data/PosProductEntity.cs`
- Modify: `src/AFK4.Platform.Api/Data/PlatformDbContext.cs`
- Create (generated): `src/AFK4.Platform.Api/Data/Migrations/<timestamp>_AddShopOrders.cs`

- [ ] **Step 1: Create the entities**

`ShopOrderEntity.cs`:
```csharp
namespace AFK4.Platform.Api.Data;

public sealed class ShopOrderEntity
{
    public Guid ShopOrderId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public Guid PlayerAccountId { get; set; }

    public Guid SessionId { get; set; }

    public Guid SeatId { get; set; }

    public string Status { get; set; } = string.Empty;

    public long TotalMinorUnits { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    // The wallet debit entry; a cancellation writes a reversal that points back at it.
    public Guid WalletLedgerEntryId { get; set; }

    public DateTimeOffset PlacedAtUtc { get; set; }

    public DateTimeOffset? AcceptedAtUtc { get; set; }

    public DateTimeOffset? DeliveredAtUtc { get; set; }

    public DateTimeOffset? CancelledAtUtc { get; set; }

    public string? CancelReason { get; set; }

    // Optimistic-concurrency token: bumped on every transition so two operators cannot double-act.
    public int Version { get; set; }
}
```

`ShopOrderLineEntity.cs`:
```csharp
namespace AFK4.Platform.Api.Data;

public sealed class ShopOrderLineEntity
{
    public Guid ShopOrderLineId { get; set; }

    public Guid ShopOrderId { get; set; }

    public Guid ProductId { get; set; }

    public string NameSnapshot { get; set; } = string.Empty;

    public long UnitPriceMinorUnits { get; set; }

    public int Quantity { get; set; }

    public long LineTotalMinorUnits { get; set; }
}
```

- [ ] **Step 2: Add the `AvailableInShell` flag to `PosProductEntity`**

In `src/AFK4.Platform.Api/Data/PosProductEntity.cs`, add after `IsActive`:
```csharp
    /// <summary>True when this product is offered to players in the shell shop (delivery to seat).</summary>
    public bool AvailableInShell { get; set; }
```

- [ ] **Step 3: Register DbSets and configuration in `PlatformDbContext`**

Add the DbSets near the other POS/ledger DbSets:
```csharp
    public DbSet<ShopOrderEntity> ShopOrders => Set<ShopOrderEntity>();

    public DbSet<ShopOrderLineEntity> ShopOrderLines => Set<ShopOrderLineEntity>();
```

Add to `OnModelCreating` (mirroring the `payment_intents` config style):
```csharp
        modelBuilder.Entity<ShopOrderEntity>(entity =>
        {
            entity.ToTable("shop_orders");
            entity.HasKey(order => order.ShopOrderId);
            entity.Property(order => order.Status).HasMaxLength(32).IsRequired();
            entity.Property(order => order.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(order => order.CancelReason).HasMaxLength(240);
            entity.Property(order => order.Version).IsConcurrencyToken();
            entity.HasIndex(order => new { order.BranchId, order.Status });
            entity.HasIndex(order => new { order.PlayerAccountId, order.PlacedAtUtc });
        });

        modelBuilder.Entity<ShopOrderLineEntity>(entity =>
        {
            entity.ToTable("shop_order_lines");
            entity.HasKey(line => line.ShopOrderLineId);
            entity.Property(line => line.NameSnapshot).HasMaxLength(160).IsRequired();
            entity.HasIndex(line => line.ShopOrderId);
        });
```

- [ ] **Step 4: Create the migration**

Run: `dotnet ef migrations add AddShopOrders --project src/AFK4.Platform.Api`
Expected: a new `<timestamp>_AddShopOrders.cs` + `.Designer.cs` under `Data/Migrations/`, creating `shop_orders` and `shop_order_lines` tables and adding `AvailableInShell` to `pos_products`.

> If the `dotnet ef` tool is missing: `dotnet tool restore` (or `dotnet tool install --global dotnet-ef`) first.

- [ ] **Step 5: Build to verify**

Run: `dotnet build src/AFK4.Platform.Api/AFK4.Platform.Api.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Platform.Api/Data
git commit -m "feat(shop): shop order entities, AvailableInShell flag, migration"
```

---

### Task 3: ShopOrder projection + result type

**Files:**
- Create: `src/AFK4.Platform.Api/Shop/ShopOrderProjection.cs`
- Create: `src/AFK4.Platform.Api/Shop/ShopOrderActionResult.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Shop/ShopOrderProjectionTests.cs`

- [ ] **Step 1: Write the failing test**

`tests/AFK4.Platform.Api.Tests/Shop/ShopOrderProjectionTests.cs`:
```csharp
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Shop;
using AFK4.Shared.Contracts.Shop;
using Xunit;

namespace AFK4.Platform.Api.Tests.Shop;

public sealed class ShopOrderProjectionTests
{
    [Fact]
    public void ToDto_MapsOrderAndLines()
    {
        var order = new ShopOrderEntity
        {
            ShopOrderId = Guid.NewGuid(),
            BranchId = Guid.NewGuid(),
            SeatId = Guid.NewGuid(),
            PlayerAccountId = Guid.NewGuid(),
            Status = ShopOrderStatusNames.Placed,
            TotalMinorUnits = 1500,
            CurrencyCode = "TJS",
            PlacedAtUtc = DateTimeOffset.UnixEpoch,
            Version = 1
        };
        var lines = new[]
        {
            new ShopOrderLineEntity
            {
                ShopOrderLineId = Guid.NewGuid(),
                ShopOrderId = order.ShopOrderId,
                ProductId = Guid.NewGuid(),
                NameSnapshot = "Cola",
                UnitPriceMinorUnits = 500,
                Quantity = 3,
                LineTotalMinorUnits = 1500
            }
        };

        var dto = ShopOrderProjection.ToDto(order, lines, playerDisplayName: "Alex");

        Assert.Equal(order.ShopOrderId, dto.Id);
        Assert.Equal("Alex", dto.PlayerDisplayName);
        Assert.Equal(1500, dto.Total.MinorUnits);
        Assert.Equal("TJS", dto.Total.CurrencyCode);
        var line = Assert.Single(dto.Lines);
        Assert.Equal("Cola", line.Name);
        Assert.Equal(3, line.Quantity);
        Assert.Equal(500, line.UnitPrice.MinorUnits);
        Assert.Equal(1500, line.LineTotal.MinorUnits);
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~ShopOrderProjectionTests"`
Expected: FAIL — `ShopOrderProjection` does not exist.

- [ ] **Step 3: Implement the projection and result type**

`src/AFK4.Platform.Api/Shop/ShopOrderProjection.cs`:
```csharp
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts;
using AFK4.Shared.Contracts.Shop;

namespace AFK4.Platform.Api.Shop;

public static class ShopOrderProjection
{
    public static ShopOrderDto ToDto(
        ShopOrderEntity order,
        IReadOnlyCollection<ShopOrderLineEntity> lines,
        string playerDisplayName)
    {
        var lineDtos = lines
            .Select(line => new ShopOrderLineDto(
                line.ProductId,
                line.NameSnapshot,
                new MoneyDto(order.CurrencyCode, line.UnitPriceMinorUnits),
                line.Quantity,
                new MoneyDto(order.CurrencyCode, line.LineTotalMinorUnits)))
            .ToList();

        return new ShopOrderDto(
            order.ShopOrderId,
            order.BranchId,
            order.SeatId,
            order.PlayerAccountId,
            playerDisplayName,
            order.Status,
            new MoneyDto(order.CurrencyCode, order.TotalMinorUnits),
            lineDtos,
            order.PlacedAtUtc,
            order.AcceptedAtUtc,
            order.DeliveredAtUtc,
            order.CancelledAtUtc,
            order.Version);
    }
}
```

`src/AFK4.Platform.Api/Shop/ShopOrderActionResult.cs`:
```csharp
using AFK4.Shared.Contracts.Shop;

namespace AFK4.Platform.Api.Shop;

public sealed record ShopOrderActionResult(
    bool Succeeded,
    bool NotFound,
    bool Conflict,
    string? ErrorCode,
    ShopOrderDto? Order,
    int? CurrentVersion)
{
    public static ShopOrderActionResult Ok(ShopOrderDto order) =>
        new(true, false, false, null, order, null);

    public static ShopOrderActionResult Business(string errorCode) =>
        new(false, false, false, errorCode, null, null);

    public static ShopOrderActionResult Missing() =>
        new(false, true, false, null, null, null);

    public static ShopOrderActionResult VersionConflict(int? currentVersion) =>
        new(false, false, true, "version_conflict", null, currentVersion);
}
```

> `MoneyDto` namespace: if the build reports it missing, adjust the `using` to its real namespace (see Task 1 note).

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~ShopOrderProjectionTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Api/Shop tests/AFK4.Platform.Api.Tests/Shop/ShopOrderProjectionTests.cs
git commit -m "feat(shop): order DTO projection + action result"
```

---

### Task 4: ShopOrder notifier (SignalR)

**Files:**
- Create: `src/AFK4.Platform.Api/Shop/IShopOrderNotifier.cs`
- Create: `src/AFK4.Platform.Api/Shop/SignalRShopOrderNotifier.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Shop/SignalRShopOrderNotifierTests.cs`

This mirrors `SignalRSessionLifecycleNotifier`.

- [ ] **Step 1: Write the failing test**

`tests/AFK4.Platform.Api.Tests/Shop/SignalRShopOrderNotifierTests.cs`:
```csharp
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.Shop;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Shop;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Xunit;

namespace AFK4.Platform.Api.Tests.Shop;

public sealed class SignalRShopOrderNotifierTests
{
    [Fact]
    public async Task NotifyCreated_SendsToBranchGroup()
    {
        var branchId = Guid.NewGuid();
        var order = new ShopOrderDto(
            Guid.NewGuid(), branchId, Guid.NewGuid(), Guid.NewGuid(), "Alex",
            ShopOrderStatusNames.Placed, new MoneyDto("TJS", 1500),
            Array.Empty<ShopOrderLineDto>(), DateTimeOffset.UnixEpoch, null, null, null, 1);

        var clientProxy = new Mock<IClientProxy>();
        var clients = new Mock<IHubClients>();
        clients.Setup(c => c.Group(DeviceHubGroups.Branch(branchId))).Returns(clientProxy.Object);
        var hubContext = new Mock<IHubContext<DeviceHub>>();
        hubContext.SetupGet(h => h.Clients).Returns(clients.Object);

        var notifier = new SignalRShopOrderNotifier(hubContext.Object);
        await notifier.NotifyCreatedAsync(order, CancellationToken.None);

        clientProxy.Verify(p => p.SendCoreAsync(
            DeviceRealtimeEvents.ShopOrderCreated,
            It.Is<object[]>(args => args.Length == 1 && args[0] == order),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

> `SendAsync` is an extension over `SendCoreAsync`; verifying `SendCoreAsync` is the standard way to assert a SignalR send. If `Moq` is not already referenced by the test project, check the `.csproj`; the repo uses Moq in Sessions tests (it ships with the test project). If it is genuinely absent, replace this with a hand-written fake `IHubContext`/`IClientProxy` that records the call.

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~SignalRShopOrderNotifierTests"`
Expected: FAIL — `IShopOrderNotifier`/`SignalRShopOrderNotifier` do not exist.

- [ ] **Step 3: Implement the notifier**

`src/AFK4.Platform.Api/Shop/IShopOrderNotifier.cs`:
```csharp
using AFK4.Shared.Contracts.Shop;

namespace AFK4.Platform.Api.Shop;

public interface IShopOrderNotifier
{
    Task NotifyCreatedAsync(ShopOrderDto order, CancellationToken cancellationToken);

    Task NotifyUpdatedAsync(ShopOrderDto order, CancellationToken cancellationToken);
}
```

`src/AFK4.Platform.Api/Shop/SignalRShopOrderNotifier.cs`:
```csharp
using AFK4.Platform.Api.Devices;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Shop;
using Microsoft.AspNetCore.SignalR;

namespace AFK4.Platform.Api.Shop;

public sealed class SignalRShopOrderNotifier(IHubContext<DeviceHub> hubContext) : IShopOrderNotifier
{
    public Task NotifyCreatedAsync(ShopOrderDto order, CancellationToken cancellationToken) =>
        hubContext.Clients
            .Group(DeviceHubGroups.Branch(order.BranchId))
            .SendAsync(DeviceRealtimeEvents.ShopOrderCreated, order, cancellationToken);

    public Task NotifyUpdatedAsync(ShopOrderDto order, CancellationToken cancellationToken) =>
        hubContext.Clients
            .Group(DeviceHubGroups.Branch(order.BranchId))
            .SendAsync(DeviceRealtimeEvents.ShopOrderUpdated, order, cancellationToken);
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~SignalRShopOrderNotifierTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Api/Shop tests/AFK4.Platform.Api.Tests/Shop/SignalRShopOrderNotifierTests.cs
git commit -m "feat(shop): SignalR order notifier"
```

---

### Task 5: ShopOrderService — place order (catalog, stock, balance, debit)

**Files:**
- Create: `src/AFK4.Platform.Api/Shop/IShopOrderService.cs`
- Create: `src/AFK4.Platform.Api/Shop/EfShopOrderService.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Shop/EfShopOrderServicePlaceTests.cs`

The service uses `PlatformDbContext`, `TimeProvider`, and `IShopOrderNotifier`. Tests use the in-memory provider and seed directly.

- [ ] **Step 1: Write the failing tests**

`tests/AFK4.Platform.Api.Tests/Shop/EfShopOrderServicePlaceTests.cs`:
```csharp
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Shop;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Inventory;
using AFK4.Shared.Contracts.Shop;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AFK4.Platform.Api.Tests.Shop;

public sealed class EfShopOrderServicePlaceTests
{
    private static PlatformDbContext NewDb() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    private static readonly Guid Org = Guid.NewGuid();
    private static readonly Guid Branch = Guid.NewGuid();
    private static readonly Guid Player = Guid.NewGuid();
    private static readonly Guid Seat = Guid.NewGuid();
    private static readonly Guid Session = Guid.NewGuid();

    private static async Task SeedAsync(PlatformDbContext db, Guid productId, long walletMinor, int stock, bool availableInShell = true)
    {
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = Player, OrganizationId = Org, BranchId = Branch,
            DisplayName = "Alex", IsActive = true, CreatedAtUtc = DateTimeOffset.UnixEpoch
        });
        db.Sessions.Add(new SessionEntity
        {
            SessionId = Session, OrganizationId = Org, BranchId = Branch, SeatId = Seat,
            PlayerAccountId = Player, State = "active",
            PlayerKind = "registered", TariffRuleVersionId = "v1", Version = 1
        });
        db.PosProducts.Add(new PosProductEntity
        {
            ProductId = productId, OrganizationId = Org, BranchId = Branch, CategoryId = Guid.NewGuid(),
            Name = "Cola", Sku = "COLA", CurrencyCode = "TJS", PriceMinorUnits = 500,
            TrackStock = true, AllowNegativeStock = false, IsActive = true, AvailableInShell = availableInShell,
            CreatedAtUtc = DateTimeOffset.UnixEpoch
        });
        if (stock != 0)
        {
            db.StockMovements.Add(new StockMovementEntity
            {
                StockMovementId = Guid.NewGuid(), OrganizationId = Org, BranchId = Branch, ProductId = productId,
                MovementType = StockMovementTypeNames.Purchase, QuantityDelta = stock,
                CurrencyCode = "TJS", UnitCostMinorUnits = 0, Reason = "seed",
                CreatedByStaffUserId = Guid.Empty, CreatedAtUtc = DateTimeOffset.UnixEpoch
            });
        }
        if (walletMinor != 0)
        {
            db.LedgerEntries.Add(BillingEntryFactory.Create(
                Org, Branch, Player, null, null, LedgerEntryTypeNames.TopUp, LedgerAccountTypeNames.Wallet,
                walletMinor, 0, "TJS", "seed", "seed", null, Guid.Empty, DateTimeOffset.UnixEpoch));
        }
        await db.SaveChangesAsync();
    }

    private static EfShopOrderService NewService(PlatformDbContext db) =>
        new(db, TimeProvider.System, new NoopShopOrderNotifier());

    [Fact]
    public async Task Place_DebitsWalletDecrementsStockAndCreatesOrder()
    {
        await using var db = NewDb();
        var productId = Guid.NewGuid();
        await SeedAsync(db, productId, walletMinor: 5000, stock: 10);

        var result = await NewService(db).PlaceAsync(
            Player, new[] { new ShopOrderLineInput(productId, 3) }, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(ShopOrderStatusNames.Placed, result.Order!.Status);
        Assert.Equal(1500, result.Order.Total.MinorUnits);
        Assert.Equal(Seat, result.Order.SeatId);

        var wallet = await db.LedgerEntries.Where(e => e.AccountType == LedgerAccountTypeNames.Wallet)
            .SumAsync(e => e.AmountMinorUnits);
        Assert.Equal(3500, wallet); // 5000 - 1500

        var onHand = await db.StockMovements.Where(m => m.ProductId == productId).SumAsync(m => m.QuantityDelta);
        Assert.Equal(7, onHand); // 10 - 3
    }

    [Fact]
    public async Task Place_WithInsufficientFunds_ReturnsBusinessError()
    {
        await using var db = NewDb();
        var productId = Guid.NewGuid();
        await SeedAsync(db, productId, walletMinor: 1000, stock: 10);

        var result = await NewService(db).PlaceAsync(
            Player, new[] { new ShopOrderLineInput(productId, 3) }, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("insufficient_funds", result.ErrorCode);
        Assert.Empty(db.ShopOrders);
    }

    [Fact]
    public async Task Place_WithInsufficientStock_ReturnsBusinessError()
    {
        await using var db = NewDb();
        var productId = Guid.NewGuid();
        await SeedAsync(db, productId, walletMinor: 5000, stock: 2);

        var result = await NewService(db).PlaceAsync(
            Player, new[] { new ShopOrderLineInput(productId, 3) }, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("out_of_stock", result.ErrorCode);
    }

    [Fact]
    public async Task Place_WithUnavailableProduct_ReturnsBusinessError()
    {
        await using var db = NewDb();
        var productId = Guid.NewGuid();
        await SeedAsync(db, productId, walletMinor: 5000, stock: 10, availableInShell: false);

        var result = await NewService(db).PlaceAsync(
            Player, new[] { new ShopOrderLineInput(productId, 1) }, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("product_unavailable", result.ErrorCode);
    }

    [Fact]
    public async Task Place_WithoutActiveSession_ReturnsBusinessError()
    {
        await using var db = NewDb();
        var productId = Guid.NewGuid();
        await SeedAsync(db, productId, walletMinor: 5000, stock: 10);
        var session = await db.Sessions.SingleAsync();
        session.State = "ended";
        await db.SaveChangesAsync();

        var result = await NewService(db).PlaceAsync(
            Player, new[] { new ShopOrderLineInput(productId, 1) }, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("no_active_session", result.ErrorCode);
    }
}

internal sealed class NoopShopOrderNotifier : IShopOrderNotifier
{
    public Task NotifyCreatedAsync(ShopOrderDto order, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task NotifyUpdatedAsync(ShopOrderDto order, CancellationToken cancellationToken) => Task.CompletedTask;
}
```

> If `SessionEntity` requires more non-null fields than seeded above, the in-memory provider will still accept it (it does not enforce `[Required]`); only adjust if a `SaveChanges` throws. Use `"active"` as the literal session state (the codebase stores lowercase state values; `SessionStateNames.Active` is the constant if you prefer).

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~EfShopOrderServicePlaceTests"`
Expected: FAIL — `IShopOrderService`/`EfShopOrderService` do not exist.

- [ ] **Step 3: Implement the service interface and `PlaceAsync`**

`src/AFK4.Platform.Api/Shop/IShopOrderService.cs`:
```csharp
using AFK4.Shared.Contracts.Shop;

namespace AFK4.Platform.Api.Shop;

public interface IShopOrderService
{
    Task<ShopOrderActionResult> PlaceAsync(
        Guid playerAccountId, IReadOnlyList<ShopOrderLineInput> lines, CancellationToken cancellationToken);

    Task<IReadOnlyList<ShopOrderDto>> ListForPlayerAsync(Guid playerAccountId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ShopOrderDto>> ListQueueAsync(Guid branchId, CancellationToken cancellationToken);

    Task<ShopOrderActionResult> AcceptAsync(
        Guid branchId, Guid shopOrderId, Guid staffUserId, int? expectedVersion, CancellationToken cancellationToken);

    Task<ShopOrderActionResult> DeliverAsync(
        Guid branchId, Guid shopOrderId, Guid staffUserId, int? expectedVersion, CancellationToken cancellationToken);

    Task<ShopOrderActionResult> CancelByOperatorAsync(
        Guid branchId, Guid shopOrderId, Guid staffUserId, int? expectedVersion, CancellationToken cancellationToken);

    Task<ShopOrderActionResult> CancelByPlayerAsync(
        Guid playerAccountId, Guid shopOrderId, CancellationToken cancellationToken);
}
```

`src/AFK4.Platform.Api/Shop/EfShopOrderService.cs` (PlaceAsync + helpers; transitions added in Task 6):
```csharp
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Inventory;
using AFK4.Shared.Contracts.Shop;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Shop;

public sealed class EfShopOrderService(
    PlatformDbContext dbContext,
    TimeProvider timeProvider,
    IShopOrderNotifier notifier) : IShopOrderService
{
    public async Task<ShopOrderActionResult> PlaceAsync(
        Guid playerAccountId, IReadOnlyList<ShopOrderLineInput> lines, CancellationToken cancellationToken)
    {
        if (lines.Count == 0 || lines.Any(line => line.Quantity <= 0))
        {
            return ShopOrderActionResult.Business("empty_order");
        }

        var player = await dbContext.PlayerAccounts.AsNoTracking()
            .SingleOrDefaultAsync(p => p.PlayerAccountId == playerAccountId, cancellationToken);
        if (player is null)
        {
            return ShopOrderActionResult.Missing();
        }

        var session = await dbContext.Sessions.AsNoTracking()
            .Where(s => s.PlayerAccountId == playerAccountId && s.State == "active")
            .OrderByDescending(s => s.SessionId)
            .FirstOrDefaultAsync(cancellationToken);
        if (session is null)
        {
            return ShopOrderActionResult.Business("no_active_session");
        }

        var requested = lines
            .GroupBy(line => line.ProductId)
            .ToDictionary(group => group.Key, group => group.Sum(line => line.Quantity));

        var products = await dbContext.PosProducts.AsNoTracking()
            .Where(p => p.BranchId == session.BranchId && requested.Keys.Contains(p.ProductId))
            .ToListAsync(cancellationToken);

        if (products.Count != requested.Count ||
            products.Any(p => !p.IsActive || !p.AvailableInShell))
        {
            return ShopOrderActionResult.Business("product_unavailable");
        }

        var currency = products[0].CurrencyCode;
        if (products.Any(p => p.CurrencyCode != currency))
        {
            return ShopOrderActionResult.Business("mixed_currency");
        }

        // Stock check for tracked products that do not allow negative stock.
        var productIds = requested.Keys.ToList();
        var onHand = await dbContext.StockMovements.AsNoTracking()
            .Where(m => m.BranchId == session.BranchId && productIds.Contains(m.ProductId))
            .GroupBy(m => m.ProductId)
            .Select(g => new { ProductId = g.Key, Quantity = g.Sum(m => m.QuantityDelta) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Quantity, cancellationToken);

        foreach (var product in products.Where(p => p.TrackStock && !p.AllowNegativeStock))
        {
            if (onHand.GetValueOrDefault(product.ProductId) < requested[product.ProductId])
            {
                return ShopOrderActionResult.Business("out_of_stock");
            }
        }

        var total = products.Sum(p => (long)requested[p.ProductId] * p.PriceMinorUnits);

        var wallet = await dbContext.LedgerEntries.AsNoTracking()
            .Where(e => e.PlayerAccountId == playerAccountId && e.AccountType == LedgerAccountTypeNames.Wallet)
            .SumAsync(e => (long?)e.AmountMinorUnits, cancellationToken) ?? 0;
        if (wallet < total)
        {
            return ShopOrderActionResult.Business("insufficient_funds");
        }

        var now = timeProvider.GetUtcNow();
        var orderId = Guid.NewGuid();

        // Wallet debit (negative amount reduces the wallet balance).
        var debit = BillingEntryFactory.Create(
            player.OrganizationId, session.BranchId, playerAccountId, session.SessionId, null,
            LedgerEntryTypeNames.WalletPayment, LedgerAccountTypeNames.Wallet,
            -total, 0, currency, "shop_order", orderId.ToString("D"),
            reversesLedgerEntryId: null, actorStaffUserId: Guid.Empty, createdAtUtc: now);
        dbContext.LedgerEntries.Add(debit);

        foreach (var product in products)
        {
            dbContext.StockMovements.Add(new StockMovementEntity
            {
                StockMovementId = Guid.NewGuid(),
                OrganizationId = player.OrganizationId,
                BranchId = session.BranchId,
                ProductId = product.ProductId,
                MovementType = StockMovementTypeNames.Sale,
                QuantityDelta = -requested[product.ProductId],
                CurrencyCode = currency,
                UnitCostMinorUnits = 0,
                Reason = "shop_order",
                CreatedByStaffUserId = Guid.Empty,
                CreatedAtUtc = now
            });
        }

        var order = new ShopOrderEntity
        {
            ShopOrderId = orderId,
            OrganizationId = player.OrganizationId,
            BranchId = session.BranchId,
            PlayerAccountId = playerAccountId,
            SessionId = session.SessionId,
            SeatId = session.SeatId,
            Status = ShopOrderStatusNames.Placed,
            TotalMinorUnits = total,
            CurrencyCode = currency,
            WalletLedgerEntryId = debit.LedgerEntryId,
            PlacedAtUtc = now,
            Version = 1
        };
        dbContext.ShopOrders.Add(order);

        var lineEntities = products.Select(product => new ShopOrderLineEntity
        {
            ShopOrderLineId = Guid.NewGuid(),
            ShopOrderId = orderId,
            ProductId = product.ProductId,
            NameSnapshot = product.Name,
            UnitPriceMinorUnits = product.PriceMinorUnits,
            Quantity = requested[product.ProductId],
            LineTotalMinorUnits = requested[product.ProductId] * product.PriceMinorUnits
        }).ToList();
        dbContext.ShopOrderLines.AddRange(lineEntities);

        await dbContext.SaveChangesAsync(cancellationToken);

        var dto = ShopOrderProjection.ToDto(order, lineEntities, player.DisplayName);
        await notifier.NotifyCreatedAsync(dto, cancellationToken);
        return ShopOrderActionResult.Ok(dto);
    }

    public Task<IReadOnlyList<ShopOrderDto>> ListForPlayerAsync(Guid playerAccountId, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<ShopOrderDto>> ListQueueAsync(Guid branchId, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<ShopOrderActionResult> AcceptAsync(Guid branchId, Guid shopOrderId, Guid staffUserId, int? expectedVersion, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<ShopOrderActionResult> DeliverAsync(Guid branchId, Guid shopOrderId, Guid staffUserId, int? expectedVersion, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<ShopOrderActionResult> CancelByOperatorAsync(Guid branchId, Guid shopOrderId, Guid staffUserId, int? expectedVersion, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<ShopOrderActionResult> CancelByPlayerAsync(Guid playerAccountId, Guid shopOrderId, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
```

> The `NotImplementedException` stubs are filled in Task 6. They let this task compile and its tests pass without pulling transition logic forward.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~EfShopOrderServicePlaceTests"`
Expected: PASS (all 5).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Api/Shop tests/AFK4.Platform.Api.Tests/Shop/EfShopOrderServicePlaceTests.cs
git commit -m "feat(shop): place order service (debit wallet, decrement stock)"
```

---

### Task 6: ShopOrderService — transitions, cancel/refund, listings

**Files:**
- Modify: `src/AFK4.Platform.Api/Shop/EfShopOrderService.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Shop/EfShopOrderServiceTransitionTests.cs`

- [ ] **Step 1: Write the failing tests**

`tests/AFK4.Platform.Api.Tests/Shop/EfShopOrderServiceTransitionTests.cs`:
```csharp
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Shop;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Inventory;
using AFK4.Shared.Contracts.Shop;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AFK4.Platform.Api.Tests.Shop;

public sealed class EfShopOrderServiceTransitionTests
{
    private static PlatformDbContext NewDb() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    private static readonly Guid Org = Guid.NewGuid();
    private static readonly Guid Branch = Guid.NewGuid();
    private static readonly Guid Player = Guid.NewGuid();
    private static readonly Guid Staff = Guid.NewGuid();
    private static readonly Guid Product = Guid.NewGuid();
    private static readonly Guid Seat = Guid.NewGuid();
    private static readonly Guid Session = Guid.NewGuid();

    private static EfShopOrderService NewService(PlatformDbContext db) =>
        new(db, TimeProvider.System, new NoopShopOrderNotifier());

    private static async Task<ShopOrderDto> SeedPlacedOrderAsync(PlatformDbContext db)
    {
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = Player, OrganizationId = Org, BranchId = Branch,
            DisplayName = "Alex", IsActive = true, CreatedAtUtc = DateTimeOffset.UnixEpoch
        });
        db.Sessions.Add(new SessionEntity
        {
            SessionId = Session, OrganizationId = Org, BranchId = Branch, SeatId = Seat,
            PlayerAccountId = Player, State = "active",
            PlayerKind = "registered", TariffRuleVersionId = "v1", Version = 1
        });
        db.PosProducts.Add(new PosProductEntity
        {
            ProductId = Product, OrganizationId = Org, BranchId = Branch, CategoryId = Guid.NewGuid(),
            Name = "Cola", Sku = "COLA", CurrencyCode = "TJS", PriceMinorUnits = 500,
            TrackStock = true, AllowNegativeStock = false, IsActive = true, AvailableInShell = true,
            CreatedAtUtc = DateTimeOffset.UnixEpoch
        });
        db.StockMovements.Add(new StockMovementEntity
        {
            StockMovementId = Guid.NewGuid(), OrganizationId = Org, BranchId = Branch, ProductId = Product,
            MovementType = StockMovementTypeNames.Purchase, QuantityDelta = 10,
            CurrencyCode = "TJS", UnitCostMinorUnits = 0, Reason = "seed",
            CreatedByStaffUserId = Guid.Empty, CreatedAtUtc = DateTimeOffset.UnixEpoch
        });
        db.LedgerEntries.Add(BillingEntryFactory.Create(
            Org, Branch, Player, null, null, LedgerEntryTypeNames.TopUp, LedgerAccountTypeNames.Wallet,
            5000, 0, "TJS", "seed", "seed", null, Guid.Empty, DateTimeOffset.UnixEpoch));
        await db.SaveChangesAsync();

        var placed = await NewService(db).PlaceAsync(
            Player, new[] { new ShopOrderLineInput(Product, 3) }, CancellationToken.None);
        return placed.Order!;
    }

    [Fact]
    public async Task Accept_ThenDeliver_AdvancesStatus()
    {
        await using var db = NewDb();
        var order = await SeedPlacedOrderAsync(db);
        var service = NewService(db);

        var accepted = await service.AcceptAsync(Branch, order.Id, Staff, order.Version, CancellationToken.None);
        Assert.True(accepted.Succeeded);
        Assert.Equal(ShopOrderStatusNames.Accepted, accepted.Order!.Status);

        var delivered = await service.DeliverAsync(Branch, order.Id, Staff, accepted.Order.Version, CancellationToken.None);
        Assert.True(delivered.Succeeded);
        Assert.Equal(ShopOrderStatusNames.Delivered, delivered.Order!.Status);
        Assert.NotNull(delivered.Order.DeliveredAtUtc);
    }

    [Fact]
    public async Task Accept_WithStaleVersion_ReturnsConflict()
    {
        await using var db = NewDb();
        var order = await SeedPlacedOrderAsync(db);
        var service = NewService(db);

        var result = await service.AcceptAsync(Branch, order.Id, Staff, expectedVersion: 99, CancellationToken.None);

        Assert.True(result.Conflict);
        Assert.Equal(order.Version, result.CurrentVersion);
    }

    [Fact]
    public async Task Deliver_WhenNotAccepted_ReturnsBusinessError()
    {
        await using var db = NewDb();
        var order = await SeedPlacedOrderAsync(db);

        var result = await NewService(db).DeliverAsync(Branch, order.Id, Staff, order.Version, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("invalid_transition", result.ErrorCode);
    }

    [Fact]
    public async Task CancelByOperator_RefundsWalletAndRestoresStock()
    {
        await using var db = NewDb();
        var order = await SeedPlacedOrderAsync(db);

        var result = await NewService(db).CancelByOperatorAsync(Branch, order.Id, Staff, order.Version, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(ShopOrderStatusNames.Cancelled, result.Order!.Status);

        var wallet = await db.LedgerEntries.Where(e => e.AccountType == LedgerAccountTypeNames.Wallet)
            .SumAsync(e => e.AmountMinorUnits);
        Assert.Equal(5000, wallet); // 5000 - 1500 + 1500 reversal

        var onHand = await db.StockMovements.Where(m => m.ProductId == Product).SumAsync(m => m.QuantityDelta);
        Assert.Equal(10, onHand); // 10 - 3 + 3 restore
    }

    [Fact]
    public async Task CancelByPlayer_AfterAccepted_ReturnsBusinessError()
    {
        await using var db = NewDb();
        var order = await SeedPlacedOrderAsync(db);
        var service = NewService(db);
        await service.AcceptAsync(Branch, order.Id, Staff, order.Version, CancellationToken.None);

        var result = await service.CancelByPlayerAsync(Player, order.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("invalid_transition", result.ErrorCode);
    }

    [Fact]
    public async Task ListForPlayer_And_ListQueue_ReturnOrders()
    {
        await using var db = NewDb();
        var order = await SeedPlacedOrderAsync(db);
        var service = NewService(db);

        var mine = await service.ListForPlayerAsync(Player, CancellationToken.None);
        Assert.Contains(mine, o => o.Id == order.Id);

        var queue = await service.ListQueueAsync(Branch, CancellationToken.None);
        Assert.Contains(queue, o => o.Id == order.Id); // placed is "open"
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~EfShopOrderServiceTransitionTests"`
Expected: FAIL — methods throw `NotImplementedException`.

- [ ] **Step 3: Replace the stubbed methods with real implementations**

In `EfShopOrderService.cs`, replace the six stubbed members with:
```csharp
    public async Task<IReadOnlyList<ShopOrderDto>> ListForPlayerAsync(Guid playerAccountId, CancellationToken cancellationToken)
    {
        var orders = await dbContext.ShopOrders.AsNoTracking()
            .Where(o => o.PlayerAccountId == playerAccountId)
            .OrderByDescending(o => o.PlacedAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken);
        return await ProjectAsync(orders, cancellationToken);
    }

    public async Task<IReadOnlyList<ShopOrderDto>> ListQueueAsync(Guid branchId, CancellationToken cancellationToken)
    {
        var open = new[] { ShopOrderStatusNames.Placed, ShopOrderStatusNames.Accepted };
        var orders = await dbContext.ShopOrders.AsNoTracking()
            .Where(o => o.BranchId == branchId && open.Contains(o.Status))
            .OrderBy(o => o.PlacedAtUtc)
            .ToListAsync(cancellationToken);
        return await ProjectAsync(orders, cancellationToken);
    }

    public Task<ShopOrderActionResult> AcceptAsync(Guid branchId, Guid shopOrderId, Guid staffUserId, int? expectedVersion, CancellationToken cancellationToken) =>
        TransitionAsync(branchId, shopOrderId, expectedVersion, ShopOrderStatusNames.Placed, ShopOrderStatusNames.Accepted, cancellationToken);

    public Task<ShopOrderActionResult> DeliverAsync(Guid branchId, Guid shopOrderId, Guid staffUserId, int? expectedVersion, CancellationToken cancellationToken) =>
        TransitionAsync(branchId, shopOrderId, expectedVersion, ShopOrderStatusNames.Accepted, ShopOrderStatusNames.Delivered, cancellationToken);

    public async Task<ShopOrderActionResult> CancelByOperatorAsync(Guid branchId, Guid shopOrderId, Guid staffUserId, int? expectedVersion, CancellationToken cancellationToken)
    {
        var order = await dbContext.ShopOrders.SingleOrDefaultAsync(o => o.ShopOrderId == shopOrderId && o.BranchId == branchId, cancellationToken);
        if (order is null) return ShopOrderActionResult.Missing();
        if (expectedVersion is { } expected && expected != order.Version) return ShopOrderActionResult.VersionConflict(order.Version);
        if (order.Status is ShopOrderStatusNames.Delivered or ShopOrderStatusNames.Cancelled) return ShopOrderActionResult.Business("invalid_transition");
        return await CancelInternalAsync(order, staffUserId, cancellationToken);
    }

    public async Task<ShopOrderActionResult> CancelByPlayerAsync(Guid playerAccountId, Guid shopOrderId, CancellationToken cancellationToken)
    {
        var order = await dbContext.ShopOrders.SingleOrDefaultAsync(o => o.ShopOrderId == shopOrderId && o.PlayerAccountId == playerAccountId, cancellationToken);
        if (order is null) return ShopOrderActionResult.Missing();
        // Players may only cancel while the order is still "placed" (before the operator accepts it).
        if (order.Status != ShopOrderStatusNames.Placed) return ShopOrderActionResult.Business("invalid_transition");
        return await CancelInternalAsync(order, Guid.Empty, cancellationToken);
    }

    private async Task<ShopOrderActionResult> TransitionAsync(
        Guid branchId, Guid shopOrderId, int? expectedVersion, string fromStatus, string toStatus, CancellationToken cancellationToken)
    {
        var order = await dbContext.ShopOrders.SingleOrDefaultAsync(o => o.ShopOrderId == shopOrderId && o.BranchId == branchId, cancellationToken);
        if (order is null) return ShopOrderActionResult.Missing();
        if (expectedVersion is { } expected && expected != order.Version) return ShopOrderActionResult.VersionConflict(order.Version);
        if (order.Status != fromStatus) return ShopOrderActionResult.Business("invalid_transition");

        var now = timeProvider.GetUtcNow();
        order.Status = toStatus;
        order.Version += 1;
        if (toStatus == ShopOrderStatusNames.Accepted) order.AcceptedAtUtc = now;
        else if (toStatus == ShopOrderStatusNames.Delivered) order.DeliveredAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        var dto = await ProjectSingleAsync(order, cancellationToken);
        await notifier.NotifyUpdatedAsync(dto, cancellationToken);
        return ShopOrderActionResult.Ok(dto);
    }

    private async Task<ShopOrderActionResult> CancelInternalAsync(ShopOrderEntity order, Guid staffUserId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var lines = await dbContext.ShopOrderLines.AsNoTracking()
            .Where(l => l.ShopOrderId == order.ShopOrderId).ToListAsync(cancellationToken);

        // Reverse the wallet debit (positive amount restores balance), linked to the original entry.
        dbContext.LedgerEntries.Add(BillingEntryFactory.Create(
            order.OrganizationId, order.BranchId, order.PlayerAccountId, order.SessionId, null,
            LedgerEntryTypeNames.Reversal, LedgerAccountTypeNames.Wallet,
            order.TotalMinorUnits, 0, order.CurrencyCode, "shop_order_cancel", order.ShopOrderId.ToString("D"),
            reversesLedgerEntryId: order.WalletLedgerEntryId, actorStaffUserId: staffUserId, createdAtUtc: now));

        // Restore stock for every line.
        foreach (var line in lines)
        {
            dbContext.StockMovements.Add(new StockMovementEntity
            {
                StockMovementId = Guid.NewGuid(),
                OrganizationId = order.OrganizationId,
                BranchId = order.BranchId,
                ProductId = line.ProductId,
                MovementType = StockMovementTypeNames.Refund,
                QuantityDelta = line.Quantity,
                CurrencyCode = order.CurrencyCode,
                UnitCostMinorUnits = 0,
                Reason = "shop_order_cancel",
                CreatedByStaffUserId = staffUserId,
                CreatedAtUtc = now
            });
        }

        order.Status = ShopOrderStatusNames.Cancelled;
        order.CancelledAtUtc = now;
        order.Version += 1;
        await dbContext.SaveChangesAsync(cancellationToken);

        var dto = await ProjectSingleAsync(order, cancellationToken);
        await notifier.NotifyUpdatedAsync(dto, cancellationToken);
        return ShopOrderActionResult.Ok(dto);
    }

    private async Task<ShopOrderDto> ProjectSingleAsync(ShopOrderEntity order, CancellationToken cancellationToken)
    {
        var lines = await dbContext.ShopOrderLines.AsNoTracking()
            .Where(l => l.ShopOrderId == order.ShopOrderId).ToListAsync(cancellationToken);
        var name = await dbContext.PlayerAccounts.AsNoTracking()
            .Where(p => p.PlayerAccountId == order.PlayerAccountId).Select(p => p.DisplayName)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;
        return ShopOrderProjection.ToDto(order, lines, name);
    }

    private async Task<IReadOnlyList<ShopOrderDto>> ProjectAsync(
        IReadOnlyList<ShopOrderEntity> orders, CancellationToken cancellationToken)
    {
        if (orders.Count == 0) return Array.Empty<ShopOrderDto>();
        var orderIds = orders.Select(o => o.ShopOrderId).ToList();
        var playerIds = orders.Select(o => o.PlayerAccountId).Distinct().ToList();
        var linesByOrder = (await dbContext.ShopOrderLines.AsNoTracking()
                .Where(l => orderIds.Contains(l.ShopOrderId)).ToListAsync(cancellationToken))
            .GroupBy(l => l.ShopOrderId).ToDictionary(g => g.Key, g => (IReadOnlyCollection<ShopOrderLineEntity>)g.ToList());
        var names = await dbContext.PlayerAccounts.AsNoTracking()
            .Where(p => playerIds.Contains(p.PlayerAccountId))
            .ToDictionaryAsync(p => p.PlayerAccountId, p => p.DisplayName, cancellationToken);

        return orders.Select(o => ShopOrderProjection.ToDto(
            o,
            linesByOrder.GetValueOrDefault(o.ShopOrderId, Array.Empty<ShopOrderLineEntity>()),
            names.GetValueOrDefault(o.PlayerAccountId, string.Empty))).ToList();
    }
```

> Remove the now-replaced `NotImplementedException` stubs. `staffUserId` is intentionally unused in `Accept/Deliver` transitions (audit is written at the endpoint layer); keep the parameter for interface symmetry. If the analyzer warns about the unused parameter, that is acceptable here (the operator endpoints will use it for the audit record).

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~EfShopOrderServiceTransitionTests"`
Expected: PASS (all 6).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Api/Shop tests/AFK4.Platform.Api.Tests/Shop/EfShopOrderServiceTransitionTests.cs
git commit -m "feat(shop): order transitions, cancel/refund, listings"
```

---

### Task 7: AvailableInShell through the POS product create/update path

**Files:**
- Modify: `src/AFK4.Shared.Contracts/Pos/CreateProductRequest.cs`
- Modify: `src/AFK4.Shared.Contracts/Pos/UpdateProductRequest.cs`
- Modify: `src/AFK4.Shared.Contracts/Pos/PosProductDto.cs`
- Modify: `src/AFK4.Platform.Api/Inventory/EfInventoryService.cs`
- Test: `tests/AFK4.Platform.Api.Tests/...` (extend existing POS service/endpoint test, or add `Inventory/InventoryAvailableInShellTests.cs`)

- [ ] **Step 1: Write the failing test**

`tests/AFK4.Platform.Api.Tests/Inventory/InventoryAvailableInShellTests.cs`:
```csharp
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Inventory;
using AFK4.Shared.Contracts;
using AFK4.Shared.Contracts.Pos;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AFK4.Platform.Api.Tests.Inventory;

public sealed class InventoryAvailableInShellTests
{
    [Fact]
    public async Task CreateProduct_PersistsAvailableInShell_AndDtoExposesIt()
    {
        var orgId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        await using var db = new PlatformDbContext(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
        db.PosProductCategories.Add(new PosProductCategoryEntity
        {
            CategoryId = categoryId, OrganizationId = orgId, BranchId = branchId,
            Name = "Drinks", IsActive = true, CreatedAtUtc = DateTimeOffset.UnixEpoch
        });
        await db.SaveChangesAsync();

        var service = new EfInventoryService(db, TimeProvider.System);
        var request = new CreateProductRequest(orgId, categoryId, "Cola", "COLA",
            new MoneyDto("TJS", 500), trackStock: true, allowNegativeStock: false,
            idempotencyKey: Guid.NewGuid().ToString("N")) { AvailableInShell = true };

        var result = await service.CreateProductAsync(branchId, Guid.NewGuid(), request, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.True(result.Response!.AvailableInShell);
        var stored = await db.PosProducts.SingleAsync();
        Assert.True(stored.AvailableInShell);
    }
}
```

> Check `EfInventoryService`'s real constructor signature and `CreateProductAsync` shape before running — match the test to it (the snippet assumes `(PlatformDbContext, TimeProvider)`; if it also takes a low-stock notifier, pass `null`). Adjust the test to compile against the actual signature; the assertions stay the same.

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~InventoryAvailableInShellTests"`
Expected: FAIL — `CreateProductRequest.AvailableInShell` / `PosProductDto.AvailableInShell` do not exist.

- [ ] **Step 3: Add the property to contracts and map it in the service**

In `CreateProductRequest.cs`, add an init-only property (default `false`, so existing callers/tests are unaffected):
```csharp
    public bool AvailableInShell { get; init; }
```

In `UpdateProductRequest.cs`, add a trailing optional parameter:
```csharp
public sealed record UpdateProductRequest(
    Guid OrganizationId,
    Guid CategoryId,
    string Name,
    string Sku,
    MoneyDto Price,
    bool TrackStock,
    bool AllowNegativeStock,
    bool IsActive,
    int ReorderThreshold = 0,
    bool AvailableInShell = false);
```

In `PosProductDto.cs`, add `AvailableInShell` to the record (mirror its existing shape — add `bool AvailableInShell` as a new member; default it in the constructor if the DTO is a positional record so existing construction sites still compile, or add to the `ToDto` projection).

In `EfInventoryService.cs`:
- where a `PosProductEntity` is constructed in `CreateProductAsync`, set `AvailableInShell = request.AvailableInShell`;
- where a product is updated in `UpdateProductAsync`, set `product.AvailableInShell = request.AvailableInShell`;
- in the `ToDto(...)` helper, include `AvailableInShell = product.AvailableInShell` (or the positional equivalent).

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~InventoryAvailableInShellTests"`
Expected: PASS.

- [ ] **Step 5: Run the full POS test class to confirm nothing else broke**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~Pos"`
Expected: PASS (existing POS tests still green).

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Shared.Contracts/Pos src/AFK4.Platform.Api/Inventory tests/AFK4.Platform.Api.Tests/Inventory
git commit -m "feat(shop): AvailableInShell flag through POS product create/update"
```

---

### Task 8: Player shop endpoints

**Files:**
- Create: `src/AFK4.Platform.Api/Endpoints/PlayerShopEndpoints.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Shop/PlayerShopEndpointTests.cs`

The endpoints mirror `PlayerCatalogEndpoints` (player context, rate limit `player-me`). Catalog lists `AvailableInShell && IsActive` products with stock on hand for the player's active-session branch.

- [ ] **Step 1: Write the failing test**

`tests/AFK4.Platform.Api.Tests/Shop/PlayerShopEndpointTests.cs` (use the existing player-auth test helpers — mirror `PlayerCatalogEndpointTests`):
```csharp
using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Shop;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AFK4.Platform.Api.Tests.Shop;

public sealed class PlayerShopEndpointTests
{
    [Fact]
    public async Task GetCatalog_ReturnsOnlyShellAvailableProducts()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var seeded = await ShopTestSeed.SeedActivePlayerWithProductsAsync(factory);
        await PlayerAuthTestHelper.AuthenticateAsync(client, seeded.OrganizationId, seeded.Phone, seeded.Pin);

        var catalog = await client.GetFromJsonAsync<List<ShopCatalogItemDto>>("/api/me/shop/catalog");

        Assert.NotNull(catalog);
        Assert.Single(catalog!); // only the AvailableInShell product
        Assert.Equal("Cola", catalog![0].Name);
        Assert.Equal(10, catalog[0].StockOnHand);
    }

    [Fact]
    public async Task PlaceOrder_DebitsWalletAndReturnsPlaced()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var seeded = await ShopTestSeed.SeedActivePlayerWithProductsAsync(factory);
        await PlayerAuthTestHelper.AuthenticateAsync(client, seeded.OrganizationId, seeded.Phone, seeded.Pin);

        var response = await client.PostAsJsonAsync("/api/me/shop/orders",
            new PlaceShopOrderRequest(new[] { new ShopOrderLineInput(seeded.ColaProductId, 3) }));
        var order = await response.Content.ReadFromJsonAsync<ShopOrderDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ShopOrderStatusNames.Placed, order!.Status);
        Assert.Equal(1500, order.Total.MinorUnits);
    }

    [Fact]
    public async Task PlaceOrder_WithInsufficientFunds_Returns409WithCode()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var seeded = await ShopTestSeed.SeedActivePlayerWithProductsAsync(factory, walletMinor: 1000);
        await PlayerAuthTestHelper.AuthenticateAsync(client, seeded.OrganizationId, seeded.Phone, seeded.Pin);

        var response = await client.PostAsJsonAsync("/api/me/shop/orders",
            new PlaceShopOrderRequest(new[] { new ShopOrderLineInput(seeded.ColaProductId, 3) }));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ShopErrorBody>();
        Assert.Equal("insufficient_funds", body!.Error);
    }

    [Fact]
    public async Task GetCatalog_Unauthenticated_Returns401()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/me/shop/catalog");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed record ShopErrorBody(string Error);
}
```

> This task depends on two small test helpers: `PlayerAuthTestHelper.AuthenticateAsync` (the player sign-in helper used by `PlayerCatalogEndpointTests` — reuse or copy it) and a new `ShopTestSeed` (seed an org/branch/player with credentials, wallet ledger entry, an active session at a seat, one `AvailableInShell` product "Cola" with stock 10, and one non-shell product). Create `tests/AFK4.Platform.Api.Tests/Shop/ShopTestSeed.cs` modelled on the seeding in `PlayerCatalogEndpointTests` (look at how it seeds a player + branch + credential and how it issues a phone/pin). Keep the seeded org id reachable via the sign-in helper.

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~PlayerShopEndpointTests"`
Expected: FAIL — routes return 404 (not mapped yet).

- [ ] **Step 3: Implement the endpoints**

`src/AFK4.Platform.Api/Endpoints/PlayerShopEndpoints.cs`:
```csharp
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Shop;
using AFK4.Shared.Contracts;
using AFK4.Shared.Contracts.Shop;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Endpoints;

internal static class PlayerShopEndpoints
{
    public static void MapPlayerShopEndpoints(this WebApplication app)
    {
        app.MapGet("/api/me/shop/catalog", async (
            IPlayerContextAccessor playerContextAccessor,
            PlatformDbContext db,
            CancellationToken ct) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null) return Results.Unauthorized();

            var session = await db.Sessions.AsNoTracking()
                .Where(s => s.PlayerAccountId == player.PlayerAccountId && s.State == "active")
                .OrderByDescending(s => s.SessionId)
                .FirstOrDefaultAsync(ct);
            if (session is null) return Results.Ok(Array.Empty<ShopCatalogItemDto>());

            var products = await db.PosProducts.AsNoTracking()
                .Where(p => p.BranchId == session.BranchId && p.IsActive && p.AvailableInShell)
                .OrderBy(p => p.Name)
                .ToListAsync(ct);
            var productIds = products.Select(p => p.ProductId).ToList();
            var stock = (await db.StockMovements.AsNoTracking()
                    .Where(m => m.BranchId == session.BranchId && productIds.Contains(m.ProductId))
                    .ToListAsync(ct))
                .GroupBy(m => m.ProductId)
                .ToDictionary(g => g.Key, g => g.Sum(m => m.QuantityDelta));

            var catalog = products
                .Where(p => !p.TrackStock || p.AllowNegativeStock || stock.GetValueOrDefault(p.ProductId) > 0)
                .Select(p => new ShopCatalogItemDto(
                    p.ProductId, p.Name, p.Sku,
                    new MoneyDto(p.CurrencyCode, p.PriceMinorUnits),
                    stock.GetValueOrDefault(p.ProductId)))
                .ToList();
            return Results.Ok(catalog);
        }).RequireRateLimiting("player-me");

        app.MapPost("/api/me/shop/orders", async (
            PlaceShopOrderRequest request,
            IPlayerContextAccessor playerContextAccessor,
            IShopOrderService shopOrderService,
            CancellationToken ct) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null) return Results.Unauthorized();
            var result = await shopOrderService.PlaceAsync(player.PlayerAccountId, request.Lines, ct);
            return ToHttpResult(result);
        }).RequireRateLimiting("player-me");

        app.MapGet("/api/me/shop/orders", async (
            IPlayerContextAccessor playerContextAccessor,
            IShopOrderService shopOrderService,
            CancellationToken ct) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null) return Results.Unauthorized();
            return Results.Ok(await shopOrderService.ListForPlayerAsync(player.PlayerAccountId, ct));
        }).RequireRateLimiting("player-me");

        app.MapPost("/api/me/shop/orders/{orderId:guid}/cancel", async (
            Guid orderId,
            IPlayerContextAccessor playerContextAccessor,
            IShopOrderService shopOrderService,
            CancellationToken ct) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null) return Results.Unauthorized();
            var result = await shopOrderService.CancelByPlayerAsync(player.PlayerAccountId, orderId, ct);
            return ToHttpResult(result);
        }).RequireRateLimiting("player-me");
    }

    private static IResult ToHttpResult(ShopOrderActionResult result)
    {
        if (result.Succeeded) return Results.Ok(result.Order);
        if (result.NotFound) return Results.NotFound();
        if (result.Conflict) return Results.Conflict(new { error = result.ErrorCode, currentVersion = result.CurrentVersion });
        return Results.Conflict(new { error = result.ErrorCode });
    }
}
```

In `Program.cs`, beside `app.MapPlayerCatalogEndpoints();`:
```csharp
app.MapPlayerShopEndpoints();
```

And register the services (near other scoped registrations such as `ISessionLifecycleNotifier`):
```csharp
builder.Services.AddScoped<IShopOrderService, EfShopOrderService>();
builder.Services.AddScoped<IShopOrderNotifier, SignalRShopOrderNotifier>();
```

> Confirm the `IPlayerContextAccessor` namespace (`AFK4.Platform.Api.Identity`) and the player-context property names (`PlayerAccountId`, `OrganizationId`) against `PlayerCatalogEndpoints.cs`; match them exactly.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~PlayerShopEndpointTests"`
Expected: PASS (all 4).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Api/Endpoints/PlayerShopEndpoints.cs src/AFK4.Platform.Api/Program.cs tests/AFK4.Platform.Api.Tests/Shop
git commit -m "feat(shop): player shop endpoints (catalog, place, list, cancel)"
```

---

### Task 9: Operator shop-queue endpoints

**Files:**
- Create: `src/AFK4.Platform.Api/Endpoints/ShopOrderEndpoints.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Shop/ShopOrderEndpointTests.cs`

Operator endpoints follow the staff pattern: `StaffAuthorizationService.RequireBranchPermissionAsync(branchId, StaffPermissionNames.ManageShopOrders, ct)` + `IAuditRecordWriter`. Use the shared `WriteAuditAsync` helper.

- [ ] **Step 1: Write the failing test**

`tests/AFK4.Platform.Api.Tests/Shop/ShopOrderEndpointTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Shop;
using Xunit;

namespace AFK4.Platform.Api.Tests.Shop;

public sealed class ShopOrderEndpointTests
{
    [Fact]
    public async Task Queue_Accept_Deliver_Flow_WithPermission()
    {
        await using var factory = new PlatformApiFactory();
        using var staffClient = factory.CreateClient();
        using var playerClient = factory.CreateClient();

        // Seed a placed order via the player API.
        var seeded = await ShopTestSeed.SeedActivePlayerWithProductsAsync(factory);
        await PlayerAuthTestHelper.AuthenticateAsync(playerClient, seeded.OrganizationId, seeded.Phone, seeded.Pin);
        var placed = await (await playerClient.PostAsJsonAsync("/api/me/shop/orders",
            new PlaceShopOrderRequest(new[] { new ShopOrderLineInput(seeded.ColaProductId, 3) })))
            .Content.ReadFromJsonAsync<ShopOrderDto>();

        // Authorize staff with the shop permission for the seeded branch.
        await ShopTestSeed.AuthorizeStaffForBranchAsync(factory, staffClient, seeded.BranchId, StaffPermissionNames.ManageShopOrders);

        var queue = await staffClient.GetFromJsonAsync<List<ShopOrderDto>>($"/api/branches/{seeded.BranchId:D}/shop/orders");
        Assert.Contains(queue!, o => o.Id == placed!.Id);

        var accept = await staffClient.PostAsJsonAsync(
            $"/api/branches/{seeded.BranchId:D}/shop/orders/{placed!.Id:D}/accept",
            new { expectedVersion = placed.Version });
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        var accepted = await accept.Content.ReadFromJsonAsync<ShopOrderDto>();

        var deliver = await staffClient.PostAsJsonAsync(
            $"/api/branches/{seeded.BranchId:D}/shop/orders/{placed.Id:D}/deliver",
            new { expectedVersion = accepted!.Version });
        Assert.Equal(HttpStatusCode.OK, deliver.StatusCode);
        var delivered = await deliver.Content.ReadFromJsonAsync<ShopOrderDto>();
        Assert.Equal(ShopOrderStatusNames.Delivered, delivered!.Status);
    }

    [Fact]
    public async Task Queue_WithoutPermission_Returns403()
    {
        await using var factory = new PlatformApiFactory();
        using var staffClient = factory.CreateClient();
        var seeded = await ShopTestSeed.SeedActivePlayerWithProductsAsync(factory);
        await ShopTestSeed.AuthorizeStaffForBranchAsync(factory, staffClient, seeded.BranchId, permission: null);

        var response = await staffClient.GetAsync($"/api/branches/{seeded.BranchId:D}/shop/orders");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
```

> `ShopTestSeed.AuthorizeStaffForBranchAsync` should sign a staff user in for the seeded org/branch and grant a role that includes `ManageShopOrders` (when `permission` is non-null) — model it on `StaffAuthTestHelper.AuthorizeAsAsync`, but using the org/branch ids from `ShopTestSeed` so the seeded order's branch matches. For the 403 case, grant a role WITHOUT the permission. Extend `ShopTestSeed` accordingly. The request bodies use an anonymous `{ expectedVersion }` that binds to the request record defined in Step 3.

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~ShopOrderEndpointTests"`
Expected: FAIL — routes not mapped.

- [ ] **Step 3: Implement the endpoints**

`src/AFK4.Platform.Api/Endpoints/ShopOrderEndpoints.cs`:
```csharp
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Shop;
using AFK4.Shared.Contracts.Audit;
using AFK4.Shared.Contracts.Identity;

namespace AFK4.Platform.Api.Endpoints;

public sealed record ShopOrderActionRequest(int? ExpectedVersion);

internal static class ShopOrderEndpoints
{
    public static void MapShopOrderEndpoints(this WebApplication app)
    {
        app.MapGet("/api/branches/{branchId:guid}/shop/orders", async (
            Guid branchId,
            StaffAuthorizationService authorizationService,
            IShopOrderService shopOrderService,
            CancellationToken ct) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId, StaffPermissionNames.ManageShopOrders, ct);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return Results.Ok(await shopOrderService.ListQueueAsync(branchId, ct));
        });

        MapTransition(app, "accept", AuditActionNames.AcceptShopOrder,
            (svc, branchId, orderId, staffUserId, version, ct) => svc.AcceptAsync(branchId, orderId, staffUserId, version, ct));
        MapTransition(app, "deliver", AuditActionNames.DeliverShopOrder,
            (svc, branchId, orderId, staffUserId, version, ct) => svc.DeliverAsync(branchId, orderId, staffUserId, version, ct));
        MapTransition(app, "cancel", AuditActionNames.CancelShopOrder,
            (svc, branchId, orderId, staffUserId, version, ct) => svc.CancelByOperatorAsync(branchId, orderId, staffUserId, version, ct));
    }

    private static void MapTransition(
        WebApplication app, string verb, string auditAction,
        Func<IShopOrderService, Guid, Guid, Guid, int?, CancellationToken, Task<ShopOrderActionResult>> action)
    {
        app.MapPost($"/api/branches/{{branchId:guid}}/shop/orders/{{orderId:guid}}/{verb}", async (
            Guid branchId,
            Guid orderId,
            ShopOrderActionRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IShopOrderService shopOrderService,
            CancellationToken ct) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId, StaffPermissionNames.ManageShopOrders, ct);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var result = await action(
                shopOrderService, branchId, orderId,
                authorization.StaffContext!.StaffUserId, request.ExpectedVersion, ct);

            await EndpointHelpers.WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                auditAction,
                "ShopOrder",
                orderId.ToString("D"),
                result.Succeeded ? AuditOutcome.Succeeded : AuditOutcome.Denied,
                new { result.ErrorCode },
                ct);

            if (result.Succeeded) return Results.Ok(result.Order);
            if (result.NotFound) return Results.NotFound();
            if (result.Conflict) return Results.Conflict(new { error = result.ErrorCode, currentVersion = result.CurrentVersion });
            return Results.Conflict(new { error = result.ErrorCode });
        });
    }
}
```

In `Program.cs`, beside the other endpoint maps:
```csharp
app.MapShopOrderEndpoints();
```

Add audit action constants to `src/AFK4.Shared.Contracts/Audit/AuditActionNames.cs`:
```csharp
    public const string AcceptShopOrder = "shop.order.accept";
    public const string DeliverShopOrder = "shop.order.deliver";
    public const string CancelShopOrder = "shop.order.cancel";
```

> Match `EndpointHelpers.WriteAuditAsync`'s real signature (see `EndpointHelpers.Audit.cs`) — the namespace/class name and parameter order. If the helper is `static` on a different class (e.g. `PosEndpoints` has a local `WriteAuditAsync`), call the shared one or copy the small helper. Verify `authorization.StaffContext` property names (`StaffUserId`, `OrganizationId`) against `StaffEndpoints.cs`.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~ShopOrderEndpointTests"`
Expected: PASS (both).

- [ ] **Step 5: Run the whole shop + POS suite + build**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~Shop|FullyQualifiedName~Pos"`
Expected: PASS. Then `dotnet build src/AFK4.Platform.Api/AFK4.Platform.Api.csproj` → 0 errors.

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Platform.Api/Endpoints/ShopOrderEndpoints.cs src/AFK4.Platform.Api/Program.cs src/AFK4.Shared.Contracts/Audit/AuditActionNames.cs tests/AFK4.Platform.Api.Tests/Shop
git commit -m "feat(shop): operator shop-queue endpoints (accept/deliver/cancel)"
```

---

# Unit S-shell (player WebView2 React)

All commands run from `src/AFK4.Player.Shell.Web`. Use `/home/fedya/.bun/bin/bun` for `bun`.

### Task 10: Shop DTOs + shellApi methods + ApiError code

**Files:**
- Modify: `src/AFK4.Player.Shell.Web/src/apiTypes.ts`
- Modify: `src/AFK4.Player.Shell.Web/src/shellApi.ts`
- Test: `src/AFK4.Player.Shell.Web/src/shellApi.test.ts`

- [ ] **Step 1: Write the failing test**

`src/AFK4.Player.Shell.Web/src/shellApi.test.ts`:
```typescript
import { describe, expect, it } from 'bun:test';
import { ApiError, createShellApi } from './shellApi';

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
}

describe('shellApi shop methods', () => {
  it('listShopCatalog GETs the catalog', async () => {
    let seenUrl = '';
    const api = createShellApi('https://api.test', async (url) => {
      seenUrl = String(url);
      return jsonResponse(200, [{ productId: 'p1', name: 'Cola', sku: 'COLA', price: { currencyCode: 'TJS', minorUnits: 500 }, stockOnHand: 10 }]);
    });
    const catalog = await api.listShopCatalog();
    expect(seenUrl).toBe('https://api.test/api/me/shop/catalog');
    expect(catalog[0].name).toBe('Cola');
  });

  it('placeShopOrder POSTs the lines', async () => {
    let seenBody = '';
    const api = createShellApi('https://api.test', async (_url, init) => {
      seenBody = String(init?.body ?? '');
      return jsonResponse(200, { id: 'o1', status: 'placed' });
    });
    await api.placeShopOrder([{ productId: 'p1', quantity: 2 }]);
    expect(JSON.parse(seenBody)).toEqual({ lines: [{ productId: 'p1', quantity: 2 }] });
  });

  it('surfaces the server error code on 409', async () => {
    const api = createShellApi('https://api.test', async () => jsonResponse(409, { error: 'insufficient_funds' }));
    try {
      await api.placeShopOrder([{ productId: 'p1', quantity: 99 }]);
      throw new Error('should have thrown');
    } catch (e) {
      expect(e).toBeInstanceOf(ApiError);
      expect((e as ApiError).status).toBe(409);
      expect((e as ApiError).code).toBe('insufficient_funds');
    }
  });
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `/home/fedya/.bun/bin/bun test src/shellApi.test.ts`
Expected: FAIL — `listShopCatalog`/`placeShopOrder` undefined; `ApiError` has no `code`.

- [ ] **Step 3: Add DTOs and methods**

Append to `src/AFK4.Player.Shell.Web/src/apiTypes.ts`:
```typescript
export interface ShopCatalogItemDto {
  productId: string; name: string; sku: string; price: MoneyDto; stockOnHand: number;
}

export interface ShopOrderLineDto {
  productId: string; name: string; unitPrice: MoneyDto; quantity: number; lineTotal: MoneyDto;
}

export interface ShopOrderDto {
  id: string; branchId: string; seatId: string; playerAccountId: string; playerDisplayName: string;
  status: string; total: MoneyDto; lines: ShopOrderLineDto[];
  placedAtUtc: string; acceptedAtUtc: string | null; deliveredAtUtc: string | null;
  cancelledAtUtc: string | null; version: number;
}

export interface ShopOrderLineInput { productId: string; quantity: number; }
```

In `src/AFK4.Player.Shell.Web/src/shellApi.ts`, change `ApiError` to carry an optional code, parse the error body, and add the shop methods. Replace the `ApiError` class and the `call` helper's error branch:
```typescript
export class ApiError extends Error {
  constructor(public status: number, message: string, public code?: string) { super(message); this.name = 'ApiError'; }
}
```
```typescript
    if (!response.ok) {
      let code: string | undefined;
      try { code = ((await response.clone().json()) as { error?: string }).error; } catch { /* no json body */ }
      throw new ApiError(response.status, `request to ${path} failed: ${response.status}`, code);
    }
```
Add to the returned object (and update the imports at the top to include the new types):
```typescript
    listShopCatalog: () => call<ShopCatalogItemDto[]>('/api/me/shop/catalog'),
    placeShopOrder: (lines: ShopOrderLineInput[]) =>
      call<ShopOrderDto>('/api/me/shop/orders', { method: 'POST', body: JSON.stringify({ lines }) }),
    listShopOrders: () => call<ShopOrderDto[]>('/api/me/shop/orders'),
    cancelShopOrder: (orderId: string) =>
      call<ShopOrderDto>(`/api/me/shop/orders/${orderId}/cancel`, { method: 'POST' })
```
Update the top import:
```typescript
import type { ExtendSessionRequest, PackageOptionDto, PlayerTopUpIntentDto, ShopCatalogItemDto, ShopOrderDto, ShopOrderLineInput, TariffOptionDto } from './apiTypes';
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `/home/fedya/.bun/bin/bun test src/shellApi.test.ts`
Expected: PASS (3).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Player.Shell.Web/src/apiTypes.ts src/AFK4.Player.Shell.Web/src/shellApi.ts src/AFK4.Player.Shell.Web/src/shellApi.test.ts
git commit -m "feat(shop): shell api shop methods + ApiError code"
```

---

### Task 11: ShopScreen (catalog, cart, place, insufficient→top-up, status poll, cancel)

**Files:**
- Create: `src/AFK4.Player.Shell.Web/src/screens/ShopScreen.tsx`
- Test: `src/AFK4.Player.Shell.Web/src/screens/ShopScreen.test.tsx`

Raw Russian strings (no i18n hook), mirroring `ExtendScreen`/`TopUpScreen`. The screen has two phases: **catalog/cart** (add items, place) and **active order** (status + cancel-while-placed). On `insufficient_funds` it calls `onNeedTopUp`.

- [ ] **Step 1: Write the failing test**

`src/AFK4.Player.Shell.Web/src/screens/ShopScreen.test.tsx`:
```typescript
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'bun:test';
import { ShopScreen } from './ShopScreen';
import { ApiError, type ShellApi } from '../shellApi';

const cola = { productId: 'p1', name: 'Cola', sku: 'COLA', price: { currencyCode: 'TJS', minorUnits: 500 }, stockOnHand: 10 };

function api(over: Partial<ShellApi>): ShellApi {
  return {
    listTariffs: async () => [], listPackages: async () => [], createTopUpIntent: async () => ({} as any),
    getTopUpIntents: async () => [], extendSession: async () => ({}),
    listShopCatalog: async () => [cola], placeShopOrder: async () => ({} as any),
    listShopOrders: async () => [], cancelShopOrder: async () => ({} as any),
    ...over
  } as unknown as ShellApi;
}

describe('ShopScreen', () => {
  it('lists catalog and places an order with the cart contents', async () => {
    let placedLines: unknown;
    render(<ShopScreen api={api({ placeShopOrder: async (lines) => { placedLines = lines; return { id: 'o1', status: 'placed', lines: [], total: { currencyCode: 'TJS', minorUnits: 1000 }, version: 1 } as any; } })}
      onNeedTopUp={() => {}} onDone={() => {}} pollIntervalMs={5} />);

    await waitFor(() => screen.getByText('Cola'));
    fireEvent.click(screen.getByRole('button', { name: /добавить/i }));
    fireEvent.click(screen.getByRole('button', { name: /заказать/i }));
    await waitFor(() => expect(placedLines).toEqual([{ productId: 'p1', quantity: 1 }]));
  });

  it('calls onNeedTopUp when the server says insufficient_funds', async () => {
    let asked = false;
    render(<ShopScreen api={api({ placeShopOrder: async () => { throw new ApiError(409, 'x', 'insufficient_funds'); } })}
      onNeedTopUp={() => { asked = true; }} onDone={() => {}} pollIntervalMs={5} />);
    await waitFor(() => screen.getByText('Cola'));
    fireEvent.click(screen.getByRole('button', { name: /добавить/i }));
    fireEvent.click(screen.getByRole('button', { name: /заказать/i }));
    await waitFor(() => expect(asked).toBe(true));
  });

  it('shows order status after placing and polls for updates', async () => {
    let polls = 0;
    render(<ShopScreen api={api({
      placeShopOrder: async () => ({ id: 'o1', status: 'placed', lines: [], total: { currencyCode: 'TJS', minorUnits: 500 }, version: 1 } as any),
      listShopOrders: async () => { polls++; return [{ id: 'o1', status: polls >= 2 ? 'delivered' : 'placed', lines: [], total: { currencyCode: 'TJS', minorUnits: 500 }, version: polls >= 2 ? 3 : 1 } as any]; }
    })} onNeedTopUp={() => {}} onDone={() => {}} pollIntervalMs={5} />);
    await waitFor(() => screen.getByText('Cola'));
    fireEvent.click(screen.getByRole('button', { name: /добавить/i }));
    fireEvent.click(screen.getByRole('button', { name: /заказать/i }));
    await waitFor(() => expect(screen.getByText(/доставлен/i)).toBeInTheDocument(), { timeout: 2000 });
  });
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `/home/fedya/.bun/bin/bun test src/screens/ShopScreen.test.tsx`
Expected: FAIL — `ShopScreen` does not exist.

- [ ] **Step 3: Implement ShopScreen**

`src/AFK4.Player.Shell.Web/src/screens/ShopScreen.tsx`:
```typescript
import { useEffect, useRef, useState } from 'react';
import type { ShopCatalogItemDto, ShopOrderDto } from '../apiTypes';
import { createCachedLoader, indexedDbStore } from '../idbCache';
import { ApiError, OfflineError, type ShellApi } from '../shellApi';

export interface ShopScreenProps {
  api: ShellApi;
  onNeedTopUp: () => void;
  onDone: () => void;
  pollIntervalMs?: number;
}

function formatTjs(minorUnits: number): string {
  return `${(minorUnits / 100).toFixed(2)} с.`;
}

export function ShopScreen({ api, onNeedTopUp, onDone, pollIntervalMs = 4000 }: ShopScreenProps) {
  const [catalog, setCatalog] = useState<ShopCatalogItemDto[]>([]);
  const [cart, setCart] = useState<Record<string, number>>({});
  const [order, setOrder] = useState<ShopOrderDto | null>(null);
  const [offline, setOffline] = useState(false);
  const [busy, setBusy] = useState(false);
  const placed = useRef(false);

  useEffect(() => {
    const load = createCachedLoader(indexedDbStore(), 'shop-catalog', () => api.listShopCatalog());
    load().then(setCatalog).catch((e) => { if (e instanceof OfflineError) setOffline(true); });
  }, [api]);

  useEffect(() => {
    if (!order || order.status === 'delivered' || order.status === 'cancelled') return;
    const timer = setInterval(async () => {
      try {
        const mine = await api.listShopOrders();
        const found = mine.find((o) => o.id === order.id);
        if (found) setOrder(found);
      } catch { /* keep last known status; offline is transient */ }
    }, pollIntervalMs);
    return () => clearInterval(timer);
  }, [api, order, pollIntervalMs]);

  function add(item: ShopCatalogItemDto) {
    setCart((c) => ({ ...c, [item.productId]: (c[item.productId] ?? 0) + 1 }));
  }

  async function placeOrder() {
    const lines = Object.entries(cart)
      .filter(([, qty]) => qty > 0)
      .map(([productId, quantity]) => ({ productId, quantity }));
    if (lines.length === 0) return;
    setBusy(true);
    placed.current = true;
    try {
      setOrder(await api.placeShopOrder(lines));
      setCart({});
    } catch (e) {
      if (e instanceof ApiError && e.code === 'insufficient_funds') onNeedTopUp();
      else if (e instanceof OfflineError) setOffline(true);
    } finally {
      setBusy(false);
    }
  }

  async function cancel() {
    if (!order) return;
    try { setOrder(await api.cancelShopOrder(order.id)); } catch { /* ignore; status poll reconciles */ }
  }

  if (offline) return <p role="alert">Магазин временно недоступен — обратитесь к оператору</p>;

  if (order) {
    const label = order.status === 'placed' ? 'Заказ принят, готовим'
      : order.status === 'accepted' ? 'Оператор несёт ваш заказ'
      : order.status === 'delivered' ? 'Заказ доставлен'
      : 'Заказ отменён';
    return (
      <section>
        <h1>Ваш заказ</h1>
        <p>{label}</p>
        <p>Сумма: {formatTjs(order.total.minorUnits)}</p>
        {order.status === 'placed' && <button type="button" onClick={cancel}>Отменить заказ</button>}
        {(order.status === 'delivered' || order.status === 'cancelled') &&
          <button type="button" onClick={onDone}>Готово</button>}
      </section>
    );
  }

  const cartCount = Object.values(cart).reduce((sum, qty) => sum + qty, 0);
  return (
    <section>
      <h1>Магазин</h1>
      <ul>
        {catalog.map((item) => (
          <li key={item.productId}>
            <span>{item.name}</span>
            <span>{formatTjs(item.price.minorUnits)}</span>
            <button type="button" onClick={() => add(item)}>Добавить</button>
            {cart[item.productId] ? <span aria-label={`в корзине: ${item.name}`}>×{cart[item.productId]}</span> : null}
          </li>
        ))}
      </ul>
      <button type="button" onClick={placeOrder} disabled={cartCount === 0 || busy}>Заказать</button>
    </section>
  );
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `/home/fedya/.bun/bin/bun test src/screens/ShopScreen.test.tsx`
Expected: PASS (3).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Player.Shell.Web/src/screens/ShopScreen.tsx src/AFK4.Player.Shell.Web/src/screens/ShopScreen.test.tsx
git commit -m "feat(shop): player ShopScreen (catalog, cart, status, cancel)"
```

---

### Task 12: Wire ShopScreen into SelfServiceMenu

**Files:**
- Modify: `src/AFK4.Player.Shell.Web/src/screens/SelfServiceMenu.tsx`
- Test: `src/AFK4.Player.Shell.Web/src/screens/SelfServiceMenu.test.tsx` (create if absent, else extend)

- [ ] **Step 1: Write the failing test**

`src/AFK4.Player.Shell.Web/src/screens/SelfServiceMenu.test.tsx` (add this test; if the file exists, append the `it`):
```typescript
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'bun:test';
import { SelfServiceMenu } from './SelfServiceMenu';
import type { ShellApi } from '../shellApi';

function api(): ShellApi {
  return {
    listTariffs: async () => [], listPackages: async () => [], createTopUpIntent: async () => ({} as any),
    getTopUpIntents: async () => [], extendSession: async () => ({}),
    listShopCatalog: async () => [{ productId: 'p1', name: 'Cola', sku: 'C', price: { currencyCode: 'TJS', minorUnits: 500 }, stockOnHand: 5 }],
    listShopOrders: async () => [], placeShopOrder: async () => ({} as any), cancelShopOrder: async () => ({} as any)
  } as unknown as ShellApi;
}

describe('SelfServiceMenu shop entry', () => {
  it('opens the shop from the menu', async () => {
    render(<SelfServiceMenu authenticated onSignIn={async () => true} api={api()}
      sessionId="s1" branchId="b1" onReloadState={() => {}} />);
    fireEvent.click(screen.getByRole('button', { name: /магазин/i }));
    await waitFor(() => expect(screen.getByText('Cola')).toBeInTheDocument());
  });
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `/home/fedya/.bun/bin/bun test src/screens/SelfServiceMenu.test.tsx`
Expected: FAIL — no "Магазин" button.

- [ ] **Step 3: Wire it in**

In `SelfServiceMenu.tsx`: import `ShopScreen`, extend the `View` union with `'shop'`, add the conditional render, and add the menu button:
```typescript
import { ShopScreen } from './ShopScreen';
```
```typescript
type View = 'menu' | 'extend' | 'topup' | 'shop';
```
```typescript
  if (view === 'shop') {
    return <ShopScreen api={api}
      onNeedTopUp={() => setView('topup')}
      onDone={() => { setView('menu'); onReloadState(); }} />;
  }
```
Add to the menu `<nav>`:
```typescript
      <button type="button" onClick={() => setView('shop')}>Магазин</button>
```

- [ ] **Step 4: Run the test + the full shell suite**

Run: `/home/fedya/.bun/bin/bun test src/screens/SelfServiceMenu.test.tsx`
Expected: PASS.
Run: `/home/fedya/.bun/bin/bun test` then `/home/fedya/.bun/bin/bun run build`
Expected: all tests PASS, build succeeds.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Player.Shell.Web/src/screens/SelfServiceMenu.tsx src/AFK4.Player.Shell.Web/src/screens/SelfServiceMenu.test.tsx
git commit -m "feat(shop): wire ShopScreen into SelfServiceMenu"
```

---

# Unit S-operator (operator WebView2 React)

All commands run from `src/AFK4.Operator.App.Web`.

### Task 13: Operator shop-order API client

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/operatorApiClients.ts`
- Test: `src/AFK4.Operator.App.Web/src/shopOrderClient.test.ts`

- [ ] **Step 1: Write the failing test**

`src/AFK4.Operator.App.Web/src/shopOrderClient.test.ts`:
```typescript
import { describe, expect, it } from 'bun:test';
import { createOperatorApiClients } from './operatorApiClients';
import { PlatformApiClient } from './platformApi';

function clientCapturing(record: (method: string, path: string, body: unknown) => void): PlatformApiClient {
  return new PlatformApiClient({
    baseUrl: 'https://api.test',
    getAccessToken: async () => 'token',
    fetchImpl: async (url, init) => {
      record(init?.method ?? 'GET', new URL(String(url)).pathname, init?.body ? JSON.parse(String(init.body)) : undefined);
      return new Response(JSON.stringify([]), { status: 200, headers: { 'Content-Type': 'application/json' } });
    }
  });
}

describe('shopOrders client', () => {
  it('lists the branch queue', async () => {
    let seen = '';
    const clients = createOperatorApiClients(clientCapturing((_m, path) => { seen = path; }));
    await clients.shopOrders.listQueue('b1');
    expect(seen).toBe('/api/branches/b1/shop/orders');
  });

  it('accepts an order with expectedVersion', async () => {
    let captured: { method: string; path: string; body: unknown } | null = null;
    const clients = createOperatorApiClients(clientCapturing((method, path, body) => { captured = { method, path, body }; }));
    await clients.shopOrders.accept('b1', 'o1', 2);
    expect(captured).toEqual({ method: 'POST', path: '/api/branches/b1/shop/orders/o1/accept', body: { expectedVersion: 2 } });
  });
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `/home/fedya/.bun/bin/bun test src/shopOrderClient.test.ts`
Expected: FAIL — `clients.shopOrders` undefined.

- [ ] **Step 3: Add the client**

In `operatorApiClients.ts`, add the DTO types (near other DTOs) and the client factory, then register it in `createOperatorApiClients`:
```typescript
export interface ShopOrderLineDto { productId: Guid; name: string; unitPrice: MoneyDto; quantity: number; lineTotal: MoneyDto; }
export interface ShopOrderDto {
  id: Guid; branchId: Guid; seatId: Guid; playerAccountId: Guid; playerDisplayName: string;
  status: string; total: MoneyDto; lines: ShopOrderLineDto[];
  placedAtUtc: string; acceptedAtUtc: string | null; deliveredAtUtc: string | null;
  cancelledAtUtc: string | null; version: number;
}

export function createShopOrderClient(api: PlatformApiClient) {
  return {
    listQueue(branchId: Guid): Promise<ShopOrderDto[]> {
      return api.get<ShopOrderDto[]>(`/api/branches/${branchId}/shop/orders`);
    },
    accept(branchId: Guid, orderId: Guid, expectedVersion: number): Promise<ShopOrderDto> {
      return api.post<ShopOrderDto, { expectedVersion: number }>(`/api/branches/${branchId}/shop/orders/${orderId}/accept`, { expectedVersion });
    },
    deliver(branchId: Guid, orderId: Guid, expectedVersion: number): Promise<ShopOrderDto> {
      return api.post<ShopOrderDto, { expectedVersion: number }>(`/api/branches/${branchId}/shop/orders/${orderId}/deliver`, { expectedVersion });
    },
    cancel(branchId: Guid, orderId: Guid, expectedVersion: number): Promise<ShopOrderDto> {
      return api.post<ShopOrderDto, { expectedVersion: number }>(`/api/branches/${branchId}/shop/orders/${orderId}/cancel`, { expectedVersion });
    }
  };
}
```
In `createOperatorApiClients(api)`'s returned object, add:
```typescript
    shopOrders: createShopOrderClient(api),
```

> Confirm `MoneyDto`/`Guid` are already declared in `operatorApiClients.ts` (the explore confirmed they are). If `accept`/`post` generic signatures differ, match the existing `createReservationClient` style exactly.

- [ ] **Step 4: Run the test to verify it passes**

Run: `/home/fedya/.bun/bin/bun test src/shopOrderClient.test.ts`
Expected: PASS (2).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/operatorApiClients.ts src/AFK4.Operator.App.Web/src/shopOrderClient.test.ts
git commit -m "feat(shop): operator shop-order api client"
```

---

### Task 14: Operator realtime — shop order events

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/operatorRealtime.ts`
- Test: `src/AFK4.Operator.App.Web/src/operatorRealtime.test.ts` (create or extend)

Add optional `onShopOrderCreated`/`onShopOrderUpdated` handlers to `createOperatorRealtimeClient`, subscribing to the `shopOrderCreated`/`shopOrderUpdated` events. The `ShopOrdersWorkspace` (Task 15) supplies them.

- [ ] **Step 1: Write the failing test**

`src/AFK4.Operator.App.Web/src/operatorRealtime.test.ts` (append if it exists):
```typescript
import { describe, expect, it } from 'bun:test';
import { createOperatorRealtimeClient, shopOrderCreatedEventName } from './operatorRealtime';

function fakeConnection() {
  const handlers: Record<string, (arg: unknown) => void> = {};
  return {
    state: 'Disconnected',
    on(event: string, handler: (arg: unknown) => void) { handlers[event] = handler; },
    onreconnecting() {}, onreconnected() {}, onclose() {},
    async start() {}, async stop() {},
    emit(event: string, arg: unknown) { handlers[event]?.(arg); }
  };
}

describe('operator realtime shop events', () => {
  it('routes shopOrderCreated to the handler', async () => {
    const conn = fakeConnection();
    let received: unknown;
    createOperatorRealtimeClient({
      baseUrl: 'https://api.test',
      getAccessToken: async () => 't',
      connectionFactory: () => conn as never,
      onDeviceStatusChanged: () => {},
      onShopOrderCreated: (order) => { received = order; }
    });
    conn.emit(shopOrderCreatedEventName, { id: 'o1' });
    expect(received).toEqual({ id: 'o1' });
  });
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `/home/fedya/.bun/bin/bun test src/operatorRealtime.test.ts`
Expected: FAIL — `shopOrderCreatedEventName` / `onShopOrderCreated` do not exist.

- [ ] **Step 3: Add event names + handler wiring**

In `operatorRealtime.ts`, add the event-name constants near the others:
```typescript
export const shopOrderCreatedEventName = 'shopOrderCreated';
export const shopOrderUpdatedEventName = 'shopOrderUpdated';
```
Add to the `OperatorRealtimeOptions` interface:
```typescript
  onShopOrderCreated?: (order: ShopOrderDto) => void;
  onShopOrderUpdated?: (order: ShopOrderDto) => void;
```
(import `ShopOrderDto` from `./operatorApiClients`). In `createOperatorRealtimeClient`, after the existing `connection.on(...)` calls:
```typescript
  if (options.onShopOrderCreated) {
    connection.on<ShopOrderDto>(shopOrderCreatedEventName, options.onShopOrderCreated);
  }
  if (options.onShopOrderUpdated) {
    connection.on<ShopOrderDto>(shopOrderUpdatedEventName, options.onShopOrderUpdated);
  }
```

> Match the exact shape of `OperatorRealtimeOptions` and the `connection.on<T>` generic already used for `DeviceStatusChangedDto`. If `onDeviceStatusChanged` is required (non-optional) in the real type, keep passing it in the test as done above.

- [ ] **Step 4: Run the test to verify it passes**

Run: `/home/fedya/.bun/bin/bun test src/operatorRealtime.test.ts`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/operatorRealtime.ts src/AFK4.Operator.App.Web/src/operatorRealtime.test.ts
git commit -m "feat(shop): operator realtime shop-order events"
```

---

### Task 15: ShopOrdersWorkspace + nav + i18n

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/ShopOrdersWorkspace.tsx`
- Test: `src/AFK4.Operator.App.Web/src/ShopOrdersWorkspace.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/App.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/operatorData.ts`
- Modify: `packages/i18n/src/messages.ts`

The workspace loads the queue via `clients.shopOrders.listQueue`, subscribes to realtime updates, and renders accept/deliver/cancel buttons. It resolves the seat label from the floor-map cache if available, else shows the seat id (label resolution is best-effort; the seat id is always present).

- [ ] **Step 1: Add i18n keys**

In `packages/i18n/src/messages.ts`, add keys mirroring the structure of existing `op.shell.nav.*` and `op.pos.*` entries (every locale block the file defines — at minimum ru; mirror into en/tg following the file's existing per-locale layout):
```
'op.shell.nav.shop_orders': 'Заказы',           // ru  (en: 'Orders', tg: mirror)
'op.shopOrders.title': 'Заказы из магазина',
'op.shopOrders.empty': 'Активных заказов нет',
'op.shopOrders.seat': 'Место',
'op.shopOrders.accept': 'Принять',
'op.shopOrders.deliver': 'Выдать',
'op.shopOrders.cancel': 'Отменить',
'op.shopOrders.status.placed': 'Новый',
'op.shopOrders.status.accepted': 'Готовится',
```

> Find where `'op.shell.nav.pos'` is defined (`grep -n "op.shell.nav.pos" packages/i18n/src/messages.ts`) and add the new keys in the same locale objects so the catalog stays consistent. Run `bun test packages/i18n` afterwards if the package has a messages completeness test (it has `messages.test.ts`).

- [ ] **Step 2: Write the failing workspace test**

`src/AFK4.Operator.App.Web/src/ShopOrdersWorkspace.test.tsx` (mirror `PaymentGatewaysWorkspace.test.tsx`'s mocking style):
```typescript
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterAll, afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';

const listQueue = mock(async () => ([
  { id: 'o1', branchId: 'b1', seatId: 's1', playerAccountId: 'pl', playerDisplayName: 'Alex',
    status: 'placed', total: { currencyCode: 'TJS', minorUnits: 1500 }, lines: [],
    placedAtUtc: '', acceptedAtUtc: null, deliveredAtUtc: null, cancelledAtUtc: null, version: 1 }
]));
const accept = mock(async () => ({ id: 'o1', status: 'accepted', version: 2 }));

const actualClients = await import('./operatorApiClients');
mock.module('./operatorApiClients', () => ({
  ...actualClients,
  createAuthenticatedOperatorClients: () => ({ shopOrders: { listQueue, accept, deliver: mock(async () => ({})), cancel: mock(async () => ({})) } })
}));

const realtime = await import('./operatorRealtime');
mock.module('./operatorRealtime', () => ({ ...realtime, createOperatorRealtimeClient: () => ({ async start() {}, async stop() {} }) }));

const { ShopOrdersWorkspace } = await import('./ShopOrdersWorkspace');

afterAll(() => {
  mock.module('./operatorApiClients', () => (globalThis as any).__afk4RealOperatorApiClients);
  mock.module('./operatorRealtime', () => (globalThis as any).__afk4RealOperatorRealtime);
});

const backend = { config: { platformBaseUrl: 'http://test' }, session: { accessToken: 't', organizationId: 'org' }, branchId: 'b1' };

describe('ShopOrdersWorkspace', () => {
  afterEach(() => { cleanup(); mock.restore(); });

  it('lists open orders and accepts one', async () => {
    render(<I18nProvider><ShopOrdersWorkspace backend={backend as never} /></I18nProvider>);
    expect(await screen.findByText('Alex')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /принять|accept/i }));
    await waitFor(() => expect(accept).toHaveBeenCalledWith('b1', 'o1', 1));
  });
});
```

> Confirm the real helper name used by workspaces to build authenticated clients: the explore showed `createAuthenticatedOperatorClients(backend.config, backend.session)`. Mock that exact export. If the project instead exposes `createOperatorApiClients`, mock that and adapt the workspace to whichever the sibling workspaces use (match `DashboardWorkspace`).

- [ ] **Step 3: Run to verify it fails**

Run: `/home/fedya/.bun/bin/bun test src/ShopOrdersWorkspace.test.tsx`
Expected: FAIL — `ShopOrdersWorkspace` does not exist.

- [ ] **Step 4: Implement the workspace**

`src/AFK4.Operator.App.Web/src/ShopOrdersWorkspace.tsx` (mirror `DashboardWorkspace`'s load/effect/state shape; use the project's real `OperatorBackendContext` type and `createAuthenticatedOperatorClients`/`useI18n` imports):
```typescript
import { useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { createAuthenticatedOperatorClients, type ShopOrderDto } from './operatorApiClients';
import { createOperatorRealtimeClient } from './operatorRealtime';
import type { OperatorBackendContext } from './operatorBackend';

export function ShopOrdersWorkspace({ backend }: { backend: OperatorBackendContext | null }) {
  const { t } = useI18n();
  const [orders, setOrders] = useState<ShopOrderDto[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (backend === null) { setOrders([]); return undefined; }
    let disposed = false;
    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);

    const reload = () => clients.shopOrders.listQueue(backend.branchId)
      .then((items) => { if (!disposed) setOrders(items); })
      .catch(() => { if (!disposed) setError(t('op.shopOrders.title')); });
    void reload();

    const upsert = (order: ShopOrderDto) => setOrders((current) => {
      const open = order.status === 'placed' || order.status === 'accepted';
      const without = current.filter((o) => o.id !== order.id);
      return open ? [...without, order].sort((a, b) => a.placedAtUtc.localeCompare(b.placedAtUtc)) : without;
    });

    const realtime = createOperatorRealtimeClient({
      baseUrl: backend.config.platformBaseUrl,
      getAccessToken: () => backend.session.accessToken,
      onDeviceStatusChanged: () => {},
      onShopOrderCreated: (order) => { if (order.branchId === backend.branchId) upsert(order); },
      onShopOrderUpdated: (order) => { if (order.branchId === backend.branchId) upsert(order); }
    });
    void realtime.start();

    return () => { disposed = true; void realtime.stop(); };
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken, t]);

  async function act(order: ShopOrderDto, verb: 'accept' | 'deliver' | 'cancel') {
    if (backend === null) return;
    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    try {
      const updated = await clients.shopOrders[verb](backend.branchId, order.id, order.version);
      setOrders((current) => current
        .map((o) => (o.id === order.id ? updated : o))
        .filter((o) => o.status === 'placed' || o.status === 'accepted'));
    } catch { void 0; /* a 409 means another operator acted; realtime will reconcile */ }
  }

  return (
    <main className="workspace-screen">
      <section className="screen-head"><h1>{t('op.shopOrders.title')}</h1></section>
      {orders.length === 0 && <p>{t('op.shopOrders.empty')}</p>}
      <ul>
        {orders.map((order) => (
          <li key={order.id}>
            <span>{order.playerDisplayName}</span>
            <span>{t('op.shopOrders.seat')}: {order.seatId}</span>
            <span>{(order.total.minorUnits / 100).toFixed(2)}</span>
            <span>{order.status === 'placed' ? t('op.shopOrders.status.placed') : t('op.shopOrders.status.accepted')}</span>
            {order.status === 'placed' && <button type="button" onClick={() => act(order, 'accept')}>{t('op.shopOrders.accept')}</button>}
            {order.status === 'accepted' && <button type="button" onClick={() => act(order, 'deliver')}>{t('op.shopOrders.deliver')}</button>}
            <button type="button" onClick={() => act(order, 'cancel')}>{t('op.shopOrders.cancel')}</button>
          </li>
        ))}
      </ul>
      {error && <p role="alert">{error}</p>}
    </main>
  );
}
```

> Match the real `OperatorBackendContext` import path/name and the `createAuthenticatedOperatorClients` export used by `DashboardWorkspace.tsx` (open it and copy the exact imports). If the project builds clients differently, follow that sibling exactly — the workspace logic stays the same.

- [ ] **Step 5: Register nav + workspace in App.tsx + operatorData.ts**

In `operatorData.ts`, add to `navItems` (import a `ShoppingCart` icon from `lucide-react`):
```typescript
  { labelKey: 'op.shell.nav.shop_orders', icon: ShoppingCart },
```
In `App.tsx`: add `'shop_orders'` to the `WorkspaceId` union / `workspaceIds` array (wherever workspace ids are listed), import `ShopOrdersWorkspace`, and add the conditional render beside the others:
```typescript
{workspace === 'shop_orders' && <ShopOrdersWorkspace backend={backendContext} />}
```

> Keep the order of `navItems` and `workspaceIds` in lockstep (the explore showed they are index-aligned). Place the new entry consistently in both.

- [ ] **Step 6: Run the workspace test + full operator suite + build**

Run: `/home/fedya/.bun/bin/bun test src/ShopOrdersWorkspace.test.tsx`
Expected: PASS.
Run: `/home/fedya/.bun/bin/bun test` then `/home/fedya/.bun/bin/bun run build`
Expected: all tests PASS, build succeeds.

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/ShopOrdersWorkspace.tsx src/AFK4.Operator.App.Web/src/ShopOrdersWorkspace.test.tsx src/AFK4.Operator.App.Web/src/App.tsx src/AFK4.Operator.App.Web/src/operatorData.ts packages/i18n/src/messages.ts
git commit -m "feat(shop): operator shop-orders queue workspace + nav + i18n"
```

---

### Task 16: AvailableInShell checkbox in BackendPosWorkspace

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/operatorApiClients.ts` (product DTO/request types)
- Modify: `src/AFK4.Operator.App.Web/src/BackendPosWorkspace.tsx`
- Test: extend `src/AFK4.Operator.App.Web/src/BackendPosWorkspace.test.tsx` (or create a focused test)

- [ ] **Step 1: Write the failing test**

Add a focused test `src/AFK4.Operator.App.Web/src/posAvailableInShell.test.tsx` that renders the POS product editor, toggles the "в шелле" checkbox, submits, and asserts the create/update product client was called with `availableInShell: true`. Mirror the mocking style of `PaymentGatewaysWorkspace.test.tsx`:
```typescript
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterAll, afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';

const createProduct = mock(async () => ({ productId: 'p1', availableInShell: true }));

const actualClients = await import('./operatorApiClients');
mock.module('./operatorApiClients', () => ({
  ...actualClients,
  createAuthenticatedOperatorClients: () => ({
    inventory: { /* methods used on load */ getCatalog: mock(async () => []), createProduct, createStockMovement: mock(async () => ({})) },
    players: { searchPlayers: mock(async () => []) }
  })
}));

const { BackendPosWorkspace } = await import('./BackendPosWorkspace');

afterAll(() => { mock.module('./operatorApiClients', () => (globalThis as any).__afk4RealOperatorApiClients); });

describe('POS product editor availableInShell', () => {
  afterEach(() => { cleanup(); mock.restore(); });

  it('sends availableInShell when creating a product', async () => {
    render(<I18nProvider><BackendPosWorkspace currencyCode="TJS" backend={{ config: { platformBaseUrl: 'x' }, session: { accessToken: 't', organizationId: 'org' }, branchId: 'b1' } as never} /></I18nProvider>);
    // ... open the product-create form, fill name/sku/price (match the real labels), toggle the checkbox ...
    fireEvent.click(await screen.findByLabelText(/в шелле|shop/i));
    fireEvent.click(screen.getByRole('button', { name: /создать товар|create product/i }));
    await waitFor(() => expect(createProduct).toHaveBeenCalledWith('b1', expect.objectContaining({ availableInShell: true })));
  });
});
```

> This test must match the REAL product-create form in `BackendPosWorkspace.tsx` (field labels, the submit button text/i18n key, and the exact client method name — `createProduct` vs `inventory.createProduct`). Before writing it, read the product-create handler in `BackendPosWorkspace.tsx` and the `inventory` client in `operatorApiClients.ts`, and align the test (mocked methods, labels) to them. The assertion — `availableInShell: true` reaches the client — is the contract.

- [ ] **Step 2: Run to verify it fails**

Run: `/home/fedya/.bun/bin/bun test src/posAvailableInShell.test.tsx`
Expected: FAIL — no checkbox / `availableInShell` not sent.

- [ ] **Step 3: Implement**

- In `operatorApiClients.ts`, add `availableInShell?: boolean` to the product DTO type and to the create/update product request types the `inventory` client uses.
- In `BackendPosWorkspace.tsx`: add `const [availableInShell, setAvailableInShell] = useState(false);`, a checkbox bound to it in the product-create form (label via `t('op.pos.product.availableInShell')` — add that i18n key), and include `availableInShell` in the object passed to the create (and update) product client call. When editing an existing product, initialise the checkbox from the product's `availableInShell`.
- Add the i18n key `'op.pos.product.availableInShell': 'Доступно в шелле'` (+en/tg) to `packages/i18n/src/messages.ts`.

- [ ] **Step 4: Run the test to verify it passes**

Run: `/home/fedya/.bun/bin/bun test src/posAvailableInShell.test.tsx`
Expected: PASS.

- [ ] **Step 5: Run the full operator suite + build**

Run: `/home/fedya/.bun/bin/bun test` then `/home/fedya/.bun/bin/bun run build`
Expected: all tests PASS, build succeeds.

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/operatorApiClients.ts src/AFK4.Operator.App.Web/src/BackendPosWorkspace.tsx src/AFK4.Operator.App.Web/src/posAvailableInShell.test.tsx packages/i18n/src/messages.ts
git commit -m "feat(shop): availableInShell toggle in POS product editor"
```

---

## Final verification (after all tasks)

- [ ] **Server:** `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj` → all green (existing 1120+ plus the new shop tests).
- [ ] **Player shell:** from `src/AFK4.Player.Shell.Web`, `/home/fedya/.bun/bin/bun test` + `/home/fedya/.bun/bin/bun run build` → green.
- [ ] **Operator web:** from `src/AFK4.Operator.App.Web`, `/home/fedya/.bun/bin/bun test` + `/home/fedya/.bun/bin/bun run build` → green.
- [ ] **i18n:** from `packages/i18n`, `/home/fedya/.bun/bin/bun test` → green (messages completeness, if enforced).
- [ ] **Native shell (Windows bridge, optional this cycle):** the shell web `dist` change is picked up by the existing packaging; no native C# changed, so the 33/33 xUnit native suite is unaffected. Verify only if convenient.
- [ ] Dispatch a final code-review subagent over the whole branch, then use `superpowers:finishing-a-development-branch`.

## Deferred (not this cycle)

- Loyalty/cashback and news/banners — separate Unit F cycles (own spec→plan).
- Idempotency-key replay for shop-order placement (a double-tap could create two orders; the shell disables the button while busy as the v1 guard). Add a `PlaceShopOrderRequest.IdempotencyKey` + replay record if duplicates show up in practice.
- Pay-on-delivery / cash, dcgate-per-order.
- Coupling shop orders into `PosSale`/shift Z-reports (standalone by design decision).

---

## Self-Review

**Spec coverage:**
- Payment = wallet debit → Task 5 (`PlaceAsync` debit) + insufficient→top-up in Tasks 8/10/11. ✓
- Catalog = POS + `AvailableInShell` flag → Tasks 2, 7, 8 (catalog endpoint filters), 16 (operator toggle). ✓
- Operator side in scope (React queue + SignalR) → Tasks 4, 9, 13, 14, 15. ✓
- Lifecycle placed→accepted→delivered + cancel with reversal/restore → Tasks 5, 6, 9. ✓
- Standalone accounting (own ledger + stock, not PosSale) → Tasks 5, 6. ✓
- Realtime: player polls, operator push → Task 11 (poll), Tasks 4/14/15 (push). ✓
- Seat from active session → Task 5 (copies `session.SeatId`); refinement (SeatId, not label) documented in conventions. ✓
- Error cases (no session, insufficient funds, out of stock, product unavailable, version conflict, offline) → Tasks 5, 6, 8, 11. ✓
- Tests: xUnit (Tasks 3–9) + bun/happy-dom (Tasks 10–16). ✓

**Placeholder scan:** No "TBD/TODO". Several tasks carry explicit "confirm the real signature/label against file X" notes — these are grounding checks against verbatim-extracted patterns, not missing content; each step still ships complete code. The one genuinely under-specified spot is the i18n catalog edit (Task 15/16): the file is 458 KB with per-locale blocks, so the plan gives the ru strings and instructs mirroring into en/tg next to the existing `op.shell.nav.pos` keys rather than reproducing the whole catalog.

**Type consistency:** `ShopOrderDto`/`ShopOrderLineDto`/`ShopCatalogItemDto`/`ShopOrderLineInput` are defined identically in C# (Task 1), player TS (Task 10), and operator TS (Task 13). `ShopOrderActionResult` (Task 3) is used by the service (5, 6) and both endpoint layers (8, 9). Status strings (`placed/accepted/delivered/cancelled`) come from one constant (Task 1). Error codes (`insufficient_funds`, `out_of_stock`, `product_unavailable`, `no_active_session`, `invalid_transition`, `version_conflict`) are produced in the service (5, 6) and consumed in the shell (11). Ledger sign convention (debit negative / reversal positive) and stock sign (sale negative / refund positive) are consistent between place (5) and cancel (6).
