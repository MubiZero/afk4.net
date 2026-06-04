# Customer Portal "Reads" Backend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the player-scoped read endpoints of the customer portal — dashboard (balance/debt + live active-session cost), visit/purchase history with receipts, and profile self-edit — all under `/api/me/*`, reusing the already-built player-auth foundation.

**Architecture:** New player-scoped read endpoints on the existing Platform.Api, behind the existing `PlayerAuthenticationMiddleware` (`/api/me/*` only) and `player-me` rate-limit policy. Every handler resolves data **only** for `PlayerContext.PlayerAccountId` — no route accepts a caller-supplied player id. Read logic lives in small static projector classes (mirroring the existing `LedgerBalanceProjector`), not inline in `Program.cs`. Balance/debt reuse `LedgerBalanceProjector`; live accrued cost reuses `TariffBilling.ComputeForElapsed`; visit totals derive from the `session_checkout` receipt the counter-loop already writes.

**Tech Stack:** ASP.NET Core minimal APIs, EF Core 10 (in-memory for tests), xUnit + `WebApplicationFactory<Program>` (`PlatformApiFactory`). `TreatWarningsAsErrors=true` — add only usings you actually use. Money is `long` minor units end-to-end.

**Scope boundary:** This plan is **reads + the language/marketing profile edit only**. Online reservations and the wallet top-up intent (writes) are the next plan. Phone-number self-change and OTP are deferred (gated on the notifications SMS channel) — see Task 6 note.

---

## File Structure

**New files:**
- `src/AFK4.Platform.Api/Common/CursorToken.cs` — pure base64 cursor encode/decode for `(DateTimeOffset, Guid)`.
- `src/AFK4.Shared.Contracts/Common/CursorPage.cs` — generic `CursorPage<T>(Items, NextCursor)`.
- `src/AFK4.Platform.Api/Players/PlayerDashboardProjector.cs` — static `GetDashboardAsync`.
- `src/AFK4.Platform.Api/Players/PlayerHistoryProjector.cs` — static `GetVisitsAsync`, `GetVisitReceiptAsync`, `GetPurchasesAsync`.
- `src/AFK4.Shared.Contracts/Players/PlayerDashboardDto.cs`, `ActiveSessionDto.cs`, `PlayerVisitDto.cs`, `PlayerVisitReceiptDto.cs`, `PlayerPurchaseDto.cs`, `PlayerPurchaseLineDto.cs`, `UpdatePlayerProfileRequest.cs`.
- `tests/AFK4.Platform.Api.Tests/PortalReadsEndpointTests.cs`, `CursorTokenTests.cs`.

**Modified files:**
- `src/AFK4.Platform.Api/Program.cs` — register the 5 new endpoints (`GET /api/me/dashboard`, `GET /api/me/visits`, `GET /api/me/visits/{sessionId}/receipt`, `GET /api/me/purchases`, `PATCH /api/me/profile`).

**Conventions to mirror (verified ground truth):**
- Endpoint auth: read `playerContextAccessor.Current`; if null → `Results.Unauthorized()`. Add `.RequireRateLimiting("player-me")`. (See `Program.cs` `GET /api/me/profile`.)
- Projectors are `static` classes taking `PlatformDbContext` + ids + `CancellationToken`, using `.AsNoTracking()`. (See `Billing/LedgerBalanceProjector.cs`.)
- Session duration mode is implicit: `EndsAtUtc is null` → open tab (count-up accrued cost); `EndsAtUtc is not null` → fixed (countdown remaining).
- Active session query: `State == SessionStateNames.Active && PlayerAccountId == id`.
- Tests obtain a player bearer token via `SeedPlayerWithPinAsync` + `POST /api/public/player/sign-in` → `PlayerSignInResponse.AccessToken` → `Authorization: Bearer`. (See `PlayerAuthenticationEndpointTests.cs`.)

---

## Task 1: Pagination primitives (CursorToken + CursorPage)

**Files:**
- Create: `src/AFK4.Platform.Api/Common/CursorToken.cs`
- Create: `src/AFK4.Shared.Contracts/Common/CursorPage.cs`
- Test: `tests/AFK4.Platform.Api.Tests/CursorTokenTests.cs`

A visit/purchase list is keyed by `(CreatedAtUtc DESC, Id DESC)`. The cursor encodes the last row's `(timestamp, id)` so the next page resumes after it. Tampered/garbage cursors decode to `null` (caller treats as "first page") — never throw on user input.

- [ ] **Step 1: Write the failing test**

```csharp
using System;
using AFK4.Platform.Api.Common;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public class CursorTokenTests
{
    [Fact]
    public void EncodeThenDecode_RoundTripsTimestampAndId()
    {
        var ts = DateTimeOffset.Parse("2026-06-03T12:34:56.789Z");
        var id = Guid.Parse("11111111-2222-3333-4444-555555555555");

        var encoded = CursorToken.Encode(ts, id);
        var ok = CursorToken.TryDecode(encoded, out var decodedTs, out var decodedId);

        Assert.True(ok);
        Assert.Equal(ts, decodedTs);
        Assert.Equal(id, decodedId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-base64-!!!")]
    [InlineData("YWJj")] // valid base64, wrong shape
    public void TryDecode_OnGarbage_ReturnsFalse(string garbage)
    {
        var ok = CursorToken.TryDecode(garbage, out _, out _);
        Assert.False(ok);
    }

    [Fact]
    public void TryDecode_OnNull_ReturnsFalse()
    {
        var ok = CursorToken.TryDecode(null, out _, out _);
        Assert.False(ok);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~CursorTokenTests`
Expected: FAIL — `CursorToken` does not exist (compile error).

- [ ] **Step 3: Write the implementation**

`src/AFK4.Platform.Api/Common/CursorToken.cs`:

```csharp
using System;
using System.Buffers.Text;
using System.Text;

namespace AFK4.Platform.Api.Common;

// Opaque keyset-pagination cursor for (CreatedAtUtc DESC, Id DESC) ordered lists.
// Encodes "<unixMillis>:<guid>" as URL-safe base64. Decode never throws on user
// input — bad cursors yield false so the caller falls back to the first page.
public static class CursorToken
{
    public static string Encode(DateTimeOffset timestamp, Guid id)
    {
        var payload = $"{timestamp.ToUnixTimeMilliseconds()}:{id:N}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
    }

    public static bool TryDecode(string? cursor, out DateTimeOffset timestamp, out Guid id)
    {
        timestamp = default;
        id = default;

        if (string.IsNullOrWhiteSpace(cursor))
        {
            return false;
        }

        try
        {
            var payload = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var separator = payload.IndexOf(':');
            if (separator <= 0)
            {
                return false;
            }

            if (!long.TryParse(payload[..separator], out var unixMillis) ||
                !Guid.TryParseExact(payload[(separator + 1)..], "N", out id))
            {
                return false;
            }

            timestamp = DateTimeOffset.FromUnixTimeMilliseconds(unixMillis);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
```

`src/AFK4.Shared.Contracts/Common/CursorPage.cs`:

```csharp
using System.Collections.Generic;

namespace AFK4.Shared.Contracts.Common;

// A page of results plus the cursor to fetch the next page (null when exhausted).
public sealed record CursorPage<T>(IReadOnlyList<T> Items, string? NextCursor);
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~CursorTokenTests`
Expected: PASS (all cases).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Api/Common/CursorToken.cs src/AFK4.Shared.Contracts/Common/CursorPage.cs tests/AFK4.Platform.Api.Tests/CursorTokenTests.cs
git commit -m "feat(portal): cursor pagination primitives"
```

---

## Task 2: Dashboard read (balance/debt + active session)

**Files:**
- Create: `src/AFK4.Shared.Contracts/Players/PlayerDashboardDto.cs`, `ActiveSessionDto.cs`
- Create: `src/AFK4.Platform.Api/Players/PlayerDashboardProjector.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs` (add `GET /api/me/dashboard`)
- Test: `tests/AFK4.Platform.Api.Tests/PortalReadsEndpointTests.cs`

Balance/debt come from the existing `LedgerBalanceProjector.GetWalletSummaryAsync`. The active session (if any) is the player's `Active` session; open tabs (`EndsAtUtc is null`) expose `AccruedCostMinorUnits` via `TariffBilling.ComputeForElapsed`; fixed sessions expose `RemainingSeconds`.

- [ ] **Step 1: Write the contracts**

`src/AFK4.Shared.Contracts/Players/ActiveSessionDto.cs`:

```csharp
using System;

namespace AFK4.Shared.Contracts.Players;

public sealed record ActiveSessionDto(
    Guid SessionId,
    Guid SeatId,
    string SeatName,
    DateTimeOffset StartedAtUtc,
    string DurationMode,            // "open" | "fixed"
    int? RemainingSeconds,          // fixed only
    long? AccruedCostMinorUnits,    // open only
    string CurrencyCode);
```

`src/AFK4.Shared.Contracts/Players/PlayerDashboardDto.cs`:

```csharp
using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Players;

public sealed record PlayerDashboardDto(
    MoneyDto WalletBalance,
    MoneyDto DebtBalance,
    ActiveSessionDto? ActiveSession);
```

- [ ] **Step 2: Write the failing tests**

Create `tests/AFK4.Platform.Api.Tests/PortalReadsEndpointTests.cs`. It needs a shared seed helper for an authed player and for sessions/tariffs. Start the file with this scaffold (later tasks extend it):

```csharp
using System;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Common;
using AFK4.Shared.Contracts.Players;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public class PortalReadsEndpointTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-03T12:00:00Z");

    private sealed record SeededPlayer(Guid OrgId, Guid BranchId, Guid PlayerId);

    // Seeds an active player + a PIN credential. Returns ids for further seeding.
    private static async Task<SeededPlayer> SeedPlayerAsync(PlatformApiFactory factory, string pin)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var org = Guid.NewGuid();
        var branch = Guid.NewGuid();
        var player = Guid.NewGuid();

        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = player,
            OrganizationId = org,
            HomeBranchId = branch,
            DisplayName = "Player One",
            PhoneNumber = "+992900000001",
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
            PhoneVerified = true,
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        };
        credential.PasswordHash = new PasswordHasher<PlayerCredentialEntity>().HashPassword(credential, pin);
        db.PlayerCredentials.Add(credential);
        await db.SaveChangesAsync();
        return new SeededPlayer(org, branch, player);
    }

    private static async Task AuthenticateAsync(HttpClient client, Guid orgId, string phone, string pin)
    {
        var signIn = await client.PostAsJsonAsync(
            "/api/public/player/sign-in",
            new PlayerSignInRequest(orgId, phone, pin));
        signIn.EnsureSuccessStatusCode();
        var tokens = await signIn.Content.ReadFromJsonAsync<PlayerSignInResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
    }

    private static async Task SeedLedgerAsync(
        PlatformApiFactory factory, Guid org, Guid branch, Guid player,
        string accountType, string entryType, long amount)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.LedgerEntries.Add(new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(),
            OrganizationId = org,
            BranchId = branch,
            PlayerAccountId = player,
            EntryType = entryType,
            AccountType = accountType,
            AmountMinorUnits = amount,
            CurrencyCode = "TJS",
            Description = entryType,
            Reason = "test seed",
            CreatedByStaffUserId = Guid.NewGuid(),
            CreatedAtUtc = Now
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Dashboard_ReturnsWalletAndDebtFromLedger()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        await SeedLedgerAsync(factory, p.OrgId, p.BranchId, p.PlayerId, "wallet", "top_up", 10_000);
        await SeedLedgerAsync(factory, p.OrgId, p.BranchId, p.PlayerId, "debt", "postpaid_debt", 2_500);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, "+992900000001", "1234");

        var response = await client.GetAsync("/api/me/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<PlayerDashboardDto>();
        Assert.Equal(10_000, dto!.WalletBalance.MinorUnits);
        Assert.Equal(2_500, dto.DebtBalance.MinorUnits);
        Assert.Null(dto.ActiveSession);
    }

    [Fact]
    public async Task Dashboard_WithOpenTab_ReturnsAccruedCost()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        await SeedActiveOpenSessionAsync(factory, p, pricePerMinute: 50, startedMinutesAgo: 40);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, "+992900000001", "1234");

        var dto = await (await client.GetAsync("/api/me/dashboard"))
            .Content.ReadFromJsonAsync<PlayerDashboardDto>();

        Assert.NotNull(dto!.ActiveSession);
        Assert.Equal("open", dto.ActiveSession!.DurationMode);
        Assert.Null(dto.ActiveSession.RemainingSeconds);
        // 40 min elapsed * 50/min = 2000 minor (min-billable/rounding default 1).
        Assert.Equal(2_000, dto.ActiveSession.AccruedCostMinorUnits);
        Assert.Equal("Seat 1", dto.ActiveSession.SeatName);
    }

    [Fact]
    public async Task Dashboard_WithoutToken_Returns401()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/me/dashboard");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Seeds a seat, a tariff version, and an active OPEN session for the player.
    private static async Task SeedActiveOpenSessionAsync(
        PlatformApiFactory factory, SeededPlayer p, long pricePerMinute, int startedMinutesAgo)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var seatId = Guid.NewGuid();
        var tariffVersionId = Guid.NewGuid();
        db.Seats.Add(new SeatEntity
        {
            SeatId = seatId,
            OrganizationId = p.OrgId,
            BranchId = p.BranchId,
            ZoneId = Guid.NewGuid(),
            Name = "Seat 1",
            SortOrder = 0,
            CreatedAtUtc = Now
        });
        db.TariffVersions.Add(new TariffVersionEntity
        {
            TariffVersionId = tariffVersionId,
            TariffId = Guid.NewGuid(),
            OrganizationId = p.OrgId,
            BranchId = p.BranchId,
            VersionNumber = 1,
            CurrencyCode = "TJS",
            PricePerMinuteMinorUnits = pricePerMinute,
            MinimumBillableMinutes = 1,
            RoundingIncrementMinutes = 1,
            EffectiveFromUtc = Now.AddYears(-1),
            CreatedAtUtc = Now.AddYears(-1)
        });
        db.Sessions.Add(new SessionEntity
        {
            SessionId = Guid.NewGuid(),
            OrganizationId = p.OrgId,
            BranchId = p.BranchId,
            SeatId = seatId,
            DeviceId = Guid.NewGuid(),
            CreatedByStaffUserId = Guid.NewGuid(),
            PlayerKind = "player",
            PlayerAccountId = p.PlayerId,
            TariffRuleVersionId = tariffVersionId.ToString("D"),
            State = "active",
            RequestedAtUtc = Now.AddMinutes(-startedMinutesAgo),
            StartedAtUtc = Now.AddMinutes(-startedMinutesAgo),
            EndsAtUtc = null,
            UpdatedAtUtc = Now
        });
        await db.SaveChangesAsync();
    }
}
```

> **Note for the implementer:** the accrued-cost assertion uses *wall-clock* `now` inside the handler, not the seeded `Now`. To make it deterministic, seed `StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-40)` instead of `Now.AddMinutes(...)` in `SeedActiveOpenSessionAsync`, OR assert a tolerance band (`Assert.InRange(dto.ActiveSession.AccruedCostMinorUnits!.Value, 1_950, 2_100)`). Prefer the wall-clock seed for a crisp assertion. Verify which is stable when you run it and adjust the seed/assert accordingly — do not change the production code to read a fake clock.

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~PortalReadsEndpointTests.Dashboard`
Expected: FAIL — `/api/me/dashboard` not mapped (404) / `PlayerDashboardDto` compile error.

- [ ] **Step 4: Write the projector**

`src/AFK4.Platform.Api/Players/PlayerDashboardProjector.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Players;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Players;

// Builds the player dashboard: wallet/debt (reusing LedgerBalanceProjector) plus
// the player's active session with its live accrued cost (open) or remaining
// time (fixed). The accrued-cost math reuses the shared TariffBilling primitive,
// so the portal and the operator floor map never disagree.
public static class PlayerDashboardProjector
{
    public static async Task<PlayerDashboardDto> GetDashboardAsync(
        PlatformDbContext dbContext,
        Guid playerAccountId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var wallet = await LedgerBalanceProjector.GetWalletSummaryAsync(
            dbContext, playerAccountId, cancellationToken);

        var walletBalance = wallet?.WalletBalance ?? new MoneyDto("TJS", 0);
        var debtBalance = wallet?.DebtBalance ?? new MoneyDto("TJS", 0);

        var session = await dbContext.Sessions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate =>
                    candidate.PlayerAccountId == playerAccountId &&
                    candidate.State == "active",
                cancellationToken);

        ActiveSessionDto? activeSession = null;
        if (session is not null)
        {
            activeSession = await BuildActiveSessionAsync(dbContext, session, now, cancellationToken);
        }

        return new PlayerDashboardDto(walletBalance, debtBalance, activeSession);
    }

    private static async Task<ActiveSessionDto> BuildActiveSessionAsync(
        PlatformDbContext dbContext,
        SessionEntity session,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var seatName = await dbContext.Seats
            .AsNoTracking()
            .Where(seat => seat.SeatId == session.SeatId)
            .Select(seat => seat.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        var startedAtUtc = session.StartedAtUtc ?? now;
        var currencyCode = "TJS";

        // Fixed session: expose remaining time, no accrued cost.
        if (session.EndsAtUtc is not null)
        {
            var remaining = (int)Math.Max(0, (session.EndsAtUtc.Value - now).TotalSeconds);
            return new ActiveSessionDto(
                session.SessionId, session.SeatId, seatName, startedAtUtc,
                "fixed", remaining, null, currencyCode);
        }

        // Open tab: count-up accrued cost via the shared tariff primitive.
        long? accrued = null;
        if (Guid.TryParse(session.TariffRuleVersionId, out var tariffVersionId))
        {
            var version = await dbContext.TariffVersions
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.TariffVersionId == tariffVersionId, cancellationToken);
            if (version is not null)
            {
                currencyCode = version.CurrencyCode;
                var pricing = new TariffPricing(
                    version.PricePerMinuteMinorUnits,
                    version.MinimumBillableMinutes,
                    version.RoundingIncrementMinutes,
                    version.CurrencyCode);
                var computation = TariffBilling.ComputeForElapsed(now - startedAtUtc, pricing);
                accrued = computation?.AmountMinorUnits;
            }
        }

        return new ActiveSessionDto(
            session.SessionId, session.SeatId, seatName, startedAtUtc,
            "open", null, accrued, currencyCode);
    }
}
```

> **Implementer:** confirm the exact `DbSet` names (`dbContext.Sessions`, `dbContext.Seats`, `dbContext.TariffVersions`) and the `SessionStateNames` constant. If a `SessionStateNames.Active` constant exists, use it instead of the `"active"` literal. Confirm `TariffPricing`'s constructor parameter order against `TariffBilling.cs` before relying on it.

- [ ] **Step 5: Register the endpoint**

In `Program.cs`, next to `GET /api/me/profile`, add:

```csharp
app.MapGet("/api/me/dashboard", async (
    IPlayerContextAccessor playerContextAccessor,
    PlatformDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var player = playerContextAccessor.Current;
    if (player is null)
    {
        return Results.Unauthorized();
    }

    var dashboard = await PlayerDashboardProjector.GetDashboardAsync(
        dbContext, player.PlayerAccountId, DateTimeOffset.UtcNow, cancellationToken);
    return Results.Ok(dashboard);
}).RequireRateLimiting("player-me");
```

Add `using AFK4.Platform.Api.Players;` to `Program.cs` only if not already present.

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~PortalReadsEndpointTests.Dashboard`
Expected: PASS (3 tests).

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.Shared.Contracts/Players/PlayerDashboardDto.cs src/AFK4.Shared.Contracts/Players/ActiveSessionDto.cs src/AFK4.Platform.Api/Players/PlayerDashboardProjector.cs src/AFK4.Platform.Api/Program.cs tests/AFK4.Platform.Api.Tests/PortalReadsEndpointTests.cs
git commit -m "feat(portal): GET /api/me/dashboard (balance/debt + active session)"
```

---

## Task 3: Visit history (GET /api/me/visits)

**Files:**
- Create: `src/AFK4.Shared.Contracts/Players/PlayerVisitDto.cs`
- Create: `src/AFK4.Platform.Api/Players/PlayerHistoryProjector.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs` (add `GET /api/me/visits`)
- Test: `tests/AFK4.Platform.Api.Tests/PortalReadsEndpointTests.cs`

A visit is an **ended** session for this player. Totals derive from the `session_checkout` receipt the counter-loop writes: `GrandTotal = receipt.TotalMinorUnits`; `PosTotal = Σ` attached `PosSale.TotalMinorUnits` (`SessionId == session`); `TimeCharge = GrandTotal − PosTotal`. Sessions with no receipt (e.g. comped/free) report `GrandTotal = PosTotal`, `TimeCharge = 0`, `HasReceipt = false`. Ordered `(EndedAtUtc DESC, SessionId DESC)`, cursor-paginated, page size 20.

- [ ] **Step 1: Write the contract**

`src/AFK4.Shared.Contracts/Players/PlayerVisitDto.cs`:

```csharp
using System;

namespace AFK4.Shared.Contracts.Players;

public sealed record PlayerVisitDto(
    Guid SessionId,
    Guid SeatId,
    string SeatName,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? EndedAtUtc,
    long TimeChargeMinorUnits,
    long PosTotalMinorUnits,
    long GrandTotalMinorUnits,
    string CurrencyCode,
    bool HasReceipt);
```

- [ ] **Step 2: Write the failing tests**

Add to `PortalReadsEndpointTests.cs`. Add a seed helper for an ended session + optional receipt + attached POS, then tests:

```csharp
    // Seeds an ENDED session for the player, with optional checkout receipt and
    // optional attached POS sale. Returns the sessionId.
    private static async Task<Guid> SeedEndedVisitAsync(
        PlatformApiFactory factory, SeededPlayer p, string seatName,
        DateTimeOffset endedAtUtc, long? receiptTotal, long attachedPosTotal)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var seatId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        db.Seats.Add(new SeatEntity
        {
            SeatId = seatId, OrganizationId = p.OrgId, BranchId = p.BranchId,
            ZoneId = Guid.NewGuid(), Name = seatName, SortOrder = 0, CreatedAtUtc = Now
        });
        db.Sessions.Add(new SessionEntity
        {
            SessionId = sessionId, OrganizationId = p.OrgId, BranchId = p.BranchId,
            SeatId = seatId, DeviceId = Guid.NewGuid(), CreatedByStaffUserId = Guid.NewGuid(),
            PlayerKind = "player", PlayerAccountId = p.PlayerId,
            TariffRuleVersionId = Guid.NewGuid().ToString("D"), State = "ended",
            RequestedAtUtc = endedAtUtc.AddHours(-1), StartedAtUtc = endedAtUtc.AddHours(-1),
            EndsAtUtc = null, EndedAtUtc = endedAtUtc, UpdatedAtUtc = endedAtUtc
        });
        if (attachedPosTotal > 0)
        {
            db.PosSales.Add(new PosSaleEntity
            {
                PosSaleId = Guid.NewGuid(), OrganizationId = p.OrgId, BranchId = p.BranchId,
                ShiftId = Guid.NewGuid(), CreatedByStaffUserId = Guid.NewGuid(),
                PlayerAccountId = p.PlayerId, SessionId = sessionId, State = "paid",
                CurrencyCode = "TJS", TotalMinorUnits = attachedPosTotal,
                CreatedAtUtc = endedAtUtc, PaidAtUtc = endedAtUtc
            });
        }
        if (receiptTotal is not null)
        {
            db.Receipts.Add(new ReceiptEntity
            {
                ReceiptId = Guid.NewGuid(), OrganizationId = p.OrgId, BranchId = p.BranchId,
                SessionId = sessionId, PosSaleId = null,
                ReceiptNumber = "POS-20260603-0001", ReceiptType = "session_checkout",
                CurrencyCode = "TJS", TotalMinorUnits = receiptTotal.Value, CreatedAtUtc = endedAtUtc
            });
        }
        await db.SaveChangesAsync();
        return sessionId;
    }

    [Fact]
    public async Task Visits_ReturnsOwnEndedSessions_NewestFirst_WithDerivedTotals()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        await SeedEndedVisitAsync(factory, p, "Seat A", Now.AddDays(-2), receiptTotal: 5_000, attachedPosTotal: 1_500);
        await SeedEndedVisitAsync(factory, p, "Seat B", Now.AddDays(-1), receiptTotal: null, attachedPosTotal: 0);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, "+992900000001", "1234");

        var page = await (await client.GetAsync("/api/me/visits"))
            .Content.ReadFromJsonAsync<CursorPage<PlayerVisitDto>>();

        Assert.Equal(2, page!.Items.Count);
        Assert.Equal("Seat B", page.Items[0].SeatName);     // newest first
        Assert.False(page.Items[0].HasReceipt);
        var seatA = page.Items[1];
        Assert.True(seatA.HasReceipt);
        Assert.Equal(5_000, seatA.GrandTotalMinorUnits);
        Assert.Equal(1_500, seatA.PosTotalMinorUnits);
        Assert.Equal(3_500, seatA.TimeChargeMinorUnits);    // grand - pos
    }

    [Fact]
    public async Task Visits_DoesNotReturnOtherPlayersSessions()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        var other = await SeedPlayerAsync(factory, "9999");
        await SeedEndedVisitAsync(factory, other, "Seat X", Now.AddDays(-1), receiptTotal: 9_000, attachedPosTotal: 0);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, "+992900000001", "1234");

        var page = await (await client.GetAsync("/api/me/visits"))
            .Content.ReadFromJsonAsync<CursorPage<PlayerVisitDto>>();

        Assert.Empty(page!.Items);
    }

    [Fact]
    public async Task Visits_Paginates_WithCursor()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        for (var i = 0; i < 25; i++)
        {
            await SeedEndedVisitAsync(factory, p, $"Seat {i}", Now.AddMinutes(-i), receiptTotal: 1_000, attachedPosTotal: 0);
        }
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, "+992900000001", "1234");

        var first = await (await client.GetAsync("/api/me/visits"))
            .Content.ReadFromJsonAsync<CursorPage<PlayerVisitDto>>();
        Assert.Equal(20, first!.Items.Count);
        Assert.NotNull(first.NextCursor);

        var second = await (await client.GetAsync($"/api/me/visits?cursor={Uri.EscapeDataString(first.NextCursor!)}"))
            .Content.ReadFromJsonAsync<CursorPage<PlayerVisitDto>>();
        Assert.Equal(5, second!.Items.Count);
        Assert.Null(second.NextCursor);
    }
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~PortalReadsEndpointTests.Visits`
Expected: FAIL — endpoint/contract missing.

- [ ] **Step 4: Write the projector method**

Create `src/AFK4.Platform.Api/Players/PlayerHistoryProjector.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AFK4.Platform.Api.Common;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Common;
using AFK4.Shared.Contracts.Players;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Players;

// Player-scoped history reads: past visits (ended sessions) and standalone POS
// purchases. Totals for a visit are read from the session_checkout receipt the
// counter-loop writes — the portal renders, never re-computes the charge.
public static class PlayerHistoryProjector
{
    private const int PageSize = 20;

    public static async Task<CursorPage<PlayerVisitDto>> GetVisitsAsync(
        PlatformDbContext dbContext,
        Guid playerAccountId,
        string? cursor,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Sessions
            .AsNoTracking()
            .Where(session =>
                session.PlayerAccountId == playerAccountId &&
                session.State == "ended" &&
                session.EndedAtUtc != null);

        if (CursorToken.TryDecode(cursor, out var afterTs, out var afterId))
        {
            query = query.Where(session =>
                session.EndedAtUtc < afterTs ||
                (session.EndedAtUtc == afterTs && session.SessionId.CompareTo(afterId) < 0));
        }

        var sessions = await query
            .OrderByDescending(session => session.EndedAtUtc)
            .ThenByDescending(session => session.SessionId)
            .Take(PageSize + 1)
            .ToListAsync(cancellationToken);

        var hasMore = sessions.Count > PageSize;
        if (hasMore)
        {
            sessions.RemoveAt(sessions.Count - 1);
        }

        var sessionIds = sessions.Select(s => s.SessionId).ToList();
        var seatIds = sessions.Select(s => s.SeatId).Distinct().ToList();

        var seatNames = await dbContext.Seats
            .AsNoTracking()
            .Where(seat => seatIds.Contains(seat.SeatId))
            .ToDictionaryAsync(seat => seat.SeatId, seat => seat.Name, cancellationToken);

        var receipts = await dbContext.Receipts
            .AsNoTracking()
            .Where(receipt => receipt.SessionId != null && sessionIds.Contains(receipt.SessionId.Value))
            .ToListAsync(cancellationToken);
        var receiptBySession = receipts
            .GroupBy(receipt => receipt.SessionId!.Value)
            .ToDictionary(group => group.Key, group => group.First());

        var posTotals = await dbContext.PosSales
            .AsNoTracking()
            .Where(sale => sale.SessionId != null && sessionIds.Contains(sale.SessionId.Value))
            .GroupBy(sale => sale.SessionId!.Value)
            .Select(group => new { SessionId = group.Key, Total = group.Sum(sale => sale.TotalMinorUnits) })
            .ToDictionaryAsync(row => row.SessionId, row => row.Total, cancellationToken);

        var items = new List<PlayerVisitDto>(sessions.Count);
        foreach (var session in sessions)
        {
            var posTotal = posTotals.GetValueOrDefault(session.SessionId, 0);
            var hasReceipt = receiptBySession.TryGetValue(session.SessionId, out var receipt);
            var grandTotal = hasReceipt ? receipt!.TotalMinorUnits : posTotal;
            var timeCharge = grandTotal - posTotal;
            var currency = hasReceipt ? receipt!.CurrencyCode : "TJS";

            items.Add(new PlayerVisitDto(
                session.SessionId,
                session.SeatId,
                seatNames.GetValueOrDefault(session.SeatId, string.Empty),
                session.StartedAtUtc ?? session.RequestedAtUtc,
                session.EndedAtUtc,
                timeCharge,
                posTotal,
                grandTotal,
                currency,
                hasReceipt));
        }

        string? nextCursor = hasMore && items.Count > 0
            ? CursorToken.Encode(items[^1].EndedAtUtc!.Value, items[^1].SessionId)
            : null;

        return new CursorPage<PlayerVisitDto>(items, nextCursor);
    }
}
```

> **Implementer:** EF in-memory may not translate `Guid.CompareTo` inside the keyset `Where`. If the cursor test fails to translate, fetch the candidate window by timestamp only (`EndedAtUtc <= afterTs`) and apply the `(timestamp, id)` tie-break in memory after materializing — mirror whatever the codebase already does for stable ordering. Keep the public behaviour identical.

- [ ] **Step 5: Register the endpoint**

In `Program.cs`:

```csharp
app.MapGet("/api/me/visits", async (
    string? cursor,
    IPlayerContextAccessor playerContextAccessor,
    PlatformDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var player = playerContextAccessor.Current;
    if (player is null)
    {
        return Results.Unauthorized();
    }

    var page = await PlayerHistoryProjector.GetVisitsAsync(
        dbContext, player.PlayerAccountId, cursor, cancellationToken);
    return Results.Ok(page);
}).RequireRateLimiting("player-me");
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~PortalReadsEndpointTests.Visits`
Expected: PASS (3 tests).

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.Shared.Contracts/Players/PlayerVisitDto.cs src/AFK4.Platform.Api/Players/PlayerHistoryProjector.cs src/AFK4.Platform.Api/Program.cs tests/AFK4.Platform.Api.Tests/PortalReadsEndpointTests.cs
git commit -m "feat(portal): GET /api/me/visits (paginated history)"
```

---

## Task 4: Visit receipt (GET /api/me/visits/{sessionId}/receipt)

**Files:**
- Create: `src/AFK4.Shared.Contracts/Players/PlayerVisitReceiptDto.cs`
- Modify: `src/AFK4.Platform.Api/Players/PlayerHistoryProjector.cs` (add `GetVisitReceiptAsync`)
- Modify: `src/AFK4.Platform.Api/Program.cs`
- Test: `tests/AFK4.Platform.Api.Tests/PortalReadsEndpointTests.cs`

Returns the receipt header plus the breakdown (time charge + POS lines) for one of the **player's own** ended sessions. A session belonging to another player, or one with no receipt, returns **404** (no existence disclosure — spec §7).

- [ ] **Step 1: Write the contract**

`src/AFK4.Shared.Contracts/Players/PlayerVisitReceiptDto.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace AFK4.Shared.Contracts.Players;

public sealed record PlayerVisitReceiptDto(
    string ReceiptNumber,
    DateTimeOffset CreatedAtUtc,
    Guid SessionId,
    string SeatName,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? EndedAtUtc,
    long TimeChargeMinorUnits,
    IReadOnlyList<PlayerPurchaseLineDto> PosLines,
    long PosTotalMinorUnits,
    long GrandTotalMinorUnits,
    string CurrencyCode);
```

> Depends on `PlayerPurchaseLineDto` (Task 5). To keep tasks independently compilable, create `PlayerPurchaseLineDto.cs` here as part of Task 4 (Task 5 reuses it):

`src/AFK4.Shared.Contracts/Players/PlayerPurchaseLineDto.cs`:

```csharp
namespace AFK4.Shared.Contracts.Players;

public sealed record PlayerPurchaseLineDto(
    string ProductName,
    int Quantity,
    long UnitPriceMinorUnits,
    long LineTotalMinorUnits);
```

- [ ] **Step 2: Write the failing tests**

Add to `PortalReadsEndpointTests.cs`:

```csharp
    [Fact]
    public async Task Receipt_ForOwnSession_ReturnsReceiptWithBreakdown()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        var sessionId = await SeedEndedVisitAsync(factory, p, "Seat A", Now.AddDays(-1), receiptTotal: 5_000, attachedPosTotal: 1_500);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, "+992900000001", "1234");

        var response = await client.GetAsync($"/api/me/visits/{sessionId}/receipt");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<PlayerVisitReceiptDto>();
        Assert.Equal("POS-20260603-0001", dto!.ReceiptNumber);
        Assert.Equal(5_000, dto.GrandTotalMinorUnits);
        Assert.Equal(1_500, dto.PosTotalMinorUnits);
        Assert.Equal(3_500, dto.TimeChargeMinorUnits);
    }

    [Fact]
    public async Task Receipt_ForOtherPlayersSession_Returns404()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        var other = await SeedPlayerAsync(factory, "9999");
        var otherSession = await SeedEndedVisitAsync(factory, other, "Seat X", Now.AddDays(-1), receiptTotal: 9_000, attachedPosTotal: 0);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, "+992900000001", "1234");

        var response = await client.GetAsync($"/api/me/visits/{otherSession}/receipt");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Receipt_WhenNoReceiptExists_Returns404()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        var sessionId = await SeedEndedVisitAsync(factory, p, "Seat A", Now.AddDays(-1), receiptTotal: null, attachedPosTotal: 0);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, "+992900000001", "1234");

        var response = await client.GetAsync($"/api/me/visits/{sessionId}/receipt");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~PortalReadsEndpointTests.Receipt`
Expected: FAIL.

- [ ] **Step 4: Add the projector method**

Append to `PlayerHistoryProjector`:

```csharp
    public static async Task<PlayerVisitReceiptDto?> GetVisitReceiptAsync(
        PlatformDbContext dbContext,
        Guid playerAccountId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await dbContext.Sessions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate =>
                    candidate.SessionId == sessionId &&
                    candidate.PlayerAccountId == playerAccountId,
                cancellationToken);

        if (session is null)
        {
            return null;
        }

        var receipt = await dbContext.Receipts
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.SessionId == sessionId,
                cancellationToken);

        if (receipt is null)
        {
            return null;
        }

        var seatName = await dbContext.Seats
            .AsNoTracking()
            .Where(seat => seat.SeatId == session.SeatId)
            .Select(seat => seat.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        var posSaleIds = await dbContext.PosSales
            .AsNoTracking()
            .Where(sale => sale.SessionId == sessionId)
            .Select(sale => sale.PosSaleId)
            .ToListAsync(cancellationToken);

        var posLines = await dbContext.PosSaleLines
            .AsNoTracking()
            .Where(line => posSaleIds.Contains(line.PosSaleId))
            .Select(line => new PlayerPurchaseLineDto(
                line.ProductName, line.Quantity, line.UnitPriceMinorUnits, line.LineTotalMinorUnits))
            .ToListAsync(cancellationToken);

        var posTotal = posLines.Sum(line => line.LineTotalMinorUnits);
        var grandTotal = receipt.TotalMinorUnits;

        return new PlayerVisitReceiptDto(
            receipt.ReceiptNumber,
            receipt.CreatedAtUtc,
            session.SessionId,
            seatName,
            session.StartedAtUtc ?? session.RequestedAtUtc,
            session.EndedAtUtc,
            grandTotal - posTotal,
            posLines,
            posTotal,
            grandTotal,
            receipt.CurrencyCode);
    }
```

> **Implementer:** confirm `dbContext.PosSaleLines` is the DbSet name. If the POS total derived from lines disagrees with the attached `PosSale.TotalMinorUnits` in any seeded test, prefer summing the `PosSale.TotalMinorUnits` for the `PosTotal` and keep the lines purely for display — match whichever the seed produces.

- [ ] **Step 5: Register the endpoint**

```csharp
app.MapGet("/api/me/visits/{sessionId:guid}/receipt", async (
    Guid sessionId,
    IPlayerContextAccessor playerContextAccessor,
    PlatformDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var player = playerContextAccessor.Current;
    if (player is null)
    {
        return Results.Unauthorized();
    }

    var receipt = await PlayerHistoryProjector.GetVisitReceiptAsync(
        dbContext, player.PlayerAccountId, sessionId, cancellationToken);
    return receipt is null ? Results.NotFound() : Results.Ok(receipt);
}).RequireRateLimiting("player-me");
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~PortalReadsEndpointTests.Receipt`
Expected: PASS (3 tests).

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.Shared.Contracts/Players/PlayerVisitReceiptDto.cs src/AFK4.Shared.Contracts/Players/PlayerPurchaseLineDto.cs src/AFK4.Platform.Api/Players/PlayerHistoryProjector.cs src/AFK4.Platform.Api/Program.cs tests/AFK4.Platform.Api.Tests/PortalReadsEndpointTests.cs
git commit -m "feat(portal): GET /api/me/visits/{id}/receipt"
```

---

## Task 5: Purchase history (GET /api/me/purchases)

**Files:**
- Create: `src/AFK4.Shared.Contracts/Players/PlayerPurchaseDto.cs`
- Modify: `src/AFK4.Platform.Api/Players/PlayerHistoryProjector.cs` (add `GetPurchasesAsync`)
- Modify: `src/AFK4.Platform.Api/Program.cs`
- Test: `tests/AFK4.Platform.Api.Tests/PortalReadsEndpointTests.cs`

**Standalone** POS sales for this player — `PosSale.SessionId is null` (session-attached sales already show inside the visit receipt). Ordered `(CreatedAtUtc DESC, PosSaleId DESC)`, cursor-paginated, page size 20, each with its line items.

- [ ] **Step 1: Write the contract**

`src/AFK4.Shared.Contracts/Players/PlayerPurchaseDto.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace AFK4.Shared.Contracts.Players;

public sealed record PlayerPurchaseDto(
    Guid PosSaleId,
    DateTimeOffset CreatedAtUtc,
    long TotalMinorUnits,
    string CurrencyCode,
    IReadOnlyList<PlayerPurchaseLineDto> Lines);
```

- [ ] **Step 2: Write the failing tests**

Add to `PortalReadsEndpointTests.cs`:

```csharp
    private static async Task SeedStandalonePurchaseAsync(
        PlatformApiFactory factory, SeededPlayer p, DateTimeOffset createdAtUtc,
        long total, string productName, int quantity)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var posSaleId = Guid.NewGuid();
        db.PosSales.Add(new PosSaleEntity
        {
            PosSaleId = posSaleId, OrganizationId = p.OrgId, BranchId = p.BranchId,
            ShiftId = Guid.NewGuid(), CreatedByStaffUserId = Guid.NewGuid(),
            PlayerAccountId = p.PlayerId, SessionId = null, State = "paid",
            CurrencyCode = "TJS", TotalMinorUnits = total,
            CreatedAtUtc = createdAtUtc, PaidAtUtc = createdAtUtc
        });
        db.PosSaleLines.Add(new PosSaleLineEntity
        {
            PosSaleLineId = Guid.NewGuid(), PosSaleId = posSaleId,
            ProductId = Guid.NewGuid(), ProductName = productName, Quantity = quantity,
            CurrencyCode = "TJS", UnitPriceMinorUnits = total / quantity,
            LineTotalMinorUnits = total, TrackStock = false, AllowNegativeStock = true
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Purchases_ReturnsOnlyStandaloneOwnSales_WithLines()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        await SeedStandalonePurchaseAsync(factory, p, Now.AddDays(-1), 3_000, "Cola", 2);
        // Session-attached sale must NOT appear in purchases:
        await SeedEndedVisitAsync(factory, p, "Seat A", Now.AddDays(-2), receiptTotal: 5_000, attachedPosTotal: 1_500);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, "+992900000001", "1234");

        var page = await (await client.GetAsync("/api/me/purchases"))
            .Content.ReadFromJsonAsync<CursorPage<PlayerPurchaseDto>>();

        Assert.Single(page!.Items);
        var purchase = page.Items[0];
        Assert.Equal(3_000, purchase.TotalMinorUnits);
        Assert.Single(purchase.Lines);
        Assert.Equal("Cola", purchase.Lines[0].ProductName);
        Assert.Equal(2, purchase.Lines[0].Quantity);
    }

    [Fact]
    public async Task Purchases_DoesNotReturnOtherPlayersSales()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        var other = await SeedPlayerAsync(factory, "9999");
        await SeedStandalonePurchaseAsync(factory, other, Now.AddDays(-1), 7_000, "Pizza", 1);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, "+992900000001", "1234");

        var page = await (await client.GetAsync("/api/me/purchases"))
            .Content.ReadFromJsonAsync<CursorPage<PlayerPurchaseDto>>();

        Assert.Empty(page!.Items);
    }
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~PortalReadsEndpointTests.Purchases`
Expected: FAIL.

- [ ] **Step 4: Add the projector method**

Append to `PlayerHistoryProjector`:

```csharp
    public static async Task<CursorPage<PlayerPurchaseDto>> GetPurchasesAsync(
        PlatformDbContext dbContext,
        Guid playerAccountId,
        string? cursor,
        CancellationToken cancellationToken)
    {
        var query = dbContext.PosSales
            .AsNoTracking()
            .Where(sale =>
                sale.PlayerAccountId == playerAccountId &&
                sale.SessionId == null);

        if (CursorToken.TryDecode(cursor, out var afterTs, out var afterId))
        {
            query = query.Where(sale =>
                sale.CreatedAtUtc < afterTs ||
                (sale.CreatedAtUtc == afterTs && sale.PosSaleId.CompareTo(afterId) < 0));
        }

        var sales = await query
            .OrderByDescending(sale => sale.CreatedAtUtc)
            .ThenByDescending(sale => sale.PosSaleId)
            .Take(PageSize + 1)
            .ToListAsync(cancellationToken);

        var hasMore = sales.Count > PageSize;
        if (hasMore)
        {
            sales.RemoveAt(sales.Count - 1);
        }

        var saleIds = sales.Select(sale => sale.PosSaleId).ToList();
        var lines = await dbContext.PosSaleLines
            .AsNoTracking()
            .Where(line => saleIds.Contains(line.PosSaleId))
            .ToListAsync(cancellationToken);
        var linesBySale = lines
            .GroupBy(line => line.PosSaleId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var items = sales.Select(sale => new PlayerPurchaseDto(
            sale.PosSaleId,
            sale.CreatedAtUtc,
            sale.TotalMinorUnits,
            sale.CurrencyCode,
            (linesBySale.GetValueOrDefault(sale.PosSaleId) ?? new List<PosSaleLineEntity>())
                .Select(line => new PlayerPurchaseLineDto(
                    line.ProductName, line.Quantity, line.UnitPriceMinorUnits, line.LineTotalMinorUnits))
                .ToList()))
            .ToList();

        string? nextCursor = hasMore && items.Count > 0
            ? CursorToken.Encode(items[^1].CreatedAtUtc, items[^1].PosSaleId)
            : null;

        return new CursorPage<PlayerPurchaseDto>(items, nextCursor);
    }
```

- [ ] **Step 5: Register the endpoint**

```csharp
app.MapGet("/api/me/purchases", async (
    string? cursor,
    IPlayerContextAccessor playerContextAccessor,
    PlatformDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var player = playerContextAccessor.Current;
    if (player is null)
    {
        return Results.Unauthorized();
    }

    var page = await PlayerHistoryProjector.GetPurchasesAsync(
        dbContext, player.PlayerAccountId, cursor, cancellationToken);
    return Results.Ok(page);
}).RequireRateLimiting("player-me");
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~PortalReadsEndpointTests.Purchases`
Expected: PASS (2 tests).

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.Shared.Contracts/Players/PlayerPurchaseDto.cs src/AFK4.Platform.Api/Players/PlayerHistoryProjector.cs src/AFK4.Platform.Api/Program.cs tests/AFK4.Platform.Api.Tests/PortalReadsEndpointTests.cs
git commit -m "feat(portal): GET /api/me/purchases (paginated standalone POS)"
```

---

## Task 6: Profile self-edit (PATCH /api/me/profile)

**Files:**
- Create: `src/AFK4.Shared.Contracts/Players/UpdatePlayerProfileRequest.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs`
- Test: `tests/AFK4.Platform.Api.Tests/PortalReadsEndpointTests.cs`

The player edits **`PreferredLocale` and `MarketingOptIn`** only. Returns the updated `PlayerProfileDto`.

> **Deferred (intentional):** phone-number self-change is **out of scope** here. Spec §5.6 says a phone change re-triggers OTP verification, but OTP delivery is gated on the notifications SMS channel (not yet live). Allowing a phone edit now would strand the player unverified with no way to re-verify. Phone change moves to the OTP plan. `DisplayName` is likewise left to the operator for v1 (no requirement to self-edit it).

- [ ] **Step 1: Write the contract**

`src/AFK4.Shared.Contracts/Players/UpdatePlayerProfileRequest.cs`:

```csharp
namespace AFK4.Shared.Contracts.Players;

// Player-editable profile fields. Both optional; null means "leave unchanged".
public sealed record UpdatePlayerProfileRequest(
    string? PreferredLocale,
    bool? MarketingOptIn);
```

- [ ] **Step 2: Write the failing tests**

Add to `PortalReadsEndpointTests.cs`:

```csharp
    [Fact]
    public async Task PatchProfile_UpdatesLocaleAndMarketing()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, "+992900000001", "1234");

        var response = await client.PatchAsJsonAsync(
            "/api/me/profile",
            new UpdatePlayerProfileRequest("en", true));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<PlayerProfileDto>();
        Assert.Equal("en", dto!.PreferredLocale);
        Assert.True(dto.MarketingOptIn);
    }

    [Fact]
    public async Task PatchProfile_NullFields_LeaveValuesUnchanged()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");   // seeded locale "ru", marketing false
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, "+992900000001", "1234");

        var response = await client.PatchAsJsonAsync(
            "/api/me/profile",
            new UpdatePlayerProfileRequest(null, null));

        var dto = await response.Content.ReadFromJsonAsync<PlayerProfileDto>();
        Assert.Equal("ru", dto!.PreferredLocale);
        Assert.False(dto.MarketingOptIn);
    }

    [Fact]
    public async Task PatchProfile_WithoutToken_Returns401()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var response = await client.PatchAsJsonAsync(
            "/api/me/profile",
            new UpdatePlayerProfileRequest("en", true));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~PortalReadsEndpointTests.PatchProfile`
Expected: FAIL.

- [ ] **Step 4: Register the endpoint**

In `Program.cs`, near `GET /api/me/profile`:

```csharp
app.MapPatch("/api/me/profile", async (
    UpdatePlayerProfileRequest request,
    IPlayerContextAccessor playerContextAccessor,
    PlatformDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var player = playerContextAccessor.Current;
    if (player is null)
    {
        return Results.Unauthorized();
    }

    var account = await dbContext.PlayerAccounts.SingleOrDefaultAsync(
        candidate => candidate.PlayerAccountId == player.PlayerAccountId, cancellationToken);
    if (account is null)
    {
        return Results.Unauthorized();
    }

    if (request.PreferredLocale is not null)
    {
        var locale = request.PreferredLocale.Trim();
        if (locale.Length is 0 or > 16)
        {
            return Results.BadRequest(new { Error = "PreferredLocale must be 1-16 characters." });
        }

        account.PreferredLocale = locale;
    }

    if (request.MarketingOptIn is not null)
    {
        account.MarketingOptIn = request.MarketingOptIn.Value;
    }

    await dbContext.SaveChangesAsync(cancellationToken);

    return Results.Ok(new PlayerProfileDto(
        account.PlayerAccountId,
        account.DisplayName,
        account.PhoneNumber,
        player.PhoneVerified,
        account.PreferredLocale,
        account.MarketingOptIn));
}).RequireRateLimiting("player-me");
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~PortalReadsEndpointTests.PatchProfile`
Expected: PASS (3 tests).

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Shared.Contracts/Players/UpdatePlayerProfileRequest.cs src/AFK4.Platform.Api/Program.cs tests/AFK4.Platform.Api.Tests/PortalReadsEndpointTests.cs
git commit -m "feat(portal): PATCH /api/me/profile (locale + marketing)"
```

---

## Final verification

- [ ] **Run the full backend gate**

Run: `dotnet test tests/AFK4.Platform.Api.Tests`
Expected: PASS — all pre-existing tests (877) plus the new portal-reads + cursor tests, 0 failures.

- [ ] **Dispatch a final holistic code review** of the whole branch delta for this plan (security/isolation: every `/api/me/*` handler scoped to `PlayerContext.PlayerAccountId`; no caller-supplied id trusted; other-player reads return 404; money stays `long` minor units; no secrets; no AI signatures).

- [ ] **Use superpowers:finishing-a-development-branch** to present branch options.
