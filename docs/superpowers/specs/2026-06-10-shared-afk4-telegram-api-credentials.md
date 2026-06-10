# Shared AFK4 Telegram app credentials — design (reverses per-owner creds)

**Date:** 2026-06-10
**Supersedes the credential-sourcing part of:**
`2026-06-08-per-owner-telegram-credentials-session-sharing-design.md`
**Repos:** `afk4.net` (Subsystem B + cabinet UI) and `dcgate` (Subsystem C)

## Why we reversed the 2026-06-08 decision

The 2026-06-08 design made each owner register their own Telegram **app**
(`api_id`/`api_hash`) at my.telegram.org and supply it at attach time, to avoid a
single point of failure and Telegram anti-abuse exposure on one shared `api_id`.

In practice that barrier is too high: club owners are non-technical and registering
a Telegram application is a hard blocker for adoption. We accept the trade-off and
move to **one shared AFK4 application** for all hosted MTProto logins.

**Known risk (accepted):** a ban on the shared `api_id` would break payment
confirmation for all clubs at once. Mitigations: keep the creds in dcgate's secret
env (rotatable without code), and each bank account still authorizes its **own**
Telegram session via its own OTP — only the app identity is shared.

## What changed (afk4.net — done)

- `TelegramStartRequest` no longer carries `api_id`/`api_hash` — phone only.
- `IDcGateAdminClient.StartTelegramAsync` / `DcGateAdminClient` drop the creds
  params; the `telegram-session/start` body is `{ phone }`.
- Removed the per-owner store: `OrganizationTelegramApiCredentialEntity`, its DbSet,
  the model config, and the `GET .../telegram-credentials` lookup endpoint.
- EF migration `DropOrganizationTelegramApiCredentials` drops the table (apply via
  the staging/prod runbook — afk4 does not auto-migrate).
- Cabinet UI: the attach form asks only for the phone; api_id/api_hash inputs,
  the "saved credentials" hint, and the related i18n keys are gone.

## What dcgate must do (Subsystem C — separate repo, NOT in this change)

The attach path must source `api_id`/`api_hash` from **dcgate's own env** again
(the shared AFK4 app), since afk4 no longer sends them:

1. Add/restore env: `TELEGRAM_API_ID`, `TELEGRAM_API_HASH` (the AFK4 app).
2. `TelegramAttachStartDto`: `apiId`/`apiHash` become unused inputs — read the env
   instead. `TelegramAttachService.start` no longer requires them on a new account;
   `TelegramLoginClientFactory.create()` reads env (revert the 2026-06-08 change
   that dropped the env read).
3. `finishAttach`: persist the env creds into `TelegramAccount.encryptedApiId/Hash`
   (or store a constant) — the per-account session model is unchanged; every account
   simply shares the same app identity.
4. Tests: attach a new account with **no** creds in the request body; assert the env
   creds drive the login.

Account-scoped sessions, the listener, ingestion, and `comment`-based matching are
all unchanged.
