# Setup Wizard Shell Provisioning + Installer Slimming — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the Setup Wizard the single gaming-PC provisioning tool — on the `gaming_pc` role it installs the bundled Player Shell and brings it up — and slim every installer by switching to framework-dependent publishing behind a master installer that fetches the .NET runtime once.

**Architecture:** New testable types in `AFK4.SetupWizard.Core` install the bundled Player Shell MSI via `msiexec` and resolve its path next to the wizard exe. The WebView2 host bridge runs provisioning during enrollment for `gaming_pc` (and exposes a retry message); the finish screen shows a status line and an honest error with retry. The build script bundles the Player Shell MSI into the wizard payload. Separately, all four components publish framework-dependent, and WiX Burn bundles (`setup.exe`) chain a downloadable .NET Desktop Runtime prerequisite + the component MSI.

**Tech Stack:** C# / .NET 10, xUnit, WPF + WebView2, React + `@afk4/i18n` (bun test), WiX 7 (MSI + Burn bundle), PowerShell build script.

**Spec:** `docs/superpowers/specs/2026-06-10-setup-wizard-shell-provisioning-design.md`

---

## File Structure

New (Core — testable, no WPF):
- `src/AFK4.SetupWizard.Core/ShellProvisioning.cs` — `ShellProvisionStatus`, `ShellProvisionResult`, `ISetupWizardShellProvisioner`, `IProcessRunner`, `ProcessRunResult`.
- `src/AFK4.SetupWizard.Core/MsiexecPlayerShellProvisioner.cs` — runs `msiexec`, maps exit codes.
- `src/AFK4.SetupWizard.Core/SetupWizardPayloadResolver.cs` — resolves the bundled MSI path.
- `src/AFK4.SetupWizard.Core/SystemProcessRunner.cs` — default `IProcessRunner` over `System.Diagnostics.Process`.

New tests:
- `tests/AFK4.SetupWizard.Tests/MsiexecPlayerShellProvisionerTests.cs`
- `tests/AFK4.SetupWizard.Tests/SetupWizardPayloadResolverTests.cs`

Modify (WPF wiring):
- `src/AFK4.SetupWizard/Web/SetupWizardWebHostBridge.cs` — inject provisioner; provision on `gaming_pc` enroll; add `wizard:provisionShell`; add result fields.
- `src/AFK4.SetupWizard/App.xaml.cs` — construct provisioner + resolver.
- `src/AFK4.SetupWizard/Preview/PreviewSetupWizard.cs` — preview no-op provisioner.

Modify (web):
- `src/AFK4.SetupWizard.Web/src/wizardApi.ts` — extend `WizardEnrollResult`; add `provisionShell()`.
- `src/AFK4.SetupWizard.Web/src/FinishedScreen.tsx` — shell status line + retry.
- `locales/{ru,en,tg}.json` — new finish-screen shell keys.

Modify (packaging):
- `scripts/build-client-packages.ps1` — build Player Shell MSI before agent; copy into wizard payload; switch publishes to framework-dependent; build Burn bundles.
- `installers/agent/Package.wxs` — already harvests `SetupWizardSupportDir\**` recursively (no change needed; payload rides along).
- `installers/bootstrappers/gaming-pc/Bundle.wxs` — NEW Burn bundle (runtime + agent MSI).
- `installers/bootstrappers/operator/Bundle.wxs` — NEW Burn bundle (runtime + operator MSI).

Modify (docs):
- `docs/operations/client-packaging.md` — rewrite to the wizard-as-single-tool + framework-dependent + master-installer model.

---

## Phase 1 — Core shell-provisioning types (testable)

### Task 1: Provisioning result + interfaces

**Files:**
- Create: `src/AFK4.SetupWizard.Core/ShellProvisioning.cs`

- [ ] **Step 1: Create the types**

```csharp
namespace AFK4.SetupWizard.Core;

public enum ShellProvisionStatus
{
    Installed,
    AlreadyPresent,
    Failed
}

public sealed record ShellProvisionResult(ShellProvisionStatus Status, int? ExitCode, string? Message)
{
    public static ShellProvisionResult Installed(int exitCode) => new(ShellProvisionStatus.Installed, exitCode, null);

    public static ShellProvisionResult AlreadyPresent(int exitCode) => new(ShellProvisionStatus.AlreadyPresent, exitCode, null);

    public static ShellProvisionResult Failed(int? exitCode, string? message) => new(ShellProvisionStatus.Failed, exitCode, message);
}

public interface ISetupWizardShellProvisioner
{
    ShellProvisionResult Provision();
}

public sealed record ProcessRunResult(int ExitCode, string Output);

public interface IProcessRunner
{
    ProcessRunResult Run(string fileName, IReadOnlyList<string> arguments);
}
```

- [ ] **Step 2: Build the Core project**

Run: `& "C:\Program Files\dotnet\dotnet.exe" build src/AFK4.SetupWizard.Core/AFK4.SetupWizard.Core.csproj -c Debug`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/AFK4.SetupWizard.Core/ShellProvisioning.cs
git commit -m "feat(setup-wizard): shell provisioning result + interfaces"
```

### Task 2: MsiexecPlayerShellProvisioner (exit-code mapping)

**Files:**
- Create: `src/AFK4.SetupWizard.Core/SetupWizardPayloadResolver.cs` (path resolver, used by the provisioner)
- Create: `src/AFK4.SetupWizard.Core/MsiexecPlayerShellProvisioner.cs`
- Test: `tests/AFK4.SetupWizard.Tests/MsiexecPlayerShellProvisionerTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using AFK4.SetupWizard.Core;

namespace AFK4.SetupWizard.Tests;

public sealed class MsiexecPlayerShellProvisionerTests
{
    private const string MsiPath = @"C:\Program Files\AFK4\Setup Wizard\payload\AFK4.Player.Shell.msi";

    private sealed class FakeProcessRunner(int exitCode, string output) : IProcessRunner
    {
        public string? CapturedFileName { get; private set; }
        public IReadOnlyList<string>? CapturedArguments { get; private set; }

        public ProcessRunResult Run(string fileName, IReadOnlyList<string> arguments)
        {
            CapturedFileName = fileName;
            CapturedArguments = arguments;
            return new ProcessRunResult(exitCode, output);
        }
    }

    private static MsiexecPlayerShellProvisioner Create(IProcessRunner runner) =>
        new(new SetupWizardPayloadResolver(_ => true, () => MsiPath), runner);

    [Theory]
    [InlineData(0, ShellProvisionStatus.Installed)]
    [InlineData(3010, ShellProvisionStatus.Installed)]
    [InlineData(1638, ShellProvisionStatus.AlreadyPresent)]
    [InlineData(1603, ShellProvisionStatus.Failed)]
    public void Provision_MapsMsiexecExitCodes(int exitCode, ShellProvisionStatus expected)
    {
        var provisioner = Create(new FakeProcessRunner(exitCode, "msiexec output"));

        var result = provisioner.Provision();

        Assert.Equal(expected, result.Status);
        Assert.Equal(exitCode, result.ExitCode);
    }

    [Fact]
    public void Provision_RunsMsiexecInstallQuietForTheBundledMsi()
    {
        var runner = new FakeProcessRunner(0, string.Empty);
        var provisioner = Create(runner);

        provisioner.Provision();

        Assert.Equal("msiexec.exe", runner.CapturedFileName);
        Assert.Equal(new[] { "/i", MsiPath, "/qn" }, runner.CapturedArguments);
    }

    [Fact]
    public void Provision_WhenBundledMsiMissing_FailsWithoutRunningMsiexec()
    {
        var runner = new FakeProcessRunner(0, string.Empty);
        var provisioner = new MsiexecPlayerShellProvisioner(
            new SetupWizardPayloadResolver(_ => false, () => MsiPath),
            runner);

        var result = provisioner.Provision();

        Assert.Equal(ShellProvisionStatus.Failed, result.Status);
        Assert.Null(runner.CapturedFileName);
    }
}
```

- [ ] **Step 2: Run test to verify it fails to compile**

Run: `& "C:\Program Files\dotnet\dotnet.exe" test tests/AFK4.SetupWizard.Tests/AFK4.SetupWizard.Tests.csproj --filter MsiexecPlayerShellProvisionerTests`
Expected: FAIL — `MsiexecPlayerShellProvisioner` / `SetupWizardPayloadResolver` do not exist.

- [ ] **Step 3: Implement the payload resolver**

`src/AFK4.SetupWizard.Core/SetupWizardPayloadResolver.cs`:

```csharp
using System.IO;

namespace AFK4.SetupWizard.Core;

/// <summary>
/// Locates the Player Shell MSI bundled next to the wizard executable
/// (`…\Setup Wizard\payload\AFK4.Player.Shell.msi`). Returns null when absent.
/// </summary>
public sealed class SetupWizardPayloadResolver
{
    public const string PlayerShellMsiFileName = "AFK4.Player.Shell.msi";

    private readonly Func<string, bool> fileExists;
    private readonly Func<string> resolvePath;

    public SetupWizardPayloadResolver(string baseDirectory)
        : this(File.Exists, () => Path.Combine(baseDirectory, "payload", PlayerShellMsiFileName))
    {
    }

    // Test seam: inject existence check + path so resolution is platform-independent.
    public SetupWizardPayloadResolver(Func<string, bool> fileExists, Func<string> resolvePath)
    {
        this.fileExists = fileExists;
        this.resolvePath = resolvePath;
    }

    public string? ResolvePlayerShellMsiPath()
    {
        var path = resolvePath();
        return fileExists(path) ? path : null;
    }
}
```

- [ ] **Step 4: Implement the provisioner**

`src/AFK4.SetupWizard.Core/MsiexecPlayerShellProvisioner.cs`:

```csharp
namespace AFK4.SetupWizard.Core;

public sealed class MsiexecPlayerShellProvisioner(
    SetupWizardPayloadResolver payloadResolver,
    IProcessRunner processRunner) : ISetupWizardShellProvisioner
{
    // msiexec success/reboot-pending codes treated as success.
    private const int Success = 0;
    private const int SuccessRebootRequired = 3010;
    // "Another version of this product is already installed."
    private const int ProductAlreadyInstalled = 1638;

    public ShellProvisionResult Provision()
    {
        var msiPath = payloadResolver.ResolvePlayerShellMsiPath();
        if (msiPath is null)
        {
            return ShellProvisionResult.Failed(null, "Bundled Player Shell MSI was not found next to the wizard.");
        }

        var result = processRunner.Run("msiexec.exe", ["/i", msiPath, "/qn"]);
        return result.ExitCode switch
        {
            Success or SuccessRebootRequired => ShellProvisionResult.Installed(result.ExitCode),
            ProductAlreadyInstalled => ShellProvisionResult.AlreadyPresent(result.ExitCode),
            _ => ShellProvisionResult.Failed(result.ExitCode, result.Output)
        };
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `& "C:\Program Files\dotnet\dotnet.exe" test tests/AFK4.SetupWizard.Tests/AFK4.SetupWizard.Tests.csproj --filter MsiexecPlayerShellProvisionerTests`
Expected: PASS (5 cases).

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.SetupWizard.Core/SetupWizardPayloadResolver.cs src/AFK4.SetupWizard.Core/MsiexecPlayerShellProvisioner.cs tests/AFK4.SetupWizard.Tests/MsiexecPlayerShellProvisionerTests.cs
git commit -m "feat(setup-wizard): msiexec player-shell provisioner with exit-code mapping"
```

### Task 3: Payload resolver path resolution test

**Files:**
- Test: `tests/AFK4.SetupWizard.Tests/SetupWizardPayloadResolverTests.cs`

- [ ] **Step 1: Write the test**

```csharp
using AFK4.SetupWizard.Core;

namespace AFK4.SetupWizard.Tests;

public sealed class SetupWizardPayloadResolverTests
{
    [Fact]
    public void ResolvePlayerShellMsiPath_UsesPayloadSubfolderNextToBaseDirectory()
    {
        string? probed = null;
        var resolver = new SetupWizardPayloadResolver(
            path => { probed = path; return true; },
            () => System.IO.Path.Combine(@"C:\Program Files\AFK4\Setup Wizard", "payload", "AFK4.Player.Shell.msi"));

        var result = resolver.ResolvePlayerShellMsiPath();

        Assert.EndsWith(System.IO.Path.Combine("payload", "AFK4.Player.Shell.msi"), result);
        Assert.Equal(result, probed);
    }

    [Fact]
    public void ResolvePlayerShellMsiPath_WhenMissing_ReturnsNull()
    {
        var resolver = new SetupWizardPayloadResolver(_ => false, () => @"C:\nope\payload\AFK4.Player.Shell.msi");

        Assert.Null(resolver.ResolvePlayerShellMsiPath());
    }
}
```

- [ ] **Step 2: Run tests to verify they pass**

Run: `& "C:\Program Files\dotnet\dotnet.exe" test tests/AFK4.SetupWizard.Tests/AFK4.SetupWizard.Tests.csproj --filter SetupWizardPayloadResolverTests`
Expected: PASS (2 cases).

- [ ] **Step 3: Commit**

```bash
git add tests/AFK4.SetupWizard.Tests/SetupWizardPayloadResolverTests.cs
git commit -m "test(setup-wizard): payload resolver path resolution"
```

### Task 4: SystemProcessRunner (default IProcessRunner)

**Files:**
- Create: `src/AFK4.SetupWizard.Core/SystemProcessRunner.cs`

- [ ] **Step 1: Implement**

```csharp
using System.Diagnostics;

namespace AFK4.SetupWizard.Core;

public sealed class SystemProcessRunner : IProcessRunner
{
    public ProcessRunResult Run(string fileName, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"{fileName} could not be started.");
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new ProcessRunResult(process.ExitCode, output.Trim());
    }
}
```

- [ ] **Step 2: Build Core**

Run: `& "C:\Program Files\dotnet\dotnet.exe" build src/AFK4.SetupWizard.Core/AFK4.SetupWizard.Core.csproj -c Debug`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/AFK4.SetupWizard.Core/SystemProcessRunner.cs
git commit -m "feat(setup-wizard): system process runner for msiexec"
```

---

## Phase 2 — Host bridge + WPF wiring

### Task 5: Provision the shell during gaming_pc enrollment

**Files:**
- Modify: `src/AFK4.SetupWizard/Web/SetupWizardWebHostBridge.cs`

- [ ] **Step 1: Add the provisioner dependency**

Change the primary constructor (lines 11-16) to accept the provisioner:

```csharp
public sealed class SetupWizardWebHostBridge(
    ISetupWizardApiClient apiClient,
    IDeviceKeyStore deviceKeyStore,
    ISetupWizardBootstrapWriter bootstrapWriter,
    SetupWizardMachineInfo machineInfo,
    ISetupWizardCompletionAction completionAction,
    ISetupWizardShellProvisioner shellProvisioner)
```

- [ ] **Step 2: Add a finalize helper and the retry message**

Add the `wizard:provisionShell` case in the `switch` (after `wizard:enrollAuth`, line 61):

```csharp
                "wizard:provisionShell" => FinalizeForRole(DeviceRoleNames.GamingPc),
```

Add this private method (near the enroll helpers):

```csharp
    // For gaming_pc: install the bundled Player Shell, then start the agent only on success.
    // For other roles: just start the agent. Returns the shell outcome for the finish screen.
    private WizardShellOutcome FinalizeForRole(string role)
    {
        if (role != DeviceRoleNames.GamingPc)
        {
            completionAction.Complete();
            return new WizardShellOutcome("skipped", null, null);
        }

        var result = shellProvisioner.Provision();
        if (result.Status == ShellProvisionStatus.Failed)
        {
            // Do NOT start the agent / mark ready — the finish screen shows an error + retry.
            return new WizardShellOutcome("failed", result.ExitCode, result.Message);
        }

        completionAction.Complete();
        var status = result.Status == ShellProvisionStatus.AlreadyPresent ? "already_present" : "installed";
        return new WizardShellOutcome(status, result.ExitCode, null);
    }
```

- [ ] **Step 3: Call the helper from both enroll paths**

In `EnrollAsync` replace `completionAction.Complete();` (line 174) with:

```csharp
        var shell = FinalizeForRole(role);
```

and extend the returned `WizardEnrollResult` (lines 176-185) to pass `shell`:

```csharp
        return new WizardEnrollResult(
            response.OrganizationId,
            response.BranchId,
            response.DeviceId,
            role,
            displayName,
            machineInfo.MachineName,
            response.EnrollmentState,
            response.ApiBaseUrl,
            response.UpdateChannel,
            shell);
```

Apply the identical change in `EnrollAuthenticatedAsync` (replace `completionAction.Complete();` at line 384; extend the result at lines 386-395 with `shell`).

- [ ] **Step 4: Add the outcome record + result field + error code**

Add the record near `WizardEnrollResult` (line 611):

```csharp
    private sealed record WizardShellOutcome(string Status, int? ExitCode, string? Message);
```

Extend `WizardEnrollResult` (lines 611-620) with a trailing field:

```csharp
        string UpdateChannel,
        WizardShellOutcome Shell);
```

Add to `ErrorCodeFor` (after the `wizard:enrollAuth` entry, line 507):

```csharp
        "wizard:provisionShell" => "wizard_shell_provision_failed",
```

- [ ] **Step 5: Add the using for DeviceRoleNames**

Confirm `using AFK4.Shared.Contracts.Install;` is present (line 7 — already there).

- [ ] **Step 6: Build the wizard assembly**

Run: `& "C:\Program Files\dotnet\dotnet.exe" build src/AFK4.SetupWizard/AFK4.SetupWizard.csproj -c Debug`
Expected: FAIL — constructor callers in `App.xaml.cs` / `PreviewSetupWizard.cs` now need the new argument (fixed in Task 6).

- [ ] **Step 7: Commit (after Task 6 builds green)**

Deferred to Task 6 so the solution compiles in one commit.

### Task 6: Construct the provisioner in App + Preview

**Files:**
- Modify: `src/AFK4.SetupWizard/App.xaml.cs`
- Modify: `src/AFK4.SetupWizard/Preview/PreviewSetupWizard.cs`

- [ ] **Step 1: Wire the real provisioner**

In `App.xaml.cs`, the real bridge construction (lines 39-44) becomes:

```csharp
        var bridge = new SetupWizardWebHostBridge(
            new SetupWizardApiClient(httpClient),
            new FileDeviceKeyStore(),
            new EnvironmentBootstrapWriter(machineInfo.MachineName),
            machineInfo,
            new AgentServiceCompletionAction(),
            new MsiexecPlayerShellProvisioner(
                new SetupWizardPayloadResolver(AppContext.BaseDirectory),
                new SystemProcessRunner()));
```

- [ ] **Step 2: Add a preview no-op provisioner**

In `PreviewSetupWizard.cs`, add a factory mirroring `CreateCompletionAction()`:

```csharp
    public static ISetupWizardShellProvisioner CreateShellProvisioner() =>
        new PreviewShellProvisioner();

    private sealed class PreviewShellProvisioner : ISetupWizardShellProvisioner
    {
        public ShellProvisionResult Provision() => ShellProvisionResult.Installed(0);
    }
```

In `App.xaml.cs` preview branch (lines 16-21) pass it:

```csharp
            var previewBridge = new SetupWizardWebHostBridge(
                Preview.PreviewSetupWizard.CreateApiClient(),
                Preview.PreviewSetupWizard.CreateDeviceKeyStore(),
                Preview.PreviewSetupWizard.CreateBootstrapWriter(),
                previewMachine,
                Preview.PreviewSetupWizard.CreateCompletionAction(),
                Preview.PreviewSetupWizard.CreateShellProvisioner());
```

- [ ] **Step 3: Build the solution**

Run: `& "C:\Program Files\dotnet\dotnet.exe" build AFK4.sln -c Debug`
Expected: Build succeeded.

- [ ] **Step 4: Run the full SetupWizard test suite**

Run: `& "C:\Program Files\dotnet\dotnet.exe" test tests/AFK4.SetupWizard.Tests/AFK4.SetupWizard.Tests.csproj`
Expected: PASS (existing + new tests).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.SetupWizard/Web/SetupWizardWebHostBridge.cs src/AFK4.SetupWizard/App.xaml.cs src/AFK4.SetupWizard/Preview/PreviewSetupWizard.cs
git commit -m "feat(setup-wizard): install player-shell on gaming_pc enroll + retry message"
```

---

## Phase 3 — Web finish screen status + retry

### Task 7: Extend wizardApi + finish screen + i18n

**Files:**
- Modify: `src/AFK4.SetupWizard.Web/src/wizardApi.ts`
- Modify: `src/AFK4.SetupWizard.Web/src/FinishedScreen.tsx`
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json`

- [ ] **Step 1: Extend the API types + add the retry call**

In `wizardApi.ts`, add the shell outcome type and a field on `WizardEnrollResult`:

```typescript
export interface WizardShellOutcome {
  status: 'installed' | 'already_present' | 'skipped' | 'failed';
  exitCode: number | null;
  message: string | null;
}
```

Add to the `WizardEnrollResult` interface (after `updateChannel`):

```typescript
  shell: WizardShellOutcome;
```

Add the retry function (after `authenticatedInstallClient`):

```typescript
/** Retry installing the Player Shell on a gaming PC after a failed attempt. */
export function provisionShell(): Promise<WizardShellOutcome> {
  return postHostRequest<WizardShellOutcome>('wizard:provisionShell');
}
```

- [ ] **Step 2: Show the status line + retry in the finish screen**

In `FinishedScreen.tsx`, import `useState`, `provisionShell`, and the shell type; render a status row for gaming PCs. Add below the `<dl>` summary block (after line 67):

```tsx
        {result.role === 'gaming_pc' && (
          <ShellStatusRow initial={result.shell} />
        )}
```

Add the component at the bottom of the file:

```tsx
function ShellStatusRow({ initial }: { initial: WizardShellOutcome }) {
  const { t } = useI18n();
  const [outcome, setOutcome] = useState(initial);
  const [busy, setBusy] = useState(false);

  if (outcome.status === 'installed' || outcome.status === 'already_present') {
    return (
      <div className="wizard-shell-status is-ok" role="status">
        {t('setup.wizard.finished.shell.ok')}
      </div>
    );
  }

  if (outcome.status === 'skipped') {
    return null;
  }

  return (
    <div className="wizard-shell-status is-error" role="alert">
      <span>
        {t('setup.wizard.finished.shell.failed')}
        {outcome.exitCode !== null ? ` (msiexec ${outcome.exitCode})` : ''}
      </span>
      <button
        type="button"
        className="wizard-secondary"
        disabled={busy}
        onClick={async () => {
          setBusy(true);
          try {
            setOutcome(await provisionShell());
          } finally {
            setBusy(false);
          }
        }}
      >
        {busy ? t('setup.wizard.finished.shell.installing') : t('setup.wizard.finished.shell.retry')}
      </button>
    </div>
  );
}
```

Update the imports at the top:

```tsx
import { useState } from 'react';
import { CheckCircle2 } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import { closeWizard, provisionShell, type WizardEnrollResult, type WizardSeat, type WizardShellOutcome } from './wizardApi';
```

- [ ] **Step 3: Add i18n keys (real translations, no tg=ru copies)**

Add to `locales/ru.json`:

```json
  "setup.wizard.finished.shell.installing": "Устанавливаем оболочку игрока…",
  "setup.wizard.finished.shell.ok": "Оболочка игрока установлена.",
  "setup.wizard.finished.shell.failed": "Не удалось установить оболочку игрока.",
  "setup.wizard.finished.shell.retry": "Повторить установку",
```

Add to `locales/en.json`:

```json
  "setup.wizard.finished.shell.installing": "Installing the player shell…",
  "setup.wizard.finished.shell.ok": "Player shell installed.",
  "setup.wizard.finished.shell.failed": "Could not install the player shell.",
  "setup.wizard.finished.shell.retry": "Retry install",
```

Add to `locales/tg.json`:

```json
  "setup.wizard.finished.shell.installing": "Насби пӯсти бозингар…",
  "setup.wizard.finished.shell.ok": "Пӯсти бозингар насб шуд.",
  "setup.wizard.finished.shell.failed": "Насби пӯсти бозингар нашуд.",
  "setup.wizard.finished.shell.retry": "Такрори насб",
```

- [ ] **Step 4: Regenerate the i18n bundle**

Run: `~/.bun/bin/bun run gen`
Expected: regenerates `packages/i18n` messages; no errors.

- [ ] **Step 5: Run the i18n guard + web tests**

Run: `~/.bun/bin/bun test packages/i18n`
Expected: PASS (no CAPS / no-fake-tg guard green).

Run: `cd src/AFK4.SetupWizard.Web && ~/.bun/bin/bun test`
Expected: PASS.

- [ ] **Step 6: Typecheck + build the web app**

Run: `cd src/AFK4.SetupWizard.Web && ~/.bun/bin/bun run build`
Expected: tsc 0 errors, vite build succeeds.

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.SetupWizard.Web/src/wizardApi.ts src/AFK4.SetupWizard.Web/src/FinishedScreen.tsx locales/ru.json locales/en.json locales/tg.json packages/i18n
git commit -m "feat(setup-wizard-web): finish-screen shell status + retry"
```

> Style note: add `.wizard-shell-status`, `.is-ok`, `.is-error` to `src/AFK4.SetupWizard.Web/src/styles.css` following the existing `.wizard-pending-note` pattern (same padding/radius/role-colored border). Do this in Step 2's commit.

---

## Phase 4 — Bundle the Player Shell MSI into the wizard

### Task 8: Build Player Shell MSI before the agent; copy into wizard payload

**Files:**
- Modify: `scripts/build-client-packages.ps1`

Context: `installers/agent/Package.wxs` already harvests `$(var.SetupWizardSupportDir)\**` recursively, and the support dir is now copied recursively (existing fix). So a `payload\AFK4.Player.Shell.msi` placed in the SetupWizard publish dir before the support-dir copy will ship at `…\Setup Wizard\payload\AFK4.Player.Shell.msi`.

- [ ] **Step 1: Build the Player Shell MSI before the agent MSI**

Currently the WiX builds run in order operator → agent → player-shell (lines ~329-363). Move the Player Shell MSI `wix build` block (lines 355-363) to run **before** the agent MSI `wix build` block (before line 341). The agent build now depends on the player-shell MSI existing.

- [ ] **Step 2: Copy the built MSI into the wizard payload before the support copy**

After the Player Shell MSI is built (`$playerShellMsiPath` exists) and before the SetupWizard support dir is populated (line 305), add:

```powershell
$setupWizardPayloadDir = Join-Path $setupWizardPublishDir 'payload'
New-Item -ItemType Directory -Force -Path $setupWizardPayloadDir | Out-Null
Copy-Item -LiteralPath $playerShellMsiPath -Destination (Join-Path $setupWizardPayloadDir 'AFK4.Player.Shell.msi') -Force
```

(The existing recursive support-dir copy at lines 305-307 then pulls `payload\` into `setup-wizard-support`, which the agent MSI harvests.)

- [ ] **Step 3: Assert the payload shipped (build-time guard)**

After the agent MSI `wix build`, reuse `Get-MsiFileNames` to assert the bundled MSI is present (mirrors `Assert-OperatorMsiContainsFrontendAssets`):

```powershell
$agentFiles = Get-MsiFileNames -MsiPath $agentMsiPath
if (-not ($agentFiles | Where-Object { $_ -like '*AFK4.Player.Shell.msi*' } | Select-Object -First 1))
{
    throw "Agent MSI does not contain the bundled Player Shell MSI (payload\AFK4.Player.Shell.msi)."
}
```

- [ ] **Step 4: Rebuild the packages**

Run: `powershell -File scripts/build-client-packages.ps1 -Version 0.1.38 -Channel internal`
Expected: succeeds; the new assert passes; `afk4-agent-0.1.38-internal.msi` produced.

- [ ] **Step 5: Commit**

```bash
git add scripts/build-client-packages.ps1
git commit -m "build: bundle player-shell MSI into the setup wizard payload"
```

---

## Phase 5 — Framework-dependent publishing

### Task 9: Switch component publishes to framework-dependent

**Files:**
- Modify: `scripts/build-client-packages.ps1`

- [ ] **Step 1: Flip self-contained to false**

In the `$projects` list (lines 242-247) set `SelfContained = $false` for all four projects, and in the `dotnet publish` call (line 264) the `--self-contained $(...)` already reads from the flag — confirm it now emits `false`.

- [ ] **Step 2: Rebuild and record sizes**

Run: `powershell -File scripts/build-client-packages.ps1 -Version 0.1.38 -Channel internal`
Expected: succeeds. Record the new MSI sizes (agent should drop from ~58 MB toward ~15–20 MB; the bundled shell MSI ~10 MB).

- [ ] **Step 3: Commit**

```bash
git add scripts/build-client-packages.ps1
git commit -m "build: publish client components framework-dependent"
```

---

## Phase 6 — Master installers (WiX Burn) with downloaded runtime

### Task 10: Gaming-PC and Operator Burn bundles

**Files:**
- Create: `installers/bootstrappers/gaming-pc/Bundle.wxs`
- Create: `installers/bootstrappers/operator/Bundle.wxs`
- Modify: `scripts/build-client-packages.ps1`

- [ ] **Step 1: Author the gaming-PC bundle**

`installers/bootstrappers/gaming-pc/Bundle.wxs` — chains the .NET Desktop Runtime (downloaded) then the agent MSI:

```xml
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs"
     xmlns:bal="http://wixtoolset.org/schemas/v4/wxs/bal"
     xmlns:netfx="http://wixtoolset.org/schemas/v4/wxs/netfx">
  <Bundle
      Name="AFK4.NET Gaming PC"
      Manufacturer="AFK4.NET"
      Version="$(var.PackageVersion)"
      UpgradeCode="{B6F2B0E4-2C4E-4E7B-9C2C-9A1E4B7C0D11}">
    <BootstrapperApplication>
      <bal:WixStandardBootstrapperApplication LicenseUrl="" Theme="hyperlinkLicense" />
    </BootstrapperApplication>

    <!-- Downloads the .NET 10 Desktop Runtime x64 if a compatible one is not present. -->
    <netfx:DotNetCoreSearch
        RuntimeType="desktop"
        Platform="x64"
        MajorVersion="10"
        Variable="DesktopRuntimeVersion" />

    <Chain>
      <ExePackage
          Id="DotNetDesktopRuntime"
          DisplayName=".NET 10 Desktop Runtime"
          Permanent="yes"
          Vital="yes"
          Compressed="no"
          DownloadUrl="https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe"
          InstallArguments="/install /quiet /norestart"
          DetectCondition="DesktopRuntimeVersion AND DesktopRuntimeVersion >= v10.0.0">
        <ExePackagePayload
            Name="windowsdesktop-runtime-win-x64.exe"
            ProductName=".NET Desktop Runtime"
            Description=".NET Desktop Runtime"
            Hash="$(var.DesktopRuntimeSha512)"
            Size="$(var.DesktopRuntimeSize)"
            Version="10.0.0"
            DownloadUrl="https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe" />
      </ExePackage>

      <MsiPackage Id="AgentMsi" SourceFile="$(var.AgentMsiPath)" Vital="yes" />
    </Chain>
  </Bundle>
</Wix>
```

> Note: `Hash`/`Size`/`Version` for the runtime payload are resolved at build time (Step 3). Using a remote payload keeps `setup.exe` small. The shell is intentionally NOT chained — the wizard installs it on `gaming_pc`.

- [ ] **Step 2: Author the operator bundle**

`installers/bootstrappers/operator/Bundle.wxs` — identical structure, with `UpgradeCode="{C7A3C1F5-3D5F-4F8C-8D3D-0B2F5C8D1E22}"`, `Name="AFK4.NET Operator"`, and `<MsiPackage Id="OperatorMsi" SourceFile="$(var.OperatorMsiPath)" Vital="yes" />`.

- [ ] **Step 3: Build the bundles in the script**

After the component MSIs are built, resolve the runtime payload metadata and build the bundles. Append to `scripts/build-client-packages.ps1`:

```powershell
$gamingPcSetupPath = Join-Path $artifactRoot "afk4-gaming-pc-setup-$Version-$Channel.exe"
$operatorSetupPath = Join-Path $artifactRoot "afk4-operator-setup-$Version-$Channel.exe"

& $DotnetPath wix extension add -g WixToolset.Netfx.wixext WixToolset.Bal.wixext

& $DotnetPath wix build -acceptEula wix7 (Join-Path $repoRoot 'installers/bootstrappers/gaming-pc/Bundle.wxs') `
    -ext WixToolset.Netfx.wixext -ext WixToolset.Bal.wixext `
    -d "PackageVersion=$msiVersion" `
    -d "AgentMsiPath=$agentMsiPath" `
    -o $gamingPcSetupPath
if ($LASTEXITCODE -ne 0) { throw "WiX build failed for gaming-PC bootstrapper (exit $LASTEXITCODE)." }

& $DotnetPath wix build -acceptEula wix7 (Join-Path $repoRoot 'installers/bootstrappers/operator/Bundle.wxs') `
    -ext WixToolset.Netfx.wixext -ext WixToolset.Bal.wixext `
    -d "PackageVersion=$msiVersion" `
    -d "OperatorMsiPath=$operatorMsiPath" `
    -o $operatorSetupPath
if ($LASTEXITCODE -ne 0) { throw "WiX build failed for operator bootstrapper (exit $LASTEXITCODE)." }

Write-Host "Master installers:"
Write-Host $gamingPcSetupPath
Write-Host $operatorSetupPath
```

> If `ExePackagePayload` requires explicit `Hash`/`Size`, fetch them once from the runtime URL (`Invoke-WebRequest -Method Head`) and pass via `-d "DesktopRuntimeSha512=…" -d "DesktopRuntimeSize=…"`; otherwise let `RemotePayload` harvesting resolve them. Confirm against the installed WiX 7 Netfx extension docs (use the context7 docs tool for `wixtoolset` if the element shape differs).

- [ ] **Step 4: Build the bundles**

Run: `powershell -File scripts/build-client-packages.ps1 -Version 0.1.38 -Channel internal`
Expected: produces `afk4-gaming-pc-setup-0.1.38-internal.exe` and `afk4-operator-setup-0.1.38-internal.exe`.

- [ ] **Step 5: Commit**

```bash
git add installers/bootstrappers scripts/build-client-packages.ps1
git commit -m "build: master installers (Burn) that fetch .NET runtime + install agent/operator"
```

---

## Phase 7 — Runbook

### Task 11: Update client-packaging runbook

**Files:**
- Modify: `docs/operations/client-packaging.md`

- [ ] **Step 1: Rewrite the packaging-decision section**

Replace the pre-wizard statements with the new model:
- One master installer (`setup.exe`, a WiX Burn bundle) per target (gaming PC, operator); it ensures the .NET Desktop Runtime once (downloaded), then installs the component MSI.
- The Setup Wizard installs the Player Shell on the `gaming_pc` role — it is **not** a manual step and the agent MSI/payload now carries the shell MSI.
- Components are framework-dependent, not self-contained.

- [ ] **Step 2: Sweep for contradictions**

Scan the rest of the runbook for any remaining claims that conflict (MSI split "install shell manually", "agent MSI does not carry Player Shell", owner-code-only enrollment) and fix them in the same pass. Update the `Last updated` date to 2026-06-10.

- [ ] **Step 3: Commit**

```bash
git add docs/operations/client-packaging.md
git commit -m "docs: update client-packaging runbook to wizard-as-single-tool model"
```

---

## Phase 8 — VM verification (manual)

### Task 12: Production-like verification on a clean, networked VM

- [ ] **Step 1: Fresh VM, no .NET runtime, network on.** Snapshot first so you can re-run.

- [ ] **Step 2: Run `afk4-gaming-pc-setup-0.1.38-internal.exe`.** Expected: it downloads + installs the .NET Desktop Runtime once, then installs the agent. Record `setup.exe` size vs the old self-contained agent MSI.

- [ ] **Step 3: Run the Setup Wizard** (auto-launch or Start menu). Sign in (staging `e2eowner / E2eOwner!2026`), pick a branch, pick role **gaming PC** + a free seat, enroll.

- [ ] **Step 4: Confirm the finish screen shows "Оболочка игрока установлена."** If it shows the error + retry, capture the msiexec code, click Повторить, and if it persists collect `msiexec` logs.

- [ ] **Step 5: Confirm the player screen comes up** in the active session, and in the Operator the gaming PC reports the shell version and is online.

- [ ] **Step 6: Press "Старт 60 мин" in the Operator.** Expected: the workstation lock completes; the "Офлайн: ждём подтверждение платформы" state does NOT appear. (Confirms the shell was the root cause.) If it persists → separate agent-connectivity bug; pull agent logs.

- [ ] **Step 7: Manager-workstation check.** Re-run on another VM snapshot, pick **manager workstation**: no shell is installed, finish screen shows no shell row, agent runs.

---

## Self-Review Notes

- **Spec coverage:** Decision 1 (wizard provisions shell) → Tasks 1–8. Decision 2 (framework-dependent + master installer) → Tasks 9–10. Decision 3 (option A, gaming-PC only) → Task 5 `FinalizeForRole` branch + Task 10 (shell not chained). Decision 4 (download runtime) → Task 10 `DownloadUrl`. UX/honest-failure → Tasks 5/7. Runbook → Task 11. Testing (unit + VM) → Tasks 2/3 + Task 12.
- **Type consistency:** `ShellProvisionStatus` / `ShellProvisionResult` / `ISetupWizardShellProvisioner` / `IProcessRunner` / `ProcessRunResult` defined in Task 1, used in Tasks 2/4/5/6. Web `WizardShellOutcome.status` strings (`installed`/`already_present`/`skipped`/`failed`) match the host bridge `FinalizeForRole` outputs.
- **Open verification:** the WiX Burn `netfx:DotNetCoreSearch` / `ExePackage` element shapes (Task 10) should be confirmed against the installed WiX 7 Netfx extension before coding — flagged inline.
