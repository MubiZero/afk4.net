param(
    [Parameter(Mandatory = $true)]
    [string] $Version,

    [ValidateSet('internal', 'beta', 'stable')]
    [string] $Channel = 'internal',

    [string] $Configuration = 'Release',

    [string] $Runtime = 'win-x64',

    [string] $DotnetPath = 'C:\Program Files\dotnet\dotnet.exe',

    [string] $BunPath = 'bun',

    [switch] $SkipOrganizationAdminWebRestore,

    # Адрес API для первого шага мастера вместо адреса канала — для приёмки против локального API
    # (виртуалка ходит к Mac по http://10.0.2.2:5074). Пусто — адрес канала.
    [string] $PlatformBaseUrl = ''
)

$ErrorActionPreference = 'Stop'

# The Setup Wizard's first (pre-enroll) discovery/enroll call is build-pinned per channel.
# internal/beta stay on staging; stable points at the production platform origin. The URL is
# injected into AFK4.SetupWizard.Core via -p:AFK4PlatformBaseUrl below (see SetupWizardDefaults).
$platformBaseUrlByChannel = @{
    'internal' = 'https://api.afk4.net'
    'beta' = 'https://api.afk4.net'
    'stable' = 'https://app.afk4.net'
}
$platformBaseUrl = if ($PlatformBaseUrl) { $PlatformBaseUrl } else { $platformBaseUrlByChannel[$Channel] }
if ([string]::IsNullOrWhiteSpace($platformBaseUrl)) {
    throw "No platform base URL is configured for channel '$Channel'."
}

# Official Microsoft Edge WebView2 Evergreen Standalone Installer (x64, ~210 MB) carried inside the
# Burn bundle: the club PC gets WebView2 without internet and without anyone installing it by hand.
# Stable fwlink from the "Evergreen Standalone Installer" table of the WebView2 download page
# (https://developer.microsoft.com/microsoft-edge/webview2/); it redirects to
# MicrosoftEdgeWebView2RuntimeInstallerX64.exe. The link is evergreen — a different file every time
# — so there is nothing to pin a SHA to; trust rests on the Microsoft Authenticode signature,
# verified below. (The ~2 MB bootstrapper, fwlink LinkId=2124703, is only a link for people who
# install a component MSI on its own: it downloads the runtime itself, so the bundle does not carry it.)
$webView2InstallerUrl = 'https://go.microsoft.com/fwlink/?linkid=2124701'

function Assert-MicrosoftSignature {
    param(
        [Parameter(Mandatory = $true)] [string] $Path
    )

    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    if ($signature.Status -ne 'Valid') {
        throw "WebView2 installer signature is not valid: $($signature.Status)."
    }
    if ($signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') {
        throw "WebView2 installer is signed by '$($signature.SignerCertificate.Subject)', not Microsoft."
    }
}

function Get-VerifiedWebView2Installer {
    param(
        [Parameter(Mandatory = $true)] [string] $CacheDir,
        [Parameter(Mandatory = $true)] [string] $Url
    )

    New-Item -ItemType Directory -Force -Path $CacheDir | Out-Null
    $target = Join-Path $CacheDir 'MicrosoftEdgeWebView2RuntimeInstallerX64.exe'

    # The cache is reused only while the file still carries a valid Microsoft signature; the runtime
    # updates itself after install, so an older installer is not a reason to download 210 MB again.
    if (Test-Path -LiteralPath $target) {
        try {
            Assert-MicrosoftSignature -Path $target
            return $target
        }
        catch {
            Write-Host "Cached WebView2 installer rejected ($($_.Exception.Message)); downloading it again."
            Remove-Item -LiteralPath $target -Force
        }
    }

    Write-Host 'Downloading the Microsoft Edge WebView2 Evergreen Standalone Installer for the bundle payload (~210 MB)...'
    $partial = "$target.download"
    try {
        Invoke-WebRequest -Uri $Url -OutFile $partial
        # Signature is checked on the final .exe name: Get-AuthenticodeSignature trusts the extension.
        Move-Item -LiteralPath $partial -Destination $target -Force
        Assert-MicrosoftSignature -Path $target
    }
    catch {
        foreach ($leftover in @($partial, $target)) {
            if (Test-Path -LiteralPath $leftover) {
                Remove-Item -LiteralPath $leftover -Force
            }
        }
        throw "Refusing to ship an unverified WebView2 installer: $($_.Exception.Message)"
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
            [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($record)
        }
    }
    finally {
        $view.Close()
        # Release the WindowsInstaller COM handles deterministically. Otherwise the RCW keeps the
        # MSI file open until GC happens to run, which later blocks Move-Item of the agent MSI into
        # intermediates ("the process cannot access the file because it is being used by another process").
        [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($view)
        [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($database)
        [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($installer)
        [GC]::Collect()
        [GC]::WaitForPendingFinalizers()
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
            throw "Organization Admin MSI does not contain $($asset.Label). Build the React frontend and copy dist into WebAssets before WiX packaging."
        }
    }
}

function Assert-SetupWizardPublishContainsFrontendAssets {
    param(
        [Parameter(Mandatory = $true)]
        [string] $PublishDir
    )

    # Однажды эти файлы уже терялись по дороге в пакет (копия -File вместо -Recurse), и мастер на
    # чистой машине открывался пустой страницей. Проверяем то, что видит человек: экран мастера.
    $webAssets = Join-Path $PublishDir 'WebAssets'
    $requiredAssets = @(
        @{ Label = 'index.html'; Pattern = 'index.html' },
        @{ Label = 'JavaScript bundle'; Pattern = '*.js' },
        @{ Label = 'stylesheet'; Pattern = '*.css' }
    )

    foreach ($asset in $requiredAssets) {
        $found = Get-ChildItem -LiteralPath $webAssets -Recurse -File -Filter $asset.Pattern -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if (-not $found) {
            throw "Setup Wizard publish output does not contain the frontend $($asset.Label). Build src/AFK4.SetupWizard.Web before packaging."
        }
    }
}

if (-not (Test-Path -LiteralPath $DotnetPath)) {
    throw "dotnet executable was not found at '$DotnetPath'."
}

if (-not (Get-Command $BunPath -ErrorAction SilentlyContinue)) {
    throw "bun executable was not found on PATH (looked for '$BunPath')."
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $repoRoot 'artifacts/client-packages'
$publishRoot = Join-Path $artifactRoot 'publish'
$wixInputRoot = Join-Path $artifactRoot 'wix-inputs'
$organizationAdminWebRoot = Join-Path $repoRoot 'src/AFK4.OrganizationAdmin.Web'
$organizationAdminWebDist = Join-Path $organizationAdminWebRoot 'dist'
$organizationAdminWebDistIndex = Join-Path $organizationAdminWebDist 'index.html'
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

if ($SkipOrganizationAdminWebRestore) {
    Write-Host "Skipping Organization Admin frontend dependency restore because SkipOrganizationAdminWebRestore was set."
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

Push-Location $organizationAdminWebRoot
try {
    & $BunPath run build

    if ($LASTEXITCODE -ne 0) {
        throw "bun run build failed for Organization Admin frontend with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

if (-not (Test-Path -LiteralPath $organizationAdminWebDistIndex)) {
    throw "Organization Admin frontend build did not produce '$organizationAdminWebDistIndex'."
}

# The dev-only host-bridge stub (src/devHostBridge.ts) must never reach a production bundle.
# Fail the build if any dev marker leaked into the built output — a hard gate against the
# stub (including its hardcoded staging organization) shipping in the MSI. NB:
# 'browser-dev' is intentionally NOT a marker — operatorConfig.ts ships it as the legit
# fallback-config runtime label; these markers are unique to the stub.
$operatorDevMarkers = @(
    'DEV-ONLY',
    '0169044b-2f74-46a7-8e52-7656a39a8f8c')
$operatorBuiltFiles = Get-ChildItem -LiteralPath $organizationAdminWebDist -Recurse -File -Include '*.html', '*.js'
foreach ($builtFile in $operatorBuiltFiles) {
    $builtContent = Get-Content -LiteralPath $builtFile.FullName -Raw
    foreach ($marker in $operatorDevMarkers) {
        if ($builtContent -and $builtContent.Contains($marker)) {
            throw "Organization Admin build output '$($builtFile.FullName)' contains dev-only marker '$marker'. The dev host-bridge stub must not ship; ensure it stays gated behind import.meta.env.DEV."
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

# Мастер линкует этот dist прямо из своего csproj (как оболочка игрока), поэтому копировать
# ничего не нужно — но собрать обязательно: без dist в пакет не попадёт ни одного экрана, и
# мастер покажет «Setup Wizard frontend assets were not found».
$setupWizardWebRoot = Join-Path $repoRoot 'src/AFK4.SetupWizard.Web'
$setupWizardWebDist = Join-Path $setupWizardWebRoot 'dist'
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

# Every program is published self-contained: the .NET runtime sits in its own folder, so no PC needs
# a .NET install and the installer never asks for one. Each MSI harvests the whole publish directory
# (Files Include="...\**"), so the several hundred runtime files travel without being listed by hand.
$projects = @(
    @{ Name = 'organization-admin'; Path = 'src/AFK4.OrganizationAdmin.App/AFK4.OrganizationAdmin.App.csproj' },
    @{ Name = 'agent-service'; Path = 'src/AFK4.Agent.Service/AFK4.Agent.Service.csproj' },
    @{ Name = 'player-shell'; Path = 'src/AFK4.Player.Shell/AFK4.Player.Shell.csproj' },
    @{ Name = 'setup-wizard'; Path = 'src/AFK4.SetupWizard/AFK4.SetupWizard.csproj' }
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
        --self-contained true `
        -o $output `
        -p:NuGetAudit=false `
        -p:UseSharedCompilation=false `
        -p:AFK4PlatformBaseUrl="$platformBaseUrl"

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for '$($project.Name)' with exit code $LASTEXITCODE."
    }
}

$organizationAdminPublishDir = Join-Path $publishRoot "organization-admin-$Version-$Channel"
$organizationAdminWebAssetsPublishDir = Join-Path $organizationAdminPublishDir 'WebAssets'
$organizationAdminPublishDirFullPath = [System.IO.Path]::GetFullPath($organizationAdminPublishDir)
$organizationAdminWebAssetsPublishDirFullPath = [System.IO.Path]::GetFullPath($organizationAdminWebAssetsPublishDir)

if (-not $organizationAdminWebAssetsPublishDirFullPath.StartsWith($organizationAdminPublishDirFullPath, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Computed Organization Admin WebAssets directory must stay under '$organizationAdminPublishDirFullPath'."
}

if (Test-Path -LiteralPath $organizationAdminWebAssetsPublishDir) {
    Remove-Item -LiteralPath $organizationAdminWebAssetsPublishDir -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $organizationAdminWebAssetsPublishDir | Out-Null
Get-ChildItem -LiteralPath $organizationAdminWebDist -Force |
    Copy-Item -Destination $organizationAdminWebAssetsPublishDir -Recurse -Force

$agentServicePublishDir = Join-Path $publishRoot "agent-service-$Version-$Channel"
$setupWizardPublishDir = Join-Path $publishRoot "setup-wizard-$Version-$Channel"
$agentServiceSupportDir = Join-Path $wixInputRoot 'agent-service-support'
$setupWizardSupportDir = Join-Path $wixInputRoot 'setup-wizard-support'
$updateHelperDir = Join-Path $wixInputRoot 'update-helpers'

New-Item -ItemType Directory -Force -Path $agentServiceSupportDir | Out-Null
New-Item -ItemType Directory -Force -Path $setupWizardSupportDir | Out-Null
New-Item -ItemType Directory -Force -Path $updateHelperDir | Out-Null

Get-ChildItem -LiteralPath $agentServicePublishDir -Force |
    Where-Object { $_.Name -ne 'AFK4.Agent.Service.exe' } |
    Copy-Item -Destination $agentServiceSupportDir -Recurse -Force

$organizationAdminMsiPath = Join-Path $artifactRoot "afk4-organization-admin-$Version-$Channel.msi"
$agentMsiPath = Join-Path $artifactRoot "afk4-agent-$Version-$Channel.msi"
$playerShellMsiPath = Join-Path $artifactRoot "afk4-player-shell-$Version-$Channel.msi"

@($organizationAdminMsiPath, $agentMsiPath, $playerShellMsiPath) |
    Where-Object { Test-Path -LiteralPath $_ } |
    ForEach-Object { Remove-Item -LiteralPath $_ -Force }

# The Burn bundle needs the BootstrapperApplications (WixStandardBootstrapperApplication;
# the v7 rename of the old Bal extension) and Util (RegistrySearch for the WebView2 runtime)
# extensions.
# `wix extension add` is idempotent.
foreach ($wixExtension in @('WixToolset.BootstrapperApplications.wixext', 'WixToolset.Util.wixext')) {
    & $DotnetPath wix extension add -acceptEula wix7 -g $wixExtension
    if ($LASTEXITCODE -ne 0) {
        throw "Adding WiX extension '$wixExtension' failed with exit code $LASTEXITCODE."
    }
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

# Build the Organization Admin MSI next: the agent MSI also bundles it into the wizard payload so the
# wizard can install the Organization Admin on cashier/manager workstations (role manager_workstation),
# the same way it installs the Player Shell on gaming PCs. So it must exist before the harvest too.
& $DotnetPath wix build -acceptEula wix7 (Join-Path $repoRoot 'installers/organization-admin/Package.wxs') `
    -arch x64 `
    -d "PackageVersion=$msiVersion" `
    -d "OrganizationAdminPublishDir=$(Join-Path $publishRoot "organization-admin-$Version-$Channel")" `
    -o $organizationAdminMsiPath

if ($LASTEXITCODE -ne 0) {
    throw "WiX build failed for Organization Admin MSI with exit code $LASTEXITCODE."
}

Assert-OperatorMsiContainsFrontendAssets -MsiPath $organizationAdminMsiPath

# Bundle BOTH role apps into the wizard payload before the support dir is harvested.
$setupWizardPayloadDir = Join-Path $setupWizardPublishDir 'payload'
New-Item -ItemType Directory -Force -Path $setupWizardPayloadDir | Out-Null
Copy-Item -LiteralPath $playerShellMsiPath -Destination (Join-Path $setupWizardPayloadDir 'AFK4.Player.Shell.msi') -Force
Copy-Item -LiteralPath $organizationAdminMsiPath -Destination (Join-Path $setupWizardPayloadDir 'AFK4.OrganizationAdmin.msi') -Force

# -Recurse (not -File) so the WebAssets\** subfolder ships in the support dir;
# the agent MSI harvests SetupWizardFiles from here, and the wizard resolves its
# UI from WebAssets next to the exe. Files-only copy dropped it -> placeholder page.
Assert-SetupWizardPublishContainsFrontendAssets -PublishDir $setupWizardPublishDir
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

# Util — для util:ServiceConfig: правила перезапуска службы агента после падения.
& $DotnetPath wix build -acceptEula wix7 (Join-Path $repoRoot 'installers/agent/Package.wxs') `
    -arch x64 `
    -ext WixToolset.Util.wixext `
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
if (-not ($agentFiles | Where-Object { $_ -like '*AFK4.OrganizationAdmin.msi*' } | Select-Object -First 1)) {
    throw "Agent MSI does not contain the bundled Organization Admin MSI (payload\AFK4.OrganizationAdmin.msi)."
}

# The agent MSI auto-launches the Setup Wizard on interactive install. Every component is
# self-contained, so the MSIs need no .NET on the target; the bundle carries WebView2 offline.

# Carry the offline WebView2 runtime and chain the agent MSI into a single master installer .exe.
$runtimeCacheDir = Join-Path $artifactRoot 'runtime-cache'
$webView2InstallerPath = Get-VerifiedWebView2Installer `
    -CacheDir $runtimeCacheDir -Url $webView2InstallerUrl

$clientBundlePath = Join-Path $artifactRoot "afk4-client-$Version-$Channel.exe"
if (Test-Path -LiteralPath $clientBundlePath) {
    Remove-Item -LiteralPath $clientBundlePath -Force
}

# Build the native BAFunctions DLL (auto-start install + auto-close) that plugs into WixStdBA.
# Native + statically-linked CRT, so it loads on a freshly-imaged box with no .NET/VC++ runtime.
$baFunctionsProj = Join-Path $repoRoot 'installers/bundle/bafunctions/AFK4.BAFunctions.vcxproj'
$vsWhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
if (-not (Test-Path -LiteralPath $vsWhere)) {
    throw "vswhere.exe not found; install Visual Studio with the 'Desktop development with C++' workload to build the BAFunctions DLL."
}
# -products * — иначе vswhere видит только полную Visual Studio, а машина сборки с одними
# Build Tools (без IDE) остаётся без бандла: «MSBuild not found», хотя MSBuild и C++ на месте.
$msbuild = & $vsWhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) {
    throw "MSBuild not found via vswhere; install the 'Desktop development with C++' workload."
}
& $msbuild $baFunctionsProj -restore -p:Configuration=Release -p:Platform=x64 -v:minimal -nologo
if ($LASTEXITCODE -ne 0) {
    throw "MSBuild failed to build the BAFunctions DLL with exit code $LASTEXITCODE."
}
$baFunctionsPath = Join-Path $repoRoot 'installers/bundle/bafunctions/x64/Release/AFK4.BAFunctions.dll'
if (-not (Test-Path -LiteralPath $baFunctionsPath)) {
    throw "BAFunctions DLL was not produced at $baFunctionsPath."
}

# Brand assets: the bundle .exe icon and the bootstrapper window logo (committed under brand/dist).
$brandIconPath = Join-Path $repoRoot 'brand/dist/afk4.ico'
$brandLogoPath = Join-Path $repoRoot 'brand/dist/icon-64.png'
foreach ($brandAsset in @($brandIconPath, $brandLogoPath)) {
    if (-not (Test-Path -LiteralPath $brandAsset)) {
        throw "Brand asset missing: $brandAsset (regenerate with scripts/build-brand-assets.mjs)."
    }
}

& $DotnetPath wix build -acceptEula wix7 (Join-Path $repoRoot 'installers/bundle/Bundle.wxs') `
    -ext WixToolset.BootstrapperApplications.wixext `
    -ext WixToolset.Util.wixext `
    -arch x64 `
    -d "PackageVersion=$msiVersion" `
    -d "WebView2InstallerPath=$webView2InstallerPath" `
    -d "AgentMsiPath=$agentMsiPath" `
    -d "BAFunctionsPath=$baFunctionsPath" `
    -d "BrandIconPath=$brandIconPath" `
    -d "BrandLogoPath=$brandLogoPath" `
    -o $clientBundlePath

if ($LASTEXITCODE -ne 0) {
    throw "WiX build failed for the client master installer (Burn bundle) with exit code $LASTEXITCODE."
}

# The operator + player-shell MSIs are only inputs now — they ship bundled inside the agent MSI
# payload, not on their own. Move them (and their .wixpdb) into an intermediates\ subfolder so the
# deliverable folder presents a single file (the agent MSI) and nobody hands a client the wrong one.
$intermediatesDir = Join-Path $artifactRoot 'intermediates'
New-Item -ItemType Directory -Force -Path $intermediatesDir | Out-Null
foreach ($bundledMsi in @($organizationAdminMsiPath, $playerShellMsiPath)) {
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
        Move-Item -LiteralPath $artifact -Destination $intermediatesDir -Force
    }
}

# Keep only the bundle .exe in the package folder; its .wixpdb is a build artifact.
$clientBundleWixPdb = [System.IO.Path]::ChangeExtension($clientBundlePath, '.wixpdb')
if (Test-Path -LiteralPath $clientBundleWixPdb) {
    Move-Item -LiteralPath $clientBundleWixPdb -Destination $intermediatesDir -Force
}

Write-Host "Published client package inputs under $publishRoot"
Write-Host "Deliverable master installer (install this one):"
Write-Host $clientBundlePath
Write-Host "Bundled inputs (agent/operator/player-shell MSIs) moved to: $intermediatesDir"
