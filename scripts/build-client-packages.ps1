param(
    [Parameter(Mandatory = $true)]
    [string] $Version,

    [ValidateSet('internal', 'beta', 'stable')]
    [string] $Channel = 'internal',

    [string] $Configuration = 'Release',

    [string] $Runtime = 'win-x64',

    [string] $DotnetPath = 'C:\Program Files\dotnet\dotnet.exe',

    [string] $StagingLeasePublicKeyPath = ''
)

$ErrorActionPreference = 'Stop'

function ConvertTo-MsiVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string] $InputVersion
    )

    $match = [System.Text.RegularExpressions.Regex]::Match(
        $InputVersion,
        '^(?<version>\d+\.\d+\.\d+(?:\.\d+)?)')

    if (-not $match.Success) {
        throw "Version '$InputVersion' must start with a Windows Installer compatible version such as 1.2.3 or 1.2.3.4."
    }

    return $match.Groups['version'].Value
}

if (-not (Test-Path -LiteralPath $DotnetPath)) {
    throw "dotnet executable was not found at '$DotnetPath'."
}

if (-not [string]::IsNullOrWhiteSpace($StagingLeasePublicKeyPath) -and -not (Test-Path -LiteralPath $StagingLeasePublicKeyPath)) {
    throw "Staging lease public key file was not found at '$StagingLeasePublicKeyPath'."
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $repoRoot 'artifacts/client-packages'
$publishRoot = Join-Path $artifactRoot 'publish'
$wixInputRoot = Join-Path $artifactRoot 'wix-inputs'
$msiVersion = ConvertTo-MsiVersion $Version
$publishRootFullPath = [System.IO.Path]::GetFullPath($publishRoot)
$artifactRootFullPath = [System.IO.Path]::GetFullPath($artifactRoot)
$wixInputRootFullPath = [System.IO.Path]::GetFullPath($wixInputRoot)

if (-not $publishRootFullPath.StartsWith($artifactRootFullPath, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Computed publish directory must stay under '$artifactRootFullPath'."
}

if (-not $wixInputRootFullPath.StartsWith($artifactRootFullPath, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Computed WiX input directory must stay under '$artifactRootFullPath'."
}

New-Item -ItemType Directory -Force -Path $publishRoot | Out-Null
if (Test-Path -LiteralPath $wixInputRoot) {
    Remove-Item -LiteralPath $wixInputRoot -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $wixInputRoot | Out-Null

$projects = @(
    @{ Name = 'operator-app'; Path = 'src/AFK4.Operator.App/AFK4.Operator.App.csproj'; SelfContained = $false },
    @{ Name = 'agent-service'; Path = 'src/AFK4.Agent.Service/AFK4.Agent.Service.csproj'; SelfContained = $true },
    @{ Name = 'player-shell'; Path = 'src/AFK4.Player.Shell/AFK4.Player.Shell.csproj'; SelfContained = $true }
)

foreach ($project in $projects) {
    $output = Join-Path $publishRoot "$($project.Name)-$Version-$Channel"
    $outputFullPath = [System.IO.Path]::GetFullPath($output)

    if (-not $outputFullPath.StartsWith($publishRootFullPath, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Computed project publish directory must stay under '$publishRootFullPath'."
    }

    if (Test-Path -LiteralPath $output) {
        Remove-Item -LiteralPath $output -Recurse -Force
    }

    & $DotnetPath publish (Join-Path $repoRoot $project.Path) `
        -c $Configuration `
        -r $Runtime `
        --self-contained $($project.SelfContained.ToString().ToLowerInvariant()) `
        -o $output `
        -p:NuGetAudit=false `
        -p:UseSharedCompilation=false

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for '$($project.Name)' with exit code $LASTEXITCODE."
    }
}

$agentServicePublishDir = Join-Path $publishRoot "agent-service-$Version-$Channel"
$agentServiceSupportDir = Join-Path $wixInputRoot 'agent-service-support'
$updateHelperDir = Join-Path $wixInputRoot 'update-helpers'

New-Item -ItemType Directory -Force -Path $agentServiceSupportDir | Out-Null
New-Item -ItemType Directory -Force -Path $updateHelperDir | Out-Null

Get-ChildItem -LiteralPath $agentServicePublishDir -File |
    Where-Object { $_.Name -ne 'AFK4.Agent.Service.exe' } |
    Copy-Item -Destination $agentServiceSupportDir -Force

$updateHelperScripts = @(
    'install-afk4-update-msi.ps1',
    'rollback-afk4-update-msi.ps1',
    'restart-afk4-agent-service.ps1'
)

foreach ($helperScript in $updateHelperScripts) {
    Copy-Item -LiteralPath (Join-Path $repoRoot "scripts/$helperScript") -Destination $updateHelperDir -Force
}

$operatorMsiPath = Join-Path $artifactRoot "afk4-operator-app-$Version-$Channel.msi"
$gamingPcMsiPath = Join-Path $artifactRoot "afk4-gaming-pc-$Version-$Channel.msi"

& $DotnetPath wix build -acceptEula wix7 (Join-Path $repoRoot 'installers/operator-app/Package.wxs') `
    -d "PackageVersion=$msiVersion" `
    -d "OperatorAppPublishDir=$(Join-Path $publishRoot "operator-app-$Version-$Channel")" `
    -o $operatorMsiPath

if ($LASTEXITCODE -ne 0) {
    throw "WiX build failed for Operator App MSI with exit code $LASTEXITCODE."
}

& $DotnetPath wix build -acceptEula wix7 (Join-Path $repoRoot 'installers/gaming-pc/Package.wxs') `
    -d "PackageVersion=$msiVersion" `
    -d "AgentServicePublishDir=$agentServicePublishDir" `
    -d "AgentServiceSupportDir=$agentServiceSupportDir" `
    -d "PlayerShellPublishDir=$(Join-Path $publishRoot "player-shell-$Version-$Channel")" `
    -d "UpdateHelperDir=$updateHelperDir" `
    -o $gamingPcMsiPath

if ($LASTEXITCODE -ne 0) {
    throw "WiX build failed for gaming-PC MSI with exit code $LASTEXITCODE."
}

if (-not [string]::IsNullOrWhiteSpace($StagingLeasePublicKeyPath)) {
    $setupPublishDir = Join-Path $publishRoot "gaming-pc-setup-$Version-$Channel"
    $setupArtifactPath = Join-Path $artifactRoot "afk4-gaming-pc-setup-$Version-$Channel.exe"

    if (Test-Path -LiteralPath $setupPublishDir) {
        Remove-Item -LiteralPath $setupPublishDir -Recurse -Force
    }

    & $DotnetPath publish (Join-Path $repoRoot 'src/AFK4.GamingPc.Setup/AFK4.GamingPc.Setup.csproj') `
        -c $Configuration `
        -r $Runtime `
        --self-contained true `
        -o $setupPublishDir `
        -p:GamingPcMsiPath="$gamingPcMsiPath" `
        -p:StagingLeasePublicKeyPath="$StagingLeasePublicKeyPath" `
        -p:PublishSingleFile=true `
        -p:SelfContained=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:PublishTrimmed=false `
        -p:NuGetAudit=false `
        -p:UseSharedCompilation=false

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for Gaming PC setup bootstrapper with exit code $LASTEXITCODE."
    }

    Copy-Item -LiteralPath (Join-Path $setupPublishDir 'AFK4.GamingPc.Setup.exe') -Destination $setupArtifactPath -Force
}

Write-Host "Published client package inputs under $publishRoot"
Write-Host "MSI artifacts:"
Write-Host $operatorMsiPath
Write-Host $gamingPcMsiPath

if (-not [string]::IsNullOrWhiteSpace($StagingLeasePublicKeyPath)) {
    Write-Host "Setup artifact:"
    Write-Host (Join-Path $artifactRoot "afk4-gaming-pc-setup-$Version-$Channel.exe")
}
