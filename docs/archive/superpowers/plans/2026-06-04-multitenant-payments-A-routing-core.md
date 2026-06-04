# Multi-Tenant Payments — Subsystem A: Routing Core Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Unit 2's single global dcgate project with per-branch dcgate credentials (org-level fallback), so each tenant's online top-ups are created with their own API key and each inbound webhook is verified with that tenant's own webhook secret.

**Architecture:** A new `branch_payment_gateways` table holds one row per dcgate project (= one card), keyed to a branch (nullable `BranchId` ⇒ org-level fallback), storing the dcgate `apiKey` and `webhookSecret` **encrypted at rest** via a new AES-256-GCM `ISecretProtector`. A resolver picks the right gateway for a branch (outbound) or by `x-dcgate-project-id` (inbound). The top-up endpoint resolves the gateway, decrypts the apiKey, and creates the payment through a per-apiKey client built by `IDcGateClientFactory`; the webhook endpoint resolves the gateway by project id, decrypts its secret, and verifies the HMAC with it. The counter (operator-confirmed) path is untouched.

**Tech Stack:** ASP.NET Core minimal APIs, EF Core 10 (Postgres prod / InMemory tests), `dotnet ef` 10.x, `System.Security.Cryptography.AesGcm`, xUnit + `WebApplicationFactory<Program>` (`PlatformApiFactory`). `TreatWarningsAsErrors=true` — add only usings you actually use. Money stays `long` minor units; the only major-unit conversion stays inside `DcGateClient`.

**Branch:** continue on `sp4-customer-shell` (Unit 1 + Unit 2 already committed there). Work directory: `/home/fedya/projects/afk4.net`.

---

## Key findings (verified ground truth, 2026-06-04)

- **Outbound endpoint** `app.MapPost("/api/me/wallet/top-up-intent", ...)` is at `src/AFK4.Platform.Api/Program.cs:962`. It currently injects `IDcGateClient dcGateClient` (param line 965) and, in the `if (method == "dcgate")` block (lines 1016-1028), calls `dcGateClient.CreatePaymentAsync(...)`. The intent's branch is `account.HomeBranchId` (line 1005). Counter path and the `PlayerTopUpIntentDto` response (lines 1033-1045) stay exactly as-is.
- **Inbound webhook** `app.MapPost("/api/public/payments/dcgate/webhook", ...)` is at `Program.cs:697`. It injects `IOptions<DcGateOptions> dcGateOptions` (line 699), reads the raw body (lines 706-712), and verifies with `DcGateSignatureIsValid(httpRequest, rawBody, options.WebhookSecret)` (line 714). Everything after the signature check (idempotency by `EventId`, intent lookup, `payment.paid/expired/disputed` switch, `TopUpWalletAsync` credit with `TopUpIntentCreditReason` + intent-id `"N"` key, 503-on-no-shift, race-safe event insert) must remain unchanged.
- **Signature helper** `static bool DcGateSignatureIsValid(HttpRequest request, string rawBody, string secret)` is a local function at `Program.cs:11288`. It already takes the secret as a parameter — pass the decrypted per-project secret. Do not change its body.
- **DI registration** for dcgate is at `Program.cs:267-275`: `Configure<DcGateOptions>` + `AddHttpClient<IDcGateClient, DcGateClient>(...)` setting `BaseAddress` from `opts.BaseUrl`. Other scoped services are registered just above (lines 255-265).
- **`DcGateOptions`** (`src/AFK4.Platform.Api/Payments/DcGate/DcGateOptions.cs`) has `SectionName = "DcGate"`, `BaseUrl`, `ApiKey`, `WebhookSecret`. After this plan, `ApiKey` and `WebhookSecret` are **removed** (per-project now); only `BaseUrl` remains.
- **`DcGateClient`** (`src/AFK4.Platform.Api/Payments/DcGate/DcGateClient.cs`) currently reads the apiKey from `IOptions<DcGateOptions>`. It will be refactored to take the apiKey via its constructor.
- **`IDcGateClient` / `DcGatePaymentResult`** live in `src/AFK4.Platform.Api/Payments/DcGate/IDcGateClient.cs`. The `CreatePaymentAsync` signature is unchanged.
- **DbContext**: `src/AFK4.Platform.Api/Data/PlatformDbContext.cs`. DbSets are declared near the top (e.g. `PaymentIntents` at line 93, `DcGateWebhookEvents` at line 95, `Branches` at line 9). Entity config blocks live in `OnModelCreating` (e.g. `PaymentIntentEntity` at line 770, `DcGateWebhookEventEntity` at line 975). Mirror the `PaymentIntentEntity` block style.
- **Migrations** live in `src/AFK4.Platform.Api/Data/Migrations/`, namespace `AFK4.Platform.Api.Data.Migrations`, `#nullable disable`. Latest is `20260604050241_AddDcGateColumnsAndWebhookEvents`. Generate with `dotnet ef migrations add`.
- **Existing Unit 2 tests that WILL break and must be migrated by this plan:**
  - `tests/AFK4.Platform.Api.Tests/DcGateClientTests.cs` — constructs `DcGateClient` with `IOptions<DcGateOptions>` (Task 4 changes the ctor).
  - `tests/AFK4.Platform.Api.Tests/DcGateTopUpIntentTests.cs` — injects a fake `IDcGateClient` and asserts the dcgate path (Task 5 changes wiring to a factory + gateway resolution).
  - `tests/AFK4.Platform.Api.Tests/DcGateWebhookEndpointTests.cs` — sets a global `DcGateOptions.WebhookSecret` and signs with it (Task 6 changes to per-project secret).
- **Test infra**: `tests/AFK4.Platform.Api.Tests/PlatformApiFactory.cs` — ctor `PlatformApiFactory(bool useRealSessionBilling = false, Action<IServiceCollection>? extraServices = null)`; `extraServices` runs last in `ConfigureWebHost` (line 68). `TestIds.OrganizationId` / `TestIds.BranchId` / `TestIds.TechnicianStaffUserId` exist. `StaffAuthTestHelper` seeds an org+branch.

---

## File Structure

**New production files:**
- `src/AFK4.Platform.Api/Security/SecretProtectionOptions.cs` — bound config (`EncryptionKeyBase64`), `SectionName = "Secrets"`.
- `src/AFK4.Platform.Api/Security/ISecretProtector.cs` — `Protect(string)` / `Unprotect(string)` seam.
- `src/AFK4.Platform.Api/Security/AesGcmSecretProtector.cs` — AES-256-GCM envelope implementation.
- `src/AFK4.Platform.Api/Data/BranchPaymentGatewayEntity.cs` — the per-card gateway row.
- `src/AFK4.Platform.Api/Payments/BranchPaymentGatewayStatus.cs` — status string constants.
- `src/AFK4.Platform.Api/Payments/IBranchPaymentGatewayResolver.cs` — resolver seam.
- `src/AFK4.Platform.Api/Payments/EfBranchPaymentGatewayResolver.cs` — EF resolver implementation.
- `src/AFK4.Platform.Api/Payments/DcGate/IDcGateClientFactory.cs` — per-apiKey client factory seam.
- `src/AFK4.Platform.Api/Payments/DcGate/DcGateClientFactory.cs` — factory implementation.
- `src/AFK4.Platform.Api/Data/Migrations/<timestamp>_AddBranchPaymentGateways.cs` — generated migration.

**New test files:**
- `tests/AFK4.Platform.Api.Tests/AesGcmSecretProtectorTests.cs`
- `tests/AFK4.Platform.Api.Tests/BranchPaymentGatewayEntityTests.cs`
- `tests/AFK4.Platform.Api.Tests/BranchPaymentGatewayResolverTests.cs`

**Modified files:**
- `src/AFK4.Platform.Api/Payments/DcGate/DcGateOptions.cs` — drop `ApiKey` + `WebhookSecret`.
- `src/AFK4.Platform.Api/Payments/DcGate/DcGateClient.cs` — take apiKey via ctor, not options.
- `src/AFK4.Platform.Api/Data/PlatformDbContext.cs` — add DbSet + config block.
- `src/AFK4.Platform.Api/Program.cs` — DI (secret protector, resolver, client factory, drop singleton client); rewire outbound + inbound endpoints.
- `src/AFK4.Platform.Api/appsettings.json` — drop `DcGate:ApiKey`/`DcGate:WebhookSecret`; add `Secrets:EncryptionKeyBase64` placeholder.
- `tests/AFK4.Platform.Api.Tests/PlatformApiFactory.cs` — add a default test encryption key.
- `tests/AFK4.Platform.Api.Tests/DcGateClientTests.cs` — new ctor.
- `tests/AFK4.Platform.Api.Tests/DcGateTopUpIntentTests.cs` — fake factory + seed gateway.
- `tests/AFK4.Platform.Api.Tests/DcGateWebhookEndpointTests.cs` — seed gateway + per-project secret.

---

## Task 1 — `ISecretProtector` (AES-256-GCM) + options + DI

**Files:**
- Create: `src/AFK4.Platform.Api/Security/SecretProtectionOptions.cs`
- Create: `src/AFK4.Platform.Api/Security/ISecretProtector.cs`
- Create: `src/AFK4.Platform.Api/Security/AesGcmSecretProtector.cs`
- Create test: `tests/AFK4.Platform.Api.Tests/AesGcmSecretProtectorTests.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs` (registration)

- [ ] **Step 1: Write the failing test.**
  Create `tests/AFK4.Platform.Api.Tests/AesGcmSecretProtectorTests.cs`:

  ```csharp
  using System;
  using AFK4.Platform.Api.Security;
  using Microsoft.Extensions.Options;
  using Xunit;

  namespace AFK4.Platform.Api.Tests;

  public class AesGcmSecretProtectorTests
  {
      // A throwaway 32-byte (all-zero) key, base64-encoded. Tests only.
      private const string TestKeyBase64 = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

      private static AesGcmSecretProtector Create(string keyBase64 = TestKeyBase64) =>
          new(Options.Create(new SecretProtectionOptions { EncryptionKeyBase64 = keyBase64 }));

      [Fact]
      public void ProtectThenUnprotect_RoundTrips()
      {
          var protector = Create();
          const string secret = "dcg_super-secret-api-key-value";

          var protectedValue = protector.Protect(secret);

          Assert.NotEqual(secret, protectedValue);
          Assert.Equal(secret, protector.Unprotect(protectedValue));
      }

      [Fact]
      public void Protect_ProducesDifferentCiphertextEachTime()
      {
          var protector = Create();
          var a = protector.Protect("same-input");
          var b = protector.Protect("same-input");

          Assert.NotEqual(a, b); // random nonce per call
          Assert.Equal("same-input", protector.Unprotect(a));
          Assert.Equal("same-input", protector.Unprotect(b));
      }

      [Fact]
      public void Unprotect_WithTamperedCiphertext_Throws()
      {
          var protector = Create();
          var protectedValue = protector.Protect("secret");
          var tampered = protectedValue[..^2] + (protectedValue.EndsWith("A") ? "B=" : "A=");

          Assert.ThrowsAny<Exception>(() => protector.Unprotect(tampered));
      }

      [Fact]
      public void Unprotect_WithWrongKey_Throws()
      {
          var enc = Create();
          var protectedValue = enc.Protect("secret");
          var other = Create("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb=");

          Assert.ThrowsAny<Exception>(() => other.Unprotect(protectedValue));
      }

      [Fact]
      public void Constructor_WithShortKey_Throws()
      {
          Assert.ThrowsAny<Exception>(() =>
              new AesGcmSecretProtector(Options.Create(new SecretProtectionOptions
              {
                  EncryptionKeyBase64 = Convert.ToBase64String(new byte[16]) // 16 bytes, not 32
              })));
      }
  }
  ```

- [ ] **Step 2: Run the test — expect FAIL (compile error).**
  ```bash
  dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~AesGcmSecretProtectorTests"
  ```
  Expected: build error — `SecretProtectionOptions` / `AesGcmSecretProtector` do not exist.

- [ ] **Step 3: Implement the options, the seam, and the implementation.**

  `src/AFK4.Platform.Api/Security/SecretProtectionOptions.cs`:
  ```csharp
  namespace AFK4.Platform.Api.Security;

  public sealed class SecretProtectionOptions
  {
      public const string SectionName = "Secrets";

      // Base64 of a 32-byte (256-bit) key. Supplied via environment/secret store, never committed.
      public string EncryptionKeyBase64 { get; set; } = string.Empty;
  }
  ```

  `src/AFK4.Platform.Api/Security/ISecretProtector.cs`:
  ```csharp
  namespace AFK4.Platform.Api.Security;

  // Encrypts/decrypts small secret strings (dcgate apiKey + webhook secret) for storage at rest.
  public interface ISecretProtector
  {
      string Protect(string plaintext);

      string Unprotect(string protectedValue);
  }
  ```

  `src/AFK4.Platform.Api/Security/AesGcmSecretProtector.cs`:
  ```csharp
  using System.Security.Cryptography;
  using System.Text;
  using Microsoft.Extensions.Options;

  namespace AFK4.Platform.Api.Security;

  // AES-256-GCM envelope. Format: "v1.<base64 nonce>.<base64 ciphertext>.<base64 tag>".
  // The version prefix lets the key be rotated later without breaking stored values.
  public sealed class AesGcmSecretProtector : ISecretProtector
  {
      private const string Version = "v1";
      private const int NonceSize = 12; // AES-GCM standard nonce
      private const int TagSize = 16;   // 128-bit auth tag

      private readonly byte[] key;

      public AesGcmSecretProtector(IOptions<SecretProtectionOptions> options)
      {
          var keyBase64 = options.Value.EncryptionKeyBase64;
          if (string.IsNullOrWhiteSpace(keyBase64))
          {
              throw new InvalidOperationException(
                  "Secrets:EncryptionKeyBase64 is not configured; secret protection is unavailable.");
          }

          key = Convert.FromBase64String(keyBase64);
          if (key.Length != 32)
          {
              throw new InvalidOperationException(
                  $"Secrets:EncryptionKeyBase64 must decode to 32 bytes, got {key.Length}.");
          }
      }

      public string Protect(string plaintext)
      {
          var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
          var nonce = new byte[NonceSize];
          RandomNumberGenerator.Fill(nonce);
          var ciphertext = new byte[plaintextBytes.Length];
          var tag = new byte[TagSize];

          using var aes = new AesGcm(key, TagSize);
          aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

          return string.Join('.',
              Version,
              Convert.ToBase64String(nonce),
              Convert.ToBase64String(ciphertext),
              Convert.ToBase64String(tag));
      }

      public string Unprotect(string protectedValue)
      {
          var parts = protectedValue.Split('.');
          if (parts.Length != 4 || parts[0] != Version)
          {
              throw new FormatException("Unrecognized protected-secret envelope.");
          }

          var nonce = Convert.FromBase64String(parts[1]);
          var ciphertext = Convert.FromBase64String(parts[2]);
          var tag = Convert.FromBase64String(parts[3]);
          var plaintextBytes = new byte[ciphertext.Length];

          using var aes = new AesGcm(key, TagSize);
          aes.Decrypt(nonce, ciphertext, tag, plaintextBytes); // throws CryptographicException on tamper/wrong key

          return Encoding.UTF8.GetString(plaintextBytes);
      }
  }
  ```

- [ ] **Step 4: Register in DI.**
  In `src/AFK4.Platform.Api/Program.cs`, immediately before the dcgate registration at line 267 (`builder.Services.Configure<DcGateOptions>...`), add:
  ```csharp
  builder.Services.Configure<SecretProtectionOptions>(
      builder.Configuration.GetSection(SecretProtectionOptions.SectionName));
  builder.Services.AddSingleton<ISecretProtector, AesGcmSecretProtector>();
  ```
  Add `using AFK4.Platform.Api.Security;` to the top of `Program.cs` (only if not already present).

- [ ] **Step 5: Run the test — expect PASS.**
  ```bash
  dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~AesGcmSecretProtectorTests"
  ```
  Expected: 5 passed.

- [ ] **Step 6: Commit.**
  ```bash
  git add -A && git commit -m "Add AES-256-GCM ISecretProtector for at-rest secret storage"
  ```

---

## Task 2 — `BranchPaymentGatewayEntity` + status constants + DbContext + migration

**Files:**
- Create: `src/AFK4.Platform.Api/Payments/BranchPaymentGatewayStatus.cs`
- Create: `src/AFK4.Platform.Api/Data/BranchPaymentGatewayEntity.cs`
- Modify: `src/AFK4.Platform.Api/Data/PlatformDbContext.cs`
- Create: `src/AFK4.Platform.Api/Data/Migrations/<timestamp>_AddBranchPaymentGateways.cs` (generated)
- Create test: `tests/AFK4.Platform.Api.Tests/BranchPaymentGatewayEntityTests.cs`

- [ ] **Step 1: Write the failing schema round-trip test.**
  Create `tests/AFK4.Platform.Api.Tests/BranchPaymentGatewayEntityTests.cs`:

  ```csharp
  using System;
  using System.Threading.Tasks;
  using AFK4.Platform.Api.Data;
  using AFK4.Platform.Api.Payments;
  using Microsoft.EntityFrameworkCore;
  using Microsoft.Extensions.DependencyInjection;
  using Xunit;

  namespace AFK4.Platform.Api.Tests;

  public class BranchPaymentGatewayEntityTests
  {
      [Fact]
      public async Task BranchPaymentGateway_PersistsAndReadsBack()
      {
          await using var factory = new PlatformApiFactory();
          var id = Guid.NewGuid();
          var orgId = Guid.NewGuid();
          var branchId = Guid.NewGuid();

          await using (var scope = factory.Services.CreateAsyncScope())
          {
              var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
              db.BranchPaymentGateways.Add(new BranchPaymentGatewayEntity
              {
                  BranchPaymentGatewayId = id,
                  OrganizationId = orgId,
                  BranchId = branchId,
                  DcgateProjectId = "proj_abc",
                  ApiKeyEncrypted = "v1.aaa.bbb.ccc",
                  WebhookSecretEncrypted = "v1.ddd.eee.fff",
                  CardLast4 = "1953",
                  Status = BranchPaymentGatewayStatus.PendingTelegram,
                  CreatedAtUtc = DateTimeOffset.UtcNow,
                  UpdatedAtUtc = DateTimeOffset.UtcNow
              });
              await db.SaveChangesAsync();
          }

          await using (var scope = factory.Services.CreateAsyncScope())
          {
              var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
              var stored = await db.BranchPaymentGateways.SingleAsync(g => g.BranchPaymentGatewayId == id);
              Assert.Equal(orgId, stored.OrganizationId);
              Assert.Equal(branchId, stored.BranchId);
              Assert.Equal("proj_abc", stored.DcgateProjectId);
              Assert.Equal("v1.aaa.bbb.ccc", stored.ApiKeyEncrypted);
              Assert.Equal("1953", stored.CardLast4);
              Assert.Equal("pending_telegram", stored.Status);
          }
      }

      [Fact]
      public async Task BranchPaymentGateway_AllowsNullBranchForOrgLevel()
      {
          await using var factory = new PlatformApiFactory();
          var id = Guid.NewGuid();

          await using var scope = factory.Services.CreateAsyncScope();
          var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
          db.BranchPaymentGateways.Add(new BranchPaymentGatewayEntity
          {
              BranchPaymentGatewayId = id,
              OrganizationId = Guid.NewGuid(),
              BranchId = null, // org-level
              DcgateProjectId = "proj_org",
              ApiKeyEncrypted = "v1.a.b.c",
              WebhookSecretEncrypted = "v1.d.e.f",
              CardLast4 = "0000",
              Status = BranchPaymentGatewayStatus.Active,
              CreatedAtUtc = DateTimeOffset.UtcNow,
              UpdatedAtUtc = DateTimeOffset.UtcNow
          });
          await db.SaveChangesAsync();

          var stored = await db.BranchPaymentGateways.SingleAsync(g => g.BranchPaymentGatewayId == id);
          Assert.Null(stored.BranchId);
      }
  }
  ```

- [ ] **Step 2: Run the test — expect FAIL (compile error).**
  ```bash
  dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~BranchPaymentGatewayEntityTests"
  ```
  Expected: build error — entity / status / DbSet missing.

- [ ] **Step 3: Add the status constants, the entity, and DbContext wiring.**

  `src/AFK4.Platform.Api/Payments/BranchPaymentGatewayStatus.cs`:
  ```csharp
  namespace AFK4.Platform.Api.Payments;

  // Lifecycle of a per-card dcgate gateway.
  public static class BranchPaymentGatewayStatus
  {
      // Project created in dcgate, but its Telegram session is not yet attached/online.
      public const string PendingTelegram = "pending_telegram";

      // Telegram attached and online; online top-up is allowed.
      public const string Active = "active";

      // Owner-disabled; outbound is refused but late inbound webhooks still verify.
      public const string Disabled = "disabled";
  }
  ```

  `src/AFK4.Platform.Api/Data/BranchPaymentGatewayEntity.cs`:
  ```csharp
  namespace AFK4.Platform.Api.Data;

  // One row per dcgate project (= one card). Bound to a branch; a null BranchId is the
  // org-level fallback used by branches that have no card of their own. The dcgate apiKey
  // (outbound) and webhook secret (inbound HMAC) are stored encrypted via ISecretProtector.
  public sealed class BranchPaymentGatewayEntity
  {
      public Guid BranchPaymentGatewayId { get; set; }

      public Guid OrganizationId { get; set; }

      // null => organization-level gateway (fallback for branches without their own card).
      public Guid? BranchId { get; set; }

      // dcgate project id; matches the x-dcgate-project-id webhook header.
      public string DcgateProjectId { get; set; } = string.Empty;

      public string ApiKeyEncrypted { get; set; } = string.Empty;

      public string WebhookSecretEncrypted { get; set; } = string.Empty;

      // Display only; the full card number lives in dcgate, never here.
      public string CardLast4 { get; set; } = string.Empty;

      public string Status { get; set; } = string.Empty;

      public DateTimeOffset CreatedAtUtc { get; set; }

      public DateTimeOffset UpdatedAtUtc { get; set; }
  }
  ```

  In `src/AFK4.Platform.Api/Data/PlatformDbContext.cs`, add the DbSet next to `Branches` (after line 9):
  ```csharp
      public DbSet<BranchPaymentGatewayEntity> BranchPaymentGateways => Set<BranchPaymentGatewayEntity>();
  ```
  Add a config block in `OnModelCreating` (mirror the `PaymentIntentEntity` block at line 770):
  ```csharp
          modelBuilder.Entity<BranchPaymentGatewayEntity>(entity =>
          {
              entity.ToTable("branch_payment_gateways");
              entity.HasKey(gateway => gateway.BranchPaymentGatewayId);
              entity.Property(gateway => gateway.DcgateProjectId).HasMaxLength(128).IsRequired();
              entity.Property(gateway => gateway.ApiKeyEncrypted).HasMaxLength(1024).IsRequired();
              entity.Property(gateway => gateway.WebhookSecretEncrypted).HasMaxLength(1024).IsRequired();
              entity.Property(gateway => gateway.CardLast4).HasMaxLength(4).IsRequired();
              entity.Property(gateway => gateway.Status).HasMaxLength(32).IsRequired();
              entity.HasIndex(gateway => gateway.DcgateProjectId).IsUnique();
              entity.HasIndex(gateway => new { gateway.OrganizationId, gateway.BranchId });
          });
  ```

- [ ] **Step 4: Generate the EF migration.**
  ```bash
  dotnet ef migrations add AddBranchPaymentGateways \
    --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj \
    --output-dir Data/Migrations
  ```
  Verify the generated `Up` has `CreateTable("branch_payment_gateways", ...)` with all columns (`BranchId` nullable), a unique index on `DcgateProjectId`, and an index on `(OrganizationId, BranchId)`. If `dotnet ef` is unavailable, hand-write the migration mirroring `20260603094045_AddPaymentIntents.cs` (table-create style), namespace `AFK4.Platform.Api.Data.Migrations`, `#nullable disable`, and update `PlatformDbContextModelSnapshot.cs` to match (the snapshot must be consistent or the build/tests will fail).

- [ ] **Step 5: Run the test — expect PASS.**
  ```bash
  dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~BranchPaymentGatewayEntityTests"
  ```
  Expected: 2 passed.

- [ ] **Step 6: Commit.**
  ```bash
  git add -A && git commit -m "Add branch_payment_gateways table for per-card dcgate credentials"
  ```

---

## Task 3 — `IBranchPaymentGatewayResolver` (branch→org fallback, by-project-id) + DI

**Files:**
- Create: `src/AFK4.Platform.Api/Payments/IBranchPaymentGatewayResolver.cs`
- Create: `src/AFK4.Platform.Api/Payments/EfBranchPaymentGatewayResolver.cs`
- Create test: `tests/AFK4.Platform.Api.Tests/BranchPaymentGatewayResolverTests.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs` (registration)

- [ ] **Step 1: Write the failing resolver tests.**
  Create `tests/AFK4.Platform.Api.Tests/BranchPaymentGatewayResolverTests.cs`:

  ```csharp
  using System;
  using System.Threading;
  using System.Threading.Tasks;
  using AFK4.Platform.Api.Data;
  using AFK4.Platform.Api.Payments;
  using Microsoft.Extensions.DependencyInjection;
  using Xunit;

  namespace AFK4.Platform.Api.Tests;

  public class BranchPaymentGatewayResolverTests
  {
      private static BranchPaymentGatewayEntity Gateway(
          Guid orgId, Guid? branchId, string projectId, string status) =>
          new()
          {
              BranchPaymentGatewayId = Guid.NewGuid(),
              OrganizationId = orgId,
              BranchId = branchId,
              DcgateProjectId = projectId,
              ApiKeyEncrypted = "v1.a.b.c",
              WebhookSecretEncrypted = "v1.d.e.f",
              CardLast4 = "0001",
              Status = status,
              CreatedAtUtc = DateTimeOffset.UtcNow,
              UpdatedAtUtc = DateTimeOffset.UtcNow
          };

      private static async Task SeedAsync(PlatformApiFactory factory, params BranchPaymentGatewayEntity[] rows)
      {
          await using var scope = factory.Services.CreateAsyncScope();
          var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
          db.BranchPaymentGateways.AddRange(rows);
          await db.SaveChangesAsync();
      }

      private static async Task<T> WithResolver<T>(
          PlatformApiFactory factory, Func<IBranchPaymentGatewayResolver, Task<T>> act)
      {
          await using var scope = factory.Services.CreateAsyncScope();
          var resolver = scope.ServiceProvider.GetRequiredService<IBranchPaymentGatewayResolver>();
          return await act(resolver);
      }

      [Fact]
      public async Task ResolveForBranch_PrefersBranchSpecificActiveGateway()
      {
          await using var factory = new PlatformApiFactory();
          var org = Guid.NewGuid();
          var branch = Guid.NewGuid();
          await SeedAsync(factory,
              Gateway(org, null, "proj_org", BranchPaymentGatewayStatus.Active),
              Gateway(org, branch, "proj_branch", BranchPaymentGatewayStatus.Active));

          var result = await WithResolver(factory, r => r.ResolveForBranchAsync(org, branch, CancellationToken.None));

          Assert.NotNull(result);
          Assert.Equal("proj_branch", result!.DcgateProjectId);
      }

      [Fact]
      public async Task ResolveForBranch_FallsBackToOrgLevelGateway()
      {
          await using var factory = new PlatformApiFactory();
          var org = Guid.NewGuid();
          var branch = Guid.NewGuid();
          await SeedAsync(factory, Gateway(org, null, "proj_org", BranchPaymentGatewayStatus.Active));

          var result = await WithResolver(factory, r => r.ResolveForBranchAsync(org, branch, CancellationToken.None));

          Assert.NotNull(result);
          Assert.Equal("proj_org", result!.DcgateProjectId);
      }

      [Fact]
      public async Task ResolveForBranch_IgnoresNonActiveAndForeignOrg()
      {
          await using var factory = new PlatformApiFactory();
          var org = Guid.NewGuid();
          var branch = Guid.NewGuid();
          await SeedAsync(factory,
              Gateway(org, branch, "proj_disabled", BranchPaymentGatewayStatus.Disabled),
              Gateway(Guid.NewGuid(), null, "proj_other_org", BranchPaymentGatewayStatus.Active));

          var result = await WithResolver(factory, r => r.ResolveForBranchAsync(org, branch, CancellationToken.None));

          Assert.Null(result);
      }

      [Fact]
      public async Task ResolveByProjectId_ReturnsRowRegardlessOfStatus()
      {
          await using var factory = new PlatformApiFactory();
          var org = Guid.NewGuid();
          await SeedAsync(factory, Gateway(org, null, "proj_late", BranchPaymentGatewayStatus.Disabled));

          var result = await WithResolver(factory, r => r.ResolveByProjectIdAsync("proj_late", CancellationToken.None));

          Assert.NotNull(result);
          Assert.Equal(org, result!.OrganizationId);
      }

      [Fact]
      public async Task ResolveByProjectId_UnknownReturnsNull()
      {
          await using var factory = new PlatformApiFactory();

          var result = await WithResolver(factory, r => r.ResolveByProjectIdAsync("nope", CancellationToken.None));

          Assert.Null(result);
      }
  }
  ```

- [ ] **Step 2: Run the test — expect FAIL (compile error).**
  ```bash
  dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~BranchPaymentGatewayResolverTests"
  ```
  Expected: build error — resolver types missing.

- [ ] **Step 3: Implement the resolver.**

  `src/AFK4.Platform.Api/Payments/IBranchPaymentGatewayResolver.cs`:
  ```csharp
  using AFK4.Platform.Api.Data;

  namespace AFK4.Platform.Api.Payments;

  public interface IBranchPaymentGatewayResolver
  {
      // Outbound: the active gateway for a branch, falling back to the org-level (null-branch) gateway.
      // Returns null when the branch has no usable online-payment gateway.
      Task<BranchPaymentGatewayEntity?> ResolveForBranchAsync(
          Guid organizationId, Guid branchId, CancellationToken cancellationToken);

      // Inbound: the gateway that owns a dcgate project id (any status), or null if unknown.
      Task<BranchPaymentGatewayEntity?> ResolveByProjectIdAsync(
          string dcgateProjectId, CancellationToken cancellationToken);
  }
  ```

  `src/AFK4.Platform.Api/Payments/EfBranchPaymentGatewayResolver.cs`:
  ```csharp
  using AFK4.Platform.Api.Data;
  using Microsoft.EntityFrameworkCore;

  namespace AFK4.Platform.Api.Payments;

  public sealed class EfBranchPaymentGatewayResolver(PlatformDbContext dbContext)
      : IBranchPaymentGatewayResolver
  {
      public async Task<BranchPaymentGatewayEntity?> ResolveForBranchAsync(
          Guid organizationId, Guid branchId, CancellationToken cancellationToken)
      {
          var branchGateway = await dbContext.BranchPaymentGateways
              .AsNoTracking()
              .SingleOrDefaultAsync(
                  gateway => gateway.OrganizationId == organizationId
                      && gateway.BranchId == branchId
                      && gateway.Status == BranchPaymentGatewayStatus.Active,
                  cancellationToken);
          if (branchGateway is not null)
          {
              return branchGateway;
          }

          return await dbContext.BranchPaymentGateways
              .AsNoTracking()
              .SingleOrDefaultAsync(
                  gateway => gateway.OrganizationId == organizationId
                      && gateway.BranchId == null
                      && gateway.Status == BranchPaymentGatewayStatus.Active,
                  cancellationToken);
      }

      public async Task<BranchPaymentGatewayEntity?> ResolveByProjectIdAsync(
          string dcgateProjectId, CancellationToken cancellationToken) =>
          await dbContext.BranchPaymentGateways
              .AsNoTracking()
              .SingleOrDefaultAsync(
                  gateway => gateway.DcgateProjectId == dcgateProjectId, cancellationToken);
  }
  ```

- [ ] **Step 4: Register in DI.**
  In `src/AFK4.Platform.Api/Program.cs`, with the other scoped services (near line 265), add:
  ```csharp
  builder.Services.AddScoped<IBranchPaymentGatewayResolver, EfBranchPaymentGatewayResolver>();
  ```
  Add `using AFK4.Platform.Api.Payments;` to the top of `Program.cs` (only if not already present).

- [ ] **Step 5: Run the test — expect PASS.**
  ```bash
  dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~BranchPaymentGatewayResolverTests"
  ```
  Expected: 5 passed.

- [ ] **Step 6: Commit.**
  ```bash
  git add -A && git commit -m "Add branch payment gateway resolver (branch->org fallback, by-project-id)"
  ```

---

## Task 4 — Per-apiKey `IDcGateClientFactory` + refactor `DcGateClient` ctor

**Files:**
- Modify: `src/AFK4.Platform.Api/Payments/DcGate/DcGateClient.cs`
- Create: `src/AFK4.Platform.Api/Payments/DcGate/IDcGateClientFactory.cs`
- Create: `src/AFK4.Platform.Api/Payments/DcGate/DcGateClientFactory.cs`
- Modify: `src/AFK4.Platform.Api/Payments/DcGate/DcGateOptions.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs` (replace singleton client registration)
- Modify: `tests/AFK4.Platform.Api.Tests/DcGateClientTests.cs` (new ctor)

- [ ] **Step 1: Update the client unit test to the new ctor (this is the failing test).**
  In `tests/AFK4.Platform.Api.Tests/DcGateClientTests.cs`, the helper currently builds the client with `Options.Create(new DcGateOptions { ... ApiKey = "test-api-key" ... })`. Change the `CreateClient` helper so the apiKey is passed to the ctor directly. Replace the helper with:
  ```csharp
      private static DcGateClient CreateClient(StubHandler handler) =>
          new(
              new HttpClient(handler) { BaseAddress = new Uri("https://dcgate.example") },
              apiKey: "test-api-key");
  ```
  Remove the now-unused `using Microsoft.Extensions.Options;` if it is no longer referenced anywhere in the file. All existing assertions (Bearer == "test-api-key", "/api/payments", amount formatting, throw-on-error) stay unchanged.

- [ ] **Step 2: Run the test — expect FAIL (compile error).**
  ```bash
  dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~DcGateClientTests"
  ```
  Expected: build error — `DcGateClient` has no `(HttpClient, string)` ctor.

- [ ] **Step 3: Refactor `DcGateClient`, add the factory, trim options.**

  In `src/AFK4.Platform.Api/Payments/DcGate/DcGateClient.cs`, replace the `IOptions<DcGateOptions>` dependency with a plain apiKey:
  ```csharp
  using System.Globalization;
  using System.Net.Http.Headers;
  using System.Net.Http.Json;

  namespace AFK4.Platform.Api.Payments.DcGate;

  public sealed class DcGateClient : IDcGateClient
  {
      private readonly HttpClient httpClient;
      private readonly string apiKey;

      public DcGateClient(HttpClient httpClient, string apiKey)
      {
          this.httpClient = httpClient;
          this.apiKey = apiKey;
      }

      public async Task<DcGatePaymentResult> CreatePaymentAsync(
          long amountMinorUnits,
          string currencyCode,
          string externalOrderId,
          object metadata,
          CancellationToken cancellationToken)
      {
          using var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments")
          {
              Content = JsonContent.Create(new
              {
                  amount = ToMajorUnitString(amountMinorUnits),
                  externalOrderId,
                  metadata
              })
          };
          request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

          using var response = await httpClient.SendAsync(request, cancellationToken);
          response.EnsureSuccessStatusCode();

          var result = await response.Content.ReadFromJsonAsync<DcGatePaymentResult>(cancellationToken);
          return result ?? throw new HttpRequestException("dcgate returned an empty payment body.");
      }

      // Money stays long minor units inside AFK4; dcgate's wire format is a
      // major-unit decimal string. This boundary is the ONLY place we convert.
      private static string ToMajorUnitString(long minorUnits) =>
          (minorUnits / 100m).ToString("0.00", CultureInfo.InvariantCulture);
  }
  ```

  `src/AFK4.Platform.Api/Payments/DcGate/IDcGateClientFactory.cs`:
  ```csharp
  namespace AFK4.Platform.Api.Payments.DcGate;

  // Builds a dcgate client bound to a specific project apiKey, over the shared
  // platform base-URL HttpClient pool.
  public interface IDcGateClientFactory
  {
      IDcGateClient CreateForApiKey(string apiKey);
  }
  ```

  `src/AFK4.Platform.Api/Payments/DcGate/DcGateClientFactory.cs`:
  ```csharp
  namespace AFK4.Platform.Api.Payments.DcGate;

  public sealed class DcGateClientFactory(IHttpClientFactory httpClientFactory) : IDcGateClientFactory
  {
      // Named client configured in Program.cs with the platform dcgate BaseAddress.
      public const string HttpClientName = "dcgate";

      public IDcGateClient CreateForApiKey(string apiKey) =>
          new DcGateClient(httpClientFactory.CreateClient(HttpClientName), apiKey);
  }
  ```

  In `src/AFK4.Platform.Api/Payments/DcGate/DcGateOptions.cs`, remove the `ApiKey` and `WebhookSecret` properties (they are per-project now); keep only `BaseUrl`:
  ```csharp
  namespace AFK4.Platform.Api.Payments.DcGate;

  public sealed class DcGateOptions
  {
      public const string SectionName = "DcGate";

      // dcgate base URL, e.g. https://dcgate.mubi.dev
      public string BaseUrl { get; set; } = string.Empty;
  }
  ```

- [ ] **Step 4: Replace the DI registration.**
  In `src/AFK4.Platform.Api/Program.cs`, replace the block at lines 267-275 (`Configure<DcGateOptions>` + `AddHttpClient<IDcGateClient, DcGateClient>`) with a **named** HttpClient + the factory:
  ```csharp
  builder.Services.Configure<DcGateOptions>(builder.Configuration.GetSection(DcGateOptions.SectionName));
  builder.Services.AddHttpClient(DcGateClientFactory.HttpClientName, (provider, http) =>
  {
      var opts = provider.GetRequiredService<IOptions<DcGateOptions>>().Value;
      if (!string.IsNullOrWhiteSpace(opts.BaseUrl))
      {
          http.BaseAddress = new Uri(opts.BaseUrl);
      }
  });
  builder.Services.AddSingleton<IDcGateClientFactory, DcGateClientFactory>();
  ```
  (The `SecretProtector` / resolver registrations from Tasks 1 and 3 stay.)

- [ ] **Step 5: Run the test — expect PASS.**
  ```bash
  dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~DcGateClientTests"
  ```
  Expected: 6 passed (the previous count is preserved).

- [ ] **Step 6: Commit.**
  ```bash
  git add -A && git commit -m "Add per-apiKey DcGate client factory; drop global apiKey/webhook secret from options"
  ```

---

## Task 5 — Rewire outbound top-up to resolve the per-branch gateway (+ gating)

**Files:**
- Modify: `src/AFK4.Platform.Api/Program.cs` (the `/api/me/wallet/top-up-intent` endpoint)
- Modify: `tests/AFK4.Platform.Api.Tests/PlatformApiFactory.cs` (default test encryption key)
- Modify: `tests/AFK4.Platform.Api.Tests/DcGateTopUpIntentTests.cs` (fake factory + seed gateway)

- [ ] **Step 1: Add a default test encryption key to the factory.**
  In `tests/AFK4.Platform.Api.Tests/PlatformApiFactory.cs`, inside `ConfigureWebHost`'s `ConfigureServices`, just before `extraServices?.Invoke(services);` (line 68), add:
  ```csharp
            services.PostConfigure<AFK4.Platform.Api.Security.SecretProtectionOptions>(options =>
            {
                // Throwaway 32-byte (all-zero) key, base64. Tests only.
                options.EncryptionKeyBase64 = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
            });
  ```

- [ ] **Step 2: Update the outbound test to the new wiring (failing test).**
  In `tests/AFK4.Platform.Api.Tests/DcGateTopUpIntentTests.cs`:

  (a) Change the fake to implement `IDcGateClientFactory` returning a captured fake client. Replace the existing `FakeDcGateClient` private class and the `CreateGatewayFactory` helper with:
  ```csharp
      private sealed class FakeDcGateClient : IDcGateClient
      {
          public int Calls { get; private set; }
          public string? LastExternalOrderId { get; private set; }
          public long LastAmountMinor { get; private set; }

          public Task<DcGatePaymentResult> CreatePaymentAsync(
              long amountMinorUnits, string currencyCode, string externalOrderId,
              object metadata, System.Threading.CancellationToken cancellationToken)
          {
              Calls++;
              LastExternalOrderId = externalOrderId;
              LastAmountMinor = amountMinorUnits;
              return Task.FromResult(new DcGatePaymentResult(
                  PaymentId: "pay_fake",
                  Status: "pending",
                  Amount: "50.00",
                  Currency: "TJS",
                  Comment: "AFK4-CMT-0001",
                  ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(15),
                  PayUrl: "http://pay.dc.tj/?A=1&s=50.00&c=cmt"));
          }
      }

      private sealed class FakeDcGateClientFactory(IDcGateClient client) : IDcGateClientFactory
      {
          public IDcGateClient CreateForApiKey(string apiKey) => client;
      }

      private static PlatformApiFactory CreateGatewayFactory(IDcGateClient fake) =>
          new PlatformApiFactory(extraServices: services =>
          {
              services.RemoveAll<IDcGateClientFactory>();
              services.AddSingleton<IDcGateClientFactory>(new FakeDcGateClientFactory(fake));
          });
  ```
  Ensure the file's usings include `using AFK4.Platform.Api.Payments;`, `using AFK4.Platform.Api.Payments.DcGate;`, `using Microsoft.Extensions.DependencyInjection;`, and `using Microsoft.Extensions.DependencyInjection.Extensions;`.

  (b) Add a helper that seeds an active gateway for the player's branch, and adjust the existing dcgate-path test to seed it. The player seeding helper in this file creates the account with a `HomeBranchId`; capture that branch id and seed a matching gateway. Add this helper:
  ```csharp
      private static async Task SeedActiveGatewayAsync(PlatformApiFactory factory, Guid orgId, Guid branchId)
      {
          await using var scope = factory.Services.CreateAsyncScope();
          var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
          var protector = scope.ServiceProvider.GetRequiredService<AFK4.Platform.Api.Security.ISecretProtector>();
          db.BranchPaymentGateways.Add(new BranchPaymentGatewayEntity
          {
              BranchPaymentGatewayId = Guid.NewGuid(),
              OrganizationId = orgId,
              BranchId = branchId,
              DcgateProjectId = "proj_test",
              ApiKeyEncrypted = protector.Protect("dcg_test_api_key"),
              WebhookSecretEncrypted = protector.Protect("test-webhook-secret"),
              CardLast4 = "1953",
              Status = BranchPaymentGatewayStatus.Active,
              CreatedAtUtc = DateTimeOffset.UtcNow,
              UpdatedAtUtc = DateTimeOffset.UtcNow
          });
          await db.SaveChangesAsync();
      }
  ```
  In the existing test that posts `Method = "dcgate"` and expects success, after seeding the player (which yields the org id and the account's `HomeBranchId`), call `await SeedActiveGatewayAsync(factory, orgId, branchId);` before posting. (Read the existing player-seed helper to get the exact org/branch values it uses; seed the gateway with those same ids.)

  (c) Add a new test that a dcgate top-up on a branch **without** a gateway is refused and never calls dcgate:
  ```csharp
      [Fact]
      public async Task TopUpIntent_DcGateWithoutGateway_ReturnsUnavailableAndDoesNotCallGateway()
      {
          var fake = new FakeDcGateClient();
          await using var factory = CreateGatewayFactory(fake);
          // seed player but NO gateway row
          var seeded = await SeedPlayerAsync(factory); // use the file's existing player-seed helper
          using var client = factory.CreateClient();
          await AuthenticateAsync(client, seeded); // use the file's existing auth helper

          var response = await client.PostAsJsonAsync(
              "/api/me/wallet/top-up-intent",
              new PlayerTopUpIntentRequest(5_000, "TJS", "dcgate"));

          Assert.Equal(System.Net.HttpStatusCode.Conflict, response.StatusCode);
          Assert.Equal(0, fake.Calls);
      }
  ```
  Adapt `SeedPlayerAsync` / `AuthenticateAsync` calls to the exact helper signatures already in this test file (do not invent new ones — reuse what the dcgate-success test uses).

- [ ] **Step 3: Run the test — expect FAIL.**
  ```bash
  dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~DcGateTopUpIntentTests"
  ```
  Expected: failures — the endpoint still injects `IDcGateClient` directly and has no gateway gating.

- [ ] **Step 4: Rewire the outbound endpoint.**
  In `src/AFK4.Platform.Api/Program.cs`, in `app.MapPost("/api/me/wallet/top-up-intent", ...)`:

  (a) Change the injected parameter (line 965) from `IDcGateClient dcGateClient` to:
  ```csharp
      IDcGateClientFactory dcGateClientFactory,
      IBranchPaymentGatewayResolver gatewayResolver,
      ISecretProtector secretProtector,
  ```

  (b) Replace the `if (method == "dcgate") { ... }` block (lines 1016-1028) with a gateway-resolving version:
  ```csharp
      if (method == "dcgate")
      {
          var gateway = await gatewayResolver.ResolveForBranchAsync(
              intent.OrganizationId, intent.BranchId, cancellationToken);
          if (gateway is null)
          {
              return Results.Json(
                  new { Error = "online_payment_unavailable" },
                  statusCode: StatusCodes.Status409Conflict);
          }

          var apiKey = secretProtector.Unprotect(gateway.ApiKeyEncrypted);
          var dcGateClient = dcGateClientFactory.CreateForApiKey(apiKey);

          var payment = await dcGateClient.CreatePaymentAsync(
              intent.AmountMinorUnits,
              intent.CurrencyCode,
              intent.PaymentIntentId.ToString("N"),
              new { playerAccountId = intent.PlayerAccountId, branchId = intent.BranchId },
              cancellationToken);
          intent.GatewayPaymentId = payment.PaymentId;
          intent.GatewayPayUrl = payment.PayUrl;
          intent.GatewayComment = payment.Comment;
          intent.GatewayExpiresAtUtc = payment.ExpiresAt;
      }
  ```
  Leave the intent construction, the counter path, the `PaymentIntents.Add` + `SaveChanges`, and the `PlayerTopUpIntentDto` response untouched.

- [ ] **Step 5: Run the test — expect PASS.**
  ```bash
  dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~DcGateTopUpIntentTests"
  ```
  Expected: all DcGateTopUpIntentTests pass (dcgate path with a seeded gateway, counter path unchanged, no-gateway → 409).

- [ ] **Step 6: Commit.**
  ```bash
  git add -A && git commit -m "Resolve per-branch dcgate gateway for online top-up; gate when none active"
  ```

---

## Task 6 — Rewire inbound webhook to per-project secret + config cleanup

**Files:**
- Modify: `src/AFK4.Platform.Api/Program.cs` (the webhook endpoint)
- Modify: `src/AFK4.Platform.Api/appsettings.json`
- Modify: `tests/AFK4.Platform.Api.Tests/DcGateWebhookEndpointTests.cs` (seed gateway + per-project secret)

- [ ] **Step 1: Update the webhook tests to per-project secret (failing test).**
  In `tests/AFK4.Platform.Api.Tests/DcGateWebhookEndpointTests.cs`:

  (a) The webhook test factory currently sets a global `DcGateOptions.WebhookSecret` via `extraServices` (lines 24-27). Replace that factory helper with a plain `new PlatformApiFactory()` (the secret now lives on a seeded gateway row, signed with the same known constant). Keep the `private const string WebhookSecret = "test-webhook-secret";`.

  (b) In `SeedDcGateIntentAsync` (which seeds a player + a dcgate intent at `TestIds.BranchId`), also seed a matching gateway row whose `DcgateProjectId` equals the project id the signed request sends in the `x-dcgate-project-id` header (the existing `SignedRequest` helper sets that header — confirm its value; the Unit 2 tests use `"afk4"`). Seed it encrypted with the app's real protector so the endpoint can decrypt it:
  ```csharp
      private static async Task SeedGatewayAsync(PlatformApiFactory factory, string projectId, string webhookSecret)
      {
          await using var scope = factory.Services.CreateAsyncScope();
          var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
          var protector = scope.ServiceProvider.GetRequiredService<AFK4.Platform.Api.Security.ISecretProtector>();
          db.BranchPaymentGateways.Add(new BranchPaymentGatewayEntity
          {
              BranchPaymentGatewayId = Guid.NewGuid(),
              OrganizationId = TestIds.OrganizationId,
              BranchId = TestIds.BranchId,
              DcgateProjectId = projectId,
              ApiKeyEncrypted = protector.Protect("dcg_test_api_key"),
              WebhookSecretEncrypted = protector.Protect(webhookSecret),
              CardLast4 = "1953",
              Status = AFK4.Platform.Api.Payments.BranchPaymentGatewayStatus.Active,
              CreatedAtUtc = DateTimeOffset.UtcNow,
              UpdatedAtUtc = DateTimeOffset.UtcNow
          });
          await db.SaveChangesAsync();
      }
  ```
  Call `await SeedGatewayAsync(factory, "afk4", WebhookSecret);` in each test right after seeding the intent (use the exact project-id string the `SignedRequest` header uses). Add usings `using AFK4.Platform.Api.Data;` and `using Microsoft.Extensions.DependencyInjection;` if missing.

  (c) Add one new test: a valid signature but for an **unknown project id** → 401 and no credit. Build a signed request whose `x-dcgate-project-id` header is `"unknown_project"` (sign with any secret); assert `HttpStatusCode.Unauthorized`. (If the `SignedRequest` helper hard-codes the project-id header, add an overload/parameter to set it for this one test.)

  The existing tests (valid paid credits once, replay no double-credit, bad/missing signature 401, unknown order 200, expired) stay — they now additionally seed the gateway.

- [ ] **Step 2: Run the tests — expect FAIL.**
  ```bash
  dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~DcGateWebhookEndpointTests"
  ```
  Expected: failures — the endpoint still verifies against the (now-removed) global `DcGateOptions.WebhookSecret`.

- [ ] **Step 3: Rewire the webhook endpoint.**
  In `src/AFK4.Platform.Api/Program.cs`, in `app.MapPost("/api/public/payments/dcgate/webhook", ...)`:

  (a) Change the injected parameter `IOptions<DcGateOptions> dcGateOptions` (line 699) to:
  ```csharp
      IBranchPaymentGatewayResolver gatewayResolver,
      ISecretProtector secretProtector,
  ```

  (b) Replace the body's signature step (lines 704-717) — which reads `var options = dcGateOptions.Value;` and verifies with `options.WebhookSecret` — with a per-project resolution. Keep the raw-body read exactly as is, then:
  ```csharp
      httpRequest.EnableBuffering();
      string rawBody;
      using (var reader = new StreamReader(httpRequest.Body, Encoding.UTF8, leaveOpen: true))
      {
          rawBody = await reader.ReadToEndAsync(cancellationToken);
      }
      httpRequest.Body.Position = 0;

      if (!httpRequest.Headers.TryGetValue("x-dcgate-project-id", out var projectIdHeader)
          || string.IsNullOrWhiteSpace(projectIdHeader.ToString()))
      {
          return Results.Unauthorized();
      }

      var gateway = await gatewayResolver.ResolveByProjectIdAsync(
          projectIdHeader.ToString(), cancellationToken);
      if (gateway is null)
      {
          return Results.Unauthorized();
      }

      var webhookSecret = secretProtector.Unprotect(gateway.WebhookSecretEncrypted);
      if (!DcGateSignatureIsValid(httpRequest, rawBody, webhookSecret))
      {
          return Results.Unauthorized();
      }
  ```
  Everything below the signature check (deserialize, `EventId` idempotency, intent lookup, `payment.paid/expired/disputed` switch, `TopUpWalletAsync` credit, race-safe event insert, returns) stays byte-for-byte unchanged.

- [ ] **Step 4: Clean up config.**
  In `src/AFK4.Platform.Api/appsettings.json`, change the `DcGate` section to keep only `BaseUrl`, and add a `Secrets` placeholder:
  ```json
    "DcGate": {
      "BaseUrl": ""
    },
    "Secrets": {
      "EncryptionKeyBase64": ""
    }
  ```
  (Add `DcGate:AdminSecret` later in Subsystem B; not needed for A.) If any other `appsettings.*.json` files set `DcGate:ApiKey`/`DcGate:WebhookSecret`, remove those keys too.

- [ ] **Step 5: Run the tests — expect PASS.**
  ```bash
  dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~DcGateWebhookEndpointTests"
  ```
  Expected: all webhook tests pass, including the new unknown-project → 401.

- [ ] **Step 6: Commit.**
  ```bash
  git add -A && git commit -m "Verify dcgate webhook with per-project secret resolved by x-dcgate-project-id"
  ```

---

## Verification gate

The full Platform.Api suite must stay green and the API must build warnings-as-errors clean:
```bash
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj
dotnet build src/AFK4.Platform.Api/AFK4.Platform.Api.csproj
```
Expected: 0 failures (the suite was 963 passing before this plan; this plan changes wiring and adds ~12 tests — the net count rises and stays green), and `Build succeeded, 0 warnings`.

Confirm no leftover references to the removed options:
```bash
grep -rn "DcGateOptions" src/AFK4.Platform.Api | grep -iE "ApiKey|WebhookSecret"
```
Expected: no matches.

## Notes for downstream

- **Subsystem B** (owner cabinet onboarding) writes `BranchPaymentGatewayEntity` rows: it calls dcgate `POST /api/admin/projects`, then persists a row (`Status = pending_telegram`, encrypting the returned apiKey + the generated webhook secret via `ISecretProtector`, storing `CardLast4`). B adds a `StaffPermissionNames.ManagePaymentGateways` permission + Owner-gated endpoints, and flips `Status` to `active` after the Telegram attach (Subsystem C). It also needs `DcGate:AdminSecret` in config.
- **Gating beyond `active`**: this plan gates online top-up on a gateway being `active`. The "Telegram online" sub-check (from the design's gating rule) depends on Subsystem C's status endpoint and is layered in with B; A's `active` status is the coarse gate until then.
- The encryption key (`Secrets:EncryptionKeyBase64`) must be provisioned in each environment (32 random bytes, base64) before online payments work; a missing/short key fails `AesGcmSecretProtector` construction fast.
