# Shift Revenue Screen — Design

- **Date:** 2026-06-10
- **Status:** Approved (design), pending implementation plan
- **Area:** Operator app (AFK4.Operator.App.Web) + Platform API reports

## Problem & Why

The operator dashboard shows "today's revenue" sliced by a **UTC** calendar day. The business runs in Tajikistan (UTC+5), so the day boundary falls at 05:00 local — a night club's after-midnight revenue lands in the wrong day. Rather than patch the UTC boundary, we move the **default operational revenue view to shifts** (open → close), which is the unit a cashier is actually accountable for and is inherently timezone-immune (it is an interval, not a wall-clock day). Calendar-day/trend views remain for later and will use the new per-branch `PreferredTimeZone` field.

This is not a new data-collection effort: every money movement is already tagged with `ShiftId`. We add a new **aggregate** and a new **screen** over existing data.

## Goals

- A "Shifts" screen in the operator web app showing, for the **current open shift** and a **history of recent closed shifts**:
  - **Earned** (consumption): session time + POS goods.
  - **Inflow** (live money in): by payment method — cash / non-cash / wallet top-ups.
  - **Cash reconciliation**: expected / counted / difference (reuse existing logic).
- Backend aggregate computed from data already tagged by `ShiftId`.

## Non-Goals (YAGNI)

- Charts / trends / time-series.
- CSV export (existing `ReportCsvExporter` can be extended later).
- Multi-branch roll-up.
- Calendar-day aggregates by branch timezone (the `PreferredTimeZone` field exists; wiring day-boundary trends to it is a separate follow-up).

## Definitions (the core of this spec)

Three distinct concepts, kept separate:

1. **Earned** — value the business actually delivered during the shift, regardless of how/when paid:
   - **Time** = `−Σ GameplayCharge.amount + Σ PostpaidDebt.amount` for the shift. `GameplayCharge` is stored as a **negative** ledger amount (wallet debit) — negate it to get positive earnings; `PostpaidDebt` is stored positive (played on credit) and is also earned time. (`PackageConsumption` carries `amount = 0` — package money is recognized at purchase, not consumption — so it is **not** counted here.)
   - **Goods** = `Σ` POS sales for the shift (paid sales, minus refunds).
2. **Inflow** — live money that physically arrived during the shift. Two separate lines (deliberately not one "method" axis, because top-ups don't store their method and live payments do):
   - **Direct payments: cash / non-cash** = `Payments` (`PaymentKind = payment` minus `refund`) grouped by `PaymentMethod`: `cash` → cash, `card_manual` → non-cash. `wallet` payments are **excluded** — paying from an existing wallet balance is an internal transfer; that money already arrived at top-up time, so counting it would double-count.
   - **Wallet top-ups** = ledger `TopUp` for the shift, shown as a single "Пополнения кошелька" line **without** a cash/non-cash split (the `TopUp` ledger entry does not carry a payment method, and online vs in-cash top-ups go through different paths). This is the line the original mockup labeled "кошелёк" in inflow — relabeled to be semantically correct.
3. **Cash reconciliation** — unchanged existing computation (`StartingCash + cashMovements + posCashPayments + posRefunds + billingCashImpact` → `expected`; `counted - expected` → `difference`).

### Resolved open questions

- **Bonuses (`BonusConsumption`)** — `amount = 0` ledger entries (tracked in seconds, not money), excluded from monetary Earned. Out of scope for MVP.
- **Packages (`PackagePurchase` / `PackageConsumption`)** — out of scope for MVP. Package money is recognized at purchase; `PackageConsumption` is `amount = 0`. A later iteration can add a "packages" earned line from `PackagePurchase`.
- **Postpaid (`PostpaidDebt` / `DebtPayment`)** — `PostpaidDebt` (played on credit) IS earned time and is included in Earned. `DebtPayment` (later repayment) is a ledger entry, not a `Payment`, so it does not appear in the Inflow direct-payments line; for MVP it is not separately surfaced. *Implementation note: keep this explicit in tests so the boundary is intentional.*

All amounts are in the shift's `CurrencyCode` (reuse the existing `IsCurrency` filter); mixed-currency movements outside the shift currency are ignored, matching current `GetShiftReportAsync` behavior.

## Architecture

### Backend (AFK4.Platform.Api)

- New method `GetShiftRevenueAsync(organizationId, branchId, query)` on `IReportService` / `EfReportService`, alongside the existing `GetShiftReportAsync`. It reuses the same source queries (Shifts, Payments, LedgerEntries, PosSales, CashMovements scoped by `ShiftId`).
- New DTOs in `AFK4.Shared.Contracts/Shifts`:
  - `ShiftRevenueDto` — shift meta (id, opener/closer, opened/closed, state) + `EarnedBreakdownDto` (time, goods, total) + `InflowBreakdownDto` (cash, nonCash, walletTopUps, total) + `CashReconciliationDto` (starting, expected, counted?, difference?).
  - `ShiftRevenueListDto` — `IReadOnlyList<ShiftRevenueDto>` + limit (history of recent shifts).
- New endpoints in `ShiftEndpoints.cs`, guarded by existing `ViewShift` permission:
  - `GET /api/branches/{branchId}/shifts/revenue/current` → current open shift's `ShiftRevenueDto` (404 if none open).
  - `GET /api/branches/{branchId}/shifts/revenue` → `ShiftRevenueListDto` (recent closed shifts, `OpenedAtUtc desc`, `limit`).
- `TimeProvider` injected per existing convention (no raw `DateTimeOffset.UtcNow`).

### Frontend (AFK4.Operator.App.Web)

- New `ShiftsWorkspace.tsx` following `NewsWorkspace.tsx` conventions (api client in `operatorApiClients`, `useI18n`, workspace shell).
- Layout per approved mockup: current shift card on top (Earned → time/goods; Inflow → cash/non-cash/wallet; Cash recon), history list below (date, staff, earned total, cash ✓/difference).
- Navigation entry + permission wiring (mirrors how `NewsWorkspace` was added).
- i18n keys for ru / en / tg.

## Data Flow

1. Operator opens the Shifts screen → React calls `…/shifts/revenue/current` and `…/shifts/revenue`.
2. `EfReportService` loads shift(s) by `ShiftId`, pulls the tagged Payments / LedgerEntries / PosSales / CashMovements, computes Earned / Inflow / Cash per the definitions above, returns DTOs.
3. React renders the two-section screen.

## Error Handling & Edge Cases

- **No open shift:** `current` returns 404; UI shows an empty "no open shift" state.
- **Open vs closed shift:** `counted`/`difference` are null while open (UI shows "—"), populated after close (reuse existing nullability from `ShiftReportRow`).
- **Empty shift:** all totals zero, renders cleanly.
- **Currency:** values in the shift currency; non-matching movements ignored.
- **Authorization:** `ViewShift` required; unauthenticated → 401, unauthorized → 403, matching sibling shift endpoints.

## Testing

- **Backend (xUnit, pattern of existing report tests):** `GetShiftRevenueAsync` — earned from `GameplayCharge`+`PackageConsumption`+POS; inflow grouped by method incl. top-ups; no double-count of wallet-funded play; postpaid debt payment lands in inflow; open vs closed nullability; empty shift; currency filtering.
- **Frontend (component test, like `NewsWorkspace.test.tsx`):** renders current-shift breakdown blocks and history rows; "no open shift" empty state; money formatting.

## Build / Verify Gates

- `dotnet build` + `dotnet test tests/AFK4.Platform.Api.Tests` green.
- Operator web: `bun test` + `bun run build` (tsc typecheck) green.
