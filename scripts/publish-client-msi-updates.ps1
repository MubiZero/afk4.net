param(
    [Parameter(Mandatory = $true)]
    [string] $Version,

    [Parameter(Mandatory = $true)]
    [ValidateSet('internal', 'beta', 'stable')]
    [string] $Channel,

    [Parameter(Mandatory = $true)]
    [guid] $OrganizationId,

    [string] $PackageDirectory,

    [string] $OutputDirectory,

    [ValidateSet('file-system', 'http-put')]
    [string] $ArtifactStore = 'file-system',

    [string] $HostingRoot,

    [uri] $PublicBaseUri,

    [uri] $OperatorArtifactUploadUri,

    [uri] $OperatorArtifactPublicUri,

    [uri] $GamingPcArtifactUploadUri,

    [uri] $GamingPcArtifactPublicUri,

    [string] $SigningKeyPath,

    [string] $SigningKeyEnvVar,

    [Parameter(Mandatory = $true)]
    [string] $ReleaseNotes,

    [string] $DotnetPath = 'C:\Program Files\dotnet\dotnet.exe'
)

$ErrorActionPreference = 'Stop'

function Require-File {
    param(
        [string] $Path,
        [string] $Description
    )

    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Description '$Path' was not found."
    }

    return (Resolve-Path -LiteralPath $Path).Path
}

function Require-Directory {
    param(
        [string] $Path,
        [string] $Description
    )

    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path -PathType Container)) {
        throw "$Description '$Path' was not found."
    }

    return (Resolve-Path -LiteralPath $Path).Path
}

function Add-SigningKeyArguments {
    param(
        [string[]] $Arguments
    )

    if (-not [string]::IsNullOrWhiteSpace($SigningKeyPath)) {
        return $Arguments + @('--signing-key', (Resolve-Path -LiteralPath $SigningKeyPath).Path)
    }

    return $Arguments + @('--signing-key-env-var', $SigningKeyEnvVar)
}

function Add-ArtifactStoreArguments {
    param(
        [string[]] $Arguments,
        [uri] $ArtifactUploadUri,
        [uri] $ArtifactPublicUri
    )

    if ($ArtifactStore -eq 'file-system') {
        return $Arguments + @(
            '--hosting-root', $HostingRoot,
            '--public-base-uri', $PublicBaseUri.AbsoluteUri)
    }

    return $Arguments + @(
        '--artifact-upload-uri', $ArtifactUploadUri.AbsoluteUri,
        '--artifact-public-uri', $ArtifactPublicUri.AbsoluteUri)
}

function Invoke-UpdatePublisher {
    param(
        [string] $Component,
        [string] $ArtifactPath,
        [string] $RequestPath,
        [uri] $ArtifactUploadUri,
        [uri] $ArtifactPublicUri
    )

    $publisherArgs = @(
        '--organization-id', $OrganizationId.ToString('D'),
        '--component', $Component,
        '--version', $Version,
        '--channel', $Channel,
        '--artifact', $ArtifactPath,
        '--artifact-store', $ArtifactStore,
        '--release-notes', $ReleaseNotes,
        '--output', $RequestPath)

    $publisherArgs = Add-SigningKeyArguments $publisherArgs
    $publisherArgs = Add-ArtifactStoreArguments $publisherArgs $ArtifactUploadUri $ArtifactPublicUri

    & $resolvedDotnetPath run --project $publisherProject -- @publisherArgs
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        throw "AFK4.Update.Publisher failed for component '$Component' with exit code $exitCode."
    }
}

$repoRoot = Split-Path -Parent $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($PackageDirectory)) {
    $PackageDirectory = Join-Path $repoRoot 'artifacts/client-packages'
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot 'artifacts/update-packages'
}

$resolvedDotnetPath = Require-File $DotnetPath 'dotnet executable'
$resolvedPackageDirectory = Require-Directory $PackageDirectory 'Package directory'
$resolvedOutputDirectory = (New-Item -ItemType Directory -Path $OutputDirectory -Force).FullName

$operatorMsi = Require-File (Join-Path $resolvedPackageDirectory "afk4-operator-app-$Version-$Channel.msi") 'Operator App MSI'
$gamingPcMsi = Require-File (Join-Path $resolvedPackageDirectory "afk4-gaming-pc-$Version-$Channel.msi") 'Gaming-PC MSI'

$usingSigningKeyPath = -not [string]::IsNullOrWhiteSpace($SigningKeyPath)
$usingSigningKeyEnvVar = -not [string]::IsNullOrWhiteSpace($SigningKeyEnvVar)
if ($usingSigningKeyPath -eq $usingSigningKeyEnvVar) {
    throw 'Specify exactly one update metadata signing key source: SigningKeyPath or SigningKeyEnvVar.'
}

if ($usingSigningKeyPath) {
    $SigningKeyPath = Require-File $SigningKeyPath 'SigningKeyPath'
}

if ([string]::IsNullOrWhiteSpace($ReleaseNotes)) {
    throw 'ReleaseNotes is required.'
}

if ($ArtifactStore -eq 'file-system') {
    if ([string]::IsNullOrWhiteSpace($HostingRoot)) {
        throw 'HostingRoot is required when ArtifactStore is file-system.'
    }

    if ($null -eq $PublicBaseUri) {
        throw 'PublicBaseUri is required when ArtifactStore is file-system.'
    }
}
else {
    if ($null -eq $OperatorArtifactUploadUri -or $null -eq $OperatorArtifactPublicUri) {
        throw 'OperatorArtifactUploadUri and OperatorArtifactPublicUri are required when ArtifactStore is http-put.'
    }

    if ($null -eq $GamingPcArtifactUploadUri -or $null -eq $GamingPcArtifactPublicUri) {
        throw 'GamingPcArtifactUploadUri and GamingPcArtifactPublicUri are required when ArtifactStore is http-put.'
    }
}

$publisherProject = Join-Path $repoRoot 'src/AFK4.Update.Publisher/AFK4.Update.Publisher.csproj'
$requests = @(
    [pscustomobject]@{
        Component = 'operator-app'
        ArtifactPath = $operatorMsi
        RequestPath = Join-Path $resolvedOutputDirectory "operator-app-$Version-$Channel-request.json"
        ArtifactUploadUri = $OperatorArtifactUploadUri
        ArtifactPublicUri = $OperatorArtifactPublicUri
    },
    [pscustomobject]@{
        Component = 'agent-service'
        ArtifactPath = $gamingPcMsi
        RequestPath = Join-Path $resolvedOutputDirectory "agent-service-$Version-$Channel-request.json"
        ArtifactUploadUri = $GamingPcArtifactUploadUri
        ArtifactPublicUri = $GamingPcArtifactPublicUri
    },
    [pscustomobject]@{
        Component = 'player-shell'
        ArtifactPath = $gamingPcMsi
        RequestPath = Join-Path $resolvedOutputDirectory "player-shell-$Version-$Channel-request.json"
        ArtifactUploadUri = $GamingPcArtifactUploadUri
        ArtifactPublicUri = $GamingPcArtifactPublicUri
    }
)

foreach ($request in $requests) {
    Invoke-UpdatePublisher `
        -Component $request.Component `
        -ArtifactPath $request.ArtifactPath `
        -RequestPath $request.RequestPath `
        -ArtifactUploadUri $request.ArtifactUploadUri `
        -ArtifactPublicUri $request.ArtifactPublicUri
}

foreach ($request in $requests) {
    Write-Host "Generated update package request: $($request.RequestPath)"
}
