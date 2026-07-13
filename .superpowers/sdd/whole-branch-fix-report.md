# Whole-Branch Important Findings Fix Report

Date: 2026-07-13
Branch: `feat/commerce-financial-integrity-impl`
Base reviewed: `ddf2c875`

## Result

All five accepted Important findings are implemented with focused RED/GREEN
regressions. Shop, POS, Inventory, Session checkout, reporting, and migration
boundaries remain explicit; no Player Shop path creates a fake `StaffUser`.

## Finding Evidence

1. **Session checkout inventory cost**
   - RED: attached sale expected immutable unit cost `175`, but checkout wrote
     retail unit price `500`.
   - Fix: checkout sale movements now use `PosSaleLineEntity.UnitCostMinorUnits`.
   - GREEN: checkout sale and subsequent refund movements both use `175`.

2. **Immutable stock-tracking decision**
   - RED: a tracked sale became non-refundable after the current product's
     `TrackStock` flag was disabled.
   - Fix: the persisted sale-line snapshot is explicitly named `TracksStock`;
     migration `SnapshotSaleLineStockTracking` renames the existing column and
     preserves historical values. Refunds use only this sale-line snapshot.
   - GREEN: a tracked-at-sale line still restores stock after the product toggle;
     untracked-at-sale lines remain excluded from stock mutation.

3. **Currency-safe inventory reconciliation**
   - RED: product currency could change after inventory history, and legacy
     currency mismatch reached refund reconciliation.
   - Fix: product currency changes return stable
     `product_currency_immutable` once stock or sale-line history exists. Inbound
     reconciliation now receives the snapshot currency; both POS refund paths
     validate it and return `inventory_currency_mismatch` before staging finance.
   - GREEN: matching-currency historical refunds work after tracking toggles;
     mismatched legacy state leaves sale, payment, receipt, and movement state
     unchanged.

4. **Player Shop actor identity**
   - PRD and architecture were updated before implementation.
   - Player-initiated placement/cancellation uses reserved synthetic actor
     `00000000-0000-4000-8000-000000000004` (`Player Shop`) instead of
     `Guid.Empty`. It is not a staff FK or `StaffUser` row.
   - `PosSale.PlayerAccountId` remains the human initiator. Owner/operator report
     name resolution displays `Player Shop` deterministically instead of a GUID
     fragment.

5. **Accept/cancel concurrency translation**
   - RED: deterministic cancellation save conflict escaped as
     `DbUpdateConcurrencyException`; accept returned no current version.
   - Fix: cancellation clears staged mutations and returns `version_conflict`
     with current version; accept clears its failed mutation and reloads the
     current version. Relational transactions still roll back before translation.
   - GREEN: deterministic regressions prove no added/modified tracker entries and
     unchanged payment, receipt, ledger, and stock-movement counts.

## Fresh Verification

- Focused RED/GREEN tests: each finding failed for the expected pre-fix reason,
  then passed after its minimal implementation.
- Affected Platform suites:
  `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal --filter "Shop|Pos|Inventory|SessionCheckout|Report|BillingShiftIntegrationTests|PlatformDbContextDesignTimeFactoryTests"`
  — 341 passed, 1 skipped, 0 failed. The skip is the explicit
  `AFK4_COMMERCE_TEST_POSTGRES` environment gate.
- Shared contracts:
  `dotnet test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal`
  — 125 passed, 0 failed.
- Complete Platform API:
  `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --no-build -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal`
  — 1299 passed, 1 skipped, 0 failed.
- Full solution build:
  `dotnet build AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -p:EnableWindowsTargeting=true -v minimal`
  — succeeded with 0 warnings and 0 errors.
- `git diff --check` — clean.

## Self-Review

- No retail-price-as-cost assignment remains in session checkout.
- New/refund stock mutations use immutable sale-line `TracksStock`, cost, and
  currency snapshots.
- Player Shop still delegates wallet, payment, receipt, and stock writes through
  Billing/POS/Inventory boundaries.
- Concurrency translation does not publish notifications or retain staged finance.
- Progress truth was updated; production-readiness roadmap did not require a
  change because no release gate changed.
