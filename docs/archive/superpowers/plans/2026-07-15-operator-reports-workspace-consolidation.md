# Operator Reports And Workspace Consolidation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the current Reports dashboard and overlapping Cash/Logs/Stock journals with the approved reporting center, one Events feed, three Cash tabs, and secure contextual second-manager confirmation.

**Architecture:** Add backend-authoritative report projections over reports, shifts, POS, sessions, and audit data, using branch-local calendar ranges and server-owned financial definitions. Add a five-minute single-use secondary-approval challenge orchestrator with explicit handlers for ledger actions, POS refunds, compensated sessions, and discrepant shift close; the native Windows host gathers existing manager credentials and returns only a sanitized result to React. Keep legacy approvals available until every replacement path is verified, then remove duplicate UI in the final slice.

**Tech Stack:** .NET 10, ASP.NET Core minimal APIs, EF Core/PostgreSQL, shared C# contracts, WPF/WebView2, React 19, TypeScript 6, Bun test, Testing Library, AFK4 i18n/tokens, Playwright with the approved system Chromium fallback.

## Global Constraints

- Execute from an isolated worktree and topic branch created from `bbab04c2`; invoke `superpowers:using-git-worktrees` before implementation.
- Use Windows and PowerShell for authoritative .NET/Operator verification. Linux portable checks require `-p:EnableWindowsTargeting=true` and do not replace the WindowsDesktop testhost.
- Preserve exactly: `Сводка`, `Смены и касса`, `Выручка`, `Продажа`, `Смена`, `Чеки`, `События`, `Остатки`, `Приёмка`, `История`, `Инвентаризация`. Never use `Движения` as a tab label.
- Wallet top-ups, debt payments, opening cash, and cash deposits/withdrawals are cash flows, never revenue.
- Net revenue is settled gameplay charges plus settled POS goods/services sales minus completed refunds.
- Resolve report dates through `BranchEntity.PreferredTimeZone`; React never derives financial UTC boundaries.
- Report totals, exception state, shift state, and critical actions remain backend-authoritative.
- Secondary approval requires a different active user in the same branch with `billing.money_action.approve`.
- Manager credentials never enter WebView DOM, React state, browser storage, protected primary-session storage, logs, analytics, or a token response.
- Challenges are single-use, payload/idempotency-bound, and expire after five minutes.
- Preserve ledger/audit immutability, module boundaries, backend confirmation, idempotency, no-self-approval, caps, thresholds, and concurrency guards.
- Do not remove `ReviewWorkspace` or legacy approval endpoints before Tasks 10–13 pass on Windows.
- Use TDD for contracts, financial definitions, timezones, endpoints, authorization, challenge lifecycle, host behavior, models, and wiring.
- Reuse existing AFK4 components, tokens, icons, rail, titlebar, footer, responsive breakpoints, and both themes.
- Do not push, merge, or delete branches without explicit instruction.

## File Structure

- `src/AFK4.Shared.Contracts/Reports/OperatorReportWorkspaceContracts.cs` — Summary, shift/cash, and revenue DTOs.
- `src/AFK4.Platform.Api/Reports/BranchBusinessDayRangeResolver.cs` — branch-local date ranges.
- `src/AFK4.Platform.Api/Reports/OperatorReportWorkspaceService.cs` — aggregates, exceptions, and trend.
- `src/AFK4.Platform.Api/Endpoints/OperatorReportWorkspaceEndpoints.cs` — JSON and CSV routes.
- `src/AFK4.Operator.App.Web/src/reports/` — typed models and three report tabs.
- `src/AFK4.Operator.App.Web/src/events/` — one audit-backed Events feed.
- `src/AFK4.Shared.Contracts/Approvals/SecondaryApprovalContracts.cs` — cross-domain challenge/confirmation contracts.
- `src/AFK4.Platform.Api/AntiFraud/SecondaryApproval/` — challenge lifecycle, verifier, coordinator, and handlers.
- `src/AFK4.Operator.App/Approvals/` — native API client and secure WPF prompt.
- `src/AFK4.Operator.App.Web/src/secondaryApprovalClient.ts` — sanitized host request.

---

### Task 1: Add Report Contracts And Branch-Day Resolution

**Files:**
- Create: `src/AFK4.Shared.Contracts/Reports/OperatorReportWorkspaceContracts.cs`
- Create: `src/AFK4.Platform.Api/Reports/IBranchBusinessDayRangeResolver.cs`
- Create: `src/AFK4.Platform.Api/Reports/BranchBusinessDayRangeResolver.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs`
- Modify: `tests/AFK4.Shared.Contracts.Tests/ReportContractSerializationTests.cs`
- Create: `tests/AFK4.Platform.Api.Tests/Reports/BranchBusinessDayRangeResolverTests.cs`

**Interfaces:** Produces `ReportCalendarRangeDto`, `ReportAttentionItemDto`, `ReportTrendPointDto`, `ActiveShiftReportContextDto`, `OperatorReportSummaryDto`, `ShiftCashWorkspaceReportDto`, `RevenueWorkspaceReportDto`, and `ResolveAsync(organizationId, branchId, fromDate, toDate, ct)`.

- [ ] Write failing JSON round-trip tests for all DTOs and range tests proving `Asia/Dushanbe` maps local `2026-07-15` to `[2026-07-14T19:00:00Z, 2026-07-15T19:00:00Z)`; cover reversed dates, unknown branch/timezone, cross-tenant branch, and ranges over 366 days.
- [ ] Run red:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Shared.Contracts.Tests\AFK4.Shared.Contracts.Tests.csproj --filter ReportContractSerializationTests
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Platform.Api.Tests\AFK4.Platform.Api.Tests.csproj --filter BranchBusinessDayRangeResolverTests
```

- [ ] Implement explicit records reusing `MoneyDto`, `ShiftReportRowDto`, and `CashOperationReportRowDto`. The resolver loads `PreferredTimeZone` under tenant scope, converts local midnights to a half-open UTC range, and returns a typed invalid/missing result rather than falling back to machine timezone.
- [ ] Run green and commit:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Shared.Contracts.Tests\AFK4.Shared.Contracts.Tests.csproj --filter ReportContractSerializationTests
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Platform.Api.Tests\AFK4.Platform.Api.Tests.csproj --filter BranchBusinessDayRangeResolverTests
git add src/AFK4.Shared.Contracts src/AFK4.Platform.Api/Reports src/AFK4.Platform.Api/Program.cs tests
git commit -m "feat(reports): add branch-day workspace contracts"
```

### Task 2: Make Financial Totals Independent Of Row Limits

**Files:**
- Modify: `src/AFK4.Platform.Api/Reports/EfReportService.cs`
- Modify: `tests/AFK4.Platform.Api.Tests/EfReportServiceTests.cs`

**Interfaces:** Preserves `IReportService`; guarantees sales/gameplay totals cover the full filter while only `Rows` obeys `Limit`.

- [ ] Seed three sales/sessions, query with `Limit: 1`, and assert one row but full totals. Add refunded sale, top-up, and debt payment; assert `NetSalesTotal = GrossSalesTotal + RefundsTotal` and non-revenue flows do not affect revenue.
- [ ] Run `dotnet test tests\AFK4.Platform.Api.Tests\AFK4.Platform.Api.Tests.csproj --filter EfReportServiceTests`; expect the new limit assertions to fail.
- [ ] Compute totals and currency fail-closed checks before `Take(limit)`; apply row limiting only to details and retain checked sums/sign conventions.
- [ ] Rerun `EfReportServiceTests|ReportEndpointTests|ReportCsvExporterTests`, then commit `fix(reports): aggregate full filtered ranges`.

### Task 3: Build Backend Workspace Projections

**Files:**
- Create: `src/AFK4.Platform.Api/Reports/IOperatorReportWorkspaceService.cs`
- Create: `src/AFK4.Platform.Api/Reports/OperatorReportWorkspaceService.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs`
- Create: `tests/AFK4.Platform.Api.Tests/Reports/OperatorReportWorkspaceServiceTests.cs`

**Interfaces:** Produces `GetSummaryAsync`, `GetShiftCashAsync`, and `GetRevenueAsync` using local dates and optional actor/limit.

- [ ] Write failing tests for: Summary figures; seven local-day points; provisional open shift; discrepant closed shift and failed critical audit attention; three-row cap plus full count; shift/cash actor filter; previous equivalent period; tenant exclusion; mixed-currency failure.
- [ ] Run red with `--filter OperatorReportWorkspaceServiceTests`.
- [ ] Implement Summary by composing full-range report totals, open-shift context, staff names, discrepancy tolerance, and critical audit failures. Sort exceptions by severity/recency; return three and keep total count.
- [ ] Implement shifts/cash under one range and revenue using only settled POS/gameplay sources. Resolve previous period by equal local-day length.
- [ ] Run `OperatorReportWorkspaceServiceTests|EfReportServiceTests`, then commit `feat(reports): add operator workspace projections`.

### Task 4: Expose JSON And Matching CSV Endpoints

**Files:**
- Create: `src/AFK4.Platform.Api/Endpoints/OperatorReportWorkspaceEndpoints.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs`
- Modify: `src/AFK4.Platform.Api/Reports/ReportCsvExporter.cs`
- Create: `tests/AFK4.Platform.Api.Tests/Reports/OperatorReportWorkspaceEndpointTests.cs`
- Modify: `tests/AFK4.Platform.Api.Tests/ReportCsvExporterTests.cs`

**Interfaces:** Adds `/reports/workspace/summary`, `/shifts-cash`, `/revenue`, and filtered CSV routes for the latter two.

- [ ] Write failing 401/403/400, branch isolation, JSON shape, filtered export, audit-detail, and CSV encoding tests.
- [ ] Require `reports.view`, obtain organization only from staff context, parse `DateOnly`, call Task 3, and audit success/denial with local dates and row counts.
- [ ] Export the same timezone, filters, totals, and provisional/closed labels as JSON. Keep operator-action export owned by Events.
- [ ] Run `OperatorReportWorkspaceEndpointTests|ReportCsvExporterTests|ReportEndpointTests`; commit `feat(reports): expose consolidated report endpoints`.

### Task 5: Add Typed React Clients And Pure Models

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/api/clients/reports.ts`
- Modify: `src/AFK4.Operator.App.Web/src/api/clients/index.ts`
- Modify: `src/AFK4.Operator.App.Web/src/operatorApiClients.ts`
- Modify: `src/AFK4.Operator.App.Web/src/operatorApiClients.test.ts`
- Create: `src/AFK4.Operator.App.Web/src/reports/reportsModel.ts`
- Create: `src/AFK4.Operator.App.Web/src/reports/reportsModel.test.ts`

**Interfaces:** Produces `ReportTab = 'summary' | 'shifts_cash' | 'revenue'`, typed requests/responses, copy/tone projection, and accessible trend rows.

- [ ] Write failing tests for date-only URLs, omitted null filters, CSV text responses, exact tab order, zero-attention status, provisional shift copy, and unchanged server money values.
- [ ] Run `bun test src/operatorApiClients.test.ts src/reports/reportsModel.test.ts`; expect missing modules.
- [ ] Mirror C# DTOs exactly, use `normalizeReportQuery`, preserve minor units, and prohibit client-side financial addition/subtraction.
- [ ] Rerun focused tests; commit `feat(operator): add report workspace client model`.

### Task 6: Build The Selected Reports UI

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/reports/ReportTabBar.tsx`
- Create: `src/AFK4.Operator.App.Web/src/reports/ReportTrend.tsx`
- Create: `src/AFK4.Operator.App.Web/src/reports/SummaryReport.tsx`
- Create: `src/AFK4.Operator.App.Web/src/reports/ShiftCashReport.tsx`
- Create: `src/AFK4.Operator.App.Web/src/reports/RevenueReport.tsx`
- Create: `src/AFK4.Operator.App.Web/src/reports/ReportsWorkspace.tsx`
- Create: `src/AFK4.Operator.App.Web/src/reports/ReportsWorkspace.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/App.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/operatorTypes.ts`
- Modify: `src/AFK4.Operator.App.Web/src/operatorData.ts`
- Modify: `src/AFK4.Operator.App.Web/src/operatorPermissions.ts`
- Modify: `src/AFK4.Operator.App.Web/src/App.test.tsx`
- Create: `src/AFK4.Operator.App.Web/src/styles/23-reports.css`
- Modify: `src/AFK4.Operator.App.Web/src/styles.css`
- Modify: `src/AFK4.Operator.App.Web/package.json`
- Modify: `bun.lock`
- Modify: `packages/i18n/src/messages.ts`

**Interfaces:** Replaces workspace ID `dashboard` with `reports`; navigation to Cash uses the existing workspace callback.

- [ ] Write failing tests for exactly three tabs, Summary default, maximum three exception rows, no filler cards, three figures, accessible chart/table, provisional shift, stable shift inspector, revenue comparison, filtered exports, permissions, and realtime reload.
- [ ] Run `bun test src/reports/ReportsWorkspace.test.tsx src/App.test.tsx src/WorkspaceRail.test.tsx src/operatorVisibility.test.ts`; expect red.
- [ ] Add the repository-standard `recharts` version already used by Platform Web to the Operator package. Match the selected hierarchy: title/tabs/date/export, large status, bordered exceptions, three-figure rail, Recharts seven-day line trend, and active-shift context. Use Lucide icons and existing register/inspector primitives; do not handcraft chart SVG/CSS art.
- [ ] Build shifts/cash and revenue states with stable selection, canonical links, and stacked narrow layout. Delete old dashboard UI only after the new route is green; keep backend dashboard APIs until zero references are proven.
- [ ] Run focused tests, `src/i18nKeysExist.test.ts`, and `bun run build`; commit `feat(operator): build consolidated reports center`.

### Task 7: Consolidate Events And Stock Labels

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/events/eventsModel.ts`
- Create: `src/AFK4.Operator.App.Web/src/events/eventsModel.test.ts`
- Rename: `src/AFK4.Operator.App.Web/src/BackendLogsWorkspace.tsx` to `src/AFK4.Operator.App.Web/src/events/EventsWorkspace.tsx`
- Create: `src/AFK4.Operator.App.Web/src/events/EventsWorkspace.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/App.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/operatorTypes.ts`
- Modify: `src/AFK4.Operator.App.Web/src/operatorData.ts`
- Modify: `src/AFK4.Operator.App.Web/src/operatorPermissions.ts`
- Modify: `src/AFK4.Operator.App.Web/src/stock/StockWorkspace.tsx`
- Modify: `packages/i18n/src/messages.ts`
- Modify: `src/AFK4.Operator.App.Web/src/App.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/WorkspaceRail.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/operatorVisibility.test.ts`
- Modify: `src/AFK4.Operator.App.Web/src/stock/StockWorkspace.test.tsx`

**Interfaces:** Replaces workspace ID `logs` with `events`; categories are `staff`, `cash`, `pc`, `system`. Legacy approval queue remains reachable until Task 13.

- [ ] Test one feed, no nested diagnostics/source tabs, combinable category/action/result/actor/object/text filters, immutable inspector, operator-action export, no financial totals, and visible `История` stock label.
- [ ] Reduce Logs to audit-backed Events; remove diagnostics cards/support bundles from this workspace. Map categories in a pure model and preserve canonical links/export.
- [ ] Keep internal stock tab value `journal`; change only visible copy to `История`. Do not use `Движения`.
- [ ] Run Events/Stock/App/rail/i18n tests; commit `refactor(operator): consolidate events and stock history`.

### Task 8: Add Five-Minute Approval Challenge Persistence

**Files:**
- Create: `src/AFK4.Shared.Contracts/Approvals/SecondaryApprovalContracts.cs`
- Create: `src/AFK4.Platform.Api/Data/SecondaryApprovalChallengeEntity.cs`
- Modify: `src/AFK4.Platform.Api/Data/PlatformDbContext.cs`
- Add: generated `src/AFK4.Platform.Api/Data/Migrations/*_AddSecondaryApprovalChallenges.cs`
- Modify: `src/AFK4.Platform.Api/Data/Migrations/PlatformDbContextModelSnapshot.cs`
- Create: `src/AFK4.Platform.Api/AntiFraud/SecondaryApproval/SecondaryApprovalActionKinds.cs`
- Create: `src/AFK4.Platform.Api/AntiFraud/SecondaryApproval/ISecondaryApprovalChallengeService.cs`
- Create: `src/AFK4.Platform.Api/AntiFraud/SecondaryApproval/SecondaryApprovalChallengeService.cs`
- Create: `tests/AFK4.Platform.Api.Tests/AntiFraud/SecondaryApprovalChallengeServiceTests.cs`

**Interfaces:** Produces `SecondaryApprovalChallengeDto`, `SecondaryApprovalConfirmRequest`, and create/load/approve/expire/cancel lifecycle methods.

- [ ] Test payload SHA-256 binding, five-minute boundary, tenant/branch/requester/action/target/amount/reason/idempotency persistence, cancellation, terminal one-use behavior, approved retry, and absence of credential/token columns.
- [ ] Implement fields for IDs, action, target, amount/currency, reason, payload JSON/hash, idempotency, state, timestamps, confirmer, and sanitized result. States are `pending`, `approved`, `cancelled`, `expired`.
- [ ] Generate migration with `dotnet ef migrations add AddSecondaryApprovalChallenges`; inspect tenant/branch/state/expiry index and verify no credentials.
- [ ] Run contract/lifecycle tests; commit `feat(antifraud): add secondary approval challenges`.

### Task 9: Verify Secondary Credentials Without Tokens

**Files:**
- Create: `src/AFK4.Platform.Api/Identity/ISecondaryStaffCredentialVerifier.cs`
- Create: `src/AFK4.Platform.Api/Identity/SecondaryStaffCredentialVerifier.cs`
- Create: `tests/AFK4.Platform.Api.Tests/AntiFraud/SecondaryStaffCredentialVerifierTests.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs`

**Interfaces:** `VerifyAsync(organizationId, branchId, login, password, ct)` returns only staff ID, display name, branch IDs, roles, and permissions.

- [ ] Test username/email/verified-phone, wrong password, inactive/cross-org/unassigned users, missing permission, valid manager, and zero token-issuer calls.
- [ ] Reuse credential normalization and `PasswordHasher<StaffUserEntity>`, then derive branch-scoped roles/permissions through `PermissionCatalog`; never call `IStaffTokenService`.
- [ ] Run `SecondaryStaffCredentialVerifierTests`; commit `feat(auth): verify secondary approvers without tokens`.

### Task 10: Add Confirmation Coordinator And Migrate Money Actions

**Files:**
- Create: `src/AFK4.Platform.Api/AntiFraud/SecondaryApproval/ISecondaryApprovalActionHandler.cs`
- Create: `src/AFK4.Platform.Api/AntiFraud/SecondaryApproval/SecondaryApprovalCoordinator.cs`
- Create: `src/AFK4.Platform.Api/AntiFraud/SecondaryApproval/MoneyActionApprovalHandler.cs`
- Modify: `src/AFK4.Platform.Api/AntiFraud/MoneyActionApprovalService.cs`
- Modify: `src/AFK4.Platform.Api/AntiFraud/IMoneyActionApprovalService.cs`
- Modify: `src/AFK4.Platform.Api/Endpoints/MoneyActionEndpoints.cs`
- Modify: `src/AFK4.Shared.Contracts/Billing/MoneyActionContracts.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs`
- Create: `tests/AFK4.Platform.Api.Tests/AntiFraud/SecondaryApprovalEndpointTests.cs`
- Modify: `tests/AFK4.Platform.Api.Tests/AntiFraud/MoneyActionApprovalServiceTests.cs`
- Modify: `tests/AFK4.Platform.Api.Tests/AntiFraud/MoneyActionEndpointTests.cs`

**Interfaces:** Adds confirm/cancel endpoints. `MoneyActionSubmitResponse` gains additive `Confirmation`; new over-threshold submissions return `202` and `confirmation_required`.

- [ ] Test wrong credentials, self-confirmation, wrong branch/permission, expiry/cancel/hash change, successful manager, double and concurrent confirm, execution conflict, audit identities, and no tokens in JSON.
- [ ] Coordinator scopes the challenge to primary requester/tenant/branch, verifies manager, rechecks hash, dispatches exactly one handler, and stores sanitized result. Money handler uses existing `IMoneyActionExecutor` with requester attribution.
- [ ] Stop creating 24-hour pending rows for new money actions. Preserve historical rows for audit. Add a `secondary-approval` limiter of five attempts per five minutes keyed by IP plus primary staff.
- [ ] Run secondary/money/audit tests; commit `feat(antifraud): confirm money actions inline`.

### Task 11: Cover POS Refunds, Shift Close, And Comp Sessions

**Files:**
- Create: three handlers under `src/AFK4.Platform.Api/AntiFraud/SecondaryApproval/`
- Modify: `src/AFK4.Platform.Api/Endpoints/PosEndpoints.cs`
- Modify: `src/AFK4.Platform.Api/Endpoints/ShiftEndpoints.cs`
- Modify: `src/AFK4.Platform.Api/Endpoints/SessionEndpoints.cs`
- Modify: `src/AFK4.Platform.Api/Shifts/EfShiftService.cs`
- Modify: `src/AFK4.Shared.Contracts/Shifts/CloseShiftRequest.cs`
- Modify: `src/AFK4.Shared.Contracts/Sessions/StartGuestSessionRequest.cs`
- Create: `tests/AFK4.Platform.Api.Tests/AntiFraud/PosRefundSecondaryApprovalTests.cs`
- Create: `tests/AFK4.Platform.Api.Tests/AntiFraud/ShiftCloseSecondaryApprovalTests.cs`
- Create: `tests/AFK4.Platform.Api.Tests/AntiFraud/SessionCompSecondaryApprovalTests.cs`
- Modify: `tests/AFK4.Platform.Api.Tests/AntiFraud/EfShiftServiceSignOffTests.cs`

**Interfaces:** Domain endpoints return normal DTOs under policy or `202 SecondaryApprovalChallengeDto`; handlers call domain application services only.

- [ ] POS test: over-threshold refund has no payment/ledger/inventory/shop effect before confirm and exactly one original-mix refund after confirm.
- [ ] Shift test: over-tolerance close rejects caller-trusted manager GUID; confirmed close records verified manager and closes once.
- [ ] Comp tests for map and reservation starts: over-threshold value requires different manager, retains requester as session creator, and creates one session/ledger/audit set.
- [ ] Persist full immutable command payload plus original idempotency key. Revalidate state, amount, currency, open shift, and branch at execution. POS calls `IShopCommerceCoordinator`, shift calls `IShiftService`, comp calls existing session-start service.
- [ ] Keep sign-off ID serialization-compatible but accept it only from server-confirmed handler context.
- [ ] Run new tests plus `EfShiftServiceSignOffTests|EfPosSettlementServiceTests|SessionEndpointTests`; commit `feat(antifraud): unify high-risk action confirmation`.

### Task 12: Add Secure Native Manager Prompt

**Files:**
- Create: `src/AFK4.Operator.App/Approvals/IOperatorSecondaryApprovalApiClient.cs`
- Create: `src/AFK4.Operator.App/Approvals/HttpOperatorSecondaryApprovalApiClient.cs`
- Create: `src/AFK4.Operator.App/Approvals/ISecondaryApprovalPrompt.cs`
- Create: `src/AFK4.Operator.App/Approvals/SecondaryApprovalPrompt.xaml`
- Create: `src/AFK4.Operator.App/Approvals/SecondaryApprovalPrompt.xaml.cs`
- Create: `src/AFK4.Operator.App/Approvals/WpfSecondaryApprovalPrompt.cs`
- Modify: `src/AFK4.Operator.App/Web/OperatorWebHostBridge.cs`
- Modify: `src/AFK4.Operator.App/Web/WebViewOperatorWindow.xaml.cs`
- Modify: `tests/AFK4.Operator.App.Tests/OperatorWebHostBridgeTests.cs`
- Create: `tests/AFK4.Operator.App.Tests/OperatorSecondaryApprovalApiClientTests.cs`

**Interfaces:** `approval:confirm` receives challenge metadata only; native HTTP adds credentials locally and returns approved/confirmer/result only.

- [ ] Test credential-free React payload, cancel, password only in TLS body, sanitized response/logs, unchanged primary token store, structured errors, and fail-closed missing host.
- [ ] Build WPF login `TextBox`, `PasswordBox`, action summary, requester warning, Cancel/Confirm, keyboard focus, and localized errors. Clear password after failure/close.
- [ ] Inject prompt into bridge, allow only `approval:confirm/cancel`, dispatch on UI thread, and preserve current auth/connection behavior.
- [ ] Run Windows Operator host/client tests; commit `feat(operator-host): add secure secondary approval prompt`.

### Task 13: Wire Canonical React Actions And Delete Queue UI

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/secondaryApprovalClient.ts`
- Create: `src/AFK4.Operator.App.Web/src/secondaryApprovalClient.test.ts`
- Modify: `src/AFK4.Operator.App.Web/src/platformApi.ts`
- Modify: `src/AFK4.Operator.App.Web/src/api/clients/moneyActions.ts`
- Modify: `src/AFK4.Operator.App.Web/src/api/clients/pos.ts`
- Modify: `src/AFK4.Operator.App.Web/src/api/clients/sessions.ts`
- Modify: `src/AFK4.Operator.App.Web/src/api/clients/shifts.ts`
- Modify: `src/AFK4.Operator.App.Web/src/BackendPlayersWorkspace.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/cash/CashReceiptsLedger.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/cash/CashShiftCommandBar.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/useFloorMap.ts`
- Modify: `src/AFK4.Operator.App.Web/src/BackendBookingWorkspace.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/operatorApiClients.test.ts`
- Modify: `src/AFK4.Operator.App.Web/src/App.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/cash/CashReceiptsLedger.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/cash/CashShiftCommandBar.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/BackendBookingWorkspace.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/devHostBridge.ts`
- Modify: `src/AFK4.Operator.App.Web/src/devMockBackend.ts`
- Modify: `src/AFK4.Platform.Api/Endpoints/MoneyActionEndpoints.cs`
- Add: generated `src/AFK4.Platform.Api/Data/Migrations/*_ExpireLegacyPendingMoneyActions.cs`
- Delete after green: `src/AFK4.Operator.App.Web/src/ReviewWorkspace.tsx` and test

**Interfaces:** `completeSecondaryApproval(challenge)` posts only sanitized metadata; canonical actions handle normal success or `confirmation_required` and refetch authority.

- [ ] Test manual correction, ledger/POS refund, discrepant close, map/reservation comp, cancel, confirm/refetch, original idempotency reuse, no React credentials, and offline block.
- [ ] Add explicit action response handling in `PlatformApiClient`: normal DTO on 200, validated challenge on 202, existing projected errors otherwise.
- [ ] Browser preview may simulate a clearly marked approval result; packaged runtime without native bridge fails closed.
- [ ] After every canonical test passes, add `ExpireLegacyPendingMoneyActions`: update only legacy `MoneyActionRequests` in `pending` to `expired` at cutover. Make legacy list/approve/reject routes return `410 Gone` with instructions to reopen the canonical object; retain rows and read-only audit evidence. Then delete queue UI/client/dev fixtures.
- [ ] Run focused frontend tests and build; commit `feat(operator): confirm high-risk actions in context`.

### Task 14: Finalize Cash Ownership

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/cash/CashTabBar.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/cash/CashWorkspace.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/cash/cashModel.ts`
- Modify: `src/AFK4.Operator.App.Web/src/cash/CashShiftWorkspace.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/cash/CashOperationsLedger.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/cash/CashReceiptsLedger.tsx`
- Delete: `src/AFK4.Operator.App.Web/src/cash/CashJournalWorkspace.tsx` and test
- Delete: `src/AFK4.Operator.App.Web/src/cash/cashTerminalModel.ts` and test
- Modify: Cash tests and `packages/i18n/src/messages.ts`

**Interfaces:** Final `CashTab = 'sales' | 'shift' | 'receipts'`; order/labels are `Продажа / Смена / Чеки`.

- [ ] Rewrite tests: receipt-only sees Checks, shift-only sees Shift, sales-only sees Sale, approval-only creates no Cash tab, no Journal/Approvals copy, current operations under Shift, historical comparison links Reports.
- [ ] Render receipts directly, integrate current-shift operation register into Shift, retain all shift commands there, and remove duplicated historical exports.
- [ ] Remove journal CSS/i18n after zero-reference search. Run all Cash/App/i18n tests and build; commit `refactor(operator): finalize cash workspace ownership`.

### Task 15: Full Verification, Visual QA, And Durable Docs

**Files:**
- Modify: report/events/cash components/styles found by QA
- Modify: `src/AFK4.Operator.App.Web/src/qaContrast.test.ts`
- Modify: `src/AFK4.Operator.App.Web/design-qa.md`
- Modify: `docs/progress/2026-05-12-vertical-slice-progress.md`
- Modify: `docs/superpowers/plans/README.md`
- Modify: `docs/superpowers/specs/2026-07-15-operator-reports-workspace-consolidation-design.md`
- Delete legacy server queue endpoints only when repository references are zero and compatibility tests permit it

**Interfaces:** No new interfaces; proves the integrated design.

- [ ] Run scoped reference audit:

```powershell
rg -n "ReviewWorkspace|CashJournalWorkspace|listPending|dashboard|BackendLogsWorkspace|Журнал кассы|Согласования" src tests packages
```

Classify matches and preserve unrelated device approvals/auth/historical audit terms.

- [ ] Run full verification:

```powershell
Set-Location src\AFK4.Operator.App.Web
bun run test
bun run build
Set-Location ..\..\..
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Shared.Contracts.Tests\AFK4.Shared.Contracts.Tests.csproj
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Platform.Api.Tests\AFK4.Platform.Api.Tests.csproj
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Operator.App.Tests\AFK4.Operator.App.Tests.csproj
& 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

- [ ] Manually prove wrong password, self-confirmation, missing permission, cancel, expiry, network loss, success, retry/double-click, and unchanged primary session.
- [ ] Capture dark/light Reports, shift inspector, revenue, Events, Cash, and native prompt at 1920/1440/1280/narrow, plus Windows 100%/125%. Compare with selected concept; fix overflow, focus, contrast, chart labels, and console/page errors; record evidence in `design-qa.md`.
- [ ] Update progress with ownership, report definitions, security boundary, exact verification, gaps, and next work. Mark spec implemented only after gates pass; archive plan/spec only after landing on `main`.
- [ ] Run `git diff --check`, inspect staged status/stat, commit `docs(operator): record reports consolidation verification`, and stop before push/merge.

## Plan Self-Review

- **Coverage:** Tasks 1–6 cover report definitions, timezone, aggregates, exports, selected UI, permissions, accessibility, and themes. Task 7 covers Events/Stock. Tasks 8–13 cover action-bound native confirmation for money, POS, comp, and shift flows. Task 14 removes duplicate Cash/approval UI only after replacement. Task 15 covers Windows, visual QA, docs, and compatibility.
- **Scope safety:** One ordered workstream prevents capability gaps; each task ends in an independently reviewable commit.
- **Type consistency:** `SecondaryApprovalChallengeDto` crosses backend, native prompt metadata, and React unchanged; credentials exist only in the native confirmation request. Task 1 DTO names remain unchanged through Tasks 3–6.
- **Removal safety:** `ReviewWorkspace` survives until Task 13 and `CashJournalWorkspace` until Task 14; Task 13 expires hidden legacy pending work and disables its mutation routes without deleting evidence.
