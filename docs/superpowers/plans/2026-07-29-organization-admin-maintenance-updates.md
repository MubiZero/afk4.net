# Organization Admin Maintenance Updates Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Update Organization Admin without interrupting critical club work, using a maintenance window or an explicit operator-approved restart.

**Architecture:** Store a branch-local window, expose read/update preferences to Organization Admin, and coordinate native app shutdown with Agent through an authenticated local named pipe. Agent installs immediately only when the app is closed; otherwise it defers until approval/window and requires graceful-exit acknowledgement.

**Tech Stack:** .NET 10, WPF/WebView2, React/TypeScript, Windows named pipes, EF Core/PostgreSQL, xUnit, Bun.

## Global Constraints

- Agent is the only installer/package authority.
- Default window is 04:00-05:00 in the branch timezone.
- Never auto-close while a critical backend command is in flight.
- Expected postponement is `deferred`, not `failed`.
- Restart-now bypasses the clock, not critical-command safety.
- Local protocol is authenticated and carries no release authority.

---

### Task 1: Preferences and statuses

**Files:**
- Create: `src/AFK4.Shared.Contracts/Updates/OrganizationAdminUpdatePreferenceDto.cs`
- Create: `src/AFK4.Shared.Contracts/Updates/UpdateOrganizationAdminUpdatePreferenceRequest.cs`
- Modify: `src/AFK4.Shared.Contracts/Updates/UpdateStatusNames.cs`
- Modify: `src/AFK4.Platform.Api/Data/BranchEntity.cs`
- Create: `src/AFK4.Platform.Api/Data/Migrations/20260729130000_AddOrganizationAdminMaintenanceWindow.cs`
- Modify: `src/AFK4.Platform.Api/Endpoints/UpdateEndpoints.cs`
- Test: `tests/AFK4.Shared.Contracts.Tests/UpdateContractSerializationTests.cs`
- Test: `tests/AFK4.Platform.Api.Tests/UpdateEndpointTests.cs`

**Interfaces:**
- `GET/PUT /api/organizations/{organizationId}/branches/{branchId}/updates/preferences`.
- Statuses: `deferred`, `ready-to-install`, `awaiting-app-exit`, `health-checking`, `rollback-required`.

- [ ] **Step 1: Write failing tests** for default, valid window, zero-length/invalid window, timezone, permissions, isolation, and status JSON.
- [ ] **Step 2: Run RED** in Shared Contracts/API suites.
- [ ] **Step 3: Implement `TimeOnly` persistence and endpoints; evaluate with existing `PreferredTimeZone`.**
- [ ] **Step 4: Run GREEN and inspect migration SQL.**
- [ ] **Step 5: Commit** as `feat(updates): add admin maintenance preferences`.

### Task 2: Authenticated local coordination

**Files:**
- Create: `src/AFK4.Shared.Contracts/Updates/LocalUpdateCoordinationMessage.cs`
- Create: `src/AFK4.Agent.Service/Updates/IOrganizationAdminUpdateCoordinatorClient.cs`
- Create: `src/AFK4.Agent.Service/Updates/NamedPipeOrganizationAdminUpdateCoordinatorClient.cs`
- Create: `src/AFK4.OrganizationAdmin.App/Updates/NamedPipeUpdateCoordinationServer.cs`
- Create: `src/AFK4.OrganizationAdmin.App/Updates/OrganizationAdminActivityState.cs`
- Modify: `src/AFK4.Agent.Service/AgentOptions.cs`
- Modify: `src/AFK4.Agent.Service/Program.cs`
- Modify: `src/AFK4.OrganizationAdmin.App/App.xaml.cs`
- Test: `tests/AFK4.Agent.Service.Tests/NamedPipeOrganizationAdminUpdateCoordinatorClientTests.cs`
- Test: `tests/AFK4.OrganizationAdmin.App.Tests/NamedPipeUpdateCoordinationServerTests.cs`

**Interfaces:**
- `QueryStateAsync` and `RequestShutdownAsync(rolloutId, packageId)` return `not-running`, `idle`, `critical-command-active`, or `shutdown-acknowledged`.
- A random Setup-provisioned machine secret authenticates length-prefixed JSON; pipe ACL permits LocalSystem and the interactive AFK4 user.

- [ ] **Step 1: Write failing protocol tests** for bad secret, wrong IDs, unavailable/idle/busy app, timeout, cancellation, and persisted acknowledgement.
- [ ] **Step 2: Run RED.**
- [ ] **Step 3: Implement using existing Player Shell pipe framing, but a separate pipe/contracts/ACL.**
- [ ] **Step 4: Run GREEN and full Agent/App tests.**
- [ ] **Step 5: Commit** as `feat(updates): coordinate admin shutdown over local pipe`.

### Task 3: Track critical commands

**Files:**
- Create: `src/AFK4.OrganizationAdmin.Web/src/updateActivity.ts`
- Modify: `src/AFK4.OrganizationAdmin.Web/src/api/clients/index.ts`
- Modify: `src/AFK4.OrganizationAdmin.App/Web/OrganizationAdminWebHostBridge.cs`
- Modify: `src/AFK4.OrganizationAdmin.App/Updates/OrganizationAdminActivityState.cs`
- Test: `src/AFK4.OrganizationAdmin.Web/src/updateActivity.test.ts`
- Test: `tests/AFK4.OrganizationAdmin.App.Tests/OrganizationAdminActivityStateTests.cs`

**Interfaces:**
- WebView sends reference-counted start/finish messages for money, session, POS, device, and shift mutations; native state exposes `HasCriticalCommandInFlight`.

- [ ] **Step 1: Write failing tests** for concurrency, failure cleanup, read exclusion, explicit mutation classification, and malformed messages.
- [ ] **Step 2: Run RED** in Bun/App tests.
- [ ] **Step 3: Implement explicit mutation allowlist and native bridge counter.**
- [ ] **Step 4: Run GREEN and full Web/App tests.**
- [ ] **Step 5: Commit** as `feat(organization-admin): protect critical work during updates`.

### Task 4: Agent deferral and relaunch

**Files:**
- Create: `src/AFK4.Agent.Service/Updates/OrganizationAdminUpdateReadiness.cs`
- Create: `src/AFK4.Agent.Service/Updates/OrganizationAdminProcessLauncher.cs`
- Modify: `src/AFK4.Agent.Service/Updates/AgentUpdateCoordinator.cs`
- Modify: `src/AFK4.Agent.Service/Updates/SafeUpdateInstaller.cs`
- Modify: `src/AFK4.Agent.Service/Updates/AgentUpdateWorker.cs`
- Modify: `src/AFK4.Agent.Service/AgentOptions.cs`
- Test: `tests/AFK4.Agent.Service.Tests/OrganizationAdminUpdateReadinessTests.cs`
- Test: `tests/AFK4.Agent.Service.Tests/AgentUpdateCoordinatorTests.cs`

**Interfaces:**
- Readiness result: `InstallNow`, `DeferredOutsideWindow`, `DeferredCriticalCommand`, `ReadyAfterShutdown`.
- Deferred rollout clears its in-memory attempted mark and retries only on normal polling cadence.

- [ ] **Step 1: Write failing tests** for closed/idle/restart-now, midnight/DST, critical command, exit timeout, installer failure, and exactly-once relaunch.
- [ ] **Step 2: Run RED; confirm current immediate install.**
- [ ] **Step 3: Gate install with readiness and precise statuses; restart Agent for version reload, then launch app once.**
- [ ] **Step 4: Run GREEN and full Agent tests.**
- [ ] **Step 5: Commit** as `feat(agent): defer admin updates safely`.

### Task 5: Organization Admin status card

**Files:**
- Create: `src/AFK4.OrganizationAdmin.Web/src/settings/OrganizationAdminUpdateCard.tsx`
- Test: `src/AFK4.OrganizationAdmin.Web/src/settings/OrganizationAdminUpdateCard.test.tsx`
- Modify: `src/AFK4.OrganizationAdmin.Web/src/api/clients/updates.ts`
- Modify: `src/AFK4.OrganizationAdmin.Web/src/BackendSettingsWorkspace.tsx`
- Modify: `src/AFK4.OrganizationAdmin.Web/src/i18n/messages.ts`
- Modify: `src/AFK4.OrganizationAdmin.App/Web/OrganizationAdminWebHostBridge.cs`

**Interfaces:**
- Shows installed/offered version, progress, safe exact error, maintenance window, `Restart and update`, `Later`; restart is rollout/package-bound via native bridge.

- [x] **Step 1: Write failing UI tests** for empty/deferred/progress/failure states, permissions, save, restart disablement, focus, and keyboard.
- [x] **Step 2: Run RED.**
- [x] **Step 3: Implement using existing settings/i18n primitives; never show URI/signature internals.**
- [x] **Step 4: Run full Bun tests and production build.**
- [x] **Step 5: Commit** as `feat(organization-admin): add safe update controls`.

### Task 6: Provision and prove Windows lifecycle

**Files:**
- Modify: `src/AFK4.SetupWizard.Core/AgentBootstrapValues.cs`
- Modify: `src/AFK4.SetupWizard.Core/EnvironmentBootstrapWriter.cs`
- Modify: `installers/organization-admin/Package.wxs`
- Create: `tests/AFK4.SetupWizard.Tests/AgentBootstrapValuesTests.cs`
- Modify: `tests/AFK4.Agent.Service.Tests/UpdateHelperScriptTests.cs`
- Modify: `docs/operations/client-update-rollout.md`
- Modify: `docs/operations/real-device-windows-pc-smoke.md`
- Modify: `docs/progress/2026-05-12-vertical-slice-progress.md`

**Interfaces:**
- Setup provisions pipe name/secret and app path without logging secrets; smoke proves app closed, idle-open, and critical-command-open.

- [ ] **Step 1: Write failing provisioning/package tests** for coordination config, protected secret, executable path, and log redaction.
- [ ] **Step 2: Run RED.**
- [ ] **Step 3: Implement provisioning and exact smoke runbook/status expectations.**
- [ ] **Step 4: Run full solution/frontends/package gates and physical Windows scenarios.**
- [ ] **Step 5: Commit code/docs in coherent units; record only fresh evidence.**
