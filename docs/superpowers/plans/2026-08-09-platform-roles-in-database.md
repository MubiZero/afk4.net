# Роли платформы в базе — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Состав прав роли платформы меняется мышкой в панели, а не релизом, — и при этом ни один путь не позволяет выдать себе больше, чем есть, отнять у себя ключ от платформы или оставить платформу без полного администратора.

**Architecture:** Названия прав остаются в коде (на них ссылаются эндпоинты); в базу переезжают **роли** — именованные наборы этих прав, плюс флаг «полный доступ» для роли администратора. Права пользователя считаются из базы при каждом запросе, поэтому отнятое право перестаёт действовать сразу, без перелогина.

**Tech Stack:** .NET 10 (Platform.Api, EF Core, Postgres), xUnit + `PlatformApiFactory`, React 19 + TypeScript (PlatformControl.Web), `bun test`, `@afk4/i18n`.

**Ветка:** `feat/platform-roles-in-database-wave-d`

## Global Constraints

- **Названия прав живут в коде.** `PlatformAdminPermissionNames` — источник истины; право, которое никто не проверяет, ничего не даёт. Из панели заводятся роли, а не права.
- **Нельзя выдать больше, чем есть у тебя.** Изменение состава роли не может добавить в неё право, которого нет у действующего администратора. Проверка серверная, при каждом изменении.
- **Ключ от платформы нельзя выбросить.** Право `platform.admins.manage` нельзя отнять у роли, которую носит сам действующий администратор, и нельзя отнять у последней роли, которая его даёт.
- **Полный администратор не исчезает.** Инвариант волны A (`LastFullAdmin`) распространяется на роли: нельзя снять флаг полного доступа с последней роли, которая его несёт.
- **Права считаются по текущему составу роли**, а не по слепку на момент входа. Отнятое право перестаёт действовать сразу.
- **Инварианты каталога держит serializable-транзакция**, как уже сделано в `PlatformAdminDirectoryService`; serialization failure маппится в generic `Conflict`, НЕ в конкретную причину отказа — иначе пользователь получит ложное объяснение.
- **Сегодняшнее поведение не меняется.** Две встроенные роли заводятся ровно с теми наборами прав, что сегодня захардкожены. Ни один администратор не теряет и не получает права от самого переезда.
- **Сервер прозу не рендерит.** Отказ несёт машинный код; фразу собирает панель из `@afk4/i18n`.
- **Каждое изменение роли пишется в аудит платформы.**
- **Новые таблицы — snake_case** (`platform_roles`, `platform_role_permissions`), колонки PascalCase в кавычках; сырой SQL в миграциях сверяется с `PlatformDbContext`.
- **Тесты на гонки — только на настоящем Postgres** (`[PlatformAdminPostgresFact]`).
- **Строки интерфейса — только через `@afk4/i18n`** (`locales/{ru,en,tg}.json` → `bun run gen`), таджикский по-таджикски.
- **`bun` только по полному пути** `~/.bun/bin/bun`; `bun run build` обязателен.

## Что уже есть в коде (контекст для исполнителя)

- `PlatformAdminUserEntity.RolesJson` — роли администратора списком строк. Эта форма **не меняется**: переезжает не «кто в какой роли», а «что роль даёт».
- `PlatformAdminPermissionCatalog` (`src/AFK4.Platform.Api/Platform/Identity/`) — статический словарь роль→права с методами `GetPermissions(roles)` и `IsKnownRole(role)`. Именно словарь и уезжает в базу.
- `PlatformAdminRoleNames` (`platform_admin`, `platform_support`) — **константы остаются**: ими называются встроенные строки в таблице, на них ссылается бутстрап первого администратора (`PlatformAdminBootstrapHostedService`) и dev-сид.
- Контекст администратора **уже пересчитывается на каждый запрос**: `PlatformAdminAuthenticationMiddleware` → `IPlatformAdminTokenService.ValidateAsync` → `OpaquePlatformAdminTokenService.CreateContext(user)`. Сегодня `CreateContext` синхронный и берёт права из статического словаря; после переезда он обязан брать их из базы.
- `PlatformAdminAuthorizationService.RequirePermission` читает `context.Permissions` — не меняется.
- `PlatformAdminDirectoryService` знает про роли три вещи, и все три завязаны на хардкод:
  - `IsFullAdminRole(role)` — сравнение имени с `platform_admin`;
  - `IsRoleDowngrade(from, to)` — **сравнение количества прав**; при редактируемых ролях счёт прав ничего не значит;
  - `PrimaryRole(roles)` — «если есть `platform_admin`, то он, иначе первая по алфавиту».
  Плюс `UpdateCoreAsync` защищает инварианты `LastFullAdmin` и `SelfDemotion` в serializable-транзакции — образец, которому следует новая работа.
- Панель знает свои возможности через `src/AFK4.PlatformControl.Web/src/auth/platformAccess.ts` — таблица «возможность → нужные права». Она не меняется: права те же.
- Прецедент «объявлено в коде, живёт в базе» из предыдущего плана волны — `FeatureCatalog` + `FeatureCatalogSeedHostedService` (`src/AFK4.Platform.Api/Platform/Entitlements/`). Держись того же вида.

## Ключевое решение: флаг «полный доступ»

Роль администратора получает флаг `GrantsAllPermissions`. Роль с этим флагом имеет **все** права из кода — включая те, что появятся завтра.

Зачем: если бы состав роли администратора был обычным списком строк в базе, то каждое новое право, добавленное в код, после деплоя не принадлежало бы никому, и новый раздел был бы недоступен всем до ручной правки роли. Это тихая поломка, которую замечают в проде. Флаг снимает её и остаётся данными: его видно и можно снять в панели.

Роли без флага перечисляют права явно — и новое право к ним само не приезжает. Это правильно: кому его дать, решает человек.

## Структура файлов

**Создаются:**

| Файл | Ответственность |
|---|---|
| `src/AFK4.Platform.Api/Data/PlatformRoleEntity.cs` | Строка роли: имя, описание, встроенность, флаг полного доступа |
| `src/AFK4.Platform.Api/Data/PlatformRolePermissionEntity.cs` | Право в составе роли |
| `src/AFK4.Platform.Api/Platform/Identity/PlatformRoleCatalog.cs` | Объявление встроенных ролей в коде |
| `src/AFK4.Platform.Api/Platform/Identity/PlatformRoleSeedHostedService.cs` | Заведение встроенных ролей в базу при старте |
| `src/AFK4.Platform.Api/Platform/Identity/IPlatformRolePermissionResolver.cs` | Контракт «роли → права» |
| `src/AFK4.Platform.Api/Platform/Identity/EfPlatformRolePermissionResolver.cs` | Реализация поверх базы |
| `src/AFK4.Platform.Api/Platform/Identity/PlatformRoleService.cs` | Правка состава ролей с предохранителями |
| `src/AFK4.Platform.Api/Endpoints/PlatformRoleEndpoints.cs` | Эндпоинты управления ролями |
| `src/AFK4.Shared.Contracts/Platform/Auth/PlatformRoleContracts.cs` | DTO ролей и запросов |
| `src/AFK4.PlatformControl.Web/src/platform/settings/RolesSection.tsx` | Экран ролей в настройках |

**Изменяются:** `PlatformDbContext` + миграция, `Program.cs`, `PlatformAdminPermissionNames` (список всех прав), `PlatformAdminPermissionCatalog` (удаляется словарь), `OpaquePlatformAdminTokenService`, `PlatformAdminDirectoryService`, панель (`SettingsScreen.tsx`, клиент API, `locales/*.json`).

---

### Task 1: Схема ролей, полный список прав и сидер

**Files:**
- Create: `src/AFK4.Platform.Api/Data/PlatformRoleEntity.cs`
- Create: `src/AFK4.Platform.Api/Data/PlatformRolePermissionEntity.cs`
- Create: `src/AFK4.Platform.Api/Platform/Identity/PlatformRoleCatalog.cs`
- Create: `src/AFK4.Platform.Api/Platform/Identity/PlatformRoleSeedHostedService.cs`
- Modify: `src/AFK4.Shared.Contracts/Platform/Auth/PlatformAdminPermissionNames.cs`
- Modify: `src/AFK4.Platform.Api/Data/PlatformDbContext.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs`
- Test: `tests/AFK4.Shared.Contracts.Tests/Platform/PlatformAdminPermissionNamesTests.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/PlatformRoleSeedTests.cs`

**Interfaces:**
- Produces: `PlatformAdminPermissionNames.All` (`IReadOnlyList<string>`);
  `PlatformRoleEntity(RoleName, DisplayName, Description, IsBuiltIn, GrantsAllPermissions, CreatedAtUtc, UpdatedAtUtc)`;
  `PlatformRolePermissionEntity(PlatformRolePermissionId, RoleName, PermissionName)`;
  `PlatformRoleCatalog.Declared`.

- [ ] **Step 1: Написать падающий страж-тест на полноту списка прав**

`tests/AFK4.Shared.Contracts.Tests/Platform/PlatformAdminPermissionNamesTests.cs`:

```csharp
using System.Reflection;
using AFK4.Shared.Contracts.Platform.Auth;

namespace AFK4.Shared.Contracts.Tests.Platform;

public sealed class PlatformAdminPermissionNamesTests
{
    [Fact]
    public void All_ListsEveryDeclaredPermissionConstant()
    {
        var constants = typeof(PlatformAdminPermissionNames)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToArray();

        // Список All — то, что панель показывает как чекбоксы, а роль с полным доступом получает
        // целиком. Право, забытое в списке, нельзя ни выдать роли, ни увидеть — и обнаружится это
        // только жалобой из прода.
        Assert.Equal(
            constants.OrderBy(name => name, StringComparer.Ordinal),
            PlatformAdminPermissionNames.All.OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public void All_HasNoDuplicates()
    {
        Assert.Equal(PlatformAdminPermissionNames.All.Count, PlatformAdminPermissionNames.All.Distinct(StringComparer.Ordinal).Count());
    }
}
```

- [ ] **Step 2: Прогнать тест и убедиться, что он падает**

```bash
cd /home/fedya/projects/afk4.net
dotnet test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter FullyQualifiedName~PlatformAdminPermissionNamesTests
```

Ожидание: ошибка компиляции — `All` не существует.

- [ ] **Step 3: Список всех прав**

В конец `PlatformAdminPermissionNames` добавить:

```csharp
    /// <summary>
    /// Все права платформы. Роль с полным доступом получает этот список целиком, панель
    /// показывает его как набор переключателей. Полнота стережётся тестом.
    /// </summary>
    public static readonly IReadOnlyList<string> All =
    [
        UseSupportAccess,
        ViewOrganizations,
        CreateOrganization,
        UpdateOrganizationStatus,
        UpdateOrganizationLimits,
        UpdateOrganizationProfile,
        UpdateOrganizationUpdateChannel,
        ViewOrganizationSupportNotes,
        ManageOrganizationSupportNotes,
        ManageOrganizationOwnerInvites,
        TransferOrganizationOwner,
        ViewOrganizationHealth,
        ManageOrganizationFeatures,
        ViewPlatformAudit,
        ViewBilling,
        ManagePlans,
        ManageSubscriptions,
        ManageInvoices,
        ViewUpdates,
        ManageUpdatePackages,
        ManageUpdateRollouts,
        ManagePlatformAdmins,
        ViewPlatformHealth
    ];
```

Сверить список с фактическим набором констант в файле — если там появились ещё права, добавить и их; страж-тест поймает расхождение.

- [ ] **Step 4: Сущности**

`src/AFK4.Platform.Api/Data/PlatformRoleEntity.cs`:

```csharp
namespace AFK4.Platform.Api.Data;

/// <summary>
/// Роль платформы — именованный набор прав. Названия самих прав живут в коде: право, которое
/// никто не проверяет, ничего не даёт.
/// </summary>
public sealed class PlatformRoleEntity
{
    public string RoleName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Встроенную роль можно редактировать, но нельзя удалить.</summary>
    public bool IsBuiltIn { get; set; }

    /// <summary>
    /// Роль с полным доступом имеет все права из кода, включая те, что появятся завтра.
    /// Без этого флага каждое новое право после деплоя не принадлежало бы никому, и новый раздел
    /// был бы недоступен всем до ручной правки роли.
    /// </summary>
    public bool GrantsAllPermissions { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
```

`src/AFK4.Platform.Api/Data/PlatformRolePermissionEntity.cs`:

```csharp
namespace AFK4.Platform.Api.Data;

public sealed class PlatformRolePermissionEntity
{
    public Guid PlatformRolePermissionId { get; set; }

    public string RoleName { get; set; } = string.Empty;

    public string PermissionName { get; set; } = string.Empty;
}
```

- [ ] **Step 5: Регистрация в `PlatformDbContext`**

DbSet-ы:

```csharp
    public DbSet<PlatformRoleEntity> PlatformRoles => Set<PlatformRoleEntity>();

    public DbSet<PlatformRolePermissionEntity> PlatformRolePermissions => Set<PlatformRolePermissionEntity>();
```

Конфигурация в `OnModelCreating` рядом с блоком `PlatformFeatureEntity`:

```csharp
        modelBuilder.Entity<PlatformRoleEntity>(entity =>
        {
            entity.ToTable("platform_roles");
            entity.HasKey(role => role.RoleName);
            entity.Property(role => role.RoleName).HasMaxLength(64);
            entity.Property(role => role.DisplayName).HasMaxLength(128).IsRequired();
            entity.Property(role => role.Description).HasMaxLength(500).IsRequired();
        });

        modelBuilder.Entity<PlatformRolePermissionEntity>(entity =>
        {
            entity.ToTable("platform_role_permissions");
            entity.HasKey(rolePermission => rolePermission.PlatformRolePermissionId);
            entity.Property(rolePermission => rolePermission.RoleName).HasMaxLength(64).IsRequired();
            entity.Property(rolePermission => rolePermission.PermissionName).HasMaxLength(128).IsRequired();
            entity.HasIndex(rolePermission => new { rolePermission.RoleName, rolePermission.PermissionName })
                .IsUnique()
                .HasDatabaseName("IX_platform_role_permissions_Role_Permission");
            entity.HasIndex(rolePermission => rolePermission.RoleName);
        });
```

Уникальный индекс — не украшение: «одно право в роли не дублируется» держит база, а не аккуратность вызывающего кода.

- [ ] **Step 6: Объявление встроенных ролей и сидер**

`src/AFK4.Platform.Api/Platform/Identity/PlatformRoleCatalog.cs` — две роли ровно с теми правами, что сегодня в `PlatformAdminPermissionCatalog`:

```csharp
using AFK4.Shared.Contracts.Platform.Auth;

namespace AFK4.Platform.Api.Platform.Identity;

/// <summary>Встроенные роли, объявленные кодом. Дальше их состав правит панель.</summary>
public static class PlatformRoleCatalog
{
    public sealed record Declaration(
        string RoleName,
        string DisplayName,
        string Description,
        bool GrantsAllPermissions,
        IReadOnlyList<string> Permissions);

    public static readonly IReadOnlyList<Declaration> Declared =
    [
        new(PlatformAdminRoleNames.PlatformAdmin,
            "Администратор платформы",
            "Полный доступ ко всем разделам платформы.",
            GrantsAllPermissions: true,
            Permissions: []),
        new(PlatformAdminRoleNames.PlatformSupport,
            "Поддержка",
            "Наблюдение за клубами, заметки и приглашения владельцев без доступа к деньгам и раскатам.",
            GrantsAllPermissions: false,
            Permissions:
            [
                PlatformAdminPermissionNames.UseSupportAccess,
                PlatformAdminPermissionNames.ViewOrganizations,
                PlatformAdminPermissionNames.UpdateOrganizationStatus,
                PlatformAdminPermissionNames.ViewOrganizationSupportNotes,
                PlatformAdminPermissionNames.ManageOrganizationSupportNotes,
                PlatformAdminPermissionNames.ManageOrganizationOwnerInvites,
                PlatformAdminPermissionNames.ViewOrganizationHealth,
                PlatformAdminPermissionNames.ViewPlatformAudit,
                PlatformAdminPermissionNames.ViewPlatformHealth
            ])
    ];
}
```

Перед тем как писать список прав поддержки — открыть `PlatformAdminPermissionCatalog.cs` и перенести набор `PlatformSupport` **дословно**. Любое расхождение здесь тихо меняет права живых людей.

`PlatformRoleSeedHostedService` — по образцу `FeatureCatalogSeedHostedService`: добавляет недостающие объявленные роли вместе с их правами, **не трогает существующие строки** (после создания авторитетна панель) и **не выходит рано** на непустой таблице. Зарегистрировать в `Program.cs` тем же способом, что и сидер фич.

- [ ] **Step 7: Написать падающий тест сидера**

`tests/AFK4.Platform.Api.Tests/Platform/PlatformRoleSeedTests.cs` — четыре проверки:

```csharp
[Fact] public async Task Seed_CreatesBothBuiltInRoles() { }
[Fact] public async Task Seed_GivesSupportExactlyTodaysPermissions() { }
[Fact] public async Task Seed_MarksAdminRoleAsGrantingAllPermissions() { }
[Fact] public async Task Seed_DoesNotOverwriteAnExistingRole() { }
```

Тела написать настоящим кодом. `Seed_GivesSupportExactlyTodaysPermissions` — ключевой: он сравнивает набор прав роли поддержки в базе с **девятью конкретными правами**, перечисленными явно в тесте (не через `PlatformRoleCatalog`, иначе тест сверяет объявление само с собой и ничего не доказывает). `Seed_DoesNotOverwriteAnExistingRole` меняет описание и состав существующей роли, повторно прогоняет сидер и проверяет, что правка уцелела.

- [ ] **Step 8: Миграция**

```bash
dotnet ef migrations add AddPlatformRoles --project src/AFK4.Platform.Api --startup-project src/AFK4.Platform.Api --output-dir Data/Migrations
```

Прочитать сгенерированный файл и сверить имена таблиц, колонок и индексов с `PlatformDbContext`.

- [ ] **Step 9: Прогнать тесты**

```bash
dotnet test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj
```

Ожидание: всё зелёное. На этом шаге поведение не изменилось — таблицы заполнены, но их ещё никто не читает.

- [ ] **Step 10: Коммит**

```bash
git add src/AFK4.Platform.Api/Data/ src/AFK4.Platform.Api/Platform/Identity/ \
        src/AFK4.Shared.Contracts/Platform/Auth/PlatformAdminPermissionNames.cs \
        src/AFK4.Platform.Api/Program.cs tests/
git commit -m "feat(platform): таблицы ролей платформы и полный список прав"
```

---

### Task 2: Права считаются из базы на каждом запросе

**Files:**
- Create: `src/AFK4.Platform.Api/Platform/Identity/IPlatformRolePermissionResolver.cs`
- Create: `src/AFK4.Platform.Api/Platform/Identity/EfPlatformRolePermissionResolver.cs`
- Modify: `src/AFK4.Platform.Api/Platform/Identity/OpaquePlatformAdminTokenService.cs`
- Modify: `src/AFK4.Platform.Api/Platform/Identity/PlatformAdminPermissionCatalog.cs` (удалить статический словарь)
- Modify: `src/AFK4.Platform.Api/Program.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/PlatformRolePermissionResolverTests.cs`

**Interfaces:**
- Consumes: `PlatformRoles`, `PlatformRolePermissions`, `PlatformAdminPermissionNames.All`.
- Produces: `IPlatformRolePermissionResolver.ResolveAsync(IEnumerable<string> roleNames, CancellationToken)` → `IReadOnlySet<string>`; `IsKnownRoleAsync(string roleName, CancellationToken)` → `bool`.

- [ ] **Step 1: Написать падающий тест**

`tests/AFK4.Platform.Api.Tests/Platform/PlatformRolePermissionResolverTests.cs` — шесть проверок:

```csharp
[Fact] public async Task Resolve_ReturnsPermissionsOfTheRole() { }
[Fact] public async Task Resolve_UnionsPermissionsOfSeveralRoles() { }
[Fact] public async Task Resolve_GivesEveryPermissionToARoleThatGrantsAll() { }
[Fact] public async Task Resolve_IgnoresUnknownRoleNames() { }
[Fact] public async Task Resolve_ReflectsAPermissionRemovedRightNow() { }
[Fact] public async Task IsKnownRole_AnswersFromTheDatabase() { }
```

Тела написать настоящим кодом. `Resolve_GivesEveryPermissionToARoleThatGrantsAll` сравнивает результат с `PlatformAdminPermissionNames.All` целиком. `Resolve_ReflectsAPermissionRemovedRightNow` — ключевой: разрешить, снять право строкой из `PlatformRolePermissions`, разрешить снова и убедиться, что второй ответ уже без него; это и есть смысл всей задачи.

- [ ] **Step 2: Прогнать тест и убедиться, что он падает**

```bash
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter FullyQualifiedName~PlatformRolePermissionResolverTests
```

- [ ] **Step 3: Резолвер**

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Auth;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Identity;

public sealed class EfPlatformRolePermissionResolver(PlatformDbContext dbContext) : IPlatformRolePermissionResolver
{
    public async Task<IReadOnlySet<string>> ResolveAsync(
        IEnumerable<string> roleNames,
        CancellationToken cancellationToken)
    {
        var names = roleNames.ToArray();
        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (names.Length == 0)
        {
            return permissions;
        }

        var roles = await dbContext.PlatformRoles
            .AsNoTracking()
            .Where(role => names.Contains(role.RoleName))
            .Select(role => new { role.RoleName, role.GrantsAllPermissions })
            .ToArrayAsync(cancellationToken);

        // Роль с полным доступом получает и те права, которых ещё не существовало, когда её
        // заводили: иначе новое право после деплоя не принадлежит никому.
        if (roles.Any(role => role.GrantsAllPermissions))
        {
            permissions.UnionWith(PlatformAdminPermissionNames.All);
            return permissions;
        }

        var granted = await dbContext.PlatformRolePermissions
            .AsNoTracking()
            .Where(rolePermission => names.Contains(rolePermission.RoleName))
            .Select(rolePermission => rolePermission.PermissionName)
            .ToArrayAsync(cancellationToken);

        permissions.UnionWith(granted);

        // Право, исчезнувшее из кода, не должно продолжать действовать из-за старой строки в базе.
        permissions.IntersectWith(PlatformAdminPermissionNames.All);
        return permissions;
    }

    public Task<bool> IsKnownRoleAsync(string roleName, CancellationToken cancellationToken) =>
        dbContext.PlatformRoles.AnyAsync(role => role.RoleName == roleName, cancellationToken);
}
```

- [ ] **Step 4: Подключить к контексту администратора**

В `OpaquePlatformAdminTokenService` сделать `CreateContext` асинхронным и брать права у резолвера вместо `PlatformAdminPermissionCatalog.GetPermissions`. Резолвер получить через первичный конструктор.

Это ровно то место, ради которого задача делается: контекст строится в `ValidateAsync` **на каждый запрос**, поэтому снятое право перестаёт действовать сразу, без перелогина. Один дополнительный запрос к базе на аутентифицированный запрос при горстке администраторов и двух-трёх ролях — приемлемая цена; кэш здесь был бы прямым отказом от цели.

- [ ] **Step 5: Убрать статический словарь**

Из `PlatformAdminPermissionCatalog` удалить `RolePermissions`, `GetPermissions` и `IsKnownRole`. Если после этого файл становится пустым — удалить файл целиком. Все вызывающие переводятся на резолвер; места перечислены в задаче 3, и до её завершения проект может не собираться только в них — тогда сделай минимальные правки вызовов здесь, а смысловую часть (сравнение ролей, `LastFullAdmin`) оставь задаче 3.

- [ ] **Step 6: Прогнать тесты**

```bash
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj
```

Ожидание: всё зелёное. Существующие тесты входа и прав обязаны пройти **без правок утверждений** — если какой-то падает, значит переезд изменил чьи-то права, и это дефект, а не повод поправить тест.

- [ ] **Step 7: Коммит**

```bash
git add src/AFK4.Platform.Api/ tests/
git commit -m "feat(platform): права администратора считаются из базы на каждом запросе"
```

---

### Task 3: Каталог администраторов перестаёт знать имена ролей

**Files:**
- Modify: `src/AFK4.Platform.Api/Platform/Identity/PlatformAdminDirectoryService.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/PlatformAdminDirectoryServiceTests.cs`

**Interfaces:**
- Consumes: `IPlatformRolePermissionResolver`, `PlatformRoles.GrantsAllPermissions`.

- [ ] **Step 1: Написать падающие тесты**

Добавить в существующий файл тестов каталога:

```csharp
[Fact] public async Task Update_RefusesToRemoveTheLastRoleThatGrantsFullAccess() { }
[Fact] public async Task Update_TreatsAnyRoleWithFullAccessAsFullAdmin() { }
[Fact] public async Task Update_DetectsSelfDemotionByLostPermissions_NotByCount() { }
[Fact] public async Task Update_AcceptsACustomRoleCreatedInThePanel() { }
```

Тела написать настоящим кодом, по образцу существующих тестов этого файла.

`Update_DetectsSelfDemotionByLostPermissions_NotByCount` — ключевой. Сцена: две роли с **одинаковым числом прав**, но разными наборами; администратор переводит сам себя со своей роли на вторую и теряет право. Сегодняшняя проверка по количеству это пропустит, правильная — поймает. Второй частью того же теста проверить обратное: переход на роль с тем же или расширенным набором прав самопонижением не считается.

`Update_TreatsAnyRoleWithFullAccessAsFullAdmin` — заведённая в панели роль с флагом полного доступа обязана считаться полноценным администратором в инварианте «последний не исчезает»; проверка по имени `platform_admin` этого не увидит.

- [ ] **Step 2: Прогнать тесты и убедиться, что они падают**

```bash
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter FullyQualifiedName~PlatformAdminDirectoryServiceTests
```

- [ ] **Step 3: Переписать три места**

- `IsFullAdminRole(role)` → проверка флага `GrantsAllPermissions` у строки роли в базе.
- `IsRoleDowngrade(from, to)` → сравнение **множеств** прав: понижение = в новом наборе отсутствует хотя бы одно право из старого. Счёт прав при редактируемых ролях ничего не значит: две роли по десять прав могут не пересекаться вовсе.
- Валидация `request.Role` → `IsKnownRoleAsync` вместо статического `IsKnownRole`, чтобы роль, заведённая в панели, принималась.

`PrimaryRole(roles)` оставить как есть по форме, но «главной» считать роль с полным доступом, а не строку `platform_admin`.

Все обращения к базе внутри `UpdateCoreAsync` уже идут в serializable-транзакции — новые чтения ролей должны оказаться **внутри** неё, иначе инвариант «последний полный администратор» перестанет держаться под гонкой.

- [ ] **Step 4: Прогнать тесты**

```bash
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj
```

- [ ] **Step 5: Коммит**

```bash
git add src/AFK4.Platform.Api/Platform/Identity/PlatformAdminDirectoryService.cs tests/
git commit -m "feat(platform): каталог администраторов опирается на роли из базы, а не на имена"
```

---

### Task 4: Управление ролями — эндпоинты и предохранители

**Files:**
- Create: `src/AFK4.Shared.Contracts/Platform/Auth/PlatformRoleContracts.cs`
- Create: `src/AFK4.Platform.Api/Platform/Identity/PlatformRoleService.cs`
- Create: `src/AFK4.Platform.Api/Endpoints/PlatformRoleEndpoints.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/PlatformRoleEndpointTests.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/PlatformRoleConcurrencyPostgresTests.cs`

**Interfaces:**
- Produces: `PlatformRoleDto(RoleName, DisplayName, Description, IsBuiltIn, GrantsAllPermissions, IReadOnlyList<string> Permissions, int AdminCount)`;
  `CreatePlatformRoleRequest(RoleName, DisplayName, Description, IReadOnlyList<string> Permissions)`;
  `UpdatePlatformRoleRequest(DisplayName, Description, IReadOnlyList<string> Permissions)`;
  маршруты `GET /api/platform/roles`, `POST /api/platform/roles`, `PUT /api/platform/roles/{roleName}`, `DELETE /api/platform/roles/{roleName}`, `GET /api/platform/permissions`.

- [ ] **Step 1: Написать падающие тесты**

`tests/AFK4.Platform.Api.Tests/Platform/PlatformRoleEndpointTests.cs`:

```csharp
[Fact] public async Task Get_RequiresManagePlatformAdmins() { }                       // поддержка → 403 + аудит Denied
[Fact] public async Task Get_ListsRolesWithPermissionsAndAdminCount() { }
[Fact] public async Task GetPermissions_ListsEveryPermissionFromCode() { }            // сверка с PlatformAdminPermissionNames.All
[Fact] public async Task Post_CreatesACustomRole() { }
[Fact] public async Task Post_RejectsADuplicateRoleName() { }                          // 409
[Fact] public async Task Post_RejectsAnUnknownPermission() { }                         // 400: право, которого нет в коде, ничего не значит
[Fact] public async Task Post_RefusesToGrantAPermissionTheActorLacks() { }             // 403: нельзя выдать больше, чем есть у тебя
[Fact] public async Task Put_ChangesPermissionsOfACustomRole() { }
[Fact] public async Task Put_RefusesToGrantAPermissionTheActorLacks() { }
[Fact] public async Task Put_RefusesToRemoveAdminsManageFromARoleTheActorHolds() { }   // 409: самоблокировка
[Fact] public async Task Put_RefusesToRemoveAdminsManageFromTheLastRoleThatGrantsIt() { } // 409
[Fact] public async Task Put_CanEditABuiltInRole() { }                                 // встроенную можно править
[Fact] public async Task Delete_RemovesACustomRoleThatNobodyHolds() { }
[Fact] public async Task Delete_RefusesABuiltInRole() { }                              // 409
[Fact] public async Task Delete_RefusesARoleThatSomebodyHolds() { }                    // 409: иначе человек остаётся без прав молча
[Fact] public async Task PermissionRemoval_TakesEffectWithoutRelogin() { }             // ключевой: тем же токеном ранее доступный маршрут отвечает 403
```

Тела написать настоящим кодом; авторизация — `PlatformAdminTestHelper.AuthorizeAsAsync`, аудит — как в `PlatformFeatureEndpointTests`.

`PermissionRemoval_TakesEffectWithoutRelogin` — то, ради чего задача существует: получить токен, убедиться, что маршрут доступен, снять право у роли, тем же токеном получить 403.

`tests/AFK4.Platform.Api.Tests/Platform/PlatformRoleConcurrencyPostgresTests.cs` — один тест с атрибутом настоящего Postgres: два одновременных запроса, каждый снимает флаг полного доступа со своей из двух последних таких ролей; ровно один должен победить, вторая роль обязана сохранить флаг. На InMemory этот тест не проверяет ничего и должен быть помечен соответствующим атрибутом, а не притворяться зелёным.

- [ ] **Step 2: Прогнать тесты и убедиться, что они падают**

```bash
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter FullyQualifiedName~PlatformRole
```

- [ ] **Step 3: Контракты**

`src/AFK4.Shared.Contracts/Platform/Auth/PlatformRoleContracts.cs` — четыре записи из блока Interfaces выше плюс `PlatformPermissionDto(string PermissionName)`.

- [ ] **Step 4: Сервис с предохранителями**

`PlatformRoleService` — вся работа в serializable-транзакции, по образцу `PlatformAdminDirectoryService.ExecuteInSerializableTransactionAsync`. Перечень отказов (машинными кодами, не прозой):

| Код | Когда |
|---|---|
| `role_name_taken` | роль с таким именем уже есть |
| `unknown_permission` | право отсутствует в `PlatformAdminPermissionNames.All` |
| `permission_not_held_by_actor` | действующий администратор сам не имеет выдаваемого права |
| `self_lockout` | снимается `platform.admins.manage` с роли, которую носит сам действующий администратор |
| `last_admins_manage_role` | снимается `platform.admins.manage` с последней роли, которая его даёт |
| `last_full_access_role` | снимается флаг полного доступа с последней роли, которая его несёт |
| `built_in_role` | попытка удалить встроенную роль |
| `role_in_use` | попытка удалить роль, которую кто-то носит |
| `conflict` | serialization failure — «повторите» |

`conflict` не должен подменяться конкретной причиной: serializable может отменить и «невиновную» сторону гонки, и объяснять её отказ чужим правилом — враньё (урок волны A).

Роль с флагом полного доступа при проверке `permission_not_held_by_actor` считается имеющей все права.

- [ ] **Step 5: Эндпоинты**

`PlatformRoleEndpoints` — пять маршрутов, все под `PlatformAdminPermissionNames.ManagePlatformAdmins` (кто управляет администраторами, тот управляет и ролями). Проверка права до обращения к данным; аудит на успех и на отказ по правам — форму брать из `PlatformFeatureEndpoints.cs`; действия добавить в `AuditActionNames`. Маппинг кодов отказа: `role_name_taken`/`self_lockout`/`last_*`/`built_in_role`/`role_in_use`/`conflict` → 409, `unknown_permission` → 400, `permission_not_held_by_actor` → 403, отсутствующая роль → 404.

Зарегистрировать в `Program.cs`.

- [ ] **Step 6: Прогнать тесты**

```bash
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj
```

Postgres-тест обязан пройти, а не пропуститься: задай перед прогоном все четыре переменные подключения и `AFK4_REQUIRE_POSTGRES_TESTS=1` (см. финальную проверку в конце плана).

- [ ] **Step 7: Коммит**

```bash
git add src/AFK4.Platform.Api/ src/AFK4.Shared.Contracts/ tests/
git commit -m "feat(platform): управление ролями платформы с предохранителями"
```

---

### Task 5: Панель — экран ролей

**Files:**
- Create: `src/AFK4.PlatformControl.Web/src/platform/settings/RolesSection.tsx`
- Create: `src/AFK4.PlatformControl.Web/src/platform/settings/RolesSection.test.tsx`
- Create: `src/AFK4.PlatformControl.Web/src/api/platformClients/roles.ts`
- Modify: `src/AFK4.PlatformControl.Web/src/api/types.ts`
- Modify: `src/AFK4.PlatformControl.Web/src/platform/settings/SettingsScreen.tsx`
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json`

**Interfaces:**
- Consumes: пять маршрутов из задачи 4.

- [ ] **Step 1: Написать падающие тесты**

```tsx
it('показывает роли, их права и сколько человек их носит', async () => { });
it('создаёт роль с выбранными правами', async () => { });
it('не даёт создать роль без имени или без единого права', async () => { });
it('объясняет отказ, когда права нет у самого администратора', async () => { });
it('объясняет отказ при попытке снять ключ от платформы у своей роли', async () => { });
it('не показывает удаление у встроенной роли', async () => { });
```

Тела написать настоящим кодом по образцу `src/AFK4.PlatformControl.Web/src/platform/settings/SettingsScreen.test.tsx`.

Отказы приходят машинными кодами (`permission_not_held_by_actor`, `self_lockout`, `last_admins_manage_role`, `last_full_access_role`, `built_in_role`, `role_in_use`, `role_name_taken`) — каждый обязан превращаться в свою фразу. Общий текст «не удалось сохранить» на все случаи — это потеря конкретики, которая уже на руках.

- [ ] **Step 2: Прогнать тесты и убедиться, что они падают**

```bash
cd /home/fedya/projects/afk4.net/src/AFK4.PlatformControl.Web && ~/.bun/bin/bun test src/platform/settings/RolesSection.test.tsx
```

- [ ] **Step 3: Строки**

Добавить в `locales/ru.json` (алфавитно, среди соседних `platform.settings.*`), плюс честные переводы в `en.json` и `tg.json`:

```json
"platform.settings.roles.title": "Роли",
"platform.settings.roles.description": "Роль — это набор прав. Состав меняется здесь, без выпуска новой версии.",
"platform.settings.roles.builtIn": "Встроенная",
"platform.settings.roles.fullAccess": "Полный доступ",
"platform.settings.roles.holders": "{count, plural, one {# администратор} few {# администратора} other {# администраторов}}",
"platform.settings.roles.create": "Новая роль",
"platform.settings.roles.name": "Короткое имя",
"platform.settings.roles.displayName": "Название",
"platform.settings.roles.permissions": "Права",
"platform.settings.roles.save": "Сохранить",
"platform.settings.roles.delete": "Удалить роль",
"platform.settings.roles.nameRequired": "Укажите короткое имя и название роли.",
"platform.settings.roles.permissionsRequired": "Выберите хотя бы одно право.",
"platform.settings.roles.error.roleNameTaken": "Роль с таким коротким именем уже есть.",
"platform.settings.roles.error.permissionNotHeld": "Нельзя выдать право, которого нет у вас самих.",
"platform.settings.roles.error.selfLockout": "Это ваша роль: снять с неё управление администраторами нельзя — вы потеряете доступ к платформе.",
"platform.settings.roles.error.lastAdminsManage": "Это последняя роль, которая даёт управление администраторами.",
"platform.settings.roles.error.lastFullAccess": "Это последняя роль с полным доступом.",
"platform.settings.roles.error.builtIn": "Встроенную роль удалить нельзя.",
"platform.settings.roles.error.roleInUse": "Роль носят администраторы — сначала переведите их на другую.",
"platform.settings.roles.error.conflict": "Кто-то менял роли одновременно с вами. Повторите."
```

Затем `cd /home/fedya/projects/afk4.net/packages/i18n && ~/.bun/bin/bun run gen`.

- [ ] **Step 4: Клиент и экран**

`roles.ts` — пять методов по образцу соседнего `admins.ts`. `RolesSection.tsx` — список ролей: название, короткое имя, метки «Встроенная»/«Полный доступ», число носителей, свёрнутый список прав; форма создания и правки с переключателями прав, сгруппированными по разделу (по префиксу права до второй точки). Удаление показывается только у не встроенной роли.

Разметку и компоненты брать из `SettingsScreen.tsx` и соседних секций, свой стиль не привносить. Встроить секцию в `SettingsScreen` рядом с администраторами; показывать её только при праве `admins.manage` (`platformAccess.ts` уже умеет эту возможность) — рычаг, доступный тому, кому он запрещён, хуже отсутствующего.

- [ ] **Step 5: Прогнать тесты и сборку**

```bash
cd /home/fedya/projects/afk4.net/src/AFK4.PlatformControl.Web && ~/.bun/bin/bun test && ~/.bun/bin/bun run build
cd /home/fedya/projects/afk4.net/packages/i18n && ~/.bun/bin/bun test
```

- [ ] **Step 6: Коммит**

```bash
git add src/AFK4.PlatformControl.Web/src/ locales/ packages/i18n/src/
git commit -m "feat(platform-control): экран ролей платформы"
```

---

## Финальная проверка перед завершением ветки

```bash
cd /home/fedya/projects/afk4.net
dotnet build AFK4.sln
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj
dotnet test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj
(cd src/AFK4.PlatformControl.Web && ~/.bun/bin/bun test && ~/.bun/bin/bun run build)
(cd packages/i18n && ~/.bun/bin/bun test)
```

Прогон бэкенда обязан идти с настоящим Postgres и **без пропусков**: задай `AFK4_PLATFORM_ADMIN_POSTGRES_TEST_CONNECTION_STRING`, `AFK4_RESERVATION_POSTGRES_TEST_CONNECTION_STRING`, `AFK4_POS_POSTGRES_TEST_CONNECTION_STRING`, `AFK4_COMMERCE_TEST_POSTGRES` (все — одна и та же строка подключения) и `AFK4_REQUIRE_POSTGRES_TESTS=1`. Пропущенный тест ничего не доказывает: в прошлом плане волны так пряталась настоящая регрессия сборки контейнера.

## Отклонения от спеки (§2) и почему

- **Введён флаг «полный доступ» у роли.** В спеке роль — просто набор прав. Но тогда каждое новое право, добавленное в код, после деплоя не принадлежало бы никому, и новый раздел был бы недоступен всем до ручной правки роли — тихая поломка, которую замечают в проде. Флаг остаётся данными: его видно и можно снять в панели.
- **Добавлен запрет на удаление роли, которую кто-то носит** (`role_in_use`). В спеке этого нет; без него человек молча остаётся без прав, и понять почему — невозможно.
- **Самопонижение определяется по множествам прав, а не по их количеству.** Сегодняшний `IsRoleDowngrade` считает права; при редактируемых ролях счёт не значит ничего — две роли по десять прав могут не пересекаться вовсе.
- **Управление ролями идёт под правом `platform.admins.manage`**, отдельного права не заводится: кто управляет администраторами, тот управляет и ролями, а лишнее право усложняет модель, ничего не давая.
- **Права из старых строк, исчезнувшие из кода, отсекаются при резолве.** В спеке не оговорено; без этого удалённое из кода право продолжало бы действовать из-за строки в базе.
