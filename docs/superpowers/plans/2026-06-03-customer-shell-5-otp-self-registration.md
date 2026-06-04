# Customer Shell — Unit 5: OTP Self-Registration

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a player self-register and verify their own phone via SMS one-time-password (OTP), then set their own PIN — removing the current "operator must set the PIN" bottleneck (`POST /api/branches/{branchId}/players/{playerId}/pin`, perm `players.create`). A player on the public web/PWA enters their phone, receives an SMS code, types it back, and on success sets a PIN and is immediately signed in. Today `PhoneVerified` is only ever set `true` by seeding; this unit introduces the real flow.

**Architecture:** Three layers, all Linux-testable behind a fake SMS sender.

- **SMS seam (`ISmsSender`):** the platform never talks to the gateway directly. `ISmsSender.SendAsync(e164Phone, text, ct) → SmsSendResult` is the single port. Two adapters: `FakeSmsSender` (an in-memory capture used in every test and registered by the test factory) and `HttpSmsSender` (the production adapter; its wire-format request-shaping is a single small swappable method, filled in from the gateway contract in the **last task**). This mirrors the existing notification seam `INotificationChannel` (delivery hidden behind one interface, concrete channels registered later "with no caller change") but stays focused on SMS — `ISmsSender` is intentionally smaller than a full notification channel because OTP send is a single synchronous side-effect, not a templated outbox row.
- **OTP service (`PhoneVerificationService`):** owns code generation, hashing, storage, expiry, attempt-counting, lockout, and minting the short-lived verification token. It never stores or returns the raw code. It is anti-enumeration: `StartAsync` always succeeds (HTTP 200) whether or not the phone is known, and only actually sends an SMS — the response reveals nothing. Mirrors the lockout shape already proven in `PlayerCredentialService` (`MaxFailedAttempts = 5`, `LockoutDuration = 15min`, `TimeProvider`-driven, EF `SingleOrDefaultAsync`).
- **Public endpoints:** three new `POST /api/public/player/registration/*` endpoints, all `.RequireRateLimiting("player-public")` (10/min per IP, same policy `/api/public/player/sign-in` already uses). `complete` reuses `OpaquePlayerTokenService.IssueAsync` to return a real `PlayerSignInResponse` and `PlayerCredentialService.SetPasswordAsync` to set the PIN, so a freshly-registered player is signed in exactly like one who signed in normally.

**Tech Stack:** .NET 10 / C# minimal-API (`src/AFK4.Platform.Api/Program.cs` top-level statements). EF Core 10 with `PlatformDbContext` (Npgsql in prod; **EF InMemory** in tests via `PlatformApiFactory`). Migrations live under `src/AFK4.Platform.Api/Data/Migrations` and are generated with `dotnet ef` (10.x). Password/PIN hashing via `PasswordHasher<T>` (ASP.NET Core Identity). Tests: xUnit in `tests/AFK4.Platform.Api.Tests/` (~936 passing), `WebApplicationFactory<Program>` + EF InMemory, helper style mirrored from `PlayerAuthenticationEndpointTests.cs` (`SeedPlayerWithPinAsync`, `CreateClient`, `PostAsJsonAsync`). Contracts (request/response records) live in `src/AFK4.Shared.Contracts/Players/`.

**Money/units:** none — this unit is auth-only.

**Test command (Linux):**
```
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~<TestClass>"
```

---

## Dependencies / external

- **SMS gateway contract — PENDING (the only external blocker).** The user confirmed an SMS gateway **exists**, but its exact contract (base URL, auth scheme, send-SMS request/response JSON shape) is **not yet known** and will be provided at execution time. This plan therefore **designs against a seam**: every test and every shippable behaviour runs against `FakeSmsSender`. Only **Task 8** — implementing `HttpSmsSender`'s real wire-format — waits on the gateway details. Tasks 1–7 are fully self-contained and Linux-testable without it. When Task 8 lands, the OTP flow flips from fake to real with zero changes to the service or endpoints.
- **Self-contained (no external blocker):** OTP storage, the verification service, the three public endpoints, the short-lived verification token, lockout/expiry, and all anti-enumeration behaviour. Everything except Task 8 ships and is verified on Linux today.
- **Operational follow-up (not code, collect at execution):** gateway base URL + API key go into AFK4 secrets/config under the `Sms` section (`SmsOptions.BaseUrl`, `SmsOptions.ApiKey`).

**Ground truth verified 2026-06-03 (reuse, do not rebuild):**
- `PlayerAccountEntity` (`src/AFK4.Platform.Api/Data/PlayerAccountEntity.cs`): `PlayerAccountId, OrganizationId, HomeBranchId, DisplayName, PhoneNumber?, Email?, PreferredLocale?, MarketingOptIn, IsActive, PostpaidCreditLimitMinorUnits?, CreatedAtUtc`.
- `PlayerCredentialEntity` (`.../Data/PlayerCredentialEntity.cs`): `PlayerCredentialId, PlayerAccountId, OrganizationId, PasswordHash?, PhoneVerified, PhoneVerifiedAtUtc?, FailedLoginCount, LockedUntilUtc?, CreatedAtUtc, UpdatedAtUtc`.
- `PlayerCredentialService.SetPasswordAsync(playerAccountId, password, ct)` (`.../Identity/PlayerCredentialService.cs`) hashes the PIN with `PasswordHasher<PlayerCredentialEntity>` and **creates the credential row if absent**.
- `OpaquePlayerTokenService.IssueAsync(account, phoneVerified, ct) → PlayerSignInResponse` (`.../Identity/OpaquePlayerTokenService.cs`) mints the access/refresh token pair. Registered as `IPlayerTokenService` (`Program.cs:164`).
- `IPlayerCredentialService`/`IPlayerTokenService`/`IPlayerContextAccessor` DI at `Program.cs:164–166`.
- Public anti-enumeration sign-in: `POST /api/public/player/sign-in` (`Program.cs:647`), `.RequireRateLimiting("player-public")`. Policy `player-public` = fixed window 10/min per remote IP (`Program.cs:269`).
- Operator-set PIN endpoint: `Program.cs:6971` calls `SetPasswordAsync`; perm `players.create`. (We **leave it in place** — it is the fallback until the shell adopts OTP. Removing it is out of scope.)
- DbContext: `PlatformDbContext` (`.../Data/PlatformDbContext.cs`); DbSets at lines 45–51; entity config for players at 412–448; `payment_intents` config at 768. New entities follow the same `ToTable("snake_case")` + `HasKey` + `HasMaxLength` + `HasIndex` convention.
- Migration style: plain `MigrationBuilder.CreateTable` with `nullable false/true`, `uuid`/`character varying(N)`/`boolean`/`integer`/`timestamp with time zone` column types (see `20260603062941_AddPlayerCredentials.cs`, `20260603094045_AddPaymentIntents.cs`).
- Test factory `PlatformApiFactory` (`tests/AFK4.Platform.Api.Tests/PlatformApiFactory.cs`) swaps the DbContext to InMemory and lets tests override DI via `ConfigureWebHost` — it is the place to register `FakeSmsSender`.

---

## File Structure

**New files:**
- `src/AFK4.Platform.Api/Sms/ISmsSender.cs` — port + `SmsSendResult`.
- `src/AFK4.Platform.Api/Sms/SmsOptions.cs` — `BaseUrl`, `ApiKey`, `SectionName = "Sms"`.
- `src/AFK4.Platform.Api/Sms/HttpSmsSender.cs` — production adapter (wire-format `BuildRequest` filled in Task 8).
- `src/AFK4.Platform.Api/Data/PhoneVerificationEntity.cs` — OTP storage row.
- `src/AFK4.Platform.Api/Identity/IPhoneVerificationService.cs` — service port.
- `src/AFK4.Platform.Api/Identity/PhoneVerificationService.cs` — service impl.
- `src/AFK4.Platform.Api/Data/Migrations/<timestamp>_AddPhoneVerifications.cs` (+ `.Designer.cs`, snapshot updated) — generated by `dotnet ef`.
- `src/AFK4.Shared.Contracts/Players/RegistrationStartRequest.cs`
- `src/AFK4.Shared.Contracts/Players/RegistrationVerifyRequest.cs`
- `src/AFK4.Shared.Contracts/Players/RegistrationVerifyResponse.cs`
- `src/AFK4.Shared.Contracts/Players/RegistrationCompleteRequest.cs`
- `tests/AFK4.Platform.Api.Tests/FakeSmsSender.cs` — in-memory capture test double.
- `tests/AFK4.Platform.Api.Tests/PhoneVerificationServiceTests.cs` — service-level tests.
- `tests/AFK4.Platform.Api.Tests/RegistrationEndpointTests.cs` — endpoint (happy path + negatives).
- `tests/AFK4.Platform.Api.Tests/HttpSmsSenderTests.cs` — wire-shape test (Task 8).

**Modified files:**
- `src/AFK4.Platform.Api/Data/PlatformDbContext.cs` — add `DbSet<PhoneVerificationEntity>` + entity config.
- `src/AFK4.Platform.Api/Program.cs` — DI (`SmsOptions`, `ISmsSender`, `IPhoneVerificationService`, typed `HttpClient`) + three `registration/*` endpoints.
- `tests/AFK4.Platform.Api.Tests/PlatformApiFactory.cs` — register `FakeSmsSender` as `ISmsSender` (singleton, so tests can read captures).

**Conventions to mirror (verified):**
- Contracts are `public sealed record` in `AFK4.Shared.Contracts.Players` (see `PlayerSignInRequest.cs`).
- Services take ctor-injected `PlatformDbContext`, `TimeProvider`, and collaborators (see `PlayerCredentialService`), use `SingleOrDefaultAsync`, mutate + `SaveChangesAsync`.
- Endpoints: `app.MapPost("/api/public/player/...", async (Request request, IService svc, CancellationToken ct) => {...}).RequireRateLimiting("player-public");` (see `Program.cs:647`).
- Tests: `await using var factory = new PlatformApiFactory();` then either `factory.Services.CreateAsyncScope()` for service-level, or `factory.CreateClient()` + `PostAsJsonAsync` for endpoint-level (see `PlayerAuthenticationEndpointTests.cs`).

---

## Task 1 — `ISmsSender` seam + `FakeSmsSender` + DI

**Files:**
- Create: `src/AFK4.Platform.Api/Sms/ISmsSender.cs`
- Create: `src/AFK4.Platform.Api/Sms/SmsOptions.cs`
- Create: `tests/AFK4.Platform.Api.Tests/FakeSmsSender.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs` (register `FakeSmsSender`? **no** — prod uses Http; the fake is registered only in tests)
- Modify: `tests/AFK4.Platform.Api.Tests/PlatformApiFactory.cs`
- Test: `tests/AFK4.Platform.Api.Tests/PhoneVerificationServiceTests.cs` (first assertion only — the fake captures)

- [ ] **Step 1: Write the failing test.** Create `tests/AFK4.Platform.Api.Tests/FakeSmsSender.cs` *as the test double* and a first test in `PhoneVerificationServiceTests.cs` that proves the seam is wired into DI as `ISmsSender` and is a capture:
  ```csharp
  using AFK4.Platform.Api.Sms;
  using Microsoft.Extensions.DependencyInjection;

  namespace AFK4.Platform.Api.Tests;

  public sealed class PhoneVerificationServiceTests
  {
      [Fact]
      public async Task FakeSmsSender_IsRegisteredAsTheSmsSeam_AndCaptures()
      {
          await using var factory = new PlatformApiFactory();
          await using var scope = factory.Services.CreateAsyncScope();
          var sender = scope.ServiceProvider.GetRequiredService<ISmsSender>();
          Assert.IsType<FakeSmsSender>(sender);

          var result = await sender.SendAsync("+992900000001", "code 1234", default);
          Assert.True(result.Delivered);
          var captured = Assert.Single(((FakeSmsSender)sender).Sent);
          Assert.Equal("+992900000001", captured.E164Phone);
          Assert.Contains("1234", captured.Text);
      }
  }
  ```
  And the capture double:
  ```csharp
  using System.Collections.Concurrent;
  using AFK4.Platform.Api.Sms;

  namespace AFK4.Platform.Api.Tests;

  public sealed class FakeSmsSender : ISmsSender
  {
      public readonly record struct SentSms(string E164Phone, string Text);
      private readonly ConcurrentQueue<SentSms> sent = new();
      public IReadOnlyList<SentSms> Sent => sent.ToArray();

      public Task<SmsSendResult> SendAsync(string e164Phone, string text, CancellationToken ct)
      {
          sent.Enqueue(new SentSms(e164Phone, text));
          return Task.FromResult(SmsSendResult.Ok("fake-" + Guid.NewGuid().ToString("N")));
      }
  }
  ```

- [ ] **Step 2: Run — expect FAIL** (`ISmsSender`/`SmsSendResult` and the DI registration do not exist):
  ```
  dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~PhoneVerificationServiceTests.FakeSmsSender_IsRegisteredAsTheSmsSeam_AndCaptures"
  ```
  Expected: compile error — type `ISmsSender` not found.

- [ ] **Step 3: Minimal impl — the port + options.** Create `src/AFK4.Platform.Api/Sms/ISmsSender.cs`:
  ```csharp
  namespace AFK4.Platform.Api.Sms;

  /// <summary>
  /// Single port for sending an SMS. Production talks to the gateway via <c>HttpSmsSender</c>;
  /// tests capture with a fake. OTP self-registration depends only on this seam, so the unit is
  /// fully testable before the gateway wire-format is known.
  /// </summary>
  public interface ISmsSender
  {
      Task<SmsSendResult> SendAsync(string e164Phone, string text, CancellationToken ct);
  }

  public readonly record struct SmsSendResult(bool Delivered, string? ProviderMessageId, string? Error)
  {
      public static SmsSendResult Ok(string providerMessageId) => new(true, providerMessageId, null);
      public static SmsSendResult Failed(string error) => new(false, null, error);
  }
  ```
  Create `src/AFK4.Platform.Api/Sms/SmsOptions.cs`:
  ```csharp
  namespace AFK4.Platform.Api.Sms;

  public sealed class SmsOptions
  {
      public const string SectionName = "Sms";
      public string BaseUrl { get; set; } = string.Empty;
      public string ApiKey { get; set; } = string.Empty;
  }
  ```
  Register the fake in `PlatformApiFactory.ConfigureWebHost` (inside the existing `ConfigureServices`), as a singleton so a test can read `.Sent`:
  ```csharp
  services.RemoveAll<ISmsSender>();
  services.AddSingleton<ISmsSender, FakeSmsSender>();
  ```
  (Add `using AFK4.Platform.Api.Sms;`. `RemoveAll` is already imported via `Microsoft.Extensions.DependencyInjection.Extensions`.) Prod DI for `HttpSmsSender` is wired in Task 7; the factory override keeps tests on the fake regardless.

- [ ] **Step 4: Run — expect PASS:**
  ```
  dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~PhoneVerificationServiceTests.FakeSmsSender_IsRegisteredAsTheSmsSeam_AndCaptures"
  ```

- [ ] **Step 5: Commit:**
  ```
  git commit -am "feat(api): ISmsSender seam + FakeSmsSender capture for OTP self-registration"
  ```

---

## Task 2 — `PhoneVerificationEntity` + migration

**Files:**
- Create: `src/AFK4.Platform.Api/Data/PhoneVerificationEntity.cs`
- Modify: `src/AFK4.Platform.Api/Data/PlatformDbContext.cs`
- Create: `src/AFK4.Platform.Api/Data/Migrations/<timestamp>_AddPhoneVerifications.cs` (+ Designer + snapshot, generated)
- Test: `tests/AFK4.Platform.Api.Tests/PhoneVerificationServiceTests.cs` (add a round-trip test)

- [ ] **Step 1: Write the failing test.** Add to `PhoneVerificationServiceTests.cs`:
  ```csharp
  [Fact]
  public async Task PhoneVerificationEntity_RoundTrips()
  {
      await using var factory = new PlatformApiFactory();
      await using var scope = factory.Services.CreateAsyncScope();
      var db = scope.ServiceProvider.GetRequiredService<AFK4.Platform.Api.Data.PlatformDbContext>();

      var id = Guid.NewGuid();
      var orgId = Guid.NewGuid();
      db.PhoneVerifications.Add(new AFK4.Platform.Api.Data.PhoneVerificationEntity
      {
          PhoneVerificationId = id,
          OrganizationId = orgId,
          PhoneNumber = "+992900000001",
          CodeHash = "hash",
          AttemptCount = 0,
          ExpiresAtUtc = DateTimeOffset.Parse("2026-06-03T00:05:00Z"),
          CreatedAtUtc = DateTimeOffset.Parse("2026-06-03T00:00:00Z")
      });
      await db.SaveChangesAsync();

      var loaded = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
          .SingleAsync(db.PhoneVerifications, v => v.PhoneVerificationId == id);
      Assert.Equal("+992900000001", loaded.PhoneNumber);
      Assert.Null(loaded.ConsumedAtUtc);
      Assert.Null(loaded.VerificationTokenHash);
  }
  ```

- [ ] **Step 2: Run — expect FAIL** (`PhoneVerifications` DbSet + entity do not exist):
  ```
  dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~PhoneVerificationServiceTests.PhoneVerificationEntity_RoundTrips"
  ```

- [ ] **Step 3: Minimal impl.** Create `src/AFK4.Platform.Api/Data/PhoneVerificationEntity.cs`:
  ```csharp
  namespace AFK4.Platform.Api.Data;

  /// <summary>
  /// A pending phone-verification challenge for OTP self-registration. The raw code is NEVER
  /// stored — only <see cref="CodeHash"/>. One active row per (org, phone); a new start supersedes
  /// the previous one. On successful verify, <see cref="VerificationTokenHash"/> + expiry are set and
  /// the row is single-use via <see cref="ConsumedAtUtc"/>.
  /// </summary>
  public sealed class PhoneVerificationEntity
  {
      public Guid PhoneVerificationId { get; set; }
      public Guid OrganizationId { get; set; }

      /// <summary>E.164 phone the code was sent to.</summary>
      public string PhoneNumber { get; set; } = string.Empty;

      /// <summary>Hash of the OTP code (PasswordHasher); never the raw code.</summary>
      public string CodeHash { get; set; } = string.Empty;

      public int AttemptCount { get; set; }
      public DateTimeOffset ExpiresAtUtc { get; set; }

      /// <summary>Hash of the short-lived verification token issued on successful verify; null until verified.</summary>
      public string? VerificationTokenHash { get; set; }
      public DateTimeOffset? VerificationTokenExpiresAtUtc { get; set; }

      /// <summary>Set when the token is redeemed by <c>complete</c>; makes the token single-use.</summary>
      public DateTimeOffset? ConsumedAtUtc { get; set; }

      public DateTimeOffset CreatedAtUtc { get; set; }
  }
  ```
  In `PlatformDbContext.cs` add the DbSet (next to the other player sets near line 51):
  ```csharp
  public DbSet<PhoneVerificationEntity> PhoneVerifications => Set<PhoneVerificationEntity>();
  ```
  And entity config (next to the `PlayerCredentialEntity` block ~line 430):
  ```csharp
  modelBuilder.Entity<PhoneVerificationEntity>(entity =>
  {
      entity.ToTable("phone_verifications");
      entity.HasKey(verification => verification.PhoneVerificationId);
      entity.Property(verification => verification.PhoneNumber).HasMaxLength(64).IsRequired();
      entity.Property(verification => verification.CodeHash).HasMaxLength(512).IsRequired();
      entity.Property(verification => verification.VerificationTokenHash).HasMaxLength(512);
      entity.HasIndex(verification => new { verification.OrganizationId, verification.PhoneNumber });
  });
  ```
  Generate the migration (project + startup project are the same API project):
  ```
  dotnet ef migrations add AddPhoneVerifications --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --output-dir Data/Migrations
  ```
  Confirm the generated `Up` creates `phone_verifications` with the columns above and the snapshot updated. (Migrations are not exercised by the InMemory test DB, but they must compile and stay in sync — the build covers that.)

- [ ] **Step 4: Run — expect PASS:**
  ```
  dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~PhoneVerificationServiceTests.PhoneVerificationEntity_RoundTrips"
  ```

- [ ] **Step 5: Commit:**
  ```
  git commit -am "feat(api): PhoneVerificationEntity + migration for OTP storage"
  ```

---

## Task 3 — `PhoneVerificationService.StartAsync` (generate, hash, store, send, anti-enumeration)

**Files:**
- Create: `src/AFK4.Platform.Api/Identity/IPhoneVerificationService.cs`
- Create: `src/AFK4.Platform.Api/Identity/PhoneVerificationService.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs` (DI registration)
- Test: `tests/AFK4.Platform.Api.Tests/PhoneVerificationServiceTests.cs`

- [ ] **Step 1: Write the failing tests.** Add:
  ```csharp
  [Fact]
  public async Task StartAsync_SendsExactlyOneSms_WithACode_AndStoresOnlyTheHash()
  {
      await using var factory = new PlatformApiFactory();
      await using var scope = factory.Services.CreateAsyncScope();
      var service = scope.ServiceProvider.GetRequiredService<IPhoneVerificationService>();
      var db = scope.ServiceProvider.GetRequiredService<AFK4.Platform.Api.Data.PlatformDbContext>();
      var sender = (FakeSmsSender)scope.ServiceProvider.GetRequiredService<ISmsSender>();
      var orgId = Guid.NewGuid();

      await service.StartAsync(orgId, "+992900000001", default);

      var sms = Assert.Single(sender.Sent);
      Assert.Equal("+992900000001", sms.E164Phone);
      var code = new string(sms.Text.Where(char.IsDigit).ToArray());
      Assert.InRange(code.Length, 4, 6);

      var row = Assert.Single(db.PhoneVerifications);
      Assert.NotEqual(code, row.CodeHash);            // raw code is never stored
      Assert.DoesNotContain(code, row.CodeHash);
  }

  [Fact]
  public async Task StartAsync_Resend_SupersedesPreviousChallenge()
  {
      await using var factory = new PlatformApiFactory();
      await using var scope = factory.Services.CreateAsyncScope();
      var service = scope.ServiceProvider.GetRequiredService<IPhoneVerificationService>();
      var db = scope.ServiceProvider.GetRequiredService<AFK4.Platform.Api.Data.PlatformDbContext>();
      var orgId = Guid.NewGuid();

      await service.StartAsync(orgId, "+992900000001", default);
      await service.StartAsync(orgId, "+992900000001", default);

      // Only one active (unconsumed) challenge per (org, phone).
      Assert.Single(db.PhoneVerifications);
  }
  ```

- [ ] **Step 2: Run — expect FAIL** (`IPhoneVerificationService` does not exist):
  ```
  dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~PhoneVerificationServiceTests.StartAsync"
  ```

- [ ] **Step 3: Minimal impl.** Create `src/AFK4.Platform.Api/Identity/IPhoneVerificationService.cs`:
  ```csharp
  namespace AFK4.Platform.Api.Identity;

  public interface IPhoneVerificationService
  {
      /// <summary>Anti-enumeration: always succeeds. Generates a code, stores its hash, sends one SMS.</summary>
      Task StartAsync(Guid organizationId, string phoneNumber, CancellationToken cancellationToken);

      /// <summary>Returns a short-lived verification token on success; null on bad/expired/too-many code.</summary>
      Task<string?> VerifyAsync(Guid organizationId, string phoneNumber, string code, CancellationToken cancellationToken);
  }
  ```
  Create `src/AFK4.Platform.Api/Identity/PhoneVerificationService.cs` (only `StartAsync` filled in this task; `VerifyAsync` is Task 4 — stub it to `throw new NotImplementedException()` so the file compiles, then implement in Task 4):
  ```csharp
  using System.Security.Cryptography;
  using AFK4.Platform.Api.Data;
  using AFK4.Platform.Api.Sms;
  using Microsoft.AspNetCore.Identity;
  using Microsoft.EntityFrameworkCore;

  namespace AFK4.Platform.Api.Identity;

  public sealed class PhoneVerificationService(
      PlatformDbContext dbContext,
      ISmsSender smsSender,
      TimeProvider timeProvider) : IPhoneVerificationService
  {
      private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(5);
      private readonly PasswordHasher<PhoneVerificationEntity> hasher = new();

      public async Task StartAsync(Guid organizationId, string phoneNumber, CancellationToken cancellationToken)
      {
          var now = timeProvider.GetUtcNow();

          // Supersede any active challenge for this (org, phone) so there is one live code at a time.
          var existing = await dbContext.PhoneVerifications
              .Where(v => v.OrganizationId == organizationId && v.PhoneNumber == phoneNumber && v.ConsumedAtUtc == null)
              .ToListAsync(cancellationToken);
          dbContext.PhoneVerifications.RemoveRange(existing);

          var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
          var entity = new PhoneVerificationEntity
          {
              PhoneVerificationId = Guid.NewGuid(),
              OrganizationId = organizationId,
              PhoneNumber = phoneNumber,
              AttemptCount = 0,
              ExpiresAtUtc = now.Add(CodeLifetime),
              CreatedAtUtc = now
          };
          entity.CodeHash = hasher.HashPassword(entity, code);
          dbContext.PhoneVerifications.Add(entity);
          await dbContext.SaveChangesAsync(cancellationToken);

          // Side-effect last, so a send failure does not leave an un-stored code (the row is the source of truth).
          await smsSender.SendAsync(phoneNumber, $"AFK4 verification code: {code}", cancellationToken);
      }

      public Task<string?> VerifyAsync(Guid organizationId, string phoneNumber, string code, CancellationToken cancellationToken)
          => throw new NotImplementedException();
  }
  ```
  Register in `Program.cs` next to the player services (~line 166):
  ```csharp
  builder.Services.AddScoped<IPhoneVerificationService, PhoneVerificationService>();
  ```

- [ ] **Step 4: Run — expect PASS:**
  ```
  dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~PhoneVerificationServiceTests.StartAsync"
  ```

- [ ] **Step 5: Commit:**
  ```
  git commit -am "feat(api): PhoneVerificationService.StartAsync — generate/hash/store/send OTP"
  ```

---

## Task 4 — `PhoneVerificationService.VerifyAsync` (check hash/expiry, attempts, lockout, mint token)

**Files:**
- Modify: `src/AFK4.Platform.Api/Identity/PhoneVerificationService.cs`
- Test: `tests/AFK4.Platform.Api.Tests/PhoneVerificationServiceTests.cs`

- [ ] **Step 1: Write the failing tests.** Add (use a `FakeTimeProvider`-free approach by overriding via the service `TimeProvider` DI — the factory already registers the system `TimeProvider`; for the expiry test, drive expiry by writing `ExpiresAtUtc` in the past directly on the row):
  ```csharp
  private static async Task<(IPhoneVerificationService Service, AFK4.Platform.Api.Data.PlatformDbContext Db, string Code)>
      StartChallengeAsync(IServiceScope scope, Guid orgId, string phone)
  {
      var service = scope.ServiceProvider.GetRequiredService<IPhoneVerificationService>();
      var sender = (FakeSmsSender)scope.ServiceProvider.GetRequiredService<ISmsSender>();
      var db = scope.ServiceProvider.GetRequiredService<AFK4.Platform.Api.Data.PlatformDbContext>();
      await service.StartAsync(orgId, phone, default);
      var code = new string(sender.Sent.Single().Text.Where(char.IsDigit).ToArray());
      return (service, db, code);
  }

  [Fact]
  public async Task VerifyAsync_CorrectCode_ReturnsToken_AndDoesNotLeakTheCode()
  {
      await using var factory = new PlatformApiFactory();
      await using var scope = factory.Services.CreateAsyncScope();
      var orgId = Guid.NewGuid();
      var (service, _, code) = await StartChallengeAsync(scope, orgId, "+992900000001");

      var token = await service.VerifyAsync(orgId, "+992900000001", code, default);

      Assert.False(string.IsNullOrWhiteSpace(token));
      Assert.DoesNotContain(code, token!);   // token is not the code
  }

  [Fact]
  public async Task VerifyAsync_WrongCode_IncrementsAttempts_ThenLocksOut()
  {
      await using var factory = new PlatformApiFactory();
      await using var scope = factory.Services.CreateAsyncScope();
      var orgId = Guid.NewGuid();
      var (service, db, code) = await StartChallengeAsync(scope, orgId, "+992900000001");
      var wrong = code == "000000" ? "111111" : "000000";

      for (var i = 0; i < 5; i++)
          Assert.Null(await service.VerifyAsync(orgId, "+992900000001", wrong, default));

      Assert.Equal(5, db.PhoneVerifications.Single().AttemptCount);

      // After lockout, even the correct code is refused.
      Assert.Null(await service.VerifyAsync(orgId, "+992900000001", code, default));
  }

  [Fact]
  public async Task VerifyAsync_ExpiredCode_IsRejected()
  {
      await using var factory = new PlatformApiFactory();
      await using var scope = factory.Services.CreateAsyncScope();
      var orgId = Guid.NewGuid();
      var (service, db, code) = await StartChallengeAsync(scope, orgId, "+992900000001");

      var row = db.PhoneVerifications.Single();
      row.ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
      await db.SaveChangesAsync();

      Assert.Null(await service.VerifyAsync(orgId, "+992900000001", code, default));
  }
  ```

- [ ] **Step 2: Run — expect FAIL** (`VerifyAsync` throws `NotImplementedException`):
  ```
  dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~PhoneVerificationServiceTests.VerifyAsync"
  ```

- [ ] **Step 3: Minimal impl.** Replace the `VerifyAsync` stub:
  ```csharp
  private const int MaxAttempts = 5;
  private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(10);

  public async Task<string?> VerifyAsync(
      Guid organizationId, string phoneNumber, string code, CancellationToken cancellationToken)
  {
      var now = timeProvider.GetUtcNow();
      var row = await dbContext.PhoneVerifications.SingleOrDefaultAsync(
          v => v.OrganizationId == organizationId && v.PhoneNumber == phoneNumber && v.ConsumedAtUtc == null,
          cancellationToken);

      if (row is null || row.ExpiresAtUtc <= now || row.AttemptCount >= MaxAttempts)
      {
          return null;
      }

      var verification = hasher.VerifyHashedPassword(row, row.CodeHash, code);
      if (verification == PasswordVerificationResult.Failed)
      {
          row.AttemptCount++;
          await dbContext.SaveChangesAsync(cancellationToken);
          return null;
      }

      var (tokenId, token) = CreateToken();
      row.VerificationTokenHash = HashToken(token);
      row.VerificationTokenExpiresAtUtc = now.Add(TokenLifetime);
      await dbContext.SaveChangesAsync(cancellationToken);
      return token;
  }

  private static (Guid TokenId, string Token) CreateToken()
  {
      var tokenId = Guid.NewGuid();
      var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
      return (tokenId, $"{tokenId:N}.{secret}");
  }

  private static string HashToken(string token)
      => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
  ```
  (Token shape mirrors `OpaquePlayerTokenService.CreateToken`. We store the SHA-256 hash, never the raw token.)

- [ ] **Step 4: Run — expect PASS:**
  ```
  dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~PhoneVerificationServiceTests.VerifyAsync"
  ```

- [ ] **Step 5: Commit:**
  ```
  git commit -am "feat(api): PhoneVerificationService.VerifyAsync — attempts, lockout, expiry, single-use token"
  ```

---

## Task 5 — Contracts + `registration/start` & `registration/verify` endpoints

**Files:**
- Create: `src/AFK4.Shared.Contracts/Players/RegistrationStartRequest.cs`
- Create: `src/AFK4.Shared.Contracts/Players/RegistrationVerifyRequest.cs`
- Create: `src/AFK4.Shared.Contracts/Players/RegistrationVerifyResponse.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs`
- Test: `tests/AFK4.Platform.Api.Tests/RegistrationEndpointTests.cs`

- [ ] **Step 1: Write the failing tests.** Create `RegistrationEndpointTests.cs`:
  ```csharp
  using System.Net;
  using System.Net.Http.Json;
  using AFK4.Platform.Api.Sms;
  using AFK4.Shared.Contracts.Players;
  using Microsoft.Extensions.DependencyInjection;

  namespace AFK4.Platform.Api.Tests;

  public sealed class RegistrationEndpointTests
  {
      private static string LastCode(PlatformApiFactory factory)
      {
          using var scope = factory.Services.CreateScope();
          var sender = (FakeSmsSender)scope.ServiceProvider.GetRequiredService<ISmsSender>();
          return new string(sender.Sent[^1].Text.Where(char.IsDigit).ToArray());
      }

      [Fact]
      public async Task Start_AlwaysReturns200_EvenForUnknownPhone_AntiEnumeration()
      {
          await using var factory = new PlatformApiFactory();
          using var client = factory.CreateClient();
          var resp = await client.PostAsJsonAsync(
              "/api/public/player/registration/start",
              new RegistrationStartRequest(Guid.NewGuid(), "+992900000999"));
          Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
      }

      [Fact]
      public async Task Verify_WithCorrectCode_ReturnsVerificationToken_NeverTheCode()
      {
          await using var factory = new PlatformApiFactory();
          using var client = factory.CreateClient();
          var orgId = Guid.NewGuid();

          await client.PostAsJsonAsync("/api/public/player/registration/start",
              new RegistrationStartRequest(orgId, "+992900000001"));
          var code = LastCode(factory);

          var verifyResp = await client.PostAsJsonAsync("/api/public/player/registration/verify",
              new RegistrationVerifyRequest(orgId, "+992900000001", code));
          Assert.Equal(HttpStatusCode.OK, verifyResp.StatusCode);
          var body = await verifyResp.Content.ReadFromJsonAsync<RegistrationVerifyResponse>();
          Assert.False(string.IsNullOrWhiteSpace(body!.VerificationToken));
          Assert.DoesNotContain(code, body.VerificationToken);
      }

      [Fact]
      public async Task Verify_WithWrongCode_Returns400()
      {
          await using var factory = new PlatformApiFactory();
          using var client = factory.CreateClient();
          var orgId = Guid.NewGuid();
          await client.PostAsJsonAsync("/api/public/player/registration/start",
              new RegistrationStartRequest(orgId, "+992900000001"));

          var verifyResp = await client.PostAsJsonAsync("/api/public/player/registration/verify",
              new RegistrationVerifyRequest(orgId, "+992900000001", "999999"));
          Assert.Equal(HttpStatusCode.BadRequest, verifyResp.StatusCode);
      }
  }
  ```

- [ ] **Step 2: Run — expect FAIL** (contracts + endpoints do not exist):
  ```
  dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~RegistrationEndpointTests"
  ```

- [ ] **Step 3: Minimal impl.** Contracts:
  ```csharp
  // RegistrationStartRequest.cs
  namespace AFK4.Shared.Contracts.Players;
  public sealed record RegistrationStartRequest(Guid OrganizationId, string PhoneNumber);
  ```
  ```csharp
  // RegistrationVerifyRequest.cs
  namespace AFK4.Shared.Contracts.Players;
  public sealed record RegistrationVerifyRequest(Guid OrganizationId, string PhoneNumber, string Code);
  ```
  ```csharp
  // RegistrationVerifyResponse.cs
  namespace AFK4.Shared.Contracts.Players;
  public sealed record RegistrationVerifyResponse(string VerificationToken);
  ```
  Endpoints in `Program.cs` next to `/api/public/player/sign-in` (~line 654):
  ```csharp
  app.MapPost("/api/public/player/registration/start", async (
      RegistrationStartRequest request,
      IPhoneVerificationService verificationService,
      CancellationToken cancellationToken) =>
  {
      // Anti-enumeration: 200 regardless of whether the phone is known. Basic shape guard only.
      if (!string.IsNullOrWhiteSpace(request.PhoneNumber) && request.OrganizationId != Guid.Empty)
      {
          await verificationService.StartAsync(request.OrganizationId, request.PhoneNumber.Trim(), cancellationToken);
      }
      return Results.Ok();
  }).RequireRateLimiting("player-public");

  app.MapPost("/api/public/player/registration/verify", async (
      RegistrationVerifyRequest request,
      IPhoneVerificationService verificationService,
      CancellationToken cancellationToken) =>
  {
      var token = await verificationService.VerifyAsync(
          request.OrganizationId, request.PhoneNumber.Trim(), request.Code, cancellationToken);
      return token is null
          ? Results.BadRequest(new { error = "Invalid or expired code." })
          : Results.Ok(new RegistrationVerifyResponse(token));
  }).RequireRateLimiting("player-public");
  ```

- [ ] **Step 4: Run — expect PASS:**
  ```
  dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~RegistrationEndpointTests"
  ```

- [ ] **Step 5: Commit:**
  ```
  git commit -am "feat(api): public registration/start + registration/verify endpoints"
  ```

---

## Task 6 — `registration/complete` endpoint (consume token → account + PIN + signed in)

**Files:**
- Create: `src/AFK4.Shared.Contracts/Players/RegistrationCompleteRequest.cs`
- Modify: `src/AFK4.Platform.Api/Identity/IPhoneVerificationService.cs` (add `RedeemTokenAsync`)
- Modify: `src/AFK4.Platform.Api/Identity/PhoneVerificationService.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs`
- Test: `tests/AFK4.Platform.Api.Tests/RegistrationEndpointTests.cs`

The token redemption must be **single-use** and is owned by the service (so storage details stay encapsulated). `complete` then resolves-or-creates the `PlayerAccount`, sets the PIN via `SetPasswordAsync`, marks `PhoneVerified=true`, and issues tokens via `IPlayerTokenService.IssueAsync` — returning the same `PlayerSignInResponse` a normal sign-in returns.

- [ ] **Step 1: Write the failing tests.** Add to `RegistrationEndpointTests.cs`:
  ```csharp
  private async Task<(Guid OrgId, string Phone, string Token)> StartVerifyAsync(
      PlatformApiFactory factory, HttpClient client)
  {
      var orgId = Guid.NewGuid();
      const string phone = "+992900000001";
      await client.PostAsJsonAsync("/api/public/player/registration/start",
          new RegistrationStartRequest(orgId, phone));
      var code = LastCode(factory);
      var verify = await client.PostAsJsonAsync("/api/public/player/registration/verify",
          new RegistrationVerifyRequest(orgId, phone, code));
      var body = await verify.Content.ReadFromJsonAsync<RegistrationVerifyResponse>();
      return (orgId, phone, body!.VerificationToken);
  }

  [Fact]
  public async Task Complete_HappyPath_CreatesVerifiedAccount_AndCanSignIn()
  {
      await using var factory = new PlatformApiFactory();
      using var client = factory.CreateClient();
      var (orgId, phone, token) = await StartVerifyAsync(factory, client);

      var complete = await client.PostAsJsonAsync("/api/public/player/registration/complete",
          new RegistrationCompleteRequest(token, "Анна", "1234", "ru", true));
      Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
      var signedIn = await complete.Content.ReadFromJsonAsync<PlayerSignInResponse>();
      Assert.True(signedIn!.PhoneVerified);
      Assert.False(string.IsNullOrWhiteSpace(signedIn.AccessToken));

      // The new PIN works on the normal sign-in path.
      var reSignIn = await client.PostAsJsonAsync("/api/public/player/sign-in",
          new PlayerSignInRequest(orgId, phone, "1234"));
      Assert.Equal(HttpStatusCode.OK, reSignIn.StatusCode);
  }

  [Fact]
  public async Task Complete_TokenIsSingleUse_SecondAttemptFails()
  {
      await using var factory = new PlatformApiFactory();
      using var client = factory.CreateClient();
      var (_, _, token) = await StartVerifyAsync(factory, client);

      var first = await client.PostAsJsonAsync("/api/public/player/registration/complete",
          new RegistrationCompleteRequest(token, "Анна", "1234", null, false));
      Assert.Equal(HttpStatusCode.OK, first.StatusCode);

      var second = await client.PostAsJsonAsync("/api/public/player/registration/complete",
          new RegistrationCompleteRequest(token, "Анна", "1234", null, false));
      Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
  }

  [Fact]
  public async Task Complete_RejectsUnknownToken()
  {
      await using var factory = new PlatformApiFactory();
      using var client = factory.CreateClient();
      var complete = await client.PostAsJsonAsync("/api/public/player/registration/complete",
          new RegistrationCompleteRequest(Guid.NewGuid().ToString("N") + ".deadbeef", "Анна", "1234", null, false));
      Assert.Equal(HttpStatusCode.BadRequest, complete.StatusCode);
  }
  ```

- [ ] **Step 2: Run — expect FAIL** (`RegistrationCompleteRequest` + endpoint + `RedeemTokenAsync` missing):
  ```
  dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~RegistrationEndpointTests.Complete"
  ```

- [ ] **Step 3: Minimal impl.** Contract:
  ```csharp
  // RegistrationCompleteRequest.cs
  namespace AFK4.Shared.Contracts.Players;
  public sealed record RegistrationCompleteRequest(
      string VerificationToken,
      string DisplayName,
      string Pin,
      string? PreferredLocale,
      bool MarketingOptIn);
  ```
  Add to `IPhoneVerificationService`:
  ```csharp
  /// <summary>Single-use: redeems the verification token, returning the verified (org, phone) or null.</summary>
  Task<(Guid OrganizationId, string PhoneNumber)?> RedeemTokenAsync(string verificationToken, CancellationToken cancellationToken);
  ```
  Impl in `PhoneVerificationService`:
  ```csharp
  public async Task<(Guid OrganizationId, string PhoneNumber)?> RedeemTokenAsync(
      string verificationToken, CancellationToken cancellationToken)
  {
      if (string.IsNullOrWhiteSpace(verificationToken))
      {
          return null;
      }

      var now = timeProvider.GetUtcNow();
      var tokenHash = HashToken(verificationToken);
      var row = await dbContext.PhoneVerifications.SingleOrDefaultAsync(
          v => v.VerificationTokenHash == tokenHash && v.ConsumedAtUtc == null, cancellationToken);

      if (row is null || row.VerificationTokenExpiresAtUtc is not { } expires || expires <= now)
      {
          return null;
      }

      row.ConsumedAtUtc = now;
      await dbContext.SaveChangesAsync(cancellationToken);
      return (row.OrganizationId, row.PhoneNumber);
  }
  ```
  Endpoint in `Program.cs` after `registration/verify`:
  ```csharp
  app.MapPost("/api/public/player/registration/complete", async (
      RegistrationCompleteRequest request,
      IPhoneVerificationService verificationService,
      IPlayerCredentialService credentialService,
      IPlayerTokenService tokenService,
      PlatformDbContext dbContext,
      TimeProvider timeProvider,
      CancellationToken cancellationToken) =>
  {
      if (string.IsNullOrWhiteSpace(request.Pin) || request.Pin.Length < 4 ||
          string.IsNullOrWhiteSpace(request.DisplayName))
      {
          return Results.BadRequest(new { error = "Display name and a 4+ digit PIN are required." });
      }

      var redeemed = await verificationService.RedeemTokenAsync(request.VerificationToken, cancellationToken);
      if (redeemed is not var (orgId, phone) || redeemed is null)
      {
          return Results.BadRequest(new { error = "Invalid or expired verification token." });
      }

      var now = timeProvider.GetUtcNow();
      var account = await dbContext.PlayerAccounts.SingleOrDefaultAsync(
          p => p.OrganizationId == orgId && p.PhoneNumber == phone, cancellationToken);
      if (account is null)
      {
          account = new PlayerAccountEntity
          {
              PlayerAccountId = Guid.NewGuid(),
              OrganizationId = orgId,
              HomeBranchId = Guid.Empty, // self-registered; branch assigned on first visit
              DisplayName = request.DisplayName.Trim(),
              PhoneNumber = phone,
              PreferredLocale = request.PreferredLocale,
              MarketingOptIn = request.MarketingOptIn,
              IsActive = true,
              CreatedAtUtc = now
          };
          dbContext.PlayerAccounts.Add(account);
          await dbContext.SaveChangesAsync(cancellationToken);
      }

      await credentialService.SetPasswordAsync(account.PlayerAccountId, request.Pin, cancellationToken);

      var credential = await dbContext.PlayerCredentials.SingleAsync(
          c => c.PlayerAccountId == account.PlayerAccountId, cancellationToken);
      credential.PhoneVerified = true;
      credential.PhoneVerifiedAtUtc = now;
      credential.UpdatedAtUtc = now;
      await dbContext.SaveChangesAsync(cancellationToken);

      var response = await tokenService.IssueAsync(account, phoneVerified: true, cancellationToken);
      return Results.Ok(response);
  }).RequireRateLimiting("player-public");
  ```
  (Pattern note: `redeemed is not var (orgId, phone) || redeemed is null` is shown for clarity — implementer may instead bind `if (redeemed is not { } pair) return BadRequest; var (orgId, phone) = pair;`. The behaviour is the assertion under test.)

- [ ] **Step 4: Run — expect PASS:**
  ```
  dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~RegistrationEndpointTests"
  ```

- [ ] **Step 5: Commit:**
  ```
  git commit -am "feat(api): registration/complete — verified self-registration creates account, PIN, signs in"
  ```

---

## Task 7 — Production DI wiring: `HttpSmsSender` skeleton + typed HttpClient + options binding

This task wires the **production** path so the app boots with a real `ISmsSender` (the fake stays test-only via the factory). The wire-format itself is a single method left empty until Task 8.

**Files:**
- Create: `src/AFK4.Platform.Api/Sms/HttpSmsSender.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs` (options binding + `AddHttpClient<ISmsSender, HttpSmsSender>`)
- Test: `tests/AFK4.Platform.Api.Tests/PhoneVerificationServiceTests.cs` (DI-boots + still-fake assertion)

- [ ] **Step 1: Write the failing test.** Add:
  ```csharp
  [Fact]
  public async Task ProductionWiring_BootsWithHttpSmsSender_ButTestsOverrideWithFake()
  {
      // The factory must keep the fake even after production registers HttpSmsSender.
      await using var factory = new PlatformApiFactory();
      await using var scope = factory.Services.CreateAsyncScope();
      var sender = scope.ServiceProvider.GetRequiredService<ISmsSender>();
      Assert.IsType<FakeSmsSender>(sender);
  }
  ```
  (This currently passes only because no prod `ISmsSender` exists yet; once we add the prod registration in Step 3, the factory's `RemoveAll<ISmsSender>()` + `AddSingleton<FakeSmsSender>` must still win. The test guards that ordering — it fails if the prod registration is added without the factory override surviving.)

- [ ] **Step 2: Run — expect PASS-then-protect.** Run the test, confirm green, then proceed; this is a guard test that must stay green through Step 3:
  ```
  dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~PhoneVerificationServiceTests.ProductionWiring"
  ```

- [ ] **Step 3: Minimal impl.** Create `src/AFK4.Platform.Api/Sms/HttpSmsSender.cs` with the wire-format isolated to one method:
  ```csharp
  using Microsoft.Extensions.Options;

  namespace AFK4.Platform.Api.Sms;

  /// <summary>
  /// Production SMS adapter. The gateway-specific request shaping lives entirely in
  /// <see cref="BuildRequest"/>; everything else is gateway-agnostic. Until the gateway contract
  /// is provided (see Task 8), <see cref="BuildRequest"/> throws — the rest of the OTP unit ships
  /// and is verified against <c>FakeSmsSender</c>.
  /// </summary>
  public sealed class HttpSmsSender(HttpClient httpClient, IOptions<SmsOptions> options) : ISmsSender
  {
      private readonly SmsOptions options = options.Value;

      public async Task<SmsSendResult> SendAsync(string e164Phone, string text, CancellationToken ct)
      {
          var request = BuildRequest(e164Phone, text);
          using var response = await httpClient.SendAsync(request, ct);
          if (!response.IsSuccessStatusCode)
          {
              return SmsSendResult.Failed($"gateway returned {(int)response.StatusCode}");
          }

          return ParseResult(await response.Content.ReadAsStringAsync(ct));
      }

      // --- Gateway-specific seam: FILL IN at Task 8 from the provided SMS-gateway contract. ---
      private HttpRequestMessage BuildRequest(string e164Phone, string text)
          => throw new NotImplementedException("SMS gateway request shape pending — see Task 8.");

      private static SmsSendResult ParseResult(string body)
          => throw new NotImplementedException("SMS gateway response shape pending — see Task 8.");
  }
  ```
  In `Program.cs`, near the other `Configure<...>` calls (~line 200) and the player-service registrations:
  ```csharp
  builder.Services.Configure<SmsOptions>(builder.Configuration.GetSection(SmsOptions.SectionName));
  builder.Services.AddHttpClient<ISmsSender, HttpSmsSender>((provider, client) =>
  {
      var opts = provider.GetRequiredService<IOptions<SmsOptions>>().Value;
      if (!string.IsNullOrWhiteSpace(opts.BaseUrl))
      {
          client.BaseAddress = new Uri(opts.BaseUrl);
      }
  });
  ```
  (Add `using AFK4.Platform.Api.Sms;` and `using Microsoft.Extensions.Options;` if not present.)

- [ ] **Step 4: Run — expect PASS** (the guard test stays green; the fake still wins in tests):
  ```
  dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~PhoneVerificationServiceTests"
  ```

- [ ] **Step 5: Commit:**
  ```
  git commit -am "feat(api): production HttpSmsSender skeleton + typed HttpClient + SmsOptions binding"
  ```

---

## Task 8 — Implement `HttpSmsSender` wire-format (FILL IN the provided gateway contract at execution)

> **This task is the only one that waits on the external SMS gateway.** The task itself is fully specified — it is **not** a placeholder. The deliverable is: take the gateway's actual base URL / auth / send-SMS request+response shape (provided by the user at execution time), implement the two seam methods (`BuildRequest`, `ParseResult`), and add **one** request-shape test that pins the wire format. Tasks 1–7 already ship and are green; this task flips production from "throws" to "sends".

**TODO-FOR-THE-HUMAN (collect before starting this task):**
- Gateway **base URL** (prod) → goes into `Sms:BaseUrl`.
- **Auth** scheme: header name + token (likely `Authorization: Bearer <key>` or `X-Api-Key`) → `Sms:ApiKey`, applied in `BuildRequest`.
- Send-SMS **request**: HTTP method, path, body shape (e.g. `{ "to": "...", "text": "..." }` or form-encoded), and the phone format the gateway expects (E.164 vs local).
- Send-SMS **response**: success status, and where the provider message id / error lives in the body (drives `ParseResult`).

**Files:**
- Modify: `src/AFK4.Platform.Api/Sms/HttpSmsSender.cs` (fill `BuildRequest` + `ParseResult`)
- Test: `tests/AFK4.Platform.Api.Tests/HttpSmsSenderTests.cs`

- [ ] **Step 1: Write the failing wire-shape test** (once the contract is known). Use a stub `HttpMessageHandler` to capture the outgoing request and assert the exact shape the provider requires. Template (fill the assertions to match the provided contract):
  ```csharp
  using System.Net;
  using AFK4.Platform.Api.Sms;
  using Microsoft.Extensions.Options;

  namespace AFK4.Platform.Api.Tests;

  public sealed class HttpSmsSenderTests
  {
      private sealed class CapturingHandler : HttpMessageHandler
      {
          public HttpRequestMessage? Captured;
          public string? CapturedBody;
          public HttpResponseMessage Response = new(HttpStatusCode.OK)
          {
              // Replace with the provider's real success body once known:
              Content = new StringContent("{\"messageId\":\"abc123\"}")
          };
          protected override async Task<HttpResponseMessage> SendAsync(
              HttpRequestMessage request, CancellationToken cancellationToken)
          {
              Captured = request;
              if (request.Content is not null)
                  CapturedBody = await request.Content.ReadAsStringAsync(cancellationToken);
              return Response;
          }
      }

      [Fact]
      public async Task SendAsync_ShapesRequest_PerGatewayContract()
      {
          var handler = new CapturingHandler();
          var client = new HttpClient(handler) { BaseAddress = new Uri("https://sms.example/") };
          var options = Options.Create(new SmsOptions { BaseUrl = "https://sms.example/", ApiKey = "K" });
          var sender = new HttpSmsSender(client, options);

          var result = await sender.SendAsync("+992900000001", "code 1234", default);

          Assert.True(result.Delivered);
          Assert.Equal("abc123", result.ProviderMessageId);
          // FILL IN once the contract is known, e.g.:
          // Assert.Equal(HttpMethod.Post, handler.Captured!.Method);
          // Assert.Equal("/api/send", handler.Captured.RequestUri!.AbsolutePath);
          // Assert.Equal("Bearer K", handler.Captured.Headers.Authorization!.ToString());
          // Assert.Contains("\"to\":\"+992900000001\"", handler.CapturedBody);
      }
  }
  ```

- [ ] **Step 2: Run — expect FAIL** (`BuildRequest`/`ParseResult` still throw):
  ```
  dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~HttpSmsSenderTests"
  ```

- [ ] **Step 3: Implement the two seam methods** from the provided contract — apply auth header + path + body in `BuildRequest`, and extract the provider message id / error in `ParseResult`. Keep all gateway specifics inside these two methods so the rest of the adapter stays generic.

- [ ] **Step 4: Run — expect PASS:**
  ```
  dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~HttpSmsSenderTests"
  ```

- [ ] **Step 5: Commit:**
  ```
  git commit -am "feat(api): implement HttpSmsSender wire-format for the SMS gateway"
  ```

---

## Verification gate

Run the full API test suite on Linux and confirm green (baseline ~936 tests + the new OTP tests; Task 8's wire-shape test counts only once the gateway contract is provided — Tasks 1–7 must be fully green regardless):

```
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj
```

Expected: **all tests pass**, including `PhoneVerificationServiceTests` and `RegistrationEndpointTests`. The unit is shippable after Task 7 with `FakeSmsSender`; production SMS sending goes live when Task 8's gateway wire-format lands. The operator-set-PIN endpoint remains available as a fallback and is intentionally untouched.
