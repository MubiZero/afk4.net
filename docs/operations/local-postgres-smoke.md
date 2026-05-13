# Local PostgreSQL And Device Smoke Runbook

This runbook verifies the current AFK4 identity, audit, layout, device
persistence, and session lifecycle slice against a real local PostgreSQL
database. It covers EF migration application, staff sign-in, authorized device
enrollment-code creation, device enrollment, authenticated heartbeat
persistence, persisted floor-map reads, signed session leases, session command
idempotency, reconnect reconciliation, installed app reporting, device detail
reads, command status storage, and staff-protected device credential
rotation/revocation.

The commands assume PowerShell from the repository root:

```powershell
Set-Location D:\afk4.net
```

## Prerequisites

- Docker Desktop or another Docker runtime with Compose support.
- .NET SDK `10.0.203`.
- Port `5432` available on `127.0.0.1`.
- Port `5074` available for the Platform API.

The Compose file binds PostgreSQL to localhost only and uses trust
authentication for local development. Do not use this Compose profile for
staging or production.

## Start PostgreSQL

```powershell
docker compose up -d postgres
docker compose ps
```

Wait until the `afk4-postgres` health state is `healthy`.

If the database must be reset completely:

```powershell
docker compose down -v
docker compose up -d postgres
```

## Restore EF Tool

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' tool restore
```

## Apply EF Migrations

The current local connection string matches the Platform API fallback and the
Compose service:

```text
Host=localhost;Port=5432;Database=afk4_dev;Username=postgres
```

Apply the migrations:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' ef database update `
  --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj `
  --startup-project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj
```

## Start Platform API

Generate a local ECDSA key pair for backend session lease signing. Keep the
private key only in local environment/configuration; the Agent receives only
the public key.

```powershell
$leaseSigningKey = [System.Security.Cryptography.ECDsa]::Create(
    [System.Security.Cryptography.ECCurve+NamedCurves]::nistP256)
$env:Sessions__SigningPrivateKeyPem = $leaseSigningKey.ExportECPrivateKeyPem()
$env:Agent__LeaseSigningPublicKeyPem = $leaseSigningKey.ExportSubjectPublicKeyInfoPem()
```

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' run `
  --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj `
  --urls http://localhost:5074
```

Keep this process running while executing the smoke commands in another
PowerShell session.

## Live Smoke

Use stable IDs for repeatable smoke requests:

```powershell
$baseUrl = 'http://localhost:5074'
$organizationId = '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08'
$branchId = 'acfc0212-967f-4d84-94be-9003387b09c2'
```

Verify health:

```powershell
Invoke-RestMethod "$baseUrl/api/health"
```

Seed a local branch manager staff user for the smoke run. The seeded password
is `Passw0rd!` and the password hash is for local development only:

```powershell
$staffUserId = '3db1367b-88c6-4b1c-99c3-bcbb5f4d5134'
$staffRoleAssignmentId = '58e8a836-82cd-45d1-a0cc-c13621e76c4e'
$passwordHash = 'AQAAAAIAAYagAAAAEBtg5uNEqBhvMLTcq8WPczYLamzC17d4URzbuoedWQV8HBZPONhd1Wapb1t6X/wKag=='

$seedSql = @"
INSERT INTO organizations ("OrganizationId", "Name", "CreatedAtUtc")
VALUES ('$organizationId', 'Demo Organization', now())
ON CONFLICT ("OrganizationId")
DO UPDATE SET "Name" = EXCLUDED."Name";

INSERT INTO branches ("BranchId", "OrganizationId", "Name", "CreatedAtUtc")
VALUES ('$branchId', '$organizationId', 'Demo Branch', now())
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
    'tech@afk4.test',
    'TECH@AFK4.TEST',
    'Smoke Branch Manager',
    '$passwordHash',
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
"@

$seedSql | docker exec -i afk4-postgres psql -U postgres -d afk4_dev
```

Sign in as the local branch manager:

```powershell
$signInBody = @{
    organizationId = $organizationId
    userName = 'tech@afk4.test'
    password = 'Passw0rd!'
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

Rotate the refresh token once and continue with the refreshed access token:

```powershell
$refreshBody = @{
    organizationId = $organizationId
    refreshToken = $staffSession.refreshToken
} | ConvertTo-Json -Depth 4

$refreshedStaffSession = Invoke-RestMethod `
    "$baseUrl/api/auth/staff/refresh" `
    -Method Post `
    -ContentType 'application/json' `
    -Body $refreshBody

$staffHeaders = @{
    Authorization = "Bearer $($refreshedStaffSession.accessToken)"
}
```

Create an enrollment code with the staff bearer token:

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

Enroll a device:

```powershell
$enrollBody = @{
    organizationId = $organizationId
    branchId = $branchId
    enrollmentCode = $code.code
    machineName = 'PC-SMOKE-001'
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

Post an authenticated heartbeat:

```powershell
$heartbeatBody = @{
    organizationId = $organizationId
    branchId = $branchId
    deviceId = $enrollment.deviceId
    machineName = 'PC-SMOKE-001'
    agentVersion = '0.1.0'
    shellVersion = '0.1.0'
    observedAtUtc = (Get-Date).ToUniversalTime().ToString('O')
    isLocked = $true
} | ConvertTo-Json -Depth 4

$heartbeat = Invoke-RestMethod `
    "$baseUrl/api/devices/$($enrollment.deviceId)/heartbeat" `
    -Method Post `
    -Headers @{ 'X-AFK4-Device-Credential' = $enrollment.credentialSecret } `
    -ContentType 'application/json' `
    -Body $heartbeatBody
```

Seed one zone, one seat, and an active device-seat assignment for the enrolled
device:

```powershell
$zoneId = '2e37f7b3-41bb-4a19-9d50-94eb848f4e01'
$seatId = '9f3adbd3-957e-4dc8-8d34-a6bfa56b9275'
$assignmentId = 'ad8c15f4-7ff1-44b4-9f9e-f27e2f0c1b44'

@"
INSERT INTO zones ("ZoneId", "OrganizationId", "BranchId", "Name", "SortOrder", "CreatedAtUtc")
VALUES ('$zoneId', '$organizationId', '$branchId', 'Main Hall', 10, now())
ON CONFLICT ("ZoneId")
DO UPDATE SET "Name" = EXCLUDED."Name",
              "SortOrder" = EXCLUDED."SortOrder";

INSERT INTO seats ("SeatId", "OrganizationId", "BranchId", "ZoneId", "Name", "SortOrder", "CreatedAtUtc")
VALUES ('$seatId', '$organizationId', '$branchId', '$zoneId', 'PC-SMOKE-001', 10, now())
ON CONFLICT ("SeatId")
DO UPDATE SET "ZoneId" = EXCLUDED."ZoneId",
              "Name" = EXCLUDED."Name",
              "SortOrder" = EXCLUDED."SortOrder";

UPDATE device_seat_assignments
SET "DetachedAtUtc" = now()
WHERE "DeviceId" = '$($enrollment.deviceId)'
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
    NULL)
ON CONFLICT ("DeviceSeatAssignmentId")
DO UPDATE SET "SeatId" = EXCLUDED."SeatId",
              "DeviceId" = EXCLUDED."DeviceId",
              "DetachedAtUtc" = NULL;
"@ | docker exec -i afk4-postgres psql -U postgres -d afk4_dev
```

Read the staff-protected persisted floor map:

```powershell
$floorMap = Invoke-RestMethod `
    "$baseUrl/api/branches/$branchId/floor-map" `
    -Headers $staffHeaders
```

Start a guest session on the assigned seat with a stable idempotency key:

```powershell
$startSessionBody = @{
    organizationId = $organizationId
    seatId = $seatId
    durationMinutes = 60
    tariffRuleVersionId = 'manual-v1'
    idempotencyKey = 'smoke-start-001'
} | ConvertTo-Json -Depth 6

$startedSession = Invoke-RestMethod `
    "$baseUrl/api/branches/$branchId/sessions/start" `
    -Method Post `
    -Headers $staffHeaders `
    -ContentType 'application/json' `
    -Body $startSessionBody
```

Repeat the same start request and confirm the idempotent response returns the
same `sessionId`:

```powershell
$repeatedStartSession = Invoke-RestMethod `
    "$baseUrl/api/branches/$branchId/sessions/start" `
    -Method Post `
    -Headers $staffHeaders `
    -ContentType 'application/json' `
    -Body $startSessionBody

if ($startedSession.session.sessionId -ne $repeatedStartSession.session.sessionId) {
    throw 'Repeated start did not return the original sessionId.'
}
```

Extend the session with a second idempotency key:

```powershell
$sessionId = $startedSession.session.sessionId
$extendSessionBody = @{
    additionalMinutes = 15
    tariffRuleVersionId = 'manual-v1'
    idempotencyKey = 'smoke-extend-001'
} | ConvertTo-Json -Depth 6

$extendedSession = Invoke-RestMethod `
    "$baseUrl/api/sessions/$sessionId/extend" `
    -Method Post `
    -Headers $staffHeaders `
    -ContentType 'application/json' `
    -Body $extendSessionBody

$activeLease = $extendedSession.session.currentLease
```

Report reconciliation while the cloud session is active. A matching local lease
should return `continue`:

```powershell
$reconciliationBody = @{
    organizationId = $organizationId
    branchId = $branchId
    deviceId = $enrollment.deviceId
    activeSessionId = $sessionId
    activeLease = $activeLease
    isLocked = $false
    pendingLocalEventCount = 0
    observedAtUtc = (Get-Date).ToUniversalTime().ToString('O')
} | ConvertTo-Json -Depth 12

$activeReconciliation = Invoke-RestMethod `
    "$baseUrl/api/devices/$($enrollment.deviceId)/session-reconciliation" `
    -Method Post `
    -Headers @{ 'X-AFK4-Device-Credential' = $enrollment.credentialSecret } `
    -ContentType 'application/json' `
    -Body $reconciliationBody
```

If the smoke setup seeds a second enrolled device and assigned seat, transfer
the session by posting to `POST /api/sessions/{sessionId}/transfer` with:

```json
{
  "targetSeatId": "<second-seat-id>",
  "idempotencyKey": "smoke-transfer-001"
}
```

End the session and then reconcile the still-local lease. Because the cloud
session is ending, reconciliation should return `lock`:

```powershell
$endSessionBody = @{
    reason = 'local-postgres-smoke'
    idempotencyKey = 'smoke-end-001'
} | ConvertTo-Json -Depth 6

$endingSession = Invoke-RestMethod `
    "$baseUrl/api/sessions/$sessionId/end" `
    -Method Post `
    -Headers $staffHeaders `
    -ContentType 'application/json' `
    -Body $endSessionBody

$endingReconciliation = Invoke-RestMethod `
    "$baseUrl/api/devices/$($enrollment.deviceId)/session-reconciliation" `
    -Method Post `
    -Headers @{ 'X-AFK4-Device-Credential' = $enrollment.credentialSecret } `
    -ContentType 'application/json' `
    -Body $reconciliationBody
```

Post an authenticated installed apps report from the device:

```powershell
$installedAppsBody = @{
    organizationId = $organizationId
    branchId = $branchId
    deviceId = $enrollment.deviceId
    reportedAtUtc = (Get-Date).ToUniversalTime().ToString('O')
    apps = @(
        @{
            displayName = 'Counter-Strike 2'
            version = '2.0.0'
            publisher = 'Valve'
            installLocation = 'C:\Games\Counter-Strike 2'
            installedAtUtc = '2026-05-01T08:30:00Z'
        },
        @{
            displayName = 'Discord'
            version = '1.0.9059'
            publisher = 'Discord Inc.'
            installLocation = $null
            installedAtUtc = $null
        }
    )
} | ConvertTo-Json -Depth 8

Invoke-RestMethod `
    "$baseUrl/api/devices/$($enrollment.deviceId)/installed-apps/report" `
    -Method Post `
    -Headers @{ 'X-AFK4-Device-Credential' = $enrollment.credentialSecret } `
    -ContentType 'application/json' `
    -Body $installedAppsBody
```

Read the staff-protected device detail projection:

```powershell
$deviceDetail = Invoke-RestMethod `
    "$baseUrl/api/devices/$($enrollment.deviceId)" `
    -Headers $staffHeaders
```

Create a device command and read its persisted status:

```powershell
$commandBody = @{
    type = 'lock'
    payload = @{
        reason = 'local-postgres-smoke'
    }
} | ConvertTo-Json -Depth 4

$command = Invoke-RestMethod `
    "$baseUrl/api/devices/$($enrollment.deviceId)/commands" `
    -Method Post `
    -Headers $staffHeaders `
    -ContentType 'application/json' `
    -Body $commandBody

$status = Invoke-RestMethod `
    "$baseUrl/api/devices/$($enrollment.deviceId)/commands/$($command.commandId)/status" `
    -Headers $staffHeaders
```

Rotate the device credential, then verify the new secret can authenticate a
heartbeat:

```powershell
$rotatedCredential = Invoke-RestMethod `
    "$baseUrl/api/devices/$($enrollment.deviceId)/credentials/rotate" `
    -Method Post `
    -Headers $staffHeaders

$rotatedHeartbeat = Invoke-RestMethod `
    "$baseUrl/api/devices/$($enrollment.deviceId)/heartbeat" `
    -Method Post `
    -Headers @{ 'X-AFK4-Device-Credential' = $rotatedCredential.credentialSecret } `
    -ContentType 'application/json' `
    -Body $heartbeatBody
```

Revoke the rotated credential:

```powershell
$revokedCredential = Invoke-RestMethod `
    "$baseUrl/api/devices/$($enrollment.deviceId)/credentials/$($rotatedCredential.credentialId)/revoke" `
    -Method Post `
    -Headers $staffHeaders
```

Expected results:

- health returns `status = ok`;
- staff sign-in returns non-empty `accessToken` and `refreshToken`, and
  includes `devices.enrollment_codes.create`, `devices.credentials.rotate`,
  `devices.credentials.revoke`, `sessions.start`, `sessions.extend`,
  `sessions.transfer`, and `sessions.end` in `permissions`;
- refresh returns a new non-empty `accessToken` and `refreshToken`;
- enrollment returns non-empty `deviceId`, `credentialId`, and
  `credentialSecret`;
- heartbeat returns `heartbeatIntervalSeconds = 10`;
- floor map returns the seeded `PC-SMOKE-001` seat with `zoneName = Main Hall`
  and the enrolled `deviceId`;
- session start returns an active session with a non-empty signed
  `currentLease`;
- repeated session start with `smoke-start-001` returns the same `sessionId`;
- session extend returns a refreshed signed lease;
- active reconciliation returns `action = continue`;
- session end returns `state = ending`;
- ending reconciliation returns `action = lock`;
- installed apps report returns no content and persists two app snapshot rows;
- device detail returns `machineName = PC-SMOKE-001`, assigned seat
  `PC-SMOKE-001`, `activeCredentialCount = 1`, and `installedAppCount = 2`;
- command status returns `status = Pending` and `type = lock`.
- rotation returns a new non-empty `credentialId` and `credentialSecret`;
- heartbeat with the rotated credential returns `heartbeatIntervalSeconds = 10`;
- revocation returns the rotated `credentialId` and a non-empty
  `revokedAtUtc`.

Optionally inspect recent audit records for the protected staff actions:

```powershell
@'
SELECT "Action", "Outcome", "TargetId"
FROM audit_records
WHERE "Action" IN (
    'devices.enrollment_codes.create',
    'devices.commands.dispatch',
    'devices.commands.status.view',
    'devices.credentials.rotate',
    'devices.credentials.revoke',
    'sessions.start',
    'sessions.extend',
    'sessions.transfer',
    'sessions.end')
ORDER BY "CreatedAtUtc" DESC
LIMIT 5;
'@ | docker exec -i afk4-postgres psql -U postgres -d afk4_dev
```

Expected:

```text
devices.credentials.revoke       | Succeeded | ...
devices.credentials.rotate       | Succeeded | ...
devices.commands.status.view     | Succeeded | ...
devices.commands.dispatch        | Succeeded | ...
sessions.end                     | Succeeded | ...
sessions.extend                  | Succeeded | ...
sessions.start                   | Succeeded | ...
devices.enrollment_codes.create  | Succeeded | AFK4-...
```

Optionally inspect the Phase 4 session rows:

```powershell
@"
SELECT "SessionId", "State", "SeatId", "DeviceId", "TariffRuleVersionId"
FROM sessions
WHERE "SessionId" = '$sessionId';

SELECT "SessionId", "Sequence", "ExpiresAtUtc", length("Signature") AS "SignatureLength"
FROM session_leases
WHERE "SessionId" = '$sessionId'
ORDER BY "Sequence";

SELECT "EventType", "DeviceId"
FROM session_events
WHERE "SessionId" = '$sessionId'
ORDER BY "CreatedAtUtc";

SELECT "Operation", "ExpiresAtUtc"
FROM session_command_idempotency
WHERE "BranchId" = '$branchId'
ORDER BY "CreatedAtUtc";

SELECT "Type", "Status", "PayloadJson"
FROM device_commands
WHERE "DeviceId" = '$($enrollment.deviceId)'
ORDER BY "CreatedAtUtc";
"@ | docker exec -i afk4-postgres psql -U postgres -d afk4_dev
```

Expected:

```text
sessions: one row with state ending after the end request
session_leases: at least two signed lease rows with increasing Sequence values
session_events: session-started, session-extended, device-reconciled, session-ending
session_command_idempotency: start, extend, and end rows
device_commands: unlock, refresh-session-lease, and lock commands
```

Optionally inspect the Phase 3 layout and installed app rows:

```powershell
@"
SELECT z."Name" AS "ZoneName",
       s."Name" AS "SeatName",
       d."DeviceId",
       d."MachineName",
       d."IsOnline",
       d."IsLocked"
FROM seats s
JOIN zones z ON z."ZoneId" = s."ZoneId"
LEFT JOIN device_seat_assignments a
  ON a."SeatId" = s."SeatId"
 AND a."DetachedAtUtc" IS NULL
LEFT JOIN devices d ON d."DeviceId" = a."DeviceId"
WHERE s."SeatId" = '$seatId';
"@ | docker exec -i afk4-postgres psql -U postgres -d afk4_dev

@"
SELECT "DisplayName", "Version", "Publisher"
FROM device_installed_apps
WHERE "DeviceId" = '$($enrollment.deviceId)'
ORDER BY "DisplayName";
"@ | docker exec -i afk4-postgres psql -U postgres -d afk4_dev
```

Expected:

```text
Main Hall | PC-SMOKE-001 | ... | PC-SMOKE-001 | t | t
Counter-Strike 2 | 2.0.0    | Valve
Discord          | 1.0.9059 | Discord Inc.
```

Optionally inspect credential revocation state:

```powershell
@"
SELECT "CredentialId", "RevokedAtUtc"
FROM device_credentials
WHERE "DeviceId" = '$($enrollment.deviceId)'
ORDER BY "CreatedAtUtc";
"@ | docker exec -i afk4-postgres psql -U postgres -d afk4_dev
```

## Stop Local Services

Stop the API process with `Ctrl+C`.

Stop PostgreSQL while keeping data:

```powershell
docker compose down
```

Delete local PostgreSQL data:

```powershell
docker compose down -v
```
