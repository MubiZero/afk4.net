# Unit F — Shop (snacks/drinks to seat) Design

**Date:** 2026-06-09
**Status:** Approved (design), pending implementation plan
**Epic:** Customer-shell WebView2 pivot — Unit F (engagement/commerce layer). Unit F = three independent subsystems (shop, loyalty, news). This spec covers **Shop only**; loyalty and news get their own spec→plan→implementation cycles later.

## Goal

Let a seated player order snacks/drinks from the WebView2 React shell, pay from their wallet balance, and have an operator receive the order in a live queue and mark it delivered — a smartshell.gg-class self-service flow. Server is authoritative for catalog, money, stock, and order status; the shell and the operator UI are non-authoritative faces.

## Decisions (locked during brainstorming)

1. **Payment:** debit from the player's wallet balance (ledger) at order placement. Insufficient balance → block placement and route the player to the existing top-up flow.
2. **Catalog:** reuse the existing POS catalog (`PosProductEntity`); add a per-product `AvailableInShell` flag. Players see only `AvailableInShell && in-stock` products for their branch.
3. **Operator side is in scope this cycle** as a React queue workspace in `AFK4.Operator.App.Web` (operator app is a WebView2 host + React, builds/tests on Linux via bun — not WPF).
4. **Order lifecycle:** debit at placement; states `placed → accepted → delivered`, plus `cancelled`. Cancellation: operator any time before `delivered`; player only while `placed` (before `accepted`). Cancel reverses the ledger debit and restores stock.
5. **Accounting:** standalone — `ShopOrder` with its own ledger entries and its own stock movements. Reports read shop orders separately; **not** coupled to `PosSale` (avoids the staff-user requirement and keeps the shell flow decoupled from counter sales).
6. **Realtime:** the player shell polls `GET /api/me/shop/orders` (the shell has no SignalR client to `Platform.Api`, consistent with the existing top-up poll). The operator gets live SignalR push via the existing `DeviceHub` (already wired in the operator web).

## Architecture & Boundary

Thin layer over established patterns; nothing changes in enforcement (lock/kiosk/lease stay in `Agent.Service`). Three sides:

- **Server (`AFK4.Platform.Api`)** — source of truth: catalog, orders, money (ledger), stock, status transitions. Exposes player routes under `/api/me/shop/*` and operator routes under `/api/branches/{branchId}/shop/*`.
- **Player shell (`AFK4.Player.Shell.Web`)** — `ShopScreen`: catalog → cart → place order → status (poll) → cancel-while-placed. Reuses the native-token transport already built (token never reaches JS).
- **Operator web (`AFK4.Operator.App.Web`)** — a React "shop orders" queue workspace: live list via `DeviceHub`, buttons accept / deliver / cancel.

**Seat/session rule:** ordering requires an **active session** for the player. The seat label is resolved server-side from the player's active session; the player never types a seat. With no active session the shop is hidden/disabled in the shell.

## Data Model (standalone, not PosSale)

All entities follow project conventions: sealed classes, `Guid` PKs, `OrganizationId` + `BranchId` multi-tenant columns, `DateTimeOffset *Utc` timestamps, configured in `PlatformDbContext.OnModelCreating`, migration named `YYYYMMDDHHMMSS_AddShopOrders`.

**`ShopOrderEntity`**
- `Id: Guid`
- `OrganizationId: Guid`, `BranchId: Guid`
- `PlayerAccountId: Guid`
- `SessionId: Guid` — the active session the order was placed from
- `SeatLabel: string` — resolved from the session at placement (snapshot)
- `Status: string` — `placed | accepted | delivered | cancelled`
- `TotalMinor: long`, `Currency: string`
- `PlacedAtUtc: DateTimeOffset`
- `AcceptedAtUtc: DateTimeOffset?`, `DeliveredAtUtc: DateTimeOffset?`, `CancelledAtUtc: DateTimeOffset?`
- `CancelReason: string?`
- `RowVersion` — optimistic concurrency token (same approach as session extend's `ExpectedVersion`)

**`ShopOrderLineEntity`**
- `Id: Guid`, `ShopOrderId: Guid`
- `PosProductId: Guid`
- `NameSnapshot: string`, `UnitPriceMinor: long`, `Quantity: int`, `LineTotalMinor: long`
  (name + unit price are snapshotted at placement so later catalog edits don't change history)

**`PosProductEntity`** — add `AvailableInShell: bool` (default `false`) + migration.

**Money & stock reuse existing primitives:**
- Wallet debit/refund via `LedgerEntryEntity` (the same account/entry-type pattern wallet top-up uses; balance read via `LedgerBalanceProjector.GetWalletSummaryAsync`).
- Stock decrement/restore via `StockMovementEntity` (the same pattern POS uses), tagged to the shop order.

## Order Flow & Money

1. **Place** (`POST /api/me/shop/orders`): server validates the player has an active session, every line product is `AvailableInShell` and in stock, and the wallet balance covers the total.
   - Insufficient funds → `409 insufficient_funds` (shell offers top-up).
   - Out of stock between display and order → `409 out_of_stock`.
   - On success, in a single transaction: debit ledger + decrement stock + create `ShopOrder` (`placed`) with line snapshots + emit SignalR `ShopOrderCreated` to the branch group.
2. **Accept** (`POST /api/branches/{branchId}/shop/orders/{id}/accept`): `placed → accepted`; SignalR `ShopOrderUpdated`.
3. **Deliver** (`POST .../{id}/deliver`): `accepted → delivered`; SignalR `ShopOrderUpdated`.
4. **Cancel** (operator `POST .../{id}/cancel`; player `POST /api/me/shop/orders/{id}/cancel`): allowed per the lifecycle rule above; reverses ledger debit + restores stock + `cancelled`; SignalR `ShopOrderUpdated`.
5. **Player status:** `GET /api/me/shop/orders` returns the player's recent orders with current status (polled by the shell, like top-up intents).

All state transitions are server-authoritative; the shell never infers paid/delivered locally.

## Components (plan units)

- **S-server (`AFK4.Platform.Api`):** `AvailableInShell` flag + migration (and the existing POS product create/update endpoint accepts it); `ShopOrderEntity`/`ShopOrderLineEntity` + EF config + migration; a shop-order service (place / accept / deliver / cancel, each wrapping ledger + stock); player endpoints `GET /api/me/shop/catalog`, `POST /api/me/shop/orders`, `GET /api/me/shop/orders`, `POST /api/me/shop/orders/{id}/cancel` (rate-limited `player-me`, `IPlayerContextAccessor`-guarded, org/branch-scoped); operator endpoints `GET /api/branches/{branchId}/shop/orders`, `accept`/`deliver`/`cancel` (staff permission via `StaffAuthorizationService` + audit via `IAuditRecordWriter`); SignalR `ShopOrderCreated`/`ShopOrderUpdated` to `DeviceHubGroups.Branch`.
- **S-shell (`AFK4.Player.Shell.Web`):** DTO mirrors in `apiTypes.ts`; `shellApi.ts` methods `listShopCatalog` / `placeShopOrder` / `listShopOrders` / `cancelShopOrder`; `screens/ShopScreen.tsx` (catalog grid, cart, place, insufficient-balance → top-up, order status poll + cancel-while-placed); offline catalog via `idbCache`; wired into `SelfServiceMenu`.
- **S-operator (`AFK4.Operator.App.Web`):** `operatorApiClients.ts` shop-queue methods; a shop-orders queue workspace (live via `DeviceHub` subscription + accept/deliver/cancel); a nav entry; plus the `AvailableInShell` toggle added to the existing `BackendPosWorkspace` product editor.

## Error Handling & Edge Cases

- No active session → shop hidden/disabled in the shell.
- Insufficient balance → `409 insufficient_funds`; shell offers a soft transition to top-up.
- Item sold out between display and order → `409 out_of_stock`.
- Accept/cancel race → optimistic concurrency on `RowVersion` → `409` (same reconcile UX as session extend).
- Offline → catalog served from `idbCache`; placement disabled with a clear message.
- Server-authoritative throughout: the shell never marks an order paid or delivered on its own.

## Testing Strategy

- **Server (xUnit):** place happy-path; `409` for insufficient funds and out-of-stock; status transitions; cancel reverses both ledger and stock; org/branch scoping; staff-permission enforcement on operator routes; optimistic-concurrency conflict.
- **Player shell (`bun test` + happy-dom):** catalog render, cart, placement, insufficient-balance branch → top-up, status polling, cancel-while-placed.
- **Operator web (`bun test` + happy-dom):** queue render, accept/deliver/cancel, live update on a simulated `ShopOrderUpdated` event.

## Out of Scope (this cycle)

- Loyalty/cashback and news/banners (separate Unit F cycles).
- No new operator catalog screen: the `AvailableInShell` flag is exposed as a toggle inside the existing operator POS product editor (`BackendPosWorkspace`) and its backing product create/update endpoint. That small addition is part of S-operator; everything else about POS catalog management stays as-is.
- Pay-on-delivery / cash, dcgate-per-order (wallet debit only this cycle).
- Coupling shop orders into `PosSale`/shift Z-reports (standalone accounting by decision #5).
