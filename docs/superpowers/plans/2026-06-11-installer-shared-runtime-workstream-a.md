# Installer Shared Runtime (Workstream A) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cut the AFK4 agent installer from ~160 MB to ~70–90 MB by publishing the four .NET client components framework-dependent and shipping **one** .NET 10 Desktop Runtime **carried inside** a WiX Burn bundle (`afk4-client-<ver>-<channel>.exe`) — never downloaded at install time.

**Architecture:** Two stages. **A1** flips the build's `dotnet publish` calls from self-contained to framework-dependent; the same per-component MSIs are still emitted (smaller now), installable on any machine that already has the runtime. **A2** resurrects a single Burn bundle that chains the **embedded** Desktop Runtime (carried as a compressed payload) → the agent MSI, so a clean machine gets the runtime first and the agent + wizard start. The deliverable becomes the bundle `.exe`; the agent MSI moves to `intermediates/`.

**Tech Stack:** .NET 10 (SDK 10.0.203), WiX Toolset 7.0.0 (`dotnet tool`), WiX `Bal` + `Netfx` extensions, PowerShell 7 (`pwsh`), xUnit tests that parse the build script / WiX / workflows as text.

---

## Why this is not a repeat of the revert

The earlier attempt was reverted in commit `20a7a31` (2026-06-10):

> Framework-dependent broke the agent Windows service on freshly-imaged machines: the runtime install location was not discoverable (missing registry registration), so the apphost could not find Microsoft.NETCore.App and the service failed to start (sc 1053). … Removes the fragile .NET-runtime-download bootstrapper machinery.

The prior `Bundle.wxs` already chained the runtime first as a `bal:PrereqPackage`, but it **downloaded** the runtime (`ExePackagePayload DownloadUrl=…`). On freshly-imaged machines the download/registration was not reliably complete before the chained MSI ran, so the service started against a runtime the apphost could not resolve.

**The single change that fixes this:** carry the runtime **inside** the bundle (`Compressed="yes"` + `SourceFile`, no `DownloadUrl`). Burn extracts and installs the embedded runtime synchronously as a vital prereq before the MSI in the chain runs — no network, no race. Sources for the embed syntax: [FireGiant ExePackage docs](https://docs.firegiant.com/wix/schema/wxs/exepackage/), [Sparx Engineering: Burn Payload syntax](https://sparxeng.com/blog/software/wix-burn-bootstrapper-payload-element-syntax).

This is why **Task 1 (Spike A0) is mandatory and blocking**: prove the runtime-discovery failure is gone on a real clean VM before investing in the bundle.

## Decisions that deviate from the design spec (intentional)

1. **One bundle, not two.** The spec referenced the old two-bundle layout (gaming-pc + operator). The current model ships **one** agent MSI for every role (the wizard installs the role app from a bundled payload), so we author **one** bundle: runtime → agent MSI. Operator gets no separate bundle. (YAGNI.)
2. **Runtime pin lives in the build script, not a `RuntimePrereq.wxi`.** Because the runtime is downloaded **at build time** (by PowerShell, to embed it), the version/URL/SHA-512 pin lives next to that download logic in `build-client-packages.ps1`. The bundle just embeds the verified file via `-d RuntimeInstallerPath=…`. One fewer file; the pin sits where it is consumed. The old `.wxi` was a single-source-of-truth for **two** bundles sharing a **download** URL — neither condition holds now.
3. **package-smoke.yml stays MSI-only.** Smoke keeps validating that the component MSIs build (framework-dependent now); the bundle — which requires a ~55 MB runtime download at build time — is produced only by the release path (`client-packages.yml`) and the final clean-VM verification. Keeps smoke fast and avoids a 55 MB download every push.

## Runtime pin (starting values — verify latest servicing release at kickoff)

```
RuntimeVersion = 10.0.9
RuntimeUrl     = https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/10.0.9/windowsdesktop-runtime-10.0.9-win-x64.exe
RuntimeSha512  = 99BC2215D67F8AEA1ECB3DF642423CCABF76E5261B225F0F2D78123D84D58E64923F050A5DC58405C4D5CF074ACBAD32C4A1021A67E94629DCC57206AC4116DE
RuntimeSize    = 59878128
```

These are the exact values from the reverted `RuntimePrereq.wxi`. The .NET 10 Desktop Runtime is a **superset** of the base runtime, so it covers all four components: 3 WPF apps (`AFK4.Operator.App`, `AFK4.Player.Shell`, `AFK4.SetupWizard`, all `net10.0-windows`) need Desktop; `AFK4.Agent.Service` (`net10.0` Worker) needs only base. To move to a newer servicing release, bump `RuntimeVersion`, `RuntimeUrl`, and `RuntimeSha512` together (recompute via `(Get-FileHash -Algorithm SHA512 -LiteralPath <runtime.exe>).Hash`).

## File structure

- `scripts/build-client-packages.ps1` — flip `$projects` to framework-dependent (A1); add runtime download+verify + bundle build + agent-MSI-to-intermediates (A2).
- `installers/bundle/Bundle.wxs` — **new**, single Burn bundle: embedded runtime prereq → agent MSI.
- `docs/operations/client-packaging.md` — rewrite to the carry-the-runtime, single-bundle model (currently describes the reverted download model).
- `installers/README.md` — add the bundle to the component list.
- `tests/AFK4.Agent.Service.Tests/UpdateHelperScriptTests.cs` — update the `SelfContained = $true` assertion; add FD + bundle assertions.
- `tests/AFK4.Agent.Service.Tests/ClientReleaseAutomationTests.cs` — add bundle build-script + extension-restore assertions.

---

## Phase A0 — Spike (BLOCKING gate, manual on a clean VM)

### Task 1: Prove framework-dependent runs on a freshly-imaged VM

This reproduces and clears the exact failure that caused the revert. No automated test — it is a manual gate whose result decides whether A1/A2 proceed.

- [ ] **Step 1: Publish one WPF component framework-dependent**

Run (pwsh7, repo root):

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' publish src/AFK4.Operator.App/AFK4.Operator.App.csproj `
  -c Release -r win-x64 --self-contained false `
  -o artifacts/spike/operator-fd `
  -p:NuGetAudit=false -p:UseSharedCompilation=false
```

Expected: publish succeeds; `artifacts/spike/operator-fd` is a few MB (no `Microsoft.WindowsDesktop.App` runtime copied in), not ~50 MB.

- [ ] **Step 2: Confirm it FAILS without the runtime on a clean VM**

On a freshly-imaged Windows VM **without** any .NET 10 runtime, copy `operator-fd/` over and run `AFK4.Operator.App.exe`.
Expected: it does **not** run (apphost reports the runtime is missing). This confirms the spike is testing the real condition.

- [ ] **Step 3: Hand-install the Desktop Runtime, confirm it now runs**

On the same VM, install the pinned runtime:

```powershell
Start-Process -Wait windowsdesktop-runtime-10.0.9-win-x64.exe -ArgumentList '/install','/quiet','/norestart'
```

Re-run `AFK4.Operator.App.exe`.
Expected: the app launches. **This is the gate** — it proves a framework-dependent AFK4 app resolves the Desktop Runtime once it is installed, i.e. the sc-1053 / "runtime not discoverable" failure is an *ordering* problem the bundle's vital prereq will solve, not an intrinsic FD problem.

- [ ] **Step 4: Record the size delta**

Note self-contained vs framework-dependent publish folder sizes in the PR description. Expected: FD operator publish ≈ 1/10th of self-contained.

- [ ] **Step 5: Decision checkpoint**

If Step 3 passes → proceed to A1. If it fails → STOP; the bundle cannot fix an FD app that can't find an installed runtime, and the epic is blocked pending investigation. Do not delete `artifacts/spike/` until A3 is done.

---

## Phase A1 — Framework-dependent publishing

### Task 2: Flip the four components to framework-dependent

**Files:**
- Modify: `scripts/build-client-packages.ps1:276-279`
- Test: `tests/AFK4.Agent.Service.Tests/UpdateHelperScriptTests.cs:126`

- [ ] **Step 1: Update the failing assertion to expect framework-dependent**

In `tests/AFK4.Agent.Service.Tests/UpdateHelperScriptTests.cs`, replace the line-126 assertion:

```csharp
Assert.Contains("@{ Name = 'operator-app'; Path = 'src/AFK4.Operator.App/AFK4.Operator.App.csproj'; SelfContained = $false }", script, StringComparison.Ordinal);
```

- [ ] **Step 2: Add an assertion that NONE of the four components stays self-contained**

In the same test method (`ClientPackageBuildScript_BuildsStandaloneX64OperatorAppMsi`), after the line above, add:

```csharp
// All four client components must publish framework-dependent so the bundle's shared
// runtime is the single .NET copy (see Workstream A). A stray "SelfContained = $true" in
// the $projects list would re-bloat the MSI back toward 160 MB.
Assert.Contains("@{ Name = 'agent-service'; Path = 'src/AFK4.Agent.Service/AFK4.Agent.Service.csproj'; SelfContained = $false }", script, StringComparison.Ordinal);
Assert.Contains("@{ Name = 'player-shell'; Path = 'src/AFK4.Player.Shell/AFK4.Player.Shell.csproj'; SelfContained = $false }", script, StringComparison.Ordinal);
Assert.Contains("@{ Name = 'setup-wizard'; Path = 'src/AFK4.SetupWizard/AFK4.SetupWizard.csproj'; SelfContained = $false }", script, StringComparison.Ordinal);
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test AFK4.sln --filter "FullyQualifiedName~ClientPackageBuildScript_BuildsStandaloneX64OperatorAppMsi" -p:NuGetAudit=false -p:UseSharedCompilation=false`
Expected: FAIL — the script still has `SelfContained = $true`.

- [ ] **Step 4: Flip the `$projects` list to framework-dependent**

In `scripts/build-client-packages.ps1`, replace lines 276-279:

```powershell
    @{ Name = 'operator-app'; Path = 'src/AFK4.Operator.App/AFK4.Operator.App.csproj'; SelfContained = $false },
    @{ Name = 'agent-service'; Path = 'src/AFK4.Agent.Service/AFK4.Agent.Service.csproj'; SelfContained = $false },
    @{ Name = 'player-shell'; Path = 'src/AFK4.Player.Shell/AFK4.Player.Shell.csproj'; SelfContained = $false },
    @{ Name = 'setup-wizard'; Path = 'src/AFK4.SetupWizard/AFK4.SetupWizard.csproj'; SelfContained = $false }
```

(The `--self-contained $($project.SelfContained...)` on line 297 already reads the flag, so it now emits `--self-contained false`. The legacy bootstrapper's `-p:SelfContained=true` on line 458 is untouched — that single-file recovery exe stays self-contained.)

- [ ] **Step 5: Update the build-script comment that still claims self-contained**

In `scripts/build-client-packages.ps1`, replace the comment at lines 422-424:

```powershell
# Components publish framework-dependent (one shared .NET runtime, carried by the Burn
# bundle — see installers/bundle/Bundle.wxs). The agent MSI is a build input to that bundle;
# it must be installed on a machine where the Desktop Runtime is already present (the bundle
# guarantees that ordering). The agent MSI still auto-launches the Setup Wizard on interactive install.
```

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test AFK4.sln --filter "FullyQualifiedName~ClientPackageBuildScript_BuildsStandaloneX64OperatorAppMsi" -p:NuGetAudit=false -p:UseSharedCompilation=false`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add scripts/build-client-packages.ps1 tests/AFK4.Agent.Service.Tests/UpdateHelperScriptTests.cs
git commit -m "build: publish client components framework-dependent"
```

### Task 3: Verify the framework-dependent MSIs still build and shrink

**Files:** none (verification gate on a dev machine that has the .NET 10 SDK/runtime).

- [ ] **Step 1: Build the packages framework-dependent**

Run (pwsh7):

```powershell
pwsh -File scripts/build-client-packages.ps1 -Version 0.1.0-ci -Channel internal
```

Expected: build succeeds; `artifacts/client-packages/afk4-agent-0.1.0-ci-internal.msi` is produced.

- [ ] **Step 2: Confirm the size dropped**

```powershell
(Get-Item artifacts/client-packages/afk4-agent-0.1.0-ci-internal.msi).Length / 1MB
```

Expected: materially smaller than the ~160 MB self-contained baseline (target band ~70–90 MB once the runtime is de-duplicated; at this stage the MSI still has no runtime at all, so it is even smaller — the runtime is added once by the bundle in A2).

- [ ] **Step 3: Sanity-install on THIS dev machine (already has the runtime)**

Install the agent MSI on the dev machine (which has .NET 10), confirm the service registers and the wizard launches. This is not the clean-VM gate (that is A3) — it only confirms FD MSIs are well-formed.

- [ ] **Step 4: Run the full backend test suite**

Run: `dotnet test AFK4.sln -p:NuGetAudit=false -p:UseSharedCompilation=false`
Expected: green (the FD flip touches only the publish flag + its assertions).

- [ ] **Step 5: Commit (if any incidental fixes were needed)**

```bash
git commit -am "test: confirm framework-dependent client MSIs build green"
```

---

## Phase A2 — Burn bundle that carries the runtime

### Task 4: Author the single Burn bundle

**Files:**
- Create: `installers/bundle/Bundle.wxs`

- [ ] **Step 1: Write the bundle**

Create `installers/bundle/Bundle.wxs`:

```xml
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs"
     xmlns:bal="http://wixtoolset.org/schemas/v4/wxs/bal"
     xmlns:netfx="http://wixtoolset.org/schemas/v4/wxs/netfx">
  <!--
    AFK4.NET client master installer (Burn bundle), one per channel:
    afk4-client-<version>-<channel>.exe.

    Chains the .NET 10 Desktop Runtime (x64) CARRIED INSIDE the bundle (Compressed="yes",
    SourceFile, NO DownloadUrl) as a vital prerequisite, then the agent MSI. Carrying the
    runtime — rather than downloading it — is the deliberate difference from the reverted
    download bundle (commit 20a7a31): the runtime is extracted and installed synchronously
    before the MSI runs, so a freshly-imaged machine never starts the framework-dependent
    agent service against a missing/unregistered runtime (the old sc-1053 failure).

    The pinned runtime version/URL/SHA-512 live in scripts/build-client-packages.ps1, which
    downloads + verifies the runtime at build time and passes its path via RuntimeInstallerPath.
  -->
  <Bundle
      Name="AFK4.NET"
      Manufacturer="AFK4.NET"
      Version="$(var.PackageVersion)"
      Compressed="yes"
      UpgradeCode="{8E2C4A91-7B3D-4F6A-9C1E-2D5A8B0F3471}">

    <BootstrapperApplication>
      <bal:WixStandardBootstrapperApplication Theme="hyperlinkLicense" LicenseUrl="" SuppressOptionsUI="yes" />
    </BootstrapperApplication>

    <netfx:DotNetCoreSearch
        Id="DesktopRuntime10"
        RuntimeType="desktop"
        Platform="x64"
        MajorVersion="10"
        Variable="DesktopRuntime10Version" />

    <Chain>
      <ExePackage
          DetectCondition="DesktopRuntime10Version AND DesktopRuntime10Version &gt;= v$(var.RuntimeVersion)"
          Permanent="yes"
          Vital="yes"
          PerMachine="yes"
          Compressed="yes"
          InstallArguments="/install /quiet /norestart"
          RepairArguments="/repair /quiet /norestart"
          UninstallArguments="/uninstall /quiet /norestart"
          bal:PrereqPackage="yes">
        <ExePackagePayload
            Name="windowsdesktop-runtime-$(var.RuntimeVersion)-win-x64.exe"
            SourceFile="$(var.RuntimeInstallerPath)"
            ProductName=".NET Desktop Runtime $(var.RuntimeVersion) (x64)"
            Description=".NET Desktop Runtime $(var.RuntimeVersion) (x64)" />
      </ExePackage>

      <MsiPackage SourceFile="$(var.AgentMsiPath)" Vital="yes" />
    </Chain>
  </Bundle>
</Wix>
```

Key differences from the reverted bundle: `Bundle Compressed="yes"`, `ExePackage Compressed="yes"`, `ExePackagePayload SourceFile=` (a build-time local path) with **no** `DownloadUrl`/`Hash`/`Size` (Burn hashes the embedded payload itself). `UpgradeCode` is reused from the old gaming-pc bundle so a machine that ever had it upgrades cleanly.

- [ ] **Step 2: There is no unit test for raw WiX XML; correctness is proven by the build (Task 6) and the clean-VM run (Task 9). Commit the bundle source.**

```bash
git add installers/bundle/Bundle.wxs
git commit -m "build: add Burn bundle carrying the .NET 10 Desktop Runtime"
```

### Task 5: Build the bundle from the build script (download+verify runtime, emit `.exe`, move agent MSI to intermediates)

**Files:**
- Modify: `scripts/build-client-packages.ps1` (runtime pin + download helper near the top; bundle build after the agent MSI is built ~line 421; intermediates move ~line 476)

- [ ] **Step 1: Add the assertion for the new build-script behavior (test-first)**

In `tests/AFK4.Agent.Service.Tests/ClientReleaseAutomationTests.cs`, add a new test method (place it after `BuildClientPackagesScript_MapsChannelToPlatformBaseUrl`, ~line 821):

```csharp
[Fact]
public void BuildClientPackagesScript_CarriesRuntimeInBundleExeAndMovesAgentMsiToIntermediates()
{
    var script = NormalizeLineEndings(File.ReadAllText(ScriptPath("scripts/build-client-packages.ps1")));

    // Runtime pin lives next to the build-time download/verify.
    Assert.Contains("$runtimeVersion = '10.0.9'", script, StringComparison.Ordinal);
    Assert.Contains("windowsdesktop-runtime-10.0.9-win-x64.exe", script, StringComparison.Ordinal);
    Assert.Contains("Get-FileHash -Algorithm SHA512", script, StringComparison.Ordinal);
    Assert.Contains("Runtime installer SHA-512 mismatch", script, StringComparison.Ordinal);

    // The WiX Bal/Netfx extensions are required for the bundle.
    Assert.Contains("wix extension add", script, StringComparison.Ordinal);
    Assert.Contains("WixToolset.Bal.wixext", script, StringComparison.Ordinal);
    Assert.Contains("WixToolset.Netfx.wixext", script, StringComparison.Ordinal);

    // The bundle is built from the single Bundle.wxs and carries the runtime + agent MSI.
    Assert.Contains("installers/bundle/Bundle.wxs", script, StringComparison.Ordinal);
    Assert.Contains("RuntimeInstallerPath=", script, StringComparison.Ordinal);
    Assert.Contains("AgentMsiPath=", script, StringComparison.Ordinal);
    Assert.Contains("afk4-client-$Version-$Channel.exe", script, StringComparison.Ordinal);

    // The bundle is the deliverable; the agent MSI becomes a build input in intermediates\.
    var bundleIndex = script.IndexOf("afk4-client-$Version-$Channel.exe", StringComparison.Ordinal);
    var agentToIntermediatesIndex = script.IndexOf("Move-Item -LiteralPath $agentMsiPath -Destination $intermediatesDir", StringComparison.Ordinal);
    Assert.True(agentToIntermediatesIndex > bundleIndex, "Agent MSI must be moved to intermediates only after the bundle that embeds it is built.");
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test AFK4.sln --filter "FullyQualifiedName~CarriesRuntimeInBundleExeAndMovesAgentMsiToIntermediates" -p:NuGetAudit=false -p:UseSharedCompilation=false`
Expected: FAIL — none of the strings exist yet.

- [ ] **Step 3: Add the runtime pin + download/verify helper near the top of the script**

In `scripts/build-client-packages.ps1`, after the `$platformBaseUrl` block (after line 42), add:

```powershell
# Pinned .NET 10 Desktop Runtime (x64) carried inside the Burn bundle. Bump version+url+sha
# together when moving to a newer servicing release; recompute the SHA via
# (Get-FileHash -Algorithm SHA512 -LiteralPath <runtime.exe>).Hash
$runtimeVersion = '10.0.9'
$runtimeUrl = "https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/$runtimeVersion/windowsdesktop-runtime-$runtimeVersion-win-x64.exe"
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
```

- [ ] **Step 4: Add the WiX Bal/Netfx extensions before any WiX build**

In `scripts/build-client-packages.ps1`, immediately before the first `wix build` (before line 352, the player-shell build), add:

```powershell
# The Burn bundle needs the Bal (WixStandardBootstrapperApplication) and Netfx
# (DotNetCoreSearch) extensions. `wix extension add` is idempotent.
& $DotnetPath wix extension add -g WixToolset.Bal.wixext
& $DotnetPath wix extension add -g WixToolset.Netfx.wixext
if ($LASTEXITCODE -ne 0) {
    throw "Adding WiX extensions failed with exit code $LASTEXITCODE."
}
```

- [ ] **Step 5: Build the bundle right after the agent MSI is verified**

In `scripts/build-client-packages.ps1`, after the agent-MSI payload assertions (after line 420, before the legacy gaming-pc block) and replacing the now-stale comment at 422-424, add:

```powershell
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
```

- [ ] **Step 6: Move the agent MSI into intermediates and make the bundle the announced deliverable**

In `scripts/build-client-packages.ps1`, in the intermediates block (lines 474-482), extend the loop to also move the agent MSI, and update the final `Write-Host` deliverable lines (485-487):

Replace lines 476 (`foreach ($bundledMsi in @($operatorMsiPath, $playerShellMsiPath))`) through 482 with:

```powershell
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
        Move-Item -LiteralPath $artifact -Destination $intermediatesDir -Force
    }
}
```

Then replace the deliverable echo at lines 484-487:

```powershell
Write-Host "Published client package inputs under $publishRoot"
Write-Host "Deliverable master installer (install this one):"
Write-Host $clientBundlePath
Write-Host "Bundled inputs (agent/operator/player-shell MSIs) moved to: $intermediatesDir"
```

- [ ] **Step 7: Ensure the runtime cache stays out of git**

Confirm `artifacts/` is already gitignored (it is — `artifacts/client-packages/` is ignored per `installers/README.md`). The `runtime-cache/` and the bundle live under `artifacts/`. No `.gitignore` change needed; verify with `git status` after a build that no `artifacts/**` shows up.

- [ ] **Step 8: Run the build-script assertion test**

Run: `dotnet test AFK4.sln --filter "FullyQualifiedName~CarriesRuntimeInBundleExeAndMovesAgentMsiToIntermediates" -p:NuGetAudit=false -p:UseSharedCompilation=false`
Expected: PASS.

- [ ] **Step 9: Build end-to-end and confirm the deliverable shape**

Run: `pwsh -File scripts/build-client-packages.ps1 -Version 0.1.0-ci -Channel internal`
Expected: `artifacts/client-packages/afk4-client-0.1.0-ci-internal.exe` exists; `afk4-agent-…msi`, `afk4-operator-app-…msi`, `afk4-player-shell-…msi` are all under `artifacts/client-packages/intermediates/`. Note the bundle size (expect ~70–90 MB: one runtime + the FD MSIs).

- [ ] **Step 10: Commit**

```bash
git add scripts/build-client-packages.ps1 tests/AFK4.Agent.Service.Tests/ClientReleaseAutomationTests.cs
git commit -m "build: produce afk4-client master installer carrying the shared runtime"
```

### Task 6: Update the runbook and installer README to the carry-the-runtime model

**Files:**
- Modify: `docs/operations/client-packaging.md`
- Modify: `installers/README.md`

- [ ] **Step 1: Rewrite the Packaging Decision section of the runbook**

In `docs/operations/client-packaging.md`, replace the "Packaging Decision" section (lines 20-42) with text describing: one master installer `afk4-client-<version>-<channel>.exe` (WiX Burn bundle) that **carries** the .NET 10 Desktop Runtime (x64) embedded (extracted and installed if absent, skipped if present), then installs the agent MSI; component MSIs are framework-dependent; the runtime is **carried, never downloaded at install time** (offline-reliable); the pin lives in `scripts/build-client-packages.ps1`. Explicitly note the contrast with the reverted download bundle (commit `20a7a31`) and why carrying fixes the sc-1053 runtime-discovery failure. Update the "Last updated" to 2026-06-11 and the status line to "single client master installer (Burn bundle) carrying the shared runtime; wizard installs role apps".

- [ ] **Step 2: Update the expected-artifact section**

In the same file, update the "Expected master installer" block (lines 172-177) to the single name:

```text
afk4-client-<version>-<channel>.exe
```

and update the surrounding prose that mentions two `setup.exe` installers (gaming-pc + operator) to the single client bundle. Remove the "downloaded from Microsoft if missing" phrasing everywhere (lines 28-29, 36) — it is now carried.

- [ ] **Step 3: Update installers/README.md**

In `installers/README.md`, add to the component list (after line 7):

```markdown
- `bundle` is the WiX Burn master installer (`afk4-client-<version>-<channel>.exe`) that
  carries the .NET 10 Desktop Runtime and chains the `agent` MSI. It is the single
  deliverable; the component MSIs are build inputs moved to `intermediates/`.
```

- [ ] **Step 4: Commit**

```bash
git add docs/operations/client-packaging.md installers/README.md
git commit -m "docs: client packaging runbook for the carry-the-runtime bundle"
```

---

## Phase A3 — Clean-VM verification (BLOCKING gate)

### Task 7: Prove the bundle provisions a freshly-imaged machine end-to-end

This is the gate the reverted attempt failed. No automated test — manual on a clean VM with **no .NET runtime preinstalled**.

> **VM hygiene:** memory notes VM clones may carry a stale (May-17) agent. Start from a genuinely clean image or fully uninstall any prior AFK4 agent + .NET runtime first, otherwise the `DetectCondition` skip-if-present will mask the very thing under test.

- [ ] **Step 1: Build a stable-channel bundle**

Run: `pwsh -File scripts/build-client-packages.ps1 -Version 0.1.0-ci -Channel internal`
Copy `artifacts/client-packages/afk4-client-0.1.0-ci-internal.exe` to the clean VM.

- [ ] **Step 2: Confirm the runtime is absent on the VM**

On the VM: `dotnet --list-runtimes` shows no `Microsoft.WindowsDesktop.App 10.x` (or `dotnet` is not installed at all).

- [ ] **Step 3: Run the bundle**

Run `afk4-client-0.1.0-ci-internal.exe` (interactive). Accept the prereq install.
Expected: the bundle installs the Desktop Runtime first, then the agent MSI; `dotnet --list-runtimes` now lists `Microsoft.WindowsDesktop.App 10.0.9`.

- [ ] **Step 4: Confirm the service starts (the old failure point)**

```powershell
Get-Service AFK4.Agent.Service
```
Expected: status is **not** stopped-with-1053; the service is registered and runs (it stays idle until enrollment writes `%ProgramData%\AFK4\Agent\bootstrap.json`, per the current model). Check `%ProgramData%\AFK4\logs\agent.log` for a clean start with no missing-runtime error.

- [ ] **Step 5: Confirm the wizard auto-launches and runs**

Expected: the Setup Wizard window appears after install (the agent MSI's `LaunchSetupWizard` custom action). It renders its WebView2 UI (framework-dependent wizard resolving the just-installed runtime). If the wizard does **not** auto-launch under the bundle (Burn may run the MSI below `UILevel >= 3`), record it: the fix is to launch the wizard from the bundle after the chain, or relax the MSI custom-action condition — decide then, do not pre-build it.

- [ ] **Step 6: Complete an enrollment against staging**

Sign in, pick `gaming_pc`, confirm the Player Shell installs from the payload and the agent supervises it; or pick `manager_workstation` and confirm the Operator App launches. This reuses the existing provisioning path — it only needs to prove FD apps run under the carried runtime.

- [ ] **Step 7: Record results and the final size in the PR**

Capture: bundle size, runtime-absent→present transition, service status, wizard launch, enrollment success. These are the evidence that A is genuinely done (not a green-on-fake claim).

- [ ] **Step 8: Decision checkpoint**

All steps pass → Workstream A is complete; open the PR. Any step fails → systematic-debugging on that specific failure before merge; do not mark A done.

---

## Self-review notes

- **Spec coverage:** Goal "≤90 MB framework-dependent + one carried runtime" → A1 (Task 2) + A2 (Tasks 4-5). "Carried, never downloaded" → Task 4 (`Compressed`/`SourceFile`, no `DownloadUrl`) + Task 5 (build-time download+verify). "Bundle is an `.exe`, agent MSI to intermediates" → Task 5 Steps 5-6. Runbook honesty → Task 6. Clean-VM proof → Task 7. Workstream B (prod URL) is already shipped (channel→URL map in the build script) and out of scope here. Workstream C (signing) is blocked on a cert and explicitly deferred.
- **Known risks carried into execution:** (1) wizard auto-launch under Burn `UILevel` (Task 7 Step 5 — diagnosed, not pre-fixed); (2) the unsigned `.exe` bootstrapper triggers worse SmartScreen than an `.msi` (Workstream C concern, acceptable for internal/demo); (3) servicing-release drift on the runtime pin (single place to bump, Task 5 Step 3).
- **Not touched:** `package-smoke.yml` (stays MSI-only by decision 3); the legacy gaming-pc MSI and `AFK4.GamingPc.Setup` bootstrapper (still behind their explicit switches, still self-contained).
