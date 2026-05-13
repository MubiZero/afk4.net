# Local PostgreSQL And Device Smoke Runbook

This runbook verifies the current AFK4 identity, audit, layout, and device
persistence slice against a real local PostgreSQL database. It covers EF
migration application, staff sign-in, authorized device enrollment-code
creation, device enrollment, authenticated heartbeat persistence, persisted
floor-map reads, installed app reporting, device detail reads, command status
storage, and staff-protected device credential rotation/revocation.

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

Seed a local technician staff user for the smoke run. The seeded password is
`Passw0rd!` and the password hash is for local development only:

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
    'Smoke Technician',
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
    'technician')
ON CONFLICT ("StaffUserId", "OrganizationId", "BranchId", "RoleName")
DO NOTHING;
"@

$seedSql | docker exec -i afk4-postgres psql -U postgres -d afk4_dev
```

Sign in as the local technician:

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
  and `devices.credentials.revoke` in `permissions`;
- refresh returns a new non-empty `accessToken` and `refreshToken`;
- enrollment returns non-empty `deviceId`, `credentialId`, and
  `credentialSecret`;
- heartbeat returns `heartbeatIntervalSeconds = 10`;
- floor map returns the seeded `PC-SMOKE-001` seat with `zoneName = Main Hall`
  and the enrolled `deviceId`;
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
    'devices.credentials.revoke')
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
devices.enrollment_codes.create  | Succeeded | AFK4-...
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
