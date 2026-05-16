# AFK4 Phase 13 Client Packaging And CI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the MVP packaging baseline for AFK4 Windows clients: WiX/MSI packaging decisions, local package build scripts, Agent MSI update helper scripts, and a CI release workflow using the same commands.

**Architecture:** Use WiX-authored MSI packages for the Operator App and for the coordinated gaming-PC Agent Service + Player Shell bundle. Keep the existing AFK4 update authority model: packages are externally hosted artifacts, metadata is signed by `AFK4.Update.Publisher`, and Agents install through the already implemented external install, rollback, and restart adapters. CI should be a wrapper over local scripts rather than a separate release path.

**Tech Stack:** .NET 10, WPF, Windows Worker Service, WiX Toolset `wix` dotnet tool `7.0.0`, PowerShell, GitHub Actions Windows runners, xUnit, existing Update Publisher.

---

## Scope

Phase 13 implements:

- the approved WiX/MSI packaging design for client surfaces;
- local PowerShell scripts for MSI update install, rollback, and Agent restart
  scheduling;
- a local packaging entrypoint that publishes Windows client projects and
  places MSI/update artifacts under ignored `artifacts/`;
- WiX tool manifest setup;
- first WiX project scaffolding for Operator App and gaming-PC packages;
- GitHub Actions release workflow that builds, tests, packages, and uploads
  artifacts without committing binaries or secrets;
- docs and runbook updates for package build and rollout.

Phase 13 does not implement:

- a local club server;
- a web admin panel;
- MSIX distribution;
- production provider-specific object-store/CDN SDK adapters;
- production certificate/key procurement;
- committed binary artifacts, signing keys, or generated release request JSON.

## File Structure

Create and modify these files:

```text
D:\afk4.net\
  .config\dotnet-tools.json
  .github\workflows\client-packages.yml
  README.md
  docs\operations\client-packaging.md
  docs\operations\agent-installer-enrollment.md
  docs\operations\update-package-publishing.md
  docs\progress\2026-05-12-vertical-slice-progress.md
  docs\superpowers\specs\2026-05-14-afk4-client-packaging-design.md
  docs\superpowers\plans\2026-05-14-afk4-phase13-client-packaging-ci.md
  installers\
    README.md
    operator-app\
      Package.wxs
    gaming-pc\
      Package.wxs
  scripts\
    build-client-packages.ps1
    install-afk4-update-msi.ps1
    rollback-afk4-update-msi.ps1
    restart-afk4-agent-service.ps1
  tests\
    AFK4.Agent.Service.Tests\
      UpdateHelperScriptTests.cs
```

Responsibilities:

- `.config/dotnet-tools.json`: pins `wix` alongside `dotnet-ef`.
- `installers/operator-app/Package.wxs`: Operator App MSI authoring.
- `installers/gaming-pc/Package.wxs`: coordinated Agent Service + Player Shell
  MSI authoring.
- `scripts/build-client-packages.ps1`: local and CI package entrypoint.
- `scripts/install-afk4-update-msi.ps1`: update install helper around
  `msiexec.exe /i`.
- `scripts/rollback-afk4-update-msi.ps1`: rollback helper around a previous
  known-good MSI artifact.
- `scripts/restart-afk4-agent-service.ps1`: schedules or performs Agent
  service restart outside the current update process.
- `.github/workflows/client-packages.yml`: Windows release workflow.
- `docs/operations/client-packaging.md`: runbook for local and CI package
  builds.

## Task 1: Packaging Decision Documentation

**Files:**

- Create: `docs\superpowers\specs\2026-05-14-afk4-client-packaging-design.md`
- Create: `docs\superpowers\plans\2026-05-14-afk4-phase13-client-packaging-ci.md`
- Modify: `docs\superpowers\specs\2026-05-12-afk4-platform-architecture-design.md`
- Modify: `README.md`
- Modify: `docs\progress\2026-05-12-vertical-slice-progress.md`

- [ ] **Step 1: Record the approved packaging decision**

Write a focused design spec that states:

- Operator App uses WiX/MSI for the MVP;
- Agent Service and Player Shell use one coordinated gaming-PC WiX/MSI
  installer;
- MSIX is deferred;
- update packages remain signed metadata plus externally hosted artifact bytes;
- Agent update execution uses the existing external installer adapter.

- [ ] **Step 2: Add this implementation plan**

Save this plan at:

```text
docs/superpowers/plans/2026-05-14-afk4-phase13-client-packaging-ci.md
```

- [ ] **Step 3: Update architecture and progress navigation**

Update the main architecture spec Deployment And Updates section to reference
the WiX/MSI decision. Add this plan to the progress document plan list and mark
the old "decide MSI/MSIX/WiX packaging" next-work item as resolved into Phase
13 implementation work.

- [ ] **Step 4: Verify documentation references**

Run:

```powershell
rg -n "Phase 13|WiX|MSI|client-packaging|client packaging" README.md docs
```

Expected:

```text
README.md:...
docs\operations\...
docs\progress\...
docs\superpowers\plans\2026-05-14-afk4-phase13-client-packaging-ci.md:...
docs\superpowers\specs\2026-05-14-afk4-client-packaging-design.md:...
```

- [ ] **Step 5: Commit documentation decision**

Run:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add README.md docs
& 'C:\Program Files\Git\cmd\git.exe' commit -m "docs: add client packaging decision"
```

Expected:

```text
[codex/phase11-operational-reports ...] docs: add client packaging decision
```

## Task 2: Pin WiX Tooling

**Files:**

- Modify: `.config\dotnet-tools.json`
- Modify: `README.md`
- Modify: `docs\operations\client-packaging.md`

- [ ] **Step 1: Add the WiX dotnet tool to the manifest**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' tool install wix --version 7.0.0
```

Expected:

```text
You can invoke the tool from this directory using the following commands: 'dotnet tool run wix' or 'dotnet wix'.
Tool 'wix' (version '7.0.0') was successfully installed.
```

- [ ] **Step 2: Verify the tool manifest contains both tools**

Run:

```powershell
Get-Content -Raw .config/dotnet-tools.json
```

Expected content includes:

```json
{
  "tools": {
    "dotnet-ef": {
      "version": "10.0.4",
      "commands": [
        "dotnet-ef"
      ]
    },
    "wix": {
      "version": "7.0.0",
      "commands": [
        "wix"
      ]
    }
  }
}
```

- [ ] **Step 3: Verify WiX restores**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' tool restore
& 'C:\Program Files\dotnet\dotnet.exe' wix --version
```

Expected:

```text
Tool 'wix' (version '7.0.0') was restored.
7.0.0+...
```

- [ ] **Step 4: Commit tool manifest**

Run:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add .config/dotnet-tools.json README.md docs/operations/client-packaging.md
& 'C:\Program Files\Git\cmd\git.exe' commit -m "chore: pin wix packaging tool"
```

Expected:

```text
[codex/phase11-operational-reports ...] chore: pin wix packaging tool
```

## Task 3: Add MSI Update Helper Scripts

**Files:**

- Create: `scripts\install-afk4-update-msi.ps1`
- Create: `scripts\rollback-afk4-update-msi.ps1`
- Create: `scripts\restart-afk4-agent-service.ps1`
- Create: `tests\AFK4.Agent.Service.Tests\UpdateHelperScriptTests.cs`

- [ ] **Step 1: Write failing script contract tests**

Create `tests\AFK4.Agent.Service.Tests\UpdateHelperScriptTests.cs`:

```csharp
using System.Management.Automation.Language;

namespace AFK4.Agent.Service.Tests;

public sealed class UpdateHelperScriptTests
{
    [Theory]
    [InlineData("scripts/install-afk4-update-msi.ps1", "PackagePath")]
    [InlineData("scripts/rollback-afk4-update-msi.ps1", "PackagePath")]
    [InlineData("scripts/restart-afk4-agent-service.ps1", "ServiceName")]
    public void Script_ParsesWithoutPowerShellErrors(string scriptPath, string requiredParameter)
    {
        var absolutePath = Path.GetFullPath(Path.Combine(GetRepositoryRoot(), scriptPath));

        var ast = Parser.ParseFile(absolutePath, out _, out var errors);

        Assert.Empty(errors);
        Assert.Contains(
            ast.ParamBlock!.Parameters,
            parameter => string.Equals(
                parameter.Name.VariablePath.UserPath,
                requiredParameter,
                StringComparison.Ordinal));
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AFK4.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }
}
```

- [ ] **Step 2: Add PowerShell SDK package for parser-only tests**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' add tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj package System.Management.Automation
```

Expected:

```text
PackageReference for package 'System.Management.Automation' added
```

- [ ] **Step 3: Run tests and verify RED**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --filter UpdateHelperScriptTests -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
```

Expected:

```text
Could not find file ... scripts\install-afk4-update-msi.ps1
```

- [ ] **Step 4: Implement install helper**

Create `scripts\install-afk4-update-msi.ps1`:

```powershell
param(
    [Parameter(Mandatory = $true)]
    [string] $PackagePath,

    [Parameter(Mandatory = $true)]
    [string] $Component,

    [Parameter(Mandatory = $true)]
    [string] $Version,

    [string] $LogDirectory = "$env:ProgramData\AFK4\Agent\UpdateLogs",

    [string] $MsiexecPath = "$env:WINDIR\System32\msiexec.exe"
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $PackagePath)) {
    throw "MSI package was not found at '$PackagePath'."
}

if ([System.IO.Path]::GetExtension($PackagePath) -ne '.msi') {
    throw "Update package must be an .msi file."
}

if (-not (Test-Path -LiteralPath $MsiexecPath)) {
    throw "msiexec was not found at '$MsiexecPath'."
}

New-Item -ItemType Directory -Force -Path $LogDirectory | Out-Null
$safeComponent = $Component -replace '[^A-Za-z0-9_.-]', '_'
$safeVersion = $Version -replace '[^A-Za-z0-9_.-]', '_'
$logPath = Join-Path $LogDirectory "$safeComponent-$safeVersion-install.log"
$arguments = @('/i', $PackagePath, '/qn', '/norestart', '/l*v', $logPath)

$process = Start-Process -FilePath $MsiexecPath -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
if ($process.ExitCode -eq 0 -or $process.ExitCode -eq 3010) {
    exit 0
}

exit $process.ExitCode
```

- [ ] **Step 5: Implement rollback helper**

Create `scripts\rollback-afk4-update-msi.ps1`:

```powershell
param(
    [Parameter(Mandatory = $true)]
    [string] $PackagePath,

    [Parameter(Mandatory = $true)]
    [string] $Component,

    [Parameter(Mandatory = $true)]
    [string] $Version,

    [string] $LogDirectory = "$env:ProgramData\AFK4\Agent\UpdateLogs",

    [string] $MsiexecPath = "$env:WINDIR\System32\msiexec.exe"
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $PackagePath)) {
    throw "Rollback MSI package was not found at '$PackagePath'."
}

if ([System.IO.Path]::GetExtension($PackagePath) -ne '.msi') {
    throw "Rollback package must be an .msi file."
}

if (-not (Test-Path -LiteralPath $MsiexecPath)) {
    throw "msiexec was not found at '$MsiexecPath'."
}

New-Item -ItemType Directory -Force -Path $LogDirectory | Out-Null
$safeComponent = $Component -replace '[^A-Za-z0-9_.-]', '_'
$safeVersion = $Version -replace '[^A-Za-z0-9_.-]', '_'
$logPath = Join-Path $LogDirectory "$safeComponent-$safeVersion-rollback.log"
$arguments = @('/i', $PackagePath, '/qn', '/norestart', '/l*v', $logPath)

$process = Start-Process -FilePath $MsiexecPath -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
if ($process.ExitCode -eq 0 -or $process.ExitCode -eq 3010) {
    exit 0
}

exit $process.ExitCode
```

- [ ] **Step 6: Implement Agent restart scheduler**

Create `scripts\restart-afk4-agent-service.ps1`:

```powershell
param(
    [string] $ServiceName = 'AFK4.Agent.Service',

    [int] $DelaySeconds = 5
)

$ErrorActionPreference = 'Stop'

$scriptBlock = {
    param($Name, $Delay)
    Start-Sleep -Seconds $Delay
    Restart-Service -Name $Name -Force
}

Start-Job -ScriptBlock $scriptBlock -ArgumentList $ServiceName, $DelaySeconds | Out-Null
exit 0
```

- [ ] **Step 7: Run tests and commit**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --filter UpdateHelperScriptTests -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal
& 'C:\Program Files\Git\cmd\git.exe' add scripts tests/AFK4.Agent.Service.Tests
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat: add msi update helper scripts"
```

Expected:

```text
Passed! - Failed: 0
[codex/phase11-operational-reports ...] feat: add msi update helper scripts
```

## Task 4: Add Local Client Package Build Entrypoint

**Files:**

- Create: `scripts\build-client-packages.ps1`
- Modify: `docs\operations\client-packaging.md`
- Modify: `README.md`

- [ ] **Step 1: Add parser verification command to the runbook**

Document this command in `docs/operations/client-packaging.md`:

```powershell
[System.Management.Automation.Language.Parser]::ParseFile(
  (Resolve-Path scripts/build-client-packages.ps1),
  [ref] $null,
  [ref] $null
) | Out-Null
```

- [ ] **Step 2: Implement the build script**

Create `scripts\build-client-packages.ps1`:

```powershell
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

New-Item -ItemType Directory -Force -Path $publishRoot | Out-Null

$projects = @(
    @{ Name = 'operator-app'; Path = 'src/AFK4.Operator.App/AFK4.Operator.App.csproj' },
    @{ Name = 'agent-service'; Path = 'src/AFK4.Agent.Service/AFK4.Agent.Service.csproj' },
    @{ Name = 'player-shell'; Path = 'src/AFK4.Player.Shell/AFK4.Player.Shell.csproj' }
)

foreach ($project in $projects) {
    $output = Join-Path $publishRoot "$($project.Name)-$Version-$Channel"
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
}

Write-Host "Published client package inputs under $publishRoot"
Write-Host "WiX MSI build steps will consume these directories in the next task."
```

- [ ] **Step 3: Verify script parses**

Run:

```powershell
powershell -NoProfile -Command "[System.Management.Automation.Language.Parser]::ParseFile((Resolve-Path scripts/build-client-packages.ps1), [ref] `$null, [ref] `$null) | Out-Null"
```

Expected: command exits `0`.

- [ ] **Step 4: Verify publish works**

Run:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build-client-packages.ps1 -Version 0.1.0-ci -Channel internal
```

Expected:

```text
Published client package inputs under D:\afk4.net\artifacts\client-packages\publish
WiX MSI build steps will consume these directories in the next task.
```

- [ ] **Step 5: Commit build script**

Run:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add scripts/build-client-packages.ps1 docs/operations/client-packaging.md README.md
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat: add client package build script"
```

Expected:

```text
[codex/phase11-operational-reports ...] feat: add client package build script
```

## Task 5: Add WiX MSI Scaffolding

**Files:**

- Create: `installers\README.md`
- Create: `installers\operator-app\Package.wxs`
- Create: `installers\gaming-pc\Package.wxs`
- Modify: `scripts\build-client-packages.ps1`
- Modify: `docs\operations\client-packaging.md`

- [ ] **Step 1: Add installer directory README**

Create `installers\README.md`:

```markdown
# AFK4 Installers

AFK4 uses WiX-authored MSI packages for MVP Windows client distribution.

- `operator-app` packages `AFK4.Operator.App`.
- `gaming-pc` packages `AFK4.Agent.Service` and `AFK4.Player.Shell` together.

Generated MSI files belong under ignored `artifacts/client-packages/`.
Do not commit built installers, signing keys, certificates, or generated update
package request JSON.
```

- [ ] **Step 2: Add minimal Operator App WiX package**

Create `installers\operator-app\Package.wxs`:

```xml
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Package
      Name="AFK4 Operator App"
      Manufacturer="AFK4"
      Version="$(var.PackageVersion)"
      UpgradeCode="{5B345D78-17AE-4CC7-9FE7-7E5F4203FAF1}"
      Scope="perMachine">
    <MajorUpgrade DowngradeErrorMessage="A newer version of AFK4 Operator App is already installed." />
    <MediaTemplate EmbedCab="yes" />

    <StandardDirectory Id="ProgramFiles64Folder">
      <Directory Id="AFK4RootFolder" Name="AFK4">
        <Directory Id="OperatorAppFolder" Name="Operator App" />
      </Directory>
    </StandardDirectory>

    <Feature Id="MainFeature" Title="AFK4 Operator App" Level="1">
      <ComponentGroupRef Id="OperatorAppFiles" />
    </Feature>

    <ComponentGroup Id="OperatorAppFiles" Directory="OperatorAppFolder">
      <Files Include="$(var.OperatorAppPublishDir)\**" />
    </ComponentGroup>
  </Package>
</Wix>
```

- [ ] **Step 3: Add minimal gaming-PC WiX package**

Create `installers\gaming-pc\Package.wxs`:

```xml
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Package
      Name="AFK4 Gaming PC Client"
      Manufacturer="AFK4"
      Version="$(var.PackageVersion)"
      UpgradeCode="{36974A53-4931-491E-8FD0-7DA533E6DCC3}"
      Scope="perMachine">
    <MajorUpgrade DowngradeErrorMessage="A newer version of AFK4 Gaming PC Client is already installed." />
    <MediaTemplate EmbedCab="yes" />

    <StandardDirectory Id="ProgramFiles64Folder">
      <Directory Id="AFK4RootFolder" Name="AFK4">
        <Directory Id="AgentServiceFolder" Name="Agent Service" />
        <Directory Id="PlayerShellFolder" Name="Player Shell" />
        <Directory Id="UpdateHelperFolder" Name="Update Helpers" />
      </Directory>
    </StandardDirectory>

    <Feature Id="MainFeature" Title="AFK4 Gaming PC Client" Level="1">
      <ComponentGroupRef Id="AgentServiceFiles" />
      <ComponentGroupRef Id="PlayerShellFiles" />
      <ComponentGroupRef Id="UpdateHelperScripts" />
      <ComponentRef Id="AgentServiceRegistration" />
    </Feature>

    <ComponentGroup Id="AgentServiceFiles" Directory="AgentServiceFolder">
      <Files Include="$(var.AgentServiceSupportDir)\**" />
    </ComponentGroup>

    <Component Id="AgentServiceRegistration" Directory="AgentServiceFolder" Guid="{1D0F3E02-6939-4A77-B937-9888F7C89C98}">
      <File Id="AgentServiceExe" Source="$(var.AgentServicePublishDir)\AFK4.Agent.Service.exe" KeyPath="yes" />
      <ServiceInstall
          Id="AgentServiceInstall"
          Name="AFK4.Agent.Service"
          DisplayName="AFK4 Agent Service"
          Description="AFK4 gaming PC control and update service."
          Type="ownProcess"
          Start="auto"
          ErrorControl="normal"
          Account="LocalSystem" />
      <ServiceControl
          Id="AgentServiceControl"
          Name="AFK4.Agent.Service"
          Start="install"
          Stop="both"
          Remove="uninstall"
          Wait="yes" />
    </Component>

    <ComponentGroup Id="PlayerShellFiles" Directory="PlayerShellFolder">
      <Files Include="$(var.PlayerShellPublishDir)\**" />
    </ComponentGroup>

    <ComponentGroup Id="UpdateHelperScripts" Directory="UpdateHelperFolder">
      <Files Include="$(var.UpdateHelperDir)\*.ps1" />
    </ComponentGroup>
  </Package>
</Wix>
```

- [ ] **Step 4: Update the build script to call WiX**

Extend `scripts\build-client-packages.ps1` so it invokes:

```powershell
& $DotnetPath wix build -acceptEula wix7 (Join-Path $repoRoot 'installers/operator-app/Package.wxs') `
    -d "PackageVersion=$Version" `
    -d "OperatorAppPublishDir=$(Join-Path $publishRoot "operator-app-$Version-$Channel")" `
    -o (Join-Path $artifactRoot "afk4-operator-app-$Version-$Channel.msi")

& $DotnetPath wix build -acceptEula wix7 (Join-Path $repoRoot 'installers/gaming-pc/Package.wxs') `
    -d "PackageVersion=$Version" `
    -d "AgentServicePublishDir=$(Join-Path $publishRoot "agent-service-$Version-$Channel")" `
    -d "AgentServiceSupportDir=$(Join-Path $artifactRoot 'wix-inputs/agent-service-support')" `
    -d "PlayerShellPublishDir=$(Join-Path $publishRoot "player-shell-$Version-$Channel")" `
    -d "UpdateHelperDir=$(Join-Path $artifactRoot 'wix-inputs/update-helpers')" `
    -o (Join-Path $artifactRoot "afk4-gaming-pc-$Version-$Channel.msi")
```

- [ ] **Step 5: Verify MSI artifacts build**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' tool restore
powershell -ExecutionPolicy Bypass -File scripts/build-client-packages.ps1 -Version 0.1.0-ci -Channel internal
Get-ChildItem artifacts/client-packages/*.msi
```

Expected:

```text
afk4-operator-app-0.1.0-ci-internal.msi
afk4-gaming-pc-0.1.0-ci-internal.msi
```

- [ ] **Step 6: Commit MSI scaffolding**

Run:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add installers scripts/build-client-packages.ps1 docs/operations/client-packaging.md
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat: add wix msi packaging scaffold"
```

Expected:

```text
[codex/phase11-operational-reports ...] feat: add wix msi packaging scaffold
```

## Task 6: Add CI Client Package Workflow

**Files:**

- Create: `.github\workflows\client-packages.yml`
- Modify: `docs\operations\client-packaging.md`
- Modify: `README.md`

- [ ] **Step 1: Create workflow**

Create `.github\workflows\client-packages.yml`:

```yaml
name: Client Packages

on:
  workflow_dispatch:
    inputs:
      version:
        description: Package version
        required: true
        type: string
      channel:
        description: Update channel
        required: true
        default: internal
        type: choice
        options:
          - internal
          - beta
          - stable

jobs:
  build-client-packages:
    runs-on: windows-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          global-json-file: global.json

      - name: Restore tools
        run: dotnet tool restore

      - name: Restore
        run: dotnet restore AFK4.sln

      - name: Build
        run: dotnet build AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false

      - name: Test
        run: dotnet test AFK4.sln --no-build -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal

      - name: Build client packages
        shell: pwsh
        run: |
          powershell -ExecutionPolicy Bypass -File scripts/build-client-packages.ps1 -Version "${{ inputs.version }}" -Channel "${{ inputs.channel }}"

      - name: Upload client packages
        uses: actions/upload-artifact@v4
        with:
          name: afk4-client-packages-${{ inputs.version }}-${{ inputs.channel }}
          path: artifacts/client-packages/*.msi
```

- [ ] **Step 2: Verify workflow YAML exists**

Run:

```powershell
Test-Path .github/workflows/client-packages.yml
```

Expected:

```text
True
```

- [ ] **Step 3: Run local workflow-equivalent commands**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' tool restore
& 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
powershell -ExecutionPolicy Bypass -File scripts/build-client-packages.ps1 -Version 0.1.0-ci -Channel internal
```

Expected:

```text
Build succeeded.
Passed! - Failed: 0
```

and two MSI files under `artifacts/client-packages/`.

- [ ] **Step 4: Commit CI workflow**

Run:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add .github/workflows/client-packages.yml docs/operations/client-packaging.md README.md
& 'C:\Program Files\Git\cmd\git.exe' commit -m "ci: add client package workflow"
```

Expected:

```text
[codex/phase11-operational-reports ...] ci: add client package workflow
```

## Task 7: Phase 13 Verification And Progress Update

**Files:**

- Modify: `README.md`
- Modify: `docs\progress\2026-05-12-vertical-slice-progress.md`

- [ ] **Step 1: Run full verification**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' tool restore
& 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
powershell -ExecutionPolicy Bypass -File scripts/build-client-packages.ps1 -Version 0.1.0-ci -Channel internal
```

Expected:

```text
Build succeeded.
Passed! - Failed: 0
Published client package inputs under ...
```

and:

```powershell
Get-ChildItem artifacts/client-packages/*.msi
```

returns Operator App and gaming-PC MSI artifacts.

- [ ] **Step 2: Update progress**

Record:

- WiX/MSI decision implemented;
- helper scripts added;
- local package build command;
- CI workflow;
- verification results with exact commands.

- [ ] **Step 3: Commit verification docs**

Run:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add README.md docs/progress/2026-05-12-vertical-slice-progress.md
& 'C:\Program Files\Git\cmd\git.exe' commit -m "docs: record phase 13 packaging verification"
```

Expected:

```text
[codex/phase11-operational-reports ...] docs: record phase 13 packaging verification
```

## Plan Self-Review

Spec coverage:

- Operator App WiX/MSI baseline is covered by Tasks 1, 5, and 6.
- Coordinated Agent Service + Player Shell gaming-PC MSI is covered by Tasks 1,
  3, 5, and 6.
- Agent update adapter integration is covered by Task 3.
- Local developer package flow is covered by Task 4.
- CI release job is covered by Task 6.
- Verification and progress tracking are covered by Task 7.

Deferred:

- production Authenticode certificate procurement;
- provider-specific object-store/CDN SDK adapters;
- key-vault SDK integration;
- MSIX;
- production rollout promotion automation beyond artifact upload and existing
  Update Publisher boundaries.
