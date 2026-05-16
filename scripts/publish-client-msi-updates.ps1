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

function Resolve-OutputDirectory {
    param(
        [string] $Path
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        throw "OutputDirectory is required."
    }

    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        throw "OutputDirectory '$Path' must be a directory."
    }

    return (New-Item -ItemType Directory -Path $Path -Force).FullName
}

function Assert-FilenameSafeVersion {
    param(
        [string] $Value
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw 'Version is required.'
    }

    if ($Value -eq '.' -or $Value -eq '..') {
        throw 'Version must be filename-safe: it cannot contain path separators or invalid filename characters.'
    }

    $invalidCharacters = [System.IO.Path]::GetInvalidFileNameChars()
    if ($Value.IndexOfAny($invalidCharacters) -ge 0) {
        throw 'Version must be filename-safe: it cannot contain path separators or invalid filename characters.'
    }
}

function Assert-PathInsideDirectory {
    param(
        [string] $Root,
        [string] $Path,
        [string] $Description
    )

    $rootFullPath = [System.IO.Path]::GetFullPath($Root)
    $pathFullPath = [System.IO.Path]::GetFullPath($Path)
    $rootWithSeparator = $rootFullPath.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

    if (-not $pathFullPath.StartsWith($rootWithSeparator, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$Description must stay under '$rootFullPath'."
    }

    return $pathFullPath
}

function Require-AbsoluteUri {
    param(
        [uri] $Value,
        [string] $Name,
        [string] $RequiredMessage
    )

    if ($null -eq $Value) {
        throw $RequiredMessage
    }

    if (-not $Value.IsAbsoluteUri) {
        throw "$Name must be an absolute URI."
    }
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

Assert-FilenameSafeVersion $Version

$resolvedDotnetPath = Require-File $DotnetPath 'dotnet executable'
$resolvedPackageDirectory = Require-Directory $PackageDirectory 'Package directory'
$resolvedOutputDirectory = Resolve-OutputDirectory $OutputDirectory

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

    Require-AbsoluteUri $PublicBaseUri 'PublicBaseUri' 'PublicBaseUri is required when ArtifactStore is file-system.'
}
else {
    Require-AbsoluteUri $OperatorArtifactUploadUri 'OperatorArtifactUploadUri' 'OperatorArtifactUploadUri is required when ArtifactStore is http-put.'
    Require-AbsoluteUri $OperatorArtifactPublicUri 'OperatorArtifactPublicUri' 'OperatorArtifactPublicUri is required when ArtifactStore is http-put.'
    Require-AbsoluteUri $GamingPcArtifactUploadUri 'GamingPcArtifactUploadUri' 'GamingPcArtifactUploadUri is required when ArtifactStore is http-put.'
    Require-AbsoluteUri $GamingPcArtifactPublicUri 'GamingPcArtifactPublicUri' 'GamingPcArtifactPublicUri is required when ArtifactStore is http-put.'
}

$publisherProject = Join-Path $repoRoot 'src/AFK4.Update.Publisher/AFK4.Update.Publisher.csproj'
$requests = @(
    [pscustomobject]@{
        Component = 'operator-app'
        ArtifactPath = $operatorMsi
        RequestPath = Assert-PathInsideDirectory $resolvedOutputDirectory (Join-Path $resolvedOutputDirectory "operator-app-$Version-$Channel-request.json") 'Update package request path'
        ArtifactUploadUri = $OperatorArtifactUploadUri
        ArtifactPublicUri = $OperatorArtifactPublicUri
    },
    [pscustomobject]@{
        Component = 'agent-service'
        ArtifactPath = $gamingPcMsi
        RequestPath = Assert-PathInsideDirectory $resolvedOutputDirectory (Join-Path $resolvedOutputDirectory "agent-service-$Version-$Channel-request.json") 'Update package request path'
        ArtifactUploadUri = $GamingPcArtifactUploadUri
        ArtifactPublicUri = $GamingPcArtifactPublicUri
    },
    [pscustomobject]@{
        Component = 'player-shell'
        ArtifactPath = $gamingPcMsi
        RequestPath = Assert-PathInsideDirectory $resolvedOutputDirectory (Join-Path $resolvedOutputDirectory "player-shell-$Version-$Channel-request.json") 'Update package request path'
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
