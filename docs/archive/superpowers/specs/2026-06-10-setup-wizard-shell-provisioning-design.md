# Setup Wizard provisions the Player Shell on gaming PCs (+ installer slimming)

Status: design, approved for planning
Date: 2026-06-10
Branch: wip/client-demo-setup

## Problem

The Setup Wizard is meant to be the single provisioning tool an operator runs on a
gaming PC: install the agent, bring up the Player Shell, configure the device — the
whole gaming-PC setup. Today it does not. The wizard's completion action
(`AgentServiceCompletionAction`) only configures the agent service for auto-start and
starts it. It never installs or launches the Player Shell.

The shell is a separate component the agent *supervises*: the agent reads
`Agent__PlayerShellExecutablePath` from machine environment (written today by the
standalone `afk4-player-shell` MSI) and, if the file exists, launches the shell in the
active user session (`PlayerShellProcessSupervisor`). No path / no file → the agent
silently does nothing.

Second, the installers are bloated: every component is published **self-contained
win-x64**, so each MSI carries its own full copy of the .NET runtime (~40 MB of ~55 MB).
The agent package is ~58 MB before we add anything.

### Observed symptoms (staging VM, agent 0.1.37)

Both functional symptoms share one root cause — no shell on the gaming PC:

1. **No player screen.** Nothing locks the workstation or shows the player login,
   because there is no shell executable for the agent to supervise.
2. **"Офлайн: ждём подтверждение платформы" after 1–2 minutes.** The agent itself stays
   connected. The *shell channel* is offline (no shell), so the workstation-lock command
   issued by "Старт 60 мин" never gets confirmed and the action hangs. The operator card
   combines `op.floor.state.offline` + `op.helper.feedback.pending`.

### Stale runbook

`docs/operations/client-packaging.md` (last updated 2026-05-26) describes the
pre-wizard model: separate MSIs, agent MSI does **not** carry the Player Shell payload,
shell installed manually. This is now stale and was the source of an incorrect
"install the second MSI by hand" instruction. The runbook must be updated as part of
this work; it is no longer the source of truth — code is.

## What already works (do not rebuild)

- Wizard flow: phone login → branch → **role selection** (`RoleScreen`,
  `gaming_pc` + floor-map seat, or `manager_workstation`) → device → finish.
- `EnvironmentBootstrapWriter` writes `Agent__DeviceRole` (default `gaming_pc`) and the
  rest of the bootstrap env (server URL, tokens, seat).
- `afk4-player-shell` MSI installs the shell to
  `C:\Program Files\AFK4\Player Shell\AFK4.Player.Shell.exe` and writes three machine
  env values the agent reads: `Agent__PlayerShellExecutablePath`, `Agent__ShellVersion`,
  `Agent__PlayerShellAutoStartEnabled`.
- The agent already supervises the shell once those env values exist and the service is
  restarted (`PlayerShellProcessSupervisor`).
- The update pipeline keeps an already-installed shell current. It does **not** do first
  install — it only reports the shell component when
  `DeviceRole == GamingPc && ShellVersion` is set (`AgentComponentVersionProvider`).
  Therefore the shell must be installed at provisioning time; the pipeline takes over
  afterward.

## Decisions

1. **Wizard provisions the shell on `gaming_pc`.** During finalization, for the
   `gaming_pc` role only, the wizard installs the bundled Player Shell MSI, then starts
   the agent so it supervises the shell. We reuse the existing, tested MSI verbatim (it
   writes exactly the env the agent needs). Works offline of any staging rollout —
   correct for the client demo — and preserves per-component separation so the update
   pipeline keeps working.

2. **Slim the installers: framework-dependent + a one-time shared runtime.** All
   components (agent, shell, wizard, operator) switch from self-contained to
   framework-dependent. A master installer (`setup.exe`, a WiX Burn bundle) ensures the
   **.NET Desktop Runtime** is present once, then installs the component MSIs. Each MSI
   drops from ~55 MB to ~5–15 MB. Side effect: the bundled shell MSI shrinks to ~10 MB,
   so bundling it in the wizard is cheap.

3. **Shell delivery = option A (wizard, gaming PC only).** The master installer for a
   gaming PC installs runtime + agent. The shell is bundled in the wizard and installed
   by the wizard via `msiexec` **only** when the operator picks the `gaming_pc` role. The
   shell never lands on manager workstations. The wizard stays the single point of
   gaming-PC setup.

4. **Download the runtime (production path), do not embed it.** The master installer
   carries a *downloadable* .NET Desktop Runtime prerequisite (fetched from Microsoft at
   install time, skipped if already present), keeping `setup.exe` small. This is the real
   production experience, so the demo validates exactly that — the demo VM must have
   network. (An embedded/offline variant can be added later for air-gapped sites, but is
   out of scope here.)

Rejected alternatives: platform-rollout first install (pipeline does not do first
install; needs backend + staging rollout); wizard writes shell files/env without an MSI
(duplicates MSI logic, breaks update-pipeline component accounting); `PublishTrimmed`
(unsafe for WPF wizard/shell); shell installed for every machine by the bootstrapper
(dead weight on manager workstations).

## Design

### A. Functional — wizard provisions the shell

Finalization flow for `gaming_pc`:

1. Write bootstrap env (unchanged: `DeviceRole`, server URL, tokens, seat).
2. **[new]** Install the bundled shell MSI:
   `msiexec /i "<payload>\AFK4.Player.Shell.msi" /qn`.
   The MSI writes `Agent__PlayerShellExecutablePath` / `ShellVersion` /
   `PlayerShellAutoStartEnabled` and lays down the exe.
3. Start / restart the agent service (existing completion action). The agent reads all
   env at startup and launches the shell in the active user session.

For `manager_workstation`, step 2 is skipped. The wizard runs as its own elevated
process after the agent install has finished, so `msiexec` will not collide with an
in-progress install mutex.

New units (small, isolated, testable):

- **`PlayerShellProvisioningAction`** — next to `AgentServiceCompletionAction`. Input:
  path to the bundled MSI + an injectable process runner. Runs `msiexec /i <msi> /qn`,
  maps exit codes: `0` and `3010` (reboot pending) → success; `1638` (already installed)
  → already-provisioned; anything else → failure with the raw code surfaced. Returns a
  result object; no UI, no global state. Unit-tested with a fake runner, mirroring
  `AgentServiceCompletionAction`'s tests.
- **`SetupWizardPayloadResolver`** — locates the bundled MSI relative to the wizard exe,
  mirroring `SetupWizardWebAssetResolver`. Dev/repo: resolve from `artifacts`. Prod:
  `…\Setup Wizard\payload\AFK4.Player.Shell.msi`.
- **Finalization caller** (`SetupWizardViewModel` / completion path) invokes the
  provisioning action only for `gaming_pc`, then the existing service-start action.

UX and failure handling: the finish screen shows an explicit provisioning line
"Установка оболочки игрока…" with its result, rendered inside the existing finish screen
(no new Stepper step). Success / already-present → continue to "ready". Failure → show
the code/reason, do **not** mark the PC ready, offer "Повторить". No silent "ready" over
a missing shell.

### B. Packaging — framework-dependent + master installer

- Publish all four components framework-dependent (`--self-contained false`). The
  prerequisite is the **.NET 10 Desktop Runtime** (`Microsoft.WindowsDesktop.App`, which
  includes the base runtime) — covers the WPF wizard/shell, the agent service, and the
  operator app.
- Add a **WiX Burn bundle** (`setup.exe`) per install target:
  - **Gaming-PC master installer**: chain (1) a downloadable .NET Desktop Runtime
    prerequisite (fetched from Microsoft, skipped if a compatible runtime is already
    present), then (2) the agent MSI. The shell is **not** chained here — the wizard
    installs it on `gaming_pc`.
  - **Operator master installer**: chain the downloadable runtime prerequisite + the
    operator MSI, for the same one-time-runtime benefit on operator workstations.
- The bundled shell MSI (in the wizard payload) is also framework-dependent (~10 MB).
- Keep the standalone per-component MSIs as build artifacts (recovery + update-pipeline
  source). The master installers wrap them; they are not replaced.

### Packaging / build script

`scripts/build-client-packages.ps1`:
- Build the Player Shell MSI **before** the agent MSI; copy the built
  `AFK4.Player.Shell-<version>.msi` into the wizard payload folder; the agent MSI harvests
  that folder (same mechanism as `WebAssets`) so it ships at
  `…\Setup Wizard\payload\AFK4.Player.Shell.msi`.
- Switch the `dotnet publish` calls to `--self-contained false`.
- Build the Burn bundles after the MSIs, referencing the .NET Desktop Runtime as a
  downloadable prerequisite package (not embedded).
- The bundled shell version equals the build version; the update pipeline upgrades it
  afterward.

### Runbook update (deliverable)

Update `docs/operations/client-packaging.md` to the new model:
- One master installer (`setup.exe`) per target; it ensures the .NET runtime once.
- The wizard installs the Player Shell on `gaming_pc`; it is no longer a manual step.
- Components are framework-dependent, not self-contained.
- Sweep the rest of the runbook for statements that contradict the new model (MSI split,
  manual shell install, owner-code/enrollment) and fix them in the same pass.

## Sequencing

The demo runs on the **production-like packaging** (framework-dependent + master
installer with a downloaded runtime) — we are not keeping a self-contained interim build.
Implementation order is by dependency: build the master installer + framework-dependent
publish (Part B), then the wizard shell-provisioning (Part A) ships inside it, and the VM
demo validates both together against the real prod install path.

## Testing

- Unit: `PlayerShellProvisioningAction` exit-code mapping (0/3010/1638/other), skip for
  manager role, missing-MSI handling.
- Unit: `SetupWizardPayloadResolver` dev vs prod path resolution.
- VM verification, functional (staging): provision a `gaming_pc` → shell installs, player
  screen comes up in the active session; "Старт 60 мин" completes the workstation lock;
  the "офлайн / ждём подтверждение" state clears (confirming same root cause). If it
  persists → separate agent-connectivity bug → pull agent logs.
- VM verification, packaging (production-like): on a **clean, networked** VM with no .NET
  runtime, the gaming-PC master installer **downloads** the runtime once then installs the
  agent; the wizard + shell run framework-dependent; size of the master installer recorded
  vs the old self-contained MSI. This is the real prod experience the demo signs off on.

## Out of scope

- Changing the update pipeline to do first installs.
- Bundling the shell for `manager_workstation`.
- Any redesign of the wizard's existing screens beyond the finish-screen status line.
- Trimming (`PublishTrimmed`) of any component.
