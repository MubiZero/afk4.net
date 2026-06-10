# Split Monoliths (Port-Forward) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Split the two giant files — `Program.cs` (13 303 lines) and Operator `App.tsx` (10 469 lines) — into focused modules, behavior-preserving, all tests green.

**Architecture:** A proven split already exists on branch `refactor/program-cs-endpoints` (commits `54ba839` Program.cs→`Endpoints/`, `775f9d7` App.tsx→modules; tested 1001/170 there). Rather than re-cut from scratch or merge the 21-commit-stale branch, **port the split forward**: `git checkout` the new module files + the reduced shells from that branch onto a fresh branch off current `main`, then re-apply the small drift that landed on `main` after the split was cut.

**Drift to reconcile (measured):**
- Backend `Program.cs`: **+115 lines, 0 deletions** since the split's base — pure additions: **7 DI registrations** + **4 `/api/auth/staff/*` endpoints**. (No existing endpoint bodies changed → the split's extracted bodies are still byte-current.)
- Frontend `App.tsx`: **+20/−8 lines** — `AccountPanel` wiring (import + state + button + render) and 4 brand `AFK4`→`AFK4.NET` strings. All in the retained shell **except** the `buildPosReceiptText` brand string, which lives in `operatorHelpers.ts`.

**Tech Stack:** .NET 10 minimal API + EF Core (backend); React 19 + TS + hand-rolled CSS (Operator.App.Web, `bun`/`vite`).

**Branch:** `refactor/split-monoliths` (already created from `main`).

**Invariants (every task):** behavior unchanged; routes/registration order unchanged; tests are the gate. The split commits move bodies **byte-for-byte** — do not rewrite logic.

---

## Phase 1 — Backend: Program.cs → Endpoints/

### Task B1: Bring in Endpoints/ modules and reduced Program.cs, reconcile drift

**Files:**
- Create (via checkout from `refactor/program-cs-endpoints`): `src/AFK4.Platform.Api/Endpoints/` — 36 files (`*Endpoints.cs`, `EndpointHelpers.*.cs`, `EndpointContracts.cs`).
- Replace (via checkout): `src/AFK4.Platform.Api/Program.cs` (13 303 → 397 lines).
- Modify (reconcile): `src/AFK4.Platform.Api/Program.cs` (add 7 DI lines), `src/AFK4.Platform.Api/Endpoints/AuthEndpoints.cs` (add 4 endpoints).
- Reference (read-only): current `main` Program.cs is the byte source for the reconciled lines.

- [ ] **Step 1: Capture the reconciliation source before overwriting.** The 7 DI lines and 4 endpoint bodies exist verbatim in the current working tree's `Program.cs` (HEAD == main content). Run this to see exactly what must be re-homed and its anchors:

```bash
git diff 54ba839^..HEAD -- src/AFK4.Platform.Api/Program.cs
```

Expected: only additions. The **7 DI registrations** (keep in Program.cs):
- `builder.Services.Configure<SmsOptions>(...)`
- `builder.Services.AddSingleton<ISmsTransport>(provider => ...)`
- `builder.Services.AddSingleton<INotificationChannel, SmsChannel>();`
- `builder.Services.Configure<PhoneOtpOptions>(...)`
- `builder.Services.AddSingleton<IPhoneOtpHasher, Sha256PhoneOtpHasher>();`
- `builder.Services.AddSingleton<IPhoneOtpGenerator, RandomPhoneOtpGenerator>();`
- `builder.Services.AddScoped<IStaffPhoneVerificationService, EfStaffPhoneVerificationService>();`

The **4 endpoints** (move into AuthEndpoints.cs):
- `app.MapPost("/api/auth/staff/sign-in-by-phone", ...)`
- `app.MapPost("/api/auth/staff/phone/start-verification", ...)`
- `app.MapPost("/api/auth/staff/phone/confirm", ...)`
- `app.MapGet("/api/auth/staff/phone", ...)`

Keep this diff open in a scratch buffer; it is the byte-exact source for Steps 4–5.

- [ ] **Step 2: Bring in the 36 Endpoints/ files (clean adds — they do not exist on main).**

```bash
git checkout refactor/program-cs-endpoints -- src/AFK4.Platform.Api/Endpoints/
```

- [ ] **Step 3: Replace Program.cs with the reduced shell (397 lines).** This temporarily drops the 7 DI lines and 4 endpoints — restored in Steps 4–5.

```bash
git checkout refactor/program-cs-endpoints -- src/AFK4.Platform.Api/Program.cs
```

- [ ] **Step 4: Re-home the 7 DI registrations into the reduced Program.cs.** Insert them in the DI section (lines 1–326, before `var app = builder.Build();` at ~327) at the **same anchor they occupy on main** — the diff from Step 1 shows the surrounding context lines (they sit next to the existing notification/email DI registrations). Order matters for `INotificationChannel` registration: place `AddSingleton<INotificationChannel, SmsChannel>()` exactly where the diff shows it relative to the email channel. Copy the lines **verbatim** from the Step-1 diff.

- [ ] **Step 5: Move the 4 endpoints into AuthEndpoints.cs.** Open `src/AFK4.Platform.Api/Endpoints/AuthEndpoints.cs`. Inside `public static void MapAuthEndpoints(this WebApplication app)`, append the 4 endpoint registrations **verbatim** from the Step-1 diff (they are already `app.MapXxx(...)` calls — they drop in unchanged since the extension method's parameter is named `app`). Add any missing `using` directives the bodies need — check the new endpoints reference: `IStaffContextAccessor`, `IStaffPhoneVerificationService`, `StaffPhoneStartVerificationRequest`/`StaffPhoneConfirmRequest`/`StaffPhoneConfirmedResponse`/`StaffPhoneStatusResponse`, `PhoneVerificationStartStatus`/`PhoneConfirmStatus`. Most resolve via the file's existing `using AFK4.Platform.Api.Identity;` / `using AFK4.Shared.Contracts.Identity;` — add whichever the compiler reports missing in Step 7.

- [ ] **Step 6: Normalize whitespace in the new files.** `--include` MUST be a path **relative to cwd** (absolute silently matches 0 files):

```bash
dotnet format whitespace src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --include 'src/AFK4.Platform.Api/Endpoints/'
```

- [ ] **Step 7: Build.**

```bash
dotnet build src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --nologo
```

Expected: 0 errors. Fix any missing `using` (Step 5) or misplaced DI (Step 4) until clean.

- [ ] **Step 8: Route parity check — no endpoint lost or duplicated.** Compare the route set of the pre-split monolith against the new structure:

```bash
# Routes registered in the new structure (Endpoints/ + any left in Program.cs)
git grep -hoE 'app\.Map(Get|Post|Put|Patch|Delete)\("([^"]+)"' -- 'src/AFK4.Platform.Api/Endpoints/*.cs' 'src/AFK4.Platform.Api/Program.cs' | sort -u > /tmp/routes_new.txt
# Routes in the monolith as it was on main
git show main:src/AFK4.Platform.Api/Program.cs | grep -oE 'app\.Map(Get|Post|Put|Patch|Delete)\("([^"]+)"' | sort -u > /tmp/routes_old.txt
diff /tmp/routes_old.txt /tmp/routes_new.txt && echo "ROUTE PARITY OK"
```

Expected: `ROUTE PARITY OK` (empty diff). Any difference is a lost/extra endpoint — fix before continuing.

- [ ] **Step 9: Full backend test suite (the real gate).**

```bash
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --nologo
```

Expected: **1055/1055 passing** (≈8–10 min). This includes `StaffPhoneVerificationEndpointTests` (the 4 moved endpoints) and `HealthEndpointTests` (references `HealthResponse` from `EndpointContracts.cs`, global namespace).

- [ ] **Step 10: Commit.**

```bash
git add src/AFK4.Platform.Api/Endpoints/ src/AFK4.Platform.Api/Program.cs
git commit -m "refactor(platform-api): split Program.cs into domain endpoint modules"
```

---

## Phase 2 — Frontend: Operator App.tsx → modules

### Task F1: Bring in operator modules and reduced App.tsx, reconcile drift

**Files:**
- Create (via checkout from `refactor/program-cs-endpoints`) — 15 new module files:
  `operatorTypes.ts`, `operatorPermissions.ts`, `operatorHelpers.ts`, `operatorPrimitives.tsx`,
  `BackendPosWorkspace.tsx`, `BackendBookingWorkspace.tsx`, `BackendPlayersWorkspace.tsx`,
  `BackendPaymentsWorkspace.tsx`, `BackendLogsWorkspace.tsx`, `BackendSettingsWorkspace.tsx`,
  `ReviewWorkspace.tsx`, `MapWorkspace.tsx`, `DashboardWorkspace.tsx`, `MapSidePanel.tsx`, `SummarySidePanel.tsx`
  (all under `src/AFK4.Operator.App.Web/src/`).
- Replace (via checkout): `src/AFK4.Operator.App.Web/src/App.tsx` (10 469 → 1223 lines).
- Modify (reconcile): `src/AFK4.Operator.App.Web/src/App.tsx` (AccountPanel wiring + 3 brand strings), `src/AFK4.Operator.App.Web/src/operatorHelpers.ts` (1 brand string).
- Untouched (already on main, not extracted): `AccountPanel.tsx`, `PhoneVerificationCard.tsx`, `operatorApiClients.ts`, `operatorData.ts`, `PaymentGatewaysWorkspace.tsx`, `platformApi.ts`, `styles.css`, etc.

- [ ] **Step 1: Capture the frontend reconciliation source.** The App.tsx drift is the byte source for the shell edits:

```bash
git diff 775f9d7^..HEAD -- src/AFK4.Operator.App.Web/src/App.tsx
```

This shows: 1 import add (`AccountPanel`), `accountPanelOpen` state, the identity `<span>`→`<button className="top-account" aria-label="Мой аккаунт">` swap, the `<AccountPanel .../>` render block, and 4 brand `AFK4`→`AFK4.NET` edits. **3 brand edits + all the AccountPanel wiring are in shell functions** (`SignInScreen`, `BlockedTenantScreen`, `AppInner`) → re-apply to App.tsx. **1 brand edit** (`'AFK4 Касса'`→`'AFK4.NET Касса'`) is inside `buildPosReceiptText` → applies to `operatorHelpers.ts` (Step 5).

- [ ] **Step 2: Bring in the 15 module files (clean adds).**

```bash
git checkout refactor/program-cs-endpoints -- \
  src/AFK4.Operator.App.Web/src/operatorTypes.ts \
  src/AFK4.Operator.App.Web/src/operatorPermissions.ts \
  src/AFK4.Operator.App.Web/src/operatorHelpers.ts \
  src/AFK4.Operator.App.Web/src/operatorPrimitives.tsx \
  src/AFK4.Operator.App.Web/src/BackendPosWorkspace.tsx \
  src/AFK4.Operator.App.Web/src/BackendBookingWorkspace.tsx \
  src/AFK4.Operator.App.Web/src/BackendPlayersWorkspace.tsx \
  src/AFK4.Operator.App.Web/src/BackendPaymentsWorkspace.tsx \
  src/AFK4.Operator.App.Web/src/BackendLogsWorkspace.tsx \
  src/AFK4.Operator.App.Web/src/BackendSettingsWorkspace.tsx \
  src/AFK4.Operator.App.Web/src/ReviewWorkspace.tsx \
  src/AFK4.Operator.App.Web/src/MapWorkspace.tsx \
  src/AFK4.Operator.App.Web/src/DashboardWorkspace.tsx \
  src/AFK4.Operator.App.Web/src/MapSidePanel.tsx \
  src/AFK4.Operator.App.Web/src/SummarySidePanel.tsx
```

- [ ] **Step 3: Replace App.tsx with the reduced shell (1223 lines).** Temporarily drops the brand + AccountPanel wiring (restored next).

```bash
git checkout refactor/program-cs-endpoints -- src/AFK4.Operator.App.Web/src/App.tsx
```

- [ ] **Step 4: Re-apply the shell edits to App.tsx** (from the Step-1 diff). Apply these exact hunks:
  1. Add `import { AccountPanel } from './AccountPanel';` after the `PaymentGatewaysWorkspace` import.
  2. `SignInScreen`: `<strong>AFK4</strong>` → `<img className="brand-logo" src="/afk4-logo-horizontal.svg" alt="AFK4.NET" />`; `<span>AFK4 Оператор</span>` → `<span>AFK4.NET Оператор</span>`.
  3. `BlockedTenantScreen`: same logo swap; `...поддержкой AFK4 для...` → `AFK4.NET`; `...через AFK4 Operator недоступен.` → `AFK4.NET Operator`.
  4. `AppInner`: same logo swap in `top-command` header; add `const [accountPanelOpen, setAccountPanelOpen] = useState(false);` alongside the other shell state; swap the identity `<span>{operatorDisplayNameLabel(...)} · {shellModeLabel(...)}</span>` for the `<button type="button" className="top-account" aria-label="Мой аккаунт" onClick={() => setAccountPanelOpen(true)}>...</button>`; add the `{accountPanelOpen && backendContext !== null && (<AccountPanel backend={backendContext} displayName={operatorDisplayNameLabel(authSession.displayName)} onClose={() => setAccountPanelOpen(false)} />)}` block immediately after `</header>`.

  Note: `operatorDisplayNameLabel`, `shellModeLabel`, `backendContext`, `authSession`, `config`, `useState` are already in scope in the reduced shell (the pre-split header used them). Only the `AccountPanel` import is new.

- [ ] **Step 5: Re-apply the brand string in operatorHelpers.ts.** In `buildPosReceiptText` (~line 1045), change the receipt header literal `'AFK4 Касса'` → `'AFK4.NET Касса'`.

- [ ] **Step 6: Type-check.**

```bash
cd src/AFK4.Operator.App.Web && bunx tsc -b
```

Expected: 0 errors. (If `tsc` reports an unused/missing import, fix it; the reduced shell's import list was finalized against the split, so the only expected new edit is the `AccountPanel` import.)

- [ ] **Step 7: Tests (the gate).**

```bash
~/.bun/bin/bun test
```

Expected: **173/173 passing** (incl. `App.test.tsx` Settings-personnel tests that depend on the `aria-label="Мой аккаунт"` disambiguation, `AccountPanel`, and operator `PhoneVerificationCard` tests).

- [ ] **Step 8: Production build.**

```bash
~/.bun/bin/bun run build
```

Expected: exit 0.

- [ ] **Step 9: Stale-brand sweep.** Confirm no old `AFK4` (without `.NET`) brand strings survived in the ported modules:

```bash
grep -rnE 'AFK4 (Касса|Оператор|Operator)|поддержк[уи] AFK4|через AFK4 |>AFK4<|<strong>AFK4</strong>' src/AFK4.Operator.App.Web/src || echo "NO STALE BRAND"
```

Expected: `NO STALE BRAND`.

- [ ] **Step 10: Commit.**

```bash
git add src/AFK4.Operator.App.Web/src/
git commit -m "refactor(operator-web): split App.tsx into modules"
```

---

## Phase 3 — Final verification

### Task V1: Whole-refactor verification

- [ ] **Step 1: Re-run both gates from a clean state** to confirm the two commits compose:
  - Backend: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --nologo` → 1055/1055.
  - Frontend: `cd src/AFK4.Operator.App.Web && bunx tsc -b` (0) + `~/.bun/bin/bun test` (173/173) + `~/.bun/bin/bun run build` (0).

- [ ] **Step 2: Confirm file sizes** — `Program.cs` ≈ 400 lines, `App.tsx` ≈ 1240 lines (1223 + the re-applied wiring). Neither is a monolith anymore.

- [ ] **Step 3: Sanity diff vs main** — confirm the change is purely structural: `git diff --stat main..HEAD` should show Program.cs and App.tsx shrinking, the new module files added, and **no behavioral edits** outside the documented reconciliation (7 DI + 4 endpoints relocated; AccountPanel wiring + brand strings re-applied).

- [ ] **Step 4:** Hand off to the controller for the final code review and `superpowers:finishing-a-development-branch`.

---

## Self-Review notes (author)

- **Spec coverage:** Both monoliths split (user-chosen scope). Drift fully enumerated (backend +115 = 7 DI + 4 endpoints; frontend +20/−8 = AccountPanel wiring + 4 brand strings) and each item has a reconciliation step.
- **Byte-exactness:** Endpoint bodies and module bodies come straight from git (`checkout` + verbatim copy of the drift diff), not hand-retyped — logic cannot drift.
- **Type/route consistency:** Route-parity check (B1 Step 8) and the two test suites are the objective gates. `EndpointContracts.cs` keeps result records in the global namespace (tests reference them). `MapAuthEndpoints()` is already registered, so the 4 relocated endpoints need no new registration call.
- **Risk:** lowest-risk path — 95% of the work is proven, tested extraction; the only manual surface is ~115 backend lines + ~20 frontend lines, all copied verbatim from git with anchors shown.
