# Operator Staging Day-Flow Smoke Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prove the native Windows Operator App can complete the pilot-critical day flow against Coolify staging, fix any reproducible P0 blockers, and record durable evidence.

**Architecture:** Create an isolated staging tenant and owner through public Control Plane APIs, seed only the branch data required for the operator flow, then launch the real WPF/WebView2 host against staging. Drive and observe the embedded React surface through a temporary CDP/Playwright harness outside the repository; any product defect must be reproduced by a failing automated test before the smallest production fix.

**Tech Stack:** Windows 11, PowerShell 5.1, .NET SDK 10.0.203, WPF/WebView2, React 19, Bun 1.3.14, Playwright/CDP, ASP.NET Core Platform API, PostgreSQL staging.

## Global Constraints

- Use `https://afk4.staging.mubi.dev` and the staging database only.
- Keep generated credentials, tokens, screenshots, traces, and smoke transcripts outside the repository.
- Use a fresh tenant slug and idempotency keys for every run; do not mutate an existing club tenant.
- Critical actions pass only after the backend response and authoritative UI refresh confirm them.
- Do not direct-edit PostgreSQL for normal smoke setup or success assertions.
- Do not fix production code without a failing regression test first.

---

### Task 1: Establish Windows and staging baseline

**Files:**
- Verify: `src/AFK4.Operator.App/AFK4.Operator.App.csproj`
- Verify: `src/AFK4.Operator.App.Web/package.json`
- Verify: `tests/AFK4.Operator.App.Tests/AFK4.Operator.App.Tests.csproj`

**Interfaces:**
- Consumes: `origin/main` at the deployed staging revision.
- Produces: a clean native build and focused Operator Web/.NET baseline.

- [x] **Step 1: Verify staging health**

```powershell
Invoke-RestMethod https://afk4.staging.mubi.dev/api/health
```

Expected: HTTP 200 with `status = ok`.

- [x] **Step 2: Restore and build the Operator Web assets**

```powershell
& "$env:USERPROFILE\.bun\bin\bun.exe" install --frozen-lockfile
& "$env:USERPROFILE\.bun\bin\bun.exe" run --cwd src/AFK4.Operator.App.Web build
```

Expected: TypeScript and Vite production build pass.

- [x] **Step 3: Run focused frontend tests**

```powershell
& "$env:USERPROFILE\.bun\bin\bun.exe" run --cwd src/AFK4.Operator.App.Web test
```

Expected: all Operator Web tests pass.

- [x] **Step 4: Build and test the native Operator host**

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build src/AFK4.Operator.App/AFK4.Operator.App.csproj -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Operator.App.Tests/AFK4.Operator.App.Tests.csproj --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

Expected: build and Windows-host tests pass with zero failures.

### Task 2: Provision isolated staging smoke data

**Files:**
- Reference: `scripts/staging-smoke.py`
- Reference: `docs/operations/pilot-branch-setup.md`
- Temporary only: `%TEMP%\afk4-operator-p0\context.json`

**Interfaces:**
- Consumes: platform-admin bootstrap credentials from the staging container environment.
- Produces: organization id, branch id, owner credentials, seats, tariff, client wallet, POS product, and stock held only in the temporary context.

- [x] **Step 1: Create a fresh tenant and accept its owner invite through public APIs**

Use the same request shapes as `scripts/staging-smoke.py`, a timestamped `p0-operator-*` slug, and a generated password. Save secrets to the temporary context with user-only ACLs; print only resource ids and HTTP status codes.

- [x] **Step 2: Seed branch prerequisites through staff APIs**

Create two seats, one active tariff/version, one player with wallet funds, one product category/product, inbound stock, and no open shift. Verify every returned entity belongs to the new organization and branch.

- [x] **Step 3: Verify the starting invariant**

```text
floor map has at least two free seats
current shift is absent
player wallet can cover the wallet portion
product stock is positive
```

### Task 3: Execute the native Operator App critical path

**Files:**
- Exercise: `src/AFK4.Operator.App/Web/WebViewOperatorWindow.xaml.cs`
- Exercise: `src/AFK4.Operator.App.Web/src/App.tsx`
- Temporary only: `%TEMP%\afk4-operator-p0\operator-day-flow.mjs`

**Interfaces:**
- Consumes: Task 2 context and the freshly built native host.
- Produces: screenshots, console/network diagnostics, and backend-confirmed state transitions.

- [x] **Step 1: Launch the real native host against staging**

```powershell
$env:AFK4_OPERATOR_PLATFORM_BASE_URL = 'https://afk4.staging.mubi.dev'
$env:AFK4_OPERATOR_ORGANIZATION_ID = '<temporary context organization id>'
$env:AFK4_OPERATOR_BRANCH_ID = '<temporary context branch id>'
$env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = '--remote-debugging-port=9222'
& .\src\AFK4.Operator.App\bin\Debug\net10.0-windows\AFK4.Operator.App.exe
```

Expected: native WebView2 sign-in screen renders with no startup failure.

- [x] **Step 2: Prove page identity and runtime health**

Attach Playwright over CDP, assert the Operator title/content, no framework overlay, no relevant console/page errors, and capture the first viewport.

- [x] **Step 3: Complete the critical interaction path**

```text
sign in -> open shift -> select/create client -> start session ->
create and confirm reservation -> start reserved session ->
add product -> mixed cash/wallet payment -> open receipt -> refund ->
verify returned stock -> close shift -> sign out/in -> verify authoritative state
```

After each critical action, assert both visible confirmation and the corresponding authenticated API state.

- [x] **Step 4: Exercise desktop behavior**

Verify 100% and 125% scaling-compatible layout bounds, keyboard focus for sign-in/payment/dialog actions, and reconnect after one forced WebView reload.

### Task 4: Fix reproducible P0 blockers

**Files:**
- Modify: only files identified by root-cause tracing.
- Test: nearest existing Operator Web, native host, contract, or API test file.

**Interfaces:**
- Consumes: one deterministic failure with DOM/console/network/API evidence.
- Produces: a regression test and the smallest root-cause fix.

- [x] **Step 1: Record one root-cause hypothesis per defect**

Trace the failing value/action from UI through host bridge and API response; compare with the nearest working flow before editing.

- [x] **Step 2: Write and run the failing regression test**

Run the narrowest existing Bun or .NET test command and confirm it fails for the observed defect, not test setup.

- [x] **Step 3: Implement the minimal fix and rerun GREEN**

Change only the root-cause owner, rerun the focused test, then rerun the exact failing native interaction.

- [x] **Step 4: Repeat until the P0 path passes or an external blocker is proven**

Do not classify physical gaming-PC enforcement or unavailable external payment rails as Operator P0 unless they block this manual/mock-payment staging flow.

### Task 5: Final verification and delivery

**Files:**
- Modify: `docs/progress/2026-05-12-vertical-slice-progress.md`
- Modify when a durable gate changes: `docs/roadmap/production-readiness.md`
- Archive: `docs/superpowers/plans/2026-07-14-operator-staging-day-flow-smoke.md`

**Interfaces:**
- Consumes: passing rerun evidence and any product diff.
- Produces: concise durable project status, verified commit, PR, green latest-head CI, and merged `main`.

- [x] **Step 1: Run blast-radius verification**

Run focused changed tests, Operator Web full tests/build, native Operator tests/build, and full solution build/test if shared contracts, API, migrations, or cross-module code changed.

- [x] **Step 2: Self-review and update durable docs**

Record the staging tenant/resource ids and pass/fail scope without credentials. Move detailed logs/screenshots outside the repository and archive this completed plan.

- [x] **Step 3: Commit and push coherent changes**

Inspect staged status and diff summary, commit verified units, and push `p0/operator-staging-day-flow`.

- [x] **Step 4: Merge only after latest-head verification is green**

Open the PR, wait for the required `PR Verification Result` on the current head, merge, and verify `origin/main` contains the merge.
