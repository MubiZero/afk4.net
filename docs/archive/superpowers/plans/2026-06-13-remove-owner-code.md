# Remove Owner-Code — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Полностью удалить фичу owner-code (генерация кода владельца + enroll устройства по коду без логина сотрудника) из backend, platform-admin web, .NET wizard-хоста, i18n, БД и тестов. Authed install-путь (логин сотрудника) — единственный остаётся; визард его уже использует.

**Architecture:** Удаляем «листья → корень», чтобы солюшн компилировался и тесты были зелёными на каждом коммите: сначала независимый platform-web, затем wizard-хост (перестаёт использовать owner-code контракты), затем backend-эндпоинты+сервис, затем backend-core+контракты+permission, затем drop-миграция БД, финальная сверка. Тесты на удаляемый код удаляются в той же задаче, что и код.

**Tech Stack:** .NET 10 (C#, EF Core, минимал-API), React+TS (Vite, bun test), `@afk4/i18n` (flat-JSON + codegen).

**Спека:** `docs/superpowers/specs/2026-06-13-remove-owner-code-design.md`
**Ветка:** `chore/remove-owner-code` (поверх `feature/setup-wizard-design-polish`).

**Команды (справочно):**
- bun: `~/.bun/bin/bun`, `~/.bun/bin/bunx`.
- platform-web: `cd src/AFK4.Platform.Web && ~/.bun/bin/bun test` и `~/.bun/bin/bun run build`.
- i18n: `cd packages/i18n && ~/.bun/bin/bun run gen` и `~/.bun/bin/bun test`.
- backend build: `dotnet build src/AFK4.Platform.Api/AFK4.Platform.Api.csproj -c Debug --nologo -v q`.
- backend tests: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj -c Debug --nologo`.
- wizard tests: `dotnet test tests/AFK4.SetupWizard.Tests/AFK4.SetupWizard.Tests.csproj -c Debug --nologo`.
- НИКАКОЙ AI-атрибуции в коммитах.

---

## File Structure

| Action | File | Task |
|--------|------|------|
| Delete | `src/AFK4.Platform.Web/src/club/install/OwnerCodePanel.tsx` (+`.test.tsx`) | 1 |
| Delete | `src/AFK4.Platform.Web/src/club/install/useOwnerCode.ts` (+`.test.ts`) | 1 |
| Delete | `src/AFK4.Platform.Web/src/api/clients/ownerCode.ts` | 1 |
| Modify | `api/types.ts`, `api/clubApi.ts`, `club/install/installModel.ts`, `club/install/InstallScreen.tsx`, `App.tsx`, `App.test.tsx` | 1 |
| Modify | `locales/{ru,en,tg}.json`, regen `packages/i18n` | 1 |
| Modify | `SetupWizard.Core/SetupWizardContracts.cs`, `SetupWizardApiClient.cs`, `SetupWizard/Web/SetupWizardWebHostBridge.cs`, `SetupWizard/Preview/PreviewSetupWizard.cs` | 2 |
| Modify/Delete | `tests/AFK4.SetupWizard.Tests/SetupWizardApiClientTests.cs` | 2 |
| Modify | `Endpoints/DeviceEndpoints.cs`, `Install/IInstallService.cs`, `Install/EfInstallService.cs`, `Install/InstallOperationResult.cs` | 3 |
| Delete | `tests/AFK4.Platform.Api.Tests/InstallEndpointTests.cs` | 3 |
| Delete | `Identity/OwnerCodes/*`, `Data/OwnerCodeEntity.cs`, `Endpoints/OwnerCodeEndpoints.cs`, Shared.Contracts owner-code DTOs, unauth install contracts | 4 |
| Modify | `Program.cs`, `Data/PlatformDbContext.cs`, `Data/DeviceEntity.cs`, `StaffPermissionNames.cs`, `PermissionCatalog.cs`, `Audit/AuditActionNames.cs` | 4 |
| Delete | `tests/AFK4.Platform.Api.Tests/OwnerCodeEndpointTests.cs`, `tests/AFK4.Shared.Contracts.Tests/InstallContractSerializationTests.cs` | 4 |
| Create | EF migration (drop table + column) + snapshot update | 5 |

---

## Task 1: Platform-admin web — remove owner-code UI + i18n

**Files:** see table (Task 1 rows). Independent of backend (the deleted endpoints still exist server-side at this point; we just stop calling them from the UI).

- [ ] **Step 1: Delete owner-code-only web files**

```bash
cd /d/afk4.net
git rm src/AFK4.Platform.Web/src/club/install/OwnerCodePanel.tsx \
       src/AFK4.Platform.Web/src/club/install/OwnerCodePanel.test.tsx \
       src/AFK4.Platform.Web/src/club/install/useOwnerCode.ts \
       src/AFK4.Platform.Web/src/club/install/useOwnerCode.test.ts \
       src/AFK4.Platform.Web/src/api/clients/ownerCode.ts
```

- [ ] **Step 2: Remove owner-code types from `api/types.ts`**

Delete the two interface blocks (currently lines 43–54):
```typescript
export interface OwnerCodeSummary {
  codeSuffix: string;
  expiresAtUtc: string;
  lastUsedAtUtc: string | null;
  failedAttemptCount: number;
}

export interface OwnerCodeIssued {
  ownerCode: string;
  codeSuffix: string;
  expiresAtUtc: string;
}
```

- [ ] **Step 3: Remove owner-code wiring from `api/clubApi.ts`**

Remove these three lines (import, field declaration, init):
```typescript
import { OwnerCodeApi } from './clients/ownerCode';
  public readonly ownerCode: OwnerCodeApi;
    this.ownerCode = new OwnerCodeApi(this.transport);
```

- [ ] **Step 4: Trim `club/install/installModel.ts`**

Remove the line-1 import `import type { OwnerCodeSummary, OwnerCodeIssued } from '@/api/types';`, and remove the `OwnerCodeView` interface and `toOwnerCodeView` function entirely. KEEP `getSetupMsiUrl()`. Resulting file contains only `getSetupMsiUrl`.

- [ ] **Step 5: Rewrite `club/install/InstallScreen.tsx`**

Replace the whole file with (drops `OwnerCodePanel`, the `client`/`canManage` props, and the owner-code wizard step):
```tsx
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { EmptyState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import { getSetupMsiUrl } from './installModel';

export function InstallScreen({ branches }: {
  branches: { branchId: string; name: string; city?: string }[];
}) {
  const { t } = useI18n();
  const msiUrl = getSetupMsiUrl();

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="text-lg font-semibold">{t('install.title')}</h2>
          <p className="text-sm text-muted-foreground">{t('install.subtitle')}</p>
        </div>
        <Button asChild>
          <a href={msiUrl} download>{t('install.download')}</a>
        </Button>
      </div>

      <Card>
        <CardHeader><CardTitle>{t('install.wizard.title')}</CardTitle></CardHeader>
        <CardContent className="flex flex-col gap-3">
          <ol className="list-decimal space-y-1 pl-5 text-sm">
            <li>{t('install.wizard.step1')}</li>
            <li>{t('install.wizard.step2')}</li>
            <li>{t('install.wizard.step3')}</li>
            <li>{t('install.wizard.step4')}</li>
          </ol>
          <pre className="rounded-md bg-muted px-3 py-2 font-mono text-xs">msiexec /i AFK4-Agent.msi</pre>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>{t('install.branches.title')}</CardTitle></CardHeader>
        <CardContent>
          {branches.length === 0 ? (
            <EmptyState message={t('install.branches.empty')} />
          ) : (
            <ul className="grid grid-cols-1 gap-2 sm:grid-cols-2">
              {branches.map(b => (
                <li key={b.branchId} className="rounded-md border px-3 py-2">
                  <div className="text-sm font-medium">{b.name}</div>
                  {b.city !== undefined && b.city.length > 0 && <div className="text-xs text-muted-foreground">{b.city}</div>}
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
```

- [ ] **Step 6: Update `App.tsx` render of `InstallScreen`**

Replace the render block (currently ~lines 496–499) with the props-trimmed version:
```tsx
        <InstallScreen
          branches={session.branchIds.map(id => ({ branchId: id, name: directory[id]?.name ?? t('branches.unnamed'), city: directory[id]?.city }))}
        />
```
Leave the `import { InstallScreen }` line as-is.

- [ ] **Step 7: Remove the owner-code test from `App.test.tsx`**

Delete the test case `'generates and reveals an owner code from /club/install'` (~lines 433–451) and the owner-code endpoint mock setup tied to it (the `/api/staff/me/owner-code` mock at ~lines 64, 95, 98). Leave unrelated mocks/tests intact.

- [ ] **Step 8: i18n — remove owner-code keys, rewrite copy**

In `locales/ru.json`, `locales/en.json`, `locales/tg.json`:
- Delete all 12 keys `install.ownerCode.*` (`title, noAccess, none, validUntil, lastUsed, failed, generate, rotate, reason, generated, rotated, error`).
- Rewrite `install.subtitle`:
  - ru: `"install.subtitle": "Установите агента на каждый ПК и привяжите его через мастер установки.",`
  - en: `"install.subtitle": "Install the agent on each PC and enrol it via the setup wizard.",`
  - tg: `"install.subtitle": "Агентро ба ҳар ПК насб кунед ва онро тавассути устоди насб пайваст кунед.",` (черновой перевод — на ревью носителю)
- Rewrite `install.wizard.step2` (was «Введите 8-значный код владельца»):
  - ru: `"install.wizard.step2": "Войдите в мастере под учётной записью сотрудника.",`
  - en: `"install.wizard.step2": "Sign in with your staff account in the wizard.",`
  - tg: `"install.wizard.step2": "Дар устод бо ҳисоби корманд ворид шавед.",` (черновой перевод — на ревью носителю)
- Keep all other `install.*` keys.

Then regen:
```bash
cd /d/afk4.net/packages/i18n && ~/.bun/bin/bun run gen
```

- [ ] **Step 9: Verify web + i18n green**

```bash
cd /d/afk4.net/packages/i18n && ~/.bun/bin/bun test
cd /d/afk4.net/src/AFK4.Platform.Web && ~/.bun/bin/bun test && ~/.bun/bin/bun run build
```
Expected: i18n tests pass (parity + guard); platform-web tests pass; build clean. If a leftover reference to `ownerCode`/`OwnerCode`/`OwnerCodePanel`/`useOwnerCode` remains, the build/tsc fails — fix it. Sanity grep:
```bash
cd /d/afk4.net && grep -rniE "ownerCode|owner-code" src/AFK4.Platform.Web/src --include=*.ts --include=*.tsx
```
Expected: no matches.

- [ ] **Step 10: Commit**

```bash
git add -A
git commit -m "chore(platform-web): remove owner-code UI, client and i18n keys"
```

---

## Task 2: SetupWizard host + Core — remove owner-code client path

**Files:** `src/AFK4.SetupWizard.Core/SetupWizardContracts.cs`, `src/AFK4.SetupWizard.Core/SetupWizardApiClient.cs`, `src/AFK4.SetupWizard/Web/SetupWizardWebHostBridge.cs`, `src/AFK4.SetupWizard/Preview/PreviewSetupWizard.cs`, `tests/AFK4.SetupWizard.Tests/SetupWizardApiClientTests.cs`.

This removes the wizard's *use* of the owner-code (unauth) contracts. The contract types themselves are deleted later (Task 4) — they still exist now, just unused.

- [ ] **Step 1: Remove owner-code methods from `ISetupWizardApiClient`**

In `SetupWizardContracts.cs`, delete these three interface members (keep all sign-in/forgot/reset and all `*AuthenticatedAsync`):
```csharp
    Task<InstallDiscoverResponse> DiscoverAsync(string ownerCode, CancellationToken cancellationToken);

    Task<InstallCreateSeatResponse> CreateSeatAsync(
        string ownerCode,
        Guid branchId,
        Guid zoneId,
        string name,
        CancellationToken cancellationToken);

    Task<InstallEnrollResponse> EnrollAsync(InstallEnrollRequest request, CancellationToken cancellationToken);
```

- [ ] **Step 2: Remove the matching impls in `SetupWizardApiClient.cs`**

Delete the three method implementations (`DiscoverAsync(string ownerCode…)`, `CreateSeatAsync(string ownerCode…)`, `EnrollAsync(InstallEnrollRequest…)`). Keep the `*AuthenticatedAsync` implementations and sign-in/forgot/reset. If the file imports `InstallEnrollRequest` only for the removed method and it becomes unused, remove that using (the build will flag unused if treated as error; otherwise leave — but prefer removing the now-dead method bodies fully).

- [ ] **Step 3: Rewrite the preview `FakeApiClient` to not depend on removed methods**

In `PreviewSetupWizard.cs`, the authed fakes currently delegate to the owner-code fakes. Replace the owner-code methods + delegating authed methods with self-contained authed fakes. Delete:
```csharp
        public Task<InstallDiscoverResponse> DiscoverAsync(string ownerCode, CancellationToken cancellationToken)
            => Task.FromResult(new InstallDiscoverResponse("Preview Owner", [BuildBranch()]));

        public Task<InstallCreateSeatResponse> CreateSeatAsync(
            string ownerCode,
            Guid branchId,
            Guid zoneId,
            string name,
            CancellationToken cancellationToken)
            => Task.FromResult(new InstallCreateSeatResponse(
                OrgId, branchId, zoneId, Guid.NewGuid(), name, SortOrder: 99));

        public Task<InstallEnrollResponse> EnrollAsync(InstallEnrollRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new InstallEnrollResponse(
                OrgId,
                request.BranchId,
                DeviceId: Guid.NewGuid(),
                CredentialId: Guid.NewGuid(),
                CredentialSecret: "preview-secret",
                EnrollmentState: DeviceEnrollmentStateNames.Approved,
                ApiBaseUrl: "https://preview.local",
                UpdateChannel: "stable",
                EnrolledAtUtc: DateTimeOffset.UnixEpoch));
```
And replace the two delegating authed methods:
```csharp
        public Task<InstallCreateSeatResponse> CreateSeatAuthenticatedAsync(
            string accessToken, Guid branchId, Guid zoneId, string name, CancellationToken cancellationToken)
            => CreateSeatAsync(ownerCode: string.Empty, branchId, zoneId, name, cancellationToken);

        public Task<InstallEnrollResponse> EnrollAuthenticatedAsync(
            string accessToken, AuthenticatedInstallEnrollRequest request, CancellationToken cancellationToken)
            => EnrollAsync(
                new InstallEnrollRequest(
                    string.Empty, request.BranchId, request.SeatId, request.Role,
                    request.DisplayName, request.MachineName, request.DevicePublicKey),
                cancellationToken);
```
with self-contained versions:
```csharp
        public Task<InstallCreateSeatResponse> CreateSeatAuthenticatedAsync(
            string accessToken, Guid branchId, Guid zoneId, string name, CancellationToken cancellationToken)
            => Task.FromResult(new InstallCreateSeatResponse(
                OrgId, branchId, zoneId, Guid.NewGuid(), name, SortOrder: 99));

        public Task<InstallEnrollResponse> EnrollAuthenticatedAsync(
            string accessToken, AuthenticatedInstallEnrollRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new InstallEnrollResponse(
                OrgId,
                request.BranchId,
                DeviceId: Guid.NewGuid(),
                CredentialId: Guid.NewGuid(),
                CredentialSecret: "preview-secret",
                EnrollmentState: DeviceEnrollmentStateNames.Approved,
                ApiBaseUrl: "https://preview.local",
                UpdateChannel: "stable",
                EnrolledAtUtc: DateTimeOffset.UnixEpoch));
```
(Keep `DiscoverAuthenticatedAsync` as-is — it already builds its own response.)

- [ ] **Step 4: Strip owner-code from `SetupWizardWebHostBridge.cs`**

- Remove the three switch arms (lines ~52–54):
```csharp
                "wizard:discover" => await DiscoverAsync(request.Payload, cancellationToken),
                "wizard:createSeat" => await CreateSeatAsync(request.Payload, cancellationToken),
                "wizard:enroll" => await EnrollAsync(request.Payload, cancellationToken),
```
- Remove the three private methods `DiscoverAsync(JsonElement…)`, `CreateSeatAsync(JsonElement…)`, `EnrollAsync(JsonElement…)` (the owner-code variants; keep the `*Authenticated*` ones).
- Remove the now-dead `ValidateOwnerCode` static method (lines ~516–549).
- Remove the three payload records `WizardDiscoverPayload`, `WizardCreateSeatPayload`, `WizardEnrollPayload` (keep `WizardCreateSeatAuthPayload`, `WizardEnrollAuthPayload`).
- Remove the three `ErrorCodeFor` arms (lines ~574–576) `"wizard:discover"`, `"wizard:createSeat"`, `"wizard:enroll"`. (The auth arms map to the same error-code strings, so those constants remain in use.)

- [ ] **Step 5: Remove owner-code cases from `SetupWizardApiClientTests.cs`**

Delete any test(s) that call the removed `DiscoverAsync(ownerCode)`/`CreateSeatAsync(ownerCode)`/`EnrollAsync(InstallEnrollRequest)`. Keep tests for authed methods + sign-in/reset.

- [ ] **Step 6: Build + test the wizard**

```bash
cd /d/afk4.net
dotnet build src/AFK4.SetupWizard/AFK4.SetupWizard.csproj -c Debug --nologo -v q
dotnet test tests/AFK4.SetupWizard.Tests/AFK4.SetupWizard.Tests.csproj -c Debug --nologo
```
Expected: build 0 errors, tests pass. (If `AFK4.SetupWizard.exe` is locked by a running preview, kill it first: `taskkill //IM AFK4.SetupWizard.exe //F`.)

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "chore(setup-wizard): drop owner-code client path from host bridge and core"
```

---

## Task 3: Backend — remove unauth install endpoints + owner-code in install service

**Files:** `Endpoints/DeviceEndpoints.cs`, `Install/IInstallService.cs`, `Install/EfInstallService.cs`, `Install/InstallOperationResult.cs`; delete `tests/AFK4.Platform.Api.Tests/InstallEndpointTests.cs`.

- [ ] **Step 1: Delete the three unauth install endpoints in `DeviceEndpoints.cs`**

Remove the three consecutive `app.MapPost(...)` blocks (lines ~155–286): `/api/install/discover`, `/api/install/enroll`, `/api/install/seats`. KEEP the authed blocks `/api/install/auth/*` (288–406) and the shared helpers `WriteInstallAuditAsync`, `GetSourceIp`, `ToInstallHttpResult`.

- [ ] **Step 2: Remove the unauth methods from `IInstallService.cs`**

Delete the unauth trio (keep the `*ForStaffAsync` trio):
```csharp
    Task<InstallOperationResult<InstallDiscoverResponse>> DiscoverAsync(
        InstallDiscoverRequest request,
        CancellationToken cancellationToken);

    Task<InstallOperationResult<InstallEnrollResponse>> EnrollAsync(
        InstallEnrollRequest request,
        CancellationToken cancellationToken);

    Task<InstallOperationResult<InstallCreateSeatResponse>> CreateSeatAsync(
        InstallCreateSeatRequest request,
        CancellationToken cancellationToken);
```

- [ ] **Step 3: Strip owner-code from `EfInstallService.cs`**

- Remove `using AFK4.Platform.Api.Identity.OwnerCodes;` (line 5) and the `IOwnerCodeService ownerCodeService,` primary-ctor param (line 17), and `private const int MaxResolvedOwnerCodeFailures = 5;` (line 23).
- Delete the public unauth methods `DiscoverAsync` (30–86), `EnrollAsync` (88–120), `CreateSeatAsync` (348–379), and the private `RecordResolvedOwnerCodeFailureAsync` (587–604).
- `EnrollResolvedAsync`: remove the `Guid? enrolledViaOwnerCodeId,` parameter (line 141); remove the entity assignment `EnrolledViaOwnerCodeId = enrolledViaOwnerCodeId,` (line 295); remove `enrolledViaOwnerCodeId` from all `BadRequest`/`Conflict`/`Success` returns inside this method (it was threaded into the result's `OwnerCodeId` — see Step 5).
- `CreateSeatResolvedAsync`: remove the `Guid? ownerCodeId,` parameter (line ~401) and stop passing it into result factory calls.
- Update the two authed callers: `EnrollForStaffAsync` (drop `enrolledViaOwnerCodeId: null` arg) and `CreateSeatForStaffAsync` (drop `ownerCodeId: null` arg).

- [ ] **Step 4: Delete the unauth install test file**

```bash
cd /d/afk4.net && git rm tests/AFK4.Platform.Api.Tests/InstallEndpointTests.cs
```
(The authed path is covered by `AuthenticatedInstallEndpointTests.cs`, which stays.)

- [ ] **Step 5: Remove `OwnerCodeId` from `InstallOperationResult<T>`**

In `Install/InstallOperationResult.cs`, remove the `Guid? OwnerCodeId` member and the `ownerCodeId` parameter from every factory method (`Success`, `BadRequest`, `NotFound`, `Conflict`). Then fix every call site the compiler flags — these are now only in the authed endpoints' audit calls and `EfInstallService` authed methods. The audit writes in the authed `/api/install/auth/*` endpoints that referenced `result.OwnerCodeId` should drop that field (owner-code is gone).

- [ ] **Step 6: Build + test backend**

```bash
cd /d/afk4.net
dotnet build src/AFK4.Platform.Api/AFK4.Platform.Api.csproj -c Debug --nologo -v q
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj -c Debug --nologo
```
Expected: build 0 errors; tests pass. At this point owner-code core (service/entity/endpoints) still exists but is unused by the install path — that's fine, it's deleted in Task 4.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "chore(platform-api): remove unauth owner-code install endpoints and service path"
```

---

## Task 4: Backend — delete owner-code core, contracts, permission, audit

**Files:** delete `Identity/OwnerCodes/*`, `Data/OwnerCodeEntity.cs`, `Endpoints/OwnerCodeEndpoints.cs`, Shared.Contracts owner-code DTOs + unauth install contracts; modify `Program.cs`, `Data/PlatformDbContext.cs`, `Data/DeviceEntity.cs`, `StaffPermissionNames.cs`, `PermissionCatalog.cs`, `Audit/AuditActionNames.cs`; delete `OwnerCodeEndpointTests.cs`, `InstallContractSerializationTests.cs`.

- [ ] **Step 1: Delete owner-code source files**

```bash
cd /d/afk4.net
git rm -r src/AFK4.Platform.Api/Identity/OwnerCodes
git rm src/AFK4.Platform.Api/Data/OwnerCodeEntity.cs \
       src/AFK4.Platform.Api/Endpoints/OwnerCodeEndpoints.cs \
       src/AFK4.Shared.Contracts/Identity/OwnerCodeIssuedResponse.cs \
       src/AFK4.Shared.Contracts/Identity/OwnerCodeSummaryResponse.cs
```
Also delete the rotate-request contract if present:
```bash
git rm src/AFK4.Shared.Contracts/Identity/RotateOwnerCodeRequest.cs 2>/dev/null || true
```

- [ ] **Step 2: Delete the unauth install contracts**

```bash
cd /d/afk4.net
git rm src/AFK4.Shared.Contracts/Install/InstallDiscoverRequest.cs \
       src/AFK4.Shared.Contracts/Install/InstallEnrollRequest.cs \
       src/AFK4.Shared.Contracts/Install/InstallCreateSeatRequest.cs
```
(KEEP `AuthenticatedInstallEnrollRequest`/`AuthenticatedInstallCreateSeatRequest` and the `Install*Response` types.) If any of the three live in a shared file rather than separate files, remove just the three record declarations instead.

- [ ] **Step 3: Clean `Program.cs`**

Remove the using (line 18) `using AFK4.Platform.Api.Identity.OwnerCodes;`, the DI block (lines ~190–194):
```csharp
builder.Services.Configure<OwnerCodeOptions>(
    builder.Configuration.GetSection(OwnerCodeOptions.SectionName));
builder.Services.AddSingleton<IOwnerCodeGenerator, RandomOwnerCodeGenerator>();
builder.Services.AddSingleton<IOwnerCodeHasher, Sha256OwnerCodeHasher>();
builder.Services.AddScoped<IOwnerCodeService, OwnerCodeService>();
```
and the mapping line (line ~432) `app.MapOwnerCodeEndpoints();`.

- [ ] **Step 4: Clean `PlatformDbContext.cs` + `DeviceEntity.cs`**

- `DeviceEntity.cs`: delete line 23 `public Guid? EnrolledViaOwnerCodeId { get; set; }`.
- `PlatformDbContext.cs`: delete the `DbSet<OwnerCodeEntity> OwnerCodes` property, the entire `OwnerCodeEntity` `modelBuilder.Entity<OwnerCodeEntity>(...)` config block, and the device index line (line ~334) `entity.HasIndex(device => device.EnrolledViaOwnerCodeId);`.

- [ ] **Step 5: Remove permission + audit constants**

- `StaffPermissionNames.cs`: delete line 108 `public const string ManageOwnerCode = "identity.owner_code.manage";`.
- `PermissionCatalog.cs`: delete line 64 `StaffPermissionNames.ManageOwnerCode,` (inside the Owner role seed set).
- `Audit/AuditActionNames.cs`: delete lines 212 & 214 (`GenerateOwnerCode`, `RotateOwnerCode`).

- [ ] **Step 6: Delete owner-code + owner-code-contract tests**

```bash
cd /d/afk4.net
git rm tests/AFK4.Platform.Api.Tests/OwnerCodeEndpointTests.cs \
       tests/AFK4.Shared.Contracts.Tests/InstallContractSerializationTests.cs
```
(All 4 cases in `InstallContractSerializationTests` exercise the deleted unauth request types; the file goes wholesale. Authed-contract round-trips live elsewhere.)

- [ ] **Step 7: Build + full backend tests**

```bash
cd /d/afk4.net
dotnet build src/AFK4.Platform.Api/AFK4.Platform.Api.csproj -c Debug --nologo -v q
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj -c Debug --nologo
```
Expected: 0 build errors, all tests pass. If a permission-catalog parity test fails, it derives from the constants — confirm the only change is the removed `ManageOwnerCode` and update the expected set if the test hard-codes a list. Sanity grep (should be empty except migrations + this plan/spec):
```bash
grep -rniE "ownerCode|owner_code|OwnerCode" src --include=*.cs | grep -viE "Migrations"
```

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "chore(platform-api): delete owner-code core, contracts, permission and audit actions"
```

---

## Task 5: DB migration — drop owner_codes table + device column

**Files:** new migration under `src/AFK4.Platform.Api/Data/Migrations/` + updated `PlatformDbContextModelSnapshot.cs` (auto).

- [ ] **Step 1: Generate the drop migration**

The model no longer has `OwnerCodeEntity` or `DeviceEntity.EnrolledViaOwnerCodeId` (Task 4), so EF will scaffold the drops automatically.
```bash
cd /d/afk4.net
dotnet ef migrations add DropOwnerCodes --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --startup-project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj
```
(If `dotnet ef` is missing: `dotnet tool restore` or `dotnet tool install --global dotnet-ef`.)

- [ ] **Step 2: Verify the generated migration**

Open the new `*_DropOwnerCodes.cs`. Confirm `Up()` performs: `migrationBuilder.DropTable(name: "owner_codes")`, `migrationBuilder.DropIndex(name: "IX_devices_EnrolledViaOwnerCodeId", table: "devices")`, and `migrationBuilder.DropColumn(name: "EnrolledViaOwnerCodeId", table: "devices")`. Confirm `Down()` recreates them (EF auto-generates). If EF scaffolded anything unrelated, delete those statements (the migration must be owner-code-only).

- [ ] **Step 3: Build to confirm the snapshot compiles**

```bash
cd /d/afk4.net && dotnet build src/AFK4.Platform.Api/AFK4.Platform.Api.csproj -c Debug --nologo -v q
```
Expected: 0 errors. (The model snapshot is updated by the `ef migrations add` command.)

- [ ] **Step 4: Run backend tests (migrations apply against the test DB)**

```bash
cd /d/afk4.net && dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj -c Debug --nologo
```
Expected: all pass (the test harness applies migrations including the new drop).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "chore(platform-api): migration dropping owner_codes table and device enrollment column"
```

---

## Task 6: Final cross-suite verification

- [ ] **Step 1: Backend solution build + Platform.Api + SetupWizard tests**

```bash
cd /d/afk4.net
taskkill //IM AFK4.SetupWizard.exe //F 2>/dev/null || true
dotnet build src/AFK4.SetupWizard/AFK4.SetupWizard.csproj -c Debug --nologo -v q
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj -c Debug --nologo
dotnet test tests/AFK4.SetupWizard.Tests/AFK4.SetupWizard.Tests.csproj -c Debug --nologo
dotnet test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj -c Debug --nologo
```
Expected: all green.

- [ ] **Step 2: Web + i18n**

```bash
cd /d/afk4.net/packages/i18n && ~/.bun/bin/bun test
cd /d/afk4.net/src/AFK4.Platform.Web && ~/.bun/bin/bun test && ~/.bun/bin/bun run build
```
Expected: all green.

- [ ] **Step 3: Final owner-code sweep**

```bash
cd /d/afk4.net
grep -rniE "ownerCode|owner_code|OwnerCode|owner-code" src packages locales tests --include=*.cs --include=*.ts --include=*.tsx --include=*.json | grep -viE "Migrations|docs/"
```
Expected: no matches (migrations keep historical references — that's correct). If anything remains, remove it and re-verify.

---

## Self-Review

- **Spec coverage:** backend core (T4) ✓; unauth endpoints + service (T3) ✓; DB drop (T5) ✓; permissions/audit (T4) ✓; wizard host (T2) ✓; platform-web + InstallScreen rewrite (T1) ✓; i18n keys + subtitle (T1) ✓; tests deleted alongside code (T1/T2/T3/T4) ✓; final verification (T6) ✓.
- **Placeholder scan:** signature edits and rewritten files quoted verbatim; whole-file deletions use `git rm`; copy strings concrete (tg flagged for native review). No TBDs.
- **Type consistency:** `ISetupWizardApiClient` trio removed matches impl + preview + bridge (T2); `IInstallService` unauth trio removed matches `EfInstallService` deletions (T3); `InstallOperationResult.OwnerCodeId` removal (T3) is the one cross-cutting edit flagged with "fix all compiler-flagged call sites"; `EnrolledViaOwnerCodeId` removed from entity (T4) before migration scaffolds the drop (T5) — correct order.
- **Note:** `InstallOperationResult.OwnerCodeId` removal (T3 Step 5) has the widest blast radius; the implementer leans on the compiler to find call sites and verifies via build. tg copy strings are drafts for native review (engineering-honesty: not ru-copies, flagged).
