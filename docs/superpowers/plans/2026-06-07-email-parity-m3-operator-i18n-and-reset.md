# Email parity M3 — Operator: ICU i18n engine, full localization, login-by-email + channel-aware reset — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bring the Operator desktop app (`src/AFK4.Operator.App.Web` + its WPF/WebView2 host `src/AFK4.Operator.App`) to email/SMS parity — relabel login to "Логин или email", add a channel-aware "Забыли пароль?" flow — and do it on a *real* i18n foundation: upgrade the shared `@afk4/i18n` `t()` to ICU MessageFormat (interpolation + per-locale plurals), wire `<I18nProvider>` into Operator, and migrate every hardcoded Operator string into the catalog with **real ru/en/tg translations**.

**Architecture:** Five layers, built bottom-up so each is independently green. (A) shared i18n engine gains `intl-messageformat` and `t(key, values?)`; (B) Operator mounts `<I18nProvider>`; (C) the .NET host learns four reset bridge ops and the TS bridge stops discarding the backend error `code`/`remainingAttempts`; (D) `authClient.ts` exposes four reset calls; (E) two Operator-styled screens (`ForgotPassword`, `ResetPassword`) plus a login relabel + link, wired through an auth sub-view in `App.tsx`; (F) the bulk string migration of all remaining hardcoded Operator copy into the ICU catalog (deleting `pluralRu`); (G) full verification across every harness.

**Tech Stack:** .NET 10 minimal-API host + WebView2 bridge + xunit (host); React 19 + TypeScript + Vite + `bun test` (happy-dom + @testing-library/react) (Operator web); shared `@afk4/i18n` workspace package (catalog `locales/{ru,en,tg}.json` → generated `packages/i18n/src/messages.ts`) now backed by `intl-messageformat`.

**Translation policy (LOCKED — honest, no fake-green):** Every catalog key exists in **ru, en, and tg with a real translation**. The catalog parity test (`packages/i18n/src/messages.test.ts`) requires identical key sets across locales; copying ru into en/tg to satisfy it is forbidden (a passing test on copied data is a false "done"). ru = source meaning; en = real English; tg = real Tajik (authored in this work; the product owner is a native Tajik speaker and verifies quality). The voice guard (`voice.test.ts`) still applies: no Cyrillic ALL-CAPS of 4+ letters, never «компьютер» (use «ПК»).

**Scope boundary:** M3 only — Operator. M4 (SetupWizard.Web) and M5 (cross-app parity check) remain separate plans. The ICU engine upgrade (Phase A) is additive and backward-compatible; existing Platform.Web / Customer.Web call sites (`t(key)`) keep working unchanged and are **not** retrofitted to ICU here (a possible later cleanup). Operator gets email **login** for free from M1 (`auth:signIn` → backend `SignInAsync`, which already resolves username-or-email) — no backend login change in M3.

---

## File map

**Phase A — shared i18n engine (`packages/i18n`):**
- Modify: `packages/i18n/package.json` — add `intl-messageformat` dependency.
- Modify: `packages/i18n/src/I18nProvider.tsx` — ICU-backed `t(key, values?)`.
- Create: `packages/i18n/src/I18nProvider.test.tsx` — interpolation + per-locale plural tests.
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json` — the first ICU keys (`op.dashboard.signals`, used by the Phase F worked example) in all three locales; then `bun run gen`.

**Phase B — Operator provider wiring:**
- Modify: `src/AFK4.Operator.App.Web/src/main.tsx` — wrap `<App/>` in `<I18nProvider>`.

**Phase C — .NET host reset bridge ops:**
- Modify: `src/AFK4.Operator.App/Auth/IOperatorAuthApiClient.cs` — 4 reset method signatures.
- Modify: `src/AFK4.Operator.App/Auth/HttpOperatorAuthApiClient.cs` — 4 reset HTTP calls + structured-error parsing.
- Create: `src/AFK4.Operator.App/Auth/OperatorAuthApiException.cs` — carries backend `code` + `remainingAttempts`.
- Modify: `src/AFK4.Operator.App/Web/OperatorWebHostBridge.cs` — 4 switch cases + handlers + `remainingAttempts` on the bridge error.
- Modify: `tests/AFK4.Operator.App.Tests/OperatorWebHostBridgeTests.cs` — reset op tests + recording-client additions.

**Phase D — TS bridge + auth client:**
- Modify: `src/AFK4.Operator.App.Web/src/hostBridge.ts` — preserve `code`/`remainingAttempts`; add `HostBridgeRequestError`.
- Modify: `src/AFK4.Operator.App.Web/src/authClient.ts` — 4 reset functions.
- Modify: `src/AFK4.Operator.App.Web/src/authClient.test.ts` — tests for the 4 functions.

**Phase E — Operator reset screens + login wiring:**
- Create: `src/AFK4.Operator.App.Web/src/ForgotPassword.tsx` + `ForgotPassword.test.tsx`.
- Create: `src/AFK4.Operator.App.Web/src/ResetPassword.tsx` + `ResetPassword.test.tsx`.
- Modify: `src/AFK4.Operator.App.Web/src/App.tsx` — `SignInScreen` relabel + forgot link; auth sub-view state; render the screens.
- Modify: `src/AFK4.Operator.App.Web/src/App.test.tsx` — extend the bridge fake with the 4 reset ops; add a forgot-flow wiring test.

**Phase F — bulk string migration (per-file):**
- Modify: every Operator component still carrying hardcoded RU copy — worked example `DashboardWorkspace.tsx`, then `MapWorkspace.tsx`, `MapSidePanel.tsx`, `SummarySidePanel.tsx`, `ReviewWorkspace.tsx`, `operatorPrimitives.tsx`, `BackendPosWorkspace.tsx`, `BackendPaymentsWorkspace.tsx`, `BackendPlayersWorkspace.tsx`, `BackendBookingWorkspace.tsx`, `BackendLogsWorkspace.tsx`, `BackendSettingsWorkspace.tsx`, the `App.tsx` shell strings, and `operatorHelpers.ts` (string builders) — into ICU catalog keys; delete `pluralRu`.
- Modify: `locales/{ru,en,tg}.json` (+ `bun run gen`) per file.

**Phase G — verification only.**

---

## Phase A — Shared i18n engine: ICU MessageFormat behind `t()`

### Task 1: Add `intl-messageformat` and the first ICU keys

**Files:**
- Modify: `packages/i18n/package.json`
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json`
- Regenerate: `packages/i18n/src/messages.ts`

- [ ] **Step 1: Add the dependency**

Run: `cd packages/i18n && ~/.bun/bin/bun add intl-messageformat`
Expected: `package.json` gains `"intl-messageformat": "^10.x"` under `dependencies`; `bun.lock` updates.

- [ ] **Step 2: Add the first ICU plural key to all three locales**

This key is consumed by the Phase F worked example (`DashboardWorkspace`). Append to the **end** of each locale's JSON object (mind trailing commas — keep valid JSON). The plural categories must include `other` (ICU requires it) and the language's real forms.

`locales/ru.json`:
```json
  "op.dashboard.signals": "{count, plural, one {# сигнал} few {# сигнала} many {# сигналов} other {# сигнала}}"
```
`locales/en.json`:
```json
  "op.dashboard.signals": "{count, plural, one {# signal} other {# signals}}"
```
`locales/tg.json`:
```json
  "op.dashboard.signals": "{count, plural, one {# сигнал} other {# сигнал}}"
```
(Tajik does not inflect the counted noun after a numeral — the bare noun is correct for every count.)

- [ ] **Step 3: Regenerate the catalog**

Run: `cd packages/i18n && ~/.bun/bin/bun run gen`
Expected: `generated …/src/messages.ts from 3 locales`.

- [ ] **Step 4: Verify catalog guards still pass**

Run: `cd packages/i18n && ~/.bun/bin/bun test`
Expected: PASS — parity (identical key sets), generated-matches-source, and voice guards all green (the engine test is added next and will fail until Task 2).

- [ ] **Step 5: Commit**

```bash
git add packages/i18n/package.json bun.lock packages/i18n/src/messages.ts locales/ru.json locales/en.json locales/tg.json
git commit -m "build(i18n): add intl-messageformat and first ICU plural key"
```

### Task 2: ICU-backed `t(key, values?)`

**Files:**
- Modify: `packages/i18n/src/I18nProvider.tsx`
- Test: `packages/i18n/src/I18nProvider.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `packages/i18n/src/I18nProvider.test.tsx`:
```tsx
import { render, screen } from '@testing-library/react';
import { it, expect } from 'bun:test';
import { I18nProvider, useI18n } from './I18nProvider';
import type { Locale, MessageKey } from './messages';

function Probe({ messageKey, count }: { messageKey: MessageKey; count: number }) {
  const { t } = useI18n();
  return <span>{t(messageKey, { count })}</span>;
}

function renderProbe(locale: Locale, count: number) {
  render(
    <I18nProvider initialLocale={locale}>
      <Probe messageKey="op.dashboard.signals" count={count} />
    </I18nProvider>
  );
}

it('applies Russian plural forms via ICU', () => {
  renderProbe('ru', 1);
  expect(screen.getByText('1 сигнал')).toBeInTheDocument();
});

it('selects the Russian few/many forms by count', () => {
  const { rerender } = render(
    <I18nProvider initialLocale="ru"><Probe messageKey="op.dashboard.signals" count={2} /></I18nProvider>
  );
  expect(screen.getByText('2 сигнала')).toBeInTheDocument();
  rerender(<I18nProvider initialLocale="ru"><Probe messageKey="op.dashboard.signals" count={5} /></I18nProvider>);
  expect(screen.getByText('5 сигналов')).toBeInTheDocument();
});

it('applies English plural forms for the en locale', () => {
  renderProbe('en', 1);
  expect(screen.getByText('1 signal')).toBeInTheDocument();
  renderProbe('en', 2);
  expect(screen.getByText('2 signals')).toBeInTheDocument();
});

it('returns a plain message unchanged when called without values', () => {
  function Plain() {
    const { t } = useI18n();
    return <span>{t('auth.field.password')}</span>;
  }
  render(<I18nProvider initialLocale="ru"><Plain /></I18nProvider>);
  expect(screen.getByText('Пароль')).toBeInTheDocument();
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd packages/i18n && ~/.bun/bin/bun test src/I18nProvider.test.tsx`
Expected: FAIL — `t` does not accept a second argument / plural not applied (output shows the raw `{count, plural …}` or a type error).

- [ ] **Step 3: Implement the ICU engine**

In `packages/i18n/src/I18nProvider.tsx`, add the import at the top (after the existing imports):
```tsx
import { IntlMessageFormat } from 'intl-messageformat';
```

Change the context type signature for `t` (line ~8):
```tsx
  t: (key: MessageKey, values?: Record<string, string | number>) => string;
```

Add these module-level helpers just below `const DEFAULT_LOCALE: Locale = 'ru';` (and the existing `STORAGE_KEY`):
```tsx
// Compiled ICU messages cached by locale-tag + raw message text (a given key+locale
// resolves to a stable string, so this never goes stale within a session).
const icuCache = new Map<string, IntlMessageFormat>();

function resolveMessage(locale: Locale, key: MessageKey): string {
  const direct = (messages[locale] as Record<string, string>)[key];
  if (direct !== undefined) return direct;
  for (const fb of LOCALE_FALLBACK[locale]) {
    const value = (messages[fb] as Record<string, string>)[key];
    if (value !== undefined) return value;
  }
  return key;
}

function formatIcu(message: string, localeTag: string, values: Record<string, string | number>): string {
  const cacheKey = `${localeTag} ${message}`;
  let formatter = icuCache.get(cacheKey);
  if (formatter === undefined) {
    formatter = new IntlMessageFormat(message, localeTag);
    icuCache.set(cacheKey, formatter);
  }
  return String(formatter.format(values));
}
```

Replace the existing `t` callback (the `const t = useCallback(...)` block) with:
```tsx
  const t = useCallback(
    (key: MessageKey, values?: Record<string, string | number>): string => {
      const message = resolveMessage(locale, key);
      // Fast path: plain strings with no ICU placeholders skip the formatter.
      if (values === undefined && !message.includes('{')) {
        return message;
      }
      try {
        return formatIcu(message, LOCALE_TAG[locale], values ?? {});
      } catch {
        // A malformed ICU string must never crash the UI — show the raw message.
        return message;
      }
    },
    [locale]
  );
```

(`resolveMessage` reuses the existing `LOCALE_FALLBACK`/`messages`; the old inline fallback loop is now inside it.)

- [ ] **Step 4: Run the test to verify it passes**

Run: `cd packages/i18n && ~/.bun/bin/bun test`
Expected: PASS — engine tests green, plus existing parity/voice/messages tests still green.

- [ ] **Step 5: Confirm the other frontends still build (backward compatibility)**

Run: `cd src/AFK4.Platform.Web && ~/.bun/bin/bun x tsc -b && ~/.bun/bin/bun test`
Expected: PASS — `t(key)` calls still typecheck and behave identically (the new param is optional).

- [ ] **Step 6: Commit**

```bash
git add packages/i18n/src/I18nProvider.tsx packages/i18n/src/I18nProvider.test.tsx
git commit -m "feat(i18n): ICU MessageFormat interpolation and per-locale plurals in t()"
```

---

## Phase B — Operator mounts the i18n provider

### Task 3: Wrap the Operator app in `<I18nProvider>`

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/main.tsx`

- [ ] **Step 1: Wire the provider**

Replace the whole body of `main.tsx` with:
```tsx
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { I18nProvider } from '@afk4/i18n';
import { App } from './App';
import './styles.css';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <I18nProvider>
      <App />
    </I18nProvider>
  </StrictMode>
);
```
(`@afk4/i18n` is already a dependency in `package.json`. Default locale = `ru` via the provider, persisted to `localStorage` — correct for a Russian-first kiosk; a locale switcher is out of scope.)

- [ ] **Step 2: Verify the app still builds and the suite is green**

Run: `cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun x tsc -b && ~/.bun/bin/bun test`
Expected: PASS. (Components already using `useI18n` — e.g. `PhoneVerificationCard` — now have a provider in the real app too; tests already wrap in their own provider.)

- [ ] **Step 3: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/main.tsx
git commit -m "feat(operator-web): mount @afk4/i18n provider at the app root"
```

---

## Phase C — .NET host: four reset bridge ops

### Task 4: Reset HTTP calls on the auth API client

**Files:**
- Modify: `src/AFK4.Operator.App/Auth/IOperatorAuthApiClient.cs`
- Create: `src/AFK4.Operator.App/Auth/OperatorAuthApiException.cs`
- Modify: `src/AFK4.Operator.App/Auth/HttpOperatorAuthApiClient.cs`

- [ ] **Step 1: Add the exception type**

Create `src/AFK4.Operator.App/Auth/OperatorAuthApiException.cs`:
```csharp
namespace AFK4.Operator.App.Auth;

/// <summary>
/// A reset endpoint returned a structured business error (e.g. invalid_code with a
/// remaining-attempts count). Carries the backend error code and remaining attempts so the
/// host bridge can forward them to the web UI instead of collapsing to a generic message.
/// </summary>
public sealed class OperatorAuthApiException(string code, string message, int? remainingAttempts)
    : Exception(message)
{
    public string Code { get; } = code;
    public int? RemainingAttempts { get; } = remainingAttempts;
}
```

- [ ] **Step 2: Extend the interface**

In `IOperatorAuthApiClient.cs`, add these members inside the interface (after `RefreshAsync`):
```csharp
    Task ForgotPasswordByEmailAsync(string userNameOrEmail, CancellationToken cancellationToken);

    Task ResetPasswordByEmailAsync(string token, string newPassword, CancellationToken cancellationToken);

    Task ForgotPasswordByPhoneAsync(string phoneNumber, CancellationToken cancellationToken);

    Task ResetPasswordByPhoneAsync(
        string phoneNumber,
        string code,
        string newPassword,
        CancellationToken cancellationToken);
```

- [ ] **Step 3: Implement the calls**

In `HttpOperatorAuthApiClient.cs`, add `using System.Text.Json;` is already present. Add these methods to the class (after `RefreshAsync`, before the private `SendAsync<T>`):
```csharp
    public Task ForgotPasswordByEmailAsync(string userNameOrEmail, CancellationToken cancellationToken)
        => PostResetAsync(
            "/api/auth/staff/forgot-password",
            new StaffForgotPasswordRequest(userNameOrEmail),
            cancellationToken);

    public Task ResetPasswordByEmailAsync(string token, string newPassword, CancellationToken cancellationToken)
        => PostResetAsync(
            "/api/auth/staff/reset-password",
            new StaffResetPasswordRequest(token, newPassword),
            cancellationToken);

    public Task ForgotPasswordByPhoneAsync(string phoneNumber, CancellationToken cancellationToken)
        => PostResetAsync(
            "/api/auth/staff/forgot-password-by-phone",
            new StaffForgotPasswordByPhoneRequest(phoneNumber),
            cancellationToken);

    public Task ResetPasswordByPhoneAsync(
        string phoneNumber,
        string code,
        string newPassword,
        CancellationToken cancellationToken)
        => PostResetAsync(
            "/api/auth/staff/reset-password-by-phone",
            new StaffResetPasswordByPhoneRequest(phoneNumber, code, newPassword),
            cancellationToken);

    // Reset endpoints return 200 on success (no token to persist). On a non-2xx, the body is
    // { "error": "<code>", "remainingAttempts": <n>? } — preserve both so the UI can show the
    // specific reason and the attempts left (parity with the Platform.Web reset screen).
    private async Task PostResetAsync<T>(string path, T body, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(path, body, JsonOptions, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var code = "reset_failed";
        int? remainingAttempts = null;
        try
        {
            using var document = JsonDocument.Parse(errorBody);
            if (document.RootElement.TryGetProperty("error", out var errorElement)
                && errorElement.ValueKind == JsonValueKind.String)
            {
                code = errorElement.GetString() ?? code;
            }

            if (document.RootElement.TryGetProperty("remainingAttempts", out var remainingElement)
                && remainingElement.ValueKind == JsonValueKind.Number)
            {
                remainingAttempts = remainingElement.GetInt32();
            }
        }
        catch (JsonException)
        {
            // Non-JSON error body: keep the generic code.
        }

        throw new OperatorAuthApiException(
            code,
            $"Platform API returned {(int)response.StatusCode} for {path}.",
            remainingAttempts);
    }
```

- [ ] **Step 4: Build**

Run: `dotnet build src/AFK4.Operator.App`
Expected: builds (the interface members are now implemented).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App/Auth/IOperatorAuthApiClient.cs src/AFK4.Operator.App/Auth/OperatorAuthApiException.cs src/AFK4.Operator.App/Auth/HttpOperatorAuthApiClient.cs
git commit -m "feat(operator-host): add staff password-reset api calls with structured errors"
```

### Task 5: Bridge dispatch + handlers + structured error forwarding

**Files:**
- Modify: `src/AFK4.Operator.App/Web/OperatorWebHostBridge.cs`
- Test: `tests/AFK4.Operator.App.Tests/OperatorWebHostBridgeTests.cs`

- [ ] **Step 1: Write the failing tests**

In `OperatorWebHostBridgeTests.cs`, first extend the recording auth client so it implements the four new interface methods. Add these members to `RecordingOperatorAuthApiClient` (record the last call; allow an injected failure):
```csharp
    public string? LastForgotEmail { get; private set; }
    public (string Token, string NewPassword)? LastResetEmail { get; private set; }
    public string? LastForgotPhone { get; private set; }
    public (string Phone, string Code, string NewPassword)? LastResetPhone { get; private set; }
    public OperatorAuthApiException? ResetException { get; set; }

    public Task ForgotPasswordByEmailAsync(string userNameOrEmail, CancellationToken cancellationToken)
    {
        LastForgotEmail = userNameOrEmail;
        return ResetException is null ? Task.CompletedTask : throw ResetException;
    }

    public Task ResetPasswordByEmailAsync(string token, string newPassword, CancellationToken cancellationToken)
    {
        LastResetEmail = (token, newPassword);
        return ResetException is null ? Task.CompletedTask : throw ResetException;
    }

    public Task ForgotPasswordByPhoneAsync(string phoneNumber, CancellationToken cancellationToken)
    {
        LastForgotPhone = phoneNumber;
        return ResetException is null ? Task.CompletedTask : throw ResetException;
    }

    public Task ResetPasswordByPhoneAsync(string phoneNumber, string code, string newPassword, CancellationToken cancellationToken)
    {
        LastResetPhone = (phoneNumber, code, newPassword);
        return ResetException is null ? Task.CompletedTask : throw ResetException;
    }
```

Then add the tests:
```csharp
[Fact]
public async Task HandleAsync_ForgotByEmail_CallsClientAndReturnsOk()
{
    var authClient = new RecordingOperatorAuthApiClient();
    var bridge = new OperatorWebHostBridge(authClient, new RecordingOperatorTokenStore(), new RecordingOperatorConnectionStore());

    var responseJson = await bridge.HandleAsync(
        JsonSerializer.Serialize(new
        {
            type = "auth:forgotByEmail",
            requestId = "request-1",
            payload = new { userNameOrEmail = " owner@demo.test " }
        }),
        CancellationToken.None);

    using var document = JsonDocument.Parse(responseJson!);
    Assert.True(document.RootElement.GetProperty("ok").GetBoolean());
    Assert.Equal("owner@demo.test", authClient.LastForgotEmail);
}

[Fact]
public async Task HandleAsync_ResetByPhone_ForwardsCodeAndRemainingAttempts()
{
    var authClient = new RecordingOperatorAuthApiClient
    {
        ResetException = new OperatorAuthApiException("invalid_code", "bad code", 2)
    };
    var bridge = new OperatorWebHostBridge(authClient, new RecordingOperatorTokenStore(), new RecordingOperatorConnectionStore());

    var responseJson = await bridge.HandleAsync(
        JsonSerializer.Serialize(new
        {
            type = "auth:resetByPhone",
            requestId = "request-2",
            payload = new { phoneNumber = "+992937380070", code = "000000", newPassword = "Passw0rd!New" }
        }),
        CancellationToken.None);

    using var document = JsonDocument.Parse(responseJson!);
    var root = document.RootElement;
    Assert.False(root.GetProperty("ok").GetBoolean());
    var error = root.GetProperty("error");
    Assert.Equal("invalid_code", error.GetProperty("code").GetString());
    Assert.Equal(2, error.GetProperty("remainingAttempts").GetInt32());
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/AFK4.Operator.App.Tests --filter "FullyQualifiedName~HandleAsync_ForgotByEmail|FullyQualifiedName~HandleAsync_ResetByPhone"`
Expected: FAIL — `auth:forgotByEmail` / `auth:resetByPhone` are "Unsupported host bridge request" (and `error` has no `remainingAttempts`).

- [ ] **Step 3: Implement the dispatch, handlers, and error shape**

In `OperatorWebHostBridge.cs`:

(a) Add the four cases to the `request.Type switch` (after `"auth:signOut"`):
```csharp
                "auth:forgotByEmail" => await ForgotPasswordByEmailAsync(request.Payload, cancellationToken),
                "auth:resetByEmail" => await ResetPasswordByEmailAsync(request.Payload, cancellationToken),
                "auth:forgotByPhone" => await ForgotPasswordByPhoneAsync(request.Payload, cancellationToken),
                "auth:resetByPhone" => await ResetPasswordByPhoneAsync(request.Payload, cancellationToken),
```

(b) Add an `OperatorAuthApiException` catch **before** the existing general catch (so its structured fields survive):
```csharp
        catch (OperatorAuthApiException exception)
        {
            return CreateResponse(
                request.RequestId,
                ok: false,
                payload: null,
                new OperatorWebBridgeError(exception.Code, exception.Message, exception.RemainingAttempts));
        }
        catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException or JsonException)
        {
            return CreateResponse(
                request.RequestId,
                ok: false,
                payload: null,
                new OperatorWebBridgeError(errorCode, exception.Message, null));
        }
```

(c) Add the four handler methods (after `SignOutAsync`):
```csharp
    private async Task<object> ForgotPasswordByEmailAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        var request = DeserializePayload<OperatorWebForgotByEmailPayload>(payload);
        if (string.IsNullOrWhiteSpace(request.UserNameOrEmail))
        {
            throw new InvalidOperationException("Login or email is required.");
        }

        await authApiClient.ForgotPasswordByEmailAsync(request.UserNameOrEmail.Trim(), cancellationToken);
        return new { ok = true };
    }

    private async Task<object> ResetPasswordByEmailAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        var request = DeserializePayload<OperatorWebResetByEmailPayload>(payload);
        if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            throw new InvalidOperationException("Token and new password are required.");
        }

        await authApiClient.ResetPasswordByEmailAsync(request.Token.Trim(), request.NewPassword, cancellationToken);
        return new { ok = true };
    }

    private async Task<object> ForgotPasswordByPhoneAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        var request = DeserializePayload<OperatorWebForgotByPhonePayload>(payload);
        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            throw new InvalidOperationException("Phone number is required.");
        }

        await authApiClient.ForgotPasswordByPhoneAsync(request.PhoneNumber.Trim(), cancellationToken);
        return new { ok = true };
    }

    private async Task<object> ResetPasswordByPhoneAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        var request = DeserializePayload<OperatorWebResetByPhonePayload>(payload);
        if (string.IsNullOrWhiteSpace(request.PhoneNumber)
            || string.IsNullOrWhiteSpace(request.Code)
            || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            throw new InvalidOperationException("Phone, code, and new password are required.");
        }

        await authApiClient.ResetPasswordByPhoneAsync(
            request.PhoneNumber.Trim(),
            request.Code.Trim(),
            request.NewPassword,
            cancellationToken);
        return new { ok = true };
    }
```

(d) Add `RemainingAttempts` to the bridge error record (replace the existing `OperatorWebBridgeError` record). `JsonOptions` already ignores null when writing, so `remainingAttempts` only serializes when present:
```csharp
    private sealed record OperatorWebBridgeError(
        string Code,
        string Message,
        int? RemainingAttempts);
```

(e) Add the four payload records (next to `OperatorWebSignInPayload`):
```csharp
    private sealed record OperatorWebForgotByEmailPayload(string? UserNameOrEmail);

    private sealed record OperatorWebResetByEmailPayload(string? Token, string? NewPassword);

    private sealed record OperatorWebForgotByPhonePayload(string? PhoneNumber);

    private sealed record OperatorWebResetByPhonePayload(string? PhoneNumber, string? Code, string? NewPassword);
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/AFK4.Operator.App.Tests`
Expected: PASS — full host suite green (existing + the two new reset tests).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App/Web/OperatorWebHostBridge.cs tests/AFK4.Operator.App.Tests/OperatorWebHostBridgeTests.cs
git commit -m "feat(operator-host): route password-reset bridge ops with structured errors"
```

---

## Phase D — TS bridge + auth client

### Task 6: Preserve error code + remaining attempts in the bridge

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/hostBridge.ts`
- Test: `src/AFK4.Operator.App.Web/src/hostBridge.test.ts`

- [ ] **Step 1: Write the failing test**

Append to `hostBridge.test.ts` (it already drives `window.chrome.webview` — mirror the existing helper that posts a `host:response`; this test posts an error response and asserts the rejected error carries `code` + `remainingAttempts`):
```ts
it('rejects with code and remainingAttempts from an error response', async () => {
  const listeners = new Set<(event: { data: unknown }) => void>();
  window.chrome = {
    webview: {
      postMessage: (message: unknown) => {
        const request = message as { requestId: string };
        queueMicrotask(() => {
          for (const listener of listeners) {
            listener({
              data: {
                type: 'host:response',
                requestId: request.requestId,
                ok: false,
                error: { code: 'invalid_code', message: 'bad code', remainingAttempts: 2 }
              }
            });
          }
        });
      },
      addEventListener: (_type, listener) => listeners.add(listener as (event: { data: unknown }) => void),
      removeEventListener: (_type, listener) => listeners.delete(listener as (event: { data: unknown }) => void)
    }
  };

  await expect(postHostRequest('auth:resetByPhone', {})).rejects.toMatchObject({
    code: 'invalid_code',
    remainingAttempts: 2,
    message: 'bad code'
  });
});
```
(If `hostBridge.test.ts` already defines a reusable bridge-install helper, use it instead of inlining `window.chrome`. Import `postHostRequest` is already present.)

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun test src/hostBridge.test.ts`
Expected: FAIL — the rejection is a plain `Error` (`code`/`remainingAttempts` are `undefined`).

- [ ] **Step 3: Implement**

In `hostBridge.ts`:

(a) Extend `HostBridgeError`:
```ts
export interface HostBridgeError {
  code: string;
  message: string;
  remainingAttempts?: number | null;
}
```

(b) Add the rich error class (after `HostBridgeUnavailableError`):
```ts
export class HostBridgeRequestError extends Error {
  constructor(
    message: string,
    public readonly code: string,
    public readonly remainingAttempts: number | null
  ) {
    super(message);
    this.name = 'HostBridgeRequestError';
  }
}
```

(c) In `postHostRequest`'s `onMessage`, replace the failure `reject(...)` line with:
```ts
      reject(new HostBridgeRequestError(
        response.error?.message ?? 'Native host bridge request failed.',
        response.error?.code ?? 'host_error',
        response.error?.remainingAttempts ?? null
      ));
```
(`HostBridgeRequestError extends Error`, so existing callers that read `.message` — e.g. `projectAuthHostError` — keep working unchanged.)

- [ ] **Step 4: Run the test to verify it passes**

Run: `cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun test src/hostBridge.test.ts`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/hostBridge.ts src/AFK4.Operator.App.Web/src/hostBridge.test.ts
git commit -m "feat(operator-web): preserve bridge error code and remaining attempts"
```

### Task 7: Reset functions on the auth client

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/authClient.ts`
- Test: `src/AFK4.Operator.App.Web/src/authClient.test.ts`

- [ ] **Step 1: Write the failing tests**

In `authClient.test.ts`, add (reuse the existing `installAuthBridge(respond)` helper that captures the posted message and replies):
```ts
it('requests an email reset through the bridge', async () => {
  const postMessage = installAuthBridge((message) => {
    expect(message).toMatchObject({ type: 'auth:forgotByEmail', payload: { userNameOrEmail: 'owner@demo.test' } });
    return { ok: true };
  });
  await forgotPasswordByEmail('owner@demo.test');
  expect(postMessage).toHaveBeenCalledTimes(1);
});

it('completes a phone reset through the bridge', async () => {
  const postMessage = installAuthBridge((message) => {
    expect(message).toMatchObject({
      type: 'auth:resetByPhone',
      payload: { phoneNumber: '+992937380070', code: '123456', newPassword: 'Passw0rd!New' }
    });
    return { ok: true };
  });
  await resetPasswordByPhone('+992937380070', '123456', 'Passw0rd!New');
  expect(postMessage).toHaveBeenCalledTimes(1);
});
```
Add `forgotPasswordByEmail` and `resetPasswordByPhone` to the existing import from `./authClient` at the top of the test file (alongside `signInOperator`).

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun test src/authClient.test.ts`
Expected: FAIL — `forgotPasswordByEmail is not a function`.

- [ ] **Step 3: Implement**

In `authClient.ts`, append (after `signOutOperator`):
```ts
export function forgotPasswordByEmail(userNameOrEmail: string): Promise<void> {
  return postHostRequest<void>('auth:forgotByEmail', { userNameOrEmail });
}

export function resetPasswordByEmail(token: string, newPassword: string): Promise<void> {
  return postHostRequest<void>('auth:resetByEmail', { token, newPassword });
}

export function forgotPasswordByPhone(phoneNumber: string): Promise<void> {
  return postHostRequest<void>('auth:forgotByPhone', { phoneNumber });
}

export function resetPasswordByPhone(phoneNumber: string, code: string, newPassword: string): Promise<void> {
  return postHostRequest<void>('auth:resetByPhone', { phoneNumber, code, newPassword });
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun test src/authClient.test.ts`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/authClient.ts src/AFK4.Operator.App.Web/src/authClient.test.ts
git commit -m "feat(operator-web): add staff password-reset auth-client calls"
```

---

## Phase E — Operator reset screens + login wiring

Operator's auth screens use the app's own shell classes (`operator-shell auth-shell`, `auth-panel`, `auth-form`, `auth-error`, `primary-wide`), not the Platform.Web primitives. These two screens mirror that shell so they look native, and they reuse the **shared** catalog keys M2 already added (`auth.field.login`, `auth.forgot.*`, `auth.reset.*`).

### Task 8: `ForgotPassword` screen (email + SMS channels)

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/ForgotPassword.tsx`
- Test: `src/AFK4.Operator.App.Web/src/ForgotPassword.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `ForgotPassword.test.tsx`:
```tsx
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { HostBridgeRequestError } from './hostBridge';

const forgotPasswordByEmail = mock(async () => {});
const forgotPasswordByPhone = mock(async () => {});
const resetPasswordByPhone = mock(async () => {});

mock.module('./authClient', () => ({
  forgotPasswordByEmail,
  forgotPasswordByPhone,
  resetPasswordByPhone
}));

const { ForgotPassword } = await import('./ForgotPassword');

function renderScreen() {
  return render(
    <I18nProvider>
      <ForgotPassword onBackToSignIn={() => {}} onOpenReset={() => {}} />
    </I18nProvider>
  );
}

describe('ForgotPassword (operator)', () => {
  afterEach(() => { mock.restore(); forgotPasswordByEmail.mockClear(); forgotPasswordByPhone.mockClear(); resetPasswordByPhone.mockClear(); });

  it('requests an email reset and confirms it was sent', async () => {
    renderScreen();
    fireEvent.change(screen.getByLabelText('Логин или email'), { target: { value: 'owner@demo.test' } });
    fireEvent.click(screen.getByRole('button', { name: 'Отправить код' }));
    await waitFor(() => expect(forgotPasswordByEmail).toHaveBeenCalledWith('owner@demo.test'));
    expect(await screen.findByText(/мы отправили код/i)).toBeInTheDocument();
  });

  it('runs the SMS flow and shows remaining attempts on a bad code', async () => {
    resetPasswordByPhone.mockImplementationOnce(async () => {
      throw new HostBridgeRequestError('bad', 'invalid_code', 2);
    });
    renderScreen();
    fireEvent.click(screen.getByRole('button', { name: 'По SMS' }));
    fireEvent.change(screen.getByLabelText('Номер телефона'), { target: { value: '+992937380070' } });
    fireEvent.click(screen.getByRole('button', { name: 'Получить код' }));
    fireEvent.change(await screen.findByLabelText('Код из SMS'), { target: { value: '000000' } });
    fireEvent.change(screen.getByLabelText('Новый пароль'), { target: { value: 'Passw0rd!New' } });
    fireEvent.click(screen.getByRole('button', { name: 'Сменить пароль' }));
    expect(await screen.findByText(/осталось попыток: 2/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun test src/ForgotPassword.test.tsx`
Expected: FAIL — cannot resolve `./ForgotPassword`.

- [ ] **Step 3: Implement the component**

Create `ForgotPassword.tsx`:
```tsx
import { useState, type FormEvent } from 'react';
import { AlertTriangle } from 'lucide-react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { forgotPasswordByEmail, forgotPasswordByPhone, resetPasswordByPhone } from './authClient';
import { HostBridgeRequestError } from './hostBridge';

type Channel = 'email' | 'phone';
type PhoneStep = 'request' | 'verify' | 'done';

export function ForgotPassword({
  onBackToSignIn,
  onOpenReset
}: {
  onBackToSignIn: () => void;
  onOpenReset: () => void;
}) {
  const { t } = useI18n();
  const [channel, setChannel] = useState<Channel>('email');
  const [emailLogin, setEmailLogin] = useState('');
  const [emailSent, setEmailSent] = useState(false);
  const [phone, setPhone] = useState('');
  const [phoneStep, setPhoneStep] = useState<PhoneStep>('request');
  const [code, setCode] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isBusy, setIsBusy] = useState(false);

  function selectChannel(next: Channel) {
    setChannel(next);
    setError(null);
  }

  async function submitEmail(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!emailLogin.trim()) { setError(t('auth.error.required')); return; }
    setIsBusy(true); setError(null);
    try {
      await forgotPasswordByEmail(emailLogin.trim());
      setEmailSent(true);
    } catch {
      setError(t('auth.forgot.email.error'));
    } finally {
      setIsBusy(false);
    }
  }

  async function submitPhoneRequest(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!phone.trim()) { setError(t('auth.error.required')); return; }
    setIsBusy(true); setError(null);
    try {
      await forgotPasswordByPhone(phone.trim());
      setPhoneStep('verify');
    } catch (cause) {
      setError(projectResetError(cause, t));
    } finally {
      setIsBusy(false);
    }
  }

  async function submitPhoneReset(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!code.trim() || newPassword.length < 8) { setError(t('auth.forgot.phone.error.fields')); return; }
    setIsBusy(true); setError(null);
    try {
      await resetPasswordByPhone(phone.trim(), code.trim(), newPassword);
      setPhoneStep('done');
    } catch (cause) {
      setError(projectResetError(cause, t));
    } finally {
      setIsBusy(false);
    }
  }

  return (
    <div className="operator-shell auth-shell">
      <main className="auth-workspace">
        <section className="auth-panel">
          <header>
            <span>AFK4.NET {t('op.auth.operator')}</span>
            <h1>{t('auth.forgot.title')}</h1>
            <p>{t('auth.forgot.subtitle')}</p>
          </header>

          <div className="auth-channel-toggle" role="tablist" aria-label={t('auth.forgot.subtitle')}>
            <button type="button" className={channel === 'email' ? 'primary' : ''} aria-pressed={channel === 'email'} onClick={() => selectChannel('email')}>
              {t('auth.forgot.channel.email')}
            </button>
            <button type="button" className={channel === 'phone' ? 'primary' : ''} aria-pressed={channel === 'phone'} onClick={() => selectChannel('phone')}>
              {t('auth.forgot.channel.phone')}
            </button>
          </div>

          {channel === 'email' && (emailSent ? (
            <section className="auth-confirm">
              <p>{t('auth.forgot.email.sent')}</p>
              <button type="button" className="primary-wide" onClick={onOpenReset}>{t('auth.forgot.email.openReset')}</button>
              <button type="button" className="auth-link" onClick={onBackToSignIn}>{t('auth.forgot.back')}</button>
            </section>
          ) : (
            <form className="auth-form" onSubmit={submitEmail}>
              <label>
                {t('auth.forgot.email.field')}
                <input value={emailLogin} onChange={(e) => setEmailLogin(e.currentTarget.value)} autoComplete="username" disabled={isBusy} autoFocus />
              </label>
              <button type="submit" className="primary-wide" disabled={isBusy}>
                {isBusy ? t('auth.forgot.email.submitting') : t('auth.forgot.email.submit')}
              </button>
            </form>
          ))}

          {channel === 'phone' && (phoneStep === 'done' ? (
            <section className="auth-confirm">
              <p>{t('auth.forgot.phone.done')}</p>
              <button type="button" className="primary-wide" onClick={onBackToSignIn}>{t('auth.forgot.phone.toSignIn')}</button>
            </section>
          ) : phoneStep === 'verify' ? (
            <form className="auth-form" onSubmit={submitPhoneReset}>
              <p className="auth-hint">{t('auth.forgot.phone.sent')}</p>
              <label>
                {t('auth.forgot.phone.codeField')}
                <input value={code} onChange={(e) => setCode(e.currentTarget.value)} inputMode="numeric" autoComplete="one-time-code" disabled={isBusy} />
              </label>
              <label>
                {t('auth.forgot.phone.newPassword')}
                <input type="password" value={newPassword} onChange={(e) => setNewPassword(e.currentTarget.value)} autoComplete="new-password" disabled={isBusy} />
              </label>
              <button type="submit" className="primary-wide" disabled={isBusy}>
                {isBusy ? t('auth.forgot.phone.resetting') : t('auth.forgot.phone.reset')}
              </button>
            </form>
          ) : (
            <form className="auth-form" onSubmit={submitPhoneRequest}>
              <label>
                {t('auth.forgot.phone.field')}
                <input type="tel" value={phone} onChange={(e) => setPhone(e.currentTarget.value)} inputMode="tel" autoComplete="tel" disabled={isBusy} />
              </label>
              <button type="submit" className="primary-wide" disabled={isBusy}>
                {isBusy ? t('auth.forgot.phone.submitting') : t('auth.forgot.phone.submit')}
              </button>
            </form>
          ))}

          {error && (
            <div className="auth-error" role="alert">
              <AlertTriangle size={16} />
              <span>{error}</span>
            </div>
          )}

          <button type="button" className="auth-link" onClick={onBackToSignIn}>{t('auth.forgot.back')}</button>
        </section>
      </main>
    </div>
  );
}

function projectResetError(cause: unknown, t: (key: MessageKey) => string): string {
  if (cause instanceof HostBridgeRequestError) {
    switch (cause.code) {
      case 'invalid_phone':
        return t('auth.forgot.phone.error.invalidPhone');
      case 'invalid_code':
        return cause.remainingAttempts === null
          ? t('auth.forgot.phone.error.invalidCode')
          : `${t('auth.forgot.phone.error.invalidCode')} ${t('auth.forgot.phone.error.remaining')}: ${cause.remainingAttempts}`;
      case 'code_expired':
        return t('auth.forgot.phone.error.expired');
      case 'too_many_attempts':
        return t('auth.forgot.phone.error.tooMany');
      default:
        return t('auth.forgot.phone.error.generic');
    }
  }
  return t('auth.forgot.phone.error.generic');
}
```
(This reuses M2's shared `auth.forgot.*` keys verbatim, including the remaining-attempts concat, so Operator and Platform.Web behave identically. The one Operator-only string, `op.auth.operator` = «Оператор», is added in Task 10.)

- [ ] **Step 4: Run the test to verify it passes**

Run: `cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun test src/ForgotPassword.test.tsx`
Expected: PASS. (Requires `op.auth.operator` from Task 10; if running this task first, temporarily inline «Оператор» and switch to `t('op.auth.operator')` after Task 10 — or do Task 10 before Step 4.)

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/ForgotPassword.tsx src/AFK4.Operator.App.Web/src/ForgotPassword.test.tsx
git commit -m "feat(operator-web): channel-aware forgot-password screen"
```

### Task 9: `ResetPassword` screen (pasted email code)

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/ResetPassword.tsx`
- Test: `src/AFK4.Operator.App.Web/src/ResetPassword.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `ResetPassword.test.tsx`:
```tsx
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { HostBridgeRequestError } from './hostBridge';

const resetPasswordByEmail = mock(async () => {});
mock.module('./authClient', () => ({ resetPasswordByEmail }));

const { ResetPassword } = await import('./ResetPassword');

describe('ResetPassword (operator)', () => {
  afterEach(() => { mock.restore(); resetPasswordByEmail.mockClear(); });

  it('submits the pasted code and new password', async () => {
    render(<I18nProvider><ResetPassword onBackToSignIn={() => {}} /></I18nProvider>);
    fireEvent.change(screen.getByLabelText('Код из письма'), { target: { value: 'tok.en' } });
    fireEvent.change(screen.getByLabelText('Новый пароль'), { target: { value: 'Passw0rd!New' } });
    fireEvent.click(screen.getByRole('button', { name: 'Сменить пароль' }));
    await waitFor(() => expect(resetPasswordByEmail).toHaveBeenCalledWith('tok.en', 'Passw0rd!New'));
    expect(await screen.findByText(/Пароль изменён/)).toBeInTheDocument();
  });

  it('shows an invalid-link error when the code is rejected', async () => {
    resetPasswordByEmail.mockImplementationOnce(async () => { throw new HostBridgeRequestError('bad', 'reset_failed', null); });
    render(<I18nProvider><ResetPassword onBackToSignIn={() => {}} /></I18nProvider>);
    fireEvent.change(screen.getByLabelText('Код из письма'), { target: { value: 'bad' } });
    fireEvent.change(screen.getByLabelText('Новый пароль'), { target: { value: 'Passw0rd!New' } });
    fireEvent.click(screen.getByRole('button', { name: 'Сменить пароль' }));
    expect(await screen.findByText(/недействительна или устарела/)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun test src/ResetPassword.test.tsx`
Expected: FAIL — cannot resolve `./ResetPassword`.

- [ ] **Step 3: Implement the component**

Create `ResetPassword.tsx`:
```tsx
import { useState, type FormEvent } from 'react';
import { AlertTriangle } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import { resetPasswordByEmail } from './authClient';

export function ResetPassword({ onBackToSignIn }: { onBackToSignIn: () => void }) {
  const { t } = useI18n();
  const [token, setToken] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [done, setDone] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isBusy, setIsBusy] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!token.trim() || newPassword.length < 8) { setError(t('auth.reset.error.fields')); return; }
    setIsBusy(true); setError(null);
    try {
      await resetPasswordByEmail(token.trim(), newPassword);
      setDone(true);
    } catch {
      setError(t('auth.reset.error.invalid'));
    } finally {
      setIsBusy(false);
    }
  }

  return (
    <div className="operator-shell auth-shell">
      <main className="auth-workspace">
        <section className="auth-panel">
          <header>
            <span>AFK4.NET {t('op.auth.operator')}</span>
            <h1>{t('auth.reset.title')}</h1>
            <p>{t('auth.reset.subtitle')}</p>
          </header>

          {done ? (
            <section className="auth-confirm">
              <p>{t('auth.reset.success')}</p>
              <button type="button" className="primary-wide" onClick={onBackToSignIn}>{t('auth.reset.toSignIn')}</button>
            </section>
          ) : (
            <form className="auth-form" onSubmit={handleSubmit}>
              <label>
                {t('auth.reset.field.token')}
                <input value={token} onChange={(e) => setToken(e.currentTarget.value)} autoFocus disabled={isBusy} />
              </label>
              <label>
                {t('auth.reset.field.newPassword')}
                <input type="password" value={newPassword} onChange={(e) => setNewPassword(e.currentTarget.value)} autoComplete="new-password" disabled={isBusy} />
              </label>
              <button type="submit" className="primary-wide" disabled={isBusy}>
                {isBusy ? t('auth.reset.action.submitting') : t('auth.reset.action.submit')}
              </button>
            </form>
          )}

          {error && (
            <div className="auth-error" role="alert">
              <AlertTriangle size={16} />
              <span>{error}</span>
            </div>
          )}

          <button type="button" className="auth-link" onClick={onBackToSignIn}>{t('auth.reset.back')}</button>
        </section>
      </main>
    </div>
  );
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun test src/ResetPassword.test.tsx`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/ResetPassword.tsx src/AFK4.Operator.App.Web/src/ResetPassword.test.tsx
git commit -m "feat(operator-web): token-based reset-password screen"
```

### Task 10: One Operator-only auth string + CSS for the new auth bits

**Files:**
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json` (+ `bun run gen`)
- Modify: `src/AFK4.Operator.App.Web/src/styles.css`

- [ ] **Step 1: Add `op.auth.operator` to all three locales**

`locales/ru.json`: `"op.auth.operator": "Оператор"`
`locales/en.json`: `"op.auth.operator": "Operator"`
`locales/tg.json`: `"op.auth.operator": "Оператор"`

Run: `cd packages/i18n && ~/.bun/bin/bun run gen && ~/.bun/bin/bun test`
Expected: PASS (parity + voice green).

- [ ] **Step 2: Add styles for the new auth elements**

Append to `styles.css` (reusing existing auth tokens; values match the existing auth shell — verify against the current `.auth-panel`/`.auth-form` rules and adjust to match):
```css
.auth-channel-toggle { display: flex; gap: 8px; margin: 16px 0; }
.auth-channel-toggle button { flex: 1; }
.auth-confirm { display: flex; flex-direction: column; gap: 12px; }
.auth-hint { opacity: 0.7; }
.auth-link { background: none; border: 0; color: inherit; opacity: 0.75; cursor: default; text-align: left; padding: 8px 0; }
.auth-link:hover { opacity: 1; }
.auth-link:focus-visible { outline: 2px solid var(--focus, currentColor); outline-offset: 2px; }
```

- [ ] **Step 3: Commit**

```bash
git add locales/ru.json locales/en.json locales/tg.json packages/i18n/src/messages.ts src/AFK4.Operator.App.Web/src/styles.css
git commit -m "feat(operator-web): auth-screen string and styles for reset flow"
```

### Task 11: Wire login relabel, forgot link, and auth sub-view routing

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/App.tsx`
- Test: `src/AFK4.Operator.App.Web/src/App.test.tsx`

- [ ] **Step 1: Relabel `SignInScreen` and add the forgot link**

In `App.tsx`, `SignInScreen` currently hardcodes Russian. Make it use `useI18n` and add an `onForgotPassword` prop. Change the component signature to accept `onForgotPassword: () => void`, add `const { t } = useI18n();` at the top of the component, then:

- Replace the `Пользователь` label text with `{t('auth.field.login')}` (now "Логин или email").
- Replace the `Пароль` label text with `{t('auth.field.password')}`.
- Replace the submit button text `{isBusy ? 'Проверяем' : 'Войти'}` with `{isBusy ? t('auth.action.signingIn') : t('auth.action.signIn')}`.
- Replace the validation strings: `'Укажите имя пользователя.'`/`'Укажите пароль.'`/the org-not-configured string with catalog keys (`auth.error.required` for the first two; add `op.auth.connectionMissing` for the org one — see Step 4).
- After the sign-in `</form>`, add:
```tsx
            <button type="button" className="auth-link" onClick={onForgotPassword}>
              {t('auth.forgot.link')}
            </button>
```
- Also localize the screen's other shell strings (`Оператор`, `Вход оператора`, the storage/platform/currency aside, status text). These are part of the Phase F shell migration — either localize them here with `op.auth.*` keys added in Step 4, or leave them for Task (App shell) in Phase F. To keep this task focused on the *flow*, localize at minimum the field labels, button, validation, and the forgot link; defer the decorative aside copy to Phase F.

- [ ] **Step 2: Add the auth sub-view state and render the screens**

Operator has no URL router; model the auth sub-view with state. Near the other `App` state (around `const [authStatus, ...]`), add:
```tsx
  const [authView, setAuthView] = useState<'signIn' | 'forgot' | 'reset'>('signIn');
```
Add imports near the top component imports:
```tsx
import { ForgotPassword } from './ForgotPassword';
import { ResetPassword } from './ResetPassword';
```
Replace the unauthenticated render branch (`if (authStatus !== 'signed-in' || authSession === null) { return (<SignInScreen .../>); }`) with:
```tsx
  if (authStatus !== 'signed-in' || authSession === null) {
    if (authView === 'forgot') {
      return (
        <ForgotPassword
          onBackToSignIn={() => setAuthView('signIn')}
          onOpenReset={() => setAuthView('reset')}
        />
      );
    }
    if (authView === 'reset') {
      return <ResetPassword onBackToSignIn={() => setAuthView('signIn')} />;
    }
    return (
      <SignInScreen
        config={config}
        authStatus={authStatus}
        hostError={authError}
        onSignIn={handleSignIn}
        onForgotPassword={() => setAuthView('forgot')}
      />
    );
  }
```

- [ ] **Step 3: Extend the App.test bridge fake and add a flow test**

In `App.test.tsx`, `installSessionBridge` builds the fake `window.chrome.webview`. Add reset ops so the screens don't error if called. After the `auth:signOut` block, add:
```ts
        if (request.type === 'auth:forgotByEmail' || request.type === 'auth:resetByEmail'
          || request.type === 'auth:forgotByPhone' || request.type === 'auth:resetByPhone') {
          payload = { ok: true };
        }
```
Add a wiring test (mirror the existing sign-in render tests; assert the forgot screen opens from the link):
```ts
it('opens the forgot-password screen from the sign-in link', async () => {
  installSessionBridge(null); // start signed-out
  render(<I18nProvider><App /></I18nProvider>);
  fireEvent.click(await screen.findByRole('button', { name: 'Забыли пароль?' }));
  expect(await screen.findByRole('button', { name: 'По SMS' })).toBeInTheDocument();
});
```
(Match the file's existing render helper — if `App.test.tsx` already wraps `<App/>` in a provider via a helper, use that instead of inlining `<I18nProvider>`.)

- [ ] **Step 4: Add the `op.auth.*` keys this task introduces**

Add to all three locales (+ `bun run gen`):

`ru` / `en` / `tg`:
- `op.auth.connectionMissing` = «Подключение клуба не настроено. Смените подключение и повторите вход.» / "Club connection isn't set up. Change the connection and sign in again." / «Пайвасти клуб танзим нашудааст. Пайвастро иваз кунед ва аз нав ворид шавед.»

(Any other `op.auth.*` keys you localize in Step 1 — e.g. `op.auth.signInTitle` for «Вход оператора» — add here too with real ru/en/tg.)

- [ ] **Step 5: Verify the suite**

Run: `cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun x tsc -b && ~/.bun/bin/bun test`
Expected: PASS — including the new flow test and existing `App.test.tsx`.

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/App.tsx src/AFK4.Operator.App.Web/src/App.test.tsx locales/ru.json locales/en.json locales/tg.json packages/i18n/src/messages.ts
git commit -m "feat(operator-web): email-login label, forgot link, reset screen routing"
```

---

## Phase F — Bulk migration: hardcoded Operator copy → ICU catalog

This phase removes the harmful pattern (hardcoded RU strings, `pluralRu` fragments, in-component concatenation) across the remaining Operator components. **Test files (`*.test.tsx`) are not migration targets** — they assert on the rendered Russian text, which the default `ru` locale still produces; touch a test only if a component's structure changes. The work is per-file; delegate each file to a Sonnet subagent using the recipe below, with a review checkpoint per file.

### The migration recipe (apply to each component file)

1. **Add `const { t } = useI18n();`** (import `useI18n` from `@afk4/i18n`). For string-building helpers in `operatorHelpers.ts` that aren't components, pass `t` in as a parameter from the calling component (do not import the hook outside React).
2. **Extract every user-facing RU string** into a catalog key. Namespace by area: `op.<area>.<name>` (e.g. `op.dashboard.title`, `op.pos.empty`, `op.map.offline`). **Reuse** an existing `common.*` key when one already means exactly this (`common.save`, `common.cancel`); never invent a second key for an existing concept (terminology lock).
3. **Replace interpolation and pluralization with ICU**, never concat + `pluralRu`:
   - Variables → one ICU message with `{name}` placeholders, called `t('op.x', { name })`.
   - Counts → one ICU `{count, plural, …}` message with the language's real forms. **Delete the `pluralRu` call.**
4. **Add real ru / en / tg** for every new key to `locales/{ru,en,tg}.json` (real Tajik, not a ru copy). Respect the voice guard: no Cyrillic ALL-CAPS 4+, «ПК» not «компьютер».
5. **Regenerate** (`cd packages/i18n && ~/.bun/bin/bun run gen`).
6. **Verify** the file's own test + the catalog guards, typecheck, then commit.
7. **Do not change behavior** — only move strings. Dates/money keep going through `@afk4/money` / `@afk4/formatting`; ICU handles only text interpolation and plurals.

### Task 12: Worked example — `DashboardWorkspace.tsx`

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/DashboardWorkspace.tsx`
- Modify: `locales/{ru,en,tg}.json` (+ `bun run gen`)
- Test: `src/AFK4.Operator.App.Web/src/DashboardWorkspace.test.tsx` (if present; else rely on `App.test.tsx`)

This is the template every other Phase F task follows. `op.dashboard.signals` already exists (Task 1).

- [ ] **Step 1: Add the Dashboard keys to all three locales**

Representative subset (add the full set the file needs — the pattern is identical). `locales/ru.json`:
```json
  "op.dashboard.title": "Обзор",
  "op.dashboard.heading": "Что требует внимания · {range}",
  "op.dashboard.period.today": "Сегодня",
  "op.dashboard.period.week": "Неделя",
  "op.dashboard.period.month": "Месяц",
  "op.dashboard.range.today": "сегодня",
  "op.dashboard.range.week": "за неделю",
  "op.dashboard.range.month": "за месяц",
  "op.dashboard.range.custom": "за выбранный период",
  "op.dashboard.days": "{count, plural, one {# день} few {# дня} many {# дней} other {# дня}}",
  "op.dashboard.export": "Экспорт",
  "op.dashboard.metric.cash": "Касса",
  "op.dashboard.metric.activePcs": "Активные ПК",
  "op.dashboard.metric.attention": "Внимание",
  "op.dashboard.metric.bookings": "Брони",
  "op.dashboard.status.backend": "Данные платформы",
  "op.dashboard.status.loading": "Загрузка данных",
  "op.dashboard.status.error": "Ошибка данных"
```
`locales/en.json`:
```json
  "op.dashboard.title": "Overview",
  "op.dashboard.heading": "What needs attention · {range}",
  "op.dashboard.period.today": "Today",
  "op.dashboard.period.week": "Week",
  "op.dashboard.period.month": "Month",
  "op.dashboard.range.today": "today",
  "op.dashboard.range.week": "this week",
  "op.dashboard.range.month": "this month",
  "op.dashboard.range.custom": "for the selected period",
  "op.dashboard.days": "{count, plural, one {# day} other {# days}}",
  "op.dashboard.export": "Export",
  "op.dashboard.metric.cash": "Cash",
  "op.dashboard.metric.activePcs": "Active PCs",
  "op.dashboard.metric.attention": "Attention",
  "op.dashboard.metric.bookings": "Bookings",
  "op.dashboard.status.backend": "Platform data",
  "op.dashboard.status.loading": "Loading data",
  "op.dashboard.status.error": "Data error"
```
`locales/tg.json`:
```json
  "op.dashboard.title": "Шарҳ",
  "op.dashboard.heading": "Он чи диққат металабад · {range}",
  "op.dashboard.period.today": "Имрӯз",
  "op.dashboard.period.week": "Ҳафта",
  "op.dashboard.period.month": "Моҳ",
  "op.dashboard.range.today": "имрӯз",
  "op.dashboard.range.week": "дар ҳафта",
  "op.dashboard.range.month": "дар моҳ",
  "op.dashboard.range.custom": "барои давраи интихобшуда",
  "op.dashboard.days": "{count, plural, one {# рӯз} other {# рӯз}}",
  "op.dashboard.export": "Содирот",
  "op.dashboard.metric.cash": "Хазина",
  "op.dashboard.metric.activePcs": "ПК-ҳои фаъол",
  "op.dashboard.metric.attention": "Диққат",
  "op.dashboard.metric.bookings": "Брон",
  "op.dashboard.status.backend": "Маълумоти платформа",
  "op.dashboard.status.loading": "Боргирии маълумот",
  "op.dashboard.status.error": "Хатои маълумот"
```

- [ ] **Step 2: Replace the strings in `DashboardWorkspace.tsx`**

Examples of the exact transformation (apply the same to every remaining string in the file):

- `<span>Обзор</span>` → `<span>{t('op.dashboard.title')}</span>`
- `<h1>Что требует внимания · {activeRange.label}</h1>` → `<h1>{t('op.dashboard.heading', { range: activeRange.label })}</h1>` (and `activeRange.label` becomes a `t('op.dashboard.range.*')` value, not a literal).
- Period buttons `Сегодня`/`Неделя`/`Месяц` → `t('op.dashboard.period.today'|week|month)`.
- The plural+concat `` `${activeDays} дн.` `` and `pluralRu(attentionCount, ['сигнал','сигнала','сигналов'])` → `t('op.dashboard.days', { count: activeDays })` and `t('op.dashboard.signals', { count: attentionCount })`. **Remove the `pluralRu` import** from this file.
- `dashboardStatusText` ternary → `t('op.dashboard.status.backend'|loading|error)`.
- Metric labels `Касса`/`Активные ПК`/`Внимание`/`Брони` → `t('op.dashboard.metric.*')`.

`presetRanges` labels move to keys too:
```tsx
  const presetRanges = {
    today: { from: todayInput, to: todayInput, label: t('op.dashboard.range.today'), metricLabel: t('op.dashboard.range.today') },
    week: { from: weekStartInput, to: todayInput, label: t('op.dashboard.range.week'), metricLabel: t('op.dashboard.range.week') },
    month: { from: monthStartInput, to: todayInput, label: t('op.dashboard.range.month'), metricLabel: t('op.dashboard.range.month') }
  };
```

- [ ] **Step 3: Regenerate, typecheck, test**

Run: `cd packages/i18n && ~/.bun/bin/bun run gen && cd ../../src/AFK4.Operator.App.Web && ~/.bun/bin/bun x tsc -b && ~/.bun/bin/bun test`
Expected: PASS — catalog guards green, Operator tests green (the rendered ru text is identical to before, so assertions still match).

- [ ] **Step 4: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/DashboardWorkspace.tsx locales/*.json packages/i18n/src/messages.ts
git commit -m "i18n(operator): localize DashboardWorkspace"
```

### Tasks 13–24: Remaining component files (same recipe)

Each task = apply the recipe to one file, with its own `op.<area>.*` namespace, real ru/en/tg, `bun run gen`, that file's test (or `App.test.tsx`) green, then commit `i18n(operator): localize <File>`. Delegate each to a subagent; review the diff + the new catalog entries (especially tg quality and any plural/interpolation) before accepting.

- [ ] **Task 13:** `MapWorkspace.tsx` — namespace `op.map.*`.
- [ ] **Task 14:** `MapSidePanel.tsx` — namespace `op.map.*` (seat/session detail; watch the `pluralRu`/concat seat-count strings → ICU).
- [ ] **Task 15:** `SummarySidePanel.tsx` — namespace `op.summary.*`.
- [ ] **Task 16:** `ReviewWorkspace.tsx` — namespace `op.review.*`.
- [ ] **Task 17:** `operatorPrimitives.tsx` — namespace `op.common.*` (shared notice/feedback bits; small).
- [ ] **Task 18:** `BackendPosWorkspace.tsx` — namespace `op.pos.*` (large, ~170 strings; many money labels — keep `@afk4/money` formatting, localize only text).
- [ ] **Task 19:** `BackendPaymentsWorkspace.tsx` — namespace `op.payments.*`.
- [ ] **Task 20:** `BackendPlayersWorkspace.tsx` — namespace `op.players.*` (reuse `clients.*` keys where the concept already exists in the catalog).
- [ ] **Task 21:** `BackendBookingWorkspace.tsx` — namespace `op.booking.*`.
- [ ] **Task 22:** `BackendLogsWorkspace.tsx` — namespace `op.logs.*` (reuse `journal.*` where identical).
- [ ] **Task 23:** `BackendSettingsWorkspace.tsx` — namespace `op.settings.*` (largest, ~381 strings; reuse existing `settings.*`/`branches.*`/`tariffs.*`/`products.*` keys where the catalog already covers the concept — check `messages.test.ts` for the existing key families before inventing new ones).
- [ ] **Task 24:** `App.tsx` shell + `operatorHelpers.ts` — namespace `op.shell.*` / `op.status.*`. The shell strings (top bar `Оператор`, search placeholder «Игрок, ПК, команда», `Выйти`, nav rail labels, the signals-strip footer with its `ПК без связи: {n}` / `требуют внимания: {n}` counts → ICU with `{count}`), the `operatorHelpers.ts` string builders (`projectAuthHostError`, the machine-status string at line ~598, dashboard focus labels) take `t` as a parameter. **Delete `pluralRu` from `operatorHelpers.ts`** once no caller remains; if any non-component caller needs a plural, convert it to a `t(key,{count})` at the component boundary.

After each task, run `cd packages/i18n && ~/.bun/bin/bun test` (catalog parity/voice) plus the Operator suite for the touched file.

### Task 25: Sweep for stragglers and remove `pluralRu`

**Files:** various.

- [ ] **Step 1: Find remaining hardcoded Cyrillic in non-test source**

Run (PowerShell): use Grep for `[А-Яа-яЁё]` across `src/AFK4.Operator.App.Web/src/*.tsx` and `*.ts` excluding `*.test.*`. Anything left in a user-facing position is a straggler — migrate it with the recipe. Comments and dev-only strings can stay.

- [ ] **Step 2: Confirm `pluralRu` is gone**

Run: Grep for `pluralRu` in `src/AFK4.Operator.App.Web/src`. Expected: only its definition remains, with no callers → delete the definition. If callers remain, finish migrating them first.

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "i18n(operator): remove pluralRu and migrate remaining strings"
```

---

## Phase G — Full verification

### Task 26: All gates green

**Files:** none (verification only).

- [ ] **Step 1: i18n package (engine + catalog guards)**

Run: `cd packages/i18n && ~/.bun/bin/bun test`
Expected: PASS — ICU engine tests, parity (ru/en/tg identical key sets), generated-matches-source, voice guard.

- [ ] **Step 2: Backend host suite**

Run: `dotnet test tests/AFK4.Operator.App.Tests`
Expected: PASS — including the four reset bridge ops + structured-error test.

- [ ] **Step 3: Operator web — tests + typecheck + build**

Run: `cd src/AFK4.Operator.App.Web && ~/.bun/bin/bun test && ~/.bun/bin/bun x tsc -b && ~/.bun/bin/bun run build`
Expected: tests PASS, `tsc -b` clean, `vite build` succeeds.

- [ ] **Step 4: Backward-compat — the other two frontends still green**

Run: `cd src/AFK4.Platform.Web && ~/.bun/bin/bun test && ~/.bun/bin/bun x tsc -b`
Run (if present): `cd src/AFK4.Customer.Web && ~/.bun/bin/bun test && ~/.bun/bin/bun x tsc -b`
Expected: PASS — the ICU `t()` change is backward-compatible.

- [ ] **Step 5: Full backend suite (sanity — M3 changed no platform API)**

Run: `dotnet test tests/AFK4.Platform.Api.Tests`
Expected: PASS (unchanged from M1+M2; confirms nothing regressed).

- [ ] **Step 6: Commit any fixups**

```bash
git add -A
git commit -m "test(email-parity): green M3 across host, operator-web, and i18n"
```

---

## Self-review notes (for the executor)

- **No fake translations.** Every new key has real ru/en/tg (tg authored, not copied). The parity test passing means the keys exist *and are real* — do not satisfy it with ru copies.
- **ICU is the engine, not the exception.** Any string with a count or a variable is one ICU message; `pluralRu` and in-JSX concatenation of pluralized words are removed, not migrated.
- **Reuse beats invent.** Operator reset screens reuse M2's shared `auth.*` keys verbatim. Bulk migration reuses existing `common.*`/`settings.*`/`clients.*`/`journal.*`/`reports.*` families where the concept already exists — check `messages.test.ts` for the families before adding `op.*` duplicates.
- **Structured errors reach the user (#34).** The SMS reset path carries the backend `code` + `remainingAttempts` through the host bridge to the screen — no generic "что-то пошло не так".
- **Backward compatibility.** The `t(key, values?)` signature is additive; Platform.Web/Customer call sites (`t(key)`) are untouched and verified in Phase G Step 4.
- **Type consistency.** Bridge: `HostBridgeRequestError { code, remainingAttempts }` (D) is what `ForgotPassword.projectResetError` (E) switches on. Host: `OperatorAuthApiException { Code, RemainingAttempts }` (C) maps to `OperatorWebBridgeError(Code, Message, RemainingAttempts)` (C). Auth client fns `forgotPasswordByEmail`/`resetPasswordByEmail`/`forgotPasswordByPhone`/`resetPasswordByPhone` (D) are consumed by the screens (E).
- **Desktop has no reset URL.** The email channel tells the user a code was sent; they open the reset screen and paste the code (no `?token=`), unlike Platform.Web.
- **Default locale `ru`.** Operator is Russian-first; the provider defaults to ru and persists per-machine. Tests render under ru, so existing Russian assertions keep matching after migration.
