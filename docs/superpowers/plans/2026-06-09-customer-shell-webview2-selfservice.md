# Customer Shell WebView2 — Self-Service (Units D + E + G) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the already-built WebView2 shell foundation into a usable self-service kiosk: account login (token stays native), self extend-time, dcgate QR top-up with the payment-status machine, and packaging that retires the old WPF UI.

**Architecture:** The React app (`AFK4.Player.Shell.Web`) renders and sends requests but never holds secrets. Login credentials go *in* through the narrow native bridge; the native host (`AFK4.Player.Shell`) authenticates against `Platform.Api`, keeps the opaque bearer tokens in memory, and injects `Authorization` on every `/api/me/*` request via `WebResourceRequested`. Self-service REST (tariffs, extend, top-up) is server-authoritative; the shell only reflects state. Packaging bundles the web build into the existing WiX installer and removes the legacy `MainWindow`/`PlayerShellViewModel`.

**Tech Stack:** .NET 10 (`net10.0-windows`, WPF), `Microsoft.Web.WebView2` 1.0.3967.48, `System.Net.Http`, xUnit; Vite + React 19 + TypeScript, `bun test` + happy-dom + Testing Library, `@afk4/i18n` / `@afk4/money` / `@afk4/formatting`, `qrcode`.

---

## Scope, assumptions & decisions

These were resolved against the real codebase (the spec's MVP list did not match what the server actually exposes). Read before starting — they are binding for this plan.

1. **Login = account only (phone + password).** The only player-auth path that exists is `POST /api/public/player/sign-in` (`PlayerSignInRequest { OrganizationId, PhoneNumber, Password }`). There is **no** public guest / session-code entry — guest sessions are staff-initiated. Guest/session-code login is **deferred to Phase 2**.
2. **`OrganizationId` for sign-in comes from pipe state**, not from the user. The shell already receives `PlayerShellStateDto.OrganizationId` over the named pipe; the bridge composes the full `PlayerSignInRequest` natively. The login form only collects phone + password.
3. **The token never enters JavaScript.** Login flows *in* over the bridge (`auth:signIn`); the native `PlayerApiAuthClient` holds access + refresh tokens in memory and injects `Authorization: Bearer …` on the API origin via `WebResourceRequested`. The web only ever learns a non-secret snapshot (`authenticated`, `displayName`, `phoneVerified`).
4. **Login gates self-service, not the lock.** Lock/active routing stays exactly as the foundation built it (driven by pipe state). API auth is an independent layer: a player at an active seat can still be "not signed in for self-service". Self-service screens (extend, top-up) require `auth:signIn` first.
5. **Player tariff/package listing is a NEW small endpoint.** `…/tariffs/options` and `…/packages/options` are staff-only (`ViewTariffs`/`ViewPackages`). Unit E adds player-scoped read endpoints reusing `IOperatorReferenceDataService`.
6. **MVP extend = wallet-funded minutes at a chosen tariff.** `POST /api/me/sessions/{id}/extend` (`ExtendSessionRequest`) is used with `TariffRuleVersionId` + `AdditionalMinutes`. Self-service **package purchase + apply** (`PlayerPackageId`) needs a player-facing purchase route that does not exist (the existing purchase endpoint is staff-only and needs a `staffUserId`); it is **deferred**. Packages are still *listed* for display.
7. **409 (version conflict) is handled by refetch.** MVP omits `ExpectedVersion` on extend and, on a `409`, shows "state changed, refreshing" and re-reads authoritative state (the realtime-epic pattern).
8. **Offline cache is abstracted.** The cache-fallback *logic* is unit-tested against an in-memory fake; the real IndexedDB binding is thin glue verified on the Windows bridge / device.
9. **Unit F (shop / loyalty / news) is NOT in this plan.** Those three backends do not exist server-side and loyalty needs a business-rules design pass. They get their own brainstorm → spec → plan immediately after this one.
10. **TDD discipline:** web units and .NET policies are test-first (`bun test`, xUnit). WebView2-touching glue (`WebResourceRequested`, virtual host, real IndexedDB) is kept thin and verified on the Windows bridge per the env-quirks runbook (build the web `dist` in WSL, `cp -r` into the `D:\projects\afk4.net` clone, run `dotnet.exe test`).

---

## File Structure

### Native host (`src/AFK4.Player.Shell/`)
- **Modify** `Configuration/PlayerShellOptions.cs` — add `ApiBaseUrl`.
- **Create** `Web/AuthorizationHeaderPolicy.cs` — pure helper: should-inject decision + header value for a request URI vs API base.
- **Create** `Identity/PlayerApiAuthClient.cs` + `Identity/IPlayerApiAuthClient.cs` — sign-in/refresh/sign-out against `Platform.Api`, holds tokens, exposes `CurrentAccessToken` + `AuthSnapshot`.
- **Modify** `Web/PlayerShellWebHostBridge.cs` — add `auth:signIn` / `auth:signOut` / `auth:loadState` handlers + push helper.
- **Modify** `Web/WebViewPlayerWindow.xaml.cs` — wire token injection (`WebResourceRequested`), background refresh loop, auth-state push.
- **Delete** (Unit G) `MainWindow.xaml`, `MainWindow.xaml.cs`, `Shell/PlayerShellViewModel.cs`, `Preview/PreviewPlayerShell.cs`, unused `Mvvm/`.

### Server (`src/AFK4.Platform.Api/`)
- **Create** `Endpoints/PlayerCatalogEndpoints.cs` — `GET /api/me/branches/{branchId}/tariffs` + `/packages`.
- **Modify** `Program.cs` — map the new endpoints under the `player-me` rate-limit; add the shell origin to CORS.

### Web (`src/AFK4.Player.Shell.Web/src/`)
- **Create** `shellAuth.ts`, `useAuth.tsx` (AuthContext), `screens/LoginScreen.tsx`.
- **Create** `shellApi.ts`, `apiTypes.ts`, `paymentStatus.ts`, `idbCache.ts`.
- **Create** `screens/TopUpScreen.tsx`, `screens/ExtendScreen.tsx`, `screens/SelfServiceMenu.tsx`.
- **Modify** `App.tsx` — mount `AuthProvider`, add self-service routing.
- **Modify** i18n message catalog(s) used by `@afk4/i18n`.

### Tests
- Native: `tests/AFK4.Player.Shell.Tests/Identity/PlayerApiAuthClientTests.cs`, `Web/AuthorizationHeaderPolicyTests.cs`, extend `Web/PlayerShellWebHostBridgeTests.cs`.
- Server: `tests/AFK4.Platform.Api.Tests/PlayerCatalogEndpointTests.cs`.
- Web: `*.test.ts(x)` next to each new module.

---

## UNIT D — Login + native token transport

### Task D1: `ApiBaseUrl` option + `AuthorizationHeaderPolicy`

**Files:**
- Modify: `src/AFK4.Player.Shell/Configuration/PlayerShellOptions.cs`
- Create: `src/AFK4.Player.Shell/Web/AuthorizationHeaderPolicy.cs`
- Test: `tests/AFK4.Player.Shell.Tests/Web/AuthorizationHeaderPolicyTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using AFK4.Player.Shell.Web;

namespace AFK4.Player.Shell.Tests.Web;

public sealed class AuthorizationHeaderPolicyTests
{
    private const string ApiBase = "https://afk4.staging.mubi.dev";

    [Fact]
    public void Injects_WhenRequestMatchesApiOriginAndTokenPresent()
    {
        var decision = AuthorizationHeaderPolicy.Decide(
            requestUri: "https://afk4.staging.mubi.dev/api/me/dashboard",
            apiBaseUrl: ApiBase,
            accessToken: "tok123");

        Assert.True(decision.ShouldInject);
        Assert.Equal("Bearer tok123", decision.HeaderValue);
    }

    [Fact]
    public void DoesNotInject_WhenTokenMissing()
    {
        var decision = AuthorizationHeaderPolicy.Decide(
            "https://afk4.staging.mubi.dev/api/me/dashboard", ApiBase, accessToken: null);

        Assert.False(decision.ShouldInject);
    }

    [Fact]
    public void DoesNotInject_ForForeignOrigin()
    {
        var decision = AuthorizationHeaderPolicy.Decide(
            "https://evil.example.com/api/me/dashboard", ApiBase, accessToken: "tok123");

        Assert.False(decision.ShouldInject);
    }

    [Fact]
    public void DoesNotInject_ForLocalVirtualHostAssets()
    {
        var decision = AuthorizationHeaderPolicy.Decide(
            "https://player.afk4.local/index.html", ApiBase, accessToken: "tok123");

        Assert.False(decision.ShouldInject);
    }
}
```

- [ ] **Step 2: Run it, verify it fails**

Run (Windows bridge): build the project; expected FAIL — `AuthorizationHeaderPolicy` does not exist.

- [ ] **Step 3: Implement**

```csharp
namespace AFK4.Player.Shell.Web;

public readonly record struct AuthorizationHeaderDecision(bool ShouldInject, string? HeaderValue);

/// Pure decision so the WebResourceRequested glue stays untestable-thin: only
/// requests to the configured API origin get the bearer header, and only when a
/// token is held. Foreign origins (and the local asset virtual host) never do.
public static class AuthorizationHeaderPolicy
{
    public static AuthorizationHeaderDecision Decide(string? requestUri, string? apiBaseUrl, string? accessToken)
    {
        if (string.IsNullOrEmpty(accessToken)
            || !Uri.TryCreate(requestUri, UriKind.Absolute, out var request)
            || !Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var apiBase))
        {
            return new AuthorizationHeaderDecision(false, null);
        }

        var sameOrigin = string.Equals(request.Scheme, apiBase.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(request.Host, apiBase.Host, StringComparison.OrdinalIgnoreCase)
            && request.Port == apiBase.Port;

        return sameOrigin
            ? new AuthorizationHeaderDecision(true, $"Bearer {accessToken}")
            : new AuthorizationHeaderDecision(false, null);
    }
}
```

Add to `PlayerShellOptions` (mirror the existing string-prop style in that file):

```csharp
public string ApiBaseUrl { get; set; } =
    Environment.GetEnvironmentVariable("AFK4_PLATFORM_API_BASE_URL") ?? "https://afk4.staging.mubi.dev";
```

- [ ] **Step 4: Run tests, verify pass** (4/4).

- [ ] **Step 5: Commit** — `feat(player-shell): API base option + authorization-header policy`

---

### Task D2: `PlayerApiAuthClient` (sign-in / refresh / sign-out)

**Files:**
- Create: `src/AFK4.Player.Shell/Identity/IPlayerApiAuthClient.cs`
- Create: `src/AFK4.Player.Shell/Identity/PlayerApiAuthClient.cs`
- Test: `tests/AFK4.Player.Shell.Tests/Identity/PlayerApiAuthClientTests.cs`

Server contracts (from `AFK4.Shared.Contracts/Players/`, already referenced by the project):
`PlayerSignInRequest(Guid OrganizationId, string PhoneNumber, string Password)`,
`PlayerSignInResponse(Guid PlayerAccountId, Guid OrganizationId, string DisplayName, bool PhoneVerified, string AccessToken, DateTimeOffset AccessTokenExpiresAtUtc, string RefreshToken, DateTimeOffset RefreshTokenExpiresAtUtc)`,
`PlayerRefreshRequest(string RefreshToken)`.

- [ ] **Step 1: Write the failing test**

```csharp
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using AFK4.Player.Shell.Identity;
using AFK4.Shared.Contracts.Players;

namespace AFK4.Player.Shell.Tests.Identity;

public sealed class PlayerApiAuthClientTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
            return Task.FromResult(responder(request));
        }
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static HttpResponseMessage Ok(PlayerSignInResponse body) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(body, options: Json) };

    private static PlayerSignInResponse Response(string access, string refresh, DateTimeOffset accessExp) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Alex", true, access, accessExp, refresh, DateTimeOffset.UtcNow.AddDays(30));

    [Fact]
    public async Task SignIn_Success_StoresTokensAndReturnsSnapshotWithoutSecret()
    {
        var handler = new StubHandler(_ => Ok(Response("acc-1", "ref-1", DateTimeOffset.UtcNow.AddHours(1))));
        var client = new PlayerApiAuthClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

        var snapshot = await client.SignInAsync(Guid.NewGuid(), "+992900000000", "pw", CancellationToken.None);

        Assert.True(snapshot.Authenticated);
        Assert.Equal("Alex", snapshot.DisplayName);
        Assert.True(snapshot.PhoneVerified);
        Assert.Equal("acc-1", client.CurrentAccessToken);
        Assert.Contains(handler.Requests, r => r.RequestUri!.AbsolutePath == "/api/public/player/sign-in");
    }

    [Fact]
    public async Task SignIn_Unauthorized_ReturnsUnauthenticatedAndHoldsNoToken()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var client = new PlayerApiAuthClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

        var snapshot = await client.SignInAsync(Guid.NewGuid(), "+992900000000", "bad", CancellationToken.None);

        Assert.False(snapshot.Authenticated);
        Assert.Null(client.CurrentAccessToken);
    }

    [Fact]
    public async Task EnsureFreshToken_RefreshesWhenAccessExpired()
    {
        var calls = 0;
        var handler = new StubHandler(req =>
        {
            calls++;
            // first sign-in: already-expired access token; second call must be a refresh
            return req.RequestUri!.AbsolutePath == "/api/public/player/refresh"
                ? Ok(Response("acc-2", "ref-2", DateTimeOffset.UtcNow.AddHours(1)))
                : Ok(Response("acc-1", "ref-1", DateTimeOffset.UtcNow.AddSeconds(-5)));
        });
        var client = new PlayerApiAuthClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

        await client.SignInAsync(Guid.NewGuid(), "+992900000000", "pw", CancellationToken.None);
        await client.EnsureFreshTokenAsync(CancellationToken.None);

        Assert.Equal("acc-2", client.CurrentAccessToken);
        Assert.Contains(handler.Requests, r => r.RequestUri!.AbsolutePath == "/api/public/player/refresh");
    }

    [Fact]
    public async Task SignOut_ClearsToken()
    {
        var handler = new StubHandler(_ => Ok(Response("acc-1", "ref-1", DateTimeOffset.UtcNow.AddHours(1))));
        var client = new PlayerApiAuthClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

        await client.SignInAsync(Guid.NewGuid(), "+992900000000", "pw", CancellationToken.None);
        client.SignOut();

        Assert.Null(client.CurrentAccessToken);
        Assert.False(client.Current.Authenticated);
    }
}
```

- [ ] **Step 2: Run, verify fails** — type does not exist.

- [ ] **Step 3: Implement**

`IPlayerApiAuthClient.cs`:

```csharp
namespace AFK4.Player.Shell.Identity;

public readonly record struct AuthSnapshot(bool Authenticated, string? DisplayName, bool PhoneVerified);

public interface IPlayerApiAuthClient
{
    AuthSnapshot Current { get; }
    string? CurrentAccessToken { get; }
    Task<AuthSnapshot> SignInAsync(Guid organizationId, string phoneNumber, string password, CancellationToken ct);
    Task EnsureFreshTokenAsync(CancellationToken ct);
    void SignOut();
}
```

`PlayerApiAuthClient.cs`:

```csharp
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using AFK4.Shared.Contracts.Players;

namespace AFK4.Player.Shell.Identity;

/// Holds player tokens in memory ONLY (never handed to JS). The WebView injects
/// CurrentAccessToken on API-origin requests; this client refreshes proactively.
public sealed class PlayerApiAuthClient(HttpClient http) : IPlayerApiAuthClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan RefreshSkew = TimeSpan.FromMinutes(2);
    private readonly SemaphoreSlim gate = new(1, 1);

    private string? accessToken;
    private string? refreshToken;
    private DateTimeOffset accessExpiresAtUtc;
    private AuthSnapshot current;

    public AuthSnapshot Current => current;
    public string? CurrentAccessToken => accessToken;

    public async Task<AuthSnapshot> SignInAsync(Guid organizationId, string phoneNumber, string password, CancellationToken ct)
    {
        var response = await http.PostAsJsonAsync(
            "/api/public/player/sign-in",
            new PlayerSignInRequest(organizationId, phoneNumber, password),
            Json,
            ct);

        if (response.StatusCode == HttpStatusCode.Unauthorized || !response.IsSuccessStatusCode)
        {
            return current = new AuthSnapshot(false, null, false);
        }

        var body = await response.Content.ReadFromJsonAsync<PlayerSignInResponse>(Json, ct);
        return body is null ? (current = new AuthSnapshot(false, null, false)) : Store(body);
    }

    public async Task EnsureFreshTokenAsync(CancellationToken ct)
    {
        if (refreshToken is null || DateTimeOffset.UtcNow < accessExpiresAtUtc - RefreshSkew)
        {
            return;
        }

        await gate.WaitAsync(ct);
        try
        {
            if (refreshToken is null || DateTimeOffset.UtcNow < accessExpiresAtUtc - RefreshSkew)
            {
                return;
            }

            var response = await http.PostAsJsonAsync(
                "/api/public/player/refresh", new PlayerRefreshRequest(refreshToken), Json, ct);

            if (!response.IsSuccessStatusCode)
            {
                SignOut();
                return;
            }

            var body = await response.Content.ReadFromJsonAsync<PlayerSignInResponse>(Json, ct);
            if (body is not null)
            {
                Store(body);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public void SignOut()
    {
        accessToken = null;
        refreshToken = null;
        accessExpiresAtUtc = default;
        current = new AuthSnapshot(false, null, false);
    }

    private AuthSnapshot Store(PlayerSignInResponse body)
    {
        accessToken = body.AccessToken;
        refreshToken = body.RefreshToken;
        accessExpiresAtUtc = body.AccessTokenExpiresAtUtc;
        return current = new AuthSnapshot(true, body.DisplayName, body.PhoneVerified);
    }
}
```

- [ ] **Step 4: Run tests, verify pass** (4/4).

- [ ] **Step 5: Commit** — `feat(player-shell): native player API auth client (tokens stay in host)`

---

### Task D3: Bridge `auth:*` handlers

**Files:**
- Modify: `src/AFK4.Player.Shell/Web/PlayerShellWebHostBridge.cs`
- Test: `tests/AFK4.Player.Shell.Tests/Web/PlayerShellWebHostBridgeTests.cs` (extend)

The bridge constructor currently is `PlayerShellWebHostBridge(ILauncherCommandClient launcher, Func<PlayerShellStateDto?> getLatestState)`. Add `IPlayerApiAuthClient auth`.

- [ ] **Step 1: Write the failing tests** (append to the existing test class; update the existing `CreateBridge` helper to pass a stub auth client)

```csharp
private sealed class StubAuth : IPlayerApiAuthClient
{
    public AuthSnapshot Current { get; private set; }
    public string? CurrentAccessToken => Current.Authenticated ? "tok" : null;
    public Guid? LastOrg { get; private set; }
    public bool Fail { get; set; }

    public Task<AuthSnapshot> SignInAsync(Guid organizationId, string phone, string password, CancellationToken ct)
    {
        LastOrg = organizationId;
        Current = Fail ? new AuthSnapshot(false, null, false) : new AuthSnapshot(true, "Alex", true);
        return Task.FromResult(Current);
    }
    public Task EnsureFreshTokenAsync(CancellationToken ct) => Task.CompletedTask;
    public void SignOut() => Current = new AuthSnapshot(false, null, false);
}

[Fact]
public async Task SignIn_UsesOrgFromPipeState_ReturnsSnapshotWithoutToken()
{
    var auth = new StubAuth();
    var org = Guid.NewGuid();
    var state = StateWith(org); // helper: PlayerShellStateDto with OrganizationId = org, State = Active
    var bridge = new PlayerShellWebHostBridge(new StubLauncher(), () => state, auth);

    var request = """{"requestId":"a1","type":"auth:signIn","payload":{"phoneNumber":"+992900000000","password":"pw"}}""";
    var response = Parse((await bridge.HandleAsync(request, CancellationToken.None))!);

    Assert.True(response.GetProperty("ok").GetBoolean());
    Assert.Equal(org, auth.LastOrg);
    Assert.True(response.GetProperty("payload").GetProperty("authenticated").GetBoolean());
    Assert.Equal("Alex", response.GetProperty("payload").GetProperty("displayName").GetString());
    Assert.False(response.GetProperty("payload").TryGetProperty("accessToken", out _)); // never leaks
}

[Fact]
public async Task SignIn_WithoutPipeState_IsRejected()
{
    var bridge = new PlayerShellWebHostBridge(new StubLauncher(), getLatestState: () => null, new StubAuth());
    var request = """{"requestId":"a2","type":"auth:signIn","payload":{"phoneNumber":"x","password":"y"}}""";
    var response = Parse((await bridge.HandleAsync(request, CancellationToken.None))!);

    Assert.False(response.GetProperty("ok").GetBoolean());
    Assert.Equal("no_state", response.GetProperty("error").GetProperty("code").GetString());
}

[Fact]
public async Task LoadAuthState_ReflectsClient()
{
    var auth = new StubAuth();
    await auth.SignInAsync(Guid.NewGuid(), "p", "pw", CancellationToken.None);
    var bridge = new PlayerShellWebHostBridge(new StubLauncher(), () => StateWith(Guid.NewGuid()), auth);

    var response = Parse((await bridge.HandleAsync("""{"requestId":"a3","type":"auth:loadState"}""", CancellationToken.None))!);
    Assert.True(response.GetProperty("payload").GetProperty("authenticated").GetBoolean());
}
```

Add a `StateWith(Guid org)` helper in the test class building a minimal `PlayerShellStateDto` (mirror the `LoadStateRequest_ReturnsCurrentState` test's DTO construction, with `OrganizationId: org`).

- [ ] **Step 2: Run, verify fails** — constructor arity + new types unknown.

- [ ] **Step 3: Implement** — extend the bridge:

Add `using AFK4.Player.Shell.Identity;`. Change the primary constructor to:

```csharp
public sealed class PlayerShellWebHostBridge(
    ILauncherCommandClient launcher,
    Func<PlayerShellStateDto?> getLatestState,
    IPlayerApiAuthClient auth)
```

Add to `AllowedTypes`: `"auth:signIn"`, `"auth:signOut"`, `"auth:loadState"`. Add switch arms:

```csharp
"auth:signIn" => await HandleSignInAsync(requestId, payload, cancellationToken),
"auth:signOut" => HandleSignOut(requestId),
"auth:loadState" => Ok(requestId, Snapshot()),
```

New members:

```csharp
private async Task<string> HandleSignInAsync(string requestId, JsonElement payload, CancellationToken ct)
{
    var state = getLatestState();
    if (state is null)
    {
        return Error(requestId, "no_state", "Shell state not yet available; cannot determine organization.");
    }

    if (payload.ValueKind != JsonValueKind.Object
        || !payload.TryGetProperty("phoneNumber", out var phoneEl) || phoneEl.ValueKind != JsonValueKind.String
        || !payload.TryGetProperty("password", out var pwEl) || pwEl.ValueKind != JsonValueKind.String
        || string.IsNullOrWhiteSpace(phoneEl.GetString()) || string.IsNullOrEmpty(pwEl.GetString()))
    {
        return Error(requestId, "invalid_payload", "auth:signIn requires phoneNumber and password.");
    }

    await auth.SignInAsync(state.OrganizationId, phoneEl.GetString()!, pwEl.GetString()!, ct);
    return Ok(requestId, Snapshot());
}

private string HandleSignOut(string requestId)
{
    auth.SignOut();
    return Ok(requestId, Snapshot());
}

private object Snapshot()
{
    var s = auth.Current;
    return new { authenticated = s.Authenticated, displayName = s.DisplayName, phoneVerified = s.PhoneVerified };
}

public static string CreateAuthPush(AuthSnapshot s) =>
    JsonSerializer.Serialize(
        new { type = "shell:authChanged",
              payload = new { authenticated = s.Authenticated, displayName = s.DisplayName, phoneVerified = s.PhoneVerified } },
        JsonOptions);
```

- [ ] **Step 4: Run tests, verify pass** (existing 5 + new 3).

- [ ] **Step 5: Commit** — `feat(player-shell): bridge auth:signIn/signOut/loadState (token never leaves host)`

---

### Task D4: Wire token injection + refresh in `WebViewPlayerWindow` (Windows-bridge verified)

**Files:**
- Modify: `src/AFK4.Player.Shell/Web/WebViewPlayerWindow.xaml.cs`

This is WebView2 glue — not unit-tested; verified on the Windows bridge. Keep it thin (delegates to `AuthorizationHeaderPolicy` + `PlayerApiAuthClient`).

- [ ] **Step 1: Construct the auth client + pass to bridge**

In the `internal WebViewPlayerWindow(PlayerShellOptions options)` constructor, before building the bridge:

```csharp
var apiHttp = new HttpClient { BaseAddress = new Uri(options.ApiBaseUrl) };
authClient = new PlayerApiAuthClient(apiHttp);
bridge = new PlayerShellWebHostBridge(new LauncherCommandClient(options), getLatestState: () => latestState, authClient);
```

Add fields: `private readonly PlayerApiAuthClient authClient;` and keep `apiHttp` alive (store as field for disposal). Add `using AFK4.Player.Shell.Identity;` and `using System.Net.Http;`.

- [ ] **Step 2: Register the WebResourceRequested injector** (in `OnLoaded`, right after `HardenForKiosk`)

```csharp
var apiBase = options.ApiBaseUrl.TrimEnd('/');
Browser.CoreWebView2.AddWebResourceRequestedFilter(apiBase + "/*", CoreWebView2WebResourceContext.All);
Browser.CoreWebView2.WebResourceRequested += OnApiResourceRequested;
```

```csharp
private void OnApiResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
{
    var decision = AuthorizationHeaderPolicy.Decide(e.Request.Uri, options.ApiBaseUrl, authClient.CurrentAccessToken);
    if (decision.ShouldInject)
    {
        e.Request.Headers.SetHeader("Authorization", decision.HeaderValue!);
    }
}
```

- [ ] **Step 3: Background refresh + auth push** (add a loop started in `OnLoaded`, like `ListenForStateAsync`)

```csharp
_ = RefreshAuthLoopAsync(lifetime.Token);
```

```csharp
private async Task RefreshAuthLoopAsync(CancellationToken ct)
{
    try
    {
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(1), ct);
            await authClient.EnsureFreshTokenAsync(ct);
        }
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
}
```

After each `auth:signIn`/`auth:signOut` response is posted in `OnWebMessageReceived`, also push the snapshot so other components react:

```csharp
// after PostWebMessageAsJson(responseJson):
if (Browser.CoreWebView2 is not null)
{
    Browser.CoreWebView2.PostWebMessageAsJson(PlayerShellWebHostBridge.CreateAuthPush(authClient.Current));
}
```

Dispose `apiHttp` in `OnClosed`.

- [ ] **Step 4: Build on Windows bridge** — follow env-quirks runbook: `find src -type d \( -name obj -o -name bin \) -prune -exec rm -rf {} +`; build the web dist in WSL; `cp -r` into the D: clone; then `powershell.exe -NoProfile -Command "cd D:\projects\afk4.net; & 'C:\Program Files\dotnet\dotnet.exe' build src\AFK4.Player.Shell\AFK4.Player.Shell.csproj --nologo"`. Expected: 0 errors. Re-run `dotnet.exe test tests\AFK4.Player.Shell.Tests\AFK4.Player.Shell.Tests.csproj` — all green.

- [ ] **Step 5: Commit** — `feat(player-shell): inject bearer on API origin + background refresh`

---

### Task D5: Web `shellAuth.ts` + `AuthProvider`

**Files:**
- Create: `src/AFK4.Player.Shell.Web/src/shellAuth.ts`
- Create: `src/AFK4.Player.Shell.Web/src/useAuth.tsx`
- Test: `src/AFK4.Player.Shell.Web/src/useAuth.test.tsx`

`shellAuth.ts` reuses the existing `postShellRequest` + `onShellStateChanged`-style listener from `shellBridge.ts`. Add a generic state listener for `shell:authChanged` (mirror `onShellStateChanged`).

- [ ] **Step 1: Write the failing test** (mock `window.chrome.webview` like the foundation bridge tests)

```tsx
import { render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, beforeEach } from 'bun:test';
import { AuthProvider, useAuth } from './useAuth';

function installBridge(responder: (msg: any) => any) {
  const listeners: Array<(e: { data: unknown }) => void> = [];
  (window as any).chrome = {
    webview: {
      postMessage(msg: any) {
        const payload = responder(msg);
        queueMicrotask(() =>
          listeners.forEach((l) => l({ data: { type: 'host:response', requestId: msg.requestId, ok: true, payload } })));
      },
      addEventListener: (_t: string, l: any) => listeners.push(l),
      removeEventListener: () => {}
    }
  };
}

function Probe() {
  const { auth } = useAuth();
  return <div>{auth.authenticated ? `hi ${auth.displayName}` : 'anon'}</div>;
}

describe('useAuth', () => {
  beforeEach(() => { delete (window as any).chrome; });

  it('loads anonymous auth state on mount', async () => {
    installBridge(() => ({ authenticated: false, displayName: null, phoneVerified: false }));
    render(<AuthProvider><Probe /></AuthProvider>);
    await waitFor(() => expect(screen.getByText('anon')).toBeInTheDocument());
  });

  it('reflects a successful sign-in', async () => {
    installBridge((msg) =>
      msg.type === 'auth:signIn'
        ? { authenticated: true, displayName: 'Alex', phoneVerified: true }
        : { authenticated: false, displayName: null, phoneVerified: false });
    function SignInProbe() {
      const { auth, signIn } = useAuth();
      return (<><button onClick={() => signIn('+992900000000', 'pw')}>in</button>
        <span>{auth.authenticated ? `hi ${auth.displayName}` : 'anon'}</span></>);
    }
    render(<AuthProvider><SignInProbe /></AuthProvider>);
    screen.getByText('in').click();
    await waitFor(() => expect(screen.getByText('hi Alex')).toBeInTheDocument());
  });
});
```

- [ ] **Step 2: Run, verify fails** — `bun test src/useAuth.test.tsx` — module missing.

- [ ] **Step 3: Implement**

`shellAuth.ts`:

```ts
import { postShellRequest } from './shellBridge';

export interface AuthSnapshot {
  authenticated: boolean;
  displayName: string | null;
  phoneVerified: boolean;
}

export const ANONYMOUS: AuthSnapshot = { authenticated: false, displayName: null, phoneVerified: false };

export function loadAuthState(): Promise<AuthSnapshot> {
  return postShellRequest<AuthSnapshot>('auth:loadState').catch(() => ANONYMOUS);
}

export function signIn(phoneNumber: string, password: string): Promise<AuthSnapshot> {
  return postShellRequest<AuthSnapshot>('auth:signIn', { phoneNumber, password });
}

export function signOut(): Promise<AuthSnapshot> {
  return postShellRequest<AuthSnapshot>('auth:signOut');
}

export function onAuthChanged(handler: (s: AuthSnapshot) => void): () => void {
  const webview = window.chrome?.webview;
  if (!webview?.addEventListener) return () => {};
  const listener = (event: { data: unknown }) => {
    const data = event.data as { type?: string; payload?: AuthSnapshot };
    if (data?.type === 'shell:authChanged' && data.payload) handler(data.payload);
  };
  webview.addEventListener('message', listener);
  return () => webview.removeEventListener?.('message', listener);
}
```

`useAuth.tsx`:

```tsx
import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { ANONYMOUS, loadAuthState, onAuthChanged, signIn as apiSignIn, signOut as apiSignOut, type AuthSnapshot } from './shellAuth';

interface AuthContextValue {
  auth: AuthSnapshot;
  signIn: (phoneNumber: string, password: string) => Promise<AuthSnapshot>;
  signOut: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [auth, setAuth] = useState<AuthSnapshot>(ANONYMOUS);

  useEffect(() => {
    let active = true;
    loadAuthState().then((s) => active && setAuth(s));
    const off = onAuthChanged(setAuth);
    return () => { active = false; off(); };
  }, []);

  const value = useMemo<AuthContextValue>(() => ({
    auth,
    signIn: async (phone, password) => { const s = await apiSignIn(phone, password); setAuth(s); return s; },
    signOut: async () => { const s = await apiSignOut(); setAuth(s); }
  }), [auth]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
}
```

- [ ] **Step 4: Run tests, verify pass.** Then `bun run build` (type-check).

- [ ] **Step 5: Commit** — `feat(player-shell-web): auth bridge client + AuthProvider`

---

### Task D6: `LoginScreen` + App auth gate + i18n

**Files:**
- Create: `src/AFK4.Player.Shell.Web/src/screens/LoginScreen.tsx`
- Test: `src/AFK4.Player.Shell.Web/src/screens/LoginScreen.test.tsx`
- Modify: `src/AFK4.Player.Shell.Web/src/App.tsx`
- Modify: i18n catalog (locate the catalog `@afk4/i18n` consumes — mirror how `ActiveSessionScreen` already resolves strings; add keys `login.title`, `login.phone`, `login.password`, `login.submit`, `login.error`).

- [ ] **Step 1: Write the failing test**

```tsx
import { render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'bun:test';
import { LoginScreen } from './LoginScreen';

describe('LoginScreen', () => {
  it('calls onSubmit with entered credentials', async () => {
    let captured: { phone: string; password: string } | null = null;
    render(<LoginScreen onSubmit={async (phone, password) => { captured = { phone, password }; return true; }} />);

    (screen.getByLabelText(/phone/i) as HTMLInputElement).value = '';
    const phone = screen.getByLabelText(/phone/i) as HTMLInputElement;
    const pw = screen.getByLabelText(/password/i) as HTMLInputElement;
    // Testing Library fireEvent is available; use it to set values
    const { fireEvent } = await import('@testing-library/react');
    fireEvent.change(phone, { target: { value: '+992900000000' } });
    fireEvent.change(pw, { target: { value: 'secret' } });
    fireEvent.click(screen.getByRole('button', { name: /sign in|войти/i }));

    await waitFor(() => expect(captured).toEqual({ phone: '+992900000000', password: 'secret' }));
  });

  it('shows an error when sign-in fails', async () => {
    render(<LoginScreen onSubmit={async () => false} />);
    const { fireEvent } = await import('@testing-library/react');
    fireEvent.change(screen.getByLabelText(/phone/i), { target: { value: 'x' } });
    fireEvent.change(screen.getByLabelText(/password/i), { target: { value: 'y' } });
    fireEvent.click(screen.getByRole('button', { name: /sign in|войти/i }));
    await waitFor(() => expect(screen.getByRole('alert')).toBeInTheDocument());
  });
});
```

- [ ] **Step 2: Run, verify fails.**

- [ ] **Step 3: Implement** (match the i18n usage already present in `ActiveSessionScreen.tsx`; the snippet below uses a `t()`-style helper — adapt to the exact hook the foundation screens use)

```tsx
import { useState, type FormEvent } from 'react';

export interface LoginScreenProps {
  /** returns true on success, false on bad credentials */
  onSubmit: (phoneNumber: string, password: string) => Promise<boolean>;
}

export function LoginScreen({ onSubmit }: LoginScreenProps) {
  const [phone, setPhone] = useState('');
  const [password, setPassword] = useState('');
  const [pending, setPending] = useState(false);
  const [failed, setFailed] = useState(false);

  async function handle(e: FormEvent) {
    e.preventDefault();
    setPending(true);
    setFailed(false);
    const ok = await onSubmit(phone.trim(), password).catch(() => false);
    setPending(false);
    if (!ok) setFailed(true);
  }

  return (
    <form onSubmit={handle} aria-label="login">
      <h1>Войти</h1>
      <label htmlFor="phone">Телефон</label>
      <input id="phone" inputMode="tel" autoComplete="username"
             value={phone} onChange={(e) => setPhone(e.target.value)} />
      <label htmlFor="password">Пароль</label>
      <input id="password" type="password" autoComplete="current-password"
             value={password} onChange={(e) => setPassword(e.target.value)} />
      {failed && <p role="alert">Неверный телефон или пароль</p>}
      <button type="submit" disabled={pending || !phone || !password}>Войти</button>
    </form>
  );
}
```

> Replace literal strings with i18n keys per the project pattern before finishing (the test matches `/sign in|войти/i` to tolerate either). Confirm the foundation screens' i18n hook and reuse it.

- [ ] **Step 4: Wire into `App.tsx`** — wrap with `AuthProvider` and route self-service through auth:

```tsx
import { AuthProvider } from './useAuth';
// ...
export function App() {
  return (
    <AuthProvider>
      <ShellRouter />
    </AuthProvider>
  );
}
```

Move the existing locked/active logic into `ShellRouter` (unchanged), and have `ActiveSessionScreen`'s self-service entry consult `useAuth()`: if not authenticated, render `LoginScreen` (with `onSubmit={(p, pw) => signIn(p, pw).then((s) => s.authenticated)}`) before showing extend/top-up. Keep lock routing untouched (Assumption 4).

- [ ] **Step 5: Run tests + `bun run build`; verify pass.**

- [ ] **Step 6: Commit** — `feat(player-shell-web): login screen + auth-gated self-service`

---

## UNIT E — Self-service: tariffs + extend + top-up (QR)

### Task E1: Player-facing tariff/package listing endpoints (server)

**Files:**
- Create: `src/AFK4.Platform.Api/Endpoints/PlayerCatalogEndpoints.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs` (map + rate limit)
- Test: `tests/AFK4.Platform.Api.Tests/PlayerCatalogEndpointTests.cs`

Reuse `IOperatorReferenceDataService.GetTariffOptionsAsync(orgId, branchId, ct)` → `List<TariffOptionDto>` and `GetPackageOptionsAsync(orgId, branchId, ct)` → `List<PackageOptionDto>`. Auth via `IPlayerContextAccessor` (player bearer; `/api/me/*`). Validate the branch belongs to the player's org.

- [ ] **Step 1: Write the failing test** (mirror the harness in `DcGateTopUpIntentTests.cs` — it seeds a player + authenticates; copy its setup)

```csharp
[Fact]
public async Task ListTariffs_ForOwnOrgBranch_ReturnsOptions()
{
    // arrange: seed org + branch + an active tariff; seed player in same org; sign in -> bearer
    var client = await SignedInPlayerClientAsync(out var branchId);

    var response = await client.GetAsync($"/api/me/branches/{branchId}/tariffs");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var options = await response.Content.ReadFromJsonAsync<List<TariffOptionDto>>();
    Assert.NotEmpty(options!);
}

[Fact]
public async Task ListTariffs_ForForeignBranch_Returns404()
{
    var client = await SignedInPlayerClientAsync(out _);
    var foreignBranch = Guid.NewGuid();

    var response = await client.GetAsync($"/api/me/branches/{foreignBranch}/tariffs");

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
}

[Fact]
public async Task ListTariffs_Unauthenticated_Returns401()
{
    var client = UnauthenticatedClient();
    var response = await client.GetAsync($"/api/me/branches/{Guid.NewGuid()}/tariffs");
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
}
```

(Add an equivalent `ListPackages_*` happy-path test against `/packages`.)

- [ ] **Step 2: Run, verify fails** — 404 (route missing).

- [ ] **Step 3: Implement** `PlayerCatalogEndpoints.cs`

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Operator; // IOperatorReferenceDataService (verify namespace)
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Endpoints;

public static class PlayerCatalogEndpoints
{
    public static void MapPlayerCatalogEndpoints(this WebApplication app)
    {
        app.MapGet("/api/me/branches/{branchId:guid}/tariffs", async (
            Guid branchId,
            IPlayerContextAccessor playerContextAccessor,
            PlatformDbContext dbContext,
            IOperatorReferenceDataService referenceData,
            CancellationToken ct) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null) return Results.Unauthorized();
            if (!await BranchInOrgAsync(dbContext, branchId, player.OrganizationId, ct)) return Results.NotFound();

            var options = await referenceData.GetTariffOptionsAsync(player.OrganizationId, branchId, ct);
            return Results.Ok(options);
        }).RequireRateLimiting("player-me");

        app.MapGet("/api/me/branches/{branchId:guid}/packages", async (
            Guid branchId,
            IPlayerContextAccessor playerContextAccessor,
            PlatformDbContext dbContext,
            IOperatorReferenceDataService referenceData,
            CancellationToken ct) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null) return Results.Unauthorized();
            if (!await BranchInOrgAsync(dbContext, branchId, player.OrganizationId, ct)) return Results.NotFound();

            var options = await referenceData.GetPackageOptionsAsync(player.OrganizationId, branchId, ct);
            return Results.Ok(options);
        }).RequireRateLimiting("player-me");
    }

    private static Task<bool> BranchInOrgAsync(PlatformDbContext db, Guid branchId, Guid orgId, CancellationToken ct) =>
        db.Branches.AsNoTracking().AnyAsync(b => b.BranchId == branchId && b.OrganizationId == orgId, ct);
}
```

> Verify exact names while implementing: the `IOperatorReferenceDataService` namespace, `PlatformDbContext.Branches` DbSet, and `BranchEntity.BranchId`/`OrganizationId`. Match the registration style of `PlayerSelfServiceEndpoints` in `Program.cs` (find its `Map…Endpoints(...)` call and add `app.MapPlayerCatalogEndpoints();` beside it).

- [ ] **Step 4: Run tests, verify pass.** Then run the full Platform.Api test project to confirm no regressions.

- [ ] **Step 5: Commit** — `feat(api): player-facing tariff/package listing under /api/me`

---

### Task E2: Allow the shell origin through CORS

**Files:**
- Modify: `src/AFK4.Platform.Api/Program.cs` (CORS policy)

- [ ] **Step 1:** Locate the existing CORS policy (search `AddCors` / `WithOrigins`). The WebView2 web runs at origin `https://player.afk4.local` (prod virtual host) and `http://127.0.0.1:5175` (dev server). Add both to the allowed origins for the player/public + `/api/me` surface (mirror how the customer-web origin is currently allowed).

- [ ] **Step 2:** If the project has CORS integration tests, add a case asserting a preflight from `https://player.afk4.local` to `/api/me/...` is allowed; otherwise note this is verified on the Windows bridge / staging (browser-enforced, not unit-testable here).

- [ ] **Step 3: Commit** — `feat(api): allow player-shell WebView origin through CORS`

---

### Task E3: Web `shellApi.ts` (authed fetch + offline detection)

**Files:**
- Create: `src/AFK4.Player.Shell.Web/src/apiTypes.ts`
- Create: `src/AFK4.Player.Shell.Web/src/shellApi.ts`
- Test: `src/AFK4.Player.Shell.Web/src/shellApi.test.ts`

`apiTypes.ts` — hand-mirrored TS for the DTOs used here (camelCase; values mirror the wire). Add a header comment "Hand-mirrored from AFK4.Shared.Contracts; no codegen — keep in sync." Define:

```ts
export interface MoneyDto { currencyCode: string; minorUnits: number; }

export interface TariffOptionDto {
  tariffId: string; tariffVersionId: string; name: string; tariffRuleVersionId: string;
  versionNumber: number; currencyCode: string; pricePerMinuteMinorUnits: number;
  minimumBillableMinutes: number; roundingIncrementMinutes: number; effectiveFromUtc: string;
}

export interface PackageOptionDto {
  packageDefinitionId: string; name: string; currencyCode: string; priceMinorUnits: number;
  includedSeconds: number; bonusSeconds: number; expiresAfterDays: number;
}

export interface PlayerTopUpIntentDto {
  paymentIntentId: string; amountMinorUnits: number; currencyCode: string; state: string;
  purpose: string; method: string; createdAtUtc: string; fulfilledAtUtc: string | null;
  isExpired: boolean; payUrl: string | null; comment: string | null; gatewayExpiresAtUtc: string | null;
}

export interface ExtendSessionRequest {
  additionalMinutes: number; tariffRuleVersionId: string; idempotencyKey: string;
}
```

- [ ] **Step 1: Write the failing test**

```ts
import { describe, expect, it } from 'bun:test';
import { createShellApi, OfflineError } from './shellApi';

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
}

describe('shellApi', () => {
  it('lists tariffs for a branch', async () => {
    const api = createShellApi('https://api.test', async () => jsonResponse([{ name: 'Standard' }]));
    const tariffs = await api.listTariffs('branch-1');
    expect(tariffs[0].name).toBe('Standard');
  });

  it('creates a dcgate top-up intent', async () => {
    let captured: any;
    const api = createShellApi('https://api.test', async (url, init) => {
      captured = { url, body: JSON.parse(String(init?.body)) };
      return jsonResponse({ paymentIntentId: 'p1', state: 'pending', payUrl: 'pay.dc.tj/x' });
    });
    const intent = await api.createTopUpIntent(5000);
    expect(captured.url).toContain('/api/me/wallet/top-up-intent');
    expect(captured.body.method).toBe('dcgate');
    expect(intent.payUrl).toBe('pay.dc.tj/x');
  });

  it('throws OfflineError when fetch rejects', async () => {
    const api = createShellApi('https://api.test', async () => { throw new TypeError('Failed to fetch'); });
    await expect(api.listTariffs('b')).rejects.toBeInstanceOf(OfflineError);
  });

  it('surfaces a 409 as a typed conflict', async () => {
    const api = createShellApi('https://api.test', async () => jsonResponse({ error: 'conflict' }, 409));
    await expect(api.extendSession('s1', { additionalMinutes: 30, tariffRuleVersionId: 't', idempotencyKey: 'k' }))
      .rejects.toMatchObject({ status: 409 });
  });
});
```

- [ ] **Step 2: Run, verify fails.**

- [ ] **Step 3: Implement** `shellApi.ts`

```ts
import type { ExtendSessionRequest, PackageOptionDto, PlayerTopUpIntentDto, TariffOptionDto } from './apiTypes';

export class OfflineError extends Error {
  constructor() { super('offline'); this.name = 'OfflineError'; }
}

export class ApiError extends Error {
  constructor(public status: number, message: string) { super(message); this.name = 'ApiError'; }
}

type FetchLike = (url: string, init?: RequestInit) => Promise<Response>;

// idempotency keys vary the request; crypto.randomUUID is available in the WebView2 origin.
function newKey(): string {
  return (globalThis.crypto?.randomUUID?.() ?? `k-${Date.now()}-${Math.floor(performance.now())}`);
}

export function createShellApi(baseUrl: string, fetchImpl: FetchLike = fetch) {
  const base = baseUrl.replace(/\/$/, '');

  async function call<T>(path: string, init?: RequestInit): Promise<T> {
    let response: Response;
    try {
      response = await fetchImpl(`${base}${path}`, {
        ...init,
        headers: { 'Content-Type': 'application/json', ...(init?.headers ?? {}) }
      });
    } catch {
      throw new OfflineError(); // network failure (the native host injects auth; we never send a token)
    }
    if (!response.ok) {
      throw new ApiError(response.status, `request to ${path} failed: ${response.status}`);
    }
    return (await response.json()) as T;
  }

  return {
    listTariffs: (branchId: string) => call<TariffOptionDto[]>(`/api/me/branches/${branchId}/tariffs`),
    listPackages: (branchId: string) => call<PackageOptionDto[]>(`/api/me/branches/${branchId}/packages`),
    createTopUpIntent: (amountMinorUnits: number, currencyCode = 'TJS') =>
      call<PlayerTopUpIntentDto>('/api/me/wallet/top-up-intent', {
        method: 'POST',
        body: JSON.stringify({ amountMinorUnits, currencyCode, method: 'dcgate' })
      }),
    getTopUpIntents: () => call<PlayerTopUpIntentDto[]>('/api/me/wallet/top-up-intents'),
    extendSession: (sessionId: string, req: Omit<ExtendSessionRequest, 'idempotencyKey'> & { idempotencyKey?: string }) =>
      call<unknown>(`/api/me/sessions/${sessionId}/extend`, {
        method: 'POST',
        body: JSON.stringify({ ...req, idempotencyKey: req.idempotencyKey ?? newKey() })
      })
  };
}

export type ShellApi = ReturnType<typeof createShellApi>;
```

The API base in prod/dev comes from `import.meta.env.VITE_PLATFORM_API_BASE_URL`. Add a tiny `src/apiBase.ts` exporting `export const API_BASE = import.meta.env.VITE_PLATFORM_API_BASE_URL ?? 'https://afk4.staging.mubi.dev';` and a `.env`/`vite-env.d.ts` typing for it.

- [ ] **Step 4: Run tests + `bun run build`; verify pass.**

- [ ] **Step 5: Commit** — `feat(player-shell-web): authed REST client with offline detection`

---

### Task E4: Payment status machine (`paymentStatus.ts`)

**Files:**
- Create: `src/AFK4.Player.Shell.Web/src/paymentStatus.ts`
- Test: `src/AFK4.Player.Shell.Web/src/paymentStatus.test.ts`

The authority is the server intent. Map a `PlayerTopUpIntentDto` to a UI status. `disputed` is a flag with `state` still `pending` server-side, but the API surfaces it via `state`/`isExpired` only — the shell reflects `state` + `isExpired`; "disputed" is currently surfaced as a still-`pending` intent past expiry resolution, so MVP maps: `fulfilled`→fulfilled, `expired` or `isExpired`→expired, else `pending`. (Add a `disputed` arm now keyed off a future `disputed` field; default false.)

- [ ] **Step 1: Write the failing test**

```ts
import { describe, expect, it } from 'bun:test';
import { toPaymentStatus } from './paymentStatus';

const base = {
  paymentIntentId: 'p', amountMinorUnits: 5000, currencyCode: 'TJS', purpose: 'wallet_topup',
  method: 'dcgate', createdAtUtc: '', fulfilledAtUtc: null, payUrl: 'pay.dc.tj/x', comment: '123', gatewayExpiresAtUtc: null
};

describe('toPaymentStatus', () => {
  it('pending while awaiting confirmation', () => {
    expect(toPaymentStatus({ ...base, state: 'pending', isExpired: false })).toBe('pending');
  });
  it('fulfilled when server confirms', () => {
    expect(toPaymentStatus({ ...base, state: 'fulfilled', isExpired: false })).toBe('fulfilled');
  });
  it('expired by state', () => {
    expect(toPaymentStatus({ ...base, state: 'expired', isExpired: false })).toBe('expired');
  });
  it('expired by isExpired flag even if still pending', () => {
    expect(toPaymentStatus({ ...base, state: 'pending', isExpired: true })).toBe('expired');
  });
});
```

- [ ] **Step 2: Run, verify fails.**

- [ ] **Step 3: Implement**

```ts
import type { PlayerTopUpIntentDto } from './apiTypes';

export type PaymentStatus = 'pending' | 'fulfilled' | 'expired' | 'disputed';

/** Server is authoritative. The shell NEVER infers success from "QR scanned"; only `fulfilled` counts. */
export function toPaymentStatus(intent: Pick<PlayerTopUpIntentDto, 'state' | 'isExpired'> & { disputed?: boolean }): PaymentStatus {
  if (intent.disputed) return 'disputed';
  if (intent.state === 'fulfilled') return 'fulfilled';
  if (intent.state === 'expired' || intent.isExpired) return 'expired';
  return 'pending';
}
```

- [ ] **Step 4: Run tests, verify pass.**

- [ ] **Step 5: Commit** — `feat(player-shell-web): payment-intent status machine`

---

### Task E5: `TopUpScreen` (QR + poll + status)

**Files:**
- Create: `src/AFK4.Player.Shell.Web/src/screens/TopUpScreen.tsx`
- Test: `src/AFK4.Player.Shell.Web/src/screens/TopUpScreen.test.tsx`
- Add dependency: `qrcode` (verify it is not already in a workspace package before adding; if a QR helper already exists in `packages/`, reuse it).

- [ ] **Step 1: Write the failing test** (inject a fake `ShellApi` + a fake clock/poll; assert pending→QR shown, then fulfilled→success)

```tsx
import { render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'bun:test';
import { TopUpScreen } from './TopUpScreen';
import type { ShellApi } from '../shellApi';

function fakeApi(over: Partial<ShellApi>): ShellApi {
  return {
    listTariffs: async () => [], listPackages: async () => [],
    createTopUpIntent: async () => ({ paymentIntentId: 'p1', amountMinorUnits: 5000, currencyCode: 'TJS',
      state: 'pending', purpose: 'wallet_topup', method: 'dcgate', createdAtUtc: '', fulfilledAtUtc: null,
      isExpired: false, payUrl: 'pay.dc.tj/abc', comment: '123456789012345678', gatewayExpiresAtUtc: null }),
    getTopUpIntents: async () => [], extendSession: async () => ({}),
    ...over
  } as ShellApi;
}

describe('TopUpScreen', () => {
  it('shows the QR comment after creating an intent', async () => {
    render(<TopUpScreen api={fakeApi({})} amountMinorUnits={5000} pollIntervalMs={5} />);
    await waitFor(() => expect(screen.getByText(/123456789012345678/)).toBeInTheDocument());
    expect(screen.getByTestId('topup-qr')).toBeInTheDocument();
  });

  it('shows success once the intent is fulfilled', async () => {
    let polls = 0;
    const api = fakeApi({
      getTopUpIntents: async () => { polls++;
        return [{ paymentIntentId: 'p1', amountMinorUnits: 5000, currencyCode: 'TJS',
          state: polls >= 2 ? 'fulfilled' : 'pending', purpose: 'wallet_topup', method: 'dcgate',
          createdAtUtc: '', fulfilledAtUtc: null, isExpired: false, payUrl: 'pay.dc.tj/abc',
          comment: '123456789012345678', gatewayExpiresAtUtc: null }]; }
    });
    render(<TopUpScreen api={api} amountMinorUnits={5000} pollIntervalMs={5} />);
    await waitFor(() => expect(screen.getByText(/успешно|success/i)).toBeInTheDocument(), { timeout: 2000 });
  });
});
```

- [ ] **Step 2: Run, verify fails.**

- [ ] **Step 3: Implement** (render QR from `payUrl` via `qrcode` to a data URL; poll `getTopUpIntents` on `pollIntervalMs` until status ≠ pending)

```tsx
import { useEffect, useRef, useState } from 'react';
import QRCode from 'qrcode';
import type { PlayerTopUpIntentDto } from '../apiTypes';
import type { ShellApi } from '../shellApi';
import { toPaymentStatus, type PaymentStatus } from '../paymentStatus';

export interface TopUpScreenProps {
  api: ShellApi;
  amountMinorUnits: number;
  pollIntervalMs?: number;
}

export function TopUpScreen({ api, amountMinorUnits, pollIntervalMs = 3000 }: TopUpScreenProps) {
  const [intent, setIntent] = useState<PlayerTopUpIntentDto | null>(null);
  const [status, setStatus] = useState<PaymentStatus>('pending');
  const [qr, setQr] = useState<string | null>(null);
  const [offline, setOffline] = useState(false);
  const created = useRef(false);

  useEffect(() => {
    if (created.current) return;
    created.current = true;
    api.createTopUpIntent(amountMinorUnits)
      .then(setIntent)
      .catch(() => setOffline(true));
  }, [api, amountMinorUnits]);

  useEffect(() => {
    if (!intent?.payUrl) return;
    QRCode.toDataURL(intent.payUrl).then(setQr).catch(() => setQr(null));
  }, [intent?.payUrl]);

  useEffect(() => {
    if (!intent || status !== 'pending') return;
    const timer = setInterval(async () => {
      try {
        const all = await api.getTopUpIntents();
        const mine = all.find((i) => i.paymentIntentId === intent.paymentIntentId);
        if (mine) setStatus(toPaymentStatus(mine));
      } catch { setOffline(true); }
    }, pollIntervalMs);
    return () => clearInterval(timer);
  }, [api, intent, status, pollIntervalMs]);

  if (offline) return <p role="alert">Временно недоступно — обратитесь к оператору</p>;
  if (!intent) return <p>Создаём платёж…</p>;

  if (status === 'fulfilled') return <p>Оплата успешно зачислена</p>;
  if (status === 'expired') return <p role="alert">Срок истёк — начните заново</p>;
  if (status === 'disputed') return <p role="alert">Платёж на проверке — обратитесь к оператору</p>;

  return (
    <section>
      <h1>Пополнение</h1>
      {qr && <img data-testid="topup-qr" src={qr} alt="QR" />}
      <p>Комментарий: <strong>{intent.comment}</strong></p>
      <p>Ожидаем подтверждение оплаты…</p>
    </section>
  );
}
```

- [ ] **Step 4: Run tests + `bun run build`; verify pass.**

- [ ] **Step 5: Commit** — `feat(player-shell-web): dcgate QR top-up screen with status polling`

---

### Task E6: `ExtendScreen` (tariffs + extend + 409 reconcile)

**Files:**
- Create: `src/AFK4.Player.Shell.Web/src/screens/ExtendScreen.tsx`
- Test: `src/AFK4.Player.Shell.Web/src/screens/ExtendScreen.test.tsx`

- [ ] **Step 1: Write the failing test** (fake `ShellApi`; pick tariff + minutes → calls `extendSession`; on a 409 `ApiError`, shows "refreshing" and calls an `onConflict` reload)

```tsx
import { render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'bun:test';
import { ExtendScreen } from './ExtendScreen';
import { ApiError, type ShellApi } from '../shellApi';

const tariff = { tariffId: 't', tariffVersionId: 'tv', name: 'Standard', tariffRuleVersionId: 'trv1',
  versionNumber: 1, currencyCode: 'TJS', pricePerMinuteMinorUnits: 100, minimumBillableMinutes: 1,
  roundingIncrementMinutes: 1, effectiveFromUtc: '' };

function api(extend: ShellApi['extendSession']): ShellApi {
  return { listTariffs: async () => [tariff], listPackages: async () => [], createTopUpIntent: async () => ({} as any),
    getTopUpIntents: async () => [], extendSession: extend } as ShellApi;
}

describe('ExtendScreen', () => {
  it('extends with the selected tariff and minutes', async () => {
    let captured: any;
    render(<ExtendScreen api={api(async (s, req) => { captured = { s, req }; return {}; })}
      branchId="b" sessionId="s1" onExtended={() => {}} onConflict={() => {}} />);
    const { fireEvent } = await import('@testing-library/react');
    await waitFor(() => screen.getByText('Standard'));
    fireEvent.click(screen.getByText('Standard'));
    fireEvent.change(screen.getByLabelText(/minutes|минут/i), { target: { value: '30' } });
    fireEvent.click(screen.getByRole('button', { name: /extend|продлить/i }));
    await waitFor(() => expect(captured.req).toMatchObject({ additionalMinutes: 30, tariffRuleVersionId: 'trv1' }));
  });

  it('on 409 calls onConflict', async () => {
    let conflicted = false;
    render(<ExtendScreen api={api(async () => { throw new ApiError(409, 'conflict'); })}
      branchId="b" sessionId="s1" onExtended={() => {}} onConflict={() => { conflicted = true; }} />);
    const { fireEvent } = await import('@testing-library/react');
    await waitFor(() => screen.getByText('Standard'));
    fireEvent.click(screen.getByText('Standard'));
    fireEvent.change(screen.getByLabelText(/minutes|минут/i), { target: { value: '30' } });
    fireEvent.click(screen.getByRole('button', { name: /extend|продлить/i }));
    await waitFor(() => expect(conflicted).toBe(true));
  });
});
```

- [ ] **Step 2: Run, verify fails.**

- [ ] **Step 3: Implement**

```tsx
import { useEffect, useState } from 'react';
import type { TariffOptionDto } from '../apiTypes';
import { ApiError, OfflineError, type ShellApi } from '../shellApi';

export interface ExtendScreenProps {
  api: ShellApi;
  branchId: string;
  sessionId: string;
  onExtended: () => void;
  onConflict: () => void;
}

export function ExtendScreen({ api, branchId, sessionId, onExtended, onConflict }: ExtendScreenProps) {
  const [tariffs, setTariffs] = useState<TariffOptionDto[]>([]);
  const [selected, setSelected] = useState<TariffOptionDto | null>(null);
  const [minutes, setMinutes] = useState(30);
  const [offline, setOffline] = useState(false);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    api.listTariffs(branchId).then(setTariffs).catch((e) => { if (e instanceof OfflineError) setOffline(true); });
  }, [api, branchId]);

  async function extend() {
    if (!selected) return;
    setBusy(true);
    try {
      await api.extendSession(sessionId, { additionalMinutes: minutes, tariffRuleVersionId: selected.tariffRuleVersionId });
      onExtended();
    } catch (e) {
      if (e instanceof ApiError && e.status === 409) onConflict();
      else if (e instanceof OfflineError) setOffline(true);
    } finally {
      setBusy(false);
    }
  }

  if (offline) return <p role="alert">Временно недоступно — обратитесь к оператору</p>;

  return (
    <section>
      <h1>Продлить время</h1>
      <ul>
        {tariffs.map((t) => (
          <li key={t.tariffVersionId}>
            <button onClick={() => setSelected(t)} aria-pressed={selected?.tariffVersionId === t.tariffVersionId}>
              {t.name}
            </button>
          </li>
        ))}
      </ul>
      <label htmlFor="minutes">Минут</label>
      <input id="minutes" type="number" min={1} value={minutes}
             onChange={(e) => setMinutes(Number(e.target.value))} />
      <button onClick={extend} disabled={!selected || busy}>Продлить</button>
    </section>
  );
}
```

- [ ] **Step 4: Run tests + `bun run build`; verify pass.** Wire `ExtendScreen`/`TopUpScreen` into the self-service menu reachable from `ActiveSessionScreen` (auth-gated). `sessionId` comes from `useShellBridge().state.sessionId`; `branchId` from `state.branchId`. On `onConflict`, re-trigger the bridge `shell:loadState` (the foundation hook already re-renders from pushes).

- [ ] **Step 5: Commit** — `feat(player-shell-web): self-service extend screen with 409 reconcile`

---

## UNIT G — Packaging + offline cache + retire WPF UI

### Task G1: Offline cache for tariffs (`idbCache.ts`)

**Files:**
- Create: `src/AFK4.Player.Shell.Web/src/idbCache.ts`
- Test: `src/AFK4.Player.Shell.Web/src/idbCache.test.ts`

Abstract the store behind an interface so the fallback logic is unit-tested with an in-memory fake; real IndexedDB binding is thin glue (Assumption 8).

- [ ] **Step 1: Write the failing test**

```ts
import { describe, expect, it } from 'bun:test';
import { createCachedLoader, type KeyValueStore } from './idbCache';

function memoryStore(): KeyValueStore {
  const m = new Map<string, unknown>();
  return { get: async (k) => m.get(k), set: async (k, v) => void m.set(k, v) };
}

describe('createCachedLoader', () => {
  it('returns fresh data and caches it', async () => {
    const store = memoryStore();
    const load = createCachedLoader(store, 'tariffs', async () => [{ name: 'A' }]);
    expect(await load()).toEqual([{ name: 'A' }]);
    expect(await store.get('tariffs')).toEqual([{ name: 'A' }]);
  });

  it('falls back to cache when the loader throws', async () => {
    const store = memoryStore();
    await store.set('tariffs', [{ name: 'cached' }]);
    const load = createCachedLoader(store, 'tariffs', async () => { throw new Error('offline'); });
    expect(await load()).toEqual([{ name: 'cached' }]);
  });

  it('rethrows when offline and nothing is cached', async () => {
    const load = createCachedLoader(memoryStore(), 'tariffs', async () => { throw new Error('offline'); });
    await expect(load()).rejects.toThrow('offline');
  });
});
```

- [ ] **Step 2: Run, verify fails.**

- [ ] **Step 3: Implement**

```ts
export interface KeyValueStore {
  get(key: string): Promise<unknown>;
  set(key: string, value: unknown): Promise<void>;
}

/** Try the network loader; on failure fall back to the last cached value; cache fresh successes. */
export function createCachedLoader<T>(store: KeyValueStore, key: string, loader: () => Promise<T>): () => Promise<T> {
  return async () => {
    try {
      const fresh = await loader();
      await store.set(key, fresh);
      return fresh;
    } catch (error) {
      const cached = await store.get(key);
      if (cached !== undefined) return cached as T;
      throw error;
    }
  };
}

/** Thin IndexedDB-backed KeyValueStore (real binding; verified on device, not in unit tests). */
export function indexedDbStore(dbName = 'afk4-player-shell', storeName = 'cache'): KeyValueStore {
  function open(): Promise<IDBDatabase> {
    return new Promise((resolve, reject) => {
      const req = indexedDB.open(dbName, 1);
      req.onupgradeneeded = () => req.result.createObjectStore(storeName);
      req.onsuccess = () => resolve(req.result);
      req.onerror = () => reject(req.error);
    });
  }
  async function tx<R>(mode: IDBTransactionMode, fn: (s: IDBObjectStore) => IDBRequest): Promise<R> {
    const db = await open();
    return new Promise<R>((resolve, reject) => {
      const request = fn(db.transaction(storeName, mode).objectStore(storeName));
      request.onsuccess = () => resolve(request.result as R);
      request.onerror = () => reject(request.error);
    });
  }
  return {
    get: (key) => tx('readonly', (s) => s.get(key)),
    set: async (key, value) => { await tx('readwrite', (s) => s.put(value, key)); }
  };
}
```

Wire `shellApi.listTariffs` through `createCachedLoader(indexedDbStore(), 'tariffs:'+branchId, …)` at the call site in `ExtendScreen` (so offline shows last tariffs instead of an error).

- [ ] **Step 4: Run tests + `bun run build`; verify pass.**

- [ ] **Step 5: Commit** — `feat(player-shell-web): offline cache loader for tariffs`

---

### Task G2: WebView2 runtime prerequisite

**Files:**
- Modify: `installers/player-shell/Package.wxs` and/or `scripts/build-client-packages.ps1`

The host requires the **WebView2 Evergreen Runtime**. Most managed Windows 10/11 already have it, but the installer must guarantee it.

- [ ] **Step 1:** Decide + document the approach (recommend: WiX bundle/bootstrapper chaining the Evergreen Standalone/Bootstrapper, or a launch-condition that detects the runtime and prompts). Add a short note at the top of `Package.wxs` describing the chosen mechanism.
- [ ] **Step 2:** Implement the prerequisite in the installer (chain the Evergreen bootstrapper, or add a `RegistrySearch` launch condition for `pv` under `…\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}`).
- [ ] **Step 3:** On the Windows bridge, build the installer per `scripts/build-client-packages.ps1` and confirm it produces an MSI without errors. (Functional runtime install verified on device.)
- [ ] **Step 4: Commit** — `build(player-shell): ensure WebView2 runtime prerequisite in installer`

---

### Task G3: Retire the legacy WPF UI

**Files:**
- Delete: `src/AFK4.Player.Shell/MainWindow.xaml`, `MainWindow.xaml.cs`, `Shell/PlayerShellViewModel.cs`, `Preview/PreviewPlayerShell.cs`, and (if unreferenced) `Mvvm/`.
- Modify: `src/AFK4.Player.Shell/App.xaml.cs` (remove the `#if DEBUG --preview` branch and the `localization`-only path that fed `MainWindow`).
- Modify/Delete: any `tests/AFK4.Player.Shell.Tests/**` that reference `MainWindow`/`PlayerShellViewModel`/`PreviewPlayerShell`.

> **Behavior change warning (Assumption per spec):** this removes the old full-screen WPF render and the `--preview` design-time mode. The WebView2 window is already the production path. `NamedPipePlayerShellStateClient`, `LauncherCommandClient`, and `RemainingTimeFormatter` are infra and **must stay**.

- [ ] **Step 1:** Grep the test project + `src/AFK4.Player.Shell` for `MainWindow`, `PlayerShellViewModel`, `PreviewPlayerShell`, `Mvvm`. List every reference.
- [ ] **Step 2:** Simplify `App.xaml.cs` to:

```csharp
protected override void OnStartup(StartupEventArgs e)
{
    var localization = LocalizationService.LoadEmbedded(Locales.Default);
    LocalizationScope.Current = localization;

    new AFK4.Player.Shell.Web.WebViewPlayerWindow().Show();
    base.OnStartup(e);
}
```

(Confirm `App.xaml` has no `StartupUri` pointing at `MainWindow`; remove it if present.)

- [ ] **Step 3:** Delete the legacy files and any tests that only exercised the retired ViewModel. Preserve tests covering pipe/launcher/formatter infra.
- [ ] **Step 4: Build + test on the Windows bridge** (clean obj/bin first per env-quirks): `dotnet.exe build src\AFK4.Player.Shell\…` → 0 errors; `dotnet.exe test tests\AFK4.Player.Shell.Tests\…` → all green (count drops by however many ViewModel tests were removed; note the new total).
- [ ] **Step 5: Commit** — `refactor(player-shell): retire legacy WPF MainWindow/ViewModel (WebView2 is the shell)`

---

### Task G4: Packaging verification (web dist → installer)

**Files:**
- Verify only: `src/AFK4.Player.Shell/AFK4.Player.Shell.csproj` (`CopyPlayerWebAssets` target — already present from foundation C5), `installers/player-shell/Package.wxs`, `scripts/build-client-packages.ps1`.

- [ ] **Step 1:** On the Windows bridge, run the full client build: `bun run build` (web, in WSL) → `cp -r dist` into the D: clone → `scripts/build-client-packages.ps1` for the player-shell channel. Confirm the published output contains `WebAssets\index.html` + hashed assets, and the resulting MSI includes them.
- [ ] **Step 2:** Smoke-launch the published `AFK4.Player.Shell.exe` on the gaming PC / Windows host: the WebView2 window renders the React app from `https://player.afk4.local/index.html`, the timer ticks from pipe state, and a launcher tile launches a game.
- [ ] **Step 3:** If `build-client-packages.ps1` needs an explicit web-build step (rather than relying on a pre-built `dist`), add it and document it. Otherwise leave as-is.
- [ ] **Step 4: Commit** (only if files changed) — `build(player-shell): verify/extend web-dist packaging into installer`

---

### Task G5: Final end-to-end verification on the Windows bridge

Not a code task — the closing gate for the plan (run after the per-task two-stage reviews).

- [ ] Web: `bun test` (all suites green) + `bun run build` (no type errors).
- [ ] Native: `dotnet.exe test tests\AFK4.Player.Shell.Tests\…` and `tests\AFK4.Platform.Api.Tests\…` (player-catalog tests) green on the D: clone.
- [ ] On-device manual checklist (carried over from foundation residual + this plan):
  - Login: enter phone+password → authenticated snapshot pushed; token never visible in any web context (DevTools are disabled in prod, verify on a dev build with the dev server).
  - `Authorization` header reaches `/api/me/*` (inspect via a staging request log).
  - Top-up: QR renders, scanning + paying on a real banking app moves the intent to `fulfilled` (webhook) and the screen shows success; let one expire → "expired".
  - Extend: pick a tariff + minutes → time increases; force a concurrent change → 409 → "refreshing".
  - Offline: pull the network → timer keeps running (pipe), tariffs render from cache, top-up shows "call operator".
  - Kiosk: full-screen, no F12 / context menu; kill `msedgewebview2` child → native fallback → auto-recover.

---

## Self-Review (writing-plans)

- **Spec coverage:** Unit D (login + native token transport) ✓; Unit E (tariffs endpoint + extend + dcgate QR top-up + status machine + 409) ✓; Unit G (packaging + offline cache + retire WPF) ✓. Unit F explicitly deferred with rationale (Assumption 9). Login guest/session-code deferred (Assumption 1). Package purchase-and-apply deferred (Assumption 6) — both flagged, not silently dropped.
- **Placeholder scan:** every code step shows real code. Two "verify exact name while implementing" notes (E1 service namespace / DbSet; D6 i18n hook) are verification instructions against the live repo, not invented APIs — the surrounding code is concrete.
- **Type consistency:** `IPlayerApiAuthClient`/`AuthSnapshot` used identically across D2/D3/D4; `AuthSnapshot` (TS) shape matches the C# `Snapshot()` payload (`authenticated`/`displayName`/`phoneVerified`); `ShellApi`/`PlayerTopUpIntentDto`/`TariffOptionDto`/`ExtendSessionRequest` consistent across E3/E4/E5/E6/G1; `toPaymentStatus` signature matches its callers; bridge `AllowedTypes` additions match the TS `postShellRequest` types.
