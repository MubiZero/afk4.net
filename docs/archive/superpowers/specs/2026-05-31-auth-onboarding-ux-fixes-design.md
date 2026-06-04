# Auth & Onboarding UX Fixes — Design

**Date:** 2026-05-31
**Status:** Approved design, pre-implementation
**Scope:** `src/AFK4.Platform.Web` (frontend) + `src/AFK4.Platform.Api` (one new endpoint, no migration)

## Context

A UX audit of the sign-in / onboarding userflow surfaced seven gross convenience
violations. The trigger: the **club web sign-in requires a "Club key" (org slug)**
that a returning owner has no natural way to know, while the platform-admin sign-in
needs only login + password. This spec fixes all seven.

The web **club console** (`/club/*`, `StaffSignIn`) is used by **owners and
managers**. Shift operators/technicians do **not** use the web console — they use
the native Operator App, which is enrolled to a branch and already carries club
context. This distinction shapes the identity decision below.

## Decisions (locked)

1. **Identity model = B (global resolution for web users only).** The web club
   sign-in resolves the club from the entered **login** across all clubs. Operators
   keep simple per-club logins (the Operator App supplies their club). **No DB
   migration** — the existing `(OrganizationId, NormalizedUserName)` unique index
   stays. The web login is resolved by a new endpoint, with a password-verified
   disambiguation fallback in the (rare) case the same login+password is valid in
   more than one club.
2. **Login field is labelled "Логин"** (not "User name"/"username") everywhere in
   auth UI.
3. **Display name is auto-derived from the login** during onboarding (no separate
   required field); editable later in profile.
4. **forgot-password is kept** as a localized "Скоро" placeholder now; real
   self-service reset is future work (the user has an email server to wire up —
   tracked separately). Not deleted.
5. **All auth/onboarding screens are localized** (RU primary, EN fallback). They
   are currently hardcoded English.

## Backend changes (`AFK4.Platform.Api`)

### New endpoint: `POST /api/auth/staff/sign-in-by-login`

Request: `StaffSignInByLoginRequest(string Login, string Password)`.

Resolution algorithm (in `IStaffCredentialService.SignInByLoginAsync`):

1. Normalize the login (same normalization as existing username handling).
2. Find **all** `StaffUserEntity` rows across all orgs with that
   `NormalizedUserName` (no org filter).
3. For each candidate, verify the password (reuse the existing credential check).
4. Collect candidates whose password **matches**:
   - **0 matches → 401** (unknown login or wrong password — single generic message,
     no tenant-existence leak).
   - **1 match → 200** with the normal `StaffSignInResponse` (org resolved).
   - **2+ matches → 409** `StaffSignInChooseClubResponse(IReadOnlyList<ClubChoice>)`
     where `ClubChoice = (Guid OrganizationId, string Name)`. The client shows a
     club picker and completes via the existing org-scoped
     `POST /api/auth/staff/sign-in` (it still holds the password in form state).

Notes:
- Verifying the password **before** revealing any club name avoids leaking which
  logins/clubs exist.
- Existing endpoints (`/api/auth/staff/sign-in`,
  `/api/auth/staff/sign-in-by-tenant-key`) are **unchanged** — the native Operator
  App and any other callers keep working.

### Tests (backend)

- `sign-in-by-login`: single-club success; wrong password → 401; unknown login →
  401; two clubs with the same login but only one correct password → signs into the
  correct one; two clubs where the same login+password validates in both → 409 with
  both club names.
- No migration is added, so no migration test.

## Frontend changes (`AFK4.Platform.Web`)

### 1 — Remove "Club key" from club sign-in
`components/StaffSignIn.tsx`: drop the `tenantKey` field and `initialTenantKey`
prop. Form = **Логин + Пароль**. On submit call the new
`staffAuthApi.signInByLogin(login, password)`:
- `signedIn` → proceed.
- `chooseClub` → render a club picker (list of returned clubs); selecting one calls
  the existing org-scoped sign-in with `{ organizationId, login, password }`.
`api/staffAuthApi.ts`: replace `signIn(tenantKey, userName, password)` with
`signInByLogin(login, password)` + `signInToClub(organizationId, login, password)`.

### 2 — Localize auth screens
Route `SignIn`, `StaffSignIn`, `AcceptInvite`, and the forgot-password placeholder
through `useI18n()`. Add an `auth.*` key group to **both** ru and en blocks of
`i18n/messages.ts` (the ru/en parity test enforces completeness). All hardcoded
strings — titles, labels, buttons, hints, and error messages — move to keys.

### 3 — Land on dashboard after club sign-in
`App.tsx`: change the club `onSignedIn` from `navigateToClubInstall` to navigate to
`/club` (clubDashboard).

### 4 — Simplify onboarding form
`components/AcceptInvite.tsx`:
- Rename "User name" → **«Логин»** (`type="text"`/`email`, `autoComplete="username"`).
- **Remove the separate required "Display name" field.** If the client sends an
  empty/absent display name, the **backend** derives it from the login (local part
  before `@`, else the whole login). Confirm `AcceptOwnerInviteAsync` already
  tolerates an empty display name and add the derivation if not.
- Required fields become: setup code (prefilled from invite link), login, password,
  confirm password.

### 5 — forgot-password placeholder (kept)
`ReservedAuthPage`: localize it to an honest "Скоро / обратитесь к администратору"
message. Keep the `/auth/forgot-password` and `/auth/reset-password` routes so the
page survives for the future flow. **Do not add any link to it from the sign-in
screens** while it is non-functional — the only "fix" the dead-end needs now is an
honest, localized message; the link arrives with the real reset flow (future work).

### 6 — Auto-slug in new-tenant
`platform/tenants/NewTenantScreen.tsx`: `organizationSlug` is **auto-generated from
the organization name** with a live, editable preview. Add a `slugify` helper with
basic **ru→latin transliteration** (e.g. "AFK4 Душанбе" → `afk4-dushanbe`) producing
`[a-z0-9-]` per `SlugValidator`. The admin can still override. `branchSlug` keeps its
`main` default. Slug is now purely a technical identifier (no longer needed for
login).

### 7 — Cross-link sign-in ↔ setup code
`StaffSignIn`: add a localized link "Впервые здесь? У меня есть код приглашения" →
the `AcceptInvite` screen. `AcceptInvite` already links back to sign-in; make it
symmetric.

### Tests (frontend)
- `StaffSignIn`: no club-key field; submit calls `signInByLogin`; `chooseClub`
  response renders the picker and completes via `signInToClub`.
- i18n: new `auth.*` keys present in ru and en (parity test covers it).
- `slugify`: latin + cyrillic inputs produce valid slugs; NewTenant preview updates
  from name and respects manual override.
- `App.test`: club sign-in lands on `/club` dashboard; forgot-password routes still
  resolve to the localized placeholder.

## Out of scope / future work

- **Self-service password reset** (forgot-password → email link). Requires wiring
  the user's existing email server. Tracked as a separate future task.
- **Admin-assisted password reset** (owner resets operator; platform admin resets
  owner) — a no-email alternative, deferred; user to choose direction later.
- Native Operator App sign-in flow (unchanged here).

## Risks

- **Disambiguation leak:** mitigated by verifying the password before returning any
  club name; 0-match and wrong-password both return a single generic 401.
- **Transliteration coverage:** the ru→latin map is basic; unusual characters fall
  back to being stripped. Slug stays editable, so the admin can always fix an
  unsatisfactory auto-slug.
- **Operators on the web console:** an operator whose login happens to resolve could
  reach the web console; existing permission gating governs what they can see. Out
  of scope to restrict here.
