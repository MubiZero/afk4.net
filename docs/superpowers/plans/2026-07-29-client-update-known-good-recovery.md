# Client Update Known-Good Recovery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace fake MSI rollback with verified last-known-good recovery and local startup health confirmation.

**Architecture:** Retain one signed, health-confirmed MSI per component in protected Agent storage. After candidate install, launch with a one-time health token; promote only after confirmation, otherwise verify and restore known-good. Existing installs without cached material report exact manual recovery.

**Tech Stack:** .NET 10, Windows Installer/WiX 7, Windows named pipes, DPAPI, SHA-256/ECDSA, PowerShell, xUnit.

## Global Constraints

- Never report rollback success after reinstalling the failed candidate.
- Re-verify cached bytes against original signed immutable metadata.
- Keep one confirmed package plus one candidate per component.
- MSI transactional rollback handles installer failure first.
- Local health excludes cloud/auth availability.
- No known-good package means `manual-recovery-required`.
- Downgrade permission is restricted to authenticated Agent recovery.

---

### Task 1: Protected known-good store

**Files:**
- Create: `src/AFK4.Agent.Service/Updates/KnownGoodUpdatePackage.cs`
- Create: `src/AFK4.Agent.Service/Updates/IKnownGoodUpdatePackageStore.cs`
- Create: `src/AFK4.Agent.Service/Updates/FileKnownGoodUpdatePackageStore.cs`
- Modify: `src/AFK4.Agent.Service/AgentOptions.cs`
- Modify: `src/AFK4.Agent.Service/Program.cs`
- Test: `tests/AFK4.Agent.Service.Tests/FileKnownGoodUpdatePackageStoreTests.cs`

**Interfaces:**
- `StageCandidateAsync`, `PromoteCandidateAsync`, `GetKnownGoodAsync`, `DiscardCandidateAsync`.
- Metadata holds component/version/immutable URI/size/hash/signature/algorithm/release notes/path/confirmation time.

- [ ] **Step 1: Write failing tests** for atomic writes, copied bytes, per-component isolation, promotion, retention, corruption, traversal, cancellation, and absent initial package.
- [ ] **Step 2: Run RED.**
- [ ] **Step 3: Implement temp-file plus atomic replace under protected Agent storage and component allowlist paths.**
- [ ] **Step 4: Run GREEN and full Agent tests.**
- [ ] **Step 5: Commit** as `feat(updates): retain verified known-good packages`.

### Task 2: Verification and version identity

**Files:**
- Modify: `src/AFK4.Agent.Service/Updates/IUpdatePackageVerifier.cs`
- Modify: `src/AFK4.Agent.Service/Updates/Sha256UpdatePackageVerifier.cs`
- Modify: `src/AFK4.Agent.Service/Updates/SafeUpdateInstaller.cs`
- Modify: `src/AFK4.Agent.Service/Updates/AgentComponentVersionProvider.cs`
- Test: `tests/AFK4.Agent.Service.Tests/Sha256UpdatePackageVerifierTests.cs`
- Test: `tests/AFK4.Agent.Service.Tests/SafeUpdateInstallerTests.cs`

**Interfaces:**
- One canonical signature payload verifies rollout and cached metadata.
- Safe installer reads current version through `IAgentComponentVersionProvider`; configured Organization Admin is never `unknown`.

- [ ] **Step 1: Write failing tests** for current `unknown`, cache hash mismatch, metadata tampering, wrong identity, and valid cache.
- [ ] **Step 2: Run RED.**
- [ ] **Step 3: Implement canonical verification/version lookup without weakening local verification.**
- [ ] **Step 4: Run GREEN and full Agent tests.**
- [ ] **Step 5: Commit** as `fix(updates): preserve verified recovery identity`.

### Task 3: Startup health protocol

**Files:**
- Create: `src/AFK4.Shared.Contracts/Updates/ComponentStartupHealthMessage.cs`
- Create: `src/AFK4.Agent.Service/Updates/IComponentStartupHealthMonitor.cs`
- Create: `src/AFK4.Agent.Service/Updates/NamedPipeComponentStartupHealthMonitor.cs`
- Create: `src/AFK4.OrganizationAdmin.App/Updates/OrganizationAdminStartupHealthReporter.cs`
- Modify: `src/AFK4.OrganizationAdmin.App/App.xaml.cs`
- Modify: `src/AFK4.OrganizationAdmin.App/Web/OrganizationAdminWindow.xaml.cs`
- Modify: `src/AFK4.Agent.Service/AgentOptions.cs`
- Test: `tests/AFK4.Agent.Service.Tests/NamedPipeComponentStartupHealthMonitorTests.cs`
- Test: `tests/AFK4.OrganizationAdmin.App.Tests/OrganizationAdminStartupHealthReporterTests.cs`

**Interfaces:**
- One-time token binds component/version/package; app confirms native host, Web assets, WebView2, config, and Agent pipe readiness.

- [ ] **Step 1: Write failing tests** for valid, wrong/expired/replayed token, wrong version, partial readiness, timeout/crash, and offline success.
- [ ] **Step 2: Run RED.**
- [ ] **Step 3: Implement local protocol; expose token only to launched child and redact logs.**
- [ ] **Step 4: Run GREEN and full Agent/App tests.**
- [ ] **Step 5: Commit** as `feat(updates): verify client startup health`.

### Task 4: Recovery state machine

**Files:**
- Create: `src/AFK4.Agent.Service/Updates/IKnownGoodRecoveryExecutor.cs`
- Create: `src/AFK4.Agent.Service/Updates/KnownGoodRecoveryExecutor.cs`
- Modify: `src/AFK4.Agent.Service/Updates/SafeUpdateInstaller.cs`
- Delete: `src/AFK4.Agent.Service/Updates/ExternalProcessUpdateRollbackExecutor.cs`
- Modify: `src/AFK4.Agent.Service/Updates/UpdateRecoveryService.cs`
- Modify: `src/AFK4.Agent.Service/Updates/UpdateInstallState.cs`
- Modify: `src/AFK4.Shared.Contracts/Updates/UpdateStatusNames.cs`
- Test: `tests/AFK4.Agent.Service.Tests/KnownGoodRecoveryExecutorTests.cs`
- Test: `tests/AFK4.Agent.Service.Tests/UpdateRecoveryServiceTests.cs`
- Test: `tests/AFK4.Agent.Service.Tests/SafeUpdateInstallerTests.cs`

**Interfaces:**
- Candidate path: staged -> installed -> health-checking -> promoted; failure path: rollback-required -> verified restore -> rolled-back/manual-recovery-required.

- [ ] **Step 1: Write failing tests** for MSI failure without reinstall, promotion, health timeout/recovery, corrupt cache, restore failure, interrupted recovery, and missing cache.
- [ ] **Step 2: Run RED and prove current same-artifact rollback fails.**
- [ ] **Step 3: Persist each transition before external actions and remove old rollback registration.**
- [ ] **Step 4: Run GREEN and full Agent tests.**
- [ ] **Step 5: Commit** as `fix(updates): restore verified known-good package`.

### Task 5: Controlled MSI recovery

**Files:**
- Modify: `installers/organization-admin/Package.wxs`
- Modify: `scripts/install-afk4-update-msi.ps1`
- Delete: `scripts/rollback-afk4-update-msi.ps1`
- Modify: `src/AFK4.SetupWizard.Core/AgentBootstrapValues.cs`
- Modify: `tests/AFK4.Agent.Service.Tests/UpdateHelperScriptTests.cs`
- Create: `tests/AFK4.SetupWizard.Tests/AgentBootstrapValuesTests.cs`

**Interfaces:**
- Helper accepts `-Mode Install|Recover` and Agent-generated protected recovery authorization for the exact component/version/hash.
- Normal install rejects downgrade; only validated recovery permits the recorded known-good version.

- [ ] **Step 1: Write failing tests** for fake helper removal, authorization/mismatch, normal downgrade rejection, recovery allowance, redaction, and service survival.
- [ ] **Step 2: Run RED.**
- [ ] **Step 3: Implement secure WiX/helper recovery; never enable unrestricted `AllowDowngrades` globally.**
- [ ] **Step 4: Run GREEN and build prerelease MSI on Windows.**
- [ ] **Step 5: Commit** as `fix(installer): authorize known-good recovery only`.

### Task 6: Physical recovery evidence and docs

**Files:**
- Modify: `docs/operations/client-update-rollout.md`
- Modify: `docs/operations/real-device-windows-pc-smoke.md`
- Modify: `docs/operations/client-packaging.md`
- Modify: `docs/roadmap/production-readiness.md`
- Modify: `docs/progress/2026-05-12-vertical-slice-progress.md`
- Modify: `docs/superpowers/specs/2026-07-29-platform-managed-client-updates-design.md`

**Interfaces:**
- Produces version/hash/process/service/status proof for success and every recovery branch.

- [ ] **Step 1: Define exact A/B package smoke scenarios** for success, MSI failure, health failure/recovery, corrupt cache, and absent cache before running them.
- [ ] **Step 2: Run full solution/frontends/package automation gates and `git diff --check`.**
- [ ] **Step 3: Run physical Windows smoke and capture non-secret evidence; Agent must remain manageable throughout.**
- [ ] **Step 4: Archive long logs and update current docs with only fresh results/gaps.**
- [ ] **Step 5: Commit** as `docs(updates): record known-good recovery proof` after evidence exists.
