# Platform Update Release Control Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move update package and rollout authority from organization staff to Platform Control/release automation and make device eligibility safe and deterministic.

**Architecture:** Replace branch-owned packages with a platform-global catalog and platform-authorized rollout targets. Device check/status remain device-credential endpoints, but resolve eligibility from platform rollout data. Remove Organization Admin mutation controls and add an Updates workspace to Platform Control.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core/PostgreSQL, xUnit, React 19, TypeScript, Bun, GitHub Actions, PowerShell.

## Global Constraints

- Managed updates use immutable versioned URIs; `latest` is compatibility-only.
- Only `validated` packages may start rollouts; retired packages are never offered.
- Only platform tokens or a least-privilege platform release credential mutate releases.
- Organization staff retain read-only status and cannot mutate packages or rollouts.
- Batching is deterministic and monotonic; each component gets at most one newest instruction.
- Big-bang development migration: remove obsolete endpoints instead of preserving aliases.

---

### Task 1: Platform contracts, permissions, and schema

**Files:**
- Create: `src/AFK4.Shared.Contracts/Platform/Updates/PlatformUpdateContracts.cs`
- Modify: `src/AFK4.Shared.Contracts/Platform/Auth/PlatformAdminPermissionNames.cs`
- Modify: `src/AFK4.Platform.Api/Platform/Identity/PlatformAdminPermissionCatalog.cs`
- Modify: `src/AFK4.Platform.Api/Data/UpdatePackageEntity.cs`
- Modify: `src/AFK4.Platform.Api/Data/UpdateRolloutEntity.cs`
- Modify: `src/AFK4.Platform.Api/Data/UpdateRolloutTargetEntity.cs`
- Modify: `src/AFK4.Platform.Api/Data/PlatformDbContext.cs`
- Create: `src/AFK4.Platform.Api/Data/Migrations/20260729120000_MoveUpdatesToPlatformControl.cs`
- Test: `tests/AFK4.Shared.Contracts.Tests/Platform/PlatformUpdateContractSerializationTests.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/PlatformUpdatePersistenceTests.cs`

**Interfaces:**
- Produces `PlatformUpdatePackageDto`, `CreatePlatformUpdatePackageRequest`, `ChangePlatformUpdatePackageStateRequest`, `PlatformUpdateRolloutDto`, `CreatePlatformUpdateRolloutRequest`, `ChangePlatformUpdateRolloutStateRequest`.
- Produces platform permissions `updates.packages.manage`, `updates.rollouts.manage`, `updates.view`.
- Package is global; rollout targets contain optional organization, branch, and device scope plus `CreatedByPlatformAdminUserId`.

- [ ] **Step 1: Write failing contract/schema tests.** Assert JSON round-trips, uniqueness `(Component, Version, Channel)`, nullable target scope, platform actor IDs, and absence of package branch/staff ownership.
- [ ] **Step 2: Run RED.** Run focused Shared Contracts and Platform API tests; expect missing types/schema.
- [ ] **Step 3: Implement minimal contracts/entities and generate the EF migration.** The migration drops/recreates unused update release tables but preserves device status history where references remain valid.
- [ ] **Step 4: Run GREEN and inspect `dotnet ef migrations script`.** Confirm no unrelated tables change.
- [ ] **Step 5: Commit** as `feat(updates): add platform release catalog`.

### Task 2: Platform-authorized release endpoints

**Files:**
- Create: `src/AFK4.Platform.Api/Updates/IPlatformUpdateReleaseService.cs`
- Create: `src/AFK4.Platform.Api/Updates/EfPlatformUpdateReleaseService.cs`
- Create: `src/AFK4.Platform.Api/Endpoints/PlatformUpdateEndpoints.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs`
- Modify: `src/AFK4.Platform.Api/Audit/AuditActionNames.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/PlatformUpdateEndpointTests.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Architecture/AuthenticationDomainEndpointTests.cs`

**Interfaces:**
- Produces `/api/platform/updates/packages`, `/packages/{id}/state`, `/rollouts`, `/rollouts/{id}/state` list/mutation endpoints.
- Every mutation consumes a platform permission and writes the platform actor and reason to audit.

- [ ] **Step 1: Write failing endpoint tests** for unauthenticated, organization-token, missing-permission, success, audit, duplicate package, invalid scope, registered-package rollout, and retired-package rollout cases.
- [ ] **Step 2: Run RED.** Expect route-not-found and missing behavior failures.
- [ ] **Step 3: Implement using existing Platform endpoints/auth/idempotency patterns.** Never add organization-domain aliases.
- [ ] **Step 4: Run GREEN, then full Platform API tests.**
- [ ] **Step 5: Commit** as `feat(updates): authorize releases through Platform Control`.

### Task 3: Deterministic rollout eligibility

**Files:**
- Create: `src/AFK4.Platform.Api/Updates/UpdateRolloutBucket.cs`
- Modify: `src/AFK4.Platform.Api/Updates/EfUpdateService.cs`
- Modify: `src/AFK4.Platform.Api/Updates/IUpdateService.cs`
- Test: `tests/AFK4.Platform.Api.Tests/EfUpdateServiceTests.cs`

**Interfaces:**
- `UpdateRolloutBucket.GetBucket(Guid rolloutId, Guid deviceId)` hashes RFC-4122 bytes with SHA-256, reads the first unsigned big-endian 32-bit value, and returns modulo 100.
- Device check returns at most one highest eligible version per component.

- [ ] **Step 1: Write failing tests** with fixed bucket vectors, repeatability, 1/100% boundaries, monotonic widening, explicit targets, retired exclusion, and multiple-version selection.
- [ ] **Step 2: Run RED.** Confirm current batching and multi-rollout behavior fail.
- [ ] **Step 3: Apply target, lifecycle, role, time, channel, and bucket filters before grouping; select highest semantic version with newest rollout as tie-breaker.**
- [ ] **Step 4: Run GREEN and full API tests.**
- [ ] **Step 5: Commit** as `fix(updates): enforce deterministic staged rollout`.

### Task 4: Platform Control Updates workspace

**Files:**
- Create: `src/AFK4.PlatformControl.Web/src/api/platformClients/updates.ts`
- Create: `src/AFK4.PlatformControl.Web/src/platform/updates/UpdatesScreen.tsx`
- Create: `src/AFK4.PlatformControl.Web/src/platform/updates/usePlatformUpdates.ts`
- Test: `src/AFK4.PlatformControl.Web/src/platform/updates/UpdatesScreen.test.tsx`
- Modify: `src/AFK4.PlatformControl.Web/src/api/types.ts`
- Modify: `src/AFK4.PlatformControl.Web/src/api/platformApi.ts`
- Modify: `src/AFK4.PlatformControl.Web/src/platform/nav.ts`
- Modify: `src/AFK4.PlatformControl.Web/src/App.tsx`
- Modify: `src/AFK4.PlatformControl.Web/src/i18n/messages.ts`

**Interfaces:**
- Consumes Task 2 endpoints; provides package catalog, validation/retirement, rollout targets, batch percentage, lifecycle actions, and device progress.

- [ ] **Step 1: Write failing UI tests** for permission-aware nav, loading/empty/error, validated-only rollout, target summary, confirmations, and device errors.
- [ ] **Step 2: Run RED** with `bun test src/platform/updates src/platform/nav.test.ts`.
- [ ] **Step 3: Implement with existing Table/Dialog/Select/Badge/Skeleton/toast/i18n patterns.** Do not expose secrets.
- [ ] **Step 4: Run full `bun test` and `bun run build`.**
- [ ] **Step 5: Commit** as `feat(platform-control): manage client update releases`.

### Task 5: Remove Organization Admin release mutation

**Files:**
- Delete: `src/AFK4.OrganizationAdmin.App/Updates/UpdateStatusWorkspaceViewModel.cs`
- Delete: `src/AFK4.OrganizationAdmin.App/Updates/HttpOperatorUpdateApiClient.cs`
- Delete: `src/AFK4.OrganizationAdmin.App/Updates/IOperatorUpdateApiClient.cs`
- Delete: `src/AFK4.OrganizationAdmin.App/Updates/UnconfiguredOperatorUpdateApiClient.cs`
- Modify: `src/AFK4.OrganizationAdmin.App/Settings/SettingsWorkspaceViewModel.cs`
- Modify: `src/AFK4.OrganizationAdmin.App/MainWindow.xaml`
- Modify: `src/AFK4.OrganizationAdmin.App/MainWindow.xaml.cs`
- Modify/Delete: matching `tests/AFK4.OrganizationAdmin.App.Tests/*Update*` tests
- Modify: `src/AFK4.OrganizationAdmin.Web/src/api/clients/updates.ts`
- Test: `src/AFK4.OrganizationAdmin.Web/src/settingsSectionsSmoke.test.tsx`
- Test: `tests/AFK4.Platform.Api.Tests/UpdateEndpointTests.cs`

**Interfaces:**
- Removes tenant package/rollout mutation routes, clients, and controls; preserves permission-filtered read-only device status.

- [ ] **Step 1: Write failing boundary tests** asserting tenant mutation denial and absence of URI/hash/signature/package/rollout controls.
- [ ] **Step 2: Run RED** across focused API, App, and Web tests.
- [ ] **Step 3: Delete routes and dead UI wiring rather than hiding them.**
- [ ] **Step 4: Run focused/full Organization Admin tests and production build.**
- [ ] **Step 5: Commit** as `refactor(updates): remove organization release controls`.

### Task 6: CI cutover, docs, and gate

**Files:**
- Modify: `scripts/register-update-package-requests.ps1`
- Modify: `.github/workflows/client-packages.yml`
- Modify: `.github/workflows/package-smoke.yml`
- Modify: `tests/AFK4.Agent.Service.Tests/ClientReleaseAutomationTests.cs`
- Modify: `docs/product/AFK4-MVP-PRD.md`
- Modify: `docs/superpowers/specs/2026-05-12-afk4-platform-architecture-design.md`
- Modify: `docs/operations/update-package-publishing.md`
- Modify: `docs/operations/client-update-rollout.md`
- Modify: `docs/roadmap/production-readiness.md`
- Modify: `docs/progress/2026-05-12-vertical-slice-progress.md`

**Interfaces:**
- Package CI authenticates with a least-privilege platform release credential, validates before rollout, targets a canary device, and never restarts the API.

- [ ] **Step 1: Write failing automation assertions** for platform auth, validate-before-rollout, immutable URI, stable-alias separation, and absence of staff/Coolify restart calls.
- [ ] **Step 2: Run RED** with the focused release automation tests.
- [ ] **Step 3: Update workflows/scripts/docs without logging credentials.**
- [ ] **Step 4: Run full Shared Contracts/API/Agent tests, both frontend tests/builds, full Release solution build, and `git diff --check`.**
- [ ] **Step 5: Commit CI and docs separately** as `ci(updates): publish through platform release boundary` and `docs(updates): document platform-managed rollout`.
