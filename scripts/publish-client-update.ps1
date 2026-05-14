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

    [Parameter(Mandatory = $true)]
    [string] $HostingRoot,

    [Parameter(Mandatory = $true)]
    [uri] $PublicBaseUri,

    [Parameter(Mandatory = $true)]
    [string] $SigningKeyPath,

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

& $DotnetPath run --project (Join-Path $repoRoot 'src/AFK4.Update.Publisher/AFK4.Update.Publisher.csproj') -- `
    --organization-id $OrganizationId `
    --component $Component `
    --version $Version `
    --channel $Channel `
    --artifact $artifactPath `
    --hosting-root $HostingRoot `
    --public-base-uri $PublicBaseUri.AbsoluteUri `
    --signing-key $SigningKeyPath `
    --release-notes $ReleaseNotes `
    --output $requestPath

Write-Host "Artifact: $artifactPath"
Write-Host "CreateUpdatePackageRequest: $requestPath"
