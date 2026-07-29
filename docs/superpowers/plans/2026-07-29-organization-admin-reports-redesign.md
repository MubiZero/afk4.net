# Organization Admin Reports Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the transitional Reports workspace with the owner-oriented `Сводка`, `Смены и касса`, and `Выручка` center defined by the approved product specification.

**Architecture:** Keep financial interpretation in backend report services and expose typed, branch-scoped workspace projections. Organization Admin renders those projections without recomputing money, keeps CSV exports aligned with the selected range, and leaves immutable staff actions exclusively in the separate Events surface.

**Tech Stack:** .NET 10, ASP.NET Core minimal APIs, EF Core, shared C# contracts, React 19, TypeScript, Bun test, Testing Library, AFK4 i18n and design tokens.

## Global Constraints

- The only Reports tabs are `Сводка`, `Смены и касса`, and `Выручка`.
- Operator actions and audit records are not rendered or exported from Reports.
- Revenue includes settled gameplay and POS sales minus completed refunds; top-ups, debt payments, opening cash, deposits, and withdrawals remain cash flows.
- Date ranges are resolved in the branch timezone and represented as half-open UTC intervals.
- The frontend displays backend-authoritative totals and never reconstructs financial truth from limited rows.
- Existing Cash and Events workflows remain independently reachable and unchanged.
- Every behavior change follows red-green-refactor TDD.

---

### Task 1: Correct full-range report aggregation

**Files:**
- Modify: `src/AFK4.Platform.Api/Reports/EfReportService.cs`
- Modify: `tests/AFK4.Platform.Api.Tests/EfReportServiceTests.cs`

**Interfaces:** Preserve `IReportService`; report totals cover the full filter while detail rows alone obey `Limit`.

- [x] Add failing sales and gameplay tests with more matching records than `Limit`.
- [x] Run `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter EfReportServiceTests` and verify the new assertions fail for the expected reason.
- [x] Move aggregate calculation ahead of detail-row limiting without weakening currency validation.
- [x] Rerun `EfReportServiceTests|ReportEndpointTests|ReportCsvExporterTests` and keep the focused suite green.

### Task 2: Add typed owner-report workspace projections

**Files:**
- Create: `src/AFK4.Shared.Contracts/Reports/OrganizationAdminReportContracts.cs`
- Create: `src/AFK4.Platform.Api/Reports/IOrganizationAdminReportService.cs`
- Create: `src/AFK4.Platform.Api/Reports/OrganizationAdminReportService.cs`
- Create: `src/AFK4.Platform.Api/Endpoints/OrganizationAdminReportEndpoints.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs`
- Modify: `tests/AFK4.Shared.Contracts.Tests/ReportContractSerializationTests.cs`
- Create: `tests/AFK4.Platform.Api.Tests/Reports/OrganizationAdminReportServiceTests.cs`
- Create: `tests/AFK4.Platform.Api.Tests/Reports/OrganizationAdminReportEndpointTests.cs`

**Interfaces:** Produce summary, shift/cash, and revenue DTOs addressed by local `fromDate` and `toDate`; JSON and matching CSV endpoints require `organization.reports.view` and derive organization scope from the authenticated session.

- [ ] Add failing contract round-trip, branch-timezone, authorization, tenant-isolation, revenue-definition, attention-cap, active-shift, previous-period, and CSV parity tests.
- [ ] Verify focused tests fail because the contracts and services do not exist.
- [ ] Implement branch-local half-open ranges, backend-owned totals, seven-day summary trend, at most three attention rows plus total count, stable shift rows, revenue source/payment breakdowns, and previous-period comparison.
- [ ] Register services/endpoints, retain existing low-level report endpoints for compatibility, and run the focused backend tests green.

### Task 3: Replace the transitional Reports UI

**Files:**
- Create: `src/AFK4.OrganizationAdmin.Web/src/api/clients/reports.ts`
- Modify: `src/AFK4.OrganizationAdmin.Web/src/api/clients/index.ts`
- Replace: `src/AFK4.OrganizationAdmin.Web/src/reports/ReportsWorkspace.tsx`
- Replace: `src/AFK4.OrganizationAdmin.Web/src/reports/ReportsWorkspace.test.tsx`
- Replace: `src/AFK4.OrganizationAdmin.Web/src/reports/reportsNav.ts`
- Replace: `src/AFK4.OrganizationAdmin.Web/src/reports/reportsNav.test.ts`
- Create: `src/AFK4.OrganizationAdmin.Web/src/reports/SummaryReport.tsx`
- Create: `src/AFK4.OrganizationAdmin.Web/src/reports/ShiftCashReport.tsx`
- Create: `src/AFK4.OrganizationAdmin.Web/src/reports/RevenueReport.tsx`
- Replace: `src/AFK4.OrganizationAdmin.Web/src/styles/26-reports.css`
- Modify: `packages/i18n/src/messages.ts`
- Delete after green: `src/AFK4.OrganizationAdmin.Web/src/reports/overview/`, `history/`, and `journal/`.

**Interfaces:** `ReportTab = 'summary' | 'shiftsCash' | 'revenue'`; each destination consumes a typed backend projection and shares one branch-local date-range control.

- [ ] Write failing tests for exact tab order, Summary default, calm/attention states, maximum-three attention rows, three figures, active shift provisional label/link, stable shift selection, revenue sources/comparison, exports, loading/error/empty states, and permission visibility.
- [x] Verify the focused Bun tests fail against the transitional UI.
- [x] Add the typed client and implement the first dense desktop reports layout using existing tokens/components, semantic controls, tabular numbers, visible focus, and responsive stacking.
- [x] Remove Journal/operator-actions from Reports, delete obsolete report destinations after zero-reference search, add real ru/en/tg copy, and run focused tests plus the catalog integrity tests green.

The first UI pass deliberately reuses the existing dashboard and low-level report
endpoints. It is not the completion of this task: the dedicated projection in
Task 2 must replace client-composed ranges and exports before the approved
timezone, trend, comparison, and full attention semantics can be checked off.

### Task 4: Integrated verification and durable state

**Files:**
- Modify: `docs/progress/2026-05-12-vertical-slice-progress.md`
- Modify: `docs/superpowers/plans/README.md`

**Interfaces:** No new interfaces; records implementation and remaining physical/visual smoke gaps.

- [ ] Run the complete Organization Admin Web tests and production build.
- [ ] Run affected shared-contract and Platform API tests, then the full solution build with Windows targeting.
- [ ] Run `git diff --check`, inspect the final diff for stale `overview/history/journal/operatorActions` report references, and update progress with exact evidence.
- [ ] Commit coherent verified units and report branch/ahead/push state without pushing unless explicitly requested.

## Plan Self-Review

- The plan is intentionally limited to Reports and the removal of audit duplication from that surface.
- Cash command ownership, the Events implementation, approvals, and native credential prompts are outside this slice.
- Backend authority, branch timezone, row-limit correctness, permissions, exports, localization, empty/error/loading states, and responsive UI all have explicit tests and completion gates.
