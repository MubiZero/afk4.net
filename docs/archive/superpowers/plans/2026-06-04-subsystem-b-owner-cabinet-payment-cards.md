# Subsystem B — Owner Cabinet Payment-Card Onboarding — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give a club Owner a self-serve "Приём платежей" cabinet to connect their own DC-Bank card per branch (or org-wide) — provision a dcgate project (Phase 1) and attach the receiving Telegram account interactively (Phase 2) — so multi-tenant online top-up actually works end to end.

**Architecture:** A new owner-gated section of the operator API + web app, built on Subsystem A's data model (`BranchPaymentGatewayEntity`, `ISecretProtector`, `IBranchPaymentGatewayResolver`) and Subsystem C's hosted dcgate admin/attach API (already live at `https://dcgate.mubi.dev`). AFK4 holds the dcgate `AdminSecret` server-side and proxies every admin/attach call; the owner never sees it. Card numbers and Telegram codes are relayed, never persisted (only `CardLast4` is kept).

**Tech Stack:** ASP.NET Core Minimal API + EF Core (PostgreSQL), `System.Net.Http.Json`, AES-256-GCM; React + TypeScript (Vite), `bun test` + `@testing-library/react`, custom i18n (`packages/i18n`, locales `ru/en/tg`).

---

## Context the engineer needs (read before starting)

**Subsystem A is already merged.** These exist and MUST be reused, not recreated:
- `src/AFK4.Platform.Api/Data/BranchPaymentGatewayEntity.cs` — the gateway row (one per dcgate project = one card). Fields: `BranchPaymentGatewayId` (PK), `OrganizationId`, `BranchId` (nullable → org-level fallback), `DcgateProjectId` (unique), `ApiKeyEncrypted`, `WebhookSecretEncrypted`, `CardLast4`, `Status` (`pending_telegram`/`active`/`disabled`), `CreatedAtUtc`, `UpdatedAtUtc`.
- `src/AFK4.Platform.Api/Data/PlatformDbContext.cs` — `DbSet<BranchPaymentGatewayEntity> BranchPaymentGateways` (line 11); entity config at lines ~787–798.
- `src/AFK4.Platform.Api/Security/ISecretProtector.cs` — `string Protect(string)` / `string Unprotect(string)` (AES-256-GCM, throws `CryptographicException` on tamper). Registered singleton.
- `src/AFK4.Platform.Api/Payments/IBranchPaymentGatewayResolver.cs` + `EfBranchPaymentGatewayResolver.cs` — A gates top-up on `Status=active`. **Do not change A's hot-path gating.**
- `src/AFK4.Platform.Api/Payments/DcGate/DcGateOptions.cs` — config section `DcGate`, currently only `BaseUrl`.
- `src/AFK4.Platform.Api/Identity/StaffAuthorizationService.cs` — `RequireOrganizationPermission(string permission)` (sync, no branch scope) and `RequireBranchPermissionAsync(branchId, permission, ct)`. Returns `StaffAuthorizationResult` with `IsAuthenticated`, `IsAllowed`, `StaffContext` (has `OrganizationId`, `BranchIds`, `Permissions`).
- `src/AFK4.Shared.Contracts/Identity/StaffPermissionNames.cs` — permission string constants, format `"domain.resource.action"`.
- `src/AFK4.Platform.Api/Identity/PermissionCatalog.cs` — role→permission sets; `[StaffRoleNames.Owner]` HashSet starts at line 10.

**Subsystem C is live (the B-facing contract).** dcgate base `https://dcgate.mubi.dev`, all admin calls guarded by `ADMIN_JWT_SECRET` via header `x-admin-secret` (or `Authorization: Bearer`). Telegram `apiId`/`apiHash` are platform-level inside dcgate — **B never sends them.**
- `POST /api/admin/projects` — body `{ name, cardNumber, paymentExpiresInMinutes, webhookUrl?, webhookSecret?, externalId? }`. When `webhookSecret` omitted dcgate mints+returns one **once**; when `externalId` (we pass the new `BranchPaymentGatewayId`) repeats, returns `{...project, idempotentReplay:true}` with **no** apiKey/secret. → **persist apiKey + webhookSecret on the FIRST response.** Success body: `{ id, name, status, paymentExpiresInMinutes, webhookUrl?, cardLast4, apiKey, webhookSecret? }`.
- `POST /api/admin/projects/{id}/telegram-session/start` — `{ phone }` → `{ loginAttemptId, state:"code_required" }`.
- `POST /api/admin/projects/{id}/telegram-session/verify-code` — `{ loginAttemptId, code }` → `{ state:"attached" | "password_required" }`.
- `POST /api/admin/projects/{id}/telegram-session/verify-password` — `{ loginAttemptId, password }` → `{ state:"attached" }`.
- `GET /api/admin/projects/{id}/status` — `{ sessionHealth, lastConnectedAt, lastMessageAt, telegramMessagesCount }`. `sessionHealth` is `online`/`offline`/`configured`.
- Errors surface as 4xx with the gramjs/Nest message in the body.

**Design decisions locked for this plan:**
1. **Hot-path gating stays on `Status=active`** (A's behavior). Live `sessionHealth` is shown only in the cabinet, not polled on every player top-up.
2. **One active gateway per scope** (A deferred this to B): enforced in **app logic** at provision time — reject a second non-`disabled` gateway for the same `(OrganizationId, BranchId)` with `409 gateway_scope_taken`. (No DB partial-unique index in this plan; app-level guard is sufficient and matches the resolver's `SingleOrDefault` expectation.)
3. **Idempotent provision:** generate the `BranchPaymentGatewayId` Guid up front and pass it as dcgate `externalId`; persist the row in the same request right after the dcgate success response.
4. **Webhook URL** for provisioning comes from new config `DcGate:WebhookUrl` (full public URL, e.g. `https://afk4.staging.mubi.dev/api/public/payments/dcgate/webhook`). Empty ⇒ provisioning disabled (returns `online_payment_unavailable`-style 503), same fail-safe posture as the encryption key.
5. **`loginAttemptId` is NOT persisted in AFK4** — it lives only client-side between start→verify within one attach session, passed back in each verify request body. (dcgate holds the live login in memory keyed by it.)
6. **New owner section is a separate React file** (`PaymentGatewaysWorkspace.tsx`), not piled into the already-10k-line `App.tsx`; `App.tsx` only registers and renders it.

**Build/verify commands:**
- Backend tests: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj`
- Backend single test: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~<TestName>"`
- Migration: `dotnet ef migrations add <Name> --project src/AFK4.Platform.Api` (only if a schema change is introduced — this plan introduces none beyond A).
- Web build: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run build` (runs `tsc -b && vite build` — catches type errors esbuild skips).
- Web tests: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test`
- i18n regen: `cd packages/i18n && /home/fedya/.bun/bin/bun run gen` (after editing `locales/*.json`).

> NOTE: `bun` is NOT on PATH in the Bash tool — always use the full path `/home/fedya/.bun/bin/bun`.

---

## File Structure

**Backend — new files:**
- `src/AFK4.Platform.Api/Payments/DcGate/IDcGateAdminClient.cs` — admin client interface + result DTOs.
- `src/AFK4.Platform.Api/Payments/DcGate/DcGateAdminClient.cs` — typed HTTP client to dcgate admin/attach API.
- `src/AFK4.Shared.Contracts/Payments/OwnerPaymentGatewayDtos.cs` — request/response DTOs shared with the web app.

**Backend — modified files:**
- `src/AFK4.Platform.Api/Payments/DcGate/DcGateOptions.cs` — add `AdminSecret`, `WebhookUrl`, `PaymentExpiresInMinutes`.
- `src/AFK4.Platform.Api/appsettings.json` — add empty placeholders for the new config keys.
- `src/AFK4.Shared.Contracts/Identity/StaffPermissionNames.cs` — add `ManagePaymentGateways`.
- `src/AFK4.Platform.Api/Identity/PermissionCatalog.cs` — grant it to `Owner` only.
- `src/AFK4.Platform.Api/Program.cs` — register the admin client + the named HttpClient; map the six owner endpoints.

**Backend — new tests:**
- `tests/AFK4.Platform.Api.Tests/DcGateAdminClientTests.cs`
- `tests/AFK4.Platform.Api.Tests/OwnerPaymentGatewayEndpointTests.cs`

**Frontend — new files:**
- `src/AFK4.Operator.App.Web/src/PaymentGatewaysWorkspace.tsx` — the cabinet section.
- `src/AFK4.Operator.App.Web/src/PaymentGatewaysWorkspace.test.tsx` — component test.

**Frontend — modified files:**
- `src/AFK4.Operator.App.Web/src/operatorApiClients.ts` — add `paymentGateways` client + DTOs.
- `src/AFK4.Operator.App.Web/src/App.tsx` — add workspace id, permission name, nav wiring, render block.
- `src/AFK4.Operator.App.Web/src/operatorData.ts` — add nav item.
- `locales/ru.json`, `locales/en.json`, `locales/tg.json` — strings.
- `src/AFK4.Operator.App.Web/src/styles.css` — section styles.

---

## PART 1 — BACKEND

### Task 1: Extend `DcGateOptions` with admin config

**Files:**
- Modify: `src/AFK4.Platform.Api/Payments/DcGate/DcGateOptions.cs`
- Modify: `src/AFK4.Platform.Api/appsettings.json:19-21`

- [ ] **Step 1: Add the new options**

Replace the body of `DcGateOptions`:

```csharp
namespace AFK4.Platform.Api.Payments.DcGate;

public sealed class DcGateOptions
{
    public const string SectionName = "DcGate";

    // dcgate base URL, e.g. https://dcgate.mubi.dev
    public string BaseUrl { get; set; } = string.Empty;

    // dcgate ADMIN_JWT_SECRET — sent as the x-admin-secret header on /api/admin/* calls.
    // Empty => owner provisioning/attach is disabled (fail-safe, like the encryption key).
    public string AdminSecret { get; set; } = string.Empty;

    // Full public webhook URL stamped into newly provisioned dcgate projects,
    // e.g. https://afk4.staging.mubi.dev/api/public/payments/dcgate/webhook
    public string WebhookUrl { get; set; } = string.Empty;

    // Payment-link expiry stamped on provisioned projects.
    public int PaymentExpiresInMinutes { get; set; } = 30;
}
```

- [ ] **Step 2: Add placeholders to appsettings.json**

In `src/AFK4.Platform.Api/appsettings.json`, change the `DcGate` block:

```json
"DcGate": {
  "BaseUrl": "",
  "AdminSecret": "",
  "WebhookUrl": "",
  "PaymentExpiresInMinutes": 30
},
```

- [ ] **Step 3: Build to verify**

Run: `dotnet build src/AFK4.Platform.Api/AFK4.Platform.Api.csproj`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/AFK4.Platform.Api/Payments/DcGate/DcGateOptions.cs src/AFK4.Platform.Api/appsettings.json
git commit -m "feat(payments): add dcgate admin/webhook config to DcGateOptions"
```

---

### Task 2: `IDcGateAdminClient` interface + result DTOs

**Files:**
- Create: `src/AFK4.Platform.Api/Payments/DcGate/IDcGateAdminClient.cs`

- [ ] **Step 1: Write the interface and DTOs**

```csharp
namespace AFK4.Platform.Api.Payments.DcGate;

public interface IDcGateAdminClient
{
    // Phase 1: provision a dcgate project (= one card). externalId lets dcgate dedupe replays.
    Task<DcGateAdminProjectResult> CreateProjectAsync(
        DcGateCreateProjectRequest request,
        CancellationToken cancellationToken);

    // Phase 2 attach proxy.
    Task<DcGateTelegramStartResult> StartTelegramAsync(
        string dcgateProjectId,
        string phone,
        CancellationToken cancellationToken);

    Task<DcGateTelegramVerifyResult> VerifyTelegramCodeAsync(
        string dcgateProjectId,
        string loginAttemptId,
        string code,
        CancellationToken cancellationToken);

    Task<DcGateTelegramVerifyResult> VerifyTelegramPasswordAsync(
        string dcgateProjectId,
        string loginAttemptId,
        string password,
        CancellationToken cancellationToken);

    Task<DcGateProjectStatusResult> GetStatusAsync(
        string dcgateProjectId,
        CancellationToken cancellationToken);
}

public sealed record DcGateCreateProjectRequest(
    string Name,
    string CardNumber,
    string WebhookUrl,
    int PaymentExpiresInMinutes,
    string ExternalId);

// apiKey + webhookSecret are present only on the FIRST (non-replay) response.
public sealed record DcGateAdminProjectResult(
    string Id,
    string Status,
    string CardLast4,
    string? ApiKey,
    string? WebhookSecret,
    bool IdempotentReplay);

public sealed record DcGateTelegramStartResult(
    string LoginAttemptId,
    string State);

public sealed record DcGateTelegramVerifyResult(
    string State);

public sealed record DcGateProjectStatusResult(
    string SessionHealth,
    DateTimeOffset? LastConnectedAt,
    DateTimeOffset? LastMessageAt,
    int TelegramMessagesCount);
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build src/AFK4.Platform.Api/AFK4.Platform.Api.csproj`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/AFK4.Platform.Api/Payments/DcGate/IDcGateAdminClient.cs
git commit -m "feat(payments): add IDcGateAdminClient contract and DTOs"
```

---

### Task 3: `DcGateAdminClient` implementation (TDD)

**Files:**
- Test: `tests/AFK4.Platform.Api.Tests/DcGateAdminClientTests.cs`
- Create: `src/AFK4.Platform.Api/Payments/DcGate/DcGateAdminClient.cs`

Mirror the existing `DcGateClientTests.cs` `StubHandler : HttpMessageHandler` style.

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AFK4.Platform.Api.Payments.DcGate;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public sealed class DcGateAdminClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest;
        public string? LastBody;
        private readonly HttpStatusCode status;
        private readonly string body;

        public StubHandler(HttpStatusCode status, string body)
        {
            this.status = status;
            this.body = body;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
            };
        }
    }

    private static DcGateAdminClient Create(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://dcgate.test") }, "admin-secret-123");

    [Fact]
    public async Task CreateProjectAsync_sends_admin_secret_and_parses_apikey_and_secret()
    {
        var handler = new StubHandler(HttpStatusCode.OK,
            """{"id":"proj_1","status":"pending_telegram","cardLast4":"4242","apiKey":"key_live","webhookSecret":"whsec_abc"}""");
        var client = Create(handler);

        var result = await client.CreateProjectAsync(
            new DcGateCreateProjectRequest("AFK4 / Org / Branch", "4111111111114242",
                "https://afk4.test/api/public/payments/dcgate/webhook", 30, "11111111-1111-1111-1111-111111111111"),
            CancellationToken.None);

        Assert.Equal("proj_1", result.Id);
        Assert.Equal("4242", result.CardLast4);
        Assert.Equal("key_live", result.ApiKey);
        Assert.Equal("whsec_abc", result.WebhookSecret);
        Assert.False(result.IdempotentReplay);
        Assert.Equal("admin-secret-123", handler.LastRequest!.Headers.GetValues("x-admin-secret").Single());
        Assert.Contains("\"externalId\":\"11111111-1111-1111-1111-111111111111\"", handler.LastBody);
    }

    [Fact]
    public async Task CreateProjectAsync_marks_idempotent_replay_when_apikey_absent()
    {
        var handler = new StubHandler(HttpStatusCode.OK,
            """{"id":"proj_1","status":"pending_telegram","cardLast4":"4242","idempotentReplay":true}""");
        var client = Create(handler);

        var result = await client.CreateProjectAsync(
            new DcGateCreateProjectRequest("n", "4111111111114242", "https://afk4.test/wh", 30, "x"),
            CancellationToken.None);

        Assert.True(result.IdempotentReplay);
        Assert.Null(result.ApiKey);
    }

    [Fact]
    public async Task StartTelegramAsync_posts_phone_and_parses_attempt()
    {
        var handler = new StubHandler(HttpStatusCode.OK,
            """{"loginAttemptId":"att_9","state":"code_required"}""");
        var client = Create(handler);

        var result = await client.StartTelegramAsync("proj_1", "+992900000000", CancellationToken.None);

        Assert.Equal("att_9", result.LoginAttemptId);
        Assert.Equal("code_required", result.State);
        Assert.Equal("/api/admin/projects/proj_1/telegram-session/start", handler.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetStatusAsync_parses_session_health()
    {
        var handler = new StubHandler(HttpStatusCode.OK,
            """{"sessionHealth":"online","lastConnectedAt":"2026-06-04T10:00:00Z","lastMessageAt":null,"telegramMessagesCount":7}""");
        var client = Create(handler);

        var result = await client.GetStatusAsync("proj_1", CancellationToken.None);

        Assert.Equal("online", result.SessionHealth);
        Assert.Equal(7, result.TelegramMessagesCount);
    }

    [Fact]
    public async Task CreateProjectAsync_throws_with_dcgate_message_on_4xx()
    {
        var handler = new StubHandler(HttpStatusCode.BadRequest, """{"message":"card already in use"}""");
        var client = Create(handler);

        var ex = await Assert.ThrowsAsync<DcGateAdminException>(() =>
            client.CreateProjectAsync(new DcGateCreateProjectRequest("n", "4111", "wh", 30, "x"), CancellationToken.None));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Contains("card already in use", ex.Message);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~DcGateAdminClientTests"`
Expected: FAIL — `DcGateAdminClient` / `DcGateAdminException` do not exist.

- [ ] **Step 3: Implement the client**

Create `src/AFK4.Platform.Api/Payments/DcGate/DcGateAdminClient.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace AFK4.Platform.Api.Payments.DcGate;

// Thrown when dcgate returns a non-success status; carries the dcgate message verbatim
// so the owner endpoint can relay it as a 4xx (Subsystem B error-handling contract).
public sealed class DcGateAdminException(HttpStatusCode statusCode, string message) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}

public sealed class DcGateAdminClient : IDcGateAdminClient
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly HttpClient httpClient;
    private readonly string adminSecret;

    public DcGateAdminClient(HttpClient httpClient, string adminSecret)
    {
        this.httpClient = httpClient;
        this.adminSecret = adminSecret;
    }

    public async Task<DcGateAdminProjectResult> CreateProjectAsync(
        DcGateCreateProjectRequest request, CancellationToken cancellationToken)
    {
        using var http = BuildRequest(HttpMethod.Post, "/api/admin/projects", new
        {
            name = request.Name,
            cardNumber = request.CardNumber,
            webhookUrl = request.WebhookUrl,
            paymentExpiresInMinutes = request.PaymentExpiresInMinutes,
            externalId = request.ExternalId
        });

        using var doc = await SendAsync(http, cancellationToken);
        var root = doc.RootElement;
        return new DcGateAdminProjectResult(
            Id: root.GetProperty("id").GetString() ?? throw Empty("id"),
            Status: root.TryGetProperty("status", out var st) ? st.GetString() ?? "" : "",
            CardLast4: root.TryGetProperty("cardLast4", out var cl) ? cl.GetString() ?? "" : "",
            ApiKey: root.TryGetProperty("apiKey", out var ak) ? ak.GetString() : null,
            WebhookSecret: root.TryGetProperty("webhookSecret", out var ws) ? ws.GetString() : null,
            IdempotentReplay: root.TryGetProperty("idempotentReplay", out var ir) && ir.GetBoolean());
    }

    public async Task<DcGateTelegramStartResult> StartTelegramAsync(
        string dcgateProjectId, string phone, CancellationToken cancellationToken)
    {
        using var http = BuildRequest(HttpMethod.Post,
            $"/api/admin/projects/{dcgateProjectId}/telegram-session/start", new { phone });
        using var doc = await SendAsync(http, cancellationToken);
        var root = doc.RootElement;
        return new DcGateTelegramStartResult(
            root.GetProperty("loginAttemptId").GetString() ?? throw Empty("loginAttemptId"),
            root.GetProperty("state").GetString() ?? throw Empty("state"));
    }

    public async Task<DcGateTelegramVerifyResult> VerifyTelegramCodeAsync(
        string dcgateProjectId, string loginAttemptId, string code, CancellationToken cancellationToken)
    {
        using var http = BuildRequest(HttpMethod.Post,
            $"/api/admin/projects/{dcgateProjectId}/telegram-session/verify-code",
            new { loginAttemptId, code });
        using var doc = await SendAsync(http, cancellationToken);
        return new DcGateTelegramVerifyResult(
            doc.RootElement.GetProperty("state").GetString() ?? throw Empty("state"));
    }

    public async Task<DcGateTelegramVerifyResult> VerifyTelegramPasswordAsync(
        string dcgateProjectId, string loginAttemptId, string password, CancellationToken cancellationToken)
    {
        using var http = BuildRequest(HttpMethod.Post,
            $"/api/admin/projects/{dcgateProjectId}/telegram-session/verify-password",
            new { loginAttemptId, password });
        using var doc = await SendAsync(http, cancellationToken);
        return new DcGateTelegramVerifyResult(
            doc.RootElement.GetProperty("state").GetString() ?? throw Empty("state"));
    }

    public async Task<DcGateProjectStatusResult> GetStatusAsync(
        string dcgateProjectId, CancellationToken cancellationToken)
    {
        using var http = BuildRequest(HttpMethod.Get,
            $"/api/admin/projects/{dcgateProjectId}/status", content: null);
        using var doc = await SendAsync(http, cancellationToken);
        var root = doc.RootElement;
        return new DcGateProjectStatusResult(
            SessionHealth: root.TryGetProperty("sessionHealth", out var sh) ? sh.GetString() ?? "offline" : "offline",
            LastConnectedAt: ReadDate(root, "lastConnectedAt"),
            LastMessageAt: ReadDate(root, "lastMessageAt"),
            TelegramMessagesCount: root.TryGetProperty("telegramMessagesCount", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetInt32() : 0);
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string path, object? content)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation("x-admin-secret", adminSecret);
        if (content is not null)
        {
            request.Content = JsonContent.Create(content);
        }
        return request;
    }

    private async Task<JsonDocument> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new DcGateAdminException(response.StatusCode, ExtractMessage(payload, response.StatusCode));
        }
        return JsonDocument.Parse(string.IsNullOrWhiteSpace(payload) ? "{}" : payload);
    }

    private static string ExtractMessage(string payload, HttpStatusCode status)
    {
        if (!string.IsNullOrWhiteSpace(payload))
        {
            try
            {
                using var doc = JsonDocument.Parse(payload);
                if (doc.RootElement.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
                {
                    return m.GetString()!;
                }
            }
            catch (JsonException) { /* fall through to status text */ }
        }
        return $"dcgate admin call failed ({(int)status}).";
    }

    private static DateTimeOffset? ReadDate(JsonElement root, string name) =>
        root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetDateTimeOffset()
            : null;

    private static InvalidOperationException Empty(string field) =>
        new($"dcgate admin response missing '{field}'.");
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~DcGateAdminClientTests"`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Api/Payments/DcGate/DcGateAdminClient.cs tests/AFK4.Platform.Api.Tests/DcGateAdminClientTests.cs
git commit -m "feat(payments): implement DcGateAdminClient with admin-secret auth"
```

---

### Task 4: Register the admin client in DI

**Files:**
- Modify: `src/AFK4.Platform.Api/Program.cs` (DcGate registration block, ~lines 273–282)

- [ ] **Step 1: Add a named HttpClient + singleton registration**

Right after the existing `AddSingleton<IDcGateClientFactory, DcGateClientFactory>();` line, add:

```csharp
builder.Services.AddHttpClient(DcGateAdminClientRegistration.HttpClientName, (provider, http) =>
{
    var opts = provider.GetRequiredService<IOptions<DcGateOptions>>().Value;
    if (!string.IsNullOrWhiteSpace(opts.BaseUrl))
    {
        http.BaseAddress = new Uri(opts.BaseUrl);
    }
});
builder.Services.AddSingleton<IDcGateAdminClient>(provider =>
{
    var opts = provider.GetRequiredService<IOptions<DcGateOptions>>().Value;
    var factory = provider.GetRequiredService<IHttpClientFactory>();
    return new DcGateAdminClient(
        factory.CreateClient(DcGateAdminClientRegistration.HttpClientName),
        opts.AdminSecret);
});
```

- [ ] **Step 2: Add the registration constant**

At the bottom of `src/AFK4.Platform.Api/Payments/DcGate/DcGateAdminClient.cs`, add:

```csharp
public static class DcGateAdminClientRegistration
{
    public const string HttpClientName = "dcgate-admin";
}
```

(Ensure `using AFK4.Platform.Api.Payments.DcGate;` and `using Microsoft.Extensions.Options;` are present in `Program.cs` — they already are, from Subsystem A.)

- [ ] **Step 3: Build to verify**

Run: `dotnet build src/AFK4.Platform.Api/AFK4.Platform.Api.csproj`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/AFK4.Platform.Api/Program.cs src/AFK4.Platform.Api/Payments/DcGate/DcGateAdminClient.cs
git commit -m "feat(payments): register IDcGateAdminClient in DI"
```

---

### Task 5: Add the `ManagePaymentGateways` permission (Owner only)

**Files:**
- Modify: `src/AFK4.Shared.Contracts/Identity/StaffPermissionNames.cs:108`
- Modify: `src/AFK4.Platform.Api/Identity/PermissionCatalog.cs` (Owner HashSet)

- [ ] **Step 1: Add the constant**

After `ManageBranchSettings` (the last constant), add inside the class:

```csharp
    // Owner-only: connect/manage the club's DC-Bank payment cards (dcgate gateways).
    public const string ManagePaymentGateways = "payments.gateways.manage";
```

- [ ] **Step 2: Grant it to Owner**

In `PermissionCatalog.cs`, inside the `[StaffRoleNames.Owner]` HashSet, add a line (after `StaffPermissionNames.ManageBranchSettings,` or anywhere in the set):

```csharp
                StaffPermissionNames.ManagePaymentGateways,
```

Do **not** add it to any other role.

- [ ] **Step 3: Build to verify**

Run: `dotnet build src/AFK4.Platform.Api/AFK4.Platform.Api.csproj`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/AFK4.Shared.Contracts/Identity/StaffPermissionNames.cs src/AFK4.Platform.Api/Identity/PermissionCatalog.cs
git commit -m "feat(identity): add Owner-only payments.gateways.manage permission"
```

---

### Task 6: Owner-facing DTOs (shared contracts)

**Files:**
- Create: `src/AFK4.Shared.Contracts/Payments/OwnerPaymentGatewayDtos.cs`

- [ ] **Step 1: Write the DTOs**

```csharp
namespace AFK4.Shared.Contracts.Payments;

// One row in the owner cabinet list.
public sealed record OwnerPaymentGatewayDto(
    Guid BranchPaymentGatewayId,
    Guid? BranchId,
    string DcgateProjectId,
    string CardLast4,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record OwnerPaymentGatewayListResponse(
    IReadOnlyList<OwnerPaymentGatewayDto> Gateways);

// Phase 1 — provision. Null BranchId => org-level (network-wide) gateway.
public sealed record ProvisionPaymentGatewayRequest(
    Guid? BranchId,
    string CardNumber);

// Phase 2 — telegram attach.
public sealed record TelegramStartRequest(string Phone);
public sealed record TelegramStartResponse(string LoginAttemptId, string State);

public sealed record TelegramVerifyCodeRequest(string LoginAttemptId, string Code);
public sealed record TelegramVerifyPasswordRequest(string LoginAttemptId, string Password);
public sealed record TelegramVerifyResponse(string State, string GatewayStatus);

// Live dcgate status proxied to the cabinet.
public sealed record OwnerGatewayStatusResponse(
    string GatewayStatus,
    string SessionHealth,
    DateTimeOffset? LastConnectedAt,
    DateTimeOffset? LastMessageAt,
    int TelegramMessagesCount);
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build src/AFK4.Shared.Contracts/AFK4.Shared.Contracts.csproj`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/AFK4.Shared.Contracts/Payments/OwnerPaymentGatewayDtos.cs
git commit -m "feat(payments): add owner payment-gateway DTOs"
```

---

### Task 7: `GET /api/owner/payment-gateways` — list (TDD)

**Files:**
- Test: `tests/AFK4.Platform.Api.Tests/OwnerPaymentGatewayEndpointTests.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs` (add endpoint near the other payment endpoints, e.g. after the webhook endpoint block)

This task also establishes the shared test helpers reused by Tasks 8–11. Study `DcGateTopUpIntentTests.cs` for the `PlatformApiFactory` + authenticated-staff seeding helpers and copy their conventions (`SeedActiveGatewayAsync`, staff sign-in). Reuse the existing staff-auth test helper used by other owner-gated endpoint tests (search the test project for `RequireOrganizationPermission` coverage or an existing `AuthenticateStaffAsync`/owner sign-in helper and reuse it verbatim — do not invent a new auth path).

- [ ] **Step 1: Write the failing test (list happy path + 403 for non-owner)**

```csharp
using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Security;
using AFK4.Shared.Contracts.Payments;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public sealed class OwnerPaymentGatewayEndpointTests
{
    // Seeds a gateway row directly (encrypting creds) for list/status tests.
    private static async Task<Guid> SeedGatewayAsync(
        PlatformApiFactory factory, Guid orgId, Guid? branchId, string projectId, string status)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();
        var id = Guid.NewGuid();
        db.BranchPaymentGateways.Add(new BranchPaymentGatewayEntity
        {
            BranchPaymentGatewayId = id,
            OrganizationId = orgId,
            BranchId = branchId,
            DcgateProjectId = projectId,
            ApiKeyEncrypted = protector.Protect("key"),
            WebhookSecretEncrypted = protector.Protect("whsec"),
            CardLast4 = "4242",
            Status = status,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task List_returns_only_callers_org_gateways_for_owner()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        // Reuse the project's owner sign-in helper; it returns (orgId, authedClient).
        var (orgId, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);
        await SeedGatewayAsync(factory, orgId, branchId: null, "proj_mine", "active");
        await SeedGatewayAsync(factory, Guid.NewGuid(), branchId: null, "proj_other", "active");

        var response = await owner.GetAsync("/api/owner/payment-gateways");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<OwnerPaymentGatewayListResponse>();
        Assert.Single(body!.Gateways);
        Assert.Equal("proj_mine", body.Gateways[0].DcgateProjectId);
        Assert.Equal("4242", body.Gateways[0].CardLast4);
    }

    [Fact]
    public async Task List_returns_403_for_non_owner()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var nonOwner = await OwnerTestAuth.SignInNonOwnerAsync(factory, client);

        var response = await nonOwner.GetAsync("/api/owner/payment-gateways");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
```

> If no `OwnerTestAuth` helper exists, create `tests/AFK4.Platform.Api.Tests/OwnerTestAuth.cs` adapting the staff sign-in flow already used by existing owner-gated endpoint tests (the one that produces an authenticated `HttpClient` whose staff has `StaffRoleNames.Owner`). `SignInNonOwnerAsync` signs in a staff member with a role lacking `ManagePaymentGateways` (e.g. `CashierOperator`).

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~OwnerPaymentGatewayEndpointTests.List"`
Expected: FAIL — endpoint not mapped (404).

- [ ] **Step 3: Map the list endpoint**

In `Program.cs`, after the dcgate webhook endpoint, add:

```csharp
app.MapGet("/api/owner/payment-gateways", async (
    StaffAuthorizationService authorizationService,
    PlatformDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequireOrganizationPermission(
        StaffPermissionNames.ManagePaymentGateways);
    if (!authorization.IsAuthenticated)
    {
        return Results.Unauthorized();
    }
    if (!authorization.IsAllowed)
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var orgId = authorization.StaffContext!.OrganizationId;
    var rows = await dbContext.BranchPaymentGateways
        .AsNoTracking()
        .Where(g => g.OrganizationId == orgId)
        .OrderBy(g => g.CreatedAtUtc)
        .Select(g => new OwnerPaymentGatewayDto(
            g.BranchPaymentGatewayId, g.BranchId, g.DcgateProjectId,
            g.CardLast4, g.Status, g.CreatedAtUtc, g.UpdatedAtUtc))
        .ToListAsync(cancellationToken);

    return Results.Ok(new OwnerPaymentGatewayListResponse(rows));
});
```

Ensure `using AFK4.Shared.Contracts.Payments;` and `using AFK4.Shared.Contracts.Identity;` are imported in `Program.cs`.

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~OwnerPaymentGatewayEndpointTests.List"`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Api/Program.cs tests/AFK4.Platform.Api.Tests/OwnerPaymentGatewayEndpointTests.cs tests/AFK4.Platform.Api.Tests/OwnerTestAuth.cs
git commit -m "feat(payments): owner gateway list endpoint (owner-gated)"
```

---

### Task 8: `POST /api/owner/payment-gateways` — Phase 1 provision (TDD)

**Files:**
- Test: `tests/AFK4.Platform.Api.Tests/OwnerPaymentGatewayEndpointTests.cs` (add tests)
- Modify: `src/AFK4.Platform.Api/Program.cs`

Inject a fake `IDcGateAdminClient` the way `DcGateTopUpIntentTests` injects a fake `IDcGateClientFactory` (via `PlatformApiFactory(extraServices: services => { services.RemoveAll<IDcGateAdminClient>(); services.AddSingleton<IDcGateAdminClient>(fake); })`).

- [ ] **Step 1: Write the failing tests**

```csharp
// Add to OwnerPaymentGatewayEndpointTests.cs

private sealed class FakeAdminClient : IDcGateAdminClient
{
    public DcGateCreateProjectRequest? LastCreate;
    public DcGateAdminProjectResult CreateResult = new("proj_new", "pending_telegram", "4242", "key_live", "whsec_x", false);

    public Task<DcGateAdminProjectResult> CreateProjectAsync(DcGateCreateProjectRequest request, CancellationToken ct)
    { LastCreate = request; return Task.FromResult(CreateResult); }
    public Task<DcGateTelegramStartResult> StartTelegramAsync(string p, string phone, CancellationToken ct)
        => Task.FromResult(new DcGateTelegramStartResult("att", "code_required"));
    public Task<DcGateTelegramVerifyResult> VerifyTelegramCodeAsync(string p, string a, string c, CancellationToken ct)
        => Task.FromResult(new DcGateTelegramVerifyResult("attached"));
    public Task<DcGateTelegramVerifyResult> VerifyTelegramPasswordAsync(string p, string a, string pw, CancellationToken ct)
        => Task.FromResult(new DcGateTelegramVerifyResult("attached"));
    public Task<DcGateProjectStatusResult> GetStatusAsync(string p, CancellationToken ct)
        => Task.FromResult(new DcGateProjectStatusResult("online", null, null, 0));
}

private static PlatformApiFactory FactoryWithAdmin(FakeAdminClient fake) =>
    new(extraServices: services =>
    {
        services.RemoveAll<IDcGateAdminClient>();
        services.AddSingleton<IDcGateAdminClient>(fake);
    });

[Fact]
public async Task Provision_persists_encrypted_creds_and_returns_pending_row()
{
    var fake = new FakeAdminClient();
    await using var factory = FactoryWithAdmin(fake);
    var client = factory.CreateClient();
    var (orgId, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);

    var response = await owner.PostAsJsonAsync("/api/owner/payment-gateways",
        new ProvisionPaymentGatewayRequest(BranchId: null, CardNumber: "4111111111114242"));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var dto = await response.Content.ReadFromJsonAsync<OwnerPaymentGatewayDto>();
    Assert.Equal("pending_telegram", dto!.Status);
    Assert.Equal("4242", dto.CardLast4);
    Assert.Equal("proj_new", dto.DcgateProjectId);

    // externalId must equal the persisted PK (idempotency contract).
    Assert.Equal(dto.BranchPaymentGatewayId.ToString(), fake.LastCreate!.ExternalId);

    // creds stored encrypted, not plaintext.
    await using var scope = factory.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
    var row = await db.BranchPaymentGateways.FindAsync(dto.BranchPaymentGatewayId);
    Assert.NotNull(row);
    Assert.NotEqual("key_live", row!.ApiKeyEncrypted);
    Assert.NotEqual("whsec_x", row.WebhookSecretEncrypted);
    var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();
    Assert.Equal("key_live", protector.Unprotect(row.ApiKeyEncrypted));
    Assert.Equal("whsec_x", protector.Unprotect(row.WebhookSecretEncrypted));
}

[Fact]
public async Task Provision_rejects_second_gateway_for_same_scope()
{
    var fake = new FakeAdminClient();
    await using var factory = FactoryWithAdmin(fake);
    var client = factory.CreateClient();
    var (orgId, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);
    await SeedGatewayAsync(factory, orgId, branchId: null, "proj_existing", "pending_telegram");

    var response = await owner.PostAsJsonAsync("/api/owner/payment-gateways",
        new ProvisionPaymentGatewayRequest(BranchId: null, CardNumber: "4111111111114242"));

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
}

[Fact]
public async Task Provision_relays_dcgate_4xx_and_persists_nothing()
{
    var fake = new FakeAdminClient();
    await using var factory = new PlatformApiFactory(extraServices: services =>
    {
        services.RemoveAll<IDcGateAdminClient>();
        services.AddSingleton<IDcGateAdminClient>(new ThrowingAdminClient());
    });
    var client = factory.CreateClient();
    var (orgId, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);

    var response = await owner.PostAsJsonAsync("/api/owner/payment-gateways",
        new ProvisionPaymentGatewayRequest(BranchId: null, CardNumber: "4111111111114242"));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    await using var scope = factory.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
    Assert.Empty(db.BranchPaymentGateways.ToList());
}

private sealed class ThrowingAdminClient : IDcGateAdminClient
{
    public Task<DcGateAdminProjectResult> CreateProjectAsync(DcGateCreateProjectRequest r, CancellationToken ct)
        => throw new DcGateAdminException(HttpStatusCode.BadRequest, "card already in use");
    public Task<DcGateTelegramStartResult> StartTelegramAsync(string p, string phone, CancellationToken ct) => throw new NotImplementedException();
    public Task<DcGateTelegramVerifyResult> VerifyTelegramCodeAsync(string p, string a, string c, CancellationToken ct) => throw new NotImplementedException();
    public Task<DcGateTelegramVerifyResult> VerifyTelegramPasswordAsync(string p, string a, string pw, CancellationToken ct) => throw new NotImplementedException();
    public Task<DcGateProjectStatusResult> GetStatusAsync(string p, CancellationToken ct) => throw new NotImplementedException();
}
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~OwnerPaymentGatewayEndpointTests.Provision"`
Expected: FAIL — endpoint not mapped.

- [ ] **Step 3: Map the provision endpoint**

In `Program.cs`, add (a shared local helper at the top of the owner endpoints group keeps the authz check DRY — but inline is fine if simpler):

```csharp
app.MapPost("/api/owner/payment-gateways", async (
    ProvisionPaymentGatewayRequest request,
    StaffAuthorizationService authorizationService,
    IDcGateAdminClient adminClient,
    ISecretProtector secretProtector,
    IOptions<DcGateOptions> dcGateOptions,
    PlatformDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequireOrganizationPermission(
        StaffPermissionNames.ManagePaymentGateways);
    if (!authorization.IsAuthenticated) return Results.Unauthorized();
    if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

    var orgId = authorization.StaffContext!.OrganizationId;
    var options = dcGateOptions.Value;

    if (string.IsNullOrWhiteSpace(options.AdminSecret) || string.IsNullOrWhiteSpace(options.WebhookUrl))
    {
        return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "online_payment_unavailable",
            detail: "Payment provisioning is not configured on this environment.");
    }

    var cardNumber = (request.CardNumber ?? string.Empty).Trim();
    if (cardNumber.Length < 12)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["cardNumber"] = ["Enter a valid card number."]
        });
    }

    // If a branch scope is given, it must belong to the caller's org and be assigned to them.
    if (request.BranchId is Guid branchScope && !authorization.StaffContext.BranchIds.Contains(branchScope))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    // One active/pending gateway per scope (A deferred this invariant to B).
    var scopeTaken = await dbContext.BranchPaymentGateways.AnyAsync(g =>
        g.OrganizationId == orgId && g.BranchId == request.BranchId && g.Status != "disabled",
        cancellationToken);
    if (scopeTaken)
    {
        return Results.Problem(statusCode: StatusCodes.Status409Conflict,
            title: "gateway_scope_taken",
            detail: "This scope already has a payment card. Disable it before adding another.");
    }

    var gatewayId = Guid.NewGuid();
    var name = $"AFK4 / {orgId} / {(request.BranchId?.ToString() ?? "org")}";

    DcGateAdminProjectResult created;
    try
    {
        created = await adminClient.CreateProjectAsync(
            new DcGateCreateProjectRequest(name, cardNumber, options.WebhookUrl,
                options.PaymentExpiresInMinutes, gatewayId.ToString()),
            cancellationToken);
    }
    catch (DcGateAdminException ex)
    {
        return Results.Problem(statusCode: (int)ex.StatusCode, title: "dcgate_error", detail: ex.Message);
    }

    if (string.IsNullOrEmpty(created.ApiKey) || string.IsNullOrEmpty(created.WebhookSecret))
    {
        // Replay with no creds means we lost the first response — cannot persist usable creds.
        return Results.Problem(statusCode: StatusCodes.Status409Conflict,
            title: "provision_replay_without_secret",
            detail: "dcgate replayed an existing project without returning credentials. Disable and retry.");
    }

    var now = DateTimeOffset.UtcNow;
    var row = new BranchPaymentGatewayEntity
    {
        BranchPaymentGatewayId = gatewayId,
        OrganizationId = orgId,
        BranchId = request.BranchId,
        DcgateProjectId = created.Id,
        ApiKeyEncrypted = secretProtector.Protect(created.ApiKey),
        WebhookSecretEncrypted = secretProtector.Protect(created.WebhookSecret),
        CardLast4 = string.IsNullOrEmpty(created.CardLast4)
            ? cardNumber[^4..]
            : created.CardLast4,
        Status = "pending_telegram",
        CreatedAtUtc = now,
        UpdatedAtUtc = now
    };
    dbContext.BranchPaymentGateways.Add(row);
    await dbContext.SaveChangesAsync(cancellationToken);

    return Results.Ok(new OwnerPaymentGatewayDto(
        row.BranchPaymentGatewayId, row.BranchId, row.DcgateProjectId,
        row.CardLast4, row.Status, row.CreatedAtUtc, row.UpdatedAtUtc));
});
```

- [ ] **Step 4: Run to verify they pass**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~OwnerPaymentGatewayEndpointTests.Provision"`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Api/Program.cs tests/AFK4.Platform.Api.Tests/OwnerPaymentGatewayEndpointTests.cs
git commit -m "feat(payments): owner gateway provision endpoint (Phase 1)"
```

---

### Task 9: Telegram attach endpoints — start / verify-code / verify-password (TDD)

**Files:**
- Test: `tests/AFK4.Platform.Api.Tests/OwnerPaymentGatewayEndpointTests.cs` (add tests)
- Modify: `src/AFK4.Platform.Api/Program.cs`

All three resolve the gateway by `{id}`, **verify it belongs to the caller's org**, proxy to dcgate, and on `verify` returning `"attached"` flip `Status` from `pending_telegram` to `active`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public async Task TelegramStart_proxies_and_returns_attempt()
{
    var fake = new FakeAdminClient();
    await using var factory = FactoryWithAdmin(fake);
    var client = factory.CreateClient();
    var (orgId, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);
    var id = await SeedGatewayAsync(factory, orgId, null, "proj_1", "pending_telegram");

    var response = await owner.PostAsJsonAsync($"/api/owner/payment-gateways/{id}/telegram/start",
        new TelegramStartRequest("+992900000000"));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var body = await response.Content.ReadFromJsonAsync<TelegramStartResponse>();
    Assert.Equal("code_required", body!.State);
}

[Fact]
public async Task VerifyPassword_attached_flips_gateway_to_active()
{
    var fake = new FakeAdminClient(); // verify-password returns "attached"
    await using var factory = FactoryWithAdmin(fake);
    var client = factory.CreateClient();
    var (orgId, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);
    var id = await SeedGatewayAsync(factory, orgId, null, "proj_1", "pending_telegram");

    var response = await owner.PostAsJsonAsync($"/api/owner/payment-gateways/{id}/telegram/verify-password",
        new TelegramVerifyPasswordRequest("att", "2fa-pass"));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var body = await response.Content.ReadFromJsonAsync<TelegramVerifyResponse>();
    Assert.Equal("attached", body!.State);
    Assert.Equal("active", body.GatewayStatus);

    await using var scope = factory.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
    var row = await db.BranchPaymentGateways.FindAsync(id);
    Assert.Equal("active", row!.Status);
}

[Fact]
public async Task TelegramStart_404_for_other_orgs_gateway()
{
    var fake = new FakeAdminClient();
    await using var factory = FactoryWithAdmin(fake);
    var client = factory.CreateClient();
    var (orgId, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);
    var foreignId = await SeedGatewayAsync(factory, Guid.NewGuid(), null, "proj_foreign", "pending_telegram");

    var response = await owner.PostAsJsonAsync($"/api/owner/payment-gateways/{foreignId}/telegram/start",
        new TelegramStartRequest("+992900000000"));

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
}
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~OwnerPaymentGatewayEndpointTests" --filter "FullyQualifiedName~Telegram|VerifyPassword"`
Expected: FAIL — endpoints not mapped. (If the double `--filter` is rejected by the runner, run the whole `OwnerPaymentGatewayEndpointTests` class.)

- [ ] **Step 3: Map the three endpoints**

In `Program.cs`, add a private resolve helper and the three maps. Because all three share the org-ownership lookup, define a local function just above them:

```csharp
// Resolves a gateway by id scoped to the authenticated owner's org.
// Returns (row, errorResult). One of the two is null.
static async Task<(BranchPaymentGatewayEntity? Row, IResult? Error)> ResolveOwnerGatewayAsync(
    Guid gatewayId,
    StaffAuthorizationService authorizationService,
    PlatformDbContext dbContext,
    CancellationToken ct)
{
    var authorization = authorizationService.RequireOrganizationPermission(
        StaffPermissionNames.ManagePaymentGateways);
    if (!authorization.IsAuthenticated) return (null, Results.Unauthorized());
    if (!authorization.IsAllowed) return (null, Results.StatusCode(StatusCodes.Status403Forbidden));

    var orgId = authorization.StaffContext!.OrganizationId;
    var row = await dbContext.BranchPaymentGateways
        .FirstOrDefaultAsync(g => g.BranchPaymentGatewayId == gatewayId && g.OrganizationId == orgId, ct);
    return row is null ? (null, Results.NotFound()) : (row, null);
}

app.MapPost("/api/owner/payment-gateways/{id:guid}/telegram/start", async (
    Guid id, TelegramStartRequest request,
    StaffAuthorizationService authorizationService,
    IDcGateAdminClient adminClient,
    PlatformDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var (row, error) = await ResolveOwnerGatewayAsync(id, authorizationService, dbContext, cancellationToken);
    if (error is not null) return error;
    try
    {
        var result = await adminClient.StartTelegramAsync(row!.DcgateProjectId, request.Phone, cancellationToken);
        return Results.Ok(new TelegramStartResponse(result.LoginAttemptId, result.State));
    }
    catch (DcGateAdminException ex)
    {
        return Results.Problem(statusCode: (int)ex.StatusCode, title: "dcgate_error", detail: ex.Message);
    }
});

app.MapPost("/api/owner/payment-gateways/{id:guid}/telegram/verify-code", async (
    Guid id, TelegramVerifyCodeRequest request,
    StaffAuthorizationService authorizationService,
    IDcGateAdminClient adminClient,
    PlatformDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var (row, error) = await ResolveOwnerGatewayAsync(id, authorizationService, dbContext, cancellationToken);
    if (error is not null) return error;
    try
    {
        var result = await adminClient.VerifyTelegramCodeAsync(
            row!.DcgateProjectId, request.LoginAttemptId, request.Code, cancellationToken);
        var status = await ApplyAttachResultAsync(row, result.State, dbContext, cancellationToken);
        return Results.Ok(new TelegramVerifyResponse(result.State, status));
    }
    catch (DcGateAdminException ex)
    {
        return Results.Problem(statusCode: (int)ex.StatusCode, title: "dcgate_error", detail: ex.Message);
    }
});

app.MapPost("/api/owner/payment-gateways/{id:guid}/telegram/verify-password", async (
    Guid id, TelegramVerifyPasswordRequest request,
    StaffAuthorizationService authorizationService,
    IDcGateAdminClient adminClient,
    PlatformDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var (row, error) = await ResolveOwnerGatewayAsync(id, authorizationService, dbContext, cancellationToken);
    if (error is not null) return error;
    try
    {
        var result = await adminClient.VerifyTelegramPasswordAsync(
            row!.DcgateProjectId, request.LoginAttemptId, request.Password, cancellationToken);
        var status = await ApplyAttachResultAsync(row, result.State, dbContext, cancellationToken);
        return Results.Ok(new TelegramVerifyResponse(result.State, status));
    }
    catch (DcGateAdminException ex)
    {
        return Results.Problem(statusCode: (int)ex.StatusCode, title: "dcgate_error", detail: ex.Message);
    }
});

// Flip pending_telegram -> active once dcgate reports the session attached.
static async Task<string> ApplyAttachResultAsync(
    BranchPaymentGatewayEntity row, string state, PlatformDbContext dbContext, CancellationToken ct)
{
    if (state == "attached" && row.Status == "pending_telegram")
    {
        row.Status = "active";
        row.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(ct);
    }
    return row.Status;
}
```

> Local `static` functions in a top-level `Program.cs` must be declared before first use or be plain (non-captured) statics. Place `ResolveOwnerGatewayAsync` and `ApplyAttachResultAsync` near the other owner endpoints; both take all deps as parameters so they capture nothing.

- [ ] **Step 4: Run to verify they pass**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~OwnerPaymentGatewayEndpointTests"`
Expected: PASS (all owner tests so far).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Api/Program.cs tests/AFK4.Platform.Api.Tests/OwnerPaymentGatewayEndpointTests.cs
git commit -m "feat(payments): owner telegram attach proxy endpoints (Phase 2)"
```

---

### Task 10: `GET /api/owner/payment-gateways/{id}/status` — proxy live status (TDD)

**Files:**
- Test: `tests/AFK4.Platform.Api.Tests/OwnerPaymentGatewayEndpointTests.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public async Task Status_proxies_dcgate_session_health()
{
    var fake = new FakeAdminClient(); // GetStatusAsync returns ("online", ...)
    await using var factory = FactoryWithAdmin(fake);
    var client = factory.CreateClient();
    var (orgId, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);
    var id = await SeedGatewayAsync(factory, orgId, null, "proj_1", "active");

    var response = await owner.GetAsync($"/api/owner/payment-gateways/{id}/status");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var body = await response.Content.ReadFromJsonAsync<OwnerGatewayStatusResponse>();
    Assert.Equal("online", body!.SessionHealth);
    Assert.Equal("active", body.GatewayStatus);
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~OwnerPaymentGatewayEndpointTests.Status"`
Expected: FAIL — endpoint not mapped.

- [ ] **Step 3: Map the status endpoint**

```csharp
app.MapGet("/api/owner/payment-gateways/{id:guid}/status", async (
    Guid id,
    StaffAuthorizationService authorizationService,
    IDcGateAdminClient adminClient,
    PlatformDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var (row, error) = await ResolveOwnerGatewayAsync(id, authorizationService, dbContext, cancellationToken);
    if (error is not null) return error;
    try
    {
        var status = await adminClient.GetStatusAsync(row!.DcgateProjectId, cancellationToken);
        return Results.Ok(new OwnerGatewayStatusResponse(
            row.Status, status.SessionHealth, status.LastConnectedAt,
            status.LastMessageAt, status.TelegramMessagesCount));
    }
    catch (DcGateAdminException ex)
    {
        return Results.Problem(statusCode: (int)ex.StatusCode, title: "dcgate_error", detail: ex.Message);
    }
});
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~OwnerPaymentGatewayEndpointTests.Status"`
Expected: PASS.

- [ ] **Step 5: Run the FULL backend suite**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj`
Expected: PASS (no regressions in A's tests).

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Platform.Api/Program.cs tests/AFK4.Platform.Api.Tests/OwnerPaymentGatewayEndpointTests.cs
git commit -m "feat(payments): owner gateway live-status proxy endpoint"
```

---

## PART 2 — FRONTEND

### Task 11: API client + DTOs for the cabinet

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/operatorApiClients.ts`

- [ ] **Step 1: Add DTOs and the client function**

Near the other DTO interfaces in `operatorApiClients.ts`, add:

```typescript
export interface OwnerPaymentGatewayDto {
  branchPaymentGatewayId: Guid;
  branchId: Guid | null;
  dcgateProjectId: string;
  cardLast4: string;
  status: string;            // pending_telegram | active | disabled
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface OwnerPaymentGatewayListResponse {
  gateways: OwnerPaymentGatewayDto[];
}

export interface ProvisionPaymentGatewayRequest extends Record<string, unknown> {
  branchId?: Guid | null;
  cardNumber: string;
}

export interface TelegramStartRequest extends Record<string, unknown> { phone: string; }
export interface TelegramStartResponse { loginAttemptId: string; state: string; }
export interface TelegramVerifyCodeRequest extends Record<string, unknown> { loginAttemptId: string; code: string; }
export interface TelegramVerifyPasswordRequest extends Record<string, unknown> { loginAttemptId: string; password: string; }
export interface TelegramVerifyResponse { state: string; gatewayStatus: string; }

export interface OwnerGatewayStatusResponse {
  gatewayStatus: string;
  sessionHealth: string;     // online | offline | configured
  lastConnectedAt: string | null;
  lastMessageAt: string | null;
  telegramMessagesCount: number;
}

export function createPaymentGatewayClient(api: PlatformApiClient) {
  return {
    list(): Promise<OwnerPaymentGatewayListResponse> {
      return api.get<OwnerPaymentGatewayListResponse>('/api/owner/payment-gateways');
    },
    provision(request: ProvisionPaymentGatewayRequest): Promise<OwnerPaymentGatewayDto> {
      return api.post<OwnerPaymentGatewayDto, ProvisionPaymentGatewayRequest>(
        '/api/owner/payment-gateways', request);
    },
    telegramStart(id: Guid, request: TelegramStartRequest): Promise<TelegramStartResponse> {
      return api.post<TelegramStartResponse, TelegramStartRequest>(
        `/api/owner/payment-gateways/${id}/telegram/start`, request);
    },
    telegramVerifyCode(id: Guid, request: TelegramVerifyCodeRequest): Promise<TelegramVerifyResponse> {
      return api.post<TelegramVerifyResponse, TelegramVerifyCodeRequest>(
        `/api/owner/payment-gateways/${id}/telegram/verify-code`, request);
    },
    telegramVerifyPassword(id: Guid, request: TelegramVerifyPasswordRequest): Promise<TelegramVerifyResponse> {
      return api.post<TelegramVerifyResponse, TelegramVerifyPasswordRequest>(
        `/api/owner/payment-gateways/${id}/telegram/verify-password`, request);
    },
    status(id: Guid): Promise<OwnerGatewayStatusResponse> {
      return api.get<OwnerGatewayStatusResponse>(`/api/owner/payment-gateways/${id}/status`);
    }
  };
}
```

- [ ] **Step 2: Register it in `createOperatorApiClients`**

In the returned object (line ~463–480), add:

```typescript
    moneyActions: createMoneyActionClient(api),
    paymentGateways: createPaymentGatewayClient(api)
```

(Add a comma after `createMoneyActionClient(api)`.)

- [ ] **Step 3: Type-check**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run build`
Expected: build succeeds (no `tsc` errors).

- [ ] **Step 4: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/operatorApiClients.ts
git commit -m "feat(web): add owner payment-gateway API client"
```

---

### Task 12: i18n strings

**Files:**
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json`
- Regenerate: `packages/i18n/src/messages.ts` (via `bun run gen`)

> Tajik (`tg`) falls back to `ru`, so it's acceptable to copy the Russian text for `tg` for now (real Tajik is in the deferred backlog). Keep keys identical across all three files.

- [ ] **Step 1: Add keys to `locales/ru.json`**

Add (matching the file's existing flat `"key": "value"` shape):

```json
"payments_cards.nav": "Приём платежей",
"payments_cards.title": "Приём платежей",
"payments_cards.subtitle": "Подключите карту для приёма онлайн-оплаты от игроков",
"payments_cards.empty": "Пока нет подключённых карт",
"payments_cards.add": "Добавить карту",
"payments_cards.scope.org": "Вся сеть",
"payments_cards.scope.branch": "Клуб",
"payments_cards.card_number": "Номер карты",
"payments_cards.provision": "Создать",
"payments_cards.status.pending_telegram": "Нужно подключить Telegram",
"payments_cards.status.active": "Активна",
"payments_cards.status.disabled": "Отключена",
"payments_cards.telegram.title": "Подключение Telegram",
"payments_cards.telegram.phone": "Телефон банковского аккаунта",
"payments_cards.telegram.start": "Получить код",
"payments_cards.telegram.code": "Код из Telegram",
"payments_cards.telegram.code_submit": "Подтвердить код",
"payments_cards.telegram.password": "Пароль 2FA",
"payments_cards.telegram.password_submit": "Подтвердить пароль",
"payments_cards.telegram.attached": "Telegram подключён, карта активна",
"payments_cards.session.online": "На связи",
"payments_cards.session.offline": "Нет связи",
"payments_cards.session.configured": "Настроена, ожидает запуска",
"payments_cards.error.generic": "Не удалось выполнить операцию"
```

- [ ] **Step 2: Add the same keys to `locales/en.json`** (English values)

```json
"payments_cards.nav": "Payments",
"payments_cards.title": "Payment cards",
"payments_cards.subtitle": "Connect a card to accept online top-ups from players",
"payments_cards.empty": "No payment cards connected yet",
"payments_cards.add": "Add card",
"payments_cards.scope.org": "Whole network",
"payments_cards.scope.branch": "Branch",
"payments_cards.card_number": "Card number",
"payments_cards.provision": "Create",
"payments_cards.status.pending_telegram": "Needs Telegram attach",
"payments_cards.status.active": "Active",
"payments_cards.status.disabled": "Disabled",
"payments_cards.telegram.title": "Connect Telegram",
"payments_cards.telegram.phone": "Phone of the bank account",
"payments_cards.telegram.start": "Send code",
"payments_cards.telegram.code": "Telegram code",
"payments_cards.telegram.code_submit": "Verify code",
"payments_cards.telegram.password": "2FA password",
"payments_cards.telegram.password_submit": "Verify password",
"payments_cards.telegram.attached": "Telegram attached, card active",
"payments_cards.session.online": "Online",
"payments_cards.session.offline": "Offline",
"payments_cards.session.configured": "Configured, awaiting start",
"payments_cards.error.generic": "The operation failed"
```

- [ ] **Step 3: Add the same keys to `locales/tg.json`** (copy Russian values for now)

Use the same key set as Step 1 with the Russian strings (tg falls back to ru anyway; this keeps the key list complete so codegen is symmetric).

- [ ] **Step 4: Regenerate messages and type-check**

Run: `cd packages/i18n && /home/fedya/.bun/bin/bun run gen`
Then: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run build`
Expected: `messages.ts` updated with the new keys; build succeeds.

- [ ] **Step 5: Commit**

```bash
git add locales/ru.json locales/en.json locales/tg.json packages/i18n/src/messages.ts
git commit -m "feat(i18n): add payment-cards cabinet strings"
```

---

### Task 13: `PaymentGatewaysWorkspace` component

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/PaymentGatewaysWorkspace.tsx`

The component mirrors `BackendPaymentsWorkspace`'s shape: load list in `useEffect`, local `useState`, `feedback` tri-state, actions call `createAuthenticatedOperatorClients(...).paymentGateways.*`, errors via `projectOperatorError`. It receives `backend` (config + session + branchId) as a prop, same as the other workspaces.

- [ ] **Step 1: Write the component**

```tsx
import { useCallback, useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import {
  createAuthenticatedOperatorClients,
  type OperatorBackend
} from './operatorBackend'; // <-- match the actual import the other workspaces use for backend + client factory
import type { OwnerPaymentGatewayDto } from './operatorApiClients';
import { projectOperatorError } from './operatorErrors'; // <-- match actual path used by BackendPaymentsWorkspace

type AttachPhase = 'idle' | 'code_required' | 'password_required' | 'attached';

interface Props {
  backend: OperatorBackend; // the same backend object the other workspaces receive
}

export function PaymentGatewaysWorkspace({ backend }: Props) {
  const { t } = useI18n();
  const [gateways, setGateways] = useState<OwnerPaymentGatewayDto[]>([]);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  // provision form
  const [cardNumber, setCardNumber] = useState('');
  const [scopeBranch, setScopeBranch] = useState(false);

  // telegram attach state
  const [attachId, setAttachId] = useState<string | null>(null);
  const [attachPhase, setAttachPhase] = useState<AttachPhase>('idle');
  const [loginAttemptId, setLoginAttemptId] = useState('');
  const [phone, setPhone] = useState('');
  const [code, setCode] = useState('');
  const [password, setPassword] = useState('');

  const clients = createAuthenticatedOperatorClients(backend.config, backend.session).paymentGateways;

  const reload = useCallback(async () => {
    try {
      const result = await clients.list();
      setGateways(result.gateways);
      setLoadError(null);
    } catch (error) {
      setLoadError(projectOperatorError(error).detail);
    }
  }, [clients]);

  useEffect(() => {
    let disposed = false;
    void (async () => {
      try {
        const result = await clients.list();
        if (!disposed) { setGateways(result.gateways); setLoadError(null); }
      } catch (error) {
        if (!disposed) setLoadError(projectOperatorError(error).detail);
      }
    })();
    return () => { disposed = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const provision = async () => {
    setBusy(true);
    try {
      await clients.provision({
        branchId: scopeBranch ? backend.branchId : null,
        cardNumber: cardNumber.trim()
      });
      setCardNumber('');
      await reload();
    } catch (error) {
      setLoadError(projectOperatorError(error).detail);
    } finally {
      setBusy(false);
    }
  };

  const startAttach = async (id: string) => {
    setBusy(true);
    try {
      const result = await clients.telegramStart(id, { phone: phone.trim() });
      setAttachId(id);
      setLoginAttemptId(result.loginAttemptId);
      setAttachPhase('code_required');
    } catch (error) {
      setLoadError(projectOperatorError(error).detail);
    } finally {
      setBusy(false);
    }
  };

  const verifyCode = async () => {
    if (!attachId) return;
    setBusy(true);
    try {
      const result = await clients.telegramVerifyCode(attachId, { loginAttemptId, code: code.trim() });
      if (result.state === 'password_required') {
        setAttachPhase('password_required');
      } else if (result.state === 'attached') {
        setAttachPhase('attached');
        await reload();
      }
    } catch (error) {
      setLoadError(projectOperatorError(error).detail);
    } finally {
      setBusy(false);
    }
  };

  const verifyPassword = async () => {
    if (!attachId) return;
    setBusy(true);
    try {
      const result = await clients.telegramVerifyPassword(attachId, { loginAttemptId, password });
      if (result.state === 'attached') {
        setAttachPhase('attached');
        await reload();
      }
    } catch (error) {
      setLoadError(projectOperatorError(error).detail);
    } finally {
      setBusy(false);
    }
  };

  return (
    <main className="workspace-screen payment-cards-screen">
      <section className="screen-head">
        <h1>{t('payments_cards.title')}</h1>
        <p>{t('payments_cards.subtitle')}</p>
      </section>

      {loadError && <p className="payment-cards-error" role="alert">{loadError}</p>}

      <section className="payment-cards-provision">
        <label>{t('payments_cards.card_number')}
          <input value={cardNumber} onChange={(e) => setCardNumber(e.currentTarget.value)} inputMode="numeric" />
        </label>
        <label>
          <input type="checkbox" checked={scopeBranch} onChange={(e) => setScopeBranch(e.currentTarget.checked)} />
          {scopeBranch ? t('payments_cards.scope.branch') : t('payments_cards.scope.org')}
        </label>
        <button type="button" disabled={busy || cardNumber.trim().length < 12} onClick={() => void provision()}>
          {t('payments_cards.provision')}
        </button>
      </section>

      <section className="payment-cards-list">
        {gateways.length === 0 && <p className="payment-cards-empty">{t('payments_cards.empty')}</p>}
        {gateways.map((g) => (
          <article key={g.branchPaymentGatewayId} className="payment-card-row" data-status={g.status}>
            <span className="payment-card-pan">•••• {g.cardLast4}</span>
            <span className="payment-card-scope">
              {g.branchId ? t('payments_cards.scope.branch') : t('payments_cards.scope.org')}
            </span>
            <span className="payment-card-status">{t(`payments_cards.status.${g.status}` as never)}</span>

            {g.status === 'pending_telegram' && (
              <div className="payment-card-attach">
                <h3>{t('payments_cards.telegram.title')}</h3>
                {(attachId !== g.branchPaymentGatewayId || attachPhase === 'idle') && (
                  <>
                    <label>{t('payments_cards.telegram.phone')}
                      <input value={phone} onChange={(e) => setPhone(e.currentTarget.value)} />
                    </label>
                    <button type="button" disabled={busy} onClick={() => void startAttach(g.branchPaymentGatewayId)}>
                      {t('payments_cards.telegram.start')}
                    </button>
                  </>
                )}
                {attachId === g.branchPaymentGatewayId && attachPhase === 'code_required' && (
                  <>
                    <label>{t('payments_cards.telegram.code')}
                      <input value={code} onChange={(e) => setCode(e.currentTarget.value)} inputMode="numeric" />
                    </label>
                    <button type="button" disabled={busy} onClick={() => void verifyCode()}>
                      {t('payments_cards.telegram.code_submit')}
                    </button>
                  </>
                )}
                {attachId === g.branchPaymentGatewayId && attachPhase === 'password_required' && (
                  <>
                    <label>{t('payments_cards.telegram.password')}
                      <input type="password" value={password} onChange={(e) => setPassword(e.currentTarget.value)} />
                    </label>
                    <button type="button" disabled={busy} onClick={() => void verifyPassword()}>
                      {t('payments_cards.telegram.password_submit')}
                    </button>
                  </>
                )}
                {attachId === g.branchPaymentGatewayId && attachPhase === 'attached' && (
                  <p className="payment-card-attached">{t('payments_cards.telegram.attached')}</p>
                )}
              </div>
            )}
          </article>
        ))}
      </section>
    </main>
  );
}
```

> **Import-path caveat:** the exact import specifiers (`@afk4/i18n` vs a relative path, `OperatorBackend` type name, `createAuthenticatedOperatorClients` and `projectOperatorError` locations) must match what `App.tsx`'s `BackendPaymentsWorkspace` actually imports. Before writing, grep `App.tsx` for `createAuthenticatedOperatorClients`, `projectOperatorError`, and the backend prop type, and copy those specifiers verbatim. If those helpers are defined inline in `App.tsx` (not exported), either export them or pass the already-created `apiClients`/helpers down as props.

- [ ] **Step 2: Type-check**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run build`
Expected: build succeeds. Fix any import-path mismatches flagged by `tsc`.

- [ ] **Step 3: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/PaymentGatewaysWorkspace.tsx
git commit -m "feat(web): add PaymentGatewaysWorkspace cabinet component"
```

---

### Task 14: Wire the workspace into `App.tsx` + nav

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/App.tsx` (WorkspaceId type ~line 112, workspaceIds ~line 150, permissionNames ~line 213, workspacePermissionRules ~line 218, render block ~line 10422)
- Modify: `src/AFK4.Operator.App.Web/src/operatorData.ts` (navItems ~line 48)

- [ ] **Step 1: Add the workspace id**

Line ~112 — add `'payment_cards'` to the `WorkspaceId` union:

```typescript
type WorkspaceId = 'map' | 'dashboard' | 'booking' | 'pos' | 'players' | 'payments' | 'payment_cards' | 'logs' | 'settings' | 'review';
```

Line ~150 — add it to `workspaceIds` **in the same position** as the nav item you add in operatorData.ts (order must line up — the nav render zips `navItems[index]` with `workspaceIds[index]`):

```typescript
const workspaceIds: WorkspaceId[] = ['map', 'dashboard', 'booking', 'pos', 'players', 'payments', 'payment_cards', 'logs', 'settings', 'review'];
```

- [ ] **Step 2: Add the permission name + workspace rule**

In `permissionNames` (~line 213), add:

```typescript
  managePaymentGateways: 'payments.gateways.manage',
```

In `workspacePermissionRules` (~line 218), add:

```typescript
  payment_cards: [permissionNames.managePaymentGateways],
```

- [ ] **Step 3: Add the nav item in `operatorData.ts`**

In the `navItems` array (~line 48), insert an entry **at the same index** as in `workspaceIds` (between `payments` and `logs`). Use an existing lucide icon already imported there (e.g. `CreditCard`; if not imported, add it to the import). Label via i18n is applied at render in some apps and static here — match the existing items' shape:

```typescript
  { label: 'Приём платежей', icon: CreditCard },
```

> If `navItems` labels are plain strings (as the explore report shows), keep the Russian label here to match siblings; the i18n key `payments_cards.nav` is still available for in-component headings. If siblings use an i18n key, follow that instead.

- [ ] **Step 4: Add the render block**

Near the other `{workspace === '...' && <... />}` blocks (~line 10422), add:

```tsx
{workspace === 'payment_cards' && backend !== null && (
  <PaymentGatewaysWorkspace backend={backend} />
)}
```

And add the import at the top of `App.tsx`:

```typescript
import { PaymentGatewaysWorkspace } from './PaymentGatewaysWorkspace';
```

- [ ] **Step 5: Type-check + build**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run build`
Expected: build succeeds.

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/App.tsx src/AFK4.Operator.App.Web/src/operatorData.ts
git commit -m "feat(web): register payment-cards workspace in nav (owner-gated)"
```

---

### Task 15: Section styles

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/styles.css`

- [ ] **Step 1: Append styles following the existing BEM-ish dark palette**

```css
.payment-cards-screen { display: grid; gap: 16px; padding: 18px 22px; }
.payment-cards-screen .screen-head h1 { margin: 0 0 4px; }
.payment-cards-error { color: #ff8d8d; background: #2a1518; border-radius: 8px; padding: 8px 12px; }
.payment-cards-provision { display: flex; flex-wrap: wrap; gap: 12px; align-items: end;
  background: #121a24; border-radius: 12px; padding: 14px 16px; }
.payment-cards-provision label { display: grid; gap: 4px; font-size: 13px; }
.payment-cards-provision input[type="text"],
.payment-cards-provision input { background: #0b1118; color: #eef3fa; border: 1px solid #243140;
  border-radius: 8px; padding: 8px 10px; }
.payment-cards-list { display: grid; gap: 10px; }
.payment-card-row { display: grid; gap: 8px; background: #121a24; border-radius: 12px; padding: 14px 16px;
  grid-template-columns: auto auto 1fr; align-items: center; }
.payment-card-row[data-status="pending_telegram"] { border-left: 3px solid #e0a23a; }
.payment-card-row[data-status="active"] { border-left: 3px solid #43c08a; }
.payment-card-row[data-status="disabled"] { border-left: 3px solid #5a6b7b; opacity: 0.7; }
.payment-card-pan { font-variant-numeric: tabular-nums; font-weight: 600; }
.payment-card-attach { grid-column: 1 / -1; display: grid; gap: 8px; border-top: 1px solid #243140;
  padding-top: 10px; }
.payment-card-attached { color: #43c08a; }
```

- [ ] **Step 2: Build to verify nothing breaks**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run build`
Expected: build succeeds.

- [ ] **Step 3: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/styles.css
git commit -m "style(web): payment-cards cabinet section styles"
```

---

### Task 16: Component test

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/PaymentGatewaysWorkspace.test.tsx`

Follow `App.test.tsx` conventions: `@testing-library/react`, `bun:test`, mock the API client (or `fetch`). Prefer mocking `createAuthenticatedOperatorClients` to return a stub `paymentGateways` so the test is independent of HTTP.

- [ ] **Step 1: Write the test**

```tsx
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n'; // match actual i18n provider import

// Mock the backend client factory used by the component.
const listMock = mock(async () => ({ gateways: [
  { branchPaymentGatewayId: 'g1', branchId: null, dcgateProjectId: 'p1',
    cardLast4: '4242', status: 'pending_telegram',
    createdAtUtc: '2026-06-04T00:00:00Z', updatedAtUtc: '2026-06-04T00:00:00Z' }
] }));
const provisionMock = mock(async () => ({}));
const startMock = mock(async () => ({ loginAttemptId: 'att', state: 'code_required' }));

mock.module('./operatorBackend', () => ({  // match actual module that exports createAuthenticatedOperatorClients
  createAuthenticatedOperatorClients: () => ({
    paymentGateways: {
      list: listMock, provision: provisionMock, telegramStart: startMock,
      telegramVerifyCode: mock(async () => ({ state: 'attached', gatewayStatus: 'active' })),
      telegramVerifyPassword: mock(async () => ({ state: 'attached', gatewayStatus: 'active' })),
      status: mock(async () => ({ gatewayStatus: 'active', sessionHealth: 'online',
        lastConnectedAt: null, lastMessageAt: null, telegramMessagesCount: 0 }))
    }
  })
}));

const { PaymentGatewaysWorkspace } = await import('./PaymentGatewaysWorkspace');

const backend = {
  config: { platformBaseUrl: 'http://test' },
  session: { accessToken: 't', organizationId: 'o1', permissions: ['payments.gateways.manage'], branchIds: ['b1'] },
  branchId: 'b1'
} as never;

describe('PaymentGatewaysWorkspace', () => {
  afterEach(() => { cleanup(); mock.restore(); });

  it('lists existing gateways with a pending-telegram badge', async () => {
    render(<I18nProvider><PaymentGatewaysWorkspace backend={backend} /></I18nProvider>);
    expect(await screen.findByText(/4242/)).toBeInTheDocument();
    expect(listMock).toHaveBeenCalled();
  });

  it('starts telegram attach for a pending gateway', async () => {
    render(<I18nProvider><PaymentGatewaysWorkspace backend={backend} /></I18nProvider>);
    await screen.findByText(/4242/);
    const phone = screen.getByLabelText(/телефон|phone/i);
    fireEvent.change(phone, { target: { value: '+992900000000' } });
    fireEvent.click(screen.getByRole('button', { name: /код|code/i }));
    await waitFor(() => expect(startMock).toHaveBeenCalled());
  });
});
```

> Adjust the mocked module specifier and `I18nProvider` import to the real ones (see Task 13 caveat). If `createAuthenticatedOperatorClients` lives inside `App.tsx` and isn't separately importable, refactor it into a small importable module first (and update Task 13's import), or inject the client as a prop and drop the module mock.

- [ ] **Step 2: Run the test**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test PaymentGatewaysWorkspace`
Expected: PASS (2 tests).

- [ ] **Step 3: Run the full web suite + build**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test && /home/fedya/.bun/bin/bun run build`
Expected: all tests pass, build succeeds.

- [ ] **Step 4: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/PaymentGatewaysWorkspace.test.tsx
git commit -m "test(web): PaymentGatewaysWorkspace list + attach"
```

---

## PART 3 — DOCS & WRAP-UP

### Task 17: Document the new env vars

**Files:**
- Modify: `deploy/coolify/staging.env.template` (PR #51 documented A's vars here — follow that format)

- [ ] **Step 1: Add the new vars with comments**

Append to the dcgate section:

```bash
# dcgate admin secret (= dcgate ADMIN_JWT_SECRET) — lets the owner cabinet provision projects + attach Telegram.
DcGate__AdminSecret=
# Full public webhook URL stamped into newly provisioned dcgate projects.
DcGate__WebhookUrl=https://afk4.staging.mubi.dev/api/public/payments/dcgate/webhook
# Payment link expiry (minutes) for provisioned projects.
DcGate__PaymentExpiresInMinutes=30
```

- [ ] **Step 2: Commit**

```bash
git add deploy/coolify/staging.env.template
git commit -m "docs(deploy): document dcgate admin/webhook env vars for owner cabinet"
```

---

### Task 18: Full verification pass

- [ ] **Step 1: Backend full suite**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj`
Expected: PASS, no regressions.

- [ ] **Step 2: Web build + tests**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run build && /home/fedya/.bun/bin/bun test`
Expected: build succeeds, all tests pass.

- [ ] **Step 3: Review the diff against the spec**

Confirm each Subsystem-B spec bullet (list, provision, telegram start/verify-code/verify-password, status, owner-gating, encrypted creds, CardLast4 only, one-active-per-scope) maps to a shipped endpoint/test.

---

## Out of scope (deferred — matches the design doc)

- **End-to-end staging validation** with a real card + live phone (attach Telegram via OTP/2FA, restart dcgate worker so session→`online`, exercise player top-up). Already tracked as PENDING in the epic.
- **Prod afk4 env + DB migration** for the multi-tenant payments stack (staging only so far).
- **Strict "telegram online" gate in the player top-up hot path** — current gate stays on `Status=active`; live health shown only in the cabinet.
- **DB partial-unique index** for one-active-gateway-per-scope (enforced in app logic here).
- Multiple cards per single branch, payouts/settlement, refunds UI, non-TJS currencies, encryption-key rotation tooling.

---

## Self-Review notes

- **Spec coverage:** list ✓ (Task 7), provision/Phase 1 ✓ (Task 8), telegram start/verify-code/verify-password/Phase 2 ✓ (Task 9), status ✓ (Task 10), owner-gating ✓ (Tasks 5/7), encrypted creds + CardLast4-only ✓ (Task 8 test), one-active-per-scope invariant ✓ (Task 8), UI section + gating + state surfacing ✓ (Tasks 11–16), env docs ✓ (Task 17).
- **Idempotency contract:** externalId = PK, persist-on-first-response, replay-without-secret guarded (Task 8).
- **Type consistency:** `TelegramVerifyResponse { state, gatewayStatus }`, `OwnerGatewayStatusResponse { gatewayStatus, sessionHealth, ... }`, `DcGateAdminProjectResult { ApiKey?, WebhookSecret?, IdempotentReplay }` used consistently across backend tasks and TS DTOs.
- **Known soft spots flagged for the implementer:** exact frontend import specifiers (Task 13/16 caveats) and the existing owner-auth test helper name (Task 7) must be confirmed against the real code — the plan says where to look and what to do if they differ.
