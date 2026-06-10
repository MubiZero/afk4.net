# dcgate: per-owner Telegram credentials + session sharing — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make dcgate hosted Telegram attach use per-owner `api_id`/`api_hash` and share one Telegram session per bank account across many cards.

**Architecture:** Replace the project-scoped `TelegramSession` with an account-scoped `TelegramAccount` (keyed by phone). Many `Project`s link to one account. Attach accepts the owner's creds and short-circuits to `attached` when the phone is already authorized. The listener runs one gramjs client per account and routes each notification to the correct project by the unique payment `comment`.

**Tech Stack:** NestJS, Prisma (PostgreSQL), gramjs (`telegram`), Vitest. Repo: `/home/fedya/projects/dcgate`. Package manager: pnpm. Tests run from `apps/api`: `pnpm exec vitest run <path>`.

**Spec:** `afk4.net/docs/superpowers/specs/2026-06-08-per-owner-telegram-credentials-session-sharing-design.md`

> All paths below are relative to `/home/fedya/projects/dcgate`. Create a branch first: `git checkout -b feature/per-owner-telegram-session-sharing`.

---

### Task 1: Prisma schema — `TelegramAccount`, project link, account-level messages

**Files:**
- Modify: `prisma/schema.prisma`
- Create (generated): `prisma/migrations/<timestamp>_telegram_account_session_sharing/migration.sql`

The production `TelegramSession` table is empty (attach never succeeded), so no data backfill is required.

- [ ] **Step 1: Edit `schema.prisma`** — remove `model TelegramSession`, add `model TelegramAccount`, and edit `Project` + `TelegramMessage`.

Add:

```prisma
model TelegramAccount {
  id               String            @id @default(cuid())
  phone            String            @unique
  encryptedSession String
  encryptedApiId   String
  encryptedApiHash String
  healthStatus     String            @default("offline")
  lastError        String?
  lastConnectedAt  DateTime?
  createdAt        DateTime          @default(now())
  updatedAt        DateTime          @updatedAt
  projects         Project[]
  telegramMessages TelegramMessage[]
}
```

In `model Project`: remove `telegramSessions TelegramSession[]` and add:

```prisma
  telegramAccountId String?
  telegramAccount   TelegramAccount? @relation(fields: [telegramAccountId], references: [id])
```

Replace `model TelegramMessage` with (note nullable `projectId`, new `telegramAccountId`, swapped unique index):

```prisma
model TelegramMessage {
  id                String              @id @default(cuid())
  telegramAccountId String
  projectId         String?
  chatId            String
  messageId         String
  rawText           String
  parseStatus       TelegramParseStatus @default(PENDING)
  parsedAmount      Decimal?            @db.Decimal(12, 2)
  parsedCardLast4   String?
  parsedComment     String?
  parsedSender      String?
  operationDate     DateTime?
  parseError        String?
  receivedAt        DateTime
  createdAt         DateTime            @default(now())
  telegramAccount   TelegramAccount     @relation(fields: [telegramAccountId], references: [id], onDelete: Cascade)
  project           Project?            @relation(fields: [projectId], references: [id], onDelete: SetNull)
  payment           Payment?

  @@unique([telegramAccountId, chatId, messageId])
  @@index([telegramAccountId, parseStatus])
  @@index([telegramAccountId, parsedComment])
}
```

- [ ] **Step 2: Generate the migration**

Run (from `apps/api` or repo root, matching how migrations are normally created here):
`pnpm exec prisma migrate dev --name telegram_account_session_sharing --schema ../../prisma/schema.prisma`
Expected: a new folder under `prisma/migrations/` with `DROP TABLE "TelegramSession"`, `CREATE TABLE "TelegramAccount"`, `ALTER TABLE "Project" ADD COLUMN "telegramAccountId"`, and the `TelegramMessage` changes. Prisma client regenerates.

- [ ] **Step 3: Verify the build compiles against the new client**

Run: `pnpm --filter @dcgate/api build` (or `pnpm -r build`)
Expected: TypeScript errors ONLY in files that referenced `telegramSession` (attach service, listener, messages service, admin service, scripts). These are fixed in later tasks. If there are errors elsewhere, stop and reassess.

- [ ] **Step 4: Commit**

```bash
git add prisma/schema.prisma prisma/migrations
git commit -m "feat(dcgate): TelegramAccount model for per-account session sharing"
```

---

### Task 2: `PendingTelegramLoginStore` carries phone + api credentials

**Files:**
- Modify: `apps/api/src/telegram-attach/pending-telegram-login.store.ts`
- Test: `apps/api/src/telegram-attach/pending-telegram-login.store.test.ts`

- [ ] **Step 1: Extend the test** — add fields to the stored login and assert they round-trip through `get`/`take`.

```typescript
it("preserves phone and api credentials through get and take", () => {
  const store = new PendingTelegramLoginStore();
  const client = makeFakeClient(); // existing helper in this test file
  store.put("attempt-1", { projectId: "p1", phone: "+992900000000", apiId: 123, apiHash: "hash", client });

  expect(store.get("attempt-1")).toMatchObject({ projectId: "p1", phone: "+992900000000", apiId: 123, apiHash: "hash" });
  expect(store.take("attempt-1")).toMatchObject({ phone: "+992900000000", apiId: 123, apiHash: "hash" });
  expect(store.get("attempt-1")).toBeUndefined();
});
```

- [ ] **Step 2: Run the test, expect FAIL**

Run (from `apps/api`): `pnpm exec vitest run src/telegram-attach/pending-telegram-login.store.test.ts`
Expected: FAIL (type/assert error — fields not carried).

- [ ] **Step 3: Implement** — add `phone`, `apiId`, `apiHash` to `PendingTelegramLogin`, and include them in the objects returned by `get` and `take`.

```typescript
export interface PendingTelegramLogin {
  projectId: string;
  phone: string;
  apiId: number;
  apiHash: string;
  client: TelegramLoginClient;
}
```

In `get` and `take`, return the full set:
```typescript
return stored
  ? { projectId: stored.projectId, phone: stored.phone, apiId: stored.apiId, apiHash: stored.apiHash, client: stored.client }
  : undefined;
```

- [ ] **Step 4: Run the test, expect PASS**

Run: `pnpm exec vitest run src/telegram-attach/pending-telegram-login.store.test.ts`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/telegram-attach/pending-telegram-login.store.ts apps/api/src/telegram-attach/pending-telegram-login.store.test.ts
git commit -m "feat(dcgate): pending login store carries phone + api credentials"
```

---

### Task 3: `TelegramLoginClientFactory.create(apiId, apiHash)`

**Files:**
- Modify: `apps/api/src/telegram-attach/telegram-login-client.factory.ts`
- Test: `apps/api/src/telegram-attach/telegram-login-client.factory.test.ts` (create if absent)

- [ ] **Step 1: Write/extend the test**

```typescript
import { describe, expect, it } from "vitest";
import { TelegramLoginClientFactory } from "./telegram-login-client.factory";
import { GramjsTelegramLoginClient } from "./gramjs-telegram-login-client";

describe("TelegramLoginClientFactory", () => {
  it("builds a client from the supplied credentials, not env", () => {
    delete process.env.TELEGRAM_API_ID;
    delete process.env.TELEGRAM_API_HASH;
    const client = new TelegramLoginClientFactory().create(123456, "abc-hash");
    expect(client).toBeInstanceOf(GramjsTelegramLoginClient);
  });
});
```

- [ ] **Step 2: Run, expect FAIL** — `pnpm exec vitest run src/telegram-attach/telegram-login-client.factory.test.ts` (FAIL: `create` takes no args / throws on missing env).

- [ ] **Step 3: Implement**

```typescript
import { Injectable } from "@nestjs/common";
import { GramjsTelegramLoginClient } from "./gramjs-telegram-login-client";
import type { TelegramLoginClient } from "./telegram-login-client";

@Injectable()
export class TelegramLoginClientFactory {
  create(apiId: number, apiHash: string): TelegramLoginClient {
    return new GramjsTelegramLoginClient(apiId, apiHash);
  }
}
```

- [ ] **Step 4: Run, expect PASS.**

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/telegram-attach/telegram-login-client.factory.*
git commit -m "feat(dcgate): factory builds login client from supplied credentials"
```

---

### Task 4: `TelegramAttachService.start` — accept creds + short-circuit already-attached

**Files:**
- Modify: `apps/api/src/telegram-attach/telegram-attach.service.ts`
- Test: `apps/api/src/telegram-attach/telegram-attach.service.test.ts`

Service `start` signature becomes `start(projectId, phone, apiId?, apiHash?)`. Behaviour:
- project missing → `NotFoundException`.
- a `TelegramAccount` with this `phone` already exists → set `project.telegramAccountId`, return `{ state: "attached" }` (no `loginAttemptId`).
- otherwise: require `apiId` + `apiHash` (throw `BadRequestException` if missing), `factory.create(apiId, apiHash)`, `startLogin(phone)`, store `{ projectId, phone, apiId, apiHash, client }`, return `{ loginAttemptId, state: "code_required" }`.

Update `AttachStartResult` to a union:
```typescript
export type AttachStartResult =
  | { loginAttemptId: string; state: "code_required" }
  | { state: "attached" };
```

- [ ] **Step 1: Write failing tests** — add cases to the existing test file. The test harness already fakes `prisma`, `factory`, `pendingLogins`. Extend the fake prisma to include `telegramAccount.findUnique` and `project.update`.

```typescript
it("links the project and returns attached when the phone is already attached", async () => {
  const { service, prisma } = createService({ project: { id: "p1" } });
  prisma.telegramAccount.findUnique.mockResolvedValue({ id: "acc1", phone: "+992900000000" });

  const result = await service.start("p1", "+992900000000");

  expect(result).toEqual({ state: "attached" });
  expect(prisma.project.update).toHaveBeenCalledWith({
    where: { id: "p1" },
    data: { telegramAccountId: "acc1" },
  });
});

it("starts an OTP login with the supplied creds when the phone is new", async () => {
  const client = new FakeTelegramLoginClient();
  const { service, prisma, factory } = createService({ client, project: { id: "p1" } });
  prisma.telegramAccount.findUnique.mockResolvedValue(null);

  const result = await service.start("p1", "+992900000000", 123, "hash");

  expect(factory.create).toHaveBeenCalledWith(123, "hash");
  expect(client.startLogin).toHaveBeenCalledWith("+992900000000");
  expect(result).toMatchObject({ state: "code_required" });
});

it("rejects a new phone without credentials", async () => {
  const { service, prisma } = createService({ project: { id: "p1" } });
  prisma.telegramAccount.findUnique.mockResolvedValue(null);

  await expect(service.start("p1", "+992900000000")).rejects.toThrow(/api/i);
});
```

(Update `createService` so `factory.create` is a `vi.fn` returning the fake client, and the fake `prisma` exposes `telegramAccount.findUnique` and `project.update` as `vi.fn`s.)

- [ ] **Step 2: Run, expect FAIL** — `pnpm exec vitest run src/telegram-attach/telegram-attach.service.test.ts`.

- [ ] **Step 3: Implement `start`**

```typescript
import { BadRequestException, Injectable, NotFoundException } from "@nestjs/common";
// ...
async start(projectId: string, phone: string, apiId?: number, apiHash?: string): Promise<AttachStartResult> {
  const project = await this.prisma.project.findUnique({ where: { id: projectId } });
  if (!project) {
    throw new NotFoundException("Project not found");
  }

  const existingAccount = await this.prisma.telegramAccount.findUnique({ where: { phone } });
  if (existingAccount) {
    await this.prisma.project.update({
      where: { id: projectId },
      data: { telegramAccountId: existingAccount.id },
    });
    return { state: "attached" };
  }

  if (!apiId || !apiHash) {
    throw new BadRequestException("api_id and api_hash are required to attach a new Telegram account");
  }

  const client = this.loginClientFactory.create(apiId, apiHash);
  try {
    await client.startLogin(phone);
  } catch (error: unknown) {
    await client.disconnect();
    throw error;
  }

  const loginAttemptId = randomUUID();
  this.pendingLogins.put(loginAttemptId, { projectId, phone, apiId, apiHash, client });
  return { loginAttemptId, state: "code_required" };
}
```

Remove the `readTelegramAttachApiCredentials` import (it is no longer used by `start`; `finishAttach` is updated in Task 5).

- [ ] **Step 4: Run, expect PASS.**

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/telegram-attach/telegram-attach.service.ts apps/api/src/telegram-attach/telegram-attach.service.test.ts
git commit -m "feat(dcgate): attach start accepts creds and short-circuits attached phones"
```

---

### Task 5: `finishAttach` upserts `TelegramAccount` by phone + links the project

**Files:**
- Modify: `apps/api/src/telegram-attach/telegram-attach.service.ts`
- Test: `apps/api/src/telegram-attach/telegram-attach.service.test.ts`

- [ ] **Step 1: Write failing test** — verify the account is upserted by phone with the pending creds (not env) and the project is linked.

```typescript
it("persists the account by phone with the supplied creds and links the project", async () => {
  const client = new FakeTelegramLoginClient();
  client.codeOutcome = { kind: "attached" };
  const { service, prisma, factory } = createService({ client, project: { id: "p1" } });
  prisma.telegramAccount.findUnique.mockResolvedValue(null);
  prisma.telegramAccount.upsert.mockResolvedValue({ id: "acc1", phone: "+992900000000" });

  const start = await service.start("p1", "+992900000000", 123, "hash");
  const attemptId = (start as { loginAttemptId: string }).loginAttemptId;
  const result = await service.verifyCode(attemptId, "00000");

  expect(result).toEqual({ loginAttemptId: attemptId, state: "attached" });
  const upsertArg = prisma.telegramAccount.upsert.mock.calls[0][0];
  expect(upsertArg.where).toEqual({ phone: "+992900000000" });
  // encrypted via the fake SecretEncryptionService: enc("123"), enc("hash"), enc("SAVED_SESSION_STRING")
  expect(upsertArg.create).toMatchObject({ phone: "+992900000000", healthStatus: "configured" });
  expect(prisma.project.update).toHaveBeenCalledWith({
    where: { id: "p1" },
    data: { telegramAccountId: "acc1" },
  });
});
```

(Extend the fake prisma with `telegramAccount.upsert` as a `vi.fn`. The fake `SecretEncryptionService.encryptText` can return `enc:${input}` so assertions are deterministic.)

- [ ] **Step 2: Run, expect FAIL.**

- [ ] **Step 3: Implement `finishAttach`** — pull creds from the pending entry, upsert the account by phone, link the project.

```typescript
private async finishAttach(
  loginAttemptId: string,
  pending: PendingTelegramLogin,
): Promise<AttachStepResult> {
  const session = pending.client.saveSession();
  const data = {
    encryptedSession: this.secretEncryption.encryptText(session),
    encryptedApiId: this.secretEncryption.encryptText(String(pending.apiId)),
    encryptedApiHash: this.secretEncryption.encryptText(pending.apiHash),
    healthStatus: "configured",
    lastError: null as string | null,
  };

  const account = await this.prisma.telegramAccount.upsert({
    where: { phone: pending.phone },
    create: { ...data, phone: pending.phone },
    update: data,
  });

  await this.prisma.project.update({
    where: { id: pending.projectId },
    data: { telegramAccountId: account.id },
  });

  await this.dropAttempt(loginAttemptId, pending.client);
  return { loginAttemptId, state: "attached" };
}
```

Update the `requirePending` return type / call sites so `finishAttach` receives the full `PendingTelegramLogin` (with `phone`, `apiId`, `apiHash`). Delete the now-unused `readTelegramAttachApiCredentials` import.

- [ ] **Step 4: Run, expect PASS.** Also run the whole attach folder: `pnpm exec vitest run src/telegram-attach`.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/telegram-attach/telegram-attach.service.ts apps/api/src/telegram-attach/telegram-attach.service.test.ts
git commit -m "feat(dcgate): finishAttach upserts account by phone and links project"
```

---

### Task 6: Start DTO + controller — optional `apiId`/`apiHash`

**Files:**
- Modify: `apps/api/src/telegram-attach/dto/telegram-attach.dto.ts`
- Modify: `apps/api/src/telegram-attach/telegram-attach.controller.ts`
- Test: `apps/api/src/telegram-attach/telegram-attach.controller.test.ts` (if present; else extend service test)

- [ ] **Step 1: Edit `TelegramAttachStartDto`**

```typescript
import { IsInt, IsNotEmpty, IsOptional, IsPositive, IsString, MaxLength } from "class-validator";

export class TelegramAttachStartDto {
  @IsString()
  @IsNotEmpty()
  @MaxLength(32)
  phone!: string;

  @IsOptional()
  @IsInt()
  @IsPositive()
  apiId?: number;

  @IsOptional()
  @IsString()
  @IsNotEmpty()
  @MaxLength(64)
  apiHash?: string;
}
```

- [ ] **Step 2: Edit the controller `start`**

```typescript
@Post(":projectId/telegram-session/start")
start(@Param("projectId") projectId: string, @Body() body: TelegramAttachStartDto) {
  return this.attachService.start(projectId, body.phone, body.apiId, body.apiHash);
}
```

- [ ] **Step 3: Build to verify wiring**

Run: `pnpm --filter @dcgate/api build`
Expected: PASS (the attach module compiles).

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/telegram-attach/dto/telegram-attach.dto.ts apps/api/src/telegram-attach/telegram-attach.controller.ts
git commit -m "feat(dcgate): start DTO accepts optional api_id/api_hash"
```

---

### Task 7: Listener — one client per `TelegramAccount`

**Files:**
- Modify: `apps/api/src/telegram-messages/telegram-listener.service.ts`
- Test: `apps/api/src/telegram-messages/telegram-listener.service.test.ts` (create if absent)

- [ ] **Step 1: Write failing test** — `startConfiguredSessions` loads accounts (each with ≥1 ENABLED project) and starts one client per account; the message handler calls ingestion with the **account**, not a project.

```typescript
it("starts one client per account and routes messages to account-scoped ingestion", async () => {
  const prisma = makeFakePrisma();
  prisma.telegramAccount.findMany.mockResolvedValue([
    { id: "acc1", encryptedApiId: "enc:123", encryptedApiHash: "enc:hash", encryptedSession: "enc:sess" },
  ]);
  const messages = { ingestTelegramMessage: vi.fn() };
  const service = new TelegramListenerService(prisma, fakeSecretEncryption, messages as any);
  // inject a fake TelegramClient (see existing test seam) that reports authorized and replays a message
  await service.startConfiguredSessions();
  // simulate a NewMessage event -> expect messages.ingestTelegramMessage called with { id: "acc1" }-shaped account
});
```

(If the listener currently has no test seam for `TelegramClient`, introduce a minimal injectable client factory or guard the gramjs construction behind an overridable protected method so the test can stub `connect`/`checkAuthorization`/`addEventHandler`. Keep the seam small.)

- [ ] **Step 2: Run, expect FAIL.**

- [ ] **Step 3: Implement** — query accounts, start one client each, bind the handler to the account.

```typescript
async startConfiguredSessions(): Promise<void> {
  const accounts = await this.prisma.telegramAccount.findMany({
    where: { projects: { some: { status: "ENABLED" } } },
  });
  for (const account of accounts) {
    await this.startSession(account);
  }
}

private async startSession(account: TelegramAccount): Promise<void> {
  try {
    const apiId = this.decryptApiId(account.encryptedApiId);
    const apiHash = this.secretEncryptionService.decryptText(account.encryptedApiHash);
    const session = this.secretEncryptionService.decryptText(account.encryptedSession);
    const client = new TelegramClient(new StringSession(session), apiId, apiHash, { connectionRetries: 5 });
    await client.connect();
    if (!(await client.checkAuthorization())) {
      throw new Error("Telegram session is not authorized");
    }
    client.addEventHandler(
      (event) => {
        void this.handleNewMessage(account.id, event).catch((error: unknown) =>
          this.logListenerError(account.id, error),
        );
      },
      new NewMessage({ chats: [readTelegramSourceChat()], incoming: true }),
    );
    this.clients.push(client);
    await this.prisma.telegramAccount.update({
      where: { id: account.id },
      data: { healthStatus: "online", lastConnectedAt: new Date(), lastError: null },
    });
  } catch (error: unknown) {
    await this.markAccountOffline(account.id, error);
  }
}

private async handleNewMessage(accountId: string, event: NewMessageEvent): Promise<void> {
  const rawText = event.message.message?.trim();
  if (!rawText) return;
  await this.telegramMessagesService.ingestTelegramMessage(accountId, {
    chatId: event.chatId?.toString() ?? readTelegramSourceChat(),
    messageId: event.message.id.toString(),
    rawText,
    receivedAt: new Date(event.message.date * 1000).toISOString(),
  });
}
```

Rename `markSessionOffline` → `markAccountOffline` (updates `telegramAccount` by id). Drop the old `StartTelegramSessionInput` interface and `project` plumbing.

- [ ] **Step 4: Run, expect PASS.**

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/telegram-messages/telegram-listener.service.*
git commit -m "feat(dcgate): listener runs one client per Telegram account"
```

---

### Task 8: Ingestion — resolve project by `comment` across the account's projects

**Files:**
- Modify: `apps/api/src/telegram-messages/telegram-messages.service.ts`
- Test: `apps/api/src/telegram-messages/telegram-messages.service.test.ts`

`ingestTelegramMessage(accountId, input)`: dedup by `(telegramAccountId, chatId, messageId)`; parse; resolve the matching pending `Payment` by `comment` **among projects on this account**; that payment's project is the target; reconcile (existing amount/`cardLast4`/webhook logic). Unmatched / parse-failed messages persist with `telegramAccountId` and `projectId = null`.

- [ ] **Step 1: Write failing tests**

```typescript
it("credits the correct project on a shared account by comment", async () => {
  // two ENABLED projects on acc1: cardLast4 1111 (proj A) and 2222 (proj B)
  // a pending payment comment "ref-B" belongs to proj B, amount 50.00, cardLast4 2222
  // a notification: amount 50.00, cardLast4 2222, comment "ref-B"
  const result = await service.ingestTelegramMessage("acc1", notification);
  expect(result.reconciliationStatus).toBe("paid");
  expect(result.matchedPaymentId).toBe("pay-B");
  // proj A untouched
});

it("stores an unmatched message at the account level with null project", async () => {
  const result = await service.ingestTelegramMessage("acc1", notificationWithUnknownComment);
  expect(result.reconciliationStatus).toBe("no_match");
  // created telegramMessage has telegramAccountId acc1, projectId null
});

it("stores a parse-failed message at the account level", async () => {
  const result = await service.ingestTelegramMessage("acc1", { ...input, rawText: "garbage" });
  expect(result.reconciliationStatus).toBe("parse_failed");
});

it("dedups by account + chat + message id", async () => {
  // existing message for (acc1, chat, msg) -> returns "duplicate"
});
```

- [ ] **Step 2: Run, expect FAIL.**

- [ ] **Step 3: Implement** — change the signature and matching.

```typescript
async ingestTelegramMessage(accountId: string, input: IngestTelegramMessageDto): Promise<TelegramMessageResponseDto> {
  const existing = await this.prisma.telegramMessage.findUnique({
    where: { telegramAccountId_chatId_messageId: { telegramAccountId: accountId, chatId: input.chatId, messageId: input.messageId } },
    include: { payment: true },
  });
  if (existing) {
    return this.toResponse(existing, { matchedPaymentId: existing.payment?.id, status: "duplicate" });
  }

  const parsed = this.tryParseNotification(input.rawText);
  if (!parsed.parsed) {
    const msg = await this.createTelegramMessage(accountId, null, input, parsed);
    return this.toResponse(msg, { status: "parse_failed" });
  }

  // Resolve the project via the unique payment comment among this account's projects.
  const payment = await this.prisma.payment.findFirst({
    where: {
      comment: parsed.value.comment,
      status: { in: ["CREATED", "PENDING"] },
      project: { telegramAccountId: accountId, status: "ENABLED" },
    },
    include: { project: true },
  });

  if (!payment) {
    const msg = await this.createTelegramMessage(accountId, null, input, parsed);
    return this.toResponse(msg, { status: "no_match" });
  }

  const msg = await this.createTelegramMessage(accountId, payment.projectId, input, parsed);
  const decision = decideTelegramPaymentMatch({
    notificationAmount: parsed.value.amount,
    notificationCardLast4: parsed.value.cardLast4,
    paymentAmount: payment.amount.toFixed(2),
    projectCardLast4: payment.project.cardLast4,
  });
  await this.applyPaymentMatchDecision(payment, msg.id, decision.status);
  await this.webhookEventsService.createPaymentStatusEvent(payment.id);
  return this.toResponse(msg, { matchedPaymentId: payment.id, status: decision.status });
}
```

Change `createTelegramMessage(accountId, projectId | null, input, parsed)` to write `telegramAccountId` + nullable `projectId`. Remove the `AuthenticatedProject` parameter usage from this service.

> Note: `PaymentStatus` enum literals are `CREATED`/`PENDING` as used in the original `reconcileParsedMessage` — keep them.

- [ ] **Step 4: Run, expect PASS.** Then run the whole messages folder: `pnpm exec vitest run src/telegram-messages`.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/telegram-messages/telegram-messages.service.*
git commit -m "feat(dcgate): ingest resolves project by comment across shared account"
```

---

### Task 9: Admin surface + scripts referencing `TelegramSession`

**Files:**
- Modify: `apps/api/src/admin/admin.service.ts` (lines ~91, ~237–257)
- Modify: `apps/api/src/admin/admin.controller.ts` (if it surfaces session shape)
- Modify/remove: `apps/api/src/scripts/create-telegram-session.ts`, `apps/api/src/scripts/attach-telegram-session.ts`, `apps/api/src/scripts/backfill-telegram-messages.ts`
- Test: existing admin tests if present

- [ ] **Step 1: Update admin queries** — replace `prisma.telegramSession` with `prisma.telegramAccount`. For the per-project status (the `GET /api/admin/projects/:id/status` shape), derive health from the project's linked account:

```typescript
// in the project status query
const project = await this.prisma.project.findUnique({
  where: { id: projectId },
  include: { telegramAccount: { select: { healthStatus: true, lastConnectedAt: true } } },
});
const account = project?.telegramAccount;
return {
  sessionHealth: account?.healthStatus ?? "offline",
  lastConnectedAt: account?.lastConnectedAt ?? null,
  // ...existing message counts unchanged
};
```

And the offline count: `this.prisma.telegramAccount.count({ where: { healthStatus: "offline" } })`.

- [ ] **Step 2: Fix the scripts** — update `create-telegram-session.ts` / `attach-telegram-session.ts` to write `telegramAccount` (or delete them if unused operationally; prefer deleting `attach-telegram-session.ts` since the hosted attach replaces it). `backfill-telegram-messages.ts`: set `telegramAccountId` instead of `projectId`-only.

- [ ] **Step 3: Build the whole API**

Run: `pnpm --filter @dcgate/api build`
Expected: PASS, zero TypeScript errors across the repo.

- [ ] **Step 4: Run the full API test suite**

Run (from `apps/api`): `pnpm exec vitest run`
Expected: all green.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/admin apps/api/src/scripts
git commit -m "feat(dcgate): admin + scripts read TelegramAccount"
```

---

### Task 10: Full verification + push

- [ ] **Step 1:** `pnpm -r build` → PASS.
- [ ] **Step 2:** `pnpm -r test` → all green.
- [ ] **Step 3:** `pnpm exec prisma validate --schema ../../prisma/schema.prisma` (from `apps/api`) → schema valid.
- [ ] **Step 4: Push the branch and open a PR.**

```bash
git push -u origin feature/per-owner-telegram-session-sharing
```

> Deploy note (post-merge): Prisma migrations auto-apply on dcgate deploy via `docker/api-entrypoint.sh`. After deploy, the existing prod gateway's project (`cmq50ockc0000nw01ltbhfegp`) is still `pending_telegram` on the afk4 side — it gets attached once Subsystem B (the afk4 plan) ships and the owner enters real creds.

---

## Self-Review notes

- **Spec coverage:** TelegramAccount model (T1), creds through pending (T2), factory (T3), start short-circuit + creds (T4), finishAttach upsert-by-phone (T5), DTO/controller (T6), one-client-per-account listener (T7), comment-based routing + account-level messages (T8), admin/scripts (T9). All spec sections for Subsystem C are covered.
- **Type consistency:** `AttachStartResult` union (`code_required` | `attached`), `ingestTelegramMessage(accountId, input)`, `PendingTelegramLogin` with `phone/apiId/apiHash` — used consistently across T2/T4/T5/T7/T8.
- **No global env:** attach no longer imports `readTelegramAttachApiCredentials` (T4/T5).
