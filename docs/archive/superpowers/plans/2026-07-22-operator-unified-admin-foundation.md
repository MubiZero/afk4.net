# Оператор = единая админка — Фундамент — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Оператор-веб работает идентично в WebView2 и обычном браузере; owner логинится с любого устройства и переключает свои филиалы, с починенной per-branch авторизацией.

**Architecture:** Три пласта. (1) Бэкенд: убрать cross-branch эскалацию — права резолвятся per-branch в `StaffContext`, `RequireBranchPermissionAsync` проверяет право для конкретного branchId (контракт sign-in не меняется). (2) Транспорт: auth уходит из WPF-моста — оба хоста логинятся прямым HTTP в `/api/auth/staff/*` и хранят сессию в `sessionStorage` (зеркало Platform.Web); WPF-хост худеет, `connection:*` (пиннинг машины) остаётся. (3) Мульти-филиальность: реактивный `activeBranchId` + `BranchSwitcher` (порт из Platform.Web), при смене — пересборка `backendContext`/realtime/гейта.

**Tech Stack:** .NET 10 minimal-API + EF Core + xUnit (`PlatformApiFactory`); React/TS на `bun test` (happy-dom+jest-dom) + `bun run build` (`tsc -b && vite`); C# WPF-хост (WebView2).

## Global Constraints

- Ветка: `feat/operator-unified-admin-foundation` (уже создана, спека закоммичена).
- **Контракт `StaffSignInResponse` НЕ меняется** — плоский `Permissions[]` остаётся UI-подсказкой; Platform.Web не должен сломаться.
- **Money-path не трогаем.** Никаких секретов в коде/логах. Никаких AI-подписей в коммитах.
- i18n: строки только через `/locales/{ru,en,tg}.json` + `cd packages/i18n && bun run gen` (генерит `messages.ts`), НЕ хардкод. Таджикский — реально таджикский (guard-тест `tg===ru`).
- Frontend: зелёный `bun test` ≠ зелёная сборка — `tsc -b` тайпчекает и тест-файлы; финал обязан включать `bun run build`. Bun-моки типизировать.
- Оператор: тема dark по умолчанию, акцент emerald `#2cc592`; тень = подъём (светлая тема); фидбэк = тост (`useFeedbackToasts`).
- Точки входа bun: полный путь к `bun` из [[afk4-env-quirks]] (см. окружение проекта).
- Sign-in двухступенчатый: `POST /api/auth/staff/sign-in-by-login {login,password}` → 200 `StaffSignInResponse` | 401 | **409** `{clubs:[{organizationId,name}]}`; при 409 → `POST /api/auth/staff/sign-in {organizationId,userName,password}`. Refresh: `POST /api/auth/staff/refresh {organizationId,refreshToken}`. Серверного sign-out нет (локальный).
- Сессия: `sessionStorage['afk4.staff.session']`; активный филиал: `localStorage['afk4.operator.activeBranchId']`.

---

## File Structure

**Backend (`src/AFK4.Platform.Api`):**
- Modify `Identity/StaffContext.cs` — добавить per-branch карту прав + `HasBranchPermission`.
- Modify `Identity/OpaqueStaffTokenService.cs` — `CreateContextAsync` собирает `PermissionsByBranch`.
- Modify `Identity/StaffAuthorizationService.cs` — `RequireBranchPermissionAsync` через `HasBranchPermission`.
- Test `tests/AFK4.Platform.Api.Tests/StaffAuthorizationServiceTests.cs` (new) — регресс cross-branch.

**Operator web (`src/AFK4.Operator.App.Web/src`):**
- Create `auth/staffSessionStore.ts` — sessionStorage-стор сессии (зеркало Platform.Web).
- Create `auth/staffAuthApi.ts` — HTTP staff-auth клиент.
- Modify `authClient.ts` — фасад поверх store+api, форма `OperatorAuthSession` сохранена.
- Modify `useOperatorAuth.ts` — restore/refresh/signIn/signOut на HTTP; two-step choose-club.
- Modify `operatorHelpers.ts` — `resolveActiveBranchId` + фабрика клиентов с авто-refresh токена.
- Create `branches/useActiveBranch.ts`, `branches/useBranchDirectory.ts`, `branches/BranchSwitcher.tsx` (порт).
- Modify `App.tsx` — реактивный `activeBranchId`, мемо `backendContext`, свитчер, пересчёт гейта.
- Modify `useOperatorRealtime.ts` — deps по активному филиалу.
- Modify `devHostBridge.ts` — убрать `auth:*` ветки (переезд в HTTP-мок).
- Modify Vite env/config — браузерный `platformBaseUrl` из env.

**WPF host (`src/AFK4.Operator.App`):**
- Modify `Web/OperatorWebHostBridge.cs` — удалить `auth:*` хендлеры, оставить `connection:*`.
- Delete `Auth/HttpOperatorAuthApiClient.cs`, `Auth/ProtectedDataOperatorTokenStore.cs` + их интерфейсы/records/DI.

---

## Task 1: Бэкенд — per-branch права в StaffContext

**Files:**
- Modify: `src/AFK4.Platform.Api/Identity/StaffContext.cs`
- Modify: `src/AFK4.Platform.Api/Identity/OpaqueStaffTokenService.cs` (метод `CreateContextAsync`)
- Test: `tests/AFK4.Platform.Api.Tests/StaffContextTests.cs` (new)

**Interfaces:**
- Produces: `StaffContext.PermissionsByBranch: IReadOnlyDictionary<Guid, IReadOnlySet<string>>` и метод `bool HasBranchPermission(Guid branchId, string permission)` (case-insensitive). `Permissions` (union) остаётся.

- [ ] **Step 1: Failing test** — `tests/AFK4.Platform.Api.Tests/StaffContextTests.cs`

```csharp
using AFK4.Platform.Api.Identity;

public class StaffContextTests
{
    private static StaffContext Ctx(Dictionary<Guid, IReadOnlySet<string>> byBranch)
    {
        var branchIds = byBranch.Keys.ToHashSet();
        var union = byBranch.Values.SelectMany(p => p).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new StaffContext(Guid.NewGuid(), Guid.NewGuid(), "Test",
            branchIds, union) { PermissionsByBranch = byBranch };
    }

    [Fact]
    public void HasBranchPermission_true_only_for_branch_that_grants_it()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var ctx = Ctx(new()
        {
            [a] = new HashSet<string> { "branches.settings.manage" },
            [b] = new HashSet<string> { "pos.sell" }
        });

        Assert.True(ctx.HasBranchPermission(a, "branches.settings.manage"));
        Assert.False(ctx.HasBranchPermission(b, "branches.settings.manage")); // не протекает на B
        Assert.True(ctx.HasBranchPermission(b, "POS.SELL"));                    // case-insensitive
        Assert.False(ctx.HasBranchPermission(Guid.NewGuid(), "pos.sell"));      // неизвестный branch
    }
}
```

- [ ] **Step 2: Run — FAIL** (нет `PermissionsByBranch`/`HasBranchPermission`)

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~StaffContextTests`
Expected: не компилируется / FAIL.

- [ ] **Step 3: Implement** — `StaffContext.cs`

```csharp
namespace AFK4.Platform.Api.Identity;

public sealed record StaffContext(
    Guid StaffUserId,
    Guid OrganizationId,
    string DisplayName,
    IReadOnlySet<Guid> BranchIds,
    IReadOnlySet<string> Permissions)
{
    public IReadOnlyList<string> RoleNames { get; init; } = [];

    // Права, сгруппированные по филиалу. Пустой словарь = деградация к union (обратная совместимость
    // для контекстов, собранных не через CreateContextAsync).
    public IReadOnlyDictionary<Guid, IReadOnlySet<string>> PermissionsByBranch { get; init; }
        = new Dictionary<Guid, IReadOnlySet<string>>();

    public bool HasBranchPermission(Guid branchId, string permission)
    {
        if (PermissionsByBranch.TryGetValue(branchId, out var perms))
        {
            return perms.Contains(permission);
        }
        // Фолбэк: если карта не заполнена (старый путь), — прежнее поведение union.
        return BranchIds.Contains(branchId) && Permissions.Contains(permission);
    }
}
```

> Примечание: `IReadOnlySet<string>` внутри должен быть case-insensitive — в `CreateContextAsync` создавать `HashSet<string>(StringComparer.OrdinalIgnoreCase)`. Тестовый фолбэк использует такой же union.

- [ ] **Step 4: Populate `PermissionsByBranch` в `OpaqueStaffTokenService.CreateContextAsync`**

Найти в `Identity/OpaqueStaffTokenService.cs` метод `CreateContextAsync` (около строк 155-162), где сейчас:
```csharp
BranchIds: roles.Select(role => role.BranchId).ToHashSet(),
Permissions: PermissionCatalog.GetPermissions(roleNames)
```
Собрать карту по филиалам ДО создания record и передать через `with`/init:
```csharp
var byBranch = roles
    .GroupBy(role => role.BranchId)
    .ToDictionary(
        g => g.Key,
        g => (IReadOnlySet<string>)PermissionCatalog
            .GetPermissions(g.Select(r => r.RoleName).Distinct().ToArray())
            .ToHashSet(StringComparer.OrdinalIgnoreCase));

var context = new StaffContext(
    /* существующие позиционные аргументы: staffUserId, organizationId, displayName */
    BranchIds: byBranch.Keys.ToHashSet(),
    Permissions: PermissionCatalog.GetPermissions(roleNames)
        .ToHashSet(StringComparer.OrdinalIgnoreCase))
{
    RoleNames = roleNames,          // сохранить как было
    PermissionsByBranch = byBranch
};
```
Точные имена локальных (`roles`, `roleNames`, аргументы record) взять из фактического кода — сохранить существующую сигнатуру record, добавить только `PermissionsByBranch`/`RoleNames` через init.

- [ ] **Step 5: Run — PASS**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~StaffContextTests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Platform.Api/Identity/StaffContext.cs src/AFK4.Platform.Api/Identity/OpaqueStaffTokenService.cs tests/AFK4.Platform.Api.Tests/StaffContextTests.cs
git commit -m "feat(auth): per-branch права в StaffContext (HasBranchPermission)"
```

---

## Task 2: Бэкенд — RequireBranchPermissionAsync per-branch + регресс эскалации

**Files:**
- Modify: `src/AFK4.Platform.Api/Identity/StaffAuthorizationService.cs` (метод `RequireBranchPermissionAsync`)
- Test: `tests/AFK4.Platform.Api.Tests/StaffAuthorizationServiceTests.cs` (new)

**Interfaces:**
- Consumes: `StaffContext.HasBranchPermission` (Task 1).

- [ ] **Step 1: Failing regression test** — `tests/AFK4.Platform.Api.Tests/StaffAuthorizationServiceTests.cs`

Тест воспроизводит эскалацию через реальный staff-стек `PlatformApiFactory`: сотрудник — менеджер филиала A (право `branches.settings.manage`) и кассир филиала B (без этого права). Действие, требующее `branches.settings.manage` на филиале B, должно быть отклонено (403/`RequireBranchPermissionAsync` бросает Forbidden), а на A — разрешено.

```csharp
using System.Net;
using AFK4.Platform.Api.Tests.Infrastructure; // фактический namespace фабрики/хелперов

public class StaffAuthorizationServiceTests
{
    [Fact]
    public async Task Manager_permission_does_not_leak_across_branches()
    {
        await using var factory = new PlatformApiFactory();
        // Хелпер сидинга: организация с двумя филиалами A,B; staff = manager@A + cashier@B.
        // Использовать существующие сид-хелперы тестов (см. другие *EndpointsTests как назначаются
        // per-branch роли). Точные имена ролей взять из PermissionCatalog / staffRoleOptions.
        var seed = await factory.SeedTwoBranchStaffAsync(
            managerBranch: "A", cashierBranch: "B");
        var client = factory.CreateClient();
        client.AuthorizeAs(seed.StaffToken); // как в других тестах ставится Bearer

        // Эндпоинт, гейтованный branches.settings.manage — напр. PUT профиля филиала.
        var onA = await client.PutAsJsonAsync(
            $"/api/branches/{seed.BranchA}/settings/profile", seed.ValidProfilePayload);
        var onB = await client.PutAsJsonAsync(
            $"/api/branches/{seed.BranchB}/settings/profile", seed.ValidProfilePayload);

        Assert.NotEqual(HttpStatusCode.Forbidden, onA.StatusCode); // на A разрешено
        Assert.Equal(HttpStatusCode.Forbidden, onB.StatusCode);    // на B — НЕ протекает
    }
}
```

> Реальные детали (namespace фабрики, метод авторизации клиента, наличие сид-хелпера для двух филиалов) сверить с существующими тестами — если сид-хелпера `SeedTwoBranchStaffAsync` нет, реализовать локально по образцу существующих per-branch сидов (искать в тестах `StaffRoleAssignment`/`branch`-назначения). Выбрать реальный branch-гейтованный эндпоинт с правом `branches.settings.manage`.

- [ ] **Step 2: Run — FAIL** (сейчас эскалация проходит → `onB` НЕ 403)

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~StaffAuthorizationServiceTests`
Expected: FAIL — `onB` вернёт не-403 (право протекает).

- [ ] **Step 3: Fix `RequireBranchPermissionAsync`**

В `Identity/StaffAuthorizationService.cs` заменить проверку права. Было (по сути):
```csharp
if (!staffContext.BranchIds.Contains(branchId)) return Forbidden(...);
if (!staffContext.Permissions.Contains(permission)) return Forbidden(...);
```
Стало — единая per-branch проверка:
```csharp
if (!staffContext.HasBranchPermission(branchId, permission))
{
    return /* существующий способ вернуть Forbidden/бросить */;
}
```
Проверку `branch.OrganizationId == staffContext.OrganizationId` и существование филиала — оставить как есть (она до проверки права). `HasBranchPermission` уже включает членство в филиале (через карту / фолбэк).

- [ ] **Step 4: Run — PASS** + прогнать смежные auth/endpoint тесты, чтобы убедиться, что легитимный доступ не сломан.

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~StaffAuthorizationServiceTests`
Then: `dotnet test tests/AFK4.Platform.Api.Tests` (полный прогон — убедиться, что per-branch ужесточение не уронило легитимные сценарии; если что-то упало из-за некорректного сида ролей в старых тестах — чинить в корне, см. WORKING-STYLE #39).
Expected: целевой PASS; полный сьют зелёный.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Api/Identity/StaffAuthorizationService.cs tests/AFK4.Platform.Api.Tests/StaffAuthorizationServiceTests.cs
git commit -m "fix(auth): закрыть cross-branch эскалацию прав (per-branch проверка)"
```

---

## Task 3: Оператор — staff session store (sessionStorage)

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/auth/staffSessionStore.ts`
- Test: `src/AFK4.Operator.App.Web/src/auth/staffSessionStore.test.ts`

**Interfaces:**
- Produces: `OperatorAuthSession` (та же форма, что в `authClient.ts` — реэкспортировать оттуда, не дублировать тип); `readStoredSession(): OperatorAuthSession | null`; `writeStoredSession(s: OperatorAuthSession): void`; `clearStoredSession(): void`; `sessionFromSignInResponse(r: StaffSignInResponse): OperatorAuthSession`; `isAccessTokenExpired(s: OperatorAuthSession, nowMs: number): boolean`.

- [ ] **Step 1: Failing test** — `staffSessionStore.test.ts`

```ts
import { test, expect, beforeEach } from 'bun:test';
import { readStoredSession, writeStoredSession, clearStoredSession, isAccessTokenExpired } from './staffSessionStore';
import type { OperatorAuthSession } from '../authClient';

const sample: OperatorAuthSession = {
  staffUserId: 's1', organizationId: 'o1', displayName: 'Owner',
  accessToken: 'a.b', accessTokenExpiresAtUtc: '2999-01-01T00:00:00Z',
  refreshToken: 'r.b', refreshTokenExpiresAtUtc: '2999-01-01T00:00:00Z',
  branchIds: ['b1', 'b2'], permissions: ['pos.sell'], roleNames: ['branch_manager']
};

beforeEach(() => clearStoredSession());

test('write → read roundtrip', () => {
  writeStoredSession(sample);
  expect(readStoredSession()).toEqual(sample);
});

test('read returns null on empty / invalid', () => {
  expect(readStoredSession()).toBeNull();
  sessionStorage.setItem('afk4.staff.session', '{ broken');
  expect(readStoredSession()).toBeNull();
});

test('read rejects session without accessToken/organizationId', () => {
  sessionStorage.setItem('afk4.staff.session', JSON.stringify({ ...sample, accessToken: '' }));
  expect(readStoredSession()).toBeNull();
});

test('isAccessTokenExpired compares against expiry', () => {
  const soon = { ...sample, accessTokenExpiresAtUtc: '2000-01-01T00:00:00Z' };
  expect(isAccessTokenExpired(soon, Date.parse('2001-01-01T00:00:00Z'))).toBe(true);
  expect(isAccessTokenExpired(sample, Date.parse('2001-01-01T00:00:00Z'))).toBe(false);
});
```

> Тип `OperatorAuthSession` расширяем: добавить `refreshToken: string` (в текущем `authClient.ts` его нет — он жил в нативном сторе; теперь нужен в вебе). Обновить интерфейс в `authClient.ts` в Task 5; здесь импорт типа уже подразумевает поле.

- [ ] **Step 2: Run — FAIL**

Run: `<bun> test src/auth/staffSessionStore.test.ts` (из `src/AFK4.Operator.App.Web`)
Expected: FAIL (модуль не существует).

- [ ] **Step 3: Implement** — `staffSessionStore.ts` (зеркало `Platform.Web/src/auth/staffTokenStore.ts`)

```ts
import type { OperatorAuthSession } from '../authClient';
import type { StaffSignInResponse } from './staffAuthApi';

const KEY = 'afk4.staff.session';

export function readStoredSession(): OperatorAuthSession | null {
  const raw = sessionStorage.getItem(KEY);
  if (!raw) return null;
  try {
    const s = JSON.parse(raw) as OperatorAuthSession;
    if (!s.accessToken || !s.organizationId) return null;
    return s;
  } catch {
    return null;
  }
}

export function writeStoredSession(session: OperatorAuthSession): void {
  sessionStorage.setItem(KEY, JSON.stringify(session));
}

export function clearStoredSession(): void {
  sessionStorage.removeItem(KEY);
}

export function sessionFromSignInResponse(r: StaffSignInResponse): OperatorAuthSession {
  return {
    staffUserId: r.staffUserId,
    organizationId: r.organizationId,
    displayName: r.displayName,
    accessToken: r.accessToken,
    accessTokenExpiresAtUtc: r.accessTokenExpiresAtUtc,
    refreshToken: r.refreshToken,
    refreshTokenExpiresAtUtc: r.refreshTokenExpiresAtUtc,
    branchIds: r.branchIds,
    permissions: r.permissions,
    roleNames: r.roleNames ?? []
  };
}

export function isAccessTokenExpired(session: OperatorAuthSession, nowMs: number): boolean {
  return Date.parse(session.accessTokenExpiresAtUtc) <= nowMs;
}
```

- [ ] **Step 4: Run — PASS**

Run: `<bun> test src/auth/staffSessionStore.test.ts`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/auth/staffSessionStore.ts src/AFK4.Operator.App.Web/src/auth/staffSessionStore.test.ts
git commit -m "feat(operator): staff session store в sessionStorage"
```

---

## Task 4: Оператор — HTTP staff-auth клиент

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/auth/staffAuthApi.ts`
- Test: `src/AFK4.Operator.App.Web/src/auth/staffAuthApi.test.ts`

**Interfaces:**
- Produces: тип `StaffSignInResponse` (поля: `staffUserId, organizationId, displayName, accessToken, accessTokenExpiresAtUtc, refreshToken, refreshTokenExpiresAtUtc, branchIds: string[], permissions: string[], roleNames?: string[]`); класс `StaffAuthApi(baseUrl: string, fetchImpl?)` с методами: `signInByLogin(login, password): Promise<StaffSignInResponse>` (бросает `ChooseClubError{clubs}` при 409, `Error` при 401), `signInToClub(organizationId, login, password)`, `refresh(organizationId, refreshToken)`, `forgotByEmail/resetByEmail/forgotByPhone/resetByPhone`.

- [ ] **Step 1: Failing test** — `staffAuthApi.test.ts`

```ts
import { test, expect } from 'bun:test';
import { StaffAuthApi, ChooseClubError } from './staffAuthApi';

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
}

test('signInByLogin returns session on 200', async () => {
  const api = new StaffAuthApi('http://x/', async (url, init) => {
    expect(String(url)).toBe('http://x/api/auth/staff/sign-in-by-login');
    expect(JSON.parse(String(init?.body))).toEqual({ login: 'u', password: 'p' });
    return jsonResponse(200, { staffUserId: 's', organizationId: 'o', displayName: 'D',
      accessToken: 'a', accessTokenExpiresAtUtc: 'x', refreshToken: 'r', refreshTokenExpiresAtUtc: 'y',
      branchIds: ['b1'], permissions: [], roleNames: [] });
  });
  const s = await api.signInByLogin('u', 'p');
  expect(s.branchIds).toEqual(['b1']);
});

test('signInByLogin throws ChooseClubError on 409', async () => {
  const api = new StaffAuthApi('http://x/', async () =>
    jsonResponse(409, { clubs: [{ organizationId: 'o1', name: 'Club 1' }, { organizationId: 'o2', name: 'Club 2' }] }));
  const err = await api.signInByLogin('u', 'p').catch((e) => e);
  expect(err).toBeInstanceOf(ChooseClubError);
  expect((err as ChooseClubError).clubs).toHaveLength(2);
});

test('signInByLogin throws on 401', async () => {
  const api = new StaffAuthApi('http://x/', async () => jsonResponse(401, {}));
  await expect(api.signInByLogin('u', 'p')).rejects.toThrow();
});

test('refresh posts organizationId + refreshToken', async () => {
  const api = new StaffAuthApi('http://x/', async (url, init) => {
    expect(String(url)).toBe('http://x/api/auth/staff/refresh');
    expect(JSON.parse(String(init?.body))).toEqual({ organizationId: 'o', refreshToken: 'r' });
    return jsonResponse(200, { staffUserId: 's', organizationId: 'o', displayName: 'D',
      accessToken: 'a2', accessTokenExpiresAtUtc: 'x', refreshToken: 'r2', refreshTokenExpiresAtUtc: 'y',
      branchIds: ['b1'], permissions: [], roleNames: [] });
  });
  const s = await api.refresh('o', 'r');
  expect(s.accessToken).toBe('a2');
});
```

- [ ] **Step 2: Run — FAIL**

Run: `<bun> test src/auth/staffAuthApi.test.ts`
Expected: FAIL.

- [ ] **Step 3: Implement** — `staffAuthApi.ts`

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

export interface ClubChoice { organizationId: string; name: string; }

export class ChooseClubError extends Error {
  constructor(public readonly clubs: ClubChoice[]) {
    super('Multiple clubs matched; choose one.');
    this.name = 'ChooseClubError';
  }
}

type FetchLike = (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>;

export class StaffAuthApi {
  private readonly base: URL;
  private readonly fetchImpl: FetchLike;
  constructor(baseUrl: string, fetchImpl?: FetchLike) {
    this.base = new URL(baseUrl);
    this.fetchImpl = fetchImpl ?? ((i, init) => globalThis.fetch(i, init));
  }

  private async post<T>(path: string, body: unknown, on409?: (r: Response) => Promise<never>): Promise<T> {
    const res = await this.fetchImpl(new URL(path, this.base).toString(), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body)
    });
    if (res.status === 409 && on409) return on409(res);
    if (!res.ok) throw new Error(`Auth request failed: ${res.status}`);
    return res.status === 204 ? (null as T) : (await res.json() as T);
  }

  signInByLogin(login: string, password: string): Promise<StaffSignInResponse> {
    return this.post<StaffSignInResponse>('api/auth/staff/sign-in-by-login', { login, password },
      async (res) => {
        const body = await res.json() as { clubs: ClubChoice[] };
        throw new ChooseClubError(body.clubs);
      });
  }

  signInToClub(organizationId: string, login: string, password: string): Promise<StaffSignInResponse> {
    return this.post<StaffSignInResponse>('api/auth/staff/sign-in', { organizationId, userName: login, password });
  }

  refresh(organizationId: string, refreshToken: string): Promise<StaffSignInResponse> {
    return this.post<StaffSignInResponse>('api/auth/staff/refresh', { organizationId, refreshToken });
  }

  forgotByEmail(userNameOrEmail: string) { return this.post<void>('api/auth/staff/forgot-password', { userNameOrEmail }); }
  resetByEmail(userNameOrEmail: string, code: string, newPassword: string) { return this.post<void>('api/auth/staff/reset-password', { userNameOrEmail, code, newPassword }); }
  forgotByPhone(phoneNumber: string) { return this.post<void>('api/auth/staff/forgot-password-by-phone', { phoneNumber }); }
  resetByPhone(phoneNumber: string, code: string, newPassword: string) { return this.post<void>('api/auth/staff/reset-password-by-phone', { phoneNumber, code, newPassword }); }
}
```

> Точные пути forgot/reset сверить с `Platform.Web/src/api/staffAuthApi.ts` (в отчёте перечислены `/forgot-password`, `/reset-password`, `/forgot-password-by-phone`, `/reset-password-by-phone`).

- [ ] **Step 4: Run — PASS**

Run: `<bun> test src/auth/staffAuthApi.test.ts`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/auth/staffAuthApi.ts src/AFK4.Operator.App.Web/src/auth/staffAuthApi.test.ts
git commit -m "feat(operator): HTTP staff-auth клиент (двухступенчатый sign-in)"
```

---

## Task 5: Оператор — authClient.ts на HTTP + useOperatorAuth + choose-club UI

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/authClient.ts`
- Modify: `src/AFK4.Operator.App.Web/src/useOperatorAuth.ts`
- Modify: sign-in view (найти по использованию `signInOperator`/`OperatorSignInRequest`, вероятно `SignInScreen.tsx`/`authView`)
- Test: `src/AFK4.Operator.App.Web/src/useOperatorAuth.test.ts` (или дополнение существующего)

**Interfaces:**
- Consumes: `staffSessionStore` (Task 3), `StaffAuthApi`/`ChooseClubError` (Task 4).
- Produces: `authClient.ts` сохраняет имена экспортов (`loadOperatorSession`, `signInOperator`, `refreshOperatorSession`, `signOutOperator`, forgot/reset) — но реализованы через HTTP; `OperatorAuthSession` расширен полем `refreshToken: string`. Новый экспорт `signInByLoginOperator(login, password): Promise<OperatorAuthSession>` (может бросить `ChooseClubError`).

- [ ] **Step 1: Failing test** — sign-in по HTTP пишет сессию в стор, sign-out чистит.

```ts
import { test, expect, beforeEach, mock } from 'bun:test';

beforeEach(() => sessionStorage.clear());

test('signInByLoginOperator stores session', async () => {
  mock.module('./staffAuthApi', () => ({
    ChooseClubError: class extends Error {},
    StaffAuthApi: class {
      signInByLogin() { return Promise.resolve({
        staffUserId: 's', organizationId: 'o', displayName: 'D',
        accessToken: 'a', accessTokenExpiresAtUtc: '2999-01-01T00:00:00Z',
        refreshToken: 'r', refreshTokenExpiresAtUtc: '2999-01-01T00:00:00Z',
        branchIds: ['b1'], permissions: [], roleNames: [] }); }
    }
  }));
  const { signInByLoginOperator } = await import('./authClient');
  const s = await signInByLoginOperator('u', 'p');
  expect(s.organizationId).toBe('o');
  expect(sessionStorage.getItem('afk4.staff.session')).toContain('"accessToken":"a"');
});
```

> `mock.module` течёт process-wide (см. [[frontends-on-bun-test]]) — держать этот тест в отдельном файле или аккуратно с порядком.

- [ ] **Step 2: Run — FAIL**

- [ ] **Step 3: Implement `authClient.ts`**

Заменить импорт моста на store+api. `baseUrl` берём из `getOperatorConfig().platformBaseUrl`. Один общий инстанс `StaffAuthApi`.

```ts
import { getOperatorConfig } from './operatorConfig';
import { StaffAuthApi, ChooseClubError } from './auth/staffAuthApi';
import { readStoredSession, writeStoredSession, clearStoredSession, sessionFromSignInResponse } from './auth/staffSessionStore';

export { ChooseClubError };

export interface OperatorAuthSession {
  staffUserId: string;
  organizationId: string;
  displayName: string;
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;                 // NEW — раньше жил в нативном сторе
  refreshTokenExpiresAtUtc: string;
  branchIds: string[];
  activeBranchId?: string;
  permissions: string[];
  roleNames?: string[];
}

function api(): StaffAuthApi {
  return new StaffAuthApi(getOperatorConfig().platformBaseUrl);
}

export function loadOperatorSession(): Promise<OperatorAuthSession | null> {
  return Promise.resolve(readStoredSession());
}

export async function signInByLoginOperator(login: string, password: string): Promise<OperatorAuthSession> {
  const session = sessionFromSignInResponse(await api().signInByLogin(login, password));
  writeStoredSession(session);
  return session;
}

export async function signInToClubOperator(organizationId: string, login: string, password: string): Promise<OperatorAuthSession> {
  const session = sessionFromSignInResponse(await api().signInToClub(organizationId, login, password));
  writeStoredSession(session);
  return session;
}

export async function refreshOperatorSession(): Promise<OperatorAuthSession> {
  const current = readStoredSession();
  if (!current) throw new Error('No session to refresh.');
  const session = sessionFromSignInResponse(await api().refresh(current.organizationId, current.refreshToken));
  writeStoredSession(session);
  return session;
}

export function signOutOperator(): Promise<{ signedOut: boolean }> {
  clearStoredSession();                 // серверного sign-out нет — локальная чистка
  return Promise.resolve({ signedOut: true });
}

export function forgotPasswordByEmail(userNameOrEmail: string) { return api().forgotByEmail(userNameOrEmail); }
export function resetPasswordByEmail(userNameOrEmail: string, code: string, newPassword: string) { return api().resetByEmail(userNameOrEmail, code, newPassword); }
export function forgotPasswordByPhone(phoneNumber: string) { return api().forgotByPhone(phoneNumber); }
export function resetPasswordByPhone(phoneNumber: string, code: string, newPassword: string) { return api().resetByPhone(phoneNumber, code, newPassword); }
```

> Старый `signInOperator(request)` заменяется на пару `signInByLoginOperator`/`signInToClubOperator`. Обновить все вызовы (grep `signInOperator`, `OperatorSignInRequest`). Тип `OperatorSignInRequest` можно удалить или оставить для явного клуба.

- [ ] **Step 4: Адаптировать `useOperatorAuth.ts`**

- `restore`: `loadOperatorSession()` → если сессия есть и access истёк (`isAccessTokenExpired`) — `refreshOperatorSession()`; при неудаче refresh → `clearStoredSession()` + signed-out.
- `handleSignIn`: работать через `signInByLoginOperator`; ловить `ChooseClubError` → выставить состояние выбора клуба (список клубов), затем `signInToClubOperator(orgId, login, password)`.
- `handleSignOut`: `signOutOperator()` (локально) → чистка (как сейчас в `finally`).
- Убрать зависимость от `HostBridgeUnavailableError`/`projectAuthHostError` в auth-пути (bridge больше не источник auth-ошибок); заменить на человекочитаемый разбор `PlatformApiError`/`Error`.

- [ ] **Step 5: Choose-club в sign-in view**

В экране входа добавить UI-состояние «выбор клуба»: если `handleSignIn` поймал `ChooseClubError`, показать список `clubs[].name`, по клику — `signInToClubOperator`. Строки — через i18n (`op.auth.chooseClub.*`), 3 локали + `bun run gen`.

- [ ] **Step 6: Run tests + build**

Run: `<bun> test` (весь оператор) и `<bun> run build`
Expected: зелёно; починить типы/вызовы `signInOperator` по месту.

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.Operator.App.Web/src
git commit -m "feat(operator): auth на прямом HTTP + выбор клуба при коллизии логина"
```

---

## Task 6: Оператор — авто-refresh токена в фабрике клиентов

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/operatorHelpers.ts` (`createAuthenticatedOperatorClients`)
- Test: `src/AFK4.Operator.App.Web/src/operatorHelpers.refresh.test.ts` (new)

**Interfaces:**
- `getAccessToken` в `PlatformApiClient` поддерживает async (`platformApi.ts`) — возвращаем свежий токен: если истёк, рефрешим и пишем в стор.

- [ ] **Step 1: Failing test** — истёкший токен → `getAccessToken` триггерит refresh и возвращает новый.

```ts
import { test, expect } from 'bun:test';
import { makeAccessTokenProvider } from './operatorHelpers';

test('provider refreshes expired token', async () => {
  const expired = { accessToken: 'old', accessTokenExpiresAtUtc: '2000-01-01T00:00:00Z',
    organizationId: 'o', refreshToken: 'r' } as never;
  let refreshed = false;
  const provider = makeAccessTokenProvider(expired, {
    isExpired: () => true,
    refresh: async () => { refreshed = true; return { ...expired, accessToken: 'new' } as never; }
  });
  expect(await provider()).toBe('new');
  expect(refreshed).toBe(true);
});
```

- [ ] **Step 2: Run — FAIL**

- [ ] **Step 3: Implement** — вынести провайдер токена (тестируемый), использовать в фабрике.

```ts
// operatorHelpers.ts
export function makeAccessTokenProvider(
  session: OperatorAuthSession,
  deps: { isExpired: (s: OperatorAuthSession, nowMs: number) => boolean; refresh: () => Promise<OperatorAuthSession> }
): () => Promise<string | null> {
  let current = session;
  let inFlight: Promise<OperatorAuthSession> | null = null;
  return async () => {
    if (deps.isExpired(current, Date.now())) {
      inFlight ??= deps.refresh().then((s) => { current = s; inFlight = null; return s; });
      current = await inFlight;
    }
    return current.accessToken;
  };
}

export function createAuthenticatedOperatorClients(config: OperatorConfig, session: OperatorAuthSession) {
  const getAccessToken = makeAccessTokenProvider(session, {
    isExpired: isAccessTokenExpired,
    refresh: refreshOperatorSession
  });
  return createOperatorApiClients(new PlatformApiClient({ baseUrl: config.platformBaseUrl, getAccessToken }));
}
```

> `Date.now()` в проде допустим (в workflow-скриптах он запрещён — это НЕ workflow). Импортировать `isAccessTokenExpired` из `./auth/staffSessionStore`, `refreshOperatorSession` из `./authClient`.

- [ ] **Step 4: Run — PASS** + `<bun> run build`

- [ ] **Step 5: Commit**

```bash
git commit -am "feat(operator): авто-refresh access-токена в фабрике клиентов"
```

---

## Task 7: WPF-хост — снос auth-моста

**Files:**
- Modify: `src/AFK4.Operator.App/Web/OperatorWebHostBridge.cs` (удалить `auth:*`, оставить `connection:*`)
- Delete: `src/AFK4.Operator.App/Auth/HttpOperatorAuthApiClient.cs`, `src/AFK4.Operator.App/Auth/ProtectedDataOperatorTokenStore.cs` + их интерфейсы/records
- Modify: DI-регистрация в WPF (где регистрировались `IOperatorAuthApiClient`/`IOperatorTokenStore`)

- [ ] **Step 1: Удалить auth-хендлеры** в `OperatorWebHostBridge.HandleAsync` — ветки `auth:loadToken/signIn/refresh/signOut/forgotByEmail/resetByEmail/forgotByPhone/resetByPhone` и приватные методы `LoadTokenAsync/SignInAsync/RefreshAsync/SignOutAsync/...`. Оставить роутинг `connection:*` и `window:*`. Тип-роутер, принимавший префикс `auth:`, теперь его не принимает.

- [ ] **Step 2: Удалить классы** `HttpOperatorAuthApiClient`, `ProtectedDataOperatorTokenStore`, интерфейсы `IOperatorAuthApiClient`/`IOperatorTokenStore`, `OperatorTokenSnapshot`, auth-payload records; снять их DI-регистрацию. `OperatorWebAuthSession`/маппинг из `StaffSignInResponse` — удалить, если больше не используется.

- [ ] **Step 3: Проверить bootstrap** — `OperatorWebBootstrapScript.cs` продолжает инъектить `__AFK4_OPERATOR_CONFIG__` c `platformBaseUrl`/`organizationId`/`branchId`. Веб теперь логинится сам по HTTP внутри WebView2 (fetch+sessionStorage доступны). Убедиться, что `platformBaseUrl` в инъекции корректный (тот же, куда ходят data-клиенты).

- [ ] **Step 4: Сборка WPF**

Run: сборка проекта `AFK4.Operator.App` (dotnet build — на Linux WPF может не собираться; см. [[afk4-env-quirks]] WPF-мост через D:\ clone). Если WPF не собирается на Linux — задачу выполнять/верифицировать на Windows-клоне; в SDD отметить как требующую Windows-сборки. Как минимум: удаляемые типы не должны иметь висячих ссылок (grep по решению).

Run (кросс-платформенно): `grep -rn "IOperatorAuthApiClient\|IOperatorTokenStore\|HttpOperatorAuthApiClient\|ProtectedDataOperatorTokenStore\|auth:signIn" src/AFK4.Operator.App` → пусто.

- [ ] **Step 5: Commit**

```bash
git add -A src/AFK4.Operator.App
git commit -m "refactor(operator-host): снести auth-мост, WPF-хост как тонкая оболочка"
```

---

## Task 8: Оператор — resolveActiveBranchId с явным выбором

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/operatorHelpers.ts` (`resolveActiveBranchId`)
- Test: `src/AFK4.Operator.App.Web/src/operatorHelpers.branch.test.ts` (new)

**Interfaces:**
- Produces: `resolveActiveBranchId(session, configBranchId?, chosenBranchId?): string | null` — приоритет `chosenBranchId` (если валиден) → `session.activeBranchId` → `configBranchId` → `branchIds[0]` → `null`. `chosenBranchId` игнорируется, если не в `session.branchIds`.

- [ ] **Step 1: Failing test**

```ts
import { test, expect } from 'bun:test';
import { resolveActiveBranchId } from './operatorHelpers';

const session = { branchIds: ['b1', 'b2'], activeBranchId: undefined } as never;

test('chosen branch wins when valid', () => {
  expect(resolveActiveBranchId(session, 'b1', 'b2')).toBe('b2');
});
test('invalid chosen falls through to machine pin', () => {
  expect(resolveActiveBranchId(session, 'b1', 'zzz')).toBe('b1');
});
test('no chosen → pin → first', () => {
  expect(resolveActiveBranchId(session, undefined, undefined)).toBe('b1');
});
```

- [ ] **Step 2: Run — FAIL** (сигнатура без `chosenBranchId`)

- [ ] **Step 3: Implement**

```ts
export function resolveActiveBranchId(
  session: OperatorAuthSession, configBranchId?: string, chosenBranchId?: string
): string | null {
  if (chosenBranchId && session.branchIds.includes(chosenBranchId)) return chosenBranchId;
  return session.activeBranchId ?? configBranchId ?? session.branchIds[0] ?? null;
}
```

- [ ] **Step 4: Run — PASS** (существующие вызовы с 2 аргументами не ломаются — 3-й опционален)

- [ ] **Step 5: Commit**

```bash
git commit -am "feat(operator): resolveActiveBranchId учитывает явный выбор филиала"
```

---

## Task 9: Оператор — useActiveBranch (localStorage)

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/branches/useActiveBranch.ts`
- Test: `src/AFK4.Operator.App.Web/src/branches/useActiveBranch.test.ts`

**Interfaces:**
- Produces: `useActiveBranch(branchIds: readonly string[]): { activeBranchId: string | null; select: (id: string) => void }`. Хранит в `localStorage['afk4.operator.activeBranchId']`. Дефолт — сохранённый если в списке, иначе `branchIds[0]`. `select` игнорирует id вне списка. При смене списка держит выбор валидным.

- [ ] **Step 1: Failing test** — рендер-хук через `@testing-library/react` (как другие хук-тесты оператора).

```ts
import { test, expect, beforeEach } from 'bun:test';
import { renderHook, act } from '@testing-library/react';
import { useActiveBranch } from './useActiveBranch';

beforeEach(() => localStorage.clear());

test('defaults to first branch, select persists', () => {
  const { result } = renderHook(() => useActiveBranch(['b1', 'b2']));
  expect(result.current.activeBranchId).toBe('b1');
  act(() => result.current.select('b2'));
  expect(result.current.activeBranchId).toBe('b2');
  expect(localStorage.getItem('afk4.operator.activeBranchId')).toBe('b2');
});

test('ignores select outside list', () => {
  const { result } = renderHook(() => useActiveBranch(['b1']));
  act(() => result.current.select('zzz'));
  expect(result.current.activeBranchId).toBe('b1');
});
```

- [ ] **Step 2: Run — FAIL**

- [ ] **Step 3: Implement** (порт `Platform.Web/src/club/branches/useActiveBranch.ts`, ключ на оператор)

```ts
import { useCallback, useEffect, useState } from 'react';

const KEY = 'afk4.operator.activeBranchId';

export function useActiveBranch(branchIds: readonly string[]): { activeBranchId: string | null; select: (id: string) => void } {
  const [activeBranchId, setActiveBranchId] = useState<string | null>(() => {
    const stored = localStorage.getItem(KEY);
    if (stored && branchIds.includes(stored)) return stored;
    return branchIds[0] ?? null;
  });

  useEffect(() => {
    if (activeBranchId && branchIds.includes(activeBranchId)) return;
    const next = branchIds[0] ?? null;
    setActiveBranchId(next);
    if (next) localStorage.setItem(KEY, next); else localStorage.removeItem(KEY);
  }, [branchIds, activeBranchId]);

  const select = useCallback((id: string) => {
    if (!branchIds.includes(id)) return;
    setActiveBranchId(id);
    localStorage.setItem(KEY, id);
  }, [branchIds]);

  return { activeBranchId, select };
}
```

- [ ] **Step 4: Run — PASS**

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/branches/useActiveBranch.ts src/AFK4.Operator.App.Web/src/branches/useActiveBranch.test.ts
git commit -m "feat(operator): useActiveBranch (выбор филиала в localStorage)"
```

---

## Task 10: Оператор — BranchSwitcher + useBranchDirectory

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/branches/useBranchDirectory.ts`
- Create: `src/AFK4.Operator.App.Web/src/branches/BranchSwitcher.tsx`
- Test: `src/AFK4.Operator.App.Web/src/branches/BranchSwitcher.test.tsx`

**Interfaces:**
- Consumes: доменный клиент филиалов оператора для имени профиля (проверить `api/clients` — есть ли `branches`/`getBranchProfile`; путь `GET /api/branches/{id}/profile`). Если клиента нет — добавить тонкий метод в существующий клиент.
- Produces: `useBranchDirectory(getProfile, branchIds): Record<string, { name: string }>`; `<BranchSwitcher branches={{branchId,name}[]} activeBranchId onSelect />` — в стиле оператора (kit `.ui-*`, dark-тема), НЕ копия shadcn-разметки Platform.Web (перерисовать под операторский shell).

- [ ] **Step 1: Failing test** — свитчер рендерит активный филиал и вызывает `onSelect`.

```tsx
import { test, expect, mock } from 'bun:test';
import { render, screen, fireEvent } from '@testing-library/react';
import { BranchSwitcher } from './BranchSwitcher';

test('renders active branch and fires onSelect', () => {
  const onSelect = mock(() => {});
  render(<BranchSwitcher branches={[{ branchId: 'b1', name: 'Center' }, { branchId: 'b2', name: 'Sever' }]}
    activeBranchId="b1" onSelect={onSelect} />);
  expect(screen.getByText('Center')).toBeInTheDocument();
  fireEvent.click(screen.getByText('Sever'));
  expect(onSelect).toHaveBeenCalledWith('b2');
});
```

- [ ] **Step 2: Run — FAIL**

- [ ] **Step 3: Implement** `useBranchDirectory.ts` (параллельная дозагрузка профилей, ошибки глотать) + `BranchSwitcher.tsx` (dropdown на операторском kit; тач-таргеты ≥44px под будущую мобилку).

`useBranchDirectory` (эскиз):
```ts
import { useEffect, useState } from 'react';
export function useBranchDirectory(
  getProfile: (branchId: string) => Promise<{ name: string }>, branchIds: readonly string[]
): Record<string, { name: string }> {
  const [dir, setDir] = useState<Record<string, { name: string }>>({});
  useEffect(() => {
    let alive = true;
    Promise.all(branchIds.map(async (id) => {
      try { const p = await getProfile(id); return [id, { name: p.name }] as const; }
      catch { return [id, { name: id }] as const; }
    })).then((entries) => { if (alive) setDir(Object.fromEntries(entries)); });
    return () => { alive = false; };
  }, [branchIds, getProfile]);
  return dir;
}
```

- [ ] **Step 4: Run — PASS**

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/branches/
git commit -m "feat(operator): BranchSwitcher + справочник имён филиалов"
```

---

## Task 11: Оператор — App.tsx реактивный контекст филиала + свитчер в shell

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/App.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/useOperatorRealtime.ts` (deps)
- Test: `src/AFK4.Operator.App.Web/src/App.branchSwitch.test.tsx` (new)

**Interfaces:**
- Consumes: `useActiveBranch` (Task 9), `BranchSwitcher`/`useBranchDirectory` (Task 10), `resolveActiveBranchId` c 3-м аргументом (Task 8).

- [ ] **Step 1: Failing test** — при `branchIds.length > 1` свитчер виден; выбор филиала меняет `backendContext.branchId`, ведёт к перезагрузке (рефетчу) данных; при одном филиале свитчер скрыт. Тест на уровне App с мок-клиентами (как существующие `App.test`-прогоны — отдельным `bun test`-запуском, см. [[operator-redesign-phase0-decisions]]).

Ключевые проверки:
```tsx
// >1 филиал → свитчер в шапке; клик по другому филиалу → data-клиенты вызваны с новым branchId
// 1 филиал → свитчер отсутствует (queryByRole/queryByText null)
```

- [ ] **Step 2: Run — FAIL**

- [ ] **Step 3: Implement в `App.tsx`**

- Ввести выбор филиала: `const { activeBranchId: chosenBranchId, select } = useActiveBranch(authSession?.branchIds ?? []);`
- Заменить строку 105:
  ```ts
  const activeBranchId = authSession === null ? null
    : resolveActiveBranchId(authSession, config.branchId, chosenBranchId ?? undefined);
  ```
- Мемоизировать `backendContext` по `[config, authSession, activeBranchId]` (сейчас пересоздаётся каждый рендер — оставить можно, но зависимые эффекты завязать на `activeBranchId`).
- Свитчер в шапке shell (там же, где заголовок/навигация): рендерить `<BranchSwitcher .../>` только если `(authSession?.branchIds.length ?? 0) > 1`. Список имён — `useBranchDirectory(clients.branches.getBranchProfile, authSession.branchIds)` (клиенты создать из `backendContext`/фабрики).
- Пересчёт гейта при смене филиала: в `useEffect` редиректа на `firstAllowedWorkspace` (стр. 161-169) добавить `activeBranchId` в deps, чтобы после смены филиала (и потенциально другого набора доступных разделов) увести с недоступного воркспейса.

- [ ] **Step 4: `useOperatorRealtime.ts` — переподписка на активный филиал**

- Прокинуть активный branch в realtime: заменить внутренний `resolveActiveBranchId(authSession, config.branchId)` (стр. 66) на переданный `activeBranchId` (добавить в `UseOperatorRealtimeOptions`), и добавить его в массив зависимостей эффекта (стр. 218). Это гарантирует чистый teardown (существующий `return () => realtimeClient.stop()`) и переподписку на новый филиал.

- [ ] **Step 5: Run — PASS** (целевой тест + весь `<bun> test`) + `<bun> run build`

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Operator.App.Web/src
git commit -m "feat(operator): реактивный контекст филиала + свитчер в shell"
```

---

## Task 12: Оператор — браузерная сборка (platformBaseUrl из env) + чистка dev-моста

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/operatorConfig.ts` (браузерный источник `platformBaseUrl`)
- Modify: `src/AFK4.Operator.App.Web/src/devHostBridge.ts` (убрать `auth:*` ветки)
- Modify: `src/AFK4.Operator.App.Web/vite.config.*` / `.env` (env-переменная baseUrl)
- Test: `src/AFK4.Operator.App.Web/src/operatorConfig.test.ts` (new)

- [ ] **Step 1: Failing test** — в браузере без инъекции конфиг берёт `platformBaseUrl` из env, иначе явная ошибка.

```ts
import { test, expect } from 'bun:test';
import { getOperatorConfig } from './operatorConfig';

test('browser config uses injected config when present', () => {
  (window as never as { __AFK4_OPERATOR_CONFIG__?: unknown }).__AFK4_OPERATOR_CONFIG__ =
    { runtime: 'browser', shellMode: 'web', platformBaseUrl: 'https://api.example/', currencyCode: 'TJS' };
  expect(getOperatorConfig().platformBaseUrl).toBe('https://api.example/');
});
```

- [ ] **Step 2: Run — FAIL** (или уже частично проходит — довести до нужного поведения)

- [ ] **Step 3: Implement**

- `operatorConfig.ts`: если `window.__AFK4_OPERATOR_CONFIG__` нет (браузер), собрать конфиг из `import.meta.env.VITE_PLATFORM_BASE_URL` (обязателен в браузерном билде; отсутствует → бросить понятную ошибку конфигурации, НЕ молчаливый fallback на localhost в prod-браузере). Dev-режим сохраняет текущий `fallbackConfig`.
- `devHostBridge.ts`: удалить `auth:*` из `handle`/`previewAuthResponse`/overlay-проксирования — auth теперь HTTP; `installMockFetch`/`devMockFetch` продолжает мокать `/api/auth/staff/*` для preview-режима (перенести туда мок-логин, который раньше был в `handle('auth:signIn')`).
- Добавить `VITE_PLATFORM_BASE_URL` в `.env.example`/README (не хардкодить реальные URL).

- [ ] **Step 4: Run — PASS** + `<bun> test` (весь) + `<bun> run build`

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web
git commit -m "feat(operator): браузерная сборка (platformBaseUrl из env) + чистка dev-моста"
```

---

## Финальные гейты (перед finishing-a-development-branch)

- `dotnet test tests/AFK4.Platform.Api.Tests` — зелёный (вкл. новые StaffContext/authorization тесты).
- `<bun> test` (оператор, вкл. отдельный App.test-прогон) — зелёный.
- `<bun> run build` (оператор) — зелёный (tsc тайпчекает тесты).
- Ручная проверка обоих хостов: браузерный билд логинится и переключает филиалы; WPF-билд логинится (теперь по HTTP) и работает (сборка WPF — на Windows-клоне при необходимости, см. Task 7).
- i18n: `cd packages/i18n && bun run gen` выполнен, `tg` реально таджикский, guard `voice.test.ts`/`tg===ru` зелёный.

Открытые пункты закрыты в спеке (sign-in двухступенчатый, PC-control удалённо ОК, auth-код копируем). Cross-branch эскалация чинится Tasks 1-2 до свитчера.
