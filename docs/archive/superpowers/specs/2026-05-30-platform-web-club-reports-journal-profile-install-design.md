# Club Console Block 7 — Reports + Journal + Profile + Install (Design)

**Date:** 2026-05-30
**Scope:** Final block of sub-project 2 (full `/club/*` owner console redesign) for `src/AFK4.Platform.Web`.
**Outcome:** After this block, `ClubDashboard.tsx` (`LegacyClubScreen` + embedded `InstallScreen` + `DashboardHome`) can be deleted and the dead legacy branch-detail routes retired.

---

## 1. Goal

Build four areas of the club-owner console on the established modern (shadcn/ui) design system with RU/EN i18n:

1. **Reports** (`/club/reports`) — 5 detailed financial/operational reports with date range, summary totals, tables, and CSV export.
2. **Journal** (`/club/journal`) — audit event log with filters.
3. **Profile** (`/club/profile`) — read-only identity/permissions view.
4. **Install** (`/club/install`) — redesigned owner-code + setup-wizard screen, replacing the legacy embedded one.

"Аналитика" (dashboard KPIs) is already served by the existing **Overview** screen (`/club`); no new analytics work.

## 2. Decomposition (2 plans)

- **Plan 7a — Reports + Journal**
- **Plan 7b — Profile + Install + delete legacy** (ports Install, retires dead branch-detail routes, deletes `ClubDashboard.tsx`)

Each plan: fresh design→spec→plan→implementation cycle, merged to `main` locally with `--no-ff` (not pushed). Executed via subagent-driven development (TDD).

## 3. Architecture (shared)

Follow the established club-console feature shape:
- **Pure model module** (view-model builders; no React). Money minor→major, seconds→minutes, date formatting, label/variant lookups.
- **Load-only `use*` hook**: discriminated union `{ status: 'loading' } | { status: 'error'; retry } | { status: 'ready'; …; retry }`, `useRef` client, `cancelled` flag, deps `[…, tick]`, `retry` = `setTick(t => t+1)`. **Error-check before loading/null-check** in the return guard (lesson from 6a `useWalletSummary`).
- **Presentational components** (tables, cards, filters); **Dialog** components self-contained (own API call + toast) for mutations.
- **Screen shell** orchestrates sub-components.
- **Route + nav + i18n** wiring; permission gates render `EmptyState` when denied.

i18n: RU primary / EN secondary in `src/i18n/messages.ts`, parity enforced by `messages.test.ts`. `useI18n()` → `{ t, formatNumber, formatCurrency(amountMajor, currencyCode), formatDate(iso) }`.

Money shapes: backend reports use `MoneyDto { currencyCode, minorUnits }` → frontend `MoneyMinor { currencyCode, minorUnits }`; model layer converts to major for display via `formatCurrency`.

---

## PLAN 7a — Reports + Journal

### 4. Backend contracts (source of truth)

All report endpoints require permission `reports.view` (`StaffPermissionNames.ViewReports`). Audit requires `audit.view` (`ViewAudit`). JSON wire is camelCase.

#### 4.1 Reports — common
- Route shape: `GET /api/branches/{branchId:guid}/reports/{name}` with optional query `fromUtc`, `toUtc`, `limit`.
- CSV export: `GET /api/branches/{branchId:guid}/reports/{name}/export.csv` (same query params).
- Every result is `{ rows: Row[], limit: number, ...totals }`.

#### 4.2 Shifts — `/reports/shifts` → `ShiftReportResultDto`
- Result: `rows: ShiftReportRowDto[]`, `limit`.
- Row: `shiftId, organizationId, branchId, openedByStaffUserId, closedByStaffUserId, state, startingCash: MoneyDto, cashMovementsTotal: MoneyDto, posCashPaymentsTotal: MoneyDto, posRefundsTotal: MoneyDto, billingCashImpactTotal: MoneyDto, expectedCash: MoneyDto, countedCash: MoneyDto?, difference: MoneyDto?, openedAtUtc, closedAtUtc?`.

#### 4.3 Sales — `/reports/sales` → `SalesReportResultDto`
- Result: `rows: SalesReportRowDto[]`, `limit`, `grossSalesTotal: MoneyDto`, `refundsTotal: MoneyDto`, `netSalesTotal: MoneyDto`.
- Row: `posSaleId, organizationId, branchId, shiftId, createdByStaffUserId, state, total: MoneyDto, paidAmount: MoneyDto, refundAmount: MoneyDto, lineCount, itemQuantity, createdAtUtc, paidAtUtc?, refundedAtUtc?, voidedAtUtc?`.

#### 4.4 Gameplay time — `/reports/gameplay-time` → `GameplayTimeReportResultDto`
- Result: `rows: GameplayTimeReportRowDto[]`, `limit`, `totalDurationSeconds`, `totalPackageSeconds`, `totalBonusSeconds`, `gameplayRevenueTotal: MoneyDto`.
- Row: `sessionId, organizationId, branchId, seatId, deviceId, createdByStaffUserId, playerKind, playerAccountId?, state, durationSeconds, packageSeconds, bonusSeconds, gameplayRevenue: MoneyDto, startedAtUtc?, endedAtUtc?, endsAtUtc?`.

#### 4.5 Cash operations — `/reports/cash-operations` → `CashOperationReportResultDto`
- Result: `rows: CashOperationReportRowDto[]`, `limit`, `cashInTotal: MoneyDto`, `cashOutTotal: MoneyDto`, `netCashTotal: MoneyDto`.
- Row: `operationId, organizationId, branchId, shiftId?, createdByStaffUserId, sourceType, operationType, cashImpact: MoneyDto, reason, createdAtUtc`.

#### 4.6 Operator actions — `/reports/operator-actions` → `OperatorActionReportResultDto`
- Result: `rows: OperatorActionReportRowDto[]`, `limit`, `totalActionCount`.
- Row: `actorStaffUserId?, actorDisplayName, action, outcome, count, firstAtUtc, lastAtUtc`.

#### 4.7 Audit — `GET /api/branches/{branchId:guid}/audit` → `AuditSearchResultDto`
- Query (all optional): `action`, `outcome`, `targetType`, `fromUtc`, `toUtc`, `limit`.
- Result: `records: AuditRecordDto[]`, `limit`.
- Record: `auditRecordId, organizationId, branchId?, actorStaffUserId?, action, targetType, targetId?, outcome, sourceApp, detailsJson, createdAtUtc, actorPlatformAdminUserId?`.

### 5. Reports screen

**Route** `/club/reports`; nav `reports` flip `soon:true→false` (`ownerOnly:false`). Gate: `session.permissions.includes('reports.view')` else `EmptyState`.

**Layout:** `ReportsScreen` with Radix Tabs (pattern from `MonetizationScreen`), 5 tabs:

| Tab key | Label (RU) | Endpoint | Summary cards (totals) | Key columns |
|---|---|---|---|---|
| shifts | Смены | shifts | стартовая/ожидаемая/посчитанная касса, расхождение (per-row aggregates shown in table; no result-level totals) | состояние, открыта, закрыта, движения, ожидаемая, посчитано, расхождение |
| sales | Продажи | sales | gross / возвраты / net | состояние, сумма, оплачено, возврат, позиции, кол-во, создано, оплачено |
| gameplay | Игровое время | gameplay-time | сумм. длительность/пакет/бонус (мин), выручка | место, устройство, тип игрока, состояние, длительность (мин), выручка |
| cash | Касса | cash-operations | приход / расход / нетто | источник, тип операции, влияние, причина, создано |
| operatorActions | Действия операторов | operator-actions | всего действий | оператор, действие, исход, кол-во, первое, последнее |

**Date range:** screen-level shared state `{ fromUtc, toUtc }`, default = today (00:00:00 → 23:59:59 UTC, same construction as `getDashboardSummary`). `DateRangeControl` exposes presets **Сегодня / 7 дней / 30 дней** plus two `Input type="date"` fields. Changing range refetches the active tab.

**Generic hook** `useReport<T>(loader: () => Promise<T>, deps: unknown[]): ReportState<T>` — load-only union (`loading | error{retry} | ready{data,retry}`), `cancelled` flag, error-before-loading guard. Each tab passes a memoized loader closure; a per-tab pure model builder maps `T` → `{ summaryCards: SummaryCard[], columns: Column[], rows: Row[] }`.

Model types (in `reportsModel.ts`):
```ts
interface SummaryCard { labelKey: MessageKey; value: string }
interface ReportColumn { key: string; labelKey: MessageKey }
// per-report row view-model interfaces with pre-formatted display strings
```

**CSV export:**
- `clubApi.fetchReportCsv(branchId, name, fromUtc?, toUtc?): Promise<Blob>` — uses `sendRaw('GET', '/api/branches/{branchId}/reports/{name}/export.csv?…')` then `response.blob()` (Bearer auth carried by `sendRaw`).
- Util `saveBlob(blob: Blob, filename: string): void` in `src/lib/saveBlob.ts` — creates object URL, triggers `<a download>` click, revokes. Isolated so component tests mock it.
- `ExportButton` component (props: `onExport: () => Promise<Blob>`, `filename`, label) — calls export, `saveBlob`, toast on error.

**Files (Plan 7a — Reports portion):**
- `src/api/types.ts` — add `MoneyMinor` (if not present), 5 result + 5 row interfaces.
- `src/api/clubApi.ts` — `getShiftReport`, `getSalesReport`, `getGameplayTimeReport`, `getCashOperationReport`, `getOperatorActionReport` (each `(branchId, fromUtc?, toUtc?, limit?)`), `fetchReportCsv`.
- `src/lib/saveBlob.ts` — `saveBlob`.
- `src/club/reports/reportsModel.ts` — date-range presets helper, per-report view-model builders, `SummaryCard`/`ReportColumn` types.
- `src/club/reports/useReport.ts` — generic load-only hook.
- `src/club/reports/DateRangeControl.tsx`, `ExportButton.tsx`.
- `src/club/reports/ShiftsTab.tsx`, `SalesTab.tsx`, `GameplayTab.tsx`, `CashTab.tsx`, `OperatorActionsTab.tsx`.
- `src/club/reports/ReportsScreen.tsx`.

### 6. Journal screen

**Route** `/club/journal`; **new** nav item `journal` in the `branch` group after `reports` (`ownerOnly:false`, `soon:false`). Gate: `session.permissions.includes('audit.view')` else `EmptyState`.

**Layout:** `JournalScreen` = `AuditFilters` (action text input, outcome `Select`, targetType text input, `DateRangeControl` reused, limit) + records table.

**Table columns:** дата (`formatDate(createdAtUtc)`), актор (`actorStaffUserId` / `actorPlatformAdminUserId` / «—»), действие (`action`), цель (`targetType` + `targetId`), исход (`outcome` → Badge variant), источник (`sourceApp`), детали (`detailsJson` shown monospace, collapsible/raw).

**Files:**
- `src/api/types.ts` — `AuditRecord`, `AuditSearchResult`, `AuditSearchQuery`.
- `src/api/clubApi.ts` — `searchAudit(branchId, query: AuditSearchQuery): Promise<AuditSearchResult>` (builds query string from set fields only).
- `src/club/journal/auditModel.ts` — `toAuditRows` (format dates, resolve actor, default targetId), `outcomeBadgeVariant(outcome)`.
- `src/club/journal/useAudit.ts` — load-only by `[branchId, JSON.stringify(query), tick]`.
- `src/club/journal/AuditFilters.tsx`, `JournalScreen.tsx`.

### 7. Wiring (Plan 7a)
- `src/club/nav.ts`: `reports` `soon:false`; add `journal` item (branch group, after reports).
- `src/App.tsx`: `ClubRoute` union gains `clubReports`, `clubJournal`; `CLUB_SCREEN_TITLE` (Отчёты, Журнал); `pathForRoute`; `resolvePlatformRoute` (`/club/reports`, `/club/journal`); `isClubRoute`; `ClubArea` render branches with permission gates.
- i18n: `reports.*` (tabs, columns, summary labels, export, ranges), `journal.*` (filters, columns, outcomes) keys ru+en + parity coverage.

---

## PLAN 7b — Profile + Install + delete legacy

### 8. Profile screen

**Route** `/club/profile`; nav `profile` flip `soon:true→false` and **`ownerOnly:true→false`** (every staff user has a profile). No extra permission gate (session is always present).

**Read-only content:**
- Identity card: `displayName`, organization (`organizationId`), `staffUserId`, role (`roleFromPermissions(session.permissions)` → `ROLE_LABEL`), accessible branches (`session.branchIds` → names via the `directory` already built in `ClubArea`, passed as a `branches: {branchId,name}[]` prop).
- Permissions list: `session.permissions` grouped/readable.
- «Выйти» button → `onSignOut`.
- **Honest gap:** profile editing unavailable (no backend endpoint) — explicit note.

**Files:**
- `src/club/profile/profileModel.ts` — pure: group permissions into readable sections, resolve role label key, map branchIds→names. No hook (data is synchronous from props).
- `src/club/profile/ProfileScreen.tsx`.

Owner code is **not** here — it lives in Install.

### 9. Install screen (redesigned)

**Route** `/club/install` (already routed). Port from `ClubDashboard.tsx` lines 442–611 onto shadcn/ui + RU i18n.

**Content:**
- **Owner-code panel** (gate `identity.owner_code.manage` — `Permissions.manageOwnerCode`; else note): load via `getOwnerCode()`; «Сгенерировать» (`generateOwnerCode()`), «Перевыпустить» with reason (`rotateOwnerCode(reason)`). Show full code on issue (`issued.ownerCode`), else masked `**** {codeSuffix}`; valid-until, last-used, failed-attempts. Uses `Card`/`Button`/`Input`/`useToast` (replaces legacy `ErrorBanner`/CSS).
- **Setup wizard** steps (RU copy), `msiexec /i AFK4-Agent.msi` command block, MSI download button (`getSetupMsiUrl()` static link).
- **Branches available** list (Cards: name, city, PC count) from the `branches` directory prop.

**Files:**
- `src/club/install/installModel.ts` — owner-code view-model (masked/issued code, expiry, last-used, failed-attempts display) + `getSetupMsiUrl()` (ported, reads `import.meta.env.VITE_SETUP_MSI_URL`, fallback `/downloads/AFK4-Agent.msi`).
- `src/club/install/useOwnerCode.ts` — load-only hook (`getOwnerCode`), with `regenerate`/`rotate` actions updating local state.
- `src/club/install/OwnerCodePanel.tsx` — code display + generate/rotate (inline actions, own toast).
- `src/club/install/InstallScreen.tsx` — shell (header + MSI download + OwnerCodePanel + wizard steps + branches list).

`clubApi` already has `getOwnerCode`/`generateOwnerCode`/`rotateOwnerCode`.

### 10. Delete legacy (final task of 7b)

Current `ClubArea` reaches `LegacyClubScreen` (the `else` branch) only for `clubInstall` + the dead branch-detail routes (`clubBranchDetail`, `clubBranchFloorMap`, `clubBranchDevices`, `clubBranchPendingDevices`, `clubBranchOperators`). The branch-detail functionality is already migrated: devices + floor map → `VenueScreen`; operators → `SettingsScreen`; branch list/profile → `BranchesScreen`. Nothing in the current app navigates to those route kinds.

Steps:
1. `ClubArea`: render new `InstallScreen` for `clubInstall`; render `ProfileScreen` for `clubProfile`; remove the `else → LegacyClubScreen` fallback.
2. Remove dead route kinds `clubBranchDetail/clubBranchFloorMap/clubBranchDevices/clubBranchPendingDevices/clubBranchOperators` from the `ClubRoute` union, `pathForRoute`, `resolvePlatformRoute`, `isClubRoute`, `CLUB_SCREEN_TITLE`.
3. Delete `src/components/ClubDashboard.tsx` (`ClubDashboard`, `LegacyClubScreen`, `DashboardHome`, embedded `InstallScreen`, `getSetupMsiUrl`); remove its import in `App.tsx:9`.
4. Grep for any remaining imports from `components/ClubDashboard`; fix/remove. Full suite + `tsc -b && vite build` gate.

### 11. Wiring (Plan 7b)
- `src/club/nav.ts`: `profile` `soon:false` + `ownerOnly:false`.
- `src/App.tsx`: `ClubRoute` gains `clubProfile`; remove dead branch-detail kinds; `CLUB_SCREEN_TITLE` (Профиль; Установка already 'Установка'); `pathForRoute` (`/club/profile`); `resolvePlatformRoute` (`/club/profile`); `isClubRoute`; `ClubArea` renders `InstallScreen` + `ProfileScreen`, threads `branches`, `session`, `onSignOut`.
- i18n: `profile.*`, `install.*` keys ru+en + parity coverage.

---

## 12. Testing (both plans)

Vitest 4 + jsdom + @testing-library/react; `globals: false` (import `{ it, expect, vi }`). Radix Select tested at default selection (no dropdown open in jsdom); Radix Tabs need `fireEvent.mouseDown` then `click`.

- **Model builders:** minor→major money, seconds→minutes, date formatting, outcome→badge variant, date-range presets, permission grouping. Pure, table-driven.
- **Hooks:** loading → ready and loading → error via mock client (error-check before loading-check); `retry` increments tick and refetches.
- **Components:** tables render rows + summary cards; default tab; filters update query; export calls `fetchReportCsv` + `saveBlob` (both mocked); owner-code generate/rotate update display.
- **tsc trap:** typed mocks with explicit signatures (`vi.fn<(a: string) => Promise<X>>()`) so `tsc -b` (not just `vitest`) passes — vitest/esbuild does NOT type-check.
- **i18n parity:** `messages.test.ts` coverage blocks for new namespaces.
- **Final gate per plan:** `vitest run` (full suite) + `tsc -b && vite build` clean.

## 13. Honest gaps (surfaced in UI)
- **Profile:** view-only — no backend edit endpoint (explicit note).
- **Reports/Journal:** view + CSV export only; no server pagination — `limit`-bounded, labeled «последние N».
- **Journal `detailsJson`:** shown raw (monospace), no per-type parsing.
- **Shifts report:** backend returns no result-level totals; per-row money shown, no aggregate cards (or omit summary row for that tab).

## 14. Process constraints
- Merge each plan to `main` locally with `--no-ff`; do **not** push to origin.
- Subagent-driven development (fresh subagent per task; two-stage review).
- Communicate in Russian.
