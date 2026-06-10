# Setup Wizard provisions the Player Shell on gaming PCs

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

### Observed symptoms (staging VM, agent 0.1.37)

Both symptoms share one root cause — no shell on the gaming PC:

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
- The update pipeline keeps an already-installed shell current (reports component
  versions, applies rollouts). It does **not** do first install — it only reports the
  shell component when `DeviceRole == GamingPc && ShellVersion` is set
  (`AgentComponentVersionProvider`). Therefore the shell must be installed at
  provisioning time; the pipeline takes over afterward.

## Decision

For the `gaming_pc` role, the wizard installs the **bundled Player Shell MSI** during
finalization, then starts the agent so it supervises the shell. We reuse the existing,
tested MSI verbatim (it writes exactly the env the agent needs). This works offline of
any staging rollout — correct for the client demo — and preserves per-component
separation so the update pipeline keeps working.

Rejected alternatives:
- **Platform rollout for first install** — the pipeline does not do first install;
  would need backend work + a configured staging rollout. Too heavy and fragile for a
  demo.
- **Wizard writes shell files/env itself (no MSI)** — duplicates MSI logic (versioning,
  upgrade, uninstall) and breaks component accounting in the update pipeline.

## Design

### Finalization flow (gaming_pc role)

1. Write bootstrap env (unchanged: `DeviceRole`, server URL, tokens, seat).
2. **[new]** Install the bundled shell MSI:
   `msiexec /i "<payload>\AFK4.Player.Shell.msi" /qn`.
   The MSI writes `Agent__PlayerShellExecutablePath` / `ShellVersion` /
   `PlayerShellAutoStartEnabled` and lays down the exe.
3. Start / restart the agent service (existing completion action). The agent reads all
   env at startup and launches the shell in the active user session.

For `manager_workstation`, step 2 is skipped.

The wizard runs as its own elevated process and the agent MSI has already finished by
the time it runs, so `msiexec` will not collide with an in-progress install mutex.

### Components (small, isolated, testable)

- **`PlayerShellProvisioningAction`** — new unit next to `AgentServiceCompletionAction`.
  - Input: path to the bundled MSI, an injectable process runner.
  - Runs `msiexec /i <msi> /qn`, maps exit codes:
    `0` and `3010` (success, reboot pending) → success;
    `1638` (same/another version already installed) → treated as already provisioned;
    anything else → failure with the raw exit code surfaced.
  - Returns a result object (success / already-present / failure + code). No UI, no
    global state. Unit-tested with a fake runner, mirroring how
    `AgentServiceCompletionAction` is tested.
- **`SetupWizardPayloadResolver`** — locates the bundled MSI relative to the wizard exe,
  mirroring `SetupWizardWebAssetResolver`. Dev/repo: resolve from `artifacts`. Prod:
  `…\Setup Wizard\payload\AFK4.Player.Shell.msi`.
- **Finalization caller** (`SetupWizardViewModel` / completion path) invokes the
  provisioning action only for `gaming_pc`, then the existing service-start action.

### UX and failure handling

Finish screen shows an explicit provisioning line: "Установка оболочки игрока…" with its
result, rendered inside the existing finish screen (no new Stepper step).

- Success / already-present → continue to "ready".
- Failure (non-accepted msiexec exit code) → show the code/reason, do **not** mark the PC
  as ready, offer "Повторить". No silent "ready" over a missing shell.

### Packaging / build

- `scripts/build-client-packages.ps1`: build the Player Shell MSI **before** the agent
  MSI; copy the built `AFK4.Player.Shell-<version>.msi` into the wizard payload folder;
  the agent MSI harvests that folder (same mechanism as `WebAssets`) so it ships at
  `…\Setup Wizard\payload\AFK4.Player.Shell.msi`.
- Consequence: the agent package grows by ~54 MB (≈110 MB total) — the accepted cost of
  bundling.
- The bundled shell version equals the build version; the update pipeline upgrades it
  afterward.
- Keep the standalone `afk4-player-shell` MSI artifact too (recovery / pipeline source).

### Runbook update (deliverable)

Update `docs/operations/client-packaging.md` to the wizard-as-single-tool model:
- The agent/wizard package now carries and installs the Player Shell on `gaming_pc`.
- Remove / correct the "agent MSI does not carry Player Shell" and "install shell
  manually" instructions.
- Sweep the rest of the runbook for statements that contradict the new model
  (enrollment, owner-code, MSI split) and fix them in the same pass, not piecemeal.

## Testing

- Unit: `PlayerShellProvisioningAction` exit-code mapping (0/3010/1638/other), skip for
  manager role, missing-MSI handling.
- Unit: `SetupWizardPayloadResolver` dev vs prod path resolution.
- VM verification (staging): provision a `gaming_pc` →
  - shell installs, player screen comes up in the active session;
  - "Старт 60 мин" completes the workstation lock;
  - the "офлайн / ждём подтверждение" state clears — confirming it was the same root
    cause. If it persists, that is a separate agent-connectivity bug → pull agent logs.

## Out of scope

- Changing the update pipeline to do first installs.
- Bundling the shell for `manager_workstation`.
- Any redesign of the wizard's existing screens beyond the finish-screen status line.
