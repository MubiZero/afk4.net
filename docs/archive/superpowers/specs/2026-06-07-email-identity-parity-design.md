# Email identity parity (staff) — design

**Date:** 2026-06-07
**Status:** Approved design, pending implementation plan
**Scope:** Staff only (owner + admins). Customers/players out of scope — they have their own OTP self-registration shell.
**Related:** [[email-identity-parity]], [[phone-staff-registration]] (Phase D, SMS reset), [[email-server-available]]

## Principle

Wherever a phone number is used as an identity/auth channel, **email must be a co-equal alternative, not a deferred afterthought** (user rule #33 — a correction names a *class*, not one case; rule #32 — no half-presence). Phase D shipped SMS password-reset backend-only and deliberately deferred the reset *screen* so it could be built once, channel-aware. This epic builds that screen and closes the email gap.

Two capabilities, each applied where it makes sense:

1. **Login by email** — on every surface that already logs in by login/password (Platform.Web, Operator.App.Web). The Setup Wizard stays phone-first by design — email login is **not** added there.
2. **Channel-aware password reset** — a single "Forgot password?" screen on all three frontends that lets the user pick **SMS** or **Email**.

## Current state (verified in code 2026-06-07)

**Backend:**
- Login by email **does not work**: `PasswordHashingStaffCredentialService.SignInAsync` (org-scoped) matches only `NormalizedUserName`; `SignInByLoginAsync` (cross-org, used by Platform.Web) likewise. `StaffUserEntity.Email` exists (nullable, no normalized/indexed column).
- Email reset **works**: `POST /api/auth/staff/forgot-password` (username or email) + `reset-password` (opaque token, 60-min TTL, emailed) → `EfStaffPasswordResetService` (in `Endpoints/StaffOnboardingEndpoints.cs`).
- SMS reset **works** (Phase D): `POST /api/auth/staff/forgot-password-by-phone` + `reset-password-by-phone` (6-digit OTP) → `EfStaffPhonePasswordResetService` (in `Endpoints/AuthEndpoints.cs`).
- Login by phone **works**: `sign-in-by-phone`.
- All reset request/response contracts already exist (email + SMS). **No new backend contracts needed.** Login-by-email reuses `StaffSignInByLoginRequest`.

**Frontends — gaps everywhere:**
- **Platform.Web** (browser SPA, fetches the API directly): `/auth/forgot-password` + `/auth/reset-password` render the placeholder `ReservedAuthPage`. `StaffSignIn` has a login field labelled "login" and **no** "Forgot password?" link.
- **Operator.App.Web** (WebView2, reaches the API **only via host-bridge** `window.chrome.webview.postMessage`): login is `org (from config) + username + password` via `auth:signIn` → backend `SignInAsync`. No forgot flow.
- **SetupWizard.Web** (WebView2, host-bridge only): `PhoneLoginScreen` (phone+password) + owner-code fallback. No "Forgot password?" link, no reset screen.

**Plumbing tax:** both WebView2 apps reach the backend strictly through the host bridge. Each new backend call they make needs (a) a TS host-bridge wrapper, (b) a .NET host method that performs the HTTP call, (c) a preview fake. Platform.Web has no such tax (direct fetch).

## Decisions (locked)

1. **Email lookup = runtime match**, no migration. Match `Email != null && Email.ToLower() == input.ToLower()`, mirroring `EfStaffPasswordResetService`. No `NormalizedEmail` column/index for now (YAGNI; revisit if login volume grows). *Trade-off accepted:* unindexed scan on the email branch, fine at staff scale (hundreds–low thousands).
2. **Email trust = email-on-file is enough to log in.** Staff email arrives only via email-code `StaffInvite` (control already proven), and reset-by-email already trusts email-on-file. No separate "verify your email" flow.
3. **Core backend change lives in `SignInAsync`.** Teach the org-scoped `SignInAsync` to resolve by `NormalizedUserName` **OR** email. This gives email login to the club-picker path and Operator for free. `SignInByLoginAsync` adds the email branch to its candidate query and issues the token from the already-loaded, password-verified candidate (instead of re-calling `SignInAsync` by username — which would break for an email login).
4. **Reset UX = one screen, channel toggle** (Email / Phone), not two separate entry points. The token-based `reset-password` page is email-only and shared.
5. **Implementation = single spec/branch, but modular M1→M5 with review checkpoints** (not one monolith).
6. **Anti-enumeration preserved** everywhere: login failures and forgot requests reveal nothing about account existence (matches existing behavior).

## Architecture

### Backend (login by email)

`PasswordHashingStaffCredentialService`:
- `SignInAsync(org, login, password)`: resolve the user by `OrganizationId == org && IsActive && (NormalizedUserName == normalized(login) || Email.ToLower() == lower(login))`. If both a username and an email match different rows, prefer the username match (deterministic; collision is pathological). Password verify unchanged.
- `SignInByLoginAsync(login, password)`: candidate query gains the email branch (`NormalizedUserName == normalized || Email.ToLower() == lower`); on single org, issue the token from the loaded candidate; on multiple orgs, the existing club-picker resolution is unchanged. The club-pick follow-up (`SignInAsync(org, login, password)`) now works for email because `SignInAsync` handles it.

No DTO, no migration. Operator's `auth:signIn` host op already calls `SignInAsync`, so Operator gets email login with zero backend changes beyond the above.

### Reset screen (shared shape, per-frontend implementation)

The two channels have genuinely different flows; the screen models both:

- **Email channel:** input (login/email) → POST `forgot-password` → terminal "if the account exists, we sent a link" state. Password is actually set on the separate `/auth/reset-password?token=…` page reached from the email link (email-only, shared, lives in Platform.Web; the email link points at the platform web URL).
- **Phone channel:** input phone → POST `forgot-password-by-phone` → inline step on the **same** screen: enter OTP code + new password → POST `reset-password-by-phone` → done.

One "Forgot password?" entry with a channel toggle. Honest, uniform, no half-presence.

### Per-frontend work

**M2 — Platform.Web** (cheapest; direct fetch):
- `StaffSignIn`: relabel login field → "Login or email"; add "Forgot password?" link to `/auth/forgot-password`.
- Replace `ReservedAuthPage` with the real channel-aware forgot screen (email + phone) and the token-based reset-password page.
- Extend `staffAuthApi.ts` with forgot/reset calls (email + phone) against existing endpoints.

**M3 — Operator.App.Web** (host-bridge tax):
- Relabel login field → "Login or email" (backend already resolves it via `SignInAsync`).
- Add "Forgot password?" link → channel-aware screen.
- New bridge ops (e.g. `auth:forgotByEmail` / `auth:resetByEmail` / `auth:forgotByPhone` / `auth:resetByPhone`) + .NET host methods (HTTP to the public endpoints) + preview fakes.

**M4 — SetupWizard.Web** (host-bridge tax):
- `PhoneLoginScreen`: add "Forgot password?" link → channel-aware screen.
- Email channel sends the link (completed in a browser via Platform.Web's reset page); SMS channel completes inline in the wizard.
- New bridge ops + .NET host methods + preview fakes (mirror M3).

**M5 — Parity check + i18n + harness runs:**
- Every login with a "Forgot password?" exposes both channels; email login works wherever login-by-login works.
- All new strings via `t()` with ru/en/tg keys (single i18n source → `bun run gen`); no hardcoded strings, no placeholders.
- Run every test harness green (backend xunit; Platform.Web / Operator.App.Web / SetupWizard.Web `bun test` + `tsc` + build; wizard .NET host tests; i18n guard).

## Testing

- **M1 (TDD):** `SignInAsync` and `SignInByLoginAsync` email resolution — single-org success, multi-org → club picker, wrong password → 401, unknown email → 401 (anti-enumeration, no enumeration via error shape), username still works, username-vs-email collision determinism.
- **M2–M4:** frontend component tests for the channel toggle, email terminal state, phone inline OTP+password step, and error/loading states; host-bridge op tests + preview fakes for M3/M4.
- **M5:** full-suite green across all harnesses; manual parity walkthrough per the checklist.

## Out of scope (YAGNI)

- Customers/players email parity (separate identity model + shell).
- Email login in the Setup Wizard (phone-first by design).
- `NormalizedEmail` column + index (revisit only if scale demands).
- Separate "verify your email" flow for staff.

## Modules summary

| Module | What | Risk |
|--------|------|------|
| M1 | Backend login-by-email (`SignInAsync` + `SignInByLoginAsync`) + tests | Low, surgical |
| M2 | Platform.Web: relabel + forgot/reset screens (email + phone) | Low (direct fetch) |
| M3 | Operator.App.Web: relabel + screen + bridge ops/host/fakes | Medium (bridge tax) |
| M4 | SetupWizard.Web: link + screen + bridge ops/host/fakes | Medium (bridge tax) |
| M5 | Parity check + i18n (ru/en/tg) + all harness runs | Low |
