# Staging Gaming PC Bootstrapper Design

## Purpose

The first AFK4 Windows device smoke must not require a developer-style
PowerShell runbook on the test VM. A clean Windows 11 x64 VM should be prepared
by running one executable as administrator. The executable installs the Gaming
PC client, enrolls the current machine against staging through staff login, and
starts the Agent Service with the correct configuration.

This is a staging-only pilot tool. It is not the final commercial installer UX
and does not reopen the MVP decisions that the Gaming PC runtime is Windows-only
and split into Agent Service plus Player Shell.

## Scope

Included:

- Create a Windows desktop bootstrapper executable for the staging environment.
- Hardcode the staging Platform API URL:
  `https://afk4.staging.mubi.dev`.
- Hardcode the staging organization and branch used by the current smoke
  runbook.
- Ask the operator only for staff username and password.
- Default the machine name to `Environment.MachineName`.
- Sign in through the existing staff auth endpoint.
- Create a short-lived device enrollment code through the existing backend
  endpoint.
- Enroll the current machine through the existing device enrollment endpoint.
- Install the bundled Gaming PC MSI.
- Configure the Agent Service through machine-scoped settings.
- Start `AFK4.Agent.Service`.
- Show a clear success or failure result without exposing secrets.

Excluded for this first slice:

- Production installer branding and signing.
- Generic environment, organization, or branch selection.
- Staff, layout, seat, tariff, or shift creation.
- Stable update rollout preparation.
- Secret storage beyond the same machine-scoped configuration path already used
  by the Agent smoke runbook.

## UX

The bootstrapper is launched by double-clicking a single `.exe` on the Windows
11 VM. If it is not elevated, it asks Windows for elevation and exits the
non-elevated instance.

The main window is intentionally small and operational:

- Shows the target environment as `AFK4 Staging`.
- Shows the current machine name.
- Accepts staff username and password.
- Has one primary action: `Install And Enroll`.
- Shows step status for health check, sign-in, enrollment, MSI install, Agent
  configuration, service start, and heartbeat check.

The final success screen displays the `deviceId`, service state, and whether a
recent heartbeat was observed. It never displays the staff token, refresh token,
device credential secret, enrollment code, or lease verification key.

## Architecture

Add a new WPF/MVVM client project:

- `src/AFK4.GamingPc.Setup`

The setup app owns only bootstrapper behavior. It does not become the Agent and
does not duplicate Agent runtime logic. It depends on `AFK4.Shared.Contracts`
for request/response DTOs and uses `HttpClient` for backend calls.

Core units:

- `StagingSetupDefaults` contains hardcoded staging URL, organization, branch,
  Agent/Shell versions, update channel, service name, and install paths.
- `SetupApiClient` wraps health, staff sign-in, enrollment code creation,
  device enrollment, and device detail polling.
- `GamingPcMsiInstaller` extracts the bundled MSI to a temporary directory and
  runs `msiexec.exe /i ... /qn /norestart`.
- `AgentMachineConfigurationWriter` writes machine-scoped Agent configuration.
- `WindowsServiceController` starts and queries `AFK4.Agent.Service`.
- `GamingPcSetupOrchestrator` coordinates the full flow and reports progress.
- `SetupShellViewModel` exposes username/password input, step state, and
  command enablement.

The bundled MSI is produced by the existing client package build path. The
bootstrapper build step embeds `afk4-gaming-pc-<version>-internal.msi` as a
resource so the VM receives one executable.

## Configuration

The first staging build embeds the staging session lease public key as a public
configuration asset. This key is not secret; the private signing key remains
only in the backend runtime environment.

After enrollment, the bootstrapper configures these machine-scoped Agent
settings:

- `Agent__PlatformBaseUrl`
- `Agent__OrganizationId`
- `Agent__BranchId`
- `Agent__DeviceId`
- `Agent__MachineName`
- `Agent__AgentVersion`
- `Agent__ShellVersion`
- `Agent__DeviceCredentialSecret`
- `Agent__LeaseSigningPublicKeyPem`
- `Agent__PlayerShellExecutablePath`
- `Agent__UpdateChannel`
- update installer, rollback, and restart adapter paths

The device credential secret is written only to the local VM machine
environment, matching the current smoke runbook behavior. It is not logged or
shown in the UI.

## Error Handling

Every setup step returns a structured result with a user-visible message and a
technical detail string for local troubleshooting. The UI reports which step
failed and stops before dependent steps run.

Expected failures:

- staging health check fails;
- staff credentials are rejected;
- staff user lacks device enrollment permission;
- MSI install returns a non-zero exit code;
- service cannot be started;
- heartbeat is not observed before timeout.

The first implementation treats heartbeat timeout as a partial success: the
client may be installed and service may be running, but the smoke gate remains
open until backend device detail confirms a recent heartbeat.

## Testing

Use TDD for the bootstrapper orchestration and build packaging invariants.

Automated tests cover:

- orchestrator calls setup steps in the correct order;
- sign-in failure stops before enrollment and install;
- MSI install failure stops before Agent configuration;
- service start failure reports the service step as failed;
- heartbeat timeout is reported as partial success, not full pass;
- staging defaults contain the expected URL, organization, branch, service name,
  update channel, and install paths;
- package build script has a bootstrapper artifact path and embeds the Gaming PC
  MSI into the setup executable.

Manual verification remains required on the two Windows 11 VMs:

- double-click setup exe;
- enter staging staff credentials;
- confirm UAC elevation;
- confirm service reaches `RUNNING`;
- confirm backend device detail shows online heartbeat;
- record Player Shell visibility and session behavior separately through the
  real-device smoke evidence checklist.
