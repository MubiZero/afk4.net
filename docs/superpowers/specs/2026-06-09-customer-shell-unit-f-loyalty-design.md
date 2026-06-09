# Unit F — Loyalty / Cashback Design

**Date:** 2026-06-09
**Status:** Approved (design), pending implementation plan
**Epic:** Customer-shell WebView2 pivot — Unit F (engagement/commerce layer). Unit F = three independent subsystems (shop, loyalty, news). Shop shipped (cycle 1). This spec covers **Loyalty/cashback only**; news gets its own spec→plan→implementation cycle later.

## Goal

Give a seated player cashback in real wallet money — configurable by the network owner — on wallet top-ups and/or shop purchases. The player sees their current rates and accumulated cashback in the WebView2 React shell. Server is authoritative for configuration, accrual, and money; the shell and the owner UI are non-authoritative faces.

## Decisions (locked during brainstorming)

1. **Reward form:** cashback is credited as real wallet money — a `cashback` ledger entry on the `wallet` account. It is spent like any other balance (time, shop). No separate bonus account, no points currency.
2. **Accrual sources:** **wallet top-up** and **shop purchase**. Each is an independent toggle with its own percent. (Session/time accrual is out of scope — would require hooking session billing ticks.)
3. **Configuration level:** **organization-wide** (one setting per organization, edited by the owner). No per-branch override this cycle.
4. **No tiers** — a single flat, configurable percent per source.
5. **Accrual timing:** at the successful/terminal moment. Top-up cashback is posted when the top-up is confirmed (inside `TopUpWalletCoreAsync`, which both the counter path and the dcgate webhook funnel through). Shop cashback is posted when the order transitions to `delivered`. A cancellation before delivery means no cashback was ever granted, so nothing is reversed.
6. **Clawback on reversal:** if a *credited* source is later reversed, the cashback should be reversed too. This is a documented rule, but **no current flow triggers it** (a shop order cannot be cancelled after `delivered`; the player path has no reverse-a-confirmed-top-up flow), so the clawback wiring is **not implemented this cycle** (YAGNI). Revisit if a post-fulfillment reversal path is added.

## Architecture & Boundary

Thin layer over established patterns; nothing changes in enforcement (lock/kiosk/lease stay in `Agent.Service`). Accrual reuses the existing append-only ledger; configuration reuses the existing owner-scoped (`/api/owner/*`) settings pattern. Three sides:

- **Server (`AFK4.Platform.Api`)** — source of truth: org loyalty settings, accrual computation, the cashback ledger entries. Owner routes under `/api/owner/loyalty-settings`; player route under `/api/me/loyalty`.
- **Player shell (`AFK4.Player.Shell.Web`)** — `LoyaltyScreen`: informational — current rates, total earned, recent cashback credits. Cashback is already spendable as wallet balance, so there is no separate redeem flow.
- **Owner web (`AFK4.Operator.App.Web`)** — a loyalty-settings form (per-source toggle + percent), alongside the existing owner-scoped surfaces (payment gateways, owner code).

## Data Model

Follows project conventions: sealed classes, `Guid` keys, `DateTimeOffset *Utc`, configured in `PlatformDbContext.OnModelCreating`, migration named `YYYYMMDDHHMMSS_AddOrganizationLoyaltySettings`.

**`OrganizationLoyaltySettingsEntity`** — one row per organization (1:1):
- `OrganizationId: Guid` — PK (also the FK to the org)
- `TopUpEnabled: bool`
- `TopUpPercentBasisPoints: int` — 500 = 5% (basis points avoid floating-point money math)
- `ShopEnabled: bool`
- `ShopPercentBasisPoints: int`
- `UpdatedAtUtc: DateTimeOffset`

**No row = loyalty disabled** (the default). Reading settings for an org with no row yields all-disabled.

**Ledger:** add `LedgerEntryTypeNames.Cashback = "cashback"`, posted to `LedgerAccountTypeNames.Wallet` with a positive amount.

**Cashback amount:** `floor(sourceMinorUnits * percentBasisPoints / 10000)`, in the source's currency, rounded down. `CreatedByStaffUserId = Guid.Empty` (system-initiated). The `Reason` field carries a source tag for traceability (`cashback:topup`, `cashback:shop:{orderId}`). A zero computed amount (tiny source or 0%) posts no entry.

## Accrual Flow

1. **Top-up cashback.** Both the counter top-up and the dcgate webhook funnel through `EfBillingCommandService.TopUpWalletCoreAsync`. After the `TopUp` wallet entry is created (and within the same transaction/save), invoke the loyalty accrual: if the org has `TopUpEnabled`, compute the cashback on the top-up amount and add a `cashback` wallet entry. The cashback entry is not itself a top-up, so it triggers no recursion.
2. **Shop cashback.** In `EfShopOrderService.TransitionAsync`, when an order transitions `accepted → delivered`, if the org has `ShopEnabled`, compute cashback on the order total and add a `cashback` wallet entry before the transaction's `SaveChanges`, so it is atomic with the status change.
3. **Owner configuration.** `GET /api/owner/loyalty-settings` returns the current settings (or all-disabled defaults). `PUT /api/owner/loyalty-settings` upserts them. Both require `RequireOrganizationPermission(StaffPermissionNames.ManageLoyaltySettings)` (owner-only); the PUT validates percents are in `[0, 10000]` and writes an audit record.
4. **Player view.** `GET /api/me/loyalty` returns the enabled rates, the player's total cashback earned, and recent cashback credits (read from the ledger filtered by `EntryType == cashback`). Rate-limited `player-me`, `IPlayerContextAccessor`-guarded.

All accrual is server-authoritative; the shell never grants cashback locally.

## Components (plan units)

- **L-server (`AFK4.Platform.Api`):** `OrganizationLoyaltySettingsEntity` + EF config + migration; `LedgerEntryTypeNames.Cashback`; a `LoyaltyAccrualService` (look up org settings, compute cashback, add the wallet ledger entry for a given source); a hook in `TopUpWalletCoreAsync` (top-up source); a hook in the shop `delivered` transition (shop source); owner endpoints `GET`/`PUT /api/owner/loyalty-settings` (`RequireOrganizationPermission` + `ManageLoyaltySettings`, audit via `IAuditRecordWriter`); player endpoint `GET /api/me/loyalty` (rate-limited `player-me`, org/player-scoped).
- **Shared.Contracts:** `Loyalty/LoyaltySettingsDto`, `Loyalty/UpdateLoyaltySettingsRequest`, `Loyalty/PlayerLoyaltyDto` (rates + total earned + recent entries); `Identity/StaffPermissionNames.ManageLoyaltySettings = "loyalty.settings.manage"`; audit action name(s) for the settings update.
- **L-shell (`AFK4.Player.Shell.Web`):** DTO mirrors in `apiTypes.ts`; `shellApi.getLoyalty`; `screens/LoyaltyScreen.tsx` (current rates, total earned, recent cashback, "cashback goes straight to your wallet"); offline via `idbCache`; wired into `SelfServiceMenu`.
- **L-owner (`AFK4.Operator.App.Web`):** an owner loyalty-settings form (per-source toggle + percent input), its `operatorApiClients` methods, a nav entry, and i18n keys in `locales/{ru,en,tg}.json` regenerated via `bun run gen`.

## Error Handling & Edge Cases

- Org with no settings row → all sources disabled → no cashback, player sees "cashback off".
- Disabled source → no accrual for that source even if the other is enabled.
- Zero/sub-unit computed cashback → no ledger entry posted.
- Percent out of `[0, 10000]` on PUT → `400` validation error.
- Owner config endpoints require owner-only permission → `401/403` otherwise.
- Idempotency: both hooks are naturally once-only — top-up fulfillment is guarded (`intent.State != "fulfilled"` / open-shift), and the shop `delivered` transition is a one-way guarded status change. Cashback therefore posts exactly once per source event.
- Offline shell → loyalty served from `idbCache`; clearly marked as last-known.
- Server-authoritative throughout: the shell never marks cashback earned on its own.

## Testing Strategy

- **Server (xUnit):** top-up accrues correct cashback when enabled (correct percent, floor rounding); shop `deliver` accrues cashback; no accrual when the source is disabled or no settings row exists; cancel-before-deliver yields no cashback; owner `PUT` validates percent bounds and writes audit; owner config requires the owner permission (denied otherwise); player `GET /api/me/loyalty` returns rates + total earned + recent entries; org/player scoping.
- **Player shell (`bun test` + happy-dom):** renders enabled rates and total earned; hides a disabled source; offline path.
- **Owner web (`bun test` + happy-dom):** renders the form, saves toggles + percents, validates percent input.

## Out of Scope (this cycle)

- Loyalty tiers / status levels.
- Cashback on session time or package purchases.
- Points currency with an exchange rate / manual redemption (cashback is plain wallet money).
- Active clawback wiring on source reversal (documented rule; no triggering flow exists this cycle).
- Per-branch override of the org-wide rates.
- News/banners (separate Unit F cycle).
