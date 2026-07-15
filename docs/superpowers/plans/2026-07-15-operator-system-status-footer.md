# Operator System Status Footer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the simplified Operator footer with an authoritative system status row showing operator, real staff roles, current club, realtime/backend health, installed host version, and local time.

**Architecture:** Extend the additive staff-auth response with ordered role names, preserve them through the native protected token snapshot and WebView session projection, and expose the installed Operator assembly version through the existing bootstrap config. Build a pure React footer projection plus a minute-aligned clock hook, then render the supplied reference composition from existing shell state without adding a new API request.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core, shared C# contracts, WPF/WebView2, React 19, TypeScript 6, Bun test, Testing Library, existing AFK4 i18n/tokens, Playwright with the already-approved system Chromium fallback.

## Global Constraints

- Work only in `.worktrees/operator-cash-terminal-redesign` on `feat/operator-cash-terminal-redesign`.
- Role names come from `StaffRoleAssignmentEntity`; never infer roles from permissions.
- Installed version comes from Operator host assembly metadata; never hard-code a production version in React.
- Displayed role data is informational and must not replace permission-based authorization.
- Club name comes from the current authoritative floor-map projection; show an em dash while unavailable.
- `Online` reflects SignalR, while `Server: OK` reflects backend data-source health; do not conflate them.
- The footer stays one row, does not wrap, and removes the cash summary.
- Preserve compatibility with protected snapshots and older hosts by defaulting new lists to empty and treating `appVersion` as optional in TypeScript.
- Use TDD for shared contracts, token behavior, host bootstrap, the footer model, clock, component, and App wiring.
- Do not push or merge without an explicit follow-up request.

## File Structure

- `src/AFK4.Shared.Contracts/Identity/StaffSignInResponse.cs` — additive `RoleNames` auth contract.
- `src/AFK4.Platform.Api/Identity/OpaqueStaffTokenService.cs` — loads ordered role names once and issues them on sign-in/refresh.
- `src/AFK4.Platform.Web/src/api/types.ts`, `src/AFK4.Platform.Web/src/auth/staffTokenStore.ts` — keep the browser staff consumer contract-complete.
- `src/AFK4.Operator.App/Auth/OperatorTokenSnapshot.cs` — protected role persistence with empty-list backward compatibility.
- `src/AFK4.Operator.App/Auth/HttpOperatorAuthApiClient.cs` — copies roles into the protected snapshot.
- `src/AFK4.Operator.App/Web/OperatorWebHostBridge.cs` — projects roles to React for sign-in, refresh, and restore.
- `src/AFK4.Operator.App/Web/OperatorWebBootstrapScript.cs` — exposes installed assembly version.
- `src/AFK4.Operator.App.Web/src/operatorConfig.ts` — optional `appVersion` bootstrap field and explicit browser-dev version.
- `src/AFK4.Operator.App.Web/src/systemStatusModel.ts` — pure role labels, field projection, and semantic tones.
- `src/AFK4.Operator.App.Web/src/useMinuteClock.ts` — minute-aligned local time with visibility refresh.
- `src/AFK4.Operator.App.Web/src/ShellStatusBar.tsx` — reference-matched one-row status presentation.
- `src/AFK4.Operator.App.Web/src/App.tsx` — supplies authoritative session, branch, connection, server, and version state.
- `src/AFK4.Operator.App.Web/src/styles/17-status-bar.css` — fixed-height clusters, dividers, truncation, and tones.

---

### Task 1: Add Real Staff Roles To The Auth Contract

**Files:**
- Modify: `src/AFK4.Shared.Contracts/Identity/StaffSignInResponse.cs`
- Modify: `src/AFK4.Platform.Api/Identity/StaffContext.cs`
- Modify: `src/AFK4.Platform.Api/Identity/OpaqueStaffTokenService.cs`
- Modify: `tests/AFK4.Shared.Contracts.Tests/StaffAuthContractSerializationTests.cs`
- Modify: `tests/AFK4.Platform.Api.Tests/StaffAuthenticationEndpointTests.cs`
- Modify: `src/AFK4.Platform.Web/src/api/types.ts`
- Modify: `src/AFK4.Platform.Web/src/auth/staffTokenStore.ts`
- Modify: `src/AFK4.Platform.Web/src/auth/staffTokenStore.test.ts`

**Interfaces:**
- Produces: `StaffSignInResponse.RoleNames : IReadOnlyList<string>`.
- Produces: `StaffSession.roleNames : string[]` for the existing Platform Web consumer.
- Preserves: current positional `StaffSignInResponse` constructor call sites by adding an init property rather than another positional parameter.

- [ ] **Step 1: Write failing shared-contract and endpoint assertions**

Extend the serialization fixture with role names:

```csharp
var response = new StaffSignInResponse(
    StaffUserId: Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134"),
    OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
    DisplayName: "Tech One",
    AccessToken: "token",
    AccessTokenExpiresAtUtc: DateTimeOffset.Parse("2026-05-12T01:00:00Z"),
    RefreshToken: "refresh-token",
    RefreshTokenExpiresAtUtc: DateTimeOffset.Parse("2026-06-11T01:00:00Z"),
    BranchIds: [Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2")],
    Permissions: [StaffPermissionNames.CreateDeviceEnrollmentCode])
{
    RoleNames = ["cashier_operator", "shift_supervisor"]
};

Assert.Equal(
    ["cashier_operator", "shift_supervisor"],
    copy.RoleNames);
```

In the existing staff sign-in/refresh endpoint test, seed both roles and assert the response is ordered:

```csharp
Assert.Equal(
    [StaffRoleNames.CashierOperator, StaffRoleNames.ShiftSupervisor],
    signInBody!.RoleNames);
Assert.Equal(signInBody.RoleNames, refreshBody!.RoleNames);
```

- [ ] **Step 2: Run the focused .NET tests and verify red**

From the repository root:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Shared.Contracts.Tests\AFK4.Shared.Contracts.Tests.csproj --filter StaffAuthContractSerializationTests
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Platform.Api.Tests\AFK4.Platform.Api.Tests.csproj --filter StaffAuthenticationEndpointTests
```

Expected: FAIL because `StaffSignInResponse.RoleNames` does not exist.

- [ ] **Step 3: Add the additive contract and issue ordered roles**

Use an init property for source and JSON compatibility:

```csharp
public sealed record StaffSignInResponse(
    Guid StaffUserId,
    Guid OrganizationId,
    string DisplayName,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc,
    IReadOnlyList<Guid> BranchIds,
    IReadOnlyList<string> Permissions)
{
    public IReadOnlyList<string> RoleNames { get; init; } = [];
}
```

Add an empty-list-compatible init property to `StaffContext`:

```csharp
public sealed record StaffContext(
    Guid StaffUserId,
    Guid OrganizationId,
    string DisplayName,
    IReadOnlySet<Guid> BranchIds,
    IReadOnlySet<string> Permissions)
{
    public IReadOnlyList<string> RoleNames { get; init; } = [];
}
```

Then issue both permissions and roles from the same assignment query:

```csharp
var roleNames = roles
    .Select(role => role.RoleName)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .Order(StringComparer.OrdinalIgnoreCase)
    .ToArray();

return new StaffContext(
    StaffUserId: user.StaffUserId,
    OrganizationId: user.OrganizationId,
    DisplayName: user.DisplayName,
    BranchIds: roles.Select(role => role.BranchId).ToHashSet(),
    Permissions: PermissionCatalog.GetPermissions(roleNames))
{
    RoleNames = roleNames
};
```

Return the roles on every issued token pair:

```csharp
return new StaffSignInResponse(
    StaffUserId: user.StaffUserId,
    OrganizationId: user.OrganizationId,
    DisplayName: user.DisplayName,
    AccessToken: accessToken,
    AccessTokenExpiresAtUtc: accessTokenExpiresAt,
    RefreshToken: refreshToken,
    RefreshTokenExpiresAtUtc: refreshTokenExpiresAt,
    BranchIds: context.BranchIds.OrderBy(branchId => branchId).ToArray(),
    Permissions: context.Permissions.Order(StringComparer.OrdinalIgnoreCase).ToArray())
{
    RoleNames = context.RoleNames
};
```

- [ ] **Step 4: Keep Platform Web auth storage contract-complete**

Add the field to the TypeScript response and stored session:

```ts
export interface StaffSignInResponse {
  staffUserId: string;
  organizationId: string;
  displayName: string;
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
  refreshTokenExpiresAtUtc: string;
  branchIds: string[];
  permissions: string[];
  roleNames?: string[];
}

export interface StaffSession {
  staffUserId: string;
  organizationId: string;
  displayName: string;
  branchIds: string[];
  permissions: string[];
  roleNames: string[];
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
  refreshTokenExpiresAtUtc: string;
}
```

Map it with rolling-response compatibility:

```ts
roleNames: response.roleNames ?? [],
```

Add a token-store assertion:

```ts
expect(staffSessionFromSignInResponse({
  ...response,
  roleNames: ['cashier_operator']
}).roleNames).toEqual(['cashier_operator']);
```

- [ ] **Step 5: Verify green and commit**

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Shared.Contracts.Tests\AFK4.Shared.Contracts.Tests.csproj --filter StaffAuthContractSerializationTests
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Platform.Api.Tests\AFK4.Platform.Api.Tests.csproj --filter StaffAuthenticationEndpointTests
```

```bash
cd src/AFK4.Platform.Web
bun test src/auth/staffTokenStore.test.ts
cd ../..
git add src/AFK4.Shared.Contracts src/AFK4.Platform.Api/Identity tests/AFK4.Shared.Contracts.Tests tests/AFK4.Platform.Api.Tests/StaffAuthenticationEndpointTests.cs src/AFK4.Platform.Web/src/api/types.ts src/AFK4.Platform.Web/src/auth
git commit -m "feat(identity): expose authenticated staff roles"
```

Expected: focused C# and Bun tests PASS.

---

### Task 2: Preserve Roles And Expose The Installed Host Version

**Files:**
- Modify: `src/AFK4.Operator.App/Auth/OperatorTokenSnapshot.cs`
- Modify: `src/AFK4.Operator.App/Auth/HttpOperatorAuthApiClient.cs`
- Modify: `src/AFK4.Operator.App/Web/OperatorWebHostBridge.cs`
- Modify: `src/AFK4.Operator.App/Web/OperatorWebBootstrapScript.cs`
- Modify: `tests/AFK4.Operator.App.Tests/OperatorAuthApiClientTests.cs`
- Modify: `tests/AFK4.Operator.App.Tests/OperatorTokenStoreTests.cs`
- Modify: `tests/AFK4.Operator.App.Tests/OperatorWebHostBridgeTests.cs`
- Modify: `tests/AFK4.Operator.App.Tests/OperatorWebBootstrapScriptTests.cs`

**Interfaces:**
- Consumes: `StaffSignInResponse.RoleNames` from Task 1.
- Produces: `OperatorTokenSnapshot.RoleNames : IReadOnlyList<string>`.
- Produces: WebView auth JSON `roleNames: string[]`.
- Produces: bootstrap JSON `appVersion: string`.

- [ ] **Step 1: Write failing native snapshot and bridge tests**

Add role data to the token-store fixture:

```csharp
var snapshot = new OperatorTokenSnapshot(
    StaffUserId: Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134"),
    OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
    DisplayName: "Tech One",
    AccessToken: "access-token",
    AccessTokenExpiresAtUtc: DateTimeOffset.Parse("2026-05-12T01:00:00Z"),
    RefreshToken: "refresh-token",
    RefreshTokenExpiresAtUtc: DateTimeOffset.Parse("2026-06-11T01:00:00Z"))
{
    RoleNames = ["cashier_operator"],
    BranchIds = [branchId],
    Permissions = [StaffPermissionNames.ViewFloorMap]
};

Assert.Equal(snapshot.RoleNames, loaded!.RoleNames);
```

In `OperatorAuthApiClientTests`, assert a sign-in response saves roles:

```csharp
Assert.Equal(["cashier_operator"], tokenStore.SavedSnapshot!.RoleNames);
```

In `OperatorWebHostBridgeTests`, inspect sign-in and restored-session payloads:

```csharp
Assert.Equal("cashier_operator", payload.GetProperty("roleNames")[0].GetString());
```

- [ ] **Step 2: Write the failing bootstrap version test**

Change the bootstrap call to an injectable overload and assert exact JSON:

```csharp
var script = OperatorWebBootstrapScript.Create(appOptions, launchTarget, "2.45.1");
using var document = JsonDocument.Parse(
    script["window.__AFK4_OPERATOR_CONFIG__ = ".Length..].TrimEnd(';'));
var root = document.RootElement;
Assert.Equal("2.45.1", root.GetProperty("appVersion").GetString());
```

- [ ] **Step 3: Run the Operator App tests and verify red**

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Operator.App.Tests\AFK4.Operator.App.Tests.csproj --filter "OperatorAuthApiClientTests|OperatorTokenStoreTests|OperatorWebHostBridgeTests|OperatorWebBootstrapScriptTests"
```

Expected: FAIL for missing role and version properties/overload.

- [ ] **Step 4: Persist and project role names**

Add backward-compatible role storage:

```csharp
public IReadOnlyList<string> RoleNames { get; init; } = [];
```

Copy roles in `HttpOperatorAuthApiClient.SaveSnapshotAsync`, then add
`IReadOnlyList<string> RoleNames` to `OperatorWebAuthSession`. Both
`CreateSession(StaffSignInResponse)` and `CreateSession(OperatorTokenSnapshot)`
must pass their real role lists.

- [ ] **Step 5: Resolve and expose the installed version**

Add a testable overload and production resolver:

```csharp
public static string Create(OperatorAppOptions appOptions, OperatorWebShellLaunchTarget launchTarget) =>
    Create(appOptions, launchTarget, ResolveInstalledVersion());

public static string Create(
    OperatorAppOptions appOptions,
    OperatorWebShellLaunchTarget launchTarget,
    string appVersion)
{
    ArgumentNullException.ThrowIfNull(appOptions);
    ArgumentNullException.ThrowIfNull(launchTarget);
    var normalizedVersion = string.IsNullOrWhiteSpace(appVersion) ? "—" : appVersion.Trim();
    var payload = new OperatorWebBootstrapPayload(
        Runtime: "webview2",
        ShellMode: launchTarget.Mode,
        PlatformBaseUrl: appOptions.PlatformBaseUrl.ToString(),
        CurrencyCode: appOptions.CurrencyCode,
        AppVersion: normalizedVersion,
        OrganizationId: appOptions.OrganizationId,
        BranchId: appOptions.BranchId);
    var json = JsonSerializer.Serialize(payload, JsonOptions);
    return $"window.__AFK4_OPERATOR_CONFIG__ = {json};";
}
```

Resolve informational metadata before assembly version:

```csharp
private static string ResolveInstalledVersion()
{
    var assembly = typeof(OperatorWebBootstrapScript).Assembly;
    var informational = assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
        .InformationalVersion?
        .Split('+', 2)[0];
    return !string.IsNullOrWhiteSpace(informational)
        ? informational
        : assembly.GetName().Version?.ToString() ?? "—";
}
```

- [ ] **Step 6: Verify green and commit**

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Operator.App.Tests\AFK4.Operator.App.Tests.csproj --filter "OperatorAuthApiClientTests|OperatorTokenStoreTests|OperatorWebHostBridgeTests|OperatorWebBootstrapScriptTests"
```

```bash
git add src/AFK4.Operator.App tests/AFK4.Operator.App.Tests
git commit -m "feat(operator-host): expose roles and installed version"
```

Expected: all four focused Operator App test classes PASS. If the current Linux host cannot execute the WindowsDesktop testhost, run the matching build with `-p:EnableWindowsTargeting=true` and record the Windows test gate explicitly rather than claiming the tests ran.

---

### Task 3: Build The Authoritative Footer Model And Clock

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/systemStatusModel.ts`
- Create: `src/AFK4.Operator.App.Web/src/systemStatusModel.test.ts`
- Create: `src/AFK4.Operator.App.Web/src/useMinuteClock.ts`
- Create: `src/AFK4.Operator.App.Web/src/useMinuteClock.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/operatorConfig.ts`
- Modify: `src/AFK4.Operator.App.Web/src/authClient.ts`
- Modify: `locales/ru.json`
- Modify: `locales/en.json`
- Modify: `locales/tg.json`
- Regenerate: `packages/i18n/src/messages.ts`

**Interfaces:**
- Consumes: WebView `roleNames` and bootstrap `appVersion` from Task 2.
- Produces: `SystemStatusField`, `SystemStatusViewModel`, `buildSystemStatusModel(input: SystemStatusInput, t: TFunc): SystemStatusViewModel`, and `staffRoleLabel(roleName: string, t: TFunc): string`.
- Produces: `useMinuteClock(locale): string`.

- [ ] **Step 1: Write failing footer-model tests**

Create tests for known, multiple, unknown, and missing values:

```ts
it('projects real roles and independent realtime/server states', () => {
  const model = buildSystemStatusModel({
    operatorName: 'Иванов И.И.',
    roleNames: ['cashier_operator', 'shift_supervisor'],
    clubName: 'Арена',
    realtimeState: 'connected',
    dataSource: 'backend',
    appVersion: '2.45.1'
  }, t);

  expect(model.left.map((field) => field.value)).toEqual([
    'Иванов И.И.', 'Кассир-оператор, Старший смены', 'Арена'
  ]);
  expect(model.connection.tone).toBe('ok');
  expect(model.server.tone).toBe('ok');
  expect(model.version.value).toBe('2.45.1');
});

it('keeps an unknown backend role visible and never fabricates missing data', () => {
  const model = buildSystemStatusModel({
    operatorName: '', roleNames: ['future_role'], clubName: '',
    realtimeState: 'disconnected', dataSource: 'fixture', appVersion: ''
  }, t);
  expect(model.left.map((field) => field.value)).toEqual(['—', 'future_role', '—']);
  expect(model.server.value).toBe('Недоступен');
  expect(model.version.value).toBe('—');
});
```

- [ ] **Step 2: Write failing minute-clock tests**

Use fake timers to prove minute alignment and visibility refresh:

```tsx
it('updates on the next minute boundary and after visibility resume', () => {
  setSystemTime('2026-07-15T11:02:45');
  const { result } = renderHook(() => useMinuteClock('ru-RU'));
  expect(result.current).toBe('11:02');
  act(() => advanceTimersByTime(15_000));
  expect(result.current).toBe('11:03');
  setSystemTime('2026-07-15T11:17:10');
  document.dispatchEvent(new Event('visibilitychange'));
  expect(result.current).toBe('11:17');
});
```

- [ ] **Step 3: Run Bun tests and verify red**

```bash
cd src/AFK4.Operator.App.Web
bun test src/systemStatusModel.test.ts src/useMinuteClock.test.tsx
```

Expected: FAIL because the new modules do not exist.

- [ ] **Step 4: Implement the pure projection**

Use existing `roles.*` catalog keys and raw fallback:

```ts
import type { MessageKey } from '@afk4/i18n';
import type { OperatorRealtimeConnectionState } from './operatorRealtime';
import type { TFunc } from './operatorHelpers';

export type SystemStatusTone = 'neutral' | 'ok' | 'warn' | 'bad';
export interface SystemStatusField {
  key: 'operator' | 'role' | 'club';
  label: string;
  value: string;
}
export interface SystemStatusInput {
  operatorName: string;
  roleNames: string[];
  clubName: string;
  realtimeState: OperatorRealtimeConnectionState;
  dataSource: string;
  appVersion: string;
}
export interface SystemStatusValue {
  value: string;
  tone: SystemStatusTone;
}
export interface SystemStatusViewModel {
  left: SystemStatusField[];
  connection: SystemStatusValue;
  server: SystemStatusValue;
  version: SystemStatusValue;
}

export function staffRoleLabel(roleName: string, t: TFunc): string {
  const key = `roles.${roleName}` as MessageKey;
  const translated = t(key);
  return translated === key ? roleName : translated;
}

export function buildSystemStatusModel(input: SystemStatusInput, t: TFunc): SystemStatusViewModel {
  const value = (candidate: string | null | undefined) => candidate?.trim() || '—';
  const connection: SystemStatusValue = input.realtimeState === 'connected'
    ? { value: t('op.status.online'), tone: 'ok' }
    : input.realtimeState === 'connecting' || input.realtimeState === 'reconnecting'
      ? { value: t('op.status.reconnecting'), tone: 'warn' }
      : { value: t('op.status.offline'), tone: 'bad' };
  return {
    left: [
      { key: 'operator', label: t('op.status.operator'), value: value(input.operatorName) },
      { key: 'role', label: t('op.status.role'), value: input.roleNames.length ? input.roleNames.map(role => staffRoleLabel(role, t)).join(', ') : '—' },
      { key: 'club', label: t('op.status.club'), value: value(input.clubName) }
    ],
    connection,
    server: input.dataSource === 'backend'
      ? { value: t('op.status.serverOk'), tone: 'ok' }
      : { value: t('op.status.serverUnavailable'), tone: 'bad' },
    version: { value: value(input.appVersion), tone: 'neutral' }
  };
}
```

- [ ] **Step 5: Implement the minute-aligned hook**

```ts
export function useMinuteClock(locale: string): string {
  const [now, setNow] = useState(() => new Date());
  useEffect(() => {
    let timer = window.setTimeout(function tick() {
      setNow(new Date());
      timer = window.setTimeout(tick, 60_000);
    }, 60_000 - Date.now() % 60_000);
    const refresh = () => { if (document.visibilityState === 'visible') setNow(new Date()); };
    document.addEventListener('visibilitychange', refresh);
    return () => {
      window.clearTimeout(timer);
      document.removeEventListener('visibilitychange', refresh);
    };
  }, []);
  return new Intl.DateTimeFormat(locale, { hour: '2-digit', minute: '2-digit' }).format(now);
}
```

- [ ] **Step 6: Extend React contracts and localized copy**

```ts
export interface OperatorConfig {
  runtime: string;
  shellMode: string;
  platformBaseUrl: string;
  currencyCode: string;
  organizationId?: string;
  branchId?: string;
  appVersion?: string;
}

const fallbackConfig: OperatorConfig = {
  runtime: 'browser-dev',
  shellMode: 'vite-dev',
  platformBaseUrl: 'http://localhost:5074/',
  currencyCode: 'TJS',
  appVersion: 'dev'
};

export interface OperatorAuthSession {
  staffUserId: string;
  organizationId: string;
  displayName: string;
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshTokenExpiresAtUtc: string;
  branchIds: string[];
  activeBranchId?: string;
  permissions: string[];
  roleNames?: string[];
}
```

Add `op.status.operator`, `op.status.role`, `op.status.club`,
`op.status.online`, `op.status.reconnecting`, `op.status.offline`,
`op.status.server`, `op.status.serverOk`, `op.status.serverUnavailable`, and
`op.status.version` in ru/en/tg, then regenerate:

```bash
cd packages/i18n
bun run gen
bun test src/messages.test.ts
```

- [ ] **Step 7: Verify green and commit**

```bash
cd src/AFK4.Operator.App.Web
bun test src/systemStatusModel.test.ts src/useMinuteClock.test.tsx
bunx tsc --noEmit
cd ../..
git add src/AFK4.Operator.App.Web/src/systemStatusModel* src/AFK4.Operator.App.Web/src/useMinuteClock* src/AFK4.Operator.App.Web/src/operatorConfig.ts src/AFK4.Operator.App.Web/src/authClient.ts locales packages/i18n/src/messages.ts
git commit -m "feat(operator-web): model authoritative system status"
```

Expected: model, hook, i18n, and typecheck PASS.

---

### Task 4: Render The Reference Footer And Wire Live State

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/ShellStatusBar.tsx`
- Create: `src/AFK4.Operator.App.Web/src/ShellStatusBar.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/App.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/App.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/styles/02-shell.css`
- Modify: `src/AFK4.Operator.App.Web/src/styles/17-status-bar.css`
- Modify: `src/AFK4.Operator.App.Web/src/styles/qaContrast.test.ts`

**Interfaces:**
- Consumes: `buildSystemStatusModel`, `useMinuteClock`, `OperatorAuthSession.roleNames`, `OperatorConfig.appVersion`.
- Produces: the final one-row reference footer with real live values.

- [ ] **Step 1: Write failing component semantics tests**

Render explicit inputs and assert all permanent fields:

```tsx
render(<ShellStatusBar
  operatorName="Иванов И.И."
  roleNames={['cashier_operator']}
  clubName="Арена"
  realtimeState="connected"
  realtimeError={null}
  dataSource="backend"
  appVersion="2.45.1"
  workspaceFeedback={null}
/>);

expect(screen.getByText('Иванов И.И.')).toBeInTheDocument();
expect(screen.getByText('Кассир-оператор')).toBeInTheDocument();
expect(screen.getByText('Арена')).toBeInTheDocument();
expect(screen.getByText('Онлайн')).toBeInTheDocument();
expect(screen.getByText('OK')).toBeInTheDocument();
expect(screen.getByText('2.45.1')).toBeInTheDocument();
expect(screen.getByText(/^\d{2}:\d{2}$/)).toBeInTheDocument();
expect(screen.queryByText(/Касса:/)).not.toBeInTheDocument();
```

- [ ] **Step 2: Write failing App wiring test**

Extend the restored native session with `roleNames: ['cashier_operator']` and
bootstrap config with `appVersion: '2.45.1'`, then assert the rendered App footer
contains the real operator, role, current mocked branch, and version.

- [ ] **Step 3: Write failing CSS guards**

```ts
expect(statusCss).toMatch(/\.signals-strip\s*\{[^}]*white-space:\s*nowrap/s);
expect(statusCss).toMatch(/\.signal-field\s*\{[^}]*border-left:\s*1px solid/s);
expect(statusCss).toMatch(/\.signal-left\s*\{[^}]*overflow:\s*hidden/s);
expect(statusCss).not.toContain('.signal-pos');
```

- [ ] **Step 4: Run tests and verify red**

```bash
cd src/AFK4.Operator.App.Web
bun test src/ShellStatusBar.test.tsx src/styles/qaContrast.test.ts
bun test src/App.test.tsx -t "system status footer"
```

Expected: FAIL because the footer still accepts `posText` and omits identity, roles, club, version, and time.

- [ ] **Step 5: Implement the status row**

Render left and right clusters from the model:

```tsx
const model = buildSystemStatusModel({
  operatorName, roleNames, clubName, realtimeState, dataSource, appVersion
}, t);
const time = useMinuteClock(locale);
const connectionTitle = `${realtimeLabel(realtimeState, realtimeError, t)} · ${dataSourceLabel(dataSource, t)}`;

return <footer className="signals-strip">
  <div className="signal-cluster signal-left">
    {model.left.map(field => <span className={`signal-field signal-${field.key}`} key={field.key} title={`${field.label}: ${field.value}`}>
      <span>{field.label}:</span><strong>{field.value}</strong>
    </span>)}
  </div>
  {workspaceFeedback && <span className="signal-feedback"><LockKeyhole size={13} />{workspaceFeedback}</span>}
  <div className="signal-cluster signal-right">
    <span className={`signal-field tone-${model.connection.tone}`} title={connectionTitle}><i className={`signal-dot ${model.connection.tone}`} />{model.connection.value}</span>
    <span className={`signal-field tone-${model.server.tone}`}><span>{t('op.status.server')}:</span><strong>{model.server.value}</strong></span>
    <span className="signal-field"><span>{t('op.status.version')}:</span><strong>{model.version.value}</strong></span>
    <time className="signal-field" dateTime={new Date().toISOString()}>{time}</time>
  </div>
</footer>;
```

`App` supplies:

```tsx
<ShellStatusBar
  operatorName={operatorDisplayName}
  roleNames={authSession.roleNames ?? []}
  clubName={displayedFloorMap.source === 'backend' ? displayedFloorMap.branchName : ''}
  realtimeState={realtimeState}
  realtimeError={realtimeError}
  dataSource={floorMap.source}
  appVersion={config.appVersion ?? ''}
  workspaceFeedback={workspaceFeedback}
/>
```

- [ ] **Step 6: Implement reference-matched CSS**

```css
.signals-strip {
  grid-column: 1 / 4;
  display: flex;
  min-width: 0;
  height: 32px;
  align-items: center;
  overflow: hidden;
  padding: 0 16px;
  border-top: 1px solid var(--border-default);
  background: var(--surface-elevated);
  white-space: nowrap;
}
.signal-cluster { display: flex; min-width: 0; align-items: center; height: 100%; }
.signal-left { flex: 1; overflow: hidden; }
.signal-right { flex: none; margin-left: auto; }
.signal-field { display: inline-flex; min-width: 0; align-items: center; gap: 4px; padding: 0 14px; border-left: 1px solid var(--border-soft); color: var(--text-secondary); font-size: 11px; }
.signal-field:first-child { padding-left: 0; border-left: 0; }
.signal-field strong { overflow: hidden; color: var(--text-primary); font-weight: 600; text-overflow: ellipsis; }
.signal-left .signal-club { flex-shrink: 3; }
.signal-left .signal-role { flex-shrink: 2; }
.signal-left .signal-operator { flex-shrink: 1; }
```

Keep existing dot colors and add explicit `.tone-ok`, `.tone-warn`, and
`.tone-bad` text colors. Do not introduce cards, shadows, gradients, emoji, or
custom SVG assets.

- [ ] **Step 7: Verify green and commit**

```bash
cd src/AFK4.Operator.App.Web
bun test src/ShellStatusBar.test.tsx src/styles/qaContrast.test.ts
bun test src/App.test.tsx -t "system status footer"
bunx tsc --noEmit
cd ../..
git add src/AFK4.Operator.App.Web/src/ShellStatusBar* src/AFK4.Operator.App.Web/src/App* src/AFK4.Operator.App.Web/src/styles
git commit -m "feat(operator-shell): add authoritative system footer"
```

Expected: component, App wiring, CSS guard, and typecheck PASS.

---

### Task 5: Rendered QA, Full Verification, And Durable State

**Files:**
- Modify: `design-qa.md`
- Modify: `docs/progress/2026-05-12-vertical-slice-progress.md`
- Modify: `docs/superpowers/plans/README.md`
- Modify: `docs/superpowers/specs/README.md`
- Move after completion: this plan and its design spec to `docs/archive/superpowers/`.

**Interfaces:**
- Consumes: completed backend/native/web footer implementation.
- Produces: same-state reference comparison, verification evidence, archived design/plan, and clean topic branch.

- [ ] **Step 1: Start or reuse the Operator preview**

```bash
cd src/AFK4.Operator.App.Web
bunx vite --host 127.0.0.1 --port 5177
```

Use the existing preview login and the already-approved system Chromium fallback.

- [ ] **Step 2: Capture all required states outside the repository**

Capture:

```text
/tmp/afk4-system-footer-dark-1920.png
/tmp/afk4-system-footer-dark-1280.png
/tmp/afk4-system-footer-light-1280.png
```

The dark 1920 capture must show a real preview operator, localized role,
`AFK4 Dushanbe`, Online, Server OK, `dev`, and current time. Record browser
console/page errors and assert zero.

- [ ] **Step 3: Run the blocking design comparison**

Normalize the supplied footer reference and implementation footer crop to the
same height, combine them into one side-by-side comparison input, and inspect it
with `view_image`. Check field order, dividers, row height, typography, semantic
tones, right alignment, truncation, and absence of wrapping. Update
`design-qa.md` with each P0/P1/P2 finding and repeat until the exact final line is:

```text
final result: passed
```

- [ ] **Step 4: Run the full affected verification gate**

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Shared.Contracts.Tests\AFK4.Shared.Contracts.Tests.csproj
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Platform.Api.Tests\AFK4.Platform.Api.Tests.csproj
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Operator.App.Tests\AFK4.Operator.App.Tests.csproj
```

```bash
cd packages/i18n && bun test && cd ../..
cd src/AFK4.Platform.Web && bun run test && bun run build && cd ../..
cd src/AFK4.Operator.App.Web && bun run test && bun run build && cd ../..
dotnet build AFK4.sln -p:EnableWindowsTargeting=true -p:NuGetAudit=false
git diff --check
```

Expected: all portable suites and builds PASS. Run the Windows Operator testhost
on Windows if it cannot execute in the current Linux environment; report that
environment boundary explicitly.

- [ ] **Step 5: Update and archive durable project state**

Record the role/version contract, footer behavior, exact test counts, build
result, rendered sizes/themes, and remaining environment-limited verification in
the compact progress snapshot. Move:

```text
docs/superpowers/plans/2026-07-15-operator-system-status-footer.md
  -> docs/archive/superpowers/plans/2026-07-15-operator-system-status-footer.md
docs/superpowers/specs/2026-07-15-operator-system-status-footer-design.md
  -> docs/archive/superpowers/specs/2026-07-15-operator-system-status-footer-design.md
```

Remove the active index entries and list the work among implemented archived
Operator UI specs/plans.

- [ ] **Step 6: Commit completion evidence**

```bash
git add -A
git diff --cached --check
git diff --cached --stat
git commit -m "docs(operator): record system status footer"
git status --short --branch
git log --oneline --decorate origin/main..HEAD
```

Expected: clean topic branch, unpushed and ready for review.
