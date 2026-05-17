# Real Device Windows PC Smoke Runbook

Status: manual staging smoke path  
Last updated: 2026-05-17

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
- Windows 10/11 gaming PC install and Agent configuration;
- device enrollment and credential authentication;
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
- any branch-local authority or browser-based administration surface;
- non-Windows client runtime validation.

## Current Caveats

- Operator App currently constructs `OperatorAppOptions` with the default
  `http://localhost:5074` base URL. Use it in this staging smoke only if a
  staging-configured build is prepared; otherwise use the API commands below
  and treat Operator App as optional observation only.
- The preferred Windows 11 VM install path is now the staging Gaming PC setup
  executable. It is built on the release workstation and copied to the VM; the
  VM does not need the repository, .NET SDK, PowerShell runbook execution, or
  manual Agent environment-variable commands.
- `WorkstationLockController` currently records lock/unlock requests through
  the enforcement adapter. If the physical Windows desktop does not actually
  lock or unlock, record that as a real enforcement gap rather than inventing a
  pass.
- Player Shell auto-start from the Agent Service is disabled by default for
  MVP hardening. Launch the Shell from the logged-in interactive Windows
  session for this smoke, and record whether named-pipe state updates are
  received. Do not enable service-session Shell auto-start as a pass shortcut.
- Do not commit secrets, filled environment files, database URLs, staff
  passwords, device credential secrets, PEM files, MSI artifacts, or smoke
  transcripts containing secrets.

## Endpoint Checklist

The smoke exercises these API boundaries:

| Area | Endpoint |
| --- | --- |
| Health | `GET /api/health` |
| Staff auth | `POST /api/auth/staff/sign-in` |
| Enrollment code | `POST /api/branches/{branchId}/device-enrollment-codes` |
| Device enrollment | `POST /api/devices/enroll` |
| Heartbeat | `POST /api/devices/{deviceId}/heartbeat` |
| SignalR | `/hubs/devices` |
| Installed apps | `POST /api/devices/{deviceId}/installed-apps/report` |
| Device detail | `GET /api/devices/{deviceId}` |
| Sessions | `POST /api/branches/{branchId}/sessions/start` |
| Session end | `POST /api/sessions/{sessionId}/end` |
| Reconciliation | `POST /api/devices/{deviceId}/session-reconciliation` |
| Diagnostics | `GET /api/branches/{branchId}/diagnostics` |
| Updates | `POST /api/devices/{deviceId}/updates/check` |
| Updates | `POST /api/devices/{deviceId}/updates/status` |

## Prerequisites

Release workstation:

- PowerShell.
- Git for Windows.
- .NET SDK `10.0.203`.
- `psql` access to staging through a trusted shell, private network path, or
  temporary approved tunnel.
- Staging database URL available only in the current shell as
  `AFK4_STAGING_DATABASE_URL`.
- Staging session lease public key PEM available outside the repository.

Windows gaming PC:

- Windows 10/11 x64.
- Local Administrator access.
- Outbound HTTPS access to `https://afk4.staging.mubi.dev`.
- The current internal Gaming PC MSI publishes Agent Service and Player Shell
  as self-contained `win-x64` outputs, so a separate .NET Desktop Runtime
  install is not required for the MSI smoke path.
- A clean test Windows user session where Player Shell can be observed.

## Prepare Staging Data

Use one PowerShell session on the release workstation. Do not paste real
secrets into chat or repository files.

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

Seed the staging smoke organization, branch, staff user, and seat. This uses
direct SQL because the current MVP does not yet include operator-safe staff or
layout management screens.

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
    'branch_manager')
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

$staffSession = Invoke-RestMethod `
    "$baseUrl/api/auth/staff/sign-in" `
    -Method Post `
    -ContentType 'application/json' `
    -Body $signInBody

$staffHeaders = @{
    Authorization = "Bearer $($staffSession.accessToken)"
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
    "$baseUrl/api/branches/$branchId/players" `
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
    "$baseUrl/api/branches/$branchId/shifts/open" `
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
    "$baseUrl/api/branches/$branchId/tariffs" `
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
    "$baseUrl/api/branches/$branchId/tariffs/$($tariff.tariffId)/versions" `
    -Method Post `
    -Headers $staffHeaders `
    -ContentType 'application/json' `
    -Body $tariffVersionBody
```

## Build Or Select The Gaming PC Package

Use an internal package only. Do not use this runbook to create a stable
release.

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' tool restore

powershell -ExecutionPolicy Bypass -File scripts/build-client-packages.ps1 `
  -Version 0.1.0-ci `
  -Channel internal `
  -StagingLeasePublicKeyPath .\deploy\coolify\staging-session-signing-public.pem
```

Preferred path for clean Windows 11 VMs: copy this single file to the VM and
run it as administrator:

```text
artifacts/client-packages/afk4-gaming-pc-setup-0.1.0-ci-internal.exe
```

The setup executable is staging-only for now. It has the staging Platform API,
organization, and branch fixed at build time. It asks for staff username and
password, creates the enrollment code, enrolls the VM, installs the bundled MSI,
writes Agent machine configuration, starts `AFK4.Agent.Service`, and waits for
backend heartbeat evidence.

Fallback/manual path: copy this MSI to the Windows gaming PC through a secure
internal channel and follow the explicit configuration commands below.

```text
artifacts/client-packages/afk4-gaming-pc-0.1.0-ci-internal.msi
```

## Enroll The Device

Create a short-lived enrollment code:

```powershell
$codeBody = @{
    organizationId = $organizationId
    expiresInSeconds = 300
} | ConvertTo-Json -Depth 4

$code = Invoke-RestMethod `
    "$baseUrl/api/branches/$branchId/device-enrollment-codes" `
    -Method Post `
    -Headers $staffHeaders `
    -ContentType 'application/json' `
    -Body $codeBody
```

Enroll the PC from the release workstation or from the PC. Use the real PC name
in `machineName`.

```powershell
$machineName = 'REAL-PC-SMOKE-001'

$enrollBody = @{
    organizationId = $organizationId
    branchId = $branchId
    enrollmentCode = $code.code
    machineName = $machineName
    agentVersion = '0.1.0'
    shellVersion = '0.1.0'
    requestedAtUtc = (Get-Date).ToUniversalTime().ToString('O')
} | ConvertTo-Json -Depth 4

$enrollment = Invoke-RestMethod `
    "$baseUrl/api/devices/enroll" `
    -Method Post `
    -ContentType 'application/json' `
    -Body $enrollBody
```

Assign the enrolled device to the smoke seat:

```powershell
$assignmentId = [Guid]::NewGuid().ToString('D')

$assignSql = @"
UPDATE device_seat_assignments
SET "DetachedAtUtc" = now()
WHERE ("SeatId" = '$seatId' OR "DeviceId" = '$($enrollment.deviceId)')
  AND "DetachedAtUtc" IS NULL;

INSERT INTO device_seat_assignments (
    "DeviceSeatAssignmentId",
    "OrganizationId",
    "BranchId",
    "SeatId",
    "DeviceId",
    "AttachedAtUtc",
    "DetachedAtUtc")
VALUES (
    '$assignmentId',
    '$organizationId',
    '$branchId',
    '$seatId',
    '$($enrollment.deviceId)',
    now(),
    NULL);
"@

$assignSql | psql $env:AFK4_STAGING_DATABASE_URL
```

## Configure The Windows Gaming PC

Skip this section when using
`afk4-gaming-pc-setup-0.1.0-ci-internal.exe`; the setup executable performs
these actions itself.

Run these commands from an elevated PowerShell prompt on the Windows gaming PC
only when using the fallback MSI path.
Replace placeholders with values from the enrollment response and the staging
lease public key file supplied outside the repository.

```powershell
$packagePath = 'C:\AFK4-Smoke\afk4-gaming-pc-0.1.0-ci-internal.msi'
$publicKeyPath = 'C:\AFK4-Smoke\staging-session-signing-public.pem'
$deviceCredentialSecret = '<credentialSecret-from-enrollment-response>'
$organizationId = '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08'
$branchId = 'acfc0212-967f-4d84-94be-9003387b09c2'
$deviceId = '<deviceId-from-enrollment-response>'

New-Item -ItemType Directory -Force -Path 'C:\ProgramData\AFK4\Agent\InstallLogs' | Out-Null
msiexec.exe /i $packagePath /qn /norestart /l*v C:\ProgramData\AFK4\Agent\InstallLogs\gaming-pc-install.log

Stop-Service -Name AFK4.Agent.Service -ErrorAction SilentlyContinue

$leasePublicKeyPem = Get-Content -Raw -LiteralPath $publicKeyPath

[Environment]::SetEnvironmentVariable('Agent__PlatformBaseUrl', 'https://afk4.staging.mubi.dev', 'Machine')
[Environment]::SetEnvironmentVariable('Agent__OrganizationId', $organizationId, 'Machine')
[Environment]::SetEnvironmentVariable('Agent__BranchId', $branchId, 'Machine')
[Environment]::SetEnvironmentVariable('Agent__DeviceId', $deviceId, 'Machine')
[Environment]::SetEnvironmentVariable('Agent__MachineName', $env:COMPUTERNAME, 'Machine')
[Environment]::SetEnvironmentVariable('Agent__AgentVersion', '0.1.0', 'Machine')
[Environment]::SetEnvironmentVariable('Agent__ShellVersion', '0.1.0', 'Machine')
[Environment]::SetEnvironmentVariable('Agent__DeviceCredentialSecret', $deviceCredentialSecret, 'Machine')
[Environment]::SetEnvironmentVariable('Agent__LeaseSigningPublicKeyPem', $leasePublicKeyPem, 'Machine')
[Environment]::SetEnvironmentVariable('Agent__PlayerShellExecutablePath', 'C:\Program Files\AFK4\Player Shell\AFK4.Player.Shell.exe', 'Machine')
[Environment]::SetEnvironmentVariable('Agent__UpdateChannel', 'internal', 'Machine')
[Environment]::SetEnvironmentVariable('Agent__UpdateInstallerExecutablePath', 'powershell.exe', 'Machine')
[Environment]::SetEnvironmentVariable('Agent__UpdateInstallerArgumentsTemplate', '-NoProfile -ExecutionPolicy Bypass -File "C:\Program Files\AFK4\Update Helpers\install-afk4-update-msi.ps1" -PackagePath "{PackagePath}" -Component "{Component}" -Version "{Version}"', 'Machine')
[Environment]::SetEnvironmentVariable('Agent__UpdateRollbackExecutablePath', 'powershell.exe', 'Machine')
[Environment]::SetEnvironmentVariable('Agent__UpdateRollbackArgumentsTemplate', '-NoProfile -ExecutionPolicy Bypass -File "C:\Program Files\AFK4\Update Helpers\rollback-afk4-update-msi.ps1" -PackagePath "{PackagePath}" -Component "{Component}" -Version "{Version}"', 'Machine')
[Environment]::SetEnvironmentVariable('Agent__UpdateRestartExecutablePath', 'powershell.exe', 'Machine')
[Environment]::SetEnvironmentVariable('Agent__UpdateRestartArgumentsTemplate', '-NoProfile -ExecutionPolicy Bypass -File "C:\Program Files\AFK4\Update Helpers\restart-afk4-agent-service.ps1"', 'Machine')

Start-Service -Name AFK4.Agent.Service
sc.exe query AFK4.Agent.Service
```

Expected:

- service state becomes `RUNNING`;
- install log exists under `C:\ProgramData\AFK4\Agent\InstallLogs`;
- `C:\ProgramData\AFK4\Agent\runtime-state.json` exists after the first
  heartbeat loop;
- the backend device detail shows a recent heartbeat.

If the service does not start, capture:

```powershell
sc.exe query AFK4.Agent.Service
Get-Content -LiteralPath C:\ProgramData\AFK4\Agent\InstallLogs\gaming-pc-install.log -Tail 120
Get-WinEvent -LogName Application -MaxEvents 100 |
  Where-Object { $_.ProviderName -like '*AFK4*' -or $_.Message -like '*AFK4*' } |
  Select-Object TimeCreated, ProviderName, Id, LevelDisplayName, Message
```

## Baseline Device Evidence

On the release workstation, verify heartbeat, installed apps, diagnostics, and
SignalR registration evidence.

```powershell
$deviceId = $enrollment.deviceId

$deviceDetail = Invoke-RestMethod `
    "$baseUrl/api/devices/$deviceId" `
    -Headers $staffHeaders

$diagnostics = Invoke-RestMethod `
    "$baseUrl/api/branches/$branchId/diagnostics" `
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

Start the Shell from the logged-in desktop session:

```powershell
& 'C:\Program Files\AFK4\Player Shell\AFK4.Player.Shell.exe'
```

Expected before a session starts:

- Player Shell window is visible full-screen or maximized;
- state text shows locked/offline until the first Agent state publish arrives;
- after a state publish, state is `locked` and the message says the PC is
  locked.

If the Shell cannot receive named-pipe state from the service, record the
failure and capture the Shell process session/user context.

If the visible Shell remains locked while
`C:\ProgramData\AFK4\Agent\runtime-state.json` shows `state=active`, check for
duplicate Shell processes. The hardened Agent Service should not auto-start a
non-visible Shell in session `0` by default; if such a process appears, record
it as a regression.

```powershell
Get-Process AFK4.Player.Shell -IncludeUserName -ErrorAction SilentlyContinue |
  Select-Object Id, SessionId, UserName, Path
```

For manual smoke evidence after any duplicate-process regression, stop the
duplicate Shell processes and relaunch the visible Shell from the interactive
desktop session:

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
    "$baseUrl/api/branches/$branchId/sessions/start" `
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
    "$baseUrl/api/sessions/$sessionId/end" `
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
  completed `lock` command result;
- local lease is cleared;
- Player Shell state returns to locked;
- physical lock result is recorded honestly.

After lock is accepted, confirm the seat/device can be reused through the
normal product path by starting a second short session with a new idempotency
key. If the backend still reports the session as `ending` or blocks the second
start with `Seat or device already has an active session`, record that as a
session-finalization regression. Do not use manual SQL reactivation as pass
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
    "$baseUrl/api/branches/$branchId/sessions/start" `
    -Method Post `
    -Headers $staffHeaders `
    -ContentType 'application/json' `
    -Body $restartSessionBody
```

## Update Check And Status Smoke

Run this baseline check even when no package is being offered. It verifies the
device-authenticated update boundary without installing anything.

```powershell
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
    -Headers @{ 'X-AFK4-Device-Credential' = $enrollment.credentialSecret } `
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
- backend rollout status from `GET /api/branches/{branchId}/updates/rollouts`;
- device status rows from diagnostics.

Do not create a fake successful `POST /api/devices/{deviceId}/updates/status`
for a package that was not actually offered to the Agent.

## Diagnostics And Audit Evidence

Collect backend evidence:

```powershell
$diagnostics = Invoke-RestMethod `
    "$baseUrl/api/branches/$branchId/diagnostics" `
    -Headers $staffHeaders

$audit = Invoke-RestMethod `
    "$baseUrl/api/branches/$branchId/audit?limit=50" `
    -Headers $staffHeaders

$diagnostics
$audit.records | Select-Object action,outcome,targetId,createdAtUtc
```

Collect PC evidence:

```powershell
sc.exe query AFK4.Agent.Service
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
- enrollment code creation timestamp, not the code value;
- enrolled `deviceId`, not the credential secret;
- service install log;
- `sc.exe query` output;
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
- a short-lived enrollment code enrolls one Windows 10/11 PC;
- the Agent Service runs as `AFK4.Agent.Service`;
- authenticated heartbeat succeeds repeatedly;
- SignalR connects and registers the device, or the fallback heartbeat command
  path is explicitly observed;
- installed apps are reported and visible in device detail;
- session start returns backend approval and creates an unlock command;
- Agent accepts the signed lease and records active runtime state;
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
- session reuse requires manual SQL after an accepted lock result.

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
$env:AFK4_SMOKE_STAFF_PASSWORD = $null
$env:AFK4_SMOKE_STAFF_PASSWORD_HASH = $null
```

Leave staging data in place only when the next smoke run should reuse the same
organization, branch, staff user, and seat. Revoke stale device credentials
from staging before assigning a replacement PC to the same smoke seat.
