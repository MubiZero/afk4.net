# afk4: per-owner Telegram credentials + attach UI — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let an owner supply `api_id`/`api_hash` once per Telegram phone (stored encrypted, reused across cards) and consume dcgate's `attached`-without-OTP short-circuit in the cabinet.

**Architecture:** New `(org, phone)`-keyed encrypted credential table in `AFK4.Platform.Api`. The `/telegram/start` owner endpoint resolves or stores creds, forwards them to dcgate, and flips the gateway active immediately when dcgate answers `attached`. The cabinet prefills saved creds by phone and skips the OTP step on `attached`.

**Tech Stack:** .NET 10 (`AFK4.Platform.Api`), EF Core (PostgreSQL, in-memory for tests), xunit. Web: React + TS in `src/AFK4.Operator.App.Web`, `bun test` (`/home/fedya/.bun/bin/bun`) + `bun run build`. i18n in `locales/{ru,en,tg}.json`.

**Spec:** `docs/superpowers/specs/2026-06-08-per-owner-telegram-credentials-session-sharing-design.md`
**Depends on:** the dcgate plan shipped (start accepts `apiId`/`apiHash`, may return `{state:"attached"}` with no `loginAttemptId`).

> All paths relative to `/home/fedya/projects/afk4.net`. Continue on branch `feature/per-owner-telegram-credentials` (the spec was committed there).

---

### Task 1: `OrganizationTelegramApiCredentialEntity` + EF config + migration

**Files:**
- Create: `src/AFK4.Platform.Api/Data/OrganizationTelegramApiCredentialEntity.cs`
- Modify: `src/AFK4.Platform.Api/Data/PlatformDbContext.cs` (DbSet + `OnModelCreating`)
- Create (generated): `src/AFK4.Platform.Api/Data/Migrations/<ts>_AddOrganizationTelegramApiCredentials.cs`

- [ ] **Step 1: Create the entity** (mirror `BranchPaymentGatewayEntity`)

```csharp
namespace AFK4.Platform.Api.Data;

// One row per (organization, Telegram phone). Holds the owner's Telegram application
// credentials (api_id / api_hash) encrypted via ISecretProtector, reused across every card
// whose bank notifications arrive in that Telegram account.
public sealed class OrganizationTelegramApiCredentialEntity
{
    public Guid OrganizationTelegramApiCredentialId { get; set; }

    public Guid OrganizationId { get; set; }

    public string PhoneNumber { get; set; } = string.Empty;

    public string ApiIdEncrypted { get; set; } = string.Empty;

    public string ApiHashEncrypted { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
```

- [ ] **Step 2: Register the DbSet** in `PlatformDbContext` (next to `BranchPaymentGateways`)

```csharp
public DbSet<OrganizationTelegramApiCredentialEntity> OrganizationTelegramApiCredentials =>
    Set<OrganizationTelegramApiCredentialEntity>();
```

- [ ] **Step 3: Configure the model** in `OnModelCreating` (next to the `BranchPaymentGatewayEntity` block)

```csharp
modelBuilder.Entity<OrganizationTelegramApiCredentialEntity>(entity =>
{
    entity.ToTable("organization_telegram_api_credentials");
    entity.HasKey(c => c.OrganizationTelegramApiCredentialId);
    entity.Property(c => c.PhoneNumber).HasMaxLength(32).IsRequired();
    entity.Property(c => c.ApiIdEncrypted).HasMaxLength(1024).IsRequired();
    entity.Property(c => c.ApiHashEncrypted).HasMaxLength(1024).IsRequired();
    entity.HasIndex(c => new { c.OrganizationId, c.PhoneNumber }).IsUnique();
});
```

- [ ] **Step 4: Generate the migration**

Run:
```bash
dotnet ef migrations add AddOrganizationTelegramApiCredentials \
  --project src/AFK4.Platform.Api --startup-project src/AFK4.Platform.Api \
  --output-dir Data/Migrations
```
Expected: a new migration `CREATE TABLE "organization_telegram_api_credentials"` with the unique index.

- [ ] **Step 5: Build**

Run: `dotnet build src/AFK4.Platform.Api/AFK4.Platform.Api.csproj`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Platform.Api/Data
git commit -m "feat: organization Telegram api credential table (org, phone)"
```

---

### Task 2: Shared contracts — optional creds in/out

**Files:**
- Modify: `src/AFK4.Shared.Contracts/Payments/` (the file holding `TelegramStartRequest`/`TelegramStartResponse` — `OwnerPaymentGatewayDtos.cs`)

- [ ] **Step 1: Edit the records**

```csharp
// Phase 2 — telegram attach. ApiId/ApiHash optional: supplied to set/replace the saved
// app credentials for this phone; omitted to reuse the saved pair.
public sealed record TelegramStartRequest(string Phone, long? ApiId = null, string? ApiHash = null);

// LoginAttemptId is null when dcgate short-circuited an already-attached phone (State="attached").
public sealed record TelegramStartResponse(string? LoginAttemptId, string State);

// Cabinet prefill: does this (org, phone) already have saved app credentials?
public sealed record OwnerTelegramCredentialsResponse(bool HasCredentials, long? ApiId);
```

- [ ] **Step 2: Build**

Run: `dotnet build src/AFK4.Shared.Contracts/AFK4.Shared.Contracts.csproj`
Expected: PASS (existing call sites use positional `Phone`; the new params are optional).

- [ ] **Step 3: Commit**

```bash
git add src/AFK4.Shared.Contracts/Payments
git commit -m "feat: optional api_id/api_hash on telegram start contract"
```

---

### Task 3: `DcGateAdminClient.StartTelegramAsync` forwards creds + parses `attached`

**Files:**
- Modify: `src/AFK4.Platform.Api/Payments/DcGate/IDcGateAdminClient.cs`
- Modify: `src/AFK4.Platform.Api/Payments/DcGate/DcGateAdminClient.cs`
- Test: `tests/AFK4.Platform.Api.Tests/DcGateAdminClientTests.cs` (create if absent; otherwise add to existing client test)

- [ ] **Step 1: Write a failing test** — start sends `apiId`/`apiHash` and parses a response without `loginAttemptId`.

```csharp
[Fact]
public async Task StartTelegram_sends_creds_and_parses_attached_without_attempt()
{
    var handler = new CapturingHandler("""{"state":"attached"}""");
    var client = new DcGateAdminClient(new HttpClient(handler) { BaseAddress = new Uri("https://dcgate.test") },
        Options.Create(new DcGateOptions { AdminSecret = "s" }));

    var result = await client.StartTelegramAsync("proj_1", "+992900000000", 123, "hash", CancellationToken.None);

    Assert.Null(result.LoginAttemptId);
    Assert.Equal("attached", result.State);
    Assert.Contains("\"apiId\":123", handler.LastBody);
    Assert.Contains("\"apiHash\":\"hash\"", handler.LastBody);
}
```

(Use the existing test's `CapturingHandler`/fake `HttpMessageHandler` pattern if one exists; otherwise add a minimal handler that records the request body and returns a canned JSON.)

- [ ] **Step 2: Run, expect FAIL** — `dotnet test tests/AFK4.Platform.Api.Tests --filter StartTelegram_sends_creds`.

- [ ] **Step 3: Update the interface**

```csharp
Task<DcGateTelegramStartResult> StartTelegramAsync(
    string dcgateProjectId,
    string phone,
    long apiId,
    string apiHash,
    CancellationToken cancellationToken);
```

And make `LoginAttemptId` nullable on the result record:
```csharp
public sealed record DcGateTelegramStartResult(
    string? LoginAttemptId,
    string State);
```

- [ ] **Step 4: Update `DcGateAdminClient.StartTelegramAsync`**

```csharp
public async Task<DcGateTelegramStartResult> StartTelegramAsync(
    string dcgateProjectId, string phone, long apiId, string apiHash, CancellationToken cancellationToken)
{
    using var http = BuildRequest(HttpMethod.Post,
        $"/api/admin/projects/{dcgateProjectId}/telegram-session/start",
        new { phone, apiId, apiHash });
    using var doc = await SendAsync(http, cancellationToken);
    var root = doc.RootElement;
    var loginAttemptId = root.TryGetProperty("loginAttemptId", out var la) ? la.GetString() : null;
    return new DcGateTelegramStartResult(
        loginAttemptId,
        root.GetProperty("state").GetString() ?? throw Empty("state"));
}
```

- [ ] **Step 5: Run, expect PASS.**

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Platform.Api/Payments/DcGate tests/AFK4.Platform.Api.Tests/DcGateAdminClientTests.cs
git commit -m "feat: dcgate admin client forwards api creds, parses attached"
```

---

### Task 4: `/telegram/start` resolves/stores creds and applies `attached`

**Files:**
- Modify: `src/AFK4.Platform.Api/Endpoints/PaymentGatewayEndpoints.cs` (the `/telegram/start` handler, ~line 400)
- Test: `tests/AFK4.Platform.Api.Tests/OwnerPaymentGatewayEndpointTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
[Fact]
public async Task TelegramStart_stores_supplied_creds_and_reuses_them()
{
    var fake = new FakeAdminClient(); // capture the apiId/apiHash it receives (extend FakeAdminClient)
    await using var factory = FactoryWithAdmin(fake);
    var client = factory.CreateClient();
    var (orgId, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);
    var id = await SeedGatewayAsync(factory, orgId, null, "proj_1", "pending_telegram");

    // first call provides creds -> stored + forwarded
    await owner.PostAsJsonAsync($"/api/owner/payment-gateways/{id}/telegram/start",
        new TelegramStartRequest("+992900000000", 123, "hash"));
    Assert.Equal(123, fake.LastApiId);
    Assert.Equal("hash", fake.LastApiHash);

    // second call omits creds -> reuses stored
    fake.LastApiId = 0;
    await owner.PostAsJsonAsync($"/api/owner/payment-gateways/{id}/telegram/start",
        new TelegramStartRequest("+992900000000"));
    Assert.Equal(123, fake.LastApiId);
    Assert.Equal("hash", fake.LastApiHash);
}

[Fact]
public async Task TelegramStart_without_creds_and_none_saved_returns_400()
{
    var fake = new FakeAdminClient();
    await using var factory = FactoryWithAdmin(fake);
    var client = factory.CreateClient();
    var (orgId, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);
    var id = await SeedGatewayAsync(factory, orgId, null, "proj_1", "pending_telegram");

    var response = await owner.PostAsJsonAsync($"/api/owner/payment-gateways/{id}/telegram/start",
        new TelegramStartRequest("+992900000000"));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
}

[Fact]
public async Task TelegramStart_attached_response_marks_gateway_active()
{
    var fake = new FakeAdminClient { StartResult = new DcGateTelegramStartResult(null, "attached") };
    await using var factory = FactoryWithAdmin(fake);
    var client = factory.CreateClient();
    var (orgId, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);
    var id = await SeedGatewayAsync(factory, orgId, null, "proj_1", "pending_telegram");

    var response = await owner.PostAsJsonAsync($"/api/owner/payment-gateways/{id}/telegram/start",
        new TelegramStartRequest("+992900000000", 123, "hash"));

    var body = await response.Content.ReadFromJsonAsync<TelegramStartResponse>();
    Assert.Equal("attached", body!.State);
    Assert.Null(body.LoginAttemptId);

    await using var scope = factory.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
    var row = await db.BranchPaymentGateways.SingleAsync(g => g.BranchPaymentGatewayId == id);
    Assert.Equal("active", row.Status);
}
```

(Extend `FakeAdminClient`: add `long LastApiId; string? LastApiHash; DcGateTelegramStartResult StartResult = new("att","code_required");` and have `StartTelegramAsync(p, phone, apiId, apiHash, ct)` record them and return `StartResult`. Update its signature to the new 5-arg interface.)

- [ ] **Step 2: Run, expect FAIL.**

- [ ] **Step 3: Implement the handler** — replace the body of `/telegram/start`. Inject `ISecretProtector secretProtector`.

```csharp
app.MapPost("/api/owner/payment-gateways/{id:guid}/telegram/start", async (
    Guid id, TelegramStartRequest request,
    StaffAuthorizationService authorizationService,
    IDcGateAdminClient adminClient,
    ISecretProtector secretProtector,
    PlatformDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var (row, error) = await ResolveOwnerGatewayAsync(id, authorizationService, dbContext, cancellationToken);
    if (error is not null) return error;
    var orgId = row!.OrganizationId;
    var phone = (request.Phone ?? string.Empty).Trim();

    // Resolve the (org, phone) credentials: store the supplied pair, or reuse the saved one.
    long apiId;
    string apiHash;
    var existing = await dbContext.OrganizationTelegramApiCredentials.SingleOrDefaultAsync(
        c => c.OrganizationId == orgId && c.PhoneNumber == phone, cancellationToken);

    if (request.ApiId is long suppliedId && !string.IsNullOrWhiteSpace(request.ApiHash))
    {
        if (suppliedId <= 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["apiId"] = ["api_id must be a positive integer."]
            });
        }
        apiId = suppliedId;
        apiHash = request.ApiHash.Trim();
        var now = DateTimeOffset.UtcNow;
        if (existing is null)
        {
            dbContext.OrganizationTelegramApiCredentials.Add(new OrganizationTelegramApiCredentialEntity
            {
                OrganizationTelegramApiCredentialId = Guid.NewGuid(),
                OrganizationId = orgId,
                PhoneNumber = phone,
                ApiIdEncrypted = secretProtector.Protect(apiId.ToString()),
                ApiHashEncrypted = secretProtector.Protect(apiHash),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }
        else
        {
            existing.ApiIdEncrypted = secretProtector.Protect(apiId.ToString());
            existing.ApiHashEncrypted = secretProtector.Protect(apiHash);
            existing.UpdatedAtUtc = now;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }
    else if (existing is not null)
    {
        apiId = long.Parse(secretProtector.Unprotect(existing.ApiIdEncrypted));
        apiHash = secretProtector.Unprotect(existing.ApiHashEncrypted);
    }
    else
    {
        return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
            title: "telegram_api_credentials_required",
            detail: "Enter api_id and api_hash for this Telegram account.");
    }

    try
    {
        var result = await adminClient.StartTelegramAsync(row.DcgateProjectId, phone, apiId, apiHash, cancellationToken);
        if (result.State == DcGateTelegramState.Attached)
        {
            await ApplyAttachResultAsync(row, result.State, dbContext, cancellationToken);
        }
        return Results.Ok(new TelegramStartResponse(result.LoginAttemptId, result.State));
    }
    catch (DcGateAdminException ex)
    {
        return Results.Problem(statusCode: (int)ex.StatusCode, title: "dcgate_error", detail: ex.Message);
    }
});
```

(`ApplyAttachResultAsync` and `DcGateTelegramState.Attached` already exist from Subsystem B. `ApplyAttachResultAsync` flips `pending_telegram → active` when state is `attached`.)

- [ ] **Step 4: Run, expect PASS.**

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Api/Endpoints/PaymentGatewayEndpoints.cs tests/AFK4.Platform.Api.Tests/OwnerPaymentGatewayEndpointTests.cs
git commit -m "feat: telegram start resolves/stores creds, applies attached"
```

---

### Task 5: `GET /telegram-credentials?phone=` lookup

**Files:**
- Modify: `src/AFK4.Platform.Api/Endpoints/PaymentGatewayEndpoints.cs`
- Test: `tests/AFK4.Platform.Api.Tests/OwnerPaymentGatewayEndpointTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
[Fact]
public async Task TelegramCredentials_reports_saved_state_without_hash()
{
    var fake = new FakeAdminClient();
    await using var factory = FactoryWithAdmin(fake);
    var client = factory.CreateClient();
    var (orgId, owner) = await OwnerTestAuth.SignInOwnerAsync(factory, client);
    var id = await SeedGatewayAsync(factory, orgId, null, "proj_1", "pending_telegram");
    await owner.PostAsJsonAsync($"/api/owner/payment-gateways/{id}/telegram/start",
        new TelegramStartRequest("+992900000000", 123, "hash"));

    var none = await owner.GetFromJsonAsync<OwnerTelegramCredentialsResponse>(
        "/api/owner/payment-gateways/telegram-credentials?phone=%2B992999999999");
    Assert.False(none!.HasCredentials);

    var saved = await owner.GetFromJsonAsync<OwnerTelegramCredentialsResponse>(
        "/api/owner/payment-gateways/telegram-credentials?phone=%2B992900000000");
    Assert.True(saved!.HasCredentials);
    Assert.Equal(123, saved.ApiId);
}
```

- [ ] **Step 2: Run, expect FAIL.**

- [ ] **Step 3: Implement** — add the endpoint (owner-gated, like the list endpoint)

```csharp
app.MapGet("/api/owner/payment-gateways/telegram-credentials", async (
    string phone,
    StaffAuthorizationService authorizationService,
    ISecretProtector secretProtector,
    PlatformDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequireOrganizationPermission(
        StaffPermissionNames.ManagePaymentGateways);
    if (!authorization.IsAuthenticated) return Results.Unauthorized();
    if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

    var orgId = authorization.StaffContext!.OrganizationId;
    var normalized = (phone ?? string.Empty).Trim();
    var existing = await dbContext.OrganizationTelegramApiCredentials.AsNoTracking().SingleOrDefaultAsync(
        c => c.OrganizationId == orgId && c.PhoneNumber == normalized, cancellationToken);

    if (existing is null)
    {
        return Results.Ok(new OwnerTelegramCredentialsResponse(false, null));
    }
    var apiId = long.Parse(secretProtector.Unprotect(existing.ApiIdEncrypted));
    return Results.Ok(new OwnerTelegramCredentialsResponse(true, apiId));
});
```

> Route order: register this **before** the `{id:guid}` routes so `telegram-credentials` is not captured as an `id` — actually it is a distinct literal segment under a different path, but place it next to the list endpoint to be safe.

- [ ] **Step 4: Run, expect PASS.** Then run the whole owner-gateway test class: `dotnet test tests/AFK4.Platform.Api.Tests --filter OwnerPaymentGateway`.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Api/Endpoints/PaymentGatewayEndpoints.cs tests/AFK4.Platform.Api.Tests/OwnerPaymentGatewayEndpointTests.cs
git commit -m "feat: owner telegram-credentials lookup endpoint"
```

---

### Task 6: Web — api client types + calls

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/operatorApiClients.ts`

- [ ] **Step 1: Extend the request/response types**

```typescript
export interface TelegramStartRequest extends Record<string, unknown> {
  phone: string;
  apiId?: number;
  apiHash?: string;
}
export interface TelegramStartResponse {
  loginAttemptId: string | null;
  state: string;
}
export interface OwnerTelegramCredentialsResponse {
  hasCredentials: boolean;
  apiId: number | null;
}
```

- [ ] **Step 2: Add the lookup call** to `createPaymentGatewayClient`

```typescript
telegramCredentials(phone: string): Promise<OwnerTelegramCredentialsResponse> {
  return api.get<OwnerTelegramCredentialsResponse>(
    `/api/owner/payment-gateways/telegram-credentials?phone=${encodeURIComponent(phone)}`);
},
```

(`telegramStart` already passes the request through; no change needed beyond the type.)

- [ ] **Step 3: Type-check**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run build`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/operatorApiClients.ts
git commit -m "feat(web): telegram credentials lookup + creds on start request"
```

---

### Task 7: Web — attach UI (creds prefill + skip OTP on attached)

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/PaymentGatewaysWorkspace.tsx`
- Test: `src/AFK4.Operator.App.Web/src/PaymentGatewaysWorkspace.test.tsx`

- [ ] **Step 1: Write a failing test** — when the phone has no saved creds, the api_id/api_hash inputs are required and sent; on `attached` the OTP step is skipped.

```typescript
it('sends api creds on first attach and skips OTP when attached', async () => {
  credentialsMock.mockResolvedValueOnce({ hasCredentials: false, apiId: null });
  startMock.mockResolvedValueOnce({ loginAttemptId: null, state: 'attached' });
  render(<I18nProvider><PaymentGatewaysWorkspace backend={backend} /></I18nProvider>);
  await screen.findByText(/4242/);
  fireEvent.change(screen.getByLabelText(/телефон|phone/i), { target: { value: '+992900000000' } });
  fireEvent.change(screen.getByLabelText(/api_id/i), { target: { value: '123' } });
  fireEvent.change(screen.getByLabelText(/api_hash/i), { target: { value: 'hash' } });
  fireEvent.click(screen.getByRole('button', { name: /код|code/i }));
  await waitFor(() => expect(startMock).toHaveBeenCalledWith('g1', { phone: '+992900000000', apiId: 123, apiHash: 'hash' }));
  await screen.findByText(/активна|active|подключён/i); // attached, no code input shown
  expect(screen.queryByLabelText(/код из telegram|code from telegram/i)).toBeNull();
});
```

(Add `const credentialsMock = mock(async () => ({ hasCredentials: false, apiId: null }));` to the mocked `paymentGateways` object: `telegramCredentials: credentialsMock`.)

- [ ] **Step 2: Run, expect FAIL** — `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/PaymentGatewaysWorkspace.test.tsx`.

- [ ] **Step 3: Implement** — add creds state + a phone-driven lookup, conditional inputs, and an `attached` short-circuit.

Add state (near the other attach state):
```typescript
const [apiId, setApiId] = useState('');
const [apiHash, setApiHash] = useState('');
const [savedApiId, setSavedApiId] = useState<number | null>(null);
const [hasSavedCreds, setHasSavedCreds] = useState(false);
const [changeCreds, setChangeCreds] = useState(false);
```

Look up saved creds when the phone loses focus:
```typescript
const lookupCreds = async () => {
  const trimmed = phone.trim();
  if (!trimmed) return;
  try {
    const res = await clients.telegramCredentials(trimmed);
    setHasSavedCreds(res.hasCredentials);
    setSavedApiId(res.apiId);
    setChangeCreds(false);
  } catch { /* lookup is best-effort; fall back to manual entry */ }
};
```

Update `startAttach` to include creds and handle `attached`:
```typescript
const startAttach = async (id: string) => {
  setBusy(true);
  try {
    const sendCreds = !hasSavedCreds || changeCreds;
    const request: TelegramStartRequest = sendCreds
      ? { phone: phone.trim(), apiId: Number(apiId), apiHash: apiHash.trim() }
      : { phone: phone.trim() };
    const result = await clients.telegramStart(id, request);
    setAttachId(id);
    if (result.state === 'attached') {
      setAttachPhase('attached');
      await reload(); // refresh the gateway list so the card shows active
    } else {
      setLoginAttemptId(result.loginAttemptId ?? '');
      setAttachPhase(result.state as AttachPhase);
    }
  } catch (error) {
    setLoadError(projectOperatorError(error).detail);
  } finally {
    setBusy(false);
  }
};
```

In the attach JSX, after the phone input, add (when `attachPhase === 'idle'`):
```tsx
<input
  aria-label="phone"
  value={phone}
  onChange={(e) => setPhone(e.currentTarget.value)}
  onBlur={() => void lookupCreds()}
/>
{hasSavedCreds && !changeCreds ? (
  <p className="payment-card-saved-creds">
    {t('payments_cards.telegram.saved_creds', { apiId: savedApiId })}
    <button type="button" onClick={() => setChangeCreds(true)}>
      {t('payments_cards.telegram.change_creds')}
    </button>
  </p>
) : (
  <>
    <label>{t('payments_cards.telegram.api_id')}
      <input aria-label="api_id" inputMode="numeric" value={apiId}
        onChange={(e) => setApiId(e.currentTarget.value)} />
    </label>
    <label>{t('payments_cards.telegram.api_hash')}
      <input aria-label="api_hash" type="password" value={apiHash}
        onChange={(e) => setApiHash(e.currentTarget.value)} />
    </label>
    <p className="payment-card-api-help">{t('payments_cards.telegram.api_help')}</p>
  </>
)}
```

(`reload` is the existing function that refetches the gateway list — confirm its name in the file; the workspace already reloads after provision/disable. Reuse that.)

- [ ] **Step 4: Run, expect PASS.** Then the whole file: `bun test src/PaymentGatewaysWorkspace.test.tsx`.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/PaymentGatewaysWorkspace.tsx src/AFK4.Operator.App.Web/src/PaymentGatewaysWorkspace.test.tsx
git commit -m "feat(web): collect/reuse api creds, skip OTP on attached"
```

---

### Task 8: Web — i18n keys (ru / en / tg)

**Files:**
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json`

- [ ] **Step 1: Add keys** (ru.json; tg.json = ru copy per project convention; en.json translated)

ru.json (and tg.json identical copy):
```json
"payments_cards.telegram.api_id": "api_id приложения Telegram",
"payments_cards.telegram.api_hash": "api_hash приложения Telegram",
"payments_cards.telegram.api_help": "api_id и api_hash получите на my.telegram.org (раздел «API development tools») под аккаунтом, который получает банковские уведомления.",
"payments_cards.telegram.saved_creds": "Используются сохранённые ключи приложения (api_id {{apiId}})",
"payments_cards.telegram.change_creds": "Изменить ключи",
"payments_cards.error.api_credentials_required": "Введите api_id и api_hash приложения Telegram",
```

en.json:
```json
"payments_cards.telegram.api_id": "Telegram app api_id",
"payments_cards.telegram.api_hash": "Telegram app api_hash",
"payments_cards.telegram.api_help": "Get api_id and api_hash at my.telegram.org (API development tools) under the account that receives the bank notifications.",
"payments_cards.telegram.saved_creds": "Using saved app credentials (api_id {{apiId}})",
"payments_cards.telegram.change_creds": "Change credentials",
"payments_cards.error.api_credentials_required": "Enter the Telegram app api_id and api_hash",
```

- [ ] **Step 2: Verify locale integrity** (whatever the repo uses — e.g. a locale-keys test). Run the web test suite to catch missing-key assertions: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test`.
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add locales/ru.json locales/en.json locales/tg.json
git commit -m "feat(web): i18n for telegram app credentials"
```

---

### Task 9: Full verification

- [ ] **Step 1: Backend tests** — `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj` → all green.
- [ ] **Step 2: Web tests** — `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test` → all green.
- [ ] **Step 3: Web build (type-check)** — `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run build` → PASS.
- [ ] **Step 4: Backend build** — `dotnet build src/AFK4.Platform.Api/AFK4.Platform.Api.csproj` → PASS.
- [ ] **Step 5: Push**

```bash
git push -u origin feature/per-owner-telegram-credentials
```

> Deploy note: afk4 does NOT auto-migrate. Apply `AddOrganizationTelegramApiCredentials` via the staging/prod EF runbook (expose DB → `dotnet ef migrations script --idempotent` → apply → close), then deploy the API; the deploy workflow gates on `confirm_migrations_applied=true` because this touches `Data/Migrations/**`.

---

## Self-Review notes

- **Spec coverage:** credential table (T1), optional creds contract (T2), admin client forwarding + attached parse (T3), start resolve/store/attached (T4), credentials lookup (T5), web client (T6), attach UI prefill + skip-OTP (T7), i18n (T8). All Subsystem B + UI spec sections covered.
- **Type consistency:** `TelegramStartRequest(Phone, ApiId?, ApiHash?)`, `TelegramStartResponse(LoginAttemptId?, State)`, `DcGateTelegramStartResult(LoginAttemptId?, State)`, `OwnerTelegramCredentialsResponse(HasCredentials, ApiId?)`, `StartTelegramAsync(projectId, phone, apiId, apiHash, ct)` — consistent across T2/T3/T4/T5/T6.
- **Security:** `api_hash` encrypted via `ISecretProtector`, never returned (lookup returns only `apiId`); inputs are `type="password"`.
- **Verify before done:** the in-memory test DB exercises encrypt/Protect round-trips; run both test suites + builds (T9) before claiming complete (per WORKING-STYLE).
