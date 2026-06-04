# Customer Shell — Unit 4: WPF self-service UI (`AFK4.Player.Shell`)

- **Date:** 2026-06-03
- **Status:** Plan — ready to execute (Windows-gated)
- **Spec:** `docs/superpowers/specs/2026-06-03-customer-shell-implementation-design.md` (§5 "Unit 4 — WPF shell")
- **Scope:** Unit 4 ONLY — the member self-service UI inside the kiosk app `AFK4.Player.Shell`.
  Backend (Units 1–2), operator web (Unit 3), and OTP self-registration (Unit 5) are out of scope here.

## Goal

Turn the lock-only kiosk shell into a member self-service surface: sign in with phone + PIN (or QR),
see wallet balance / time / debt, self-start and self-extend a session (optimistic UI that only
commits on the backend-pushed state), top up via a dcgate QR with an operator-confirm fallback, and
theme the screen from the branch branding — all localized through the existing catalog, money through
`FormatCurrency`, time through `RemainingTimeFormatter`. The backend remains the only authority
(trust boundary §7): the shell shows requests, the backend pushes truth over the named pipe.

## Architecture

- **Today:** `AFK4.Player.Shell` is `MainWindow.xaml` + `PlayerShellViewModel` only. State arrives over a
  named pipe (`NamedPipePlayerShellStateClient` → `PlayerShellStateDto`); the only outbound IPC is
  "launch-app" (`LauncherCommandClient`). There is **no HTTP client**.
- **This unit adds** an HTTP layer that talks to `/api/me/*` (and `/api/public/player/sign-in`) under a
  Bearer player token held **in memory only**, plus a set of small view-models (Login, MemberHome, TopUp)
  composed under the existing `PlayerShellViewModel`, wired into `MainWindow.xaml`.
- **Trust boundary (spec §7 / decision D8):** self-start and self-extend are *requests*. The view-model
  shows an optimistic "starting…/extending…" state but flips `IsSessionActive` to true **only** when the
  next `ApplyState(PlayerShellStateDto)` from the pipe reports `Active`/grace/ending. If the next state is
  still `Locked`, the optimistic flag reverts. The token is dropped on lock/idle/sign-out.
- **Patterns to mirror (already in this repo):**
  - HTTP client idiom: `src/AFK4.Operator.App/Players/HttpOperatorPlayerApiClient.cs` (typed methods,
    `JsonContent.Create`, `Bearer` from a token store, `SendAndReadAsync` with status-code error wrapping).
  - Client test idiom: `tests/AFK4.Operator.App.Tests/OperatorPlayerApiClientTests.cs` — a
    `RecordingHttpMessageHandler` capturing `LastMethod` / `LastPathAndQuery` / `LastAuthorization` /
    `LastRequestBody`, asserting request shape + Bearer header (no live server). **Confirmed testable in a
    `net10.0-windows` test project** — `OperatorPlayerApiClientTests` already runs exactly this way.
  - VM/test idiom: `PlayerShellViewModel` + `tests/AFK4.Player.Shell.Tests/PlayerShellViewModelTests.cs`
    (`INotifyPropertyChanged`, `RelayCommand`, `ILocalizationService` injected, `LocalizationService.LoadEmbedded`).
- **Money/time/locale:** money is `long` minor units end-to-end; convert to display only via
  `ILocalizationService.FormatCurrency(minor, currencyCode)`. Time via `RemainingTimeFormatter.Format(int?)`.
  Every new string is a catalog key in `locales/ru.json` / `en.json` / `tg.json` (tg mirrors ru as a STOPGAP).

## Tech Stack

- `net10.0-windows`, WPF (`UseWPF`), C# (nullable + implicit usings on).
- xUnit (`xunit` 2.9.3) test project `AFK4.Player.Shell.Tests` (already `net10.0-windows`).
- `System.Net.Http.Json` for typed JSON requests (BCL, no extra package).
- Existing references: `AFK4.Shared.Contracts`, `AFK4.Localization`, `AFK4.Localization.Wpf`.

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` to execute
> this plan task-by-task. For every task: write the failing test first, run it and confirm it FAILS for
> the right reason, write the minimal implementation, run it and confirm it PASSES, then commit with the
> exact message given. Do not batch tasks. Do not write XAML/visual polish by hand — when a task touches
> HUD/login/grace layout, exact colors, type scale, or motion, invoke the `interface-limb` skill at that
> step; this plan covers structure, bindings, and VM logic, not pixel design.

---

## CRITICAL environment gate (read before any build/test)

**WPF (`net10.0-windows`) builds and tests ONLY on Windows.** This repo's primary checkout is on WSL,
where the WPF app does **not** run. The execution of this plan happens on the **native Windows clone at
`D:\projects\afk4.net`** using the Windows dotnet:

```
"/mnt/c/Program Files/dotnet/dotnet.exe" test "D:\projects\afk4.net\tests\AFK4.Player.Shell.Tests\AFK4.Player.Shell.Tests.csproj"
```

Before a Windows build of the shell, **clean cross-OS `obj`/`bin`** (Linux-built intermediates poison the
Windows build):

```
find src -type d \( -name obj -o -name bin \) -prune -exec rm -rf {} +
```

Every "run the test" step below uses the Windows `dotnet.exe test` command above. The expected-FAIL step
is a **compile failure** (type/member does not exist yet) until the impl step adds it; that counts as the
red phase. View-models and the HTTP client are **pure .NET logic, unit-testable without a display** — the
existing `AFK4.Player.Shell.Tests` proves this. XAML views are **not** unit-tested: their "test" is a
successful build of `AFK4.Player.Shell` plus a manual acceptance note.

---

## Dependencies (honest cross-unit notes — read before starting)

This unit consumes work from Units 1–2. Where a dependency is not yet on `main`, the plan says so and gives
a safe path so Unit 4 is not blocked on a missing endpoint.

1. **Unit 1 — `PlayerShellStateDto` extensions (`WarningKind`, configurable `WarningThresholdSeconds`,
   nullable `Branding`).** Verified TODAY: `src/AFK4.Shared.Contracts/Shell/PlayerShellStateDto.cs` does
   **not** yet have `WarningKind` or `Branding` (it has `WarningThresholdSeconds` already). Tasks 4, 7, 8
   assume Unit 1 added:
   - `string WarningKind` (`none|low_time|low_balance|credit_limit|connectivity`),
   - a nullable `BrandingDto Branding` (`Name`, `LogoUrl?`, `AccentColor?`).
   **If Unit 1 has not landed when Task 4 starts:** add these two fields to `PlayerShellStateDto` as part of
   Task 4's impl (and the `Branding` record under `AFK4.Shared.Contracts/Shell/`), default `WarningKind="none"`
   and `Branding=null`, and update the contracts round-trip test in
   `tests/AFK4.Shared.Contracts.Tests` (or wherever `PlayerShellContractSerialization` lives). Prefer landing
   Unit 1 first; this fallback exists only so Unit 4 is not hard-blocked.

2. **Unit 1 — `POST /api/me/sessions/start` and `POST /api/me/sessions/{id}/extend`.** Verified TODAY: these
   endpoints do **not** exist yet (Program.cs has `/api/branches/{branchId}/sessions/start` and
   `/api/sessions/{sessionId}/extend` for **operators**, not the player-token `/api/me/*` routes). The shell
   client in Task 1 targets the `/api/me/sessions/*` routes from the spec; the **client unit tests use a stub
   handler and never hit a live server**, so they pass regardless. End-to-end self-start/extend only works
   once Unit 1 ships those routes — note this in the verification gate, do not block.

3. **Unit 2 — dcgate `payUrl`/`comment` on the top-up intent.** Verified TODAY:
   `src/AFK4.Shared.Contracts/Players/PlayerTopUpIntentDto.cs` has **no `PayUrl`/`Comment`** fields, and
   `PlayerTopUpIntentRequest` is `(long AmountMinorUnits, string? CurrencyCode)`. Task 6 needs a `PayUrl`
   (and optionally `Comment`) on the create response. **Approach:** Task 6 introduces a small shell-local
   response type `TopUpIntentResult(PaymentIntentId, AmountMinorUnits, CurrencyCode, State, Method, PayUrl?, Comment?)`
   that the client maps from whatever the endpoint returns; the VM only depends on `PayUrl?` being present
   when `Method=="dcgate"`. When Unit 2 adds `PayUrl`/`Comment` to `PlayerTopUpIntentDto`, the client maps
   directly. The VM and its tests are unaffected either way.

4. **QR login backend endpoint.** Verified TODAY: there is **no `/api/public/player/sign-in-qr`** (only
   `/api/public/player/sign-in`). Decision E ("QR login in scope") is a backend dependency that is **not
   yet built**. Task 3 therefore ships QR login as a **wired stub**: a "Scan QR" affordance whose command,
   when the backend endpoint is absent, surfaces a localized "coming soon" message and otherwise routes the
   scanned token through the **same `SignInAsync` token flow** (QR payload → orgId/phone/pin or a token).
   Mark this sub-feature **dependent/optional**; the PIN path is the primary, always-working login.

5. **`/api/public/player/sign-in` body field name.** Verified TODAY: `PlayerSignInRequest` is
   `(Guid OrganizationId, string PhoneNumber, string Password)` — the PIN goes in `Password`. The client must
   send `Password`, not `pin`. Tests assert this exact shape.

---

## Task 1 — `IPlayerSelfApiClient` + `HttpPlayerSelfApiClient` (Bearer player token, in memory)

Adds the HTTP layer the shell has never had. Mirrors `HttpOperatorPlayerApiClient`. The token comes from an
in-memory `IPlayerSessionTokenHolder` (Task 2) — but to keep Task 1 self-contained and testable now, the
client takes the holder via constructor and Task 2 only adds the production holder; tests use a tiny static
holder exactly like `StaticOperatorTokenStore` in the operator tests.

**Files:**
- Create `src/AFK4.Player.Shell/Self/IPlayerSelfApiClient.cs`
- Create `src/AFK4.Player.Shell/Self/HttpPlayerSelfApiClient.cs`
- Create `src/AFK4.Player.Shell/Self/IPlayerSessionTokenHolder.cs` (interface only here; impl in Task 2)
- Create `src/AFK4.Player.Shell/Self/TopUpIntentResult.cs` (shell-local result for Dependency #3)
- Test `tests/AFK4.Player.Shell.Tests/PlayerSelfApiClientTests.cs`
- Modify `src/AFK4.Player.Shell/Configuration/PlayerShellOptions.cs` (add `ApiBaseUrl`)

### Steps

1. **Failing test** — create `tests/AFK4.Player.Shell.Tests/PlayerSelfApiClientTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AFK4.Player.Shell.Self;
using AFK4.Shared.Contracts.Players;

namespace AFK4.Player.Shell.Tests;

public sealed class PlayerSelfApiClientTests
{
    private static readonly Guid OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid PlayerAccountId = Guid.Parse("65b9b565-eb5c-4ff5-890c-85f3e12a0fc2");
    private static readonly Guid SessionId = Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task SignInAsync_PostsPhoneAndPinAsPasswordToPublicEndpoint()
    {
        var response = new PlayerSignInResponse(
            PlayerAccountId, OrganizationId, "Alex Player", PhoneVerified: false,
            AccessToken: "player-access-token",
            AccessTokenExpiresAtUtc: DateTimeOffset.Parse("2026-06-03T11:00:00Z"),
            RefreshToken: "player-refresh-token",
            RefreshTokenExpiresAtUtc: DateTimeOffset.Parse("2026-06-04T11:00:00Z"));
        var handler = new RecordingHttpMessageHandler(_ => JsonResponse(response));
        var client = CreateClient(handler, token: null);

        var result = await client.SignInAsync(OrganizationId, "+992000000001", "1234", CancellationToken.None);

        Assert.Equal("player-access-token", result.AccessToken);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal("/api/public/player/sign-in", handler.LastPathAndQuery);
        Assert.Null(handler.LastAuthorization); // sign-in is anonymous
        var body = Deserialize<PlayerSignInRequest>(handler.LastRequestBody);
        Assert.Equal(OrganizationId, body.OrganizationId);
        Assert.Equal("+992000000001", body.PhoneNumber);
        Assert.Equal("1234", body.Password); // PIN travels in Password (Dependency #5)
    }

    [Fact]
    public async Task GetDashboardAsync_GetsMeDashboardWithBearer()
    {
        var dashboard = new PlayerDashboardDto(
            new MoneyDto("TJS", 12000), new MoneyDto("TJS", 0), ActiveSession: null);
        var handler = new RecordingHttpMessageHandler(_ => JsonResponse(dashboard));
        var client = CreateClient(handler, token: "player-access-token");

        var result = await client.GetDashboardAsync(CancellationToken.None);

        Assert.Equal(12000, result.WalletBalance.MinorUnits);
        Assert.Equal(HttpMethod.Get, handler.LastMethod);
        Assert.Equal("/api/me/dashboard", handler.LastPathAndQuery);
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "player-access-token"), handler.LastAuthorization);
    }

    [Fact]
    public async Task StartSessionAsync_PostsSeatAndIdempotencyKey()
    {
        var dashboard = new PlayerDashboardDto(
            new MoneyDto("TJS", 11000), new MoneyDto("TJS", 0),
            new ActiveSessionDto(SessionId, Guid.Parse("11111111-1111-4111-8111-111111111111"),
                "Seat 7", DateTimeOffset.Parse("2026-06-03T10:00:00Z"), "open", null, 0, "TJS"));
        var handler = new RecordingHttpMessageHandler(_ => JsonResponse(dashboard));
        var client = CreateClient(handler, token: "player-access-token");

        var seatId = Guid.Parse("11111111-1111-4111-8111-111111111111");
        await client.StartSessionAsync(seatId, tariffRuleVersionId: null, "start-key-1", CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal("/api/me/sessions/start", handler.LastPathAndQuery);
        var body = JsonSerializer.Deserialize<JsonElement>(handler.LastRequestBody!);
        Assert.Equal(seatId, body.GetProperty("seatId").GetGuid());
        Assert.Equal("start-key-1", body.GetProperty("idempotencyKey").GetString());
    }

    [Fact]
    public async Task ExtendSessionAsync_PostsMinutesAndIdempotencyKey()
    {
        var dashboard = new PlayerDashboardDto(
            new MoneyDto("TJS", 9000), new MoneyDto("TJS", 0), ActiveSession: null);
        var handler = new RecordingHttpMessageHandler(_ => JsonResponse(dashboard));
        var client = CreateClient(handler, token: "player-access-token");

        await client.ExtendSessionAsync(SessionId, minutes: 30, "extend-key-1", CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal($"/api/me/sessions/{SessionId:D}/extend", handler.LastPathAndQuery);
        var body = JsonSerializer.Deserialize<JsonElement>(handler.LastRequestBody!);
        Assert.Equal(30, body.GetProperty("minutes").GetInt32());
        Assert.Equal("extend-key-1", body.GetProperty("idempotencyKey").GetString());
    }

    [Fact]
    public async Task CreateTopUpIntentAsync_PostsAmountAndMapsPayUrl()
    {
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                paymentIntentId = Guid.Parse("22222222-2222-4222-8222-222222222222"),
                amountMinorUnits = 5000L,
                currencyCode = "TJS",
                state = "pending",
                method = "dcgate",
                payUrl = "http://pay.dc.tj/?A=card&s=50&c=abc",
                comment = "abc123"
            })
        });
        var client = CreateClient(handler, token: "player-access-token");

        var result = await client.CreateTopUpIntentAsync(amountMinorUnits: 5000, "TJS", CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal("/api/me/wallet/top-up-intent", handler.LastPathAndQuery);
        Assert.Equal("dcgate", result.Method);
        Assert.Equal("http://pay.dc.tj/?A=card&s=50&c=abc", result.PayUrl);
    }

    [Fact]
    public async Task GetDashboardAsync_WhenNoToken_Throws()
    {
        var handler = new RecordingHttpMessageHandler(_ => JsonResponse(
            new PlayerDashboardDto(new MoneyDto("TJS", 0), new MoneyDto("TJS", 0), null)));
        var client = CreateClient(handler, token: null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetDashboardAsync(CancellationToken.None));
    }

    private static HttpPlayerSelfApiClient CreateClient(RecordingHttpMessageHandler handler, string? token)
    {
        return new HttpPlayerSelfApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5074") },
            new StaticTokenHolder(token));
    }

    private static T Deserialize<T>(string? json)
    {
        Assert.False(string.IsNullOrWhiteSpace(json));
        var result = JsonSerializer.Deserialize<T>(json!, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(result);
        return result!;
    }

    private static HttpResponseMessage JsonResponse<T>(T body) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };

    private sealed class RecordingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        public HttpMethod? LastMethod { get; private set; }
        public string? LastPathAndQuery { get; private set; }
        public string? LastRequestBody { get; private set; }
        public AuthenticationHeaderValue? LastAuthorization { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastMethod = request.Method;
            LastPathAndQuery = request.RequestUri?.PathAndQuery;
            LastAuthorization = request.Headers.Authorization;
            LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return responder(request);
        }
    }

    private sealed class StaticTokenHolder(string? token) : IPlayerSessionTokenHolder
    {
        public string? AccessToken => token;
        public bool IsSignedIn => token is not null;
        public void Set(string accessToken, Guid playerAccountId, string displayName) { }
        public void Clear() { }
    }
}
```

2. **Run (expect FAIL — `IPlayerSelfApiClient`/`HttpPlayerSelfApiClient`/`IPlayerSessionTokenHolder` do not exist; compile error):**

```
find src -type d \( -name obj -o -name bin \) -prune -exec rm -rf {} +
"/mnt/c/Program Files/dotnet/dotnet.exe" test "D:\projects\afk4.net\tests\AFK4.Player.Shell.Tests\AFK4.Player.Shell.Tests.csproj"
```

3. **Minimal impl:**

`src/AFK4.Player.Shell/Self/IPlayerSessionTokenHolder.cs`:
```csharp
namespace AFK4.Player.Shell.Self;

/// <summary>In-memory holder for the player Bearer token; never persisted, cleared on lock/idle/sign-out.</summary>
public interface IPlayerSessionTokenHolder
{
    string? AccessToken { get; }
    bool IsSignedIn { get; }
    void Set(string accessToken, Guid playerAccountId, string displayName);
    void Clear();
}
```

`src/AFK4.Player.Shell/Self/TopUpIntentResult.cs`:
```csharp
namespace AFK4.Player.Shell.Self;

/// <summary>
/// Shell-local result of a top-up intent. Decouples the VM from the backend DTO so we work
/// before Unit 2 adds PayUrl/Comment to PlayerTopUpIntentDto (see plan Dependency #3).
/// </summary>
public sealed record TopUpIntentResult(
    Guid PaymentIntentId,
    long AmountMinorUnits,
    string CurrencyCode,
    string State,
    string Method,
    string? PayUrl,
    string? Comment);
```

`src/AFK4.Player.Shell/Self/IPlayerSelfApiClient.cs`:
```csharp
using AFK4.Shared.Contracts.Players;

namespace AFK4.Player.Shell.Self;

public interface IPlayerSelfApiClient
{
    Task<PlayerSignInResponse> SignInAsync(Guid orgId, string phone, string pin, CancellationToken cancellationToken);
    Task<PlayerDashboardDto> GetDashboardAsync(CancellationToken cancellationToken);
    Task<PlayerDashboardDto> StartSessionAsync(Guid seatId, Guid? tariffRuleVersionId, string idempotencyKey, CancellationToken cancellationToken);
    Task<PlayerDashboardDto> ExtendSessionAsync(Guid sessionId, int minutes, string idempotencyKey, CancellationToken cancellationToken);
    Task<TopUpIntentResult> CreateTopUpIntentAsync(long amountMinorUnits, string? currencyCode, CancellationToken cancellationToken);
}
```

`src/AFK4.Player.Shell/Self/HttpPlayerSelfApiClient.cs` (mirror `HttpOperatorPlayerApiClient`; sign-in is
anonymous, every `/api/me/*` call attaches the in-memory Bearer; map the top-up response to `TopUpIntentResult`):
```csharp
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AFK4.Shared.Contracts.Players;

namespace AFK4.Player.Shell.Self;

public sealed class HttpPlayerSelfApiClient(HttpClient httpClient, IPlayerSessionTokenHolder tokenHolder)
    : IPlayerSelfApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<PlayerSignInResponse> SignInAsync(Guid orgId, string phone, string pin, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/public/player/sign-in")
        {
            Content = JsonContent.Create(new PlayerSignInRequest(orgId, phone, pin), options: JsonOptions)
        };
        return await SendAndReadAsync<PlayerSignInResponse>(request, ct);
    }

    public Task<PlayerDashboardDto> GetDashboardAsync(CancellationToken ct) =>
        AuthorizedAsync<PlayerDashboardDto>(HttpMethod.Get, "/api/me/dashboard", body: (object?)null, ct);

    public Task<PlayerDashboardDto> StartSessionAsync(Guid seatId, Guid? tariffRuleVersionId, string idempotencyKey, CancellationToken ct) =>
        AuthorizedAsync<PlayerDashboardDto>(HttpMethod.Post, "/api/me/sessions/start",
            new { seatId, tariffRuleVersionId, idempotencyKey }, ct);

    public Task<PlayerDashboardDto> ExtendSessionAsync(Guid sessionId, int minutes, string idempotencyKey, CancellationToken ct) =>
        AuthorizedAsync<PlayerDashboardDto>(HttpMethod.Post, $"/api/me/sessions/{sessionId:D}/extend",
            new { minutes, idempotencyKey }, ct);

    public async Task<TopUpIntentResult> CreateTopUpIntentAsync(long amountMinorUnits, string? currencyCode, CancellationToken ct)
    {
        var raw = await AuthorizedAsync<TopUpIntentRaw>(HttpMethod.Post, "/api/me/wallet/top-up-intent",
            new PlayerTopUpIntentRequest(amountMinorUnits, currencyCode), ct);
        return new TopUpIntentResult(
            raw.PaymentIntentId, raw.AmountMinorUnits, raw.CurrencyCode, raw.State, raw.Method, raw.PayUrl, raw.Comment);
    }

    private async Task<TResponse> AuthorizedAsync<TResponse>(
        HttpMethod method, string uri, object? body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tokenHolder.AccessToken))
        {
            throw new InvalidOperationException("Player access token is missing.");
        }

        using var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenHolder.AccessToken);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType(), options: JsonOptions);
        }

        return await SendAndReadAsync<TResponse>(request, ct);
    }

    private async Task<TResponse> SendAndReadAsync<TResponse>(HttpRequestMessage request, CancellationToken ct)
    {
        using var response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"Player API returned {(int)response.StatusCode} {response.ReasonPhrase}: {errorBody}",
                inner: null, response.StatusCode);
        }

        var payload = await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, ct);
        return payload ?? throw new InvalidOperationException("Player API returned an empty response.");
    }

    // Tolerant of Unit-2 PayUrl/Comment being present or absent on the wire.
    private sealed record TopUpIntentRaw(
        Guid PaymentIntentId, long AmountMinorUnits, string CurrencyCode,
        string State, string Method, string? PayUrl, string? Comment);
}
```

Add `ApiBaseUrl` to `PlayerShellOptions`:
```csharp
public string ApiBaseUrl { get; init; } = "http://localhost:5074";
```

4. **Run (expect PASS):**

```
"/mnt/c/Program Files/dotnet/dotnet.exe" test "D:\projects\afk4.net\tests\AFK4.Player.Shell.Tests\AFK4.Player.Shell.Tests.csproj"
```

5. **Commit:** `feat(player-shell): add player self-service HTTP client (sign-in, dashboard, start/extend, top-up intent)`

---

## Task 2 — In-memory player-session token holder

The production `IPlayerSessionTokenHolder`: holds the Bearer token + identity in memory only, cleared on
lock/idle/sign-out. No persistence (unlike the operator's `ProtectedDataOperatorTokenStore` — by spec D8/§7
the player token is **never** written to disk).

**Files:**
- Create `src/AFK4.Player.Shell/Self/InMemoryPlayerSessionTokenHolder.cs`
- Test `tests/AFK4.Player.Shell.Tests/PlayerSessionTokenHolderTests.cs`

### Steps

1. **Failing test** — `tests/AFK4.Player.Shell.Tests/PlayerSessionTokenHolderTests.cs`:

```csharp
using AFK4.Player.Shell.Self;

namespace AFK4.Player.Shell.Tests;

public sealed class PlayerSessionTokenHolderTests
{
    [Fact]
    public void NewHolder_IsAnonymous()
    {
        var holder = new InMemoryPlayerSessionTokenHolder();

        Assert.False(holder.IsSignedIn);
        Assert.Null(holder.AccessToken);
    }

    [Fact]
    public void Set_ExposesTokenAndIdentity()
    {
        var holder = new InMemoryPlayerSessionTokenHolder();

        holder.Set("player-access-token", Guid.Parse("65b9b565-eb5c-4ff5-890c-85f3e12a0fc2"), "Alex Player");

        Assert.True(holder.IsSignedIn);
        Assert.Equal("player-access-token", holder.AccessToken);
        Assert.Equal("Alex Player", holder.DisplayName);
    }

    [Fact]
    public void Clear_DropsTokenAndIdentity()
    {
        var holder = new InMemoryPlayerSessionTokenHolder();
        holder.Set("player-access-token", Guid.NewGuid(), "Alex Player");

        holder.Clear();

        Assert.False(holder.IsSignedIn);
        Assert.Null(holder.AccessToken);
        Assert.Null(holder.DisplayName);
    }
}
```

2. **Run (expect FAIL — `InMemoryPlayerSessionTokenHolder` / `DisplayName` do not exist):**

```
"/mnt/c/Program Files/dotnet/dotnet.exe" test "D:\projects\afk4.net\tests\AFK4.Player.Shell.Tests\AFK4.Player.Shell.Tests.csproj"
```

3. **Minimal impl** — add `string? DisplayName { get; }` to `IPlayerSessionTokenHolder`, then:

```csharp
namespace AFK4.Player.Shell.Self;

public sealed class InMemoryPlayerSessionTokenHolder : IPlayerSessionTokenHolder
{
    public string? AccessToken { get; private set; }
    public Guid? PlayerAccountId { get; private set; }
    public string? DisplayName { get; private set; }
    public bool IsSignedIn => AccessToken is not null;

    public void Set(string accessToken, Guid playerAccountId, string displayName)
    {
        AccessToken = accessToken;
        PlayerAccountId = playerAccountId;
        DisplayName = displayName;
    }

    public void Clear()
    {
        AccessToken = null;
        PlayerAccountId = null;
        DisplayName = null;
    }
}
```
(Add `string? DisplayName { get; }` to the interface from Task 1; update the test's `StaticTokenHolder` in
`PlayerSelfApiClientTests` to add `public string? DisplayName => null;`.)

4. **Run (expect PASS):**

```
"/mnt/c/Program Files/dotnet/dotnet.exe" test "D:\projects\afk4.net\tests\AFK4.Player.Shell.Tests\AFK4.Player.Shell.Tests.csproj"
```

5. **Commit:** `feat(player-shell): add in-memory player-session token holder (no persistence per trust boundary)`

---

## Task 3 — `LoginViewModel` (phone + PIN, errors, lockout, QR stub)

Phone + PIN entry → `SignInAsync` → on success store token in the holder and raise a "signed-in" signal;
on failure show a localized error; on repeated failure a localized lockout message. QR-login is a wired
stub (Dependency #4). New catalog keys added to all three locale files.

**Files:**
- Create `src/AFK4.Player.Shell/Self/LoginViewModel.cs`
- Test `tests/AFK4.Player.Shell.Tests/LoginViewModelTests.cs`
- Modify `locales/ru.json`, `locales/en.json`, `locales/tg.json`

### Steps

1. **Add catalog keys** (insert next to the existing `shell.*` block, keep keys grouped). In each of
   `ru.json` / `en.json` / `tg.json` add:
   - `shell.login.title`, `shell.login.phone`, `shell.login.pin`, `shell.login.submit`,
     `shell.login.scanQr`, `shell.login.qrUnavailable`, `shell.login.error.invalid`,
     `shell.login.error.lockout`, `shell.login.signingIn`.

   `en.json` values (examples): `"shell.login.title": "Sign in to play"`,
   `"shell.login.phone": "Phone number"`, `"shell.login.pin": "PIN"`, `"shell.login.submit": "Sign in"`,
   `"shell.login.scanQr": "Scan QR"`, `"shell.login.qrUnavailable": "QR sign-in is coming soon."`,
   `"shell.login.error.invalid": "Wrong phone or PIN."`,
   `"shell.login.error.lockout": "Too many attempts. Ask the operator for help."`,
   `"shell.login.signingIn": "Signing in…"`.
   `ru.json` values: `"Войдите, чтобы играть"`, `"Номер телефона"`, `"PIN"`, `"Войти"`, `"Сканировать QR"`,
   `"QR-вход скоро будет доступен."`, `"Неверный телефон или PIN."`,
   `"Слишком много попыток. Обратитесь к оператору."`, `"Вход…"`. **tg mirrors ru (STOPGAP).**

2. **Failing test** — `tests/AFK4.Player.Shell.Tests/LoginViewModelTests.cs`:

```csharp
using AFK4.Localization;
using AFK4.Player.Shell.Self;
using AFK4.Shared.Contracts.Players;

namespace AFK4.Player.Shell.Tests;

public sealed class LoginViewModelTests
{
    private static readonly Guid OrgId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");

    private static LoginViewModel Build(IPlayerSelfApiClient client, IPlayerSessionTokenHolder holder) =>
        new(client, holder, LocalizationService.LoadEmbedded(Locales.En), OrgId);

    [Fact]
    public async Task SignIn_OnSuccess_StoresTokenAndSignalsSignedIn()
    {
        var holder = new InMemoryPlayerSessionTokenHolder();
        var client = new FakeSelfApiClient
        {
            SignInResult = new PlayerSignInResponse(
                Guid.NewGuid(), OrgId, "Alex Player", false, "player-access-token",
                DateTimeOffset.UtcNow.AddHours(1), "refresh", DateTimeOffset.UtcNow.AddDays(1))
        };
        var vm = Build(client, holder);
        var signedIn = false;
        vm.SignedIn += (_, _) => signedIn = true;
        vm.Phone = "+992000000001";
        vm.Pin = "1234";

        await vm.SignInAsync(CancellationToken.None);

        Assert.True(holder.IsSignedIn);
        Assert.Equal("player-access-token", holder.AccessToken);
        Assert.True(signedIn);
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task SignIn_OnApiFailure_ShowsLocalizedInvalidError()
    {
        var client = new FakeSelfApiClient { SignInThrows = true };
        var vm = Build(client, new InMemoryPlayerSessionTokenHolder());
        vm.Phone = "+992000000001";
        vm.Pin = "0000";

        await vm.SignInAsync(CancellationToken.None);

        Assert.True(vm.HasError);
        Assert.Equal("Wrong phone or PIN.", vm.ErrorMessage);
    }

    [Fact]
    public async Task SignIn_AfterThreeFailures_ShowsLockoutMessage()
    {
        var client = new FakeSelfApiClient { SignInThrows = true };
        var vm = Build(client, new InMemoryPlayerSessionTokenHolder());
        vm.Phone = "+992000000001";
        vm.Pin = "0000";

        await vm.SignInAsync(CancellationToken.None);
        await vm.SignInAsync(CancellationToken.None);
        await vm.SignInAsync(CancellationToken.None);

        Assert.Equal("Too many attempts. Ask the operator for help.", vm.ErrorMessage);
    }

    [Fact]
    public void ScanQr_WhenBackendAbsent_ShowsComingSoon()
    {
        var vm = Build(new FakeSelfApiClient(), new InMemoryPlayerSessionTokenHolder());

        vm.ScanQrCommand.Execute(null);

        Assert.True(vm.HasError);
        Assert.Equal("QR sign-in is coming soon.", vm.ErrorMessage);
    }

    private sealed class FakeSelfApiClient : IPlayerSelfApiClient
    {
        public PlayerSignInResponse? SignInResult { get; set; }
        public bool SignInThrows { get; set; }

        public Task<PlayerSignInResponse> SignInAsync(Guid orgId, string phone, string pin, CancellationToken ct)
        {
            if (SignInThrows)
            {
                throw new HttpRequestException("401");
            }

            return Task.FromResult(SignInResult!);
        }

        public Task<PlayerDashboardDto> GetDashboardAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<PlayerDashboardDto> StartSessionAsync(Guid s, Guid? t, string k, CancellationToken ct) => throw new NotSupportedException();
        public Task<PlayerDashboardDto> ExtendSessionAsync(Guid s, int m, string k, CancellationToken ct) => throw new NotSupportedException();
        public Task<TopUpIntentResult> CreateTopUpIntentAsync(long a, string? c, CancellationToken ct) => throw new NotSupportedException();
    }
}
```

3. **Run (expect FAIL — `LoginViewModel` does not exist):**

```
"/mnt/c/Program Files/dotnet/dotnet.exe" test "D:\projects\afk4.net\tests\AFK4.Player.Shell.Tests\AFK4.Player.Shell.Tests.csproj"
```

4. **Minimal impl** — `src/AFK4.Player.Shell/Self/LoginViewModel.cs` (mirror `PlayerShellViewModel` style:
   `INotifyPropertyChanged` + `SetField`, `RelayCommand`):

```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AFK4.Localization;
using AFK4.Player.Shell.Mvvm;

namespace AFK4.Player.Shell.Self;

public sealed class LoginViewModel : INotifyPropertyChanged
{
    private const int LockoutThreshold = 3;

    private readonly IPlayerSelfApiClient apiClient;
    private readonly IPlayerSessionTokenHolder tokenHolder;
    private readonly ILocalizationService localization;
    private readonly Guid organizationId;

    private string phone = string.Empty;
    private string pin = string.Empty;
    private string? errorMessage;
    private bool isSigningIn;
    private int failedAttempts;

    public LoginViewModel(
        IPlayerSelfApiClient apiClient,
        IPlayerSessionTokenHolder tokenHolder,
        ILocalizationService localization,
        Guid organizationId)
    {
        this.apiClient = apiClient;
        this.tokenHolder = tokenHolder;
        this.localization = localization;
        this.organizationId = organizationId;
        SignInCommand = new RelayCommand(
            _ => _ = SignInAsync(CancellationToken.None),
            _ => !IsSigningIn && Phone.Length > 0 && Pin.Length > 0);
        ScanQrCommand = new RelayCommand(_ => ShowQrUnavailable());
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? SignedIn;

    public string Phone { get => phone; set { if (SetField(ref phone, value)) Raise(); } }
    public string Pin { get => pin; set { if (SetField(ref pin, value)) Raise(); } }
    public string? ErrorMessage { get => errorMessage; private set => SetField(ref errorMessage, value); }
    public bool HasError => ErrorMessage is not null;
    public bool IsSigningIn { get => isSigningIn; private set { if (SetField(ref isSigningIn, value)) Raise(); } }

    public ICommand SignInCommand { get; }
    public ICommand ScanQrCommand { get; }

    public async Task SignInAsync(CancellationToken cancellationToken)
    {
        if (IsSigningIn)
        {
            return;
        }

        IsSigningIn = true;
        SetError(null);
        try
        {
            var response = await apiClient.SignInAsync(organizationId, Phone, Pin, cancellationToken);
            tokenHolder.Set(response.AccessToken, response.PlayerAccountId, response.DisplayName);
            failedAttempts = 0;
            SignedIn?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception)
        {
            failedAttempts++;
            SetError(localization.T(failedAttempts >= LockoutThreshold
                ? "shell.login.error.lockout"
                : "shell.login.error.invalid"));
        }
        finally
        {
            IsSigningIn = false;
        }
    }

    public void Reset()
    {
        Phone = string.Empty;
        Pin = string.Empty;
        failedAttempts = 0;
        SetError(null);
    }

    private void ShowQrUnavailable() => SetError(localization.T("shell.login.qrUnavailable"));

    private void SetError(string? message)
    {
        ErrorMessage = message;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasError)));
    }

    private void Raise()
    {
        (SignInCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
```
(When QR backend lands, `ScanQrCommand` will instead route the scanned payload through `SignInAsync` —
swap `ShowQrUnavailable` for the real call; tests gate the stub behavior until then.)

5. **Run (expect PASS):**

```
"/mnt/c/Program Files/dotnet/dotnet.exe" test "D:\projects\afk4.net\tests\AFK4.Player.Shell.Tests\AFK4.Player.Shell.Tests.csproj"
```

6. **Commit:** `feat(player-shell): add LoginViewModel (phone+PIN, lockout, QR stub) with login catalog keys`

> **XAML note (deferred to Task 9):** the login view's PIN keypad and inputs must use touch targets
> **≥44×44 px** and **WCAG-AA** contrast. Build-verified only; not unit-tested. Layout via `interface-limb`.

---

## Task 4 — `MemberHomeViewModel` (balance / time / debt / warnings / tariff selection)

Renders wallet balance, debt, and time remaining from a `PlayerDashboardDto` + the live
`PlayerShellStateDto`; surfaces localized warning text driven by the new `WarningKind`; selects a tariff
(single → auto; multiple → chooser, spec assumption H).

**Files:**
- Create `src/AFK4.Player.Shell/Self/MemberHomeViewModel.cs`
- Create `src/AFK4.Player.Shell/Self/TariffOptionViewModel.cs`
- Test `tests/AFK4.Player.Shell.Tests/MemberHomeViewModelTests.cs`
- Modify `locales/ru.json`, `locales/en.json`, `locales/tg.json`
- Modify `src/AFK4.Shared.Contracts/Shell/PlayerShellStateDto.cs` **only if Unit 1 has not landed**
  `WarningKind`/`Branding` (Dependency #1) — add `string WarningKind = "none"` and `BrandingDto? Branding = null`,
  create `src/AFK4.Shared.Contracts/Shell/BrandingDto.cs`, and fix the contracts round-trip test.

### Steps

1. **Add catalog keys** in all three locale files:
   `shell.member.balance`, `shell.member.debt`, `shell.member.timeLeft`, `shell.member.noActiveSession`,
   `shell.warning.lowTime`, `shell.warning.lowBalance`, `shell.warning.creditLimit`,
   `shell.warning.connectivity`, `shell.member.chooseTariff`.
   `en` examples: `"Balance"`, `"Debt"`, `"Time left"`, `"No active session"`,
   `"Running low on time."`, `"Low balance."`, `"Credit limit reached."`, `"Connection unstable."`,
   `"Choose a tariff"`. `ru`: `"Баланс"`, `"Долг"`, `"Осталось времени"`, `"Нет активной сессии"`,
   `"Времени почти не осталось."`, `"Низкий баланс."`, `"Достигнут лимит кредита."`,
   `"Связь нестабильна."`, `"Выберите тариф"`. **tg mirrors ru.**

2. **Failing test** — `tests/AFK4.Player.Shell.Tests/MemberHomeViewModelTests.cs`:

```csharp
using AFK4.Localization;
using AFK4.Player.Shell.Self;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Players;

namespace AFK4.Player.Shell.Tests;

public sealed class MemberHomeViewModelTests
{
    private static MemberHomeViewModel Build() =>
        new(LocalizationService.LoadEmbedded(Locales.En));

    [Fact]
    public void ApplyDashboard_FormatsBalanceAndDebtAsCurrency()
    {
        var vm = Build();

        vm.ApplyDashboard(new PlayerDashboardDto(
            new MoneyDto("TJS", 12550), new MoneyDto("TJS", 3000), ActiveSession: null));

        Assert.Contains("125", vm.BalanceText);     // 125.50 TJS formatted
        Assert.Contains("30", vm.DebtText);          // 30.00 TJS
        Assert.False(vm.HasActiveSession);
    }

    [Fact]
    public void ApplyState_FixedSession_RendersRemainingTime()
    {
        var vm = Build();

        vm.ApplyState(StateWith(remainingSeconds: 1800, warningKind: "none"));

        Assert.Equal("30:00", vm.TimeLeftText);
    }

    [Fact]
    public void ApplyState_LowTimeWarning_SurfacesLocalizedLowTimeText()
    {
        var vm = Build();

        vm.ApplyState(StateWith(remainingSeconds: 120, warningKind: "low_time"));

        Assert.True(vm.HasWarning);
        Assert.Equal("Running low on time.", vm.WarningText);
    }

    [Fact]
    public void ApplyState_CreditLimitWarning_SurfacesLocalizedCreditLimitText()
    {
        var vm = Build();

        vm.ApplyState(StateWith(remainingSeconds: 0, warningKind: "credit_limit"));

        Assert.Equal("Credit limit reached.", vm.WarningText);
    }

    [Fact]
    public void SetTariffs_SingleTariff_AutoSelectsAndHidesChooser()
    {
        var vm = Build();
        var only = Guid.Parse("33333333-3333-4333-8333-333333333333");

        vm.SetTariffs([new TariffOptionViewModel(only, "Member hourly")]);

        Assert.Equal(only, vm.SelectedTariffRuleVersionId);
        Assert.False(vm.ShowTariffChooser);
    }

    [Fact]
    public void SetTariffs_MultipleTariffs_ShowsChooserNoAutoSelect()
    {
        var vm = Build();

        vm.SetTariffs(
        [
            new TariffOptionViewModel(Guid.NewGuid(), "Member hourly"),
            new TariffOptionViewModel(Guid.NewGuid(), "Member night")
        ]);

        Assert.True(vm.ShowTariffChooser);
        Assert.Null(vm.SelectedTariffRuleVersionId);
    }

    private static AFK4.Shared.Contracts.Shell.PlayerShellStateDto StateWith(int? remainingSeconds, string warningKind) =>
        new(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId: Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            State: AFK4.Shared.Contracts.Shell.PlayerShellStateNames.Active,
            SessionId: Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
            LeaseExpiresAtUtc: DateTimeOffset.Parse("2026-06-03T11:00:00Z"),
            RemainingSeconds: remainingSeconds,
            IsOnline: true,
            IsGraceMode: false,
            WarningThresholdSeconds: 300,
            Message: "Session active.",
            LauncherApps: [],
            Locale: Locales.En)
        {
            WarningKind = warningKind // assumes Unit 1 field (Dependency #1)
        };
}
```
> **Note on `StateWith`:** `PlayerShellStateDto` is a positional record. If Unit 1 added `WarningKind` as a
> **positional** parameter rather than an init-settable property, replace the `with { WarningKind = ... }`
> object-initializer above with the positional argument in the correct slot. Confirm the actual member shape
> against the merged Unit 1 DTO before running — this is the one place the test must match Unit 1 exactly.

3. **Run (expect FAIL — `MemberHomeViewModel` / `TariffOptionViewModel` / `WarningKind` do not exist):**

```
"/mnt/c/Program Files/dotnet/dotnet.exe" test "D:\projects\afk4.net\tests\AFK4.Player.Shell.Tests\AFK4.Player.Shell.Tests.csproj"
```

4. **Minimal impl** — `TariffOptionViewModel` is a tiny record-like VM:
```csharp
namespace AFK4.Player.Shell.Self;

public sealed record TariffOptionViewModel(Guid TariffRuleVersionId, string DisplayName);
```
`MemberHomeViewModel` (`INotifyPropertyChanged` + `SetField`, reuse `RemainingTimeFormatter` and
`ILocalizationService.FormatCurrency`; map `WarningKind` → catalog key):
```csharp
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using AFK4.Localization;
using AFK4.Player.Shell.Shell;
using AFK4.Shared.Contracts.Players;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Player.Shell.Self;

public sealed class MemberHomeViewModel(ILocalizationService localization) : INotifyPropertyChanged
{
    private string balanceText = string.Empty;
    private string debtText = string.Empty;
    private string timeLeftText = "--:--";
    private bool hasActiveSession;
    private string? warningText;
    private bool showTariffChooser;
    private Guid? selectedTariffRuleVersionId;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string BalanceText { get => balanceText; private set => SetField(ref balanceText, value); }
    public string DebtText { get => debtText; private set => SetField(ref debtText, value); }
    public string TimeLeftText { get => timeLeftText; private set => SetField(ref timeLeftText, value); }
    public bool HasActiveSession { get => hasActiveSession; private set => SetField(ref hasActiveSession, value); }
    public string? WarningText { get => warningText; private set { if (SetField(ref warningText, value)) Raise(nameof(HasWarning)); } }
    public bool HasWarning => WarningText is not null;
    public bool ShowTariffChooser { get => showTariffChooser; private set => SetField(ref showTariffChooser, value); }
    public Guid? SelectedTariffRuleVersionId { get => selectedTariffRuleVersionId; private set => SetField(ref selectedTariffRuleVersionId, value); }

    public ObservableCollection<TariffOptionViewModel> Tariffs { get; } = [];

    public void ApplyDashboard(PlayerDashboardDto dashboard)
    {
        BalanceText = localization.FormatCurrency(dashboard.WalletBalance.MinorUnits, dashboard.WalletBalance.CurrencyCode);
        DebtText = localization.FormatCurrency(dashboard.DebtBalance.MinorUnits, dashboard.DebtBalance.CurrencyCode);
        HasActiveSession = dashboard.ActiveSession is not null;
    }

    public void ApplyState(PlayerShellStateDto state)
    {
        TimeLeftText = RemainingTimeFormatter.Format(state.RemainingSeconds);
        WarningText = WarningKey(state.WarningKind) is { } key ? localization.T(key) : null;
    }

    public void SetTariffs(IReadOnlyList<TariffOptionViewModel> tariffs)
    {
        Tariffs.Clear();
        foreach (var tariff in tariffs)
        {
            Tariffs.Add(tariff);
        }

        if (tariffs.Count == 1)
        {
            SelectedTariffRuleVersionId = tariffs[0].TariffRuleVersionId;
            ShowTariffChooser = false;
        }
        else
        {
            SelectedTariffRuleVersionId = null;
            ShowTariffChooser = tariffs.Count > 1;
        }
    }

    public void SelectTariff(Guid tariffRuleVersionId) => SelectedTariffRuleVersionId = tariffRuleVersionId;

    private static string? WarningKey(string warningKind) => warningKind switch
    {
        "low_time" => "shell.warning.lowTime",
        "low_balance" => "shell.warning.lowBalance",
        "credit_limit" => "shell.warning.creditLimit",
        "connectivity" => "shell.warning.connectivity",
        _ => null
    };

    private void Raise(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
```

5. **Run (expect PASS):**

```
"/mnt/c/Program Files/dotnet/dotnet.exe" test "D:\projects\afk4.net\tests\AFK4.Player.Shell.Tests\AFK4.Player.Shell.Tests.csproj"
```

6. **Commit:** `feat(player-shell): add MemberHomeViewModel (balance/debt/time, WarningKind, tariff selection)`

---

## Task 5 — Self-start / self-extend with optimistic→confirmed transition (trust boundary)

The commands set an optimistic "starting…/extending…" flag and call the API, but the VM flips to active
**only** when the next `ApplyState(PlayerShellStateDto)` reports active; a still-locked state reverts the
flag (spec §7 / D8). This lives on `MemberHomeViewModel` (it owns the dashboard + state).

**Files:**
- Modify `src/AFK4.Player.Shell/Self/MemberHomeViewModel.cs`
- Modify `tests/AFK4.Player.Shell.Tests/MemberHomeViewModelTests.cs`
- Modify `locales/ru.json`, `locales/en.json`, `locales/tg.json` (`shell.member.starting`, `shell.member.extending`)

### Steps

1. **Add catalog keys** `shell.member.starting` / `shell.member.extending` in all three locales
   (`en`: `"Starting…"`, `"Extending…"`; `ru`: `"Запуск…"`, `"Продление…"`; tg mirrors ru).

2. **Failing test** — append to `MemberHomeViewModelTests`:

```csharp
[Fact]
public async Task StartSession_SetsOptimisticPendingThenConfirmsOnActiveState()
{
    var client = new FakeSelfApiClient
    {
        Dashboard = new PlayerDashboardDto(new MoneyDto("TJS", 9000), new MoneyDto("TJS", 0), null)
    };
    var vm = new MemberHomeViewModel(LocalizationService.LoadEmbedded(Locales.En));
    vm.AttachClient(client, seatId: Guid.Parse("11111111-1111-4111-8111-111111111111"));
    vm.SetTariffs([new TariffOptionViewModel(Guid.NewGuid(), "Member hourly")]);

    var startTask = vm.StartSessionAsync(CancellationToken.None);
    await startTask;

    Assert.True(vm.IsPending);                 // optimistic, not yet confirmed
    Assert.False(vm.IsConfirmedActive);

    vm.ApplyState(StateWith(remainingSeconds: 3600, warningKind: "none")); // backend confirms Active

    Assert.False(vm.IsPending);
    Assert.True(vm.IsConfirmedActive);
}

[Fact]
public async Task StartSession_WhenNextStateStillLocked_RevertsOptimisticPending()
{
    var client = new FakeSelfApiClient
    {
        Dashboard = new PlayerDashboardDto(new MoneyDto("TJS", 9000), new MoneyDto("TJS", 0), null)
    };
    var vm = new MemberHomeViewModel(LocalizationService.LoadEmbedded(Locales.En));
    vm.AttachClient(client, seatId: Guid.Parse("11111111-1111-4111-8111-111111111111"));
    vm.SetTariffs([new TariffOptionViewModel(Guid.NewGuid(), "Member hourly")]);

    await vm.StartSessionAsync(CancellationToken.None);
    Assert.True(vm.IsPending);

    vm.ApplyState(LockedState()); // backend did NOT start the session

    Assert.False(vm.IsPending);
    Assert.False(vm.IsConfirmedActive);
}

private static AFK4.Shared.Contracts.Shell.PlayerShellStateDto LockedState() =>
    new(
        OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
        BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
        DeviceId: Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
        State: AFK4.Shared.Contracts.Shell.PlayerShellStateNames.Locked,
        SessionId: null, LeaseExpiresAtUtc: null, RemainingSeconds: null,
        IsOnline: true, IsGraceMode: false, WarningThresholdSeconds: 300,
        Message: "Locked.", LauncherApps: [], Locale: Locales.En);
```
Extend the existing `FakeSelfApiClient` in this test file with `Dashboard` and implement `StartSessionAsync`/
`ExtendSessionAsync` to return it (and a `LastIdempotencyKey` capture if you want to assert non-empty keys).

3. **Run (expect FAIL — `IsPending`/`IsConfirmedActive`/`AttachClient`/`StartSessionAsync` do not exist):**

```
"/mnt/c/Program Files/dotnet/dotnet.exe" test "D:\projects\afk4.net\tests\AFK4.Player.Shell.Tests\AFK4.Player.Shell.Tests.csproj"
```

4. **Minimal impl** — add to `MemberHomeViewModel`:
```csharp
// fields
private IPlayerSelfApiClient? apiClient;
private Guid seatId;
private Guid? activeSessionId;
private bool isPending;
private bool isConfirmedActive;

public bool IsPending { get => isPending; private set => SetField(ref isPending, value); }
public bool IsConfirmedActive { get => isConfirmedActive; private set => SetField(ref isConfirmedActive, value); }

public void AttachClient(IPlayerSelfApiClient client, Guid seatId)
{
    apiClient = client;
    this.seatId = seatId;
}

public async Task StartSessionAsync(CancellationToken cancellationToken)
{
    if (apiClient is null || IsPending)
    {
        return;
    }

    IsPending = true; // optimistic; NOT confirmed (trust boundary)
    try
    {
        var dashboard = await apiClient.StartSessionAsync(
            seatId, SelectedTariffRuleVersionId, Guid.NewGuid().ToString("N"), cancellationToken);
        ApplyDashboard(dashboard);
        activeSessionId = dashboard.ActiveSession?.SessionId;
    }
    catch (Exception)
    {
        IsPending = false; // request failed → drop optimistic state, leave error to caller surface
    }
}

public async Task ExtendSessionAsync(int minutes, CancellationToken cancellationToken)
{
    if (apiClient is null || activeSessionId is null || IsPending)
    {
        return;
    }

    IsPending = true;
    try
    {
        var dashboard = await apiClient.ExtendSessionAsync(
            activeSessionId.Value, minutes, Guid.NewGuid().ToString("N"), cancellationToken);
        ApplyDashboard(dashboard);
    }
    catch (Exception)
    {
        IsPending = false;
    }
}
```
And in `ApplyState`, add the confirm/revert at the end (the backend-pushed state is the only authority):
```csharp
var active =
    string.Equals(state.State, PlayerShellStateNames.Active, StringComparison.Ordinal) ||
    string.Equals(state.State, PlayerShellStateNames.Grace, StringComparison.Ordinal) ||
    string.Equals(state.State, PlayerShellStateNames.Ending, StringComparison.Ordinal) ||
    state.IsGraceMode;
IsConfirmedActive = active;
if (IsPending)
{
    IsPending = false; // the pushed state (active or still-locked) resolves the optimistic request
}
```

5. **Run (expect PASS):**

```
"/mnt/c/Program Files/dotnet/dotnet.exe" test "D:\projects\afk4.net\tests\AFK4.Player.Shell.Tests\AFK4.Player.Shell.Tests.csproj"
```

6. **Commit:** `feat(player-shell): self-start/extend with optimistic-then-confirmed transition (trust boundary)`

---

## Task 6 — `TopUpViewModel` (presets → intent, dcgate QR payUrl, poll-to-confirm, operator fallback)

Preset amounts → `CreateTopUpIntentAsync`; if `Method=="dcgate"` and `PayUrl` is present, expose the
`PayUrl` string and a "waiting for confirmation" state, then poll the dashboard for a balance change. If the
method is `counter` (no `PayUrl`), show the operator-confirm fallback message. **QR rendering is a XAML
concern** (Task 9) — the VM only exposes the `PayUrl` string + flags.

**Files:**
- Create `src/AFK4.Player.Shell/Self/TopUpViewModel.cs`
- Test `tests/AFK4.Player.Shell.Tests/TopUpViewModelTests.cs`
- Modify `locales/ru.json`, `locales/en.json`, `locales/tg.json`

### Steps

1. **Add catalog keys** in all three locales: `shell.topup.title`, `shell.topup.waiting`,
   `shell.topup.operatorFallback`, `shell.topup.confirmed`.
   `en`: `"Top up"`, `"Scan to pay. Waiting for confirmation…"`,
   `"Ask the operator to confirm your top-up."`, `"Top-up confirmed."`.
   `ru`: `"Пополнить"`, `"Отсканируйте для оплаты. Ожидаем подтверждения…"`,
   `"Попросите оператора подтвердить пополнение."`, `"Пополнение подтверждено."`. tg mirrors ru.

2. **Failing test** — `tests/AFK4.Player.Shell.Tests/TopUpViewModelTests.cs`:

```csharp
using AFK4.Localization;
using AFK4.Player.Shell.Self;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Players;

namespace AFK4.Player.Shell.Tests;

public sealed class TopUpViewModelTests
{
    [Fact]
    public async Task CreateIntent_Dcgate_ExposesPayUrlAndWaitingState()
    {
        var client = new FakeSelfApiClient
        {
            TopUpResult = new TopUpIntentResult(
                Guid.NewGuid(), 5000, "TJS", "pending", "dcgate",
                "http://pay.dc.tj/?A=card&s=50&c=abc", "abc123")
        };
        var vm = new TopUpViewModel(client, LocalizationService.LoadEmbedded(Locales.En));

        await vm.CreateIntentAsync(amountMinorUnits: 5000, "TJS", CancellationToken.None);

        Assert.Equal("http://pay.dc.tj/?A=card&s=50&c=abc", vm.PayUrl);
        Assert.True(vm.IsWaitingForConfirmation);
        Assert.False(vm.IsOperatorFallback);
        Assert.Equal("Scan to pay. Waiting for confirmation…", vm.StatusText);
    }

    [Fact]
    public async Task CreateIntent_Counter_ShowsOperatorFallbackNoPayUrl()
    {
        var client = new FakeSelfApiClient
        {
            TopUpResult = new TopUpIntentResult(
                Guid.NewGuid(), 5000, "TJS", "pending", "counter", PayUrl: null, Comment: null)
        };
        var vm = new TopUpViewModel(client, LocalizationService.LoadEmbedded(Locales.En));

        await vm.CreateIntentAsync(amountMinorUnits: 5000, "TJS", CancellationToken.None);

        Assert.Null(vm.PayUrl);
        Assert.True(vm.IsOperatorFallback);
        Assert.Equal("Ask the operator to confirm your top-up.", vm.StatusText);
    }

    [Fact]
    public async Task PollOnce_WhenBalanceIncreased_MarksConfirmed()
    {
        var client = new FakeSelfApiClient
        {
            TopUpResult = new TopUpIntentResult(
                Guid.NewGuid(), 5000, "TJS", "pending", "dcgate", "http://pay.dc.tj/?x", "abc"),
            Dashboard = new PlayerDashboardDto(new MoneyDto("TJS", 17000), new MoneyDto("TJS", 0), null)
        };
        var vm = new TopUpViewModel(client, LocalizationService.LoadEmbedded(Locales.En));
        await vm.CreateIntentAsync(amountMinorUnits: 5000, "TJS", CancellationToken.None);

        var confirmed = await vm.PollOnceAsync(previousBalanceMinorUnits: 12000, CancellationToken.None);

        Assert.True(confirmed);
        Assert.True(vm.IsConfirmed);
        Assert.False(vm.IsWaitingForConfirmation);
        Assert.Equal("Top-up confirmed.", vm.StatusText);
    }

    private sealed class FakeSelfApiClient : IPlayerSelfApiClient
    {
        public TopUpIntentResult? TopUpResult { get; set; }
        public PlayerDashboardDto? Dashboard { get; set; }

        public Task<TopUpIntentResult> CreateTopUpIntentAsync(long a, string? c, CancellationToken ct) =>
            Task.FromResult(TopUpResult!);
        public Task<PlayerDashboardDto> GetDashboardAsync(CancellationToken ct) => Task.FromResult(Dashboard!);
        public Task<PlayerSignInResponse> SignInAsync(Guid o, string p, string pin, CancellationToken ct) => throw new NotSupportedException();
        public Task<PlayerDashboardDto> StartSessionAsync(Guid s, Guid? t, string k, CancellationToken ct) => throw new NotSupportedException();
        public Task<PlayerDashboardDto> ExtendSessionAsync(Guid s, int m, string k, CancellationToken ct) => throw new NotSupportedException();
    }
}
```

3. **Run (expect FAIL — `TopUpViewModel` does not exist):**

```
"/mnt/c/Program Files/dotnet/dotnet.exe" test "D:\projects\afk4.net\tests\AFK4.Player.Shell.Tests\AFK4.Player.Shell.Tests.csproj"
```

4. **Minimal impl** — `src/AFK4.Player.Shell/Self/TopUpViewModel.cs` (`INotifyPropertyChanged` + `SetField`;
   `PollOnceAsync` fetches the dashboard and confirms when the balance rose above the supplied baseline; a
   background polling loop is wired in Task 9 / App, the VM exposes the single-step poll for testability):
```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;
using AFK4.Localization;

namespace AFK4.Player.Shell.Self;

public sealed class TopUpViewModel(IPlayerSelfApiClient apiClient, ILocalizationService localization)
    : INotifyPropertyChanged
{
    private string? payUrl;
    private bool isWaitingForConfirmation;
    private bool isOperatorFallback;
    private bool isConfirmed;
    private string statusText = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string? PayUrl { get => payUrl; private set => SetField(ref payUrl, value); }
    public bool IsWaitingForConfirmation { get => isWaitingForConfirmation; private set => SetField(ref isWaitingForConfirmation, value); }
    public bool IsOperatorFallback { get => isOperatorFallback; private set => SetField(ref isOperatorFallback, value); }
    public bool IsConfirmed { get => isConfirmed; private set => SetField(ref isConfirmed, value); }
    public string StatusText { get => statusText; private set => SetField(ref statusText, value); }

    public static IReadOnlyList<long> PresetAmountsMinorUnits { get; } = [5000, 10000, 20000, 50000];

    public async Task CreateIntentAsync(long amountMinorUnits, string? currencyCode, CancellationToken cancellationToken)
    {
        IsConfirmed = false;
        var intent = await apiClient.CreateTopUpIntentAsync(amountMinorUnits, currencyCode, cancellationToken);
        if (string.Equals(intent.Method, "dcgate", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(intent.PayUrl))
        {
            PayUrl = intent.PayUrl;
            IsWaitingForConfirmation = true;
            IsOperatorFallback = false;
            StatusText = localization.T("shell.topup.waiting");
        }
        else
        {
            PayUrl = null;
            IsWaitingForConfirmation = false;
            IsOperatorFallback = true;
            StatusText = localization.T("shell.topup.operatorFallback");
        }
    }

    public async Task<bool> PollOnceAsync(long previousBalanceMinorUnits, CancellationToken cancellationToken)
    {
        var dashboard = await apiClient.GetDashboardAsync(cancellationToken);
        if (dashboard.WalletBalance.MinorUnits > previousBalanceMinorUnits)
        {
            IsConfirmed = true;
            IsWaitingForConfirmation = false;
            StatusText = localization.T("shell.topup.confirmed");
            return true;
        }

        return false;
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
```

5. **Run (expect PASS):**

```
"/mnt/c/Program Files/dotnet/dotnet.exe" test "D:\projects\afk4.net\tests\AFK4.Player.Shell.Tests\AFK4.Player.Shell.Tests.csproj"
```

6. **Commit:** `feat(player-shell): add TopUpViewModel (dcgate payUrl + poll-confirm, operator fallback)`

---

## Task 7 — Theming view-model from `Branding`

A `ShellThemeViewModel` exposes club name, logo URL, and accent color from the (nullable) `Branding` on the
state, with safe defaults when absent. XAML binds to these (Task 9). Accent color is exposed as the raw hex
string; the XAML converts it to a `Brush`.

**Files:**
- Create `src/AFK4.Player.Shell/Self/ShellThemeViewModel.cs`
- Test `tests/AFK4.Player.Shell.Tests/ShellThemeViewModelTests.cs`
- (`BrandingDto` already created in Task 4 if Unit 1 had not landed.)

### Steps

1. **Failing test** — `tests/AFK4.Player.Shell.Tests/ShellThemeViewModelTests.cs`:

```csharp
using AFK4.Player.Shell.Self;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Player.Shell.Tests;

public sealed class ShellThemeViewModelTests
{
    [Fact]
    public void Apply_WithBranding_ExposesNameLogoAccent()
    {
        var vm = new ShellThemeViewModel();

        vm.Apply(new BrandingDto("Cyber Arena", "https://cdn/logo.png", "#7C3AED"));

        Assert.Equal("Cyber Arena", vm.ClubName);
        Assert.Equal("https://cdn/logo.png", vm.LogoUrl);
        Assert.Equal("#7C3AED", vm.AccentColorHex);
    }

    [Fact]
    public void Apply_NullBranding_FallsBackToDefaults()
    {
        var vm = new ShellThemeViewModel();

        vm.Apply(branding: null);

        Assert.Equal("AFK4", vm.ClubName);
        Assert.Null(vm.LogoUrl);
        Assert.Equal("#2563EB", vm.AccentColorHex); // default accent
    }
}
```
> If Unit 1 named the branding record differently (e.g. members `Name`/`LogoUrl`/`AccentColor`), match the
> real `BrandingDto` member names in `Apply`.

2. **Run (expect FAIL — `ShellThemeViewModel` does not exist):**

```
"/mnt/c/Program Files/dotnet/dotnet.exe" test "D:\projects\afk4.net\tests\AFK4.Player.Shell.Tests\AFK4.Player.Shell.Tests.csproj"
```

3. **Minimal impl** — `src/AFK4.Player.Shell/Self/ShellThemeViewModel.cs`:
```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Player.Shell.Self;

public sealed class ShellThemeViewModel : INotifyPropertyChanged
{
    private const string DefaultClubName = "AFK4";
    private const string DefaultAccent = "#2563EB";

    private string clubName = DefaultClubName;
    private string? logoUrl;
    private string accentColorHex = DefaultAccent;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string ClubName { get => clubName; private set => SetField(ref clubName, value); }
    public string? LogoUrl { get => logoUrl; private set => SetField(ref logoUrl, value); }
    public string AccentColorHex { get => accentColorHex; private set => SetField(ref accentColorHex, value); }

    public void Apply(BrandingDto? branding)
    {
        ClubName = string.IsNullOrWhiteSpace(branding?.Name) ? DefaultClubName : branding!.Name;
        LogoUrl = branding?.LogoUrl;
        AccentColorHex = string.IsNullOrWhiteSpace(branding?.AccentColor) ? DefaultAccent : branding!.AccentColor!;
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
```

4. **Run (expect PASS):**

```
"/mnt/c/Program Files/dotnet/dotnet.exe" test "D:\projects\afk4.net\tests\AFK4.Player.Shell.Tests\AFK4.Player.Shell.Tests.csproj"
```

5. **Commit:** `feat(player-shell): add ShellThemeViewModel from Branding with safe defaults`

> **XAML acceptance (Task 9):** accent must keep **WCAG-AA** contrast against the dark background; all
> tappable controls **≥44×44 px**. Build-verified, not unit-tested; layout via `interface-limb`.

---

## Task 8 — Grace-mode "top up to keep playing" actionable panel

When `WarningKind ∈ {low_time, credit_limit}`, `MemberHomeViewModel` surfaces an actionable
"top up to keep playing" panel that deep-links to top-up then self-extend. We test the **VM logic**: the
panel flag flips on the right warnings and the deep-link command is enabled only then.

**Files:**
- Modify `src/AFK4.Player.Shell/Self/MemberHomeViewModel.cs`
- Modify `tests/AFK4.Player.Shell.Tests/MemberHomeViewModelTests.cs`
- Modify `locales/ru.json`, `locales/en.json`, `locales/tg.json` (`shell.member.keepPlaying`)

### Steps

1. **Add catalog key** `shell.member.keepPlaying` in all three locales (`en`: `"Top up to keep playing"`,
   `ru`: `"Пополните, чтобы продолжить игру"`, tg mirrors ru).

2. **Failing test** — append to `MemberHomeViewModelTests`:

```csharp
[Theory]
[InlineData("low_time", true)]
[InlineData("credit_limit", true)]
[InlineData("low_balance", false)]
[InlineData("connectivity", false)]
[InlineData("none", false)]
public void ApplyState_ShowsKeepPlayingPanelOnlyForActionableWarnings(string warningKind, bool expected)
{
    var vm = new MemberHomeViewModel(LocalizationService.LoadEmbedded(Locales.En));

    vm.ApplyState(StateWith(remainingSeconds: 60, warningKind: warningKind));

    Assert.Equal(expected, vm.ShowKeepPlayingPanel);
    Assert.Equal(expected, vm.KeepPlayingCommand.CanExecute(null));
    if (expected)
    {
        Assert.Equal("Top up to keep playing", vm.KeepPlayingText);
    }
}
```

3. **Run (expect FAIL — `ShowKeepPlayingPanel`/`KeepPlayingCommand`/`KeepPlayingText` do not exist):**

```
"/mnt/c/Program Files/dotnet/dotnet.exe" test "D:\projects\afk4.net\tests\AFK4.Player.Shell.Tests\AFK4.Player.Shell.Tests.csproj"
```

4. **Minimal impl** — add to `MemberHomeViewModel`:
```csharp
// fields
private bool showKeepPlayingPanel;

public bool ShowKeepPlayingPanel { get => showKeepPlayingPanel; private set { if (SetField(ref showKeepPlayingPanel, value)) (KeepPlayingCommand as RelayCommand)?.RaiseCanExecuteChanged(); } }
public string KeepPlayingText => localization.T("shell.member.keepPlaying");

// event the host wires to open TopUp then self-extend (deep-link)
public event EventHandler? KeepPlayingRequested;
public ICommand KeepPlayingCommand { get; }
```
Construct the command in the constructor (convert the primary-constructor `MemberHomeViewModel(...)` to a
classic ctor that stores `localization`):
```csharp
KeepPlayingCommand = new RelayCommand(_ => KeepPlayingRequested?.Invoke(this, EventArgs.Empty), _ => ShowKeepPlayingPanel);
```
In `ApplyState`, after computing the warning, set the panel:
```csharp
ShowKeepPlayingPanel =
    string.Equals(state.WarningKind, "low_time", StringComparison.Ordinal) ||
    string.Equals(state.WarningKind, "credit_limit", StringComparison.Ordinal);
```
(Add `using System.Windows.Input;` and `using AFK4.Player.Shell.Mvvm;`.)

5. **Run (expect PASS):**

```
"/mnt/c/Program Files/dotnet/dotnet.exe" test "D:\projects\afk4.net\tests\AFK4.Player.Shell.Tests\AFK4.Player.Shell.Tests.csproj"
```

6. **Commit:** `feat(player-shell): grace-mode keep-playing panel on actionable WarningKind`

---

## Task 9 — Wire views into `MainWindow.xaml` + compose services (build-verified)

XAML-only structural wiring: a login view when locked + self-service is offered, a member-home view when
signed in, plus the top-up panel and branding bindings. Compose the new services in `App`/`MainWindow`.
**No unit test** — the "test" is a successful build of `AFK4.Player.Shell`; acceptance is manual.

**Files:**
- Modify `src/AFK4.Player.Shell/MainWindow.xaml`
- Modify `src/AFK4.Player.Shell/MainWindow.xaml.cs`
- Modify `src/AFK4.Player.Shell/App.xaml.cs`
- Modify `src/AFK4.Player.Shell/Shell/PlayerShellViewModel.cs` (expose `Login` / `MemberHome` / `TopUp` /
  `Theme` child VMs + an `IsSelfServiceLogin` flag; fan `ApplyState` out to the children)
- Create `src/AFK4.Player.Shell/Self/HexToBrushConverter.cs` (XAML value converter for `AccentColorHex`)

### Steps

1. **Compose + fan-out (this part IS unit-tested via `PlayerShellViewModelTests`).** Add a failing test to
   `PlayerShellViewModelTests` asserting `ApplyState` fans out to the children and that a locked-but-online
   state offers self-service login:

```csharp
[Fact]
public void ApplyState_LockedOnline_OffersSelfServiceLoginAndFansOutToChildren()
{
    var viewModel = BuildViewModel(); // existing helper

    viewModel.ApplyState(CreateState(PlayerShellStateNames.Locked, remainingSeconds: null, launcherApps: []));

    Assert.True(viewModel.IsSelfServiceLogin);   // locked → show login
    Assert.NotNull(viewModel.Login);
    Assert.NotNull(viewModel.MemberHome);
    Assert.NotNull(viewModel.TopUp);
    Assert.NotNull(viewModel.Theme);
}
```
Run (expect FAIL — members do not exist), then add to `PlayerShellViewModel`: construct `Login`,
`MemberHome`, `TopUp`, `Theme` (inject `IPlayerSelfApiClient` + `IPlayerSessionTokenHolder` via ctor; the
existing two-arg ctor stays for the current tests by overloading or defaulting the new deps to in-memory
fakes — prefer adding an overload that the existing tests keep using). In `ApplyState`, after the current
body, call `MemberHome.ApplyState(dto)`, `Theme.Apply(dto.Branding)`, and set
`IsSelfServiceLogin = IsLocked && !tokenHolder.IsSignedIn`. Re-run (expect PASS). This keeps the existing
five `PlayerShellViewModelTests` green (verify them in the run).
Commit: `feat(player-shell): compose Login/MemberHome/TopUp/Theme under PlayerShellViewModel and fan out state`

2. **XAML wiring (build-verified).** Add to `MainWindow.xaml`: a `loc`-localized login panel bound to
   `Login` (visible when `IsSelfServiceLogin`), a member-home panel bound to `MemberHome` (visible when
   signed in / active), the top-up panel bound to `TopUp`, the header bound to `Theme.ClubName` /
   `Theme.LogoUrl`, and accent via `HexToBrushConverter` on `Theme.AccentColorHex`. Use
   `{loc:T Key=shell.login.*}` etc. for every literal. Keep `BooleanToVisibilityConverter` for flags.
   **Invoke `interface-limb` here** for the actual login keypad / HUD / grace-panel layout, type scale,
   spacing, motion, and to enforce **≥44×44 px** targets and **WCAG-AA** contrast.

3. **Create `HexToBrushConverter`** (`IValueConverter`: hex string → `SolidColorBrush`, fallback to the
   default accent on parse failure).

4. **Compose services in `App.xaml.cs` / `MainWindow`.** Build `HttpClient { BaseAddress = options.ApiBaseUrl }`,
   `InMemoryPlayerSessionTokenHolder`, `HttpPlayerSelfApiClient`, and pass them into `PlayerShellViewModel`.
   Drop the token (`tokenHolder.Clear()` + `Login.Reset()`) whenever `ApplyState` reports `Locked` (idle/lock/
   sign-out all collapse to a locked state over the pipe).

5. **Build (the "test" for XAML) — on the Windows clone:**

```
find src -type d \( -name obj -o -name bin \) -prune -exec rm -rf {} +
"/mnt/c/Program Files/dotnet/dotnet.exe" build "D:\projects\afk4.net\src\AFK4.Player.Shell\AFK4.Player.Shell.csproj"
```
Expect: **build succeeds.** Manual acceptance (operator, on the kiosk): locked screen shows the login
panel; PIN sign-in reveals member home with balance/time/debt; a low-time state shows the keep-playing
panel; top-up shows a QR (dcgate) or the operator-confirm message; the header reflects branding; all touch
targets are comfortably ≥44 px.

6. **Run the full shell test project once more (regression):**

```
"/mnt/c/Program Files/dotnet/dotnet.exe" test "D:\projects\afk4.net\tests\AFK4.Player.Shell.Tests\AFK4.Player.Shell.Tests.csproj"
```
Expect: **all PASS.**

7. **Commit:** `feat(player-shell): wire login/member-home/top-up views + branding into MainWindow`

---

## Verification gate

Unit 4 is complete when, **on the Windows clone `D:\projects\afk4.net`** (after cleaning cross-OS obj/bin):

```
find src -type d \( -name obj -o -name bin \) -prune -exec rm -rf {} +
"/mnt/c/Program Files/dotnet/dotnet.exe" test "D:\projects\afk4.net\tests\AFK4.Player.Shell.Tests\AFK4.Player.Shell.Tests.csproj"
"/mnt/c/Program Files/dotnet/dotnet.exe" build "D:\projects\afk4.net\src\AFK4.Player.Shell\AFK4.Player.Shell.csproj"
```

- **`dotnet.exe test` for `AFK4.Player.Shell.Tests` PASSES** (all existing + new VM/client tests green).
- **`AFK4.Player.Shell` builds** (XAML compiles, converters resolve, bindings parse).
- The three locale catalogs (`locales/ru.json` / `en.json` / `tg.json`) all carry every new `shell.*` key
  (no missing-key fallbacks at runtime); tg mirrors ru as the agreed STOPGAP.

On Linux/WSL, only a XAML build-check is possible (`EnableWindowsTargeting=true`); the **real test run is on
the `D:\` clone**. Do not claim completion from a Linux build alone.

**Known not-end-to-end-yet at gate time (by design — see Dependencies):** self-start/extend and dcgate QR
top-up exercise the VM + client request shape against stub handlers and pass, but live behavior depends on
Unit 1's `/api/me/sessions/*` routes + `WarningKind`/`Branding` DTO fields and Unit 2's `payUrl` on the
top-up intent. QR **login** is a wired stub until a backend `sign-in-qr` endpoint exists.
