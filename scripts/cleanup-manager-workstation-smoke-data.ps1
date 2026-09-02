<#
.SYNOPSIS
Removes mistaken manager-workstation smoke device assignments from staging.

.DESCRIPTION
The script uses the public staff API only:
- POST /api/devices/{deviceId}/remove to revoke credentials and detach seats.
- DELETE /api/organizations/{organizationId}/branches/{branchId}/layout/seats/{seatId} to remove empty smoke
  seats when explicitly requested.

It runs as a dry run by default. Pass -Apply to mutate staging data, and pass
-DeleteEmptySmokeSeats to delete empty smoke seats after device cleanup.
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = "",

    [Guid]$OrganizationId = [Guid]::Empty,

    [Guid]$BranchId = [Guid]::Empty,

    [string]$StaffUserName = "",

    [string]$StaffPassword = "",

    [Guid[]]$DeviceId = @(),

    [Guid[]]$SeatId = @(),

    [Guid[]]$ProtectedSeatId = @([Guid]"9f3adbd3-957e-4dc8-8d34-a6bfa56b9275"),

    [string]$DeviceNamePattern = "",

    [string]$SeatNamePattern = "(?i)(smoke|manager|workstation|operator|front.?desk)",

    [switch]$DeleteEmptySmokeSeats,

    [switch]$Apply,

    [switch]$ForceNonStaging,

    [switch]$ShowHelp,

    [ValidateRange(1, 600)]
    [int]$RequestTimeoutSeconds = 60
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = "Stop"

$DefaultBaseUrl = "https://api.afk4.net"
$DefaultOrganizationId = [Guid]"0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"
$DefaultBranchId = [Guid]"acfc0212-967f-4d84-94be-9003387b09c2"
$DefaultStaffUserName = "real-device-smoke@afk4.test"

function Show-Usage {
    @"
Usage:
  `$env:AFK4_STAGING_STAFF_PASSWORD = '<smoke staff password>'

  # Inspect candidates only.
  .\scripts\cleanup-manager-workstation-smoke-data.ps1

  # Remove manager_workstation devices that still have floor-map seats.
  .\scripts\cleanup-manager-workstation-smoke-data.ps1 -Apply

  # Also delete empty smoke seats after device removal.
  .\scripts\cleanup-manager-workstation-smoke-data.ps1 -Apply -DeleteEmptySmokeSeats

Useful overrides:
  -BaseUrl https://api.afk4.net
  -OrganizationId 0c04d6c0-bfa8-4e26-9263-fc0d307d0f08
  -BranchId acfc0212-967f-4d84-94be-9003387b09c2
  -StaffUserName real-device-smoke@afk4.test
  -DeviceId <guid>              # remove only explicit devices
  -SeatId <guid>                # delete only explicit seats
  -DeviceNamePattern '<regex>'  # optional extra filter for discovered devices
  -SeatNamePattern '<regex>'    # default: smoke/manager/operator/front desk

Environment fallbacks:
  AFK4_STAGING_BASE_URL
  AFK4_STAGING_ORGANIZATION_ID or AFK4_ORGANIZATION_ADMIN_ORGANIZATION_ID
  AFK4_STAGING_BRANCH_ID or AFK4_ORGANIZATION_ADMIN_BRANCH_ID
  AFK4_STAGING_STAFF_USERNAME
  AFK4_STAGING_STAFF_PASSWORD or AFK4_SMOKE_STAFF_PASSWORD
"@ | Write-Host
}

function Get-FirstEnvironmentValue {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Names
    )

    foreach ($name in $Names) {
        $value = [Environment]::GetEnvironmentVariable($name)
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            return $value
        }
    }

    return $null
}

function Resolve-GuidValue {
    param(
        [Parameter(Mandatory = $true)]
        [Guid]$Value,

        [Parameter(Mandatory = $true)]
        [string[]]$EnvironmentNames,

        [Parameter(Mandatory = $true)]
        [Guid]$DefaultValue,

        [Parameter(Mandatory = $true)]
        [string]$DisplayName
    )

    if ($Value -ne [Guid]::Empty) {
        return $Value
    }

    $environmentValue = Get-FirstEnvironmentValue -Names $EnvironmentNames
    if ([string]::IsNullOrWhiteSpace($environmentValue)) {
        return $DefaultValue
    }

    $parsed = [Guid]::Empty
    if ([Guid]::TryParse($environmentValue, [ref]$parsed)) {
        return $parsed
    }

    throw "$DisplayName environment value must be a GUID."
}

function ConvertTo-GuidText {
    param(
        [object]$Value
    )

    if ($null -eq $Value -or [string]::IsNullOrWhiteSpace([string]$Value)) {
        return ""
    }

    return ([Guid]([string]$Value)).ToString("D")
}

function New-GuidLookup {
    param(
        [Guid[]]$Values
    )

    $lookup = @{}
    foreach ($value in @($Values)) {
        if ($value -ne [Guid]::Empty) {
            $lookup[$value.ToString("D")] = $true
        }
    }

    return $lookup
}

function Invoke-Afk4Api {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Method,

        [Parameter(Mandatory = $true)]
        [string]$Path,

        [object]$Body = $null,

        [hashtable]$Headers = @{}
    )

    $uri = "$($script:ResolvedBaseUrl)$Path"
    $requestPath = $null
    $responsePath = Join-Path $env:TEMP ("afk4-cleanup-response-{0}.json" -f [Guid]::NewGuid().ToString("N"))

    try {
        $curlArguments = @(
            "-sS",
            "--max-time", $RequestTimeoutSeconds.ToString(),
            "-X", $Method.ToUpperInvariant(),
            "-H", "Accept: application/json",
            "-o", $responsePath,
            "-w", "%{http_code}"
        )

        foreach ($headerName in $Headers.Keys) {
            $curlArguments += @("-H", "$($headerName): $($Headers[$headerName])")
        }

        if ($null -ne $Body) {
            $requestPath = Join-Path $env:TEMP ("afk4-cleanup-request-{0}.json" -f [Guid]::NewGuid().ToString("N"))
            $requestJson = $Body | ConvertTo-Json -Depth 20 -Compress
            [System.IO.File]::WriteAllText(
                $requestPath,
                $requestJson,
                [System.Text.UTF8Encoding]::new($false))

            $curlArguments += @(
                "-H", "Content-Type: application/json",
                "--data-binary", "@$requestPath"
            )
        }

        $curlArguments += $uri
        $statusText = & $script:CurlPath @curlArguments
        $exitCode = $LASTEXITCODE
        $responseText = if (Test-Path -LiteralPath $responsePath) {
            [System.IO.File]::ReadAllText($responsePath)
        }
        else {
            ""
        }

        if ($exitCode -ne 0) {
            throw "AFK4 API request failed: curl exited with code $exitCode for $Method $Path."
        }

        $statusCode = [int]$statusText
        if ($statusCode -lt 200 -or $statusCode -gt 299) {
            throw "AFK4 API request failed: $Method $Path returned HTTP $statusCode. Body: $responseText"
        }

        if ([string]::IsNullOrWhiteSpace($responseText)) {
            return $null
        }

        return $responseText | ConvertFrom-Json
    }
    finally {
        if ($null -ne $requestPath) {
            Remove-Item -LiteralPath $requestPath -Force -ErrorAction SilentlyContinue
        }

        Remove-Item -LiteralPath $responsePath -Force -ErrorAction SilentlyContinue
    }
}

function Get-CandidateDevices {
    param(
        [object[]]$Devices,

        [hashtable]$ExplicitDeviceLookup
    )

    foreach ($device in @($Devices)) {
        $deviceIdText = ConvertTo-GuidText $device.deviceId
        $isExplicit = $ExplicitDeviceLookup.ContainsKey($deviceIdText)
        $hasSeat = -not [string]::IsNullOrWhiteSpace([string]$device.seatId)
        $isManagerWithSeat = ([string]$device.role) -eq "manager_workstation" -and $hasSeat
        $matchText = "$($device.machineName) $($device.displayName) $($device.seatName)"
        $matchesName = [string]::IsNullOrWhiteSpace($DeviceNamePattern) -or $matchText -match $DeviceNamePattern

        if ($isExplicit -or ($ExplicitDeviceLookup.Count -eq 0 -and $isManagerWithSeat -and $matchesName)) {
            [pscustomobject]@{
                DeviceId = $deviceIdText
                MachineName = [string]$device.machineName
                DisplayName = [string]$device.displayName
                Role = [string]$device.role
                EnrollmentState = [string]$device.enrollmentState
                SeatId = ConvertTo-GuidText $device.seatId
                SeatName = [string]$device.seatName
                ActiveCredentialCount = [int]$device.activeCredentialCount
            }
        }
    }
}

function Get-CandidateSeats {
    param(
        [object[]]$Seats,

        [object[]]$Devices,

        [hashtable]$ExplicitSeatLookup,

        [hashtable]$ProtectedSeatLookup
    )

    $activeSeatLookup = @{}
    foreach ($device in @($Devices)) {
        $activeSeatIdText = ConvertTo-GuidText $device.seatId
        if (-not [string]::IsNullOrWhiteSpace($activeSeatIdText)) {
            $activeSeatLookup[$activeSeatIdText] = $true
        }
    }

    foreach ($seat in @($Seats)) {
        $seatIdText = ConvertTo-GuidText $seat.seatId
        if ($ProtectedSeatLookup.ContainsKey($seatIdText)) {
            continue
        }

        $isExplicit = $ExplicitSeatLookup.ContainsKey($seatIdText)
        $matchesName = -not [string]::IsNullOrWhiteSpace($SeatNamePattern) -and ([string]$seat.seatName) -match $SeatNamePattern

        if ($ExplicitSeatLookup.Count -gt 0) {
            if (-not $isExplicit) {
                continue
            }
        }
        elseif (-not $matchesName) {
            continue
        }

        $hasFloorMapDevice = -not [string]::IsNullOrWhiteSpace([string]$seat.deviceId)
        $hasInventoryDevice = $activeSeatLookup.ContainsKey($seatIdText)
        $hasActiveSession = -not [string]::IsNullOrWhiteSpace([string]$seat.activeSessionId)
        $isEmpty = -not $hasFloorMapDevice -and -not $hasInventoryDevice -and -not $hasActiveSession
        $skipReason = ""

        if (-not $isEmpty) {
            if ($hasActiveSession) {
                $skipReason = "active session"
            }
            elseif ($hasFloorMapDevice -or $hasInventoryDevice) {
                $skipReason = "active device assignment"
            }
            else {
                $skipReason = "not empty"
            }
        }

        [pscustomobject]@{
            SeatId = $seatIdText
            Name = [string]$seat.seatName
            Zone = [string]$seat.zoneName
            IsEmpty = $isEmpty
            SkipReason = $skipReason
        }
    }
}

function Show-DeviceTable {
    param(
        [object[]]$Devices
    )

    if ($Devices.Count -eq 0) {
        Write-Host "No manager_workstation devices with floor-map seat assignments matched."
        return
    }

    $table = $Devices |
        Select-Object DeviceId, MachineName, DisplayName, Role, EnrollmentState, SeatName, SeatId, ActiveCredentialCount |
        Format-Table -AutoSize |
        Out-String
    Write-Host $table
}

function Show-SeatTable {
    param(
        [object[]]$Seats
    )

    if ($Seats.Count -eq 0) {
        Write-Host "No smoke seats matched the current filters."
        return
    }

    $table = $Seats |
        Select-Object SeatId, Name, Zone, IsEmpty, SkipReason |
        Format-Table -AutoSize |
        Out-String
    Write-Host $table
}

if ($ShowHelp) {
    Show-Usage
    return
}

if ($null -eq $DeviceId) {
    $DeviceId = @()
}

if ($null -eq $SeatId) {
    $SeatId = @()
}

if ($null -eq $ProtectedSeatId) {
    $ProtectedSeatId = @()
}

if ([string]::IsNullOrWhiteSpace($BaseUrl)) {
    $BaseUrl = Get-FirstEnvironmentValue -Names @("AFK4_STAGING_BASE_URL", "AFK4_PLATFORM_API")
}

if ([string]::IsNullOrWhiteSpace($BaseUrl)) {
    $BaseUrl = $DefaultBaseUrl
}

$script:ResolvedBaseUrl = $BaseUrl.TrimEnd("/")
$OrganizationId = Resolve-GuidValue `
    -Value $OrganizationId `
    -EnvironmentNames @("AFK4_STAGING_ORGANIZATION_ID", "AFK4_ORGANIZATION_ADMIN_ORGANIZATION_ID") `
    -DefaultValue $DefaultOrganizationId `
    -DisplayName "OrganizationId"
$BranchId = Resolve-GuidValue `
    -Value $BranchId `
    -EnvironmentNames @("AFK4_STAGING_BRANCH_ID", "AFK4_ORGANIZATION_ADMIN_BRANCH_ID") `
    -DefaultValue $DefaultBranchId `
    -DisplayName "BranchId"

if ([string]::IsNullOrWhiteSpace($StaffUserName)) {
    $StaffUserName = Get-FirstEnvironmentValue -Names @("AFK4_STAGING_STAFF_USERNAME")
}

if ([string]::IsNullOrWhiteSpace($StaffUserName)) {
    $StaffUserName = $DefaultStaffUserName
}

if ([string]::IsNullOrWhiteSpace($StaffPassword)) {
    $StaffPassword = Get-FirstEnvironmentValue -Names @("AFK4_STAGING_STAFF_PASSWORD", "AFK4_SMOKE_STAFF_PASSWORD")
}

if ([string]::IsNullOrWhiteSpace($StaffPassword)) {
    throw "Set AFK4_STAGING_STAFF_PASSWORD or AFK4_SMOKE_STAFF_PASSWORD, or pass -StaffPassword."
}

if ($script:ResolvedBaseUrl -notmatch "(?i)(staging|localhost|127\.0\.0\.1|\[::1\])" -and -not $ForceNonStaging) {
    throw "Refusing to run against non-staging URL '$script:ResolvedBaseUrl'. Pass -ForceNonStaging only for an intentional non-staging cleanup."
}

$script:CurlPath = (Get-Command curl.exe -ErrorAction Stop).Source
$explicitDeviceLookup = New-GuidLookup -Values $DeviceId
$explicitSeatLookup = New-GuidLookup -Values $SeatId
$protectedSeatLookup = New-GuidLookup -Values $ProtectedSeatId
$mode = if ($Apply) { "APPLY" } else { "DRY RUN" }

Write-Host "Target API: $script:ResolvedBaseUrl"
Write-Host "Organization: $($OrganizationId.ToString("D"))"
Write-Host "Branch: $($BranchId.ToString("D"))"
Write-Host "Mode: $mode"

Invoke-Afk4Api -Method Get -Path "/api/health" | Out-Null

$signIn = Invoke-Afk4Api `
    -Method Post `
    -Path "/api/organizations/$($OrganizationId.ToString("D"))/auth/staff/sign-in" `
    -Headers @{
        'X-AFK4-Product' = 'organization-admin'
        'X-AFK4-Compatibility-Epoch' = '2'
        'X-AFK4-Client-Version' = '0.2.0-cleanup-smoke'
    } `
    -Body @{
        organizationId = $OrganizationId.ToString("D")
        userName = $StaffUserName
        password = $StaffPassword
    }

$headers = @{
    Authorization = "Bearer $($signIn.accessToken)"
    'X-AFK4-Product' = 'organization-admin'
    'X-AFK4-Compatibility-Epoch' = '2'
    'X-AFK4-Client-Version' = '0.2.0-cleanup-smoke'
}

$organizationPrefix = "/api/organizations/$($OrganizationId.ToString("D"))"
$devices = @(Invoke-Afk4Api -Method Get -Path "$organizationPrefix/branches/$($BranchId.ToString("D"))/devices" -Headers $headers)
$candidateDevices = @(Get-CandidateDevices -Devices $devices -ExplicitDeviceLookup $explicitDeviceLookup)

Write-Host ""
Write-Host "Candidate devices"
Show-DeviceTable -Devices $candidateDevices

if ($candidateDevices.Count -gt 0 -and $Apply) {
    foreach ($device in $candidateDevices) {
        Write-Host "Removing device $($device.DeviceId) ($($device.DisplayName))..."
        Invoke-Afk4Api `
            -Method Post `
            -Path "/api/devices/$($device.DeviceId)/remove" `
            -Headers $headers `
            -Body @{
                organizationId = $OrganizationId.ToString("D")
                reason = "Manager workstation smoke cleanup"
            } | Out-Null
    }
}
elseif ($candidateDevices.Count -gt 0) {
    Write-Host "Dry run: re-run with -Apply to remove these devices and detach their seat assignments."
}

$devicesAfterDeviceCleanup = @(Invoke-Afk4Api -Method Get -Path "$organizationPrefix/branches/$($BranchId.ToString("D"))/devices" -Headers $headers)
$floorMap = Invoke-Afk4Api -Method Get -Path "$organizationPrefix/branches/$($BranchId.ToString("D"))/floor-map" -Headers $headers
$candidateSeats = @(Get-CandidateSeats `
    -Seats @($floorMap.seats) `
    -Devices $devicesAfterDeviceCleanup `
    -ExplicitSeatLookup $explicitSeatLookup `
    -ProtectedSeatLookup $protectedSeatLookup)

Write-Host ""
Write-Host "Candidate smoke seats"
Show-SeatTable -Seats $candidateSeats

$deletableSeats = @($candidateSeats | Where-Object { $_.IsEmpty })
if ($DeleteEmptySmokeSeats -and $deletableSeats.Count -gt 0 -and $Apply) {
    $failedSeatDeletes = 0
    foreach ($seat in $deletableSeats) {
        try {
            Write-Host "Deleting empty seat $($seat.SeatId) ($($seat.Name))..."
            Invoke-Afk4Api `
                -Method Delete `
                -Path "$organizationPrefix/branches/$($BranchId.ToString("D"))/layout/seats/$($seat.SeatId)?organizationId=$($OrganizationId.ToString("D"))" `
                -Headers $headers | Out-Null
        }
        catch {
            $failedSeatDeletes++
            Write-Warning $_.Exception.Message
        }
    }

    if ($failedSeatDeletes -gt 0) {
        throw "$failedSeatDeletes seat delete request(s) failed."
    }
}
elseif ($DeleteEmptySmokeSeats -and $deletableSeats.Count -gt 0) {
    Write-Host "Dry run: re-run with -Apply -DeleteEmptySmokeSeats to delete the empty seats above."
}
elseif (-not $DeleteEmptySmokeSeats -and $deletableSeats.Count -gt 0) {
    Write-Host "Empty smoke seats matched. Pass -DeleteEmptySmokeSeats with -Apply to delete them."
}

$finalDevices = @(Invoke-Afk4Api -Method Get -Path "$organizationPrefix/branches/$($BranchId.ToString("D"))/devices" -Headers $headers)
$finalFloorMap = Invoke-Afk4Api -Method Get -Path "$organizationPrefix/branches/$($BranchId.ToString("D"))/floor-map" -Headers $headers
$remainingDevices = @(Get-CandidateDevices -Devices $finalDevices -ExplicitDeviceLookup $explicitDeviceLookup)
$remainingSeats = @(Get-CandidateSeats `
    -Seats @($finalFloorMap.seats) `
    -Devices $finalDevices `
    -ExplicitSeatLookup $explicitSeatLookup `
    -ProtectedSeatLookup $protectedSeatLookup)

Write-Host ""
Write-Host "Verification"
Write-Host "Remaining candidate devices: $($remainingDevices.Count)"
Write-Host "Remaining matching smoke seats: $($remainingSeats.Count)"

if (-not $Apply) {
    Write-Host "No changes were made."
}
