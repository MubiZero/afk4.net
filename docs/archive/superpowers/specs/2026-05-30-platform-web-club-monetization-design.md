# Club Console — Монетизация (Monetization) Design

**Date:** 2026-05-30
**Sub-project:** 2 (full `/club/*` owner console) — block 5 of the implementation sequence.
**Status:** Approved design; to be implemented as **three sequential plans** (Тарифы → Товары → Лояльность), each its own spec-derived TDD plan + local merge.

---

## Goal

Give club owners a **Монетизация** screen to manage what they sell: gameplay **Тарифы** (per-minute pricing), POS **Товары** (catalog), and **Лояльность** (prepaid time packages). Branch-scoped, on the active branch. All on **existing backend endpoints** — no new backend contracts; we add new `clubApi` wrappers + TS types only.

## Non-goals

- No new backend endpoints/migrations. We live within current CRUD coverage (see Backend Reality).
- No hard delete anywhere (backend exposes none) → deactivation via `IsActive`.
- No stock-movement / inventory-adjustment UI (separate `inventory.stock.manage` concern, deferred).
- No player-side package purchase/consume from this admin screen (player-initiated, out of scope).
- No spatial/visual layout (that's Карта зала, already shipped).

---

## Backend Reality (ground truth, verified 2026-05-30)

All routes are branch-scoped under `/api/branches/{branchId}/...`; auth via `RequireOrganizationPermission(...)`. JSON is camelCase (matches existing `types.ts`). Money is in **minor units** (long; e.g. kopecks): `MoneyDto { currencyCode, minorUnits }`, and tariff prices as `pricePerMinuteMinorUnits`. Create requests carry an `idempotencyKey` (frontend generates one per create via `crypto.randomUUID()`).

### Тарифы — `tariffs.view` / `tariffs.manage`
| Method | Route | Purpose |
|---|---|---|
| GET | `/tariffs/options` | list **active versions** → `TariffOptionDto[]` (tariffId, tariffVersionId, name, currencyCode, pricePerMinuteMinorUnits, minimumBillableMinutes, roundingIncrementMinutes, versionNumber, effectiveFromUtc) |
| POST | `/tariffs` | create base tariff → `TariffDto` (request: organizationId, name, idempotencyKey) |
| POST | `/tariffs/{tariffId}/versions` | create version → `TariffVersionDto` (currencyCode, pricePerMinuteMinorUnits, minimumBillableMinutes, roundingIncrementMinutes, effectiveFromUtc, idempotencyKey) |
| PATCH | `/tariffs/{tariffId}` | update name/active → `TariffDto` (organizationId, name, isActive) |
| PATCH | `/tariffs/{tariffId}/versions/{versionId}` | update version → `TariffVersionDto` |
| POST | `/tariffs/calculate` | price preview → `TariffCalculationResult` (request: organizationId, tariffVersionId, durationMinutes) |

**Gaps:** no get-by-id, no list-all (only active `/options`), no delete.

### Товары — `inventory.view` / `pos.catalog.manage`
| Method | Route | Purpose |
|---|---|---|
| GET | `/pos/catalog` | list products → `PosProductDto[]` (productId, categoryId, name, sku, price:Money, trackStock, allowNegativeStock, isActive, stockOnHand) |
| POST | `/pos/categories` | create category → `PosProductCategoryDto` (categoryId, name, isActive) |
| POST | `/pos/products` | create product → `PosProductDto` |
| PATCH | `/pos/products/{productId}` | update product → `PosProductDto` (categoryId, name, sku, price, trackStock, allowNegativeStock, isActive) |

**Gaps:** no category list/update/delete; no product delete (soft via `isActive`). If `PosProductDto` carries no `categoryName`, categories are referenced by `categoryId` (see "Категории" decision).

### Лояльность (пакеты) — `packages.view` / `packages.manage`
| Method | Route | Purpose |
|---|---|---|
| GET | `/packages/options` | list **active** → `PackageOptionDto[]` (packageDefinitionId, name, currencyCode, priceMinorUnits, includedSeconds, bonusSeconds, expiresAfterDays) |
| POST | `/packages` | create → `PackageDefinitionDto` (name, price:Money, includedSeconds, bonusSeconds, expiresAfterDays, idempotencyKey) |
| PATCH | `/packages/{packageDefinitionId}` | update → `PackageDefinitionDto` (+ isActive) |

**Gaps:** no get-by-id, no list-all (only active `/options`), no delete.

> Exact TS field names are confirmed against the C# contracts under `src/AFK4.Shared.Contracts/` and re-verified per plan when the wrappers/types are written.

---

## Cross-cutting decisions

1. **Money in minor units.** A pure `src/club/money.ts` helper module: `minorToMajor(minorUnits)` (÷100), `majorToMinor(major)` (×100, rounded), reused everywhere prices are shown/entered. Display via existing `formatCurrency(major, currencyCode)`. Unit-tested for rounding (e.g. `12345 → 123.45`, `99.99 → 9999`). Seconds rendered as hours/minutes via a small `formatDuration` helper.
2. **No hard delete → deactivate.** Editing exposes an `isActive` toggle; turning it off is the "remove" affordance, confirmed via `ConfirmDialog`, server-confirmed (no optimistic success), toast on result.
3. **Active-only lists, labelled.** Тарифы and Лояльность read only `/options` (active items). Under each list, a muted note: deactivated items leave the list because the backend exposes no list-all endpoint. (Same honest-gap rule used for branch add/deactivate.)
4. **Idempotency.** Each create generates `idempotencyKey: crypto.randomUUID()` once per submit attempt.
5. **Role gating.** Owner + branch_manager → manage; other roles → read-only (lists visible, create/edit hidden) keyed off the `*.manage` permission threaded from the session, exactly like Карта зала's `canManageLayout`.
6. **Reused patterns (Plans 1–4).** `use*` hook returning a discriminated-union state (`loading|error|ready` + `retry`, `useRef` client, deps `[branchId, tick]`); pure view-model builder; presentational tab; create/edit via `Dialog`; deactivate via `ConfirmDialog`; `toast.saved`/`toast.failed`; `LoadingCards`/`ErrorState`/`EmptyState`.

---

## Screen architecture

`src/club/monetization/MonetizationScreen.tsx` — a `Tabs` shell (`Тарифы` / `Товары` / `Лояльность`), branch-scoped via the active branch, taking `{ client, branchId, organizationId, canManageTariffs, canManageCatalog, canManagePackages }`. Each tab is a self-contained feature folder. Wired into `App.tsx` `ClubArea` on the existing `clubMonetization` route (nav item already present; flip `soon:false`), threading `organizationId` and the three `*.manage` permission flags from `session.permissions`.

### Tab Тарифы (`src/club/monetization/tariffs/`)
- `tariffsModel.ts` (pure): map `TariffOptionDto[]` → display rows (name, price/min in major units, min. billable minutes, rounding, currency, effective-from); build `CreateTariffRequest` / `CreateTariffVersionRequest` / `UpdateTariff*` from form state (major→minor).
- `useTariffs.ts`: load `getTariffOptions`; `create` (POST tariff → POST first version, server-confirmed two-step), `updateTariff`, `updateVersion`; `tick` reload.
- `TariffsTab.tsx`: table of active tariffs; "Создать тариф" dialog (name + version pricing); edit dialog (name/active + version pricing); optional **price calculator** block (`calculateTariff(versionId, durationMinutes)` → preview); active-only note.

### Tab Товары (`src/club/monetization/catalog/`)
- `catalogModel.ts` (pure): group `PosProductDto[]` by category; derive the category list from the catalog (`categoryId` + name if present) merged with categories created this session; build `CreateProduct*` / `UpdateProductRequest`.
- `useCatalog.ts`: load `getCatalog`; `createCategory`, `createProduct`, `updateProduct` (incl. `isActive` deactivate); session-scoped category-name map; `tick` reload.
- `CatalogTab.tsx`: products grouped by category; "Создать категорию" + "Создать товар" dialogs (category select from derived list, name, SKU, price, trackStock, allowNegativeStock); edit-product dialog (+ deactivate via ConfirmDialog); **категории note**: categories are create-only and can't be renamed/deleted (backend gap).
- **Категории decision:** prefer `categoryName` if the catalog DTO carries it; otherwise label by a session name-map, falling back to a short `categoryId`. Document the limitation in-UI.

### Tab Лояльность (`src/club/monetization/packages/`)
- `packagesModel.ts` (pure): map `PackageOptionDto[]` → rows (name, price major, included/bonus time as h/m, expires-after days); build `CreatePackageDefinitionRequest` / `UpdatePackageDefinitionRequest`.
- `usePackages.ts`: load `getPackageOptions`; `create`, `update` (+ `isActive` deactivate); `tick` reload.
- `PackagesTab.tsx`: table of active packages; create/edit dialogs; deactivate via ConfirmDialog; active-only note.

### clubApi wrappers (added across the plans, all via `this.send<T>`)
`getTariffOptions`, `createTariff`, `createTariffVersion`, `updateTariff`, `updateTariffVersion`, `calculateTariff`; `getCatalog`, `createProductCategory`, `createProduct`, `updateProduct`; `getPackageOptions`, `createPackageDefinition`, `updatePackageDefinition`. Plus the corresponding camelCase TS types in `types.ts`.

---

## Data flow

Session → `ClubArea` passes `branchId` (active branch), `organizationId`, and `canManage*` flags → `MonetizationScreen` → tab hook loads via `clubApi` GET (`/options` or `/catalog`) → pure model maps to view rows (minor→major) → user edits in a `Dialog` → submit builds a request (major→minor, `idempotencyKey`) → `clubApi` POST/PATCH → on success `tick` reload + `toast.saved`; on failure `toast.failed`; deactivation goes through `ConfirmDialog`.

## Error handling

`PlatformApiError.status` drives messaging: load failure → `ErrorState` + retry; mutation failure → `toast.failed` (the dialog stays open so the user can retry). 403 (insufficient permission) shouldn't occur because the UI gates on `*.manage`, but a failed mutation still surfaces as a toast rather than a crash. Empty lists → `EmptyState`.

## Testing

Per established conventions (Vitest `globals: false`; import `it`/`expect`/`vi` from `'vitest'`): pure model + money helpers fully unit-tested (mapping, request-building, rounding); each hook tested with a fake client (load/create/update/deactivate, error paths); each tab tested with `I18nProvider`+`ToastProvider` (renders rows, opens dialog, submits → wrapper called with expected payload incl. `organizationId`, read-only hides manage controls). i18n keys added with ru/en parity (parity test enforces it). Build gate `tsc -b && vite build` per plan.

---

## Decomposition into plans

Three independent areas → three sequential plans, each a full TDD cycle + local merge to `main`:

1. **План 5a — Тарифы:** `money.ts` helper (shared, lands here first), TS types + tariff clubApi wrappers, `tariffsModel`/`useTariffs`/`TariffsTab`, `MonetizationScreen` shell with the Тарифы tab, route + nav wiring (`clubMonetization`, `soon:false`), role gating. (Товары/Лояльность tabs render a temporary "soon" placeholder until their plans land.)
2. **План 5b — Товары:** catalog types + wrappers, `catalogModel`/`useCatalog`/`CatalogTab`, replace the Товары placeholder; category limitation handling.
3. **План 5c — Лояльность:** package types + wrappers, `packagesModel`/`usePackages`/`PackagesTab`, replace the Лояльность placeholder.

Each plan is green on the full suite + build before merge. After 5c the screen is complete; ClubDashboard deletion remains gated on the later Установка redesign (plan 7).

---

## Open limitations (carried forward honestly)

- Тарифы/Лояльность lists show **active items only** (no list-all endpoint).
- **Категории** are create-only (no rename/delete/list endpoint); referenced by id+session-name.
- No hard delete; deactivation is the removal affordance.
- Calculator on Тарифы is a convenience preview, not a saved artifact.
