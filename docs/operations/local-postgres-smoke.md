# Local PostgreSQL And Device Smoke Runbook

This runbook verifies the current AFK4 device persistence slice against a real
local PostgreSQL database. It covers EF migration application, device
enrollment, authenticated heartbeat persistence, and command status storage.

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

Create an enrollment code:

```powershell
$codeBody = @{
    organizationId = $organizationId
    expiresInSeconds = 300
} | ConvertTo-Json -Depth 4

$code = Invoke-RestMethod `
    "$baseUrl/api/branches/$branchId/device-enrollment-codes" `
    -Method Post `
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
    -ContentType 'application/json' `
    -Body $commandBody

$status = Invoke-RestMethod `
    "$baseUrl/api/devices/$($enrollment.deviceId)/commands/$($command.commandId)/status"
```

Expected results:

- health returns `status = ok`;
- enrollment returns non-empty `deviceId`, `credentialId`, and
  `credentialSecret`;
- heartbeat returns `heartbeatIntervalSeconds = 10`;
- command status returns `status = Pending` and `type = lock`.

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
