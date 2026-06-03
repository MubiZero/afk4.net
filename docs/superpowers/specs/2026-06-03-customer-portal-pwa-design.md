# Customer Portal PWA — Design Spec

**Date:** 2026-06-03
**Track:** SP4 customer track, frontend phase
**Branch:** `sp4-customer` (off `sp4-counter-loop`)
**Status:** Design approved, pending implementation plan

## Context

The customer-portal **backend** is feature-complete and verified on `sp4-customer` (auth
foundation + reads + writes; 929 backend tests passing, not yet merged). This spec covers the
**player-facing PWA frontend** `AFK4.Customer.Web` that consumes that API, plus a small backend
slice to support per-tenant branding.

Source specs: `docs/superpowers/specs/2026-06-01-platform-customer-portal-design.md` (founder
decisions D1–D8) and `…-customer-shell-experience-design.md` (shell, out of scope here).

OTP/SMS flows and the WPF shell remain deferred (notifications Stage 6 / Windows-CI-gated).
This spec is the PWA only.

## Goals

- A mobile-first, installable PWA for players: sign in, see wallet/active session, history,
  receipts, purchases, request top-ups, book/cancel reservations, manage profile.
- Visual quality on par with smartshell.gg — direction **"Clean Esports"** (dark, restrained,
  one punchy accent, sharp geometry), themeable per tenant.
- Real multi-tenant branding in v1 (logo/name/accent resolved from the tenant key).
- Installable + offline-read: app shell works offline, last dashboard/history is shown when the
  network is down.

## Non-goals (v1)

- OTP sign-in and phone verification (deferred — UI present but disabled with honest "coming
  soon" copy). Self-edit of phone/display name (needs OTP re-verify).
- Online self-payment (top-up is request-to-counter; D5).
- Offline writes / outbox queue for the portal (writes simply block offline with a toast).
- Tajik (`tg`) locale — `@afk4/i18n` ships ru/en today; tg is owned by the localization track.
- The WPF shell and its `/api/player-self/*` session endpoints.

## Founder decisions honored

D1 responsive installable PWA · D2 phone + PIN/password (OTP later) · D3 opaque bearer tokens ·
D4 reservation request-to-confirm · D5 top-up request-only at counter · D6 single PlayerAccount
identity · D7 per-tenant branding via public tenant key · D8 wallet/booking gated on verified
phone.

---

## Architecture

### Approach

New standalone workspace package **`src/AFK4.Customer.Web`**, mirroring the existing
`AFK4.Platform.Web` stack and patterns (Vite 8 + React 19 + TS 6, Tailwind 4 + shadcn "new-york",
bun test + happy-dom + testing-library, `@afk4/money` + `@afk4/i18n`). Added to root
`package.json` `workspaces`. Rejected: bolting player routes onto an existing staff app (mixes
audiences/tokens/manifest) and a new framework (breaks repo uniformity).

shadcn primitives are copied into the app's own `components/ui` (the repo's established
non-monorepo shadcn pattern — both existing web apps do this).

### Project structure

```
src/AFK4.Customer.Web/
  package.json · vite.config.ts (+ vite-plugin-pwa) · bunfig.toml · components.json
  index.html · tsconfig.json · src/test/setup.ts
  public/            icons + generated PWA assets (default-brand placeholders)
  src/
    main.tsx · App.tsx (hand-rolled router + auth gate) · index.css (Clean Esports tokens)
    api/        playerApi.ts · brandingApi.ts · types.ts
    auth/       playerTokenStore.ts
    branding/   resolveTenantKey.ts · useBranding.ts · applyTheme.ts
    pwa/        registerSW.ts · offlineCache.ts
    components/ AppShell.tsx · BottomNav.tsx · OfflineBanner.tsx · MoneyAmount.tsx · ui/…
    screens/
      auth/ SignInScreen
      dashboard/ DashboardScreen · useDashboard · LiveSessionCard
      wallet/ WalletPanel · TopUpRequestForm · IntentList
      history/ VisitsScreen · useVisits · ReceiptScreen
      purchases/ PurchasesScreen
      reservations/ ReservationsScreen · CreateReservationForm
      profile/ ProfileScreen
    lib/ utils.ts (cn) · money.ts
```

### API client (`playerApi.ts`)

Mirror `Platform.Web`'s class pattern: constructor takes `{ baseUrl, fetchImpl?, session,
onSessionChanged }`; `buildHeaders()` injects `Authorization: Bearer {accessToken}`; on 401 call
`refreshTokenOnce()` (`POST /api/public/player/refresh`), update session via `onSessionChanged`,
retry once; `toError()` parses `{ error?, status? }` into a typed `PlayerApiError`. One method per
endpoint, returning typed contracts from `types.ts`.

### Token store (`playerTokenStore.ts`)

Same export shape as `staffTokenStore.ts` (`readSession/writeSession/clearSession/
sessionFromSignInResponse/isAccessTokenExpired`) **but persisted in `localStorage`, not
`sessionStorage`** — players are on personal devices and expect an installed PWA to stay signed
in (refresh token lives 30 days). On sign-out, clear the session **and** delete player offline
caches.

### Routing & auth gate (`App.tsx`)

Hand-rolled discriminated-union router like `Platform.Web` (`PlayerRoute` union +
`resolvePlayerRoute(pathname)` + `popstate` handling). No react-router. If `playerSession` is
null → render `SignInScreen`; otherwise render `AppShell` with the resolved screen. Bottom tab
navigation: **Главная / История / Брони / Профиль** (wallet lives inside Главная and a quick
action). Meaningful view state (active tab, open receipt) reflected in the URL.

### Tenant branding

**Backend slice (this spec):**
- Migration `AddBrandingToOrganization`: `LogoUrl` (nullable string) and `AccentColor` (nullable
  hex string) on `OrganizationEntity` (which already has the public `Slug` and `Name`).
- `GET /api/public/tenant/{tenantKey}/branding` in `Program.cs` (mirrors the `/api/public/player/*`
  pattern), `.RequireRateLimiting("player-public")`. `tenantKey == Organization.Slug`. Returns
  `{ name, logoUrl, accentColor }`. 404 when the slug is unknown or the org is not `active`.
- Contract `TenantBrandingDto` in `Shared.Contracts`.
- DevSeed sets a slug + demo branding so the frontend has something to render.

**Frontend:**
- `resolveTenantKey()` — derive the tenant key from the hostname subdomain in production
  (`{slug}.portal.afk4.net`), with a `?tenant={slug}` override for local/Linux testing; cache the
  resolved key in `localStorage` so deep links work without re-deriving.
- `useBranding()` fetches `/api/public/tenant/{key}/branding`; `applyTheme()` writes
  `accentColor` into the `--accent` CSS variable and swaps logo/name. A built-in default theme is
  the fallback when the fetch fails or no branding is set, so the app never renders unbranded.

---

## Screens & flows

All screens map to already-shipped endpoints. OTP-dependent affordances render **disabled with
honest copy**, never hidden.

| Screen | Endpoint(s) | Behavior |
|---|---|---|
| **Sign in** | `POST /api/public/player/sign-in`, `/refresh` | Phone + PIN/password. OTP button disabled ("скоро"). Backend does anti-enumeration + lockout; UI shows a single generic "неверные данные". |
| **Dashboard** | `GET /api/me/dashboard` | Wallet balance + debt; active-session card. Timer and "≈ accrued" **tick client-side every 1s**; full refetch every ~30s. open → accrued cost; fixed → remaining seconds; none → empty state. |
| **Wallet** (on Dashboard) | `POST /api/me/wallet/top-up-intent`, `GET …/top-up-intents` | Request top-up (amount, currency default TJS). **D8 gate**: no verified phone → form closed with explanation + recovery path. Intent list with pending/fulfilled/expired states. |
| **Visit history** | `GET /api/me/visits?cursor=` | Cursor-paginated infinite list. Row: seat, start/end, duration, time charge, POS total, grand total. |
| **Receipt** | `GET /api/me/visits/{id}/receipt` | Read-only receipt. Foreign/missing → 404 screen. |
| **Purchases** | `GET /api/me/purchases?cursor=` | Cursor-paginated standalone POS purchases. |
| **Reservations** | `GET/POST /api/me/reservations`, `DELETE …/{id}` | List (pending→confirmed→seated/cancelled). Create for a future time (**D8 gate**). Cancel with confirm. |
| **Profile** | `GET/PATCH /api/me/profile` | Preferred locale (ru/en) + marketing opt-in. Phone/display name read-only with "изменение через OTP, скоро". Sign out. |

**Live session ticking:** the dashboard projects the accrued cost / remaining time locally
between refetches (labelled an estimate; counter checkout is the source of truth). One 1s
interval drives the display; a ~30s poll refetches `/api/me/dashboard`. Honors
`prefers-reduced-motion` (no animated flourishes; the numeric tick itself is essential and kept).

---

## PWA & offline

- **Install:** `vite-plugin-pwa` generates `manifest.json` (name/icons/theme from default brand;
  per-tenant name applied at runtime) + a service worker. "Add to home screen" works.
- **App shell:** precache JS/CSS/fonts/icons so the shell starts and opens offline.
- **Offline read cache:** runtime **NetworkFirst** caching for `GET /api/me/dashboard`,
  `/api/me/visits`, `/api/me/purchases`. Online → fresh; on network failure → last cache +
  `OfflineBanner` ("Офлайн — данные на HH:MM"). Writes (top-up / reservation / profile) block
  offline with a toast; no offline queue (YAGNI for the portal).
- **Cache safety:** `/api/me/*` responses are authenticated → on sign-out, `caches.delete(...)`
  all player caches so data never leaks to the next account on a shared device.

---

## Visual system — "Clean Esports"

Dark theme on CSS-variable tokens in `index.css`:

- **Surfaces:** `--bg #101314`, `--surface #181c1d`, `--border #262c2c`. Depth via *lightening*
  surfaces, not shadows (dark-theme rule).
- **Text:** 87 / 60 / 38% opacity for primary / secondary / disabled; never pure white.
- **Accent:** single tenant token `--accent` (default lime `#c8ff00`), overridden by
  `applyTheme()` from branding. Roughly 60-30-10 neutral/surface/accent.
- **Type:** one distinctive sans (not Inter — e.g. Geist/Satoshi), modular scale ratio 1.25,
  tabular numerals for balance and timer, tightened tracking on large headings.
- **Spacing:** 4px scale; concentric border radii.
- **States:** all six microstates (default/hover/pressed/`:focus-visible`/disabled/loading) on
  every interactive element — including quiet ones (tab bar, list rows, icon buttons). Every data
  view has loading (skeleton mirroring final layout) / empty / error states.
- **Accessibility floor:** contrast ≥ 4.5:1 (3:1 large/non-text), touch targets ≥ 44pt, visible
  focus on everything focusable, `prefers-reduced-motion` honored.

States and tokens are encoded as component variants / design tokens (per interface-limb §9), not
per-screen one-offs.

---

## Testing

`bun test` + happy-dom + testing-library, fetch mocked via `bun:test`'s `mock()` (pattern from
`clubApi.staff.test.ts`). Coverage:

- `playerApi` — Bearer header, 401→refresh→retry, error parsing.
- `playerTokenStore` — read/write/clear, expiry check, localStorage persistence.
- `resolveTenantKey` — subdomain, `?tenant=` override, cached key.
- `applyTheme` — writes `--accent`, falls back to default.
- Screens — render of loading/empty/error states; D8 gate closes wallet/reservation forms when
  phone unverified; live timer tick + formatting.
- Backend — branding endpoint 200 / 404 / rate-limit (TDD).

---

## Build sequence (TDD, small tasks)

1. **Backend:** branding migration + `GET /api/public/tenant/{key}/branding` + `TenantBrandingDto`
   + DevSeed branding.
2. **Scaffold** `AFK4.Customer.Web` (vite/react/ts/tailwind/shadcn/pwa) + workspace entry + smoke
   test.
3. Token store + `playerApi` + `brandingApi`.
4. Branding bootstrap (`resolveTenantKey` → fetch → `applyTheme`, default fallback).
5. App shell: router + auth gate + bottom nav + **Sign in** screen.
6. **Dashboard** + live session card.
7. **Visit history** + receipt.
8. **Purchases.**
9. **Wallet** (balance + top-up request + intent list, D8 gate).
10. **Reservations** (list / create / cancel, D8 gate).
11. **Profile** (locale + marketing + sign out).
12. **PWA:** manifest / icons / SW + offline read cache + offline banner + sign-out cache clear.
13. **i18n** wiring (ru/en) across screens; shadcn primitives ported as needed along the way.

Implementation will likely be split into **two plans** (tasks 1–6 "foundation + shell +
dashboard", then 7–13 "remaining screens + PWA") to keep each plan focused.

## Open follow-ups (post-v1)

- OTP sign-in + phone verification (gated on notifications SMS) → unlocks phone/display-name
  self-edit.
- Online self-payment wired to a real gateway (the `PaymentIntent` seam already exists).
- Tajik (`tg`) locale once the localization track ships the catalog.
- Web-push notifications and the WPF shell track.
