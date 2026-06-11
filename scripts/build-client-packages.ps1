param(
    [Parameter(Mandatory = $true)]
    [string] $Version,

    [ValidateSet('internal', 'beta', 'stable')]
    [string] $Channel = 'internal',

    [string] $Configuration = 'Release',

    [string] $Runtime = 'win-x64',

    [string] $DotnetPath = 'C:\Program Files\dotnet\dotnet.exe',

    [string] $BunPath = 'bun',

    [switch] $SkipOperatorWebRestore,

    [switch] $IncludeLegacyGamingPcPackage,

    [switch] $BuildLegacyStagingBootstrapper,

    [string] $StagingLeasePublicKeyPath = '',

    [string] $StagingUpdateSigningPublicKeyPath = ''
)

$ErrorActionPreference = 'Stop'
$includeLegacyGamingPcPackage = $IncludeLegacyGamingPcPackage.IsPresent -or $BuildLegacyStagingBootstrapper.IsPresent
$buildLegacyStagingBootstrapper = $BuildLegacyStagingBootstrapper.IsPresent

# The Setup Wizard's first (pre-enroll) discovery/enroll call is build-pinned per channel.
# internal/beta stay on staging; stable points at the production platform origin. The URL is
# injected into AFK4.SetupWizard.Core via -p:AFK4PlatformBaseUrl below (see SetupWizardDefaults).
$platformBaseUrlByChannel = @{
    'internal' = 'https://afk4.staging.mubi.dev'
    'beta' = 'https://afk4.staging.mubi.dev'
    'stable' = 'https://app.afk4.net'
}
$platformBaseUrl = $platformBaseUrlByChannel[$Channel]
if ([string]::IsNullOrWhiteSpace($platformBaseUrl)) {
    throw "No platform base URL is configured for channel '$Channel'."
}

# Pinned .NET 10 Desktop Runtime (x64) carried inside the Burn bundle. Bump version+url+sha
# together when moving to a newer servicing release; recompute the SHA via
# (Get-FileHash -Algorithm SHA512 -LiteralPath <runtime.exe>).Hash
$runtimeVersion = '10.0.9'
$runtimeUrl = 'https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/10.0.9/windowsdesktop-runtime-10.0.9-win-x64.exe'
$runtimeSha512 = '99BC2215D67F8AEA1ECB3DF642423CCABF76E5261B225F0F2D78123D84D58E64923F050A5DC58405C4D5CF074ACBAD32C4A1021A67E94629DCC57206AC4116DE'

function Get-VerifiedRuntimeInstaller {
    param(
        [Parameter(Mandatory = $true)] [string] $CacheDir,
        [Parameter(Mandatory = $true)] [string] $Version,
        [Parameter(Mandatory = $true)] [string] $Url,
        [Parameter(Mandatory = $true)] [string] $ExpectedSha512
    )

    New-Item -ItemType Directory -Force -Path $CacheDir | Out-Null
    $target = Join-Path $CacheDir "windowsdesktop-runtime-$Version-win-x64.exe"

    if (Test-Path -LiteralPath $target) {
        $existingHash = (Get-FileHash -Algorithm SHA512 -LiteralPath $target).Hash
        if ($existingHash -eq $ExpectedSha512) {
            return $target
        }
        Remove-Item -LiteralPath $target -Force
    }

    Write-Host "Downloading .NET Desktop Runtime $Version for the bundle payload..."
    Invoke-WebRequest -Uri $Url -OutFile $target

    $actualHash = (Get-FileHash -Algorithm SHA512 -LiteralPath $target).Hash
    if ($actualHash -ne $ExpectedSha512) {
        Remove-Item -LiteralPath $target -Force
        throw "Runtime installer SHA-512 mismatch: expected $ExpectedSha512 but downloaded $actualHash."
    }

    return $target
}

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

function Get-MsiFileNames {
    param(
        [Parameter(Mandatory = $true)]
        [string] $MsiPath
    )

    $installer = New-Object -ComObject WindowsInstaller.Installer
    $database = $installer.OpenDatabase($MsiPath, 0)
    $view = $database.OpenView('SELECT `FileName` FROM `File`')
    $fileNames = @()

    try {
        $view.Execute()
        while ($record = $view.Fetch()) {
            $fileNames += $record.StringData(1)
        }
    }
    finally {
        $view.Close()
    }

    return $fileNames
}

function Assert-OperatorMsiContainsFrontendAssets {
    param(
        [Parameter(Mandatory = $true)]
        [string] $MsiPath
    )

    $fileNames = Get-MsiFileNames -MsiPath $MsiPath
    $requiredAssets = @(
        @{ Label = 'frontend index.html'; Pattern = '*index.html*' },
        @{ Label = 'frontend JavaScript bundle'; Pattern = '*.js*' },
        @{ Label = 'frontend stylesheet'; Pattern = '*.css*' }
    )

    foreach ($asset in $requiredAssets) {
        if (-not ($fileNames | Where-Object { $_ -like $asset.Pattern } | Select-Object -First 1)) {
            throw "Operator App MSI does not contain $($asset.Label). Build the React frontend and copy dist into WebAssets before WiX packaging."
        }
    }
}

if (-not (Test-Path -LiteralPath $DotnetPath)) {
    throw "dotnet executable was not found at '$DotnetPath'."
}

if (-not (Get-Command $BunPath -ErrorAction SilentlyContinue)) {
    throw "bun executable was not found on PATH (looked for '$BunPath')."
}

if (-not $buildLegacyStagingBootstrapper -and
    (-not [string]::IsNullOrWhiteSpace($StagingLeasePublicKeyPath) -or
        -not [string]::IsNullOrWhiteSpace($StagingUpdateSigningPublicKeyPath))) {
    throw "Staging lease/update signing public key paths are only used when BuildLegacyStagingBootstrapper is set."
}

if ($buildLegacyStagingBootstrapper -and [string]::IsNullOrWhiteSpace($StagingLeasePublicKeyPath)) {
    throw "StagingLeasePublicKeyPath is required when BuildLegacyStagingBootstrapper is set."
}

if ($buildLegacyStagingBootstrapper -and [string]::IsNullOrWhiteSpace($StagingUpdateSigningPublicKeyPath)) {
    throw "StagingUpdateSigningPublicKeyPath is required when BuildLegacyStagingBootstrapper is set."
}

if ($buildLegacyStagingBootstrapper -and -not (Test-Path -LiteralPath $StagingLeasePublicKeyPath)) {
    throw "Staging lease public key file was not found at '$StagingLeasePublicKeyPath'."
}

if ($buildLegacyStagingBootstrapper -and -not (Test-Path -LiteralPath $StagingUpdateSigningPublicKeyPath)) {
    throw "Staging update signing public key file was not found at '$StagingUpdateSigningPublicKeyPath'."
}

$resolvedStagingLeasePublicKeyPath = ''
if ($buildLegacyStagingBootstrapper) {
    $resolvedStagingLeasePublicKeyPath = (Resolve-Path -LiteralPath $StagingLeasePublicKeyPath).Path
}

$resolvedStagingUpdateSigningPublicKeyPath = ''
if ($buildLegacyStagingBootstrapper) {
    $resolvedStagingUpdateSigningPublicKeyPath = (Resolve-Path -LiteralPath $StagingUpdateSigningPublicKeyPath).Path
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $repoRoot 'artifacts/client-packages'
$publishRoot = Join-Path $artifactRoot 'publish'
$wixInputRoot = Join-Path $artifactRoot 'wix-inputs'
$operatorWebRoot = Join-Path $repoRoot 'src/AFK4.Operator.App.Web'
$operatorWebDist = Join-Path $operatorWebRoot 'dist'
$operatorWebDistIndex = Join-Path $operatorWebDist 'index.html'
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

if ($SkipOperatorWebRestore) {
    Write-Host "Skipping Operator App frontend dependency restore because SkipOperatorWebRestore was set."
}
else {
    # Dependencies are managed at the Bun workspace root (single bun.lock), so install from repoRoot.
    Push-Location $repoRoot
    try {
        & $BunPath install --frozen-lockfile

        if ($LASTEXITCODE -ne 0) {
            throw "bun install failed for the web workspace with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
}

Push-Location $operatorWebRoot
try {
    & $BunPath run build

    if ($LASTEXITCODE -ne 0) {
        throw "bun run build failed for Operator App frontend with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

if (-not (Test-Path -LiteralPath $operatorWebDistIndex)) {
    throw "Operator App frontend build did not produce '$operatorWebDistIndex'."
}

# The dev-only host-bridge stub (src/devHostBridge.ts) must never reach a production bundle.
# Fail the build if any dev marker leaked into the built output — a hard gate against the
# stub (hardcoded staging org GUID + /api/auth/staff/sign-in) shipping in the MSI. NB:
# 'browser-dev' is intentionally NOT a marker — operatorConfig.ts ships it as the legit
# fallback-config runtime label; these markers are unique to the stub.
$operatorDevMarkers = @(
    'DEV-ONLY',
    '/api/auth/staff/sign-in',
    '0169044b-2f74-46a7-8e52-7656a39a8f8c')
$operatorBuiltFiles = Get-ChildItem -LiteralPath $operatorWebDist -Recurse -File -Include '*.html', '*.js'
foreach ($builtFile in $operatorBuiltFiles) {
    $builtContent = Get-Content -LiteralPath $builtFile.FullName -Raw
    foreach ($marker in $operatorDevMarkers) {
        if ($builtContent -and $builtContent.Contains($marker)) {
            throw "Operator App build output '$($builtFile.FullName)' contains dev-only marker '$marker'. The dev host-bridge stub must not ship; ensure it stays gated behind import.meta.env.DEV."
        }
    }
}

# Player.Shell.Web dist is linked by AFK4.Player.Shell.csproj (Content Include ...\dist\**),
# so it must exist before publish. Build it here (the script historically built only operator web).
$playerShellWebRoot = Join-Path $repoRoot 'src/AFK4.Player.Shell.Web'
$playerShellWebDistIndex = Join-Path $playerShellWebRoot 'dist/index.html'
Push-Location $playerShellWebRoot
try {
    & $BunPath run build

    if ($LASTEXITCODE -ne 0) {
        throw "bun run build failed for Player Shell frontend with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

if (-not (Test-Path -LiteralPath $playerShellWebDistIndex)) {
    throw "Player Shell frontend build did not produce '$playerShellWebDistIndex'."
}

# SetupWizard.Web dist is NOT linked by the csproj; AFK4.SetupWizard ships WebAssets\** instead.
# Build it and copy the dist into WebAssets, otherwise the committed placeholder index.html
# ships and the wizard shows a "Setup Wizard frontend assets were not found" error page.
$setupWizardWebRoot = Join-Path $repoRoot 'src/AFK4.SetupWizard.Web'
$setupWizardWebDist = Join-Path $setupWizardWebRoot 'dist'
$setupWizardWebAssets = Join-Path $repoRoot 'src/AFK4.SetupWizard/WebAssets'
Push-Location $setupWizardWebRoot
try {
    & $BunPath run build

    if ($LASTEXITCODE -ne 0) {
        throw "bun run build failed for Setup Wizard frontend with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

if (-not (Test-Path -LiteralPath (Join-Path $setupWizardWebDist 'index.html'))) {
    throw "Setup Wizard frontend build did not produce an index.html under '$setupWizardWebDist'."
}

if (Test-Path -LiteralPath $setupWizardWebAssets) {
    Get-ChildItem -LiteralPath $setupWizardWebAssets -Force |
        Remove-Item -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $setupWizardWebAssets | Out-Null
Get-ChildItem -LiteralPath $setupWizardWebDist -Force |
    Copy-Item -Destination $setupWizardWebAssets -Recurse -Force

$projects = @(
    @{ Name = 'operator-app'; Path = 'src/AFK4.Operator.App/AFK4.Operator.App.csproj'; SelfContained = $false },
    @{ Name = 'agent-service'; Path = 'src/AFK4.Agent.Service/AFK4.Agent.Service.csproj'; SelfContained = $false },
    @{ Name = 'player-shell'; Path = 'src/AFK4.Player.Shell/AFK4.Player.Shell.csproj'; SelfContained = $false },
    @{ Name = 'setup-wizard'; Path = 'src/AFK4.SetupWizard/AFK4.SetupWizard.csproj'; SelfContained = $false }
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
        -p:UseSharedCompilation=false `
        -p:AFK4PlatformBaseUrl="$platformBaseUrl"

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for '$($project.Name)' with exit code $LASTEXITCODE."
    }
}

$operatorAppPublishDir = Join-Path $publishRoot "operator-app-$Version-$Channel"
$operatorWebAssetsPublishDir = Join-Path $operatorAppPublishDir 'WebAssets'
$operatorAppPublishDirFullPath = [System.IO.Path]::GetFullPath($operatorAppPublishDir)
$operatorWebAssetsPublishDirFullPath = [System.IO.Path]::GetFullPath($operatorWebAssetsPublishDir)

if (-not $operatorWebAssetsPublishDirFullPath.StartsWith($operatorAppPublishDirFullPath, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Computed Operator App WebAssets directory must stay under '$operatorAppPublishDirFullPath'."
}

if (Test-Path -LiteralPath $operatorWebAssetsPublishDir) {
    Remove-Item -LiteralPath $operatorWebAssetsPublishDir -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $operatorWebAssetsPublishDir | Out-Null
Get-ChildItem -LiteralPath $operatorWebDist -Force |
    Copy-Item -Destination $operatorWebAssetsPublishDir -Recurse -Force

$agentServicePublishDir = Join-Path $publishRoot "agent-service-$Version-$Channel"
$setupWizardPublishDir = Join-Path $publishRoot "setup-wizard-$Version-$Channel"
$agentServiceSupportDir = Join-Path $wixInputRoot 'agent-service-support'
$setupWizardSupportDir = Join-Path $wixInputRoot 'setup-wizard-support'
$updateHelperDir = Join-Path $wixInputRoot 'update-helpers'

New-Item -ItemType Directory -Force -Path $agentServiceSupportDir | Out-Null
New-Item -ItemType Directory -Force -Path $setupWizardSupportDir | Out-Null
New-Item -ItemType Directory -Force -Path $updateHelperDir | Out-Null

Get-ChildItem -LiteralPath $agentServicePublishDir -File |
    Where-Object { $_.Name -ne 'AFK4.Agent.Service.exe' } |
    Copy-Item -Destination $agentServiceSupportDir -Force

$operatorMsiPath = Join-Path $artifactRoot "afk4-operator-app-$Version-$Channel.msi"
$agentMsiPath = Join-Path $artifactRoot "afk4-agent-$Version-$Channel.msi"
$playerShellMsiPath = Join-Path $artifactRoot "afk4-player-shell-$Version-$Channel.msi"
$gamingPcMsiPath = Join-Path $artifactRoot "afk4-gaming-pc-$Version-$Channel.msi"
$legacySetupArtifactPath = Join-Path $artifactRoot "afk4-gaming-pc-setup-$Version-$Channel.exe"

@($operatorMsiPath, $agentMsiPath, $playerShellMsiPath, $gamingPcMsiPath, $legacySetupArtifactPath) |
    Where-Object { Test-Path -LiteralPath $_ } |
    ForEach-Object { Remove-Item -LiteralPath $_ -Force }

# The Burn bundle needs the Bal (WixStandardBootstrapperApplication) and Netfx
# (DotNetCoreSearch) extensions. `wix extension add` is idempotent.
& $DotnetPath wix extension add -g WixToolset.Bal.wixext
& $DotnetPath wix extension add -g WixToolset.Netfx.wixext
if ($LASTEXITCODE -ne 0) {
    throw "Adding WiX extensions failed with exit code $LASTEXITCODE."
}

# Build the Player Shell MSI first: the agent MSI bundles it into the wizard payload
# (so the wizard can install the Player Shell on gaming PCs), which means it must exist
# before the setup-wizard support dir is harvested.
& $DotnetPath wix build -acceptEula wix7 (Join-Path $repoRoot 'installers/player-shell/Package.wxs') `
    -arch x64 `
    -d "PackageVersion=$msiVersion" `
    -d "PlayerShellPublishDir=$(Join-Path $publishRoot "player-shell-$Version-$Channel")" `
    -o $playerShellMsiPath

if ($LASTEXITCODE -ne 0) {
    throw "WiX build failed for Player Shell MSI with exit code $LASTEXITCODE."
}

# Build the Operator App MSI next: the agent MSI also bundles it into the wizard payload so the
# wizard can install the Operator App on cashier/manager workstations (role manager_workstation),
# the same way it installs the Player Shell on gaming PCs. So it must exist before the harvest too.
& $DotnetPath wix build -acceptEula wix7 (Join-Path $repoRoot 'installers/operator-app/Package.wxs') `
    -arch x64 `
    -d "PackageVersion=$msiVersion" `
    -d "OperatorAppPublishDir=$(Join-Path $publishRoot "operator-app-$Version-$Channel")" `
    -o $operatorMsiPath

if ($LASTEXITCODE -ne 0) {
    throw "WiX build failed for Operator App MSI with exit code $LASTEXITCODE."
}

Assert-OperatorMsiContainsFrontendAssets -MsiPath $operatorMsiPath

# Bundle BOTH role apps into the wizard payload before the support dir is harvested.
$setupWizardPayloadDir = Join-Path $setupWizardPublishDir 'payload'
New-Item -ItemType Directory -Force -Path $setupWizardPayloadDir | Out-Null
Copy-Item -LiteralPath $playerShellMsiPath -Destination (Join-Path $setupWizardPayloadDir 'AFK4.Player.Shell.msi') -Force
Copy-Item -LiteralPath $operatorMsiPath -Destination (Join-Path $setupWizardPayloadDir 'AFK4.Operator.App.msi') -Force

# -Recurse (not -File) so the WebAssets\** subfolder ships in the support dir;
# the agent MSI harvests SetupWizardFiles from here, and the wizard resolves its
# UI from WebAssets next to the exe. Files-only copy dropped it -> placeholder page.
Get-ChildItem -LiteralPath $setupWizardPublishDir -Force |
    Where-Object { $_.Name -ne 'AFK4.SetupWizard.exe' } |
    Copy-Item -Destination $setupWizardSupportDir -Recurse -Force

$updateHelperScripts = @(
    'install-afk4-update-msi.ps1',
    'rollback-afk4-update-msi.ps1',
    'restart-afk4-agent-service.ps1'
)

foreach ($helperScript in $updateHelperScripts) {
    Copy-Item -LiteralPath (Join-Path $repoRoot "scripts/$helperScript") -Destination $updateHelperDir -Force
}

& $DotnetPath wix build -acceptEula wix7 (Join-Path $repoRoot 'installers/agent/Package.wxs') `
    -arch x64 `
    -d "PackageVersion=$msiVersion" `
    -d "AgentServicePublishDir=$agentServicePublishDir" `
    -d "AgentServiceSupportDir=$agentServiceSupportDir" `
    -d "SetupWizardPublishDir=$setupWizardPublishDir" `
    -d "SetupWizardSupportDir=$setupWizardSupportDir" `
    -d "UpdateHelperDir=$updateHelperDir" `
    -o $agentMsiPath

if ($LASTEXITCODE -ne 0) {
    throw "WiX build failed for single Agent MSI with exit code $LASTEXITCODE."
}

$agentFiles = Get-MsiFileNames -MsiPath $agentMsiPath
if (-not ($agentFiles | Where-Object { $_ -like '*AFK4.Player.Shell.msi*' } | Select-Object -First 1)) {
    throw "Agent MSI does not contain the bundled Player Shell MSI (payload\AFK4.Player.Shell.msi)."
}
if (-not ($agentFiles | Where-Object { $_ -like '*AFK4.Operator.App.msi*' } | Select-Object -First 1)) {
    throw "Agent MSI does not contain the bundled Operator App MSI (payload\AFK4.Operator.App.msi)."
}

# Components publish framework-dependent (one shared .NET runtime, carried by the Burn
# bundle — see installers/bundle/Bundle.wxs). The agent MSI is a build input to that bundle;
# it must be installed on a machine where the Desktop Runtime is already present (the bundle
# guarantees that ordering). The agent MSI still auto-launches the Setup Wizard on interactive install.

# Carry the runtime and chain the agent MSI into a single master installer .exe.
$runtimeCacheDir = Join-Path $artifactRoot 'runtime-cache'
$runtimeInstallerPath = Get-VerifiedRuntimeInstaller `
    -CacheDir $runtimeCacheDir -Version $runtimeVersion -Url $runtimeUrl -ExpectedSha512 $runtimeSha512

$clientBundlePath = Join-Path $artifactRoot "afk4-client-$Version-$Channel.exe"
if (Test-Path -LiteralPath $clientBundlePath) {
    Remove-Item -LiteralPath $clientBundlePath -Force
}

& $DotnetPath wix build -acceptEula wix7 (Join-Path $repoRoot 'installers/bundle/Bundle.wxs') `
    -ext WixToolset.Bal.wixext `
    -ext WixToolset.Netfx.wixext `
    -arch x64 `
    -d "PackageVersion=$msiVersion" `
    -d "RuntimeInstallerPath=$runtimeInstallerPath" `
    -d "AgentMsiPath=$agentMsiPath" `
    -o $clientBundlePath

if ($LASTEXITCODE -ne 0) {
    throw "WiX build failed for the client master installer (Burn bundle) with exit code $LASTEXITCODE."
}

if ($includeLegacyGamingPcPackage) {
    & $DotnetPath wix build -acceptEula wix7 (Join-Path $repoRoot 'installers/gaming-pc/Package.wxs') `
        -arch x64 `
        -d "PackageVersion=$msiVersion" `
        -d "AgentServicePublishDir=$agentServicePublishDir" `
        -d "AgentServiceSupportDir=$agentServiceSupportDir" `
        -d "PlayerShellPublishDir=$(Join-Path $publishRoot "player-shell-$Version-$Channel")" `
        -d "UpdateHelperDir=$updateHelperDir" `
        -o $gamingPcMsiPath

    if ($LASTEXITCODE -ne 0) {
        throw "WiX build failed for legacy gaming-PC MSI with exit code $LASTEXITCODE."
    }
}

if ($buildLegacyStagingBootstrapper) {
    $setupPublishDir = Join-Path $publishRoot "gaming-pc-setup-$Version-$Channel"
    $setupArtifactPath = $legacySetupArtifactPath

    if (Test-Path -LiteralPath $setupPublishDir) {
        Remove-Item -LiteralPath $setupPublishDir -Recurse -Force
    }

    & $DotnetPath publish (Join-Path $repoRoot 'src/AFK4.GamingPc.Setup/AFK4.GamingPc.Setup.csproj') `
        -c $Configuration `
        -r $Runtime `
        --self-contained true `
        -o $setupPublishDir `
        -p:GamingPcMsiPath="$gamingPcMsiPath" `
        -p:StagingLeasePublicKeyPath="$resolvedStagingLeasePublicKeyPath" `
        -p:StagingUpdateSigningPublicKeyPath="$resolvedStagingUpdateSigningPublicKeyPath" `
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

# The operator + player-shell MSIs are only inputs now — they ship bundled inside the agent MSI
# payload, not on their own. Move them (and their .wixpdb) into an intermediates\ subfolder so the
# deliverable folder presents a single file (the agent MSI) and nobody hands a client the wrong one.
$intermediatesDir = Join-Path $artifactRoot 'intermediates'
New-Item -ItemType Directory -Force -Path $intermediatesDir | Out-Null
foreach ($bundledMsi in @($operatorMsiPath, $playerShellMsiPath)) {
    foreach ($artifact in @($bundledMsi, [System.IO.Path]::ChangeExtension($bundledMsi, '.wixpdb'))) {
        if (Test-Path -LiteralPath $artifact) {
            Move-Item -LiteralPath $artifact -Destination $intermediatesDir -Force
        }
    }
}

# The agent MSI is now a build input to the bundle (the bundle embeds it), so move it to
# intermediates too — the single deliverable in the package folder is the bundle .exe.
foreach ($artifact in @($agentMsiPath, [System.IO.Path]::ChangeExtension($agentMsiPath, '.wixpdb'))) {
    if (Test-Path -LiteralPath $artifact) {
        Move-Item -LiteralPath $agentMsiPath -Destination $intermediatesDir -Force
    }
}

Write-Host "Published client package inputs under $publishRoot"
Write-Host "Deliverable master installer (install this one):"
Write-Host $clientBundlePath
Write-Host "Bundled inputs (agent/operator/player-shell MSIs) moved to: $intermediatesDir"

if ($includeLegacyGamingPcPackage) {
    Write-Host "Legacy gaming-PC MSI artifact:"
    Write-Host $gamingPcMsiPath
}

if ($buildLegacyStagingBootstrapper) {
    Write-Host "Legacy setup artifact:"
    Write-Host $legacySetupArtifactPath
}
