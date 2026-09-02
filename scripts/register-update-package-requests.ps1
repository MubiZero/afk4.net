<#
.SYNOPSIS
Регистрирует собранные пакеты обновлений на платформе и, по желанию, создаёт раскатку.

.DESCRIPTION
Релиз клиентских приложений принадлежит платформе, а не клубу: пакет заводится один раз на всю
сеть, а раскатка адресуется организациям, филиалам или конкретным устройствам. Поэтому скрипт
ходит в `/api/platform/updates/*` и требует токен администратора платформы с правом
`platform.updates.packages.manage`. Клубный токен сотрудника эти маршруты не пустят.

Вход админа платформы проходит через двухфакторку, поэтому получить токен неинтерактивно нельзя —
это осознанное ограничение, а не недоработка. Скрипт запускается человеком, у которого токен уже
на руках, либо из ручного релизного workflow, где токен лежит в секрете.
#>
param(
    [Parameter(Mandatory = $true)]
    [uri] $PlatformBaseUrl,

    [string[]] $RequestPath,

    [string] $RequestDirectory,

    [string] $AccessToken,

    [string] $AccessTokenEnvVar,

    [switch] $CreateRollouts,

    [ValidateSet('agent-service', 'player-shell', 'organization-admin')]
    [string[]] $RolloutComponent = @('agent-service'),

    [ValidateSet('organization', 'branch', 'device')]
    [string] $RolloutTargetKind = 'device',

    [guid[]] $RolloutOrganizationId,

    [guid[]] $RolloutBranchId,

    [guid[]] $RolloutDeviceId,

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

<# Файл запроса пишет AFK4.Update.Publisher, и в нём есть поле organizationId — наследство клубной
   модели релизов. Платформенный контракт его не знает, поэтому тело собирается по полям явно:
   лишнее не уезжает, а недостающее видно сразу и по имени. #>
function New-PlatformPackageBody {
    param(
        [object] $Request,
        [string] $SourceFile
    )

    $fields = [ordered]@{}
    foreach ($name in @('component', 'version', 'channel', 'artifactUri', 'sha256', 'signature', 'signatureAlgorithm', 'sizeBytes', 'releaseNotes')) {
        $value = Get-JsonPropertyValue $Request $name
        if ($null -eq $value -or ([string]::IsNullOrWhiteSpace([string]$value) -and $name -ne 'sizeBytes')) {
            throw "Update package request '$SourceFile' is missing required field '$name'."
        }

        $fields[$name] = $value
    }

    return $fields | ConvertTo-Json -Depth 5
}

function Invoke-PlatformApi {
    param(
        [string] $Uri,
        [hashtable] $Headers,
        [string] $Body,
        [string] $What
    )

    try {
        return Invoke-RestMethod -Method Post -Uri $Uri -Headers $Headers -ContentType 'application/json' -Body $Body
    }
    catch {
        $status = $null
        $response = $_.Exception.Response
        if ($null -ne $response) {
            $status = [int]$response.StatusCode
        }

        $hint = switch ($status) {
            401 { "The access token is not a platform administrator session. Club staff tokens cannot register releases." }
            403 { "The platform administrator lacks the 'platform.updates.packages.manage' permission." }
            404 { "The platform build at '$PlatformBaseUrl' does not expose $Uri. Check that it is current." }
            default { $null }
        }

        $message = "$What failed"
        if ($null -ne $status) { $message += " with HTTP $status" }
        $message += ": $($_.Exception.Message)"
        if ($null -ne $hint) { $message += " $hint" }

        throw $message
    }
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

$rolloutOrganizationIds = @($RolloutOrganizationId | Where-Object { $null -ne $_ } | ForEach-Object { $_.ToString('D') })
$rolloutBranchIds = @($RolloutBranchId | Where-Object { $null -ne $_ } | ForEach-Object { $_.ToString('D') })
$rolloutDeviceIds = @($RolloutDeviceId | Where-Object { $null -ne $_ } | ForEach-Object { $_.ToString('D') })

if ($CreateRollouts) {
    if ([string]::IsNullOrWhiteSpace($RolloutReason)) {
        throw "RolloutReason is required when CreateRollouts is set."
    }

    $targets = switch ($RolloutTargetKind) {
        'organization' { $rolloutOrganizationIds }
        'branch' { $rolloutBranchIds }
        'device' { $rolloutDeviceIds }
    }

    if ($targets.Count -eq 0) {
        throw "Rollout target kind '$RolloutTargetKind' requires at least one Rollout$([char]::ToUpper($RolloutTargetKind[0]) + $RolloutTargetKind.Substring(1))Id."
    }
}

$requestFiles = Resolve-RequestFiles $RequestPath $RequestDirectory
$baseUri = $PlatformBaseUrl.AbsoluteUri.TrimEnd('/')
$registrationUri = "$baseUri/api/platform/updates/packages"
$rolloutsUri = "$baseUri/api/platform/updates/rollouts"
$headers = @{
    Authorization = "Bearer $AccessToken"
}
$rolloutComponents = @($RolloutComponent | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })

foreach ($requestFile in $requestFiles) {
    $request = Get-Content -LiteralPath $requestFile -Raw | ConvertFrom-Json
    $component = [string](Get-JsonPropertyValue $request 'component')
    $channel = [string](Get-JsonPropertyValue $request 'channel')
    $body = New-PlatformPackageBody -Request $request -SourceFile $requestFile

    $registration = Invoke-PlatformApi -Uri $registrationUri -Headers $headers -Body $body -What "Update package registration for '$requestFile'"
    Write-Host "Registered update package request: $requestFile"

    if ($CreateRollouts -and $rolloutComponents -contains $component) {
        $updatePackageId = [string](Get-JsonPropertyValue $registration 'updatePackageId')
        if ([string]::IsNullOrWhiteSpace($updatePackageId)) {
            throw "Platform registration response did not include updatePackageId for '$requestFile'."
        }

        $rolloutBody = @{
            updatePackageId = $updatePackageId
            channel = $channel
            targetKind = $RolloutTargetKind
            organizationIds = $rolloutOrganizationIds
            branchIds = $rolloutBranchIds
            deviceIds = $rolloutDeviceIds
            batchPercent = $RolloutBatchPercent
            startsAtUtc = $RolloutStartsAtUtc.ToUniversalTime().ToString('O')
            reason = $RolloutReason
        } | ConvertTo-Json -Depth 5

        $rollout = Invoke-PlatformApi -Uri $rolloutsUri -Headers $headers -Body $rolloutBody -What "Rollout creation for component '$component'"
        $rolloutId = [string](Get-JsonPropertyValue $rollout 'updateRolloutId')
        Write-Host "Created update rollout for component '$component': $rolloutId"
    }
}
