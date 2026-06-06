# Phase D — SMS Password Reset (backend) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a staff member reset their password by SMS — request a one-time code to their verified phone, then set a new password with that code — entirely on the backend (no UI this phase).

**Architecture:** Mirror the Phase B phone-verification stack. A new `EfStaffPhonePasswordResetService` reuses the existing OTP infrastructure (`StaffPhoneOtpEntity` with the already-present `Purpose=PasswordReset`, `IPhoneOtpHasher/Generator`, `PhoneOtpOptions`) and sends a new `staff.password_reset_sms` template via `INotificationService`. Two public, IP-rate-limited endpoints (`forgot/reset-password-by-phone`) drive it. The password-set + token-revocation "core" is extracted from `EfStaffPasswordResetService` into a shared `StaffTokenRevocation` helper so both email and SMS resets share it. Anti-enumeration: the forgot endpoint returns a uniform response whether or not the phone maps to an account.

**Tech Stack:** .NET 10, ASP.NET Core minimal APIs, EF Core (InMemory in tests, `WebApplicationFactory<Program>`), `IOptions<T>`, xUnit (`tests/AFK4.Platform.Api.Tests`). Contracts in `AFK4.Shared.Contracts`.

**Scope note:** Backend only. The user-facing reset screen, login-by-email, and email parity are deliberately deferred to the *email-identity-parity* epic (see spec §7), so the reset screen is built once, channel-aware. The existing email reset path is untouched (only refactored to share `StaffTokenRevocation`).

---

## File Structure

**Backend — `src/AFK4.Platform.Api`:**
- Create `Identity/StaffTokenRevocation.cs` — shared static helper: revoke active access+refresh tokens.
- Modify `Identity/EfStaffPasswordResetService.cs` — call the shared helper (delete the private method).
- Modify `Notifications/NotificationTemplateKeys.cs` — add `StaffPasswordResetSms` + register in `All`.
- Create `Notifications/Templates/{ru,en,tg}/staff.password_reset_sms.json` — the SMS bodies.
- Create `Identity/IStaffPhonePasswordResetService.cs` — interface + status enums + result records.
- Create `Identity/EfStaffPhonePasswordResetService.cs` — the reset service.
- Modify `Program.cs` — DI registration (~line 247) + `staff-reset` rate-limiter policy (~line 352).
- Modify `Endpoints/AuthEndpoints.cs` — two new public endpoints.

**Contracts — `src/AFK4.Shared.Contracts`:**
- Create `Identity/StaffForgotPasswordByPhoneRequest.cs`
- Create `Identity/StaffResetPasswordByPhoneRequest.cs`

**Tests — `tests/AFK4.Platform.Api.Tests`:**
- Create `Notifications/StaffPasswordResetSmsTemplateTests.cs`
- Create `Identity/EfStaffPhonePasswordResetServiceTests.cs`
- Create `StaffPasswordResetByPhoneEndpointTests.cs`

Reference (do not modify): `Identity/EfStaffPhoneVerificationService.cs`, `Identity/IStaffPhoneVerificationService.cs`, `Identity/PasswordHashingStaffCredentialService.cs`, `Identity/PhoneNumberNormalizer.cs`, `tests/.../Identity/StaffPhoneVerificationServiceTests.cs`, `tests/.../StaffSignInByPhoneEndpointTests.cs`, `tests/.../StaffPhoneVerificationEndpointTests.cs`, `tests/.../Notifications/StaffPhoneVerificationTemplateTests.cs`.

**Sequencing:** Task 1 (refactor) → 2 (template) → 3 (contracts) → 4 (interface) → 5 (service+unit tests) → 6 (DI+rate limit) → 7 (endpoints+endpoint tests). Each task builds and stays green on its own.

---

### Task 1: Extract `StaffTokenRevocation` shared helper (behavior-preserving refactor)

Pull the active-token revocation out of `EfStaffPasswordResetService` so the SMS reset service can reuse the exact same logic. No behavior change — existing email-reset tests are the safety net.

**Files:**
- Create: `src/AFK4.Platform.Api/Identity/StaffTokenRevocation.cs`
- Modify: `src/AFK4.Platform.Api/Identity/EfStaffPasswordResetService.cs:97` (call site) and `:118-135` (delete private method)

- [ ] **Step 1: Create the shared helper**

Create `src/AFK4.Platform.Api/Identity/StaffTokenRevocation.cs`:

```csharp
using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Identity;

/// <summary>
/// Revokes a staff account's active access + refresh tokens (sets <c>RevokedAtUtc</c>). Shared by
/// the email and SMS password-reset flows so a completed reset logs the account out everywhere.
/// Does NOT call SaveChanges — the caller commits within its own unit of work.
/// </summary>
internal static class StaffTokenRevocation
{
    public static async Task RevokeActiveAsync(
        PlatformDbContext db,
        Guid organizationId,
        Guid staffUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var accessTokens = await db.StaffAccessTokens
            .Where(token => token.OrganizationId == organizationId && token.StaffUserId == staffUserId && token.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var token in accessTokens)
        {
            token.RevokedAtUtc = now;
        }

        var refreshTokens = await db.StaffRefreshTokens
            .Where(token => token.OrganizationId == organizationId && token.StaffUserId == staffUserId && token.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var token in refreshTokens)
        {
            token.RevokedAtUtc = now;
        }
    }
}
```

- [ ] **Step 2: Call the helper from `EfStaffPasswordResetService`**

In `src/AFK4.Platform.Api/Identity/EfStaffPasswordResetService.cs`, replace the call at line 97:

```csharp
        await RevokeActiveTokensAsync(staff.OrganizationId, staff.StaffUserId, now, cancellationToken);
```

with:

```csharp
        await StaffTokenRevocation.RevokeActiveAsync(db, staff.OrganizationId, staff.StaffUserId, now, cancellationToken);
```

Then delete the now-unused private method (lines 118-135):

```csharp
    private async Task RevokeActiveTokensAsync(Guid organizationId, Guid staffUserId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // ... full body ...
    }
```

(Leave `HashToken` and the rest of the class as-is.)

- [ ] **Step 3: Build**

Run: `dotnet build src/AFK4.Platform.Api/AFK4.Platform.Api.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Run the existing password-reset tests (safety net for the refactor)**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~PasswordReset"`
Expected: PASS — `StaffPasswordResetServiceTests` + `StaffPasswordResetEndpointTests` green (proves token revocation behavior is unchanged).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Api/Identity/StaffTokenRevocation.cs src/AFK4.Platform.Api/Identity/EfStaffPasswordResetService.cs
git commit -m "refactor(identity): extract StaffTokenRevocation shared helper"
```

---

### Task 2: SMS reset template (`staff.password_reset_sms`)

**Files:**
- Modify: `src/AFK4.Platform.Api/Notifications/NotificationTemplateKeys.cs`
- Create: `src/AFK4.Platform.Api/Notifications/Templates/ru/staff.password_reset_sms.json`
- Create: `src/AFK4.Platform.Api/Notifications/Templates/en/staff.password_reset_sms.json`
- Create: `src/AFK4.Platform.Api/Notifications/Templates/tg/staff.password_reset_sms.json`
- Test: `tests/AFK4.Platform.Api.Tests/Notifications/StaffPasswordResetSmsTemplateTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/AFK4.Platform.Api.Tests/Notifications/StaffPasswordResetSmsTemplateTests.cs`:

```csharp
using AFK4.Platform.Api.Notifications;
using Xunit;

namespace AFK4.Platform.Api.Tests.Notifications;

public sealed class StaffPasswordResetSmsTemplateTests
{
    private static readonly ITemplateProvider Provider = new EmbeddedTemplateProvider(defaultLocale: "ru");

    [Theory]
    [InlineData("ru")]
    [InlineData("en")]
    [InlineData("tg")]
    public void Template_PresentForLocale_WithCodePlaceholder(string locale)
    {
        var template = Provider.Get(NotificationTemplateKeys.StaffPasswordResetSms, locale);
        Assert.Contains("{{code}}", template.BodyText, StringComparison.Ordinal);
    }

    [Fact]
    public void Key_IsRegisteredInAll()
    {
        Assert.Contains(NotificationTemplateKeys.StaffPasswordResetSms, NotificationTemplateKeys.All);
        var exception = Record.Exception(() => Provider.EnsureKeysPresent(NotificationTemplateKeys.All));
        Assert.Null(exception);
    }

    [Fact]
    public void RuBody_StaysWithinOneSmsSegment()
    {
        var template = Provider.Get(NotificationTemplateKeys.StaffPasswordResetSms, "ru");
        var rendered = template.BodyText.Replace("{{code}}", "123456", StringComparison.Ordinal);
        Assert.True(rendered.Length <= 70, $"SMS body is {rendered.Length} chars: \"{rendered}\"");
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter StaffPasswordResetSmsTemplateTests`
Expected: FAIL — compile error, `NotificationTemplateKeys.StaffPasswordResetSms` does not exist.

- [ ] **Step 3: Add the key (and register it) + create the three template files atomically**

> **Important:** add the key to `All` **and** create all three files in this one step. A key in `All` with no file makes the startup check `EnsureKeysPresent` throw — which would break every `WebApplicationFactory` test, not just this one.

In `src/AFK4.Platform.Api/Notifications/NotificationTemplateKeys.cs`, after the `StaffPhoneVerification` constant (line 18) add:

```csharp
    /// <summary>SMS password-reset code for a staff/owner account (Phase D).</summary>
    public const string StaffPasswordResetSms = "staff.password_reset_sms";
```

and add `StaffPasswordResetSms` to the `All` collection initializer (line 48):

```csharp
    public static readonly IReadOnlyList<string> All =
        [Test, StaffPasswordReset, StaffPhoneVerification, StaffPasswordResetSms, OwnerInvite, StaffInvite, InvoiceIssued, InvoicePaid, InvoiceOverdue, ShiftDiscrepancy, LowStock, OwnerDailySummary, ScheduledReport];
```

Create `src/AFK4.Platform.Api/Notifications/Templates/ru/staff.password_reset_sms.json`:

```json
{
  "subject": "Сброс пароля AFK4.NET",
  "bodyText": "AFK4.NET: код сброса пароля {{code}}. Никому не сообщайте.",
  "bodyHtml": ""
}
```

Create `src/AFK4.Platform.Api/Notifications/Templates/en/staff.password_reset_sms.json`:

```json
{
  "subject": "AFK4.NET password reset",
  "bodyText": "AFK4.NET: password reset code {{code}}. Do not share it with anyone.",
  "bodyHtml": ""
}
```

Create `src/AFK4.Platform.Api/Notifications/Templates/tg/staff.password_reset_sms.json`:

```json
{
  "subject": "Барқарорсозии пароли AFK4.NET",
  "bodyText": "AFK4.NET: рамзи барқарорсозии парол {{code}}. Ба касе нагӯед.",
  "bodyHtml": ""
}
```

> These files are embedded resources via a wildcard in the `.csproj` (the Phase B `staff.phone_verification.json` files are picked up the same way) — no `.csproj` edit needed. If a build reports them missing as embedded resources, confirm the `Notifications/Templates/**` glob in `AFK4.Platform.Api.csproj` and add the files to it.

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter StaffPasswordResetSmsTemplateTests`
Expected: PASS (3 locale cases + key-registered + ru-segment = 5 assertions; ru body renders to 56 chars).

- [ ] **Step 5: Guard against suite-wide startup breakage**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~Notifications"`
Expected: PASS — all notification tests green, proving `EnsureKeysPresent(All)` still passes with the new key.

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Platform.Api/Notifications/NotificationTemplateKeys.cs src/AFK4.Platform.Api/Notifications/Templates/ru/staff.password_reset_sms.json src/AFK4.Platform.Api/Notifications/Templates/en/staff.password_reset_sms.json src/AFK4.Platform.Api/Notifications/Templates/tg/staff.password_reset_sms.json tests/AFK4.Platform.Api.Tests/Notifications/StaffPasswordResetSmsTemplateTests.cs
git commit -m "feat(notifications): add staff SMS password-reset template"
```

---

### Task 3: Request contracts

**Files:**
- Create: `src/AFK4.Shared.Contracts/Identity/StaffForgotPasswordByPhoneRequest.cs`
- Create: `src/AFK4.Shared.Contracts/Identity/StaffResetPasswordByPhoneRequest.cs`

- [ ] **Step 1: Create the contracts**

Create `src/AFK4.Shared.Contracts/Identity/StaffForgotPasswordByPhoneRequest.cs`:

```csharp
namespace AFK4.Shared.Contracts.Identity;

/// <summary>Requests an SMS password-reset code to a staff account's verified phone.</summary>
public sealed record StaffForgotPasswordByPhoneRequest(string PhoneNumber);
```

Create `src/AFK4.Shared.Contracts/Identity/StaffResetPasswordByPhoneRequest.cs`:

```csharp
namespace AFK4.Shared.Contracts.Identity;

/// <summary>Completes an SMS password reset using the code delivered to the verified phone.</summary>
public sealed record StaffResetPasswordByPhoneRequest(string PhoneNumber, string Code, string NewPassword);
```

- [ ] **Step 2: Build the contracts project**

Run: `dotnet build src/AFK4.Shared.Contracts/AFK4.Shared.Contracts.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/AFK4.Shared.Contracts/Identity/StaffForgotPasswordByPhoneRequest.cs src/AFK4.Shared.Contracts/Identity/StaffResetPasswordByPhoneRequest.cs
git commit -m "feat(contracts): add SMS password-reset request DTOs"
```

---

### Task 4: Reset service interface + result types

**Files:**
- Create: `src/AFK4.Platform.Api/Identity/IStaffPhonePasswordResetService.cs`

- [ ] **Step 1: Create the interface, enums, and result records**

Create `src/AFK4.Platform.Api/Identity/IStaffPhonePasswordResetService.cs`:

```csharp
namespace AFK4.Platform.Api.Identity;

public enum ForgotPasswordByPhoneStatus
{
    /// <summary>Request accepted. Uniform regardless of whether the phone maps to an account (anti-enumeration).</summary>
    Accepted,
    /// <summary>The supplied string is not a normalizable E.164 phone number.</summary>
    InvalidPhone,
}

public sealed record ForgotPasswordByPhoneResult(
    ForgotPasswordByPhoneStatus Status,
    int ExpiresInSeconds,
    int ResendAfterSeconds);

public enum ResetPasswordByPhoneStatus
{
    Success,
    InvalidCode,
    Expired,
    NoActiveCode,
    TooManyAttempts,
}

public sealed record ResetPasswordByPhoneResult(
    ResetPasswordByPhoneStatus Status,
    int RemainingAttempts);

public interface IStaffPhonePasswordResetService
{
    /// <summary>
    /// Sends an SMS reset code to the verified phone if it maps to an active staff account. The
    /// result is uniform whether or not an account exists (anti-enumeration); only a malformed
    /// phone yields <see cref="ForgotPasswordByPhoneStatus.InvalidPhone"/>.
    /// </summary>
    Task<ForgotPasswordByPhoneResult> RequestResetAsync(string rawPhone, CancellationToken cancellationToken);

    /// <summary>
    /// Verifies the SMS code for the phone and, on success, sets the new password and revokes the
    /// account's active tokens. A missing account/code collapses to
    /// <see cref="ResetPasswordByPhoneStatus.NoActiveCode"/> (no enumeration).
    /// </summary>
    Task<ResetPasswordByPhoneResult> ResetAsync(
        string rawPhone, string code, string newPassword, CancellationToken cancellationToken);
}
```

- [ ] **Step 2: Build**

Run: `dotnet build src/AFK4.Platform.Api/AFK4.Platform.Api.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/AFK4.Platform.Api/Identity/IStaffPhonePasswordResetService.cs
git commit -m "feat(identity): add IStaffPhonePasswordResetService contract"
```

---

### Task 5: Reset service implementation + unit tests

Mirror `EfStaffPhoneVerificationService` (OTP create → SMS) and `StaffPhoneVerificationServiceTests` (test doubles, `FixedTimeProvider`).

**Files:**
- Create: `src/AFK4.Platform.Api/Identity/EfStaffPhonePasswordResetService.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Identity/EfStaffPhonePasswordResetServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/AFK4.Platform.Api.Tests/Identity/EfStaffPhonePasswordResetServiceTests.cs`:

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Identity.PhoneOtp;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Tests.Billing;
using AFK4.Shared.Contracts.Notifications;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace AFK4.Platform.Api.Tests.Identity;

public sealed class EfStaffPhonePasswordResetServiceTests
{
    private const string Phone = "992937380070";
    private const string OldPassword = "OldPassw0rd!";
    private const string NewPassword = "NewPassw0rd!";
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-06T12:00:00Z");

    private static PlatformDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static async Task<(Guid StaffUserId, Guid OrgId)> SeedVerifiedStaffAsync(
        PlatformDbContext db, bool verified = true, bool active = true)
    {
        var staffUserId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var staff = new StaffUserEntity
        {
            StaffUserId = staffUserId,
            OrganizationId = orgId,
            UserName = "owner",
            NormalizedUserName = "OWNER",
            DisplayName = "Owner",
            IsActive = active,
            Phone = "+" + Phone,
            NormalizedPhone = Phone,
            PhoneVerifiedAtUtc = verified ? Now.AddDays(-1) : null,
        };
        staff.PasswordHash = new PasswordHasher<StaffUserEntity>().HashPassword(staff, OldPassword);
        db.StaffUsers.Add(staff);
        await db.SaveChangesAsync();
        return (staffUserId, orgId);
    }

    private static (EfStaffPhonePasswordResetService Service, CapturingNotificationService Notifications, FixedTimeProvider Time)
        CreateService(PlatformDbContext db, string fixedCode = "424242")
    {
        var notifications = new CapturingNotificationService();
        var time = new FixedTimeProvider(Now);
        var service = new EfStaffPhonePasswordResetService(
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
    public async Task Request_VerifiedPhone_StoresResetOtp_AndSendsSms()
    {
        await using var db = CreateDb();
        await SeedVerifiedStaffAsync(db);
        var (service, notifications, _) = CreateService(db, fixedCode: "424242");

        var result = await service.RequestResetAsync("+992 93 738-00-70", CancellationToken.None);

        Assert.Equal(ForgotPasswordByPhoneStatus.Accepted, result.Status);
        var otp = Assert.Single(await db.StaffPhoneOtps.ToListAsync());
        Assert.Equal(StaffPhoneOtpPurpose.PasswordReset, otp.Purpose);
        var request = Assert.Single(notifications.SentNow);
        Assert.Equal(NotificationTemplateKeys.StaffPasswordResetSms, request.TemplateKey);
        Assert.Equal("424242", request.Tokens["code"]);
        Assert.Equal("+992937380070", request.Recipient.PhoneNumber);
        Assert.Contains(NotificationChannel.Sms, request.PreferredChannels!);
    }

    [Fact]
    public async Task Request_UnknownPhone_IsAccepted_ButSendsNothing()
    {
        await using var db = CreateDb();
        // no staff seeded
        var (service, notifications, _) = CreateService(db);

        var result = await service.RequestResetAsync(Phone, CancellationToken.None);

        Assert.Equal(ForgotPasswordByPhoneStatus.Accepted, result.Status);
        Assert.Empty(await db.StaffPhoneOtps.ToListAsync());
        Assert.Empty(notifications.SentNow);
    }

    [Fact]
    public async Task Request_UnverifiedPhone_IsAccepted_ButSendsNothing()
    {
        await using var db = CreateDb();
        await SeedVerifiedStaffAsync(db, verified: false);
        var (service, notifications, _) = CreateService(db);

        var result = await service.RequestResetAsync(Phone, CancellationToken.None);

        Assert.Equal(ForgotPasswordByPhoneStatus.Accepted, result.Status);
        Assert.Empty(notifications.SentNow);
    }

    [Fact]
    public async Task Request_InvalidPhoneFormat_ReturnsInvalidPhone()
    {
        await using var db = CreateDb();
        var (service, notifications, _) = CreateService(db);

        var result = await service.RequestResetAsync("12345", CancellationToken.None);

        Assert.Equal(ForgotPasswordByPhoneStatus.InvalidPhone, result.Status);
        Assert.Empty(notifications.SentNow);
    }

    [Fact]
    public async Task Reset_CorrectCode_SetsNewPassword_ConsumesOtp_RevokesTokens()
    {
        await using var db = CreateDb();
        var (staffUserId, orgId) = await SeedVerifiedStaffAsync(db);
        // seed an active access + refresh token to prove revocation
        db.StaffAccessTokens.Add(new StaffAccessTokenEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            StaffUserId = staffUserId,
            TokenHash = "ah",
            IssuedAtUtc = Now.AddHours(-1),
            ExpiresAtUtc = Now.AddHours(7),
        });
        db.StaffRefreshTokens.Add(new StaffRefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            StaffUserId = staffUserId,
            TokenHash = "rh",
            IssuedAtUtc = Now.AddHours(-1),
            ExpiresAtUtc = Now.AddDays(30),
        });
        await db.SaveChangesAsync();
        var (service, _, _) = CreateService(db, fixedCode: "424242");
        await service.RequestResetAsync(Phone, CancellationToken.None);

        var result = await service.ResetAsync(Phone, "424242", NewPassword, CancellationToken.None);

        Assert.Equal(ResetPasswordByPhoneStatus.Success, result.Status);
        var staff = await db.StaffUsers.SingleAsync(u => u.StaffUserId == staffUserId);
        var hasher = new PasswordHasher<StaffUserEntity>();
        Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(staff, staff.PasswordHash, NewPassword));
        Assert.Equal(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(staff, staff.PasswordHash, OldPassword));
        Assert.NotNull((await db.StaffPhoneOtps.SingleAsync()).ConsumedAtUtc);
        Assert.All(await db.StaffAccessTokens.ToListAsync(), t => Assert.NotNull(t.RevokedAtUtc));
        Assert.All(await db.StaffRefreshTokens.ToListAsync(), t => Assert.NotNull(t.RevokedAtUtc));
    }

    [Fact]
    public async Task Reset_WrongCode_IncrementsAttempt_ReturnsRemaining()
    {
        await using var db = CreateDb();
        await SeedVerifiedStaffAsync(db);
        var (service, _, _) = CreateService(db, fixedCode: "424242");
        await service.RequestResetAsync(Phone, CancellationToken.None);

        var result = await service.ResetAsync(Phone, "000000", NewPassword, CancellationToken.None);

        Assert.Equal(ResetPasswordByPhoneStatus.InvalidCode, result.Status);
        Assert.Equal(2, result.RemainingAttempts);
        Assert.Equal(1, (await db.StaffPhoneOtps.SingleAsync()).AttemptCount);
    }

    [Fact]
    public async Task Reset_TooManyWrongAttempts_LocksOut()
    {
        await using var db = CreateDb();
        await SeedVerifiedStaffAsync(db);
        var (service, _, _) = CreateService(db, fixedCode: "424242");
        await service.RequestResetAsync(Phone, CancellationToken.None);

        await service.ResetAsync(Phone, "000000", NewPassword, CancellationToken.None);
        await service.ResetAsync(Phone, "000000", NewPassword, CancellationToken.None);
        await service.ResetAsync(Phone, "000000", NewPassword, CancellationToken.None);
        var afterLockout = await service.ResetAsync(Phone, "424242", NewPassword, CancellationToken.None);

        Assert.Equal(ResetPasswordByPhoneStatus.TooManyAttempts, afterLockout.Status);
    }

    [Fact]
    public async Task Reset_ExpiredCode_ReturnsExpired()
    {
        await using var db = CreateDb();
        await SeedVerifiedStaffAsync(db);
        var (service, _, time) = CreateService(db, fixedCode: "424242");
        await service.RequestResetAsync(Phone, CancellationToken.None);
        time.Now = Now.AddMinutes(10);

        var result = await service.ResetAsync(Phone, "424242", NewPassword, CancellationToken.None);

        Assert.Equal(ResetPasswordByPhoneStatus.Expired, result.Status);
    }

    [Fact]
    public async Task Reset_NoCodeRequested_ReturnsNoActiveCode()
    {
        await using var db = CreateDb();
        await SeedVerifiedStaffAsync(db);
        var (service, _, _) = CreateService(db, fixedCode: "424242");

        var result = await service.ResetAsync(Phone, "424242", NewPassword, CancellationToken.None);

        Assert.Equal(ResetPasswordByPhoneStatus.NoActiveCode, result.Status);
    }

    // ---- test doubles (mirror StaffPhoneVerificationServiceTests) ----

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

> **Step 1 note — verify the token entity property names before running.** This test seeds `StaffAccessTokenEntity` / `StaffRefreshTokenEntity` directly. Open `src/AFK4.Platform.Api/Data/` (the `StaffAccessTokenEntity` / `StaffRefreshTokenEntity` classes) and match the exact property names/required fields (the query in `StaffTokenRevocation` only relies on `OrganizationId`, `StaffUserId`, `RevokedAtUtc`). Adjust the seed object initializers to whatever the entities actually require (e.g. the PK property name may be `StaffAccessTokenId` rather than `Id`, and there may be other non-nullable columns). The assertions only read `RevokedAtUtc`.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter EfStaffPhonePasswordResetServiceTests`
Expected: FAIL — compile error, `EfStaffPhonePasswordResetService` does not exist.

- [ ] **Step 3: Implement the service**

Create `src/AFK4.Platform.Api/Identity/EfStaffPhonePasswordResetService.cs`:

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity.PhoneOtp;
using AFK4.Platform.Api.Notifications;
using AFK4.Shared.Contracts.Notifications;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Identity;

/// <summary>
/// SMS password reset. Mirrors <see cref="EfStaffPhoneVerificationService"/>: a 6-digit OTP
/// (<see cref="StaffPhoneOtpPurpose.PasswordReset"/>) is sent to a verified phone, then verified to
/// set a new password. Resolves staff exactly as sign-in-by-phone (verified + active). Reuses
/// <see cref="StaffTokenRevocation"/> so a completed reset logs the account out everywhere.
/// </summary>
public sealed class EfStaffPhonePasswordResetService(
    PlatformDbContext db,
    INotificationService notifications,
    IPhoneOtpHasher hasher,
    IPhoneOtpGenerator generator,
    TimeProvider timeProvider,
    IOptions<PhoneOtpOptions> otpOptions,
    IOptions<NotificationOptions> notificationOptions) : IStaffPhonePasswordResetService
{
    private readonly PhoneOtpOptions otpOptions = otpOptions.Value;
    private readonly NotificationOptions notificationOptions = notificationOptions.Value;
    private readonly PasswordHasher<StaffUserEntity> passwordHasher = new();

    public async Task<ForgotPasswordByPhoneResult> RequestResetAsync(string rawPhone, CancellationToken cancellationToken)
    {
        var expiresInSeconds = (int)otpOptions.Lifetime.TotalSeconds;
        var resendAfterSeconds = (int)otpOptions.ResendCooldown.TotalSeconds;

        var normalizedPhone = PhoneNumberNormalizer.Normalize(rawPhone);
        if (normalizedPhone is null)
        {
            return new ForgotPasswordByPhoneResult(ForgotPasswordByPhoneStatus.InvalidPhone, 0, 0);
        }

        // Anti-enumeration: this exact result is returned whether or not an account exists, and
        // whether or not an SMS is actually sent (cooldown / hourly cap suppress the send silently).
        var accepted = new ForgotPasswordByPhoneResult(
            ForgotPasswordByPhoneStatus.Accepted, expiresInSeconds, resendAfterSeconds);

        var staff = await db.StaffUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                user => user.NormalizedPhone == normalizedPhone
                    && user.PhoneVerifiedAtUtc != null
                    && user.IsActive,
                cancellationToken);
        if (staff is null)
        {
            return accepted;
        }

        var now = timeProvider.GetUtcNow();

        var recent = await db.StaffPhoneOtps
            .Where(otp => otp.StaffUserId == staff.StaffUserId && otp.Purpose == StaffPhoneOtpPurpose.PasswordReset)
            .OrderByDescending(otp => otp.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (recent is not null && now - recent.CreatedAtUtc < otpOptions.ResendCooldown)
        {
            return accepted;
        }

        var sinceHourAgo = now - TimeSpan.FromHours(1);
        var sendsLastHour = await db.StaffPhoneOtps.CountAsync(
            otp => otp.StaffUserId == staff.StaffUserId
                && otp.Purpose == StaffPhoneOtpPurpose.PasswordReset
                && otp.CreatedAtUtc > sinceHourAgo,
            cancellationToken);
        if (sendsLastHour >= otpOptions.MaxSendsPerHour)
        {
            return accepted;
        }

        var code = generator.Generate();
        var otpId = Guid.NewGuid();
        db.StaffPhoneOtps.Add(new StaffPhoneOtpEntity
        {
            StaffPhoneOtpId = otpId,
            StaffUserId = staff.StaffUserId,
            OrganizationId = staff.OrganizationId,
            Phone = normalizedPhone,
            Purpose = StaffPhoneOtpPurpose.PasswordReset,
            CodeHash = hasher.Hash(code),
            CreatedAtUtc = now,
            ExpiresAtUtc = now + otpOptions.Lifetime,
            AttemptCount = 0,
        });
        await db.SaveChangesAsync(cancellationToken);

        var request = new NotificationRequest(
            TemplateKey: NotificationTemplateKeys.StaffPasswordResetSms,
            Category: NotificationCategory.Transactional,
            Recipient: new NotificationRecipient(
                Locale: notificationOptions.DefaultLocale,
                PhoneNumber: "+" + normalizedPhone,
                StaffUserId: staff.StaffUserId),
            Tokens: new Dictionary<string, string>
            {
                ["code"] = code,
                ["expiresInMinutes"] = ((int)otpOptions.Lifetime.TotalMinutes).ToString(),
            },
            IdempotencyKey: $"staff-password-reset-sms:{otpId:N}",
            PreferredChannels: [NotificationChannel.Sms],
            OrganizationId: staff.OrganizationId);

        await notifications.SendNowAsync(request, cancellationToken);
        return accepted;
    }

    public async Task<ResetPasswordByPhoneResult> ResetAsync(
        string rawPhone, string code, string newPassword, CancellationToken cancellationToken)
    {
        var normalizedPhone = PhoneNumberNormalizer.Normalize(rawPhone);
        if (normalizedPhone is null)
        {
            return new ResetPasswordByPhoneResult(ResetPasswordByPhoneStatus.NoActiveCode, 0);
        }

        var now = timeProvider.GetUtcNow();

        var staff = await db.StaffUsers
            .FirstOrDefaultAsync(
                user => user.NormalizedPhone == normalizedPhone
                    && user.PhoneVerifiedAtUtc != null
                    && user.IsActive,
                cancellationToken);
        if (staff is null)
        {
            // Anti-enumeration: behave as if there were simply no pending code.
            return new ResetPasswordByPhoneResult(ResetPasswordByPhoneStatus.NoActiveCode, 0);
        }

        var otp = await db.StaffPhoneOtps
            .Where(candidate => candidate.StaffUserId == staff.StaffUserId
                && candidate.Purpose == StaffPhoneOtpPurpose.PasswordReset
                && candidate.ConsumedAtUtc == null)
            .OrderByDescending(candidate => candidate.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (otp is null)
        {
            return new ResetPasswordByPhoneResult(ResetPasswordByPhoneStatus.NoActiveCode, 0);
        }

        if (otp.ExpiresAtUtc <= now)
        {
            return new ResetPasswordByPhoneResult(ResetPasswordByPhoneStatus.Expired, 0);
        }

        if (otp.AttemptCount >= otpOptions.MaxAttempts)
        {
            return new ResetPasswordByPhoneResult(ResetPasswordByPhoneStatus.TooManyAttempts, 0);
        }

        var enteredDigits = PhoneOtpCode.KeepDigits(code);
        if (hasher.Hash(enteredDigits) != otp.CodeHash)
        {
            otp.AttemptCount++;
            await db.SaveChangesAsync(cancellationToken);
            var remaining = Math.Max(0, otpOptions.MaxAttempts - otp.AttemptCount);
            return new ResetPasswordByPhoneResult(ResetPasswordByPhoneStatus.InvalidCode, remaining);
        }

        otp.ConsumedAtUtc = now;
        staff.PasswordHash = passwordHasher.HashPassword(staff, newPassword);
        await StaffTokenRevocation.RevokeActiveAsync(db, staff.OrganizationId, staff.StaffUserId, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return new ResetPasswordByPhoneResult(ResetPasswordByPhoneStatus.Success, otpOptions.MaxAttempts);
    }
}
```

> `PhoneOtpCode.KeepDigits` is the `internal static` helper already defined at the bottom of `EfStaffPhoneVerificationService.cs` (same `AFK4.Platform.Api.Identity` namespace) — reused here, not redefined.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter EfStaffPhonePasswordResetServiceTests`
Expected: PASS (8 cases).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Api/Identity/EfStaffPhonePasswordResetService.cs tests/AFK4.Platform.Api.Tests/Identity/EfStaffPhonePasswordResetServiceTests.cs
git commit -m "feat(identity): add SMS password-reset service"
```

---

### Task 6: DI registration + `staff-reset` rate-limiter policy

**Files:**
- Modify: `src/AFK4.Platform.Api/Program.cs:247` (DI) and `:352` (rate limiter)

- [ ] **Step 1: Register the service**

In `src/AFK4.Platform.Api/Program.cs`, immediately AFTER the line:

```csharp
builder.Services.AddScoped<IStaffPhoneVerificationService, EfStaffPhoneVerificationService>();
```

add:

```csharp
builder.Services.AddScoped<IStaffPhonePasswordResetService, EfStaffPhonePasswordResetService>();
```

- [ ] **Step 2: Add the rate-limiter policy**

In the `builder.Services.AddRateLimiter(options => { ... })` block, after the `"player-me"` policy (it ends at line 352), add:

```csharp
    options.AddPolicy("staff-reset", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1)
            }));
```

(`IStaffPhonePasswordResetService`/`EfStaffPhonePasswordResetService` resolve from `AFK4.Platform.Api.Identity`, already imported in Program.cs for the verification service; `RateLimitPartition`/`FixedWindowRateLimiterOptions` are already used by the `player-public` policy.)

- [ ] **Step 3: Build**

Run: `dotnet build src/AFK4.Platform.Api/AFK4.Platform.Api.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/AFK4.Platform.Api/Program.cs
git commit -m "feat(identity): register SMS password-reset service and staff-reset rate limit"
```

---

### Task 7: Public endpoints + endpoint tests

**Files:**
- Modify: `src/AFK4.Platform.Api/Endpoints/AuthEndpoints.cs` (add two endpoints inside `MapAuthEndpoints`, after the `/api/auth/staff/phone` GET handler, before the closing brace ~line 254)
- Test: `tests/AFK4.Platform.Api.Tests/StaffPasswordResetByPhoneEndpointTests.cs`

- [ ] **Step 1: Write the failing endpoint tests**

Create `tests/AFK4.Platform.Api.Tests/StaffPasswordResetByPhoneEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using AFK4.Platform.Api.Notifications;
using AFK4.Shared.Contracts.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using AFK4.Platform.Api.Data;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public sealed class StaffPasswordResetByPhoneEndpointTests
{
    private const string Phone = "992937380070";
    private const string OldPassword = "OldPassw0rd!";
    private const string NewPassword = "NewPassw0rd!";

    private sealed class RecordingSmsTransport : ISmsTransport
    {
        public List<SmsMessage> Sent { get; } = [];

        public Task SendAsync(SmsMessage message, CancellationToken cancellationToken)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    private static PlatformApiFactory CreateFactory(RecordingSmsTransport recording) =>
        new(extraServices: services =>
        {
            services.RemoveAll<ISmsTransport>();
            services.AddSingleton<ISmsTransport>(recording);
        });

    private static async Task SeedVerifiedStaffAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var staff = new StaffUserEntity
        {
            StaffUserId = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            UserName = "u" + Phone,
            NormalizedUserName = "U" + Phone,
            DisplayName = "Phone Staff",
            IsActive = true,
            Phone = "+" + Phone,
            NormalizedPhone = Phone,
            PhoneVerifiedAtUtc = DateTimeOffset.Parse("2026-06-01T00:00:00Z"),
            CreatedAtUtc = DateTimeOffset.Parse("2026-05-01T00:00:00Z"),
        };
        staff.PasswordHash = new PasswordHasher<StaffUserEntity>().HashPassword(staff, OldPassword);
        db.StaffUsers.Add(staff);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Forgot_VerifiedPhone_ReturnsOk_AndSendsSms()
    {
        var recording = new RecordingSmsTransport();
        await using var factory = CreateFactory(recording);
        await SeedVerifiedStaffAsync(factory);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/staff/forgot-password-by-phone",
            new StaffForgotPasswordByPhoneRequest("+992 93 738-00-70"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sms = Assert.Single(recording.Sent);
        Assert.Equal("+992937380070", sms.ToPhoneNumber);
        Assert.Matches("\\d{6}", sms.Text);
    }

    [Fact]
    public async Task Forgot_UnknownPhone_ReturnsOk_ButSendsNothing()
    {
        var recording = new RecordingSmsTransport();
        await using var factory = CreateFactory(recording);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/staff/forgot-password-by-phone",
            new StaffForgotPasswordByPhoneRequest("992000000000"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(recording.Sent);
    }

    [Fact]
    public async Task Forgot_InvalidPhone_ReturnsBadRequest()
    {
        var recording = new RecordingSmsTransport();
        await using var factory = CreateFactory(recording);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/staff/forgot-password-by-phone",
            new StaffForgotPasswordByPhoneRequest("12345"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reset_WrongCode_ReturnsBadRequest()
    {
        var recording = new RecordingSmsTransport();
        await using var factory = CreateFactory(recording);
        await SeedVerifiedStaffAsync(factory);
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync(
            "/api/auth/staff/forgot-password-by-phone",
            new StaffForgotPasswordByPhoneRequest(Phone));

        var response = await client.PostAsJsonAsync(
            "/api/auth/staff/reset-password-by-phone",
            new StaffResetPasswordByPhoneRequest(Phone, "000000", NewPassword));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reset_WeakPassword_ReturnsBadRequest()
    {
        var recording = new RecordingSmsTransport();
        await using var factory = CreateFactory(recording);
        await SeedVerifiedStaffAsync(factory);
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync(
            "/api/auth/staff/forgot-password-by-phone",
            new StaffForgotPasswordByPhoneRequest(Phone));
        var code = Regex.Match(Assert.Single(recording.Sent).Text, "\\d{6}").Value;

        var response = await client.PostAsJsonAsync(
            "/api/auth/staff/reset-password-by-phone",
            new StaffResetPasswordByPhoneRequest(Phone, code, "short"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reset_CorrectCode_ChangesPassword_NewPasswordSignsIn_OldFails()
    {
        var recording = new RecordingSmsTransport();
        await using var factory = CreateFactory(recording);
        await SeedVerifiedStaffAsync(factory);
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync(
            "/api/auth/staff/forgot-password-by-phone",
            new StaffForgotPasswordByPhoneRequest(Phone));
        var code = Regex.Match(Assert.Single(recording.Sent).Text, "\\d{6}").Value;

        var reset = await client.PostAsJsonAsync(
            "/api/auth/staff/reset-password-by-phone",
            new StaffResetPasswordByPhoneRequest(Phone, code, NewPassword));
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        var withNew = await client.PostAsJsonAsync(
            "/api/auth/staff/sign-in-by-phone",
            new StaffSignInByPhoneRequest(Phone, NewPassword));
        Assert.Equal(HttpStatusCode.OK, withNew.StatusCode);

        var withOld = await client.PostAsJsonAsync(
            "/api/auth/staff/sign-in-by-phone",
            new StaffSignInByPhoneRequest(Phone, OldPassword));
        Assert.Equal(HttpStatusCode.Unauthorized, withOld.StatusCode);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter StaffPasswordResetByPhoneEndpointTests`
Expected: FAIL — endpoints return 404 (not mapped yet), so the status-code asserts fail.

- [ ] **Step 3: Add the endpoints**

In `src/AFK4.Platform.Api/Endpoints/AuthEndpoints.cs`, inside `MapAuthEndpoints`, after the `app.MapGet("/api/auth/staff/phone", ...)` handler block and before the method's closing brace (~line 254), add:

```csharp
        app.MapPost("/api/auth/staff/forgot-password-by-phone", async (
            StaffForgotPasswordByPhoneRequest request,
            IStaffPhonePasswordResetService resetService,
            CancellationToken cancellationToken) =>
        {
            var result = await resetService.RequestResetAsync(request.PhoneNumber, cancellationToken);
            return result.Status switch
            {
                ForgotPasswordByPhoneStatus.Accepted => Results.Ok(
                    new { expiresInSeconds = result.ExpiresInSeconds, resendAfterSeconds = result.ResendAfterSeconds }),
                ForgotPasswordByPhoneStatus.InvalidPhone => Results.BadRequest(new { error = "invalid_phone" }),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError),
            };
        }).RequireRateLimiting("staff-reset");

        app.MapPost("/api/auth/staff/reset-password-by-phone", async (
            StaffResetPasswordByPhoneRequest request,
            IStaffPhonePasswordResetService resetService,
            CancellationToken cancellationToken) =>
        {
            var passwordValidation = ValidateStaffPassword(request.NewPassword);
            if (passwordValidation is not null)
            {
                return Results.BadRequest(new { error = passwordValidation });
            }

            var result = await resetService.ResetAsync(
                request.PhoneNumber, request.Code, request.NewPassword, cancellationToken);
            return result.Status switch
            {
                ResetPasswordByPhoneStatus.Success => Results.Ok(new { message = "Password updated." }),
                ResetPasswordByPhoneStatus.InvalidCode => Results.Json(
                    new { error = "invalid_code", remainingAttempts = result.RemainingAttempts },
                    statusCode: StatusCodes.Status400BadRequest),
                ResetPasswordByPhoneStatus.Expired => Results.Json(
                    new { error = "code_expired" }, statusCode: StatusCodes.Status410Gone),
                ResetPasswordByPhoneStatus.NoActiveCode => Results.Json(
                    new { error = "code_expired" }, statusCode: StatusCodes.Status410Gone),
                ResetPasswordByPhoneStatus.TooManyAttempts => Results.Json(
                    new { error = "too_many_attempts" }, statusCode: StatusCodes.Status429TooManyRequests),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError),
            };
        }).RequireRateLimiting("staff-reset");
```

(`ValidateStaffPassword` comes from `using static AFK4.Platform.Api.Endpoints.EndpointHelpers;` already at the top of the file; `StaffForgotPasswordByPhoneRequest`/`StaffResetPasswordByPhoneRequest` from `AFK4.Shared.Contracts.Identity` and the status enums from `AFK4.Platform.Api.Identity` are both already imported.)

- [ ] **Step 4: Run the endpoint tests to verify they pass**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter StaffPasswordResetByPhoneEndpointTests`
Expected: PASS (6 cases, including the end-to-end new-password-signs-in / old-fails).

- [ ] **Step 5: Run the full backend suite**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj`
Expected: PASS — baseline (1055/1055) plus the new template, service, and endpoint tests; nothing regressed.

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Platform.Api/Endpoints/AuthEndpoints.cs tests/AFK4.Platform.Api.Tests/StaffPasswordResetByPhoneEndpointTests.cs
git commit -m "feat(identity): add SMS password-reset endpoints"
```

---

## Self-Review

**1. Spec coverage (against `2026-06-06-phase-d-sms-password-reset-design.md`):**
- §3.1 SMS template `staff.password_reset_sms` (key + 3 files, Cyrillic ≤65) → Task 2 ✓
- §3.2 `EfStaffPhonePasswordResetService` (RequestReset anti-enumeration + cooldown/cap suppress; Reset TTL/attempts/single-use + set password + revoke) → Tasks 4, 5 ✓
- §3.3 reuse revocation core via `StaffTokenRevocation` → Task 1 ✓
- §3.4 contracts → Task 3 ✓
- §3.5 public endpoints, status mapping, `ValidateStaffPassword`, rate limit → Tasks 6, 7 ✓
- §3.6 DI registration + `staff-reset` policy → Task 6 ✓
- §4 security: hashed OTP (reuses Phase B infra), verified+active only, uniform response, token revoke, IP rate limit → Tasks 5, 6, 7 ✓
- §5 edge cases: phone formats (normalizer), unverified→no-account, expired/consumed → covered by service tests (Task 5) ✓
- §6 testing: unit (request/reset paths, expiry, attempts), template render, end-to-end sign-in → Tasks 2, 5, 7 ✓
- §7 out of scope: no UI, no email changes — plan touches only backend + the behavior-preserving `StaffTokenRevocation` extraction ✓

**2. Placeholder scan:** No TBD/TODO. Every code step has complete code; every run step has an exact command + expected result. Two "verify before running" notes (token entity property names in Task 5; embedded-resource glob in Task 2) point at real files to confirm, not at missing plan content.

**3. Type consistency:** `IStaffPhonePasswordResetService.RequestResetAsync(string, ct)` / `ResetAsync(string, string, string, ct)`; `ForgotPasswordByPhoneStatus{Accepted,InvalidPhone}` + `ForgotPasswordByPhoneResult(Status,ExpiresInSeconds,ResendAfterSeconds)`; `ResetPasswordByPhoneStatus{Success,InvalidCode,Expired,NoActiveCode,TooManyAttempts}` + `ResetPasswordByPhoneResult(Status,RemainingAttempts)`; `StaffTokenRevocation.RevokeActiveAsync(db,orgId,staffUserId,now,ct)`; contracts `StaffForgotPasswordByPhoneRequest(PhoneNumber)` / `StaffResetPasswordByPhoneRequest(PhoneNumber,Code,NewPassword)`; template key `StaffPasswordResetSms = "staff.password_reset_sms"` — all identical across service, interface, endpoints, and tests. Reused existing members verified in code: `PhoneNumberNormalizer.Normalize`, `PhoneOtpCode.KeepDigits`, `PhoneOtpOptions{Lifetime,MaxAttempts,ResendCooldown,MaxSendsPerHour}`, `StaffPhoneOtpEntity{StaffPhoneOtpId,StaffUserId,OrganizationId,Phone,Purpose,CodeHash,CreatedAtUtc,ExpiresAtUtc,AttemptCount,ConsumedAtUtc}`, `StaffPhoneOtpPurpose.PasswordReset`, `NotificationRequest`/`NotificationRecipient`/`NotificationCategory.Transactional`/`NotificationChannel.Sms`, `ValidateStaffPassword`.
