# Phase B (backend): Staff phone identity + sign-in-by-phone — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give a staff member a verifiable phone number and let them sign into the backend with **phone + password**, reusing the existing opaque-token staff auth and the notification pipeline (SMS channel from Phase A). Add the `devices.install` permission.

**Architecture:** Add `Phone`/`NormalizedPhone`/`PhoneVerifiedAtUtc` to `StaffUserEntity` with a global partial-unique index (verified + active). Phone verification is an OTP flow that mirrors the existing `EfStaffPasswordResetService` + owner-code hashing: a short-lived hashed code stored in a new `staff_phone_otps` table, delivered through `INotificationService.SendNowAsync` over the `Sms` channel (Phase A). Sign-in-by-phone mirrors `SignInByLoginAsync` but resolves by `NormalizedPhone` among verified+active rows.

**Tech Stack:** .NET 10, ASP.NET Core minimal APIs (endpoints live inline in `Program.cs` — there is NO `Endpoints/` folder on this branch), EF Core + Npgsql (Postgres), `PasswordHasher<StaffUserEntity>`, xUnit (`tests/AFK4.Platform.Api.Tests`, EF InMemory via `PlatformApiFactory`).

**Scope (this plan = backend only):**
- IN: permission, phone normalizer, staff schema + migration, `sign-in-by-phone`, OTP infra + migration, SMS verification template, phone start/confirm endpoints + tests.
- OUT (separate follow-up plans): **Phase B-UI** — the admin-panel phone field + verify flow in Operator.App.Web. **Phase C** — wizard phone login + authenticated install endpoints. **Phase D** — SMS password reset (`forgot/reset-password-by-phone`). Do NOT build those here.

**Verified facts this plan relies on (do not re-derive):**
- `StaffUserEntity` POCO: `src/AFK4.Platform.Api/Data/StaffUserEntity.cs`; its EF config is **inline** in `PlatformDbContext.OnModelCreating` (`src/AFK4.Platform.Api/Data/PlatformDbContext.cs`, the `modelBuilder.Entity<StaffUserEntity>(...)` block ~lines 204–214). No `IEntityTypeConfiguration` classes exist — everything is inline.
- DbContext = `PlatformDbContext` (Npgsql). Design-time factory exists (`PlatformDbContextDesignTimeFactory`). Migrations live in `src/AFK4.Platform.Api/Data/Migrations/`. EF tool pinned in `.config/dotnet-tools.json` (`dotnet-ef` 10.0.4).
- `IStaffCredentialService` / `PasswordHashingStaffCredentialService` (`src/AFK4.Platform.Api/Identity/`): fields `dbContext` (`PlatformDbContext`), `tokenService` (`IStaffTokenService`), `passwordHasher` (`new PasswordHasher<StaffUserEntity>()`). `tokenService.IssueAsync(user, ct)` returns `StaffSignInResponse`.
- `StaffSignInResponse` and existing request DTOs live in `src/AFK4.Shared.Contracts/Identity/`.
- Auth endpoints in `Program.cs`: `sign-in-by-login` at ~line 673 (block ends ~690), `refresh` at ~692, `forgot-password`/`reset-password` at ~1922/1937.
- Notifications: `INotificationService.SendNowAsync(NotificationRequest, ct) → NotificationDeliveryResult(Handle, bool Delivered, string? Error)`. `NotificationRequest(TemplateKey, Category, Recipient, Tokens, IdempotencyKey, PreferredChannels?, OrganizationId?, BranchId?, Attachments?)`. `NotificationRecipient(Locale, EmailAddress?, PhoneNumber?, StaffUserId?, PlayerAccountId?)`. `NotificationService.ResolveAddress` already maps `NotificationChannel.Sms → recipient.PhoneNumber`. SMS channel + transport registered in Phase A.
- Templates: embedded JSON at `src/AFK4.Platform.Api/Notifications/Templates/{locale}/{key}.json`, three locales `ru`/`en`/`tg`, placeholders `{{token}}` (double braces), SMS uses `bodyText`. The csproj already globs `Notifications\Templates\**\*.json` as `EmbeddedResource` — **new template files need no csproj edit**. `NotificationTemplateKeys` + its `All` list: `src/AFK4.Platform.Api/Notifications/NotificationTemplateKeys.cs`. Startup runs `EnsureKeysPresent(NotificationTemplateKeys.All)` (`Program.cs` ~line 351) — every key in `All` MUST have a default-locale file or the app won't boot.
- Permissions: `StaffPermissionNames` (`src/AFK4.Shared.Contracts/Identity/StaffPermissionNames.cs`); `PermissionCatalog` (`src/AFK4.Platform.Api/Identity/PermissionCatalog.cs`) — per-role `HashSet<string>` maps unioned by `GetPermissions(roles)`; `StaffRoleNames` (`Owner`,`BranchManager`,`ShiftSupervisor`,`CashierOperator`,`Technician`,`AccountantAuditor`).
- Test harness: `PlatformApiFactory` (`tests/.../PlatformApiFactory.cs`) — `WebApplicationFactory<Program>`, EF InMemory, ctor takes `extraServices: Action<IServiceCollection>` for per-test overrides. Service unit tests build a standalone InMemory `PlatformDbContext` (see `StaffPasswordResetServiceTests.cs`), use `FixedTimeProvider(now)` (settable `.Now`) and an inline `INotificationService` capture. `StaffAuthTestHelper.AuthorizeAsAsync(factory, client, roleName)` seeds a user + sets the client's bearer.
- **EF InMemory ignores relational config** (`ToTable`, `HasFilter`) and does **NOT enforce unique indexes**. So phone-uniqueness must ALSO be checked in application code (testable), with the DB partial-unique index as the production backstop.

**Migration note:** run `dotnet tool restore` once at repo root, then `dotnet ef ... --project src/AFK4.Platform.Api`. `migrations add` only scaffolds from the model (no live DB needed). Tests use InMemory and pass without applying migrations; the migration files are for real Postgres and MUST be committed.

---

## File structure

Create:
- `src/AFK4.Platform.Api/Identity/PhoneNumberNormalizer.cs`
- `src/AFK4.Platform.Api/Identity/PhoneOtp/IPhoneOtpHasher.cs`
- `src/AFK4.Platform.Api/Identity/PhoneOtp/Sha256PhoneOtpHasher.cs`
- `src/AFK4.Platform.Api/Identity/PhoneOtp/IPhoneOtpGenerator.cs`
- `src/AFK4.Platform.Api/Identity/PhoneOtp/RandomPhoneOtpGenerator.cs`
- `src/AFK4.Platform.Api/Identity/PhoneOtp/PhoneOtpOptions.cs`
- `src/AFK4.Platform.Api/Identity/IStaffPhoneVerificationService.cs`
- `src/AFK4.Platform.Api/Identity/EfStaffPhoneVerificationService.cs`
- `src/AFK4.Platform.Api/Data/StaffPhoneOtpEntity.cs`
- `src/AFK4.Shared.Contracts/Identity/StaffSignInByPhoneRequest.cs`
- `src/AFK4.Shared.Contracts/Identity/StaffPhoneVerificationContracts.cs`
- `src/AFK4.Platform.Api/Notifications/Templates/{ru,en,tg}/staff.phone_verification.json`
- Tests: `tests/AFK4.Platform.Api.Tests/Identity/PhoneNumberNormalizerTests.cs`, `.../Identity/PermissionCatalogInstallDeviceTests.cs`, `.../Identity/PhoneOtpHasherAndGeneratorTests.cs`, `.../Identity/StaffPhoneVerificationServiceTests.cs`, `.../Notifications/StaffPhoneVerificationTemplateTests.cs`, `.../StaffSignInByPhoneEndpointTests.cs`, `.../StaffPhoneVerificationEndpointTests.cs`

Modify:
- `src/AFK4.Shared.Contracts/Identity/StaffPermissionNames.cs`
- `src/AFK4.Platform.Api/Identity/PermissionCatalog.cs`
- `src/AFK4.Platform.Api/Data/StaffUserEntity.cs`
- `src/AFK4.Platform.Api/Data/PlatformDbContext.cs`
- `src/AFK4.Platform.Api/Identity/IStaffCredentialService.cs`
- `src/AFK4.Platform.Api/Identity/PasswordHashingStaffCredentialService.cs`
- `src/AFK4.Platform.Api/Notifications/NotificationTemplateKeys.cs`
- `src/AFK4.Platform.Api/Program.cs` (endpoints + DI)

---

### Task 1: `devices.install` permission

**Files:**
- Modify: `src/AFK4.Shared.Contracts/Identity/StaffPermissionNames.cs`
- Modify: `src/AFK4.Platform.Api/Identity/PermissionCatalog.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Identity/PermissionCatalogInstallDeviceTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/AFK4.Platform.Api.Tests/Identity/PermissionCatalogInstallDeviceTests.cs`:

```csharp
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Identity;
using Xunit;

namespace AFK4.Platform.Api.Tests.Identity;

public sealed class PermissionCatalogInstallDeviceTests
{
    [Theory]
    [InlineData(StaffRoleNames.Owner)]
    [InlineData(StaffRoleNames.BranchManager)]
    [InlineData(StaffRoleNames.Technician)]
    public void InstallDevice_GrantedTo_InstallerRoles(string role)
    {
        var permissions = PermissionCatalog.GetPermissions([role]);
        Assert.Contains(StaffPermissionNames.InstallDevice, permissions);
    }

    [Theory]
    [InlineData(StaffRoleNames.CashierOperator)]
    [InlineData(StaffRoleNames.ShiftSupervisor)]
    [InlineData(StaffRoleNames.AccountantAuditor)]
    public void InstallDevice_NotGrantedTo_OtherRoles(string role)
    {
        var permissions = PermissionCatalog.GetPermissions([role]);
        Assert.DoesNotContain(StaffPermissionNames.InstallDevice, permissions);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter PermissionCatalogInstallDeviceTests`
Expected: FAIL — compile error, `StaffPermissionNames.InstallDevice` does not exist.

- [ ] **Step 3: Add the permission constant**

In `src/AFK4.Shared.Contracts/Identity/StaffPermissionNames.cs`, in the `devices.*` block, immediately after the `ViewDeviceDetail` constant, add:

```csharp
    public const string InstallDevice = "devices.install";
```

- [ ] **Step 4: Grant it in the catalog**

In `src/AFK4.Platform.Api/Identity/PermissionCatalog.cs`, add `StaffPermissionNames.InstallDevice,` into the permission `HashSet` for each of these three roles: `[StaffRoleNames.Owner]`, `[StaffRoleNames.BranchManager]`, `[StaffRoleNames.Technician]`. (Add one line in each set; the exact position inside the set does not matter.) Do NOT add it to any other role.

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter PermissionCatalogInstallDeviceTests`
Expected: PASS (6 cases).

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Shared.Contracts/Identity/StaffPermissionNames.cs src/AFK4.Platform.Api/Identity/PermissionCatalog.cs tests/AFK4.Platform.Api.Tests/Identity/PermissionCatalogInstallDeviceTests.cs
git commit -m "feat(identity): add devices.install permission for installer roles"
```

---

### Task 2: Phone number normalizer

**Files:**
- Create: `src/AFK4.Platform.Api/Identity/PhoneNumberNormalizer.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Identity/PhoneNumberNormalizerTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/AFK4.Platform.Api.Tests/Identity/PhoneNumberNormalizerTests.cs`:

```csharp
using AFK4.Platform.Api.Identity;
using Xunit;

namespace AFK4.Platform.Api.Tests.Identity;

public sealed class PhoneNumberNormalizerTests
{
    [Theory]
    [InlineData("+992 93 738-00-70", "992937380070")]
    [InlineData("992937380070", "992937380070")]
    [InlineData("+992-93-738-00-70", "992937380070")]
    [InlineData("  +7 (916) 123-45-67 ", "79161234567")]
    public void Normalize_StripsFormatting_KeepsDigits(string raw, string expected)
    {
        Assert.Equal(expected, PhoneNumberNormalizer.Normalize(raw));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("937380070")]            // 9 digits — no country code
    [InlineData("12345")]                // too short
    [InlineData("9929373800701234567")]  // 19 digits — too long
    [InlineData("abc-def")]              // no digits
    public void Normalize_RejectsInvalid_ReturnsNull(string? raw)
    {
        Assert.Null(PhoneNumberNormalizer.Normalize(raw));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter PhoneNumberNormalizerTests`
Expected: FAIL — compile error, `PhoneNumberNormalizer` does not exist.

- [ ] **Step 3: Create the normalizer**

Create `src/AFK4.Platform.Api/Identity/PhoneNumberNormalizer.cs`:

```csharp
using System.Text;

namespace AFK4.Platform.Api.Identity;

/// <summary>
/// Normalizes a human-typed phone number to E.164 digits-only form (no '+', spaces, dashes,
/// or parentheses), e.g. "+992 93 738-00-70" -> "992937380070". Requires a country code:
/// we serve the CIS market (+992/+7/+998 = 11–12 digits), so a bare local number is rejected.
/// Returns null when the input is missing or not a plausible international number.
/// </summary>
public static class PhoneNumberNormalizer
{
    // E.164 allows up to 15 digits; require a country code so we never store ambiguous locals.
    private const int MinDigits = 11;
    private const int MaxDigits = 15;

    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var builder = new StringBuilder(raw.Length);
        foreach (var character in raw)
        {
            if (character >= '0' && character <= '9')
            {
                builder.Append(character);
            }
        }

        return builder.Length is >= MinDigits and <= MaxDigits ? builder.ToString() : null;
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter PhoneNumberNormalizerTests`
Expected: PASS (11 cases).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Api/Identity/PhoneNumberNormalizer.cs tests/AFK4.Platform.Api.Tests/Identity/PhoneNumberNormalizerTests.cs
git commit -m "feat(identity): add E.164 phone number normalizer"
```

---

### Task 3: Staff phone columns + EF config + migration

**Files:**
- Modify: `src/AFK4.Platform.Api/Data/StaffUserEntity.cs`
- Modify: `src/AFK4.Platform.Api/Data/PlatformDbContext.cs` (the `modelBuilder.Entity<StaffUserEntity>(...)` block)
- Create: a new EF migration under `src/AFK4.Platform.Api/Data/Migrations/`
- Test: `tests/AFK4.Platform.Api.Tests/Identity/StaffPhoneColumnsRoundTripTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/AFK4.Platform.Api.Tests/Identity/StaffPhoneColumnsRoundTripTests.cs`:

```csharp
using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AFK4.Platform.Api.Tests.Identity;

public sealed class StaffPhoneColumnsRoundTripTests
{
    private static PlatformDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    [Fact]
    public async Task StaffUser_PersistsPhoneFields()
    {
        await using var db = CreateDb();
        var staffUserId = Guid.NewGuid();
        var verifiedAt = DateTimeOffset.Parse("2026-06-05T10:00:00Z");

        db.StaffUsers.Add(new StaffUserEntity
        {
            StaffUserId = staffUserId,
            OrganizationId = Guid.NewGuid(),
            UserName = "owner",
            NormalizedUserName = "OWNER",
            DisplayName = "Owner",
            PasswordHash = "x",
            Phone = "+992937380070",
            NormalizedPhone = "992937380070",
            PhoneVerifiedAtUtc = verifiedAt,
        });
        await db.SaveChangesAsync();

        var loaded = await db.StaffUsers.SingleAsync(user => user.StaffUserId == staffUserId);
        Assert.Equal("+992937380070", loaded.Phone);
        Assert.Equal("992937380070", loaded.NormalizedPhone);
        Assert.Equal(verifiedAt, loaded.PhoneVerifiedAtUtc);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter StaffPhoneColumnsRoundTripTests`
Expected: FAIL — compile error, `StaffUserEntity` has no `Phone`/`NormalizedPhone`/`PhoneVerifiedAtUtc`.

- [ ] **Step 3: Add the properties to the entity**

In `src/AFK4.Platform.Api/Data/StaffUserEntity.cs`, add these three properties (after `Email`):

```csharp
    /// <summary>Staff login phone in E.164 display form (e.g. "+992937380070"). Null until verified.</summary>
    public string? Phone { get; set; }

    /// <summary>Digits-only form of <see cref="Phone"/> used as the global login key. Null until verified.</summary>
    public string? NormalizedPhone { get; set; }

    /// <summary>When the phone was confirmed by SMS OTP. Null = unverified; only verified phones may sign in.</summary>
    public DateTimeOffset? PhoneVerifiedAtUtc { get; set; }
```

- [ ] **Step 4: Configure columns + the partial-unique index**

In `src/AFK4.Platform.Api/Data/PlatformDbContext.cs`, inside the existing `modelBuilder.Entity<StaffUserEntity>(entity => { ... })` block, add (just before the closing `});`):

```csharp
    entity.Property(staffUser => staffUser.Phone).HasMaxLength(20);
    entity.Property(staffUser => staffUser.NormalizedPhone).HasMaxLength(20);
    // Phone is a GLOBAL login id (unlike username, which is per-org): a verified, active phone
    // must map to exactly one staff. Partial unique index so unverified/old rows don't collide.
    entity.HasIndex(staffUser => staffUser.NormalizedPhone)
        .IsUnique()
        .HasFilter("\"NormalizedPhone\" IS NOT NULL AND \"PhoneVerifiedAtUtc\" IS NOT NULL AND \"IsActive\"");
```

- [ ] **Step 5: Run the round-trip test to verify it passes**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter StaffPhoneColumnsRoundTripTests`
Expected: PASS. (InMemory ignores the `HasFilter`/`IsUnique` — this test only proves the columns are mapped.)

- [ ] **Step 6: Generate the migration**

Run (repo root `D:\afk4.net`):
```bash
dotnet tool restore
dotnet ef migrations add AddStaffPhoneIdentity --project src/AFK4.Platform.Api
```
Expected: a new `Data/Migrations/<timestamp>_AddStaffPhoneIdentity.cs` (+ `.Designer.cs`) and an updated `PlatformDbContextModelSnapshot.cs`.

- [ ] **Step 7: Verify the migration content**

Open the generated `*_AddStaffPhoneIdentity.cs`. Confirm `Up()`:
- adds three columns to `staff_users`: `Phone` (`character varying(20)`, nullable), `NormalizedPhone` (`character varying(20)`, nullable), `PhoneVerifiedAtUtc` (`timestamp with time zone`, nullable);
- creates a unique index on `NormalizedPhone` with `filter: "\"NormalizedPhone\" IS NOT NULL AND \"PhoneVerifiedAtUtc\" IS NOT NULL AND \"IsActive\""`.

If the filter is missing or the column types are wrong, fix the entity/config and regenerate (`dotnet ef migrations remove --project src/AFK4.Platform.Api`, then re-add).

- [ ] **Step 8: Build + commit**

Run: `dotnet build src/AFK4.Platform.Api/AFK4.Platform.Api.csproj`
Expected: 0 errors.

```bash
git add src/AFK4.Platform.Api/Data/StaffUserEntity.cs src/AFK4.Platform.Api/Data/PlatformDbContext.cs src/AFK4.Platform.Api/Data/Migrations tests/AFK4.Platform.Api.Tests/Identity/StaffPhoneColumnsRoundTripTests.cs
git commit -m "feat(identity): add staff phone columns and partial-unique index"
```

---

### Task 4: Sign-in by phone + password

**Files:**
- Create: `src/AFK4.Shared.Contracts/Identity/StaffSignInByPhoneRequest.cs`
- Modify: `src/AFK4.Platform.Api/Identity/IStaffCredentialService.cs`
- Modify: `src/AFK4.Platform.Api/Identity/PasswordHashingStaffCredentialService.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs` (new endpoint after the `sign-in-by-login` block)
- Test: `tests/AFK4.Platform.Api.Tests/StaffSignInByPhoneEndpointTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/AFK4.Platform.Api.Tests/StaffSignInByPhoneEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public sealed class StaffSignInByPhoneEndpointTests
{
    private static async Task<Guid> SeedStaffWithPhoneAsync(
        PlatformApiFactory factory,
        string normalizedPhone,
        bool verified,
        string password = "Passw0rd!")
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var staffUserId = Guid.NewGuid();
        var staff = new StaffUserEntity
        {
            StaffUserId = staffUserId,
            OrganizationId = Guid.NewGuid(),
            UserName = $"u{normalizedPhone}",
            NormalizedUserName = $"U{normalizedPhone}",
            DisplayName = "Phone Staff",
            IsActive = true,
            Phone = "+" + normalizedPhone,
            NormalizedPhone = normalizedPhone,
            PhoneVerifiedAtUtc = verified ? DateTimeOffset.Parse("2026-06-01T00:00:00Z") : null,
            CreatedAtUtc = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
        };
        staff.PasswordHash = new PasswordHasher<StaffUserEntity>().HashPassword(staff, password);
        db.StaffUsers.Add(staff);
        await db.SaveChangesAsync();
        return staffUserId;
    }

    [Fact]
    public async Task SignInByPhone_VerifiedPhone_CorrectPassword_ReturnsToken()
    {
        await using var factory = new PlatformApiFactory();
        var staffUserId = await SeedStaffWithPhoneAsync(factory, "992937380070", verified: true);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/staff/sign-in-by-phone",
            new StaffSignInByPhoneRequest("+992 93 738-00-70", "Passw0rd!"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<StaffSignInResponse>();
        Assert.NotNull(body);
        Assert.Equal(staffUserId, body!.StaffUserId);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
    }

    [Fact]
    public async Task SignInByPhone_WrongPassword_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        await SeedStaffWithPhoneAsync(factory, "992937380070", verified: true);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/staff/sign-in-by-phone",
            new StaffSignInByPhoneRequest("992937380070", "WRONG"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SignInByPhone_UnverifiedPhone_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        await SeedStaffWithPhoneAsync(factory, "992937380070", verified: false);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/staff/sign-in-by-phone",
            new StaffSignInByPhoneRequest("992937380070", "Passw0rd!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SignInByPhone_UnknownPhone_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/staff/sign-in-by-phone",
            new StaffSignInByPhoneRequest("992000000000", "Passw0rd!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter StaffSignInByPhoneEndpointTests`
Expected: FAIL — compile error, `StaffSignInByPhoneRequest` and the endpoint do not exist.

- [ ] **Step 3: Add the request contract**

Create `src/AFK4.Shared.Contracts/Identity/StaffSignInByPhoneRequest.cs`:

```csharp
namespace AFK4.Shared.Contracts.Identity;

public sealed record StaffSignInByPhoneRequest(string PhoneNumber, string Password);
```

- [ ] **Step 4: Add the interface method**

In `src/AFK4.Platform.Api/Identity/IStaffCredentialService.cs`, add to the interface:

```csharp
    Task<StaffSignInResponse?> SignInByPhoneAsync(StaffSignInByPhoneRequest request, CancellationToken cancellationToken);
```

(Ensure `using AFK4.Shared.Contracts.Identity;` is present — it already is, since the other request types live there.)

- [ ] **Step 5: Implement it**

In `src/AFK4.Platform.Api/Identity/PasswordHashingStaffCredentialService.cs`, add this method to the class (it uses the existing `dbContext`, `passwordHasher`, and `tokenService` members). Resolve by `NormalizedPhone` among verified+active rows; the partial-unique index guarantees at most one match, so no club-choice machinery is needed.

```csharp
    public async Task<StaffSignInResponse?> SignInByPhoneAsync(
        StaffSignInByPhoneRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PhoneNumber) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return null;
        }

        var normalizedPhone = PhoneNumberNormalizer.Normalize(request.PhoneNumber);
        if (normalizedPhone is null)
        {
            return null;
        }

        var user = await dbContext.StaffUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.NormalizedPhone == normalizedPhone
                    && candidate.PhoneVerifiedAtUtc != null
                    && candidate.IsActive,
                cancellationToken);
        if (user is null)
        {
            return null;
        }

        var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        return result == PasswordVerificationResult.Failed
            ? null
            : await tokenService.IssueAsync(user, cancellationToken);
    }
```

If the build reports missing usings in this file, add `using Microsoft.EntityFrameworkCore;` (for `FirstOrDefaultAsync`) — but it is almost certainly already imported (the existing `SignInByLoginAsync` uses `ToListAsync`).

- [ ] **Step 6: Add the endpoint**

In `src/AFK4.Platform.Api/Program.cs`, immediately AFTER the `app.MapPost("/api/auth/staff/sign-in-by-login", ...)` block closes (the line `});` ending that handler, ~line 690) and BEFORE `app.MapPost("/api/auth/staff/refresh", ...)`, insert:

```csharp
app.MapPost("/api/auth/staff/sign-in-by-phone", async (
    StaffSignInByPhoneRequest request,
    IStaffCredentialService credentialService,
    CancellationToken cancellationToken) =>
{
    var signedIn = await credentialService.SignInByPhoneAsync(request, cancellationToken);
    return signedIn is not null ? Results.Ok(signedIn) : Results.Unauthorized();
});
```

(`StaffSignInByPhoneRequest` is in `AFK4.Shared.Contracts.Identity`, already imported in Program.cs for the other sign-in DTOs.)

- [ ] **Step 7: Run the test to verify it passes**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter StaffSignInByPhoneEndpointTests`
Expected: PASS (4 cases).

- [ ] **Step 8: Commit**

```bash
git add src/AFK4.Shared.Contracts/Identity/StaffSignInByPhoneRequest.cs src/AFK4.Platform.Api/Identity/IStaffCredentialService.cs src/AFK4.Platform.Api/Identity/PasswordHashingStaffCredentialService.cs src/AFK4.Platform.Api/Program.cs tests/AFK4.Platform.Api.Tests/StaffSignInByPhoneEndpointTests.cs
git commit -m "feat(auth): add staff sign-in by phone + password"
```

---

### Task 5: OTP entity + hasher + generator + options + DI + migration

**Files:**
- Create: `src/AFK4.Platform.Api/Data/StaffPhoneOtpEntity.cs`
- Create: `src/AFK4.Platform.Api/Identity/PhoneOtp/{IPhoneOtpHasher,Sha256PhoneOtpHasher,IPhoneOtpGenerator,RandomPhoneOtpGenerator,PhoneOtpOptions}.cs`
- Modify: `src/AFK4.Platform.Api/Data/PlatformDbContext.cs` (DbSet + config block)
- Modify: `src/AFK4.Platform.Api/Program.cs` (DI registration)
- Create: a new EF migration
- Test: `tests/AFK4.Platform.Api.Tests/Identity/PhoneOtpHasherAndGeneratorTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/AFK4.Platform.Api.Tests/Identity/PhoneOtpHasherAndGeneratorTests.cs`:

```csharp
using System.Text.RegularExpressions;
using AFK4.Platform.Api.Identity.PhoneOtp;
using Xunit;

namespace AFK4.Platform.Api.Tests.Identity;

public sealed class PhoneOtpHasherAndGeneratorTests
{
    [Fact]
    public void Hash_IsDeterministic_LowercaseHex64()
    {
        var hasher = new Sha256PhoneOtpHasher();

        var a = hasher.Hash("123456");
        var b = hasher.Hash("123456");

        Assert.Equal(a, b);
        Assert.Equal(64, a.Length);
        Assert.Matches("^[0-9a-f]{64}$", a);
    }

    [Fact]
    public void Hash_DiffersForDifferentCodes()
    {
        var hasher = new Sha256PhoneOtpHasher();
        Assert.NotEqual(hasher.Hash("123456"), hasher.Hash("654321"));
    }

    [Fact]
    public void Generate_ProducesSixDigitCodes()
    {
        var generator = new RandomPhoneOtpGenerator();

        for (var i = 0; i < 200; i++)
        {
            var code = generator.Generate();
            Assert.Matches("^[0-9]{6}$", code);
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter PhoneOtpHasherAndGeneratorTests`
Expected: FAIL — compile error, the `PhoneOtp` types do not exist.

- [ ] **Step 3: Create the hasher**

Create `src/AFK4.Platform.Api/Identity/PhoneOtp/IPhoneOtpHasher.cs`:

```csharp
namespace AFK4.Platform.Api.Identity.PhoneOtp;

public interface IPhoneOtpHasher
{
    /// <summary>SHA-256 hex (lowercase) of the numeric code. Codes are stored hashed, never plaintext.</summary>
    string Hash(string code);
}
```

Create `src/AFK4.Platform.Api/Identity/PhoneOtp/Sha256PhoneOtpHasher.cs` (mirrors `Sha256OwnerCodeHasher.Hash`):

```csharp
using System.Security.Cryptography;
using System.Text;

namespace AFK4.Platform.Api.Identity.PhoneOtp;

public sealed class Sha256PhoneOtpHasher : IPhoneOtpHasher
{
    public string Hash(string code)
    {
        var bytes = Encoding.ASCII.GetBytes(code);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
```

- [ ] **Step 4: Create the generator**

Create `src/AFK4.Platform.Api/Identity/PhoneOtp/IPhoneOtpGenerator.cs`:

```csharp
namespace AFK4.Platform.Api.Identity.PhoneOtp;

public interface IPhoneOtpGenerator
{
    /// <summary>A cryptographically-random 6-digit numeric code, zero-padded.</summary>
    string Generate();
}
```

Create `src/AFK4.Platform.Api/Identity/PhoneOtp/RandomPhoneOtpGenerator.cs` (mirrors `RandomOwnerCodeGenerator`):

```csharp
using System.Globalization;
using System.Security.Cryptography;

namespace AFK4.Platform.Api.Identity.PhoneOtp;

public sealed class RandomPhoneOtpGenerator : IPhoneOtpGenerator
{
    public const int Digits = 6;
    private const int UpperExclusive = 1_000_000;

    public string Generate()
    {
        var value = RandomNumberGenerator.GetInt32(0, UpperExclusive);
        return value.ToString("D" + Digits, CultureInfo.InvariantCulture);
    }
}
```

- [ ] **Step 5: Create the options**

Create `src/AFK4.Platform.Api/Identity/PhoneOtp/PhoneOtpOptions.cs`:

```csharp
namespace AFK4.Platform.Api.Identity.PhoneOtp;

public sealed class PhoneOtpOptions
{
    public const string SectionName = "PhoneOtp";

    public TimeSpan Lifetime { get; set; } = TimeSpan.FromMinutes(5);
    public int MaxAttempts { get; set; } = 3;
    public TimeSpan ResendCooldown { get; set; } = TimeSpan.FromSeconds(60);
    public int MaxSendsPerHour { get; set; } = 5;
}
```

- [ ] **Step 6: Run the hasher/generator test to verify it passes**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter PhoneOtpHasherAndGeneratorTests`
Expected: PASS (3 cases).

- [ ] **Step 7: Create the OTP entity**

Create `src/AFK4.Platform.Api/Data/StaffPhoneOtpEntity.cs`:

```csharp
namespace AFK4.Platform.Api.Data;

public enum StaffPhoneOtpPurpose
{
    PhoneVerification = 0,
    PasswordReset = 1,
}

public sealed class StaffPhoneOtpEntity
{
    public Guid StaffPhoneOtpId { get; set; }
    public Guid StaffUserId { get; set; }
    public Guid OrganizationId { get; set; }

    /// <summary>The pending phone in normalized (digits-only) form the code was sent to.</summary>
    public string Phone { get; set; } = string.Empty;

    public StaffPhoneOtpPurpose Purpose { get; set; }

    /// <summary>SHA-256 hex of the 6-digit code. Never stores plaintext.</summary>
    public string CodeHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset? ConsumedAtUtc { get; set; }
}
```

- [ ] **Step 8: Register the DbSet + config**

In `src/AFK4.Platform.Api/Data/PlatformDbContext.cs`:

(a) Add the DbSet next to the other `DbSet<>` declarations (e.g. right after `PasswordResetTokens`):

```csharp
    public DbSet<StaffPhoneOtpEntity> StaffPhoneOtps => Set<StaffPhoneOtpEntity>();
```

(b) Add a configuration block inside `OnModelCreating` (mirror the `PasswordResetTokenEntity` block):

```csharp
    modelBuilder.Entity<StaffPhoneOtpEntity>(entity =>
    {
        entity.ToTable("staff_phone_otps");
        entity.HasKey(otp => otp.StaffPhoneOtpId);
        entity.Property(otp => otp.Phone).HasMaxLength(20).IsRequired();
        entity.Property(otp => otp.CodeHash).HasMaxLength(64).IsRequired();
        entity.HasIndex(otp => new { otp.StaffUserId, otp.Purpose, otp.CreatedAtUtc });
    });
```

- [ ] **Step 9: Register DI**

In `src/AFK4.Platform.Api/Program.cs`, in the service-registration section (before `var app = builder.Build();`, near the other identity-service registrations — search for `IStaffCredentialService` or `IStaffPasswordResetService` to find the area), add:

```csharp
builder.Services.Configure<PhoneOtpOptions>(
    builder.Configuration.GetSection(PhoneOtpOptions.SectionName));
builder.Services.AddSingleton<IPhoneOtpHasher, Sha256PhoneOtpHasher>();
builder.Services.AddSingleton<IPhoneOtpGenerator, RandomPhoneOtpGenerator>();
```

Add `using AFK4.Platform.Api.Identity.PhoneOtp;` at the top of Program.cs if the build reports the namespace is not in scope.

- [ ] **Step 10: Generate the migration**

Run (repo root):
```bash
dotnet ef migrations add AddStaffPhoneOtp --project src/AFK4.Platform.Api
```
Open the generated `*_AddStaffPhoneOtp.cs`; confirm `Up()` creates table `staff_phone_otps` with columns `StaffPhoneOtpId` (uuid, PK), `StaffUserId`, `OrganizationId`, `Phone` (varchar 20), `Purpose` (integer), `CodeHash` (varchar 64), `CreatedAtUtc`, `ExpiresAtUtc`, `AttemptCount` (integer), `ConsumedAtUtc` (nullable), plus the `(StaffUserId, Purpose, CreatedAtUtc)` index.

- [ ] **Step 11: Build + full Notifications/Identity test sweep + commit**

Run: `dotnet build src/AFK4.Platform.Api/AFK4.Platform.Api.csproj`
Expected: 0 errors.

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter Identity`
Expected: PASS — Tasks 1–5 identity tests green.

```bash
git add src/AFK4.Platform.Api/Identity/PhoneOtp src/AFK4.Platform.Api/Data/StaffPhoneOtpEntity.cs src/AFK4.Platform.Api/Data/PlatformDbContext.cs src/AFK4.Platform.Api/Data/Migrations src/AFK4.Platform.Api/Program.cs tests/AFK4.Platform.Api.Tests/Identity/PhoneOtpHasherAndGeneratorTests.cs
git commit -m "feat(identity): add staff phone OTP entity, hasher, generator and config"
```

---

### Task 6: SMS phone-verification template

**Files:**
- Modify: `src/AFK4.Platform.Api/Notifications/NotificationTemplateKeys.cs`
- Create: `src/AFK4.Platform.Api/Notifications/Templates/ru/staff.phone_verification.json`
- Create: `src/AFK4.Platform.Api/Notifications/Templates/en/staff.phone_verification.json`
- Create: `src/AFK4.Platform.Api/Notifications/Templates/tg/staff.phone_verification.json`
- Test: `tests/AFK4.Platform.Api.Tests/Notifications/StaffPhoneVerificationTemplateTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/AFK4.Platform.Api.Tests/Notifications/StaffPhoneVerificationTemplateTests.cs`:

```csharp
using AFK4.Platform.Api.Notifications;
using Xunit;

namespace AFK4.Platform.Api.Tests.Notifications;

public sealed class StaffPhoneVerificationTemplateTests
{
    private static readonly ITemplateProvider Provider = new EmbeddedTemplateProvider(defaultLocale: "ru");

    [Theory]
    [InlineData("ru")]
    [InlineData("en")]
    [InlineData("tg")]
    public void Template_PresentForLocale_WithCodePlaceholder(string locale)
    {
        var template = Provider.Get(NotificationTemplateKeys.StaffPhoneVerification, locale);
        Assert.Contains("{{code}}", template.BodyText, StringComparison.Ordinal);
    }

    [Fact]
    public void Key_IsRegisteredInAll()
    {
        Assert.Contains(NotificationTemplateKeys.StaffPhoneVerification, NotificationTemplateKeys.All);
        var exception = Record.Exception(() => Provider.EnsureKeysPresent(NotificationTemplateKeys.All));
        Assert.Null(exception);
    }

    [Fact]
    public void RuBody_StaysWithinOneSmsSegment()
    {
        // After substituting a 6-digit code, the Cyrillic SMS should fit one ~67-char segment.
        var template = Provider.Get(NotificationTemplateKeys.StaffPhoneVerification, "ru");
        var rendered = template.BodyText.Replace("{{code}}", "123456", StringComparison.Ordinal);
        Assert.True(rendered.Length <= 70, $"SMS body is {rendered.Length} chars: \"{rendered}\"");
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter StaffPhoneVerificationTemplateTests`
Expected: FAIL — `NotificationTemplateKeys.StaffPhoneVerification` does not exist.

- [ ] **Step 3: Add the template key**

In `src/AFK4.Platform.Api/Notifications/NotificationTemplateKeys.cs`:
(a) add the constant (after `StaffPasswordReset`):

```csharp
    public const string StaffPhoneVerification = "staff.phone_verification";
```

(b) add `StaffPhoneVerification` to the `All` collection initializer (append it to the list).

- [ ] **Step 4: Create the three template files**

Create `src/AFK4.Platform.Api/Notifications/Templates/ru/staff.phone_verification.json`:

```json
{
  "subject": "Код подтверждения AFK4.NET",
  "bodyText": "AFK4.NET: код {{code}}. Никому не сообщайте.",
  "bodyHtml": ""
}
```

Create `src/AFK4.Platform.Api/Notifications/Templates/en/staff.phone_verification.json`:

```json
{
  "subject": "AFK4.NET verification code",
  "bodyText": "AFK4.NET: code {{code}}. Do not share it with anyone.",
  "bodyHtml": ""
}
```

Create `src/AFK4.Platform.Api/Notifications/Templates/tg/staff.phone_verification.json`:

```json
{
  "subject": "Рамзи тасдиқи AFK4.NET",
  "bodyText": "AFK4.NET: рамз {{code}}. Ба касе нагӯед.",
  "bodyHtml": ""
}
```

- [ ] **Step 5: Run the template test to verify it passes**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter StaffPhoneVerificationTemplateTests`
Expected: PASS (5 cases). If a file isn't found, confirm it's under `Notifications/Templates/<locale>/` exactly (the csproj glob embeds it automatically; a clean rebuild may be needed: `dotnet build src/AFK4.Platform.Api/AFK4.Platform.Api.csproj`).

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Platform.Api/Notifications/NotificationTemplateKeys.cs src/AFK4.Platform.Api/Notifications/Templates/ru/staff.phone_verification.json src/AFK4.Platform.Api/Notifications/Templates/en/staff.phone_verification.json src/AFK4.Platform.Api/Notifications/Templates/tg/staff.phone_verification.json tests/AFK4.Platform.Api.Tests/Notifications/StaffPhoneVerificationTemplateTests.cs
git commit -m "feat(notifications): add staff phone verification SMS template"
```

---

### Task 7: Phone verification service + endpoints (start + confirm)

**Files:**
- Create: `src/AFK4.Shared.Contracts/Identity/StaffPhoneVerificationContracts.cs`
- Create: `src/AFK4.Platform.Api/Identity/IStaffPhoneVerificationService.cs`
- Create: `src/AFK4.Platform.Api/Identity/EfStaffPhoneVerificationService.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs` (DI + two endpoints)
- Test: `tests/AFK4.Platform.Api.Tests/Identity/StaffPhoneVerificationServiceTests.cs`
- Test: `tests/AFK4.Platform.Api.Tests/StaffPhoneVerificationEndpointTests.cs`

- [ ] **Step 1: Write the failing service test**

Create `tests/AFK4.Platform.Api.Tests/Identity/StaffPhoneVerificationServiceTests.cs`:

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Identity.PhoneOtp;
using AFK4.Platform.Api.Notifications;
using AFK4.Shared.Contracts.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace AFK4.Platform.Api.Tests.Identity;

public sealed class StaffPhoneVerificationServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-05T12:00:00Z");

    private static PlatformDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static async Task<Guid> SeedStaffAsync(PlatformDbContext db, Guid orgId)
    {
        var staffUserId = Guid.NewGuid();
        db.StaffUsers.Add(new StaffUserEntity
        {
            StaffUserId = staffUserId,
            OrganizationId = orgId,
            UserName = "owner",
            NormalizedUserName = "OWNER",
            DisplayName = "Owner",
            PasswordHash = "x",
            IsActive = true,
        });
        await db.SaveChangesAsync();
        return staffUserId;
    }

    private static (EfStaffPhoneVerificationService Service, CapturingNotificationService Notifications, FixedTimeProvider Time)
        CreateService(PlatformDbContext db, string fixedCode = "123456")
    {
        var notifications = new CapturingNotificationService();
        var time = new FixedTimeProvider(Now);
        var service = new EfStaffPhoneVerificationService(
            db,
            notifications,
            new Sha256PhoneOtpHasher(),
            new FixedPhoneOtpGenerator(fixedCode),
            time,
            Options.Create(new PhoneOtpOptions()),
            Options.Create(new NotificationOptions { DefaultLocale = "ru" }));
        return (service, notifications, time);
    }

    [Fact]
    public async Task Start_StoresOtp_AndSendsSmsWithCode()
    {
        await using var db = CreateDb();
        var orgId = Guid.NewGuid();
        var staffUserId = await SeedStaffAsync(db, orgId);
        var (service, notifications, _) = CreateService(db, fixedCode: "424242");

        var result = await service.StartAsync(staffUserId, orgId, "+992 93 738-00-70", CancellationToken.None);

        Assert.Equal(PhoneVerificationStartStatus.Sent, result.Status);
        Assert.Equal(1, await db.StaffPhoneOtps.CountAsync());
        var request = Assert.Single(notifications.SentNow);
        Assert.Equal(NotificationTemplateKeys.StaffPhoneVerification, request.TemplateKey);
        Assert.Equal("424242", request.Tokens["code"]);
        Assert.Equal("+992937380070", request.Recipient.PhoneNumber);
        Assert.NotNull(request.PreferredChannels);
        Assert.Contains(NotificationChannel.Sms, request.PreferredChannels!);
    }

    [Fact]
    public async Task Start_InvalidPhone_ReturnsInvalid_NoOtp()
    {
        await using var db = CreateDb();
        var orgId = Guid.NewGuid();
        var staffUserId = await SeedStaffAsync(db, orgId);
        var (service, notifications, _) = CreateService(db);

        var result = await service.StartAsync(staffUserId, orgId, "12345", CancellationToken.None);

        Assert.Equal(PhoneVerificationStartStatus.InvalidPhone, result.Status);
        Assert.Equal(0, await db.StaffPhoneOtps.CountAsync());
        Assert.Empty(notifications.SentNow);
    }

    [Fact]
    public async Task Start_WithinCooldown_ReturnsCooldown()
    {
        await using var db = CreateDb();
        var orgId = Guid.NewGuid();
        var staffUserId = await SeedStaffAsync(db, orgId);
        var (service, _, _) = CreateService(db);

        await service.StartAsync(staffUserId, orgId, "992937380070", CancellationToken.None);
        var second = await service.StartAsync(staffUserId, orgId, "992937380070", CancellationToken.None);

        Assert.Equal(PhoneVerificationStartStatus.CooldownActive, second.Status);
        Assert.True(second.ResendAfterSeconds > 0);
        Assert.Equal(1, await db.StaffPhoneOtps.CountAsync());
    }

    [Fact]
    public async Task Confirm_CorrectCode_MarksPhoneVerified_ConsumesOtp()
    {
        await using var db = CreateDb();
        var orgId = Guid.NewGuid();
        var staffUserId = await SeedStaffAsync(db, orgId);
        var (service, _, _) = CreateService(db, fixedCode: "424242");
        await service.StartAsync(staffUserId, orgId, "992937380070", CancellationToken.None);

        var result = await service.ConfirmAsync(staffUserId, "424242", CancellationToken.None);

        Assert.Equal(PhoneConfirmStatus.Confirmed, result.Status);
        var staff = await db.StaffUsers.SingleAsync(user => user.StaffUserId == staffUserId);
        Assert.Equal("992937380070", staff.NormalizedPhone);
        Assert.Equal("+992937380070", staff.Phone);
        Assert.NotNull(staff.PhoneVerifiedAtUtc);
        var otp = await db.StaffPhoneOtps.SingleAsync();
        Assert.NotNull(otp.ConsumedAtUtc);
    }

    [Fact]
    public async Task Confirm_WrongCode_IncrementsAttempt_ThenLocksOut()
    {
        await using var db = CreateDb();
        var orgId = Guid.NewGuid();
        var staffUserId = await SeedStaffAsync(db, orgId);
        var (service, _, _) = CreateService(db, fixedCode: "424242");
        await service.StartAsync(staffUserId, orgId, "992937380070", CancellationToken.None);

        Assert.Equal(PhoneConfirmStatus.InvalidCode, (await service.ConfirmAsync(staffUserId, "000000", CancellationToken.None)).Status);
        Assert.Equal(PhoneConfirmStatus.InvalidCode, (await service.ConfirmAsync(staffUserId, "000000", CancellationToken.None)).Status);
        Assert.Equal(PhoneConfirmStatus.InvalidCode, (await service.ConfirmAsync(staffUserId, "000000", CancellationToken.None)).Status);
        // 4th attempt: attempts exhausted, even the correct code is rejected.
        Assert.Equal(PhoneConfirmStatus.TooManyAttempts, (await service.ConfirmAsync(staffUserId, "424242", CancellationToken.None)).Status);
    }

    [Fact]
    public async Task Confirm_ExpiredCode_ReturnsExpired()
    {
        await using var db = CreateDb();
        var orgId = Guid.NewGuid();
        var staffUserId = await SeedStaffAsync(db, orgId);
        var (service, _, time) = CreateService(db, fixedCode: "424242");
        await service.StartAsync(staffUserId, orgId, "992937380070", CancellationToken.None);
        time.Now = Now.AddMinutes(10);

        var result = await service.ConfirmAsync(staffUserId, "424242", CancellationToken.None);

        Assert.Equal(PhoneConfirmStatus.Expired, result.Status);
    }

    [Fact]
    public async Task Confirm_PhoneAlreadyVerifiedByAnother_ReturnsConflict()
    {
        await using var db = CreateDb();
        var orgId = Guid.NewGuid();
        var staffUserId = await SeedStaffAsync(db, orgId);
        db.StaffUsers.Add(new StaffUserEntity
        {
            StaffUserId = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            UserName = "other",
            NormalizedUserName = "OTHER",
            DisplayName = "Other",
            PasswordHash = "x",
            IsActive = true,
            NormalizedPhone = "992937380070",
            Phone = "+992937380070",
            PhoneVerifiedAtUtc = Now.AddDays(-1),
        });
        await db.SaveChangesAsync();
        var (service, _, _) = CreateService(db, fixedCode: "424242");
        await service.StartAsync(staffUserId, orgId, "992937380070", CancellationToken.None);

        var result = await service.ConfirmAsync(staffUserId, "424242", CancellationToken.None);

        Assert.Equal(PhoneConfirmStatus.PhoneAlreadyInUse, result.Status);
    }

    // ---- test doubles ----

    private sealed class FixedPhoneOtpGenerator(string code) : IPhoneOtpGenerator
    {
        public string Generate() => code;
    }

    private sealed class CapturingNotificationService : INotificationService
    {
        public List<NotificationRequest> SentNow { get; } = [];

        public Task<NotificationHandle> SendAsync(NotificationRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new NotificationHandle([Guid.NewGuid()], Created: true));

        public Task<NotificationDeliveryResult> SendNowAsync(NotificationRequest request, CancellationToken cancellationToken)
        {
            SentNow.Add(request);
            return Task.FromResult(new NotificationDeliveryResult(
                new NotificationHandle([Guid.NewGuid()], Created: true), Delivered: true, Error: null));
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter StaffPhoneVerificationServiceTests`
Expected: FAIL — compile error, the service + result types do not exist.

- [ ] **Step 3: Create the contracts**

Create `src/AFK4.Shared.Contracts/Identity/StaffPhoneVerificationContracts.cs`:

```csharp
namespace AFK4.Shared.Contracts.Identity;

public sealed record StaffPhoneStartVerificationRequest(string Phone);

public sealed record StaffPhoneVerificationStartedResponse(int ExpiresInSeconds, int ResendAfterSeconds);

public sealed record StaffPhoneConfirmRequest(string Code);

public sealed record StaffPhoneConfirmedResponse(string Phone);
```

- [ ] **Step 4: Create the service interface + result types**

Create `src/AFK4.Platform.Api/Identity/IStaffPhoneVerificationService.cs`:

```csharp
namespace AFK4.Platform.Api.Identity;

public enum PhoneVerificationStartStatus
{
    Sent,
    InvalidPhone,
    CooldownActive,
    RateLimited,
    SmsFailed,
}

public sealed record PhoneVerificationStartResult(
    PhoneVerificationStartStatus Status,
    int ExpiresInSeconds,
    int ResendAfterSeconds,
    string? Error);

public enum PhoneConfirmStatus
{
    Confirmed,
    NoActiveCode,
    Expired,
    TooManyAttempts,
    InvalidCode,
    PhoneAlreadyInUse,
}

public sealed record PhoneConfirmResult(PhoneConfirmStatus Status, int RemainingAttempts, string? VerifiedPhone);

public interface IStaffPhoneVerificationService
{
    Task<PhoneVerificationStartResult> StartAsync(
        Guid staffUserId, Guid organizationId, string rawPhone, CancellationToken cancellationToken);

    Task<PhoneConfirmResult> ConfirmAsync(
        Guid staffUserId, string code, CancellationToken cancellationToken);
}
```

- [ ] **Step 5: Implement the service**

Create `src/AFK4.Platform.Api/Identity/EfStaffPhoneVerificationService.cs`:

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity.PhoneOtp;
using AFK4.Platform.Api.Notifications;
using AFK4.Shared.Contracts.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Identity;

public sealed class EfStaffPhoneVerificationService(
    PlatformDbContext db,
    INotificationService notifications,
    IPhoneOtpHasher hasher,
    IPhoneOtpGenerator generator,
    TimeProvider timeProvider,
    IOptions<PhoneOtpOptions> otpOptions,
    IOptions<NotificationOptions> notificationOptions) : IStaffPhoneVerificationService
{
    private readonly PhoneOtpOptions otpOptions = otpOptions.Value;
    private readonly NotificationOptions notificationOptions = notificationOptions.Value;

    public async Task<PhoneVerificationStartResult> StartAsync(
        Guid staffUserId, Guid organizationId, string rawPhone, CancellationToken cancellationToken)
    {
        var normalizedPhone = PhoneNumberNormalizer.Normalize(rawPhone);
        if (normalizedPhone is null)
        {
            return new PhoneVerificationStartResult(PhoneVerificationStartStatus.InvalidPhone, 0, 0, null);
        }

        var now = timeProvider.GetUtcNow();

        var recent = await db.StaffPhoneOtps
            .Where(otp => otp.StaffUserId == staffUserId && otp.Purpose == StaffPhoneOtpPurpose.PhoneVerification)
            .OrderByDescending(otp => otp.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (recent is not null)
        {
            var sinceLast = now - recent.CreatedAtUtc;
            if (sinceLast < otpOptions.ResendCooldown)
            {
                var wait = (int)Math.Ceiling((otpOptions.ResendCooldown - sinceLast).TotalSeconds);
                return new PhoneVerificationStartResult(PhoneVerificationStartStatus.CooldownActive, 0, wait, null);
            }
        }

        var sinceHourAgo = now - TimeSpan.FromHours(1);
        var sendsLastHour = await db.StaffPhoneOtps.CountAsync(
            otp => otp.StaffUserId == staffUserId
                && otp.Purpose == StaffPhoneOtpPurpose.PhoneVerification
                && otp.CreatedAtUtc > sinceHourAgo,
            cancellationToken);
        if (sendsLastHour >= otpOptions.MaxSendsPerHour)
        {
            return new PhoneVerificationStartResult(PhoneVerificationStartStatus.RateLimited, 0, 0, null);
        }

        var code = generator.Generate();
        var otpId = Guid.NewGuid();
        db.StaffPhoneOtps.Add(new StaffPhoneOtpEntity
        {
            StaffPhoneOtpId = otpId,
            StaffUserId = staffUserId,
            OrganizationId = organizationId,
            Phone = normalizedPhone,
            Purpose = StaffPhoneOtpPurpose.PhoneVerification,
            CodeHash = hasher.Hash(code),
            CreatedAtUtc = now,
            ExpiresAtUtc = now + otpOptions.Lifetime,
            AttemptCount = 0,
        });
        await db.SaveChangesAsync(cancellationToken);

        var staff = await db.StaffUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.StaffUserId == staffUserId, cancellationToken);

        var request = new NotificationRequest(
            TemplateKey: NotificationTemplateKeys.StaffPhoneVerification,
            Category: NotificationCategory.Transactional,
            Recipient: new NotificationRecipient(
                Locale: notificationOptions.DefaultLocale,
                PhoneNumber: "+" + normalizedPhone,
                StaffUserId: staffUserId),
            Tokens: new Dictionary<string, string>
            {
                ["code"] = code,
                ["expiresInMinutes"] = ((int)otpOptions.Lifetime.TotalMinutes).ToString(),
                ["displayName"] = staff?.DisplayName ?? string.Empty,
            },
            IdempotencyKey: $"staff-phone-verify:{otpId:N}",
            PreferredChannels: [NotificationChannel.Sms],
            OrganizationId: organizationId);

        var delivery = await notifications.SendNowAsync(request, cancellationToken);

        var expiresInSeconds = (int)otpOptions.Lifetime.TotalSeconds;
        var resendAfter = (int)otpOptions.ResendCooldown.TotalSeconds;
        return delivery.Delivered
            ? new PhoneVerificationStartResult(PhoneVerificationStartStatus.Sent, expiresInSeconds, resendAfter, null)
            : new PhoneVerificationStartResult(PhoneVerificationStartStatus.SmsFailed, expiresInSeconds, resendAfter, delivery.Error);
    }

    public async Task<PhoneConfirmResult> ConfirmAsync(
        Guid staffUserId, string code, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var otp = await db.StaffPhoneOtps
            .Where(candidate => candidate.StaffUserId == staffUserId
                && candidate.Purpose == StaffPhoneOtpPurpose.PhoneVerification
                && candidate.ConsumedAtUtc == null)
            .OrderByDescending(candidate => candidate.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (otp is null)
        {
            return new PhoneConfirmResult(PhoneConfirmStatus.NoActiveCode, 0, null);
        }

        if (otp.ExpiresAtUtc <= now)
        {
            return new PhoneConfirmResult(PhoneConfirmStatus.Expired, 0, null);
        }

        if (otp.AttemptCount >= otpOptions.MaxAttempts)
        {
            return new PhoneConfirmResult(PhoneConfirmStatus.TooManyAttempts, 0, null);
        }

        var enteredDigits = PhoneOtpCode.KeepDigits(code);
        if (hasher.Hash(enteredDigits) != otp.CodeHash)
        {
            otp.AttemptCount++;
            await db.SaveChangesAsync(cancellationToken);
            var remaining = Math.Max(0, otpOptions.MaxAttempts - otp.AttemptCount);
            return new PhoneConfirmResult(PhoneConfirmStatus.InvalidCode, remaining, null);
        }

        // App-level uniqueness check (the DB partial-unique index is the production backstop;
        // EF InMemory doesn't enforce it, so we check here too — and it yields a clean error).
        var conflict = await db.StaffUsers.AnyAsync(
            user => user.StaffUserId != staffUserId
                && user.NormalizedPhone == otp.Phone
                && user.PhoneVerifiedAtUtc != null
                && user.IsActive,
            cancellationToken);
        if (conflict)
        {
            return new PhoneConfirmResult(PhoneConfirmStatus.PhoneAlreadyInUse, 0, null);
        }

        var staff = await db.StaffUsers.FirstOrDefaultAsync(
            user => user.StaffUserId == staffUserId, cancellationToken);
        if (staff is null)
        {
            return new PhoneConfirmResult(PhoneConfirmStatus.NoActiveCode, 0, null);
        }

        staff.Phone = "+" + otp.Phone;
        staff.NormalizedPhone = otp.Phone;
        staff.PhoneVerifiedAtUtc = now;
        otp.ConsumedAtUtc = now;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Lost a race against the partial-unique index.
            return new PhoneConfirmResult(PhoneConfirmStatus.PhoneAlreadyInUse, 0, null);
        }

        return new PhoneConfirmResult(PhoneConfirmStatus.Confirmed, otpOptions.MaxAttempts, staff.Phone);
    }
}

internal static class PhoneOtpCode
{
    public static string KeepDigits(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return new string(value.Where(character => character is >= '0' and <= '9').ToArray());
    }
}
```

- [ ] **Step 6: Run the service test to verify it passes**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter StaffPhoneVerificationServiceTests`
Expected: PASS (7 cases).

- [ ] **Step 7: Register DI + endpoints**

In `src/AFK4.Platform.Api/Program.cs`:

(a) Register the service near the Task-5 OTP registrations (scoped — it uses `PlatformDbContext`):

```csharp
builder.Services.AddScoped<IStaffPhoneVerificationService, EfStaffPhoneVerificationService>();
```

(b) Add the two authenticated endpoints. Place them right after the `sign-in-by-phone` endpoint from Task 4 (or anywhere in the `/api/auth/staff/*` group). They authenticate by reading `IStaffContextAccessor.Current` (the `StaffAuthenticationMiddleware` populates it from the Bearer token):

```csharp
app.MapPost("/api/auth/staff/phone/start-verification", async (
    StaffPhoneStartVerificationRequest request,
    IStaffContextAccessor staffContextAccessor,
    IStaffPhoneVerificationService verificationService,
    CancellationToken cancellationToken) =>
{
    var staff = staffContextAccessor.Current;
    if (staff is null)
    {
        return Results.Unauthorized();
    }

    var result = await verificationService.StartAsync(
        staff.StaffUserId, staff.OrganizationId, request.Phone, cancellationToken);

    return result.Status switch
    {
        PhoneVerificationStartStatus.Sent => Results.Ok(
            new StaffPhoneVerificationStartedResponse(result.ExpiresInSeconds, result.ResendAfterSeconds)),
        PhoneVerificationStartStatus.InvalidPhone => Results.BadRequest(new { error = "invalid_phone" }),
        PhoneVerificationStartStatus.CooldownActive => Results.Json(
            new { error = "cooldown_active", resendAfterSeconds = result.ResendAfterSeconds },
            statusCode: StatusCodes.Status429TooManyRequests),
        PhoneVerificationStartStatus.RateLimited => Results.Json(
            new { error = "rate_limited" }, statusCode: StatusCodes.Status429TooManyRequests),
        PhoneVerificationStartStatus.SmsFailed => Results.Json(
            new { error = "sms_unavailable" }, statusCode: StatusCodes.Status502BadGateway),
        _ => Results.StatusCode(StatusCodes.Status500InternalServerError),
    };
});

app.MapPost("/api/auth/staff/phone/confirm", async (
    StaffPhoneConfirmRequest request,
    IStaffContextAccessor staffContextAccessor,
    IStaffPhoneVerificationService verificationService,
    CancellationToken cancellationToken) =>
{
    var staff = staffContextAccessor.Current;
    if (staff is null)
    {
        return Results.Unauthorized();
    }

    var result = await verificationService.ConfirmAsync(staff.StaffUserId, request.Code, cancellationToken);

    return result.Status switch
    {
        PhoneConfirmStatus.Confirmed => Results.Ok(new StaffPhoneConfirmedResponse(result.VerifiedPhone!)),
        PhoneConfirmStatus.InvalidCode => Results.Json(
            new { error = "invalid_code", remainingAttempts = result.RemainingAttempts },
            statusCode: StatusCodes.Status400BadRequest),
        PhoneConfirmStatus.Expired => Results.Json(
            new { error = "code_expired" }, statusCode: StatusCodes.Status410Gone),
        PhoneConfirmStatus.NoActiveCode => Results.Json(
            new { error = "no_active_code" }, statusCode: StatusCodes.Status410Gone),
        PhoneConfirmStatus.TooManyAttempts => Results.Json(
            new { error = "too_many_attempts" }, statusCode: StatusCodes.Status429TooManyRequests),
        PhoneConfirmStatus.PhoneAlreadyInUse => Results.Json(
            new { error = "phone_already_in_use" }, statusCode: StatusCodes.Status409Conflict),
        _ => Results.StatusCode(StatusCodes.Status500InternalServerError),
    };
});
```

Add `using AFK4.Shared.Contracts.Identity;` and `using AFK4.Platform.Api.Identity;` at the top of Program.cs only if the build reports them missing (both are almost certainly already imported).

- [ ] **Step 8: Write the endpoint integration test**

Create `tests/AFK4.Platform.Api.Tests/StaffPhoneVerificationEndpointTests.cs`. It injects a recording `ISmsTransport` (via the factory's `extraServices` hook), authenticates as a technician, starts verification, reads the 6-digit code from the captured SMS, then confirms:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Notifications;
using AFK4.Shared.Contracts.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public sealed class StaffPhoneVerificationEndpointTests
{
    private sealed class RecordingSmsTransport : ISmsTransport
    {
        public List<SmsMessage> Sent { get; } = [];

        public Task SendAsync(SmsMessage message, CancellationToken cancellationToken)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task StartThenConfirm_VerifiesStaffPhone()
    {
        var recording = new RecordingSmsTransport();
        await using var factory = new PlatformApiFactory(extraServices: services =>
        {
            services.RemoveAll<ISmsTransport>();
            services.AddSingleton<ISmsTransport>(recording);
        });
        using var client = factory.CreateClient();
        var staffUserId = await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Technician);

        var start = await client.PostAsJsonAsync(
            "/api/auth/staff/phone/start-verification",
            new StaffPhoneStartVerificationRequest("+992 93 738-00-70"));
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);

        var sms = Assert.Single(recording.Sent);
        Assert.Equal("+992937380070", sms.ToPhoneNumber);
        var code = Regex.Match(sms.Text, "\\d{6}").Value;
        Assert.False(string.IsNullOrEmpty(code));

        var confirm = await client.PostAsJsonAsync(
            "/api/auth/staff/phone/confirm",
            new StaffPhoneConfirmRequest(code));
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var staff = db.StaffUsers.Single(user => user.StaffUserId == staffUserId);
        Assert.Equal("992937380070", staff.NormalizedPhone);
        Assert.NotNull(staff.PhoneVerifiedAtUtc);
    }

    [Fact]
    public async Task StartVerification_WithoutBearer_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/staff/phone/start-verification",
            new StaffPhoneStartVerificationRequest("992937380070"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
```

> **Note on `StaffAuthTestHelper.AuthorizeAsAsync`:** confirm its exact signature/return by reading `tests/AFK4.Platform.Api.Tests/StaffAuthTestHelper.cs` first. It seeds a staff user of the given role and sets `client.DefaultRequestHeaders.Authorization`. If it returns something other than the seeded `Guid staffUserId`, adapt the assertion to fetch the single technician staff row from the DB instead. Do not change the helper.

- [ ] **Step 9: Run the endpoint test to verify it passes**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter StaffPhoneVerificationEndpointTests`
Expected: PASS (2 cases). This exercises the real notification pipeline (SendNowAsync → SmsChannel → recording transport) and real OTP generator/hasher end-to-end.

- [ ] **Step 10: Full build + suite + commit**

Run: `dotnet build src/AFK4.Platform.Api/AFK4.Platform.Api.csproj`
Expected: 0 errors.

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj`
Expected: PASS — entire suite green (no regressions; the app still boots with the new template key in `EnsureKeysPresent(All)`).

```bash
git add src/AFK4.Shared.Contracts/Identity/StaffPhoneVerificationContracts.cs src/AFK4.Platform.Api/Identity/IStaffPhoneVerificationService.cs src/AFK4.Platform.Api/Identity/EfStaffPhoneVerificationService.cs src/AFK4.Platform.Api/Program.cs tests/AFK4.Platform.Api.Tests/Identity/StaffPhoneVerificationServiceTests.cs tests/AFK4.Platform.Api.Tests/StaffPhoneVerificationEndpointTests.cs
git commit -m "feat(auth): add staff phone verification (SMS OTP) endpoints"
```

---

## Self-review

**1. Spec coverage (§5 Phase B):**
- Schema: `Phone`/`NormalizedPhone`/`PhoneVerifiedAtUtc` + `HasMaxLength(20)` + global partial-unique index + migration → Task 3 ✓
- `NormalizedPhone` = digits-only E.164 → Task 2 (`PhoneNumberNormalizer`) ✓
- OTP entity `StaffPhoneOtpEntity` (Id, StaffUserId, OrganizationId, Phone, Purpose, CodeHash SHA256-hex, CreatedAtUtc, ExpiresAtUtc, AttemptCount, ConsumedAtUtc) + `IPhoneOtpHasher` + 6-digit generator → Task 5 ✓
- `start-verification` (upsert pending phone, generate OTP, SendNowAsync SMS, return expiry/resend) + `confirm` (≤3 attempts, TTL, set fields, enforce uniqueness) → Task 7 ✓
- `sign-in-by-phone` resolving verified+active, verify password, issue tokens → Task 4 ✓
- `devices.install` granted to owner/branch_manager/technician → Task 1 ✓
- SMS template (Cyrillic, ≤~67 chars, sender AFK4.NET) → Task 6 ✓ (Phase A intentionally deferred templates here so `EnsureKeysPresent(All)` stays green — done now.)
- **Deferred (documented, not gaps):** admin-panel UI = Phase B-UI plan; SMS password reset = Phase D; wizard/authenticated install = Phase C.

**2. Security & rate limiting (§7):** 6-digit, 5-min TTL, ≤3 attempts, 60s resend cooldown, ≤5 sends/hour, stored hashed, single-use (`ConsumedAtUtc`), only verified phones sign in, SMS failure surfaces as `sms_unavailable` (502) rather than a dead end → Tasks 5/7 ✓. (Per-IP throttle on `start-verification` is a thin add over the existing `IInstallRequestThrottle`; not implemented here since these endpoints are already behind staff auth — noted as an optional hardening for Phase D.)

**3. Placeholder scan:** every code step has complete code; commands have expected output; the only "read first" notes are for `StaffAuthTestHelper` (whose exact signature wasn't captured) and the migration content check — both are verifications, not placeholders.

**4. Type consistency:** `PhoneNumberNormalizer.Normalize(string?) → string?`; `IPhoneOtpHasher.Hash(string) → string`; `IPhoneOtpGenerator.Generate() → string`; `StaffPhoneOtpEntity`/`StaffPhoneOtpPurpose`; `IStaffPhoneVerificationService.StartAsync(Guid,Guid,string,ct)`/`ConfirmAsync(Guid,string,ct)` returning `PhoneVerificationStartResult`/`PhoneConfirmResult`; `StaffSignInByPhoneRequest(PhoneNumber,Password)`; `SignInByPhoneAsync` on `IStaffCredentialService`; contracts in `AFK4.Shared.Contracts.Identity`. Names are consistent across tasks and tests. `NotificationTemplateKeys.StaffPhoneVerification = "staff.phone_verification"` matches the three JSON filenames. `ISmsTransport.SendAsync(SmsMessage(ToPhoneNumber,Text), ct)` matches the Phase A definition used by the recording transport. ✓

**5. Ordering:** permission → normalizer → schema/migration → sign-in-by-phone (early testable win, seeds verified phones directly) → OTP infra/migration → template → verification endpoints. Two migrations generated at the right model states (Task 3, Task 5). ✓
