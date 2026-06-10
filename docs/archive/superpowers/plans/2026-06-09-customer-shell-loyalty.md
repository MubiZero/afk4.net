# Loyalty / Cashback Implementation Plan (Unit F, cycle 2)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give a seated player configurable cashback in real wallet money on wallet top-ups and shop purchases, configured org-wide by the owner, viewable in the shell.

**Architecture:** A new org-level settings entity (`OrganizationLoyaltySettingsEntity`, 1 row per org) drives a small `LoyaltyAccrualService` that *builds* a `cashback` wallet ledger entry. Two existing success-path hooks add that entry atomically: `EfBillingCommandService.TopUpWalletCoreAsync` (top-up) and `EfShopOrderService.TransitionAsync` on `accepted→delivered` (shop). Owner config lives under `/api/owner/loyalty-settings`; the player reads `/api/me/loyalty`. The shell shows rates + earned; the owner web edits the rates. Cashback is plain wallet money — spent like any balance, so there is no redeem flow.

**Tech Stack:** .NET 10 minimal API + EF Core (PostgreSQL) + xUnit/InMemory; React 19 + Vite + TypeScript + `bun test`/happy-dom; `@afk4/i18n`.

**Conventions (read before starting):**
- `bun` is at `/home/fedya/.bun/bin/bun`. Web tests: `cd <web dir> && /home/fedya/.bun/bin/bun test <file>`.
- .NET tests: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter <Name>`.
- No Moq — hand-rolled fakes (see `EfBillingCommandServiceTests`' `FixedTimeProvider`).
- Percent is stored in **basis points** (int; 500 = 5%). Cashback amount = `sourceMinorUnits * bps / 10000` using `long` math (floors for positive operands).
- Multi-tenant entities: sealed class, `Guid` keys, `DateTimeOffset *Utc`, config in `PlatformDbContext.OnModelCreating`, migration named `AddOrganizationLoyaltySettings`.
- No AI signatures in commits.
- Player→branch resolves via `PlayerAccountEntity.HomeBranchId`. The player ledger uses one currency per player.

**File map:**
- Create `src/AFK4.Shared.Contracts/Loyalty/LoyaltySettingsDto.cs`, `UpdateLoyaltySettingsRequest.cs`, `PlayerLoyaltyDto.cs`, `CashbackEntryDto.cs`.
- Modify `src/AFK4.Shared.Contracts/Billing/LedgerEntryTypeNames.cs` (add `Cashback`), `src/AFK4.Shared.Contracts/Identity/StaffPermissionNames.cs` (add `ManageLoyaltySettings`).
- Create `src/AFK4.Platform.Api/Data/OrganizationLoyaltySettingsEntity.cs`; modify `PlatformDbContext.cs` (DbSet + config); add migration.
- Create `src/AFK4.Platform.Api/Loyalty/ILoyaltyAccrualService.cs`, `LoyaltyAccrualService.cs`, `LoyaltyAccrualSource.cs`.
- Modify `src/AFK4.Platform.Api/Billing/EfBillingCommandService.cs` (ctor + top-up hook + `ExecuteLedgerSummaryCommandAsync` extra entries).
- Modify `src/AFK4.Platform.Api/Shop/EfShopOrderService.cs` (ctor + deliver hook).
- Create `src/AFK4.Platform.Api/Endpoints/LoyaltySettingsEndpoints.cs`, `PlayerLoyaltyEndpoints.cs`; modify `Identity/PermissionCatalog.cs`, `Audit/AuditActionNames.cs`, `Program.cs`.
- Create `src/AFK4.Player.Shell.Web/src/screens/LoyaltyScreen.tsx` (+ test); modify `apiTypes.ts`, `shellApi.ts`, `screens/SelfServiceMenu.tsx`.
- Create `src/AFK4.Operator.App.Web/src/LoyaltySettingsWorkspace.tsx` (+ test); modify `operatorApiClients.ts`, nav wiring, `locales/{ru,en,tg}.json`.

---

## Task L1: Shared contracts + constants

**Files:**
- Create: `src/AFK4.Shared.Contracts/Loyalty/LoyaltySettingsDto.cs`
- Create: `src/AFK4.Shared.Contracts/Loyalty/UpdateLoyaltySettingsRequest.cs`
- Create: `src/AFK4.Shared.Contracts/Loyalty/CashbackEntryDto.cs`
- Create: `src/AFK4.Shared.Contracts/Loyalty/PlayerLoyaltyDto.cs`
- Modify: `src/AFK4.Shared.Contracts/Billing/LedgerEntryTypeNames.cs`
- Modify: `src/AFK4.Shared.Contracts/Identity/StaffPermissionNames.cs`

- [ ] **Step 1: Add the contracts.** This is pure declarations — no test of its own (exercised by later tasks). Create the four DTO files:

`LoyaltySettingsDto.cs`:
```csharp
namespace AFK4.Shared.Contracts.Loyalty;

public sealed record LoyaltySettingsDto(
    bool TopUpEnabled,
    int TopUpPercentBasisPoints,
    bool ShopEnabled,
    int ShopPercentBasisPoints);
```

`UpdateLoyaltySettingsRequest.cs`:
```csharp
namespace AFK4.Shared.Contracts.Loyalty;

public sealed record UpdateLoyaltySettingsRequest(
    bool TopUpEnabled,
    int TopUpPercentBasisPoints,
    bool ShopEnabled,
    int ShopPercentBasisPoints);
```

`CashbackEntryDto.cs`:
```csharp
namespace AFK4.Shared.Contracts.Loyalty;

public sealed record CashbackEntryDto(
    long AmountMinorUnits,
    string CurrencyCode,
    string Reason,
    DateTimeOffset CreatedAtUtc);
```

`PlayerLoyaltyDto.cs`:
```csharp
using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Loyalty;

public sealed record PlayerLoyaltyDto(
    bool TopUpEnabled,
    int TopUpPercentBasisPoints,
    bool ShopEnabled,
    int ShopPercentBasisPoints,
    MoneyDto TotalEarned,
    IReadOnlyList<CashbackEntryDto> Recent);
```

- [ ] **Step 2: Add the `Cashback` ledger entry type.** In `src/AFK4.Shared.Contracts/Billing/LedgerEntryTypeNames.cs`, add the constant after `Reversal`:
```csharp
    public const string Reversal = "reversal";
    public const string Cashback = "cashback";
```

- [ ] **Step 3: Add the owner permission.** In `src/AFK4.Shared.Contracts/Identity/StaffPermissionNames.cs`, after `ManageShopOrders`:
```csharp
    public const string ManageShopOrders = "shop.orders.manage";

    // Owner-only: configure org-wide loyalty/cashback rates.
    public const string ManageLoyaltySettings = "loyalty.settings.manage";
```

- [ ] **Step 4: Build.**

Run: `dotnet build src/AFK4.Shared.Contracts/AFK4.Shared.Contracts.csproj`
Expected: Build succeeded.

- [ ] **Step 5: Commit.**
```bash
git add src/AFK4.Shared.Contracts
git commit -m "feat(loyalty): shared contracts, cashback ledger type, owner permission"
```

---

## Task L2: OrganizationLoyaltySettings entity + migration

**Files:**
- Create: `src/AFK4.Platform.Api/Data/OrganizationLoyaltySettingsEntity.cs`
- Modify: `src/AFK4.Platform.Api/Data/PlatformDbContext.cs`
- Create: migration `src/AFK4.Platform.Api/Data/Migrations/*_AddOrganizationLoyaltySettings.cs` (via ef tool)

- [ ] **Step 1: Create the entity.** `OrganizationLoyaltySettingsEntity.cs`:
```csharp
namespace AFK4.Platform.Api.Data;

public sealed class OrganizationLoyaltySettingsEntity
{
    public Guid OrganizationId { get; set; }
    public bool TopUpEnabled { get; set; }
    public int TopUpPercentBasisPoints { get; set; }
    public bool ShopEnabled { get; set; }
    public int ShopPercentBasisPoints { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
```

- [ ] **Step 2: Declare the DbSet.** In `PlatformDbContext.cs`, near the other DbSets (e.g. after `Organizations`):
```csharp
    public DbSet<OrganizationLoyaltySettingsEntity> OrganizationLoyaltySettings => Set<OrganizationLoyaltySettingsEntity>();
```

- [ ] **Step 3: Configure the entity.** In `PlatformDbContext.OnModelCreating`, after the `OrganizationEntity` config block:
```csharp
        modelBuilder.Entity<OrganizationLoyaltySettingsEntity>(entity =>
        {
            entity.ToTable("organization_loyalty_settings");
            entity.HasKey(settings => settings.OrganizationId);
        });
```

- [ ] **Step 4: Generate the migration.**

Run: `dotnet ef migrations add AddOrganizationLoyaltySettings --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj`
Expected: a new `*_AddOrganizationLoyaltySettings.cs` whose `Up` creates `organization_loyalty_settings` with PK `OrganizationId`. Confirm `Up`/`Down` look like:
```csharp
migrationBuilder.CreateTable(
    name: "organization_loyalty_settings",
    columns: table => new
    {
        OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
        TopUpEnabled = table.Column<bool>(type: "boolean", nullable: false),
        TopUpPercentBasisPoints = table.Column<int>(type: "integer", nullable: false),
        ShopEnabled = table.Column<bool>(type: "boolean", nullable: false),
        ShopPercentBasisPoints = table.Column<int>(type: "integer", nullable: false),
        UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
    },
    constraints: table => table.PrimaryKey("PK_organization_loyalty_settings", x => x.OrganizationId));
// Down: migrationBuilder.DropTable(name: "organization_loyalty_settings");
```
If the ef tool is unavailable, hand-write the migration file mirroring `20260609101348_AddShopOrders.cs` plus a matching `PlatformDbContextModelSnapshot` entry.

- [ ] **Step 5: Build + commit.**
```bash
dotnet build src/AFK4.Platform.Api/AFK4.Platform.Api.csproj
git add src/AFK4.Platform.Api/Data
git commit -m "feat(loyalty): organization loyalty settings entity + migration"
```

---

## Task L3: LoyaltyAccrualService (build cashback entry)

**Files:**
- Create: `src/AFK4.Platform.Api/Loyalty/LoyaltyAccrualSource.cs`
- Create: `src/AFK4.Platform.Api/Loyalty/ILoyaltyAccrualService.cs`
- Create: `src/AFK4.Platform.Api/Loyalty/LoyaltyAccrualService.cs`
- Test: `tests/AFK4.Platform.Api.Tests/LoyaltyAccrualServiceTests.cs`

- [ ] **Step 1: Write the failing test.** `LoyaltyAccrualServiceTests.cs`:
```csharp
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Loyalty;
using AFK4.Shared.Contracts.Billing;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

public sealed class LoyaltyAccrualServiceTests
{
    private static readonly Guid OrgId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid BranchId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid PlayerId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-09T10:00:00Z");

    private static PlatformDbContext Db() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    private static async Task SeedSettingsAsync(PlatformDbContext db, bool topUpEnabled, int topUpBps, bool shopEnabled, int shopBps)
    {
        db.OrganizationLoyaltySettings.Add(new OrganizationLoyaltySettingsEntity
        {
            OrganizationId = OrgId, TopUpEnabled = topUpEnabled, TopUpPercentBasisPoints = topUpBps,
            ShopEnabled = shopEnabled, ShopPercentBasisPoints = shopBps, UpdatedAtUtc = Now
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task BuildsFlooredCashbackForEnabledTopUp()
    {
        await using var db = Db();
        await SeedSettingsAsync(db, topUpEnabled: true, topUpBps: 500, shopEnabled: false, shopBps: 0);
        var service = new LoyaltyAccrualService(db);

        var entry = await service.BuildCashbackEntryAsync(
            LoyaltyAccrualSource.TopUp, OrgId, BranchId, PlayerId, sessionId: null,
            sourceMinorUnits: 999, currencyCode: "TJS", reason: "cashback:topup", Now, CancellationToken.None);

        Assert.NotNull(entry);
        Assert.Equal(LedgerEntryTypeNames.Cashback, entry!.EntryType);
        Assert.Equal(LedgerAccountTypeNames.Wallet, entry.AccountType);
        Assert.Equal(49, entry.AmountMinorUnits); // floor(999 * 500 / 10000) = 49
        Assert.Equal("TJS", entry.CurrencyCode);
        Assert.Equal(Guid.Empty, entry.CreatedByStaffUserId);
    }

    [Fact]
    public async Task ReturnsNullWhenSourceDisabled()
    {
        await using var db = Db();
        await SeedSettingsAsync(db, topUpEnabled: false, topUpBps: 500, shopEnabled: true, shopBps: 300);
        var service = new LoyaltyAccrualService(db);

        var entry = await service.BuildCashbackEntryAsync(
            LoyaltyAccrualSource.TopUp, OrgId, BranchId, PlayerId, null, 10000, "TJS", "cashback:topup", Now, CancellationToken.None);

        Assert.Null(entry);
    }

    [Fact]
    public async Task ReturnsNullWhenNoSettingsRow()
    {
        await using var db = Db();
        var service = new LoyaltyAccrualService(db);

        var entry = await service.BuildCashbackEntryAsync(
            LoyaltyAccrualSource.Shop, OrgId, BranchId, PlayerId, null, 10000, "TJS", "cashback:shop", Now, CancellationToken.None);

        Assert.Null(entry);
    }

    [Fact]
    public async Task ReturnsNullWhenComputedCashbackRoundsToZero()
    {
        await using var db = Db();
        await SeedSettingsAsync(db, topUpEnabled: false, topUpBps: 0, shopEnabled: true, shopBps: 100);
        var service = new LoyaltyAccrualService(db);

        var entry = await service.BuildCashbackEntryAsync(
            LoyaltyAccrualSource.Shop, OrgId, BranchId, PlayerId, null, 50, "TJS", "cashback:shop", Now, CancellationToken.None);

        Assert.Null(entry); // floor(50 * 100 / 10000) = 0 -> no entry
    }
}
```

- [ ] **Step 2: Run it — verify it fails to compile** (`LoyaltyAccrualService` not defined).

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter LoyaltyAccrualServiceTests`
Expected: build error (type not found).

- [ ] **Step 3: Implement.** `LoyaltyAccrualSource.cs`:
```csharp
namespace AFK4.Platform.Api.Loyalty;

public enum LoyaltyAccrualSource
{
    TopUp,
    Shop
}
```

`ILoyaltyAccrualService.cs`:
```csharp
using AFK4.Platform.Api.Data;

namespace AFK4.Platform.Api.Loyalty;

public interface ILoyaltyAccrualService
{
    /// <summary>
    /// Builds (does not persist) a cashback wallet ledger entry for a successful source event,
    /// or null if the org has the source disabled, has no settings row, or the cashback rounds to zero.
    /// The caller adds the returned entry to its own unit of work so the credit is atomic with the source.
    /// </summary>
    Task<LedgerEntryEntity?> BuildCashbackEntryAsync(
        LoyaltyAccrualSource source,
        Guid organizationId,
        Guid branchId,
        Guid playerAccountId,
        Guid? sessionId,
        long sourceMinorUnits,
        string currencyCode,
        string reason,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken);
}
```

`LoyaltyAccrualService.cs`:
```csharp
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Loyalty;

public sealed class LoyaltyAccrualService(PlatformDbContext dbContext) : ILoyaltyAccrualService
{
    public async Task<LedgerEntryEntity?> BuildCashbackEntryAsync(
        LoyaltyAccrualSource source,
        Guid organizationId,
        Guid branchId,
        Guid playerAccountId,
        Guid? sessionId,
        long sourceMinorUnits,
        string currencyCode,
        string reason,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken)
    {
        if (sourceMinorUnits <= 0)
        {
            return null;
        }

        var settings = await dbContext.OrganizationLoyaltySettings
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.OrganizationId == organizationId, cancellationToken);
        if (settings is null)
        {
            return null;
        }

        var (enabled, basisPoints) = source switch
        {
            LoyaltyAccrualSource.TopUp => (settings.TopUpEnabled, settings.TopUpPercentBasisPoints),
            LoyaltyAccrualSource.Shop => (settings.ShopEnabled, settings.ShopPercentBasisPoints),
            _ => (false, 0)
        };
        if (!enabled || basisPoints <= 0)
        {
            return null;
        }

        var cashback = sourceMinorUnits * (long)basisPoints / 10000;
        if (cashback <= 0)
        {
            return null;
        }

        return BillingEntryFactory.Create(
            organizationId,
            branchId,
            playerAccountId,
            sessionId,
            playerPackageId: null,
            LedgerEntryTypeNames.Cashback,
            LedgerAccountTypeNames.Wallet,
            cashback,
            quantitySeconds: 0,
            currencyCode,
            description: LedgerEntryTypeNames.Cashback,
            reason,
            reversesLedgerEntryId: null,
            actorStaffUserId: Guid.Empty,
            createdAtUtc);
    }
}
```

- [ ] **Step 4: Run tests — verify pass.**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter LoyaltyAccrualServiceTests`
Expected: 4 passed.

- [ ] **Step 5: Register the service.** In `Program.cs`, near `AddScoped<IBillingCommandService, EfBillingCommandService>()`:
```csharp
builder.Services.AddScoped<ILoyaltyAccrualService, LoyaltyAccrualService>();
```
(Add `using AFK4.Platform.Api.Loyalty;` if needed.) Build.

- [ ] **Step 6: Commit.**
```bash
git add src/AFK4.Platform.Api/Loyalty tests/AFK4.Platform.Api.Tests/LoyaltyAccrualServiceTests.cs src/AFK4.Platform.Api/Program.cs
git commit -m "feat(loyalty): accrual service computes floored cashback entry"
```

---

## Task L4: Top-up cashback hook

**Files:**
- Modify: `src/AFK4.Platform.Api/Billing/EfBillingCommandService.cs`
- Test: `tests/AFK4.Platform.Api.Tests/EfBillingCommandServiceTests.cs`

- [ ] **Step 1: Write the failing test.** Append to `EfBillingCommandServiceTests`. (Note: `CreateService` currently builds the service with two collaborators; you will extend it to pass an `ILoyaltyAccrualService` in Step 3 — write the test against the final shape.) Add a settings-seeding helper and two tests:
```csharp
    private static async Task SeedLoyaltyAsync(PlatformDbContext db, bool topUpEnabled, int topUpBps)
    {
        db.OrganizationLoyaltySettings.Add(new OrganizationLoyaltySettingsEntity
        {
            OrganizationId = TestIds.OrganizationId,
            TopUpEnabled = topUpEnabled,
            TopUpPercentBasisPoints = topUpBps,
            ShopEnabled = false,
            ShopPercentBasisPoints = 0,
            UpdatedAtUtc = Now
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task TopUpWalletAsync_AccruesCashbackWhenTopUpLoyaltyEnabled()
    {
        await using var db = CreateDbContext();
        await SeedPlayerAsync(db);
        await SeedOpenShiftAsync(db);
        await SeedLoyaltyAsync(db, topUpEnabled: true, topUpBps: 500);
        var service = CreateService(db);
        var request = new TopUpWalletRequest(TestIds.OrganizationId, new MoneyDto("TJS", 5000), "cash top-up", "topup-cb-1");

        var result = await service.TopUpWalletAsync(PlayerAccountId, TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);

        Assert.True(result.Succeeded);
        var cashback = await db.LedgerEntries.SingleAsync(e => e.EntryType == LedgerEntryTypeNames.Cashback);
        Assert.Equal(250, cashback.AmountMinorUnits); // floor(5000 * 500 / 10000)
        Assert.Equal(LedgerAccountTypeNames.Wallet, cashback.AccountType);
        // Returned summary reflects top-up + cashback.
        Assert.Equal(5250, result.Response!.WalletBalance.MinorUnits);
    }

    [Fact]
    public async Task TopUpWalletAsync_NoCashbackWhenLoyaltyDisabled()
    {
        await using var db = CreateDbContext();
        await SeedPlayerAsync(db);
        await SeedOpenShiftAsync(db);
        var service = CreateService(db);
        var request = new TopUpWalletRequest(TestIds.OrganizationId, new MoneyDto("TJS", 5000), "cash top-up", "topup-cb-2");

        var result = await service.TopUpWalletAsync(PlayerAccountId, TestIds.BranchId, ActorStaffUserId, request, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.False(await db.LedgerEntries.AnyAsync(e => e.EntryType == LedgerEntryTypeNames.Cashback));
    }
```
Also update the `CreateService` helper now (so the file compiles against the new ctor):
```csharp
    private static EfBillingCommandService CreateService(PlatformDbContext db)
    {
        return new EfBillingCommandService(
            db,
            new EfShiftService(db, new FixedTimeProvider(Now)),
            new FixedTimeProvider(Now),
            new LoyaltyAccrualService(db));
    }
```
Add `using AFK4.Platform.Api.Loyalty;` to the test file.

- [ ] **Step 2: Run — verify it fails** (ctor arity / `WalletBalance` 5250 mismatch / cashback missing).

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter EfBillingCommandServiceTests`
Expected: compile error (4-arg ctor) then, after Step 3 partial, assertion failures resolve.

- [ ] **Step 3: Implement the hook.** In `EfBillingCommandService.cs`:

(a) Extend the primary constructor to take the accrual service and add the using:
```csharp
using AFK4.Platform.Api.Loyalty;
```
```csharp
public sealed class EfBillingCommandService(
    PlatformDbContext dbContext,
    IOpenShiftResolver openShiftResolver,
    TimeProvider timeProvider,
    ILoyaltyAccrualService loyaltyAccrualService) : IBillingCommandService
```

(b) In `TopUpWalletCoreAsync`, replace the final `return await ExecuteLedgerSummaryCommandAsync(...)` block with one that builds the cashback entry and passes it as an extra entry:
```csharp
        var topUpEntry = BillingEntryFactory.Create(
            request.OrganizationId,
            branchId,
            playerAccountId,
            sessionId: null,
            playerPackageId: null,
            LedgerEntryTypeNames.TopUp,
            LedgerAccountTypeNames.Wallet,
            request.Amount.MinorUnits,
            quantitySeconds: 0,
            currencyValidation.CurrencyCode,
            description: LedgerEntryTypeNames.TopUp,
            request.Reason.Trim(),
            reversesLedgerEntryId: null,
            actorStaffUserId,
            timeProvider.GetUtcNow(),
            shiftId);

        var cashbackEntry = await loyaltyAccrualService.BuildCashbackEntryAsync(
            LoyaltyAccrualSource.TopUp,
            request.OrganizationId,
            branchId,
            playerAccountId,
            sessionId: null,
            request.Amount.MinorUnits,
            currencyValidation.CurrencyCode,
            reason: "cashback:topup",
            timeProvider.GetUtcNow(),
            cancellationToken);

        return await ExecuteLedgerSummaryCommandAsync(
            request.OrganizationId,
            branchId,
            WalletTopUpOperation,
            request.IdempotencyKey,
            request,
            PlayerScopedRequest(playerAccountId, request),
            topUpEntry,
            cashbackEntry is null ? null : new[] { cashbackEntry },
            cancellationToken);
```

(c) Add the `additionalEntries` parameter to `ExecuteLedgerSummaryCommandAsync` and persist them in the same transaction:
```csharp
    private async Task<BillingCommandServiceResult<WalletSummaryDto>> ExecuteLedgerSummaryCommandAsync<TRequest>(
        Guid organizationId,
        Guid branchId,
        string operation,
        string idempotencyKey,
        TRequest request,
        object requestHashInput,
        LedgerEntryEntity entry,
        IReadOnlyList<LedgerEntryEntity>? additionalEntries,
        CancellationToken cancellationToken)
    {
        return await ExecuteInTransactionAsync(async () =>
        {
            dbContext.LedgerEntries.Add(entry);
            if (additionalEntries is { Count: > 0 })
            {
                dbContext.LedgerEntries.AddRange(additionalEntries);
            }
            await dbContext.SaveChangesAsync(cancellationToken);
            // ... rest unchanged (summary, idempotency record, return Ok) ...
```

(d) The two other callers of `ExecuteLedgerSummaryCommandAsync` (`ManualCorrectionAsync`, `PayDebtAsync`) must pass `additionalEntries: null`. In each, insert `additionalEntries: null,` immediately before the final `cancellationToken)` argument:
```csharp
            BillingEntryFactory.Create(/* ...unchanged... */),
            additionalEntries: null,
            cancellationToken);
```

- [ ] **Step 4: Run tests — verify pass** (the new two + all existing billing tests).

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter EfBillingCommandServiceTests`
Expected: all passed.

- [ ] **Step 5: Commit.**
```bash
git add src/AFK4.Platform.Api/Billing/EfBillingCommandService.cs tests/AFK4.Platform.Api.Tests/EfBillingCommandServiceTests.cs
git commit -m "feat(loyalty): accrue cashback on confirmed top-up"
```

---

## Task L5: Shop-delivered cashback hook

**Files:**
- Modify: `src/AFK4.Platform.Api/Shop/EfShopOrderService.cs`
- Test: `tests/AFK4.Platform.Api.Tests/EfShopOrderServiceTests.cs`

- [ ] **Step 1: Write the failing test.** Append to `EfShopOrderServiceTests` (mirror its existing seeding helpers; locate `CreateService` there and update it to pass a `LoyaltyAccrualService` — see Step 3). Test that delivering an order accrues shop cashback:
```csharp
    [Fact]
    public async Task DeliverAsync_AccruesShopCashbackWhenEnabled()
    {
        await using var db = CreateDbContext();
        // Arrange: seed org loyalty (shop 3%), player+session+wallet+product, place an order, accept it.
        db.OrganizationLoyaltySettings.Add(new OrganizationLoyaltySettingsEntity
        {
            OrganizationId = TestIds.OrganizationId, TopUpEnabled = false, TopUpPercentBasisPoints = 0,
            ShopEnabled = true, ShopPercentBasisPoints = 300, UpdatedAtUtc = Now
        });
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var placed = await SeedAndPlaceOrderAsync(db, service); // helper that seeds + places (see existing place test)
        await service.AcceptAsync(placed.BranchId, Guid.Parse(placed.Id), StaffUserId, expectedVersion: null, CancellationToken.None);

        var delivered = await service.DeliverAsync(placed.BranchId, Guid.Parse(placed.Id), StaffUserId, expectedVersion: null, CancellationToken.None);

        Assert.True(delivered.IsSuccess); // use the result-success member this suite already asserts on
        var cashback = await db.LedgerEntries.SingleAsync(e => e.EntryType == LedgerEntryTypeNames.Cashback);
        var orderTotal = placed.Total.MinorUnits;
        Assert.Equal(orderTotal * 300 / 10000, cashback.AmountMinorUnits);
    }
```
If a `SeedAndPlaceOrderAsync` helper does not exist, build the smallest order inline by copying the seeding from the suite's existing "place" test (seed player, active session, one in-shell product with stock, top-up the wallet, call `PlaceAsync`). Reuse the suite's existing id constants (`StaffUserId`, `TestIds.*`, `Now`) and its success-assertion member.

- [ ] **Step 2: Run — verify it fails** (ctor arity / no cashback entry).

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter EfShopOrderServiceTests`
Expected: compile error then assertion failure.

- [ ] **Step 3: Implement.** In `EfShopOrderService.cs`:

(a) Add the using and extend the constructor:
```csharp
using AFK4.Platform.Api.Loyalty;
```
```csharp
public sealed class EfShopOrderService(
    PlatformDbContext dbContext,
    TimeProvider timeProvider,
    IShopOrderNotifier notifier,
    ILoyaltyAccrualService loyaltyAccrualService) : IShopOrderService
```

(b) In `TransitionAsync`, accrue cashback when the order becomes `delivered`, adding it to the same `SaveChanges` so it is atomic with the status change:
```csharp
        var now = timeProvider.GetUtcNow();
        order.Status = toStatus;
        order.Version += 1;
        if (toStatus == ShopOrderStatusNames.Accepted)
        {
            order.AcceptedAtUtc = now;
        }
        else if (toStatus == ShopOrderStatusNames.Delivered)
        {
            order.DeliveredAtUtc = now;
            var cashback = await loyaltyAccrualService.BuildCashbackEntryAsync(
                LoyaltyAccrualSource.Shop,
                order.OrganizationId,
                order.BranchId,
                order.PlayerAccountId,
                order.SessionId,
                order.TotalMinorUnits,
                order.CurrencyCode,
                reason: $"cashback:shop:{order.ShopOrderId:D}",
                now,
                cancellationToken);
            if (cashback is not null)
            {
                dbContext.LedgerEntries.Add(cashback);
            }
        }
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        // ... unchanged catch + projection + notify ...
```
Update the `CreateService` helper in the test suite to pass the accrual service:
```csharp
    // in EfShopOrderServiceTests
    private static EfShopOrderService CreateService(PlatformDbContext db, /* existing notifier fake */) =>
        new(db, new FixedTimeProvider(Now), /* notifier fake */, new LoyaltyAccrualService(db));
```
(Add `using AFK4.Platform.Api.Loyalty;` to the test file. Keep whatever notifier fake the suite already uses.)

- [ ] **Step 4: Run tests — verify pass** (new test + all existing shop tests, incl. cancel-before-deliver which must still produce no cashback).

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter EfShopOrderServiceTests`
Expected: all passed.

- [ ] **Step 5: Commit.**
```bash
git add src/AFK4.Platform.Api/Shop/EfShopOrderService.cs tests/AFK4.Platform.Api.Tests/EfShopOrderServiceTests.cs
git commit -m "feat(loyalty): accrue cashback on shop order delivery"
```

---

## Task L6: Owner loyalty-settings endpoints

**Files:**
- Create: `src/AFK4.Platform.Api/Endpoints/LoyaltySettingsEndpoints.cs`
- Modify: `src/AFK4.Platform.Api/Identity/PermissionCatalog.cs`
- Modify: `src/AFK4.Platform.Api/Audit/AuditActionNames.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs`
- Test: `tests/AFK4.Platform.Api.Tests/LoyaltySettingsEndpointsTests.cs`

- [ ] **Step 1: Add the audit action + grant the permission.**

In `Audit/AuditActionNames.cs`, add (near the shop actions):
```csharp
    public const string UpdateLoyaltySettings = "loyalty.settings.update";
```

In `Identity/PermissionCatalog.cs`, add `StaffPermissionNames.ManageLoyaltySettings` to the `Owner` role HashSet (after `ManagePaymentGateways`):
```csharp
    StaffPermissionNames.ManagePaymentGateways,
    StaffPermissionNames.ManageLoyaltySettings
```

- [ ] **Step 2: Write the failing test.** `LoyaltySettingsEndpointsTests.cs` — mirror an existing owner-endpoint integration test (look at how `PaymentGatewayEndpoints` tests authenticate as an owner; reuse that harness/helpers — likely `PlatformApiFactory` with a staff auth header/fake). The test should:
```csharp
// using ... (mirror PaymentGatewayEndpointsTests usings + harness)
[Fact]
public async Task PutThenGet_RoundTripsOrgLoyaltySettings()
{
    using var app = /* PlatformApiFactory authenticated as Owner of TestIds.OrganizationId */;
    var client = /* app.CreateClient with owner auth, as the gateway tests do */;

    var put = await client.PutAsJsonAsync("/api/owner/loyalty-settings",
        new UpdateLoyaltySettingsRequest(TopUpEnabled: true, TopUpPercentBasisPoints: 500, ShopEnabled: false, ShopPercentBasisPoints: 0));
    Assert.Equal(HttpStatusCode.OK, put.StatusCode);

    var dto = await (await client.GetAsync("/api/owner/loyalty-settings")).Content.ReadFromJsonAsync<LoyaltySettingsDto>();
    Assert.True(dto!.TopUpEnabled);
    Assert.Equal(500, dto.TopUpPercentBasisPoints);
    Assert.False(dto.ShopEnabled);
}

[Fact]
public async Task Put_RejectsOutOfRangePercent()
{
    using var app = /* owner-authenticated factory */;
    var client = /* owner client */;
    var put = await client.PutAsJsonAsync("/api/owner/loyalty-settings",
        new UpdateLoyaltySettingsRequest(true, 10001, false, 0));
    Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
}

[Fact]
public async Task Get_DefaultsToAllDisabledWhenNoRow()
{
    using var app = /* owner-authenticated factory, no settings seeded */;
    var client = /* owner client */;
    var dto = await (await client.GetAsync("/api/owner/loyalty-settings")).Content.ReadFromJsonAsync<LoyaltySettingsDto>();
    Assert.False(dto!.TopUpEnabled);
    Assert.False(dto.ShopEnabled);
    Assert.Equal(0, dto.TopUpPercentBasisPoints);
}
```
Match the exact owner-auth setup used by the payment-gateway endpoint tests (do not invent a new auth scheme).

- [ ] **Step 3: Run — verify it fails** (404 / route missing).

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter LoyaltySettingsEndpointsTests`
Expected: failures (no endpoint).

- [ ] **Step 4: Implement the endpoints.** `LoyaltySettingsEndpoints.cs`:
```csharp
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Loyalty;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Endpoints;

internal static class LoyaltySettingsEndpoints
{
    public static void MapLoyaltySettingsEndpoints(this WebApplication app)
    {
        app.MapGet("/api/owner/loyalty-settings", async (
            StaffAuthorizationService authorizationService,
            PlatformDbContext db,
            CancellationToken ct) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(StaffPermissionNames.ManageLoyaltySettings);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var orgId = authorization.StaffContext!.OrganizationId;
            var row = await db.OrganizationLoyaltySettings.AsNoTracking()
                .SingleOrDefaultAsync(s => s.OrganizationId == orgId, ct);

            return Results.Ok(row is null
                ? new LoyaltySettingsDto(false, 0, false, 0)
                : new LoyaltySettingsDto(row.TopUpEnabled, row.TopUpPercentBasisPoints, row.ShopEnabled, row.ShopPercentBasisPoints));
        });

        app.MapPut("/api/owner/loyalty-settings", async (
            UpdateLoyaltySettingsRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            TimeProvider timeProvider,
            PlatformDbContext db,
            CancellationToken ct) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(StaffPermissionNames.ManageLoyaltySettings);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            if (request.TopUpPercentBasisPoints is < 0 or > 10000 || request.ShopPercentBasisPoints is < 0 or > 10000)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["percentBasisPoints"] = ["Percent must be between 0 and 10000 basis points (0–100%)."]
                });
            }

            var staff = authorization.StaffContext!;
            var orgId = staff.OrganizationId;
            var now = timeProvider.GetUtcNow();

            var row = await db.OrganizationLoyaltySettings.SingleOrDefaultAsync(s => s.OrganizationId == orgId, ct);
            if (row is null)
            {
                row = new OrganizationLoyaltySettingsEntity { OrganizationId = orgId };
                db.OrganizationLoyaltySettings.Add(row);
            }
            row.TopUpEnabled = request.TopUpEnabled;
            row.TopUpPercentBasisPoints = request.TopUpPercentBasisPoints;
            row.ShopEnabled = request.ShopEnabled;
            row.ShopPercentBasisPoints = request.ShopPercentBasisPoints;
            row.UpdatedAtUtc = now;
            await db.SaveChangesAsync(ct);

            await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                orgId,
                BranchId: null,
                ActorStaffUserId: staff.StaffUserId,
                Action: AuditActionNames.UpdateLoyaltySettings,
                TargetType: "OrganizationLoyaltySettings",
                TargetId: orgId.ToString("N"),
                Outcome: "success",
                SourceApp: "operator",
                DetailsJson: System.Text.Json.JsonSerializer.Serialize(request)), ct);

            return Results.Ok(new LoyaltySettingsDto(
                row.TopUpEnabled, row.TopUpPercentBasisPoints, row.ShopEnabled, row.ShopPercentBasisPoints));
        });
    }
}
```

- [ ] **Step 5: Register in Program.cs** near `app.MapPaymentGatewayEndpoints();`:
```csharp
app.MapLoyaltySettingsEndpoints();
```

- [ ] **Step 6: Run tests — verify pass.**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter LoyaltySettingsEndpointsTests`
Expected: 3 passed.

- [ ] **Step 7: Commit.**
```bash
git add src/AFK4.Platform.Api/Endpoints/LoyaltySettingsEndpoints.cs src/AFK4.Platform.Api/Identity/PermissionCatalog.cs src/AFK4.Platform.Api/Audit/AuditActionNames.cs src/AFK4.Platform.Api/Program.cs tests/AFK4.Platform.Api.Tests/LoyaltySettingsEndpointsTests.cs
git commit -m "feat(loyalty): owner endpoints to read/update org cashback settings"
```

---

## Task L7: Player loyalty endpoint

**Files:**
- Create: `src/AFK4.Platform.Api/Endpoints/PlayerLoyaltyEndpoints.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs`
- Test: `tests/AFK4.Platform.Api.Tests/PlayerLoyaltyEndpointsTests.cs`

- [ ] **Step 1: Write the failing test.** `PlayerLoyaltyEndpointsTests.cs` — mirror the player-endpoint integration harness used by the shop player tests (`PlayerShopEndpoints` tests authenticate as a player via `IPlayerContextAccessor`). Test that the endpoint returns rates + total earned + recent entries:
```csharp
[Fact]
public async Task Get_ReturnsRatesTotalAndRecentCashback()
{
    using var app = /* PlatformApiFactory authed as the seeded player */;
    // seed: org loyalty (topUp 5%, shop off); two cashback ledger entries (120 + 80) for the player in TJS.
    var client = /* player client */;

    var dto = await (await client.GetAsync("/api/me/loyalty")).Content.ReadFromJsonAsync<PlayerLoyaltyDto>();

    Assert.True(dto!.TopUpEnabled);
    Assert.Equal(500, dto.TopUpPercentBasisPoints);
    Assert.False(dto.ShopEnabled);
    Assert.Equal(200, dto.TotalEarned.MinorUnits);
    Assert.Equal("TJS", dto.TotalEarned.CurrencyCode);
    Assert.Equal(2, dto.Recent.Count);
}
```
Use the exact player-auth seeding the shop player tests use.

- [ ] **Step 2: Run — verify it fails** (404).

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter PlayerLoyaltyEndpointsTests`
Expected: failure.

- [ ] **Step 3: Implement.** `PlayerLoyaltyEndpoints.cs`:
```csharp
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Loyalty;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Endpoints;

internal static class PlayerLoyaltyEndpoints
{
    public static void MapPlayerLoyaltyEndpoints(this WebApplication app)
    {
        app.MapGet("/api/me/loyalty", async (
            IPlayerContextAccessor playerContextAccessor,
            PlatformDbContext db,
            CancellationToken ct) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null) return Results.Unauthorized();

            var settings = await db.OrganizationLoyaltySettings.AsNoTracking()
                .SingleOrDefaultAsync(s => s.OrganizationId == player.OrganizationId, ct);

            var entries = await db.LedgerEntries.AsNoTracking()
                .Where(e => e.PlayerAccountId == player.PlayerAccountId && e.EntryType == LedgerEntryTypeNames.Cashback)
                .OrderByDescending(e => e.CreatedAtUtc)
                .Take(20)
                .ToListAsync(ct);

            var totalMinor = entries.Sum(e => e.AmountMinorUnits);
            var currency = entries.Count > 0 ? entries[0].CurrencyCode : "TJS";
            var recent = entries
                .Select(e => new CashbackEntryDto(e.AmountMinorUnits, e.CurrencyCode, e.Reason, e.CreatedAtUtc))
                .ToList();

            return Results.Ok(new PlayerLoyaltyDto(
                settings?.TopUpEnabled ?? false,
                settings?.TopUpPercentBasisPoints ?? 0,
                settings?.ShopEnabled ?? false,
                settings?.ShopPercentBasisPoints ?? 0,
                new MoneyDto(currency, totalMinor),
                recent));
        }).RequireRateLimiting("player-me");
    }
}
```
Note: `Take(20)` caps the total to the 20 most recent cashback credits; this is a recent-earnings view, not a lifetime ledger sum. If a true lifetime total is wanted later, sum separately — out of scope here.

- [ ] **Step 4: Register in Program.cs** near `app.MapPlayerShopEndpoints();`:
```csharp
app.MapPlayerLoyaltyEndpoints();
```

- [ ] **Step 5: Run tests — verify pass.**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter PlayerLoyaltyEndpointsTests`
Expected: passed.

- [ ] **Step 6: Full server test sweep + commit.**
```bash
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj
git add src/AFK4.Platform.Api/Endpoints/PlayerLoyaltyEndpoints.cs src/AFK4.Platform.Api/Program.cs tests/AFK4.Platform.Api.Tests/PlayerLoyaltyEndpointsTests.cs
git commit -m "feat(loyalty): player loyalty endpoint (rates + earned + recent)"
```

---

## Task L8: Shell API — loyalty types + client method

**Files:**
- Modify: `src/AFK4.Player.Shell.Web/src/apiTypes.ts`
- Modify: `src/AFK4.Player.Shell.Web/src/shellApi.ts`
- Test: `src/AFK4.Player.Shell.Web/src/shellApi.test.ts` (append; if absent, create)

- [ ] **Step 1: Add DTO mirrors.** In `apiTypes.ts`, append:
```typescript
export interface CashbackEntryDto {
  amountMinorUnits: number;
  currencyCode: string;
  reason: string;
  createdAtUtc: string;
}

export interface PlayerLoyaltyDto {
  topUpEnabled: boolean;
  topUpPercentBasisPoints: number;
  shopEnabled: boolean;
  shopPercentBasisPoints: number;
  totalEarned: MoneyDto;
  recent: CashbackEntryDto[];
}
```

- [ ] **Step 2: Write the failing test.** In `shellApi.test.ts` add:
```typescript
import { describe, expect, it } from 'bun:test';
import { createShellApi } from './shellApi';
import type { PlayerLoyaltyDto } from './apiTypes';

describe('shellApi.getLoyalty', () => {
  it('GETs /api/me/loyalty and returns the dto', async () => {
    const dto: PlayerLoyaltyDto = {
      topUpEnabled: true, topUpPercentBasisPoints: 500, shopEnabled: false, shopPercentBasisPoints: 0,
      totalEarned: { currencyCode: 'TJS', minorUnits: 200 }, recent: []
    };
    let calledUrl = '';
    const api = createShellApi('http://x', async (url) => {
      calledUrl = url;
      return new Response(JSON.stringify(dto), { status: 200, headers: { 'Content-Type': 'application/json' } });
    });
    const result = await api.getLoyalty();
    expect(calledUrl).toBe('http://x/api/me/loyalty');
    expect(result.topUpPercentBasisPoints).toBe(500);
  });
});
```

- [ ] **Step 3: Run — verify fail.**

Run: `cd src/AFK4.Player.Shell.Web && /home/fedya/.bun/bin/bun test src/shellApi.test.ts`
Expected: FAIL (`getLoyalty` not a function).

- [ ] **Step 4: Implement.** In `shellApi.ts`, add `PlayerLoyaltyDto` to the import from `./apiTypes`, and add a method to the returned object (next to `listShopOrders`):
```typescript
    getLoyalty: () => call<PlayerLoyaltyDto>('/api/me/loyalty'),
```

- [ ] **Step 5: Run — verify pass.**

Run: `cd src/AFK4.Player.Shell.Web && /home/fedya/.bun/bin/bun test src/shellApi.test.ts`
Expected: PASS.

- [ ] **Step 6: Commit.**
```bash
git add src/AFK4.Player.Shell.Web/src/apiTypes.ts src/AFK4.Player.Shell.Web/src/shellApi.ts src/AFK4.Player.Shell.Web/src/shellApi.test.ts
git commit -m "feat(loyalty): shell api getLoyalty + dto mirrors"
```

---

## Task L9: Shell LoyaltyScreen

**Files:**
- Create: `src/AFK4.Player.Shell.Web/src/screens/LoyaltyScreen.tsx`
- Test: `src/AFK4.Player.Shell.Web/src/screens/LoyaltyScreen.test.tsx`

- [ ] **Step 1: Write the failing test.** `LoyaltyScreen.test.tsx`:
```tsx
import { render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'bun:test';
import { LoyaltyScreen } from './LoyaltyScreen';
import type { ShellApi } from '../shellApi';

function api(over: Partial<ShellApi>): ShellApi {
  return { getLoyalty: async () => ({
    topUpEnabled: true, topUpPercentBasisPoints: 500, shopEnabled: false, shopPercentBasisPoints: 0,
    totalEarned: { currencyCode: 'TJS', minorUnits: 12345 }, recent: []
  }), ...over } as unknown as ShellApi;
}

describe('LoyaltyScreen', () => {
  it('shows the enabled top-up rate and total earned, hides the disabled shop rate', async () => {
    render(<LoyaltyScreen api={api({})} onDone={() => {}} />);
    await waitFor(() => screen.getByText(/5%/));
    expect(screen.getByText(/123[.,]45/)).toBeInTheDocument(); // 12345 minor -> 123.45
    expect(screen.queryByText(/магазин/i)).not.toBeInTheDocument();
  });

  it('shows an offline/empty message when both sources are disabled', async () => {
    render(<LoyaltyScreen api={api({ getLoyalty: async () => ({
      topUpEnabled: false, topUpPercentBasisPoints: 0, shopEnabled: false, shopPercentBasisPoints: 0,
      totalEarned: { currencyCode: 'TJS', minorUnits: 0 }, recent: []
    }) })} onDone={() => {}} />);
    await waitFor(() => screen.getByText(/кэшбэк пока недоступен/i));
  });
});
```

- [ ] **Step 2: Run — verify fail.**

Run: `cd src/AFK4.Player.Shell.Web && /home/fedya/.bun/bin/bun test src/screens/LoyaltyScreen.test.tsx`
Expected: FAIL (module not found).

- [ ] **Step 3: Implement.** `LoyaltyScreen.tsx` — match the raw-Russian-string, hooks-on-mount style of `ShopScreen.tsx`/`TopUpScreen.tsx` (no i18n hook in the kiosk shell):
```tsx
import { useEffect, useState } from 'react';
import type { ShellApi } from '../shellApi';
import type { PlayerLoyaltyDto } from '../apiTypes';

function formatMoney(minorUnits: number, currencyCode: string): string {
  return `${(minorUnits / 100).toFixed(2)} ${currencyCode}`;
}

function formatPercent(basisPoints: number): string {
  return `${(basisPoints / 100).toFixed(basisPoints % 100 === 0 ? 0 : 2)}%`;
}

export function LoyaltyScreen({ api, onDone }: { api: ShellApi; onDone: () => void }) {
  const [data, setData] = useState<PlayerLoyaltyDto | null>(null);
  const [error, setError] = useState(false);

  useEffect(() => {
    let active = true;
    api.getLoyalty().then(
      (d) => { if (active) setData(d); },
      () => { if (active) setError(true); }
    );
    return () => { active = false; };
  }, [api]);

  if (error) {
    return (
      <section>
        <h2>Кэшбэк</h2>
        <p>Не удалось загрузить лояльность. Попробуйте позже.</p>
        <button type="button" onClick={onDone}>Назад</button>
      </section>
    );
  }

  if (!data) {
    return <section><h2>Кэшбэк</h2><p>Загрузка…</p></section>;
  }

  const anyEnabled = data.topUpEnabled || data.shopEnabled;

  return (
    <section>
      <h2>Кэшбэк</h2>
      {!anyEnabled && <p>Кэшбэк пока недоступен в этом клубе.</p>}
      {anyEnabled && (
        <>
          <p>Кэшбэк падает прямо в кошелёк и тратится как обычные деньги.</p>
          <ul>
            {data.topUpEnabled && <li>Пополнение: {formatPercent(data.topUpPercentBasisPoints)} кэшбэка</li>}
            {data.shopEnabled && <li>Магазин: {formatPercent(data.shopPercentBasisPoints)} кэшбэка</li>}
          </ul>
        </>
      )}
      <p>Всего начислено: <strong>{formatMoney(data.totalEarned.minorUnits, data.totalEarned.currencyCode)}</strong></p>
      {data.recent.length > 0 && (
        <ul>
          {data.recent.map((entry, index) => (
            <li key={index}>{formatMoney(entry.amountMinorUnits, entry.currencyCode)} — {new Date(entry.createdAtUtc).toLocaleDateString('ru-RU')}</li>
          ))}
        </ul>
      )}
      <button type="button" onClick={onDone}>Назад</button>
    </section>
  );
}
```
Note: in the first test `shopEnabled` is false, so the "Магазин" `<li>` is not rendered and `queryByText(/магазин/i)` is null — matching the assertion.

- [ ] **Step 4: Run — verify pass.**

Run: `cd src/AFK4.Player.Shell.Web && /home/fedya/.bun/bin/bun test src/screens/LoyaltyScreen.test.tsx`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit.**
```bash
git add src/AFK4.Player.Shell.Web/src/screens/LoyaltyScreen.tsx src/AFK4.Player.Shell.Web/src/screens/LoyaltyScreen.test.tsx
git commit -m "feat(loyalty): shell LoyaltyScreen (rates + earned + recent)"
```

---

## Task L10: Wire LoyaltyScreen into SelfServiceMenu

**Files:**
- Modify: `src/AFK4.Player.Shell.Web/src/screens/SelfServiceMenu.tsx`
- Test: `src/AFK4.Player.Shell.Web/src/screens/SelfServiceMenu.test.tsx`

- [ ] **Step 1: Write the failing test.** Append to `SelfServiceMenu.test.tsx` a test that opens the loyalty view (mirror the existing shop-entry test in this file):
```tsx
describe('SelfServiceMenu loyalty entry', () => {
  it('opens loyalty from the menu', async () => {
    render(<SelfServiceMenu authenticated onSignIn={async () => true} api={api()}
      sessionId="s1" branchId="b1" onReloadState={() => {}} />);
    fireEvent.click(screen.getByRole('button', { name: /кэшбэк/i }));
    await waitFor(() => expect(screen.getByText(/падает прямо в кошелёк|кэшбэк пока недоступен/i)).toBeInTheDocument());
  });
});
```
Ensure the local `api()` helper in this test file includes `getLoyalty` (add it to the helper's defaults returning an enabled top-up rate, matching how the file stubs `listShopCatalog`).

- [ ] **Step 2: Run — verify fail.**

Run: `cd src/AFK4.Player.Shell.Web && /home/fedya/.bun/bin/bun test src/screens/SelfServiceMenu.test.tsx`
Expected: FAIL (no "кэшбэк" button).

- [ ] **Step 3: Implement.** In `SelfServiceMenu.tsx`, mirror exactly how the shop entry is implemented (the same `view` state machine + button + conditional render). Import `LoyaltyScreen`, add a `'loyalty'` value to the view union, add a menu button, and render `<LoyaltyScreen api={api} onDone={() => setView('menu')} />` when `view === 'loyalty'`:
```tsx
import { LoyaltyScreen } from './LoyaltyScreen';
// ...
// menu button (next to the shop button):
<button type="button" onClick={() => setView('loyalty')}>Кэшбэк</button>
// ...
// in the render switch / conditional block (next to shop):
{view === 'loyalty' && <LoyaltyScreen api={api} onDone={() => setView('menu')} />}
```
(Use the actual state setter/name this file already uses — match the shop case precisely; do not gate loyalty on `sessionId` since loyalty is viewable without an active session.)

- [ ] **Step 4: Run — verify pass + full shell sweep.**

Run: `cd src/AFK4.Player.Shell.Web && /home/fedya/.bun/bin/bun test`
Expected: all pass.

- [ ] **Step 5: Commit.**
```bash
git add src/AFK4.Player.Shell.Web/src/screens/SelfServiceMenu.tsx src/AFK4.Player.Shell.Web/src/screens/SelfServiceMenu.test.tsx
git commit -m "feat(loyalty): wire LoyaltyScreen into self-service menu"
```

---

## Task L11: Operator owner loyalty-settings API client

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/operatorApiClients.ts`
- Test: `src/AFK4.Operator.App.Web/src/operatorApiClients.test.ts` (append; if absent, create)

- [ ] **Step 1: Write the failing test.** Mirror an existing client-factory test in this file (or the gateway client). Verify GET/PUT call the right paths:
```typescript
import { describe, expect, it } from 'bun:test';
import { createLoyaltySettingsClient } from './operatorApiClients';

describe('createLoyaltySettingsClient', () => {
  it('gets and updates /api/owner/loyalty-settings', async () => {
    const calls: Array<{ method: string; path: string; body?: unknown }> = [];
    const apiFake = {
      get: async <T,>(path: string) => { calls.push({ method: 'GET', path }); return { topUpEnabled: false, topUpPercentBasisPoints: 0, shopEnabled: false, shopPercentBasisPoints: 0 } as T; },
      put: async <T,>(path: string, body: unknown) => { calls.push({ method: 'PUT', path, body }); return body as T; },
      post: async <T,>() => ({} as T)
    };
    const client = createLoyaltySettingsClient(apiFake as never);
    await client.get();
    await client.update({ topUpEnabled: true, topUpPercentBasisPoints: 500, shopEnabled: false, shopPercentBasisPoints: 0 });
    expect(calls).toEqual([
      { method: 'GET', path: '/api/owner/loyalty-settings' },
      { method: 'PUT', path: '/api/owner/loyalty-settings', body: { topUpEnabled: true, topUpPercentBasisPoints: 500, shopEnabled: false, shopPercentBasisPoints: 0 } }
    ]);
  });
});
```
Confirm the `PlatformApiClient` exposes a `put` method; if it only has `get`/`post`, use `post` to a PUT-style route per the file's convention — match what `createPaymentGatewayClient` uses. Adjust the test accordingly before implementing.

- [ ] **Step 2: Run — verify fail.**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/operatorApiClients.test.ts`
Expected: FAIL.

- [ ] **Step 3: Implement.** In `operatorApiClients.ts`, add the DTO types (mirror the C# records) and the client factory near `createPaymentGatewayClient`:
```typescript
export interface LoyaltySettingsDto {
  topUpEnabled: boolean;
  topUpPercentBasisPoints: number;
  shopEnabled: boolean;
  shopPercentBasisPoints: number;
}

export function createLoyaltySettingsClient(api: PlatformApiClient) {
  return {
    get(): Promise<LoyaltySettingsDto> {
      return api.get<LoyaltySettingsDto>('/api/owner/loyalty-settings');
    },
    update(request: LoyaltySettingsDto): Promise<LoyaltySettingsDto> {
      return api.put<LoyaltySettingsDto, LoyaltySettingsDto>('/api/owner/loyalty-settings', request);
    }
  };
}
```
(If `PlatformApiClient` has no `put`, follow whatever verb the file already uses for owner mutations — keep the test and impl consistent.)

- [ ] **Step 4: Run — verify pass.**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/operatorApiClients.test.ts`
Expected: PASS.

- [ ] **Step 5: Commit.**
```bash
git add src/AFK4.Operator.App.Web/src/operatorApiClients.ts src/AFK4.Operator.App.Web/src/operatorApiClients.test.ts
git commit -m "feat(loyalty): operator owner loyalty-settings api client"
```

---

## Task L12: Operator LoyaltySettingsWorkspace + nav + i18n

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/LoyaltySettingsWorkspace.tsx`
- Test: `src/AFK4.Operator.App.Web/src/LoyaltySettingsWorkspace.test.tsx`
- Modify: nav wiring files (mirror `ShopOrdersWorkspace` registration — `App.tsx`, `operatorData.ts`/`operatorTypes.ts`, `operatorPermissions.ts`, `SummarySidePanel.tsx` as applicable)
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json`

- [ ] **Step 1: Add i18n keys.** In each of `locales/{ru,en,tg}.json`, add a `op.loyalty.*` group (mirror the `op.shopOrders.*` group's namespacing). Russian values:
```json
  "op.loyalty.title": "Лояльность / кэшбэк",
  "op.loyalty.topUpEnabled": "Кэшбэк с пополнений",
  "op.loyalty.topUpPercent": "Процент с пополнений (%)",
  "op.loyalty.shopEnabled": "Кэшбэк с магазина",
  "op.loyalty.shopPercent": "Процент с магазина (%)",
  "op.loyalty.save": "Сохранить",
  "op.loyalty.saved": "Сохранено",
  "op.loyalty.percentError": "Процент должен быть от 0 до 100",
  "op.loyalty.nav": "Лояльность"
```
Provide English and Tajik equivalents (Tajik may reuse Russian text where no translation exists, matching the project's current `tg.json` convention). Then regenerate:

Run: `cd packages/i18n && /home/fedya/.bun/bin/bun run gen`
Expected: `packages/i18n/src/messages.ts` updated with the new keys.

- [ ] **Step 2: Write the failing test.** `LoyaltySettingsWorkspace.test.tsx` — mirror the operator workspace test setup (see `ShopOrdersWorkspace.test.tsx` for how it provides a client + i18n). UI stores the percent as a whole-number percent (0–100) and converts to/from basis points at the client boundary:
```tsx
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'bun:test';
import { LoyaltySettingsWorkspace } from './LoyaltySettingsWorkspace';

function client(initial = { topUpEnabled: false, topUpPercentBasisPoints: 0, shopEnabled: false, shopPercentBasisPoints: 0 }) {
  const saved: unknown[] = [];
  return {
    saved,
    get: async () => initial,
    update: async (req: unknown) => { saved.push(req); return req as typeof initial; }
  };
}

describe('LoyaltySettingsWorkspace', () => {
  it('loads settings and saves toggles + percent as basis points', async () => {
    const c = client();
    render(<LoyaltySettingsWorkspace client={c as never} />);
    await waitFor(() => screen.getByLabelText(/кэшбэк с пополнений/i));
    fireEvent.click(screen.getByLabelText(/кэшбэк с пополнений/i));
    fireEvent.change(screen.getByLabelText(/процент с пополнений/i), { target: { value: '5' } });
    fireEvent.click(screen.getByRole('button', { name: /сохранить/i }));
    await waitFor(() => expect(c.saved).toEqual([
      { topUpEnabled: true, topUpPercentBasisPoints: 500, shopEnabled: false, shopPercentBasisPoints: 0 }
    ]));
  });

  it('rejects a percent above 100', async () => {
    const c = client();
    render(<LoyaltySettingsWorkspace client={c as never} />);
    await waitFor(() => screen.getByLabelText(/процент с пополнений/i));
    fireEvent.change(screen.getByLabelText(/процент с пополнений/i), { target: { value: '150' } });
    fireEvent.click(screen.getByRole('button', { name: /сохранить/i }));
    await waitFor(() => screen.getByText(/процент должен быть от 0 до 100/i));
    expect(c.saved).toEqual([]);
  });
});
```

- [ ] **Step 3: Run — verify fail.**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/LoyaltySettingsWorkspace.test.tsx`
Expected: FAIL (module not found).

- [ ] **Step 4: Implement the workspace.** `LoyaltySettingsWorkspace.tsx` — use `t()` from `@afk4/i18n` like the other operator workspaces. Store percents in the form as whole-number percent strings; convert percent↔basis points at the boundary (×100 / ÷100):
```tsx
import { useEffect, useState } from 'react';
import { t } from '@afk4/i18n';
import type { LoyaltySettingsDto } from './operatorApiClients';

interface LoyaltySettingsClient {
  get(): Promise<LoyaltySettingsDto>;
  update(request: LoyaltySettingsDto): Promise<LoyaltySettingsDto>;
}

export function LoyaltySettingsWorkspace({ client }: { client: LoyaltySettingsClient }) {
  const [topUpEnabled, setTopUpEnabled] = useState(false);
  const [topUpPercent, setTopUpPercent] = useState('0');
  const [shopEnabled, setShopEnabled] = useState(false);
  const [shopPercent, setShopPercent] = useState('0');
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [ready, setReady] = useState(false);

  useEffect(() => {
    let active = true;
    client.get().then((s) => {
      if (!active) return;
      setTopUpEnabled(s.topUpEnabled);
      setTopUpPercent(String(s.topUpPercentBasisPoints / 100));
      setShopEnabled(s.shopEnabled);
      setShopPercent(String(s.shopPercentBasisPoints / 100));
      setReady(true);
    });
    return () => { active = false; };
  }, [client]);

  function toBasisPoints(percent: string): number | null {
    const value = Number(percent);
    if (!Number.isFinite(value) || value < 0 || value > 100) return null;
    return Math.round(value * 100);
  }

  async function save() {
    setSaved(false);
    const topUpBps = toBasisPoints(topUpPercent);
    const shopBps = toBasisPoints(shopPercent);
    if (topUpBps === null || shopBps === null) {
      setError(t('op.loyalty.percentError'));
      return;
    }
    setError(null);
    await client.update({ topUpEnabled, topUpPercentBasisPoints: topUpBps, shopEnabled, shopPercentBasisPoints: shopBps });
    setSaved(true);
  }

  if (!ready) return <section><h2>{t('op.loyalty.title')}</h2><p>…</p></section>;

  return (
    <section>
      <h2>{t('op.loyalty.title')}</h2>
      <label>
        <input type="checkbox" checked={topUpEnabled} onChange={(e) => setTopUpEnabled(e.target.checked)} />
        {t('op.loyalty.topUpEnabled')}
      </label>
      <label>
        {t('op.loyalty.topUpPercent')}
        <input type="number" value={topUpPercent} onChange={(e) => setTopUpPercent(e.target.value)} />
      </label>
      <label>
        <input type="checkbox" checked={shopEnabled} onChange={(e) => setShopEnabled(e.target.checked)} />
        {t('op.loyalty.shopEnabled')}
      </label>
      <label>
        {t('op.loyalty.shopPercent')}
        <input type="number" value={shopPercent} onChange={(e) => setShopPercent(e.target.value)} />
      </label>
      {error && <p role="alert">{error}</p>}
      {saved && <p>{t('op.loyalty.saved')}</p>}
      <button type="button" onClick={save}>{t('op.loyalty.save')}</button>
    </section>
  );
}
```
Note: the test queries `getByLabelText(/процент с пополнений/i)`. Ensure the label wraps its input (as above) so Testing Library associates them; the `op.loyalty.topUpPercent` value contains "Процент с пополнений".

- [ ] **Step 5: Run the workspace test — verify pass.**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/LoyaltySettingsWorkspace.test.tsx`
Expected: PASS (2).

- [ ] **Step 6: Wire navigation.** Register the workspace as an owner-only nav entry, mirroring exactly how `ShopOrdersWorkspace` was added in the shop cycle (the same files: the workspace registry/`App.tsx` switch, the nav list in `operatorData.ts`/`operatorTypes.ts`, the permission gate in `operatorPermissions.ts` keyed on `loyalty.settings.manage`, and `SummarySidePanel.tsx` if it lists nav items). Construct the client via `createLoyaltySettingsClient(<the authenticated PlatformApiClient>)` the same way `ShopOrdersWorkspace` builds its client. Gate visibility on the `ManageLoyaltySettings` permission.

- [ ] **Step 7: Full operator sweep — verify pass.**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test`
Expected: all pass (including the workspace + nav snapshot tests; update any nav snapshot the same way the shop cycle did).

- [ ] **Step 8: i18n test sweep.**

Run: `cd packages/i18n && /home/fedya/.bun/bin/bun test`
Expected: all pass.

- [ ] **Step 9: Commit.**
```bash
git add src/AFK4.Operator.App.Web locales packages/i18n/src/messages.ts
git commit -m "feat(loyalty): operator owner loyalty-settings workspace + nav + i18n"
```

---

## Final verification

- [ ] **Server:** `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj` — all green.
- [ ] **Shell:** `cd src/AFK4.Player.Shell.Web && /home/fedya/.bun/bin/bun test && /home/fedya/.bun/bin/bun run build` — green.
- [ ] **Operator:** `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test && /home/fedya/.bun/bin/bun run build` — green.
- [ ] **i18n:** `cd packages/i18n && /home/fedya/.bun/bin/bun test` — green.
- [ ] Dispatch a final cross-cutting code review over the whole branch (spec compliance + quality), then use `superpowers:finishing-a-development-branch`.

## Spec coverage check

- Reward = wallet money (`cashback` ledger entry): L1, L3, L4, L5. ✓
- Sources top-up + shop, each toggle + percent: L2 (entity), L4 (top-up hook), L5 (shop hook). ✓
- Org-wide config: L2 (1-row entity), L6 (owner endpoints). ✓
- No tiers, flat configurable percent (basis points): L2/L3. ✓
- Accrual at success/terminal (top-up confirmed; order delivered), no cancel clawback: L4 (inside `TopUpWalletCoreAsync`, after idempotency short-circuit), L5 (only on `delivered`). ✓
- No active reversal clawback wiring: intentionally absent (per spec decision 6). ✓
- Player view (rates + earned + recent): L7 (endpoint), L8/L9/L10 (shell). ✓
- Owner config UI: L11/L12. ✓
- Tests across server/shell/operator: each task's test steps + Final verification. ✓
