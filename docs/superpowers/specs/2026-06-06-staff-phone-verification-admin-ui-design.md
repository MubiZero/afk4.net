# Phase B-UI — Staff phone verification in the admin panel — Design

**Date:** 2026-06-06
**Status:** Approved for planning
**Author:** (brainstormed)
**Parent spec:** [2026-06-05-phone-staff-registration-design.md](2026-06-05-phone-staff-registration-design.md) §5 Phase B

## Revision 2 (2026-06-06) — placement corrected

During planning we found the project has **two** admin frontends, and the original
single-frontend assumption below (§2, §5) was wrong:

- **`AFK4.Platform.Web`** (browser club admin, shadcn/ui) already has a live, localized
  **profile screen** (`club/profile/ProfileScreen.tsx`) and the **owner-code panel**
  (`club/install/OwnerCodePanel.tsx`) — the owner code that phone login replaces. This is
  the natural, lowest-effort home for the self-service phone card.
- **`AFK4.Operator.App.Web`** (desktop operator app) has no profile area — hence the
  header account-panel design below.

**Decision (user-approved): build in BOTH frontends, Platform.Web first.** The phone card
goes into the existing `ProfileScreen` in Platform.Web (shadcn `Card`/`Input`/`Button`),
then the same flow as a header-opened `AccountPanel` in Operator.App.Web.

**Other corrections applied during planning:**
- i18n `t()` is **key-only (no interpolation)** in both apps — numeric details (remaining
  attempts, seconds) are concatenated in JSX, not embedded in the catalog string. New keys
  use the shared namespace `account.phone.*` in `locales/{ru,en,tg}.json` (parity test
  enforces all three locales; `bun run gen` in `packages/i18n` regenerates `messages.ts`).
- **Resend/expiry countdowns are dropped for v1** (rule #19): the backend enforces the
  60s cooldown / 5-min TTL and returns `cooldown_active` / `code_expired`, which map to
  clear messages. Countdowns can be added later if needed.
- **Owner-visibility badge is deferred to a separate follow-up.** Two different staff-list
  architectures (Platform.Web `OperatorsTable` via `settingsModel`/`OperatorRow`, and the
  Operator.App.Web record-based rows) make it a self-contained chunk; landing the
  end-to-end self-service flow in both apps is the priority. The backend `StaffUserDto`
  change moves into that follow-up too (this plan adds only `GET /api/auth/staff/phone`).

The authoritative task-by-task breakdown is in
[../plans/2026-06-06-staff-phone-verification-ui.md](../plans/2026-06-06-staff-phone-verification-ui.md).
Sections below are kept for the flow/error-mapping design, which still holds; read them
through the lens of this revision (placement + the two corrections above).

## 1. Goal

Give a logged-in operator a way to **set and SMS-verify their own phone number** inside
the Operator admin panel (`AFK4.Operator.App.Web`), and let the owner **see who has a
verified phone** in the staff list. Without this UI the already-shipped phone-login
backend (PR #56) is unreachable — no operator can attach a verified phone, so
sign-in-by-phone (Phase C) and SMS reset (Phase D) have nothing to work against.

## 2. Locked decisions

- **Self-service, not admin-on-behalf.** The backend verification endpoints act on the
  **caller's own** phone (resolved from the Bearer token). There is no "owner verifies
  employee X" endpoint, and we will not add one — the SMS code goes to the phone holder,
  so only the holder can confirm. The owner gets **read-only visibility** (a badge), not
  control.
- **Lives in a new account panel opened from the header**, reachable by **every role**
  (the header `displayName · mode` is always visible; left-rail workspaces are
  permission-gated and would hide it from cashiers/technicians who still need to verify
  their own phone). Phase D (SMS password reset) will later share this account home.
- **i18n via `t()`**, new copy in `locales/{ru,en,tg}.json` → `bun run gen`. Mirrors
  `PaymentGatewaysWorkspace.tsx` (which already uses `useI18n()`), not the hardcoded-RU
  style of the `App.tsx` monolith. New code goes in **its own files**, not into `App.tsx`.
- **Verification UX mirrors the existing Telegram-attach state machine** in
  `PaymentGatewaysWorkspace.tsx` (`idle → code_required → done`), extended with resend
  cooldown + expiry countdowns and a "change number" affordance.

## 3. Current state (what we build on)

**Backend (PR #56, already merged):**
- `POST /api/auth/staff/phone/start-verification { phone }` →
  `{ expiresInSeconds, resendAfterSeconds }`. Auth: staff Bearer token; acts on
  `StaffContext.StaffUserId`. Errors: `400 invalid_phone`, `429 cooldown_active`
  (`resendAfterSeconds`), `429 rate_limited`, `502 sms_unavailable`.
  (`Program.cs` ~707–736.)
- `POST /api/auth/staff/phone/confirm { code }` → `{ phone }` (E.164 display form).
  Errors: `400 invalid_code` (`remainingAttempts`), `410 code_expired`,
  `410 no_active_code`, `429 too_many_attempts`, `409 phone_already_in_use`.
  (`Program.cs` ~738–768.)
- OTP: 6 digits, 5-min TTL, ≤3 attempts, 60s resend cooldown, ≤5 sends/hour
  (`PhoneOtpOptions`). `StaffUserEntity` already has `Phone`, `NormalizedPhone`,
  `PhoneVerifiedAtUtc`.
- Self-service "me" convention already exists: `GET /api/staff/me/owner-code`
  (`Program.cs` ~2293) — the new read endpoint follows this shape.

**Frontend (`src/AFK4.Operator.App.Web`):**
- App shell header `top-command` (`App.tsx` ~10355–10370) renders
  `operatorDisplayNameLabel(authSession.displayName) · shellModeLabel(...)` + a "Выйти"
  button — always visible, role-independent.
- Staff management lives in `BackendSettingsWorkspace` → "Сотрудники" section
  (`App.tsx` ~8906–8964), gated by `identity.branch_staff.manage`. Staff rows show
  `displayName`, `userName · roles · status`.
- API client `operatorApiClients.ts`: `PlatformApiClient` (`platformApi.ts`) with
  `get/post/patch`; `PlatformApiError { status, statusText, body }`; errors surfaced via
  `projectOperatorError(error).detail` and a `feedback` state machine
  (`idle|pending|confirmed|failed`).
- `StaffUserDto` (contract `src/AFK4.Shared.Contracts/Identity/StaffUserDto.cs`):
  `StaffUserId, OrganizationId, UserName, DisplayName, IsActive, RoleNames, CreatedAtUtc`
  — **no phone fields today**. FE alias is `Record<string, unknown>` read via `readString`.
- Reference flow: `PaymentGatewaysWorkspace.tsx` (`useI18n()`, `AttachPhase` state machine,
  busy flag, `projectOperatorError`); test `PaymentGatewaysWorkspace.test.tsx`. Frontend
  runs `bun test` (happy-dom + jest-dom).

## 4. Backend additions (small, read-only)

The verification *flow* is done; two read-side gaps remain. Neither adds verification logic.

1. **`GET /api/auth/staff/phone`** (new) — current operator reads their own phone status.
   Auth: staff Bearer token; resolves `StaffContext.StaffUserId`. Returns new contract
   `StaffPhoneStatusResponse(string? Phone, DateTimeOffset? PhoneVerifiedAtUtc)`
   (`Phone` = E.164 display form or `null` if never set; `PhoneVerifiedAtUtc` = `null`
   until verified). Available to **any** authenticated staff (no special permission) —
   this is what makes the self-service screen renderable for cashiers/technicians.
   Follows the `/api/staff/me/owner-code` pattern.
2. **Expose phone on `StaffUserDto`** — add `string? Phone` and
   `DateTimeOffset? PhoneVerifiedAtUtc` to the record + map them in `ToStaffUserDto`
   (`Program.cs` ~12874; the `StaffUserEntity` already carries both). Powers the owner's
   read-only badge. No extra DB queries (entity already loaded at all 5 call sites).

## 5. Frontend architecture

New files under `src/AFK4.Operator.App.Web/src/`:

- **`PhoneVerificationCard.tsx`** — the self-service flow (state machine below). Props:
  `{ api, t, onVerified? }`. Owns its own state; talks to the API client; emits the
  verified phone upward so the panel can refresh.
- **`AccountPanel.tsx`** — the account surface (modal/drawer) opened from the header.
  Shows the current operator (`displayName`, role) and embeds `PhoneVerificationCard`.
  Becomes the future home for Phase D (SMS password reset).

Edits to existing files:
- **`operatorApiClients.ts`** — add three methods + DTOs:
  - `getMyPhone(): Promise<StaffPhoneStatusDto>` → `GET /api/auth/staff/phone`
  - `startPhoneVerification(req: { phone }): Promise<StaffPhoneVerificationStartedDto>`
    → `POST /api/auth/staff/phone/start-verification`
  - `confirmPhoneVerification(req: { code }): Promise<StaffPhoneConfirmedDto>`
    → `POST /api/auth/staff/phone/confirm`
  - DTOs: `StaffPhoneStatusDto { phone: string|null; phoneVerifiedAtUtc: string|null }`,
    `StaffPhoneVerificationStartedDto { expiresInSeconds; resendAfterSeconds }`,
    `StaffPhoneConfirmedDto { phone }`.
- **`App.tsx`** — (a) make the header identity (`displayName · mode`) a button that opens
  `AccountPanel`; (b) render `AccountPanel`; (c) in the "Сотрудники" rows, render a
  read-only phone badge from the now-extended `StaffUserDto`.
- **`locales/{ru,en,tg}.json`** + `bun run gen` — new keys under `account.phone.*`.
- **`styles.css`** — account panel, phone badge, verified state (reuse existing form /
  feedback / badge patterns; no new design language).

## 6. Verification state machine (`PhoneVerificationCard`)

```
                 GET /api/auth/staff/phone (on mount)
                          │
        verified phone? ──┴── no ──►  idle
              │ yes                     │  [phone input] "Получить код"
              ▼                         │      POST start-verification
          verified                      ▼
   [+992… ✓ подтверждён]            code_required ◄── "Отправить повторно" (after cooldown)
   "Изменить номер" ──► idle        [code input] "Подтвердить"
                                        │  POST confirm
                                        ▼ success
                                    verified
```

- **idle:** phone `<input inputMode="tel">`, placeholder `+992 90 123-45-67`,
  "Получить код" → `startPhoneVerification`. On success store
  `expiresInSeconds`/`resendAfterSeconds`, start both countdowns, go `code_required`.
- **code_required:** 6-digit `<input inputMode="numeric">`, "Подтвердить" →
  `confirmPhoneVerification`. Show "код действует ещё M:SS" (expiry) and "Отправить
  повторно" disabled until the resend countdown reaches 0 (re-calls start). On expiry
  reaching 0, prompt to resend.
- **verified:** "Телефон: {phone} ✓ подтверждён" + "Изменить номер" → back to `idle`.
- A single `busy` flag disables actions during in-flight requests (mirrors
  `PaymentGatewaysWorkspace`). Optimistic feedback <100ms via existing feedback states.

## 7. Error mapping (concrete copy — rules #32, #34)

Read `PlatformApiError.body.error` (+ `remainingAttempts`/`resendAfterSeconds`) and map to
i18n strings; never collapse to a generic message:

| Backend `error` | i18n key | RU copy |
|---|---|---|
| `invalid_phone` | `account.phone.err.invalid_phone` | Проверьте номер: нужен формат +992 90 123-45-67 |
| `cooldown_active` | `account.phone.err.cooldown` | Повторно можно через {n} сек |
| `rate_limited` | `account.phone.err.rate_limited` | Слишком много запросов кода, попробуйте через час |
| `sms_unavailable` | `account.phone.err.sms_unavailable` | SMS-сервис недоступен, попробуйте позже |
| `invalid_code` | `account.phone.err.invalid_code` | Неверный код, осталось попыток: {n} |
| `code_expired` / `no_active_code` | `account.phone.err.expired` | Код истёк, запросите новый |
| `too_many_attempts` | `account.phone.err.too_many` | Слишком много попыток, запросите новый код |
| `phone_already_in_use` | `account.phone.err.in_use` | Этот номер уже привязан к другому сотруднику |

Copy must pass the existing i18n guard test (no CAPS words, no «компьютер»).

## 8. Owner visibility (badge)

In the "Сотрудники" rows (`App.tsx` ~8920–8939), append a read-only badge derived from the
extended `StaffUserDto`: `phoneVerifiedAtUtc` set → "📱 подтверждён" (with the masked/last-
digits phone if `phone` present); else "телефон не задан". No action, no control — owners
cannot drive verification for others (see §2).

## 9. Edge cases (scale / i18n — rules #28, #34)

- Operator with **no phone yet** → `idle` on load (status returns nulls).
- Operator **already verified** → `verified` on load; "Изменить номер" restarts the flow
  against a new number (backend re-verifies and re-checks global uniqueness).
- **`phone_already_in_use`** mid-confirm → clear message; stay in `code_required` so the
  user can try a different number via "Изменить".
- **Countdowns:** expiry (≤300s) and resend (≤60s) shown as `M:SS`; survive re-render;
  cleared on unmount. Resend button only enabled at 0.
- **Long display names / unicode** in the panel header → reuse `operatorDisplayNameLabel`.
- **i18n:** all three locales (ru/en/tg) get the new keys; no hardcoded strings.

## 10. Testing

- **Frontend (`bun test`)** — `PhoneVerificationCard.test.tsx` with a mocked api client:
  loads verified vs unverified; send-code success → `code_required`; confirm success →
  `verified`; `invalid_code` shows remaining attempts; `code_expired` → resend prompt;
  resend disabled during cooldown; `phone_already_in_use` message. Light test that the
  header button opens `AccountPanel`.
- **Backend** — `GET /api/auth/staff/phone` returns own status (null when unset, values
  when verified) and requires auth; `StaffUserDto`/`ToStaffUserDto` now include the phone
  fields (extend existing staff-list/profile tests rather than duplicate).
- **Manual** — verify the flow in the running Operator app (preview) end-to-end against a
  staging SMS token, plus the owner badge in the staff list.

## 11. Out of scope (YAGNI)

- No admin-on-behalf verification (no new "verify employee X" endpoint).
- No phone editing from the owner's staff form (read-only badge only).
- No Phase C (wizard phone login / install endpoints) or Phase D (SMS password reset) —
  separate specs; this only unblocks them.
- No phone-number input masking library; a plain `inputMode="tel"` field + backend
  normalization is enough (rule #19).

## 12. Assumptions

- The account panel is the right long-term home for self-service account actions
  (phone now, password reset later).
- Reading `PlatformApiError.body.error` is the supported way to get the backend error code
  (confirm the body shape when wiring the client).
- `bun run gen` regenerates the typed i18n bundle from `locales/*.json` as it does for the
  rest of the app.
