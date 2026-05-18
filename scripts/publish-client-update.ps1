param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('operator-app', 'agent-service', 'player-shell')]
    [string] $Component,

    [Parameter(Mandatory = $true)]
    [string] $ProjectPath,

    [Parameter(Mandatory = $true)]
    [string] $Version,

    [Parameter(Mandatory = $true)]
    [ValidateSet('internal', 'beta', 'stable')]
    [string] $Channel,

    [Parameter(Mandatory = $true)]
    [guid] $OrganizationId,

    [ValidateSet('file-system', 'http-put', 's3')]
    [string] $ArtifactStore = 'file-system',

    [string] $HostingRoot,

    [uri] $PublicBaseUri,

    [uri] $ArtifactUploadUri,

    [uri] $ArtifactPublicUri,

    [uri] $S3Endpoint,

    [string] $S3Bucket,

    [string] $S3KeyPrefix,

    [string] $S3AccessKeyEnvVar,

    [string] $S3SecretKeyEnvVar,

    [string] $S3Region = 'us-east-1',

    [string] $SigningKeyPath,

    [string] $SigningKeyEnvVar,

    [Parameter(Mandatory = $true)]
    [string] $ReleaseNotes,

    [string] $Configuration = 'Release',

    [string] $Runtime = 'win-x64',

    [string] $DotnetPath = 'C:\Program Files\dotnet\dotnet.exe'
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $DotnetPath)) {
    throw "dotnet executable was not found at '$DotnetPath'."
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $repoRoot 'artifacts/update-packages'
$publishRoot = Join-Path $artifactRoot "$Component-$Version-$Channel-publish"
$artifactPath = Join-Path $artifactRoot "$Component-$Version-$Channel.zip"
$requestPath = Join-Path $artifactRoot "$Component-$Version-$Channel-request.json"
$artifactRootFullPath = [System.IO.Path]::GetFullPath($artifactRoot)
$publishRootFullPath = [System.IO.Path]::GetFullPath($publishRoot)

if (-not $publishRootFullPath.StartsWith($artifactRootFullPath, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Computed publish directory must stay under '$artifactRootFullPath'."
}

New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null
if (Test-Path -LiteralPath $publishRoot) {
    Remove-Item -LiteralPath $publishRoot -Recurse -Force
}

& $DotnetPath publish $ProjectPath `
    -c $Configuration `
    -r $Runtime `
    --self-contained false `
    -o $publishRoot `
    -p:NuGetAudit=false `
    -p:UseSharedCompilation=false

if (Test-Path -LiteralPath $artifactPath) {
    Remove-Item -LiteralPath $artifactPath -Force
}

Compress-Archive -Path (Join-Path $publishRoot '*') -DestinationPath $artifactPath -CompressionLevel Optimal

$publisherArgs = @(
    '--organization-id', $OrganizationId
    '--component', $Component
    '--version', $Version
    '--channel', $Channel
    '--artifact', $artifactPath
    '--artifact-store', $ArtifactStore
    '--release-notes', $ReleaseNotes
    '--output', $requestPath
)

if ($ArtifactStore -eq 'file-system') {
    if ([string]::IsNullOrWhiteSpace($HostingRoot) -or $null -eq $PublicBaseUri) {
        throw "HostingRoot and PublicBaseUri are required when ArtifactStore is 'file-system'."
    }

    $publisherArgs += @('--hosting-root', $HostingRoot)
    $publisherArgs += @('--public-base-uri', $PublicBaseUri.AbsoluteUri)
}
elseif ($ArtifactStore -eq 'http-put') {
    if ($null -eq $ArtifactUploadUri -or $null -eq $ArtifactPublicUri) {
        throw "ArtifactUploadUri and ArtifactPublicUri are required when ArtifactStore is 'http-put'."
    }

    $publisherArgs += @('--artifact-upload-uri', $ArtifactUploadUri.AbsoluteUri)
    $publisherArgs += @('--artifact-public-uri', $ArtifactPublicUri.AbsoluteUri)
}
else {
    if ($null -eq $S3Endpoint -or $null -eq $PublicBaseUri) {
        throw "S3Endpoint and PublicBaseUri are required when ArtifactStore is 's3'."
    }

    if ([string]::IsNullOrWhiteSpace($S3Bucket) -or [string]::IsNullOrWhiteSpace($S3AccessKeyEnvVar) -or [string]::IsNullOrWhiteSpace($S3SecretKeyEnvVar)) {
        throw "S3Bucket, S3AccessKeyEnvVar, and S3SecretKeyEnvVar are required when ArtifactStore is 's3'."
    }

    $publisherArgs += @('--s3-endpoint', $S3Endpoint.AbsoluteUri)
    $publisherArgs += @('--s3-bucket', $S3Bucket)
    $publisherArgs += @('--s3-access-key-env-var', $S3AccessKeyEnvVar)
    $publisherArgs += @('--s3-secret-key-env-var', $S3SecretKeyEnvVar)
    $publisherArgs += @('--s3-region', $S3Region)
    $publisherArgs += @('--public-base-uri', $PublicBaseUri.AbsoluteUri)
    if (-not [string]::IsNullOrWhiteSpace($S3KeyPrefix)) {
        $publisherArgs += @('--s3-key-prefix', $S3KeyPrefix)
    }
}

$hasSigningKeyPath = -not [string]::IsNullOrWhiteSpace($SigningKeyPath)
$hasSigningKeyEnvVar = -not [string]::IsNullOrWhiteSpace($SigningKeyEnvVar)
if ($hasSigningKeyPath -eq $hasSigningKeyEnvVar) {
    throw "Specify exactly one signing key source: SigningKeyPath or SigningKeyEnvVar."
}

if ($hasSigningKeyPath) {
    $publisherArgs += @('--signing-key', $SigningKeyPath)
}
else {
    $publisherArgs += @('--signing-key-env-var', $SigningKeyEnvVar)
}

& $DotnetPath run --project (Join-Path $repoRoot 'src/AFK4.Update.Publisher/AFK4.Update.Publisher.csproj') -- @publisherArgs

Write-Host "Artifact: $artifactPath"
Write-Host "CreateUpdatePackageRequest: $requestPath"
