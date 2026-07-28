param(
    [Parameter(Mandatory = $true)]
    [uri] $PlatformBaseUrl,

    [Parameter(Mandatory = $true)]
    [guid] $BranchId,

    [Parameter(Mandatory = $true)]
    [guid] $OrganizationId,

    [string[]] $RequestPath,

    [string] $RequestDirectory,

    [string] $AccessToken,

    [string] $AccessTokenEnvVar,

    [switch] $CreateRollouts,

    [ValidateSet('agent-service', 'player-shell', 'organization-admin')]
    [string[]] $RolloutComponent = @('agent-service'),

    [ValidateSet('device', 'branch')]
    [string] $RolloutTargetKind = 'device',

    [guid[]] $RolloutTargetDeviceId,

    [ValidateRange(1, 100)]
    [int] $RolloutBatchPercent = 100,

    [datetimeoffset] $RolloutStartsAtUtc = [datetimeoffset]::UtcNow,

    [string] $RolloutReason = 'Automated client package rollout.'
)

$ErrorActionPreference = 'Stop'

function Resolve-RequestFiles {
    param(
        [string[]] $ExplicitRequestPaths,
        [string] $DirectoryPath
    )

    $explicitPaths = @()
    if ($null -ne $ExplicitRequestPaths) {
        $explicitPaths = @($ExplicitRequestPaths | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    }

    if ($explicitPaths.Count -gt 0 -and -not [string]::IsNullOrWhiteSpace($DirectoryPath)) {
        throw "Specify RequestPath or RequestDirectory, not both."
    }

    if ($explicitPaths.Count -gt 0) {
        $resolved = @()
        foreach ($path in $explicitPaths) {
            if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
                throw "RequestPath '$path' was not found."
            }

            if ([System.IO.Path]::GetFileName($path) -notlike '*-request.json') {
                throw "RequestPath must reference *-request.json files."
            }

            $resolved += (Resolve-Path -LiteralPath $path).Path
        }

        return $resolved
    }

    if ([string]::IsNullOrWhiteSpace($DirectoryPath)) {
        throw "Specify RequestPath or RequestDirectory."
    }

    if (-not (Test-Path -LiteralPath $DirectoryPath -PathType Container)) {
        throw "RequestDirectory '$DirectoryPath' was not found."
    }

    $files = @(Get-ChildItem -LiteralPath $DirectoryPath -Filter '*-request.json' -File | Sort-Object Name)
    if ($files.Count -eq 0) {
        throw "RequestDirectory '$DirectoryPath' did not contain *-request.json files."
    }

    return @($files | ForEach-Object { $_.FullName })
}

function Get-JsonPropertyValue {
    param(
        [object] $Object,
        [string] $PropertyName
    )

    if ($null -eq $Object) {
        return $null
    }

    $property = $Object.PSObject.Properties |
        Where-Object { [string]::Equals($_.Name, $PropertyName, [System.StringComparison]::OrdinalIgnoreCase) } |
        Select-Object -First 1

    return $property.Value
}

if ($null -eq $PlatformBaseUrl -or -not $PlatformBaseUrl.IsAbsoluteUri) {
    throw "PlatformBaseUrl must be an absolute URI."
}

if ($PlatformBaseUrl.Scheme -ne 'http' -and $PlatformBaseUrl.Scheme -ne 'https') {
    throw "PlatformBaseUrl must use http or https scheme."
}

$hasDirectToken = -not [string]::IsNullOrWhiteSpace($AccessToken)
$hasTokenEnvVar = -not [string]::IsNullOrWhiteSpace($AccessTokenEnvVar)
if ($hasDirectToken -eq $hasTokenEnvVar) {
    throw "Specify exactly one access token source: AccessToken or AccessTokenEnvVar."
}

if ($hasTokenEnvVar) {
    $AccessToken = [Environment]::GetEnvironmentVariable($AccessTokenEnvVar)
    if ([string]::IsNullOrWhiteSpace($AccessToken)) {
        throw "Environment variable '$AccessTokenEnvVar' must contain the update package registration access token."
    }
}

if ($CreateRollouts) {
    if ([string]::IsNullOrWhiteSpace($RolloutReason)) {
        throw "RolloutReason is required when CreateRollouts is set."
    }

    if ($RolloutTargetKind -eq 'device' -and ($null -eq $RolloutTargetDeviceId -or $RolloutTargetDeviceId.Count -eq 0)) {
        throw "RolloutTargetDeviceId is required when CreateRollouts is set with RolloutTargetKind=device."
    }
}

$requestFiles = Resolve-RequestFiles $RequestPath $RequestDirectory
$baseUri = $PlatformBaseUrl.AbsoluteUri.TrimEnd('/')
$organizationPrefix = "$baseUri/api/organizations/$($OrganizationId.ToString('D'))"
$registrationUri = "$organizationPrefix/branches/$($BranchId.ToString('D'))/updates/packages"
$rolloutsUri = "$organizationPrefix/branches/$($BranchId.ToString('D'))/updates/rollouts"
$headers = @{
    Authorization = "Bearer $AccessToken"
    'X-AFK4-Product' = 'organization-admin'
    'X-AFK4-Compatibility-Epoch' = '2'
    'X-AFK4-Client-Version' = '0.2.0-update-registration'
}
$rolloutComponents = @($RolloutComponent | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })

foreach ($requestFile in $requestFiles) {
    $body = Get-Content -LiteralPath $requestFile -Raw
    $request = $body | ConvertFrom-Json
    $component = [string](Get-JsonPropertyValue $request 'component')
    $requestOrganizationId = [string](Get-JsonPropertyValue $request 'organizationId')
    $channel = [string](Get-JsonPropertyValue $request 'channel')
    $registration = Invoke-RestMethod -Method Post -Uri $registrationUri -Headers $headers -ContentType 'application/json' -Body $body
    Write-Host "Registered update package request: $requestFile"

    if ($CreateRollouts -and $rolloutComponents -contains $component) {
        $updatePackageId = [string](Get-JsonPropertyValue $registration 'updatePackageId')
        if ([string]::IsNullOrWhiteSpace($updatePackageId)) {
            throw "Platform registration response did not include updatePackageId for '$requestFile'."
        }

        $targetDeviceIds = @()
        if ($RolloutTargetKind -eq 'device') {
            $targetDeviceIds = @($RolloutTargetDeviceId |
                Where-Object { $null -ne $_ } |
                ForEach-Object { $_.ToString('D') })
        }

        $rolloutBody = @{
            organizationId = $requestOrganizationId
            updatePackageId = $updatePackageId
            channel = $channel
            targetKind = $RolloutTargetKind
            targetDeviceIds = $targetDeviceIds
            batchPercent = $RolloutBatchPercent
            startsAtUtc = $RolloutStartsAtUtc.ToUniversalTime().ToString('O')
            reason = $RolloutReason
        } | ConvertTo-Json -Depth 5

        $rollout = Invoke-RestMethod -Method Post -Uri $rolloutsUri -Headers $headers -ContentType 'application/json' -Body $rolloutBody
        $rolloutId = [string](Get-JsonPropertyValue $rollout 'updateRolloutId')
        Write-Host "Created update rollout for component '$component': $rolloutId"
    }
}
