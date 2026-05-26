# Agent Installer Enrollment Runbook

Status: current Setup Wizard MSI runbook after Slice 3.4 VM evidence
Last updated: 2026-05-26

## Purpose

This runbook describes the current MVP bootstrap flow for a Windows endpoint.
The default onboarding artifact is now the WiX-built `AFK4 Agent` MSI. It
installs the Agent Service and WPF Setup Wizard; Player Shell and Operator App
are intentionally not bundled in this MSI and are pulled by the Agent from the
update channel according to the enrolled device role.

## Preconditions

- The PC runs Windows 10/11.
- The branch exists in the AFK4 backend.
- The branch owner can generate an 8-digit owner code from `/club/install`.
- Agent Service and Setup Wizard binaries come from an approved AFK4
  distribution source.
- Production secrets and signing keys are not stored in the repository.

## Enrollment Flow

1. Install `afk4-agent-<version>-<channel>.msi`.
2. The MSI installs:
   - `AFK4.Agent.Service`;
   - `AFK4.SetupWizard.exe`;
   - update helper scripts;
   - a Start Menu shortcut for the wizard;
   - a per-machine first-run pending marker;
   - a HKLM `RunOnce` entry so the wizard opens on the next admin login if
     immediate launch is not possible.
3. For an interactive install, the MSI attempts to launch
   `AFK4.SetupWizard.exe` after installation. For silent/headless deployment,
   the operator can launch the wizard from the Start Menu or wait for
   `RunOnce`.
4. Enter the owner code generated in `/club/install`.
5. Choose the target branch and a free floor-map seat. If the seat is missing,
   create it from inside the wizard.
6. Choose the role: `gaming_pc` or `manager_workstation`.
7. The wizard calls `POST /api/install/enroll`.
8. The backend issues:
   - device id;
   - organization id;
   - branch id;
   - device credential secret.
9. Store the device credential and local device key material in per-machine
   storage.
10. Write Agent bootstrap configuration:
   - `Agent:PlatformBaseUrl`;
   - `Agent:OrganizationId`;
   - `Agent:BranchId`;
   - `Agent:DeviceId`;
   - `Agent:DeviceRole`;
   - `Agent:UpdateChannel`;
   - update helper install/rollback/restart commands;
   - Player Shell executable path for `gaming_pc` supervision;
   - lease and update verification public keys.
11. After writing bootstrap configuration, the wizard switches
    `AFK4.Agent.Service` to automatic startup and starts it. Then verify
    heartbeat succeeds and the device appears in the customer dashboard/device
    workflow. Successful completion also clears the first-run marker and HKLM
    `RunOnce` entry so later Agent MSI upgrades do not reopen the wizard.
12. On its update loop the Agent requests role-compatible update packages:
    `gaming_pc` devices install `player-shell`, and `manager_workstation`
    devices install `operator-app`. Operator App installation checks for the
    Microsoft Edge WebView2 Runtime first and runs the Evergreen bootstrapper
    before the MSI when the runtime is missing.
13. After installing Agent Service, Player Shell, or Operator App components,
    the update helper schedules an Agent Service restart so new machine
    environment values, component versions, and Shell executable paths are
    loaded by the running service.

The legacy PC enrollment code path and coordinated `afk4-gaming-pc` MSI remain
available only as staging fallback paths until Slice 3.5 retires them from the
default onboarding/publishing flow.

## Safety Rules

- Never ship a hardcoded device credential in installer files.
- Owner codes must remain scoped to `install/*` endpoints and must not sign in
  to the SPA.
- A device credential belongs to exactly one device id.
- If enrollment is repeated for replacement hardware, revoke old credentials
  through the backend.
- The Player Shell is not trusted for enrollment, billing, authorization, or
  update decisions.
- The Agent must remain installed and manageable after a failed Player Shell
  update.

## Manual Recovery

If enrollment or startup fails:

1. Stop the Agent Service.
2. Inspect local Agent logs and backend device command/status records.
3. Remove only the local AFK4 configuration and credential for this device if a
   clean re-enrollment is required.
4. Rotate the owner code from `/club/install` if the old code may have leaked.
5. Re-run `AFK4.SetupWizard.exe` from the Start Menu and re-enroll the device.
6. Revoke stale credentials for the previous enrollment.

Do not unlock a gaming PC manually as a substitute for a valid backend-approved
session command.
