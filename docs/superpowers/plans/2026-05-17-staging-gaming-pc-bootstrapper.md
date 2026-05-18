# Staging Gaming PC Bootstrapper Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a staging-only Windows setup executable that installs and enrolls a clean Windows 11 Gaming PC VM without manual PowerShell on the VM.

**Architecture:** Add a WPF setup app under `src/AFK4.GamingPc.Setup` plus tests under `tests/AFK4.GamingPc.Setup.Tests`. The setup app uses existing shared contracts and backend endpoints, embeds the Gaming PC MSI at publish time, writes Agent machine configuration, starts `AFK4.Agent.Service`, and polls backend device detail for heartbeat evidence.

**Tech Stack:** .NET 10, WPF/MVVM, xUnit, existing `AFK4.Shared.Contracts`, existing WiX MSI build script.

---

### Task 1: Setup Defaults And Result Model

**Files:**
- Create: `src/AFK4.GamingPc.Setup/StagingSetupDefaults.cs`
- Create: `src/AFK4.GamingPc.Setup/SetupStepResult.cs`
- Create: `tests/AFK4.GamingPc.Setup.Tests/StagingSetupDefaultsTests.cs`
- Modify: `AFK4.sln`

- [ ] **Step 1: Write failing defaults tests**

Create tests asserting:

```csharp
Assert.Equal(new Uri("https://afk4.staging.mubi.dev"), StagingSetupDefaults.PlatformBaseUrl);
Assert.Equal(Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"), StagingSetupDefaults.OrganizationId);
Assert.Equal(Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"), StagingSetupDefaults.BranchId);
Assert.Equal("AFK4.Agent.Service", StagingSetupDefaults.AgentServiceName);
Assert.Equal("internal", StagingSetupDefaults.UpdateChannel);
Assert.EndsWith(@"AFK4\Player Shell\AFK4.Player.Shell.exe", StagingSetupDefaults.PlayerShellExecutablePath);
```

- [ ] **Step 2: Run tests and confirm they fail**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.GamingPc.Setup.Tests/AFK4.GamingPc.Setup.Tests.csproj -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

Expected: project or type missing.

- [ ] **Step 3: Add minimal project, defaults, and tests pass**

Add the setup and test projects. Implement `StagingSetupDefaults` and
`SetupStepResult` with immutable values only.

- [ ] **Step 4: Run targeted tests**

Expected: defaults tests pass.

### Task 2: API Client

**Files:**
- Create: `src/AFK4.GamingPc.Setup/SetupApiClient.cs`
- Create: `tests/AFK4.GamingPc.Setup.Tests/SetupApiClientTests.cs`

- [ ] **Step 1: Write failing tests using a fake HttpMessageHandler**

Cover health check URL, staff sign-in body, enrollment-code authenticated
request, device enrollment request, and device detail polling URL.

- [ ] **Step 2: Implement minimal client**

Use `HttpClient`, shared contracts, bearer token headers for staff endpoints,
and JSON serialization defaults.

- [ ] **Step 3: Run targeted tests**

Expected: API client tests pass.

### Task 3: Orchestrator

**Files:**
- Create: `src/AFK4.GamingPc.Setup/GamingPcSetupOrchestrator.cs`
- Create: `src/AFK4.GamingPc.Setup/SetupProgress.cs`
- Create: `tests/AFK4.GamingPc.Setup.Tests/GamingPcSetupOrchestratorTests.cs`

- [ ] **Step 1: Write failing orchestration tests**

Cover successful flow, sign-in failure stops before install, MSI failure stops
before configuration, service failure reports failed step, heartbeat timeout is
partial success.

- [ ] **Step 2: Implement minimal orchestrator and dependency interfaces**

Interfaces:

```csharp
ISetupApiClient
IGamingPcMsiInstaller
IAgentMachineConfigurationWriter
IWindowsServiceController
```

- [ ] **Step 3: Run targeted tests**

Expected: orchestrator tests pass.

### Task 4: Windows Implementation And WPF UI

**Files:**
- Create: `src/AFK4.GamingPc.Setup/MainWindow.xaml`
- Create: `src/AFK4.GamingPc.Setup/MainWindow.xaml.cs`
- Create: `src/AFK4.GamingPc.Setup/App.xaml`
- Create: `src/AFK4.GamingPc.Setup/App.xaml.cs`
- Create: `src/AFK4.GamingPc.Setup/SetupShellViewModel.cs`
- Create: `src/AFK4.GamingPc.Setup/GamingPcMsiInstaller.cs`
- Create: `src/AFK4.GamingPc.Setup/AgentMachineConfigurationWriter.cs`
- Create: `src/AFK4.GamingPc.Setup/WindowsServiceController.cs`
- Create: `src/AFK4.GamingPc.Setup/ElevationGuard.cs`

- [ ] **Step 1: Write ViewModel tests**

Cover command disabled without username/password, command enabled with both,
progress steps appended, and secrets not included in displayed status.

- [ ] **Step 2: Implement WPF shell and Windows adapters**

Keep UI minimal: environment label, machine name, username, password, install
button, progress list, final result.

- [ ] **Step 3: Run tests**

Expected: setup app tests pass.

### Task 5: Package Build Integration And Docs

**Files:**
- Modify: `scripts/build-client-packages.ps1`
- Modify: `README.md`
- Modify: `docs/operations/real-device-windows-pc-smoke.md`
- Modify: `docs/progress/2026-05-12-vertical-slice-progress.md`
- Modify: `docs/roadmap/production-readiness.md` if launch gates change

- [ ] **Step 1: Add build-script invariant test**

Extend existing client release automation tests to assert the script publishes
`AFK4.GamingPc.Setup` and emits
`artifacts/client-packages/afk4-gaming-pc-setup-<version>-<channel>.exe`.

- [ ] **Step 2: Update build script**

Build Gaming PC MSI first, then publish setup as a single-file Windows exe with
the MSI path passed as an embedded resource input.

- [ ] **Step 3: Update docs**

Make the VM path explicit: copy/run only the setup exe; no project, no manual
PowerShell on the VM.

- [ ] **Step 4: Run verification**

Run targeted setup tests, client release automation tests, solution build, and
full no-build tests.
