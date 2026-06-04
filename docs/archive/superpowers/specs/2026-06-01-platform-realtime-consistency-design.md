# Real-Time & Consistency for Live Operator State

- **Date:** 2026-06-01
- **Status:** Design (best-practice defaults baked in; forks flagged as **Proposed decision:** for founder review)
- **Scope owner:** Platform (AFK4)
- **Related:** [[platform-web-redesign]], counter-loop ([2026-06-01-platform-counter-loop-postpaid-checkout-design.md](2026-06-01-platform-counter-loop-postpaid-checkout-design.md)), frontend consolidation ([2026-06-01-platform-frontend-consolidation-design.md](2026-06-01-platform-frontend-consolidation-design.md)), offline resilience ([2026-06-01-platform-offline-resilience-design.md](2026-06-01-platform-offline-resilience-design.md))

## 1. Context & Problem

The operator floor is **live state**: seats fill and empty, timers run, money accrues, and two staff
may touch the same seat at the same second. The current implementation is live in places and stale or
unguarded in others.

- **The React operator dashboard polls, it does not push.** `AFK4.Operator.App.Web` refreshes its
  shell/operational KPIs (current shift, dashboard summary — revenue, active count) on a fixed
  `window.setInterval` of `shellOperationalRefreshMs = 30_000`
  (`src/AFK4.Operator.App.Web/src/App.tsx:123`, the interval at `:9209`). So a session that starts or
  ends shows up in the KPIs up to **30 s late**, even though the WPF operator gets sub-second SignalR
  pushes for device state. The React floor map *is* already realtime; the **dashboard numbers are not**.
- **Session-lifecycle events are persisted but never broadcast.** `EfSessionCommandService` writes
  `SessionEventEntity` rows (`session-started`, `session-extended`, `session-transferred`,
  `session-ending` at `EfSessionCommandService.cs:131/259/372/477`) and the heartbeat planner adds
  `session-ended`. None of these are pushed over SignalR. The `DeviceHub` only broadcasts
  `deviceStatusChanged` and `deviceCommandResult` (`DeviceRealtimeEvents.cs`). The dashboard therefore
  has no live signal to react to and must poll.
- **The React realtime client is wired for the floor map but not finished.** `operatorRealtime.ts`
  exposes `createOperatorRealtimeClient` with `onDeviceStatusChanged` and `onDeviceCommandResult`; it is
  consumed in `App.tsx:9438` to patch floor-map seats and trigger an authoritative reload. But there is
  **no session-lifecycle subscription** and the dashboard effect (`:9209`) is a *separate* polling loop
  that the realtime client never feeds. End-to-end, floor map ≈ live, dashboard = polled.
- **Heartbeat command-delivery latency is ~10 s on the fallback path.** The agent's heartbeat interval
  is **server-driven**: `DeviceHeartbeatResponse.HeartbeatIntervalSeconds` is hard-coded to `10` in both
  `DeviceHeartbeatService.cs:109` and `InMemoryDeviceHeartbeatService.cs:25`; the agent obeys it and
  falls back to a `HeartbeatRetryIntervalSeconds = 10` on failure (`Worker.cs:25/90`). When the agent's
  SignalR DeviceHub connection is up, lock/unlock is sub-second; when it is degraded to HTTP-poll only,
  a command lands in up to **one heartbeat interval (~10 s)**.
- **No optimistic concurrency on seat/session commands.** `StartGuestSessionAsync` guards occupancy with
  a **read-then-write** check (`HasBlockingSessionAsync`, `EfSessionCommandService.cs:64/598`) inside a
  transaction that uses the **default isolation level** (`ExecuteInTransactionAsync` at `:770` passes no
  `IsolationLevel`). `SessionEntity` (`Data/SessionEntity.cs`) has **no `RowVersion`/concurrency token**
  and no unique index enforcing "one active session per seat". Two operators clicking *Start* on the same
  "free" seat can both pass the check and both insert — the server does **not** return `409`. The
  `SessionCommandServiceResult.Conflict` flag exists and is mapped to HTTP 409
  (`Program.cs:3840/3928/4017/4105`), but today it only fires on **idempotency-key reuse**, never on a
  stale read or seat collision.

This design makes the operator dashboard push-live, finishes the React realtime wiring end-to-end,
adds a real concurrency guard so two operators cannot collide on a seat, and tunes command-delivery
latency — without touching the parts that already work well (sub-second device pushes; smooth
client-side 1 s countdown timers).

## 2. Goals

1. **Push the operator dashboard live** by broadcasting **session-lifecycle events**
   (start / extend / end / checkout / grace) over the existing `DeviceHub` per-branch group, replacing
   the 30 s KPI poll with event-driven refresh.
2. **Finish the React operator realtime wiring end-to-end**: subscribe to session-lifecycle and keep the
   existing floor-map device-status + command-result subscriptions, so floor map *and* dashboard react
   to the same push stream.
3. **Add a seat/session state VERSION and enforce it**: mutating commands
   (start / extend / end / transfer / checkout) carry an expected version; the server returns
   **409 Conflict** on a stale or colliding command instead of silently double-acting.
4. **Tune command-delivery latency**: evaluate reducing the heartbeat interval (10 s → 5 s) for faster
   fallback command delivery, weighed against request load from 50 to 5000 PCs, and make the interval
   **configurable** rather than hard-coded.

### Non-goals (deferred to bordering specs)

- New checkout / grace **session states and their semantics** — owned by the counter-loop spec; this
  spec only **transports** the lifecycle events those states emit.
- **Offline buffering / outbox** for commands and billing when the backend is unreachable — owned by the
  offline-resilience spec. This spec assumes backend reachability and improves the *online* path.
- **Choosing the single operator UI** (React vs WPF) — owned by the frontend-consolidation spec. This
  spec is written so the realtime wiring is done **once** on whichever surface wins.
- SignalR scale-out backplane (Redis) sizing for a multi-node API — noted in Future, not designed here.

## 3. Decisions

| # | Decision | Choice |
|---|----------|--------|
| D1 | Dashboard freshness | **Event-driven** via `DeviceHub`; the 30 s poll demotes to a slow safety-net reconcile (≥120 s), not the primary path. |
| D2 | Event transport | **New typed `sessionLifecycleChanged` event** on the existing `DeviceHub` per-branch group — *not* an overload of the heartbeat/device-status broadcast (see **Proposed decision: event schema**). |
| D3 | Where realtime is wired | The React operator subscribes to lifecycle events in the **same `useEffect`** that already owns `createOperatorRealtimeClient`; both floor map and dashboard consume one stream. |
| D4 | Concurrency mechanism | **Optimistic concurrency**: a monotonic version on the authoritative row + a DB-level guarantee of "one active session per seat" (unique partial index). Stale/colliding mutation → `409`. |
| D5 | Where the version lives | **Session-scoped version** for extend/end/transfer/checkout; **seat occupancy** for start (see **Proposed decision: version placement**). |
| D6 | Heartbeat interval | **Make it configurable**; default stays at the current cadence with a per-tenant/per-branch override (see **Proposed decision: heartbeat interval target**). |
| D7 | Client trust model | The backend remains the **single authority**. Realtime pushes are *hints*; the client patches optimistically and reconciles against an authoritative reload (the existing `scheduleAuthoritativeFloorMapReload` pattern). |

**Proposed decision: event schema.** Two options for (1)/(2):
**(a)** *Reuse the heartbeat/device-status broadcast* — cheap, but `deviceStatusChanged` is device-shaped
(online/locked) and has no session/billing fields, so the dashboard would still have to refetch. Rejected
for the KPI use-case.
**(b, recommended)** *Add a typed `sessionLifecycleChanged` event* carrying
`{ organizationId, branchId, seatId, sessionId, kind, state, version, startedAtUtc, endsAtUtc?, accruedCostMinorUnits?, currencyCode?, observedAtUtc }`, where `kind ∈ {started, extended, ended, checkout, grace, time-up}`. This lets the dashboard apply a **delta** to active-count/revenue without a refetch, and the floor map patch the seat directly. The counter-loop spec already plans `accruedCostMinorUnits`/`currencyCode` on the active-session shape — the event reuses those fields so the two specs share one payload.

**Proposed decision: version placement.** **Recommended:** add `Version int` (or a `byte[] RowVersion`
concurrency token) to `SessionEntity` for the session-scoped mutations (extend/end/transfer/checkout),
**and** enforce start-time collisions with a **unique partial index** on `SeatId` filtered to active-ish
states (`active`, `paused`, `ending`) — because at *start* there is no session row yet to version, so the
seat is the contended resource. The alternative (a version column on a dedicated `SeatOccupancy` row) is
cleaner conceptually but adds a table and a write on every start/end; the partial-index approach reuses
the existing `SessionEntity` and gives the DB the last word with no extra row.

## 4. Architecture Overview

The backend stays the single authority. Lifecycle events ride the **same** `DeviceHub` branch group
(`branch:{branchId}`) that operator clients already join on connect (`DeviceHub.OnConnectedAsync`), so no
new hub, auth path, or group plumbing is needed.

```
 Operator (React / WPF)                 Platform.Api (DeviceHub authority)            Gaming PC
 ───────────────────────                ──────────────────────────────────           ──────────
  floor map ◀── deviceStatusChanged ──── DeviceHeartbeatService ◀── heartbeat ──────── Agent.Service
            ◀── deviceCommandResult ──── DeviceHub.ReportCommandResult ◀── result ──── (HTTP every Ns,
            ◀── sessionLifecycleChanged ─ SessionCommandService ─┐                       SignalR sub-sec
  dashboard ◀─────────────(same event)──────────────────────────┘  broadcast            when connected)
   (delta apply, no refetch)             │   to branch:{branchId} group
                                         │
  start/extend/end/  ── command(version)─▶ SessionCommandService
  transfer/checkout                       ├─ load row + check Version / seat partial-index
                                          ├─ stale or collision ─▶ 409 Conflict (typed)
                                          └─ apply → bump Version → emit sessionLifecycleChanged
  slow safety reconcile (≥120s)  ─────────▶ authoritative floor-map + summary reload
```

Three independently shippable units:

1. **Backend lifecycle broadcast** — emit `sessionLifecycleChanged` on every session mutation.
2. **React realtime completion** — subscribe to lifecycle, drive both floor map and dashboard, demote the
   30 s poll.
3. **Concurrency guard** — `Version` + seat partial-index + `409` on the five mutating commands.
4. **Heartbeat-interval tuning** — configurable interval; pick a default after the load assessment.

## 5. Components

### 5.1 Backend session-lifecycle broadcast

**Current state (verified):** `EfSessionCommandService` persists lifecycle via `AddEvent(...)` but never
pushes. `DeviceHub` already owns the per-branch group and the broadcast pattern
(`Clients.Group(DeviceHubGroups.Branch(branchId)).SendAsync(...)`).

**Changes:**

- Add `SessionLifecycleChanged = "sessionLifecycleChanged"` to `DeviceRealtimeEvents` and a typed DTO in
  `AFK4.Shared.Contracts` mirroring the **Proposed decision: event schema (b)** payload.
- Inject `IHubContext<DeviceHub>` into `EfSessionCommandService` (or, to avoid a service→hub dependency,
  publish through a thin `ISessionLifecycleNotifier` that the hub context backs). **After** each
  successful transaction commit (start/extend/transfer/end and the counter-loop's checkout/grace),
  broadcast the event to `branch:{branchId}`.
- **Broadcast after commit, not inside the transaction**, so a rolled-back command never emits a phantom
  event. Carry the post-commit `Version` in the payload so clients can order/dedupe.
- The counter-loop spec's new states (checkout, grace/time-up, auto-protection lock) emit through the
  **same notifier** — this spec owns the channel, the counter-loop owns the `kind` values it adds.

### 5.2 React operator realtime completion

**Current state (verified):** `operatorRealtime.ts` supports `onDeviceStatusChanged` /
`onDeviceCommandResult`; `App.tsx:9438` wires both for the floor map. The dashboard KPIs poll separately
at `App.tsx:9209` (`shellOperationalRefreshMs = 30_000`).

**Changes:**

- Extend `OperatorRealtimeOptions` with `onSessionLifecycleChanged` and register the
  `sessionLifecycleChanged` handler in `createOperatorRealtimeClient` (same shape as the existing two
  handlers, scope-checked against `authSession`/`branchId` exactly like `matchesRealtimeScope`).
- In the existing realtime `useEffect` (`App.tsx:9438`): on a lifecycle event,
  - **patch the floor-map seat** (state, `endsAtUtc`, `accruedCostMinorUnits`) directly, and
  - **apply a delta to the dashboard summary** (active count, revenue) for `started`/`ended`/`checkout`,
    then debounce an authoritative reconcile via the existing `scheduleAuthoritativeFloorMapReload`
    pattern extended to also refresh the summary.
- **Demote the 30 s poll** at `:9209` to a **safety-net reconcile** (`shellOperationalRefreshMs` ≥
  120 s) that only corrects drift, plus a forced reconcile on `onreconnected` (so a reconnect after a
  missed event re-syncs immediately). Keep the client-side **1 s countdown** unchanged — it is smooth and
  correct.
- **Frontend-consolidation tie-in:** write this once. If React becomes the single operator UI, WPF's
  parallel wiring is retired; if not, the WPF operator gets the same `sessionLifecycleChanged`
  subscription (it already consumes `deviceStatusChanged`).

### 5.3 Concurrency guard (version + 409)

**Current state (verified):** no `RowVersion` on `SessionEntity`; occupancy is a read-then-write check
with default isolation and no unique index; `Conflict→409` mapping exists but only fires on
idempotency-key reuse.

**Changes:**

- **Version column.** Add `Version` to `SessionEntity` (recommended: a real EF concurrency token —
  `byte[] RowVersion` on PostgreSQL `xmin`, or an `int` bumped on every write and marked
  `IsConcurrencyToken()`). Every mutation increments it; `UpdatedAtUtc` already moves in lockstep.
- **Expected-version on mutating contracts.** Add an optional `ExpectedVersion` to
  `ExtendSessionRequest`, `EndSessionRequest`, `TransferSessionRequest`, and the counter-loop
  `SessionCheckoutRequest`. The client sends the version it last saw (from the floor-map DTO / lifecycle
  event). On mismatch → **`409 Conflict`** with a typed body
  `{ error, code: "stale_version", currentVersion }` so the client can refresh and retry. Start has no
  prior version, so it is guarded differently (next bullet).
- **Start collision = DB-enforced.** Add a **unique partial index** on `SessionEntity(SeatId)` filtered to
  active-ish states (`active`, `paused`, `ending`). The second concurrent `Start` violation surfaces as a
  unique-constraint error which `StartGuestSessionAsync` catches and maps to
  `SessionCommandServiceResult.RequestConflict(...)` → `409`. This removes the read-then-write race
  entirely: the database, not application timing, decides who wins.
- **Surface the version.** Add `version` to the floor-map active-session DTO and the
  `sessionLifecycleChanged` payload so the client always has a fresh expected version to send.
- **Idempotency unchanged.** The existing idempotency-key path (collapsing accidental double-taps from the
  *same* client) stays; the version guard handles the *different-operator* collision case. They are
  complementary: idempotency = "same intent twice", version = "stale view".

### 5.4 Heartbeat-interval tuning

**Current state (verified):** interval is server-driven (`HeartbeatIntervalSeconds`) but hard-coded to
`10` in both heartbeat services; the agent obeys it (`Worker.cs:90`).

**Load assessment (steady-state heartbeat POSTs, online path):**

| PCs | @ 10 s (req/s) | @ 5 s (req/s) | Note |
|-----|----------------|---------------|------|
| 50 | 5 | 10 | trivial |
| 500 | 50 | 100 | trivial for a single API node |
| 2000 | 200 | 400 | fine; watch DB write amplification per beat |
| 5000 | 500 | 1000 | meaningful; needs connection reuse + the heartbeat write path kept lean |

The heartbeat does a small read (pending commands) and may enqueue planned commands; halving the interval
**doubles** that load. The latency win is only on the **degraded** path (SignalR down), because the
healthy path already delivers lock/unlock sub-second over the hub. So the right lever is **not a blanket
5 s** but: keep the hub as the fast path, make the interval **configurable**, and shorten it only where
fast fallback matters.

**Changes:**

- Stop hard-coding `10`; source `HeartbeatIntervalSeconds` from configuration with a per-tenant (or
  per-branch) override and a sane floor (e.g. ≥3 s) and ceiling.
- **Proposed decision: heartbeat interval target.** **Recommended:** keep the **default at 10 s** for the
  steady-state floor (it bounds load at 5000 PCs to ~500 req/s) and add an **adaptive shorten**: when a
  device has **pending commands**, the heartbeat response returns a **short interval (e.g. 2–3 s)** so the
  fallback path drains commands fast; once idle, it relaxes back to 10 s. This gives 5 s-class
  responsiveness exactly when it matters with none of the always-on cost. (Founder fork: pick adaptive vs.
  a flat 5 s vs. flat 10 s.)

## 6. Data Model Changes (summary)

| Entity / contract | Change |
|---|---|
| `DeviceRealtimeEvents` | add `SessionLifecycleChanged = "sessionLifecycleChanged"` |
| New DTO (`AFK4.Shared.Contracts`) | `SessionLifecycleChangedDto { orgId, branchId, seatId, sessionId, kind, state, version, startedAtUtc, endsAtUtc?, accruedCostMinorUnits?, currencyCode?, observedAtUtc }` |
| `ISessionLifecycleNotifier` (new) | thin publisher backed by `IHubContext<DeviceHub>`; broadcasts after commit |
| `SessionEntity` | add `Version` (EF concurrency token; recommend `xmin`/`RowVersion` or `IsConcurrencyToken` int) |
| `SessionEntity` index | **unique partial index** on `SeatId` where `State ∈ {active, paused, ending}` |
| `ExtendSessionRequest`, `EndSessionRequest`, `TransferSessionRequest`, `SessionCheckoutRequest` | add optional `ExpectedVersion` |
| floor-map / active-session DTO | add `version` (alongside counter-loop's `accruedCostMinorUnits`, `currencyCode`) |
| `409` body | typed `{ error, code: "stale_version", currentVersion }` for stale mutations |
| `DeviceHeartbeatResponse` | `HeartbeatIntervalSeconds` sourced from config; adaptive short interval when commands pending |
| `operatorRealtime.ts` | add `onSessionLifecycleChanged`; register `sessionLifecycleChanged` handler |
| `App.tsx` | consume lifecycle for floor map + dashboard delta; demote `shellOperationalRefreshMs` to ≥120 s safety reconcile + reconcile-on-reconnect |

Each backend change carries an EF migration. Money stays `long` minor units end-to-end, converted only at
the UI boundary (existing convention).

## 7. Error Handling & Edge Cases

- **Stale mutation (two operators):** the loser gets `409 {code:"stale_version"}`; the client auto-refreshes
  the seat from the authoritative reload and shows "this seat changed, refreshed — try again" rather than a
  silent no-op or a double-action.
- **Concurrent start on the same free seat:** the unique partial index guarantees exactly one insert wins;
  the loser's unique-violation maps to `409`. No double-start, no double-unlock command.
- **Missed event (client briefly disconnected):** SignalR auto-reconnect fires `onreconnected`, which
  forces an authoritative floor-map + summary reconcile, healing any gap; the slow safety-net poll is a
  second backstop. Events are **hints**, never the only source of truth (D7).
- **Out-of-order / duplicate events:** the payload carries `version` + `observedAtUtc`; the client ignores
  an event whose `version` is ≤ the version already applied for that session/seat.
- **Phantom event on rollback:** broadcast happens **after commit**, so a rolled-back command emits
  nothing.
- **Heartbeat interval mid-session change:** the agent reads the interval from each response, so a config
  change takes effect on the next beat with no restart; the adaptive short-interval is bounded by the
  configured floor so a runaway command queue can't DoS the API.
- **SignalR backplane absent on multi-node API:** without a backplane, a `Clients.Group` broadcast only
  reaches clients on the *same* node. Single-node today is fine; multi-node needs Redis (Future).
- **Offline at command time:** out of scope here (offline-resilience spec); this spec assumes
  reachability and only tightens the online path.

## 8. Testing Strategy

- **Lifecycle broadcast:** each of start/extend/transfer/end (and counter-loop checkout/grace) emits
  exactly one `sessionLifecycleChanged` to the correct `branch:{branchId}` group, **after** commit, with
  the post-commit `version`; a rolled-back command emits none.
- **Concurrency guard (the headline test):** two concurrent `Start` calls on one free seat → exactly one
  `200`, the other `409`, and exactly one session row + one unlock command. Two concurrent
  `End`/`Extend`/`Transfer`/`Checkout` with the same `ExpectedVersion` → one applies, the other `409`
  `stale_version`. A mutation with a *current* `ExpectedVersion` succeeds and bumps the version.
- **React realtime end-to-end:** a `sessionLifecycleChanged` patches the floor-map seat and updates the
  dashboard active-count/revenue delta with **no network refetch**; an `onreconnected` triggers an
  authoritative reconcile; the 1 s countdown is untouched.
- **Poll demotion:** with realtime connected, the dashboard updates from events, and the safety-net poll
  fires no more often than its (≥120 s) interval; with realtime down, the safety-net keeps KPIs fresh.
- **Heartbeat interval:** response interval is read from config; pending commands yield the short adaptive
  interval and idle relaxes back; the configured floor is honored.
- **Web/desktop parity (if both surfaces persist):** the same lifecycle event drives both operator UIs.

## 9. Decomposition & Sequencing

1. **Concurrency guard** (`Version` + seat partial-index + `409` on the five mutations). Highest-value,
   backend-only, no UI dependency — fixes the double-start data-integrity hole first.
2. **Backend lifecycle broadcast** (`sessionLifecycleChanged` + notifier). Depends on 1 for the `version`
   field in the payload.
3. **React realtime completion** (subscribe, delta-apply, demote poll). Depends on 2.
4. **Heartbeat-interval tuning** (config + adaptive). Independent; can land any time.

Units 1 and 4 are independent of the frontend; 3 should land **once** on whichever operator UI the
frontend-consolidation spec selects.

## 10. Future (v2 / other tracks)

- **SignalR Redis backplane** for multi-node API so branch-group broadcasts fan out across instances.
- **Per-seat presence/locking UI** ("Ivan is editing seat 12") layered on the version channel.
- **Owner live dashboard** (revenue/occupancy ticker) subscribing to the same `branch:{branchId}` stream.
- **Event replay / catch-up** endpoint so a long-disconnected client can fetch the lifecycle delta since a
  known `version` instead of a full reload.
- **Push lock/unlock fully off the heartbeat fallback** by guaranteeing the agent's hub connection (with
  the heartbeat as pure liveness), making interval tuning moot for command latency.
