# Staff Phone Verification UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a logged-in staff member set and SMS-verify their own phone number, self-service, in both admin frontends — so phone-login (Phase C) and SMS reset (Phase D) become reachable.

**Architecture:** The verification *flow* (start/confirm) already exists on the backend (PR #56), acting on the caller's own phone via the Bearer token. We add one read endpoint (`GET /api/auth/staff/phone`) so a card can render current state, then build a self-service "phone" card in `AFK4.Platform.Web`'s existing `ProfileScreen` (shadcn/ui) and the same flow as a header-opened `AccountPanel` in `AFK4.Operator.App.Web` (desktop app). i18n strings are shared via `locales/{ru,en,tg}.json` → `@afk4/i18n`.

**Tech Stack:** .NET 10 Minimal API + EF Core (backend); React 19 + TypeScript; `AFK4.Platform.Web` uses shadcn/ui + Vite; `AFK4.Operator.App.Web` is hand-rolled CSS in a WebView2 host; both test with `bun test` (`@testing-library/react`, happy-dom). i18n: `@afk4/i18n` (key-only `t()`, **no interpolation**).

**Branch:** `feature/staff-phone-verify-ui` (already created).

**Run notes:**
- bun binary: `~/.bun/bin/bun` (use `bun`/`bunx` below; substitute the full path if `bun` is not on PATH).
- Do not run `cd` inside the Bash tool; pass the working dir in the command or use the per-task "Run from" note.

---

## Shared API contract (both frontends call these — exact shapes)

All responses are camelCase JSON (ASP.NET web defaults).

| Method + path | Request body | Success response |
|---|---|---|
| `GET /api/auth/staff/phone` *(new, this plan)* | — | `{ "phone": string\|null, "phoneVerifiedAtUtc": string\|null }` (200 always; both null when no phone) |
| `POST /api/auth/staff/phone/start-verification` *(exists)* | `{ "phone": string }` | `{ "expiresInSeconds": number, "resendAfterSeconds": number }` |
| `POST /api/auth/staff/phone/confirm` *(exists)* | `{ "code": string }` | `{ "phone": string }` |

Error responses carry `{ "error": "<code>", ... }`. Codes: `invalid_phone`, `cooldown_active`, `rate_limited`, `sms_unavailable` (start); `invalid_code` (+`remainingAttempts`), `code_expired`, `no_active_code`, `too_many_attempts`, `phone_already_in_use` (confirm).

---

## File structure

**Backend (`src/AFK4.Platform.Api`, `src/AFK4.Shared.Contracts`):**
- Create: `src/AFK4.Shared.Contracts/Identity/StaffPhoneStatusResponse.cs` — read DTO.
- Modify: `src/AFK4.Platform.Api/Identity/IStaffPhoneVerificationService.cs` — add `GetStatusAsync` + result record.
- Modify: `src/AFK4.Platform.Api/Identity/EfStaffPhoneVerificationService.cs` — implement `GetStatusAsync`.
- Modify: `src/AFK4.Platform.Api/Program.cs` — add `GET /api/auth/staff/phone` endpoint (after the confirm endpoint, ~line 768).
- Test: `tests/AFK4.Platform.Api.Tests/StaffPhoneVerificationEndpointTests.cs` — add GET cases.

**i18n (shared):**
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json` — add `account.phone.*` keys.
- Generated: `packages/i18n/src/messages.ts` via `bun run gen`.

**Platform.Web (`src/AFK4.Platform.Web/src`):**
- Modify: `api/clubApi.ts` — add 3 phone methods to `ClubApiClient`.
- Create: `club/profile/PhoneVerificationCard.tsx` — self-service card (shadcn).
- Create: `club/profile/PhoneVerificationCard.test.tsx` — component test.
- Modify: `club/profile/ProfileScreen.tsx` — accept `client`, render the card.
- Modify: `club/profile/ProfileScreen.test.tsx` — pass fake client + ToastProvider.
- Modify: `App.tsx` — pass `client={clubClient}` to `<ProfileScreen>` (~line 470).

**Operator.App.Web (`src/AFK4.Operator.App.Web/src`):**
- Modify: `operatorApiClients.ts` — add `account` client group + DTOs.
- Create: `PhoneVerificationCard.tsx` — self-service card (hand-rolled).
- Create: `PhoneVerificationCard.test.tsx` — component test.
- Create: `AccountPanel.tsx` — modal wrapper opened from the header.
- Modify: `App.tsx` — header identity becomes a button that opens `AccountPanel`.
- Modify: `styles.css` — panel + card styles.

---

## Phase 1 — Backend: read endpoint

### Task 1: `GET /api/auth/staff/phone`

**Files:**
- Create: `src/AFK4.Shared.Contracts/Identity/StaffPhoneStatusResponse.cs`
- Modify: `src/AFK4.Platform.Api/Identity/IStaffPhoneVerificationService.cs`
- Modify: `src/AFK4.Platform.Api/Identity/EfStaffPhoneVerificationService.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs`
- Test: `tests/AFK4.Platform.Api.Tests/StaffPhoneVerificationEndpointTests.cs`

- [ ] **Step 1: Write the failing tests**

Add these three tests to `tests/AFK4.Platform.Api.Tests/StaffPhoneVerificationEndpointTests.cs` inside the existing `StaffPhoneVerificationEndpointTests` class (after `StartVerification_WithoutBearer_ReturnsUnauthorized`). Add `using System.Net.Http.Json;` is already present.

```csharp
    [Fact]
    public async Task GetPhone_BeforeVerification_ReturnsNulls()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Technician);

        var response = await client.GetAsync("/api/auth/staff/phone");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await response.Content.ReadFromJsonAsync<StaffPhoneStatusResponse>();
        Assert.NotNull(status);
        Assert.Null(status!.Phone);
        Assert.Null(status.PhoneVerifiedAtUtc);
    }

    [Fact]
    public async Task GetPhone_WithoutBearer_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/auth/staff/phone");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetPhone_AfterVerification_ReturnsVerifiedPhone()
    {
        var recording = new RecordingSmsTransport();
        await using var factory = new PlatformApiFactory(extraServices: services =>
        {
            services.RemoveAll<ISmsTransport>();
            services.AddSingleton<ISmsTransport>(recording);
        });
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Technician);

        await client.PostAsJsonAsync(
            "/api/auth/staff/phone/start-verification",
            new StaffPhoneStartVerificationRequest("+992 93 738-00-70"));
        var code = Regex.Match(Assert.Single(recording.Sent).Text, "\\d{6}").Value;
        await client.PostAsJsonAsync("/api/auth/staff/phone/confirm", new StaffPhoneConfirmRequest(code));

        var response = await client.GetAsync("/api/auth/staff/phone");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await response.Content.ReadFromJsonAsync<StaffPhoneStatusResponse>();
        Assert.NotNull(status);
        Assert.Equal("+992937380070", status!.Phone);
        Assert.NotNull(status.PhoneVerifiedAtUtc);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~StaffPhoneVerificationEndpointTests"`
Expected: FAIL — `StaffPhoneStatusResponse` does not exist (compile error).

- [ ] **Step 3: Create the contract**

Create `src/AFK4.Shared.Contracts/Identity/StaffPhoneStatusResponse.cs`:

```csharp
namespace AFK4.Shared.Contracts.Identity;

/// <summary>Current staff member's phone state (self-read). Both null until a phone is set/verified.</summary>
public sealed record StaffPhoneStatusResponse(string? Phone, DateTimeOffset? PhoneVerifiedAtUtc);
```

- [ ] **Step 4: Add the service method to the interface**

In `src/AFK4.Platform.Api/Identity/IStaffPhoneVerificationService.cs`, add a result record after `PhoneConfirmResult` (line ~28) and a method to the interface:

```csharp
public sealed record StaffPhoneStatus(string? Phone, DateTimeOffset? PhoneVerifiedAtUtc);
```

Then inside `public interface IStaffPhoneVerificationService` add:

```csharp
    Task<StaffPhoneStatus> GetStatusAsync(Guid staffUserId, CancellationToken cancellationToken);
```

- [ ] **Step 5: Implement the service method**

In `src/AFK4.Platform.Api/Identity/EfStaffPhoneVerificationService.cs`, add this method inside the class (e.g. right after the `ConfirmAsync` method):

```csharp
    public async Task<StaffPhoneStatus> GetStatusAsync(Guid staffUserId, CancellationToken cancellationToken)
    {
        var status = await db.StaffUsers
            .AsNoTracking()
            .Where(user => user.StaffUserId == staffUserId)
            .Select(user => new StaffPhoneStatus(user.Phone, user.PhoneVerifiedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);

        return status ?? new StaffPhoneStatus(null, null);
    }
```

- [ ] **Step 6: Add the endpoint**

In `src/AFK4.Platform.Api/Program.cs`, immediately after the `POST /api/auth/staff/phone/confirm` endpoint block (closing `});` at ~line 768), add:

```csharp
app.MapGet("/api/auth/staff/phone", async (
    IStaffContextAccessor staffContextAccessor,
    IStaffPhoneVerificationService verificationService,
    CancellationToken cancellationToken) =>
{
    var staff = staffContextAccessor.Current;
    if (staff is null)
    {
        return Results.Unauthorized();
    }

    var status = await verificationService.GetStatusAsync(staff.StaffUserId, cancellationToken);
    return Results.Ok(new StaffPhoneStatusResponse(status.Phone, status.PhoneVerifiedAtUtc));
});
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~StaffPhoneVerificationEndpointTests"`
Expected: PASS (all StaffPhoneVerificationEndpointTests, including the 3 new ones).

- [ ] **Step 8: Commit**

```bash
git add src/AFK4.Shared.Contracts/Identity/StaffPhoneStatusResponse.cs src/AFK4.Platform.Api/Identity/IStaffPhoneVerificationService.cs src/AFK4.Platform.Api/Identity/EfStaffPhoneVerificationService.cs src/AFK4.Platform.Api/Program.cs tests/AFK4.Platform.Api.Tests/StaffPhoneVerificationEndpointTests.cs
git commit -m "feat(api): add GET /api/auth/staff/phone self-read endpoint"
```

---

## Phase 2 — Shared i18n keys

### Task 2: Add `account.phone.*` keys to all three locales

**Files:**
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json`
- Generated: `packages/i18n/src/messages.ts`
- Test: `packages/i18n/src/messages.test.ts` (parity — no edit, must pass)

- [ ] **Step 1: Add keys to `locales/ru.json`**

Insert these entries into the JSON object (recommended: right before the final closing `}`; make sure the line before your block ends with a comma and your block's last line does **not** add a trailing comma if it is the last entry):

```json
  "account.phone.title": "Телефон для входа",
  "account.phone.loading": "Загрузка…",
  "account.phone.field": "Номер телефона",
  "account.phone.placeholder": "+992 90 123-45-67",
  "account.phone.sendCode": "Получить код",
  "account.phone.codeField": "Код из SMS",
  "account.phone.confirm": "Подтвердить",
  "account.phone.resend": "Отправить код повторно",
  "account.phone.change": "Изменить номер",
  "account.phone.verifiedBadge": "подтверждён",
  "account.phone.verifiedToast": "Телефон подтверждён",
  "account.phone.close": "Закрыть",
  "account.phone.invalidCodeAttempts": "Неверный код. Осталось попыток:",
  "account.phone.err.invalid_phone": "Проверьте номер: нужен формат +992 90 123-45-67",
  "account.phone.err.cooldown": "Запросите код повторно чуть позже",
  "account.phone.err.rate_limited": "Слишком много запросов кода, попробуйте через час",
  "account.phone.err.sms_unavailable": "SMS-сервис недоступен, попробуйте позже",
  "account.phone.err.invalid_code": "Неверный код. Проверьте и попробуйте снова",
  "account.phone.err.expired": "Код истёк, запросите новый",
  "account.phone.err.too_many": "Слишком много попыток, запросите новый код",
  "account.phone.err.in_use": "Этот номер уже привязан к другому сотруднику",
  "account.phone.err.generic": "Не удалось выполнить действие, попробуйте ещё раз"
```

- [ ] **Step 2: Add the same keys to `locales/en.json`**

```json
  "account.phone.title": "Sign-in phone",
  "account.phone.loading": "Loading…",
  "account.phone.field": "Phone number",
  "account.phone.placeholder": "+992 90 123-45-67",
  "account.phone.sendCode": "Get code",
  "account.phone.codeField": "SMS code",
  "account.phone.confirm": "Confirm",
  "account.phone.resend": "Resend code",
  "account.phone.change": "Change number",
  "account.phone.verifiedBadge": "verified",
  "account.phone.verifiedToast": "Phone verified",
  "account.phone.close": "Close",
  "account.phone.invalidCodeAttempts": "Invalid code. Attempts left:",
  "account.phone.err.invalid_phone": "Check the number: use the format +992 90 123-45-67",
  "account.phone.err.cooldown": "Request the code again a bit later",
  "account.phone.err.rate_limited": "Too many code requests, try again in an hour",
  "account.phone.err.sms_unavailable": "SMS service unavailable, try again later",
  "account.phone.err.invalid_code": "Invalid code. Check it and try again",
  "account.phone.err.expired": "The code expired, request a new one",
  "account.phone.err.too_many": "Too many attempts, request a new code",
  "account.phone.err.in_use": "This number is already linked to another staff member",
  "account.phone.err.generic": "Could not complete the action, try again"
```

- [ ] **Step 3: Add the same keys to `locales/tg.json`**

Tajik falls back to Russian (`tg → ru`), so mirror the Russian values for now (a native pass can refine later). Use the **exact same values as the `locales/ru.json` block in Step 1** (same keys, same Russian strings).

- [ ] **Step 4: Regenerate the typed catalog**

Run from `packages/i18n`: `bun run gen`
Expected output: `generated .../messages.ts from 3 locales`. This rewrites `packages/i18n/src/messages.ts`.

- [ ] **Step 5: Run the i18n parity + voice tests to verify they pass**

Run from `packages/i18n`: `bun test`
Expected: PASS — including `ru, en and tg have identical key sets`, `generated messages.ts matches the locales/*.json source of truth`, and the voice guard (no Cyrillic ALL-CAPS, no «компьютер»).

- [ ] **Step 6: Commit**

```bash
git add locales/ru.json locales/en.json locales/tg.json packages/i18n/src/messages.ts
git commit -m "i18n: add account.phone.* keys for staff phone verification"
```

---

## Phase 3 — Platform.Web (browser club admin)

### Task 3: Add phone methods to `ClubApiClient`

**Files:**
- Modify: `src/AFK4.Platform.Web/src/api/clubApi.ts`

- [ ] **Step 1: Add the three methods**

In `src/AFK4.Platform.Web/src/api/clubApi.ts`, add these methods to the `ClubApiClient` class, right after `rotateOwnerCode` (~line 106). They reuse the existing private `send`/`sendRaw`/`readJson` helpers:

```typescript
  public getStaffPhone(): Promise<{ phone: string | null; phoneVerifiedAtUtc: string | null }> {
    return this.send('GET', '/api/auth/staff/phone');
  }

  public startPhoneVerification(phone: string): Promise<{ expiresInSeconds: number; resendAfterSeconds: number }> {
    return this.send('POST', '/api/auth/staff/phone/start-verification', { phone });
  }

  public confirmPhoneVerification(code: string): Promise<{ phone: string }> {
    return this.send('POST', '/api/auth/staff/phone/confirm', { code });
  }
```

- [ ] **Step 2: Type-check**

Run from `src/AFK4.Platform.Web`: `bunx tsc -b`
Expected: no errors.

- [ ] **Step 3: Commit**

```bash
git add src/AFK4.Platform.Web/src/api/clubApi.ts
git commit -m "feat(platform-web): add staff phone API methods to ClubApiClient"
```

### Task 4: Build `PhoneVerificationCard` (Platform.Web)

**Files:**
- Create: `src/AFK4.Platform.Web/src/club/profile/PhoneVerificationCard.tsx`
- Test: `src/AFK4.Platform.Web/src/club/profile/PhoneVerificationCard.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `src/AFK4.Platform.Web/src/club/profile/PhoneVerificationCard.test.tsx`:

```typescript
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { PlatformApiError } from '@/api/platformApi';
import { PhoneVerificationCard } from './PhoneVerificationCard';

function fakeClient(overrides: Partial<{
  getStaffPhone: () => Promise<{ phone: string | null; phoneVerifiedAtUtc: string | null }>;
  startPhoneVerification: (phone: string) => Promise<{ expiresInSeconds: number; resendAfterSeconds: number }>;
  confirmPhoneVerification: (code: string) => Promise<{ phone: string }>;
}> = {}) {
  return {
    getStaffPhone: mock(overrides.getStaffPhone ?? (async () => ({ phone: null, phoneVerifiedAtUtc: null }))),
    startPhoneVerification: mock(overrides.startPhoneVerification ?? (async () => ({ expiresInSeconds: 300, resendAfterSeconds: 60 }))),
    confirmPhoneVerification: mock(overrides.confirmPhoneVerification ?? (async () => ({ phone: '+992937380070' })))
  };
}

function renderCard(client: ReturnType<typeof fakeClient>) {
  return render(
    <I18nProvider><ToastProvider>
      <PhoneVerificationCard client={client as never} />
    </ToastProvider></I18nProvider>
  );
}

it('lets an unverified staff member send a code and confirm it', async () => {
  const client = fakeClient();
  renderCard(client);
  const input = await screen.findByLabelText('Номер телефона');
  fireEvent.change(input, { target: { value: '+992937380070' } });
  fireEvent.click(screen.getByRole('button', { name: 'Получить код' }));
  await waitFor(() => expect(client.startPhoneVerification).toHaveBeenCalledWith('+992937380070'));

  const codeInput = await screen.findByLabelText('Код из SMS');
  fireEvent.change(codeInput, { target: { value: '123456' } });
  fireEvent.click(screen.getByRole('button', { name: 'Подтвердить' }));
  await waitFor(() => expect(client.confirmPhoneVerification).toHaveBeenCalledWith('123456'));
  expect(await screen.findByText('подтверждён')).toBeInTheDocument();
});

it('shows a verified phone on load', async () => {
  const client = fakeClient({ getStaffPhone: async () => ({ phone: '+992937380070', phoneVerifiedAtUtc: '2026-06-06T00:00:00Z' }) });
  renderCard(client);
  expect(await screen.findByText('+992937380070')).toBeInTheDocument();
  expect(screen.getByText('подтверждён')).toBeInTheDocument();
});

it('maps a backend error code to a localized message', async () => {
  const client = fakeClient({
    startPhoneVerification: async () => { throw new PlatformApiError(400, 'invalid_phone', 'invalid_phone'); }
  });
  renderCard(client);
  const input = await screen.findByLabelText('Номер телефона');
  fireEvent.change(input, { target: { value: 'abc' } });
  fireEvent.click(screen.getByRole('button', { name: 'Получить код' }));
  expect(await screen.findByText(/Проверьте номер/)).toBeInTheDocument();
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run from `src/AFK4.Platform.Web`: `bun test src/club/profile/PhoneVerificationCard.test.tsx`
Expected: FAIL — cannot find module `./PhoneVerificationCard`.

- [ ] **Step 3: Implement the card**

Create `src/AFK4.Platform.Web/src/club/profile/PhoneVerificationCard.tsx`:

```tsx
import { useEffect, useState } from 'react';
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { useToast } from '@/components/ui/toast';
import { useI18n, type MessageKey } from '@/i18n/I18nProvider';
import { PlatformApiError } from '@/api/platformApi';
import type { ClubApiClient } from '@/api/clubApi';

type Client = Pick<ClubApiClient, 'getStaffPhone' | 'startPhoneVerification' | 'confirmPhoneVerification'>;
type Phase = 'loading' | 'idle' | 'code' | 'verified';

// Backend error code → i18n key. t() has no interpolation, so the numeric
// "remaining attempts" detail is not shown here (the browser admin gets the
// generic invalid_code message); the desktop app surfaces the count.
const ERROR_KEYS: Record<string, MessageKey> = {
  invalid_phone: 'account.phone.err.invalid_phone',
  cooldown_active: 'account.phone.err.cooldown',
  rate_limited: 'account.phone.err.rate_limited',
  sms_unavailable: 'account.phone.err.sms_unavailable',
  invalid_code: 'account.phone.err.invalid_code',
  code_expired: 'account.phone.err.expired',
  no_active_code: 'account.phone.err.expired',
  too_many_attempts: 'account.phone.err.too_many',
  phone_already_in_use: 'account.phone.err.in_use'
};

export function PhoneVerificationCard({ client }: { client: Client }) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [phase, setPhase] = useState<Phase>('loading');
  const [currentPhone, setCurrentPhone] = useState<string | null>(null);
  const [phone, setPhone] = useState('');
  const [code, setCode] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    let disposed = false;
    void (async () => {
      try {
        const status = await client.getStaffPhone();
        if (disposed) return;
        if (status && status.phoneVerifiedAtUtc !== null) {
          setCurrentPhone(status.phone);
          setPhase('verified');
        } else {
          setPhase('idle');
        }
      } catch {
        if (!disposed) setPhase('idle');
      }
    })();
    return () => { disposed = true; };
  }, [client]);

  function describe(err: unknown): string {
    if (err instanceof PlatformApiError && err.errorCode !== null && err.errorCode in ERROR_KEYS) {
      return t(ERROR_KEYS[err.errorCode]);
    }
    return t('account.phone.err.generic');
  }

  async function sendCode() {
    setBusy(true);
    setError(null);
    try {
      await client.startPhoneVerification(phone.trim());
      setCode('');
      setPhase('code');
    } catch (err) {
      setError(describe(err));
    } finally {
      setBusy(false);
    }
  }

  async function confirm() {
    setBusy(true);
    setError(null);
    try {
      const result = await client.confirmPhoneVerification(code.trim());
      setCurrentPhone(result.phone);
      setPhase('verified');
      toast({ title: t('account.phone.verifiedToast'), variant: 'success' });
    } catch (err) {
      setError(describe(err));
    } finally {
      setBusy(false);
    }
  }

  return (
    <Card>
      <CardHeader><CardTitle>{t('account.phone.title')}</CardTitle></CardHeader>
      <CardContent className="flex flex-col gap-4">
        {error !== null && <p className="text-sm text-destructive" role="alert">{error}</p>}

        {phase === 'loading' && <p className="text-sm text-muted-foreground">{t('account.phone.loading')}</p>}

        {phase === 'idle' && (
          <div className="flex max-w-md flex-col gap-3">
            <label className="block text-sm">
              <span className="mb-1 block text-muted-foreground">{t('account.phone.field')}</span>
              <Input
                aria-label={t('account.phone.field')}
                inputMode="tel"
                placeholder={t('account.phone.placeholder')}
                value={phone}
                onChange={e => setPhone(e.target.value)}
                disabled={busy}
              />
            </label>
            <div>
              <Button disabled={busy || phone.trim().length < 6} onClick={() => void sendCode()}>
                {t('account.phone.sendCode')}
              </Button>
            </div>
          </div>
        )}

        {phase === 'code' && (
          <div className="flex max-w-md flex-col gap-3">
            <label className="block text-sm">
              <span className="mb-1 block text-muted-foreground">{t('account.phone.codeField')}</span>
              <Input
                aria-label={t('account.phone.codeField')}
                inputMode="numeric"
                value={code}
                onChange={e => setCode(e.target.value)}
                disabled={busy}
              />
            </label>
            <div className="flex flex-wrap gap-3">
              <Button disabled={busy || code.trim().length === 0} onClick={() => void confirm()}>
                {t('account.phone.confirm')}
              </Button>
              <Button variant="outline" disabled={busy} onClick={() => void sendCode()}>
                {t('account.phone.resend')}
              </Button>
            </div>
          </div>
        )}

        {phase === 'verified' && (
          <div className="flex flex-wrap items-center gap-3">
            <span className="text-sm font-medium">{currentPhone}</span>
            <Badge variant="default">{t('account.phone.verifiedBadge')}</Badge>
            <Button variant="outline" disabled={busy} onClick={() => { setPhone(''); setError(null); setPhase('idle'); }}>
              {t('account.phone.change')}
            </Button>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run from `src/AFK4.Platform.Web`: `bun test src/club/profile/PhoneVerificationCard.test.tsx`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Web/src/club/profile/PhoneVerificationCard.tsx src/AFK4.Platform.Web/src/club/profile/PhoneVerificationCard.test.tsx
git commit -m "feat(platform-web): add staff phone verification card"
```

### Task 5: Wire the card into `ProfileScreen`

**Files:**
- Modify: `src/AFK4.Platform.Web/src/club/profile/ProfileScreen.tsx`
- Modify: `src/AFK4.Platform.Web/src/club/profile/ProfileScreen.test.tsx`
- Modify: `src/AFK4.Platform.Web/src/App.tsx`

- [ ] **Step 1: Update `ProfileScreen.test.tsx` (failing first)**

In `src/AFK4.Platform.Web/src/club/profile/ProfileScreen.test.tsx`, the component will now require a `client` prop and render a card that uses toast. Update the test to wrap with `ToastProvider` and pass a fake client. Replace the existing render call (the `<ProfileScreen ... />` render inside `I18nProvider`) so it reads:

```tsx
      <I18nProvider><ToastProvider>
        <ProfileScreen
          session={session}
          branches={[{ branchId: 'b1', name: 'Центр' }]}
          roleLabel="Владелец"
          onSignOut={onSignOut}
          client={{
            getStaffPhone: async () => ({ phone: null, phoneVerifiedAtUtc: null }),
            startPhoneVerification: async () => ({ expiresInSeconds: 300, resendAfterSeconds: 60 }),
            confirmPhoneVerification: async () => ({ phone: '+992937380070' })
          } as never}
        />
      </ToastProvider></I18nProvider>
```

Add the import at the top of the test file (keep existing imports):

```tsx
import { ToastProvider } from '@/components/ui/toast';
```

- [ ] **Step 2: Run the test to verify it fails**

Run from `src/AFK4.Platform.Web`: `bun test src/club/profile/ProfileScreen.test.tsx`
Expected: FAIL — `client` is not a prop of `ProfileScreen` (type error) / card not rendered.

- [ ] **Step 3: Add the `client` prop + render the card in `ProfileScreen.tsx`**

In `src/AFK4.Platform.Web/src/club/profile/ProfileScreen.tsx`:

Add imports near the top:

```tsx
import { PhoneVerificationCard } from './PhoneVerificationCard';
import type { ClubApiClient } from '@/api/clubApi';
```

Change the function signature to accept `client`:

```tsx
export function ProfileScreen({ session, branches, roleLabel, onSignOut, client }: {
  session: StaffSession;
  branches: { branchId: string; name: string }[];
  roleLabel: string;
  onSignOut: () => void;
  client: Pick<ClubApiClient, 'getStaffPhone' | 'startPhoneVerification' | 'confirmPhoneVerification'>;
}) {
```

Render the card immediately after the identity `Card` (after its closing `</Card>`, before the branches `Card`):

```tsx
      <PhoneVerificationCard client={client} />
```

- [ ] **Step 4: Pass `clubClient` from `App.tsx`**

In `src/AFK4.Platform.Web/src/App.tsx`, find the `<ProfileScreen` render (~line 470) and add the `client` prop:

```tsx
        <ProfileScreen
          session={session}
          branches={branches}
          roleLabel={roleLabel}
          onSignOut={onSignOut}
          client={clubClient}
        />
```

(`clubClient` is already constructed at the App root via `useMemo`, ~line 114.)

- [ ] **Step 5: Run the tests + type-check to verify they pass**

Run from `src/AFK4.Platform.Web`: `bun test src/club/profile/ && bunx tsc -b`
Expected: PASS (ProfileScreen + PhoneVerificationCard tests) and no type errors.

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Platform.Web/src/club/profile/ProfileScreen.tsx src/AFK4.Platform.Web/src/club/profile/ProfileScreen.test.tsx src/AFK4.Platform.Web/src/App.tsx
git commit -m "feat(platform-web): show phone verification card in profile screen"
```

---

## Phase 4 — Operator.App.Web (desktop operator app)

### Task 6: Add the `account` client group

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/operatorApiClients.ts`

- [ ] **Step 1: Add DTOs + the client factory**

In `src/AFK4.Operator.App.Web/src/operatorApiClients.ts`, add these interfaces (near the other DTO interfaces, e.g. above `createPaymentGatewayClient` ~line 510) and a factory function right before `export function createOperatorApiClients` (~line 540):

```typescript
export interface StaffPhoneStatusDto {
  phone: string | null;
  phoneVerifiedAtUtc: string | null;
}

export interface StaffPhoneVerificationStartedDto {
  expiresInSeconds: number;
  resendAfterSeconds: number;
}

export interface StaffPhoneConfirmedDto {
  phone: string;
}

export function createAccountClient(api: PlatformApiClient) {
  return {
    getMyPhone(): Promise<StaffPhoneStatusDto> {
      return api.get<StaffPhoneStatusDto>('/api/auth/staff/phone');
    },
    startPhoneVerification(request: { phone: string }): Promise<StaffPhoneVerificationStartedDto> {
      return api.post<StaffPhoneVerificationStartedDto, { phone: string }>(
        '/api/auth/staff/phone/start-verification', request);
    },
    confirmPhoneVerification(request: { code: string }): Promise<StaffPhoneConfirmedDto> {
      return api.post<StaffPhoneConfirmedDto, { code: string }>(
        '/api/auth/staff/phone/confirm', request);
    }
  };
}
```

- [ ] **Step 2: Register the group**

In the `createOperatorApiClients` return object (~line 541), add a line:

```typescript
    account: createAccountClient(api),
```

- [ ] **Step 3: Type-check**

Run from `src/AFK4.Operator.App.Web`: `bunx tsc -b`
Expected: no errors.

- [ ] **Step 4: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/operatorApiClients.ts
git commit -m "feat(operator-web): add account client group for staff phone"
```

### Task 7: Build `PhoneVerificationCard` (Operator.App.Web)

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/PhoneVerificationCard.tsx`
- Test: `src/AFK4.Operator.App.Web/src/PhoneVerificationCard.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `src/AFK4.Operator.App.Web/src/PhoneVerificationCard.test.tsx`:

```typescript
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { PlatformApiError } from './platformApi';

const getMyPhone = mock(async () => ({ phone: null, phoneVerifiedAtUtc: null }));
const startPhoneVerification = mock(async () => ({ expiresInSeconds: 300, resendAfterSeconds: 60 }));
const confirmPhoneVerification = mock(async () => ({ phone: '+992937380070' }));

const actualClients = await import('./operatorApiClients');
mock.module('./operatorApiClients', () => ({
  ...actualClients,
  createOperatorApiClients: () => ({
    account: { getMyPhone, startPhoneVerification, confirmPhoneVerification }
  })
}));

const { PhoneVerificationCard } = await import('./PhoneVerificationCard');

const backend = { config: { platformBaseUrl: 'http://test' }, session: { accessToken: 't' } };

describe('PhoneVerificationCard (operator)', () => {
  afterEach(() => { cleanup(); mock.restore(); });

  it('sends a code then confirms it', async () => {
    render(<I18nProvider><PhoneVerificationCard backend={backend} /></I18nProvider>);
    const input = await screen.findByLabelText('Номер телефона');
    fireEvent.change(input, { target: { value: '+992937380070' } });
    fireEvent.click(screen.getByRole('button', { name: 'Получить код' }));
    await waitFor(() => expect(startPhoneVerification).toHaveBeenCalledWith({ phone: '+992937380070' }));

    const codeInput = await screen.findByLabelText('Код из SMS');
    fireEvent.change(codeInput, { target: { value: '123456' } });
    fireEvent.click(screen.getByRole('button', { name: 'Подтвердить' }));
    await waitFor(() => expect(confirmPhoneVerification).toHaveBeenCalledWith({ code: '123456' }));
    expect(await screen.findByText('подтверждён')).toBeInTheDocument();
  });

  it('shows remaining attempts on invalid_code', async () => {
    confirmPhoneVerification.mockImplementationOnce(async () => {
      throw new PlatformApiError('bad', 400, 'Bad Request', '{"error":"invalid_code","remainingAttempts":2}');
    });
    render(<I18nProvider><PhoneVerificationCard backend={backend} /></I18nProvider>);
    const input = await screen.findByLabelText('Номер телефона');
    fireEvent.change(input, { target: { value: '+992937380070' } });
    fireEvent.click(screen.getByRole('button', { name: 'Получить код' }));
    const codeInput = await screen.findByLabelText('Код из SMS');
    fireEvent.change(codeInput, { target: { value: '000000' } });
    fireEvent.click(screen.getByRole('button', { name: 'Подтвердить' }));
    expect(await screen.findByText(/осталось попыток: 2/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run from `src/AFK4.Operator.App.Web`: `bun test src/PhoneVerificationCard.test.tsx`
Expected: FAIL — cannot find module `./PhoneVerificationCard`.

- [ ] **Step 3: Implement the card**

Create `src/AFK4.Operator.App.Web/src/PhoneVerificationCard.tsx`:

```tsx
import { useEffect, useMemo, useState } from 'react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { PlatformApiClient, PlatformApiError } from './platformApi';
import { createOperatorApiClients } from './operatorApiClients';

// Structurally compatible with App.tsx's backend context (config + session);
// declared locally to avoid a circular import (App.tsx imports this file).
export interface PhoneVerificationBackend {
  config: { platformBaseUrl: string };
  session: { accessToken: string };
}

type Phase = 'loading' | 'idle' | 'code' | 'verified';

const ERROR_KEYS: Record<string, MessageKey> = {
  invalid_phone: 'account.phone.err.invalid_phone',
  cooldown_active: 'account.phone.err.cooldown',
  rate_limited: 'account.phone.err.rate_limited',
  sms_unavailable: 'account.phone.err.sms_unavailable',
  invalid_code: 'account.phone.err.invalid_code',
  code_expired: 'account.phone.err.expired',
  no_active_code: 'account.phone.err.expired',
  too_many_attempts: 'account.phone.err.too_many',
  phone_already_in_use: 'account.phone.err.in_use'
};

// PlatformApiError.body is the raw response text, so parse it for the error code
// and the invalid_code "remainingAttempts" detail (t() has no interpolation).
function describeError(err: unknown, t: (k: MessageKey) => string): string {
  if (err instanceof PlatformApiError) {
    try {
      const body = JSON.parse(err.body) as { error?: string; remainingAttempts?: number };
      if (body.error === 'invalid_code' && typeof body.remainingAttempts === 'number') {
        return `${t('account.phone.invalidCodeAttempts')} ${body.remainingAttempts}`;
      }
      if (typeof body.error === 'string' && body.error in ERROR_KEYS) {
        return t(ERROR_KEYS[body.error]);
      }
    } catch {
      // non-JSON body → fall through to generic
    }
  }
  return t('account.phone.err.generic');
}

export function PhoneVerificationCard({ backend }: { backend: PhoneVerificationBackend }) {
  const { t } = useI18n();
  const api = useMemo(
    () => createOperatorApiClients(new PlatformApiClient({
      baseUrl: backend.config.platformBaseUrl,
      getAccessToken: () => backend.session.accessToken
    })).account,
    [backend.config.platformBaseUrl, backend.session.accessToken]
  );

  const [phase, setPhase] = useState<Phase>('loading');
  const [currentPhone, setCurrentPhone] = useState<string | null>(null);
  const [phone, setPhone] = useState('');
  const [code, setCode] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    let disposed = false;
    void (async () => {
      try {
        const status = await api.getMyPhone();
        if (disposed) return;
        if (status.phoneVerifiedAtUtc !== null) {
          setCurrentPhone(status.phone);
          setPhase('verified');
        } else {
          setPhase('idle');
        }
      } catch {
        if (!disposed) setPhase('idle');
      }
    })();
    return () => { disposed = true; };
  }, [api]);

  const sendCode = async () => {
    setBusy(true);
    setError(null);
    try {
      await api.startPhoneVerification({ phone: phone.trim() });
      setCode('');
      setPhase('code');
    } catch (err) {
      setError(describeError(err, t));
    } finally {
      setBusy(false);
    }
  };

  const confirm = async () => {
    setBusy(true);
    setError(null);
    try {
      const result = await api.confirmPhoneVerification({ code: code.trim() });
      setCurrentPhone(result.phone);
      setPhase('verified');
    } catch (err) {
      setError(describeError(err, t));
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="account-phone">
      <h3>{t('account.phone.title')}</h3>
      {error !== null && <p className="account-phone-error" role="alert">{error}</p>}

      {phase === 'loading' && <p className="account-phone-hint">{t('account.phone.loading')}</p>}

      {phase === 'idle' && (
        <div className="account-phone-form">
          <label>{t('account.phone.field')}
            <input
              inputMode="tel"
              placeholder={t('account.phone.placeholder')}
              value={phone}
              onChange={(e) => setPhone(e.currentTarget.value)}
              disabled={busy}
            />
          </label>
          <button type="button" disabled={busy || phone.trim().length < 6} onClick={() => void sendCode()}>
            {t('account.phone.sendCode')}
          </button>
        </div>
      )}

      {phase === 'code' && (
        <div className="account-phone-form">
          <label>{t('account.phone.codeField')}
            <input
              inputMode="numeric"
              value={code}
              onChange={(e) => setCode(e.currentTarget.value)}
              disabled={busy}
            />
          </label>
          <div className="account-phone-actions">
            <button type="button" disabled={busy || code.trim().length === 0} onClick={() => void confirm()}>
              {t('account.phone.confirm')}
            </button>
            <button type="button" className="secondary" disabled={busy} onClick={() => void sendCode()}>
              {t('account.phone.resend')}
            </button>
          </div>
        </div>
      )}

      {phase === 'verified' && (
        <div className="account-phone-verified">
          <strong>{currentPhone}</strong>
          <span className="account-phone-badge">{t('account.phone.verifiedBadge')}</span>
          <button type="button" className="secondary" disabled={busy} onClick={() => { setPhone(''); setError(null); setPhase('idle'); }}>
            {t('account.phone.change')}
          </button>
        </div>
      )}
    </section>
  );
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run from `src/AFK4.Operator.App.Web`: `bun test src/PhoneVerificationCard.test.tsx`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/PhoneVerificationCard.tsx src/AFK4.Operator.App.Web/src/PhoneVerificationCard.test.tsx
git commit -m "feat(operator-web): add staff phone verification card"
```

### Task 8: Build `AccountPanel` and open it from the header

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/AccountPanel.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/App.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/styles.css`

- [ ] **Step 1: Create `AccountPanel.tsx`**

Create `src/AFK4.Operator.App.Web/src/AccountPanel.tsx`:

```tsx
import { useI18n } from '@afk4/i18n';
import { PhoneVerificationCard, type PhoneVerificationBackend } from './PhoneVerificationCard';

interface Props {
  backend: PhoneVerificationBackend;
  displayName: string;
  onClose: () => void;
}

export function AccountPanel({ backend, displayName, onClose }: Props) {
  const { t } = useI18n();
  return (
    <div className="account-panel-overlay" role="dialog" aria-modal="true" onClick={onClose}>
      <div className="account-panel" onClick={(e) => e.stopPropagation()}>
        <header className="account-panel-head">
          <strong>{displayName}</strong>
          <button type="button" className="account-panel-close" aria-label={t('account.phone.close')} onClick={onClose}>×</button>
        </header>
        <PhoneVerificationCard backend={backend} />
      </div>
    </div>
  );
}
```

- [ ] **Step 2: Import `AccountPanel` + add open state in `App.tsx`**

In `src/AFK4.Operator.App.Web/src/App.tsx`:

(a) Add the import alongside the other local imports (near the `PaymentGatewaysWorkspace` import, ~line 111):

```tsx
import { AccountPanel } from './AccountPanel';
```

(b) Add open-state. Locate the shell component that renders the `top-command` header (the one containing `authSession`, `config`, `backendContext` — the `<button className="sign-out-button" ...>` lives here). Add this `useState` next to that component's other `useState` hooks:

```tsx
  const [accountPanelOpen, setAccountPanelOpen] = useState(false);
```

- [ ] **Step 3: Make the header identity open the panel**

In `src/AFK4.Operator.App.Web/src/App.tsx`, replace the identity span in the header (~line 10366):

Find:

```tsx
          <span>{operatorDisplayNameLabel(authSession.displayName)} · {shellModeLabel(config.shellMode)}</span>
```

Replace with:

```tsx
          <button type="button" className="top-account" onClick={() => setAccountPanelOpen(true)}>
            {operatorDisplayNameLabel(authSession.displayName)} · {shellModeLabel(config.shellMode)}
          </button>
```

- [ ] **Step 4: Render the panel**

In `src/AFK4.Operator.App.Web/src/App.tsx`, immediately after the closing `</header>` of the `top-command` header (~line 10370), add:

```tsx
      {accountPanelOpen && backendContext !== null && (
        <AccountPanel
          backend={backendContext}
          displayName={operatorDisplayNameLabel(authSession.displayName)}
          onClose={() => setAccountPanelOpen(false)}
        />
      )}
```

- [ ] **Step 5: Add styles**

Append to `src/AFK4.Operator.App.Web/src/styles.css`:

```css
.top-account {
  background: none;
  border: none;
  color: inherit;
  font: inherit;
  cursor: pointer;
  padding: 0;
  text-align: left;
}
.top-account:hover { text-decoration: underline; }

.account-panel-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: flex-start;
  justify-content: center;
  padding-top: 64px;
  z-index: 1000;
}
.account-panel {
  background: var(--surface, #14181f);
  color: inherit;
  border-radius: 12px;
  padding: 20px;
  width: min(440px, calc(100vw - 32px));
  box-shadow: 0 16px 48px rgba(0, 0, 0, 0.4);
}
.account-panel-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 16px;
}
.account-panel-close {
  background: none;
  border: none;
  color: inherit;
  font-size: 22px;
  line-height: 1;
  cursor: pointer;
}

.account-phone { display: flex; flex-direction: column; gap: 12px; }
.account-phone h3 { margin: 0; font-size: 15px; }
.account-phone-hint { color: var(--muted, #8a93a3); font-size: 13px; }
.account-phone-error { color: var(--danger, #ff6b6b); font-size: 13px; }
.account-phone-form { display: flex; flex-direction: column; gap: 12px; }
.account-phone-form label { display: flex; flex-direction: column; gap: 4px; font-size: 13px; }
.account-phone-form input {
  padding: 8px 10px;
  border-radius: 8px;
  border: 1px solid var(--border, #2a2f3a);
  background: var(--input-bg, #0f1217);
  color: inherit;
}
.account-phone-actions { display: flex; gap: 12px; flex-wrap: wrap; }
.account-phone-verified { display: flex; align-items: center; gap: 12px; flex-wrap: wrap; }
.account-phone-badge {
  font-size: 12px;
  padding: 2px 8px;
  border-radius: 999px;
  background: var(--accent-soft, rgba(45, 212, 167, 0.15));
  color: var(--accent, #2dd4a7);
}
```

(If a CSS custom property above is not defined in this app, the fallback after the comma applies — no further action needed.)

- [ ] **Step 6: Type-check + run the existing app test suite**

Run from `src/AFK4.Operator.App.Web`: `bunx tsc -b && bun test`
Expected: no type errors; all tests pass (the new card test + the existing suite, e.g. `App.test.tsx`).

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/AccountPanel.tsx src/AFK4.Operator.App.Web/src/App.tsx src/AFK4.Operator.App.Web/src/styles.css
git commit -m "feat(operator-web): open account panel with phone verification from header"
```

---

## Phase 5 — Full verification

### Task 9: Verify the whole feature builds and tests green

- [ ] **Step 1: Backend tests**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj`
Expected: PASS (all, including the 3 new GET-phone tests).

- [ ] **Step 2: i18n package tests**

Run from `packages/i18n`: `bun test`
Expected: PASS (parity + generated-matches-source + voice).

- [ ] **Step 3: Platform.Web tests + type-check**

Run from `src/AFK4.Platform.Web`: `bunx tsc -b && bun test`
Expected: no type errors; all tests pass.

- [ ] **Step 4: Operator.App.Web tests + type-check**

Run from `src/AFK4.Operator.App.Web`: `bunx tsc -b && bun test`
Expected: no type errors; all tests pass.

- [ ] **Step 5: Manual smoke (optional, if a staging API + SMS token is available)**

In Platform.Web (`bun run dev`), sign in as a staff member, open the profile screen, enter a phone, request a code, enter the SMS code, confirm → expect the "подтверждён" badge. Reload → expect the verified phone persists. Repeat the equivalent flow via the account panel in Operator.App.Web.

- [ ] **Step 6: Final commit (only if Steps 1–4 surfaced fixes)**

```bash
git add -A
git commit -m "test: verify staff phone verification across api + both frontends"
```

---

## Out of scope (follow-up — see spec Revision 2)

- **Owner-visibility badge** ("phone verified / not set" in each staff list). Needs the
  backend `StaffUserDto` to expose `Phone` + `PhoneVerifiedAtUtc` (record + `ToStaffUserDto`
  in `Program.cs` ~12874), then a badge in `OperatorsTable.tsx` (Platform.Web, via
  `settingsModel`/`OperatorRow`) and in the Operator.App.Web staff rows
  (`App.tsx` ~8935, `readString(user, 'phoneVerifiedAtUtc')`). Self-contained; do as its
  own small plan after this lands.
- Resend/expiry countdown timers (backend already enforces; v1 relies on error messages).
- Phase C (wizard phone login / authenticated install endpoints) and Phase D (SMS password
  reset) — separate specs; this plan only unblocks them.
```
