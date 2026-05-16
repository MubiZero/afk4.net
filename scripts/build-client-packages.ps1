param(
    [Parameter(Mandatory = $true)]
    [string] $Version,

    [ValidateSet('internal', 'beta', 'stable')]
    [string] $Channel = 'internal',

    [string] $Configuration = 'Release',

    [string] $Runtime = 'win-x64',

    [string] $DotnetPath = 'C:\Program Files\dotnet\dotnet.exe'
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
    @{ Name = 'operator-app'; Path = 'src/AFK4.Operator.App/AFK4.Operator.App.csproj' },
    @{ Name = 'agent-service'; Path = 'src/AFK4.Agent.Service/AFK4.Agent.Service.csproj' },
    @{ Name = 'player-shell'; Path = 'src/AFK4.Player.Shell/AFK4.Player.Shell.csproj' }
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
        --self-contained false `
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

& $DotnetPath wix build -acceptEula wix7 (Join-Path $repoRoot 'installers/operator-app/Package.wxs') `
    -d "PackageVersion=$msiVersion" `
    -d "OperatorAppPublishDir=$(Join-Path $publishRoot "operator-app-$Version-$Channel")" `
    -o (Join-Path $artifactRoot "afk4-operator-app-$Version-$Channel.msi")

if ($LASTEXITCODE -ne 0) {
    throw "WiX build failed for Operator App MSI with exit code $LASTEXITCODE."
}

& $DotnetPath wix build -acceptEula wix7 (Join-Path $repoRoot 'installers/gaming-pc/Package.wxs') `
    -d "PackageVersion=$msiVersion" `
    -d "AgentServicePublishDir=$agentServicePublishDir" `
    -d "AgentServiceSupportDir=$agentServiceSupportDir" `
    -d "PlayerShellPublishDir=$(Join-Path $publishRoot "player-shell-$Version-$Channel")" `
    -d "UpdateHelperDir=$updateHelperDir" `
    -o (Join-Path $artifactRoot "afk4-gaming-pc-$Version-$Channel.msi")

if ($LASTEXITCODE -ne 0) {
    throw "WiX build failed for gaming-PC MSI with exit code $LASTEXITCODE."
}

Write-Host "Published client package inputs under $publishRoot"
Write-Host "MSI artifacts:"
Write-Host (Join-Path $artifactRoot "afk4-operator-app-$Version-$Channel.msi")
Write-Host (Join-Path $artifactRoot "afk4-gaming-pc-$Version-$Channel.msi")
