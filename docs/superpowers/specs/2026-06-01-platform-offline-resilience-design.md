# Offline / Network Resilience — Keep the Club Running Through Outages (Tier-0 Reliability)

- **Date:** 2026-06-01
- **Status:** Design (recommended defaults baked in; **Proposed decisions** flagged inline for founder review)
- **Scope owner:** Platform (AFK4)
- **Related:** [[platform-web-redesign]], full-product UX audit (2026-06-01); sibling Tier-0 specs
  `2026-06-01-platform-counter-loop-postpaid-checkout-design.md` (Track 1 checkout/outbox coupling),
  `2026-06-01-platform-anti-fraud-controls-design.md` (audit of offline-queued actions),
  `2026-06-01-platform-realtime-consistency-design.md` (reconnect / SignalR fallback ordering).

## 1. Context & Problem

The target market is PC clubs in places with **unreliable internet**. The product today assumes the
backend is reachable: when it is not, a paying customer gets locked out mid-game, the operator goes
blind, and money can silently vanish. Four failure surfaces, all verified in code:

- **Paying customers are kicked after ~15 minutes offline.** The grace window is a single hardcoded
  constant — `SessionLeaseOptions.LeaseMinutes = 15`
  (`src/AFK4.Platform.Api/Sessions/SessionLeaseOptions.cs:7`). The agent caches the last signed lease
  durably (`FileSessionLeaseStore`, atomic write — `src/AFK4.Agent.Service/Enforcement/FileSessionLeaseStore.cs`),
  but `GraceModeMonitor.EnforceAsync` force-locks the PC the moment `lease.ExpiresAtUtc <= now`
  (`src/AFK4.Agent.Service/Enforcement/GraceModeMonitor.cs:24-31`). Lease refresh is **backend-driven
  only**: `EfHeartbeatSessionCommandPlanner` re-issues a signed lease when within a 5-minute
  `RefreshThreshold` (`src/AFK4.Platform.Api/Sessions/EfHeartbeatSessionCommandPlanner.cs:19,106-131`).
  If the agent cannot reach the backend, there is **no local refresh path** — the 15-minute clock
  simply runs out and the customer is locked out of a session they paid for.

- **The heartbeat loop has no proactive defense near expiry.** The agent heartbeats every ~10s
  (`HeartbeatRetryIntervalSeconds = 10`, `src/AFK4.Agent.Service/Worker.cs:25`) and on failure just
  retries at the same cadence (`Worker.cs:99-103`). It does not heartbeat *harder* when the cached
  lease is about to expire and the network is flaky — exactly when a few extra retries would save the
  session.

- **The operator goes blind when the backend is down.** `FloorMapWorkspaceViewModel` is **cloud-only**:
  it loads the floor map straight from the API and on `HttpRequestException` only sets `ErrorMessage`
  (`src/AFK4.Operator.App/FloorMap/FloorMapWorkspaceViewModel.cs:198-214`). There is **no local
  mirror**. During an outage the floor map is empty and the operator cannot see seats, start/end
  sessions, or lock/unlock anything.

- **Money can be lost two ways.** (a) The billing ledger is written inline with the calling command's
  `SaveChangesAsync` — `SessionBillingService.AppendLedgerEntriesAsync` adds `LedgerEntry` rows but
  there is **no transactional outbox** (`src/AFK4.Platform.Api/Billing/SessionBillingService.cs:294-375`);
  a crash between the side-effect (lock dispatched / POS marked paid) and the ledger commit can orphan
  a charge. Track 1 moves the postpaid charge to **checkout**, which concentrates the money write at a
  single, network-dependent moment — an outbox protects exactly that. (b) The agent's command-**result**
  POST is fire-and-forget with **no local retry**: `Worker.HandleHeartbeatCommandsAsync` does
  `response.EnsureSuccessStatusCode()` and on failure the lock/unlock acknowledgement is lost
  (`src/AFK4.Agent.Service/Worker.cs:106-135`). Inbound device commands *are* durable and idempotent
  (`DeviceCommandEntity` with `CommandId`, polled via heartbeat — `src/AFK4.Platform.Api/Data/DeviceCommandEntity.cs`),
  but the return path is not.

This design hardens the everyday club against outages so that: a paying customer is **not** kicked on
a network blip; the operator keeps a usable, clearly-degraded view; and **no charge is ever lost**.

## 2. Goals

1. **Configurable, safe grace window.** Per-branch grace minutes (default 15) that the agent applies
   to *extend* the local lease while offline **only when a valid session already existed**, so a paying
   customer is not locked out mid-game by a transient outage.
2. **Transactional billing outbox.** Ledger/charge writes (session start, extend, and Track 1 checkout)
   go through an outbox so a mid-operation crash can never orphan or drop a charge.
3. **Operator offline fallback.** A local mirror of last-known floor state plus a clearly-degraded UX:
   read-only cached view and **queued** lock/unlock — no blind operation.
4. **Agent command-result outbox with retry.** Command acknowledgements survive a network failure and
   are redelivered idempotently.
5. **Proactive aggressive lease refresh.** When the cached lease is near expiry **and** heartbeats are
   failing, the agent escalates heartbeat cadence to claw a refresh back before the grace clock runs out.

## 3. Non-goals (explicitly deferred)

- **Full offline club-server / local control plane** that can *originate* billable sessions while the
  cloud is unreachable. This is a later phase (see §11). v1 offline operation is **read-only cached +
  queued lock/unlock** (see the §3-adjacent **Proposed decision** in the table and §5.3).
- **Multi-region / HA backend, database failover.** Infra, out of scope.
- **Offline POS sales origination** (selling snacks with the backend down). Deferred with offline
  session origination; the same conflict/double-charge risks apply.
- **Counter-loop semantics** (open-tab, checkout, split payment) — owned by the Track 1 spec; this spec
  only adds the **outbox** the checkout charge rides on.
- **Realtime/SignalR reconnect ordering** beyond what resilience needs — owned by the realtime spec.

## 4. Locked / Proposed Decisions

| # | Decision | Choice |
|---|----------|--------|
| D1 | Grace-window storage | **Per-branch** `GraceLeaseMinutes` (nullable) overriding the global `SessionLeaseOptions.LeaseMinutes` default; agent receives the effective value in the heartbeat/lease payload so it works offline. |
| D2 | **Operator may start/end billable sessions while offline?** | **Proposed decision: NO in v1.** Offline = cached read-only view + queued lock/unlock only. Originating/closing billable sessions offline risks double-charge and start/stop conflicts with the cloud authority; full offline origination is the later club-server phase (§11). *Founder may override to allow a constrained "emergency unlock without billing" — see §7.* |
| D3 | Grace-window **default** | **Proposed decision: 15 minutes** (preserves today's behaviour as the floor). |
| D4 | Grace-window **maximum** | **Proposed decision: 120 minutes** hard cap, validated server-side, so a misconfiguration cannot let a PC run unpaid indefinitely. *Founder may raise/lower.* |
| D5 | Offline lease **extension policy** | Extend the cached lease in place **only if** (a) a valid signed lease for an `active` session already exists locally and (b) the agent has been offline less than the effective grace window measured from **last successful contact**. Never fabricate a lease from nothing. |
| D6 | Outbox scope | One **transactional outbox** table written in the *same DB transaction* as the side-effecting command (start/extend/checkout), drained by a hosted dispatcher. Covers ledger-commit confirmation + downstream notifications. |
| D7 | Command-result delivery | Agent persists every command result to a **local result outbox** (file-backed, same pattern as `FileSessionLeaseStore`) and redelivers on each heartbeat until the backend acks; backend dedupes on `CommandId`. |
| D8 | Offline mirror freshness | Operator caches the last good floor map locally with a `CachedAtUtc`; UI shows an explicit "stale since HH:MM (offline)" banner. **Proposed decision:** mark stale after **30 s** without a successful refresh. |
| D9 | Conflict resolution on reconnect | **Backend is always authority.** Queued operator actions (lock/unlock) reconcile against current cloud state; superseded queued actions are dropped with an audit note, never silently re-applied. |

## 5. Architecture Overview

The backend stays the **single authority** for session/billing/lock state. Resilience adds three local
durability layers (agent lease + agent result outbox + operator mirror) and one server-side durability
layer (billing outbox), with the network as the *only* thing that may be missing.

```
        ┌──────────────────────── Platform.Api (authority) ────────────────────────┐
        │  Start/Extend/Checkout ─┬─ ledger write ─┐                                │
        │                         └─ OUTBOX row ────┴─[same TX]─▶ OutboxEntity      │
        │  OutboxDispatcher (hosted) ──drains──▶ confirm ledger / emit notify       │
        │  Heartbeat planner ── effective GraceLeaseMinutes (per-branch) ──┐        │
        │  Command result ack ── dedupe on CommandId ◀────────────────┐    │        │
        └──────────────────────────────────────────────────────────┐ │    │        │
                  ▲ heartbeat (≤10s; escalates near expiry)         │ │    │        │
   (network may be down at any of these edges)                      │ │    │        │
                  │                                                  │ │    ▼        ▼
   ┌────────── Agent.Service (gaming PC) ───────────┐    ┌────── Operator (WPF/React) ──────┐
   │ FileSessionLeaseStore (cached signed lease)    │    │ FloorMapWorkspaceViewModel        │
   │  + OfflineLeaseExtender (grace, D5)            │    │  + FloorMapCache (last-known, D8) │
   │ GraceModeMonitor (lock only past grace)        │    │  + ActionOutbox (queued lock/    │
   │ CommandResultOutbox (file, retry, D7) ─────────┘    │     unlock, replays on reconnect) │
   │ Heartbeat escalation when near-expiry+failing  │    │  Degraded banner + read-only mode │
   └────────────────────────────────────────────────┘    └───────────────────────────────────┘
```

Five independently testable components:

1. **Configurable grace + offline lease extension** (server option + agent extender + monitor change).
2. **Heartbeat escalation** (agent cadence policy).
3. **Billing outbox** (server table + dispatcher; Track 1 checkout rides it).
4. **Agent command-result outbox** (file-backed retry on the return path).
5. **Operator offline mirror** (local cache + degraded UX + queued action outbox).

## 6. Components

### 6.1 Configurable grace window + offline lease extension

**Server.** Add `GraceLeaseMinutes` (nullable `int`) to `BranchEntity`. Effective grace =
`branch.GraceLeaseMinutes ?? SessionLeaseOptions.LeaseMinutes` (default 15, D3), clamped to `[1, 120]`
(D4) on write. The heartbeat response and the signed lease payload carry the **effective grace minutes**
and a **`LastServerContactUtc`** echo so the agent can enforce the policy with no further calls. The
signed lease's `ExpiresAtUtc` continues to use the effective grace as its TTL (so the existing 5-minute
`RefreshThreshold` re-issue logic scales naturally).

**Agent — `OfflineLeaseExtender`.** New component consulted by `GraceModeMonitor` before it locks. When
the cached lease is at/over `ExpiresAtUtc`, the extender may extend the *local view* of expiry in place
**iff** (D5):
- a valid signed lease for an `active` session is present in `FileSessionLeaseStore`, **and**
- `now − lastSuccessfulContactUtc < effectiveGraceMinutes`.

It never mints a new signature — it only honours the customer's already-paid, already-signed session for
the grace window measured from last contact. Past the window, `GraceModeMonitor` locks exactly as today
(`GraceModeMonitor.cs:29-31`). This converts "kicked 15 min after the *lease was last refreshed*" into
"kept playing for `grace` minutes after the *network actually dropped*", which is the behaviour a paying
customer expects.

**Why this is safe:** the lease is cryptographically signed by the backend; the agent cannot extend
beyond the signed session's identity, and the wall-clock cap (D5/D4) bounds free play. When connectivity
returns and the session was actually ended cloud-side, the heartbeat planner already issues a `Lock`
(`EfHeartbeatSessionCommandPlanner.cs:58-65` "heartbeat-session-missing"), reconciling immediately.

### 6.2 Heartbeat escalation near expiry

In `Worker`, replace the flat `HeartbeatRetryIntervalSeconds = 10` retry with a **cadence policy**:
- Normal: backend-provided `HeartbeatIntervalSeconds` (today's behaviour, `Worker.cs:90`).
- **Escalated:** when the cached lease is within `RefreshThreshold` (5 min) of expiry **and** the last
  heartbeat failed, drop to a tight retry (**Proposed decision: 2 s**, with jittered backoff to avoid a
  thundering herd of agents hammering a recovering backend) until either a refresh lands or grace expires.

This is the cheapest, highest-leverage change: it maximises the chance of catching a brief connectivity
window to refresh the lease before the customer is ever affected. Escalation is purely local and uses the
same idempotent heartbeat endpoint, so it carries no new server contract.

### 6.3 Billing outbox (server)

**Problem recap:** `SessionBillingService` adds ledger rows to the ambient `DbContext` and relies on the
caller's `SaveChangesAsync` (`SessionBillingService.cs:294-375`). A crash *after* a visible side-effect
(lock dispatched, POS marked paid in Track 1 checkout) but *before* the commit orphans the charge.

**Design.** Introduce a generic **transactional outbox**:
- New `OutboxMessageEntity` { `OutboxMessageId` (Guid PK), `OrganizationId`, `BranchId`, `Type`,
  `PayloadJson`, `Status` (`Pending`/`Dispatched`/`Failed`), `AttemptCount`, `AvailableAtUtc`,
  `CreatedAtUtc`, `DispatchedAtUtc`, `IdempotencyKey` (unique) }.
- The session start/extend and the Track 1 **checkout** command write their ledger rows **and** an outbox
  row **in one transaction** via `dbContext.SaveChangesAsync`. Because EF Core batches a single
  `SaveChanges` into one transaction, the ledger entries and the outbox marker commit atomically — either
  both land or neither does. (Where multiple `SaveChanges` calls exist today, wrap them in an explicit
  `IDbContextTransaction`.)
- New hosted `OutboxDispatcher` polls `Pending` rows (ordered by `AvailableAtUtc`), performs the
  downstream effect (confirm/settle, emit notification, realtime fan-out), and marks `Dispatched`. Retries
  with exponential backoff via `AvailableAtUtc`; permanently failing rows go `Failed` and surface on an
  ops view. Dispatch is **idempotent on `IdempotencyKey`** (reuse the existing checkout/start keys from
  Track 1), so redelivery never double-charges.

**Coordination with Track 1.** The checkout transaction (time charge + attached POS settle + payments +
lock dispatch) becomes: *write ledger + payments + outbox row in one TX → return*. The lock command and
receipt emission move behind the outbox so they cannot run without a committed charge. This is the
"outbox protects the checkout charge" coupling called out in the Track 1 spec's offline non-goal.

### 6.4 Agent command-result outbox

**Problem recap:** `Worker.HandleHeartbeatCommandsAsync` posts the result and `EnsureSuccessStatusCode()`
throws on failure, losing the ack (`Worker.cs:106-135`); the realtime path has the same shape
(`DeviceRealtimeClient.HandleCommandAsync`, `DeviceRealtimeClient.cs:94-103`).

**Design.** Add a file-backed `CommandResultOutbox` (mirrors `FileSessionLeaseStore`'s atomic-write
durability, under `AgentOptions.StateDirectory`):
- After `commandHandler.HandleAsync`, **persist the result locally first** (so the lock/unlock physically
  happened *and* is recorded), then attempt delivery.
- On delivery failure, the result stays queued and is **re-POSTed on every subsequent heartbeat** until the
  backend returns success. Delivery is idempotent: the backend already keys commands by `CommandId`
  (`DeviceCommandEntity`), so it transitions the command to its terminal status at-most-once and acks
  duplicates harmlessly.
- The realtime result path enqueues to the same outbox, so SignalR and HTTP share one durable return path.

This closes the only non-durable hop in the device-command round trip; the inbound queue was already
reliable.

### 6.5 Operator offline mirror + degraded UX

**Cache.** Add a `FloorMapCache` (per branch) persisted locally by the operator app (WPF: app data dir;
React operator: IndexedDB/localStorage). On every successful `apiClient.GetFloorMapAsync`, persist the
DTO with `CachedAtUtc`. On an `HttpRequestException`/`InvalidOperationException` in
`FloorMapWorkspaceViewModel.LoadAsync` (`FloorMapWorkspaceViewModel.cs:211-214`), instead of only setting
`ErrorMessage`, **hydrate from cache** and enter a degraded state.

**Degraded UX (D8/D2).**
- Banner: **"Офлайн — данные от HH:MM, только просмотр"** once `now − CachedAtUtc > 30 s` (D8) or a refresh
  fails. Seats render from cache, visibly muted.
- **Read-only by default:** start/end/extend/checkout are **disabled offline** (D2). Their controls show a
  tooltip explaining the backend is unreachable. (Live accrued cost from Track 1 keeps ticking locally off
  the cached `StartedAtUtc`, so the operator still sees roughly what each seat owes.)
- **Queued lock/unlock only:** lock/unlock enqueue to a local **`ActionOutbox`** with an idempotency key
  and show a "queued" pill. On reconnect, queued actions are replayed against the live floor map; any
  action **superseded by current cloud state is dropped with an audit note** (D9) rather than re-applied.
  (Lock/unlock are safe to queue because they are idempotent device commands that reconcile against
  authority; *billing* actions are not, which is why D2 keeps them online-only.)

**Reconnect.** On the first successful refresh, exit degraded mode, drain the `ActionOutbox`, and resume
normal behaviour. SignalR reconnection itself is owned by the realtime spec; this component only needs the
"first good refresh" signal.

## 7. Data Model Changes (summary)

| Entity / contract | Change |
|---|---|
| `BranchEntity` | add `GraceLeaseMinutes` (nullable `int`, clamped `[1,120]` on write) |
| `SessionLeaseOptions` | keep `LeaseMinutes` as the **global default** (15); no longer the only source |
| Heartbeat response / lease payload | add `EffectiveGraceMinutes`, `LastServerContactUtc` echo |
| `OutboxMessageEntity` | **new** server table (see §6.3 fields); unique index on `IdempotencyKey` |
| New hosted service | `OutboxDispatcher` (drains `Pending` with backoff) |
| `SessionBillingService` / checkout | write ledger + outbox row in one transaction (wrap multi-`SaveChanges` paths in an explicit TX) |
| Agent `CommandResultOutbox` | **new** file-backed store under `AgentOptions.StateDirectory`; retry on heartbeat |
| Agent `OfflineLeaseExtender` | **new**; consulted by `GraceModeMonitor` before locking |
| Agent `Worker` | heartbeat **cadence policy** (escalate near-expiry + failing; 2 s jittered) |
| Operator `FloorMapCache` | **new** local last-known-state cache with `CachedAtUtc` |
| Operator `ActionOutbox` | **new** local queue for offline lock/unlock with idempotency keys |

Each server change carries an EF migration. **Money stays `long` minor units end-to-end**, converted only
at the UI boundary (the existing, verified-correct convention) — the outbox stores amounts as `long` minor
units in `PayloadJson`.

## 8. Error Handling & Edge Cases

- **Lease extended but session was actually ended cloud-side.** On reconnect the planner emits
  `Lock` ("heartbeat-session-missing", `EfHeartbeatSessionCommandPlanner.cs:58-65`); the extender's local
  extension is overridden immediately. Worst case the customer played a few extra (grace-bounded) minutes —
  acceptable and bounded by D4.
- **Clock skew on the gaming PC.** Grace is measured from `lastSuccessfulContactUtc` (a value the agent
  itself stamped at last contact) against local `TimeProvider`, so it is robust to absolute-clock drift; the
  signed lease's own `ExpiresAtUtc` remains the cryptographic bound.
- **Outbox dispatcher crash mid-drain.** Rows stay `Pending`; idempotent dispatch on `IdempotencyKey` makes
  redelivery safe. No charge is created or settled twice.
- **Duplicate command result after retry.** Backend dedupes on `CommandId`; second ack is a no-op success so
  the agent can clear its outbox entry.
- **Operator queues an unlock, then the session is force-ended cloud-side.** Queued unlock is dropped on
  reconnect with an audit note (D9); the live map shows the seat locked.
- **Two operators (desktop + browser) both offline, both queue conflicting actions.** Each reconciles
  against authority on reconnect; last-write-against-current-state wins, superseded actions dropped + audited.
- **Cache poisoning / corrupt local file.** Cache and both file outboxes validate on load and self-delete on
  parse failure (same pattern as `FileSessionLeaseStore.LoadCurrent`), degrading to "no cache / empty queue"
  rather than crashing.
- **Grace misconfigured to a huge value.** Server-side clamp to `[1,120]` (D4) rejects it before it can let a
  PC run unpaid for hours.
- **Founder override of D2 ("emergency unlock without billing").** If allowed, it must be permission-gated,
  produce **no** ledger entry, and be loudly audited (ties to the anti-fraud spec) so it cannot be used to
  give away free time routinely.

## 9. Testing Strategy

- **Offline lease extension:** with a valid cached lease and `now − lastContact < grace`, `GraceModeMonitor`
  does **not** lock; past grace it **does**; with no valid cached lease it locks immediately (no fabrication).
  Boundary tests at exactly `grace`.
- **Grace config:** effective resolution (branch override > global default), clamp rejects `<1` and `>120`,
  payload carries the effective value to the agent.
- **Heartbeat escalation:** near-expiry + failing transitions to the 2 s jittered cadence and returns to
  normal once a refresh lands; no escalation when far from expiry.
- **Billing outbox:** ledger + outbox row commit atomically (inject a failure between them → neither
  persists); dispatcher is idempotent on `IdempotencyKey` (replay → single charge); Track 1 checkout charge
  + lock + receipt all ride the outbox and survive a simulated mid-checkout crash.
- **Command-result outbox:** result persisted locally before delivery; delivery failure re-POSTs on next
  heartbeat; backend dedupes duplicate `CommandId`; outbox drains to empty on success.
- **Operator mirror:** successful load populates cache; a forced `HttpRequestException` hydrates from cache,
  shows the stale/offline banner after 30 s, disables billing actions, and allows queued lock/unlock; on
  reconnect the queue drains and superseded actions are dropped + audited.
- **Web/desktop parity:** the degraded-mode rules (read-only + queued lock/unlock) behave identically in WPF
  and React operator surfaces.

## 10. Decomposition & Sequencing

Five separable units; suggested build order:

1. **Billing outbox** (§6.3) — foundational and unblocks Track 1's safe checkout; ship first.
2. **Configurable grace + offline lease extension** (§6.1) — highest customer-facing impact (stops the
   mid-game kick); independent of the outbox.
3. **Heartbeat escalation** (§6.2) — small, pairs naturally with #2.
4. **Agent command-result outbox** (§6.4) — independent agent-side hardening.
5. **Operator offline mirror + degraded UX** (§6.5) — largest UI surface; depends only on the cache contract,
   can parallel #1–#4.

## 11. Future (v2 / later phases)

- **Offline club-server / local control plane** that can originate and close billable sessions (and POS
  sales) while the cloud is unreachable, with conflict-free reconciliation on reconnect — the real answer for
  clubs with hours-long outages, and the reason D2 stays NO in v1.
- **Store-and-forward sync** for shift/Z-report data captured during extended outages.
- **Per-device adaptive grace** tuned to a branch's historical connectivity quality.
- **Outbox-backed transactional email/notifications** once the Track 2 notification backbone lands (the
  outbox dispatcher is the natural carrier).
