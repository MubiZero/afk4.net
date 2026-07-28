# Real Device Windows PC Smoke Runbook

Status: manual staging smoke path  
Last updated: 2026-05-26

## Purpose

This runbook verifies AFK4 on one real Windows gaming PC against the staging
Platform API at:

```text
https://afk4.staging.mubi.dev
```

It is intentionally manual. The goal is to collect honest evidence for the
current cloud backend, Agent Service, Player Shell, and session path before any
pilot automation is added.

## Scope

Included:

- staging Platform API health and PostgreSQL-backed operations;
- staging organization, branch, staff, player, tariff, shift, seat, and device
  preparation;
- Windows 10/11 clean VM or gaming PC install through the single AFK4 Agent
  MSI and Setup Wizard;
- owner-code enrollment, device credential issuance, and credential
  authentication;
- heartbeat and SignalR connectivity through `/hubs/devices`;
- session start, lease refresh, session end, lease expiry behavior, and
  lock/unlock command handling;
- Player Shell visible state when an interactive shell process is available;
- installed apps report, branch diagnostics, logs, and update check/status
  smoke.

Not included:

- production rollout automation;
- stable-channel update rollout;
- production signing, CDN, or object-store decisions;
- production customer self-signup or payment provider flows;
- legacy PC enrollment code/bootstrap as the primary onboarding path;
- non-Windows client runtime validation.

## Current Caveats

- The corrected single `AFK4 Agent` MSI path has current Windows 11 VM evidence
  through internal version `0.1.29`: owner-code enrollment, Agent update,
  service automatic start after reboot, and no Setup Wizard rerun after upgrade.
  It installs the Agent Service, Setup Wizard, update helpers, Start Menu
  shortcut, first-run marker, and HKLM `RunOnce`; Player Shell and Organization Admin
  are installed later by the Agent through role-aware update rollouts.
- The older staging `gaming-pc-bootstrap` MinIO script is retired from the
  default smoke path. Use it only as a legacy recovery fallback for old staging
  devices, and mark any such run as partial/non-current evidence.
- The current Setup Wizard authenticates staff directly by phone or
  email/login and password; the removed Platform Control `/club` dashboard is not
  part of enrollment.
- A `manager_workstation` enrollment must prove the role-aware update path:
  WebView2 Runtime check/install, Organization Admin MSI install, Agent restart, and
  an Organization Admin sign-in screen pointing at staging.
- `WorkstationLockController` currently records lock/unlock requests through
  the enforcement adapter. If the physical Windows desktop does not actually
  lock or unlock, record that as a real enforcement gap rather than inventing a
  pass.
- Player Shell auto-start is expected to run from the Agent Service by launching
  into the active interactive Windows session. A Shell process in service
  session `0` is still a regression. If no interactive user session exists, the
  Agent must skip Shell launch and continue heartbeat/state publishing.
- Already enrolled PCs should receive new Agent/Shell builds through signed
  internal MSI update rollouts. Re-running the clean-machine bootstrap path on
  an installed PC is only a bootstrap diagnostic and does not prove the update
  path.
- Do not commit secrets, filled environment files, database URLs, staff
  passwords, device credential secrets, PEM files, MSI artifacts, or smoke
  transcripts containing secrets.

## Endpoint Checklist

The smoke exercises these API boundaries:

| Area | Endpoint |
| --- | --- |
| Health | `GET /api/health` |
| Staff auth | `POST /api/organizations/{organizationId}/auth/staff/sign-in` |
| Owner code | `GET /api/organizations/{organizationId}/staff/me/owner-code` |
| Owner code | `POST /api/organizations/{organizationId}/staff/me/owner-code/generate` |
| Owner code | `POST /api/organizations/{organizationId}/staff/me/owner-code/rotate` |
| Install discover | `POST /api/organizations/{organizationId}/install/discover` |
| Install seat create | `POST /api/organizations/{organizationId}/install/seats` |
| Install enroll | `POST /api/organizations/{organizationId}/install/enroll` |
| Heartbeat | `POST /api/devices/{deviceId}/heartbeat` |
| SignalR | `/hubs/devices` |
| Installed apps | `POST /api/devices/{deviceId}/installed-apps/report` |
| Device detail | `GET /api/devices/{deviceId}` |
| Sessions | `POST /api/organizations/{organizationId}/branches/{branchId}/sessions/start` |
| Session end | `POST /api/organizations/{organizationId}/sessions/{sessionId}/end` |
| Reconciliation | `POST /api/devices/{deviceId}/session-reconciliation` |
| Diagnostics | `GET /api/organizations/{organizationId}/branches/{branchId}/diagnostics` |
| Updates | `POST /api/devices/{deviceId}/updates/check` |
| Updates | `POST /api/devices/{deviceId}/updates/status` |

## Prerequisites

Release workstation:

- PowerShell.
- Git for Windows.
- .NET SDK `10.0.203`.
- GitHub CLI authenticated with `repo` scope when downloading a short-retention
  `Package Smoke` MSI artifact instead of building locally.
- `psql` access to staging through a trusted shell, private network path, or
  temporary approved tunnel only when bootstrapping a completely fresh staging
  organization/branch/seat dataset. Existing staging smoke data should use the
  dashboard/API path below and should not require direct database edits.
- Staging database URL available only in the current shell as
  `AFK4_STAGING_DATABASE_URL` when the one-time seed step is required.
- Staging session lease public key PEM available outside the repository.

Windows gaming PC:

- Windows 10/11 x64.
- Local Administrator access.
- Outbound HTTPS access to `https://afk4.staging.mubi.dev`.
- Outbound HTTPS access to `https://updates.afk4.staging.mubi.dev`.
- The current internal Agent MSI publishes the Agent Service and Setup Wizard
  as self-contained `win-x64` outputs, so a separate .NET Desktop Runtime
  install is not required for the MSI smoke path.
- A clean test Windows user session where Player Shell can be observed.

## Slice 3.4 Preferred Flow

Use this path for the clean Windows 11 VM gate.

1. Confirm the current `main` package evidence:

   - `Package Smoke` run `26442315418` passed after commit `8019013`.
   - It produced internal package version `0.1.29`.
   - The public Agent MSI URL returned HTTP 200 with non-zero
     `Content-Length` during the smoke follow-up:

     ```text
     https://updates.afk4.staging.mubi.dev/afk4-updates-staging/agent-service/internal/0.1.29/afk4-agent-0.1.29-internal.msi
     ```

2. Prepare an authorized staging branch staff account. Keep its credential only
   in the live smoke environment; do not paste it into repository files or
   chat. The Setup Wizard performs the staff sign-in itself.

3. On the clean VM, install `afk4-agent-<version>-internal.msi`
   interactively. The Setup Wizard should open after install. If it does not,
   launch it from Start Menu -> AFK4 -> AFK4 Setup Wizard, or run:

   ```powershell
   & 'C:\Program Files\AFK4\Setup Wizard\AFK4.SetupWizard.exe'
   ```

4. In the wizard, sign in, choose the branch, choose or create a seat, select
   the role, and finish enrollment:

   - `gaming_pc` for a player PC. Expected follow-on: Agent installs Player
     Shell from the internal update channel and supervises it in the active
     desktop session.
   - `manager_workstation` for an operator PC. Expected follow-on: Agent checks
     or installs WebView2 Runtime, installs Organization Admin, restarts, and leaves
     Organization Admin ready for staff sign-in against staging.

5. Confirm in Organization Admin `Управление → Залы и ПК` that the device appears
   with the selected display name, role, seat, enrollment state, and recent
   heartbeat.

## Prepare Staging Data

Preferred setup is through the Mubi admin SPA plus Setup Wizard and Operator:

1. Mubi creates the organization and owner invite under
   `https://platform.afk4.staging.mubi.dev/admin`.
2. The owner accepts the invitation through the current onboarding path and
   creates/edits the branch floor map in Organization Admin.
3. The smoke uses the authorized staff login only inside Setup Wizard.

Use the PowerShell/API fallback below only when the dashboard path is blocked
or when reusing the fixed staging smoke organization from earlier runs. Do not paste
real secrets into chat or repository files.

```powershell
Set-Location D:\afk4.net

$baseUrl = 'https://afk4.staging.mubi.dev'
$organizationId = '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08'
$branchId = 'acfc0212-967f-4d84-94be-9003387b09c2'
$zoneId = '2e37f7b3-41bb-4a19-9d50-94eb848f4e01'
$seatId = '9f3adbd3-957e-4dc8-8d34-a6bfa56b9275'
$staffUserId = '3db1367b-88c6-4b1c-99c3-bcbb5f4d5134'
$staffRoleAssignmentId = '58e8a836-82cd-45d1-a0cc-c13621e76c4e'
$runId = (Get-Date).ToUniversalTime().ToString('yyyyMMddHHmmss')

Invoke-RestMethod "$baseUrl/api/health"
```

Expected: health returns `status = ok` over trusted TLS.

Create a one-time staff password hash without writing a helper into the repo:

```powershell
$env:AFK4_SMOKE_STAFF_PASSWORD = '<one-time-staging-smoke-password>'

$hashTool = Join-Path $env:TEMP 'afk4-staff-password-hash'
Remove-Item -LiteralPath $hashTool -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $hashTool | Out-Null

@'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
</Project>
'@ | Set-Content -LiteralPath (Join-Path $hashTool 'HashTool.csproj') -Encoding UTF8

@'
using Microsoft.AspNetCore.Identity;

var password = Environment.GetEnvironmentVariable("AFK4_SMOKE_STAFF_PASSWORD");
if (string.IsNullOrWhiteSpace(password))
{
    throw new InvalidOperationException("AFK4_SMOKE_STAFF_PASSWORD is required.");
}

Console.WriteLine(new PasswordHasher<object>().HashPassword(new object(), password));
'@ | Set-Content -LiteralPath (Join-Path $hashTool 'Program.cs') -Encoding UTF8

& 'C:\Program Files\dotnet\dotnet.exe' restore $hashTool -p:NuGetAudit=false -v minimal
$env:AFK4_SMOKE_STAFF_PASSWORD_HASH = (& 'C:\Program Files\dotnet\dotnet.exe' run --project $hashTool --no-restore).Trim()
```

Seed the fixed staging smoke organization, branch, staff user, and seat only
when the dashboard/API path is unavailable or when rebuilding the historical
smoke organization. This direct SQL path is a fallback, not the preferred onboarding
flow.

```powershell
$seedSql = @"
INSERT INTO organizations ("OrganizationId", "Name", "CreatedAtUtc")
VALUES ('$organizationId', 'AFK4 Staging Smoke Organization', now())
ON CONFLICT ("OrganizationId")
DO UPDATE SET "Name" = EXCLUDED."Name";

INSERT INTO branches ("BranchId", "OrganizationId", "Name", "CreatedAtUtc")
VALUES ('$branchId', '$organizationId', 'AFK4 Staging Smoke Branch', now())
ON CONFLICT ("BranchId")
DO UPDATE SET "OrganizationId" = EXCLUDED."OrganizationId",
              "Name" = EXCLUDED."Name";

INSERT INTO staff_users (
    "StaffUserId",
    "OrganizationId",
    "UserName",
    "NormalizedUserName",
    "DisplayName",
    "PasswordHash",
    "IsActive",
    "CreatedAtUtc")
VALUES (
    '$staffUserId',
    '$organizationId',
    'real-device-smoke@afk4.test',
    'REAL-DEVICE-SMOKE@AFK4.TEST',
    'Real Device Smoke Operator',
    '$env:AFK4_SMOKE_STAFF_PASSWORD_HASH',
    true,
    now())
ON CONFLICT ("OrganizationId", "NormalizedUserName")
DO UPDATE SET "DisplayName" = EXCLUDED."DisplayName",
              "PasswordHash" = EXCLUDED."PasswordHash",
              "IsActive" = true;

INSERT INTO staff_role_assignments (
    "StaffRoleAssignmentId",
    "StaffUserId",
    "OrganizationId",
    "BranchId",
    "RoleName")
VALUES (
    '$staffRoleAssignmentId',
    '$staffUserId',
    '$organizationId',
    '$branchId',
    'owner')
ON CONFLICT ("StaffUserId", "OrganizationId", "BranchId", "RoleName")
DO NOTHING;

INSERT INTO zones ("ZoneId", "OrganizationId", "BranchId", "Name", "SortOrder", "CreatedAtUtc")
VALUES ('$zoneId', '$organizationId', '$branchId', 'Real Device Smoke Zone', 10, now())
ON CONFLICT ("ZoneId")
DO UPDATE SET "Name" = EXCLUDED."Name",
              "SortOrder" = EXCLUDED."SortOrder";

INSERT INTO seats ("SeatId", "OrganizationId", "BranchId", "ZoneId", "Name", "SortOrder", "CreatedAtUtc")
VALUES ('$seatId', '$organizationId', '$branchId', '$zoneId', 'REAL-PC-SMOKE-001', 10, now())
ON CONFLICT ("SeatId")
DO UPDATE SET "ZoneId" = EXCLUDED."ZoneId",
              "Name" = EXCLUDED."Name",
              "SortOrder" = EXCLUDED."SortOrder";
"@

$seedSql | psql $env:AFK4_STAGING_DATABASE_URL
```

Sign in and keep the token only in memory:

```powershell
$signInBody = @{
    organizationId = $organizationId
    userName = 'real-device-smoke@afk4.test'
    password = $env:AFK4_SMOKE_STAFF_PASSWORD
} | ConvertTo-Json -Depth 4

$organizationAdminHeaders = @{
    'X-AFK4-Product' = 'organization-admin'
    'X-AFK4-Compatibility-Epoch' = '2'
    'X-AFK4-Client-Version' = '0.2.0-real-device-smoke'
}

$staffSession = Invoke-RestMethod `
    "$baseUrl/api/organizations/$organizationId/auth/staff/sign-in" `
    -Method Post `
    -Headers $organizationAdminHeaders `
    -ContentType 'application/json' `
    -Body $signInBody

$staffHeaders = @{
    Authorization = "Bearer $($staffSession.accessToken)"
    'X-AFK4-Product' = 'organization-admin'
    'X-AFK4-Compatibility-Epoch' = '2'
    'X-AFK4-Client-Version' = '0.2.0-real-device-smoke'
}
```

Create the minimum billing/session setup:

```powershell
$playerBody = @{
    organizationId = $organizationId
    displayName = "Real Device Smoke Player $runId"
    phoneNumber = "+992$runId"
    idempotencyKey = "real-device-smoke-player-$runId"
} | ConvertTo-Json -Depth 6

$player = Invoke-RestMethod `
    "$baseUrl/api/organizations/$organizationId/branches/$branchId/players" `
    -Method Post `
    -Headers $staffHeaders `
    -ContentType 'application/json' `
    -Body $playerBody

$openShiftBody = @{
    organizationId = $organizationId
    startingCash = @{
        currencyCode = 'TJS'
        minorUnits = 0
    }
    openingNote = "real-device-smoke $runId"
    idempotencyKey = "real-device-smoke-shift-open-$runId"
} | ConvertTo-Json -Depth 8

$shift = Invoke-RestMethod `
    "$baseUrl/api/organizations/$organizationId/branches/$branchId/shifts/open" `
    -Method Post `
    -Headers $staffHeaders `
    -ContentType 'application/json' `
    -Body $openShiftBody

$tariffBody = @{
    organizationId = $organizationId
    name = "Real Device Smoke Hourly $runId"
    idempotencyKey = "real-device-smoke-tariff-$runId"
} | ConvertTo-Json -Depth 6

$tariff = Invoke-RestMethod `
    "$baseUrl/api/organizations/$organizationId/branches/$branchId/tariffs" `
    -Method Post `
    -Headers $staffHeaders `
    -ContentType 'application/json' `
    -Body $tariffBody

$tariffVersionBody = @{
    organizationId = $organizationId
    tariffId = $tariff.tariffId
    currencyCode = 'TJS'
    pricePerMinuteMinorUnits = 100
    minimumBillableMinutes = 1
    roundingIncrementMinutes = 1
    effectiveFromUtc = (Get-Date).ToUniversalTime().AddMinutes(-1).ToString('O')
    idempotencyKey = "real-device-smoke-tariff-version-$runId"
} | ConvertTo-Json -Depth 8

$tariffVersion = Invoke-RestMethod `
    "$baseUrl/api/organizations/$organizationId/branches/$branchId/tariffs/$($tariff.tariffId)/versions" `
    -Method Post `
    -Headers $staffHeaders `
    -ContentType 'application/json' `
    -Body $tariffVersionBody
```

## Download Or Build The Agent Package

Use an internal package only. Do not use this runbook to create a stable
release.

Preferred path for any remaining Slice 3.4 smoke: download the Agent MSI that
the latest green `Package Smoke` published to staging MinIO. The current
verified package is `0.1.29`:

```powershell
$ErrorActionPreference = 'Stop'

New-Item -ItemType Directory -Force C:\AFK4-Smoke | Out-Null

$packageVersion = '0.1.29'
$agentMsiUri = "https://updates.afk4.staging.mubi.dev/afk4-updates-staging/agent-service/internal/$packageVersion/afk4-agent-$packageVersion-internal.msi"
$agentMsiPath = "C:\AFK4-Smoke\afk4-agent-$packageVersion-internal.msi"

curl.exe -I -L --fail $agentMsiUri
curl.exe -L --fail --retry 5 --retry-delay 5 -o $agentMsiPath $agentMsiUri
Get-Item -LiteralPath $agentMsiPath | Select-Object FullName, Length
```

Optional GitHub artifact path, useful when validating the exact run artifacts
before they expire:

```powershell
gh run download 26442315418 `
  --name afk4-package-smoke-msi-0.1.29-internal `
  --dir C:\AFK4-Smoke\package-smoke
```

Fallback release-workstation build path:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' tool restore

powershell -ExecutionPolicy Bypass -File scripts/build-client-packages.ps1 `
  -Version 0.1.0-ci `
  -Channel internal
```

This produces:

```text
artifacts/client-packages/afk4-agent-0.1.0-ci-internal.msi
artifacts/client-packages/afk4-player-shell-0.1.0-ci-internal.msi
artifacts/client-packages/afk4-organization-admin-0.1.0-ci-internal.msi
```

## Enroll With Setup Wizard

Run the Agent MSI from an elevated PowerShell prompt or by double-clicking it
as a local administrator. Keep the install log for evidence:

```powershell
New-Item -ItemType Directory -Force -Path 'C:\ProgramData\AFK4\Agent\InstallLogs' | Out-Null
Start-Process msiexec.exe `
  -Wait `
  -ArgumentList @(
    '/i',
    $agentMsiPath,
    '/norestart',
    '/l*v',
    'C:\ProgramData\AFK4\Agent\InstallLogs\agent-install.log')
```

Expected immediately after install:

- `AFK4.Agent.Service` is installed with automatic startup, but the MSI does
  not start it before wizard enrollment;
- `C:\Program Files\AFK4\Setup Wizard\AFK4.SetupWizard.exe` exists;
- a Start Menu shortcut exists under AFK4;
- HKLM `RunOnce` has an `AFK4 Setup Wizard` entry until successful enrollment
  clears the first-run state;
- the Setup Wizard opens in the interactive desktop when the installer can
  launch it.

If the wizard does not open automatically, run:

```powershell
& 'C:\Program Files\AFK4\Setup Wizard\AFK4.SetupWizard.exe'
```

In the wizard:

1. Sign in with the authorized branch staff account.
2. Choose the branch.
3. Choose a free seat, or create a new seat from the wizard.
4. Select `Gaming PC` or `Manager workstation`.
5. Finish enrollment.

The finished page should expose a clickable `Done` button that closes the
wizard. If an older MSI shows `Done` as a non-clickable label, close the wizard
window or stop `AFK4.SetupWizard.exe` after confirming the bootstrap
environment and Agent Service state.

Expected after successful wizard enrollment:

- machine environment contains `Agent__PlatformBaseUrl`,
  `Agent__OrganizationId`, `Agent__BranchId`, `Agent__DeviceId`,
  `Agent__DeviceRole`, `Agent__DeviceCredentialSecret`,
  `Agent__LeaseSigningPublicKeyPem`, `Agent__UpdateChannel`, and
  `Agent__UpdatePackageSigningPublicKeyPem`;
- HKLM `RunOnce` no longer has an `AFK4 Setup Wizard` entry, and Agent MSI
  upgrades do not re-register first-run wizard launch;
- `Agent__PlatformBaseUrl` is exactly `https://afk4.staging.mubi.dev`, not
  `http://localhost:5074`;
- `AFK4.Agent.Service` is switched to automatic startup and is running;
- `C:\ProgramData\AFK4\Agent\runtime-state.json` appears after the first
  heartbeat loop;
- the dashboard/API device detail shows the selected role, enrollment state,
  seat assignment, and recent heartbeat.

If the service does not start, capture:

```powershell
sc.exe query AFK4.Agent.Service
Get-Content -LiteralPath C:\ProgramData\AFK4\Agent\InstallLogs\agent-install.log -Tail 120
Get-WinEvent -LogName Application -MaxEvents 100 |
  Where-Object { $_.ProviderName -like '*AFK4*' -or $_.Message -like '*AFK4*' } |
  Select-Object TimeCreated, ProviderName, Id, LevelDisplayName, Message
```

If the service starts and then stops with `HttpRequestException` for
`localhost:5074`, the VM was enrolled while the staging API was missing
`Install__ApiBaseUrl`. Confirm the live Coolify Platform API environment has:

```text
Install__ApiBaseUrl=https://afk4.staging.mubi.dev
Install__UpdateChannel=internal
Install__UpdatePackageSigningPublicKeyPem=<staging update signing public PEM>
```

After fixing the live API and restarting it, repair the already-enrolled VM
without reinstalling:

```powershell
[Environment]::SetEnvironmentVariable('Agent__PlatformBaseUrl', 'https://afk4.staging.mubi.dev', 'Machine')
Stop-Service AFK4.Agent.Service -Force -ErrorAction SilentlyContinue
Start-Service AFK4.Agent.Service
Start-Sleep 20
sc.exe query AFK4.Agent.Service
[Environment]::GetEnvironmentVariable('Agent__PlatformBaseUrl', 'Machine')
Get-Content C:\ProgramData\AFK4\Agent\runtime-state.json
```

## Baseline Device Evidence

On the release workstation, verify heartbeat, installed apps, diagnostics, and
SignalR registration evidence.

```powershell
$deviceId = '<deviceId-from-Setup-Wizard-machine-env-or-dashboard>'

$deviceDetail = Invoke-RestMethod `
    "$baseUrl/api/devices/$deviceId" `
    -Headers $staffHeaders

$diagnostics = Invoke-RestMethod `
    "$baseUrl/api/organizations/$organizationId/branches/$branchId/diagnostics" `
    -Headers $staffHeaders

$deviceDetail
$diagnostics.deviceSummary
```

Expected:

- `deviceDetail.isOnline = true`;
- `deviceDetail.lastHeartbeatAtUtc` is recent;
- `deviceDetail.installedAppCount` is greater than `0` on a normal Windows PC;
- diagnostics count the smoke device as online;
- backend logs contain SignalR registration for `/hubs/devices`, or Agent logs
  contain `Realtime device channel connected`.

If no installed apps are reported, confirm the Agent Service account can read
the Windows uninstall registry keys and record the result.

## Player Shell Visible State

Keep a real Windows desktop user logged in, start or restart
`AFK4.Agent.Service`, and wait for at least one heartbeat loop. The Agent should
auto-start Player Shell in the logged-in interactive session.

Verify the Shell process context:

```powershell
Get-Process AFK4.Player.Shell -IncludeUserName -ErrorAction SilentlyContinue |
  Select-Object Id, SessionId, UserName, Path
```

Expected before a session starts:

- Player Shell window is visible full-screen or maximized;
- the Shell process runs in the logged-in user's session, not session `0`;
- state text shows locked/offline until the first Agent state publish arrives;
- after a state publish, state is `locked` and the message says the PC is
  locked.

If the Agent does not auto-start the Shell, record the failure before using any
manual fallback. Capture service status, Application event log entries, and
whether an active user session existed:

```powershell
sc.exe query AFK4.Agent.Service
quser
Get-WinEvent -LogName Application -MaxEvents 100 |
  Where-Object { $_.ProviderName -like '*AFK4*' -or $_.Message -like '*Player Shell*' } |
  Select-Object TimeCreated, ProviderName, Id, LevelDisplayName, Message
```

If the Shell cannot receive named-pipe state from the service, record the
failure and capture the Shell process session/user context.
The Agent state pipe is expected to keep serving the latest state, so a
correctly running Shell must not require a manual restart to observe active or
locked state changes.

If the visible Shell remains locked while
`C:\ProgramData\AFK4\Agent\runtime-state.json` shows `state=active`, check for
duplicate Shell processes. The hardened Agent Service should not auto-start a
non-visible Shell in session `0` by default; if such a process appears, record
it as a regression.

```powershell
Get-Process AFK4.Player.Shell -IncludeUserName -ErrorAction SilentlyContinue |
  Select-Object Id, SessionId, UserName, Path
```

Manual launch is only a diagnostic fallback after recording an auto-start or
duplicate-process regression. For fallback evidence, stop duplicate Shell
processes and relaunch the visible Shell from the interactive desktop session:

```powershell
Get-Process AFK4.Player.Shell -ErrorAction SilentlyContinue |
  Stop-Process -Force

Start-Sleep -Seconds 1

& 'C:\Program Files\AFK4\Player Shell\AFK4.Player.Shell.exe'
```

Record this as a Player Shell supervision regression, not as a backend/session
failure, when local runtime state and backend device status are already correct.

## Session Start And Unlock Smoke

Start a short postpaid session. This avoids wallet top-up setup while still
exercising backend-confirmed session billing and device commands.

```powershell
$startSessionBody = @{
    organizationId = $organizationId
    seatId = $seatId
    durationMinutes = 20
    tariffRuleVersionId = $tariffVersion.tariffVersionId
    idempotencyKey = "real-device-smoke-start-$runId"
    playerAccountId = $player.playerAccountId
    billingMode = 'postpaid_debt'
    tariffVersionId = $tariffVersion.tariffVersionId
} | ConvertTo-Json -Depth 8

$startedSession = Invoke-RestMethod `
    "$baseUrl/api/organizations/$organizationId/branches/$branchId/sessions/start" `
    -Method Post `
    -Headers $staffHeaders `
    -ContentType 'application/json' `
    -Body $startSessionBody

$sessionId = $startedSession.session.sessionId
$startedSession.deviceCommands
```

Expected:

- response session state is `active`;
- `deviceCommands` includes an `unlock` command with a signed lease payload;
- after one heartbeat or SignalR command delivery, device command status moves
  away from `Pending`;
- `C:\ProgramData\AFK4\Agent\session-lease.json` appears on the PC;
- `C:\ProgramData\AFK4\Agent\runtime-state.json` shows active state;
- Player Shell visible state changes to active and shows remaining time;
- physical lock/unlock behavior is recorded honestly. If the current adapter
  only logs the request, mark physical enforcement as not yet passing.

Inspect recent command status:

```powershell
$deviceDetail = Invoke-RestMethod "$baseUrl/api/devices/$deviceId" -Headers $staffHeaders
$deviceDetail.recentCommands | Select-Object commandId,type,status,message,updatedAtUtc
```

On the PC:

```powershell
[Environment]::GetEnvironmentVariable('Agent__DeviceId', 'Machine')
Get-Content -LiteralPath C:\ProgramData\AFK4\Agent\runtime-state.json -Raw
Get-Content -LiteralPath C:\ProgramData\AFK4\Agent\session-lease.json -Raw
```

## Lease Refresh And Lease Expiry

The backend issues 15-minute leases by default and refreshes when the Agent
heartbeat reports a matching lease that is within 5 minutes of expiry.

Basic lease refresh check:

1. Keep the Agent online after session start.
2. Wait until the first lease has less than 5 minutes remaining. With default
   settings this is about 10 minutes after the first unlock.
3. Confirm a `refresh-session-lease` command appears and is acknowledged.

```powershell
$deviceDetail = Invoke-RestMethod "$baseUrl/api/devices/$deviceId" -Headers $staffHeaders
$deviceDetail.recentCommands | Where-Object { $_.type -eq 'refresh-session-lease' }
```

Basic lease expiry check:

1. Confirm a valid active `session-lease.json` exists.
2. Temporarily disconnect only the test PC from the network.
3. Wait until the local `leaseExpiresAtUtc` time passes.
4. Reconnect the test PC.
5. Record whether the Agent cleared the lease, marked runtime locked, and
   requested lock enforcement.

Pass for this step means lease expiry is observed and the Agent returns to
locked local runtime state. Physical desktop lock is a separate enforcement
result and must be recorded separately.

## Session End And Lock Smoke

End the session from the backend:

```powershell
$endSessionBody = @{
    reason = "real-device-smoke $runId"
    idempotencyKey = "real-device-smoke-end-$runId"
} | ConvertTo-Json -Depth 6

$endingSession = Invoke-RestMethod `
    "$baseUrl/api/organizations/$organizationId/sessions/$sessionId/end" `
    -Method Post `
    -Headers $staffHeaders `
    -ContentType 'application/json' `
    -Body $endSessionBody
```

Expected:

- response session state is `ending`;
- Agent receives or fetches a `lock` command;
- command status becomes accepted or completed according to current Agent
  behavior;
- backend advances the session from `ending` to `ended` after the accepted or
  completed `lock` command result, either directly from command-result
  processing or from the next heartbeat recovery check if the result was already
  persisted;
- local lease is cleared;
- Player Shell state returns to locked;
- physical lock result is recorded honestly.

After lock is accepted, confirm the seat/device can be reused through the
normal product path by starting a second short session with a new idempotency
key. If the backend still reports the session as `ending` or blocks the second
start with `Seat or device already has an active session`, record that as a
session-finalization regression after confirming at least one heartbeat has
arrived after the accepted lock. Do not use manual SQL reactivation as pass
evidence.

```powershell
$restartSessionBody = @{
    organizationId = $organizationId
    seatId = $seatId
    durationMinutes = 10
    tariffRuleVersionId = $tariffVersion.tariffVersionId
    idempotencyKey = "real-device-smoke-restart-$runId"
    playerAccountId = $player.playerAccountId
    billingMode = 'postpaid_debt'
    tariffVersionId = $tariffVersion.tariffVersionId
} | ConvertTo-Json -Depth 8

$restartedSession = Invoke-RestMethod `
    "$baseUrl/api/organizations/$organizationId/branches/$branchId/sessions/start" `
    -Method Post `
    -Headers $staffHeaders `
    -ContentType 'application/json' `
    -Body $restartSessionBody
```

## Update Check And Status Smoke

Run this baseline check even when no package is being offered. It verifies the
device-authenticated update boundary without installing anything.

For a `gaming_pc`, report `agent-service` and `player-shell`. For a
`manager_workstation`, report `agent-service` and `organization-admin` instead.
The example below is the gaming-PC shape.

```powershell
$deviceCredentialSecret = '<device-credential-secret-from-Setup-Wizard-machine-env>'

$updateCheckBody = @{
    organizationId = $organizationId
    branchId = $branchId
    deviceId = $deviceId
    channel = 'internal'
    checkedAtUtc = (Get-Date).ToUniversalTime().ToString('O')
    installedComponents = @(
        @{
            component = 'agent-service'
            version = '0.1.0'
        },
        @{
            component = 'player-shell'
            version = '0.1.0'
        }
    )
} | ConvertTo-Json -Depth 8

$updateCheck = Invoke-RestMethod `
    "$baseUrl/api/devices/$deviceId/updates/check" `
    -Method Post `
    -Headers @{ 'X-AFK4-Device-Credential' = $deviceCredentialSecret } `
    -ContentType 'application/json' `
    -Body $updateCheckBody
```

Expected: response is HTTP 200 and either an empty `updates` array or an
internal package instruction that was deliberately prepared for this device.

Only run update installation/status smoke when an internal, non-stable rollout
is intentionally prepared for this exact device. If it is prepared, collect:

- update package id;
- rollout id;
- Agent update logs under `C:\ProgramData\AFK4\Agent\UpdateLogs`;
- backend rollout status from `GET /api/organizations/{organizationId}/branches/{branchId}/updates/rollouts`;
- device status rows from diagnostics.

Expected for a passing Agent-side update smoke:

- the Agent downloads a non-zero MSI under
  `C:\ProgramData\AFK4\Agent\Updates`;
- the update log is written under
  `C:\ProgramData\AFK4\Agent\UpdateLogs`;
- Windows Installer logs successful Agent, Player Shell, or Organization Admin MSI
  installs for the components intentionally offered to this device;
- the Agent service restarts and continues heartbeats;
- backend rollout status for this device reaches `installed`;
- device detail reports the target Agent/Shell versions.

If an Agent Service update advances `Agent__AgentVersion` but
`AFK4.Agent.Service` remains stopped, collect the current update-state JSON and
Windows Installer log, then start the service once so recovery can report the
interrupted install as `installed`. Agent MSI helper builds after the VM2
staging repair start `AFK4.Agent.Service` directly after a successful
`agent-service` MSI because Windows Installer can stop the old Agent process
before its in-process restart scheduler runs.

Do not create a fake successful `POST /api/devices/{deviceId}/updates/status`
for a package that was not actually offered to the Agent.

## Diagnostics And Audit Evidence

Collect backend evidence:

```powershell
$diagnostics = Invoke-RestMethod `
    "$baseUrl/api/organizations/$organizationId/branches/$branchId/diagnostics" `
    -Headers $staffHeaders

$audit = Invoke-RestMethod `
    "$baseUrl/api/organizations/$organizationId/branches/$branchId/audit?limit=50" `
    -Headers $staffHeaders

$diagnostics
$audit.records | Select-Object action,outcome,targetId,createdAtUtc
```

Collect PC evidence:

```powershell
sc.exe query AFK4.Agent.Service
sc.exe qc AFK4.Agent.Service
Get-ChildItem -LiteralPath C:\ProgramData\AFK4\Agent -Force
Get-Content -LiteralPath C:\ProgramData\AFK4\Agent\runtime-state.json -Raw
Get-WinEvent -LogName Application -MaxEvents 200 |
  Where-Object { $_.ProviderName -like '*AFK4*' -or $_.Message -like '*Heartbeat*' -or $_.Message -like '*Command*' } |
  Select-Object TimeCreated, ProviderName, Id, LevelDisplayName, Message
```

Evidence to collect:

- staging health response time and status;
- migration state if DB access is available;
- staff sign-in success without recording token values;
- owner-code generation/rotation timestamp and suffix, not the full code;
- enrolled `deviceId`, not the credential secret;
- service install log;
- `sc.exe query` and `sc.exe qc` output, including `START_TYPE` automatic
  startup after enrollment;
- first successful heartbeat timestamp;
- SignalR registration or Agent realtime connection log;
- installed app count and sample non-sensitive app names;
- session id, command ids, command types, and statuses;
- Player Shell screenshots for locked, active, and ending or locked-after-end
  states;
- diagnostics summary before and after session end;
- update check response shape and whether updates were empty or intentionally
  offered.

## Pass criteria

Overall pass requires:

- staging `GET /api/health` returns `status = ok` without insecure TLS flags;
- staging staff sign-in succeeds for the smoke staff user;
- the customer dashboard displays/generates an owner code without direct
  database edits;
- the single `AFK4 Agent` MSI installs on one clean Windows 10/11 PC;
- Setup Wizard discovers branches/floor-map data with the owner code and
  enrolls the device into the selected branch, seat, and role;
- the Agent Service runs as `AFK4.Agent.Service`;
- authenticated heartbeat succeeds repeatedly;
- SignalR connects and registers the device, or the fallback heartbeat command
  path is explicitly observed;
- installed apps are reported and visible in device detail;
- `gaming_pc` role installs Player Shell through the update channel, or a
  concrete role-aware update blocker is recorded;
- `manager_workstation` role installs or verifies WebView2 and installs
  Organization Admin through the update channel, or a concrete role-aware update
  blocker is recorded;
- session start returns backend approval and creates an unlock command;
- Agent accepts the signed lease and records active runtime state;
- Player Shell is auto-started by the Agent into the interactive desktop
  session, or a concrete auto-start blocker is recorded before any manual
  fallback;
- lease refresh is observed or the wait was intentionally skipped and recorded;
- lease expiry behavior is observed when the network-disconnect step is run;
- session end creates a lock command and Agent returns local runtime state to
  locked;
- accepted or completed lock command result advances the backend session to
  `ended`;
- a second session can start on the same seat/device without SQL cleanup;
- Player Shell visible state is observed or a concrete Shell/session-launch
  blocker is recorded;
- diagnostics show the device, command, and update summaries;
- when an update rollout is part of the run, the Agent installs it from the
  hosted MSI and reports `installed` through the backend;
- no secrets are written to the repository.

Fail the smoke, or mark it partial, when:

- any command uses an untrusted TLS bypass;
- the device credential is missing, committed, or pasted into a durable doc;
- the Agent makes a critical session decision without backend confirmation;
- the service cannot start;
- heartbeat succeeds only by disabling credential validation;
- SignalR and heartbeat command fallback both fail;
- signed lease validation rejects the backend-issued lease;
- physical lock/unlock is claimed without evidence;
- Player Shell state is claimed without a visible screenshot or runtime log.
- a service-session `AFK4.Player.Shell.exe` competes with the visible Shell for
  named-pipe state;
- session reuse requires manual SQL after an accepted lock result;
- update smoke requires manually copying a rebuilt package onto an already
  enrolled PC instead of using the signed MSI rollout path.

## Cleanup

On the PC, remove machine-scoped smoke secrets after the run:

```powershell
Stop-Service -Name AFK4.Agent.Service -ErrorAction SilentlyContinue

[Environment]::SetEnvironmentVariable('Agent__DeviceCredentialSecret', $null, 'Machine')
[Environment]::SetEnvironmentVariable('Agent__LeaseSigningPublicKeyPem', $null, 'Machine')

Remove-Item -LiteralPath 'C:\AFK4-Smoke' -Recurse -Force -ErrorAction SilentlyContinue
```

On the release workstation:

```powershell
$env:AFK4_STAGING_STAFF_PASSWORD = $null
$env:AFK4_SMOKE_STAFF_PASSWORD = $null
$env:AFK4_SMOKE_STAFF_PASSWORD_HASH = $null
```

Before repeating a clean `manager_workstation` smoke, remove mistaken staging
manager-workstation seat assignments through the API helper rather than direct
SQL. The helper defaults to the fixed staging smoke organization/branch IDs,
protects the fixed gaming-PC smoke seat, and runs as a dry run unless `-Apply`
is passed:

```powershell
Set-Location D:\projects\afk4.net
$env:AFK4_STAGING_STAFF_PASSWORD = '<one-time-staging-smoke-password>'

.\scripts\cleanup-manager-workstation-smoke-data.ps1

.\scripts\cleanup-manager-workstation-smoke-data.ps1 `
  -Apply `
  -DeleteEmptySmokeSeats
```

Expected cleanup behavior:

- `manager_workstation` devices that still have floor-map seats are removed via
  `POST /api/devices/{deviceId}/remove`, which revokes active credentials and
  detaches active seat assignments.
- Empty smoke/operator/manager seats are deleted only when
  `-DeleteEmptySmokeSeats` is present. Seats with an active device assignment,
  active session, or session history must remain for manual inspection.
- Use `-DeviceId` or `-SeatId` to narrow an ambiguous run. Do not broaden the
  regex filters or use direct database edits unless the API path is unavailable
  and the exception is recorded.

Leave staging data in place only when the next smoke run should reuse the same
organization, branch, staff user, and seat. Revoke stale device credentials
from staging before assigning a replacement PC to the same smoke seat.
