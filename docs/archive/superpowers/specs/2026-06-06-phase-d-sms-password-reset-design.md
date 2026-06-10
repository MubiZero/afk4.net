# Phase D — SMS password reset (backend) — Design

**Date:** 2026-06-06
**Status:** Approved for planning
**Epic:** Phone-based staff registration (`2026-06-05-phone-staff-registration-design.md`, §5 Phase D)

## 1. Goal & scope

Let a staff member who forgot their password reset it **by SMS**: request a one-time
code to their verified phone, then set a new password with that code. This is the final
phase of the phone epic; landing it closes the epic.

**Scope is backend-only** (decision 2026-06-06): the notification template, the reset
service, two public endpoints, and tests. There is **no user-facing screen** in this
phase. The reset UI is deliberately deferred to the *email-identity-parity* epic, where a
single **channel-aware** reset screen (SMS *or* email) is built once — building an
SMS-only screen now and reworking it for email later would be double work.

The existing **email** password reset (`/api/auth/staff/forgot-password` +
`reset-password`, token by email) is untouched and stays in place.

## 2. Current state we build on

All paths under `src/AFK4.Platform.Api` unless noted. Verified in code 2026-06-06.

- **OTP infrastructure (Phase B), fully reusable.** `StaffPhoneOtpEntity` (table
  `staff_phone_otps`) already has `Purpose` enum value `PasswordReset = 1` — a Phase D
  placeholder. `IPhoneOtpHasher`/`Sha256PhoneOtpHasher` (SHA-256 hex), `IPhoneOtpGenerator`/
  `RandomPhoneOtpGenerator` (6-digit), `PhoneOtpOptions` (5-min TTL, 3 attempts, 60s resend
  cooldown, 5 sends/hour). All registered in DI (`Program.cs:243-247`).
- **`EfStaffPhoneVerificationService`** is the structural template to mirror: OTP create →
  `INotificationService.SendNowAsync` (channel SMS) → confirm with TTL/attempts/single-use.
- **Email password reset core** (`EfStaffPasswordResetService`): on completion it rehashes
  the password via `PasswordHasher<StaffUserEntity>` and **revokes the account's active
  access + refresh tokens** (`RevokeActiveTokensAsync`). Phase D reuses this revocation.
- **Phone resolution** (`PasswordHashingStaffCredentialService.SignInByPhoneAsync`):
  `PhoneNumberNormalizer.Normalize` (E.164 digits, 11–15) → staff where `NormalizedPhone ==`
  normalized AND `PhoneVerifiedAtUtc != null` AND `IsActive`. The reset path resolves the
  same way (only verified, active phones).
- **Notification templates**: embedded JSON at `Notifications/Templates/{locale}/{key}.json`;
  `NotificationTemplateKeys.All` is validated at startup by `EnsureKeysPresent` (a key with
  no file is a startup failure).
- **Rate limiter**: `AddRateLimiter` with IP-partitioned fixed-window policies
  (`player-public` = 10/min). New public endpoints reuse this pattern.

## 3. Architecture & decisions

### 3.1 SMS template
Add `NotificationTemplateKeys.StaffPasswordResetSms = "staff.password_reset_sms"` (and to
`All`). Create `Notifications/Templates/{ru,en,tg}/staff.password_reset_sms.json`. Cyrillic,
≤65 chars (single payom segment). Tokens: `code`, `expiresInMinutes`. RU body:
`AFK4.NET: код сброса пароля {{code}}. Никому не сообщайте.`

### 3.2 Reset service — `EfStaffPhonePasswordResetService : IStaffPhonePasswordResetService`
Mirrors `EfStaffPhoneVerificationService`; uses the Phase B OTP infra with
`Purpose = PasswordReset`.

- **`RequestResetAsync(rawPhone, ct)`**
  1. Normalize phone. If invalid format → `InvalidPhone`.
  2. Resolve staff by `NormalizedPhone` among **verified + active**.
  3. If found and not within resend cooldown / hourly cap: generate OTP (`PasswordReset`),
     `SendNowAsync` SMS with `StaffPasswordResetSms`.
  4. **Always** return a uniform `Accepted(expiresInSeconds, resendAfterSeconds)` — same
     whether the account exists or not (**anti-enumeration**; if no account, silently no
     send). Cooldown/cap simply suppress the send; they do not change the response.

- **`ResetAsync(rawPhone, code, newPassword, ct)`**
  1. Normalize phone; resolve verified+active staff.
  2. Find latest unconsumed `PasswordReset` OTP for that staff.
  3. Validate (mirrors `ConfirmAsync`): exists → else `NoActiveCode`; not expired → else
     `Expired`; attempts `< MaxAttempts` → else `TooManyAttempts`; code hash matches → else
     increment attempt, `InvalidCode(remaining)`.
  4. On success: set `PasswordHash`, mark OTP `ConsumedAtUtc`, **revoke active tokens**,
     save → `Success`.
  - Anti-enumeration: a missing account collapses to `NoActiveCode` (generic
    "invalid or expired"), never "no such phone".

### 3.3 Reuse the revocation core (DRY)
Extract `EfStaffPasswordResetService.RevokeActiveTokensAsync` into a small shared static
helper `StaffTokenRevocation.RevokeActiveAsync(db, organizationId, staffUserId, now, ct)`
(pure EF, no behavior change). Both the email reset service and the new phone reset service
call it — no duplicated security-sensitive logic. Password hashing stays a local
`PasswordHasher<StaffUserEntity>` (`new()`), as in the email service.

### 3.4 Contracts (`AFK4.Shared.Contracts/Identity`)
- `StaffForgotPasswordByPhoneRequest(string PhoneNumber)`
- `StaffResetPasswordByPhoneRequest(string PhoneNumber, string Code, string NewPassword)`

### 3.5 Endpoints (`Endpoints/AuthEndpoints.cs`, public)
A user who forgot their password has no bearer token, so both are public, both rate-limited
by IP via a new fixed-window policy `staff-reset` (modeled on `player-public`).

- `POST /api/auth/staff/forgot-password-by-phone` → `InvalidPhone` → 400 `{error:"invalid_phone"}`;
  `Accepted` → 200 `{expiresInSeconds, resendAfterSeconds}`.
- `POST /api/auth/staff/reset-password-by-phone` → validate the new password with the
  existing `ValidateStaffPassword` helper (400 on failure); then map service status,
  mirroring the confirm endpoint: `Success` → 200; `InvalidCode` → 400
  `{error:"invalid_code", remainingAttempts}`; `Expired`/`NoActiveCode` → 410
  `{error:"code_expired"}`; `TooManyAttempts` → 429 `{error:"too_many_attempts"}`.

### 3.6 DI
Register `IStaffPhonePasswordResetService → EfStaffPhonePasswordResetService` (Scoped),
next to the phone-verification registration. Add the `staff-reset` rate-limiter policy.

## 4. Security & rate limiting
- OTP: 6 digits, 5-min TTL, ≤3 verify attempts, 60s resend cooldown, ≤5 sends/phone/hour
  (existing `PhoneOtpOptions`). Stored **hashed**, single-use (`ConsumedAtUtc`).
- Only **verified + active** phones can request or complete a reset.
- Uniform response on request (anti-enumeration); generic "invalid or expired" on
  completion for a missing account/OTP.
- Public endpoints IP-rate-limited (`staff-reset`).
- On success, all active access + refresh tokens are revoked (an attacker who knew the old
  password is logged out everywhere).

## 5. Edge cases
- Phone formats `+992…`, spaces, dashes → normalized to digits; non-E.164 → `invalid_phone`.
- Unverified or inactive phone → treated as "no account" (uniform/`NoActiveCode`).
- SMS provider down → the SMS send fails inside `SendNowAsync`; request still returns the
  uniform `Accepted` (no leak), and ops see the failed delivery — consistent with
  verification. (No code-fallback concept here; this is the reset path.)
- Reusing an already-consumed or expired code → `code_expired`/`NoActiveCode`.

## 6. Testing
- **Unit/endpoint** (`tests/AFK4.Platform.Api.Tests`, xUnit, InMemory +
  `WebApplicationFactory<Program>` with a fake `ISmsTransport`):
  - request: verified phone → OTP row created + SMS sent; unknown phone → 200, no OTP, no
    send; invalid format → 400.
  - reset: wrong code → attempt increments + `invalid_code`+remaining; correct code →
    password changed, OTP consumed, active tokens revoked, sign-in works with the new
    password and fails with the old; expired → 410; >3 attempts → 429.
  - template: `staff.password_reset_sms` renders for ru/en/tg (mirror
    `StaffPhoneVerificationTemplateTests`).
- **Regression:** full backend suite stays green (baseline 1055/1055 + new tests); the
  `StaffTokenRevocation` extraction must not change existing email-reset tests.

## 7. Out of scope (moved, not dropped)
- The user-facing reset **screen** (wizard "Забыли пароль?" link + flow) → *email-identity-parity*
  epic, built once channel-aware.
- Login-by-email and broader email parity → same epic.
- No changes to the email reset path, owner code, or 2FA.
