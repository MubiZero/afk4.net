# Customer Shell — Unit 2: dcgate Online Self-Top-Up Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add real online self-top-up to AFK4 via the deployed `dcgate` DC-Bank payment gateway. A player asks for an online top-up at `POST /api/me/wallet/top-up-intent`; AFK4 creates a `dcgate` payment, returns a `payUrl` the shell renders as a QR, and credits the wallet exactly once when dcgate POSTs back a signed `payment.paid` webhook — reusing the existing audited `TopUpWalletAsync` with the intent id as the idempotency key, so the operator-confirm path and the gateway path can never double-credit.

**Architecture:** One vertical slice over the existing `PaymentIntentEntity`, all TDD-first against EF InMemory.
- **Gateway columns:** add four nullable columns to `PaymentIntentEntity` (`GatewayPaymentId`, `GatewayPayUrl`, `GatewayComment`, `GatewayExpiresAtUtc`) + EF migration. `Method` becomes `"dcgate"` for online intents; `"counter"` stays the default and untouched path.
- **Consumer client:** `IDcGateClient` / `DcGateClient` — a typed `HttpClient` that POSTs `/api/payments` with `Authorization: Bearer <ApiKey>`, converting `long` minor units → a `"X.XX"` major-unit string only at this HTTP boundary. Config from `DcGateOptions` (`BaseUrl`, `ApiKey`, `WebhookSecret`) bound from the `DcGate` configuration section.
- **Intent creation goes optionally online:** `PlayerTopUpIntentRequest` gains a `Method` field (default `"counter"`). When `"dcgate"`, the endpoint creates the intent, calls `IDcGateClient.CreatePaymentAsync` with `externalOrderId = PaymentIntentId.ToString("N")` and `metadata = { playerAccountId, branchId }`, persists `payUrl`/`comment`/`gatewayPaymentId`/`expiresAt`, and returns them in `PlayerTopUpIntentDto`. The `"counter"` path is byte-for-byte unchanged.
- **Webhook:** `POST /api/public/payments/dcgate/webhook` reads the RAW body, verifies `x-dcgate-signature: sha256=<HMAC-SHA256(rawBody, WebhookSecret)>` with a constant-time compare (401 on bad/missing), dedupes by `eventId` via a new `dcgate_webhook_events` table, and on `payment.paid` finds the intent by `externalOrderId`, credits via `TopUpWalletAsync` (idempotency key = intent id `"N"`, same as the operator path), and flips the intent to `fulfilled`. `payment.expired` → `expired`; `payment.disputed` → leave pending and set a `Disputed` flag for the operator. Rate-limited `player-public`.

**Tech Stack:** ASP.NET Core minimal APIs, EF Core 10 (Postgres in prod, InMemory for tests), `dotnet ef` 10.x for the migration, xUnit 2.9 + `WebApplicationFactory<Program>` (`PlatformApiFactory`). `TreatWarningsAsErrors=true` — add only usings you actually use. Money stays `long` minor units end-to-end; convert to a major-unit string ONLY inside `DcGateClient`.

**Key findings (verified ground truth 2026-06-03):**
- `TopUpWalletAsync` (`src/AFK4.Platform.Api/Billing/EfBillingCommandService.cs`) **requires an open shift** (`RequireOpenShiftAsync`) and dedupes internally on `request.IdempotencyKey`. The webhook credit therefore needs an open shift at the intent's branch, exactly like the operator `fulfil` path. If no shift is open the credit returns a non-success `BillingCommandServiceResult` — the webhook must NOT flip the intent in that case and must surface a retryable result so dcgate redelivers. Tests seed an open shift just like `FulfilIntent_*` tests do.
- `LedgerEntryEntity.CreatedByStaffUserId` is a non-null `Guid`. The webhook has no staff actor, so it passes `Guid.Empty` (system actor) — same shape the operator path uses but with the real staff id.
- Existing fulfil idempotency key is `intent.PaymentIntentId.ToString("N")` (`Program.cs` ~line 1141). The webhook MUST reuse the identical key so gateway-confirm and operator-confirm collapse to one ledger entry.
- Options binding convention: `builder.Services.Configure<TOptions>(builder.Configuration.GetSection(TOptions.SectionName))` (see `OwnerCodeOptions`, `NotificationOptions` registrations in `Program.cs` ~lines 180/203).
- Migrations live under `src/AFK4.Platform.Api/Data/Migrations/`, namespace `AFK4.Platform.Api.Data.Migrations`, `#nullable disable`, table name `payment_intents`. Column-add style: see `20260601043751_AddBranchPreferredLocale.cs`.
- `PlatformDbContext` config for `PaymentIntentEntity` is at `Data/PlatformDbContext.cs` ~line 768; the idempotency-table style (`platform_idempotency_records`) is the model to mirror for the new webhook-event table (~line 850).
- Test auth helpers `SeedPlayerAsync` / `AuthenticateAsync` and the fulfil seeding helper `SeedFulfilScenarioAsync` live in `tests/AFK4.Platform.Api.Tests/PortalWritesEndpointTests.cs`; `StaffAuthTestHelper.AuthorizeAsAsync` + `TestIds.OrganizationId/BranchId` are the operator-side seed.

**Scope boundary:** Unit 2 only. Unit 1 (self-start/extend, dropping the `PhoneVerified` gate, shell-state warnings) is a separate plan; this plan does NOT touch the `PhoneVerified` gate — it keeps the existing behavior so it can land independently of Unit 1. The shell QR UI is Unit 4 (Windows).

---

## File Structure

**New files:**
- `src/AFK4.Platform.Api/Payments/DcGate/DcGateOptions.cs` — bound config (`BaseUrl`, `ApiKey`, `WebhookSecret`).
- `src/AFK4.Platform.Api/Payments/DcGate/IDcGateClient.cs` — client seam + result records.
- `src/AFK4.Platform.Api/Payments/DcGate/DcGateClient.cs` — typed `HttpClient` implementation.
- `src/AFK4.Platform.Api/Data/DcGateWebhookEventEntity.cs` — webhook dedup row.
- `src/AFK4.Platform.Api/Data/Migrations/<timestamp>_AddDcGateColumnsAndWebhookEvents.cs` — EF migration (run `dotnet ef migrations add`; see Task 1).
- `src/AFK4.Shared.Contracts/Payments/DcGateWebhookPayload.cs` — webhook request body contract.
- `tests/AFK4.Platform.Api.Tests/DcGateClientTests.cs` — client unit tests (stub handler).
- `tests/AFK4.Platform.Api.Tests/DcGateWebhookEndpointTests.cs` — webhook integration tests.
- `tests/AFK4.Platform.Api.Tests/DcGateTopUpIntentTests.cs` — online-intent endpoint tests.

**Modified files:**
- `src/AFK4.Platform.Api/Data/PaymentIntentEntity.cs` — add 4 nullable gateway columns + `Disputed` flag.
- `src/AFK4.Platform.Api/Data/PlatformDbContext.cs` — extend `PaymentIntentEntity` config + add `DbSet<DcGateWebhookEventEntity>` + config.
- `src/AFK4.Shared.Contracts/Players/PlayerTopUpIntentRequest.cs` — add `Method` field (default `"counter"`).
- `src/AFK4.Shared.Contracts/Players/PlayerTopUpIntentDto.cs` — add `PayUrl`, `Comment`, `GatewayExpiresAtUtc`.
- `src/AFK4.Platform.Api/Program.cs` — register `DcGateOptions` + typed client; branch `top-up-intent` to online; add the webhook endpoint.
- `src/AFK4.Platform.Api/appsettings.json` — add a placeholder `DcGate` section.

**Conventions to mirror (verified ground truth):**
- Player endpoint auth: `playerContextAccessor.Current`; `null` → `Results.Unauthorized()`. Add `.RequireRateLimiting("player-me")`.
- Public endpoint: `.RequireRateLimiting("player-public")` (see the `/api/public/player/*` endpoints in `Program.cs`).
- Options class: `public const string SectionName = "DcGate";` plus settable properties (see `OwnerCodeOptions`).
- Migration: `#nullable disable`, namespace `AFK4.Platform.Api.Data.Migrations`, `AddColumn<T>` on `payment_intents` + `CreateTable` for the event table.

---

## Task 1 — Add nullable gateway columns to PaymentIntentEntity (+ webhook-event table) and migration

**Files:**
- Modify: `src/AFK4.Platform.Api/Data/PaymentIntentEntity.cs`
- Create: `src/AFK4.Platform.Api/Data/DcGateWebhookEventEntity.cs`
- Modify: `src/AFK4.Platform.Api/Data/PlatformDbContext.cs`
- Create: `src/AFK4.Platform.Api/Data/Migrations/<timestamp>_AddDcGateColumnsAndWebhookEvents.cs` (generated)
- Test: `tests/AFK4.Platform.Api.Tests/DcGateTopUpIntentTests.cs` (schema round-trip test added here, reused later)

- [ ] **Step 1: Write the failing schema round-trip test.**
  Create `tests/AFK4.Platform.Api.Tests/DcGateTopUpIntentTests.cs` with a test that persists a `PaymentIntentEntity` carrying the new gateway columns and reads them back, plus a `DcGateWebhookEventEntity` round-trip. This fails to compile until the entity/DbSet exist.

  ```csharp
  using System;
  using System.Threading.Tasks;
  using AFK4.Platform.Api.Data;
  using Microsoft.EntityFrameworkCore;
  using Microsoft.Extensions.DependencyInjection;
  using Xunit;

  namespace AFK4.Platform.Api.Tests;

  public class DcGateTopUpIntentTests
  {
      [Fact]
      public async Task PaymentIntent_PersistsGatewayColumns()
      {
          await using var factory = new PlatformApiFactory();
          var intentId = Guid.NewGuid();
          var expires = DateTimeOffset.UtcNow.AddMinutes(15);

          await using (var scope = factory.Services.CreateAsyncScope())
          {
              var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
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
                  Method = "dcgate",
                  GatewayPaymentId = "pay_abc123",
                  GatewayPayUrl = "http://pay.dc.tj/?A=1&s=50.00&c=cmt",
                  GatewayComment = "AFK4-COMMENT-0001",
                  GatewayExpiresAtUtc = expires,
                  Disputed = false,
                  CreatedAtUtc = DateTimeOffset.UtcNow
              });
              await db.SaveChangesAsync();
          }

          await using (var scope = factory.Services.CreateAsyncScope())
          {
              var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
              var stored = await db.PaymentIntents.SingleAsync(i => i.PaymentIntentId == intentId);
              Assert.Equal("dcgate", stored.Method);
              Assert.Equal("pay_abc123", stored.GatewayPaymentId);
              Assert.Equal("http://pay.dc.tj/?A=1&s=50.00&c=cmt", stored.GatewayPayUrl);
              Assert.Equal("AFK4-COMMENT-0001", stored.GatewayComment);
              Assert.NotNull(stored.GatewayExpiresAtUtc);
              Assert.False(stored.Disputed);
          }
      }

      [Fact]
      public async Task DcGateWebhookEvent_PersistsByEventId()
      {
          await using var factory = new PlatformApiFactory();
          await using var scope = factory.Services.CreateAsyncScope();
          var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
          db.DcGateWebhookEvents.Add(new DcGateWebhookEventEntity
          {
              DcGateWebhookEventId = Guid.NewGuid(),
              EventId = "evt_001",
              EventType = "payment.paid",
              ProcessedAtUtc = DateTimeOffset.UtcNow
          });
          await db.SaveChangesAsync();

          Assert.True(await db.DcGateWebhookEvents.AnyAsync(e => e.EventId == "evt_001"));
      }
  }
  ```

- [ ] **Step 2: Run the test — expect FAIL (compile error).**
  ```bash
  dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~DcGateTopUpIntentTests"
  ```
  Expected: build error — `PaymentIntentEntity` has no `GatewayPaymentId`/`Disputed`, and `DcGateWebhookEventEntity` / `DcGateWebhookEvents` do not exist.

- [ ] **Step 3: Add the entity columns, the webhook-event entity, and DbContext config.**

  In `src/AFK4.Platform.Api/Data/PaymentIntentEntity.cs`, append after `FulfilledByLedgerEntryId`:
  ```csharp
      // --- dcgate (online self-top-up) ---
      // Set only when Method == "dcgate"; null on the counter path.

      // dcgate's own payment id (returned by POST /api/payments). Used for status polls.
      public string? GatewayPaymentId { get; set; }

      // DC pay link the shell renders as a QR (pay.dc.tj/...).
      public string? GatewayPayUrl { get; set; }

      // 18-char DC reference comment dcgate matches incoming bank messages against.
      public string? GatewayComment { get; set; }

      public DateTimeOffset? GatewayExpiresAtUtc { get; set; }

      // payment.disputed webhook flips this true and leaves State == "pending" for the
      // operator to resolve manually; the player money is NOT credited on a dispute.
      public bool Disputed { get; set; }
  ```

  Create `src/AFK4.Platform.Api/Data/DcGateWebhookEventEntity.cs`:
  ```csharp
  namespace AFK4.Platform.Api.Data;

  // One row per dcgate webhook delivery we have already processed, keyed by the
  // gateway-supplied eventId. Lets the webhook endpoint be idempotent against
  // dcgate redeliveries without re-running TopUpWalletAsync.
  public sealed class DcGateWebhookEventEntity
  {
      public Guid DcGateWebhookEventId { get; set; }

      public string EventId { get; set; } = string.Empty;

      public string EventType { get; set; } = string.Empty;

      public DateTimeOffset ProcessedAtUtc { get; set; }
  }
  ```

  In `src/AFK4.Platform.Api/Data/PlatformDbContext.cs`, add the DbSet next to `PaymentIntents` (~line 93):
  ```csharp
      public DbSet<DcGateWebhookEventEntity> DcGateWebhookEvents => Set<DcGateWebhookEventEntity>();
  ```
  Extend the existing `PaymentIntentEntity` config block (~line 768) — add the gateway property lengths inside it:
  ```csharp
          entity.Property(intent => intent.GatewayPaymentId).HasMaxLength(128);
          entity.Property(intent => intent.GatewayComment).HasMaxLength(64);
          entity.Property(intent => intent.GatewayPayUrl).HasMaxLength(1024);
  ```
  Add a new config block (mirror `platform_idempotency_records` at ~line 850):
  ```csharp
          modelBuilder.Entity<DcGateWebhookEventEntity>(entity =>
          {
              entity.ToTable("dcgate_webhook_events");
              entity.HasKey(row => row.DcGateWebhookEventId);
              entity.Property(row => row.EventId).HasMaxLength(128).IsRequired();
              entity.Property(row => row.EventType).HasMaxLength(64).IsRequired();
              entity.HasIndex(row => row.EventId).IsUnique();
          });
  ```

- [ ] **Step 4: Generate the EF migration.**
  ```bash
  dotnet ef migrations add AddDcGateColumnsAndWebhookEvents \
    --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj \
    --output-dir Data/Migrations
  ```
  Verify the generated `Up` contains `AddColumn<string>` for `GatewayPaymentId`/`GatewayPayUrl`/`GatewayComment`, `AddColumn<DateTimeOffset>` for `GatewayExpiresAtUtc`, `AddColumn<bool>("Disputed", ... defaultValue: false)` on `payment_intents`, and a `CreateTable("dcgate_webhook_events", ...)` with a unique index on `EventId`. If `dotnet ef` is unavailable, hand-write the migration mirroring `20260601043751_AddBranchPreferredLocale.cs` (column-add) and `20260603094045_AddPaymentIntents.cs` (table-create), namespace `AFK4.Platform.Api.Data.Migrations`, `#nullable disable`, and update `PlatformDbContextModelSnapshot.cs` to match.

- [ ] **Step 5: Run the test — expect PASS.**
  ```bash
  dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~DcGateTopUpIntentTests"
  ```
  Expected: both schema round-trip tests pass.

- [ ] **Step 6: Commit.**
  ```bash
  git add -A && git commit -m "Add dcgate gateway columns + webhook-event table to payment intents"
  ```

---

## Task 2 — DcGateOptions + IDcGateClient + DcGateClient (typed HttpClient)

**Files:**
- Create: `src/AFK4.Platform.Api/Payments/DcGate/DcGateOptions.cs`
- Create: `src/AFK4.Platform.Api/Payments/DcGate/IDcGateClient.cs`
- Create: `src/AFK4.Platform.Api/Payments/DcGate/DcGateClient.cs`
- Test: `tests/AFK4.Platform.Api.Tests/DcGateClientTests.cs`

- [ ] **Step 1: Write the failing client test.**
  Create `tests/AFK4.Platform.Api.Tests/DcGateClientTests.cs`. It drives `DcGateClient` against a stub `HttpMessageHandler` and asserts the request shape, the Bearer header, and minor→major amount formatting.

  ```csharp
  using System;
  using System.Net;
  using System.Net.Http;
  using System.Text.Json;
  using System.Threading;
  using System.Threading.Tasks;
  using AFK4.Platform.Api.Payments.DcGate;
  using Microsoft.Extensions.Options;
  using Xunit;

  namespace AFK4.Platform.Api.Tests;

  public class DcGateClientTests
  {
      private sealed class StubHandler : HttpMessageHandler
      {
          private readonly Func<HttpRequestMessage, HttpResponseMessage> responder;
          public HttpRequestMessage? LastRequest { get; private set; }
          public string? LastBody { get; private set; }

          public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
              => this.responder = responder;

          protected override async Task<HttpResponseMessage> SendAsync(
              HttpRequestMessage request, CancellationToken cancellationToken)
          {
              LastRequest = request;
              LastBody = request.Content is null
                  ? null
                  : await request.Content.ReadAsStringAsync(cancellationToken);
              return responder(request);
          }
      }

      private static DcGateClient CreateClient(StubHandler handler) =>
          new(
              new HttpClient(handler) { BaseAddress = new Uri("https://dcgate.example") },
              Options.Create(new DcGateOptions
              {
                  BaseUrl = "https://dcgate.example",
                  ApiKey = "test-api-key",
                  WebhookSecret = "secret"
              }));

      private static HttpResponseMessage OkPayment() =>
          new(HttpStatusCode.OK)
          {
              Content = new StringContent(
                  """
                  {
                    "paymentId": "pay_xyz",
                    "status": "pending",
                    "amount": "50.00",
                    "currency": "TJS",
                    "comment": "AFK4-COMMENT-0001",
                    "expiresAt": "2026-06-03T12:30:00Z",
                    "payUrl": "http://pay.dc.tj/?A=1&s=50.00&c=cmt"
                  }
                  """,
                  System.Text.Encoding.UTF8,
                  "application/json")
          };

      [Fact]
      public async Task CreatePaymentAsync_SendsBearerAndMajorUnitAmount()
      {
          var handler = new StubHandler(_ => OkPayment());
          var client = CreateClient(handler);

          var result = await client.CreatePaymentAsync(
              amountMinorUnits: 5_000,
              currencyCode: "TJS",
              externalOrderId: "abcd1234",
              metadata: new { playerAccountId = "p1", branchId = "b1" },
              CancellationToken.None);

          Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
          Assert.Equal("/api/payments", handler.LastRequest.RequestUri!.AbsolutePath);
          Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization!.Scheme);
          Assert.Equal("test-api-key", handler.LastRequest.Headers.Authorization.Parameter);

          using var sent = JsonDocument.Parse(handler.LastBody!);
          Assert.Equal("50.00", sent.RootElement.GetProperty("amount").GetString());
          Assert.Equal("abcd1234", sent.RootElement.GetProperty("externalOrderId").GetString());
          Assert.True(sent.RootElement.TryGetProperty("metadata", out _));

          Assert.Equal("pay_xyz", result.PaymentId);
          Assert.Equal("AFK4-COMMENT-0001", result.Comment);
          Assert.Equal("http://pay.dc.tj/?A=1&s=50.00&c=cmt", result.PayUrl);
          Assert.NotNull(result.ExpiresAt);
      }

      [Theory]
      [InlineData(5_000, "50.00")]
      [InlineData(99, "0.99")]
      [InlineData(1, "0.01")]
      [InlineData(100_000, "1000.00")]
      public async Task CreatePaymentAsync_FormatsMinorUnitsAsMajorString(long minor, string expected)
      {
          var handler = new StubHandler(_ => OkPayment());
          var client = CreateClient(handler);

          await client.CreatePaymentAsync(minor, "TJS", "ord", metadata: new { }, CancellationToken.None);

          using var sent = JsonDocument.Parse(handler.LastBody!);
          Assert.Equal(expected, sent.RootElement.GetProperty("amount").GetString());
      }

      [Fact]
      public async Task CreatePaymentAsync_ThrowsOnNonSuccessStatus()
      {
          var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway));
          var client = CreateClient(handler);

          await Assert.ThrowsAsync<HttpRequestException>(() =>
              client.CreatePaymentAsync(5_000, "TJS", "ord", metadata: new { }, CancellationToken.None));
      }
  }
  ```

- [ ] **Step 2: Run the test — expect FAIL (compile error).**
  ```bash
  dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~DcGateClientTests"
  ```
  Expected: build error — `DcGateOptions`, `DcGateClient`, `IDcGateClient` do not exist.

- [ ] **Step 3: Implement options, interface, and client.**

  `src/AFK4.Platform.Api/Payments/DcGate/DcGateOptions.cs`:
  ```csharp
  namespace AFK4.Platform.Api.Payments.DcGate;

  public sealed class DcGateOptions
  {
      public const string SectionName = "DcGate";

      // dcgate base URL, e.g. https://dcgate.mubi.dev
      public string BaseUrl { get; set; } = string.Empty;

      // Per-project API key sent as Authorization: Bearer.
      public string ApiKey { get; set; } = string.Empty;

      // Shared secret dcgate uses to HMAC-sign webhook bodies.
      public string WebhookSecret { get; set; } = string.Empty;
  }
  ```

  `src/AFK4.Platform.Api/Payments/DcGate/IDcGateClient.cs`:
  ```csharp
  namespace AFK4.Platform.Api.Payments.DcGate;

  public interface IDcGateClient
  {
      Task<DcGatePaymentResult> CreatePaymentAsync(
          long amountMinorUnits,
          string currencyCode,
          string externalOrderId,
          object metadata,
          CancellationToken cancellationToken);
  }

  public sealed record DcGatePaymentResult(
      string PaymentId,
      string Status,
      string Amount,
      string Currency,
      string Comment,
      DateTimeOffset? ExpiresAt,
      string PayUrl);
  ```

  `src/AFK4.Platform.Api/Payments/DcGate/DcGateClient.cs`:
  ```csharp
  using System.Globalization;
  using System.Net.Http.Headers;
  using System.Net.Http.Json;
  using Microsoft.Extensions.Options;

  namespace AFK4.Platform.Api.Payments.DcGate;

  public sealed class DcGateClient : IDcGateClient
  {
      private readonly HttpClient httpClient;
      private readonly DcGateOptions options;

      public DcGateClient(HttpClient httpClient, IOptions<DcGateOptions> options)
      {
          this.httpClient = httpClient;
          this.options = options.Value;
      }

      public async Task<DcGatePaymentResult> CreatePaymentAsync(
          long amountMinorUnits,
          string currencyCode,
          string externalOrderId,
          object metadata,
          CancellationToken cancellationToken)
      {
          var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments")
          {
              Content = JsonContent.Create(new
              {
                  amount = ToMajorUnitString(amountMinorUnits),
                  externalOrderId,
                  metadata
              })
          };
          request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

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

- [ ] **Step 4: Run the test — expect PASS.**
  ```bash
  dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~DcGateClientTests"
  ```
  Expected: all client tests pass (request shape, Bearer, amount formatting, throw-on-error).

- [ ] **Step 5: Commit.**
  ```bash
  git add -A && git commit -m "Add DcGate typed HttpClient with minor->major amount conversion"
  ```

---

## Task 3 — Register the client and wire the online top-up path

**Files:**
- Modify: `src/AFK4.Platform.Api/Program.cs` (register `DcGateOptions` + typed client; branch the top-up endpoint)
- Modify: `src/AFK4.Shared.Contracts/Players/PlayerTopUpIntentRequest.cs`
- Modify: `src/AFK4.Shared.Contracts/Players/PlayerTopUpIntentDto.cs`
- Modify: `src/AFK4.Platform.Api/appsettings.json`
- Test: `tests/AFK4.Platform.Api.Tests/DcGateTopUpIntentTests.cs` (append)

- [ ] **Step 1: Write the failing online-intent endpoint test.**
  Append to `tests/AFK4.Platform.Api.Tests/DcGateTopUpIntentTests.cs`. It uses a fake `IDcGateClient` injected through the factory, mirrors `SeedPlayerAsync`/`AuthenticateAsync`, and asserts the online path persists the gateway fields and returns the `payUrl`; and that the default (counter) path neither calls dcgate nor returns a `payUrl`.

  ```csharp
  // appended to DcGateTopUpIntentTests.cs

  // using AFK4.Platform.Api.Payments.DcGate;  (add to the file's usings)
  // using AFK4.Shared.Contracts.Players;
  // using Microsoft.AspNetCore.Identity;
  // using Microsoft.Extensions.DependencyInjection.Extensions;
  // using System.Net;
  // using System.Net.Http.Headers;
  // using System.Net.Http.Json;

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

  private static PlatformApiFactory FactoryWithFakeGateway(FakeDcGateClient fake) =>
      new TestGatewayFactory(fake);

  private sealed class TestGatewayFactory : PlatformApiFactory
  {
      private readonly FakeDcGateClient fake;
      public TestGatewayFactory(FakeDcGateClient fake) => this.fake = fake;

      protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
      {
          base.ConfigureWebHost(builder);
          builder.ConfigureServices(services =>
          {
              services.RemoveAll<IDcGateClient>();
              services.AddSingleton<IDcGateClient>(fake);
          });
      }
  }

  private static async Task<(Guid OrgId, Guid PlayerId, string Phone)> SeedPlayerAsync(
      PlatformApiFactory factory, string pin)
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
          CreatedAtUtc = DateTimeOffset.UtcNow
      });
      var credential = new PlayerCredentialEntity
      {
          PlayerCredentialId = Guid.NewGuid(),
          PlayerAccountId = player,
          OrganizationId = org,
          PhoneVerified = true,
          CreatedAtUtc = DateTimeOffset.UtcNow,
          UpdatedAtUtc = DateTimeOffset.UtcNow
      };
      credential.PasswordHash =
          new PasswordHasher<PlayerCredentialEntity>().HashPassword(credential, pin);
      db.PlayerCredentials.Add(credential);
      await db.SaveChangesAsync();
      return (org, player, phone);
  }

  private static async Task AuthenticateAsync(HttpClient client, Guid orgId, string phone, string pin)
  {
      var signIn = await client.PostAsJsonAsync(
          "/api/public/player/sign-in", new PlayerSignInRequest(orgId, phone, pin));
      signIn.EnsureSuccessStatusCode();
      var tokens = await signIn.Content.ReadFromJsonAsync<PlayerSignInResponse>();
      client.DefaultRequestHeaders.Authorization =
          new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
  }

  [Fact]
  public async Task TopUpIntent_WithDcGateMethod_CreatesGatewayPaymentAndReturnsPayUrl()
  {
      var fake = new FakeDcGateClient();
      await using var factory = FactoryWithFakeGateway(fake);
      var p = await SeedPlayerAsync(factory, "1234");
      using var client = factory.CreateClient();
      await AuthenticateAsync(client, p.OrgId, p.Phone, "1234");

      var response = await client.PostAsJsonAsync(
          "/api/me/wallet/top-up-intent",
          new PlayerTopUpIntentRequest(5_000, "TJS", "dcgate"));

      Assert.Equal(HttpStatusCode.OK, response.StatusCode);
      var dto = await response.Content.ReadFromJsonAsync<PlayerTopUpIntentDto>();
      Assert.Equal("dcgate", dto!.Method);
      Assert.Equal("http://pay.dc.tj/?A=1&s=50.00&c=cmt", dto.PayUrl);
      Assert.Equal("AFK4-CMT-0001", dto.Comment);
      Assert.NotNull(dto.GatewayExpiresAtUtc);

      Assert.Equal(1, fake.Calls);
      Assert.Equal(dto.PaymentIntentId.ToString("N"), fake.LastExternalOrderId);
      Assert.Equal(5_000, fake.LastAmountMinor);

      await using var scope = factory.Services.CreateAsyncScope();
      var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
      var intent = await db.PaymentIntents.FindAsync(dto.PaymentIntentId);
      Assert.Equal("pay_fake", intent!.GatewayPaymentId);
      Assert.Equal("http://pay.dc.tj/?A=1&s=50.00&c=cmt", intent.GatewayPayUrl);
  }

  [Fact]
  public async Task TopUpIntent_DefaultMethod_StaysCounterAndDoesNotCallGateway()
  {
      var fake = new FakeDcGateClient();
      await using var factory = FactoryWithFakeGateway(fake);
      var p = await SeedPlayerAsync(factory, "1234");
      using var client = factory.CreateClient();
      await AuthenticateAsync(client, p.OrgId, p.Phone, "1234");

      var response = await client.PostAsJsonAsync(
          "/api/me/wallet/top-up-intent",
          new PlayerTopUpIntentRequest(5_000, "TJS", null));

      Assert.Equal(HttpStatusCode.OK, response.StatusCode);
      var dto = await response.Content.ReadFromJsonAsync<PlayerTopUpIntentDto>();
      Assert.Equal("counter", dto!.Method);
      Assert.Null(dto.PayUrl);
      Assert.Equal(0, fake.Calls);
  }
  ```

- [ ] **Step 2: Run the test — expect FAIL.**
  ```bash
  dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~DcGateTopUpIntentTests"
  ```
  Expected: build error — `PlayerTopUpIntentRequest` has no third arg, `PlayerTopUpIntentDto` has no `PayUrl`/`Comment`/`GatewayExpiresAtUtc`, and `IDcGateClient` is not registered.

- [ ] **Step 3: Extend the contracts, register the client, branch the endpoint.**

  `src/AFK4.Shared.Contracts/Players/PlayerTopUpIntentRequest.cs`:
  ```csharp
  namespace AFK4.Shared.Contracts.Players;

  // Player requests a wallet top-up.
  // CurrencyCode defaults to "TJS" when null or blank.
  // Method ∈ { "counter", "dcgate" }; null/blank → "counter" (operator-confirmed at the desk).
  public sealed record PlayerTopUpIntentRequest(
      long AmountMinorUnits,
      string? CurrencyCode,
      string? Method = null);
  ```

  `src/AFK4.Shared.Contracts/Players/PlayerTopUpIntentDto.cs` — append three nullable fields:
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
      bool IsExpired,
      string? PayUrl = null,
      string? Comment = null,
      DateTimeOffset? GatewayExpiresAtUtc = null);
  ```

  In `src/AFK4.Platform.Api/Program.cs`, in the service-registration block (near the other `Configure<>` calls, ~line 200), add:
  ```csharp
  builder.Services.Configure<DcGateOptions>(builder.Configuration.GetSection(DcGateOptions.SectionName));
  builder.Services.AddHttpClient<IDcGateClient, DcGateClient>((provider, http) =>
  {
      var opts = provider.GetRequiredService<IOptions<DcGateOptions>>().Value;
      if (!string.IsNullOrWhiteSpace(opts.BaseUrl))
      {
          http.BaseAddress = new Uri(opts.BaseUrl);
      }
  });
  ```
  Add the using at the top of `Program.cs`:
  ```csharp
  using AFK4.Platform.Api.Payments.DcGate;
  ```

  Replace the body of `app.MapPost("/api/me/wallet/top-up-intent", ...)` (~line 819) so it takes the gateway client and branches on `Method`. The counter path is unchanged; only the new `dcgate` branch is added:
  ```csharp
  app.MapPost("/api/me/wallet/top-up-intent", async (
      PlayerTopUpIntentRequest request,
      IPlayerContextAccessor playerContextAccessor,
      IDcGateClient dcGateClient,
      PlatformDbContext dbContext,
      CancellationToken cancellationToken) =>
  {
      var player = playerContextAccessor.Current;
      if (player is null)
      {
          return Results.Unauthorized();
      }

      // D8 gate: verified phone required for money actions. (Unit 1 removes this; kept here so
      // Unit 2 lands independently.)
      if (!player.PhoneVerified)
      {
          return Results.StatusCode(StatusCodes.Status403Forbidden);
      }

      if (request.AmountMinorUnits <= 0)
      {
          return Results.BadRequest(new { Error = "Amount must be greater than zero." });
      }

      var method = string.IsNullOrWhiteSpace(request.Method)
          ? "counter"
          : request.Method.Trim().ToLowerInvariant();
      if (method != "counter" && method != "dcgate")
      {
          return Results.BadRequest(new { Error = "Method must be 'counter' or 'dcgate'." });
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
          Method = method,
          FulfilledByLedgerEntryId = null,
          CreatedAtUtc = now,
          FulfilledAtUtc = null
      };

      if (method == "dcgate")
      {
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
          IsExpired: false,
          PayUrl: intent.GatewayPayUrl,
          Comment: intent.GatewayComment,
          GatewayExpiresAtUtc: intent.GatewayExpiresAtUtc));
  }).RequireRateLimiting("player-me");
  ```
  Update the two other call sites that construct `PlayerTopUpIntentDto` (the `GET /api/me/wallet/top-up-intents` projection ~line 905 and the operator `fulfil` responses ~line 1114 and ~line 1173) — they keep their existing positional args; the three new args default to `null`, so add nothing OR pass `intent.GatewayPayUrl, intent.GatewayComment, intent.GatewayExpiresAtUtc` on the GET projection so the shell can re-render a pending QR. Recommended: pass them on the GET projection, leave the fulfil responses on defaults.

  `src/AFK4.Platform.Api/appsettings.json` — add a placeholder section (no secrets committed):
  ```json
    "DcGate": {
      "BaseUrl": "",
      "ApiKey": "",
      "WebhookSecret": ""
    }
  ```

- [ ] **Step 4: Run the test — expect PASS.**
  ```bash
  dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~DcGateTopUpIntentTests"
  ```
  Expected: online path persists gateway fields + returns `payUrl`; counter path unchanged and never calls the gateway.

- [ ] **Step 5: Commit.**
  ```bash
  git add -A && git commit -m "Wire online top-up: dcgate method creates payment and returns payUrl"
  ```

---

## Task 4 — dcgate webhook endpoint (HMAC verify, idempotent, credit, transition)

**Files:**
- Create: `src/AFK4.Shared.Contracts/Payments/DcGateWebhookPayload.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs` (add the webhook endpoint + a private HMAC helper)
- Test: `tests/AFK4.Platform.Api.Tests/DcGateWebhookEndpointTests.cs`

Design notes for the implementer:
- Read the RAW body with `request.EnableBuffering()` / a `StreamReader`, because the HMAC is over the exact bytes — do NOT re-serialize a model-bound object.
- Verify `x-dcgate-signature` shaped `sha256=<hex>`. Compute `HMACSHA256(rawBody, WebhookSecret)`, hex-encode, compare with `CryptographicOperations.FixedTimeEquals` over the decoded bytes (constant-time). Missing/malformed/non-matching → `401`.
- Dedup: if a `dcgate_webhook_events` row with this `eventId` exists → return `200` no-op (already processed). Otherwise process, then insert the row in the same SaveChanges.
- `payment.paid`: find intent by `externalOrderId` (`Guid` parsed from the `"N"` string). Unknown id → `200` no-op (decision: ack so dcgate stops retrying a payment we don't own; log it). If intent already `fulfilled` → record the event, `200`. Else call `TopUpWalletAsync(playerAccountId, branchId, Guid.Empty, new TopUpWalletRequest(orgId, new MoneyDto(currency, amountMinor), "wallet top-up via dcgate", intentId.ToString("N")), ct)`. On `!Succeeded` (e.g. no open shift) → return `503` (or `409`) WITHOUT recording the event or flipping the intent, so dcgate redelivers. On success → set `State = "fulfilled"`, `FulfilledAtUtc`, record the event, `200`.
- `payment.expired`: set `State = "expired"` (only if still `pending`), record event, `200`.
- `payment.disputed`: set `Disputed = true`, leave `State`, record event, `200`.
- Amount on the webhook is a major-unit string; the credit uses the intent's stored `AmountMinorUnits` (authoritative), NOT the webhook amount — never trust the caller's amount for the credit.

- [ ] **Step 1: Write the failing webhook tests.**
  Create `tests/AFK4.Platform.Api.Tests/DcGateWebhookEndpointTests.cs`. It seeds a `dcgate` intent at `TestIds.BranchId` with an open shift, signs a raw JSON body with the configured secret, and posts to the webhook.

  ```csharp
  using System;
  using System.Net;
  using System.Net.Http;
  using System.Security.Cryptography;
  using System.Text;
  using System.Threading.Tasks;
  using AFK4.Platform.Api.Data;
  using AFK4.Platform.Api.Shifts;
  using AFK4.Shared.Contracts.Shifts;
  using Microsoft.AspNetCore.Hosting;
  using Microsoft.EntityFrameworkCore;
  using Microsoft.Extensions.DependencyInjection;
  using Xunit;

  namespace AFK4.Platform.Api.Tests;

  public class DcGateWebhookEndpointTests
  {
      private const string WebhookSecret = "test-webhook-secret";

      // A factory that injects a known webhook secret via configuration.
      private sealed class WebhookFactory : PlatformApiFactory
      {
          protected override void ConfigureWebHost(IWebHostBuilder builder)
          {
              base.ConfigureWebHost(builder);
              builder.ConfigureAppConfiguration((_, config) =>
              {
                  config.AddInMemoryCollection(new System.Collections.Generic.Dictionary<string, string?>
                  {
                      ["DcGate:BaseUrl"] = "https://dcgate.example",
                      ["DcGate:ApiKey"] = "k",
                      ["DcGate:WebhookSecret"] = WebhookSecret
                  });
              });
          }
      }

      private static async Task<Guid> SeedDcGateIntentAsync(
          PlatformApiFactory factory, string state = "pending", long amountMinor = 5_000)
      {
          await using var scope = factory.Services.CreateAsyncScope();
          var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
          var playerId = Guid.NewGuid();
          db.PlayerAccounts.Add(new PlayerAccountEntity
          {
              PlayerAccountId = playerId,
              OrganizationId = TestIds.OrganizationId,
              HomeBranchId = TestIds.BranchId,
              DisplayName = "Gateway Player",
              PhoneNumber = $"+99293000{playerId.ToString("N")[..4]}",
              IsActive = true,
              CreatedAtUtc = DateTimeOffset.UtcNow
          });
          var intentId = Guid.NewGuid();
          db.PaymentIntents.Add(new PaymentIntentEntity
          {
              PaymentIntentId = intentId,
              PlayerAccountId = playerId,
              OrganizationId = TestIds.OrganizationId,
              BranchId = TestIds.BranchId,
              AmountMinorUnits = amountMinor,
              CurrencyCode = "TJS",
              Purpose = "wallet_topup",
              State = state,
              Method = "dcgate",
              GatewayPaymentId = "pay_fake",
              GatewayComment = "AFK4-CMT-0001",
              CreatedAtUtc = DateTimeOffset.UtcNow
          });
          await db.SaveChangesAsync();
          return intentId;
      }

      private static async Task SeedOpenShiftAsync(PlatformApiFactory factory)
      {
          await using var scope = factory.Services.CreateAsyncScope();
          var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
          db.Shifts.Add(new ShiftEntity
          {
              ShiftId = Guid.NewGuid(),
              OrganizationId = TestIds.OrganizationId,
              BranchId = TestIds.BranchId,
              OpenedByStaffUserId = TestIds.TechnicianStaffUserId,
              State = ShiftStateNames.Open,
              CurrencyCode = "TJS",
              StartingCashMinorUnits = 50_000,
              CountedCashMinorUnits = 0,
              ExpectedCashMinorUnits = 0,
              DifferenceMinorUnits = 0,
              OpeningNote = "test",
              ClosingNote = string.Empty,
              OpenedAtUtc = DateTimeOffset.UtcNow
          });
          await db.SaveChangesAsync();
      }

      private static string PaidBody(Guid intentId, string eventId) =>
          $$"""
          {"eventId":"{{eventId}}","eventType":"payment.paid","projectId":"afk4","payment":{"id":"pay_fake","amount":"50.00","comment":"AFK4-CMT-0001","currency":"TJS","externalOrderId":"{{intentId:N}}","paidAt":"2026-06-03T12:00:00Z","status":"paid"}}
          """;

      private static string ExpiredBody(Guid intentId, string eventId) =>
          $$"""
          {"eventId":"{{eventId}}","eventType":"payment.expired","projectId":"afk4","payment":{"id":"pay_fake","amount":"50.00","comment":"AFK4-CMT-0001","currency":"TJS","externalOrderId":"{{intentId:N}}","status":"expired"}}
          """;

      private static HttpRequestMessage SignedRequest(string body, string eventId, string eventType, string secret)
      {
          var sig = Convert.ToHexString(
              new HMACSHA256(Encoding.UTF8.GetBytes(secret)).ComputeHash(Encoding.UTF8.GetBytes(body)))
              .ToLowerInvariant();
          var req = new HttpRequestMessage(HttpMethod.Post, "/api/public/payments/dcgate/webhook")
          {
              Content = new StringContent(body, Encoding.UTF8, "application/json")
          };
          req.Headers.Add("x-dcgate-event-id", eventId);
          req.Headers.Add("x-dcgate-event-type", eventType);
          req.Headers.Add("x-dcgate-project-id", "afk4");
          req.Headers.Add("x-dcgate-signature", $"sha256={sig}");
          return req;
      }

      private static async Task<int> CountTopUpsAsync(PlatformApiFactory factory, Guid intentId)
      {
          await using var scope = factory.Services.CreateAsyncScope();
          var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
          var playerId = (await db.PaymentIntents.SingleAsync(i => i.PaymentIntentId == intentId)).PlayerAccountId;
          return await db.LedgerEntries.CountAsync(e => e.PlayerAccountId == playerId && e.EntryType == "top_up");
      }

      [Fact]
      public async Task Webhook_ValidPaid_CreditsOnceAndFulfilsIntent()
      {
          await using var factory = new WebhookFactory();
          var intentId = await SeedDcGateIntentAsync(factory);
          await SeedOpenShiftAsync(factory);
          using var client = factory.CreateClient();

          var body = PaidBody(intentId, "evt_paid_1");
          var response = await client.SendAsync(SignedRequest(body, "evt_paid_1", "payment.paid", WebhookSecret));

          Assert.Equal(HttpStatusCode.OK, response.StatusCode);
          Assert.Equal(1, await CountTopUpsAsync(factory, intentId));

          await using var scope = factory.Services.CreateAsyncScope();
          var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
          var intent = await db.PaymentIntents.SingleAsync(i => i.PaymentIntentId == intentId);
          Assert.Equal("fulfilled", intent.State);
          Assert.NotNull(intent.FulfilledAtUtc);
      }

      [Fact]
      public async Task Webhook_ReplaySameEventId_DoesNotDoubleCredit()
      {
          await using var factory = new WebhookFactory();
          var intentId = await SeedDcGateIntentAsync(factory);
          await SeedOpenShiftAsync(factory);
          using var client = factory.CreateClient();

          var body = PaidBody(intentId, "evt_dup");
          var r1 = await client.SendAsync(SignedRequest(body, "evt_dup", "payment.paid", WebhookSecret));
          var r2 = await client.SendAsync(SignedRequest(body, "evt_dup", "payment.paid", WebhookSecret));

          Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
          Assert.Equal(HttpStatusCode.OK, r2.StatusCode);
          Assert.Equal(1, await CountTopUpsAsync(factory, intentId));
      }

      [Fact]
      public async Task Webhook_BadSignature_Returns401AndDoesNotCredit()
      {
          await using var factory = new WebhookFactory();
          var intentId = await SeedDcGateIntentAsync(factory);
          await SeedOpenShiftAsync(factory);
          using var client = factory.CreateClient();

          var body = PaidBody(intentId, "evt_bad");
          // Sign with the WRONG secret.
          var response = await client.SendAsync(SignedRequest(body, "evt_bad", "payment.paid", "wrong-secret"));

          Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
          Assert.Equal(0, await CountTopUpsAsync(factory, intentId));
      }

      [Fact]
      public async Task Webhook_MissingSignature_Returns401()
      {
          await using var factory = new WebhookFactory();
          var intentId = await SeedDcGateIntentAsync(factory);
          using var client = factory.CreateClient();

          var body = PaidBody(intentId, "evt_nosig");
          var req = new HttpRequestMessage(HttpMethod.Post, "/api/public/payments/dcgate/webhook")
          {
              Content = new StringContent(body, Encoding.UTF8, "application/json")
          };
          var response = await client.SendAsync(req);

          Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
      }

      [Fact]
      public async Task Webhook_UnknownExternalOrderId_Returns200Noop()
      {
          await using var factory = new WebhookFactory();
          await SeedOpenShiftAsync(factory);
          using var client = factory.CreateClient();

          var unknown = Guid.NewGuid();
          var body = PaidBody(unknown, "evt_unknown");
          var response = await client.SendAsync(SignedRequest(body, "evt_unknown", "payment.paid", WebhookSecret));

          Assert.Equal(HttpStatusCode.OK, response.StatusCode);
      }

      [Fact]
      public async Task Webhook_Expired_MarksIntentExpired()
      {
          await using var factory = new WebhookFactory();
          var intentId = await SeedDcGateIntentAsync(factory);
          using var client = factory.CreateClient();

          var body = ExpiredBody(intentId, "evt_exp");
          var response = await client.SendAsync(SignedRequest(body, "evt_exp", "payment.expired", WebhookSecret));

          Assert.Equal(HttpStatusCode.OK, response.StatusCode);
          await using var scope = factory.Services.CreateAsyncScope();
          var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
          var intent = await db.PaymentIntents.SingleAsync(i => i.PaymentIntentId == intentId);
          Assert.Equal("expired", intent.State);
      }
  }
  ```

- [ ] **Step 2: Run the tests — expect FAIL (404, endpoint not mapped).**
  ```bash
  dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~DcGateWebhookEndpointTests"
  ```
  Expected: failures — the webhook route returns 404 (not mapped); credit/transition assertions fail.

- [ ] **Step 3: Add the webhook payload contract, then the endpoint.**

  `src/AFK4.Shared.Contracts/Payments/DcGateWebhookPayload.cs`:
  ```csharp
  using System;

  namespace AFK4.Shared.Contracts.Payments;

  public sealed record DcGateWebhookPayload(
      string EventId,
      string EventType,
      string ProjectId,
      DcGateWebhookPaymentDto Payment);

  public sealed record DcGateWebhookPaymentDto(
      string Id,
      string Amount,
      string Comment,
      string Currency,
      string ExternalOrderId,
      DateTimeOffset? PaidAt,
      string Status);
  ```

  In `src/AFK4.Platform.Api/Program.cs`, add the endpoint (near the other `/api/public/*` routes). It reads the raw body, verifies the signature, and processes:
  ```csharp
  app.MapPost("/api/public/payments/dcgate/webhook", async (
      HttpRequest httpRequest,
      IOptions<DcGateOptions> dcGateOptions,
      IBillingCommandService billingCommandService,
      PlatformDbContext dbContext,
      CancellationToken cancellationToken) =>
  {
      var options = dcGateOptions.Value;

      // Read the exact bytes: the HMAC is over the raw body, so we cannot model-bind.
      httpRequest.EnableBuffering();
      string rawBody;
      using (var reader = new StreamReader(httpRequest.Body, Encoding.UTF8, leaveOpen: true))
      {
          rawBody = await reader.ReadToEndAsync(cancellationToken);
      }
      httpRequest.Body.Position = 0;

      if (!DcGateSignatureIsValid(httpRequest, rawBody, options.WebhookSecret))
      {
          return Results.Unauthorized();
      }

      DcGateWebhookPayload? payload;
      try
      {
          payload = JsonSerializer.Deserialize<DcGateWebhookPayload>(
              rawBody,
              new JsonSerializerOptions(JsonSerializerDefaults.Web));
      }
      catch (JsonException)
      {
          return Results.BadRequest();
      }

      if (payload is null || string.IsNullOrWhiteSpace(payload.EventId))
      {
          return Results.BadRequest();
      }

      // Idempotent against dcgate redeliveries.
      if (await dbContext.DcGateWebhookEvents.AnyAsync(e => e.EventId == payload.EventId, cancellationToken))
      {
          return Results.Ok();
      }

      if (!Guid.TryParseExact(payload.Payment.ExternalOrderId, "N", out var intentId))
      {
          return Results.Ok(); // not an AFK4 order id — ack so dcgate stops retrying.
      }

      var intent = await dbContext.PaymentIntents.SingleOrDefaultAsync(
          i => i.PaymentIntentId == intentId, cancellationToken);
      if (intent is null)
      {
          return Results.Ok(); // unknown order — ack, nothing to do.
      }

      switch (payload.EventType)
      {
          case "payment.paid":
              if (intent.State != "fulfilled")
              {
                  // Reuse the SAME idempotency key as the operator fulfil path so the
                  // two confirmation routes can never double-credit.
                  var topUp = new TopUpWalletRequest(
                      intent.OrganizationId,
                      new MoneyDto(intent.CurrencyCode, intent.AmountMinorUnits),
                      "wallet top-up via dcgate",
                      intent.PaymentIntentId.ToString("N"));

                  var result = await billingCommandService.TopUpWalletAsync(
                      intent.PlayerAccountId,
                      intent.BranchId,
                      Guid.Empty, // system actor: no staff user on the gateway path.
                      topUp,
                      cancellationToken);

                  if (!result.Succeeded)
                  {
                      // e.g. no open shift at the branch. Do NOT record the event or flip
                      // the intent — return 503 so dcgate redelivers later.
                      return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
                  }

                  intent.State = "fulfilled";
                  intent.FulfilledAtUtc = DateTimeOffset.UtcNow;
              }
              break;

          case "payment.expired":
              if (intent.State == "pending")
              {
                  intent.State = "expired";
              }
              break;

          case "payment.disputed":
              intent.Disputed = true;
              break;

          default:
              return Results.BadRequest();
      }

      dbContext.DcGateWebhookEvents.Add(new DcGateWebhookEventEntity
      {
          DcGateWebhookEventId = Guid.NewGuid(),
          EventId = payload.EventId,
          EventType = payload.EventType,
          ProcessedAtUtc = DateTimeOffset.UtcNow
      });
      await dbContext.SaveChangesAsync(cancellationToken);

      return Results.Ok();
  }).RequireRateLimiting("player-public");
  ```

  Add the private HMAC helper near the other local functions at the bottom of `Program.cs` (constant-time compare):
  ```csharp
  static bool DcGateSignatureIsValid(HttpRequest request, string rawBody, string secret)
  {
      if (string.IsNullOrEmpty(secret))
      {
          return false;
      }

      if (!request.Headers.TryGetValue("x-dcgate-signature", out var header))
      {
          return false;
      }

      var provided = header.ToString();
      const string prefix = "sha256=";
      if (!provided.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
      {
          return false;
      }

      var providedHex = provided[prefix.Length..];
      byte[] providedBytes;
      try
      {
          providedBytes = Convert.FromHexString(providedHex);
      }
      catch (FormatException)
      {
          return false;
      }

      using var hmac = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(secret));
      var expected = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));

      return CryptographicOperations.FixedTimeEquals(providedBytes, expected);
  }
  ```
  Ensure `using System.Security.Cryptography;` and `using AFK4.Shared.Contracts.Payments;` (the latter is already imported) are present in `Program.cs`.

- [ ] **Step 4: Run the tests — expect PASS.**
  ```bash
  dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~DcGateWebhookEndpointTests"
  ```
  Expected: paid credits once + fulfils; replay no double-credit; bad/missing signature 401 + no credit; unknown order 200 no-op; expired flips intent.

- [ ] **Step 5: Commit.**
  ```bash
  git add -A && git commit -m "Add dcgate webhook: HMAC verify, idempotent credit, intent transitions"
  ```

---

## Verification gate

The full Platform.Api test suite must stay green (currently ~936 passing; this plan adds ~13):
```bash
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj
```
Also confirm the solution builds with warnings-as-errors:
```bash
dotnet build src/AFK4.Platform.Api/AFK4.Platform.Api.csproj
```

## Operational dependency (not code — collect at execution time)

AFK4 must be registered as a `dcgate` project before the online path works in any real environment:
- **`webhookUrl`** = `https://<afk4-api-host>/api/public/payments/dcgate/webhook` (give this to dcgate).
- **Project API key** → AFK4 secret `DcGate:ApiKey`.
- **Webhook secret** → AFK4 secret `DcGate:WebhookSecret`.
- **Confirmed prod base URL** (e.g. `https://dcgate.mubi.dev`) → `DcGate:BaseUrl`.

These are secrets: keep them out of `appsettings.json` (committed file holds empty placeholders only) and supply them via environment/user-secrets/Key Vault. The user provides these three values at Unit 2 execution.

## Known constraint to flag during execution

`TopUpWalletAsync` requires an **open shift** at the intent's branch. A `payment.paid` webhook that arrives while no shift is open returns `503` and is redelivered by dcgate; the wallet is credited once a shift opens. This matches the operator `fulfil` constraint and is intentional (every money movement lands inside a shift for cash-reconciliation). If product wants gateway top-ups to credit outside shift hours, that is a separate decision — surface it before changing the credit path.
