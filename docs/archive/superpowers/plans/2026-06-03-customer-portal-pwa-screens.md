# Customer Portal PWA — Screens, PWA & i18n Implementation Plan (Plan 2)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete the player-facing PWA `AFK4.Customer.Web` by building the remaining screens (visit history + receipt, purchases, wallet top-up, reservations, profile) on top of the already-shipped Plan 1 foundation, then make it installable + offline-read capable and localized (ru/en).

**Architecture:** Extends the existing standalone workspace package `src/AFK4.Customer.Web` (Vite 8 + React 19 + TS 6, Tailwind 4, bun test + happy-dom + testing-library, `@afk4/money` + `@afk4/i18n`). All data comes from the already-built, verified `/api/me/*` and `/api/public/*` endpoints — no backend work. New API methods are added to the existing `PlayerApiClient` class. New surfaces are styled with the established `--color-*` / `--accent` CSS-variable tokens directly (the idiom `DashboardScreen` and `BottomNav` already use), not the shadcn semantic-token layer. Cursor-paginated lists use a new generic `useCursorList` hook with an explicit "Показать ещё" load-more button (mobile-friendly, accessible, deterministic to test) rather than scroll observers. The whole UI is built in Russian first, then a single i18n task extracts every string into a `customer.*` ru/en catalog and switches screens to `t()` — matching the spec's build sequence.

**Tech Stack:** Vite 8, React 19, TypeScript 6 (`strict`), Tailwind CSS 4, shadcn "new-york" primitives (copied), `bun test` + `@happy-dom/global-registrator` + `@testing-library/react`, fetch mocked via `bun:test`'s `mock()`, `vite-plugin-pwa` (added in this plan), `@afk4/i18n`, `@afk4/money`. Money is integer minor units end-to-end.

**Scope boundary:** Frontend only; the backend is complete. Covers spec build-seq items **7–13** (Plan 1 delivered 1–6). Out of scope (per the design spec's non-goals): OTP sign-in / phone-verification self-edit (UI rendered disabled with honest copy), online self-payment, offline write queue, Tajik locale, the WPF shell.

**Ground-truth conventions (verified, mirror exactly):**
- API JSON is camelCase (`walletBalance`, `nextCursor`, …) — confirmed by the working Plan-1 dashboard.
- `PlayerApiClient` already has `publicPost`, `authedGet`, `refreshOnce`, `buildHeaders`, `toError`, `updateSession`. 401 → `refreshOnce()` → retry once. `buildHeaders()` sets only `Authorization` (no Content-Type on GET).
- `PlayerApiError` carries `status` — screens branch on `err.status === 403` for the D8 gate.
- `PlayerSession.phoneVerified` is the client-side D8 signal; the 403 is the backstop.
- Reservation states: `pending` / `confirmed` / `seated` / `cancelled` (`ReservationStateNames`). Top-up intent states: `pending` / `fulfilled`, plus `isExpired` boolean from the API.
- Session `durationMode`: `'open'` (count-up + accrued cost) | `'fixed'` (count-down remaining).
- Tests obtain nothing from the network — fetch is always mocked; components receive a mock `PlayerApiClient` (the Plan-1 `DashboardScreen.test.tsx` pattern: `{ method: mock().mockResolvedValue(...) } as unknown as PlayerApiClient`).

**Known pre-existing risk (do NOT fix blindly here):** Plan 1 copied `components/ui/button.tsx` / `input.tsx` verbatim from Platform.Web; those reference shadcn semantic tokens (`--primary`, `--background`, `--ring`) that `index.css` does not define. They render acceptably enough that Plan 1 passed, but if a new screen looks unstyled, that is the cause. This plan sidesteps it by styling new surfaces with `--color-*` / `--accent` directly. If a dedicated token-mapping pass is wanted, raise it as a separate task — it is not in this plan.

---

## File Structure

**New files (created by this plan):**
- `src/AFK4.Customer.Web/src/lib/datetime.ts` — pure date/duration formatters (+ test).
- `src/AFK4.Customer.Web/src/lib/useCursorList.ts` — generic cursor-pagination hook (+ test).
- `src/AFK4.Customer.Web/src/components/ui/toast.tsx` — `ToastProvider` + `useToast` (+ test).
- `src/AFK4.Customer.Web/src/components/OfflineBanner.tsx` — offline indicator (+ test).
- `src/AFK4.Customer.Web/src/branding/useBranding.ts` — branding bootstrap hook (+ test).
- `src/AFK4.Customer.Web/src/screens/history/VisitsScreen.tsx` (+ test).
- `src/AFK4.Customer.Web/src/screens/history/ReceiptScreen.tsx` (+ test).
- `src/AFK4.Customer.Web/src/screens/purchases/PurchasesScreen.tsx` (+ test).
- `src/AFK4.Customer.Web/src/screens/history/HistoryTabs.tsx` — Визиты/Покупки segmented switch.
- `src/AFK4.Customer.Web/src/screens/wallet/WalletPanel.tsx` (+ test).
- `src/AFK4.Customer.Web/src/screens/reservations/ReservationsScreen.tsx` (+ test).
- `src/AFK4.Customer.Web/src/screens/profile/ProfileScreen.tsx` (+ test).
- `src/AFK4.Customer.Web/src/pwa/offlineCache.ts` — cache-name helpers + `clearPlayerCaches` (+ test).
- `src/AFK4.Customer.Web/src/pwa/registerSW.ts` — service-worker registration shim.
- `src/AFK4.Customer.Web/public/` — PWA icons (192/512/maskable) + favicon.

**Modified files:**
- `src/AFK4.Customer.Web/src/api/types.ts` — add the new DTO/request interfaces.
- `src/AFK4.Customer.Web/src/api/playerApi.ts` — add `authedSend` + 10 endpoint methods.
- `src/AFK4.Customer.Web/src/routing.ts` — add `purchases` + `wallet` (in-dashboard) routing as needed.
- `src/AFK4.Customer.Web/src/App.tsx` — branding bootstrap, mount new screens, sign-out, ToastProvider, OfflineBanner.
- `src/AFK4.Customer.Web/src/screens/dashboard/DashboardScreen.tsx` — embed `WalletPanel`.
- `src/AFK4.Customer.Web/src/main.tsx` — register SW + `I18nProvider`.
- `src/AFK4.Customer.Web/src/vite-env.d.ts` — `vite-plugin-pwa/client` reference.
- `src/AFK4.Customer.Web/vite.config.ts` — `VitePWA({...})`.
- `src/AFK4.Customer.Web/package.json` — add `vite-plugin-pwa` devDependency.
- `src/AFK4.Customer.Web/index.html` — manifest/theme-color/apple-touch meta.
- `packages/i18n/src/messages.ts` — add the `customer.*` keys (ru + en).
- All screen/component files — final i18n sweep to `t()`.

**Run all commands from the package directory** unless noted:
`cd /home/fedya/projects/afk4.net/src/AFK4.Customer.Web`
`bun` is at `/home/fedya/.bun/bin/bun` (use `bun` if on PATH, else the full path).

---

## Task 1: API types + `PlayerApiClient` methods

**Files:**
- Modify: `src/AFK4.Customer.Web/src/api/types.ts`
- Modify: `src/AFK4.Customer.Web/src/api/playerApi.ts`
- Test: `src/AFK4.Customer.Web/src/api/playerApi.test.ts` (extend the existing file)

- [ ] **Step 1: Add the new contract interfaces to `types.ts`**

Append to `src/AFK4.Customer.Web/src/api/types.ts` (keep existing content):

```typescript
export interface CursorPage<T> {
  items: T[];
  nextCursor: string | null;
}

export interface PlayerVisitDto {
  sessionId: string;
  seatId: string;
  seatName: string;
  startedAtUtc: string;
  endedAtUtc: string | null;
  timeChargeMinorUnits: number;
  posTotalMinorUnits: number;
  grandTotalMinorUnits: number;
  currencyCode: string;
  hasReceipt: boolean;
}

export interface PlayerPurchaseLineDto {
  productName: string;
  quantity: number;
  unitPriceMinorUnits: number;
  lineTotalMinorUnits: number;
}

export interface PlayerVisitReceiptDto {
  receiptNumber: string;
  createdAtUtc: string;
  sessionId: string;
  seatName: string;
  startedAtUtc: string;
  endedAtUtc: string | null;
  timeChargeMinorUnits: number;
  posLines: PlayerPurchaseLineDto[];
  posTotalMinorUnits: number;
  grandTotalMinorUnits: number;
  currencyCode: string;
}

export interface PlayerPurchaseDto {
  posSaleId: string;
  createdAtUtc: string;
  totalMinorUnits: number;
  currencyCode: string;
  lines: PlayerPurchaseLineDto[];
}

export interface PlayerProfileDto {
  playerAccountId: string;
  displayName: string;
  phoneNumber: string | null;
  phoneVerified: boolean;
  preferredLocale: string | null;
  marketingOptIn: boolean;
}

export interface UpdatePlayerProfileRequest {
  preferredLocale?: string | null;
  marketingOptIn?: boolean | null;
}

export interface PlayerTopUpIntentRequest {
  amountMinorUnits: number;
  currencyCode?: string | null;
}

export interface PlayerTopUpIntentDto {
  paymentIntentId: string;
  amountMinorUnits: number;
  currencyCode: string;
  state: string;
  purpose: string;
  method: string;
  createdAtUtc: string;
  fulfilledAtUtc: string | null;
  isExpired: boolean;
}

export interface CreatePlayerReservationRequest {
  seatId?: string | null;
  startsAtUtc: string;
  endsAtUtc: string;
  note?: string | null;
}

export interface PlayerReservationDto {
  reservationId: string;
  seatId: string | null;
  seatName: string | null;
  startsAtUtc: string;
  endsAtUtc: string;
  state: string;
  note: string | null;
}
```

- [ ] **Step 2: Write failing tests for the new client methods**

Append to `src/AFK4.Customer.Web/src/api/playerApi.test.ts` (reuse the existing `okJson`, `status`, `session` helpers already defined in that file):

```typescript
it('getVisits appends the cursor query and attaches the Bearer header', async () => {
  const fetchImpl = mock().mockResolvedValue(okJson({ items: [], nextCursor: null }));
  const client = new PlayerApiClient({ baseUrl: 'https://api.test', fetchImpl: fetchImpl as unknown as typeof fetch, session, onSessionChanged: () => {} });
  await client.getVisits('CURSOR_1');
  const [url, init] = fetchImpl.mock.calls[0];
  expect(url).toBe('https://api.test/api/me/visits?cursor=CURSOR_1');
  expect(init.headers.Authorization).toBe('Bearer tok');
});

it('createTopUpIntent POSTs the body with Content-Type and Bearer', async () => {
  const fetchImpl = mock().mockResolvedValue(okJson({ paymentIntentId: 'i1' }));
  const client = new PlayerApiClient({ baseUrl: 'https://api.test', fetchImpl: fetchImpl as unknown as typeof fetch, session, onSessionChanged: () => {} });
  await client.createTopUpIntent({ amountMinorUnits: 5000, currencyCode: 'TJS' });
  const [url, init] = fetchImpl.mock.calls[0];
  expect(url).toBe('https://api.test/api/me/wallet/top-up-intent');
  expect(init.method).toBe('POST');
  expect(init.headers['Content-Type']).toBe('application/json');
  expect(init.headers.Authorization).toBe('Bearer tok');
  expect(JSON.parse(init.body)).toEqual({ amountMinorUnits: 5000, currencyCode: 'TJS' });
});

it('a write refreshes once on 401 and re-sends the body with the new token', async () => {
  const fetchImpl = mock()
    .mockResolvedValueOnce(status(401))
    .mockResolvedValueOnce(okJson({ accessToken: 'tok2', accessTokenExpiresAtUtc: '2999-01-01T00:00:00Z', refreshToken: 'ref2', refreshTokenExpiresAtUtc: '2999-02-01T00:00:00Z', playerAccountId: 'p1', organizationId: 'org1', displayName: 'Ф', phoneVerified: true }))
    .mockResolvedValueOnce(okJson({ paymentIntentId: 'i1' }));
  const client = new PlayerApiClient({ baseUrl: 'https://api.test', fetchImpl: fetchImpl as unknown as typeof fetch, session, onSessionChanged: () => {} });
  await client.createTopUpIntent({ amountMinorUnits: 5000, currencyCode: 'TJS' });
  expect(fetchImpl.mock.calls[1][0]).toBe('https://api.test/api/public/player/refresh');
  expect(fetchImpl.mock.calls[2][1].headers.Authorization).toBe('Bearer tok2');
  expect(JSON.parse(fetchImpl.mock.calls[2][1].body)).toEqual({ amountMinorUnits: 5000, currencyCode: 'TJS' });
});

it('cancelReservation issues a DELETE with no body', async () => {
  const fetchImpl = mock().mockResolvedValue(okJson({ reservationId: 'r1', state: 'cancelled' }));
  const client = new PlayerApiClient({ baseUrl: 'https://api.test', fetchImpl: fetchImpl as unknown as typeof fetch, session, onSessionChanged: () => {} });
  await client.cancelReservation('r1');
  const [url, init] = fetchImpl.mock.calls[0];
  expect(url).toBe('https://api.test/api/me/reservations/r1');
  expect(init.method).toBe('DELETE');
  expect(init.body).toBeUndefined();
});

it('surfaces the 403 D8 gate as a PlayerApiError with status 403', async () => {
  const fetchImpl = mock().mockResolvedValue(status(403));
  const client = new PlayerApiClient({ baseUrl: 'https://api.test', fetchImpl: fetchImpl as unknown as typeof fetch, session, onSessionChanged: () => {} });
  await expect(client.createReservation({ startsAtUtc: '2999-01-01T10:00:00Z', endsAtUtc: '2999-01-01T11:00:00Z' }))
    .rejects.toMatchObject({ status: 403 });
});
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `bun test src/api/playerApi.test.ts`
Expected: FAIL — `client.getVisits is not a function` (and the others).

- [ ] **Step 4: Implement `authedSend` and the methods**

In `src/AFK4.Customer.Web/src/api/playerApi.ts`, extend the import of `./types` to include the new types, and add the methods. Update the import line:

```typescript
import type {
  PlayerSignInRequest, PlayerSignInResponse, PlayerDashboardDto,
  CursorPage, PlayerVisitDto, PlayerVisitReceiptDto, PlayerPurchaseDto,
  PlayerProfileDto, UpdatePlayerProfileRequest,
  PlayerTopUpIntentRequest, PlayerTopUpIntentDto,
  CreatePlayerReservationRequest, PlayerReservationDto
} from './types';
```

Add these public methods to the `PlayerApiClient` class (place them after `getDashboard()`):

```typescript
  getVisits(cursor?: string): Promise<CursorPage<PlayerVisitDto>> {
    const query = cursor ? `?cursor=${encodeURIComponent(cursor)}` : '';
    return this.authedGet<CursorPage<PlayerVisitDto>>(`/api/me/visits${query}`);
  }

  getVisitReceipt(sessionId: string): Promise<PlayerVisitReceiptDto> {
    return this.authedGet<PlayerVisitReceiptDto>(`/api/me/visits/${encodeURIComponent(sessionId)}/receipt`);
  }

  getPurchases(cursor?: string): Promise<CursorPage<PlayerPurchaseDto>> {
    const query = cursor ? `?cursor=${encodeURIComponent(cursor)}` : '';
    return this.authedGet<CursorPage<PlayerPurchaseDto>>(`/api/me/purchases${query}`);
  }

  getProfile(): Promise<PlayerProfileDto> {
    return this.authedGet<PlayerProfileDto>('/api/me/profile');
  }

  updateProfile(request: UpdatePlayerProfileRequest): Promise<PlayerProfileDto> {
    return this.authedSend<PlayerProfileDto>('PATCH', '/api/me/profile', request);
  }

  createTopUpIntent(request: PlayerTopUpIntentRequest): Promise<PlayerTopUpIntentDto> {
    return this.authedSend<PlayerTopUpIntentDto>('POST', '/api/me/wallet/top-up-intent', request);
  }

  getTopUpIntents(): Promise<PlayerTopUpIntentDto[]> {
    return this.authedGet<PlayerTopUpIntentDto[]>('/api/me/wallet/top-up-intents');
  }

  getReservations(): Promise<PlayerReservationDto[]> {
    return this.authedGet<PlayerReservationDto[]>('/api/me/reservations');
  }

  createReservation(request: CreatePlayerReservationRequest): Promise<PlayerReservationDto> {
    return this.authedSend<PlayerReservationDto>('POST', '/api/me/reservations', request);
  }

  cancelReservation(reservationId: string): Promise<PlayerReservationDto> {
    return this.authedSend<PlayerReservationDto>('DELETE', `/api/me/reservations/${encodeURIComponent(reservationId)}`);
  }
```

Add the private helper next to `authedGet` (mirrors its 401→refresh→retry shape but carries a body + method):

```typescript
  // Authenticated mutating request. Like authedGet, refreshes once on 401 and retries;
  // re-sends the same body with the refreshed token. Body omitted for verb-only calls (DELETE).
  private async authedSend<T>(method: string, path: string, body?: unknown): Promise<T> {
    const buildInit = (): RequestInit => {
      const headers = this.buildHeaders();
      if (body !== undefined) headers['Content-Type'] = 'application/json';
      return { method, headers, body: body !== undefined ? JSON.stringify(body) : undefined };
    };
    let response = await this.fetchImpl(`${this.baseUrl}${path}`, buildInit());
    if (response.status === 401 && (await this.refreshOnce())) {
      response = await this.fetchImpl(`${this.baseUrl}${path}`, buildInit());
    }
    if (!response.ok) throw await PlayerApiClient.toError(response);
    return JSON.parse(await response.text()) as T;
  }
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `bun test src/api/playerApi.test.ts`
Expected: PASS (all existing + 5 new).

- [ ] **Step 6: Type-check**

Run: `bunx tsc -b`
Expected: no errors.

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.Customer.Web/src/api/types.ts src/AFK4.Customer.Web/src/api/playerApi.ts src/AFK4.Customer.Web/src/api/playerApi.test.ts
git commit -m "feat(customer-web): player API client methods for reads/writes"
```

---

## Task 2: Branding bootstrap wiring in `App`

Completes spec build-seq item 4: resolve tenant key → fetch branding → apply theme → feed the real `organizationId` and club name into sign-in, with a default-theme fallback. Removes the hard dependency on `VITE_DEMO_ORG_ID` (kept only as a last-resort dev fallback).

**Files:**
- Create: `src/AFK4.Customer.Web/src/branding/useBranding.ts`
- Test: `src/AFK4.Customer.Web/src/branding/useBranding.test.ts`
- Modify: `src/AFK4.Customer.Web/src/App.tsx`

- [ ] **Step 1: Write the failing test for `useBranding`**

Create `src/AFK4.Customer.Web/src/branding/useBranding.test.ts`:

```typescript
import { it, expect, mock } from 'bun:test';
import { renderHook, waitFor } from '@testing-library/react';
import { useBranding } from './useBranding';

it('resolves a tenant key, fetches branding and reports ready', async () => {
  const fetchBranding = mock().mockResolvedValue({ organizationId: 'org-9', name: 'Cyber Arena', logoUrl: null, accentColor: '#ff0080' });
  const apply = mock();
  const { result } = renderHook(() => useBranding({
    hostname: 'cyber.portal.afk4.net', search: '', baseUrl: '', fetchBranding, applyThemeImpl: apply
  }));
  await waitFor(() => expect(result.current.status).toBe('ready'));
  if (result.current.status !== 'ready') throw new Error('not ready');
  expect(result.current.organizationId).toBe('org-9');
  expect(result.current.brandName).toBe('Cyber Arena');
  expect(apply).toHaveBeenCalledTimes(1);
});

it('falls back to defaults when no branding resolves', async () => {
  const fetchBranding = mock().mockResolvedValue(null);
  const { result } = renderHook(() => useBranding({
    hostname: 'localhost', search: '', baseUrl: '', fallbackOrganizationId: 'dev-org',
    fetchBranding, applyThemeImpl: mock()
  }));
  await waitFor(() => expect(result.current.status).toBe('ready'));
  if (result.current.status !== 'ready') throw new Error('not ready');
  expect(result.current.organizationId).toBe('dev-org');
  expect(result.current.brandName).toBe('AFK4');
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `bun test src/branding/useBranding.test.ts`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement `useBranding`**

Create `src/AFK4.Customer.Web/src/branding/useBranding.ts`:

```typescript
import { useEffect, useRef, useState } from 'react';
import type { TenantBrandingDto } from '../api/types';
import { resolveTenantKey } from './resolveTenantKey';
import { applyTheme } from './applyTheme';
import { fetchTenantBranding } from '../api/brandingApi';

interface UseBrandingOptions {
  hostname: string;
  search: string;
  baseUrl: string;
  fallbackOrganizationId?: string;
  // Seams for tests.
  fetchBranding?: (baseUrl: string, tenantKey: string) => Promise<TenantBrandingDto | null>;
  applyThemeImpl?: (branding: TenantBrandingDto | null) => void;
}

export type BrandingState =
  | { status: 'loading' }
  | { status: 'ready'; organizationId: string; brandName: string; logoUrl: string | null };

export function useBranding(options: UseBrandingOptions): BrandingState {
  const [state, setState] = useState<BrandingState>({ status: 'loading' });
  // Bootstrap exactly once; option values are read from the first render.
  const opts = useRef(options);

  useEffect(() => {
    let cancelled = false;
    const { hostname, search, baseUrl, fallbackOrganizationId } = opts.current;
    const fetchBranding = opts.current.fetchBranding ?? fetchTenantBranding;
    const apply = opts.current.applyThemeImpl ?? applyTheme;

    async function bootstrap() {
      const key = resolveTenantKey(hostname, search);
      let branding: TenantBrandingDto | null = null;
      if (key) {
        try {
          branding = await fetchBranding(baseUrl, key);
        } catch {
          branding = null;
        }
      }
      if (cancelled) return;
      apply(branding);
      setState({
        status: 'ready',
        organizationId: branding?.organizationId ?? fallbackOrganizationId ?? '',
        brandName: branding?.name ?? 'AFK4',
        logoUrl: branding?.logoUrl ?? null
      });
    }

    void bootstrap();
    return () => { cancelled = true; };
  }, []);

  return state;
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `bun test src/branding/useBranding.test.ts`
Expected: PASS (2/2).

- [ ] **Step 5: Wire `useBranding` into `App.tsx`**

In `src/AFK4.Customer.Web/src/App.tsx`, add the import and use the hook to drive sign-in. Add near the other imports:

```typescript
import { useBranding } from './branding/useBranding';
```

Inside `App`, after the `api` is resolved and before the `if (!session)` block, add:

```typescript
  const branding = useBranding({
    hostname: typeof window === 'undefined' ? '' : window.location.hostname,
    search: typeof window === 'undefined' ? '' : window.location.search,
    baseUrl: API_BASE,
    fallbackOrganizationId: import.meta.env.VITE_DEMO_ORG_ID ?? ''
  });
```

Replace the `if (!session)` sign-in block with a branding-aware version:

```typescript
  if (!session) {
    if (branding.status === 'loading') {
      return (
        <main className="flex min-h-dvh items-center justify-center" role="status" aria-label="Загрузка">
          <div className="h-10 w-10 animate-pulse rounded-full bg-[var(--color-surface)]" />
        </main>
      );
    }
    return (
      <SignInScreen
        organizationId={branding.organizationId}
        brandName={branding.brandName}
        signIn={(req) => api.signIn(req)}
        onSignedIn={handleSignedIn}
      />
    );
  }
```

- [ ] **Step 6: Run the full suite + type-check**

Run: `bun test && bunx tsc -b`
Expected: PASS, no type errors.

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.Customer.Web/src/branding/useBranding.ts src/AFK4.Customer.Web/src/branding/useBranding.test.ts src/AFK4.Customer.Web/src/App.tsx
git commit -m "feat(customer-web): branding bootstrap wired into sign-in (real org id + theme)"
```

---

## Task 3: Date/duration formatters (`lib/datetime.ts`)

**Files:**
- Create: `src/AFK4.Customer.Web/src/lib/datetime.ts`
- Test: `src/AFK4.Customer.Web/src/lib/datetime.test.ts`

- [ ] **Step 1: Write the failing test**

Create `src/AFK4.Customer.Web/src/lib/datetime.test.ts`:

```typescript
import { it, expect } from 'bun:test';
import { formatDateTime, formatDuration } from './datetime';

it('formats a duration between two instants as "Hч Mм"', () => {
  expect(formatDuration('2026-06-03T10:00:00Z', '2026-06-03T12:30:00Z')).toBe('2ч 30м');
});

it('formats a sub-hour duration as just minutes', () => {
  expect(formatDuration('2026-06-03T10:00:00Z', '2026-06-03T10:45:00Z')).toBe('45м');
});

it('returns an empty string for an invalid date', () => {
  expect(formatDateTime('not-a-date')).toBe('');
});

it('renders a valid instant containing a time separator', () => {
  expect(formatDateTime('2026-06-03T20:05:00Z')).toContain(':');
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `bun test src/lib/datetime.test.ts`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement**

Create `src/AFK4.Customer.Web/src/lib/datetime.ts`:

```typescript
// Localized day+time for list rows and receipts. Locale/timezone come from the
// runtime (Intl); tests assert structure, not exact localized text, to stay TZ-stable.
export function formatDateTime(iso: string): string {
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return '';
  return date.toLocaleString('ru-RU', {
    day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit'
  });
}

// Whole-minute duration between two instants (end defaults to now for open visits),
// rendered "Hч Mм" / "Mм". Pure integer math — deterministic across timezones.
export function formatDuration(startIso: string, endIso: string | null): string {
  const start = Date.parse(startIso);
  const end = endIso ? Date.parse(endIso) : Date.now();
  if (Number.isNaN(start) || Number.isNaN(end)) return '';
  const totalMinutes = Math.max(0, Math.round((end - start) / 60_000));
  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;
  return hours > 0 ? `${hours}ч ${minutes}м` : `${minutes}м`;
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `bun test src/lib/datetime.test.ts`
Expected: PASS (4/4).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Customer.Web/src/lib/datetime.ts src/AFK4.Customer.Web/src/lib/datetime.test.ts
git commit -m "feat(customer-web): date/duration formatters"
```

---

## Task 4: Toast infrastructure (`components/ui/toast.tsx`)

Minimal port of the Platform.Web toast, styled with `--color-*` tokens. Used by wallet/reservation success+error and offline-write blocks.

**Files:**
- Create: `src/AFK4.Customer.Web/src/components/ui/toast.tsx`
- Test: `src/AFK4.Customer.Web/src/components/ui/toast.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `src/AFK4.Customer.Web/src/components/ui/toast.test.tsx`:

```typescript
import { it, expect } from 'bun:test';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { ToastProvider, useToast } from './toast';

function Trigger() {
  const { toast } = useToast();
  return <button onClick={() => toast({ title: 'Заявка отправлена', variant: 'success' })}>go</button>;
}

it('shows a toast and auto-dismisses it', async () => {
  render(
    <ToastProvider autoDismissMs={50}>
      <Trigger />
    </ToastProvider>
  );
  fireEvent.click(screen.getByText('go'));
  expect(await screen.findByText('Заявка отправлена')).toBeInTheDocument();
  await waitFor(() => expect(screen.queryByText('Заявка отправлена')).not.toBeInTheDocument());
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `bun test src/components/ui/toast.test.tsx`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement**

Create `src/AFK4.Customer.Web/src/components/ui/toast.tsx`:

```typescript
import { createContext, useCallback, useContext, useRef, useState, type ReactNode } from 'react';
import { cn } from '@/lib/utils';

export type ToastVariant = 'success' | 'error';
export interface ToastOptions { title: string; variant?: ToastVariant; }
interface ActiveToast extends ToastOptions { id: number; }

interface ToastContextValue { toast: (options: ToastOptions) => void; }
const ToastContext = createContext<ToastContextValue | null>(null);

export function ToastProvider({ children, autoDismissMs = 4000 }: { children: ReactNode; autoDismissMs?: number }) {
  const [toasts, setToasts] = useState<ActiveToast[]>([]);
  const nextId = useRef(0);

  const toast = useCallback((options: ToastOptions) => {
    const id = nextId.current++;
    setToasts((prev) => [...prev, { variant: 'success', ...options, id }]);
    setTimeout(() => setToasts((prev) => prev.filter((entry) => entry.id !== id)), autoDismissMs);
  }, [autoDismissMs]);

  return (
    <ToastContext.Provider value={{ toast }}>
      {children}
      <div
        className="pointer-events-none fixed inset-x-0 bottom-20 z-50 flex flex-col items-center gap-2 px-4"
        role="region"
        aria-label="Уведомления"
      >
        {toasts.map((entry) => (
          <div
            key={entry.id}
            role="status"
            className={cn(
              'pointer-events-auto w-full max-w-sm rounded-xl border px-4 py-3 text-sm shadow-lg',
              entry.variant === 'error'
                ? 'border-red-500/40 bg-red-500/15 text-red-200'
                : 'border-[var(--color-border)] bg-[var(--color-surface-2)] text-[var(--text-1)]'
            )}
          >
            {entry.title}
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  );
}

export function useToast(): ToastContextValue {
  const ctx = useContext(ToastContext);
  if (ctx === null) throw new Error('useToast must be used within ToastProvider');
  return ctx;
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `bun test src/components/ui/toast.test.tsx`
Expected: PASS.

- [ ] **Step 5: Wrap the app in `ToastProvider`**

In `src/AFK4.Customer.Web/src/App.tsx`, import the provider and wrap the authenticated `AppShell` return. Add import:

```typescript
import { ToastProvider } from './components/ui/toast';
```

Wrap the final return's `<AppShell>...</AppShell>` in `<ToastProvider>...</ToastProvider>`:

```typescript
  return (
    <ToastProvider>
      <AppShell active={tabForRoute(route)} onNavigate={navigate}>
        {route.kind === 'dashboard' && <DashboardScreen api={api} displayName={session.displayName} />}
        {route.kind !== 'dashboard' && (
          <section className="px-6 py-10 text-[var(--text-2)]">Скоро здесь появится этот раздел.</section>
        )}
      </AppShell>
    </ToastProvider>
  );
```

(Subsequent tasks replace the placeholder branches with real screens.)

- [ ] **Step 6: Run the full suite + type-check; commit**

Run: `bun test && bunx tsc -b`

```bash
git add src/AFK4.Customer.Web/src/components/ui/toast.tsx src/AFK4.Customer.Web/src/components/ui/toast.test.tsx src/AFK4.Customer.Web/src/App.tsx
git commit -m "feat(customer-web): toast provider + app wiring"
```

---

## Task 5: `useCursorList` pagination hook

**Files:**
- Create: `src/AFK4.Customer.Web/src/lib/useCursorList.ts`
- Test: `src/AFK4.Customer.Web/src/lib/useCursorList.test.ts`

- [ ] **Step 1: Write the failing test**

Create `src/AFK4.Customer.Web/src/lib/useCursorList.test.ts`:

```typescript
import { it, expect, mock } from 'bun:test';
import { act, renderHook, waitFor } from '@testing-library/react';
import { useCursorList } from './useCursorList';

it('loads the first page and exposes hasMore from nextCursor', async () => {
  const fetchPage = mock().mockResolvedValue({ items: [{ id: 'a' }], nextCursor: 'C2' });
  const { result } = renderHook(() => useCursorList(fetchPage));
  await waitFor(() => expect(result.current.status).toBe('ready'));
  if (result.current.status !== 'ready') throw new Error('not ready');
  expect(result.current.items).toEqual([{ id: 'a' }]);
  expect(result.current.hasMore).toBe(true);
});

it('appends the next page on loadMore and clears hasMore when exhausted', async () => {
  const fetchPage = mock()
    .mockResolvedValueOnce({ items: [{ id: 'a' }], nextCursor: 'C2' })
    .mockResolvedValueOnce({ items: [{ id: 'b' }], nextCursor: null });
  const { result } = renderHook(() => useCursorList(fetchPage));
  await waitFor(() => expect(result.current.status).toBe('ready'));
  if (result.current.status !== 'ready') throw new Error('not ready');
  await act(async () => { result.current.loadMore(); });
  await waitFor(() => {
    if (result.current.status !== 'ready') throw new Error('not ready');
    expect(result.current.items).toEqual([{ id: 'a' }, { id: 'b' }]);
    expect(result.current.hasMore).toBe(false);
  });
  expect(fetchPage.mock.calls[1][0]).toBe('C2');
});

it('reports an error state on a failed first page', async () => {
  const fetchPage = mock().mockRejectedValue(new Error('boom'));
  const { result } = renderHook(() => useCursorList(fetchPage));
  await waitFor(() => expect(result.current.status).toBe('error'));
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `bun test src/lib/useCursorList.test.ts`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement**

Create `src/AFK4.Customer.Web/src/lib/useCursorList.ts`:

```typescript
import { useCallback, useEffect, useRef, useState } from 'react';
import type { CursorPage } from '@/api/types';

export type CursorListState<T> =
  | { status: 'loading' }
  | { status: 'error'; retry: () => void }
  | {
      status: 'ready';
      items: T[];
      hasMore: boolean;
      loadingMore: boolean;
      loadMore: () => void;
      retry: () => void;
    };

// Generic forward-only cursor pagination. First page loads on mount; loadMore()
// appends. fetchPage is read from a ref so a screen can pass an inline lambda
// without retriggering the initial load on every render.
export function useCursorList<T>(fetchPage: (cursor?: string) => Promise<CursorPage<T>>): CursorListState<T> {
  const fetchRef = useRef(fetchPage);
  fetchRef.current = fetchPage;

  const [phase, setPhase] = useState<'loading' | 'error' | 'ready'>('loading');
  const [items, setItems] = useState<T[]>([]);
  const [cursor, setCursor] = useState<string | null>(null);
  const [loadingMore, setLoadingMore] = useState(false);
  const [reloadTick, setReloadTick] = useState(0);

  useEffect(() => {
    let cancelled = false;
    setPhase('loading');
    fetchRef.current()
      .then((page) => {
        if (cancelled) return;
        setItems(page.items);
        setCursor(page.nextCursor);
        setPhase('ready');
      })
      .catch(() => { if (!cancelled) setPhase('error'); });
    return () => { cancelled = true; };
  }, [reloadTick]);

  const retry = useCallback(() => setReloadTick((tick) => tick + 1), []);

  const loadMore = useCallback(() => {
    if (cursor === null) return;
    setLoadingMore(true);
    fetchRef.current(cursor)
      .then((page) => {
        setItems((prev) => [...prev, ...page.items]);
        setCursor(page.nextCursor);
      })
      .finally(() => setLoadingMore(false));
  }, [cursor]);

  if (phase === 'loading') return { status: 'loading' };
  if (phase === 'error') return { status: 'error', retry };
  return { status: 'ready', items, hasMore: cursor !== null, loadingMore, loadMore, retry };
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `bun test src/lib/useCursorList.test.ts`
Expected: PASS (3/3).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Customer.Web/src/lib/useCursorList.ts src/AFK4.Customer.Web/src/lib/useCursorList.test.ts
git commit -m "feat(customer-web): generic cursor pagination hook"
```

---

## Task 6: Visit history screen

**Files:**
- Create: `src/AFK4.Customer.Web/src/screens/history/VisitsScreen.tsx`
- Test: `src/AFK4.Customer.Web/src/screens/history/VisitsScreen.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `src/AFK4.Customer.Web/src/screens/history/VisitsScreen.test.tsx`:

```typescript
import { it, expect, mock } from 'bun:test';
import { render, screen } from '@testing-library/react';
import { VisitsScreen } from './VisitsScreen';
import type { PlayerApiClient } from '@/api/playerApi';

function apiWith(page: unknown) {
  return { getVisits: mock().mockResolvedValue(page) } as unknown as PlayerApiClient;
}

it('renders a visit row with seat, total and a receipt link', async () => {
  const api = apiWith({
    items: [{
      sessionId: 's1', seatId: 'seat1', seatName: 'PC-14',
      startedAtUtc: '2026-06-01T10:00:00Z', endedAtUtc: '2026-06-01T12:00:00Z',
      timeChargeMinorUnits: 12000, posTotalMinorUnits: 3000, grandTotalMinorUnits: 15000,
      currencyCode: 'TJS', hasReceipt: true
    }],
    nextCursor: null
  });
  render(<VisitsScreen api={api} onOpenReceipt={() => {}} />);
  expect(await screen.findByText('PC-14')).toBeInTheDocument();
  expect(screen.getByText('150,00 TJS')).toBeInTheDocument();
  expect(screen.getByRole('button', { name: /чек/i })).toBeInTheDocument();
});

it('renders an empty state when there are no visits', async () => {
  const api = apiWith({ items: [], nextCursor: null });
  render(<VisitsScreen api={api} onOpenReceipt={() => {}} />);
  expect(await screen.findByText('Пока нет визитов')).toBeInTheDocument();
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `bun test src/screens/history/VisitsScreen.test.tsx`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement**

Create `src/AFK4.Customer.Web/src/screens/history/VisitsScreen.tsx`:

```typescript
import { useCallback } from 'react';
import type { PlayerApiClient } from '@/api/playerApi';
import type { PlayerVisitDto } from '@/api/types';
import { useCursorList } from '@/lib/useCursorList';
import { formatMoney } from '@/lib/money';
import { formatDateTime, formatDuration } from '@/lib/datetime';

export function VisitsScreen({ api, onOpenReceipt }: { api: PlayerApiClient; onOpenReceipt: (sessionId: string) => void }) {
  const fetchPage = useCallback((cursor?: string) => api.getVisits(cursor), [api]);
  const list = useCursorList<PlayerVisitDto>(fetchPage);

  if (list.status === 'loading') {
    return (
      <div className="space-y-3 px-6 py-6" role="status" aria-label="Загрузка визитов">
        {[0, 1, 2].map((i) => <div key={i} className="h-20 animate-pulse rounded-2xl bg-[var(--color-surface)]" />)}
      </div>
    );
  }
  if (list.status === 'error') {
    return (
      <div className="px-6 py-10 text-center">
        <p className="text-sm text-red-400">Не удалось загрузить историю.</p>
        <button type="button" onClick={list.retry} className="mt-3 text-sm text-[var(--accent)] focus-visible:outline-2 focus-visible:outline-[var(--accent)]">Повторить</button>
      </div>
    );
  }
  if (list.items.length === 0) {
    return <p className="px-6 py-12 text-center text-[var(--text-2)]">Пока нет визитов</p>;
  }

  return (
    <div className="space-y-3 px-6 py-6">
      {list.items.map((visit) => (
        <article key={visit.sessionId} className="rounded-2xl bg-[var(--color-surface)] p-4">
          <div className="flex items-center justify-between">
            <span className="font-bold text-[var(--text-1)]">{visit.seatName}</span>
            <span className="text-lg font-extrabold tracking-tight">{formatMoney(visit.grandTotalMinorUnits, visit.currencyCode)}</span>
          </div>
          <p className="mt-1 text-sm text-[var(--text-2)]">
            {formatDateTime(visit.startedAtUtc)} · {formatDuration(visit.startedAtUtc, visit.endedAtUtc)}
          </p>
          {visit.hasReceipt && (
            <button
              type="button"
              onClick={() => onOpenReceipt(visit.sessionId)}
              className="mt-2 min-h-[44px] text-sm text-[var(--accent)] focus-visible:outline-2 focus-visible:outline-[var(--accent)]"
            >
              Чек →
            </button>
          )}
        </article>
      ))}
      {list.hasMore && (
        <button
          type="button"
          onClick={list.loadMore}
          disabled={list.loadingMore}
          className="min-h-[44px] w-full rounded-xl border border-[var(--color-border)] text-sm text-[var(--text-2)] disabled:opacity-50 focus-visible:outline-2 focus-visible:outline-[var(--accent)]"
        >
          {list.loadingMore ? 'Загрузка…' : 'Показать ещё'}
        </button>
      )}
    </div>
  );
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `bun test src/screens/history/VisitsScreen.test.tsx`
Expected: PASS (2/2).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Customer.Web/src/screens/history/VisitsScreen.tsx src/AFK4.Customer.Web/src/screens/history/VisitsScreen.test.tsx
git commit -m "feat(customer-web): visit history screen"
```

---

## Task 7: Receipt screen

**Files:**
- Create: `src/AFK4.Customer.Web/src/screens/history/ReceiptScreen.tsx`
- Test: `src/AFK4.Customer.Web/src/screens/history/ReceiptScreen.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `src/AFK4.Customer.Web/src/screens/history/ReceiptScreen.test.tsx`:

```typescript
import { it, expect, mock } from 'bun:test';
import { render, screen } from '@testing-library/react';
import { ReceiptScreen } from './ReceiptScreen';
import { PlayerApiError } from '@/api/playerApi';
import type { PlayerApiClient } from '@/api/playerApi';

it('renders the receipt with its POS lines and grand total', async () => {
  const api = { getVisitReceipt: mock().mockResolvedValue({
    receiptNumber: 'R-1001', createdAtUtc: '2026-06-01T12:00:00Z', sessionId: 's1', seatName: 'PC-14',
    startedAtUtc: '2026-06-01T10:00:00Z', endedAtUtc: '2026-06-01T12:00:00Z', timeChargeMinorUnits: 12000,
    posLines: [{ productName: 'Кола', quantity: 2, unitPriceMinorUnits: 1500, lineTotalMinorUnits: 3000 }],
    posTotalMinorUnits: 3000, grandTotalMinorUnits: 15000, currencyCode: 'TJS'
  }) } as unknown as PlayerApiClient;
  render(<ReceiptScreen api={api} sessionId="s1" onBack={() => {}} />);
  expect(await screen.findByText('R-1001')).toBeInTheDocument();
  expect(screen.getByText('Кола')).toBeInTheDocument();
  expect(screen.getByText('150,00 TJS')).toBeInTheDocument();
});

it('renders a not-found state when the receipt is foreign or missing (404)', async () => {
  const api = { getVisitReceipt: mock().mockRejectedValue(new PlayerApiError(404, 'Not Found')) } as unknown as PlayerApiClient;
  render(<ReceiptScreen api={api} sessionId="sX" onBack={() => {}} />);
  expect(await screen.findByText('Чек не найден')).toBeInTheDocument();
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `bun test src/screens/history/ReceiptScreen.test.tsx`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement**

Create `src/AFK4.Customer.Web/src/screens/history/ReceiptScreen.tsx`:

```typescript
import { useEffect, useState } from 'react';
import type { PlayerApiClient } from '@/api/playerApi';
import type { PlayerVisitReceiptDto } from '@/api/types';
import { formatMoney } from '@/lib/money';
import { formatDateTime, formatDuration } from '@/lib/datetime';

type Load =
  | { state: 'loading' }
  | { state: 'notfound' }
  | { state: 'error' }
  | { state: 'ready'; receipt: PlayerVisitReceiptDto };

export function ReceiptScreen({ api, sessionId, onBack }: { api: PlayerApiClient; sessionId: string; onBack: () => void }) {
  const [load, setLoad] = useState<Load>({ state: 'loading' });

  useEffect(() => {
    let cancelled = false;
    api.getVisitReceipt(sessionId)
      .then((receipt) => { if (!cancelled) setLoad({ state: 'ready', receipt }); })
      .catch((error: unknown) => {
        if (cancelled) return;
        const status = (error as { status?: number }).status;
        setLoad({ state: status === 404 ? 'notfound' : 'error' });
      });
    return () => { cancelled = true; };
  }, [api, sessionId]);

  return (
    <main className="px-6 py-6">
      <button type="button" onClick={onBack} className="mb-4 min-h-[44px] text-sm text-[var(--text-2)] focus-visible:outline-2 focus-visible:outline-[var(--accent)]">← Назад</button>

      {load.state === 'loading' && <div role="status" aria-label="Загрузка чека" className="h-48 animate-pulse rounded-2xl bg-[var(--color-surface)]" />}
      {load.state === 'notfound' && <p className="py-12 text-center text-[var(--text-2)]">Чек не найден</p>}
      {load.state === 'error' && <p className="py-12 text-center text-red-400">Не удалось загрузить чек.</p>}

      {load.state === 'ready' && (
        <article className="space-y-4 rounded-2xl bg-[var(--color-surface)] p-5">
          <header className="flex items-baseline justify-between">
            <h1 className="text-lg font-extrabold tracking-tight">{load.receipt.receiptNumber}</h1>
            <span className="text-sm text-[var(--text-2)]">{formatDateTime(load.receipt.createdAtUtc)}</span>
          </header>
          <p className="text-sm text-[var(--text-2)]">
            {load.receipt.seatName} · {formatDuration(load.receipt.startedAtUtc, load.receipt.endedAtUtc)}
          </p>

          <div className="flex justify-between border-t border-[var(--color-border)] pt-3 text-sm">
            <span className="text-[var(--text-2)]">Время</span>
            <span>{formatMoney(load.receipt.timeChargeMinorUnits, load.receipt.currencyCode)}</span>
          </div>

          {load.receipt.posLines.length > 0 && (
            <ul className="space-y-1.5">
              {load.receipt.posLines.map((line, index) => (
                <li key={index} className="flex justify-between text-sm">
                  <span className="text-[var(--text-2)]">{line.productName} × {line.quantity}</span>
                  <span>{formatMoney(line.lineTotalMinorUnits, load.receipt.currencyCode)}</span>
                </li>
              ))}
            </ul>
          )}

          <div className="flex justify-between border-t border-[var(--color-border)] pt-3 text-base font-extrabold">
            <span>Итого</span>
            <span>{formatMoney(load.receipt.grandTotalMinorUnits, load.receipt.currencyCode)}</span>
          </div>
        </article>
      )}
    </main>
  );
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `bun test src/screens/history/ReceiptScreen.test.tsx`
Expected: PASS (2/2).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Customer.Web/src/screens/history/ReceiptScreen.tsx src/AFK4.Customer.Web/src/screens/history/ReceiptScreen.test.tsx
git commit -m "feat(customer-web): receipt screen"
```

---

## Task 8: Purchases screen + History/Purchases tabs + routing

Adds the standalone purchases list and a segmented switch at the top of the История tab. Both `/history` and `/purchases` map to the `history` bottom-nav tab.

**Files:**
- Create: `src/AFK4.Customer.Web/src/screens/purchases/PurchasesScreen.tsx`
- Test: `src/AFK4.Customer.Web/src/screens/purchases/PurchasesScreen.test.tsx`
- Create: `src/AFK4.Customer.Web/src/screens/history/HistoryTabs.tsx`
- Modify: `src/AFK4.Customer.Web/src/routing.ts`

- [ ] **Step 1: Extend routing (no test — pure type/string change covered by App tests later)**

In `src/AFK4.Customer.Web/src/routing.ts`, add the `purchases` route and path. Update the union, `resolvePlayerRoute`, and `routePath`:

```typescript
export type PlayerRoute =
  | { kind: 'dashboard' }
  | { kind: 'history' }
  | { kind: 'purchases' }
  | { kind: 'receipt'; sessionId: string }
  | { kind: 'reservations' }
  | { kind: 'profile' };
```

In `resolvePlayerRoute`, add before the `reservations` check:

```typescript
  if (parts[0] === 'purchases') return { kind: 'purchases' };
```

In `routePath`'s switch, add:

```typescript
    case 'purchases': return '/purchases';
```

- [ ] **Step 2: Write the failing test for `PurchasesScreen`**

Create `src/AFK4.Customer.Web/src/screens/purchases/PurchasesScreen.test.tsx`:

```typescript
import { it, expect, mock } from 'bun:test';
import { render, screen } from '@testing-library/react';
import { PurchasesScreen } from './PurchasesScreen';
import type { PlayerApiClient } from '@/api/playerApi';

it('renders a purchase with its lines and total', async () => {
  const api = { getPurchases: mock().mockResolvedValue({
    items: [{
      posSaleId: 'q1', createdAtUtc: '2026-06-02T15:00:00Z', totalMinorUnits: 4500, currencyCode: 'TJS',
      lines: [{ productName: 'Энергетик', quantity: 3, unitPriceMinorUnits: 1500, lineTotalMinorUnits: 4500 }]
    }],
    nextCursor: null
  }) } as unknown as PlayerApiClient;
  render(<PurchasesScreen api={api} />);
  expect(await screen.findByText('Энергетик × 3')).toBeInTheDocument();
  expect(screen.getByText('45,00 TJS')).toBeInTheDocument();
});

it('renders an empty state when there are no purchases', async () => {
  const api = { getPurchases: mock().mockResolvedValue({ items: [], nextCursor: null }) } as unknown as PlayerApiClient;
  render(<PurchasesScreen api={api} />);
  expect(await screen.findByText('Пока нет покупок')).toBeInTheDocument();
});
```

- [ ] **Step 3: Run to verify it fails**

Run: `bun test src/screens/purchases/PurchasesScreen.test.tsx`
Expected: FAIL — module not found.

- [ ] **Step 4: Implement `PurchasesScreen`**

Create `src/AFK4.Customer.Web/src/screens/purchases/PurchasesScreen.tsx`:

```typescript
import { useCallback } from 'react';
import type { PlayerApiClient } from '@/api/playerApi';
import type { PlayerPurchaseDto } from '@/api/types';
import { useCursorList } from '@/lib/useCursorList';
import { formatMoney } from '@/lib/money';
import { formatDateTime } from '@/lib/datetime';

export function PurchasesScreen({ api }: { api: PlayerApiClient }) {
  const fetchPage = useCallback((cursor?: string) => api.getPurchases(cursor), [api]);
  const list = useCursorList<PlayerPurchaseDto>(fetchPage);

  if (list.status === 'loading') {
    return (
      <div className="space-y-3 px-6 py-6" role="status" aria-label="Загрузка покупок">
        {[0, 1, 2].map((i) => <div key={i} className="h-20 animate-pulse rounded-2xl bg-[var(--color-surface)]" />)}
      </div>
    );
  }
  if (list.status === 'error') {
    return (
      <div className="px-6 py-10 text-center">
        <p className="text-sm text-red-400">Не удалось загрузить покупки.</p>
        <button type="button" onClick={list.retry} className="mt-3 text-sm text-[var(--accent)] focus-visible:outline-2 focus-visible:outline-[var(--accent)]">Повторить</button>
      </div>
    );
  }
  if (list.items.length === 0) {
    return <p className="px-6 py-12 text-center text-[var(--text-2)]">Пока нет покупок</p>;
  }

  return (
    <div className="space-y-3 px-6 py-6">
      {list.items.map((purchase) => (
        <article key={purchase.posSaleId} className="rounded-2xl bg-[var(--color-surface)] p-4">
          <div className="flex items-center justify-between">
            <span className="text-sm text-[var(--text-2)]">{formatDateTime(purchase.createdAtUtc)}</span>
            <span className="text-lg font-extrabold tracking-tight">{formatMoney(purchase.totalMinorUnits, purchase.currencyCode)}</span>
          </div>
          <ul className="mt-2 space-y-1">
            {purchase.lines.map((line, index) => (
              <li key={index} className="flex justify-between text-sm text-[var(--text-2)]">
                <span>{line.productName} × {line.quantity}</span>
                <span>{formatMoney(line.lineTotalMinorUnits, purchase.currencyCode)}</span>
              </li>
            ))}
          </ul>
        </article>
      ))}
      {list.hasMore && (
        <button
          type="button"
          onClick={list.loadMore}
          disabled={list.loadingMore}
          className="min-h-[44px] w-full rounded-xl border border-[var(--color-border)] text-sm text-[var(--text-2)] disabled:opacity-50 focus-visible:outline-2 focus-visible:outline-[var(--accent)]"
        >
          {list.loadingMore ? 'Загрузка…' : 'Показать ещё'}
        </button>
      )}
    </div>
  );
}
```

- [ ] **Step 5: Run to verify it passes**

Run: `bun test src/screens/purchases/PurchasesScreen.test.tsx`
Expected: PASS (2/2).

- [ ] **Step 6: Create the `HistoryTabs` segmented switch**

Create `src/AFK4.Customer.Web/src/screens/history/HistoryTabs.tsx`:

```typescript
import { cn } from '@/lib/utils';

export type HistoryView = 'visits' | 'purchases';

export function HistoryTabs({ active, onChange }: { active: HistoryView; onChange: (view: HistoryView) => void }) {
  const tabs: { view: HistoryView; label: string }[] = [
    { view: 'visits', label: 'Визиты' },
    { view: 'purchases', label: 'Покупки' }
  ];
  return (
    <div role="tablist" aria-label="История" className="flex gap-1 px-6 pt-6">
      {tabs.map(({ view, label }) => (
        <button
          key={view}
          type="button"
          role="tab"
          aria-selected={active === view}
          onClick={() => onChange(view)}
          className={cn(
            'min-h-[44px] flex-1 rounded-xl text-sm font-medium transition-colors focus-visible:outline-2 focus-visible:outline-[var(--accent)]',
            active === view ? 'bg-[var(--color-surface-2)] text-[var(--text-1)]' : 'text-[var(--text-3)] hover:text-[var(--text-2)]'
          )}
        >
          {label}
        </button>
      ))}
    </div>
  );
}
```

- [ ] **Step 7: Type-check + commit**

Run: `bun test && bunx tsc -b`

```bash
git add src/AFK4.Customer.Web/src/screens/purchases src/AFK4.Customer.Web/src/screens/history/HistoryTabs.tsx src/AFK4.Customer.Web/src/routing.ts
git commit -m "feat(customer-web): purchases screen + history/purchases tabs + routing"
```

---

## Task 9: Wallet panel (top-up request + intent list, D8 gate) on the dashboard

**Files:**
- Create: `src/AFK4.Customer.Web/src/screens/wallet/WalletPanel.tsx`
- Test: `src/AFK4.Customer.Web/src/screens/wallet/WalletPanel.test.tsx`
- Modify: `src/AFK4.Customer.Web/src/screens/dashboard/DashboardScreen.tsx`

- [ ] **Step 1: Write the failing test**

Create `src/AFK4.Customer.Web/src/screens/wallet/WalletPanel.test.tsx`:

```typescript
import { it, expect, mock } from 'bun:test';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { ToastProvider } from '@/components/ui/toast';
import { WalletPanel } from './WalletPanel';
import type { PlayerApiClient } from '@/api/playerApi';

function renderPanel(api: PlayerApiClient, phoneVerified: boolean) {
  return render(
    <ToastProvider autoDismissMs={1000}>
      <WalletPanel api={api} phoneVerified={phoneVerified} />
    </ToastProvider>
  );
}

it('closes the top-up form with an explanation when the phone is unverified', async () => {
  const api = { getTopUpIntents: mock().mockResolvedValue([]) } as unknown as PlayerApiClient;
  renderPanel(api, false);
  expect(await screen.findByText(/подтвердите номер/i)).toBeInTheDocument();
  expect(screen.queryByLabelText('Сумма')).not.toBeInTheDocument();
});

it('submits a top-up request and shows it in the intent list', async () => {
  const api = {
    getTopUpIntents: mock()
      .mockResolvedValueOnce([])
      .mockResolvedValueOnce([{ paymentIntentId: 'i1', amountMinorUnits: 5000, currencyCode: 'TJS', state: 'pending', purpose: 'wallet_topup', method: 'counter', createdAtUtc: '2026-06-03T10:00:00Z', fulfilledAtUtc: null, isExpired: false }]),
    createTopUpIntent: mock().mockResolvedValue({ paymentIntentId: 'i1' })
  } as unknown as PlayerApiClient;
  renderPanel(api, true);
  const amount = await screen.findByLabelText('Сумма');
  fireEvent.change(amount, { target: { value: '50' } });
  fireEvent.click(screen.getByRole('button', { name: /запросить/i }));
  await waitFor(() => expect(api.createTopUpIntent).toHaveBeenCalledWith({ amountMinorUnits: 5000, currencyCode: 'TJS' }));
  expect(await screen.findByText('Ожидает')).toBeInTheDocument();
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `bun test src/screens/wallet/WalletPanel.test.tsx`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement `WalletPanel`**

Create `src/AFK4.Customer.Web/src/screens/wallet/WalletPanel.tsx`:

```typescript
import { useCallback, useEffect, useState, type FormEvent } from 'react';
import type { PlayerApiClient } from '@/api/playerApi';
import type { PlayerTopUpIntentDto } from '@/api/types';
import { majorToMinor } from '@afk4/money';
import { formatMoney } from '@/lib/money';
import { useToast } from '@/components/ui/toast';

const DEFAULT_CURRENCY = 'TJS';

function intentStatusLabel(intent: PlayerTopUpIntentDto): string {
  if (intent.state === 'fulfilled') return 'Зачислено';
  if (intent.isExpired) return 'Истекло';
  return 'Ожидает';
}

export function WalletPanel({ api, phoneVerified }: { api: PlayerApiClient; phoneVerified: boolean }) {
  const { toast } = useToast();
  const [intents, setIntents] = useState<PlayerTopUpIntentDto[]>([]);
  const [amount, setAmount] = useState('');
  const [pending, setPending] = useState(false);

  const refreshIntents = useCallback(() => {
    api.getTopUpIntents().then(setIntents).catch(() => { /* list is best-effort */ });
  }, [api]);

  useEffect(() => { refreshIntents(); }, [refreshIntents]);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    const major = Number(amount.replace(',', '.'));
    if (!Number.isFinite(major) || major <= 0) {
      toast({ title: 'Введите сумму больше нуля', variant: 'error' });
      return;
    }
    setPending(true);
    try {
      await api.createTopUpIntent({ amountMinorUnits: majorToMinor(major), currencyCode: DEFAULT_CURRENCY });
      setAmount('');
      toast({ title: 'Заявка на пополнение отправлена', variant: 'success' });
      refreshIntents();
    } catch {
      toast({ title: 'Не удалось отправить заявку', variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  return (
    <section className="rounded-2xl bg-[var(--color-surface)] p-4">
      <h2 className="text-xs uppercase tracking-wide text-[var(--text-3)]">Пополнить кошелёк</h2>

      {phoneVerified ? (
        <form className="mt-3 flex gap-2" onSubmit={handleSubmit}>
          <label htmlFor="topup-amount" className="sr-only">Сумма</label>
          <input
            id="topup-amount"
            type="text"
            inputMode="decimal"
            value={amount}
            onChange={(event) => setAmount(event.target.value)}
            placeholder="0,00"
            className="h-11 flex-1 rounded-xl border border-[var(--color-border)] bg-[var(--color-bg)] px-3 text-base outline-none focus-visible:outline-2 focus-visible:outline-[var(--accent)]"
          />
          <button
            type="submit"
            disabled={pending}
            className="h-11 rounded-xl bg-[var(--accent)] px-4 text-sm font-bold text-[var(--accent-fg)] disabled:opacity-50 focus-visible:outline-2 focus-visible:outline-[var(--accent)]"
          >
            {pending ? 'Отправка…' : 'Запросить'}
          </button>
        </form>
      ) : (
        <p className="mt-3 rounded-xl border border-dashed border-[var(--color-border)] p-3 text-sm text-[var(--text-2)]">
          Чтобы пополнять кошелёк онлайн, подтвердите номер телефона у администратора клуба. Подтверждение по SMS появится позже.
        </p>
      )}

      {intents.length > 0 && (
        <ul className="mt-4 space-y-2">
          {intents.map((intent) => (
            <li key={intent.paymentIntentId} className="flex items-center justify-between text-sm">
              <span>{formatMoney(intent.amountMinorUnits, intent.currencyCode)}</span>
              <span className={intent.state === 'fulfilled' ? 'text-[var(--accent)]' : 'text-[var(--text-2)]'}>
                {intentStatusLabel(intent)}
              </span>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `bun test src/screens/wallet/WalletPanel.test.tsx`
Expected: PASS (2/2).

- [ ] **Step 5: Embed `WalletPanel` in the dashboard**

In `src/AFK4.Customer.Web/src/screens/dashboard/DashboardScreen.tsx`, add the import and a `phoneVerified` prop, then render the panel after the balance section.

Add import:

```typescript
import { WalletPanel } from '@/screens/wallet/WalletPanel';
```

Change the component signature:

```typescript
export function DashboardScreen({ api, displayName, phoneVerified }: { api: PlayerApiClient; displayName: string; phoneVerified: boolean }) {
```

Inside the `load.state === 'ready'` block, after the balance `</section>` and before the active-session conditional, add:

```typescript
          <WalletPanel api={api} phoneVerified={phoneVerified} />
```

In `src/AFK4.Customer.Web/src/App.tsx`, pass the prop where `DashboardScreen` is rendered:

```typescript
        {route.kind === 'dashboard' && <DashboardScreen api={api} displayName={session.displayName} phoneVerified={session.phoneVerified} />}
```

- [ ] **Step 6: Run the full suite + type-check; commit**

Run: `bun test && bunx tsc -b`
(The Plan-1 `DashboardScreen.test.tsx` needs three adjustments so it still passes: (a) pass `phoneVerified={false}` to satisfy the new required prop; (b) wrap each render in `<ToastProvider>` since the embedded `WalletPanel` calls `useToast`; (c) extend the `apiWith` helper's returned object with `getTopUpIntents: mock().mockResolvedValue([])`, because `WalletPanel` calls it on mount and an absent method would throw a `TypeError` during render. Updated helper:

```typescript
function apiWith(dashboard: unknown) {
  return {
    getDashboard: mock().mockResolvedValue(dashboard),
    getTopUpIntents: mock().mockResolvedValue([])
  } as unknown as import('@/api/playerApi').PlayerApiClient;
}
// and each render becomes:
render(<ToastProvider><DashboardScreen api={api} displayName="Фёдор" phoneVerified={false} /></ToastProvider>);
```
)

```bash
git add src/AFK4.Customer.Web/src/screens/wallet src/AFK4.Customer.Web/src/screens/dashboard/DashboardScreen.tsx src/AFK4.Customer.Web/src/screens/dashboard/DashboardScreen.test.tsx src/AFK4.Customer.Web/src/App.tsx
git commit -m "feat(customer-web): wallet top-up panel on dashboard (D8 gate)"
```

---

## Task 10: Reservations screen (list / create / cancel, D8 gate)

**Files:**
- Create: `src/AFK4.Customer.Web/src/screens/reservations/ReservationsScreen.tsx`
- Test: `src/AFK4.Customer.Web/src/screens/reservations/ReservationsScreen.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `src/AFK4.Customer.Web/src/screens/reservations/ReservationsScreen.test.tsx`:

```typescript
import { it, expect, mock } from 'bun:test';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { ToastProvider } from '@/components/ui/toast';
import { ReservationsScreen } from './ReservationsScreen';
import type { PlayerApiClient } from '@/api/playerApi';

function renderScreen(api: PlayerApiClient, phoneVerified: boolean) {
  return render(
    <ToastProvider autoDismissMs={1000}>
      <ReservationsScreen api={api} phoneVerified={phoneVerified} />
    </ToastProvider>
  );
}

it('lists reservations with a localized state', async () => {
  const api = { getReservations: mock().mockResolvedValue([
    { reservationId: 'r1', seatId: 's1', seatName: 'PC-7', startsAtUtc: '2999-01-01T10:00:00Z', endsAtUtc: '2999-01-01T12:00:00Z', state: 'pending', note: null }
  ]) } as unknown as PlayerApiClient;
  renderScreen(api, true);
  expect(await screen.findByText('PC-7')).toBeInTheDocument();
  expect(screen.getByText('Ожидает подтверждения')).toBeInTheDocument();
});

it('hides the create form behind the D8 gate when the phone is unverified', async () => {
  const api = { getReservations: mock().mockResolvedValue([]) } as unknown as PlayerApiClient;
  renderScreen(api, false);
  expect(await screen.findByText(/подтвердите номер/i)).toBeInTheDocument();
  expect(screen.queryByLabelText('Начало')).not.toBeInTheDocument();
});

it('cancels a reservation after confirmation', async () => {
  const api = {
    getReservations: mock()
      .mockResolvedValueOnce([{ reservationId: 'r1', seatId: null, seatName: null, startsAtUtc: '2999-01-01T10:00:00Z', endsAtUtc: '2999-01-01T12:00:00Z', state: 'pending', note: null }])
      .mockResolvedValueOnce([]),
    cancelReservation: mock().mockResolvedValue({ reservationId: 'r1', state: 'cancelled' })
  } as unknown as PlayerApiClient;
  // Auto-confirm the window.confirm prompt.
  (globalThis as { confirm: () => boolean }).confirm = () => true;
  renderScreen(api, true);
  fireEvent.click(await screen.findByRole('button', { name: /отменить/i }));
  await waitFor(() => expect(api.cancelReservation).toHaveBeenCalledWith('r1'));
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `bun test src/screens/reservations/ReservationsScreen.test.tsx`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement `ReservationsScreen`**

Create `src/AFK4.Customer.Web/src/screens/reservations/ReservationsScreen.tsx`:

```typescript
import { useCallback, useEffect, useState, type FormEvent } from 'react';
import type { PlayerApiClient } from '@/api/playerApi';
import type { PlayerReservationDto } from '@/api/types';
import { formatDateTime } from '@/lib/datetime';
import { useToast } from '@/components/ui/toast';

const STATE_LABELS: Record<string, string> = {
  pending: 'Ожидает подтверждения',
  confirmed: 'Подтверждена',
  seated: 'Вы за местом',
  cancelled: 'Отменена'
};

export function ReservationsScreen({ api, phoneVerified }: { api: PlayerApiClient; phoneVerified: boolean }) {
  const { toast } = useToast();
  const [reservations, setReservations] = useState<PlayerReservationDto[] | null>(null);
  const [startsAt, setStartsAt] = useState('');
  const [endsAt, setEndsAt] = useState('');
  const [pending, setPending] = useState(false);

  const refresh = useCallback(() => {
    api.getReservations().then(setReservations).catch(() => setReservations([]));
  }, [api]);

  useEffect(() => { refresh(); }, [refresh]);

  async function handleCreate(event: FormEvent) {
    event.preventDefault();
    if (!startsAt || !endsAt) {
      toast({ title: 'Укажите начало и конец', variant: 'error' });
      return;
    }
    setPending(true);
    try {
      // datetime-local has no timezone; treat as local and serialize to ISO/UTC.
      await api.createReservation({
        startsAtUtc: new Date(startsAt).toISOString(),
        endsAtUtc: new Date(endsAt).toISOString()
      });
      setStartsAt('');
      setEndsAt('');
      toast({ title: 'Бронь создана', variant: 'success' });
      refresh();
    } catch (error: unknown) {
      const status = (error as { status?: number }).status;
      toast({ title: status === 409 ? 'Это время уже занято' : 'Не удалось создать бронь', variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  async function handleCancel(reservationId: string) {
    if (!globalThis.confirm('Отменить бронь?')) return;
    try {
      await api.cancelReservation(reservationId);
      toast({ title: 'Бронь отменена', variant: 'success' });
      refresh();
    } catch {
      toast({ title: 'Не удалось отменить', variant: 'error' });
    }
  }

  return (
    <main className="space-y-5 px-6 py-6">
      <h1 className="text-2xl font-extrabold tracking-tight">Брони</h1>

      {phoneVerified ? (
        <form className="space-y-3 rounded-2xl bg-[var(--color-surface)] p-4" onSubmit={handleCreate}>
          <div className="space-y-1.5">
            <label htmlFor="res-start" className="text-sm text-[var(--text-2)]">Начало</label>
            <input id="res-start" type="datetime-local" value={startsAt} onChange={(e) => setStartsAt(e.target.value)}
              className="h-11 w-full rounded-xl border border-[var(--color-border)] bg-[var(--color-bg)] px-3 text-base outline-none focus-visible:outline-2 focus-visible:outline-[var(--accent)]" />
          </div>
          <div className="space-y-1.5">
            <label htmlFor="res-end" className="text-sm text-[var(--text-2)]">Конец</label>
            <input id="res-end" type="datetime-local" value={endsAt} onChange={(e) => setEndsAt(e.target.value)}
              className="h-11 w-full rounded-xl border border-[var(--color-border)] bg-[var(--color-bg)] px-3 text-base outline-none focus-visible:outline-2 focus-visible:outline-[var(--accent)]" />
          </div>
          <button type="submit" disabled={pending}
            className="h-11 w-full rounded-xl bg-[var(--accent)] text-sm font-bold text-[var(--accent-fg)] disabled:opacity-50 focus-visible:outline-2 focus-visible:outline-[var(--accent)]">
            {pending ? 'Создаём…' : 'Забронировать'}
          </button>
        </form>
      ) : (
        <p className="rounded-2xl border border-dashed border-[var(--color-border)] p-4 text-sm text-[var(--text-2)]">
          Чтобы бронировать онлайн, подтвердите номер телефона у администратора клуба. Подтверждение по SMS появится позже.
        </p>
      )}

      {reservations === null && <div role="status" aria-label="Загрузка броней" className="h-20 animate-pulse rounded-2xl bg-[var(--color-surface)]" />}
      {reservations !== null && reservations.length === 0 && (
        <p className="py-8 text-center text-[var(--text-2)]">Броней пока нет</p>
      )}
      {reservations !== null && reservations.length > 0 && (
        <ul className="space-y-3">
          {reservations.map((reservation) => (
            <li key={reservation.reservationId} className="rounded-2xl bg-[var(--color-surface)] p-4">
              <div className="flex items-center justify-between">
                <span className="font-bold">{reservation.seatName ?? 'Без места'}</span>
                <span className="text-sm text-[var(--text-2)]">{STATE_LABELS[reservation.state] ?? reservation.state}</span>
              </div>
              <p className="mt-1 text-sm text-[var(--text-2)]">
                {formatDateTime(reservation.startsAtUtc)} — {formatDateTime(reservation.endsAtUtc)}
              </p>
              {(reservation.state === 'pending' || reservation.state === 'confirmed') && (
                <button type="button" onClick={() => handleCancel(reservation.reservationId)}
                  className="mt-2 min-h-[44px] text-sm text-red-400 focus-visible:outline-2 focus-visible:outline-[var(--accent)]">
                  Отменить
                </button>
              )}
            </li>
          ))}
        </ul>
      )}
    </main>
  );
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `bun test src/screens/reservations/ReservationsScreen.test.tsx`
Expected: PASS (3/3).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Customer.Web/src/screens/reservations
git commit -m "feat(customer-web): reservations screen (list/create/cancel, D8 gate)"
```

---

## Task 11: Profile screen (locale + marketing + sign out)

**Files:**
- Create: `src/AFK4.Customer.Web/src/screens/profile/ProfileScreen.tsx`
- Test: `src/AFK4.Customer.Web/src/screens/profile/ProfileScreen.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `src/AFK4.Customer.Web/src/screens/profile/ProfileScreen.test.tsx`:

```typescript
import { it, expect, mock } from 'bun:test';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { ToastProvider } from '@/components/ui/toast';
import { ProfileScreen } from './ProfileScreen';
import type { PlayerApiClient } from '@/api/playerApi';

function renderScreen(api: PlayerApiClient, onSignOut = () => {}) {
  return render(
    <ToastProvider autoDismissMs={1000}>
      <ProfileScreen api={api} onSignOut={onSignOut} onLocaleChange={() => {}} />
    </ToastProvider>
  );
}

const profile = {
  playerAccountId: 'p1', displayName: 'Фёдор', phoneNumber: '+992900000001',
  phoneVerified: false, preferredLocale: 'ru', marketingOptIn: false
};

it('renders the profile and a disabled OTP note for the phone', async () => {
  const api = { getProfile: mock().mockResolvedValue(profile) } as unknown as PlayerApiClient;
  renderScreen(api);
  expect(await screen.findByText('Фёдор')).toBeInTheDocument();
  expect(screen.getByText(/через OTP/i)).toBeInTheDocument();
});

it('PATCHes the marketing opt-in when toggled', async () => {
  const api = {
    getProfile: mock().mockResolvedValue(profile),
    updateProfile: mock().mockResolvedValue({ ...profile, marketingOptIn: true })
  } as unknown as PlayerApiClient;
  renderScreen(api);
  fireEvent.click(await screen.findByLabelText(/рассылк/i));
  await waitFor(() => expect(api.updateProfile).toHaveBeenCalledWith({ marketingOptIn: true }));
});

it('calls onSignOut when the sign-out button is pressed', async () => {
  const api = { getProfile: mock().mockResolvedValue(profile) } as unknown as PlayerApiClient;
  const onSignOut = mock();
  renderScreen(api, onSignOut);
  fireEvent.click(await screen.findByRole('button', { name: /выйти/i }));
  expect(onSignOut).toHaveBeenCalledTimes(1);
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `bun test src/screens/profile/ProfileScreen.test.tsx`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement `ProfileScreen`**

Create `src/AFK4.Customer.Web/src/screens/profile/ProfileScreen.tsx`:

```typescript
import { useEffect, useState } from 'react';
import type { PlayerApiClient } from '@/api/playerApi';
import type { PlayerProfileDto } from '@/api/types';
import { useToast } from '@/components/ui/toast';

interface ProfileScreenProps {
  api: PlayerApiClient;
  onSignOut: () => void;
  onLocaleChange: (locale: 'ru' | 'en') => void;
}

export function ProfileScreen({ api, onSignOut, onLocaleChange }: ProfileScreenProps) {
  const { toast } = useToast();
  const [profile, setProfile] = useState<PlayerProfileDto | null>(null);

  useEffect(() => {
    let cancelled = false;
    api.getProfile().then((p) => { if (!cancelled) setProfile(p); }).catch(() => { /* show skeleton */ });
    return () => { cancelled = true; };
  }, [api]);

  async function patch(change: { preferredLocale?: string; marketingOptIn?: boolean }) {
    try {
      const updated = await api.updateProfile(change);
      setProfile(updated);
      if (change.preferredLocale === 'ru' || change.preferredLocale === 'en') onLocaleChange(change.preferredLocale);
      toast({ title: 'Сохранено', variant: 'success' });
    } catch {
      toast({ title: 'Не удалось сохранить', variant: 'error' });
    }
  }

  if (profile === null) {
    return <div role="status" aria-label="Загрузка профиля" className="m-6 h-40 animate-pulse rounded-2xl bg-[var(--color-surface)]" />;
  }

  return (
    <main className="space-y-5 px-6 py-6">
      <header>
        <h1 className="text-2xl font-extrabold tracking-tight">{profile.displayName}</h1>
        <p className="mt-1 text-sm text-[var(--text-2)]">
          {profile.phoneNumber ?? '—'} · <span className="text-[var(--text-3)]">изменение через OTP, скоро</span>
        </p>
      </header>

      <section className="space-y-3 rounded-2xl bg-[var(--color-surface)] p-4">
        <p className="text-xs uppercase tracking-wide text-[var(--text-3)]">Язык</p>
        <div className="flex gap-2">
          {(['ru', 'en'] as const).map((locale) => (
            <button
              key={locale}
              type="button"
              onClick={() => patch({ preferredLocale: locale })}
              aria-pressed={profile.preferredLocale === locale}
              className={
                'min-h-[44px] flex-1 rounded-xl text-sm font-medium focus-visible:outline-2 focus-visible:outline-[var(--accent)] ' +
                (profile.preferredLocale === locale ? 'bg-[var(--accent)] text-[var(--accent-fg)]' : 'border border-[var(--color-border)] text-[var(--text-2)]')
              }
            >
              {locale === 'ru' ? 'Русский' : 'English'}
            </button>
          ))}
        </div>
      </section>

      <label className="flex items-center justify-between rounded-2xl bg-[var(--color-surface)] p-4 text-sm">
        <span>Получать рассылку об акциях</span>
        <input
          type="checkbox"
          checked={profile.marketingOptIn}
          onChange={(event) => patch({ marketingOptIn: event.target.checked })}
          className="h-5 w-5 accent-[var(--accent)]"
        />
      </label>

      <button
        type="button"
        onClick={onSignOut}
        className="min-h-[44px] w-full rounded-xl border border-[var(--color-border)] text-sm text-red-400 focus-visible:outline-2 focus-visible:outline-[var(--accent)]"
      >
        Выйти
      </button>
    </main>
  );
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `bun test src/screens/profile/ProfileScreen.test.tsx`
Expected: PASS (3/3).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Customer.Web/src/screens/profile
git commit -m "feat(customer-web): profile screen (locale, marketing, sign out)"
```

---

## Task 12: Mount all screens in `App` + sign-out flow

Now wire the real screens into the router, replacing the placeholder branch, and implement sign-out (clear session; cache-clear is added in the PWA task).

**Files:**
- Modify: `src/AFK4.Customer.Web/src/App.tsx`
- Test: `src/AFK4.Customer.Web/src/App.test.tsx` (extend the Plan-1 smoke test)

- [ ] **Step 1: Write a failing routing test**

Add to `src/AFK4.Customer.Web/src/App.test.tsx` a test that, with a stored session, navigating to `/reservations` shows the reservations heading. (Use the existing test's localStorage seeding pattern; mock `fetch` globally to resolve empty lists.) Minimal addition:

```typescript
import { it, expect, mock } from 'bun:test';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { App } from './App';

it('navigates to the reservations tab and renders its screen', async () => {
  localStorage.setItem('afk4.player.session', JSON.stringify({
    playerAccountId: 'p1', organizationId: 'org1', displayName: 'Ф', phoneVerified: false,
    accessToken: 'tok', accessTokenExpiresAtUtc: '2999-01-01T00:00:00Z',
    refreshToken: 'ref', refreshTokenExpiresAtUtc: '2999-02-01T00:00:00Z'
  }));
  // One combined body satisfies every call this render makes: dashboard reads walletBalance/
  // debtBalance/activeSession; list endpoints read items/nextCursor; array endpoints simply
  // see extra fields. This avoids a render crash from an unshaped response.
  const body = JSON.stringify({
    walletBalance: { currencyCode: 'TJS', minorUnits: 0 },
    debtBalance: { currencyCode: 'TJS', minorUnits: 0 },
    activeSession: null,
    items: [], nextCursor: null
  });
  globalThis.fetch = mock().mockResolvedValue({ ok: true, status: 200, text: async () => body }) as unknown as typeof fetch;
  render(<App />);
  fireEvent.click(await screen.findByRole('button', { name: 'Брони' }));
  await waitFor(() => expect(screen.getByRole('heading', { name: 'Брони' })).toBeInTheDocument());
  localStorage.clear();
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `bun test src/App.test.tsx`
Expected: FAIL — placeholder text rendered instead of the Reservations heading.

- [ ] **Step 3: Wire the screens + sign-out in `App.tsx`**

Add imports:

```typescript
import { VisitsScreen } from './screens/history/VisitsScreen';
import { ReceiptScreen } from './screens/history/ReceiptScreen';
import { PurchasesScreen } from './screens/purchases/PurchasesScreen';
import { HistoryTabs } from './screens/history/HistoryTabs';
import { ReservationsScreen } from './screens/reservations/ReservationsScreen';
import { ProfileScreen } from './screens/profile/ProfileScreen';
```

Update `tabForRoute` (added in Plan 1) so the new `purchases` route highlights the История tab, otherwise `return route.kind` would yield a non-`PlayerTab` value and fail type-checking:

```typescript
function tabForRoute(route: PlayerRoute): PlayerTab {
  if (route.kind === 'receipt' || route.kind === 'purchases') return 'history';
  return route.kind;
}
```

Add a navigate-to-route helper alongside `navigate` (the existing `navigate` takes a `PlayerTab`; receipts need a full route). Add:

```typescript
  const navigateTo = useCallback((next: PlayerRoute) => {
    setRoute(next);
    if (typeof window !== 'undefined') window.history.pushState(null, '', routePath(next));
  }, []);

  const signOut = useCallback(() => {
    onSessionChanged(null);
    if (typeof window !== 'undefined') window.history.pushState(null, '', '/');
    setRoute({ kind: 'dashboard' });
  }, [onSessionChanged]);
```

Replace the authenticated `<AppShell>` body with the real routing:

```typescript
  return (
    <ToastProvider>
      <AppShell active={tabForRoute(route)} onNavigate={navigate}>
        {route.kind === 'dashboard' && <DashboardScreen api={api} displayName={session.displayName} phoneVerified={session.phoneVerified} />}
        {route.kind === 'history' && (
          <>
            <HistoryTabs active="visits" onChange={(view) => navigateTo({ kind: view === 'purchases' ? 'purchases' : 'history' })} />
            <VisitsScreen api={api} onOpenReceipt={(sessionId) => navigateTo({ kind: 'receipt', sessionId })} />
          </>
        )}
        {route.kind === 'purchases' && (
          <>
            <HistoryTabs active="purchases" onChange={(view) => navigateTo({ kind: view === 'purchases' ? 'purchases' : 'history' })} />
            <PurchasesScreen api={api} />
          </>
        )}
        {route.kind === 'receipt' && <ReceiptScreen api={api} sessionId={route.sessionId} onBack={() => navigateTo({ kind: 'history' })} />}
        {route.kind === 'reservations' && <ReservationsScreen api={api} phoneVerified={session.phoneVerified} />}
        {route.kind === 'profile' && <ProfileScreen api={api} onSignOut={signOut} onLocaleChange={() => {}} />}
      </AppShell>
    </ToastProvider>
  );
```

(`onLocaleChange` is a no-op until Task 14 wires i18n; leave the prop in place.)

- [ ] **Step 4: Run the full suite + type-check**

Run: `bun test && bunx tsc -b`
Expected: PASS, no type errors.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Customer.Web/src/App.tsx src/AFK4.Customer.Web/src/App.test.tsx
git commit -m "feat(customer-web): mount all screens + sign-out routing"
```

---

## Task 13: PWA — manifest, service worker, offline-read cache, offline banner, sign-out cache clear

**Files:**
- Modify: `src/AFK4.Customer.Web/package.json` (add `vite-plugin-pwa`)
- Modify: `src/AFK4.Customer.Web/vite.config.ts`
- Modify: `src/AFK4.Customer.Web/index.html`
- Modify: `src/AFK4.Customer.Web/src/vite-env.d.ts`
- Modify: `src/AFK4.Customer.Web/src/main.tsx`
- Create: `src/AFK4.Customer.Web/src/pwa/registerSW.ts`
- Create: `src/AFK4.Customer.Web/src/pwa/offlineCache.ts`
- Test: `src/AFK4.Customer.Web/src/pwa/offlineCache.test.ts`
- Create: `src/AFK4.Customer.Web/src/components/OfflineBanner.tsx`
- Test: `src/AFK4.Customer.Web/src/components/OfflineBanner.test.tsx`
- Create: `src/AFK4.Customer.Web/public/` icons (see Step 7)

- [ ] **Step 1: Add the dependency**

Run: `bun add -d vite-plugin-pwa --cwd /home/fedya/projects/afk4.net/src/AFK4.Customer.Web`
Expected: `vite-plugin-pwa` appears in `devDependencies`.

- [ ] **Step 2: Write the failing test for `offlineCache`**

Create `src/AFK4.Customer.Web/src/pwa/offlineCache.test.ts`:

```typescript
import { it, expect, mock } from 'bun:test';
import { isPlayerCacheName, clearPlayerCaches } from './offlineCache';

it('recognizes player data cache names', () => {
  expect(isPlayerCacheName('afk4-player-api')).toBe(true);
  expect(isPlayerCacheName('workbox-precache-v2')).toBe(false);
});

it('deletes only player caches on sign-out', async () => {
  const deleted: string[] = [];
  const fakeCaches = {
    keys: mock().mockResolvedValue(['afk4-player-api', 'workbox-precache-v2', 'afk4-player-shell']),
    delete: mock().mockImplementation((name: string) => { deleted.push(name); return Promise.resolve(true); })
  } as unknown as CacheStorage;
  await clearPlayerCaches(fakeCaches);
  expect(deleted).toEqual(['afk4-player-api', 'afk4-player-shell']);
});

it('is a no-op when the Cache API is unavailable', async () => {
  await clearPlayerCaches(undefined);
  expect(true).toBe(true);
});
```

- [ ] **Step 3: Run to verify it fails**

Run: `bun test src/pwa/offlineCache.test.ts`
Expected: FAIL — module not found.

- [ ] **Step 4: Implement `offlineCache.ts`**

Create `src/AFK4.Customer.Web/src/pwa/offlineCache.ts`:

```typescript
// Authenticated /api/me/* responses are cached by the service worker (NetworkFirst).
// On sign-out we must purge them so the next account on a shared device can't read them.
// The runtime cache names are prefixed "afk4-player-" (see vite.config.ts workbox config).
const PLAYER_CACHE_PREFIX = 'afk4-player-';

export function isPlayerCacheName(name: string): boolean {
  return name.startsWith(PLAYER_CACHE_PREFIX);
}

export async function clearPlayerCaches(cacheStorage: CacheStorage | undefined = globalThis.caches): Promise<void> {
  if (!cacheStorage) return;
  const names = await cacheStorage.keys();
  await Promise.all(names.filter(isPlayerCacheName).map((name) => cacheStorage.delete(name)));
}
```

- [ ] **Step 5: Run to verify it passes**

Run: `bun test src/pwa/offlineCache.test.ts`
Expected: PASS (3/3).

- [ ] **Step 6: Write + implement `OfflineBanner`**

Create `src/AFK4.Customer.Web/src/components/OfflineBanner.test.tsx`:

```typescript
import { it, expect } from 'bun:test';
import { render, screen } from '@testing-library/react';
import { OfflineBanner } from './OfflineBanner';

it('renders nothing when online', () => {
  const { container } = render(<OfflineBanner online={true} />);
  expect(container).toBeEmptyDOMElement();
});

it('shows an offline message when offline', () => {
  render(<OfflineBanner online={false} />);
  expect(screen.getByRole('status')).toHaveTextContent(/офлайн/i);
});
```

Create `src/AFK4.Customer.Web/src/components/OfflineBanner.tsx`:

```typescript
import { useEffect, useState } from 'react';

// `online` is injectable for tests; in the app it tracks navigator.onLine + online/offline events.
export function OfflineBanner({ online }: { online?: boolean }) {
  const [isOnline, setIsOnline] = useState(online ?? (typeof navigator === 'undefined' ? true : navigator.onLine));

  useEffect(() => {
    if (online !== undefined) { setIsOnline(online); return; }
    const update = () => setIsOnline(navigator.onLine);
    window.addEventListener('online', update);
    window.addEventListener('offline', update);
    return () => {
      window.removeEventListener('online', update);
      window.removeEventListener('offline', update);
    };
  }, [online]);

  if (isOnline) return null;
  return (
    <div role="status" className="bg-[var(--color-surface-2)] px-4 py-2 text-center text-xs text-[var(--text-2)]">
      Офлайн — показаны сохранённые данные
    </div>
  );
}
```

Run: `bun test src/components/OfflineBanner.test.tsx`
Expected: PASS (2/2).

Mount it in `App.tsx` at the top of the authenticated `AppShell` children (above the route switch), and wire sign-out to clear caches. Add import in `App.tsx`:

```typescript
import { OfflineBanner } from './components/OfflineBanner';
import { clearPlayerCaches } from './pwa/offlineCache';
```

Update `signOut` to also purge caches (fire-and-forget):

```typescript
  const signOut = useCallback(() => {
    void clearPlayerCaches();
    onSessionChanged(null);
    if (typeof window !== 'undefined') window.history.pushState(null, '', '/');
    setRoute({ kind: 'dashboard' });
  }, [onSessionChanged]);
```

Render `<OfflineBanner />` just inside `<AppShell ...>` before the route conditionals.

- [ ] **Step 7: Generate placeholder icons**

Create `src/AFK4.Customer.Web/public/` with default-brand PWA icons. Generate simple solid-accent PNGs (lime `#c8ff00` square with "A4"); any 192×192, 512×512, and a 512×512 maskable PNG plus `favicon.svg` are acceptable. If image tooling is unavailable, create an SVG and reference it; the key requirement is that the manifest's icon paths resolve. Suggested files: `public/pwa-192.png`, `public/pwa-512.png`, `public/pwa-maskable-512.png`, `public/favicon.svg`.

- [ ] **Step 8: Configure `VitePWA` in `vite.config.ts`**

Replace `src/AFK4.Customer.Web/vite.config.ts` with:

```typescript
import path from 'node:path';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import { defineConfig } from 'vite';
import { VitePWA } from 'vite-plugin-pwa';

export default defineConfig({
  base: '/',
  plugins: [
    react(),
    tailwindcss(),
    VitePWA({
      registerType: 'autoUpdate',
      includeAssets: ['favicon.svg', 'pwa-192.png', 'pwa-512.png'],
      manifest: {
        name: 'AFK4 — портал игрока',
        short_name: 'AFK4',
        description: 'Баланс, сессии, история и брони вашего клуба',
        theme_color: '#101314',
        background_color: '#101314',
        display: 'standalone',
        start_url: '/',
        icons: [
          { src: '/pwa-192.png', sizes: '192x192', type: 'image/png' },
          { src: '/pwa-512.png', sizes: '512x512', type: 'image/png' },
          { src: '/pwa-maskable-512.png', sizes: '512x512', type: 'image/png', purpose: 'maskable' }
        ]
      },
      workbox: {
        navigateFallback: '/index.html',
        runtimeCaching: [
          {
            // Offline-read for the player's GET data; writes are never cached.
            urlPattern: ({ url, request }) =>
              request.method === 'GET' &&
              (url.pathname === '/api/me/dashboard' ||
                url.pathname === '/api/me/visits' ||
                url.pathname === '/api/me/purchases'),
            handler: 'NetworkFirst',
            options: {
              cacheName: 'afk4-player-api',
              networkTimeoutSeconds: 4,
              expiration: { maxEntries: 32, maxAgeSeconds: 60 * 60 * 24 }
            }
          }
        ]
      }
    })
  ],
  resolve: {
    alias: { '@': path.resolve(import.meta.dirname, './src') }
  }
});
```

- [ ] **Step 9: Type reference + SW registration**

In `src/AFK4.Customer.Web/src/vite-env.d.ts`, add at the top:

```typescript
/// <reference types="vite-plugin-pwa/client" />
```

Create `src/AFK4.Customer.Web/src/pwa/registerSW.ts`:

```typescript
import { registerSW } from 'virtual:pwa-register';

// autoUpdate keeps the installed app fresh; no prompt UI in v1.
export function registerServiceWorker(): void {
  if (typeof window === 'undefined') return;
  registerSW({ immediate: true });
}
```

In `src/AFK4.Customer.Web/src/main.tsx`, call it after mounting (guarded so tests/SSR don't touch the virtual module — it only exists under Vite). Add at the end of the file:

```typescript
if (import.meta.env.PROD) {
  void import('./pwa/registerSW').then((m) => m.registerServiceWorker());
}
```

- [ ] **Step 10: Update `index.html` meta**

In `src/AFK4.Customer.Web/index.html`, add inside `<head>`:

```html
    <meta name="theme-color" content="#101314" />
    <link rel="icon" href="/favicon.svg" type="image/svg+xml" />
    <link rel="apple-touch-icon" href="/pwa-192.png" />
```

- [ ] **Step 11: Verify build + tests**

Run: `bun test && bunx tsc -b && bun run build`
Expected: tests PASS; `tsc` clean; `vite build` succeeds and emits `dist/manifest.webmanifest` + `dist/sw.js`.

- [ ] **Step 12: Commit**

```bash
git add src/AFK4.Customer.Web/package.json src/AFK4.Customer.Web/vite.config.ts src/AFK4.Customer.Web/index.html src/AFK4.Customer.Web/src/vite-env.d.ts src/AFK4.Customer.Web/src/main.tsx src/AFK4.Customer.Web/src/pwa src/AFK4.Customer.Web/src/components/OfflineBanner.tsx src/AFK4.Customer.Web/src/components/OfflineBanner.test.tsx src/AFK4.Customer.Web/src/App.tsx src/AFK4.Customer.Web/public
git commit -m "feat(customer-web): installable PWA + offline-read cache + offline banner + sign-out cache purge"
```

---

## Task 14: i18n wiring (ru/en) across all screens

Introduce the `@afk4/i18n` provider, add a `customer.*` key namespace to the shared catalog (ru + en), switch every hardcoded string to `t()`, and drive the active locale from Profile (persisted in `localStorage`).

**Files:**
- Modify: `packages/i18n/src/messages.ts` (add `customer.*` keys to both `ru` and `en`)
- Modify: `src/AFK4.Customer.Web/src/main.tsx` (wrap in `I18nProvider`)
- Modify: `src/AFK4.Customer.Web/src/App.tsx` (locale state + `onLocaleChange` + persistence)
- Modify: every screen/component with Russian literals (SignIn, BottomNav, Dashboard, LiveSessionCard, WalletPanel, Visits, Receipt, Purchases, HistoryTabs, Reservations, Profile, OfflineBanner)
- Test: `src/AFK4.Customer.Web/src/i18n.test.tsx` (a representative render-in-en test)

- [ ] **Step 1: Add `customer.*` keys to `packages/i18n/src/messages.ts`**

Add the following keys to the `ru` object and their English counterparts to the `en` object (the `MessageKey` type derives from the `ru` keys, so both must stay in sync — TS will error if `en` is missing any). Insert near related sections:

```typescript
// inside ru: { ... }
'customer.nav.dashboard': 'Главная',
'customer.nav.history': 'История',
'customer.nav.reservations': 'Брони',
'customer.nav.profile': 'Профиль',
'customer.signin.title': 'Вход в портал',
'customer.signin.phone': 'Телефон',
'customer.signin.password': 'PIN или пароль',
'customer.signin.submit': 'Войти',
'customer.signin.submitting': 'Входим…',
'customer.signin.otpSoon': 'Войти по SMS-коду · скоро',
'customer.signin.error': 'Неверный номер или пароль',
'customer.dashboard.welcome': 'С возвращением',
'customer.dashboard.balance': 'Баланс кошелька',
'customer.dashboard.debt': 'Долг',
'customer.dashboard.noSession': 'Нет активной сессии',
'customer.dashboard.sessionActive': 'СЕССИЯ АКТИВНА',
'customer.dashboard.sessionRemaining': 'ОСТАЛОСЬ',
'customer.dashboard.accrued': 'накоплено',
'customer.dashboard.loadError': 'Не удалось загрузить данные. Проверьте соединение.',
'customer.wallet.title': 'Пополнить кошелёк',
'customer.wallet.amount': 'Сумма',
'customer.wallet.request': 'Запросить',
'customer.wallet.requesting': 'Отправка…',
'customer.wallet.sent': 'Заявка на пополнение отправлена',
'customer.wallet.sendError': 'Не удалось отправить заявку',
'customer.wallet.amountError': 'Введите сумму больше нуля',
'customer.wallet.gate': 'Чтобы пополнять кошелёк онлайн, подтвердите номер телефона у администратора клуба. Подтверждение по SMS появится позже.',
'customer.wallet.statePending': 'Ожидает',
'customer.wallet.stateFulfilled': 'Зачислено',
'customer.wallet.stateExpired': 'Истекло',
'customer.history.visits': 'Визиты',
'customer.history.purchases': 'Покупки',
'customer.history.noVisits': 'Пока нет визитов',
'customer.history.noPurchases': 'Пока нет покупок',
'customer.history.loadError': 'Не удалось загрузить историю.',
'customer.history.purchasesError': 'Не удалось загрузить покупки.',
'customer.common.retry': 'Повторить',
'customer.common.loadMore': 'Показать ещё',
'customer.common.loading': 'Загрузка…',
'customer.common.back': '← Назад',
'customer.receipt.notFound': 'Чек не найден',
'customer.receipt.loadError': 'Не удалось загрузить чек.',
'customer.receipt.time': 'Время',
'customer.receipt.total': 'Итого',
'customer.receipt.openLink': 'Чек →',
'customer.reservations.title': 'Брони',
'customer.reservations.start': 'Начало',
'customer.reservations.end': 'Конец',
'customer.reservations.create': 'Забронировать',
'customer.reservations.creating': 'Создаём…',
'customer.reservations.created': 'Бронь создана',
'customer.reservations.createError': 'Не удалось создать бронь',
'customer.reservations.conflict': 'Это время уже занято',
'customer.reservations.timeError': 'Укажите начало и конец',
'customer.reservations.none': 'Броней пока нет',
'customer.reservations.cancel': 'Отменить',
'customer.reservations.cancelConfirm': 'Отменить бронь?',
'customer.reservations.cancelled': 'Бронь отменена',
'customer.reservations.cancelError': 'Не удалось отменить',
'customer.reservations.gate': 'Чтобы бронировать онлайн, подтвердите номер телефона у администратора клуба. Подтверждение по SMS появится позже.',
'customer.reservations.noSeat': 'Без места',
'customer.reservations.statePending': 'Ожидает подтверждения',
'customer.reservations.stateConfirmed': 'Подтверждена',
'customer.reservations.stateSeated': 'Вы за местом',
'customer.reservations.stateCancelled': 'Отменена',
'customer.profile.phoneNote': 'изменение через OTP, скоро',
'customer.profile.language': 'Язык',
'customer.profile.langRu': 'Русский',
'customer.profile.langEn': 'English',
'customer.profile.marketing': 'Получать рассылку об акциях',
'customer.profile.saved': 'Сохранено',
'customer.profile.saveError': 'Не удалось сохранить',
'customer.profile.signOut': 'Выйти',
'customer.offline.banner': 'Офлайн — показаны сохранённые данные',
'customer.toast.region': 'Уведомления',
```

Add the matching `en` translations under the `en` object (same keys), e.g. `'customer.nav.dashboard': 'Home'`, `'customer.signin.error': 'Wrong number or password'`, etc. (Translate every key above; TS enforces parity.)

- [ ] **Step 2: Wrap the app in `I18nProvider`**

In `src/AFK4.Customer.Web/src/main.tsx`, import and wrap. The initial locale comes from `localStorage` (key `afk4.player.locale`) defaulting to `ru`:

```typescript
import { I18nProvider } from '@afk4/i18n';
```

```typescript
const initialLocale = (globalThis.localStorage?.getItem('afk4.player.locale') as 'ru' | 'en' | null) ?? 'ru';

createRoot(root).render(
  <StrictMode>
    <I18nProvider initialLocale={initialLocale}>
      <App />
    </I18nProvider>
  </StrictMode>
);
```

- [ ] **Step 3: Drive locale changes from `App.tsx`/Profile**

In `App.tsx`, consume `useI18n` and pass a real `onLocaleChange` to `ProfileScreen` that calls `setLocale` and persists:

```typescript
import { useI18n } from '@afk4/i18n';
```

Inside `App`:

```typescript
  const { setLocale } = useI18n();
  const handleLocaleChange = useCallback((locale: 'ru' | 'en') => {
    setLocale(locale);
    globalThis.localStorage?.setItem('afk4.player.locale', locale);
  }, [setLocale]);
```

Pass `onLocaleChange={handleLocaleChange}` to `<ProfileScreen />`.

- [ ] **Step 4: Replace literals with `t()` across components**

In each component, import `useI18n` and replace Russian literals with `t('customer.…')`. Example for `BottomNav.tsx`:

```typescript
import { useI18n } from '@afk4/i18n';
// ...
export function BottomNav({ active, onNavigate }: { active: PlayerTab; onNavigate: (tab: PlayerTab) => void }) {
  const { t } = useI18n();
  const TABS: { tab: PlayerTab; key: string; Icon: typeof Home }[] = [
    { tab: 'dashboard', key: 'customer.nav.dashboard', Icon: Home },
    { tab: 'history', key: 'customer.nav.history', Icon: Clock },
    { tab: 'reservations', key: 'customer.nav.reservations', Icon: CalendarDays },
    { tab: 'profile', key: 'customer.nav.profile', Icon: User }
  ];
  // ...{t(key as never)}... (cast to MessageKey at the call site)
```

Apply the same mechanical substitution to: `SignInScreen`, `DashboardScreen`, `LiveSessionCard`, `WalletPanel`, `VisitsScreen`, `ReceiptScreen`, `PurchasesScreen`, `HistoryTabs`, `ReservationsScreen`, `ProfileScreen`, `OfflineBanner`, and the toast region label. Reservation/intent state labels map via `t('customer.reservations.state' + Capitalized)` lookups — keep an explicit `Record<string, MessageKey>` map per component for type-safety rather than string concatenation.

Note: because `MessageKey` is a strict union, `t()` calls with literal keys type-check directly; for dynamic keys (state maps) build a typed `Record<string, MessageKey>`.

- [ ] **Step 5: Write a representative en-render test**

Create `src/AFK4.Customer.Web/src/i18n.test.tsx`:

```typescript
import { it, expect, mock } from 'bun:test';
import { render, screen } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { DashboardScreen } from './screens/dashboard/DashboardScreen';
import { ToastProvider } from './components/ui/toast';
import type { PlayerApiClient } from './api/playerApi';

it('renders the dashboard in English under the en locale', async () => {
  const api = {
    getDashboard: mock().mockResolvedValue({ walletBalance: { currencyCode: 'TJS', minorUnits: 0 }, debtBalance: { currencyCode: 'TJS', minorUnits: 0 }, activeSession: null }),
    getTopUpIntents: mock().mockResolvedValue([])
  } as unknown as PlayerApiClient;
  render(
    <I18nProvider initialLocale="en">
      <ToastProvider>
        <DashboardScreen api={api} displayName="Fedor" phoneVerified={false} />
      </ToastProvider>
    </I18nProvider>
  );
  expect(await screen.findByText('Wallet balance')).toBeInTheDocument();
});
```

(Set `'customer.dashboard.balance': 'Wallet balance'` in `en`.)

- [ ] **Step 6: Update existing tests for the provider**

Screen tests that render a component using `useI18n` must wrap it in `<I18nProvider initialLocale="ru">`. Update the render helpers in `DashboardScreen.test.tsx`, `WalletPanel.test.tsx`, `ReservationsScreen.test.tsx`, `ProfileScreen.test.tsx`, `VisitsScreen.test.tsx`, `ReceiptScreen.test.tsx`, `PurchasesScreen.test.tsx`, and `App.test.tsx` accordingly (assertions stay in Russian since the default test locale is `ru`).

- [ ] **Step 7: Run everything**

Run from repo root so the i18n package is included:
`/home/fedya/.bun/bin/bun test` (in `src/AFK4.Customer.Web`) — all green.
`bunx tsc -b` — clean (this proves en/ru key parity).
`bun run build` — succeeds.

- [ ] **Step 8: Commit**

```bash
git add packages/i18n/src/messages.ts src/AFK4.Customer.Web/src
git commit -m "feat(customer-web): i18n wiring (ru/en) across all screens"
```

---

## Final Review

After all tasks: dispatch a holistic code review over the whole `src/AFK4.Customer.Web` diff for this plan, then run the full gates once more:
- `cd src/AFK4.Customer.Web && bun test` (all suites green)
- `bunx tsc -b` (no type errors; proves contract + i18n parity)
- `bun run build` (emits `dist/` with manifest + service worker)

Then update the memory file `afk4-customer-track-state.md` to record Plan 2 as done (screens + PWA + i18n), the commit range, and any deferred items (OTP-gated affordances still disabled; offline writes intentionally blocked; default-brand PWA icons are placeholders pending real per-tenant assets).

**Do NOT merge or open a PR** — the customer track and its base `sp4-counter-loop` are intentionally unmerged. Stop after the branch is complete unless the user asks to finish/merge.
