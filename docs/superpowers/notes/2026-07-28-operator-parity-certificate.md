# Operator parity certificate for Platform.Web `/club`

Date: 2026-07-28

## Verdict

**GO** for planning the separate `/club` removal sub-project. The removal itself is not part of this change. The repeated static audit found no uncovered mandatory capability in Clients, Monetization, Settings, or Venue.

## Capability matrix

| `/club` domain and capability | Operator location | Permission | Evidence | Outcome |
|---|---|---|---|---|
| Clients: create/search/profile, activate/deactivate, inactive debtor | `Клиенты` table and drawer | `players.view`, `players.create` | `BackendPlayersWorkspace`, `playersModel`, `ClientDrawer` tests | covered |
| Clients: wallet/debt, account-aware corrections, ledger detail, partial refund | client drawer money/history sections | billing and matching money permissions | `CorrectionModal`, `LedgerRow`, `RefundModal` tests | operator wider |
| Clients: acquired packages | client drawer package section | `packages.view` | `PackagesSection` and `ClientDrawer` tests | covered |
| Monetization: wallet-backed package sale | `Касса`, selected-client block | `packages.purchase` plus open shift | `PackagePurchasePanel`, `BackendPosWorkspace` tests | covered |
| Monetization: tariffs and package definitions | `Управление → Тарифы и пакеты` | `tariffs.manage`, `packages.manage` | `TariffsTab`, `PackagesTab`, `TariffsPackagesDestination` tests | covered |
| Monetization: package load failure and retry | Packages inner tab | package access permission | `ManagementWorkspace` and `TariffsPackagesDestination` failure tests | covered |
| Settings: staff multi-role invite/edit and self-deactivation guard | `Управление → Сотрудники` | staff and role management permissions | `StaffRolesDestination` tests | covered |
| Settings: product category reuse/change, catalog lifecycle and barcodes | `Управление → Товары`; `Склад` for stock | `pos.catalog.manage`, inventory permissions | `categoryModel`, `GoodsDestination`, barcode tests | operator wider |
| Settings: branch profile, payments and loyalty | `Управление → Клуб`; `Платежи и лояльность` | corresponding branch/payment/loyalty permissions | destination component tests | covered |
| Venue: zones/seats and device assignment/detail | `Управление → Залы и ПК` | `layout.manage`, device view/assignment permissions | `ZonesTab`, `DevicesTab`, destination tests | covered |
| Venue: device rename/remove | device drawer | `devices.seat_assignment.assign`, `devices.credentials.revoke` | API route/body and `DevicesTab` lifecycle tests | covered |
| Venue: day-to-day lock/unlock and session control | floor map | device/session action permissions | Map/App integration tests | operator wider |

## Approved descope

- device approval, pending approve/reject, and `requireManualDeviceApproval`;
- drag-and-drop or bulk floor-map editor;
- per-entity currency selection; currency remains branch-level;
- broad conversion of all transport `Record<string, unknown>` types;
- unrelated Operator redesign;
- deletion of Platform.Web `/club` in this change.

## Fresh verification

- Operator Web full suite: `1017 pass`, `26 skip`, `0 fail` across 161 files.
- App integration: `68 pass`, `26 skip`, `0 fail`.
- Operator Web production build: exit 0. Existing non-failing SignalR annotation and large-chunk warnings remain.
- i18n: `39 pass`, `0 fail`, including ru/en/tg parity and no silent Russian copies in Tajik.
- Focused RED→GREEN suites cover multi-role, inactive debt, correction/refund, package list/purchase, category reuse, independent package error state, and device rename/remove.

## Removal gate

The next sub-project may remove Platform.Web `/club` only with its own route/import cleanup, browser build/tests, and confirmation that no Control Plane owner/support surface imports club-only code. This certificate establishes functional parity; it does not authorize deployment or deletion by itself.
