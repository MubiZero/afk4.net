# Customer Portal "Writes" Backend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the player-scoped write endpoints of the customer portal — wallet top-up intent (create + list + operator-fulfil) and online reservations (create + list + player-cancel) — all wired into the existing Platform.Api behind `PlayerAuthenticationMiddleware` and `StaffAuthorizationService` respectively.

**Architecture:** Two loops, both tested TDD-first against EF InMemory.
- **Feature A — Wallet top-up intent:** New `PaymentIntentEntity` table + `PaymentIntents` DbSet + EF migration (real DB uses Postgres; InMemory for tests). Player creates a `pending` intent via `/api/me/wallet/top-up-intent`; the operator at the counter fulfils it via a staff endpoint that calls the existing `IBillingCommandService.TopUpWalletAsync`. Deduplication at fulfil is achieved by the `State == "fulfilled"` guard (idempotency-key level dedup lives inside `TopUpWalletAsync` itself). `FulfilledByLedgerEntryId` is left null in v1 because `TopUpWalletAsync` returns `BillingCommandServiceResult<WalletSummaryDto>` — a wallet summary — not the individual ledger entry id; comment in the entity explains why.
- **Feature B — Online reservations:** Adds `CreateOnlineAsync` and `CancelOnlineAsync` to `IReservationService`/`EfReservationService`, reusing every existing validation helper. New player-facing `POST /api/me/reservations`, `GET /api/me/reservations`, and `DELETE /api/me/reservations/{id}` endpoints inject identity from `PlayerContext` so the player cannot book as someone else.

**Tech Stack:** ASP.NET Core minimal APIs, EF Core 10 (in-memory for tests), xUnit + `WebApplicationFactory<Program>` (`PlatformApiFactory`). `TreatWarningsAsErrors=true` — add only usings you actually use. Money is `long` minor units end-to-end.

**Key finding on `TopUpWalletAsync` return type (drives Task A4 design):**
`IBillingCommandService.TopUpWalletAsync` returns `BillingCommandServiceResult<WalletSummaryDto>`. `WalletSummaryDto` is `(PlayerAccountId, MoneyDto WalletBalance, MoneyDto DebtBalance, IReadOnlyList<LedgerEntryDto> RecentEntries)`. It does NOT expose the newly created `LedgerEntryId` as a first-class field (only as the first element of `RecentEntries`, which is unreliable for correlation). Therefore `PaymentIntentEntity.FulfilledByLedgerEntryId` will be left `null` in v1 — the State flip from `pending` → `fulfilled` is the idempotency guard. A `// FulfilledByLedgerEntryId is left null (v1): TopUpWalletAsync returns WalletSummaryDto, not the created entry id.` comment documents this.

**Scope boundary:** This plan is writes only. Reads (dashboard, visits, purchases, profile) are already merged via the `customer-portal-reads` plan. Phone OTP and gateway payments are explicitly deferred.

---

## File Structure

**New files:**
- `src/AFK4.Platform.Api/Data/PaymentIntentEntity.cs` — new entity.
- `src/AFK4.Platform.Api/Data/Migrations/<timestamp>_AddPaymentIntents.cs` — EF migration (run `dotnet ef migrations add`; see Task A1).
- `src/AFK4.Shared.Contracts/Players/PlayerTopUpIntentRequest.cs` — player request contract.
- `src/AFK4.Shared.Contracts/Players/PlayerTopUpIntentDto.cs` — player-facing DTO.
- `src/AFK4.Shared.Contracts/Reservations/CreatePlayerReservationRequest.cs` — player reservation request.
- `src/AFK4.Shared.Contracts/Reservations/PlayerReservationDto.cs` — player-facing reservation DTO (no staff fields).
- `tests/AFK4.Platform.Api.Tests/PortalWritesEndpointTests.cs` — all new tests.

**Modified files:**
- `src/AFK4.Platform.Api/Data/PlatformDbContext.cs` — add `DbSet<PaymentIntentEntity> PaymentIntents` + OnModelCreating config.
- `src/AFK4.Platform.Api/Reservations/IReservationService.cs` — add `CreateOnlineAsync` and `CancelOnlineAsync`.
- `src/AFK4.Platform.Api/Reservations/EfReservationService.cs` — implement `CreateOnlineAsync` and `CancelOnlineAsync`.
- `src/AFK4.Platform.Api/Audit/AuditActionNames.cs` — add `FulfilPaymentIntent`.
- `src/AFK4.Platform.Api/Program.cs` — register 5 new endpoints.

**Conventions to mirror (verified ground truth):**
- Player endpoint auth: `playerContextAccessor.Current`; if `null` → `Results.Unauthorized()`. Then check `player.PhoneVerified` → else `Results.StatusCode(StatusCodes.Status403Forbidden)`. Add `.RequireRateLimiting("player-me")`. (See `Program.cs` line 654 `GET /api/me/profile`.)
- Staff endpoint pattern: `LoadPlayerScopedEndpointAsync(dbContext, staffContextAccessor, authorizationService, playerAccountId, StaffPermissionNames.TopUpWallet, ct)` → check `player.Result` → check `authorization.IsAllowed` → call service → `WriteAuditAsync` → `Results.Ok`. (See `Program.cs` line 6661 `POST /api/players/{id}/wallet/top-ups`.)
- `WriteAuditAsync(auditRecordWriter, orgId, branchId, actorStaffUserId, action, targetType, targetId, outcome, details, ct)` — 10-param signature (see `Program.cs` line 10770).
- `ToHttpResult(BillingCommandServiceResult<T>)` already defined at `Program.cs` line 10605.
- EF migrations: run `dotnet ef migrations add <PascalName> --project src/AFK4.Platform.Api --startup-project src/AFK4.Platform.Api`; commit the generated `.cs` + `.Designer.cs` files. Naming: `Add<PascalDescription>` matching recent examples (`AddPlayerTokens`, `AddPlayerMarketingOptIn`).

---

## Task A1: PaymentIntentEntity + DbSet + EF config + migration

**Files:**
- Create: `src/AFK4.Platform.Api/Data/PaymentIntentEntity.cs`
- Modify: `src/AFK4.Platform.Api/Data/PlatformDbContext.cs`
- Migration: run `dotnet ef migrations add AddPaymentIntents` (generates two files in `Data/Migrations/`)
- Test: `tests/AFK4.Platform.Api.Tests/PortalWritesEndpointTests.cs` (scaffold + round-trip test)

The entity persists wallet top-up intents from the portal. State lifecycle: `pending` → `fulfilled` (operator) or `cancelled`/`expired` (future; v1 just computes `IsExpired` as derived property in the DTO layer). `FulfilledByLedgerEntryId` is nullable and left null in v1 — see plan header for why.

- [ ] **Step 1: Write the failing test**

Create `tests/AFK4.Platform.Api.Tests/PortalWritesEndpointTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Players;
using AFK4.Shared.Contracts.Reservations;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public class PortalWritesEndpointTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private sealed record SeededPlayer(Guid OrgId, Guid BranchId, Guid PlayerId, string Phone);

    // Seeds an active player + a PIN credential with PhoneVerified=true.
    private static async Task<SeededPlayer> SeedPlayerAsync(
        PlatformApiFactory factory, string pin, bool phoneVerified = true)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var org = Guid.NewGuid();
        var branch = Guid.NewGuid();
        var player = Guid.NewGuid();
        var phone = $"+99290000{player.ToString("N")[..4]}";

        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = player,
            OrganizationId = org,
            HomeBranchId = branch,
            DisplayName = "Test Player",
            PhoneNumber = phone,
            PreferredLocale = "ru",
            MarketingOptIn = false,
            IsActive = true,
            CreatedAtUtc = Now
        });
        var credential = new PlayerCredentialEntity
        {
            PlayerCredentialId = Guid.NewGuid(),
            PlayerAccountId = player,
            OrganizationId = org,
            PhoneVerified = phoneVerified,
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        };
        credential.PasswordHash = new PasswordHasher<PlayerCredentialEntity>()
            .HashPassword(credential, pin);
        db.PlayerCredentials.Add(credential);
        await db.SaveChangesAsync();
        return new SeededPlayer(org, branch, player, phone);
    }

    private static async Task AuthenticateAsync(
        HttpClient client, Guid orgId, string phone, string pin)
    {
        var signIn = await client.PostAsJsonAsync(
            "/api/public/player/sign-in",
            new PlayerSignInRequest(orgId, phone, pin));
        signIn.EnsureSuccessStatusCode();
        var tokens = await signIn.Content.ReadFromJsonAsync<PlayerSignInResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
    }

    // Seeds an open shift for the staff-authenticated client (used in operator tests).
    private static async Task SeedOpenShiftAsync(PlatformApiFactory factory, Guid orgId, Guid branchId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.Shifts.Add(new ShiftEntity
        {
            ShiftId = Guid.NewGuid(),
            OrganizationId = orgId,
            BranchId = branchId,
            OpenedByStaffUserId = TestIds.TechnicianStaffUserId,
            State = ShiftStateNames.Open,
            CurrencyCode = "TJS",
            StartingCashMinorUnits = 50_000,
            CountedCashMinorUnits = 0,
            ExpectedCashMinorUnits = 0,
            DifferenceMinorUnits = 0,
            OpeningNote = "test shift",
            ClosingNote = string.Empty,
            OpenedAtUtc = Now
        });
        await db.SaveChangesAsync();
    }

    // ---- A1: round-trip persist test ----

    [Fact]
    public async Task PaymentIntentEntity_CanBePersistedAndReloaded()
    {
        await using var factory = new PlatformApiFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var intentId = Guid.NewGuid();
        db.PaymentIntents.Add(new PaymentIntentEntity
        {
            PaymentIntentId = intentId,
            PlayerAccountId = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            BranchId = Guid.NewGuid(),
            AmountMinorUnits = 5_000,
            CurrencyCode = "TJS",
            Purpose = "wallet_topup",
            State = "pending",
            Method = "counter",
            FulfilledByLedgerEntryId = null,
            CreatedAtUtc = Now,
            FulfilledAtUtc = null
        });
        await db.SaveChangesAsync();

        var loaded = await db.PaymentIntents.FindAsync(intentId);
        Assert.NotNull(loaded);
        Assert.Equal(5_000, loaded!.AmountMinorUnits);
        Assert.Equal("pending", loaded.State);
        Assert.Equal("wallet_topup", loaded.Purpose);
        Assert.Equal("counter", loaded.Method);
        Assert.Null(loaded.FulfilledByLedgerEntryId);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~PortalWritesEndpointTests.PaymentIntentEntity_CanBePersistedAndReloaded
```
Expected: FAIL — `PaymentIntentEntity` and `PaymentIntents` DbSet do not exist (compile error).

- [ ] **Step 3: Write the entity**

`src/AFK4.Platform.Api/Data/PaymentIntentEntity.cs`:

```csharp
namespace AFK4.Platform.Api.Data;

public sealed class PaymentIntentEntity
{
    public Guid PaymentIntentId { get; set; }

    public Guid PlayerAccountId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public long AmountMinorUnits { get; set; }

    public string CurrencyCode { get; set; } = "TJS";

    public string Purpose { get; set; } = "wallet_topup";

    // pending | fulfilled | cancelled | expired
    public string State { get; set; } = "pending";

    // counter (v1) | gateway (future)
    public string Method { get; set; } = "counter";

    // FulfilledByLedgerEntryId is left null (v1): TopUpWalletAsync returns
    // WalletSummaryDto, not the created ledger entry id. The State flip is
    // the idempotency guard.
    public Guid? FulfilledByLedgerEntryId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? FulfilledAtUtc { get; set; }
}
```

- [ ] **Step 4: Register the DbSet and EF config**

In `src/AFK4.Platform.Api/Data/PlatformDbContext.cs`, add the DbSet after the existing `ReservationEntity` line (line 91):

```csharp
    public DbSet<PaymentIntentEntity> PaymentIntents => Set<PaymentIntentEntity>();
```

In `OnModelCreating`, add the config block after the `ReservationEntity` config block (after line 762):

```csharp
        modelBuilder.Entity<PaymentIntentEntity>(entity =>
        {
            entity.ToTable("payment_intents");
            entity.HasKey(intent => intent.PaymentIntentId);
            entity.Property(intent => intent.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(intent => intent.Purpose).HasMaxLength(32).IsRequired();
            entity.Property(intent => intent.State).HasMaxLength(32).IsRequired();
            entity.Property(intent => intent.Method).HasMaxLength(32).IsRequired();
            entity.HasIndex(intent => intent.PlayerAccountId);
            entity.HasIndex(intent => new { intent.BranchId, intent.State });
        });
```

- [ ] **Step 5: Generate the EF migration**

> **Env note:** Use the bun-absolute-path-safe equivalent of `dotnet ef`. This project has no `dotnet ef` quirk but runs on WSL2 — use the full path if `dotnet` is not on `$PATH`.

```bash
dotnet ef migrations add AddPaymentIntents \
  --project src/AFK4.Platform.Api \
  --startup-project src/AFK4.Platform.Api
```

This generates:
- `src/AFK4.Platform.Api/Data/Migrations/<timestamp>_AddPaymentIntents.cs`
- `src/AFK4.Platform.Api/Data/Migrations/<timestamp>_AddPaymentIntents.Designer.cs`

Verify the generated `Up` method creates `payment_intents` with the expected columns and indexes.

- [ ] **Step 6: Run test to verify it passes**

```bash
dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~PortalWritesEndpointTests.PaymentIntentEntity_CanBePersistedAndReloaded
```
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add \
  src/AFK4.Platform.Api/Data/PaymentIntentEntity.cs \
  src/AFK4.Platform.Api/Data/PlatformDbContext.cs \
  src/AFK4.Platform.Api/Data/Migrations/ \
  tests/AFK4.Platform.Api.Tests/PortalWritesEndpointTests.cs
git commit -m "feat(portal): PaymentIntentEntity + DbSet + migration"
```

---

## Task A2: POST /api/me/wallet/top-up-intent (player creates pending intent)

**Files:**
- Create: `src/AFK4.Shared.Contracts/Players/PlayerTopUpIntentRequest.cs`
- Create: `src/AFK4.Shared.Contracts/Players/PlayerTopUpIntentDto.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs` (add endpoint)
- Test: `tests/AFK4.Platform.Api.Tests/PortalWritesEndpointTests.cs`

D8 gate: `player.PhoneVerified == false` → 403. BranchId = `PlayerAccountEntity.HomeBranchId` (confirmed non-nullable in code: `public Guid HomeBranchId { get; set; }`). Amount > 0 required. CurrencyCode defaults to "TJS".

- [ ] **Step 1: Write the failing tests**

Add to `PortalWritesEndpointTests.cs`:

```csharp
    [Fact]
    public async Task CreateTopUpIntent_WithVerifiedPhone_CreatesPendingIntent()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, p.Phone, "1234");

        var response = await client.PostAsJsonAsync(
            "/api/me/wallet/top-up-intent",
            new PlayerTopUpIntentRequest(10_000, null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<PlayerTopUpIntentDto>();
        Assert.NotNull(dto);
        Assert.Equal(10_000, dto!.AmountMinorUnits);
        Assert.Equal("TJS", dto.CurrencyCode);
        Assert.Equal("pending", dto.State);
        Assert.Equal("wallet_topup", dto.Purpose);
        Assert.Equal("counter", dto.Method);
        Assert.False(dto.IsExpired);
        Assert.Null(dto.FulfilledAtUtc);

        // Verify persisted to DB
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var intent = await db.PaymentIntents.FindAsync(dto.PaymentIntentId);
        Assert.NotNull(intent);
        Assert.Equal(p.PlayerId, intent!.PlayerAccountId);
        Assert.Equal(p.BranchId, intent.BranchId);
    }

    [Fact]
    public async Task CreateTopUpIntent_WithExplicitCurrency_UsesThatCurrency()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, p.Phone, "1234");

        var response = await client.PostAsJsonAsync(
            "/api/me/wallet/top-up-intent",
            new PlayerTopUpIntentRequest(5_000, "TJS"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<PlayerTopUpIntentDto>();
        Assert.Equal("TJS", dto!.CurrencyCode);
    }

    [Fact]
    public async Task CreateTopUpIntent_WithUnverifiedPhone_Returns403()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234", phoneVerified: false);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, p.Phone, "1234");

        var response = await client.PostAsJsonAsync(
            "/api/me/wallet/top-up-intent",
            new PlayerTopUpIntentRequest(10_000, null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateTopUpIntent_WithZeroAmount_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, p.Phone, "1234");

        var response = await client.PostAsJsonAsync(
            "/api/me/wallet/top-up-intent",
            new PlayerTopUpIntentRequest(0, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateTopUpIntent_WithNegativeAmount_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, p.Phone, "1234");

        var response = await client.PostAsJsonAsync(
            "/api/me/wallet/top-up-intent",
            new PlayerTopUpIntentRequest(-100, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateTopUpIntent_WithoutToken_Returns401()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/me/wallet/top-up-intent",
            new PlayerTopUpIntentRequest(10_000, null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateTopUpIntent_Isolation_IntentTiedToTokenPlayer()
    {
        // Two different players; each creates an intent; each sees only their own.
        await using var factory = new PlatformApiFactory();
        var p1 = await SeedPlayerAsync(factory, "1111");
        var p2 = await SeedPlayerAsync(factory, "2222");

        using var client1 = factory.CreateClient();
        await AuthenticateAsync(client1, p1.OrgId, p1.Phone, "1111");
        var r1 = await client1.PostAsJsonAsync(
            "/api/me/wallet/top-up-intent",
            new PlayerTopUpIntentRequest(3_000, null));
        var dto1 = await r1.Content.ReadFromJsonAsync<PlayerTopUpIntentDto>();

        using var client2 = factory.CreateClient();
        await AuthenticateAsync(client2, p2.OrgId, p2.Phone, "2222");
        var r2 = await client2.PostAsJsonAsync(
            "/api/me/wallet/top-up-intent",
            new PlayerTopUpIntentRequest(7_000, null));
        var dto2 = await r2.Content.ReadFromJsonAsync<PlayerTopUpIntentDto>();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var intent1 = await db.PaymentIntents.FindAsync(dto1!.PaymentIntentId);
        var intent2 = await db.PaymentIntents.FindAsync(dto2!.PaymentIntentId);
        Assert.Equal(p1.PlayerId, intent1!.PlayerAccountId);
        Assert.Equal(p2.PlayerId, intent2!.PlayerAccountId);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~PortalWritesEndpointTests.CreateTopUpIntent
```
Expected: FAIL — contracts and endpoint missing.

- [ ] **Step 3: Write the contracts**

`src/AFK4.Shared.Contracts/Players/PlayerTopUpIntentRequest.cs`:

```csharp
namespace AFK4.Shared.Contracts.Players;

// Player requests a wallet top-up at the counter.
// CurrencyCode defaults to "TJS" when null or blank.
public sealed record PlayerTopUpIntentRequest(
    long AmountMinorUnits,
    string? CurrencyCode);
```

`src/AFK4.Shared.Contracts/Players/PlayerTopUpIntentDto.cs`:

```csharp
using System;

namespace AFK4.Shared.Contracts.Players;

public sealed record PlayerTopUpIntentDto(
    Guid PaymentIntentId,
    long AmountMinorUnits,
    string CurrencyCode,
    string State,
    string Purpose,
    string Method,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? FulfilledAtUtc,
    bool IsExpired);
```

- [ ] **Step 4: Register the endpoint**

In `Program.cs`, after the `GET /api/me/purchases` endpoint (after line 792), add:

```csharp
app.MapPost("/api/me/wallet/top-up-intent", async (
    PlayerTopUpIntentRequest request,
    IPlayerContextAccessor playerContextAccessor,
    PlatformDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var player = playerContextAccessor.Current;
    if (player is null)
    {
        return Results.Unauthorized();
    }

    // D8 gate: verified phone required for money actions.
    if (!player.PhoneVerified)
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (request.AmountMinorUnits <= 0)
    {
        return Results.BadRequest(new { Error = "Amount must be greater than zero." });
    }

    var account = await dbContext.PlayerAccounts.SingleOrDefaultAsync(
        candidate => candidate.PlayerAccountId == player.PlayerAccountId, cancellationToken);
    if (account is null)
    {
        return Results.Unauthorized();
    }

    var currencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode)
        ? "TJS"
        : request.CurrencyCode.Trim().ToUpperInvariant();

    var now = DateTimeOffset.UtcNow;
    var intent = new PaymentIntentEntity
    {
        PaymentIntentId = Guid.NewGuid(),
        PlayerAccountId = player.PlayerAccountId,
        OrganizationId = player.OrganizationId,
        BranchId = account.HomeBranchId,
        AmountMinorUnits = request.AmountMinorUnits,
        CurrencyCode = currencyCode,
        Purpose = "wallet_topup",
        State = "pending",
        Method = "counter",
        FulfilledByLedgerEntryId = null,
        CreatedAtUtc = now,
        FulfilledAtUtc = null
    };

    dbContext.PaymentIntents.Add(intent);
    await dbContext.SaveChangesAsync(cancellationToken);

    return Results.Ok(new PlayerTopUpIntentDto(
        intent.PaymentIntentId,
        intent.AmountMinorUnits,
        intent.CurrencyCode,
        intent.State,
        intent.Purpose,
        intent.Method,
        intent.CreatedAtUtc,
        intent.FulfilledAtUtc,
        IsExpired: false));
}).RequireRateLimiting("player-me");
```

Add `using AFK4.Platform.Api.Data;` to `Program.cs` only if not already present (it is present).
Add `using AFK4.Shared.Contracts.Players;` only if not already present.

- [ ] **Step 5: Run tests to verify they pass**

```bash
dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~PortalWritesEndpointTests.CreateTopUpIntent
```
Expected: PASS (6 tests).

- [ ] **Step 6: Commit**

```bash
git add \
  src/AFK4.Shared.Contracts/Players/PlayerTopUpIntentRequest.cs \
  src/AFK4.Shared.Contracts/Players/PlayerTopUpIntentDto.cs \
  src/AFK4.Platform.Api/Program.cs \
  tests/AFK4.Platform.Api.Tests/PortalWritesEndpointTests.cs
git commit -m "feat(portal): POST /api/me/wallet/top-up-intent (D8 gate)"
```

---

## Task A3: GET /api/me/wallet/top-up-intents (player lists own intents)

**Files:**
- Modify: `src/AFK4.Platform.Api/Program.cs` (add endpoint)
- Test: `tests/AFK4.Platform.Api.Tests/PortalWritesEndpointTests.cs`

Returns the player's own intents, newest first. No cursor needed — the set is small by design. `IsExpired` is computed as `State == "pending" && now > CreatedAtUtc + 24h`. Returns `IReadOnlyList<PlayerTopUpIntentDto>` wrapped in a simple anonymous object consistent with how other endpoints return lists (or directly as an array — see below).

> The existing reads endpoints return `CursorPage<T>` for large sets. For this small personal set (no cursor required per spec), return directly as a JSON array. This is consistent with `WalletSummaryDto.RecentEntries` returning a list inline. If the codebase convention requires a wrapper, wrap as `new { Items = list }` — the test below checks `IReadOnlyList<PlayerTopUpIntentDto>` deserialized directly.

- [ ] **Step 1: Write the failing tests**

Add to `PortalWritesEndpointTests.cs`:

```csharp
    private static async Task<Guid> SeedPaymentIntentAsync(
        PlatformApiFactory factory,
        Guid orgId, Guid branchId, Guid playerId,
        string state, DateTimeOffset createdAtUtc, long amountMinorUnits = 5_000)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var intentId = Guid.NewGuid();
        db.PaymentIntents.Add(new PaymentIntentEntity
        {
            PaymentIntentId = intentId,
            PlayerAccountId = playerId,
            OrganizationId = orgId,
            BranchId = branchId,
            AmountMinorUnits = amountMinorUnits,
            CurrencyCode = "TJS",
            Purpose = "wallet_topup",
            State = state,
            Method = "counter",
            FulfilledByLedgerEntryId = null,
            CreatedAtUtc = createdAtUtc,
            FulfilledAtUtc = state == "fulfilled" ? createdAtUtc.AddMinutes(5) : null
        });
        await db.SaveChangesAsync();
        return intentId;
    }

    [Fact]
    public async Task ListTopUpIntents_ReturnsOwnIntents_NewestFirst()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        var t1 = DateTimeOffset.UtcNow.AddHours(-2);
        var t2 = DateTimeOffset.UtcNow.AddHours(-1);
        await SeedPaymentIntentAsync(factory, p.OrgId, p.BranchId, p.PlayerId, "pending", t1, 3_000);
        await SeedPaymentIntentAsync(factory, p.OrgId, p.BranchId, p.PlayerId, "fulfilled", t2, 7_000);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, p.Phone, "1234");

        var response = await client.GetAsync("/api/me/wallet/top-up-intents");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<IReadOnlyList<PlayerTopUpIntentDto>>();
        Assert.Equal(2, list!.Count);
        Assert.Equal(7_000, list[0].AmountMinorUnits);   // newest first (t2)
        Assert.Equal(3_000, list[1].AmountMinorUnits);   // oldest (t1)
    }

    [Fact]
    public async Task ListTopUpIntents_ExpiredFlag_ComputedCorrectly()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        // Pending but created >24h ago → IsExpired = true
        var oldTs = DateTimeOffset.UtcNow.AddHours(-25);
        await SeedPaymentIntentAsync(factory, p.OrgId, p.BranchId, p.PlayerId, "pending", oldTs, 1_000);
        // Fulfilled but old → IsExpired = false (not pending)
        var veryOldTs = DateTimeOffset.UtcNow.AddHours(-48);
        await SeedPaymentIntentAsync(factory, p.OrgId, p.BranchId, p.PlayerId, "fulfilled", veryOldTs, 2_000);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, p.Phone, "1234");

        var list = await (await client.GetAsync("/api/me/wallet/top-up-intents"))
            .Content.ReadFromJsonAsync<IReadOnlyList<PlayerTopUpIntentDto>>();

        var expired = list!.Single(x => x.AmountMinorUnits == 1_000);
        var fulfilled = list.Single(x => x.AmountMinorUnits == 2_000);
        Assert.True(expired.IsExpired);
        Assert.False(fulfilled.IsExpired);
    }

    [Fact]
    public async Task ListTopUpIntents_DoesNotReturnOtherPlayersIntents()
    {
        await using var factory = new PlatformApiFactory();
        var p1 = await SeedPlayerAsync(factory, "1111");
        var p2 = await SeedPlayerAsync(factory, "2222");
        await SeedPaymentIntentAsync(factory, p2.OrgId, p2.BranchId, p2.PlayerId, "pending", DateTimeOffset.UtcNow, 9_000);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p1.OrgId, p1.Phone, "1111");

        var list = await (await client.GetAsync("/api/me/wallet/top-up-intents"))
            .Content.ReadFromJsonAsync<IReadOnlyList<PlayerTopUpIntentDto>>();

        Assert.Empty(list!);
    }

    [Fact]
    public async Task ListTopUpIntents_WithoutToken_Returns401()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/me/wallet/top-up-intents");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~PortalWritesEndpointTests.ListTopUpIntents
```
Expected: FAIL — endpoint not mapped.

- [ ] **Step 3: Register the endpoint**

In `Program.cs`, after the `POST /api/me/wallet/top-up-intent` endpoint, add:

```csharp
app.MapGet("/api/me/wallet/top-up-intents", async (
    IPlayerContextAccessor playerContextAccessor,
    PlatformDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var player = playerContextAccessor.Current;
    if (player is null)
    {
        return Results.Unauthorized();
    }

    var now = DateTimeOffset.UtcNow;
    var expiryCutoff = now.AddHours(-24);

    var intents = await dbContext.PaymentIntents
        .AsNoTracking()
        .Where(intent => intent.PlayerAccountId == player.PlayerAccountId)
        .OrderByDescending(intent => intent.CreatedAtUtc)
        .ToListAsync(cancellationToken);

    var dtos = intents.Select(intent => new PlayerTopUpIntentDto(
        intent.PaymentIntentId,
        intent.AmountMinorUnits,
        intent.CurrencyCode,
        intent.State,
        intent.Purpose,
        intent.Method,
        intent.CreatedAtUtc,
        intent.FulfilledAtUtc,
        IsExpired: intent.State == "pending" && intent.CreatedAtUtc < expiryCutoff))
        .ToList();

    return Results.Ok(dtos);
}).RequireRateLimiting("player-me");
```

> **Env note:** EF InMemory supports `.OrderByDescending` on `DateTimeOffset`. The expiry computation is done in memory after materialising the list — do not push `expiryCutoff` comparison into the EF query (`TreatWarningsAsErrors` will reject LINQ it cannot translate in InMemory mode if `EnableSensitiveDataLogging` is on; stay safe by keeping it in-process).

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~PortalWritesEndpointTests.ListTopUpIntents
```
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add \
  src/AFK4.Platform.Api/Program.cs \
  tests/AFK4.Platform.Api.Tests/PortalWritesEndpointTests.cs
git commit -m "feat(portal): GET /api/me/wallet/top-up-intents (own intents + expired flag)"
```

---

## Task A4: POST /api/wallet/top-up-intents/{intentId}/fulfil (operator fulfils intent)

**Files:**
- Modify: `src/AFK4.Platform.Api/Audit/AuditActionNames.cs` (add `FulfilPaymentIntent`)
- Modify: `src/AFK4.Platform.Api/Program.cs` (add staff endpoint)
- Test: `tests/AFK4.Platform.Api.Tests/PortalWritesEndpointTests.cs`

Staff endpoint: resolves staff context, checks `StaffPermissionNames.TopUpWallet` on `intent.BranchId`, writes audit. Idempotency guard: `State == "fulfilled"` → return current state (no double credit). Expired: `State == "pending" && CreatedAtUtc < now - 24h` → 409. On success: calls `IBillingCommandService.TopUpWalletAsync`, flips state, saves. `FulfilledByLedgerEntryId` stays null — see plan header.

**IMPORTANT for the implementer:** `TopUpWalletAsync` signature (verified from `IBillingCommandService.cs`):
```csharp
Task<BillingCommandServiceResult<WalletSummaryDto>> TopUpWalletAsync(
    Guid playerAccountId,
    Guid branchId,
    Guid actorStaffUserId,
    TopUpWalletRequest request,      // (Guid OrganizationId, MoneyDto Amount, string Reason, string IdempotencyKey)
    CancellationToken cancellationToken)
```
Returns `WalletSummaryDto` — NOT the new entry id. See plan header for consequence.

- [ ] **Step 1: Add the audit action name**

In `src/AFK4.Platform.Api/Audit/AuditActionNames.cs`, add before the last `}`:

```csharp
    public const string FulfilPaymentIntent = "billing.payment_intent.fulfil";
```

- [ ] **Step 2: Write the failing tests**

Add to `PortalWritesEndpointTests.cs`:

```csharp
    // Seeds a staff user (cashier) and returns the factory with that staff token set.
    // Reuses the existing StaffAuthTestHelper.AuthorizeAsAsync pattern.
    // The BranchId used is TestIds.BranchId from the shared staff seed — but our player
    // intents are on dynamically-created branches. We need the staff to be at the player's
    // branch. Simplest approach: seed the intent using TestIds.OrganizationId / TestIds.BranchId
    // and use the seeded player account at that branch.

    private static async Task<(SeededPlayer Player, Guid IntentId)> SeedFulfilScenarioAsync(
        PlatformApiFactory factory, string state = "pending", int createdHoursAgo = 1)
    {
        // Player lives at TestIds.BranchId (the branch the staff cashier is authorised for)
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var playerId = Guid.NewGuid();
        var phone = $"+99291000{playerId.ToString("N")[..4]}";
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = playerId,
            OrganizationId = TestIds.OrganizationId,
            HomeBranchId = TestIds.BranchId,
            DisplayName = "Intent Player",
            PhoneNumber = phone,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        var credential = new PlayerCredentialEntity
        {
            PlayerCredentialId = Guid.NewGuid(),
            PlayerAccountId = playerId,
            OrganizationId = TestIds.OrganizationId,
            PhoneVerified = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        credential.PasswordHash = new PasswordHasher<PlayerCredentialEntity>()
            .HashPassword(credential, "9999");
        db.PlayerCredentials.Add(credential);

        var intentId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow.AddHours(-createdHoursAgo);
        db.PaymentIntents.Add(new PaymentIntentEntity
        {
            PaymentIntentId = intentId,
            PlayerAccountId = playerId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            AmountMinorUnits = 5_000,
            CurrencyCode = "TJS",
            Purpose = "wallet_topup",
            State = state,
            Method = "counter",
            FulfilledByLedgerEntryId = null,
            CreatedAtUtc = createdAt,
            FulfilledAtUtc = state == "fulfilled" ? createdAt.AddMinutes(5) : null
        });
        await db.SaveChangesAsync();

        var p = new SeededPlayer(TestIds.OrganizationId, TestIds.BranchId, playerId, phone);
        return (p, intentId);
    }

    [Fact]
    public async Task FulfilIntent_WithCashier_WritesWalletCreditAndFlipsToFulfilled()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);
        var (p, intentId) = await SeedFulfilScenarioAsync(factory);
        await SeedOpenShiftAsync(factory, TestIds.OrganizationId, TestIds.BranchId);

        var response = await client.PostAsJsonAsync(
            $"/api/wallet/top-up-intents/{intentId}/fulfil",
            new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<PlayerTopUpIntentDto>();
        Assert.NotNull(dto);
        Assert.Equal("fulfilled", dto!.State);
        Assert.NotNull(dto.FulfilledAtUtc);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        // Exactly one wallet top_up entry written for this player
        var entries = await db.LedgerEntries
            .Where(e => e.PlayerAccountId == p.PlayerId && e.EntryType == "top_up")
            .ToListAsync();
        Assert.Single(entries);
        Assert.Equal(5_000, entries[0].AmountMinorUnits);
    }

    [Fact]
    public async Task FulfilIntent_Idempotent_DoesNotDoubleCredit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);
        var (p, intentId) = await SeedFulfilScenarioAsync(factory, state: "pending");
        await SeedOpenShiftAsync(factory, TestIds.OrganizationId, TestIds.BranchId);

        // First fulfil
        var r1 = await client.PostAsJsonAsync($"/api/wallet/top-up-intents/{intentId}/fulfil", new { });
        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);

        // Second fulfil (already fulfilled) — must NOT write another credit
        var r2 = await client.PostAsJsonAsync($"/api/wallet/top-up-intents/{intentId}/fulfil", new { });
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var entries = await db.LedgerEntries
            .Where(e => e.PlayerAccountId == p.PlayerId && e.EntryType == "top_up")
            .ToListAsync();
        Assert.Single(entries);   // exactly one, not two
    }

    [Fact]
    public async Task FulfilIntent_WhenExpired_Returns409()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);
        // 25 hours old pending intent
        var (_, intentId) = await SeedFulfilScenarioAsync(factory, state: "pending", createdHoursAgo: 25);
        await SeedOpenShiftAsync(factory, TestIds.OrganizationId, TestIds.BranchId);

        var response = await client.PostAsJsonAsync(
            $"/api/wallet/top-up-intents/{intentId}/fulfil",
            new { });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task FulfilIntent_WhenNotFound_Returns404()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);

        var response = await client.PostAsJsonAsync(
            $"/api/wallet/top-up-intents/{Guid.NewGuid()}/fulfil",
            new { });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task FulfilIntent_RequiresTopUpWalletPermission()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        // Technician does not have TopUpWallet permission
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Technician);
        var (_, intentId) = await SeedFulfilScenarioAsync(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/wallet/top-up-intents/{intentId}/fulfil",
            new { });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
```

- [ ] **Step 3: Run tests to verify they fail**

```bash
dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~PortalWritesEndpointTests.FulfilIntent
```
Expected: FAIL — endpoint and audit action missing.

- [ ] **Step 4: Register the endpoint**

In `Program.cs`, after the `GET /api/me/wallet/top-up-intents` endpoint, add:

```csharp
app.MapPost("/api/wallet/top-up-intents/{intentId:guid}/fulfil", async (
    Guid intentId,
    IStaffContextAccessor staffContextAccessor,
    StaffAuthorizationService authorizationService,
    IAuditRecordWriter auditRecordWriter,
    IBillingCommandService billingCommandService,
    PlatformDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    if (staffContextAccessor.Current is null)
    {
        return Results.Unauthorized();
    }

    var staffContext = staffContextAccessor.Current;

    var intent = await dbContext.PaymentIntents
        .SingleOrDefaultAsync(
            candidate =>
                candidate.OrganizationId == staffContext.OrganizationId &&
                candidate.PaymentIntentId == intentId,
            cancellationToken);

    if (intent is null)
    {
        return Results.NotFound(new { Error = "Payment intent was not found." });
    }

    var authorization = await authorizationService.RequireBranchPermissionAsync(
        intent.BranchId,
        StaffPermissionNames.TopUpWallet,
        cancellationToken);

    if (!authorization.IsAuthenticated)
    {
        return Results.Unauthorized();
    }

    if (!authorization.IsAllowed)
    {
        await WriteAuditAsync(
            auditRecordWriter,
            staffContext.OrganizationId,
            intent.BranchId,
            staffContext.StaffUserId,
            AuditActionNames.FulfilPaymentIntent,
            "PaymentIntent",
            intentId.ToString("D"),
            AuditOutcome.Denied,
            new { intent.AmountMinorUnits, authorization.DenialReason },
            cancellationToken);

        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    // Idempotency guard: already fulfilled → return current state, no second credit.
    if (intent.State == "fulfilled")
    {
        var expiryCutoffForFulfilled = DateTimeOffset.UtcNow.AddHours(-24);
        return Results.Ok(new PlayerTopUpIntentDto(
            intent.PaymentIntentId,
            intent.AmountMinorUnits,
            intent.CurrencyCode,
            intent.State,
            intent.Purpose,
            intent.Method,
            intent.CreatedAtUtc,
            intent.FulfilledAtUtc,
            IsExpired: false));
    }

    // Expiry guard: pending but >24h old → 409 Conflict.
    if (intent.State == "pending" && intent.CreatedAtUtc < DateTimeOffset.UtcNow.AddHours(-24))
    {
        return Results.Conflict(new { Error = "Payment intent has expired." });
    }

    var topUpRequest = new TopUpWalletRequest(
        intent.OrganizationId,
        new MoneyDto(intent.CurrencyCode, intent.AmountMinorUnits),
        "wallet top-up via portal intent",
        intent.PaymentIntentId.ToString("N"));

    var billingResult = await billingCommandService.TopUpWalletAsync(
        intent.PlayerAccountId,
        intent.BranchId,
        staffContext.StaffUserId,
        topUpRequest,
        cancellationToken);

    if (!billingResult.Succeeded)
    {
        return ToHttpResult(billingResult);
    }

    var now = DateTimeOffset.UtcNow;
    intent.State = "fulfilled";
    intent.FulfilledAtUtc = now;
    // FulfilledByLedgerEntryId left null (v1): TopUpWalletAsync returns WalletSummaryDto,
    // not the created ledger entry id. The State guard above is the dedup, not the ledger link.
    await dbContext.SaveChangesAsync(cancellationToken);

    await WriteAuditAsync(
        auditRecordWriter,
        staffContext.OrganizationId,
        intent.BranchId,
        staffContext.StaffUserId,
        AuditActionNames.FulfilPaymentIntent,
        "PaymentIntent",
        intentId.ToString("D"),
        AuditOutcome.Succeeded,
        new { intent.AmountMinorUnits, intent.CurrencyCode },
        cancellationToken);

    return Results.Ok(new PlayerTopUpIntentDto(
        intent.PaymentIntentId,
        intent.AmountMinorUnits,
        intent.CurrencyCode,
        intent.State,
        intent.Purpose,
        intent.Method,
        intent.CreatedAtUtc,
        intent.FulfilledAtUtc,
        IsExpired: false));
});
```

> **Env note (no `.RequireRateLimiting` here):** This is a staff endpoint, not a player-me endpoint — omit `.RequireRateLimiting("player-me")`. Staff endpoints in this project have no explicit rate-limit attribute (consistent with the existing `POST /api/players/{id}/wallet/top-ups` pattern at line 6661).

Add `using AFK4.Shared.Contracts.Billing;` and `using AFK4.Shared.Contracts.Players;` to `Program.cs` only if not already present.

- [ ] **Step 5: Run tests to verify they pass**

```bash
dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~PortalWritesEndpointTests.FulfilIntent
```
Expected: PASS (5 tests).

- [ ] **Step 6: Commit**

```bash
git add \
  src/AFK4.Platform.Api/Audit/AuditActionNames.cs \
  src/AFK4.Platform.Api/Program.cs \
  tests/AFK4.Platform.Api.Tests/PortalWritesEndpointTests.cs
git commit -m "feat(portal): POST /api/wallet/top-up-intents/{id}/fulfil (operator, idempotent)"
```

---

## Task B1: IReservationService.CreateOnlineAsync + CancelOnlineAsync

**Files:**
- Create: `src/AFK4.Shared.Contracts/Reservations/CreatePlayerReservationRequest.cs`
- Modify: `src/AFK4.Platform.Api/Reservations/IReservationService.cs`
- Modify: `src/AFK4.Platform.Api/Reservations/EfReservationService.cs`
- Test: `tests/AFK4.Platform.Api.Tests/PortalWritesEndpointTests.cs`

`CreateOnlineAsync` builds a `ReservationEntity` with `Source = ReservationSourceNames.Online` (→ `State = ReservationStateNames.Pending`), `CreatedByStaffUserId = Guid.Empty` (self-service sentinel), and reuses `ValidateReservationShapeAsync` + `FindConflictAsync`.

`CancelOnlineAsync` verifies the reservation belongs to the player (else `Missing`) then calls the existing cancel logic with `Guid.Empty` as the actor sentinel.

> **Critical note on `CreateOnlineAsync` signature:** `ValidateReservationShapeAsync` is private to `EfReservationService`. `CreateOnlineAsync` is a new method on the same class so it can call private helpers directly. The public `CreateAsync(Guid branchId, Guid actorStaffUserId, CreateReservationRequest request, ct)` already exists — we add a new overload rather than modifying the existing one.

- [ ] **Step 1: Write the contract**

`src/AFK4.Shared.Contracts/Reservations/CreatePlayerReservationRequest.cs`:

```csharp
using System;

namespace AFK4.Shared.Contracts.Reservations;

// Player-initiated reservation request.
// SeatId is optional (unassigned reservation). StartsAtUtc and EndsAtUtc are
// absolute — the service derives DurationMinutes internally.
public sealed record CreatePlayerReservationRequest(
    Guid? SeatId,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    string? Note);
```

- [ ] **Step 2: Write the failing tests**

Add to `PortalWritesEndpointTests.cs`:

```csharp
    private static async Task<(Guid SeatId, Guid ZoneId)> SeedSeatAsync(
        PlatformApiFactory factory, Guid orgId, Guid branchId, string name = "PC-01")
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var zoneId = Guid.NewGuid();
        var seatId = Guid.NewGuid();
        db.Zones.Add(new ZoneEntity
        {
            ZoneId = zoneId,
            OrganizationId = orgId,
            BranchId = branchId,
            Name = "Main",
            SortOrder = 10,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        db.Seats.Add(new SeatEntity
        {
            SeatId = seatId,
            OrganizationId = orgId,
            BranchId = branchId,
            ZoneId = zoneId,
            Name = name,
            SortOrder = 10,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        return (seatId, zoneId);
    }

    [Fact]
    public async Task CreateOnlineAsync_CreatesReservation_WithPendingState_AndOnlineSource()
    {
        await using var factory = new PlatformApiFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var orgId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var playerId = Guid.NewGuid();

        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = playerId,
            OrganizationId = orgId,
            HomeBranchId = branchId,
            DisplayName = "Online Player",
            PhoneNumber = "+992911000001",
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var svc = scope.ServiceProvider.GetRequiredService<IReservationService>();
        var startsAt = DateTimeOffset.UtcNow.AddHours(2);
        var endsAt = startsAt.AddHours(1);

        var result = await svc.CreateOnlineAsync(
            playerId,
            orgId,
            branchId,
            new CreatePlayerReservationRequest(null, startsAt, endsAt, "online request"),
            default);

        Assert.True(result.Succeeded);
        Assert.Equal(ReservationStateNames.Pending, result.Response!.State);
        Assert.Equal(ReservationSourceNames.Online, result.Response.Source);
        Assert.Equal(playerId, result.Response.PlayerAccountId);
        Assert.Equal("Online Player", result.Response.CustomerName);
    }

    [Fact]
    public async Task CreateOnlineAsync_WithConflictingSeat_ReturnsConflict()
    {
        await using var factory = new PlatformApiFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var orgId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var seatId = Guid.NewGuid();

        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = playerId, OrganizationId = orgId, HomeBranchId = branchId,
            DisplayName = "P", PhoneNumber = "+992911000002", IsActive = true, CreatedAtUtc = DateTimeOffset.UtcNow
        });
        db.Zones.Add(new ZoneEntity
        {
            ZoneId = Guid.NewGuid(), OrganizationId = orgId, BranchId = branchId,
            Name = "Z", SortOrder = 1, CreatedAtUtc = DateTimeOffset.UtcNow
        });
        var zone = await db.Zones.FirstAsync(z => z.OrganizationId == orgId);
        db.Seats.Add(new SeatEntity
        {
            SeatId = seatId, OrganizationId = orgId, BranchId = branchId,
            ZoneId = zone.ZoneId, Name = "S1", SortOrder = 1, CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var svc = scope.ServiceProvider.GetRequiredService<IReservationService>();
        var startsAt = DateTimeOffset.UtcNow.AddHours(2);
        var endsAt = startsAt.AddHours(1);
        var req = new CreatePlayerReservationRequest(seatId, startsAt, endsAt, null);

        // First booking succeeds
        var r1 = await svc.CreateOnlineAsync(playerId, orgId, branchId, req, default);
        Assert.True(r1.Succeeded);

        // Second overlapping booking conflicts
        var r2 = await svc.CreateOnlineAsync(playerId, orgId, branchId,
            new CreatePlayerReservationRequest(seatId, startsAt.AddMinutes(30), endsAt, null),
            default);
        Assert.True(r2.Conflict);
    }

    [Fact]
    public async Task CancelOnlineAsync_OwnReservation_FlipsToCancelled()
    {
        await using var factory = new PlatformApiFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var orgId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var playerId = Guid.NewGuid();

        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = playerId, OrganizationId = orgId, HomeBranchId = branchId,
            DisplayName = "Canceller", PhoneNumber = "+992911000003", IsActive = true, CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var svc = scope.ServiceProvider.GetRequiredService<IReservationService>();
        var created = await svc.CreateOnlineAsync(
            playerId, orgId, branchId,
            new CreatePlayerReservationRequest(null, DateTimeOffset.UtcNow.AddHours(3), DateTimeOffset.UtcNow.AddHours(4), null),
            default);
        Assert.True(created.Succeeded);

        var cancelled = await svc.CancelOnlineAsync(created.Response!.ReservationId, playerId, default);
        Assert.True(cancelled.Succeeded);
        Assert.Equal(ReservationStateNames.Cancelled, cancelled.Response!.State);
    }

    [Fact]
    public async Task CancelOnlineAsync_ForeignReservation_ReturnsNotFound()
    {
        await using var factory = new PlatformApiFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var orgId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var otherPlayerId = Guid.NewGuid();

        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = ownerId, OrganizationId = orgId, HomeBranchId = branchId,
            DisplayName = "Owner", PhoneNumber = "+992911000004", IsActive = true, CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var svc = scope.ServiceProvider.GetRequiredService<IReservationService>();
        var created = await svc.CreateOnlineAsync(
            ownerId, orgId, branchId,
            new CreatePlayerReservationRequest(null, DateTimeOffset.UtcNow.AddHours(3), DateTimeOffset.UtcNow.AddHours(4), null),
            default);
        Assert.True(created.Succeeded);

        // otherPlayerId tries to cancel the owner's reservation
        var result = await svc.CancelOnlineAsync(created.Response!.ReservationId, otherPlayerId, default);
        Assert.True(result.NotFound);
    }
```

- [ ] **Step 3: Run tests to verify they fail**

```bash
dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~PortalWritesEndpointTests.CreateOnlineAsync\|PortalWritesEndpointTests.CancelOnlineAsync
```
Expected: FAIL — `CreateOnlineAsync`, `CancelOnlineAsync`, and `CreatePlayerReservationRequest` missing.

- [ ] **Step 4: Add the interface methods**

In `src/AFK4.Platform.Api/Reservations/IReservationService.cs`, add after `CancelAsync`:

```csharp
    Task<ReservationServiceResult<ReservationDto>> CreateOnlineAsync(
        Guid playerAccountId,
        Guid organizationId,
        Guid branchId,
        CreatePlayerReservationRequest request,
        CancellationToken cancellationToken);

    Task<ReservationServiceResult<ReservationDto>> CancelOnlineAsync(
        Guid reservationId,
        Guid playerAccountId,
        CancellationToken cancellationToken);
```

Also add `using AFK4.Shared.Contracts.Reservations;` to the interface file if `CreatePlayerReservationRequest` is in that namespace.

- [ ] **Step 5: Implement the service methods**

In `src/AFK4.Platform.Api/Reservations/EfReservationService.cs`, append before the last closing `}`:

```csharp
    public async Task<ReservationServiceResult<ReservationDto>> CreateOnlineAsync(
        Guid playerAccountId,
        Guid organizationId,
        Guid branchId,
        CreatePlayerReservationRequest request,
        CancellationToken cancellationToken)
    {
        if (request.StartsAtUtc >= request.EndsAtUtc)
        {
            return ReservationServiceResult<ReservationDto>.Invalid(
                "Reservation end time must be after start time.");
        }

        // Compute duration in minutes from the absolute times.
        var durationMinutes = (int)Math.Round((request.EndsAtUtc - request.StartsAtUtc).TotalMinutes);

        // Load the player's display name and phone for the reservation record.
        var account = await dbContext.PlayerAccounts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                player =>
                    player.OrganizationId == organizationId &&
                    player.HomeBranchId == branchId &&
                    player.PlayerAccountId == playerAccountId,
                cancellationToken);

        if (account is null)
        {
            return ReservationServiceResult<ReservationDto>.Invalid(
                "Player account was not found in this branch.");
        }

        var validation = await ValidateReservationShapeAsync(
            organizationId,
            branchId,
            playerAccountId,
            request.SeatId,
            account.DisplayName,
            request.StartsAtUtc,
            durationMinutes,
            ReservationSourceNames.Online,
            cancellationToken);

        if (validation is not null)
        {
            return ReservationServiceResult<ReservationDto>.Invalid(validation);
        }

        var endsAtUtc = request.StartsAtUtc.AddMinutes(durationMinutes);
        var conflict = await FindConflictAsync(
            organizationId,
            branchId,
            request.SeatId,
            request.StartsAtUtc,
            endsAtUtc,
            excludedReservationId: null,
            cancellationToken);

        if (conflict is not null)
        {
            return ReservationServiceResult<ReservationDto>.RequestConflict(conflict);
        }

        var now = timeProvider.GetUtcNow();
        var reservation = new ReservationEntity
        {
            ReservationId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            PlayerAccountId = playerAccountId,
            SeatId = request.SeatId,
            CustomerName = account.DisplayName,
            PhoneNumber = account.PhoneNumber,
            StartsAtUtc = request.StartsAtUtc,
            EndsAtUtc = endsAtUtc,
            // Online source → Pending state (same logic as CreateAsync)
            State = ReservationStateNames.Pending,
            Source = ReservationSourceNames.Online,
            Note = NormalizeText(request.Note),
            // Guid.Empty = self-service sentinel; no staff actor for online bookings.
            CreatedByStaffUserId = Guid.Empty,
            UpdatedByStaffUserId = Guid.Empty,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            CancelReason = string.Empty
        };

        dbContext.Reservations.Add(reservation);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ReservationServiceResult<ReservationDto>.Ok(
            (await ProjectAsync([reservation], cancellationToken))[0]);
    }

    public async Task<ReservationServiceResult<ReservationDto>> CancelOnlineAsync(
        Guid reservationId,
        Guid playerAccountId,
        CancellationToken cancellationToken)
    {
        var reservation = await dbContext.Reservations
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.ReservationId == reservationId &&
                    candidate.PlayerAccountId == playerAccountId,
                cancellationToken);

        // Return NotFound (not Forbidden) to avoid existence disclosure.
        if (reservation is null)
        {
            return ReservationServiceResult<ReservationDto>.Missing(
                "Reservation was not found.");
        }

        if (reservation.State is not ReservationStateNames.Pending and not ReservationStateNames.Confirmed and not ReservationStateNames.Cancelled)
        {
            return ReservationServiceResult<ReservationDto>.Invalid(
                "Only pending or confirmed reservations can be cancelled.");
        }

        if (reservation.State != ReservationStateNames.Cancelled)
        {
            var now = timeProvider.GetUtcNow();
            reservation.State = ReservationStateNames.Cancelled;
            reservation.CancelReason = "player-initiated";
            reservation.CancelledAtUtc = now;
            reservation.UpdatedByStaffUserId = Guid.Empty;
            reservation.UpdatedAtUtc = now;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return ReservationServiceResult<ReservationDto>.Ok(
            (await ProjectAsync([reservation], cancellationToken))[0]);
    }
```

- [ ] **Step 6: Run tests to verify they pass**

```bash
dotnet test tests/AFK4.Platform.Api.Tests --filter "FullyQualifiedName~PortalWritesEndpointTests.CreateOnlineAsync|PortalWritesEndpointTests.CancelOnlineAsync"
```
Expected: PASS (4 tests).

- [ ] **Step 7: Commit**

```bash
git add \
  src/AFK4.Shared.Contracts/Reservations/CreatePlayerReservationRequest.cs \
  src/AFK4.Platform.Api/Reservations/IReservationService.cs \
  src/AFK4.Platform.Api/Reservations/EfReservationService.cs \
  tests/AFK4.Platform.Api.Tests/PortalWritesEndpointTests.cs
git commit -m "feat(portal): IReservationService.CreateOnlineAsync + CancelOnlineAsync"
```

---

## Task B2: POST /api/me/reservations (player books online)

**Files:**
- Create: `src/AFK4.Shared.Contracts/Reservations/PlayerReservationDto.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs`
- Test: `tests/AFK4.Platform.Api.Tests/PortalWritesEndpointTests.cs`

D8 gate: `PhoneVerified == false` → 403. BranchId from `HomeBranchId` (non-nullable, so no null check needed). Identity injected from `PlayerContext` — the player cannot pass another player's id; the `CreateOnlineAsync` service method injects `CustomerName` and `PhoneNumber` from the account record. Validates `StartsAtUtc < EndsAtUtc` and `StartsAtUtc > UtcNow`. Returns `PlayerReservationDto` (no staff fields).

- [ ] **Step 1: Write the DTO**

`src/AFK4.Shared.Contracts/Reservations/PlayerReservationDto.cs`:

```csharp
using System;

namespace AFK4.Shared.Contracts.Reservations;

// Player-facing reservation view — no staff-only fields (no CustomerName separate from
// context, no CreatedByStaffUserId, no UpdatedBy, no ZoneName leak).
public sealed record PlayerReservationDto(
    Guid ReservationId,
    Guid? SeatId,
    string? SeatName,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    string State,
    string? Note);
```

- [ ] **Step 2: Write the failing tests**

Add to `PortalWritesEndpointTests.cs`:

```csharp
    [Fact]
    public async Task BookOnline_WithVerifiedPhone_CreatesPendingReservation()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        var (seatId, _) = await SeedSeatAsync(factory, p.OrgId, p.BranchId);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, p.Phone, "1234");

        var startsAt = DateTimeOffset.UtcNow.AddHours(2);
        var endsAt = startsAt.AddHours(1);
        var response = await client.PostAsJsonAsync(
            "/api/me/reservations",
            new CreatePlayerReservationRequest(seatId, startsAt, endsAt, "window seat please"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<PlayerReservationDto>();
        Assert.NotNull(dto);
        Assert.Equal("pending", dto!.State);
        Assert.Equal(seatId, dto.SeatId);
        Assert.Equal("PC-01", dto.SeatName);

        // Verify reservation belongs to the token's player, not a stranger
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var entity = await db.Reservations.FindAsync(dto.ReservationId);
        Assert.NotNull(entity);
        Assert.Equal(p.PlayerId, entity!.PlayerAccountId);
        Assert.Equal("Test Player", entity.CustomerName);
    }

    [Fact]
    public async Task BookOnline_WithUnverifiedPhone_Returns403()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234", phoneVerified: false);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, p.Phone, "1234");

        var startsAt = DateTimeOffset.UtcNow.AddHours(2);
        var response = await client.PostAsJsonAsync(
            "/api/me/reservations",
            new CreatePlayerReservationRequest(null, startsAt, startsAt.AddHours(1), null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task BookOnline_WithEndBeforeStart_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, p.Phone, "1234");

        var now = DateTimeOffset.UtcNow;
        var response = await client.PostAsJsonAsync(
            "/api/me/reservations",
            new CreatePlayerReservationRequest(null, now.AddHours(2), now.AddHours(1), null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BookOnline_WithStartInPast_Returns400()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, p.Phone, "1234");

        var past = DateTimeOffset.UtcNow.AddHours(-1);
        var response = await client.PostAsJsonAsync(
            "/api/me/reservations",
            new CreatePlayerReservationRequest(null, past, past.AddHours(1), null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BookOnline_WithOverlappingSeat_Returns409()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        var (seatId, _) = await SeedSeatAsync(factory, p.OrgId, p.BranchId);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, p.Phone, "1234");

        var startsAt = DateTimeOffset.UtcNow.AddHours(2);
        var endsAt = startsAt.AddHours(1);

        var r1 = await client.PostAsJsonAsync(
            "/api/me/reservations",
            new CreatePlayerReservationRequest(seatId, startsAt, endsAt, null));
        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);

        // Overlapping booking on the same seat
        var r2 = await client.PostAsJsonAsync(
            "/api/me/reservations",
            new CreatePlayerReservationRequest(seatId, startsAt.AddMinutes(30), endsAt.AddMinutes(30), null));
        Assert.Equal(HttpStatusCode.Conflict, r2.StatusCode);
    }

    [Fact]
    public async Task BookOnline_WithoutToken_Returns401()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var startsAt = DateTimeOffset.UtcNow.AddHours(2);
        var response = await client.PostAsJsonAsync(
            "/api/me/reservations",
            new CreatePlayerReservationRequest(null, startsAt, startsAt.AddHours(1), null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
```

- [ ] **Step 3: Run tests to verify they fail**

```bash
dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~PortalWritesEndpointTests.BookOnline
```
Expected: FAIL — DTO and endpoint missing.

- [ ] **Step 4: Add a local helper to convert `ReservationDto` → `PlayerReservationDto`**

Near the endpoint registration in `Program.cs`, add a static local or top-level function (mirror the existing `ToDto` helpers at the bottom of `Program.cs`):

```csharp
static PlayerReservationDto ToPlayerReservationDto(ReservationDto r) =>
    new(r.ReservationId, r.SeatId, r.SeatName, r.StartsAtUtc, r.EndsAtUtc, r.State, r.Note);
```

- [ ] **Step 5: Register the endpoint**

In `Program.cs`, after the `POST /api/me/wallet/top-up-intent` group, add:

```csharp
app.MapPost("/api/me/reservations", async (
    CreatePlayerReservationRequest request,
    IPlayerContextAccessor playerContextAccessor,
    IReservationService reservationService,
    PlatformDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var player = playerContextAccessor.Current;
    if (player is null)
    {
        return Results.Unauthorized();
    }

    // D8 gate: verified phone required for booking actions.
    if (!player.PhoneVerified)
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var now = DateTimeOffset.UtcNow;
    if (request.StartsAtUtc >= request.EndsAtUtc)
    {
        return Results.BadRequest(new { Error = "End time must be after start time." });
    }

    if (request.StartsAtUtc <= now)
    {
        return Results.BadRequest(new { Error = "Start time must be in the future." });
    }

    var result = await reservationService.CreateOnlineAsync(
        player.PlayerAccountId,
        player.OrganizationId,
        (await dbContext.PlayerAccounts
            .AsNoTracking()
            .Where(a => a.PlayerAccountId == player.PlayerAccountId)
            .Select(a => a.HomeBranchId)
            .SingleOrDefaultAsync(cancellationToken)),
        request,
        cancellationToken);

    if (!result.Succeeded)
    {
        if (result.Conflict)
        {
            return Results.Conflict(new { Error = result.Error });
        }

        return Results.BadRequest(new { Error = result.Error });
    }

    return Results.Ok(ToPlayerReservationDto(result.Response!));
}).RequireRateLimiting("player-me");
```

> **Note:** We load `HomeBranchId` inline via a small projection query rather than a full `PlayerAccountEntity` load, to keep the handler lean and avoid tracking an entity we don't modify. If this pattern feels inconsistent with the rest of `Program.cs` (which tends to use `SingleOrDefaultAsync` for full entities), switch to loading the full account and reusing it — but ensure you don't introduce a using conflict with the account null-check pattern above.

Add `using AFK4.Platform.Api.Reservations;` to `Program.cs` only if not already present.
Add `using AFK4.Shared.Contracts.Reservations;` only if not already present.

- [ ] **Step 6: Run tests to verify they pass**

```bash
dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~PortalWritesEndpointTests.BookOnline
```
Expected: PASS (6 tests).

- [ ] **Step 7: Commit**

```bash
git add \
  src/AFK4.Shared.Contracts/Reservations/PlayerReservationDto.cs \
  src/AFK4.Platform.Api/Program.cs \
  tests/AFK4.Platform.Api.Tests/PortalWritesEndpointTests.cs
git commit -m "feat(portal): POST /api/me/reservations (D8 gate, online booking)"
```

---

## Task B3: GET /api/me/reservations + DELETE /api/me/reservations/{id}

**Files:**
- Modify: `src/AFK4.Platform.Api/Program.cs`
- Test: `tests/AFK4.Platform.Api.Tests/PortalWritesEndpointTests.cs`

`GET /api/me/reservations` — player's own reservations via a simple `PlayerAccountId` filter, join seat name from `Seats` table, ordered upcoming first then recent (`StartsAtUtc DESC`). Returns `IReadOnlyList<PlayerReservationDto>`. No cursor needed for personal reservations — the set is small.

`DELETE /api/me/reservations/{reservationId}` — calls `CancelOnlineAsync(reservationId, player.PlayerAccountId, ct)`. `NotFound` result → 404 (no existence disclosure). `Invalid` result → 400. Success → 200 with the updated `PlayerReservationDto`.

- [ ] **Step 1: Write the failing tests**

Add to `PortalWritesEndpointTests.cs`:

```csharp
    [Fact]
    public async Task ListReservations_ReturnsOwnReservations_Only()
    {
        await using var factory = new PlatformApiFactory();
        var p1 = await SeedPlayerAsync(factory, "1111");
        var p2 = await SeedPlayerAsync(factory, "2222");
        var (seatId, _) = await SeedSeatAsync(factory, p1.OrgId, p1.BranchId);

        using var client1 = factory.CreateClient();
        await AuthenticateAsync(client1, p1.OrgId, p1.Phone, "1111");
        var startsAt = DateTimeOffset.UtcNow.AddHours(2);
        await client1.PostAsJsonAsync(
            "/api/me/reservations",
            new CreatePlayerReservationRequest(seatId, startsAt, startsAt.AddHours(1), null));

        using var client2 = factory.CreateClient();
        await AuthenticateAsync(client2, p2.OrgId, p2.Phone, "2222");
        // p2 has its own org/branch; list only its reservations
        var list2 = await (await client2.GetAsync("/api/me/reservations"))
            .Content.ReadFromJsonAsync<IReadOnlyList<PlayerReservationDto>>();
        Assert.Empty(list2!);

        var list1 = await (await client1.GetAsync("/api/me/reservations"))
            .Content.ReadFromJsonAsync<IReadOnlyList<PlayerReservationDto>>();
        Assert.Single(list1!);
        Assert.Equal(seatId, list1![0].SeatId);
        Assert.Equal("PC-01", list1[0].SeatName);
    }

    [Fact]
    public async Task ListReservations_WithoutToken_Returns401()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/me/reservations");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CancelReservation_OwnReservation_Returns200WithCancelledState()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, p.Phone, "1234");

        var startsAt = DateTimeOffset.UtcNow.AddHours(2);
        var created = await (await client.PostAsJsonAsync(
            "/api/me/reservations",
            new CreatePlayerReservationRequest(null, startsAt, startsAt.AddHours(1), null)))
            .Content.ReadFromJsonAsync<PlayerReservationDto>();

        var cancelResponse = await client.DeleteAsync($"/api/me/reservations/{created!.ReservationId}");

        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
        var dto = await cancelResponse.Content.ReadFromJsonAsync<PlayerReservationDto>();
        Assert.Equal("cancelled", dto!.State);
    }

    [Fact]
    public async Task CancelReservation_ForeignReservation_Returns404()
    {
        await using var factory = new PlatformApiFactory();
        var p1 = await SeedPlayerAsync(factory, "1111");
        var p2 = await SeedPlayerAsync(factory, "2222");

        using var client1 = factory.CreateClient();
        await AuthenticateAsync(client1, p1.OrgId, p1.Phone, "1111");
        var startsAt = DateTimeOffset.UtcNow.AddHours(2);
        var created = await (await client1.PostAsJsonAsync(
            "/api/me/reservations",
            new CreatePlayerReservationRequest(null, startsAt, startsAt.AddHours(1), null)))
            .Content.ReadFromJsonAsync<PlayerReservationDto>();

        // p2 tries to cancel p1's reservation
        using var client2 = factory.CreateClient();
        await AuthenticateAsync(client2, p2.OrgId, p2.Phone, "2222");
        var cancelResponse = await client2.DeleteAsync($"/api/me/reservations/{created!.ReservationId}");

        Assert.Equal(HttpStatusCode.NotFound, cancelResponse.StatusCode);
    }

    [Fact]
    public async Task CancelReservation_WithoutToken_Returns401()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync($"/api/me/reservations/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/AFK4.Platform.Api.Tests --filter "FullyQualifiedName~PortalWritesEndpointTests.ListReservations|PortalWritesEndpointTests.CancelReservation"
```
Expected: FAIL — endpoints not mapped.

- [ ] **Step 3: Register the endpoints**

In `Program.cs`, after `POST /api/me/reservations`, add:

```csharp
app.MapGet("/api/me/reservations", async (
    IPlayerContextAccessor playerContextAccessor,
    PlatformDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var player = playerContextAccessor.Current;
    if (player is null)
    {
        return Results.Unauthorized();
    }

    var reservations = await dbContext.Reservations
        .AsNoTracking()
        .Where(reservation => reservation.PlayerAccountId == player.PlayerAccountId)
        .OrderByDescending(reservation => reservation.StartsAtUtc)
        .ToListAsync(cancellationToken);

    var seatIds = reservations
        .Where(r => r.SeatId is not null)
        .Select(r => r.SeatId!.Value)
        .Distinct()
        .ToList();

    var seatNames = seatIds.Count == 0
        ? new Dictionary<Guid, string>()
        : await dbContext.Seats
            .AsNoTracking()
            .Where(seat => seatIds.Contains(seat.SeatId))
            .ToDictionaryAsync(seat => seat.SeatId, seat => seat.Name, cancellationToken);

    var dtos = reservations.Select(r => new PlayerReservationDto(
        r.ReservationId,
        r.SeatId,
        r.SeatId is not null ? seatNames.GetValueOrDefault(r.SeatId.Value) : null,
        r.StartsAtUtc,
        r.EndsAtUtc,
        r.State,
        string.IsNullOrEmpty(r.Note) ? null : r.Note))
        .ToList();

    return Results.Ok(dtos);
}).RequireRateLimiting("player-me");

app.MapDelete("/api/me/reservations/{reservationId:guid}", async (
    Guid reservationId,
    IPlayerContextAccessor playerContextAccessor,
    IReservationService reservationService,
    CancellationToken cancellationToken) =>
{
    var player = playerContextAccessor.Current;
    if (player is null)
    {
        return Results.Unauthorized();
    }

    var result = await reservationService.CancelOnlineAsync(
        reservationId,
        player.PlayerAccountId,
        cancellationToken);

    if (result.NotFound)
    {
        return Results.NotFound();
    }

    if (!result.Succeeded)
    {
        return Results.BadRequest(new { Error = result.Error });
    }

    return Results.Ok(ToPlayerReservationDto(result.Response!));
}).RequireRateLimiting("player-me");
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/AFK4.Platform.Api.Tests --filter "FullyQualifiedName~PortalWritesEndpointTests.ListReservations|PortalWritesEndpointTests.CancelReservation"
```
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add \
  src/AFK4.Platform.Api/Program.cs \
  tests/AFK4.Platform.Api.Tests/PortalWritesEndpointTests.cs
git commit -m "feat(portal): GET /api/me/reservations + DELETE /api/me/reservations/{id}"
```

---

## Final verification

- [ ] **Run the full backend gate**

```bash
dotnet test tests/AFK4.Platform.Api.Tests
```
Expected: PASS — all pre-existing tests plus the new portal-writes tests, 0 failures.

> **Env quirk reminder:** `TreatWarningsAsErrors=true` — if any test file has an unused `using`, the build will fail. Remove it before committing.

> **Time-rounding flakiness:** tests that compare `DateTimeOffset` values derived from `DateTimeOffset.UtcNow` (e.g. `FulfilledAtUtc`, `StartsAtUtc`) should use `Assert.InRange(actual, expected.AddSeconds(-5), expected.AddSeconds(5))` if exact equality fails intermittently.

- [ ] **Security / isolation self-review checklist:**
  - Every `/api/me/*` handler reads player id only from `PlayerContext.PlayerAccountId` (never from request body).
  - `CancelOnlineAsync` returns `NotFound` (not `Forbidden`) for foreign reservations to avoid existence disclosure.
  - `FulfilPaymentIntent` staff endpoint checks org scope before the intent load, and checks branch permission before the billing call.
  - No staff fields (`CreatedByStaffUserId`, `UpdatedByStaffUserId`) are leaked in `PlayerReservationDto` or `PlayerTopUpIntentDto`.
  - `FulfilledByLedgerEntryId = null` in v1 — documented with a comment, not silently omitted.

- [ ] **Use superpowers:finishing-a-development-branch** to present branch options (merge to main, PR, or cleanup).
