# Notification / Messaging Backbone (Track 2)

- **Date:** 2026-06-01
- **Status:** Design (best-practice defaults baked in; forks flagged **Proposed decision:** for founder review)
- **Scope owner:** Platform (AFK4)
- **Related:** [[platform-web-redesign]], counter loop (2026-06-01-platform-counter-loop-postpaid-checkout-design.md), anti-fraud (2026-06-01-platform-anti-fraud-controls-design.md), customer portal (2026-06-01-platform-customer-portal-design.md), customer shell (2026-06-01-platform-customer-shell-experience-design.md), localization (2026-06-01-platform-localization-design.md)

## 1. Context & Problem

There is **no email, SMS, or notification system anywhere in the codebase today.** A repository-wide
search for `smtp`, `mailkit`, `sendgrid`, `IEmailSender`, `MailMessage`, `NotificationService`, etc.
returns **zero `.cs` hits** — the only matches are docs and roadmap notes. The product can authenticate,
bill, and run sessions, but it cannot *talk to anyone*. The founder already has a configured SMTP/email
server standing by; this spec designs the backbone that wires it in and makes every dependent feature a
thin caller.

This is a **foundational enabler**. Many flows are blocked or degraded precisely because there is no way
to send a message. Verified today:

- **Staff creation is manual + out-of-band.** `POST /api/branches/{branchId}/staff` takes
  `CreateStaffUserRequest(OrganizationId, UserName, DisplayName, Password, RoleNames)` —
  the **password is set inline by the creator** and communicated by hand. No invite link, no email.
  (`src/AFK4.Platform.Api/Program.cs:2440`, `src/AFK4.Shared.Contracts/Identity/CreateStaffUserRequest.cs`)
- **Owner invites are GUID/code copy-paste.** `TenantOwnerInvitesSection.tsx` creates an invite, reveals
  a raw `code`, and the admin copies it out of band. There is **no email send and no resend** — the only
  recovery is *revoke + recreate* (rotate). Accept is code-based via `POST /api/platform/owner-invites/accept`.
  (`src/AFK4.Platform.Web/src/platform/tenants/TenantOwnerInvitesSection.tsx`, `Program.cs:1092`)
- **No password reset anywhere.** The only reset is admin-driven: `ResetStaffUserPasswordRequest(OrganizationId, NewPassword)`
  — an operator with permission *sets* a new password (`Program.cs:2899`). There is **no self-service
  "forgot password"** for staff, owners, or players (no `forgot`/`reset-password` token flow exists).
- **Invoices are generated silently.** `InvoiceGenerationHostedService` ticks and issues invoices via
  `IInvoiceGenerationRunner.RunAsync`, logging a count — **no email to the owner**, no "issued / paid /
  overdue" messaging. (`src/AFK4.Platform.Api/Platform/Billing/InvoiceGenerationHostedService.cs`)
- **No low-stock alerts.** The inventory backend exists (`IInventoryService`, `InventoryStockDto`) but
  `InventoryStockDto(ProductId, ProductName, Sku, TrackStock, StockOnHand)` has **no reorder threshold**
  and nothing watches stock to warn anyone.
- **No scheduled report delivery.** Reports are export/pull only (`ReportCsvExporter`, report endpoints);
  nothing pushes a report on a schedule.
- **Anti-fraud wants a daily owner summary email** (shift-discrepancy + daily digest) — impossible today.
- **No queue/outbox infra.** The codebase has polling `BackgroundService`s (`InvoiceGenerationHostedService`,
  `BillingPlanSeedHostedService`, `PlatformAdminBootstrapHostedService`) but **no durable outbox, no
  `Channel<T>` task queue, no retry/idempotency envelope** for outbound side effects.
- **Recipients have almost no contact data.** `PlayerAccountEntity.PhoneNumber` is nullable; **players
  have no email**; **staff and owners are username-based with no email field at all**
  (no `Email`/`ContactEmail` on any entity under `Data/`). Adding contact fields is part of this work.

This design delivers a **NotificationService** abstraction with pluggable channels (email-over-SMTP
first; SMS and in-app later), localized templates, a durable **delivery outbox** with retry +
idempotency, and **recipient preferences / opt-out** — then enumerates the trigger integrations it
unblocks, each as a thin caller. It makes explicit which other specs depend on this one.

## 2. Goals

1. **`INotificationService` abstraction** — one call site shape (`SendAsync(notification)`) that all
   features use; channels are pluggable behind `INotificationChannel`.
2. **Channels:** **email via the existing SMTP first**; **SMS** and **in-app** as later plug-ins with no
   call-site change.
3. **Localized templates** — every message resolves subject/body by `(templateKey, locale)`, coordinated
   with the localization spec; falls back to a default locale.
4. **Durable delivery outbox** — every send is persisted first, dispatched by a background worker, with
   **retry/backoff** and **idempotency** (no duplicate sends on retry or double-trigger).
5. **Recipient preferences / opt-out** — per recipient + per category, with a hard line between
   **transactional** (always sent: OTP, password reset, invoice) and **marketing/digest** (opt-out honoured).
6. **Thin trigger integrations** — staff invite, owner invite + resend, password reset, player OTP,
   invoice issued/paid/overdue dunning, low-stock, shift-discrepancy + daily owner summary, scheduled
   reports — each a small caller, not bespoke plumbing.

### Non-goals (explicitly deferred or owned elsewhere)

- **Push (mobile/web) notifications and rich in-app inbox UI** — `InApp` channel is stubbed structurally
  but the inbox surface is a customer-portal/shell concern (Track 3).
- **A real SMS provider integration** — the SMS *channel seam* is designed; wiring a vendor (Twilio,
  local aggregator) is a follow-up (§10). OTP ships on email first (D7).
- **The dependent features' own business logic** — e.g. invoice generation, shift close, reorder math.
  This spec adds the *messaging* those features call; their domain logic lives in their own specs.
- **Marketing campaign tooling / segmentation** — digests and dunning are transactional/operational only.
- **Localization message catalogue itself** — owned by the localization spec; this spec consumes it.

These are referenced where they border this work but are **not** implemented here.

## 3. Decisions

Locked best-practice defaults plus the explicit forks the founder should confirm.

| # | Decision | Choice |
|---|----------|--------|
| D1 | Call-site shape | **Single `INotificationService.SendAsync(NotificationRequest)`**; features never touch channels, templates, or SMTP directly. |
| D2 | Outbox-first | **Every send is persisted to a `NotificationOutbox` row before any channel is touched** — the row is the unit of retry/idempotency/audit. |
| D3 | Idempotency | **Caller-supplied `IdempotencyKey`** (e.g. `invoice-issued:{invoiceId}`); a duplicate key is a no-op that returns the existing row. |
| D4 | Transactional vs opt-out | **Two-tier categories.** `Transactional` (OTP, password reset, invite, invoice) ignore opt-out; `Operational/Digest` (low-stock, daily summary, scheduled report) honour preferences. |
| D5 | Recipient model | **`NotificationRecipient` value (channel + address + locale)**, resolved from staff/owner/player records; **add `Email` to staff/owner identities and keep player `PhoneNumber`** (+ optional player email). |
| D6 | Templating | **Server-side render of `(templateKey, locale) → {subject, body-text, body-html}`** with token substitution; rendering is pure and unit-testable, decoupled from delivery. |
| **D7** | **OTP before SMS exists** | **Proposed decision: email OTP first.** Player auth OTP (portal/shell) sends a code by **email** initially; the same `SendAsync` swaps to the SMS channel later with no caller change. If a player has only a phone and no email, that account can't receive email OTP until SMS lands — see §7. *(Recommend; founder to confirm email-first OTP is acceptable for the pilot.)* |
| **D8** | **Provider abstraction** | **Proposed decision: SMTP-first, pluggable.** `INotificationChannel` with an `Email` (MailKit/SMTP) implementation now; `Sms` and `InApp` register later behind the same seam. Channel selection is per-notification (`PreferredChannels` with fallback order). *(Recommend.)* |
| **D9** | **Template storage** | **Proposed decision: file/code-based templates now, DB-backed later.** Ship templates as **embedded resource files** (one folder per locale) loaded by an `ITemplateProvider`; a future DB-backed provider implements the same interface for owner-editable templates. Rationale: pilot has a fixed message set, versioned with the code, no admin-editing requirement yet. *(Recommend; founder may prefer DB if owners must edit copy early.)* |
| **D10** | **Synchronous vs queued** | **Proposed decision: queued outbox (async).** Callers persist + return immediately; a background dispatcher sends. SMTP latency/outages never block a checkout, invite, or invoice tick. A thin **synchronous "send now and await result"** convenience exists only for OTP/password-reset where the user is waiting (still written to the outbox first). *(Recommend queued.)* |
| D11 | Failure policy | **Retry with capped exponential backoff** (e.g. 1m, 5m, 30m, 2h, 6h), max N attempts, then `Failed` + surfaced for ops. Transactional failures are alertable. |
| D12 | Localization source | **Locale resolved per recipient** (owner/staff/branch default; player preference), consistent with the localization spec; missing locale falls back to the configured default. |

## 4. Architecture Overview

The backbone sits between feature triggers and the outside world. Backend remains the single authority;
delivery is decoupled from the triggering transaction via the outbox (consistent with current patterns).

```
 Feature triggers (thin callers)                Backbone                         Outside world
  staff-invite ─┐
  owner-invite/resend ─┐                                                      ┌─▶ SMTP server (email) ✅ now
  password-reset ──────┤   SendAsync(req)   ┌───────────────────┐            │
  player OTP ──────────┼───────────────────▶│ NotificationService│           ├─▶ SMS provider (later)
  invoice issued/paid/ │   (idempotency key) │  resolve recipient │           │
   overdue dunning ────┤                     │  + locale + prefs  │           └─▶ In-app inbox (later)
  low-stock alert ─────┤                     │  render template   │                     ▲
  shift-discrepancy ───┤                     │  WRITE outbox row ──┼──┐                  │
  daily owner summary ─┤                     └───────────────────┘  │ (persisted)       │
  scheduled report ────┘                                            ▼                    │
                                              ┌──────────────────────────────┐           │
                          NotificationOutbox ─│ NotificationDispatcher (hosted)│─ channel ┘
                          (Pending/Sent/Failed)│  poll → pick channel → send  │  by PreferredChannels
                                              │  retry/backoff, mark result   │  + recipient prefs
                                              └──────────────────────────────┘
```

Components, each independently testable:

1. **Contracts & `INotificationService`** — `NotificationRequest`, `NotificationRecipient`, categories,
   channel/preference enums; the façade that resolves + renders + persists.
2. **Template subsystem** — `ITemplateProvider` + `INotificationRenderer` (pure render of key+locale+tokens).
3. **Outbox + dispatcher** — `NotificationOutboxEntity`, `INotificationOutbox`, hosted
   `NotificationDispatcher` with retry/backoff/idempotency.
4. **Channels** — `INotificationChannel` + `SmtpEmailChannel` (now); `SmsChannel`/`InAppChannel` (seams).
5. **Preferences** — `NotificationPreferenceEntity` + resolution (transactional bypass).
6. **Trigger integrations** — thin callers in each feature (§7).

## 5. Components

### 5.1 Contracts & `INotificationService`

```csharp
public interface INotificationService
{
    // Queued: persist to outbox, return the row id; dispatcher delivers.
    Task<NotificationHandle> SendAsync(NotificationRequest request, CancellationToken ct);

    // Convenience for user-waiting flows (OTP, reset): still outbox-first, then awaits first attempt.
    Task<NotificationDeliveryResult> SendNowAsync(NotificationRequest request, CancellationToken ct);
}

public sealed record NotificationRequest(
    string TemplateKey,                         // e.g. "staff.invite", "invoice.overdue"
    NotificationCategory Category,              // Transactional | Operational | Digest
    NotificationRecipient Recipient,
    IReadOnlyDictionary<string, string> Tokens, // template substitutions (already-localized values)
    string IdempotencyKey,                      // e.g. "invoice-issued:{invoiceId}"
    IReadOnlyList<NotificationChannel>? PreferredChannels = null, // fallback order; default per category
    Guid? OrganizationId = null,
    Guid? BranchId = null);

public sealed record NotificationRecipient(
    string Locale,                              // BCP-47, resolved upstream
    string? EmailAddress = null,
    string? PhoneNumber = null,
    Guid? StaffUserId = null,                   // for in-app + audit linkage
    Guid? PlayerAccountId = null);

public enum NotificationCategory { Transactional, Operational, Digest }
public enum NotificationChannel  { Email, Sms, InApp }
```

`SendAsync` resolves the recipient's effective locale (D12) and preferences (D4), renders the template
(§5.2), and writes one `NotificationOutbox` row per chosen channel, **idempotent on `IdempotencyKey`**.
It never blocks on the network. Money/dates inside tokens are formatted **at the caller** using the
existing minor→major / culture conventions (consistent with the rest of the platform), so the renderer
stays presentation-pure.

### 5.2 Template subsystem

- `ITemplateProvider.Get(templateKey, locale)` → `NotificationTemplate { Subject, BodyText, BodyHtml }`,
  with **default-locale fallback** when a locale is missing.
- `INotificationRenderer.Render(template, tokens)` does **safe token substitution** (`{{playerName}}`,
  `{{amount}}`, `{{link}}`) and HTML-escapes interpolated values in the HTML body.
- **D9 storage:** ship as **embedded resource files** under `Notifications/Templates/{locale}/{key}.*`;
  the same `ITemplateProvider` interface admits a future DB-backed provider for owner-editable copy.
- Template **keys** form a small registry constant set (`NotificationTemplateKeys`) so callers can't
  typo a key; missing key is a startup/validation error, not a runtime silent drop.
- Coordinate the key list + locales with the **localization spec** (single source of locales; this spec
  owns only the notification keys).

### 5.3 Outbox + dispatcher

`NotificationOutboxEntity`:

| Field | Notes |
|---|---|
| `Id` (Guid) | PK |
| `IdempotencyKey` (string, unique) | D3 — collapses duplicate triggers/retries |
| `Channel` (enum) | Email / Sms / InApp |
| `Category` (enum) | for prefs + alerting |
| `TemplateKey`, `Locale` | provenance |
| `RecipientAddress` | rendered target (email/phone); InApp uses StaffUserId/PlayerAccountId |
| `StaffUserId?`, `PlayerAccountId?`, `OrganizationId?`, `BranchId?` | linkage + tenant scoping |
| `Subject`, `BodyText`, `BodyHtml` | rendered at enqueue (immutable snapshot) |
| `Status` (enum) | `Pending` → `Sending` → `Sent` / `Failed` / `Suppressed` |
| `AttemptCount`, `NextAttemptUtc`, `LastError` | retry/backoff (D11) |
| `CreatedUtc`, `SentUtc?` | timing |

`NotificationDispatcher` (hosted `BackgroundService`, mirrors `InvoiceGenerationHostedService` shape):
polls `Pending`/retry-due rows, claims a row (`Sending`, concurrency-safe), selects the channel,
sends, and records the result with backoff. **Suppressed** is used when a preference check fails at
dispatch time (belt-and-suspenders to the enqueue-time check). Sent rows form a delivery audit trail.

### 5.4 Channels

`INotificationChannel { NotificationChannel Channel; Task<ChannelResult> SendAsync(OutboxRow, ct); }`

- **`SmtpEmailChannel` (now):** MailKit over the founder's configured SMTP. Config via
  `NotificationOptions { SmtpHost, SmtpPort, UseStartTls, Username, Password, FromAddress, FromName, DefaultLocale, MaxAttempts, BackoffSchedule }` bound from configuration/secrets. Transient SMTP errors →
  retry; permanent (bad address) → `Failed` fast.
- **`SmsChannel` (seam, later, D8):** same interface; registering it lets OTP/dunning prefer SMS.
- **`InAppChannel` (seam, later):** writes to an in-app inbox table the portal/shell renders (Track 3).

### 5.5 Preferences / opt-out

`NotificationPreferenceEntity (RecipientRef, Category, Channel, OptedOut)`. Resolution:

- **Transactional** (D4) → **always send**, opt-out ignored (OTP, password reset, invite, invoice).
- **Operational/Digest** → honoured; an opted-out recipient yields a `Suppressed` outbox row (still
  recorded for audit, never delivered).
- Default-on for operational owner alerts (low-stock, daily summary) so silence is a deliberate choice.

## 6. Data Model Changes (summary)

| Entity / contract | Change |
|---|---|
| `NotificationOutboxEntity` | **new** (§5.3); unique index on `IdempotencyKey`; index on `(Status, NextAttemptUtc)` |
| `NotificationPreferenceEntity` | **new** (recipient × category × channel opt-out) |
| `INotificationService`, `NotificationRequest`, `NotificationRecipient` | **new** contracts (§5.1) |
| `NotificationCategory`, `NotificationChannel`, `NotificationTemplateKeys` | **new** enums/constants |
| `ITemplateProvider`, `INotificationRenderer`, `NotificationTemplate` | **new** template subsystem |
| `INotificationChannel`, `SmtpEmailChannel`, `NotificationOptions` | **new** channel seam + email impl |
| `NotificationDispatcher` | **new** hosted service |
| Staff/owner identity | **add `Email`** (so staff invite, owner invite, password reset have a target) — **migration** |
| `PlayerAccountEntity` | keep `PhoneNumber`; **add optional `Email`** + `PreferredLocale` for OTP/dunning/digest targeting |
| `InventoryStockDto` / product entity | **add `ReorderThreshold`** (nullable) to enable low-stock detection (consumed by §7 caller) |
| `StaffInviteEntity` (or extend owner-invite pattern) | **new/extended** — token + email send + **resend**, replacing copy-paste password |
| `PasswordResetTokenEntity` | **new** — single-use, expiring token for staff/owner/player self-service reset |
| `PlayerOtpEntity` | **new** — short-lived OTP codes for player auth (email-first per D7) |

Each change carries an EF migration. Money stays `long` minor units end-to-end; values are formatted to
display strings **at the caller** before becoming template tokens (existing convention).

## 7. Trigger Integrations (thin callers this unblocks)

Each is a small call into `SendAsync`/`SendNowAsync` — the feature owns its logic, the backbone owns delivery.

| Trigger | Template key(s) | Category | Channel(s) | Notes / new pieces |
|---|---|---|---|---|
| **Staff invite link** | `staff.invite` | Transactional | Email→(SMS) | Replace inline-password `CreateStaffUserRequest` with an invite token + email; staff sets own password on accept. Removes out-of-band password. |
| **Owner invite + resend** | `owner.invite` | Transactional | Email | Wire `createOwnerInvite` to email the link; add **`resend`** (re-send existing code) so admins stop revoke+recreate. |
| **Staff/owner password reset** | `staff.password_reset`, `owner.password_reset` | Transactional | Email | New `PasswordResetTokenEntity` + "forgot password" endpoint; complements (does not remove) admin reset. |
| **Player password reset** | `player.password_reset` | Transactional | Email→(SMS) | Self-service for portal/shell accounts. |
| **Player auth OTP** | `player.otp` | Transactional | **Email first (D7)** →SMS | `SendNowAsync` (user waiting). Email-only until SMS channel lands; phone-only accounts wait for SMS. |
| **Invoice issued** | `invoice.issued` | Transactional | Email | Hook `InvoiceGenerationHostedService`/runner to enqueue on issue; idempotent on `invoice-issued:{id}`. |
| **Invoice paid** | `invoice.paid` | Transactional | Email | Receipt/confirmation on payment. |
| **Invoice overdue (dunning)** | `invoice.overdue` | Transactional | Email→(SMS) | Scheduled reminder ladder; idempotent per `(invoiceId, dunningStage)`. |
| **Low-stock alert** | `inventory.low_stock` | Operational | Email→InApp | Needs `ReorderThreshold`; a watcher (hosted or post-movement) enqueues when `StockOnHand ≤ threshold`, idempotent per `(productId, restock-cycle)`. |
| **Shift discrepancy** | `shift.discrepancy` | Operational | Email→InApp | On shift close with cash variance over tolerance, alert the owner (anti-fraud spec). |
| **Daily owner summary** | `owner.daily_summary` | Digest | Email | Scheduled digest (revenue, discrepancies, alerts) — explicitly requested by the anti-fraud spec. |
| **Scheduled reports** | `report.scheduled` | Digest | Email | A schedule + the existing `ReportCsvExporter` output as an attachment/link. |

## 8. Error Handling & Edge Cases

- **SMTP outage:** queued sends sit `Pending` and retry on backoff (D11); nothing user-facing blocks.
  Transactional rows exceeding `MaxAttempts` go `Failed` and are surfaced to ops/alerting.
- **OTP / reset while SMTP is down:** `SendNowAsync` returns a soft failure; the UI shows "couldn't send
  a code, try again" — the code/token is **not** consumed, so a later retry works. (OTP/reset tokens are
  generated and stored before send; a failed send never burns the token.)
- **No contact on file:** recipient with neither email nor phone for the chosen channel → row is
  `Suppressed` with a clear reason; the triggering UI flags "no email on file" rather than silently
  dropping (especially staff/owner whose `Email` is newly added and may be blank on legacy rows).
- **Duplicate triggers / retries:** unique `IdempotencyKey` collapses them; a repeat returns the existing
  handle. Double-tap "resend" within a window is rate-limited per recipient+template.
- **Opt-out vs transactional:** opt-out can **never** suppress OTP, password reset, invite, or invoice
  (D4); attempting to opt out of those is rejected at the API.
- **Locale missing:** falls back to the configured `DefaultLocale` (D12); never fails the send.
- **Template missing/typo:** caught at startup validation against `NotificationTemplateKeys`; never a
  silent runtime no-op.
- **HTML injection in tokens:** the renderer escapes interpolated values in the HTML body.
- **Multi-channel fallback:** if the first preferred channel has no usable address, the dispatcher tries
  the next channel in `PreferredChannels` before giving up.
- **Tenant scoping:** outbox rows carry `OrganizationId`/`BranchId`; a suspended tenant's operational
  digests are suppressed (transactional billing/dunning still send, per D4).

## 9. Testing Strategy

- **Renderer (pure):** token substitution, HTML escaping, locale fallback, missing-key detection — unit
  tests with no I/O.
- **Outbox + dispatcher:** enqueue writes exactly one row per channel; idempotency key collapses
  duplicates; retry advances `AttemptCount`/`NextAttemptUtc` on a transient failure and stops at
  `MaxAttempts → Failed`; permanent failure fails fast; claim is concurrency-safe (no double-send).
- **Channels:** `SmtpEmailChannel` against a fake/in-memory SMTP; transient vs permanent error mapping.
- **Preferences:** transactional bypasses opt-out; operational/digest honours it (→ `Suppressed`);
  opt-out attempt on a transactional category is rejected.
- **OTP/reset token safety:** a failed send does **not** consume the code/token; a later retry succeeds.
- **Trigger callers:** each integration (staff invite, owner invite + resend, password reset, OTP,
  invoice issued/paid/overdue, low-stock, shift-discrepancy, daily summary, scheduled report) enqueues
  the right template + category + idempotency key with the expected tokens.
- **End-to-end (per channel):** trigger → outbox → dispatcher → fake SMTP receives the expected
  subject/body; idempotent and transactional w.r.t. the triggering operation (outbox write participates
  in / follows the same unit of work so a rolled-back trigger doesn't leak a send).

## 10. Decomposition & Sequencing

This is the enabling backbone; build the core, then layer triggers.

1. **Core backbone (foundation):** contracts + `INotificationService`, template subsystem,
   `NotificationOutboxEntity` + dispatcher, `SmtpEmailChannel`, `NotificationOptions`. Ship with one
   trivial template to prove the path end-to-end.
2. **Identity contact + preferences:** add `Email` to staff/owner, optional player `Email`/`PreferredLocale`,
   `NotificationPreferenceEntity` + resolution.
3. **Auth triggers:** staff invite (replace inline password), owner invite + **resend**, staff/owner/player
   password reset, player **email OTP (D7)**. (Unblocks onboarding + portal/shell auth.)
4. **Billing triggers:** invoice issued / paid / overdue dunning (hook the invoice runner).
5. **Operational triggers:** `ReorderThreshold` + low-stock watcher; shift-discrepancy + daily owner
   summary (with anti-fraud); scheduled reports (with `ReportCsvExporter`).
6. **Later channels:** `SmsChannel` (flip OTP/dunning to SMS-preferred), `InAppChannel` + inbox (Track 3).

## 11. Dependent specs (who is blocked on this)

- **Customer portal** (`2026-06-01-platform-customer-portal-design.md`) — player OTP login + self-service
  password reset depend on §7 (player OTP, player password reset) and ultimately the `InApp` inbox seam.
- **Customer shell experience** (`2026-06-01-platform-customer-shell-experience-design.md`) — in-shell
  OTP/sign-in and any on-screen alerts ride the OTP + `InApp` channel.
- **Anti-fraud controls** (`2026-06-01-platform-anti-fraud-controls-design.md`) — the **daily owner
  summary email** and **shift-discrepancy alert** are explicitly this backbone's deliverables.
- **Localization** (`2026-06-01-platform-localization-design.md`) — bidirectional: this spec **consumes**
  the locale set + resolution; the localization spec owns the catalogue. Notification template keys are
  defined here, their translations there.
- **Billing/admin** (`2026-05-31-platform-admin-control-plane-design.md`, billing backend) — invoice
  issued/paid/overdue messaging hooks the existing `InvoiceGenerationHostedService`.

## 12. Future (v2 / later tracks)

- **SMS provider** behind `SmsChannel` (local aggregator or Twilio); OTP/dunning prefer SMS; phone-only
  players become reachable.
- **In-app inbox** surface in portal/shell (`InAppChannel` + read/unread state).
- **DB-backed, owner-editable templates** (D9 second provider) with per-tenant branding (logo, from-name).
- **Mobile push** (FCM/APNs) as a fourth channel.
- **Delivery analytics / bounce + complaint handling** (suppression list from hard bounces).
- **Webhook channel** for owner-side integrations (e.g. post a daily summary to a chat system).
