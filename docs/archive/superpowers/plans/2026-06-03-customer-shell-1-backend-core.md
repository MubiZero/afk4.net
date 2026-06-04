# Customer Shell — Unit 1: Backend Core Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the player-token backend primitives the shell needs — self-start / self-extend session endpoints, an operator pending-top-up list, the removal of the `PhoneVerified` top-up gate, and a typed warning + branding extension to the shell IPC contract.

**Architecture:** Reuse the existing operator `SessionCommandService` start/extend paths under a self-service actor sentinel (`Guid.Empty`), resolving the seat/branch from the calling device via `DeviceSeatAssignments`. Eligibility is a wallet-coverage pre-check using the pure `TariffBilling` function. The shell IPC contract (`PlayerShellStateDto`) gains a typed `WarningKind`, a configurable threshold, and a nullable `Branding` block, all with backward-compatible defaults so the existing WPF build keeps compiling. All work here is Linux-testable; the WPF view-model consumption of the new fields lives in Unit 4.

**Tech Stack:** .NET 10 Minimal API (`Program.cs`), EF Core (`PlatformDbContext`, InMemory in tests), xUnit + `WebApplicationFactory<Program>` (`PlatformApiFactory`).

---

## Scope & dependencies

- This plan is **Unit 1** of the customer-shell program (spec: `docs/superpowers/specs/2026-06-03-customer-shell-implementation-design.md`).
- **Downstream consumers:** Unit 2 (dcgate) builds on the gate removal; Unit 3 (operator web) consumes the pending-list endpoint + its DTO (`DisplayName` + `SeatName`); Unit 4 (WPF) consumes the new `PlayerShellStateDto` fields and the self-start/extend endpoints.
- All tasks gate on `dotnet test tests/AFK4.Platform.Api.Tests` (~936 passing today) and `dotnet test tests/AFK4.Shared.Contracts.Tests` (~115 passing today). Run from repo root `/home/fedya/projects/afk4.net`.

---

## Task 1: Extend the shell IPC contract (WarningKind + Branding + classify helper)

**Files:**
- Create: `src/AFK4.Shared.Contracts/Shell/PlayerShellWarningKinds.cs`
- Create: `src/AFK4.Shared.Contracts/Shell/ShellBrandingDto.cs`
- Create: `src/AFK4.Shared.Contracts/Shell/PlayerShellWarning.cs`
- Modify: `src/AFK4.Shared.Contracts/Shell/PlayerShellStateDto.cs`
- Test: `tests/AFK4.Shared.Contracts.Tests/PlayerShellContractSerializationTests.cs`
- Test: `tests/AFK4.Shared.Contracts.Tests/PlayerShellWarningTests.cs` (new)

- [ ] **Step 1: Write the failing serialization test for the new fields**

Add these two tests to `PlayerShellContractSerializationTests.cs`:

```csharp
    [Fact]
    public void State_DefaultsWarningKindToNone_AndBrandingToNull()
    {
        var state = new PlayerShellStateDto(
            OrganizationId: Guid.Empty,
            BranchId: Guid.Empty,
            DeviceId: Guid.Empty,
            State: PlayerShellStateNames.Locked,
            SessionId: null,
            LeaseExpiresAtUtc: null,
            RemainingSeconds: null,
            IsOnline: true,
            IsGraceMode: false,
            WarningThresholdSeconds: 300,
            Message: "This PC is locked.",
            LauncherApps: []);

        Assert.Equal(PlayerShellWarningKinds.None, state.WarningKind);
        Assert.Null(state.Branding);
    }

    [Fact]
    public void State_RoundTripsWarningKindAndBranding()
    {
        var state = new PlayerShellStateDto(
            OrganizationId: Guid.Empty,
            BranchId: Guid.Empty,
            DeviceId: Guid.Empty,
            State: PlayerShellStateNames.Active,
            SessionId: null,
            LeaseExpiresAtUtc: null,
            RemainingSeconds: 120,
            IsOnline: true,
            IsGraceMode: false,
            WarningThresholdSeconds: 300,
            Message: "Session is active.",
            LauncherApps: [],
            Locale: "ru",
            WarningKind: PlayerShellWarningKinds.LowTime,
            Branding: new ShellBrandingDto("Club AFK4", "https://cdn/x.png", "#c8ff00"));

        var copy = JsonSerializer.Deserialize<PlayerShellStateDto>(JsonSerializer.Serialize(state));

        Assert.NotNull(copy);
        Assert.Equal(PlayerShellWarningKinds.LowTime, copy.WarningKind);
        Assert.NotNull(copy.Branding);
        Assert.Equal("Club AFK4", copy.Branding!.ClubName);
        Assert.Equal("#c8ff00", copy.Branding.AccentColor);
    }
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter "FullyQualifiedName~PlayerShellContractSerializationTests"`
Expected: FAIL (compile error — `PlayerShellWarningKinds`, `ShellBrandingDto`, and the `WarningKind`/`Branding` parameters do not exist yet).

- [ ] **Step 3: Add the new contract types**

Create `src/AFK4.Shared.Contracts/Shell/PlayerShellWarningKinds.cs`:

```csharp
namespace AFK4.Shared.Contracts.Shell;

public static class PlayerShellWarningKinds
{
    public const string None = "none";

    public const string LowTime = "low_time";

    public const string LowBalance = "low_balance";

    public const string CreditLimit = "credit_limit";

    public const string Connectivity = "connectivity";
}
```

Create `src/AFK4.Shared.Contracts/Shell/ShellBrandingDto.cs`:

```csharp
namespace AFK4.Shared.Contracts.Shell;

public sealed record ShellBrandingDto(
    string ClubName,
    string? LogoUrl,
    string? AccentColor);
```

Modify `src/AFK4.Shared.Contracts/Shell/PlayerShellStateDto.cs` to append the two new optional parameters **after** `Locale` (keep `Locale` defaulted; append-only preserves the existing positional callers and serialization shape):

```csharp
public sealed record PlayerShellStateDto(
    Guid OrganizationId,
    Guid BranchId,
    Guid DeviceId,
    string State,
    Guid? SessionId,
    DateTimeOffset? LeaseExpiresAtUtc,
    int? RemainingSeconds,
    bool IsOnline,
    bool IsGraceMode,
    int WarningThresholdSeconds,
    string Message,
    IReadOnlyList<LauncherAppDto> LauncherApps,
    string Locale = "ru",
    string WarningKind = PlayerShellWarningKinds.None,
    ShellBrandingDto? Branding = null);
```

- [ ] **Step 4: Run the serialization tests to verify they pass**

Run: `dotnet test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter "FullyQualifiedName~PlayerShellContractSerializationTests"`
Expected: PASS (all 6 tests).

- [ ] **Step 5: Write the failing test for the warning classifier**

Create `tests/AFK4.Shared.Contracts.Tests/PlayerShellWarningTests.cs`:

```csharp
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Shared.Contracts.Tests;

public sealed class PlayerShellWarningTests
{
    [Fact]
    public void GraceState_ClassifiesAsCreditLimit()
    {
        var kind = PlayerShellWarning.Classify(
            PlayerShellStateNames.Grace, remainingSeconds: null, warningThresholdSeconds: 300, isGraceMode: true);

        Assert.Equal(PlayerShellWarningKinds.CreditLimit, kind);
    }

    [Fact]
    public void OfflineState_ClassifiesAsConnectivity()
    {
        var kind = PlayerShellWarning.Classify(
            PlayerShellStateNames.Offline, remainingSeconds: null, warningThresholdSeconds: 300, isGraceMode: false);

        Assert.Equal(PlayerShellWarningKinds.Connectivity, kind);
    }

    [Fact]
    public void ActiveBelowThreshold_ClassifiesAsLowTime()
    {
        var kind = PlayerShellWarning.Classify(
            PlayerShellStateNames.Active, remainingSeconds: 120, warningThresholdSeconds: 300, isGraceMode: false);

        Assert.Equal(PlayerShellWarningKinds.LowTime, kind);
    }

    [Fact]
    public void ActiveAboveThreshold_ClassifiesAsNone()
    {
        var kind = PlayerShellWarning.Classify(
            PlayerShellStateNames.Active, remainingSeconds: 1800, warningThresholdSeconds: 300, isGraceMode: false);

        Assert.Equal(PlayerShellWarningKinds.None, kind);
    }

    [Fact]
    public void LockedState_ClassifiesAsNone()
    {
        var kind = PlayerShellWarning.Classify(
            PlayerShellStateNames.Locked, remainingSeconds: null, warningThresholdSeconds: 300, isGraceMode: false);

        Assert.Equal(PlayerShellWarningKinds.None, kind);
    }
}
```

- [ ] **Step 6: Run the classifier test to verify it fails**

Run: `dotnet test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter "FullyQualifiedName~PlayerShellWarningTests"`
Expected: FAIL (`PlayerShellWarning` does not exist).

- [ ] **Step 7: Implement the classifier**

Create `src/AFK4.Shared.Contracts/Shell/PlayerShellWarning.cs`:

```csharp
namespace AFK4.Shared.Contracts.Shell;

public static class PlayerShellWarning
{
    // Maps the shell's coarse state + remaining-time into a typed warning kind so the kiosk can
    // pick a localized message and decide whether to surface the actionable "top up to keep playing"
    // panel. Grace = the auto-protection lock window (credit/limit reached); Offline = connectivity.
    public static string Classify(string state, int? remainingSeconds, int warningThresholdSeconds, bool isGraceMode)
    {
        if (isGraceMode || string.Equals(state, PlayerShellStateNames.Grace, StringComparison.Ordinal))
        {
            return PlayerShellWarningKinds.CreditLimit;
        }

        if (string.Equals(state, PlayerShellStateNames.Offline, StringComparison.Ordinal))
        {
            return PlayerShellWarningKinds.Connectivity;
        }

        if (string.Equals(state, PlayerShellStateNames.Active, StringComparison.Ordinal) &&
            remainingSeconds is not null && remainingSeconds <= warningThresholdSeconds)
        {
            return PlayerShellWarningKinds.LowTime;
        }

        return PlayerShellWarningKinds.None;
    }
}
```

- [ ] **Step 8: Run all Shared.Contracts tests to verify they pass**

Run: `dotnet test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj`
Expected: PASS (120 tests: 115 prior + 5 new classifier; the 2 new serialization tests bring the round-trip set to 6).

- [ ] **Step 9: Commit**

```bash
git add src/AFK4.Shared.Contracts/Shell tests/AFK4.Shared.Contracts.Tests
git commit -m "feat(shell-contract): typed WarningKind + Branding on PlayerShellStateDto"
```

---

## Task 2: Agent fills WarningKind, configurable threshold, and Branding

**Files:**
- Modify: `src/AFK4.Agent.Service/Worker.cs:239-261` (`CreatePlayerShellState`)
- Modify: the agent options class that holds `OrganizationId`/`BranchId`/`DeviceId`/`PreferredLocale` (find it: `grep -rn "PreferredLocale" src/AFK4.Agent.Service` — it is the `options.Value` type used in `CreatePlayerShellState`).
- Test: `tests/AFK4.Agent.Service.Tests/PlayerShellStateProjectionTests.cs` (new)

> **Note on the agent test baseline (env quirk):** `AFK4.Agent.Service.Tests` has 26 pre-existing WSL failures in `ClientReleaseAutomationTests` / `ExternalProcessAgentRestartSchedulerTests` (they need real Windows tooling). Filter to your own test class when checking green.

- [ ] **Step 1: Add the configurable threshold + branding fields to the agent options**

In the agent options class (the type behind `options.Value` in `CreatePlayerShellState`), add:

```csharp
    public int ShellWarningThresholdSeconds { get; set; } = 300;

    public string? ClubName { get; set; }

    public string? LogoUrl { get; set; }

    public string? AccentColor { get; set; }
```

(These are config-driven for v1. Wiring branding from the live backend/heartbeat is a small follow-up noted in the spec; the shell contract is the deliverable here.)

- [ ] **Step 2: Write the failing test for the projection**

Create `tests/AFK4.Agent.Service.Tests/PlayerShellStateProjectionTests.cs`. Use the same construction the existing agent tests use for `Worker`/options — inspect a sibling test in `tests/AFK4.Agent.Service.Tests` for the exact options/DI setup and mirror it. The assertions:

```csharp
using AFK4.Shared.Contracts.Shell;
using Xunit;

namespace AFK4.Agent.Service.Tests;

public sealed class PlayerShellStateProjectionTests
{
    [Fact]
    public void ActiveStateBelowThreshold_ProjectsLowTimeWarning_AndConfiguredThreshold_AndBranding()
    {
        // Arrange: options with a 120s threshold + branding; a runtime state = active with ~60s left.
        // (Build the Worker/projection exactly as the sibling agent tests do; set
        //  ShellWarningThresholdSeconds = 120, ClubName = "Club AFK4", AccentColor = "#c8ff00".)
        var dto = ProjectActiveStateWithRemainingSeconds(60, thresholdSeconds: 120,
            clubName: "Club AFK4", accentColor: "#c8ff00");

        Assert.Equal(120, dto.WarningThresholdSeconds);
        Assert.Equal(PlayerShellWarningKinds.LowTime, dto.WarningKind);
        Assert.NotNull(dto.Branding);
        Assert.Equal("Club AFK4", dto.Branding!.ClubName);
        Assert.Equal("#c8ff00", dto.Branding.AccentColor);
    }
}
```

Replace `ProjectActiveStateWithRemainingSeconds` with the concrete `Worker.CreatePlayerShellState` invocation pattern used by the neighbouring tests (those tests already exercise the state projection — copy their setup). If `CreatePlayerShellState` is `private`, either make it `internal` and add `[assembly: InternalsVisibleTo("AFK4.Agent.Service.Tests")]` (check whether the project already exposes internals to its test assembly — `grep -rn InternalsVisibleTo src/AFK4.Agent.Service`), or extract the projection into an `internal static` helper that takes the options + runtime state and returns the DTO, and test that.

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --filter "FullyQualifiedName~PlayerShellStateProjectionTests"`
Expected: FAIL (`WarningThresholdSeconds` still hardcoded 300; `WarningKind`/`Branding` not populated).

- [ ] **Step 4: Update `CreatePlayerShellState`**

Replace the hardcoded `WarningThresholdSeconds: 300` and add `WarningKind`/`Branding`:

```csharp
    private PlayerShellStateDto CreatePlayerShellState(AgentRuntimeState runtimeState)
    {
        var agentOptions = options.Value;
        var lease = leaseStore.Current;
        int? remainingSeconds = lease is null
            ? null
            : Math.Max(0, (int)(lease.ExpiresAtUtc - DateTimeOffset.UtcNow).TotalSeconds);

        var isGraceMode = string.Equals(runtimeState.State, PlayerShellStateNames.Grace, StringComparison.Ordinal);
        var threshold = agentOptions.ShellWarningThresholdSeconds;

        return new PlayerShellStateDto(
            OrganizationId: agentOptions.OrganizationId,
            BranchId: agentOptions.BranchId,
            DeviceId: agentOptions.DeviceId,
            State: runtimeState.State,
            SessionId: lease?.SessionId ?? runtimeState.ActiveSessionId,
            LeaseExpiresAtUtc: lease?.ExpiresAtUtc ?? runtimeState.LeaseExpiresAtUtc,
            RemainingSeconds: remainingSeconds,
            IsOnline: true,
            IsGraceMode: isGraceMode,
            WarningThresholdSeconds: threshold,
            Message: CreatePlayerShellMessage(runtimeState),
            LauncherApps: [],
            Locale: agentOptions.PreferredLocale,
            WarningKind: PlayerShellWarning.Classify(runtimeState.State, remainingSeconds, threshold, isGraceMode),
            Branding: string.IsNullOrWhiteSpace(agentOptions.ClubName)
                ? null
                : new ShellBrandingDto(agentOptions.ClubName!, agentOptions.LogoUrl, agentOptions.AccentColor));
    }
```

Add `using AFK4.Shared.Contracts.Shell;` if not already present.

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --filter "FullyQualifiedName~PlayerShellStateProjectionTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Agent.Service tests/AFK4.Agent.Service.Tests
git commit -m "feat(agent): project typed WarningKind, configurable threshold, branding into shell state"
```

---

## Task 3: Remove the PhoneVerified gate on top-up-intent

**Files:**
- Modify: `src/AFK4.Platform.Api/Program.cs:819-835` (the `POST /api/me/wallet/top-up-intent` handler)
- Test: `tests/AFK4.Platform.Api.Tests/PortalWritesEndpointTests.cs`

- [ ] **Step 1: Rewrite the failing gate test to assert the new behaviour**

In `PortalWritesEndpointTests.cs`, replace `CreateTopUpIntent_WithUnverifiedPhone_Returns403` with:

```csharp
    [Fact]
    public async Task CreateTopUpIntent_WithUnverifiedPhone_StillCreatesPendingIntent()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234", phoneVerified: false);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, p.Phone, "1234");

        var response = await client.PostAsJsonAsync(
            "/api/me/wallet/top-up-intent",
            new PlayerTopUpIntentRequest(10_000, null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<PlayerTopUpIntentDto>();
        Assert.Equal("pending", dto!.State);
    }
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~CreateTopUpIntent_WithUnverifiedPhone"`
Expected: FAIL (currently returns 403 Forbidden).

- [ ] **Step 3: Remove the gate**

In `Program.cs`, delete the gate block from the `POST /api/me/wallet/top-up-intent` handler:

```csharp
    // D8 gate: verified phone required for money actions.
    if (!player.PhoneVerified)
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }
```

(Money only moves on confirmation — operator fulfil or, in Unit 2, the dcgate webhook — so the gate added friction without protection. Decision C in the spec.)

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~CreateTopUpIntent"`
Expected: PASS (the verified-phone test and the now-allowed unverified test both green).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Api/Program.cs tests/AFK4.Platform.Api.Tests/PortalWritesEndpointTests.cs
git commit -m "feat(player-api): drop PhoneVerified gate on top-up-intent (money is confirmation-gated)"
```

---

## Task 4: Operator pending-top-up list endpoint

**Files:**
- Create: `src/AFK4.Shared.Contracts/Players/OperatorTopUpIntentDto.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs` (add the endpoint near the other `/api/wallet/top-up-intents` routes)
- Test: `tests/AFK4.Platform.Api.Tests/OperatorTopUpListEndpointTests.cs` (new)

- [ ] **Step 1: Write the failing test**

Create `tests/AFK4.Platform.Api.Tests/OperatorTopUpListEndpointTests.cs`. Mirror `PortalWritesEndpointTests` seed helpers + `StaffAuthTestHelper.AuthorizeAsAsync` (which signs in a staff user holding a role). Seed a pending `PaymentIntent` for a player, then list it as a staff actor with `TopUpWallet`:

```csharp
using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Players;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public sealed class OperatorTopUpListEndpointTests
{
    [Fact]
    public async Task PendingList_ReturnsPendingIntentsWithPlayerName_ForBranch()
    {
        await using var factory = new PlatformApiFactory();

        // Seed a staff user with TopUpWallet, in TestIds.OrganizationId / TestIds.BranchId.
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, "Owner"); // a role that holds billing.wallet.top_up

        var playerId = Guid.NewGuid();
        var intentId = Guid.NewGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            db.PlayerAccounts.Add(new PlayerAccountEntity
            {
                PlayerAccountId = playerId,
                OrganizationId = TestIds.OrganizationId,
                HomeBranchId = TestIds.BranchId,
                DisplayName = "Alisher",
                PhoneNumber = "+992900000123",
                PreferredLocale = "ru",
                MarketingOptIn = false,
                IsActive = true,
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
            db.PaymentIntents.Add(new PaymentIntentEntity
            {
                PaymentIntentId = intentId,
                PlayerAccountId = playerId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                AmountMinorUnits = 5_000,
                CurrencyCode = "TJS",
                Purpose = "wallet_topup",
                State = "pending",
                Method = "counter",
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync(
            $"/api/branches/{TestIds.BranchId}/wallet/top-up-intents?status=pending");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<OperatorTopUpIntentDto>>();
        Assert.NotNull(list);
        var item = Assert.Single(list!);
        Assert.Equal(intentId, item.PaymentIntentId);
        Assert.Equal("Alisher", item.DisplayName);
        Assert.Equal(5_000, item.AmountMinorUnits);
        Assert.Equal("pending", item.State);
    }
}
```

> Confirm which seeded role holds `StaffPermissionNames.TopUpWallet` (`grep -rn "TopUpWallet" src/AFK4.Platform.Api` for the permission string `billing.wallet.top_up`, and check `StaffAuthTestHelper`/role seeding for a role that includes it). Use that role name in `AuthorizeAsAsync`.

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~OperatorTopUpListEndpointTests"`
Expected: FAIL (compile error — `OperatorTopUpIntentDto` and the endpoint do not exist).

- [ ] **Step 3: Add the DTO**

Create `src/AFK4.Shared.Contracts/Players/OperatorTopUpIntentDto.cs`:

```csharp
namespace AFK4.Shared.Contracts.Players;

public sealed record OperatorTopUpIntentDto(
    Guid PaymentIntentId,
    Guid PlayerAccountId,
    string DisplayName,
    long AmountMinorUnits,
    string CurrencyCode,
    string State,
    string Method,
    DateTimeOffset CreatedAtUtc,
    string? SeatName);
```

- [ ] **Step 4: Add the endpoint**

In `Program.cs`, near the existing `/api/wallet/top-up-intents/{intentId:guid}/fulfil` route, add (mirror the org-scoping + `RequireBranchPermissionAsync(TopUpWallet)` pattern from `fulfil`):

```csharp
app.MapGet("/api/branches/{branchId:guid}/wallet/top-up-intents", async (
    Guid branchId,
    string? status,
    StaffAuthorizationService authorizationService,
    PlatformDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequireBranchPermissionAsync(
        branchId,
        StaffPermissionNames.TopUpWallet,
        cancellationToken);

    if (!authorization.IsAuthenticated)
    {
        return Results.Unauthorized();
    }

    if (!authorization.IsAllowed)
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var organizationId = authorization.StaffContext!.OrganizationId;
    var stateFilter = string.IsNullOrWhiteSpace(status) ? "pending" : status;

    // Left-join the player's current active-session seat so the operator sees who + where.
    var items = await (
        from intent in dbContext.PaymentIntents.AsNoTracking()
        join account in dbContext.PlayerAccounts.AsNoTracking()
            on intent.PlayerAccountId equals account.PlayerAccountId
        where intent.OrganizationId == organizationId &&
            intent.BranchId == branchId &&
            intent.State == stateFilter
        orderby intent.CreatedAtUtc descending
        select new OperatorTopUpIntentDto(
            intent.PaymentIntentId,
            intent.PlayerAccountId,
            account.DisplayName,
            intent.AmountMinorUnits,
            intent.CurrencyCode,
            intent.State,
            intent.Method,
            intent.CreatedAtUtc,
            (from session in dbContext.Sessions.AsNoTracking()
             join seat in dbContext.Seats.AsNoTracking() on session.SeatId equals seat.SeatId
             where session.PlayerAccountId == intent.PlayerAccountId &&
                 session.BranchId == branchId &&
                 session.State == "active"
             select seat.Name).FirstOrDefault()))
        .ToListAsync(cancellationToken);

    return Results.Ok(items);
});
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~OperatorTopUpListEndpointTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Shared.Contracts/Players/OperatorTopUpIntentDto.cs src/AFK4.Platform.Api/Program.cs tests/AFK4.Platform.Api.Tests/OperatorTopUpListEndpointTests.cs
git commit -m "feat(operator-api): list pending top-up intents per branch with player name + seat"
```

---

## Task 5: Self-start session under a player token

**Files:**
- Create: `src/AFK4.Shared.Contracts/Players/PlayerSelfStartRequest.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs` (add `POST /api/me/sessions/start`)
- Test: `tests/AFK4.Platform.Api.Tests/PlayerSelfSessionEndpointTests.cs` (new)

**Design:** the shell sends its `deviceId` (from the IPC state) + a `tariffRuleVersionId` + an `idempotencyKey`. The endpoint resolves the player from the token, resolves the **seat + branch from the device** via `DeviceSeatAssignments` (active, approved device), pre-checks that the wallet covers the minimum-billable charge (`TariffBilling.ComputeForMinutes`), then reuses the operator `SessionCommandService.StartGuestSessionAsync` with a self-service actor sentinel `Guid.Empty`, `PlayerAccountId` from the token, `DurationMode=Fixed`, and `BillingMode=BillingModeNames.PrepaidWallet`. Below-minimum balance returns a recoverable `409 { error = "insufficient_balance" }` that the shell turns into the top-up flow.

- [ ] **Step 1: Write the failing tests**

Create `tests/AFK4.Platform.Api.Tests/PlayerSelfSessionEndpointTests.cs`. Seed: a player with a verified credential + a wallet ledger balance; a seat in `TestIds.BranchId`; an **approved device** + an active `DeviceSeatAssignmentEntity` linking that device to the seat; a `TariffVersionEntity` (50/min, min 30, increment 15). Then call self-start. (Mirror `SeedPlayerAsync`/`AuthenticateAsync`/`SeedLedgerAsync` from `PortalReadsEndpointTests`/`PortalWritesEndpointTests`, and the seat/tariff/device seeding from `SeedActiveOpenSessionAsync`; add the `DeviceSeatAssignmentEntity` + an approved `DeviceEntity`.)

```csharp
    [Fact]
    public async Task SelfStart_WithSufficientWallet_StartsSession()
    {
        await using var factory = new PlatformApiFactory(useRealSessionBilling: true);
        var ctx = await SeedSelfStartContextAsync(factory, walletMinorUnits: 100_000); // covers min charge
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, ctx.OrgId, ctx.Phone, "1234");

        var response = await client.PostAsJsonAsync("/api/me/sessions/start",
            new PlayerSelfStartRequest(ctx.DeviceId, ctx.TariffRuleVersionId, 60, Guid.NewGuid().ToString("N")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var session = await db.Sessions.SingleAsync(s => s.PlayerAccountId == ctx.PlayerId);
        Assert.Equal("active", session.State);
        Assert.Equal(ctx.SeatId, session.SeatId);
    }

    [Fact]
    public async Task SelfStart_WithInsufficientWallet_Returns409InsufficientBalance()
    {
        await using var factory = new PlatformApiFactory(useRealSessionBilling: true);
        var ctx = await SeedSelfStartContextAsync(factory, walletMinorUnits: 100); // below min charge
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, ctx.OrgId, ctx.Phone, "1234");

        var response = await client.PostAsJsonAsync("/api/me/sessions/start",
            new PlayerSelfStartRequest(ctx.DeviceId, ctx.TariffRuleVersionId, 60, Guid.NewGuid().ToString("N")));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("insufficient_balance", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task SelfStart_WithoutToken_IsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/me/sessions/start",
            new PlayerSelfStartRequest(Guid.NewGuid(), Guid.NewGuid().ToString("D"), 60, Guid.NewGuid().ToString("N")));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
```

Write `SeedSelfStartContextAsync` to seed the player + credential + wallet ledger + seat + approved device + `DeviceSeatAssignmentEntity` + `TariffVersionEntity`, returning a small record `(OrgId, BranchId, PlayerId, Phone, DeviceId, SeatId, TariffRuleVersionId)`. The tariff version's `TariffRuleVersionId` string = `tariffVersionId.ToString("D")` (matches how `SeedActiveOpenSessionAsync` stores it).

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~PlayerSelfSessionEndpointTests"`
Expected: FAIL (compile error — `PlayerSelfStartRequest` + endpoint missing).

- [ ] **Step 3: Add the request contract**

Create `src/AFK4.Shared.Contracts/Players/PlayerSelfStartRequest.cs`:

```csharp
namespace AFK4.Shared.Contracts.Players;

public sealed record PlayerSelfStartRequest(
    Guid DeviceId,
    string TariffRuleVersionId,
    int DurationMinutes,
    string IdempotencyKey);
```

- [ ] **Step 4: Add the endpoint**

In `Program.cs` (near the other `/api/me/*` routes), add. Note: read `IPlayerContextAccessor.Current`; resolve seat/branch from the device via `DeviceSeatAssignments`; load the bound `TariffVersionEntity` to build `TariffPricing`; pre-check wallet via `LedgerBalanceProjector.GetWalletSummaryAsync`; then call `StartGuestSessionAsync` with `Guid.Empty` actor.

```csharp
app.MapPost("/api/me/sessions/start", async (
    PlayerSelfStartRequest request,
    IPlayerContextAccessor playerContextAccessor,
    PlatformDbContext dbContext,
    ISessionCommandService sessionCommandService,
    CancellationToken cancellationToken) =>
{
    var player = playerContextAccessor.Current;
    if (player is null)
    {
        return Results.Unauthorized();
    }

    // Resolve the seat + branch from the calling device (active, approved assignment).
    var assignment = await (
        from a in dbContext.DeviceSeatAssignments.AsNoTracking()
        join d in dbContext.Devices.AsNoTracking() on a.DeviceId equals d.DeviceId
        where a.DeviceId == request.DeviceId &&
            a.OrganizationId == player.OrganizationId &&
            a.DetachedAtUtc == null &&
            d.EnrollmentState == DeviceEnrollmentStateNames.Approved
        orderby a.AttachedAtUtc descending
        select a).FirstOrDefaultAsync(cancellationToken);

    if (assignment is null)
    {
        return Results.NotFound(new { error = "device_not_assigned" });
    }

    // Eligibility: the wallet must cover the minimum-billable charge for the chosen duration.
    if (!Guid.TryParse(request.TariffRuleVersionId, out var tariffVersionId))
    {
        return Results.BadRequest(new { error = "invalid_tariff" });
    }

    var version = await dbContext.TariffVersions.AsNoTracking().SingleOrDefaultAsync(
        v => v.OrganizationId == player.OrganizationId &&
            v.BranchId == assignment.BranchId &&
            v.TariffVersionId == tariffVersionId,
        cancellationToken);

    if (version is null)
    {
        return Results.BadRequest(new { error = "invalid_tariff" });
    }

    var pricing = new TariffPricing(
        version.PricePerMinuteMinorUnits,
        version.MinimumBillableMinutes,
        version.RoundingIncrementMinutes,
        version.CurrencyCode);
    var charge = TariffBilling.ComputeForMinutes(request.DurationMinutes, pricing);
    if (charge is null)
    {
        return Results.BadRequest(new { error = "invalid_duration" });
    }

    var wallet = await LedgerBalanceProjector.GetWalletSummaryAsync(
        dbContext, player.OrganizationId, assignment.BranchId, player.PlayerAccountId, cancellationToken);
    var walletBalance = wallet?.WalletBalance.MinorUnits ?? 0;
    if (walletBalance < charge.AmountMinorUnits)
    {
        return Results.Conflict(new { error = "insufficient_balance" });
    }

    var startRequest = new StartGuestSessionRequest(
        OrganizationId: player.OrganizationId,
        SeatId: assignment.SeatId,
        TariffRuleVersionId: request.TariffRuleVersionId,
        IdempotencyKey: request.IdempotencyKey,
        DurationMode: SessionDurationModes.Fixed,
        DurationMinutes: request.DurationMinutes,
        PlayerAccountId: player.PlayerAccountId,
        BillingMode: BillingModeNames.PrepaidWallet);

    var result = await sessionCommandService.StartGuestSessionAsync(
        assignment.BranchId, Guid.Empty, startRequest, cancellationToken);

    if (result.Conflict)
    {
        return Results.Conflict(new { error = result.Error });
    }

    if (result.NotFound)
    {
        return Results.NotFound(new { error = result.Error });
    }

    if (!result.Succeeded)
    {
        return Results.BadRequest(new { error = result.Error });
    }

    return Results.Ok(result.Response);
}).RequireRateLimiting("player-me");
```

Confirm the exact `GetWalletSummaryAsync` parameter list by reading `src/AFK4.Platform.Api/Billing/LedgerBalanceProjector.cs:13` and adjust the call to match (org/branch/player ordering). Add any missing `using` (`AFK4.Platform.Api.Billing`, `AFK4.Platform.Api.Devices`, `AFK4.Shared.Contracts.Sessions`).

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~PlayerSelfSessionEndpointTests"`
Expected: PASS (all 3).

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Shared.Contracts/Players/PlayerSelfStartRequest.cs src/AFK4.Platform.Api/Program.cs tests/AFK4.Platform.Api.Tests/PlayerSelfSessionEndpointTests.cs
git commit -m "feat(player-api): self-start session under player token (wallet-eligibility gated)"
```

---

## Task 6: Self-extend session under a player token

**Files:**
- Create: `src/AFK4.Shared.Contracts/Players/PlayerSelfExtendRequest.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs` (add `POST /api/me/sessions/{sessionId:guid}/extend`)
- Test: `tests/AFK4.Platform.Api.Tests/PlayerSelfSessionEndpointTests.cs` (extend the same file)

**Design:** load the session; authorize that the token's `PlayerAccountId` **owns** it and it is `active`; pre-check the wallet covers the extension charge; reuse `ExtendSessionAsync` with `Guid.Empty` actor and `BillingMode=PrepaidWallet`. Foreign/missing session → 404 (no existence disclosure).

- [ ] **Step 1: Write the failing tests**

Add to `PlayerSelfSessionEndpointTests.cs`:

```csharp
    [Fact]
    public async Task SelfExtend_OwnedActiveSession_WithWallet_Extends()
    {
        await using var factory = new PlatformApiFactory(useRealSessionBilling: true);
        var ctx = await SeedSelfStartContextAsync(factory, walletMinorUnits: 1_000_000);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, ctx.OrgId, ctx.Phone, "1234");

        // Start a session first (reuse the self-start endpoint).
        var start = await client.PostAsJsonAsync("/api/me/sessions/start",
            new PlayerSelfStartRequest(ctx.DeviceId, ctx.TariffRuleVersionId, 60, Guid.NewGuid().ToString("N")));
        start.EnsureSuccessStatusCode();
        Guid sessionId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            sessionId = (await db.Sessions.SingleAsync(s => s.PlayerAccountId == ctx.PlayerId)).SessionId;
        }

        var response = await client.PostAsJsonAsync($"/api/me/sessions/{sessionId}/extend",
            new PlayerSelfExtendRequest(30, Guid.NewGuid().ToString("N")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SelfExtend_ForeignSession_Returns404()
    {
        await using var factory = new PlatformApiFactory(useRealSessionBilling: true);
        var ctx = await SeedSelfStartContextAsync(factory, walletMinorUnits: 1_000_000);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, ctx.OrgId, ctx.Phone, "1234");

        var response = await client.PostAsJsonAsync($"/api/me/sessions/{Guid.NewGuid()}/extend",
            new PlayerSelfExtendRequest(30, Guid.NewGuid().ToString("N")));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~SelfExtend"`
Expected: FAIL (`PlayerSelfExtendRequest` + endpoint missing).

- [ ] **Step 3: Add the request contract**

Create `src/AFK4.Shared.Contracts/Players/PlayerSelfExtendRequest.cs`:

```csharp
namespace AFK4.Shared.Contracts.Players;

public sealed record PlayerSelfExtendRequest(
    int AdditionalMinutes,
    string IdempotencyKey);
```

- [ ] **Step 4: Add the endpoint**

In `Program.cs`:

```csharp
app.MapPost("/api/me/sessions/{sessionId:guid}/extend", async (
    Guid sessionId,
    PlayerSelfExtendRequest request,
    IPlayerContextAccessor playerContextAccessor,
    PlatformDbContext dbContext,
    ISessionCommandService sessionCommandService,
    CancellationToken cancellationToken) =>
{
    var player = playerContextAccessor.Current;
    if (player is null)
    {
        return Results.Unauthorized();
    }

    var session = await dbContext.Sessions.AsNoTracking().SingleOrDefaultAsync(
        s => s.SessionId == sessionId, cancellationToken);

    // Ownership-scoped: a session the caller does not own is indistinguishable from a missing one.
    if (session is null ||
        session.PlayerAccountId != player.PlayerAccountId ||
        session.State != "active")
    {
        return Results.NotFound();
    }

    if (!Guid.TryParse(session.TariffRuleVersionId, out var tariffVersionId))
    {
        return Results.BadRequest(new { error = "invalid_tariff" });
    }

    var version = await dbContext.TariffVersions.AsNoTracking().SingleOrDefaultAsync(
        v => v.OrganizationId == session.OrganizationId &&
            v.BranchId == session.BranchId &&
            v.TariffVersionId == tariffVersionId,
        cancellationToken);

    if (version is null)
    {
        return Results.BadRequest(new { error = "invalid_tariff" });
    }

    var pricing = new TariffPricing(
        version.PricePerMinuteMinorUnits,
        version.MinimumBillableMinutes,
        version.RoundingIncrementMinutes,
        version.CurrencyCode);
    var charge = TariffBilling.ComputeForMinutes(request.AdditionalMinutes, pricing);
    if (charge is null)
    {
        return Results.BadRequest(new { error = "invalid_duration" });
    }

    var wallet = await LedgerBalanceProjector.GetWalletSummaryAsync(
        dbContext, session.OrganizationId, session.BranchId, player.PlayerAccountId, cancellationToken);
    if ((wallet?.WalletBalance.MinorUnits ?? 0) < charge.AmountMinorUnits)
    {
        return Results.Conflict(new { error = "insufficient_balance" });
    }

    var extendRequest = new ExtendSessionRequest(
        AdditionalMinutes: request.AdditionalMinutes,
        TariffRuleVersionId: session.TariffRuleVersionId,
        IdempotencyKey: request.IdempotencyKey,
        PlayerAccountId: player.PlayerAccountId,
        BillingMode: BillingModeNames.PrepaidWallet);

    var result = await sessionCommandService.ExtendSessionAsync(
        sessionId, Guid.Empty, extendRequest, cancellationToken);

    if (result.Conflict)
    {
        return Results.Conflict(new { error = result.Error });
    }

    if (result.NotFound)
    {
        return Results.NotFound();
    }

    if (!result.Succeeded)
    {
        return Results.BadRequest(new { error = result.Error });
    }

    return Results.Ok(result.Response);
}).RequireRateLimiting("player-me");
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~PlayerSelfSessionEndpointTests"`
Expected: PASS (all 5 in the file).

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Shared.Contracts/Players/PlayerSelfExtendRequest.cs src/AFK4.Platform.Api/Program.cs tests/AFK4.Platform.Api.Tests/PlayerSelfSessionEndpointTests.cs
git commit -m "feat(player-api): self-extend owned active session under player token"
```

---

## Verification gate

Both suites must be green before Unit 1 is done:

```bash
dotnet test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj   # ~120 pass (115 + new)
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj           # ~941 pass (936 + new), 0 fail
```

For the agent suite, filter to your own class (26 `ClientReleaseAutomation`/`ExternalProcessAgentRestartScheduler` failures are a pre-existing WSL baseline, not a regression):

```bash
dotnet test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --filter "FullyQualifiedName~PlayerShellStateProjectionTests"
```

## Notes for downstream units

- **Unit 2 (dcgate):** the gate removed in Task 3 lets unverified players create intents; the dcgate webhook becomes the confirmation actor.
- **Unit 3 (operator web):** consumes `GET /api/branches/{branchId}/wallet/top-up-intents?status=pending` → `OperatorTopUpIntentDto` (carries `DisplayName` + `SeatName`).
- **Unit 4 (WPF):** consumes `PlayerShellStateDto.WarningKind` / `.Branding` and the `POST /api/me/sessions/start` + `/api/me/sessions/{id}/extend` endpoints. The view-model wiring of the new shell fields is Unit 4's work (Windows-gated).
