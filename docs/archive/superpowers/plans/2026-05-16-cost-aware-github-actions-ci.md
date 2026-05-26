# Cost-Aware GitHub Actions CI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add GitHub Actions CI gates that verify AFK4 pull requests while keeping paid Windows runner usage predictable and low.

**Architecture:** Keep the existing manual `.github/workflows/client-packages.yml` as the guarded release/package workflow. Add a new PR verification workflow with a cheap Linux path-detection job, a conditional Windows build/test job, and an always-running result job that is safe to require in branch protection. Add a separate package smoke workflow for unsigned MSI validation on `main` or manual dispatch.

**Tech Stack:** GitHub Actions, Windows hosted runners, PowerShell, .NET 10, WiX via `dotnet wix`, xUnit workflow-content tests in `AFK4.Agent.Service.Tests`.

---

## Files

- Create: `.github/workflows/pr-verification.yml`
  - Branch-protection-safe PR workflow.
  - Always runs a cheap result job.
  - Runs the paid Windows build/test job only when PR paths can affect build, test, packaging, or workflow behavior.
- Create: `.github/workflows/package-smoke.yml`
  - Unsigned MSI package smoke.
  - Runs on `main` pushes that touch client/package-relevant files and by manual dispatch.
- Modify: `.github/workflows/client-packages.yml`
  - Preserve manual release behavior.
  - Add permissions, timeout, and short artifact retention.
- Modify: `tests/AFK4.Agent.Service.Tests/ClientReleaseAutomationTests.cs`
  - Add workflow-content tests for PR verification and package smoke.
  - Extend release workflow assertions for cost controls.
- Modify: `docs/progress/2026-05-12-vertical-slice-progress.md`
  - Record the CI workflow addition and remaining branch-protection/remote-validation gap.
- Modify: `docs/roadmap/production-readiness.md`
  - Refine the CI Gate item after implementation.

## Task 1: Add Failing Workflow Tests

**Files:**
- Modify: `tests/AFK4.Agent.Service.Tests/ClientReleaseAutomationTests.cs`

- [ ] **Step 1: Add tests for the new CI workflows and release workflow cost controls**

Add these test methods inside `ClientReleaseAutomationTests`, near the existing `ClientPackagesWorkflow_ContainsGuardedSigningPublishingAndRegistrationSteps` test:

```csharp
[Fact]
public void PrVerificationWorkflow_UsesCostAwareRequiredResultGate()
{
    var workflow = NormalizeLineEndings(File.ReadAllText(ScriptPath(".github/workflows/pr-verification.yml")));

    Assert.Contains("name: PR Verification", workflow, StringComparison.Ordinal);
    Assert.Contains("pull_request:", workflow, StringComparison.Ordinal);
    Assert.Contains("- main", workflow, StringComparison.Ordinal);
    Assert.Contains("permissions:\n  contents: read", workflow, StringComparison.Ordinal);
    Assert.Contains("concurrency:", workflow, StringComparison.Ordinal);
    Assert.Contains("cancel-in-progress: true", workflow, StringComparison.Ordinal);
    Assert.DoesNotContain("paths-ignore:", workflow, StringComparison.Ordinal);

    Assert.Contains("changes:", workflow, StringComparison.Ordinal);
    Assert.Contains("runs-on: ubuntu-latest", workflow, StringComparison.Ordinal);
    Assert.Contains("timeout-minutes: 5", workflow, StringComparison.Ordinal);
    Assert.Contains("run_windows: ${{ steps.filter.outputs.run_windows }}", workflow, StringComparison.Ordinal);
    Assert.Contains("git diff --name-only $base $head", workflow, StringComparison.Ordinal);
    Assert.Contains("run_windows=$($runWindows.ToString().ToLowerInvariant())", workflow, StringComparison.Ordinal);

    Assert.Contains("build-test-windows:", workflow, StringComparison.Ordinal);
    Assert.Contains("if: ${{ needs.changes.outputs.run_windows == 'true' }}", workflow, StringComparison.Ordinal);
    Assert.Contains("runs-on: windows-latest", workflow, StringComparison.Ordinal);
    Assert.Contains("timeout-minutes: 45", workflow, StringComparison.Ordinal);
    Assert.Contains("uses: actions/setup-dotnet@v4", workflow, StringComparison.Ordinal);
    Assert.Contains("global-json-file: global.json", workflow, StringComparison.Ordinal);
    Assert.Contains("dotnet tool restore", workflow, StringComparison.Ordinal);
    Assert.Contains("dotnet restore AFK4.sln", workflow, StringComparison.Ordinal);
    Assert.Contains("dotnet build AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal", workflow, StringComparison.Ordinal);
    Assert.Contains("dotnet test AFK4.sln --no-build -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal", workflow, StringComparison.Ordinal);

    Assert.Contains("pr-verification-result:", workflow, StringComparison.Ordinal);
    Assert.Contains("if: ${{ always() }}", workflow, StringComparison.Ordinal);
    Assert.Contains("Windows build/test gate did not pass.", workflow, StringComparison.Ordinal);
    Assert.Contains("No Windows-relevant changes detected; skipping paid Windows runner.", workflow, StringComparison.Ordinal);
}

[Fact]
public void PackageSmokeWorkflow_BuildsUnsignedMsiArtifactsWithShortRetention()
{
    var workflow = NormalizeLineEndings(File.ReadAllText(ScriptPath(".github/workflows/package-smoke.yml")));

    Assert.Contains("name: Package Smoke", workflow, StringComparison.Ordinal);
    Assert.Contains("push:", workflow, StringComparison.Ordinal);
    Assert.Contains("- main", workflow, StringComparison.Ordinal);
    Assert.Contains("workflow_dispatch:", workflow, StringComparison.Ordinal);
    Assert.Contains("permissions:\n  contents: read", workflow, StringComparison.Ordinal);
    Assert.Contains("concurrency:", workflow, StringComparison.Ordinal);
    Assert.Contains("cancel-in-progress: true", workflow, StringComparison.Ordinal);
    Assert.Contains("runs-on: windows-latest", workflow, StringComparison.Ordinal);
    Assert.Contains("timeout-minutes: 60", workflow, StringComparison.Ordinal);
    Assert.Contains("dotnet tool restore", workflow, StringComparison.Ordinal);
    Assert.Contains("powershell -ExecutionPolicy Bypass -File scripts/build-client-packages.ps1 -Version 0.1.0-ci -Channel internal", workflow, StringComparison.Ordinal);
    Assert.Contains("afk4-operator-app-0.1.0-ci-internal.msi", workflow, StringComparison.Ordinal);
    Assert.Contains("afk4-gaming-pc-0.1.0-ci-internal.msi", workflow, StringComparison.Ordinal);
    Assert.Contains("uses: actions/upload-artifact@v4", workflow, StringComparison.Ordinal);
    Assert.Contains("if-no-files-found: error", workflow, StringComparison.Ordinal);
    Assert.Contains("retention-days: 3", workflow, StringComparison.Ordinal);
}

[Fact]
public void ClientPackagesWorkflow_UsesCostControlsForManualReleaseRuns()
{
    var workflow = NormalizeLineEndings(File.ReadAllText(ScriptPath(".github/workflows/client-packages.yml")));

    Assert.Contains("permissions:\n  contents: read", workflow, StringComparison.Ordinal);
    Assert.Contains("timeout-minutes: 90", workflow, StringComparison.Ordinal);
    Assert.Contains("if-no-files-found: error", workflow, StringComparison.Ordinal);
    Assert.Equal(2, CountOccurrences(workflow, "retention-days: 3"));
}
```

Add this helper near the existing workflow helper methods at the bottom of the class:

```csharp
private static int CountOccurrences(string value, string needle)
{
    var count = 0;
    var startIndex = 0;

    while (true)
    {
        var index = value.IndexOf(needle, startIndex, StringComparison.Ordinal);
        if (index < 0)
        {
            return count;
        }

        count++;
        startIndex = index + needle.Length;
    }
}
```

- [ ] **Step 2: Run the targeted tests and verify they fail for missing workflows/cost controls**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --filter "FullyQualifiedName~ClientReleaseAutomationTests" -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

Expected:

```text
Failed!  - Failed:     3
```

At least these failures should appear:

```text
Could not find file ... .github\workflows\pr-verification.yml
Could not find file ... .github\workflows\package-smoke.yml
Assert.Contains() Failure ... timeout-minutes: 90
```

- [ ] **Step 3: Commit the failing workflow tests**

Run:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add tests/AFK4.Agent.Service.Tests/ClientReleaseAutomationTests.cs
& 'C:\Program Files\Git\cmd\git.exe' commit -m "test: cover cost-aware GitHub Actions workflows"
```

## Task 2: Add PR Verification Workflow

**Files:**
- Create: `.github/workflows/pr-verification.yml`
- Test: `tests/AFK4.Agent.Service.Tests/ClientReleaseAutomationTests.cs`

- [ ] **Step 1: Create the PR verification workflow**

Create `.github/workflows/pr-verification.yml`:

```yaml
name: PR Verification

on:
  pull_request:
    branches:
      - main

permissions:
  contents: read

concurrency:
  group: pr-verification-${{ github.workflow }}-${{ github.event.pull_request.number || github.ref }}
  cancel-in-progress: true

jobs:
  changes:
    name: Detect Relevant Changes
    runs-on: ubuntu-latest
    timeout-minutes: 5
    outputs:
      run_windows: ${{ steps.filter.outputs.run_windows }}
    steps:
      - name: Checkout
        uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Detect Windows-relevant changes
        id: filter
        shell: pwsh
        run: |
          $base = "${{ github.event.pull_request.base.sha }}"
          $head = "${{ github.event.pull_request.head.sha }}"
          $changed = git diff --name-only $base $head
          $relevantPrefixes = @(
            ".github/workflows/",
            "installers/",
            "scripts/",
            "src/",
            "tests/"
          )
          $relevantFiles = @(
            "AFK4.sln",
            "Directory.Build.props",
            "Directory.Packages.props",
            "global.json",
            "NuGet.config"
          )
          $runWindows = $false

          foreach ($path in $changed) {
            if ($relevantFiles -contains $path) {
              $runWindows = $true
              break
            }

            foreach ($prefix in $relevantPrefixes) {
              if ($path.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                $runWindows = $true
                break
              }
            }

            if ($runWindows) {
              break
            }
          }

          Write-Host "Changed paths:"
          $changed | ForEach-Object { Write-Host " - $_" }
          Write-Host "Run Windows build/test: $runWindows"
          "run_windows=$($runWindows.ToString().ToLowerInvariant())" | Out-File -FilePath $env:GITHUB_OUTPUT -Append

  build-test-windows:
    name: Build And Test Windows
    needs: changes
    if: ${{ needs.changes.outputs.run_windows == 'true' }}
    runs-on: windows-latest
    timeout-minutes: 45
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
        run: dotnet build AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal

      - name: Test
        run: dotnet test AFK4.sln --no-build -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal

  pr-verification-result:
    name: PR Verification Result
    needs:
      - changes
      - build-test-windows
    if: ${{ always() }}
    runs-on: ubuntu-latest
    timeout-minutes: 5
    steps:
      - name: Check result
        shell: pwsh
        run: |
          $changesResult = "${{ needs.changes.result }}"
          $runWindows = "${{ needs.changes.outputs.run_windows }}"
          $windowsResult = "${{ needs.build-test-windows.result }}"

          if ($changesResult -ne "success") {
            throw "Change detection failed. Result: $changesResult"
          }

          if ($runWindows -eq "true" -and $windowsResult -ne "success") {
            throw "Windows build/test gate did not pass. Result: $windowsResult"
          }

          if ($runWindows -ne "true") {
            Write-Host "No Windows-relevant changes detected; skipping paid Windows runner."
          }
          else {
            Write-Host "Windows build/test gate passed."
          }
```

- [ ] **Step 2: Run the targeted workflow tests**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --filter "FullyQualifiedName~PrVerificationWorkflow_UsesCostAwareRequiredResultGate" -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

Expected:

```text
Passed!  - Failed:     0
```

- [ ] **Step 3: Commit the PR workflow**

Run:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add .github/workflows/pr-verification.yml tests/AFK4.Agent.Service.Tests/ClientReleaseAutomationTests.cs
& 'C:\Program Files\Git\cmd\git.exe' commit -m "ci: add cost-aware PR verification"
```

## Task 3: Add Package Smoke Workflow

**Files:**
- Create: `.github/workflows/package-smoke.yml`
- Test: `tests/AFK4.Agent.Service.Tests/ClientReleaseAutomationTests.cs`

- [ ] **Step 1: Create the package smoke workflow**

Create `.github/workflows/package-smoke.yml`:

```yaml
name: Package Smoke

on:
  push:
    branches:
      - main
    paths:
      - ".github/workflows/package-smoke.yml"
      - "installers/**"
      - "scripts/build-client-packages.ps1"
      - "scripts/install-afk4-update-msi.ps1"
      - "scripts/rollback-afk4-update-msi.ps1"
      - "scripts/restart-afk4-agent-service.ps1"
      - "src/AFK4.Operator.App/**"
      - "src/AFK4.Agent.Service/**"
      - "src/AFK4.Player.Shell/**"
      - "Directory.Build.props"
      - "Directory.Packages.props"
      - "global.json"
      - "AFK4.sln"
  workflow_dispatch:

permissions:
  contents: read

concurrency:
  group: package-smoke-${{ github.workflow }}-${{ github.ref }}
  cancel-in-progress: true

jobs:
  package-smoke:
    name: Build Unsigned MSI Packages
    runs-on: windows-latest
    timeout-minutes: 60
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          global-json-file: global.json

      - name: Restore tools
        run: dotnet tool restore

      - name: Build client packages
        shell: pwsh
        run: powershell -ExecutionPolicy Bypass -File scripts/build-client-packages.ps1 -Version 0.1.0-ci -Channel internal

      - name: Verify MSI artifacts
        shell: pwsh
        run: |
          $expectedArtifacts = @(
            "artifacts/client-packages/afk4-operator-app-0.1.0-ci-internal.msi",
            "artifacts/client-packages/afk4-gaming-pc-0.1.0-ci-internal.msi"
          )

          foreach ($artifact in $expectedArtifacts) {
            if (-not (Test-Path -LiteralPath $artifact)) {
              throw "Expected MSI artifact was not produced: $artifact"
            }
          }

      - name: Upload MSI artifacts
        uses: actions/upload-artifact@v4
        with:
          name: afk4-package-smoke-msi-0.1.0-ci-internal
          path: artifacts/client-packages/*.msi
          if-no-files-found: error
          retention-days: 3
```

- [ ] **Step 2: Run the targeted workflow test**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --filter "FullyQualifiedName~PackageSmokeWorkflow_BuildsUnsignedMsiArtifactsWithShortRetention" -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

Expected:

```text
Passed!  - Failed:     0
```

- [ ] **Step 3: Commit the package smoke workflow**

Run:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add .github/workflows/package-smoke.yml tests/AFK4.Agent.Service.Tests/ClientReleaseAutomationTests.cs
& 'C:\Program Files\Git\cmd\git.exe' commit -m "ci: add client package smoke workflow"
```

## Task 4: Harden Manual Client Package Workflow Cost Controls

**Files:**
- Modify: `.github/workflows/client-packages.yml`
- Test: `tests/AFK4.Agent.Service.Tests/ClientReleaseAutomationTests.cs`

- [ ] **Step 1: Add permissions, timeout, and artifact retention to the existing manual workflow**

Modify `.github/workflows/client-packages.yml`.

Add this block after the `on:` block and before `jobs:`:

```yaml
permissions:
  contents: read
```

Change the job header from:

```yaml
jobs:
  build-client-packages:
    runs-on: windows-latest
    steps:
```

to:

```yaml
jobs:
  build-client-packages:
    runs-on: windows-latest
    timeout-minutes: 90
    steps:
```

Change the `Upload update package requests` artifact step to include short retention and fail-closed behavior:

```yaml
      - name: Upload update package requests
        if: ${{ inputs.publish_update_metadata }}
        uses: actions/upload-artifact@v4
        with:
          name: afk4-update-package-requests-${{ inputs.version }}-${{ inputs.channel }}
          path: artifacts/update-packages/*-request.json
          if-no-files-found: error
          retention-days: 3
```

Change the `Upload client packages` artifact step to include short retention and fail-closed behavior:

```yaml
      - name: Upload client packages
        uses: actions/upload-artifact@v4
        with:
          name: afk4-client-packages-${{ inputs.version }}-${{ inputs.channel }}
          path: artifacts/client-packages/*.msi
          if-no-files-found: error
          retention-days: 3
```

- [ ] **Step 2: Run the targeted release workflow cost-control test**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --filter "FullyQualifiedName~ClientPackagesWorkflow_UsesCostControlsForManualReleaseRuns" -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

Expected:

```text
Passed!  - Failed:     0
```

- [ ] **Step 3: Run all client release automation tests**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --filter "FullyQualifiedName~ClientReleaseAutomationTests" -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

Expected:

```text
Passed!  - Failed:     0
```

- [ ] **Step 4: Commit the manual workflow hardening**

Run:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add .github/workflows/client-packages.yml tests/AFK4.Agent.Service.Tests/ClientReleaseAutomationTests.cs
& 'C:\Program Files\Git\cmd\git.exe' commit -m "ci: limit manual package workflow artifacts"
```

## Task 5: Update Progress And Roadmap Docs

**Files:**
- Modify: `docs/progress/2026-05-12-vertical-slice-progress.md`
- Modify: `docs/roadmap/production-readiness.md`

- [ ] **Step 1: Update the current progress snapshot**

In `docs/progress/2026-05-12-vertical-slice-progress.md`, under `Packaging And Updates`, add this bullet:

```markdown
- Cost-aware GitHub Actions workflows:
  - PR verification with branch-protection-safe result job and conditional
    Windows build/test execution.
  - Package smoke for unsigned MSI validation on `main` and manual dispatch.
  - Manual release package workflow with short artifact retention.
```

In `Latest Verification`, add this subsection after the current final verification notes:

```markdown
Cost-aware CI configuration verification on 2026-05-16:

- targeted client release automation tests passed locally;
- workflow-content tests cover PR verification, package smoke, and manual
  package workflow cost controls;
- remote GitHub Actions validation still needs to be observed on the first PR
  after these workflows are pushed.
```

In `Known Gaps`, replace:

```markdown
- GitHub PR checks are not yet mandatory release gates.
```

with:

```markdown
- GitHub Actions workflows are defined in the repository, but branch protection
  and the first observed remote PR run are still required before they become
  mandatory release gates.
```

In `Recommended Next Work`, replace:

```markdown
2. Add mandatory GitHub Actions checks for build, tests, and packaging smoke.
```

with:

```markdown
2. Push the cost-aware GitHub Actions CI branch, observe the first remote PR
   verification run, then enable branch protection for the `PR Verification
   Result` check.
```

- [ ] **Step 2: Update the production readiness roadmap**

In `docs/roadmap/production-readiness.md`, update the `CI Gate` paragraph under `Critical Path To Pilot Production` so it reads:

```markdown
2. **CI Gate**

   Use cost-aware GitHub Actions workflows to build and test relevant pull
   requests, run package smoke for client MSI artifacts, and keep release
   packaging manual and guarded. GitHub Actions billing is enabled, but
   workflows must avoid unnecessary manual remote runs, use Windows hosted
   runners only where they add required coverage, cancel stale PR runs, set
   timeouts, and keep artifact retention short. After the first successful
   remote PR run, enable branch protection for the `PR Verification Result`
   check.
```

Under `Recommended Next Branches`, replace:

```markdown
1. `codex/ci-required-checks`

   Add PR build/test gates and packaging smoke.
```

with:

```markdown
1. `codex/staging-deploy-runbook`

   Document and script the first staging deployment path, environment variables,
   migrations, and smoke commands.
```

Then renumber the remaining recommended branches so each branch appears once.

- [ ] **Step 3: Commit the documentation update**

Run:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add docs/progress/2026-05-12-vertical-slice-progress.md docs/roadmap/production-readiness.md
& 'C:\Program Files\Git\cmd\git.exe' commit -m "docs: record GitHub Actions CI readiness"
```

## Task 6: Final Local Verification

**Files:**
- Verify all changed files.

- [ ] **Step 1: Run workflow and script-focused tests**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --filter "FullyQualifiedName~ClientReleaseAutomationTests" -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

Expected:

```text
Passed!  - Failed:     0
```

- [ ] **Step 2: Run the full solution build**

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

- [ ] **Step 3: Run the full solution tests**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

Expected:

```text
Failed: 0
```

- [ ] **Step 4: Run local package smoke once before pushing**

Run:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build-client-packages.ps1 -Version 0.1.0-ci -Channel internal
Get-ChildItem artifacts\client-packages\*.msi | Select-Object Name,Length
```

Expected output includes both MSI names:

```text
afk4-gaming-pc-0.1.0-ci-internal.msi
afk4-operator-app-0.1.0-ci-internal.msi
```

- [ ] **Step 5: Check for whitespace issues and final status**

Run:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' diff --check
& 'C:\Program Files\Git\cmd\git.exe' status --short --branch
```

Expected:

```text
diff --check emits no output
working tree is clean after commits
```

## Task 7: Push And Observe The First Remote Run

**Files:**
- No source edits unless remote validation exposes a workflow defect.

- [ ] **Step 1: Push the branch**

Run:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' push -u origin codex/ci-required-checks
```

Expected:

```text
branch 'codex/ci-required-checks' set up to track 'origin/codex/ci-required-checks'
```

- [ ] **Step 2: Open a pull request**

Open a PR from `codex/ci-required-checks` to `main`.

The expected first remote behavior:

- `PR Verification / Detect Relevant Changes` runs on Linux.
- `PR Verification / Build And Test Windows` runs because workflow and test files changed.
- `PR Verification / PR Verification Result` passes only after the Windows job passes.
- `Package Smoke` does not run on the PR because it is limited to `main` pushes and manual dispatch.
- `Client Packages` does not run automatically because it is manual release packaging.

- [ ] **Step 3: Preserve paid Actions minutes**

If the first remote run fails, inspect logs once and fix the workflow locally before pushing another commit. Do not repeatedly re-run the failed remote job while the YAML defect is visible from logs.

- [ ] **Step 4: Enable branch protection after one successful PR run**

In GitHub repository settings, require the `PR Verification Result` check for `main`.

The required check should be the final result job, not the conditional `Build And Test Windows` job, because docs-only PRs intentionally skip the paid Windows runner.
