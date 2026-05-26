# AFK4 Authenticode CI Registration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add provider-neutral Authenticode signing and optional update package metadata registration to the AFK4 Windows client package release flow.

**Architecture:** Keep `scripts/build-client-packages.ps1`, WiX/MSI authoring, `AFK4.Update.Publisher`, and the backend update registration endpoint as the existing boundaries. Add focused PowerShell scripts for signing ready MSI artifacts, publishing signed update metadata for ready MSI artifacts, and posting generated request JSON to the Platform API. Cover the scripts and GitHub Actions workflow with deterministic xUnit tests that use fake command-line tools and a local HTTP listener, so no production certificate or cloud provider is required.

**Tech Stack:** .NET 10, xUnit, `System.Management.Automation`, PowerShell, Windows `signtool.exe`, GitHub Actions Windows runners, existing `AFK4.Update.Publisher`, existing Platform API update package endpoint.

---

## Scope

This plan implements the approved provider-neutral release hardening slice:

- Authenticode signing script for ready MSI artifacts.
- MSI metadata publishing script that calls `AFK4.Update.Publisher`.
- Backend registration script for generated `CreateUpdatePackageRequest` JSON files.
- Guarded GitHub Actions workflow steps for signing, metadata publishing, request JSON upload, and optional backend registration.
- Runbook, README, and progress updates.

This plan does not procure certificates, add Azure/AWS/GCP signing SDKs, change Agent update verification, change backend package states, or create automatic stable rollout creation.

## Prerequisites

Run from the repository root:

```powershell
cd D:\afk4.net
& 'C:\Program Files\Git\cmd\git.exe' status --short --branch
```

Expected:

```text
## codex/authenticode-ci-registration
```

Use the full .NET path if `dotnet` is not on `PATH`:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' --list-sdks
```

Expected:

```text
10.0.203 [C:\Program Files\dotnet\sdk]
```

## File Structure

Create and modify these files:

```text
D:\afk4.net\
  .github\
    workflows\
      client-packages.yml
  docs\
    operations\
      client-packaging.md
      update-package-publishing.md
    progress\
      2026-05-12-vertical-slice-progress.md
    superpowers\
      plans\
        2026-05-16-afk4-authenticode-ci-registration.md
      specs\
        2026-05-16-afk4-authenticode-ci-registration-design.md
  scripts\
    sign-client-packages.ps1
    publish-client-msi-updates.ps1
    register-update-package-requests.ps1
  tests\
    AFK4.Agent.Service.Tests\
      ClientReleaseAutomationTests.cs
```

Responsibilities:

- `scripts/sign-client-packages.ps1`: signs existing `.msi` artifacts through `signtool.exe`; accepts either a PFX file plus password environment variable or a certificate-store thumbprint.
- `scripts/publish-client-msi-updates.ps1`: maps ready MSI artifacts to existing update components and invokes `AFK4.Update.Publisher` once per generated package request.
- `scripts/register-update-package-requests.ps1`: posts generated request JSON files to the existing backend package registration endpoint with a bearer token supplied directly or via environment variable.
- `.github/workflows/client-packages.yml`: keeps artifact-only package builds working while adding explicit release-mode switches for signing, metadata publishing, and backend registration.
- `tests/AFK4.Agent.Service.Tests/ClientReleaseAutomationTests.cs`: deterministic script and workflow coverage using PowerShell parser checks, fake external executables, and a local HTTP listener.
- Operation docs and progress docs: describe the new release path, safety rules, verification, and remaining production decisions.

## Task 1: Authenticode Signing Script

**Files:**

- Create: `D:\afk4.net\tests\AFK4.Agent.Service.Tests\ClientReleaseAutomationTests.cs`
- Create: `D:\afk4.net\scripts\sign-client-packages.ps1`

- [ ] **Step 1: Write failing tests for the signing script**

Create `tests\AFK4.Agent.Service.Tests\ClientReleaseAutomationTests.cs`:

```csharp
using System.Diagnostics;
using System.Management.Automation.Language;

namespace AFK4.Agent.Service.Tests;

public sealed class ClientReleaseAutomationTests : IDisposable
{
    private readonly string tempRoot = Path.Combine(Path.GetTempPath(), $"afk4-release-automation-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void SignClientPackagesScript_ParsesRequiredParameters()
    {
        var ast = ParseScript("scripts/sign-client-packages.ps1", out var errors);

        Assert.Empty(errors);
        AssertParameter(ast, "PackagePath");
        AssertParameter(ast, "PackageDirectory");
        AssertParameter(ast, "CertificatePath");
        AssertParameter(ast, "CertificatePasswordEnvVar");
        AssertParameter(ast, "CertificateSha1");
        AssertParameter(ast, "TimestampUrl");
        AssertParameter(ast, "SigntoolPath");
    }

    [Fact]
    public void SignClientPackages_WithPfxSource_InvokesSigntoolForExplicitPackage()
    {
        Directory.CreateDirectory(tempRoot);
        var packagePath = Path.Combine(tempRoot, "afk4-operator-app-1.2.3-internal.msi");
        File.WriteAllText(packagePath, "msi");
        var certificatePath = Path.Combine(tempRoot, "release-signing.pfx");
        File.WriteAllText(certificatePath, "pfx");
        var capturedArgumentsPath = Path.Combine(tempRoot, "signtool-args.txt");
        var fakeSigntoolPath = Path.Combine(tempRoot, "fake-signtool.ps1");
        File.WriteAllText(
            fakeSigntoolPath,
            "$args | Set-Content -LiteralPath " + ToPowerShellSingleQuotedLiteral(capturedArgumentsPath) + Environment.NewLine +
            "exit 0" + Environment.NewLine);

        var result = RunPowerShell(
            environment: new Dictionary<string, string?>
            {
                ["AFK4_TEST_PFX_PASSWORD"] = "test-password"
            },
            "-File", ScriptPath("scripts/sign-client-packages.ps1"),
            "-PackagePath", packagePath,
            "-CertificatePath", certificatePath,
            "-CertificatePasswordEnvVar", "AFK4_TEST_PFX_PASSWORD",
            "-TimestampUrl", "http://timestamp.test",
            "-SigntoolPath", fakeSigntoolPath);

        Assert.Equal(0, result.ExitCode);
        var capturedArguments = File.ReadAllLines(capturedArgumentsPath);
        Assert.Contains("sign", capturedArguments);
        Assert.Contains("/fd", capturedArguments);
        Assert.Contains("SHA256", capturedArguments);
        Assert.Contains("/tr", capturedArguments);
        Assert.Contains("http://timestamp.test", capturedArguments);
        Assert.Contains("/f", capturedArguments);
        Assert.Contains(certificatePath, capturedArguments);
        Assert.Contains("/p", capturedArguments);
        Assert.Contains(packagePath, capturedArguments);
    }

    [Fact]
    public void SignClientPackages_WithoutExactlyOneSigningSource_FailsClosed()
    {
        Directory.CreateDirectory(tempRoot);
        var packagePath = Path.Combine(tempRoot, "afk4-operator-app-1.2.3-internal.msi");
        File.WriteAllText(packagePath, "msi");

        var result = RunPowerShell(
            environment: null,
            "-File", ScriptPath("scripts/sign-client-packages.ps1"),
            "-PackagePath", packagePath,
            "-TimestampUrl", "http://timestamp.test",
            "-SigntoolPath", ScriptPath("scripts/sign-client-packages.ps1"));

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Specify exactly one Authenticode signing source", result.StandardError + result.StandardOutput);
    }

    private static ScriptBlockAst ParseScript(string relativePath, out ParseError[] errors)
    {
        var absolutePath = ScriptPath(relativePath);
        var ast = Parser.ParseFile(absolutePath, out _, out errors);
        return ast;
    }

    private static void AssertParameter(ScriptBlockAst ast, string parameterName)
    {
        Assert.NotNull(ast.ParamBlock);
        Assert.Contains(
            ast.ParamBlock.Parameters,
            parameter => string.Equals(parameter.Name.VariablePath.UserPath, parameterName, StringComparison.Ordinal));
    }

    private static ProcessResult RunPowerShell(
        IReadOnlyDictionary<string, string?>? environment,
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("powershell.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (environment is not null)
        {
            foreach (var pair in environment)
            {
                startInfo.Environment[pair.Key] = pair.Value;
            }
        }

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException("PowerShell process did not start.");
        }

        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new ProcessResult(process.ExitCode, standardOutput, standardError);
    }

    private static string ScriptPath(string relativePath)
    {
        return Path.GetFullPath(Path.Combine(GetRepositoryRoot(), relativePath));
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AFK4.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Repository root was not found.");
        }

        return directory.FullName;
    }

    private static string ToPowerShellSingleQuotedLiteral(string value)
    {
        return "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
```

- [ ] **Step 2: Run the signing tests and verify they fail**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Agent.Service.Tests\AFK4.Agent.Service.Tests.csproj --filter FullyQualifiedName~ClientReleaseAutomationTests -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

Expected:

```text
Could not find file ... scripts\sign-client-packages.ps1
```

- [ ] **Step 3: Implement `sign-client-packages.ps1`**

Create `scripts\sign-client-packages.ps1`:

```powershell
param(
    [string[]] $PackagePath,

    [string] $PackageDirectory,

    [string] $CertificatePath,

    [string] $CertificatePasswordEnvVar,

    [string] $CertificateSha1,

    [ValidateSet('CurrentUser', 'LocalMachine')]
    [string] $CertificateStoreLocation = 'CurrentUser',

    [string] $CertificateStoreName = 'My',

    [string] $TimestampUrl = 'http://timestamp.digicert.com',

    [string] $SigntoolPath
)

$ErrorActionPreference = 'Stop'

function Resolve-SigntoolExecutable {
    param(
        [string] $ConfiguredPath
    )

    if (-not [string]::IsNullOrWhiteSpace($ConfiguredPath)) {
        if (-not (Test-Path -LiteralPath $ConfiguredPath)) {
            throw "signtool executable was not found at '$ConfiguredPath'."
        }

        return (Resolve-Path -LiteralPath $ConfiguredPath).Path
    }

    $command = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    throw "signtool.exe was not found. Install the Windows SDK or pass -SigntoolPath."
}

function Resolve-PackageFiles {
    param(
        [string[]] $ExplicitPackagePaths,
        [string] $ConfiguredPackageDirectory
    )

    $explicitPaths = @()
    if ($null -ne $ExplicitPackagePaths) {
        $explicitPaths = @($ExplicitPackagePaths | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    }

    if ($explicitPaths.Count -gt 0 -and -not [string]::IsNullOrWhiteSpace($ConfiguredPackageDirectory)) {
        throw "Specify PackagePath or PackageDirectory, not both."
    }

    if ($explicitPaths.Count -gt 0) {
        $resolved = @()
        foreach ($path in $explicitPaths) {
            if (-not (Test-Path -LiteralPath $path)) {
                throw "Package '$path' was not found."
            }

            if ([System.IO.Path]::GetExtension($path) -ne '.msi') {
                throw "Package '$path' must be an .msi file."
            }

            $resolved += (Resolve-Path -LiteralPath $path).Path
        }

        return $resolved
    }

    $repoRoot = Split-Path -Parent $PSScriptRoot
    if ([string]::IsNullOrWhiteSpace($ConfiguredPackageDirectory)) {
        $ConfiguredPackageDirectory = Join-Path $repoRoot 'artifacts/client-packages'
    }

    if (-not (Test-Path -LiteralPath $ConfiguredPackageDirectory)) {
        throw "Package directory '$ConfiguredPackageDirectory' was not found."
    }

    $packages = @(Get-ChildItem -LiteralPath $ConfiguredPackageDirectory -Filter '*.msi' -File | Sort-Object Name)
    if ($packages.Count -eq 0) {
        throw "Package directory '$ConfiguredPackageDirectory' did not contain MSI artifacts."
    }

    return @($packages | ForEach-Object { $_.FullName })
}

$usingPfx = -not [string]::IsNullOrWhiteSpace($CertificatePath)
$usingCertificateStore = -not [string]::IsNullOrWhiteSpace($CertificateSha1)

if ($usingPfx -eq $usingCertificateStore) {
    throw "Specify exactly one Authenticode signing source: CertificatePath or CertificateSha1."
}

if ([string]::IsNullOrWhiteSpace($TimestampUrl)) {
    throw "TimestampUrl is required."
}

$signArgs = @('sign', '/fd', 'SHA256', '/tr', $TimestampUrl, '/td', 'SHA256')

if ($usingPfx) {
    if (-not (Test-Path -LiteralPath $CertificatePath)) {
        throw "CertificatePath '$CertificatePath' was not found."
    }

    if ([string]::IsNullOrWhiteSpace($CertificatePasswordEnvVar)) {
        throw "CertificatePasswordEnvVar is required when CertificatePath is supplied."
    }

    $certificatePassword = [Environment]::GetEnvironmentVariable($CertificatePasswordEnvVar)
    if ([string]::IsNullOrWhiteSpace($certificatePassword)) {
        throw "Environment variable '$CertificatePasswordEnvVar' must contain the PFX password."
    }

    $signArgs += @('/f', (Resolve-Path -LiteralPath $CertificatePath).Path, '/p', $certificatePassword)
}
else {
    $signArgs += @('/sha1', $CertificateSha1, '/s', $CertificateStoreName)
    if ($CertificateStoreLocation -eq 'LocalMachine') {
        $signArgs += '/sm'
    }
}

$resolvedSigntoolPath = Resolve-SigntoolExecutable $SigntoolPath
$packages = Resolve-PackageFiles $PackagePath $PackageDirectory

foreach ($package in $packages) {
    Write-Host "Signing MSI artifact: $package"
    & $resolvedSigntoolPath @signArgs $package
    if ($LASTEXITCODE -ne 0) {
        throw "signtool failed for '$package' with exit code $LASTEXITCODE."
    }
}
```

- [ ] **Step 4: Run the signing tests and verify they pass**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Agent.Service.Tests\AFK4.Agent.Service.Tests.csproj --filter FullyQualifiedName~ClientReleaseAutomationTests -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

Expected:

```text
Passed!  - Failed:     0
```

- [ ] **Step 5: Commit the signing script**

Run:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add scripts/sign-client-packages.ps1 tests/AFK4.Agent.Service.Tests/ClientReleaseAutomationTests.cs
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat: add client package signing script"
```

Expected:

```text
[codex/authenticode-ci-registration ...] feat: add client package signing script
```

## Task 2: MSI Update Metadata Publishing Script

**Files:**

- Modify: `D:\afk4.net\tests\AFK4.Agent.Service.Tests\ClientReleaseAutomationTests.cs`
- Create: `D:\afk4.net\scripts\publish-client-msi-updates.ps1`

- [ ] **Step 1: Add failing tests for MSI metadata publishing**

Add these tests to `ClientReleaseAutomationTests`:

```csharp
[Fact]
public void PublishClientMsiUpdatesScript_ParsesRequiredParameters()
{
    var ast = ParseScript("scripts/publish-client-msi-updates.ps1", out var errors);

    Assert.Empty(errors);
    AssertParameter(ast, "Version");
    AssertParameter(ast, "Channel");
    AssertParameter(ast, "OrganizationId");
    AssertParameter(ast, "PackageDirectory");
    AssertParameter(ast, "OutputDirectory");
    AssertParameter(ast, "ArtifactStore");
    AssertParameter(ast, "HostingRoot");
    AssertParameter(ast, "PublicBaseUri");
    AssertParameter(ast, "OperatorArtifactUploadUri");
    AssertParameter(ast, "OperatorArtifactPublicUri");
    AssertParameter(ast, "GamingPcArtifactUploadUri");
    AssertParameter(ast, "GamingPcArtifactPublicUri");
    AssertParameter(ast, "SigningKeyPath");
    AssertParameter(ast, "SigningKeyEnvVar");
    AssertParameter(ast, "ReleaseNotes");
    AssertParameter(ast, "DotnetPath");
}

[Fact]
public void PublishClientMsiUpdates_InvokesPublisherForOperatorAgentAndPlayerShell()
{
    Directory.CreateDirectory(tempRoot);
    var packageDirectory = Path.Combine(tempRoot, "client-packages");
    var outputDirectory = Path.Combine(tempRoot, "update-packages");
    Directory.CreateDirectory(packageDirectory);
    Directory.CreateDirectory(outputDirectory);
    var operatorMsi = Path.Combine(packageDirectory, "afk4-operator-app-1.2.3-internal.msi");
    var gamingPcMsi = Path.Combine(packageDirectory, "afk4-gaming-pc-1.2.3-internal.msi");
    File.WriteAllText(operatorMsi, "operator");
    File.WriteAllText(gamingPcMsi, "gaming-pc");
    var fakeDotnetPath = CreateFakeDotnetThatRecordsArguments(Path.Combine(tempRoot, "dotnet-args.log"));
    var signingKeyPath = Path.Combine(tempRoot, "update-signing-key.pem");
    File.WriteAllText(signingKeyPath, "pem");

    var result = RunPowerShell(
        environment: null,
        "-File", ScriptPath("scripts/publish-client-msi-updates.ps1"),
        "-Version", "1.2.3",
        "-Channel", "internal",
        "-OrganizationId", "0c04d6c0-bfa8-4e26-9263-fc0d307d0f08",
        "-PackageDirectory", packageDirectory,
        "-OutputDirectory", outputDirectory,
        "-ArtifactStore", "file-system",
        "-HostingRoot", Path.Combine(tempRoot, "hosted"),
        "-PublicBaseUri", "https://updates.afk4.test/packages/",
        "-SigningKeyPath", signingKeyPath,
        "-ReleaseNotes", "Internal MSI release.",
        "-DotnetPath", fakeDotnetPath);

    Assert.Equal(0, result.ExitCode);
    var dotnetInvocations = File.ReadAllLines(Path.Combine(tempRoot, "dotnet-args.log"));
    Assert.Contains(dotnetInvocations, invocation => invocation.Contains("--component|operator-app", StringComparison.Ordinal));
    Assert.Contains(dotnetInvocations, invocation => invocation.Contains("--component|agent-service", StringComparison.Ordinal));
    Assert.Contains(dotnetInvocations, invocation => invocation.Contains("--component|player-shell", StringComparison.Ordinal));
    Assert.Contains(dotnetInvocations, invocation => invocation.Contains("--artifact|" + operatorMsi, StringComparison.Ordinal));
    Assert.Equal(2, dotnetInvocations.Count(invocation => invocation.Contains("--artifact|" + gamingPcMsi, StringComparison.Ordinal)));
    Assert.Contains(dotnetInvocations, invocation => invocation.Contains("operator-app-1.2.3-internal-request.json", StringComparison.Ordinal));
    Assert.Contains(dotnetInvocations, invocation => invocation.Contains("agent-service-1.2.3-internal-request.json", StringComparison.Ordinal));
    Assert.Contains(dotnetInvocations, invocation => invocation.Contains("player-shell-1.2.3-internal-request.json", StringComparison.Ordinal));
}

private static string CreateFakeDotnetThatRecordsArguments(string capturePath)
{
    var fakeDotnetPath = Path.Combine(Path.GetDirectoryName(capturePath)!, "fake-dotnet.ps1");
    File.WriteAllText(
        fakeDotnetPath,
        "($args -join '|') | Add-Content -LiteralPath " + ToPowerShellSingleQuotedLiteral(capturePath) + Environment.NewLine +
        "exit 0" + Environment.NewLine);
    return fakeDotnetPath;
}
```

- [ ] **Step 2: Run publishing tests and verify failure**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Agent.Service.Tests\AFK4.Agent.Service.Tests.csproj --filter FullyQualifiedName~PublishClientMsiUpdates -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

Expected:

```text
Could not find file ... scripts\publish-client-msi-updates.ps1
```

- [ ] **Step 3: Implement `publish-client-msi-updates.ps1`**

Create `scripts\publish-client-msi-updates.ps1`:

```powershell
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

function Add-SigningKeyArguments {
    param(
        [string[]] $PublisherArgs
    )

    $hasSigningKeyPath = -not [string]::IsNullOrWhiteSpace($SigningKeyPath)
    $hasSigningKeyEnvVar = -not [string]::IsNullOrWhiteSpace($SigningKeyEnvVar)
    if ($hasSigningKeyPath -eq $hasSigningKeyEnvVar) {
        throw "Specify exactly one update metadata signing key source: SigningKeyPath or SigningKeyEnvVar."
    }

    if ($hasSigningKeyPath) {
        if (-not (Test-Path -LiteralPath $SigningKeyPath)) {
            throw "SigningKeyPath '$SigningKeyPath' was not found."
        }

        return $PublisherArgs + @('--signing-key', (Resolve-Path -LiteralPath $SigningKeyPath).Path)
    }

    return $PublisherArgs + @('--signing-key-env-var', $SigningKeyEnvVar)
}

function Add-ArtifactStoreArguments {
    param(
        [string[]] $PublisherArgs,
        [uri] $ArtifactUploadUri,
        [uri] $ArtifactPublicUri
    )

    if ($ArtifactStore -eq 'file-system') {
        if ([string]::IsNullOrWhiteSpace($HostingRoot) -or $null -eq $PublicBaseUri) {
            throw "HostingRoot and PublicBaseUri are required when ArtifactStore is 'file-system'."
        }

        return $PublisherArgs + @('--hosting-root', $HostingRoot, '--public-base-uri', $PublicBaseUri.AbsoluteUri)
    }

    if ($null -eq $ArtifactUploadUri -or $null -eq $ArtifactPublicUri) {
        throw "ArtifactUploadUri and ArtifactPublicUri are required when ArtifactStore is 'http-put'."
    }

    return $PublisherArgs + @(
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

    $publisherArgs = Add-ArtifactStoreArguments $publisherArgs $ArtifactUploadUri $ArtifactPublicUri
    $publisherArgs = Add-SigningKeyArguments $publisherArgs

    & $DotnetPath run --project (Join-Path $repoRoot 'src/AFK4.Update.Publisher/AFK4.Update.Publisher.csproj') -- @publisherArgs
    if ($LASTEXITCODE -ne 0) {
        throw "AFK4.Update.Publisher failed for component '$Component' with exit code $LASTEXITCODE."
    }
}

if (-not (Test-Path -LiteralPath $DotnetPath)) {
    throw "dotnet executable was not found at '$DotnetPath'."
}

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($PackageDirectory)) {
    $PackageDirectory = Join-Path $repoRoot 'artifacts/client-packages'
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot 'artifacts/update-packages'
}

if (-not (Test-Path -LiteralPath $PackageDirectory)) {
    throw "Package directory '$PackageDirectory' was not found."
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$operatorMsi = Join-Path $PackageDirectory "afk4-operator-app-$Version-$Channel.msi"
$gamingPcMsi = Join-Path $PackageDirectory "afk4-gaming-pc-$Version-$Channel.msi"
if (-not (Test-Path -LiteralPath $operatorMsi)) {
    throw "Operator App MSI '$operatorMsi' was not found."
}

if (-not (Test-Path -LiteralPath $gamingPcMsi)) {
    throw "Gaming-PC MSI '$gamingPcMsi' was not found."
}

$requests = @(
    @{
        Component = 'operator-app'
        ArtifactPath = (Resolve-Path -LiteralPath $operatorMsi).Path
        RequestPath = Join-Path $OutputDirectory "operator-app-$Version-$Channel-request.json"
        ArtifactUploadUri = $OperatorArtifactUploadUri
        ArtifactPublicUri = $OperatorArtifactPublicUri
    },
    @{
        Component = 'agent-service'
        ArtifactPath = (Resolve-Path -LiteralPath $gamingPcMsi).Path
        RequestPath = Join-Path $OutputDirectory "agent-service-$Version-$Channel-request.json"
        ArtifactUploadUri = $GamingPcArtifactUploadUri
        ArtifactPublicUri = $GamingPcArtifactPublicUri
    },
    @{
        Component = 'player-shell'
        ArtifactPath = (Resolve-Path -LiteralPath $gamingPcMsi).Path
        RequestPath = Join-Path $OutputDirectory "player-shell-$Version-$Channel-request.json"
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

Write-Host "CreateUpdatePackageRequest files:"
foreach ($request in $requests) {
    Write-Host $request.RequestPath
}
```

- [ ] **Step 4: Run publishing tests and verify pass**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Agent.Service.Tests\AFK4.Agent.Service.Tests.csproj --filter FullyQualifiedName~PublishClientMsiUpdates -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

Expected:

```text
Passed!  - Failed:     0
```

- [ ] **Step 5: Commit the MSI metadata publishing script**

Run:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add scripts/publish-client-msi-updates.ps1 tests/AFK4.Agent.Service.Tests/ClientReleaseAutomationTests.cs
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat: publish msi update metadata"
```

Expected:

```text
[codex/authenticode-ci-registration ...] feat: publish msi update metadata
```

## Task 3: Backend Registration Script

**Files:**

- Modify: `D:\afk4.net\tests\AFK4.Agent.Service.Tests\ClientReleaseAutomationTests.cs`
- Create: `D:\afk4.net\scripts\register-update-package-requests.ps1`

- [ ] **Step 1: Add failing tests for backend registration**

Add these `using` directives to `ClientReleaseAutomationTests.cs`:

```csharp
using System.Net;
using System.Net.Sockets;
using System.Text;
```

Add these tests and helper methods to `ClientReleaseAutomationTests`:

```csharp
[Fact]
public void RegisterUpdatePackageRequestsScript_ParsesRequiredParameters()
{
    var ast = ParseScript("scripts/register-update-package-requests.ps1", out var errors);

    Assert.Empty(errors);
    AssertParameter(ast, "PlatformBaseUrl");
    AssertParameter(ast, "BranchId");
    AssertParameter(ast, "RequestPath");
    AssertParameter(ast, "RequestDirectory");
    AssertParameter(ast, "AccessToken");
    AssertParameter(ast, "AccessTokenEnvVar");
}

[Fact]
public async Task RegisterUpdatePackageRequests_PostsRequestJsonWithBearerToken()
{
    Directory.CreateDirectory(tempRoot);
    var requestPath = Path.Combine(tempRoot, "agent-service-1.2.3-internal-request.json");
    await File.WriteAllTextAsync(requestPath, """{"organizationId":"0c04d6c0-bfa8-4e26-9263-fc0d307d0f08","component":"agent-service"}""");
    var port = GetFreeTcpPort();
    var baseUrl = $"http://127.0.0.1:{port}/";
    using var listener = new HttpListener();
    listener.Prefixes.Add(baseUrl);
    listener.Start();

    var capturedRequestTask = Task.Run(async () =>
    {
        var context = await listener.GetContextAsync();
        using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
        var body = await reader.ReadToEndAsync();
        context.Response.StatusCode = 201;
        var responseBody = Encoding.UTF8.GetBytes("""{"updatePackageId":"4a8f4f55-cc8e-49ce-9f69-98e9db9c8be7"}""");
        await context.Response.OutputStream.WriteAsync(responseBody);
        context.Response.Close();
        var requestPath = context.Request.Url is null
            ? string.Empty
            : context.Request.Url.AbsolutePath;
        var authorization = context.Request.Headers["Authorization"];
        return new CapturedHttpRequest(
            context.Request.HttpMethod,
            requestPath,
            authorization is null ? string.Empty : authorization,
            body);
    });

    var result = RunPowerShell(
        environment: new Dictionary<string, string?>
        {
            ["AFK4_TEST_REGISTRATION_TOKEN"] = "test-token"
        },
        "-File", ScriptPath("scripts/register-update-package-requests.ps1"),
        "-PlatformBaseUrl", baseUrl.TrimEnd('/'),
        "-BranchId", "acfc0212-967f-4d84-94be-9003387b09c2",
        "-RequestPath", requestPath,
        "-AccessTokenEnvVar", "AFK4_TEST_REGISTRATION_TOKEN");

    var capturedRequest = await capturedRequestTask;
    Assert.Equal(0, result.ExitCode);
    Assert.Equal("POST", capturedRequest.Method);
    Assert.Equal("/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/updates/packages", capturedRequest.Path);
    Assert.Equal("Bearer test-token", capturedRequest.Authorization);
    Assert.Contains("\"component\":\"agent-service\"", capturedRequest.Body, StringComparison.Ordinal);
}

private static int GetFreeTcpPort()
{
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    return port;
}

private sealed record CapturedHttpRequest(string Method, string Path, string Authorization, string Body);
```

- [ ] **Step 2: Run registration tests and verify failure**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Agent.Service.Tests\AFK4.Agent.Service.Tests.csproj --filter FullyQualifiedName~RegisterUpdatePackageRequests -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

Expected:

```text
Could not find file ... scripts\register-update-package-requests.ps1
```

- [ ] **Step 3: Implement `register-update-package-requests.ps1`**

Create `scripts\register-update-package-requests.ps1`:

```powershell
param(
    [Parameter(Mandatory = $true)]
    [uri] $PlatformBaseUrl,

    [Parameter(Mandatory = $true)]
    [guid] $BranchId,

    [string[]] $RequestPath,

    [string] $RequestDirectory,

    [string] $AccessToken,

    [string] $AccessTokenEnvVar
)

$ErrorActionPreference = 'Stop'

function Resolve-RequestFiles {
    param(
        [string[]] $ExplicitRequestPaths,
        [string] $DirectoryPath
    )

    $explicitPaths = @()
    if ($null -ne $ExplicitRequestPaths) {
        $explicitPaths = @($ExplicitRequestPaths | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    }

    if ($explicitPaths.Count -gt 0 -and -not [string]::IsNullOrWhiteSpace($DirectoryPath)) {
        throw "Specify RequestPath or RequestDirectory, not both."
    }

    if ($explicitPaths.Count -gt 0) {
        $resolved = @()
        foreach ($path in $explicitPaths) {
            if (-not (Test-Path -LiteralPath $path)) {
                throw "RequestPath '$path' was not found."
            }

            $resolved += (Resolve-Path -LiteralPath $path).Path
        }

        return $resolved
    }

    if ([string]::IsNullOrWhiteSpace($DirectoryPath)) {
        throw "Specify RequestPath or RequestDirectory."
    }

    if (-not (Test-Path -LiteralPath $DirectoryPath)) {
        throw "RequestDirectory '$DirectoryPath' was not found."
    }

    $files = @(Get-ChildItem -LiteralPath $DirectoryPath -Filter '*-request.json' -File | Sort-Object Name)
    if ($files.Count -eq 0) {
        throw "RequestDirectory '$DirectoryPath' did not contain *-request.json files."
    }

    return @($files | ForEach-Object { $_.FullName })
}

$hasDirectToken = -not [string]::IsNullOrWhiteSpace($AccessToken)
$hasTokenEnvVar = -not [string]::IsNullOrWhiteSpace($AccessTokenEnvVar)
if ($hasDirectToken -eq $hasTokenEnvVar) {
    throw "Specify exactly one access token source: AccessToken or AccessTokenEnvVar."
}

if ($hasTokenEnvVar) {
    $AccessToken = [Environment]::GetEnvironmentVariable($AccessTokenEnvVar)
    if ([string]::IsNullOrWhiteSpace($AccessToken)) {
        throw "Environment variable '$AccessTokenEnvVar' must contain the update package registration access token."
    }
}

$requestFiles = Resolve-RequestFiles $RequestPath $RequestDirectory
$baseUri = $PlatformBaseUrl.AbsoluteUri.TrimEnd('/')
$registrationUri = "$baseUri/api/branches/$($BranchId.ToString('D'))/updates/packages"
$headers = @{ Authorization = "Bearer $AccessToken" }

foreach ($requestFile in $requestFiles) {
    $body = Get-Content -LiteralPath $requestFile -Raw
    Invoke-RestMethod -Method Post -Uri $registrationUri -Headers $headers -ContentType 'application/json' -Body $body | Out-Null
    Write-Host "Registered update package request: $requestFile"
}
```

- [ ] **Step 4: Run registration tests and verify pass**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Agent.Service.Tests\AFK4.Agent.Service.Tests.csproj --filter FullyQualifiedName~RegisterUpdatePackageRequests -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

Expected:

```text
Passed!  - Failed:     0
```

- [ ] **Step 5: Commit the backend registration script**

Run:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add scripts/register-update-package-requests.ps1 tests/AFK4.Agent.Service.Tests/ClientReleaseAutomationTests.cs
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat: add update package registration script"
```

Expected:

```text
[codex/authenticode-ci-registration ...] feat: add update package registration script
```

## Task 4: GitHub Actions Release Workflow Guards

**Files:**

- Modify: `D:\afk4.net\tests\AFK4.Agent.Service.Tests\ClientReleaseAutomationTests.cs`
- Modify: `D:\afk4.net\.github\workflows\client-packages.yml`

- [ ] **Step 1: Add failing workflow text test**

Add this test to `ClientReleaseAutomationTests`:

```csharp
[Fact]
public void ClientPackagesWorkflow_ContainsGuardedSigningPublishingAndRegistrationSteps()
{
    var workflow = File.ReadAllText(ScriptPath(".github/workflows/client-packages.yml"));

    Assert.Contains("sign_packages:", workflow, StringComparison.Ordinal);
    Assert.Contains("publish_update_metadata:", workflow, StringComparison.Ordinal);
    Assert.Contains("register_update_packages:", workflow, StringComparison.Ordinal);
    Assert.Contains("platform_base_url:", workflow, StringComparison.Ordinal);
    Assert.Contains("Stable releases require signing and update metadata publishing.", workflow, StringComparison.Ordinal);
    Assert.Contains("scripts/sign-client-packages.ps1", workflow, StringComparison.Ordinal);
    Assert.Contains("scripts/publish-client-msi-updates.ps1", workflow, StringComparison.Ordinal);
    Assert.Contains("scripts/register-update-package-requests.ps1", workflow, StringComparison.Ordinal);
    Assert.Contains("AFK4_AUTHENTICODE_PFX_BASE64", workflow, StringComparison.Ordinal);
    Assert.Contains("AFK4_UPDATE_SIGNING_KEY_PEM", workflow, StringComparison.Ordinal);
    Assert.Contains("AFK4_UPDATE_REGISTRATION_TOKEN", workflow, StringComparison.Ordinal);
    Assert.Contains("artifacts/update-packages/*-request.json", workflow, StringComparison.Ordinal);
}
```

- [ ] **Step 2: Run the workflow test and verify failure**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Agent.Service.Tests\AFK4.Agent.Service.Tests.csproj --filter FullyQualifiedName~ClientPackagesWorkflow -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

Expected:

```text
Assert.Contains() Failure
```

- [ ] **Step 3: Replace `client-packages.yml` with guarded release inputs and steps**

Replace `.github\workflows\client-packages.yml`:

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
      sign_packages:
        description: Authenticode-sign generated MSI artifacts
        required: true
        default: false
        type: boolean
      publish_update_metadata:
        description: Publish signed update metadata for generated MSI artifacts
        required: true
        default: false
        type: boolean
      register_update_packages:
        description: Register generated update package request JSON with Platform API
        required: true
        default: false
        type: boolean
      organization_id:
        description: Organization id for update package metadata
        required: false
        type: string
      branch_id:
        description: Branch id for backend update package registration
        required: false
        type: string
      platform_base_url:
        description: Platform API base URL used only when register_update_packages is true
        required: false
        type: string
      artifact_store:
        description: Update artifact store mode
        required: true
        default: file-system
        type: choice
        options:
          - file-system
          - http-put
      hosting_root:
        description: File-system hosting root used when artifact_store is file-system
        required: false
        type: string
      public_base_uri:
        description: Public base URI used when artifact_store is file-system
        required: false
        type: string
      operator_artifact_upload_uri:
        description: Presigned upload URI for the Operator App MSI
        required: false
        type: string
      operator_artifact_public_uri:
        description: Public download URI for the Operator App MSI
        required: false
        type: string
      gaming_pc_artifact_upload_uri:
        description: Presigned upload URI for the coordinated gaming-PC MSI
        required: false
        type: string
      gaming_pc_artifact_public_uri:
        description: Public download URI for the coordinated gaming-PC MSI
        required: false
        type: string
      release_notes:
        description: Release notes embedded in signed update package metadata
        required: false
        type: string

jobs:
  build-client-packages:
    runs-on: windows-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Guard release mode
        shell: pwsh
        run: |
          if ('${{ inputs.channel }}' -eq 'stable' -and ('${{ inputs.sign_packages }}' -ne 'true' -or '${{ inputs.publish_update_metadata }}' -ne 'true')) {
            throw "Stable releases require signing and update metadata publishing."
          }

          if ('${{ inputs.register_update_packages }}' -eq 'true' -and '${{ inputs.publish_update_metadata }}' -ne 'true') {
            throw "Backend registration requires publish_update_metadata=true."
          }

          if ('${{ inputs.publish_update_metadata }}' -eq 'true' -and [string]::IsNullOrWhiteSpace('${{ inputs.organization_id }}')) {
            throw "organization_id is required when publish_update_metadata=true."
          }

          if ('${{ inputs.publish_update_metadata }}' -eq 'true' -and [string]::IsNullOrWhiteSpace('${{ inputs.release_notes }}')) {
            throw "release_notes is required when publish_update_metadata=true."
          }

          if ('${{ inputs.publish_update_metadata }}' -eq 'true' -and '${{ inputs.artifact_store }}' -eq 'file-system' -and ([string]::IsNullOrWhiteSpace('${{ inputs.hosting_root }}') -or [string]::IsNullOrWhiteSpace('${{ inputs.public_base_uri }}'))) {
            throw "hosting_root and public_base_uri are required when artifact_store=file-system."
          }

          if ('${{ inputs.publish_update_metadata }}' -eq 'true' -and '${{ inputs.artifact_store }}' -eq 'http-put' -and ([string]::IsNullOrWhiteSpace('${{ inputs.operator_artifact_upload_uri }}') -or [string]::IsNullOrWhiteSpace('${{ inputs.operator_artifact_public_uri }}') -or [string]::IsNullOrWhiteSpace('${{ inputs.gaming_pc_artifact_upload_uri }}') -or [string]::IsNullOrWhiteSpace('${{ inputs.gaming_pc_artifact_public_uri }}'))) {
            throw "operator and gaming-PC upload/public URIs are required when artifact_store=http-put."
          }

          if ('${{ inputs.register_update_packages }}' -eq 'true' -and ([string]::IsNullOrWhiteSpace('${{ inputs.platform_base_url }}') -or [string]::IsNullOrWhiteSpace('${{ inputs.branch_id }}'))) {
            throw "platform_base_url and branch_id are required when register_update_packages=true."
          }

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

      - name: Prepare Authenticode certificate
        if: ${{ inputs.sign_packages }}
        shell: pwsh
        env:
          AFK4_AUTHENTICODE_PFX_BASE64: ${{ secrets.AFK4_AUTHENTICODE_PFX_BASE64 }}
        run: |
          if ([string]::IsNullOrWhiteSpace($env:AFK4_AUTHENTICODE_PFX_BASE64)) {
            throw "AFK4_AUTHENTICODE_PFX_BASE64 secret is required when sign_packages=true."
          }

          $certificatePath = Join-Path $env:RUNNER_TEMP 'afk4-authenticode-signing.pfx'
          [System.IO.File]::WriteAllBytes($certificatePath, [Convert]::FromBase64String($env:AFK4_AUTHENTICODE_PFX_BASE64))
          "AFK4_AUTHENTICODE_CERTIFICATE_PATH=$certificatePath" | Out-File -FilePath $env:GITHUB_ENV -Append

      - name: Sign client packages
        if: ${{ inputs.sign_packages }}
        shell: pwsh
        env:
          AFK4_AUTHENTICODE_PFX_PASSWORD: ${{ secrets.AFK4_AUTHENTICODE_PFX_PASSWORD }}
        run: |
          powershell -ExecutionPolicy Bypass -File scripts/sign-client-packages.ps1 `
            -PackageDirectory artifacts/client-packages `
            -CertificatePath "$env:AFK4_AUTHENTICODE_CERTIFICATE_PATH" `
            -CertificatePasswordEnvVar AFK4_AUTHENTICODE_PFX_PASSWORD

      - name: Publish update metadata
        if: ${{ inputs.publish_update_metadata }}
        shell: pwsh
        env:
          AFK4_UPDATE_SIGNING_KEY_PEM: ${{ secrets.AFK4_UPDATE_SIGNING_KEY_PEM }}
        run: |
          $arguments = @(
            '-Version', '${{ inputs.version }}',
            '-Channel', '${{ inputs.channel }}',
            '-OrganizationId', '${{ inputs.organization_id }}',
            '-PackageDirectory', 'artifacts/client-packages',
            '-OutputDirectory', 'artifacts/update-packages',
            '-ArtifactStore', '${{ inputs.artifact_store }}',
            '-SigningKeyEnvVar', 'AFK4_UPDATE_SIGNING_KEY_PEM',
            '-ReleaseNotes', '${{ inputs.release_notes }}')

          if ('${{ inputs.artifact_store }}' -eq 'file-system') {
            $arguments += @('-HostingRoot', '${{ inputs.hosting_root }}')
            $arguments += @('-PublicBaseUri', '${{ inputs.public_base_uri }}')
          }
          else {
            $arguments += @('-OperatorArtifactUploadUri', '${{ inputs.operator_artifact_upload_uri }}')
            $arguments += @('-OperatorArtifactPublicUri', '${{ inputs.operator_artifact_public_uri }}')
            $arguments += @('-GamingPcArtifactUploadUri', '${{ inputs.gaming_pc_artifact_upload_uri }}')
            $arguments += @('-GamingPcArtifactPublicUri', '${{ inputs.gaming_pc_artifact_public_uri }}')
          }

          powershell -ExecutionPolicy Bypass -File scripts/publish-client-msi-updates.ps1 @arguments

      - name: Upload update package requests
        if: ${{ inputs.publish_update_metadata }}
        uses: actions/upload-artifact@v4
        with:
          name: afk4-update-package-requests-${{ inputs.version }}-${{ inputs.channel }}
          path: artifacts/update-packages/*-request.json

      - name: Register update packages
        if: ${{ inputs.register_update_packages }}
        shell: pwsh
        env:
          AFK4_UPDATE_REGISTRATION_TOKEN: ${{ secrets.AFK4_UPDATE_REGISTRATION_TOKEN }}
        run: |
          powershell -ExecutionPolicy Bypass -File scripts/register-update-package-requests.ps1 `
            -PlatformBaseUrl "${{ inputs.platform_base_url }}" `
            -BranchId "${{ inputs.branch_id }}" `
            -RequestDirectory artifacts/update-packages `
            -AccessTokenEnvVar AFK4_UPDATE_REGISTRATION_TOKEN

      - name: Upload client packages
        uses: actions/upload-artifact@v4
        with:
          name: afk4-client-packages-${{ inputs.version }}-${{ inputs.channel }}
          path: artifacts/client-packages/*.msi
```

- [ ] **Step 4: Run workflow test and verify pass**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Agent.Service.Tests\AFK4.Agent.Service.Tests.csproj --filter FullyQualifiedName~ClientPackagesWorkflow -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

Expected:

```text
Passed!  - Failed:     0
```

- [ ] **Step 5: Commit workflow guards**

Run:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add .github/workflows/client-packages.yml tests/AFK4.Agent.Service.Tests/ClientReleaseAutomationTests.cs
& 'C:\Program Files\Git\cmd\git.exe' commit -m "ci: add signed client package release steps"
```

Expected:

```text
[codex/authenticode-ci-registration ...] ci: add signed client package release steps
```

## Task 5: Release Runbook Updates

**Files:**

- Modify: `D:\afk4.net\docs\operations\client-packaging.md`
- Modify: `D:\afk4.net\docs\operations\update-package-publishing.md`
- Modify: `D:\afk4.net\README.md`
- Modify: `D:\afk4.net\docs\progress\2026-05-12-vertical-slice-progress.md`

- [ ] **Step 1: Update client packaging runbook**

Add this section to `docs\operations\client-packaging.md` after the current MSI artifact names section:

````markdown
## Authenticode Signing

Internal package builds may remain unsigned. Stable production package builds
must be Authenticode-signed before update metadata is published.

Sign ready MSI artifacts with a PFX supplied outside the repository:

```powershell
$env:AFK4_AUTHENTICODE_PFX_PASSWORD = '<supplied by release environment>'

powershell -ExecutionPolicy Bypass -File scripts/sign-client-packages.ps1 `
  -PackageDirectory artifacts/client-packages `
  -CertificatePath C:\afk4-secrets\afk4-authenticode.pfx `
  -CertificatePasswordEnvVar AFK4_AUTHENTICODE_PFX_PASSWORD
```

Sign with a certificate already installed on the release runner:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/sign-client-packages.ps1 `
  -PackageDirectory artifacts/client-packages `
  -CertificateSha1 '<certificate-thumbprint>' `
  -CertificateStoreLocation LocalMachine `
  -CertificateStoreName My
```

The script uses `signtool.exe` and fails when no signing source is configured.
It does not download certificates or read secrets from repository files.
````

- [ ] **Step 2: Update update package publishing runbook**

Add this section to `docs\operations\update-package-publishing.md` before `## Register The Package`:

````markdown
## Publish Ready MSI Packages

After `scripts/build-client-packages.ps1` has created MSI artifacts, publish
signed update metadata without republishing the projects:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/publish-client-msi-updates.ps1 `
  -Version 1.2.3 `
  -Channel internal `
  -OrganizationId 0c04d6c0-bfa8-4e26-9263-fc0d307d0f08 `
  -PackageDirectory artifacts/client-packages `
  -OutputDirectory artifacts/update-packages `
  -ArtifactStore file-system `
  -HostingRoot C:\afk4-updates `
  -PublicBaseUri https://updates.afk4.test/packages/ `
  -SigningKeyPath C:\afk4-secrets\update-signing-key.pem `
  -ReleaseNotes "Internal MSI validation build."
```

The Operator App MSI generates one request JSON for `operator-app`. The
coordinated gaming-PC MSI generates two request JSON files, one for
`agent-service` and one for `player-shell`, both pointing at the same MSI
artifact.

For production-style object storage/CDN publishing:

```powershell
$env:AFK4_UPDATE_SIGNING_KEY_PEM = '<PEM supplied by release environment>'

powershell -ExecutionPolicy Bypass -File scripts/publish-client-msi-updates.ps1 `
  -Version 1.2.3 `
  -Channel stable `
  -OrganizationId 0c04d6c0-bfa8-4e26-9263-fc0d307d0f08 `
  -ArtifactStore http-put `
  -OperatorArtifactUploadUri "https://storage-provider.example/operator-upload-token" `
  -OperatorArtifactPublicUri "https://cdn.afk4.example/operator-app/stable/1.2.3/afk4-operator-app-1.2.3-stable.msi" `
  -GamingPcArtifactUploadUri "https://storage-provider.example/gaming-pc-upload-token" `
  -GamingPcArtifactPublicUri "https://cdn.afk4.example/gaming-pc/stable/1.2.3/afk4-gaming-pc-1.2.3-stable.msi" `
  -SigningKeyEnvVar AFK4_UPDATE_SIGNING_KEY_PEM `
  -ReleaseNotes "Stable Windows client release."
```
````

Extend the existing `## Register The Package` section with:

````markdown
Register generated request JSON files with a staff access token that has
`updates.packages.manage`:

```powershell
$env:AFK4_UPDATE_REGISTRATION_TOKEN = '<short-lived staff access token>'

powershell -ExecutionPolicy Bypass -File scripts/register-update-package-requests.ps1 `
  -PlatformBaseUrl https://platform.afk4.example `
  -BranchId acfc0212-967f-4d84-94be-9003387b09c2 `
  -RequestDirectory artifacts/update-packages `
  -AccessTokenEnvVar AFK4_UPDATE_REGISTRATION_TOKEN
```

Registration leaves package state as `registered`. A human or Operator App
workflow still validates packages and creates rollouts.
````

- [ ] **Step 3: Update README release command summary**

In `README.md`, extend the client packaging paragraph with:

```markdown
For signed release jobs, run `scripts/sign-client-packages.ps1` against the
generated MSI artifacts, then run `scripts/publish-client-msi-updates.ps1` to
create signed update package request JSON, and optionally
`scripts/register-update-package-requests.ps1` to register those requests with
the Platform API. Secrets, certificates, presigned upload URLs, generated
request JSON, and MSI artifacts stay outside source control or under ignored
`artifacts/`.
```

- [ ] **Step 4: Update progress document**

In `docs\progress\2026-05-12-vertical-slice-progress.md`, add the plan to the implementation plan list:

```markdown
- `docs/superpowers/plans/2026-05-16-afk4-authenticode-ci-registration.md`
```

Add a progress section near the current update publishing and Phase 13 sections:

```markdown
## Authenticode CI Registration Slice

Planned on `codex/authenticode-ci-registration` after the heartbeat lease
refresh follow-up and Phase 13 client packaging:

- provider-neutral Authenticode signing for ready MSI artifacts through
  `signtool.exe`;
- MSI update metadata publishing through the existing `AFK4.Update.Publisher`;
- backend registration of generated request JSON through the existing
  `POST /api/branches/{branchId}/updates/packages` endpoint;
- guarded GitHub Actions workflow switches for artifact-only, signed, and
  release-registration package runs.

Remaining production decisions stay outside this slice: final certificate
authority, certificate storage policy, object-store/CDN provider, presigned URL
automation, and whether package registration later uses a service credential
instead of a short-lived staff token.
```

- [ ] **Step 5: Commit docs updates**

Run:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add README.md docs/operations/client-packaging.md docs/operations/update-package-publishing.md docs/progress/2026-05-12-vertical-slice-progress.md
& 'C:\Program Files\Git\cmd\git.exe' commit -m "docs: document signed client package release flow"
```

Expected:

```text
[codex/authenticode-ci-registration ...] docs: document signed client package release flow
```

## Task 6: Verification And Branch Readiness

**Files:**

- Modify: none unless verification exposes a concrete compile, script, or workflow issue.

- [ ] **Step 1: Run targeted release automation tests**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Agent.Service.Tests\AFK4.Agent.Service.Tests.csproj --filter FullyQualifiedName~ClientReleaseAutomationTests -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

Expected:

```text
Passed!  - Failed:     0
```

- [ ] **Step 2: Parse all release PowerShell scripts**

Run:

```powershell
powershell -NoProfile -Command "[System.Management.Automation.Language.Parser]::ParseFile((Resolve-Path scripts/sign-client-packages.ps1), [ref] `$null, [ref] `$null) | Out-Null"
powershell -NoProfile -Command "[System.Management.Automation.Language.Parser]::ParseFile((Resolve-Path scripts/publish-client-msi-updates.ps1), [ref] `$null, [ref] `$null) | Out-Null"
powershell -NoProfile -Command "[System.Management.Automation.Language.Parser]::ParseFile((Resolve-Path scripts/register-update-package-requests.ps1), [ref] `$null, [ref] `$null) | Out-Null"
```

Expected:

```text
no output
```

- [ ] **Step 3: Run full build**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

Expected:

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

- [ ] **Step 4: Run full test suite**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

Expected:

```text
Passed!  - Failed:     0
```

- [ ] **Step 5: Run local package build smoke**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' tool restore
powershell -ExecutionPolicy Bypass -File scripts/build-client-packages.ps1 -Version 0.1.0-ci -Channel internal
Get-ChildItem artifacts\client-packages\*.msi | Select-Object Name,Length
```

Expected:

```text
afk4-operator-app-0.1.0-ci-internal.msi
afk4-gaming-pc-0.1.0-ci-internal.msi
```

- [ ] **Step 6: Run git hygiene checks**

Run:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' diff --check
& 'C:\Program Files\Git\cmd\git.exe' status --short
```

Expected after all planned commits:

```text
no output from git diff --check
no output from git status --short
```

- [ ] **Step 7: Push branch and open PR after implementation is green**

Run:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' push -u origin codex/authenticode-ci-registration
```

Expected:

```text
branch 'codex/authenticode-ci-registration' set up to track 'origin/codex/authenticode-ci-registration'
```

Open the PR with the repository's GitHub workflow or `gh` if available:

```powershell
gh pr create --base main --head codex/authenticode-ci-registration --title "Add Authenticode CI update registration flow" --body "Adds provider-neutral MSI signing, update metadata publishing, backend registration scripting, CI guards, and release docs."
```

Expected:

```text
https://github.com/MubiZero/afk4.net/pull/<number>
```

## Plan Self-Review

Spec coverage:

- Authenticode signing entrypoint is covered by Task 1.
- Provider-neutral `signtool.exe` use with PFX or certificate-store selection is covered by Task 1.
- MSI metadata publishing through existing `AFK4.Update.Publisher` is covered by Task 2.
- Existing component vocabulary for Operator App, Agent Service, and Player Shell is covered by Task 2.
- Backend request JSON registration is covered by Task 3.
- Guarded GitHub Actions signing, publishing, registration, stable-channel checks, and request artifact upload are covered by Task 4.
- Runbook, README, and progress updates are covered by Task 5.
- Deterministic verification without real production signing infrastructure is covered by Tasks 1 through 4 and Task 6.

Deferred production decisions:

- Final Authenticode certificate authority and storage policy.
- Provider-specific signing SDKs or key-vault integrations.
- Object-store/CDN provider provisioning and presigned URL generation.
- Dedicated backend service credential flow for package registration.
- Automatic rollout creation after package registration.
