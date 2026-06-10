# Phone-based staff registration for the Setup Wizard — Design

**Date:** 2026-06-05
**Status:** Approved for planning
**Author:** (brainstormed)

## 1. Goal

Replace "register a gaming PC by typing an 8-digit owner code" with "the staff member
(owner or club admin) signs into the Setup Wizard with their **phone number + password**".
The owner code stays as a fallback. The SMS provider (payom.tj) is used to **verify a
staff phone number** and to **reset a staff password by SMS** — not on every PC install.

This mirrors the proven SmartShell model (owner registers by phone; managers/IT-admins
deploy PCs with their own accounts) while avoiding SmartShell's weakness of typing the
*owner's* password on every machine — in our model each staff member uses their own
account.

## 2. Locked decisions

- **Phone + password is the primary install path; owner code is the fallback.**
- **Who installs:** any staff member with a new `devices.install` permission
  (Owner, BranchManager, Technician by default).
- **SMS (payom.tj) is used for:** (a) one-time phone-number verification when a staff
  member sets/changes their phone; (b) password reset by SMS (alongside the existing
  email reset).
- **No SMS on each PC install.** The wizard authenticates with phone+password (reuses
  the existing opaque-token staff auth).
- **OTP text:** Cyrillic, kept ≤ ~65 chars (single payom segment). Sender name `AFK4.NET`.
- **PC display name:** tightened to 3–32 chars in the wizard (borrowed from SmartShell's
  short-name convention; today it allows ≤80).

## 3. Current state (what we build on)

All paths under `src/AFK4.Platform.Api` unless noted.

- **Staff auth (opaque tokens, NOT JWT).** Endpoints `POST /api/auth/staff/sign-in`,
  `sign-in-by-login`, `sign-in-by-tenant-key`, `refresh` (Program.cs ~627–680).
  `IStaffCredentialService` (`PasswordHashingStaffCredentialService`) verifies password
  via `PasswordHasher<StaffUserEntity>`; `IStaffTokenService` (`OpaqueStaffTokenService`)
  issues `StaffSignInResponse(StaffUserId, OrganizationId, DisplayName, AccessToken[8h],
  RefreshToken[30d], BranchIds, Permissions)`. `StaffAuthenticationMiddleware` resolves a
  Bearer token to `StaffContext(StaffUserId, OrganizationId, BranchIds, Permissions)` via
  `IStaffContextAccessor.Current`.
- **`sign-in-by-login`** resolves a staff user globally from a login string (no org
  needed) → `StaffLoginResolution`. Our phone sign-in mirrors this.
- **Password reset.** `EfStaffPasswordResetService` + `PasswordResetTokenEntity`
  (`TokenHash`, 60-min TTL, `ConsumedAtUtc` single-use). Email sent via
  `INotificationService.SendNowAsync` with template `NotificationTemplateKeys.StaffPasswordReset`
  (tokens: `displayName`, `code`, `expiresInMinutes`).
- **Owner-code crypto pattern** to mirror for OTP: `IOwnerCodeHasher`
  (`Normalize`/`Hash` SHA256-hex/`Suffix`), `RandomOwnerCodeGenerator`, `OwnerCodeOptions`
  (`SectionName`, `Lifetime`), `OwnerCodeEntity`.
- **Notifications already model SMS.** `NotificationChannel` enum includes `Sms`.
  `INotificationChannel.SendAsync` + `ChannelResult` (Permanent/Transient).
  `ISmtpTransport` → `MailKitSmtpTransport` (singleton); `SmtpEmailChannel` maps transport
  exceptions to permanent/transient. `NotificationOptions` bound from `"Notifications"`.
- **Permissions.** `StaffPermissionNames` constants (`{domain}.{sub}.{action}`),
  `PermissionCatalog.GetPermissions(roles)`, roles in `StaffRoleNames`
  (`owner`, `branch_manager`, `shift_supervisor`, `cashier_operator`, `technician`,
  `accountant_auditor`). Endpoints gate via
  `StaffAuthorizationService.RequireBranchPermissionAsync(branchId, permission, ct)` /
  `RequireOrganizationPermission(permission)`.
- **StaffUserEntity** (`staff_users`): `StaffUserId, OrganizationId, UserName,
  NormalizedUserName, DisplayName, Email?, PasswordHash, IsActive, CreatedAtUtc`; unique
  index `(OrganizationId, NormalizedUserName)`.
- **Install service.** `EfInstallService.EnrollAsync(InstallEnrollRequest)` resolves
  org/staff via `IOwnerCodeService.LookupActiveAsync(ownerCode)`. Install endpoints
  `/api/install/{discover,seats,enroll}` are public + IP-throttled (`IInstallRequestThrottle`).
- **Setup Wizard client.** `SetupWizardApiClient` (`ISetupWizardApiClient`) posts to
  `api/install/{discover,seats,enroll}`; base URL `SetupWizardDefaults.PlatformBaseUrl`
  (`https://afk4.staging.mubi.dev`). Native bridge `SetupWizardWebHostBridge` proxies
  `wizard:discover|createSeat|enroll`; preview fakes in `Preview/PreviewSetupWizard.cs`.

## 4. Target architecture

```
Wizard (phone path)                    Backend
─────────────────                      ───────
PhoneLoginScreen (phone+password) ──►  POST /api/auth/staff/sign-in-by-phone
   │   (fallback link → OwnerCodeScreen)   → resolve staff by NormalizedPhone (verified)
   │                                       → verify password → opaque AccessToken
   ▼ holds AccessToken
BranchSelection / Role / Device   ──►  authenticated install endpoints
   │                                   (org/staff resolved from StaffContext,
   ▼                                    require devices.install on the branch)
enroll ───────────────────────────►  device enrolled under staff identity
```

SMS is **out of the install path**. It lives in the staff account lifecycle:

```
Admin panel: staff sets/changes phone ──► start verification → payom SMS OTP
                                       ──► confirm OTP → phone marked verified
Forgot password (by phone) ────────────► payom SMS OTP → reset password
```

## 5. Phased delivery (single spec, sequenced)

### Phase A — SMS infrastructure (foundation)
- `SmsOptions` (`ConfigurationSection = "Sms"`): `BaseUrl` (default
  `https://gateway.payom.tj`), `ApiToken` (secret, from env), `SenderName` (`"AFK4.NET"`),
  `TimeoutSeconds`. Bound in Program.cs like `NotificationOptions`. Secrets from env on
  staging/prod, never committed.
- `ISmsTransport` + `PayomSmsTransport` (mirror `ISmtpTransport`/`MailKitSmtpTransport`):
  `POST {BaseUrl}/api/message`, header `Authorization: Bearer {ApiToken}`, body
  `{telephone, text, senderName, type:"SMS"}`. Map responses: `201` → ok;
  `401/403` → permanent (token/sender problem, ops); `422` → permanent (bad request);
  `5xx`/network/timeout → transient. Define `SmsTransportException(isPermanent, message)`.
- `SmsNotificationChannel : INotificationChannel` (`Channel => NotificationChannel.Sms`),
  mirrors `SmtpEmailChannel`: validates recipient phone, builds an `SmsMessage`, delegates
  to `ISmsTransport`, maps to `ChannelResult`. Registered in DI as singletons.
- SMS templates in `NotificationTemplateKeys`: `StaffPhoneVerification`,
  `StaffPasswordResetSms` (tokens: `code`, `expiresInMinutes`). Cyrillic, ≤65 chars,
  e.g. `AFK4.NET: код 123456. Никому не сообщайте.`
- **Deliverable:** we can send an SMS via the notification pipeline (`SendNowAsync`,
  channel `Sms`) and have it tested against payom (staging token).

### Phase B — Staff phone identity + sign-in-by-phone
- **Schema:** add `Phone string?` and `NormalizedPhone string?` + `PhoneVerifiedAtUtc
  DateTimeOffset?` to `StaffUserEntity`. EF: `HasMaxLength(20)`; **global unique index on
  `NormalizedPhone` filtered to verified, active rows** (so phone → exactly one staff at
  login). Migration.
  - `NormalizedPhone` = E.164 digits only (strip `+`/spaces/dashes), e.g. `992937380070`.
- **OTP entity** `StaffPhoneOtpEntity` (mirrors `PasswordResetTokenEntity` + owner-code
  hashing): `Id, StaffUserId, OrganizationId, Phone, Purpose (PhoneVerification|PasswordReset),
  CodeHash (SHA256 hex), CreatedAtUtc, ExpiresAtUtc (5 min), AttemptCount, ConsumedAtUtc`.
  `IPhoneOtpHasher` reusing the SHA256 pattern; 6-digit numeric generator.
- **Phone verification endpoints** (authenticated as the staff member setting their own
  phone, in the admin panel):
  - `POST /api/auth/staff/phone/start-verification { phone }` → upsert pending phone,
    generate OTP, `SendNowAsync` SMS, return `{ expiresInSeconds, resendAfterSeconds }`.
  - `POST /api/auth/staff/phone/confirm { code }` → verify (≤3 attempts, TTL), set
    `Phone/NormalizedPhone/PhoneVerifiedAtUtc`, enforce global uniqueness.
- **Sign-in by phone (password):** `POST /api/auth/staff/sign-in-by-phone
  { phoneNumber, password }` → resolve staff by `NormalizedPhone` among **verified +
  active**; verify password (existing `PasswordHasher`); issue tokens via
  `OpaqueStaffTokenService`. Returns `StaffSignInResponse`. Mirrors `SignInByLoginAsync`.
- **Permission:** add `StaffPermissionNames.InstallDevice = "devices.install"`; grant in
  `PermissionCatalog` to `owner`, `branch_manager`, `technician`.
- **Admin panel UI:** staff profile/settings gains a Phone field + verify flow
  (start → enter code → confirmed badge). Follows existing form/i18n patterns.
- **Deliverable:** staff can set + SMS-verify a phone and sign in by phone+password.

### Phase C — Wizard login by phone + password (the requested feature)
- **Authenticated install endpoints** that resolve org/staff from `StaffContext` instead
  of an owner code, gated by `devices.install`:
  - `POST /api/install/auth/discover` → branches + floor maps the staff may install in
    (their org; scoped to `StaffContext.BranchIds`, or all org branches for `owner`).
  - `POST /api/install/auth/seats` → create seat (require `devices.install` on branch).
  - `POST /api/install/auth/enroll` → enroll device; org/branch validated against
    `StaffContext`; reuse the `EfInstallService` enrollment core (extract the post-resolution
    logic so both owner-code and authenticated paths share it).
  - These require a valid staff Bearer token (the public owner-code endpoints stay as-is).
- **Contracts:** `StaffPhoneSignInRequest(PhoneNumber, Password)` (reuses
  `StaffSignInResponse`); authenticated install request DTOs drop `OwnerCode`.
- **Native bridge:** `SetupWizardWebHostBridge` gains `wizard:phoneSignIn`; after success
  it holds the `AccessToken` and attaches it as Bearer on `wizard:discover/createSeat/enroll`
  (authenticated variants). `ISetupWizardApiClient` gains `PhoneSignInAsync` + authenticated
  discover/seats/enroll. Preview fakes updated (fake token + same fake data).
- **Frontend:** new `PhoneLoginScreen` (phone + password) becomes the first step; small
  "Войти по коду владельца" link routes to the existing `OwnerCodeScreen` (fallback path,
  unchanged). After sign-in, continue branch → role → device → enroll exactly as today.
  Tighten PC display name to 3–32 chars. i18n strings (ru/en/tg) via the single locale
  source.
- **Errors (concrete, rule #34):** "номер не привязан/не подтверждён", "неверный пароль",
  "SMS-сервис недоступен → войдите по коду", payom `401` → log + degrade to code fallback.
- **Deliverable:** install a PC by phone+password; owner code still works as fallback.

### Phase D — SMS password reset (optional, can follow A)
- `POST /api/auth/staff/forgot-password-by-phone { phoneNumber }` → OTP (Purpose=
  PasswordReset) via SMS (don't leak existence; rate-limited).
- `POST /api/auth/staff/reset-password-by-phone { phoneNumber, code, newPassword }` →
  verify OTP, set password (reuse `EfStaffPasswordResetService` core). Email reset stays.

## 6. Data model summary

- `staff_users`: + `Phone`, `NormalizedPhone`, `PhoneVerifiedAtUtc`; global unique partial
  index on `NormalizedPhone` (verified + active).
- new `staff_phone_otps`: short-lived hashed OTPs (verification + reset), per §5 Phase B.
- Migrations: one for staff columns/index, one for the OTP table.

## 7. Security & rate limiting

- OTP: 6 digits, 5-min TTL, ≤3 verify attempts, resend cooldown 60s, cap sends per phone
  per hour. Stored **hashed** (never plaintext), single-use (`ConsumedAtUtc`).
- `start-verification` / `forgot-password-by-phone` throttled per source IP (reuse
  `IInstallRequestThrottle`-style throttle) and per phone.
- Phone sign-in: standard password-attempt protections as existing sign-in.
- payom `ApiToken` is a secret (env only). Token expiry (`401`) surfaces as "SMS service
  unavailable" + fallback to code; logged for ops.
- Only **verified** phones can sign in or receive resets.

## 8. Configuration

```jsonc
"Sms": {
  "BaseUrl": "https://gateway.payom.tj",
  "ApiToken": "",          // from env on staging/prod
  "SenderName": "AFK4.NET",
  "TimeoutSeconds": 15
}
```
Per-environment via `appsettings.{env}.json` + env vars, mirroring `Notifications`.

## 9. Edge cases (scale / i18n — rules #28, #34)

- Phone formats: accept `+992…`, spaces, dashes; normalize to digits. Reject non-E.164.
- Duplicate phone across orgs: blocked by global unique verified index; second attempt
  gets a clear error and uses the code fallback.
- Owner with multiple orgs: needs distinct phones per staff account (documented limitation).
- SMS undelivered / payom down: explicit error + code fallback; never a dead end.
- Long display names / unicode PC names: validated to 3–32; trimmed.
- i18n: all new copy via the single locale source (ru/en/tg), no hardcoded strings.

## 10. Testing

- Unit: `PayomSmsTransport` response→ChannelResult mapping (201/401/403/422/5xx/timeout);
  OTP hasher/generator; OTP verify (TTL, attempts, single-use); phone normalization;
  sign-in-by-phone (verified/active/uniqueness, wrong password).
- Integration: phone verification round-trip with a fake `ISmsTransport`; authenticated
  enroll resolves org from `StaffContext` + permission gate; owner-code path unchanged.
- Frontend (bun test): PhoneLoginScreen validation + fallback link; token threaded into
  enroll; preview fakes.
- Manual: wizard preview end-to-end on phone path + code fallback.

## 11. Out of scope (YAGNI)

- No SMS OTP on each PC install (password auth only).
- No removal of the owner code (kept as fallback; may deprecate later).
- No 2FA on wizard login.
- No flash-call/Telegram verification (SmartShell has them; payom is SMS — revisit later
  if cost matters).

## 12. Assumptions

- Each staff account that installs PCs has its own verified phone + password.
- `Technician` is the closest role to SmartShell's "IT-Administrator" and should get
  `devices.install` alongside `owner`/`branch_manager`.
- Phone is a **global** login identifier (unlike username, which is per-org) — hence the
  global unique verified index.
