# AFK4 Phase 8 Agent Enforcement And Player Shell Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the Agent and Player Shell from session-aware skeletons into the first enforceable Windows gaming-PC runtime: local lock/unlock coordination, signed-lease grace behavior, Shell supervision, Player Shell session UI, basic launcher, and process policy foundation.

**Architecture:** Keep the Cloud Backend as the business authority. The Agent executes backend-approved commands and locally enforces the last valid signed lease during temporary connectivity loss. The Player Shell is an untrusted UI process controlled by the Agent through local IPC. Windows control is MVP-grade and must not use kernel drivers.

**Tech Stack:** .NET 10, Worker Service, WPF + MVVM, named pipes for local Agent/Shell IPC, `System.Diagnostics.Process`, Windows-friendly service abstractions, xUnit, EF-backed backend APIs already implemented in Phases 1-7.

---

## Scope

Phase 8 implements:

- persistent Agent runtime state and signed-lease storage for reboot recovery;
- Agent lock/unlock enforcement coordinator driven by backend device commands;
- lease expiry and grace-mode lock behavior;
- Player Shell process supervision/watchdog from the Agent;
- local Agent-to-Shell state publishing over named pipes;
- Player Shell locked, active-session, warning, offline-grace, and launcher screens;
- local allow-list based launcher command flow from Shell to Agent;
- process allow/deny policy foundation with testable dry-run adapters;
- heartbeat/reconciliation payloads that report actual lock/session state instead of hardcoded locked state;
- focused local smoke for Agent, Shell, backend session commands, and reboot-style restart behavior.

Phase 8 does not implement:

- centralized updates, installers, rollout, rollback, or package signing;
- reports, audit search, diagnostics dashboards, or backup/restore runbooks;
- web admin, local club server, microservices, Linux/macOS agents, or kernel drivers;
- country-specific fiscal/payment integrations;
- advanced game library management or automatic Steam/Epic/Battle.net updates;
- trusting the Player Shell for billing, authorization, session rights, or process policy decisions.

## Current Baseline

Start from `main` commit `1dc8dd2 docs: record phase 7 merge`.

Already available and reused:

- backend session start, extend, transfer, end, signed lease, reconciliation, and device command endpoints;
- Agent credential-authenticated heartbeat, SignalR command channel, command result reporting, and installed-app reporting;
- Agent signed-lease validator and in-memory lease store;
- Agent reconciliation reporter;
- WPF Player Shell fullscreen locked placeholder;
- Operator App production UX for starting, extending, ending sessions, and seeing floor-map state.

Known baseline gaps Phase 8 addresses:

- `Worker` currently builds heartbeat and reconciliation payloads with hardcoded `isLocked: true`;
- `DefaultDeviceCommandHandler` validates/stores leases but does not enforce Windows state;
- current lease state is memory-only and is lost on Agent restart;
- Player Shell has no ViewModel, session screen, IPC, launcher, or tests;
- Agent does not supervise or restart Player Shell;
- process policy is not represented locally.

## File Structure

Create and modify these files:

```text
D:\afk4.net\
  docs\progress\2026-05-12-vertical-slice-progress.md
  docs\superpowers\plans\2026-05-14-afk4-phase8-agent-enforcement-player-shell.md
  AFK4.sln
  src\AFK4.Shared.Contracts\
    Shell\LauncherAppDto.cs
    Shell\PlayerShellCommandDto.cs
    Shell\PlayerShellCommandResultDto.cs
    Shell\PlayerShellStateDto.cs
    Shell\PlayerShellStateNames.cs
  src\AFK4.Agent.Service\
    AgentOptions.cs
    DefaultDeviceCommandHandler.cs
    HeartbeatPayloadFactory.cs
    Program.cs
    Worker.cs
    Enforcement\AgentRuntimeState.cs
    Enforcement\AgentRuntimeStateStore.cs
    Enforcement\FileSessionLeaseStore.cs
    Enforcement\GraceModeMonitor.cs
    Enforcement\IAgentRuntimeStateStore.cs
    Enforcement\IProcessPolicyEnforcer.cs
    Enforcement\IProcessLauncher.cs
    Enforcement\ISessionEnforcementCoordinator.cs
    Enforcement\IWorkstationLockController.cs
    Enforcement\ProcessPolicyEnforcer.cs
    Enforcement\SessionEnforcementCoordinator.cs
    Enforcement\WorkstationLockController.cs
    Shell\IPlayerShellCommandHandler.cs
    Shell\IPlayerShellProcessSupervisor.cs
    Shell\IPlayerShellStatePublisher.cs
    Shell\NamedPipePlayerShellStateServer.cs
    Shell\PlayerShellCommandHandler.cs
    Shell\PlayerShellProcessSupervisor.cs
  src\AFK4.Player.Shell\
    App.xaml.cs
    MainWindow.xaml
    MainWindow.xaml.cs
    Configuration\PlayerShellOptions.cs
    Launcher\LauncherAppViewModel.cs
    Launcher\LauncherCommandClient.cs
    Launcher\ILauncherCommandClient.cs
    Mvvm\RelayCommand.cs
    Realtime\IPlayerShellStateClient.cs
    Realtime\NamedPipePlayerShellStateClient.cs
    Shell\PlayerShellViewModel.cs
    Shell\RemainingTimeFormatter.cs
  tests\
    AFK4.Shared.Contracts.Tests\PlayerShellContractSerializationTests.cs
    AFK4.Agent.Service.Tests\AgentRuntimeStateStoreTests.cs
    AFK4.Agent.Service.Tests\FileSessionLeaseStoreTests.cs
    AFK4.Agent.Service.Tests\SessionEnforcementCoordinatorTests.cs
    AFK4.Agent.Service.Tests\GraceModeMonitorTests.cs
    AFK4.Agent.Service.Tests\PlayerShellProcessSupervisorTests.cs
    AFK4.Agent.Service.Tests\PlayerShellCommandHandlerTests.cs
    AFK4.Agent.Service.Tests\WorkerEnforcementIntegrationTests.cs
    AFK4.Player.Shell.Tests\AFK4.Player.Shell.Tests.csproj
    AFK4.Player.Shell.Tests\PlayerShellViewModelTests.cs
    AFK4.Player.Shell.Tests\RemainingTimeFormatterTests.cs
    AFK4.Player.Shell.Tests\LauncherCommandClientTests.cs
```

Responsibility boundaries:

- `AFK4.Shared.Contracts.Shell`: local transport DTOs between Agent and Player Shell only.
- `AFK4.Agent.Service.Enforcement`: local state, lease persistence, lock/unlock coordination, grace checks, launcher/process policy decisions.
- `AFK4.Agent.Service.Shell`: Shell process supervision plus named-pipe server and command handling.
- `AFK4.Player.Shell.Shell`: WPF/MVVM state projection. It displays what the Agent says; it does not authorize itself.
- `AFK4.Player.Shell.Launcher`: local UI command client. The Agent validates and launches allowed apps.

## Local Runtime Rules

State names in Shell contracts:

```text
locked
active
grace
ending
maintenance
offline
error
```

Agent rules:

- unlock is allowed only after a valid backend-signed lease is received through an `unlock` or `refresh-session-lease` command;
- lock clears the matching local lease and drives Shell into `locked`;
- lease expiry immediately returns the runtime to `locked`;
- on Agent startup, a persisted unexpired lease may be reported during reconciliation, but the Agent must not unlock until backend reconciliation returns `continue` or `unlock`;
- Player Shell process is supervised while the PC is locked, active, in grace, or ending;
- Player Shell launcher requests are advisory UI commands until the Agent validates the app id/path against local policy;
- process policy is best-effort MVP enforcement without kernel driver guarantees.

## Task 1: Shared Player Shell Contracts

**Files:**

- Create: `src\AFK4.Shared.Contracts\Shell\PlayerShellStateNames.cs`
- Create: `src\AFK4.Shared.Contracts\Shell\LauncherAppDto.cs`
- Create: `src\AFK4.Shared.Contracts\Shell\PlayerShellStateDto.cs`
- Create: `src\AFK4.Shared.Contracts\Shell\PlayerShellCommandDto.cs`
- Create: `src\AFK4.Shared.Contracts\Shell\PlayerShellCommandResultDto.cs`
- Create: `tests\AFK4.Shared.Contracts.Tests\PlayerShellContractSerializationTests.cs`

- [ ] **Step 1: Write failing Shell contract serialization tests**

Cover JSON round-trips for:

- locked state with no session;
- active state with session id, lease expiry, remaining seconds, warning threshold, and allowed launcher apps;
- launcher command request/result with app id and correlation id.

- [ ] **Step 2: Run tests and verify RED**

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter PlayerShellContractSerializationTests --no-restore -p:UseSharedCompilation=false
```

- [ ] **Step 3: Implement contracts**

`PlayerShellStateDto` must include organization, branch, device, shell state, optional session id, lease expiry, remaining seconds, connectivity/grace flags, message, and allowed launcher apps.

`PlayerShellCommandDto` must include command id, type, created timestamp, and payload. Initial command types are `launch-app`, `acknowledge-warning`, and `shell-ready`.

- [ ] **Step 4: Run tests and commit**

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter PlayerShellContractSerializationTests --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\Git\cmd\git.exe' add src/AFK4.Shared.Contracts tests/AFK4.Shared.Contracts.Tests
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat: add player shell local contracts"
```

## Task 2: Persistent Agent Runtime And Lease State

**Files:**

- Modify: `src\AFK4.Agent.Service\AgentOptions.cs`
- Modify: `src\AFK4.Agent.Service\Program.cs`
- Create: `src\AFK4.Agent.Service\Enforcement\AgentRuntimeState.cs`
- Create: `src\AFK4.Agent.Service\Enforcement\IAgentRuntimeStateStore.cs`
- Create: `src\AFK4.Agent.Service\Enforcement\AgentRuntimeStateStore.cs`
- Create: `src\AFK4.Agent.Service\Enforcement\FileSessionLeaseStore.cs`
- Modify: `src\AFK4.Agent.Service\ISessionLeaseStore.cs`
- Test: `tests\AFK4.Agent.Service.Tests\AgentRuntimeStateStoreTests.cs`
- Test: `tests\AFK4.Agent.Service.Tests\FileSessionLeaseStoreTests.cs`

- [ ] **Step 1: Write failing state-store tests**

Cover:

- initial state is locked with no active lease;
- saving an active lease persists the lease to disk;
- a new store instance loads the unexpired lease after restart;
- clearing a session removes persisted lease data;
- corrupt lease file is ignored and leaves the Agent locked.

- [ ] **Step 2: Run tests and verify RED**

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --filter "AgentRuntimeStateStoreTests|FileSessionLeaseStoreTests" --no-restore -p:UseSharedCompilation=false
```

- [ ] **Step 3: Implement state and file lease stores**

Add `AgentOptions.StateDirectory`, defaulting at runtime to `%ProgramData%\AFK4\Agent` when not configured. Tests must pass a temporary directory.

`FileSessionLeaseStore` must write JSON atomically through a temp file and replace, and must not throw on missing/corrupt files.

- [ ] **Step 4: Register services and commit**

Replace `InMemorySessionLeaseStore` registration with file-backed storage for production while keeping in-memory stores usable in tests.

## Task 3: Enforcement Coordinator And Command Handling

**Files:**

- Modify: `src\AFK4.Agent.Service\DefaultDeviceCommandHandler.cs`
- Create: `src\AFK4.Agent.Service\Enforcement\ISessionEnforcementCoordinator.cs`
- Create: `src\AFK4.Agent.Service\Enforcement\SessionEnforcementCoordinator.cs`
- Create: `src\AFK4.Agent.Service\Enforcement\IWorkstationLockController.cs`
- Create: `src\AFK4.Agent.Service\Enforcement\WorkstationLockController.cs`
- Modify: `src\AFK4.Agent.Service\Program.cs`
- Test: `tests\AFK4.Agent.Service.Tests\SessionEnforcementCoordinatorTests.cs`
- Test: `tests\AFK4.Agent.Service.Tests\SessionCommandHandlerLeaseTests.cs`

- [ ] **Step 1: Write failing coordinator tests**

Cover:

- valid `unlock` saves the lease, marks runtime active, and calls workstation unlock path;
- `refresh-session-lease` replaces the current lease without relaunching unrelated state;
- `lock` clears the matching lease, marks runtime locked, and calls workstation lock path;
- invalid lease returns rejected command result and does not unlock.

- [ ] **Step 2: Run tests and verify RED**

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --filter "SessionEnforcementCoordinatorTests|SessionCommandHandlerLeaseTests" --no-restore -p:UseSharedCompilation=false
```

- [ ] **Step 3: Implement coordinator**

`DefaultDeviceCommandHandler` should remain command parsing and result construction. It should delegate side effects to `ISessionEnforcementCoordinator`.

`WorkstationLockController` is the Windows boundary. It must expose a testable interface and keep risky OS behavior behind a small adapter. MVP enforcement may start with Shell-driven lock coverage and process policy; it must not claim kernel-level bypass resistance.

- [ ] **Step 4: Run tests and commit**

Commit as `feat: add agent session enforcement coordinator`.

## Task 4: Grace Monitor And Actual Heartbeat State

**Files:**

- Modify: `src\AFK4.Agent.Service\HeartbeatPayloadFactory.cs`
- Modify: `src\AFK4.Agent.Service\SessionReconciliationReporter.cs`
- Modify: `src\AFK4.Agent.Service\Worker.cs`
- Create: `src\AFK4.Agent.Service\Enforcement\GraceModeMonitor.cs`
- Test: `tests\AFK4.Agent.Service.Tests\HeartbeatPayloadFactoryTests.cs`
- Test: `tests\AFK4.Agent.Service.Tests\SessionReconciliationReporterTests.cs`
- Test: `tests\AFK4.Agent.Service.Tests\GraceModeMonitorTests.cs`
- Test: `tests\AFK4.Agent.Service.Tests\WorkerEnforcementIntegrationTests.cs`

- [ ] **Step 1: Write failing tests**

Cover:

- heartbeat uses current runtime lock state instead of hardcoded locked state;
- active lease snapshot is present only when a valid current lease exists;
- reconciliation reporter uses current runtime lock state;
- expired lease causes monitor to lock and clear the lease;
- worker continues heartbeat loop if grace monitor or Shell publish fails.

- [ ] **Step 2: Run tests and verify RED**

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --filter "HeartbeatPayloadFactoryTests|SessionReconciliationReporterTests|GraceModeMonitorTests|WorkerEnforcementIntegrationTests" --no-restore -p:UseSharedCompilation=false
```

- [ ] **Step 3: Implement grace monitoring**

The monitor must use `TimeProvider`, not `DateTimeOffset.UtcNow` directly in business checks. It should lock once when a lease expires and avoid repeatedly clearing already-cleared state.

- [ ] **Step 4: Run tests and commit**

Commit as `feat: enforce grace lease expiry in agent`.

## Task 5: Player Shell Process Supervision And Local IPC

**Files:**

- Modify: `src\AFK4.Agent.Service\AgentOptions.cs`
- Modify: `src\AFK4.Agent.Service\Worker.cs`
- Create: `src\AFK4.Agent.Service\Shell\IPlayerShellProcessSupervisor.cs`
- Create: `src\AFK4.Agent.Service\Shell\PlayerShellProcessSupervisor.cs`
- Create: `src\AFK4.Agent.Service\Shell\IPlayerShellStatePublisher.cs`
- Create: `src\AFK4.Agent.Service\Shell\NamedPipePlayerShellStateServer.cs`
- Create: `src\AFK4.Player.Shell\Realtime\IPlayerShellStateClient.cs`
- Create: `src\AFK4.Player.Shell\Realtime\NamedPipePlayerShellStateClient.cs`
- Modify: `src\AFK4.Player.Shell\App.xaml.cs`
- Test: `tests\AFK4.Agent.Service.Tests\PlayerShellProcessSupervisorTests.cs`

- [ ] **Step 1: Write failing supervision tests**

Cover:

- supervisor starts Shell when required and no matching process is running;
- supervisor does not start duplicate Shell processes;
- supervisor restarts Shell after unexpected exit while runtime is locked or active;
- supervisor does not require Shell while Agent is stopped.

- [ ] **Step 2: Run tests and verify RED**

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --filter PlayerShellProcessSupervisorTests --no-restore -p:UseSharedCompilation=false
```

- [ ] **Step 3: Implement IPC and supervision**

Use named pipes for local Agent/Shell messaging. The Agent publishes the latest `PlayerShellStateDto`; the Shell listens and updates its ViewModel. Tests should cover serialization and supervisor behavior with fakes, not real process spawning.

- [ ] **Step 4: Run tests and commit**

Commit as `feat: supervise player shell runtime`.

## Task 6: Player Shell MVVM Session UI

**Files:**

- Create: `tests\AFK4.Player.Shell.Tests\AFK4.Player.Shell.Tests.csproj`
- Modify: `AFK4.sln`
- Create: `src\AFK4.Player.Shell\Mvvm\RelayCommand.cs`
- Create: `src\AFK4.Player.Shell\Shell\RemainingTimeFormatter.cs`
- Create: `src\AFK4.Player.Shell\Shell\PlayerShellViewModel.cs`
- Create: `src\AFK4.Player.Shell\Launcher\LauncherAppViewModel.cs`
- Modify: `src\AFK4.Player.Shell\MainWindow.xaml`
- Modify: `src\AFK4.Player.Shell\MainWindow.xaml.cs`
- Test: `tests\AFK4.Player.Shell.Tests\RemainingTimeFormatterTests.cs`
- Test: `tests\AFK4.Player.Shell.Tests\PlayerShellViewModelTests.cs`

- [ ] **Step 1: Add Player Shell test project**

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' new xunit -n AFK4.Player.Shell.Tests -o tests/AFK4.Player.Shell.Tests -f net10.0-windows
& 'C:\Program Files\dotnet\dotnet.exe' sln AFK4.sln add tests/AFK4.Player.Shell.Tests/AFK4.Player.Shell.Tests.csproj
& 'C:\Program Files\dotnet\dotnet.exe' add tests/AFK4.Player.Shell.Tests/AFK4.Player.Shell.Tests.csproj reference src/AFK4.Player.Shell/AFK4.Player.Shell.csproj
```

- [ ] **Step 2: Write failing ViewModel tests**

Cover:

- locked state shows locked screen and hides launcher;
- active state shows remaining time and launcher apps;
- grace state shows offline warning and lease expiry countdown;
- warning state appears below configured remaining-time threshold;
- Shell ViewModel sends launcher command through an injected client and waits for Agent result.

- [ ] **Step 3: Implement ViewModels and dense fullscreen UI**

Player Shell UI must be fullscreen, controlled, and operational. It should avoid marketing-style copy and show:

- locked state;
- active session state;
- remaining time;
- offline/grace warning;
- launcher grid for allowed apps;
- last command/error status.

- [ ] **Step 4: Run tests and commit**

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Player.Shell.Tests/AFK4.Player.Shell.Tests.csproj --no-restore -p:UseSharedCompilation=false
```

Commit as `feat: add player shell session UI`.

## Task 7: Launcher And Process Policy Foundation

**Files:**

- Modify: `src\AFK4.Agent.Service\AgentOptions.cs`
- Create: `src\AFK4.Agent.Service\Enforcement\IProcessLauncher.cs`
- Create: `src\AFK4.Agent.Service\Enforcement\IProcessPolicyEnforcer.cs`
- Create: `src\AFK4.Agent.Service\Enforcement\ProcessPolicyEnforcer.cs`
- Create: `src\AFK4.Agent.Service\Shell\IPlayerShellCommandHandler.cs`
- Create: `src\AFK4.Agent.Service\Shell\PlayerShellCommandHandler.cs`
- Create: `src\AFK4.Player.Shell\Launcher\ILauncherCommandClient.cs`
- Create: `src\AFK4.Player.Shell\Launcher\LauncherCommandClient.cs`
- Test: `tests\AFK4.Agent.Service.Tests\PlayerShellCommandHandlerTests.cs`
- Test: `tests\AFK4.Player.Shell.Tests\LauncherCommandClientTests.cs`

- [ ] **Step 1: Write failing launcher/policy tests**

Cover:

- allowed app id launches configured executable with configured arguments;
- unknown app id is rejected;
- disallowed process name is terminated by policy enforcer in dry-run tests;
- launcher command results are returned to Shell with command id correlation.

- [ ] **Step 2: Run tests and verify RED**

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --filter PlayerShellCommandHandlerTests --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Player.Shell.Tests/AFK4.Player.Shell.Tests.csproj --filter LauncherCommandClientTests --no-restore -p:UseSharedCompilation=false
```

- [ ] **Step 3: Implement local allow-list launcher**

Use local `Agent:Launcher:Apps` configuration for Phase 8. Do not add backend launcher-management endpoints in this phase. Future centralized configuration belongs to later device/settings work or updates plans.

- [ ] **Step 4: Run tests and commit**

Commit as `feat: add local launcher policy foundation`.

## Task 8: Full Verification And Local Smoke

**Files:**

- Modify only files required to fix concrete failures found by verification.
- Update `README.md` and progress only after implementation evidence exists.

- [ ] **Step 1: Run targeted tests**

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter PlayerShellContractSerializationTests --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --filter "AgentRuntimeStateStoreTests|FileSessionLeaseStoreTests|SessionEnforcementCoordinatorTests|GraceModeMonitorTests|PlayerShellProcessSupervisorTests|PlayerShellCommandHandlerTests|WorkerEnforcementIntegrationTests" --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Player.Shell.Tests/AFK4.Player.Shell.Tests.csproj --no-restore -p:UseSharedCompilation=false
```

- [ ] **Step 2: Run full build and test suite**

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:UseSharedCompilation=false
```

- [ ] **Step 3: Run local smoke**

Use a PostgreSQL database migrated through Phase 7 and Platform API on `http://localhost:5074`.

Smoke path:

- sign in as branch manager or cashier/operator;
- enroll or reuse a device and credential;
- start Agent with device id, credential secret, signing public key, state directory, Shell executable path, and local launcher allow-list;
- start a guest session from Operator App or API;
- verify Agent accepts `unlock`, persists lease, publishes active Shell state, and heartbeat reports unlocked/active lease snapshot;
- verify Player Shell shows active session and remaining time;
- launch a configured harmless test app through Shell and verify Agent permits it;
- end the session and verify Agent locks, clears lease, publishes locked Shell state, and heartbeat reports locked/no active lease;
- restart Agent with the same state directory before and after session end to verify reboot-style behavior;
- allow lease expiry in a short test lease and verify grace monitor locks.

- [ ] **Step 4: Update docs and commit evidence**

Update:

- `README.md` current implementation state for Phase 8;
- `docs/progress/2026-05-12-vertical-slice-progress.md` with exact commands, pass counts, live smoke date, known limitations, and next roadmap recommendation.

Commit as `docs: record phase 8 verification`.

## Plan Self-Review

Spec coverage:

- Agent lock/unlock enforcement is covered by Tasks 2-4.
- Watchdog/Shell supervision is covered by Task 5.
- Grace mode continuation and lease expiry are covered by Tasks 2 and 4.
- Player Shell locked/session screens are covered by Task 6.
- Basic launcher and process policy foundation are covered by Task 7.
- Reboot recovery is covered by Task 2 and smoke verification in Task 8.

Architecture alignment:

- Backend remains the authority for sessions, billing, POS, and device commands.
- Agent executes backend-approved commands and validates signed leases.
- Player Shell is UI only and is not trusted for authorization.
- Offline behavior remains grace mode for already active sessions only.
- No local club server, web admin, microservices, kernel driver, or non-Windows Agent is introduced.

Out-of-scope checks:

- No updates/installers/rollout work is added in Phase 8.
- No reports or audit-search work is added in Phase 8.
- No fiscal/payment provider integrations are added.
- No advanced game library auto-update behavior is added.

Placeholder scan:

- The plan has concrete file paths, tests, runtime state names, IPC boundary, verification commands, smoke path, and out-of-scope checks.
- No MVP product decision is reopened.
