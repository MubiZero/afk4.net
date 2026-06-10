# Per-owner Telegram API credentials + session sharing — design

**Date:** 2026-06-08
**Repos:** `dcgate` (Subsystem C) and `afk4.net` (Subsystem B + Operator cabinet UI)
**Follows:** `2026-06-04-multi-tenant-dcgate-payments-design.md`

## Problem

Hosted Telegram attach (dcgate) requires Telegram **application** credentials
(`api_id` / `api_hash`) to drive the gramjs MTProto login. The original design
never addressed where these come from in a multi-tenant world. In production
dcgate they were left as empty env placeholders, so `telegram-session/start`
fails with `TELEGRAM_API_ID is required for hosted Telegram attach` (HTTP 500),
which surfaced during staging e2e.

Using a **single global** `api_id`/`api_hash` to host MTProto logins for many
unrelated club owners' accounts is a single point of failure and against
Telegram's anti-abuse rules: a ban on the shared `api_id` would break payment
confirmation for **all** clubs at once. The correct model is **per-owner**
credentials: each owner uses an app they registered (at my.telegram.org) under
the same Telegram account that receives their bank's deposit notifications.

### Domain model (clarified)

- The bank's Telegram bot sends deposit notifications to the **Telegram account
  whose phone equals the bank account's registered phone**.
- One **bank account** can hold **many cards**; all their notifications arrive
  in the **same** Telegram account.
- One owner can have **several** bank accounts → **several** Telegram accounts
  (different phones), each with its own notification stream.

Therefore the natural unit for credentials **and** for a Telegram session is the
**bank account = Telegram phone**, not the organization and not the card.

## What already exists (and works)

- dcgate `TelegramSession` already stores `encryptedApiId` / `encryptedApiHash`
  per record — the schema anticipated per-credential storage.
- The dcgate **listener** already reads `api_id`/`api_hash` from each stored
  session (`decryptApiId`) and connects per-session — it never used the global
  env.
- Payment reconciliation matches a notification to a payment by **`comment`**
  (the unique per-payment reference; afk4 uses `PaymentIntentId`), with
  amount + `cardLast4` as secondary validation
  (`telegram-messages.service.ts` → `decideTelegramPaymentMatch`).

The only place still bound to global env / a single project is the **attach
entry path** and the **session↔project 1:1 coupling**.

## Goals

1. Attach uses the **owner's** `api_id`/`api_hash`, supplied at the attach step.
2. afk4 stores credentials keyed by **`(organization, phone)`** and reuses them
   for subsequent cards on the same phone (owner enters them once per Telegram
   account).
3. **Session sharing:** one Telegram session (one gramjs client) per bank
   account; multiple cards (dcgate projects) share it. A second card on an
   already-attached phone links instantly — **no second OTP**.
4. No regression in payment matching/crediting.

## Non-goals (YAGNI)

- Global `api_id`/`api_hash` fallback (removed from the attach path).
- Live listener start for a brand-new account without a worker restart (a
  brand-new bank account still needs the worker to pick up its session, as
  today; linking a new card to an **already-running** account needs no restart).
- Cross-org credential sharing.

---

## Subsystem C — dcgate (NestJS + Prisma + gramjs)

### Data model changes

Replace the project-scoped `TelegramSession` with an account-scoped
`TelegramAccount`. The production `TelegramSession` table is empty (attach never
succeeded), so the migration carries no live rows.

```prisma
model TelegramAccount {
  id               String    @id @default(cuid())
  phone            String    @unique
  encryptedSession String
  encryptedApiId   String
  encryptedApiHash String
  healthStatus     String    @default("offline") // offline | configured | online
  lastError        String?
  lastConnectedAt  DateTime?
  createdAt        DateTime  @default(now())
  updatedAt        DateTime  @updatedAt
  projects         Project[]
  telegramMessages TelegramMessage[]
}

model Project {
  // ...existing fields...
  telegramAccountId String?
  telegramAccount   TelegramAccount? @relation(fields: [telegramAccountId], references: [id])
  // remove: telegramSessions TelegramSession[]
}

model TelegramMessage {
  // ...existing fields...
  telegramAccountId String
  telegramAccount   TelegramAccount @relation(fields: [telegramAccountId], references: [id])
  projectId         String?         // now nullable: unmatched messages have account, no project
  // unique index changes from (projectId, chatId, messageId)
  //                        to (telegramAccountId, chatId, messageId)
}
```

Migration: drop `TelegramSession`; add `TelegramAccount`; add
`Project.telegramAccountId`; add `TelegramMessage.telegramAccountId`, make
`TelegramMessage.projectId` nullable, swap the dedup unique index.

### Attach state machine

`TelegramAttachStartDto` gains:

- `apiId: number` — `@IsInt @IsPositive` (optional at the controller level;
  required only when the account is new — see service logic).
- `apiHash: string` — `@IsString @IsNotEmpty @MaxLength(64)` (optional likewise).

`TelegramAttachService.start(projectId, phone, apiId?, apiHash?)`:

1. Load the project (404 if missing).
2. Look up `TelegramAccount` by `phone`.
3. **Already attached** (account exists and has an authorized session): set
   `project.telegramAccountId = account.id`, return `{ state: "attached" }`.
   No OTP, no creds needed.
4. **New account:** require `apiId` + `apiHash` (400 if absent);
   `factory.create(apiId, apiHash)` → `client.startLogin(phone)`; store
   `{ phone, apiId, apiHash, projectId, client }` in `PendingTelegramLoginStore`;
   return `{ loginAttemptId, state: "code_required" }`.

`verifyCode` / `verifyPassword`: unchanged flow; on success call `finishAttach`.

`finishAttach`: upsert `TelegramAccount` **by `phone`** with the pending
`encryptedSession` (from `client.saveSession()`) + `encryptedApiId/Hash` (from
the pending entry, **not** env), `healthStatus = "configured"`; set
`project.telegramAccountId = account.id`; return `{ state: "attached" }`.

`TelegramLoginClientFactory.create(apiId, apiHash)`: drop the env read; build
`GramjsTelegramLoginClient(apiId, apiHash)`.

`PendingTelegramLoginStore` entry gains `apiId`, `apiHash`, `phone`.

The `readTelegramAttachApiCredentials` env helper is no longer used by attach.
It stays only if still referenced by the `create-telegram-session.ts` admin
script; the attach path stops importing it. Global `TELEGRAM_API_ID/HASH` env
vars become unnecessary at runtime for attach.

### Listener (one client per account)

`startConfiguredSessions`: load `TelegramAccount`s that have ≥1 `ENABLED`
project; start **one** `TelegramClient` per account.

Message handler (per account): hand the raw message to ingestion **scoped to the
account** (not a fixed project). Because matching is resolved per message from
the DB, **linking a new project to an already-running account needs no
restart**.

### Ingestion service

`ingestTelegramMessage(account, input)`:

1. Dedup by `(telegramAccountId, chatId, messageId)`.
2. Parse (`parseDcBankNotification`).
3. If parsed: find a pending `Payment` (`status in CREATED|PENDING`) by
   `comment == parsed.comment` among projects where
   `project.telegramAccountId = account.id`. If found → that payment's project is
   the target; run the existing `decideTelegramPaymentMatch` (amount +
   `cardLast4`), apply decision, emit webhook; store the message with
   `telegramAccountId` + resolved `projectId`. If not found → store with
   `telegramAccountId`, `projectId = null`, status `no_match`.
4. If parse fails: store with `telegramAccountId`, `projectId = null`,
   `parse_failed`.

`reconcileParsedMessage` keeps the existing amount/`cardLast4`/webhook logic; its
project now comes from the comment lookup rather than a fixed handler binding.

### Admin surface / scripts

Update `admin.service.ts` / `admin.controller.ts` `telegram-sessions` queries to
read `TelegramAccount`. Update or remove `create-telegram-session.ts` /
`attach-telegram-session.ts` scripts to the new model.

### Tests (dcgate)

- Attach service/factory/controller: pass `apiId`/`apiHash`; assert the
  **supplied** creds are persisted (not env); assert the **already-attached
  short-circuit** links the project and returns `attached` with no OTP.
- Ingestion: a message on a shared account credits the **correct** project by
  comment; non-matching projects on the same account are untouched; unmatched
  and parse-failed messages persist at the account level.
- Listener: one client per account; a newly linked project on a running account
  is credited without restart.

---

## Subsystem B — afk4.net (`AFK4.Platform.Api`)

### Credential storage (new)

Table `organization_telegram_api_credentials`:

- `Id` (Guid, PK)
- `OrganizationId` (Guid)
- `PhoneNumber` (string, normalized)
- `ApiIdEncrypted` (string)
- `ApiHashEncrypted` (string)
- `CreatedAtUtc`, `UpdatedAtUtc`
- Unique index `(OrganizationId, PhoneNumber)`

Encrypted with the existing `ISecretProtector` (`Secrets:EncryptionKeyBase64`,
already set on staging). EF migration required (afk4 does **not** auto-migrate;
apply via the staging/prod runbook before deploying the API).

### Contract / DTO changes

- `TelegramStartRequest` (`Shared.Contracts/Payments`): add
  `long? ApiId`, `string? ApiHash` (both optional). `Phone` stays required.
- `TelegramStartResponse`: `LoginAttemptId` becomes **nullable** (absent when
  the account is already attached); `State` carries `attached` | `code_required`
  (the `password_required` state only ever comes back from `verify-code`).
- `IDcGateAdminClient.StartTelegramAsync` + `DcGateAdminClient`: add
  `apiId`/`apiHash` params; body `{ phone, apiId, apiHash }`. Handle the
  `attached`-without-`loginAttemptId` response shape.

### Endpoint logic

`POST /api/owner/payment-gateways/{id}/telegram/start`:

1. Resolve gateway (owner-scoped) as today.
2. Normalize `Phone`.
3. Credentials for `(orgId, phone)`:
   - request has `ApiId` + `ApiHash` → validate (positive / non-empty) →
     **upsert** the encrypted row.
   - else → load the stored row; if none → `400 telegram_api_credentials_required`.
4. Decrypt creds; call dcgate `start(projectId, phone, apiId, apiHash)`.
5. Map the dcgate response:
   - `attached` → flip gateway `pending_telegram → active`; return
     `{ State = "attached" }` (no `LoginAttemptId`).
   - `code_required` → return `{ LoginAttemptId, State }`.

afk4 never logs `api_hash` and never returns it to the client.

`GET /api/owner/payment-gateways/telegram-credentials?phone=...` (owner-gated):
returns `{ hasCredentials: bool, apiId?: number }` for the entered phone — never
`api_hash`. Used by the cabinet to prefill / decide whether to ask.

`verify-code` / `verify-password`: unchanged (the pending login on dcgate holds
the client + creds).

### Tests (afk4)

- start: provide creds → stored + forwarded; omit with stored creds → reused;
  omit with none → 400; `attached` response → gateway active without OTP.
- credentials lookup endpoint returns `hasCredentials`/`apiId`, never `apiHash`.
- `DcGateAdminClient` sends the new body fields and parses both response shapes.

---

## UI — `AFK4.Operator.App.Web` (React)

`PaymentGatewaysWorkspace.tsx`, attach phase:

1. Owner enters the **phone** of the Telegram account that receives this card's
   bank notifications.
2. Query `telegram-credentials?phone=` → `hasCredentials`:
   - **yes:** show "Используются сохранённые ключи (api_id …)" + a "Изменить"
     toggle that reveals the inputs to override.
   - **no:** show required `api_id` (number) + `api_hash` (sensitive) inputs and
     a help link to my.telegram.org explaining how to obtain them.
3. Submit `start` with `{ phone, apiId?, apiHash? }`.
4. On response `attached` → skip the OTP step, show the card active. On
   `code_required` → show the OTP step (then `password_required` if 2FA).

`operatorApiClients.ts`: `paymentGateways.startTelegram` gains
`apiId`/`apiHash`; add `getTelegramCredentials(phone)`. i18n `payments_cards.*`
keys (ru / en / tg = ru copy): labels, help text, validation errors.
Update `PaymentGatewaysWorkspace.test.tsx`.

---

## End-to-end data flow

Cabinet (phone, + api_id/api_hash if new) → afk4 `/telegram/start` → afk4
resolves/stores `(org, phone)` creds → dcgate `start(projectId, phone, apiId,
apiHash)`:

- **phone already attached:** dcgate links the project → `attached` → afk4
  marks the gateway active → cabinet done (no OTP).
- **new phone:** gramjs `startLogin` → OTP → `verify-code` (→ `verify-password`
  if 2FA) → `finishAttach` creates the `TelegramAccount` + links the project →
  `attached`. The listener picks up the new account on the next worker restart;
  thereafter notifications for any of its cards are matched by `comment` and
  credited.

## Security

`api_hash` is a secret: encrypted at rest in **both** afk4 (new table, via
`ISecretProtector`) and dcgate (existing `encryptedApiHash`). Never logged,
never returned to any client. `api_id` may be shown back for confirmation;
`api_hash` never is.

## Rollout

1. Ship dcgate (C): schema migration (Prisma auto-applies on deploy) + attach +
   listener + ingestion + admin/scripts + tests.
2. Apply the afk4 EF migration via the manual runbook (staging, then prod).
3. Ship afk4 (B) + cabinet UI.
4. Resume staging e2e on the existing gateway `93eda272` (dcgate project
   `cmq50ockc0000nw01ltbhfegp`, `pending_telegram`): owner enters real
   `api_id`/`api_hash` + phone + OTP; restart the dcgate worker so the new
   account goes `online`; player `top-up-intent` (dcgate) → pay → webhook →
   wallet credited.

## Backlog

- Live listener start for a brand-new account without a worker restart.
