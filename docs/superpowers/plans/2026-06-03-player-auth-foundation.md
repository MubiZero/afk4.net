# Player Auth Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the shared player-authentication foundation (player credentials + opaque token edge + `/api/public/*` sign-in/refresh + `/api/me/*` protected edge + rate limiter) that both the customer portal and the in-PC shell will sit on top of.

**Architecture:** Mirror the existing staff-auth stack exactly, in a separate, additive surface that never touches the staff/admin middlewares or tables. One `PlayerAccount` identity; credentials live in a 1:1 `PlayerCredentialEntity`; tokens are opaque SHA-256-hashed bearer tokens in dedicated tables (1h access / 30d refresh — shorter than staff's 8h because customer devices are less trusted). A new `PlayerAuthenticationMiddleware` validates `Authorization: Bearer` and pins a `PlayerContext` onto an `IPlayerContextAccessor` for `/api/me/*` only. A new ASP.NET Core rate limiter (first use in the codebase) protects the public and `me` groups.

**Tech Stack:** ASP.NET Core minimal APIs, EF Core 10 (`dotnet-ef` 10.0.4 local tool), `Microsoft.AspNetCore.Identity.PasswordHasher<T>`, `System.Threading.RateLimiting` / `Microsoft.AspNetCore.RateLimiting`, xUnit + `WebApplicationFactory<Program>` (in-memory DB).

**Reconciliation decisions baked in (verified against current code):**
- `PlayerAccountEntity` **already** has `Email` and `PreferredLocale` (added by the notifications track). We **reuse** `PreferredLocale` for the player's language and add only `MarketingOptIn`. Do **not** add a `PreferredLanguage` field.
- PIN and password are **one** `PasswordHash` on `PlayerCredentialEntity` (a PIN is a short numeric password). The shell's PIN login reuses this same credential.
- No shared `IPasswordHasher` exists today (staff/admin each `new PasswordHasher<T>()`). We mirror that: `PlayerCredentialService` owns a `PasswordHasher<PlayerCredentialEntity>`. No staff-side refactor.
- OTP is **stubbed/deferred** (no SMS delivery yet — notifications Stage 6). Sign-in is password/PIN only. Operator sets a player's initial PIN at the counter (Task 10).
- Token shape is the portal's opaque-hashed pattern (mirrors `OpaqueStaffTokenService`). The shell's device-bound variant is a later concern in the shell plan and reuses `PlayerCredentialService`.

**Conventions reference (verbatim source patterns):** see `OpaqueStaffTokenService.cs`, `StaffAccessTokenEntity.cs`, `StaffAuthenticationMiddleware.cs`, `PasswordHashingStaffCredentialService.cs`, `PlatformDbContext.cs`, `Program.cs`, `tests/AFK4.Platform.Api.Tests/PlatformApiFactory.cs`, `StaffAuthenticationEndpointTests.cs`.

**Migration command (local tool):** from repo root, `dotnet tool restore` once, then
`dotnet ef migrations add <Name> --project src/AFK4.Platform.Api --startup-project src/AFK4.Platform.Api`.
Tests use the in-memory provider (`PlatformApiFactory`), so they pass without a generated migration; migrations are required for production schema and are committed alongside each entity.

**Backend test gate (run after each task that touches the API):**
`dotnet test tests/AFK4.Platform.Api.Tests`

---

## File Structure

**New files (Platform.Api):**
- `Data/PlayerCredentialEntity.cs` — 1:1 credential + lockout/verification state for a player.
- `Data/PlayerAccessTokenEntity.cs`, `Data/PlayerRefreshTokenEntity.cs` — opaque token tables (mirror staff).
- `Identity/PlayerContext.cs` — validated player principal record.
- `Identity/IPlayerContextAccessor.cs`, `Identity/PlayerContextAccessor.cs` — per-request accessor.
- `Identity/IPlayerTokenService.cs`, `Identity/OpaquePlayerTokenService.cs` — issue/refresh/validate.
- `Identity/IPlayerCredentialService.cs`, `Identity/PlayerCredentialService.cs` — sign-in, lockout, set-credential.
- `Identity/PlayerAuthenticationMiddleware.cs` — `/api/me/*` bearer validation.

**New files (Shared.Contracts):**
- `Players/PlayerSignInRequest.cs`, `Players/PlayerSignInResponse.cs`, `Players/PlayerRefreshRequest.cs`, `Players/PlayerProfileDto.cs`, `Players/SetPlayerPinRequest.cs`.

**Modified files:**
- `Data/PlayerAccountEntity.cs` — add `MarketingOptIn`.
- `Data/PlatformDbContext.cs` — DbSets + fluent config for the 3 new entities; `MarketingOptIn` default.
- `Program.cs` — DI registrations, `AddRateLimiter`, `UseRateLimiter`, `UseMiddleware<PlayerAuthenticationMiddleware>`, public + me endpoints, operator set-PIN endpoint.

**New test files:**
- `tests/AFK4.Platform.Api.Tests/PlayerAuthenticationEndpointTests.cs` — sign-in/refresh/lockout/isolation/rate-limit/cross-surface rejection.
- `tests/AFK4.Platform.Api.Tests/PlayerCredentialServiceTests.cs` — verify/lockout unit-ish via service through DI scope (optional, folded into endpoint tests where simpler).

---

## Task 1: PlayerCredentialEntity + EF config + migration

**Files:**
- Create: `src/AFK4.Platform.Api/Data/PlayerCredentialEntity.cs`
- Modify: `src/AFK4.Platform.Api/Data/PlatformDbContext.cs` (DbSet + fluent config)
- Test: `tests/AFK4.Platform.Api.Tests/PlayerAuthenticationEndpointTests.cs` (roundtrip)

- [ ] **Step 1: Write the failing test**

Create `tests/AFK4.Platform.Api.Tests/PlayerAuthenticationEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Players;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

public sealed class PlayerAuthenticationEndpointTests
{
    [Fact]
    public async Task PlayerCredentialEntity_RoundTrips()
    {
        await using var factory = new PlatformApiFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var playerAccountId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        db.PlayerCredentials.Add(new PlayerCredentialEntity
        {
            PlayerCredentialId = Guid.NewGuid(),
            PlayerAccountId = playerAccountId,
            OrganizationId = organizationId,
            PasswordHash = "hash",
            PhoneVerified = false,
            FailedLoginCount = 0,
            CreatedAtUtc = DateTimeOffset.Parse("2026-06-03T00:00:00Z"),
            UpdatedAtUtc = DateTimeOffset.Parse("2026-06-03T00:00:00Z")
        });
        await db.SaveChangesAsync();

        var loaded = await db.PlayerCredentials.SingleAsync(c => c.PlayerAccountId == playerAccountId);
        Assert.Equal("hash", loaded.PasswordHash);
        Assert.False(loaded.PhoneVerified);
        Assert.Equal(0, loaded.FailedLoginCount);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter PlayerCredentialEntity_RoundTrips`
Expected: FAIL — `PlayerCredentialEntity` / `db.PlayerCredentials` do not exist (compile error).

- [ ] **Step 3: Create the entity**

`src/AFK4.Platform.Api/Data/PlayerCredentialEntity.cs`:

```csharp
namespace AFK4.Platform.Api.Data;

/// <summary>
/// Login credentials and contact-verification state for a player. 1:1 with
/// <see cref="PlayerAccountEntity"/>. Kept separate so a player can exist
/// (counter-created) before they ever claim portal/shell access. A PIN is just a
/// short numeric password stored in <see cref="PasswordHash"/>.
/// </summary>
public sealed class PlayerCredentialEntity
{
    public Guid PlayerCredentialId { get; set; }

    public Guid PlayerAccountId { get; set; }

    public Guid OrganizationId { get; set; }

    /// <summary>Null for accounts that have not set a PIN/password yet (OTP-only future).</summary>
    public string? PasswordHash { get; set; }

    public bool PhoneVerified { get; set; }

    public DateTimeOffset? PhoneVerifiedAtUtc { get; set; }

    public int FailedLoginCount { get; set; }

    public DateTimeOffset? LockedUntilUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
```

- [ ] **Step 4: Add DbSet + fluent config**

In `src/AFK4.Platform.Api/Data/PlatformDbContext.cs`, add a DbSet near the other player DbSet (after the `PlayerAccounts` DbSet, line ~45):

```csharp
public DbSet<PlayerCredentialEntity> PlayerCredentials => Set<PlayerCredentialEntity>();
```

And add fluent config inside `OnModelCreating` (mirror the staff-token config block style):

```csharp
modelBuilder.Entity<PlayerCredentialEntity>(entity =>
{
    entity.ToTable("player_credentials");
    entity.HasKey(credential => credential.PlayerCredentialId);
    entity.Property(credential => credential.PasswordHash).HasMaxLength(512);
    entity.HasIndex(credential => credential.PlayerAccountId).IsUnique();
    entity.HasIndex(credential => new { credential.OrganizationId, credential.PlayerAccountId });
});
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter PlayerCredentialEntity_RoundTrips`
Expected: PASS.

- [ ] **Step 6: Generate migration**

Run: `dotnet ef migrations add AddPlayerCredentials --project src/AFK4.Platform.Api --startup-project src/AFK4.Platform.Api`
Expected: creates `src/AFK4.Platform.Api/Data/Migrations/<timestamp>_AddPlayerCredentials.cs` creating table `player_credentials` with the unique index on `PlayerAccountId`.

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.Platform.Api/Data/PlayerCredentialEntity.cs \
        src/AFK4.Platform.Api/Data/PlatformDbContext.cs \
        src/AFK4.Platform.Api/Data/Migrations \
        tests/AFK4.Platform.Api.Tests/PlayerAuthenticationEndpointTests.cs
git commit -m "feat(player-auth): PlayerCredentialEntity + migration"
```

---

## Task 2: Add MarketingOptIn to PlayerAccountEntity

**Files:**
- Modify: `src/AFK4.Platform.Api/Data/PlayerAccountEntity.cs`
- Modify: `src/AFK4.Platform.Api/Data/PlatformDbContext.cs` (default value, optional)
- Test: `tests/AFK4.Platform.Api.Tests/PlayerAuthenticationEndpointTests.cs`

- [ ] **Step 1: Write the failing test**

Add to `PlayerAuthenticationEndpointTests.cs`:

```csharp
[Fact]
public async Task PlayerAccount_MarketingOptIn_DefaultsFalse_AndRoundTrips()
{
    await using var factory = new PlatformApiFactory();
    await using var scope = factory.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

    var id = Guid.NewGuid();
    db.PlayerAccounts.Add(new PlayerAccountEntity
    {
        PlayerAccountId = id,
        OrganizationId = Guid.NewGuid(),
        HomeBranchId = Guid.NewGuid(),
        DisplayName = "Player One",
        CreatedAtUtc = DateTimeOffset.Parse("2026-06-03T00:00:00Z")
    });
    await db.SaveChangesAsync();

    var loaded = await db.PlayerAccounts.SingleAsync(p => p.PlayerAccountId == id);
    Assert.False(loaded.MarketingOptIn);

    loaded.MarketingOptIn = true;
    await db.SaveChangesAsync();
    var reloaded = await db.PlayerAccounts.SingleAsync(p => p.PlayerAccountId == id);
    Assert.True(reloaded.MarketingOptIn);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter MarketingOptIn`
Expected: FAIL — `MarketingOptIn` does not exist (compile error).

- [ ] **Step 3: Add the property**

In `src/AFK4.Platform.Api/Data/PlayerAccountEntity.cs`, after `PreferredLocale`:

```csharp
/// <summary>Player consent to marketing messages. Defaults false; toggled from the portal profile.</summary>
public bool MarketingOptIn { get; set; }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter MarketingOptIn`
Expected: PASS.

- [ ] **Step 5: Generate migration**

Run: `dotnet ef migrations add AddPlayerMarketingOptIn --project src/AFK4.Platform.Api --startup-project src/AFK4.Platform.Api`
Expected: adds boolean column `MarketingOptIn` to `player_accounts` (default false).

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Platform.Api/Data/PlayerAccountEntity.cs \
        src/AFK4.Platform.Api/Data/Migrations \
        tests/AFK4.Platform.Api.Tests/PlayerAuthenticationEndpointTests.cs
git commit -m "feat(player-auth): MarketingOptIn on PlayerAccount (reuse existing PreferredLocale for language)"
```

---

## Task 3: Player auth contracts

**Files:**
- Create: `src/AFK4.Shared.Contracts/Players/PlayerSignInRequest.cs`
- Create: `src/AFK4.Shared.Contracts/Players/PlayerSignInResponse.cs`
- Create: `src/AFK4.Shared.Contracts/Players/PlayerRefreshRequest.cs`
- Create: `src/AFK4.Shared.Contracts/Players/PlayerProfileDto.cs`
- Create: `src/AFK4.Shared.Contracts/Players/SetPlayerPinRequest.cs`

- [ ] **Step 1: Create the contracts**

`PlayerSignInRequest.cs`:

```csharp
namespace AFK4.Shared.Contracts.Players;

public sealed record PlayerSignInRequest(
    Guid OrganizationId,
    string PhoneNumber,
    string Password);
```

`PlayerSignInResponse.cs` (mirrors `StaffSignInResponse` shape, player-scoped fields):

```csharp
namespace AFK4.Shared.Contracts.Players;

public sealed record PlayerSignInResponse(
    Guid PlayerAccountId,
    Guid OrganizationId,
    string DisplayName,
    bool PhoneVerified,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc);
```

`PlayerRefreshRequest.cs`:

```csharp
namespace AFK4.Shared.Contracts.Players;

public sealed record PlayerRefreshRequest(string RefreshToken);
```

`PlayerProfileDto.cs`:

```csharp
namespace AFK4.Shared.Contracts.Players;

public sealed record PlayerProfileDto(
    Guid PlayerAccountId,
    string DisplayName,
    string? PhoneNumber,
    bool PhoneVerified,
    string? PreferredLocale,
    bool MarketingOptIn);
```

`SetPlayerPinRequest.cs` (operator sets a player's initial PIN):

```csharp
namespace AFK4.Shared.Contracts.Players;

public sealed record SetPlayerPinRequest(string Pin);
```

- [ ] **Step 2: Build to verify they compile**

Run: `dotnet build src/AFK4.Shared.Contracts`
Expected: build succeeds.

- [ ] **Step 3: Commit**

```bash
git add src/AFK4.Shared.Contracts/Players
git commit -m "feat(player-auth): public player auth + profile contracts"
```

---

## Task 4: Player token entities + EF config + migration

**Files:**
- Create: `src/AFK4.Platform.Api/Data/PlayerAccessTokenEntity.cs`
- Create: `src/AFK4.Platform.Api/Data/PlayerRefreshTokenEntity.cs`
- Modify: `src/AFK4.Platform.Api/Data/PlatformDbContext.cs`
- Test: `tests/AFK4.Platform.Api.Tests/PlayerAuthenticationEndpointTests.cs`

- [ ] **Step 1: Write the failing test**

Add to `PlayerAuthenticationEndpointTests.cs`:

```csharp
[Fact]
public async Task PlayerTokenEntities_RoundTrip()
{
    await using var factory = new PlatformApiFactory();
    await using var scope = factory.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

    var playerAccountId = Guid.NewGuid();
    var orgId = Guid.NewGuid();
    db.PlayerAccessTokens.Add(new PlayerAccessTokenEntity
    {
        PlayerAccessTokenId = Guid.NewGuid(),
        PlayerAccountId = playerAccountId,
        OrganizationId = orgId,
        TokenHash = new byte[] { 1, 2, 3 },
        CreatedAtUtc = DateTimeOffset.Parse("2026-06-03T00:00:00Z"),
        ExpiresAtUtc = DateTimeOffset.Parse("2026-06-03T01:00:00Z")
    });
    db.PlayerRefreshTokens.Add(new PlayerRefreshTokenEntity
    {
        PlayerRefreshTokenId = Guid.NewGuid(),
        PlayerAccountId = playerAccountId,
        OrganizationId = orgId,
        TokenHash = new byte[] { 4, 5, 6 },
        CreatedAtUtc = DateTimeOffset.Parse("2026-06-03T00:00:00Z"),
        ExpiresAtUtc = DateTimeOffset.Parse("2026-07-03T00:00:00Z")
    });
    await db.SaveChangesAsync();

    Assert.Equal(1, await db.PlayerAccessTokens.CountAsync(t => t.PlayerAccountId == playerAccountId));
    Assert.Equal(1, await db.PlayerRefreshTokens.CountAsync(t => t.PlayerAccountId == playerAccountId));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter PlayerTokenEntities_RoundTrip`
Expected: FAIL — entities/DbSets do not exist (compile error).

- [ ] **Step 3: Create the entities**

`src/AFK4.Platform.Api/Data/PlayerAccessTokenEntity.cs` (mirror `StaffAccessTokenEntity`):

```csharp
namespace AFK4.Platform.Api.Data;

public sealed class PlayerAccessTokenEntity
{
    public Guid PlayerAccessTokenId { get; set; }
    public Guid PlayerAccountId { get; set; }
    public Guid OrganizationId { get; set; }
    public byte[] TokenHash { get; set; } = [];
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
}
```

`src/AFK4.Platform.Api/Data/PlayerRefreshTokenEntity.cs`:

```csharp
namespace AFK4.Platform.Api.Data;

public sealed class PlayerRefreshTokenEntity
{
    public Guid PlayerRefreshTokenId { get; set; }
    public Guid PlayerAccountId { get; set; }
    public Guid OrganizationId { get; set; }
    public byte[] TokenHash { get; set; } = [];
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
}
```

- [ ] **Step 4: Add DbSets + fluent config**

In `PlatformDbContext.cs`, add DbSets near the staff-token DbSets:

```csharp
public DbSet<PlayerAccessTokenEntity> PlayerAccessTokens => Set<PlayerAccessTokenEntity>();

public DbSet<PlayerRefreshTokenEntity> PlayerRefreshTokens => Set<PlayerRefreshTokenEntity>();
```

And fluent config in `OnModelCreating` (mirror the staff-token blocks):

```csharp
modelBuilder.Entity<PlayerAccessTokenEntity>(entity =>
{
    entity.ToTable("player_access_tokens");
    entity.HasKey(accessToken => accessToken.PlayerAccessTokenId);
    entity.Property(accessToken => accessToken.TokenHash).IsRequired();
    entity.HasIndex(accessToken => accessToken.TokenHash);
    entity.HasIndex(accessToken => new { accessToken.PlayerAccountId, accessToken.ExpiresAtUtc });
});

modelBuilder.Entity<PlayerRefreshTokenEntity>(entity =>
{
    entity.ToTable("player_refresh_tokens");
    entity.HasKey(refreshToken => refreshToken.PlayerRefreshTokenId);
    entity.Property(refreshToken => refreshToken.TokenHash).IsRequired();
    entity.HasIndex(refreshToken => refreshToken.TokenHash);
    entity.HasIndex(refreshToken => new { refreshToken.PlayerAccountId, refreshToken.ExpiresAtUtc });
});
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter PlayerTokenEntities_RoundTrip`
Expected: PASS.

- [ ] **Step 6: Generate migration**

Run: `dotnet ef migrations add AddPlayerTokens --project src/AFK4.Platform.Api --startup-project src/AFK4.Platform.Api`
Expected: creates tables `player_access_tokens` / `player_refresh_tokens`.

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.Platform.Api/Data/PlayerAccessTokenEntity.cs \
        src/AFK4.Platform.Api/Data/PlayerRefreshTokenEntity.cs \
        src/AFK4.Platform.Api/Data/PlatformDbContext.cs \
        src/AFK4.Platform.Api/Data/Migrations \
        tests/AFK4.Platform.Api.Tests/PlayerAuthenticationEndpointTests.cs
git commit -m "feat(player-auth): opaque player token tables + migration"
```

---

## Task 5: PlayerContext + accessor

**Files:**
- Create: `src/AFK4.Platform.Api/Identity/PlayerContext.cs`
- Create: `src/AFK4.Platform.Api/Identity/IPlayerContextAccessor.cs`
- Create: `src/AFK4.Platform.Api/Identity/PlayerContextAccessor.cs`

- [ ] **Step 1: Create the record + accessor**

`PlayerContext.cs` (mirror `StaffContext`, player-scoped):

```csharp
namespace AFK4.Platform.Api.Identity;

public sealed record PlayerContext(
    Guid PlayerAccountId,
    Guid OrganizationId,
    bool PhoneVerified);
```

`IPlayerContextAccessor.cs`:

```csharp
namespace AFK4.Platform.Api.Identity;

public interface IPlayerContextAccessor
{
    PlayerContext? Current { get; set; }
}
```

`PlayerContextAccessor.cs`:

```csharp
namespace AFK4.Platform.Api.Identity;

public sealed class PlayerContextAccessor : IPlayerContextAccessor
{
    public PlayerContext? Current { get; set; }
}
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build src/AFK4.Platform.Api`
Expected: build succeeds.

- [ ] **Step 3: Commit**

```bash
git add src/AFK4.Platform.Api/Identity/PlayerContext.cs \
        src/AFK4.Platform.Api/Identity/IPlayerContextAccessor.cs \
        src/AFK4.Platform.Api/Identity/PlayerContextAccessor.cs
git commit -m "feat(player-auth): PlayerContext + accessor"
```

---

## Task 6: OpaquePlayerTokenService (issue/refresh/validate)

**Files:**
- Create: `src/AFK4.Platform.Api/Identity/IPlayerTokenService.cs`
- Create: `src/AFK4.Platform.Api/Identity/OpaquePlayerTokenService.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs` (DI registration)
- Test: `tests/AFK4.Platform.Api.Tests/PlayerAuthenticationEndpointTests.cs`

- [ ] **Step 1: Write the failing test (issue → validate → refresh)**

Add a helper to seed a player + credential, then test the service through a DI scope. Add to `PlayerAuthenticationEndpointTests.cs`:

```csharp
private static async Task<(Guid OrgId, Guid PlayerId)> SeedPlayerWithPinAsync(
    PlatformApiFactory factory, string pin)
{
    await using var scope = factory.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
    var orgId = Guid.NewGuid();
    var branchId = Guid.NewGuid();
    var playerId = Guid.NewGuid();
    var now = DateTimeOffset.Parse("2026-06-03T00:00:00Z");

    db.PlayerAccounts.Add(new PlayerAccountEntity
    {
        PlayerAccountId = playerId,
        OrganizationId = orgId,
        HomeBranchId = branchId,
        DisplayName = "Player One",
        PhoneNumber = "+992900000001",
        IsActive = true,
        CreatedAtUtc = now
    });
    var credential = new PlayerCredentialEntity
    {
        PlayerCredentialId = Guid.NewGuid(),
        PlayerAccountId = playerId,
        OrganizationId = orgId,
        PhoneVerified = true,
        CreatedAtUtc = now,
        UpdatedAtUtc = now
    };
    credential.PasswordHash = new PasswordHasher<PlayerCredentialEntity>().HashPassword(credential, pin);
    db.PlayerCredentials.Add(credential);
    await db.SaveChangesAsync();
    return (orgId, playerId);
}

[Fact]
public async Task PlayerToken_Issue_Validate_Refresh()
{
    await using var factory = new PlatformApiFactory();
    var (orgId, playerId) = await SeedPlayerWithPinAsync(factory, "1234");
    await using var scope = factory.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
    var tokenService = scope.ServiceProvider.GetRequiredService<IPlayerTokenService>();
    var account = await db.PlayerAccounts.SingleAsync(p => p.PlayerAccountId == playerId);

    var issued = await tokenService.IssueAsync(account, true, default);
    Assert.Equal(playerId, issued.PlayerAccountId);
    Assert.True(issued.RefreshTokenExpiresAtUtc > issued.AccessTokenExpiresAtUtc);

    var ctx = await tokenService.ValidateAsync(issued.AccessToken, default);
    Assert.NotNull(ctx);
    Assert.Equal(playerId, ctx!.PlayerAccountId);
    Assert.Equal(orgId, ctx.OrganizationId);

    var refreshed = await tokenService.RefreshAsync(new PlayerRefreshRequest(issued.RefreshToken), default);
    Assert.NotNull(refreshed);
    Assert.NotEqual(issued.AccessToken, refreshed!.AccessToken);

    // old refresh token is now revoked
    Assert.Null(await tokenService.RefreshAsync(new PlayerRefreshRequest(issued.RefreshToken), default));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter PlayerToken_Issue_Validate_Refresh`
Expected: FAIL — `IPlayerTokenService` not registered / not found (compile error).

- [ ] **Step 3: Create the interface**

`src/AFK4.Platform.Api/Identity/IPlayerTokenService.cs`:

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Players;

namespace AFK4.Platform.Api.Identity;

public interface IPlayerTokenService
{
    Task<PlayerSignInResponse> IssueAsync(
        PlayerAccountEntity account, bool phoneVerified, CancellationToken cancellationToken);

    Task<PlayerSignInResponse?> RefreshAsync(
        PlayerRefreshRequest request, CancellationToken cancellationToken);

    Task<PlayerContext?> ValidateAsync(string? bearerToken, CancellationToken cancellationToken);
}
```

- [ ] **Step 4: Create the implementation (mirror OpaqueStaffTokenService)**

`src/AFK4.Platform.Api/Identity/OpaquePlayerTokenService.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Players;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Identity;

public sealed class OpaquePlayerTokenService(PlatformDbContext dbContext, TimeProvider timeProvider)
    : IPlayerTokenService
{
    // Shorter access lifetime than staff (8h) — customer devices are less trusted.
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromHours(1);
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    public async Task<PlayerSignInResponse> IssueAsync(
        PlayerAccountEntity account, bool phoneVerified, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var accessToken = CreateToken();
        var refreshToken = CreateToken();
        var accessExpires = now.Add(AccessTokenLifetime);
        var refreshExpires = now.Add(RefreshTokenLifetime);

        dbContext.PlayerAccessTokens.Add(new PlayerAccessTokenEntity
        {
            PlayerAccessTokenId = Guid.NewGuid(),
            PlayerAccountId = account.PlayerAccountId,
            OrganizationId = account.OrganizationId,
            TokenHash = HashToken(accessToken),
            CreatedAtUtc = now,
            ExpiresAtUtc = accessExpires
        });
        dbContext.PlayerRefreshTokens.Add(new PlayerRefreshTokenEntity
        {
            PlayerRefreshTokenId = Guid.NewGuid(),
            PlayerAccountId = account.PlayerAccountId,
            OrganizationId = account.OrganizationId,
            TokenHash = HashToken(refreshToken),
            CreatedAtUtc = now,
            ExpiresAtUtc = refreshExpires
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return new PlayerSignInResponse(
            account.PlayerAccountId,
            account.OrganizationId,
            account.DisplayName,
            phoneVerified,
            accessToken,
            accessExpires,
            refreshToken,
            refreshExpires);
    }

    public async Task<PlayerSignInResponse?> RefreshAsync(
        PlayerRefreshRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var hash = HashToken(request.RefreshToken);
        var stored = await dbContext.PlayerRefreshTokens
            .SingleOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);

        if (stored is null || stored.RevokedAtUtc is not null || stored.ExpiresAtUtc <= now)
        {
            return null;
        }

        var account = await dbContext.PlayerAccounts
            .SingleOrDefaultAsync(p => p.PlayerAccountId == stored.PlayerAccountId, cancellationToken);
        if (account is null || !account.IsActive)
        {
            return null;
        }

        stored.RevokedAtUtc = now;
        var credential = await dbContext.PlayerCredentials
            .SingleOrDefaultAsync(c => c.PlayerAccountId == account.PlayerAccountId, cancellationToken);
        return await IssueAsync(account, credential?.PhoneVerified ?? false, cancellationToken);
    }

    public async Task<PlayerContext?> ValidateAsync(string? bearerToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var hash = HashToken(bearerToken);
        var stored = await dbContext.PlayerAccessTokens
            .SingleOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);

        if (stored is null || stored.RevokedAtUtc is not null || stored.ExpiresAtUtc <= now)
        {
            return null;
        }

        var credential = await dbContext.PlayerCredentials
            .SingleOrDefaultAsync(c => c.PlayerAccountId == stored.PlayerAccountId, cancellationToken);

        return new PlayerContext(stored.PlayerAccountId, stored.OrganizationId, credential?.PhoneVerified ?? false);
    }

    private static string CreateToken()
    {
        var tokenId = Guid.NewGuid();
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        return $"{tokenId:N}.{secret}";
    }

    private static byte[] HashToken(string token)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(token));
    }
}
```

- [ ] **Step 5: Register in DI**

In `Program.cs`, next to the staff token-service registration (`AddScoped<IStaffTokenService, OpaqueStaffTokenService>()`, ~line 158), add:

```csharp
builder.Services.AddScoped<IPlayerTokenService, OpaquePlayerTokenService>();
```

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter PlayerToken_Issue_Validate_Refresh`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.Platform.Api/Identity/IPlayerTokenService.cs \
        src/AFK4.Platform.Api/Identity/OpaquePlayerTokenService.cs \
        src/AFK4.Platform.Api/Program.cs \
        tests/AFK4.Platform.Api.Tests/PlayerAuthenticationEndpointTests.cs
git commit -m "feat(player-auth): OpaquePlayerTokenService (1h access / 30d refresh)"
```

---

## Task 7: PlayerCredentialService (sign-in + lockout + set-credential)

**Files:**
- Create: `src/AFK4.Platform.Api/Identity/IPlayerCredentialService.cs`
- Create: `src/AFK4.Platform.Api/Identity/PlayerCredentialService.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs` (DI registration)
- Test: `tests/AFK4.Platform.Api.Tests/PlayerAuthenticationEndpointTests.cs`

Lockout policy: after **5** failed attempts, lock for **15 minutes**. Anti-enumeration: a missing account, a missing/unset credential, and a wrong password all return `null` (the endpoint maps that to 401 uniformly).

- [ ] **Step 1: Write the failing test**

Add to `PlayerAuthenticationEndpointTests.cs`:

```csharp
[Fact]
public async Task PlayerSignIn_WrongPin_LocksAfterFiveFailures()
{
    await using var factory = new PlatformApiFactory();
    var (orgId, playerId) = await SeedPlayerWithPinAsync(factory, "1234");
    await using var scope = factory.Services.CreateAsyncScope();
    var service = scope.ServiceProvider.GetRequiredService<IPlayerCredentialService>();
    var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

    for (var i = 0; i < 5; i++)
    {
        Assert.Null(await service.SignInAsync(
            new PlayerSignInRequest(orgId, "+992900000001", "0000"), default));
    }

    var credential = await db.PlayerCredentials.SingleAsync(c => c.PlayerAccountId == playerId);
    Assert.NotNull(credential.LockedUntilUtc);

    // even the correct PIN is refused while locked
    Assert.Null(await service.SignInAsync(
        new PlayerSignInRequest(orgId, "+992900000001", "1234"), default));
}

[Fact]
public async Task PlayerSignIn_CorrectPin_IssuesTokens_AndResetsFailures()
{
    await using var factory = new PlatformApiFactory();
    var (orgId, _) = await SeedPlayerWithPinAsync(factory, "1234");
    await using var scope = factory.Services.CreateAsyncScope();
    var service = scope.ServiceProvider.GetRequiredService<IPlayerCredentialService>();

    var result = await service.SignInAsync(
        new PlayerSignInRequest(orgId, "+992900000001", "1234"), default);
    Assert.NotNull(result);
    Assert.False(string.IsNullOrWhiteSpace(result!.AccessToken));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter PlayerSignIn`
Expected: FAIL — `IPlayerCredentialService` not found (compile error).

- [ ] **Step 3: Create the interface**

`src/AFK4.Platform.Api/Identity/IPlayerCredentialService.cs`:

```csharp
using AFK4.Shared.Contracts.Players;

namespace AFK4.Platform.Api.Identity;

public interface IPlayerCredentialService
{
    Task<PlayerSignInResponse?> SignInAsync(PlayerSignInRequest request, CancellationToken cancellationToken);

    /// <summary>Operator-set initial PIN/password for a player (creates the credential row if absent).</summary>
    Task SetPasswordAsync(Guid playerAccountId, string password, CancellationToken cancellationToken);
}
```

- [ ] **Step 4: Create the implementation**

`src/AFK4.Platform.Api/Identity/PlayerCredentialService.cs`:

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Players;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Identity;

public sealed class PlayerCredentialService(
    PlatformDbContext dbContext,
    IPlayerTokenService tokenService,
    TimeProvider timeProvider) : IPlayerCredentialService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private readonly PasswordHasher<PlayerCredentialEntity> passwordHasher = new();

    public async Task<PlayerSignInResponse?> SignInAsync(
        PlayerSignInRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PhoneNumber) || string.IsNullOrWhiteSpace(request.Password))
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var account = await dbContext.PlayerAccounts.SingleOrDefaultAsync(
            p => p.OrganizationId == request.OrganizationId
                 && p.PhoneNumber == request.PhoneNumber
                 && p.IsActive,
            cancellationToken);
        if (account is null)
        {
            return null;
        }

        var credential = await dbContext.PlayerCredentials.SingleOrDefaultAsync(
            c => c.PlayerAccountId == account.PlayerAccountId, cancellationToken);
        if (credential?.PasswordHash is null)
        {
            return null;
        }

        if (credential.LockedUntilUtc is { } lockedUntil && lockedUntil > now)
        {
            return null;
        }

        var verification = passwordHasher.VerifyHashedPassword(
            credential, credential.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            credential.FailedLoginCount++;
            if (credential.FailedLoginCount >= MaxFailedAttempts)
            {
                credential.LockedUntilUtc = now.Add(LockoutDuration);
            }

            credential.UpdatedAtUtc = now;
            await dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }

        credential.FailedLoginCount = 0;
        credential.LockedUntilUtc = null;
        credential.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        return await tokenService.IssueAsync(account, credential.PhoneVerified, cancellationToken);
    }

    public async Task SetPasswordAsync(
        Guid playerAccountId, string password, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var account = await dbContext.PlayerAccounts.SingleAsync(
            p => p.PlayerAccountId == playerAccountId, cancellationToken);

        var credential = await dbContext.PlayerCredentials.SingleOrDefaultAsync(
            c => c.PlayerAccountId == playerAccountId, cancellationToken);
        if (credential is null)
        {
            credential = new PlayerCredentialEntity
            {
                PlayerCredentialId = Guid.NewGuid(),
                PlayerAccountId = playerAccountId,
                OrganizationId = account.OrganizationId,
                CreatedAtUtc = now
            };
            dbContext.PlayerCredentials.Add(credential);
        }

        credential.PasswordHash = passwordHasher.HashPassword(credential, password);
        credential.FailedLoginCount = 0;
        credential.LockedUntilUtc = null;
        credential.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
```

- [ ] **Step 5: Register in DI**

In `Program.cs`, next to `AddScoped<IPlayerTokenService, ...>`:

```csharp
builder.Services.AddScoped<IPlayerCredentialService, PlayerCredentialService>();
```

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter PlayerSignIn`
Expected: PASS (both tests).

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.Platform.Api/Identity/IPlayerCredentialService.cs \
        src/AFK4.Platform.Api/Identity/PlayerCredentialService.cs \
        src/AFK4.Platform.Api/Program.cs \
        tests/AFK4.Platform.Api.Tests/PlayerAuthenticationEndpointTests.cs
git commit -m "feat(player-auth): PlayerCredentialService sign-in + lockout + set-PIN"
```

---

## Task 8: PlayerAuthenticationMiddleware

**Files:**
- Create: `src/AFK4.Platform.Api/Identity/PlayerAuthenticationMiddleware.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs` (DI for accessor + middleware registration)

The middleware mirrors `StaffAuthenticationMiddleware` but writes to `IPlayerContextAccessor`. It runs for every request (cheap: only does work when a Bearer header is present); the `/api/me/*` endpoints read the accessor and 401 if null. This keeps the staff middleware untouched.

- [ ] **Step 1: Create the middleware**

`src/AFK4.Platform.Api/Identity/PlayerAuthenticationMiddleware.cs`:

```csharp
namespace AFK4.Platform.Api.Identity;

public sealed class PlayerAuthenticationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext httpContext,
        IPlayerTokenService tokenService,
        IPlayerContextAccessor playerContextAccessor)
    {
        // Only resolve a player principal for the player edge; never on staff/admin routes.
        if (httpContext.Request.Path.StartsWithSegments("/api/me", StringComparison.OrdinalIgnoreCase))
        {
            var authorization = httpContext.Request.Headers.Authorization.ToString();
            const string bearerPrefix = "Bearer ";

            if (authorization.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var token = authorization[bearerPrefix.Length..].Trim();
                playerContextAccessor.Current =
                    await tokenService.ValidateAsync(token, httpContext.RequestAborted);
            }
        }

        await next(httpContext);
    }
}
```

- [ ] **Step 2: Register accessor + middleware in Program.cs**

DI (next to the other player registrations):

```csharp
builder.Services.AddScoped<IPlayerContextAccessor, PlayerContextAccessor>();
```

Pipeline — register the player middleware right after the staff/admin middlewares (~line 290), so it shares the same pre-routing position:

```csharp
app.UseMiddleware<StaffAuthenticationMiddleware>();
app.UseMiddleware<PlatformAdminAuthenticationMiddleware>();
app.UseMiddleware<PlayerAuthenticationMiddleware>();
app.UseMiddleware<TenantSuspensionMiddleware>();
```

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build src/AFK4.Platform.Api`
Expected: build succeeds.

- [ ] **Step 4: Commit**

```bash
git add src/AFK4.Platform.Api/Identity/PlayerAuthenticationMiddleware.cs \
        src/AFK4.Platform.Api/Program.cs
git commit -m "feat(player-auth): PlayerAuthenticationMiddleware for /api/me/*"
```

---

## Task 9: Rate limiter (public + me policies)

**Files:**
- Modify: `src/AFK4.Platform.Api/Program.cs` (`AddRateLimiter`, `UseRateLimiter`)

First use of `AddRateLimiter` in the codebase. Two named policies:
- `player-public` — per-IP fixed window: **10 requests / minute** (sign-in/refresh are abuse magnets).
- `player-me` — per-token (fallback per-IP) fixed window: **60 requests / minute**.

Apply via `.RequireRateLimiting("player-public")` / `.RequireRateLimiting("player-me")` on the endpoint groups in Task 10/11. `UseRateLimiter` must sit before the auth middlewares.

- [ ] **Step 1: Add the rate limiter services**

In `Program.cs`, in the service-registration region (~line 160, after the player DI lines), add:

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("player-public", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1)
            }));

    options.AddPolicy("player-me", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Request.Headers.Authorization.ToString() is { Length: > 0 } auth
                ? auth
                : httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1)
            }));
});
```

Add the required usings at the top of `Program.cs` if not present:

```csharp
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
```

- [ ] **Step 2: Add UseRateLimiter to the pipeline**

In `Program.cs`, before `app.UseMiddleware<StaffAuthenticationMiddleware>();`:

```csharp
app.UseRateLimiter();
```

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build src/AFK4.Platform.Api`
Expected: build succeeds.

- [ ] **Step 4: Commit**

```bash
git add src/AFK4.Platform.Api/Program.cs
git commit -m "feat(player-auth): rate limiter (player-public 10/min, player-me 60/min)"
```

---

## Task 10: Public sign-in + refresh endpoints, and operator set-PIN endpoint

**Files:**
- Modify: `src/AFK4.Platform.Api/Program.cs` (endpoints)
- Test: `tests/AFK4.Platform.Api.Tests/PlayerAuthenticationEndpointTests.cs`

The operator set-PIN endpoint is staff-protected. **Step 0 below confirms the exact permission name** that already guards player management; use that name in the `RequireStaffPermission` (or equivalent) call so we don't invent a permission.

- [ ] **Step 0: Confirm the player-management permission + the staff-permission guard helper**

Run: `grep -rn "players" src/AFK4.Platform.Api/Program.cs | grep -i "MapPost\|Permission\|Require"`
Then: `grep -rn "Require.*Permission\|StaffPermissionNames" src/AFK4.Platform.Api/Program.cs | head`
Expected: identify (a) the helper used to require a staff permission on an endpoint (e.g. an extension like `.RequireStaffPermission(StaffPermissionNames.X)` or an inline check via `IStaffContextAccessor`), and (b) the permission constant guarding `POST /api/branches/{branchId}/players` (player creation). Use those exact names in Step 4. If player creation uses an inline `staffContextAccessor.Current` permission check rather than a helper, mirror that inline style.

- [ ] **Step 1: Write the failing HTTP e2e test (sign-in + refresh + 401)**

Add to `PlayerAuthenticationEndpointTests.cs`:

```csharp
[Fact]
public async Task PostPlayerSignIn_ValidPin_ReturnsTokens_ThenRefreshWorks()
{
    await using var factory = new PlatformApiFactory();
    var (orgId, _) = await SeedPlayerWithPinAsync(factory, "1234");
    using var client = factory.CreateClient();

    var signIn = await client.PostAsJsonAsync(
        "/api/public/player/sign-in",
        new PlayerSignInRequest(orgId, "+992900000001", "1234"));
    Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
    var body = await signIn.Content.ReadFromJsonAsync<PlayerSignInResponse>();
    Assert.NotNull(body);
    Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));

    var refresh = await client.PostAsJsonAsync(
        "/api/public/player/refresh",
        new PlayerRefreshRequest(body.RefreshToken));
    Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
}

[Fact]
public async Task PostPlayerSignIn_WrongPin_Returns401()
{
    await using var factory = new PlatformApiFactory();
    var (orgId, _) = await SeedPlayerWithPinAsync(factory, "1234");
    using var client = factory.CreateClient();

    var signIn = await client.PostAsJsonAsync(
        "/api/public/player/sign-in",
        new PlayerSignInRequest(orgId, "+992900000001", "9999"));
    Assert.Equal(HttpStatusCode.Unauthorized, signIn.StatusCode);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter PostPlayerSignIn`
Expected: FAIL — endpoints return 404.

- [ ] **Step 3: Add the public endpoints**

In `Program.cs`, near the staff auth endpoints (~line 547), add (mirror the staff sign-in style, with the rate-limit policy):

```csharp
app.MapPost("/api/public/player/sign-in", async (
    PlayerSignInRequest request,
    IPlayerCredentialService credentialService,
    CancellationToken cancellationToken) =>
{
    var response = await credentialService.SignInAsync(request, cancellationToken);
    return response is null ? Results.Unauthorized() : Results.Ok(response);
}).RequireRateLimiting("player-public");

app.MapPost("/api/public/player/refresh", async (
    PlayerRefreshRequest request,
    IPlayerTokenService tokenService,
    CancellationToken cancellationToken) =>
{
    var response = await tokenService.RefreshAsync(request, cancellationToken);
    return response is null ? Results.Unauthorized() : Results.Ok(response);
}).RequireRateLimiting("player-public");
```

Add the contract using at the top of `Program.cs` if needed: `using AFK4.Shared.Contracts.Players;`

- [ ] **Step 4: Add the operator set-PIN endpoint**

Using the permission name + guard style confirmed in Step 0, add (replace `StaffPermissionNames.<PlayerMgmtPermission>` and the guard call with the confirmed names):

```csharp
app.MapPost("/api/branches/{branchId:guid}/players/{playerAccountId:guid}/pin", async (
    Guid branchId,
    Guid playerAccountId,
    SetPlayerPinRequest request,
    IStaffContextAccessor staffContextAccessor,
    IPlayerCredentialService credentialService,
    PlatformDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var staff = staffContextAccessor.Current;
    if (staff is null || !staff.Permissions.Contains(StaffPermissionNames.ManagePlayers))
    {
        // Custom-middleware auth (no ASP.NET auth scheme) — return an explicit status, not Results.Forbid().
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    if (string.IsNullOrWhiteSpace(request.Pin) || request.Pin.Length < 4)
    {
        return Results.BadRequest(new { error = "PIN must be at least 4 characters." });
    }

    var account = await dbContext.PlayerAccounts.SingleOrDefaultAsync(
        p => p.PlayerAccountId == playerAccountId && p.OrganizationId == staff.OrganizationId,
        cancellationToken);
    if (account is null)
    {
        return Results.NotFound();
    }

    await credentialService.SetPasswordAsync(playerAccountId, request.Pin, cancellationToken);
    return Results.NoContent();
});
```

> Note: `StaffPermissionNames.ManagePlayers` is a placeholder for the confirmed constant from Step 0 — replace it with the real one (and the real guard style) before implementing. If no dedicated players permission exists, use the permission that guards `POST /api/branches/{branchId}/players`.

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter PostPlayerSignIn`
Expected: PASS (both).

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Platform.Api/Program.cs \
        tests/AFK4.Platform.Api.Tests/PlayerAuthenticationEndpointTests.cs
git commit -m "feat(player-auth): public sign-in/refresh + operator set-PIN endpoints"
```

---

## Task 11: Protected /api/me/profile + cross-surface isolation tests

**Files:**
- Modify: `src/AFK4.Platform.Api/Program.cs` (`GET /api/me/profile`)
- Test: `tests/AFK4.Platform.Api.Tests/PlayerAuthenticationEndpointTests.cs`

This proves the `/api/me/*` edge end-to-end and locks in the isolation guarantees: a player token works on `/api/me/*`, a missing/invalid token 401s, and a player token is rejected on a staff route (different middleware/accessor).

- [ ] **Step 1: Write the failing tests**

Add to `PlayerAuthenticationEndpointTests.cs`:

```csharp
[Fact]
public async Task GetMeProfile_WithPlayerToken_ReturnsOwnProfile()
{
    await using var factory = new PlatformApiFactory();
    var (orgId, playerId) = await SeedPlayerWithPinAsync(factory, "1234");
    using var client = factory.CreateClient();

    var signIn = await client.PostAsJsonAsync(
        "/api/public/player/sign-in",
        new PlayerSignInRequest(orgId, "+992900000001", "1234"));
    var tokens = await signIn.Content.ReadFromJsonAsync<PlayerSignInResponse>();
    client.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

    var profileResponse = await client.GetAsync("/api/me/profile");
    Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);
    var profile = await profileResponse.Content.ReadFromJsonAsync<PlayerProfileDto>();
    Assert.Equal(playerId, profile!.PlayerAccountId);
    Assert.Equal("+992900000001", profile.PhoneNumber);
}

[Fact]
public async Task GetMeProfile_WithoutToken_Returns401()
{
    await using var factory = new PlatformApiFactory();
    using var client = factory.CreateClient();
    var response = await client.GetAsync("/api/me/profile");
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
}

[Fact]
public async Task PlayerToken_RejectedOnStaffRoute()
{
    await using var factory = new PlatformApiFactory();
    var (orgId, _) = await SeedPlayerWithPinAsync(factory, "1234");
    using var client = factory.CreateClient();

    var signIn = await client.PostAsJsonAsync(
        "/api/public/player/sign-in",
        new PlayerSignInRequest(orgId, "+992900000001", "1234"));
    var tokens = await signIn.Content.ReadFromJsonAsync<PlayerSignInResponse>();
    client.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

    // A staff-protected route must not accept a player token.
    var staffResponse = await client.GetAsync($"/api/branches/{Guid.NewGuid()}/players");
    Assert.True(
        staffResponse.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
        $"expected 401/403, got {(int)staffResponse.StatusCode}");
}
```

> Step 0 check: confirm `GET /api/branches/{branchId}/players` exists as a staff route (the spec references it). If the exact staff route differs, point this test at any existing staff-permission-guarded GET. Run:
> `grep -rn "api/branches/{branchId}/players\"" src/AFK4.Platform.Api/Program.cs`

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter "GetMeProfile|PlayerToken_RejectedOnStaffRoute"`
Expected: FAIL — `/api/me/profile` returns 404 (not yet defined).

- [ ] **Step 3: Add the protected endpoint**

In `Program.cs`, add (with the `player-me` rate-limit policy):

```csharp
app.MapGet("/api/me/profile", async (
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
        p => p.PlayerAccountId == player.PlayerAccountId, cancellationToken);
    if (account is null)
    {
        return Results.Unauthorized();
    }

    return Results.Ok(new PlayerProfileDto(
        account.PlayerAccountId,
        account.DisplayName,
        account.PhoneNumber,
        player.PhoneVerified,
        account.PreferredLocale,
        account.MarketingOptIn));
}).RequireRateLimiting("player-me");
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter "GetMeProfile|PlayerToken_RejectedOnStaffRoute"`
Expected: PASS (all three).

- [ ] **Step 5: Run the full backend gate**

Run: `dotnet test tests/AFK4.Platform.Api.Tests`
Expected: all tests pass (prior count + the new player-auth tests).

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Platform.Api/Program.cs \
        tests/AFK4.Platform.Api.Tests/PlayerAuthenticationEndpointTests.cs
git commit -m "feat(player-auth): protected /api/me/profile + cross-surface isolation tests"
```

---

## Done criteria

- `dotnet test tests/AFK4.Platform.Api.Tests` is green, including: credential roundtrip, token issue/validate/refresh, lockout after 5 failures, sign-in 200/401, refresh rotation, `/api/me/profile` 200 with player token / 401 without, player token rejected on a staff route.
- Three EF migrations exist (`AddPlayerCredentials`, `AddPlayerMarketingOptIn`, `AddPlayerTokens`).
- `AddRateLimiter` + `UseRateLimiter` wired; public endpoints carry `player-public`, `/api/me/*` carries `player-me`.
- Staff/admin auth surface untouched (no edits to `OpaqueStaffTokenService`, `StaffAuthenticationMiddleware`, staff token tables).

## Out of scope (next plans)

- **Portal reads**: `GET /api/me/dashboard` (balance/debt + active session accrued cost), history/receipts, profile PATCH, reservations (online), wallet top-up intent. (Customer-portal plan.)
- **PWA frontend** `AFK4.Customer.Web` (mobile-first, smartshell.gg-grade design, tenant-branded, localized). (Customer-portal frontend plan.)
- **OTP** sign-in/verify (gated on notifications SMS — Stage 6).
- **Shell** device-bound player token variant + `/api/player-self/*` session endpoints. (Customer-shell plan.)
