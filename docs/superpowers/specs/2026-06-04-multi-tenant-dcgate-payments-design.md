# Multi-Tenant dcgate Payments — Design

> Status: approved design (brainstorm 2026-06-04). Decomposed into three independently-plannable
> subsystems **A → B → C**, each gets its own implementation plan and PR cycle.

## Problem

Customer-shell **Unit 2** shipped real online wallet top-up via the dcgate DC-Bank gateway, but it
assumed **a single global dcgate project** for all of AFK4 (`DcGate:ApiKey` / `DcGate:WebhookSecret`
in app config). AFK4 is a **multi-tenant SaaS**: each club owner wants payments on **their own card**,
and may want **a different card per branch (club)**. In dcgate, **one project = one card**, so a single
global project cannot serve multiple owners/branches.

A second, deeper reality drives the design: dcgate confirms a payment only when a **Telegram account
that has the DC-Bank bot** receives the bank's deposit message for that card. The card must live in a
bank account whose phone number is logged into that Telegram account. Therefore **different cards on
different bank accounts mean different Telegram accounts**, and each requires its **own interactive
Telegram login** (phone + real-time OTP, possibly a 2FA cloud password). This cannot be done headlessly
by AFK4 — it is inherently a human, interactive step performed by whoever controls that phone.

## Verified ground truth (dcgate source `MubiZero/dcgate@main`, read 2026-06-04)

- **Project model** (`prisma/schema.prisma`): `Project { id, name, status, encryptedCardNumber,
  cardLast4, cardFingerprint, webhookUrl?, encryptedWebhookSecret?, paymentExpiresInMinutes,
  apiKeys[], telegramSessions[], ... }`. Secrets/cards encrypted at rest (AES-256-GCM). API keys
  stored as SHA-256 `keyHash` (plaintext shown once at creation).
- **Project provisioning API**: `POST /api/admin/projects` (guarded by `ADMIN_JWT_SECRET`, header
  `x-admin-secret` or `Authorization: Bearer`). Body `{ name, cardNumber, paymentExpiresInMinutes,
  webhookUrl?, webhookSecret?, apiKeyLabel? }` → returns `{ id, name, status, paymentExpiresInMinutes,
  webhookUrl?, cardLast4, apiKey }` (apiKey plaintext once). A CLI `create-project` exists but does
  **not** set a webhook secret — the admin HTTP API does, so AFK4 uses the HTTP API.
- **Consumer payment API**: `POST /api/payments` (Bearer apiKey) body `{ amount: "major.units string",
  externalOrderId, metadata? }` → `{ paymentId, status, amount, currency, comment, expiresAt, payUrl }`.
  **Currency is not a body field** (per-project, default TJS) — confirms Unit 2's decision to omit it.
- **Webhook signature** (`webhooks/webhook-payload.ts`): header `x-dcgate-signature: sha256=<hmac-sha256
  hex, lowercase>` over the **raw JSON body**, keyed by the **project's webhook secret**. Payload
  `{ eventId, eventType, projectId, payment:{ id, status, amount, currency, comment, externalOrderId,
  paidAt? } }`; headers `x-dcgate-event-id/type/project-id`. Event types `payment.paid` /
  `payment.expired` / `payment.disputed`. **This matches AFK4's Unit 2 webhook verifier exactly** —
  the verification code does not change; only **which secret** it uses does.
- **Telegram model**: `TelegramSession { projectId, encryptedSession, encryptedApiId, encryptedApiHash,
  healthStatus ("offline"/...), lastConnectedAt, ... }` is **per project** (`Project.telegramSessions`,
  one-to-many). `TelegramMessage` is per project, parsed into `parsedAmount/parsedCardLast4/
  parsedComment` and matched to a `Payment` by comment. So different cards/bank-accounts map cleanly to
  separate per-project sessions.
- **Session attach is interactive** (`scripts/create-telegram-session.ts`): prompts for phone and the
  Telegram login **code** (and supports a hidden 2FA password prompt) via readline; needs Telegram
  `apiId`/`apiHash`. Produces a session string. Not headless-automatable.
- **Deployment**: dcgate runs on Coolify at `https://dcgate.mubi.dev` (container port 3001 → 443);
  health `GET /api/health/{live,ready}`. AFK4 staging API is `https://afk4.staging.mubi.dev`
  (webhook URL `https://afk4.staging.mubi.dev/api/public/payments/dcgate/webhook`).

## Chosen approach

**Per-branch dcgate credentials with org-level fallback (Approach 1).** Each branch (or the org as a
fallback) maps to its own dcgate project; AFK4 stores that project's `apiKey` **and** `webhookSecret`
encrypted at rest. Rejected alternatives: a single platform-wide webhook secret (weaker — one leak
forges webhooks for every tenant) and delegated dcgate tokens (large dcgate redesign, out of scope).
Per-tenant secret isolation is the correct posture for money movement, and we must store the per-branch
`apiKey` for outbound calls anyway, so co-locating the secret costs little.

---

## Subsystem A — Multi-tenant routing core (AFK4)

The foundational correctness fix. Replaces Unit 2's global gateway config. Buildable and testable on
its own with one manually-provisioned dcgate project.

### Data model

New entity `BranchPaymentGatewayEntity` (table `branch_payment_gateways`) — one row per dcgate project
(= one card):

| Field | Notes |
|---|---|
| `BranchPaymentGatewayId` (Guid, PK) | |
| `OrganizationId` (Guid) | tenant scope |
| `BranchId` (Guid?, nullable) | `null` ⇒ org-level gateway (fallback for branches without their own) |
| `DcgateProjectId` (string, **unique**) | resolves inbound webhooks by `x-dcgate-project-id` |
| `ApiKeyEncrypted` (string) | dcgate apiKey, encrypted at rest |
| `WebhookSecretEncrypted` (string) | dcgate per-project webhook secret, encrypted at rest |
| `CardLast4` (string) | display only; full card lives in dcgate |
| `Status` (string) | `pending_telegram` / `active` / `disabled` |
| `CreatedAtUtc`, `UpdatedAtUtc` | |

Indices: unique on `DcgateProjectId`; index on `(OrganizationId, BranchId)`. A partial-unique rule
ensures at most one **active** gateway per `(OrganizationId, BranchId)` and one org-level
(`BranchId == null`) per org.

### Secret protection

New server-side `ISecretProtector` (`AesGcmSecretProtector`): AES-256-GCM, key from config
`Secrets:EncryptionKeyBase64` (32-byte base64; same shape as dcgate's `ENCRYPTION_KEY_BASE64`).
Output is versioned (`v1:<nonce>:<ciphertext>:<tag>` base64 parts) so the key can be rotated later.
This is the **only** place gateway secrets are encrypted/decrypted. Missing/short key ⇒ fail fast at
startup (the online-payment feature is disabled rather than running with unprotected secrets).

### Resolution

`IBranchPaymentGatewayResolver.ResolveForBranchAsync(orgId, branchId)`:
1. active row with `BranchId == branchId` → use it;
2. else active row with `BranchId == null` for the org → use it (fallback);
3. else `null` ⇒ branch has no online payment.

`ResolveByProjectIdAsync(dcgateProjectId)` for inbound webhooks → the owning row (any status; a
`disabled` row still verifies a late webhook so in-flight payments aren't dropped).

### Endpoint changes (replace Unit 2 global config)

- **Outbound** `POST /api/me/wallet/top-up-intent` (dcgate branch): resolve gateway by the intent's
  `BranchId`; if none or not `active` → **409/422 `online_payment_unavailable`** (the counter path is
  unaffected). Decrypt its apiKey; build a dcgate client bound to that apiKey + the **platform**
  `DcGate:BaseUrl`. The minor→major conversion and `externalOrderId = PaymentIntentId "N"` stay as-is.
- **Inbound** `POST /api/public/payments/dcgate/webhook`: read `x-dcgate-project-id` (fall back to
  `payload.projectId`), `ResolveByProjectIdAsync` → decrypt that row's webhook secret → verify the HMAC
  with it. Unknown project ⇒ 401. Everything else (idempotency, credit via `TopUpWalletAsync` with the
  shared reason + intent-id key, state transitions, 503-on-no-shift) is unchanged from Unit 2.
- **Config**: drop global `DcGate:ApiKey` / `DcGate:WebhookSecret`. Keep platform-level `DcGate:BaseUrl`
  and add `DcGate:AdminSecret` (used by Subsystem B) + `Secrets:EncryptionKeyBase64`. `appsettings.json`
  holds empty placeholders only; real values via environment.

### Dcgate client factory

Replace Unit 2's single typed `IDcGateClient` singleton with `IDcGateClientFactory.CreateForApiKey(apiKey)`
that returns a client bound to a given apiKey (sharing one pooled `HttpClient`/`HttpMessageHandler` for
the platform base URL — per-call apiKey set on the request, not a new handler per call).

---

## Subsystem B — Owner cabinet card onboarding (AFK4)

A new **"Приём платежей" / Payment cards** section in `AFK4.Operator.App.Web`, gated to the `Owner`
role via a new permission in `PermissionCatalog`. The owner is the authentic actor: they connect the
card that receives their money.

### Flow (two phases per card)

**Phase 1 — provision project (automatic).** Owner enters a card number and picks scope (whole network
= org-level, or a specific branch). AFK4 backend (holding `DcGate:AdminSecret`) calls dcgate
`POST /api/admin/projects` with `{ name (e.g. "AFK4 / <org> / <branch>"), cardNumber,
webhookUrl = <this env>/api/public/payments/dcgate/webhook, webhookSecret = freshly generated,
paymentExpiresInMinutes }`. On success it stores a `BranchPaymentGatewayEntity` (`Status =
pending_telegram`, apiKey + secret encrypted, `CardLast4`). The full card number is never persisted in
AFK4 — it flows through to dcgate and AFK4 keeps only `CardLast4`.

**Phase 2 — attach Telegram (interactive, in-cabinet).** Owner enters the **phone of the bank account**
that holds the card. AFK4 proxies dcgate's hosted attach flow (Subsystem C): start → owner receives the
code on that phone → enters **code** (and **2FA password** if prompted) in the cabinet → on success
AFK4 flips `Status = active`.

### Surfaced state & gating

Each card row shows: `CardLast4`, scope (org/branch), Telegram status (online/offline from dcgate
status), last confirmation message time, and a clear "needs Telegram attach" badge while
`pending_telegram`. **Online top-up is offered to players only when the resolved gateway is `active` and
its Telegram session is online** — otherwise a player could pay and never be credited.

### New AFK4 endpoints (Owner-gated, operator API)

- `GET /api/owner/payment-gateways` — list the org's gateways (+ branches) with status.
- `POST /api/owner/payment-gateways` — Phase 1 (provision). Body `{ branchId?, cardNumber }`.
- `POST /api/owner/payment-gateways/{id}/telegram/start` — `{ phone }`.
- `POST /api/owner/payment-gateways/{id}/telegram/verify-code` — `{ code }`.
- `POST /api/owner/payment-gateways/{id}/telegram/verify-password` — `{ password }`.
- `GET  /api/owner/payment-gateways/{id}/status` — proxies dcgate status (cached briefly).

AFK4 holds the dcgate admin secret server-side; the owner never sees it. Card number and Telegram
credentials are relayed, not logged.

---

## Subsystem C — dcgate improvements (`MubiZero/dcgate`, separate PRs)

1. **Hosted Telegram-session attach** (the key enabler) — a stateful API over the existing interactive
   login, guarded by `ADMIN_JWT_SECRET`:
   - `POST /api/admin/projects/{id}/telegram-session/start` `{ phone }` → calls Telegram `sendCode`,
     holds a pending login (live `TelegramClient`) in memory keyed by `loginAttemptId` with a short TTL,
     returns `{ loginAttemptId, state: "code_required" }`. Telegram `apiId`/`apiHash` are **platform-level**
     (one Telegram application for the whole dcgate instance), not per account.
   - `POST .../telegram-session/verify-code` `{ loginAttemptId, code }` → `state: "attached"` or
     `"password_required"`.
   - `POST .../telegram-session/verify-password` `{ loginAttemptId, password }` → `"attached"`; encrypts
     and persists the session, sets `healthStatus = online`.
   - TTL expiry / wrong code / wrong password return explicit error states.
2. **Project status** — `GET /api/admin/projects/{id}/status` → `{ sessionHealth, lastConnectedAt,
   lastMessageAt, telegramMessagesCount }`, so AFK4 can gate and display.
3. **Resilience niceties** (so automation doesn't stumble): auto-generate `webhookSecret` when omitted;
   idempotent project creation keyed by an `externalId`; clearer 4xx validation messages.

---

## Error handling

- Missing/short `Secrets:EncryptionKeyBase64` → startup failure; online-payment endpoints disabled.
- dcgate admin/create call fails → Phase 1 returns the dcgate error to the owner; no gateway row is
  persisted (no half-provisioned state).
- Telegram start/verify failures (bad code, 2FA needed, TTL expired) → surfaced verbatim to the cabinet;
  the gateway stays `pending_telegram`.
- Player picks dcgate top-up on a branch whose gateway is missing/`pending_telegram`/Telegram-offline →
  `online_payment_unavailable`; the shell falls back to the counter path.
- Inbound webhook for an unknown/disabled project → 401 (unknown) / still-verified (disabled, to credit
  in-flight payments). Unchanged Unit 2 idempotency, double-credit, and 503-redeliver guarantees.

## Testing

- **A**: entity round-trip + migration; `AesGcmSecretProtector` round-trip + tamper-detection;
  resolver (branch hit, org fallback, none); outbound picks the right apiKey; webhook resolves secret by
  projectId and rejects unknown; `online_payment_unavailable` gating; the cross-path single-credit
  invariant from Unit 2 still holds.
- **B**: Phase-1 provision happy path (mocked dcgate admin client) persists encrypted creds + `CardLast4`;
  Owner-role authz (non-owner → 403); status/gating reflected; Telegram verify state transitions
  (against a faked dcgate client).
- **C**: dcgate's own test suite — attach state machine (code, 2FA, TTL, wrong inputs), status endpoint,
  idempotent create.

## Decomposition & sequencing

1. **A** — routing core. Unblocks correct multi-tenant behavior; testable with one hand-provisioned
   project. **Build first.**
2. **B** — owner cabinet onboarding (depends on A's data model and on C's attach API for Phase 2; Phase 1
   can land against the existing dcgate admin create endpoint before C ships).
3. **C** — dcgate hosted attach + status + niceties (separate repo/PRs; B's Phase-2 UX depends on it).

Each subsystem gets its own implementation plan via the writing-plans skill, starting with A.

## Out of scope / deferred

- Migrating Unit 2's single global project (staging has no real AFK4 project yet — nothing to migrate;
  the global config is simply removed).
- Multiple cards per single branch, payouts/settlement, refunds UI, non-TJS currencies.
- Rotating an already-stored encryption key (the versioned envelope leaves room; no rotation tooling now).
- Fully unattended Telegram attach (inherently interactive; the hosted flow is the closest we get).
