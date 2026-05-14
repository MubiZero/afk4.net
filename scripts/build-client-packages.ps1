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

if (-not (Test-Path -LiteralPath $DotnetPath)) {
    throw "dotnet executable was not found at '$DotnetPath'."
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $repoRoot 'artifacts/client-packages'
$publishRoot = Join-Path $artifactRoot 'publish'
$publishRootFullPath = [System.IO.Path]::GetFullPath($publishRoot)
$artifactRootFullPath = [System.IO.Path]::GetFullPath($artifactRoot)

if (-not $publishRootFullPath.StartsWith($artifactRootFullPath, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Computed publish directory must stay under '$artifactRootFullPath'."
}

New-Item -ItemType Directory -Force -Path $publishRoot | Out-Null

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

Write-Host "Published client package inputs under $publishRoot"
Write-Host "WiX MSI build steps will consume these directories in the next task."
