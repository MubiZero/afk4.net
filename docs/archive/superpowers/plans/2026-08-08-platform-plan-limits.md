# Лимиты тарифа — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Лимиты тарифа (филиалы, устройства на филиал, одновременные сеансы, сотрудники на
филиал) начинают реально ограничивать рост, а не лежать неиспользуемым `jsonb`, и в панель
платформы добавляется отсутствующая сегодня возможность создать второй филиал.

**Architecture:** Один сервис `IPlanLimitGuard` знает все четыре лимита: читает `LimitsJson`
организации, считает текущее использование и возвращает либо `null` (можно), либо
`PlanLimitExceededDto` с числами. Четыре точки роста вызывают его перед созданием сущности и при
отказе отдают 409 с этим DTO. Клиенты локализуют отказ по машинному коду `plan_limit_reached`,
подставляя пришедшие числа.

**Tech Stack:** .NET 10 (Platform.Api, EF Core, Postgres), xUnit + `PlatformApiFactory`,
React 19 + TypeScript (PlatformControl.Web, OrganizationAdmin.Web), `bun test`, `@afk4/i18n`.

**Ветка:** `feat/platform-plan-limits-wave-d`

## Global Constraints

- **Сервер прозу не рендерит.** Отказ несёт код (`plan_limit_reached`), имя лимита, предел, текущее
  значение и код тарифа. Фразу собирает клиент из `@afk4/i18n`; множественное число — ICU-плюралами.
- **«Стоп на рост, старое живёт».** Проверка спрашивает «станет ли больше, чем разрешено», а не «не
  превышено ли сейчас». Клуб, уже находящийся выше лимита, продолжает работать; добавить новое не
  даёт.
- **`null` в лимите = без ограничения.** Сегодняшнее поведение по умолчанию не меняется.
- **Правило живёт в одном месте.** Разбор `LimitsJson` и решение «можно / нельзя» существуют
  ровно в одном файле; второй экземпляр разошёлся бы с первым молча.
- **Разрешение проверяется на сервере.** Скрытая или заблокированная кнопка в интерфейсе — удобство,
  а не защита.
- **Каждое действие платформы пишется в аудит** — создание филиала в том числе.
- **Тесты на гонки — только на настоящем Postgres.** InMemory не знает уникальных индексов; делать
  вид, что знает, нельзя. В этом плане гонок на границе лимита нет по решению спеки — и тестов,
  которые притворялись бы, что «ровно N» гарантировано, тоже быть не должно.
- **Новые таблицы не заводятся.** План целиком построен на существующих `Organizations.LimitsJson`
  и `SubscriptionPlans`.
- **Строки интерфейса — только через `@afk4/i18n`.** Источник — `locales/{ru,en,tg}.json`, файлы
  `packages/i18n/src/messages.*.ts` генерируются командой `bun run gen` в `packages/i18n` и правятся
  только ей. Таджикский пишется по-таджикски, копия русского запрещена
  (`packages/i18n/src/messages.test.ts` это стережёт).

## Что уже есть в коде (контекст для исполнителя)

- `OrganizationEntity.LimitsJson` (`jsonb`) хранит `OrganizationLimitsDto(int? MaxBranches,
  int? MaxDevicesPerBranch, int? MaxConcurrentSessions, int? MaxStaffUsersPerBranch)`.
  `EfOrganizationSubscriptionService` переписывает его из тарифа при смене подписки.
  **Читать эти лимиты для принятия решений сегодня не умеет никто.**
- `EfPlatformOrganizationService` содержит приватные `SerializeLimits` / `DeserializeLimits` —
  единственное место, где `LimitsJson` разбирается. В задаче 1 они переезжают в общий файл.
- `SubscriptionPlanEntity` уже несёт те же четыре лимита; `BillingPlanSeedHostedService` заводит
  шесть тарифов (`starter` 1 филиал / 30 устройств / 40 сеансов / 10 сотрудников, `growth` 3/60/80/20,
  `scale` 10/120/200/50 и три годовых).
- **Создать второй филиал в продукте нельзя.** `BranchEntity` появляется ровно один раз — внутри
  `EfPlatformOrganizationService.CreateAsync`. Эндпоинта «добавить филиал» не существует; задача 5
  его добавляет.
- Сотрудник создаётся в двух местах: активация владельца
  (`EfPlatformOrganizationService`, первый сотрудник новой организации) и приём приглашения
  (`EfStaffInviteService.AcceptInviteAsync`). Отдельного «создать сотрудника с паролем» эндпоинта нет.
- Оператор (`src/AFK4.OrganizationAdmin.Web`) уже умеет превращать машинный код ошибки в
  локализованную фразу — `src/apiErrors.ts`, функция `projectOperatorError`, таблица
  `codeMessageKeys`. Значений (чисел) она пока не пробрасывает; задача 3 это добавляет.
- Тесты бэкенда поднимаются через `PlatformApiFactory` + `factory.Services.CreateAsyncScope()`;
  образец — `tests/AFK4.Platform.Api.Tests/Platform/Analytics/BranchSnapshotRunnerTests.cs`.

## Структура файлов

**Создаются:**

| Файл | Ответственность |
|---|---|
| `src/AFK4.Shared.Contracts/Platform/Organizations/PlanLimitNames.cs` | Машинные имена лимитов и код отказа |
| `src/AFK4.Shared.Contracts/Platform/Organizations/PlanLimitExceededDto.cs` | Тело отказа: код, имя лимита, предел, текущее значение, тариф |
| `src/AFK4.Shared.Contracts/Platform/Organizations/CreateBranchRequest.cs` | Запрос на создание филиала |
| `src/AFK4.Platform.Api/Platform/Entitlements/OrganizationLimitsJson.cs` | Единственный разбор/запись `LimitsJson` |
| `src/AFK4.Platform.Api/Platform/Entitlements/IPlanLimitGuard.cs` | Контракт четырёх проверок |
| `src/AFK4.Platform.Api/Platform/Entitlements/EfPlanLimitGuard.cs` | Счётчики использования и решение |
| `src/AFK4.Platform.Api/Endpoints/PlatformBranchEndpoints.cs` | `POST …/organizations/{id}/branches` |
| `src/AFK4.PlatformControl.Web/src/platform/organizations/NewBranchDialog.tsx` | Форма добавления филиала |

**Изменяются:** `EfPlatformOrganizationService` (переход на общий разбор лимитов, метод
`CreateBranchAsync`), `EfDeviceEnrollmentService`, `EfSessionStartWorkflow`,
`ISessionCommandService`, `SessionEndpoints`, `EfStaffInviteService`, `StaffOnboardingEndpoints`,
`Program.cs` (регистрация), `apiErrors.ts` + `locales/*.json` (Оператор),
`OrganizationClubsTab.tsx` + `api/platformClients/organizations.ts` + `api/types.ts` (панель).

---

### Task 1: Общий разбор лимитов и `IPlanLimitGuard`

**Files:**
- Create: `src/AFK4.Shared.Contracts/Platform/Organizations/PlanLimitNames.cs`
- Create: `src/AFK4.Shared.Contracts/Platform/Organizations/PlanLimitExceededDto.cs`
- Create: `src/AFK4.Platform.Api/Platform/Entitlements/OrganizationLimitsJson.cs`
- Create: `src/AFK4.Platform.Api/Platform/Entitlements/IPlanLimitGuard.cs`
- Create: `src/AFK4.Platform.Api/Platform/Entitlements/EfPlanLimitGuard.cs`
- Modify: `src/AFK4.Platform.Api/Platform/Tenancy/EfPlatformOrganizationService.cs` (удалить приватные `SerializeLimits`/`DeserializeLimits`, звать общий файл)
- Modify: `src/AFK4.Platform.Api/Program.cs` (регистрация `IPlanLimitGuard`)
- Test: `tests/AFK4.Platform.Api.Tests/Platform/Entitlements/PlanLimitGuardTests.cs`

**Interfaces:**
- Produces: `PlanLimitNames.ReachedCode` = `"plan_limit_reached"`, `PlanLimitNames.Branches`,
  `.DevicesPerBranch`, `.ConcurrentSessions`, `.StaffUsersPerBranch`;
  `PlanLimitExceededDto(string Code, string LimitName, int Limit, int Current, string PlanCode)`;
  `IPlanLimitGuard` с четырьмя методами, каждый возвращает `Task<PlanLimitExceededDto?>`
  (`null` = можно): `CheckBranchAsync(Guid organizationId, CancellationToken)`,
  `CheckDeviceAsync(Guid organizationId, Guid branchId, CancellationToken)`,
  `CheckConcurrentSessionAsync(Guid organizationId, CancellationToken)`,
  `CheckStaffUserAsync(Guid organizationId, Guid branchId, CancellationToken)`;
  `OrganizationLimitsJson.Serialize(OrganizationLimitsDto?)`,
  `OrganizationLimitsJson.Deserialize(string?)`, `OrganizationLimitsJson.None`.

- [ ] **Step 1: Написать падающий тест**

Создать `tests/AFK4.Platform.Api.Tests/Platform/Entitlements/PlanLimitGuardTests.cs`:

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Entitlements;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Platform.Organizations;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform.Entitlements;

public sealed class PlanLimitGuardTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 8, 10, 0, 0, TimeSpan.Zero);

    private static async Task<(Guid OrganizationId, Guid BranchId)> SeedAsync(
        PlatformDbContext db,
        OrganizationLimitsDto limits,
        string planCode = "growth")
    {
        var organizationId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = organizationId,
            Slug = "club-" + organizationId.ToString("N")[..8],
            Name = "Клуб",
            Status = OrganizationStatusNames.Active,
            PlanCode = planCode,
            LimitsJson = OrganizationLimitsJson.Serialize(limits),
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        });
        db.Branches.Add(new BranchEntity
        {
            BranchId = branchId,
            OrganizationId = organizationId,
            Slug = "branch-" + branchId.ToString("N")[..8],
            Name = "Филиал",
            CreatedAtUtc = Now
        });
        await db.SaveChangesAsync();
        return (organizationId, branchId);
    }

    private static void SeedDevice(PlatformDbContext db, Guid organizationId, Guid branchId, string state)
    {
        db.Devices.Add(new DeviceEntity
        {
            DeviceId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            MachineName = "PC",
            DisplayName = "PC",
            Role = DeviceRoleNames.GamingPc,
            EnrollmentState = state,
            EnrolledAtUtc = Now
        });
    }

    private static void SeedSession(PlatformDbContext db, Guid organizationId, Guid branchId, string state)
    {
        db.Sessions.Add(new SessionEntity
        {
            SessionId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            SeatId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            CreatedByStaffUserId = Guid.NewGuid(),
            PlayerKind = "guest",
            BillingMode = BillingModeNames.PostpaidDebt,
            State = state,
            RequestedAtUtc = Now,
            StartedAtUtc = Now,
            UpdatedAtUtc = Now,
            Version = 1
        });
    }

    [Fact]
    public async Task NoLimit_MeansUnlimited()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var (organizationId, branchId) = await SeedAsync(db, new OrganizationLimitsDto(null, null, null, null));
        SeedDevice(db, organizationId, branchId, DeviceEnrollmentStateNames.Approved);
        await db.SaveChangesAsync();
        var guard = scope.ServiceProvider.GetRequiredService<IPlanLimitGuard>();

        Assert.Null(await guard.CheckBranchAsync(organizationId, CancellationToken.None));
        Assert.Null(await guard.CheckDeviceAsync(organizationId, branchId, CancellationToken.None));
        Assert.Null(await guard.CheckConcurrentSessionAsync(organizationId, CancellationToken.None));
        Assert.Null(await guard.CheckStaffUserAsync(organizationId, branchId, CancellationToken.None));
    }

    [Fact]
    public async Task Branch_RefusesWhenLimitReached_AndCarriesNumbers()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var (organizationId, _) = await SeedAsync(db, new OrganizationLimitsDto(1, null, null, null), "starter");
        var guard = scope.ServiceProvider.GetRequiredService<IPlanLimitGuard>();

        var verdict = await guard.CheckBranchAsync(organizationId, CancellationToken.None);

        Assert.NotNull(verdict);
        Assert.Equal(PlanLimitNames.ReachedCode, verdict!.Code);
        Assert.Equal(PlanLimitNames.Branches, verdict.LimitName);
        Assert.Equal(1, verdict.Limit);
        Assert.Equal(1, verdict.Current);
        Assert.Equal("starter", verdict.PlanCode);
    }

    [Fact]
    public async Task Branch_AllowsWhileBelowLimit()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var (organizationId, _) = await SeedAsync(db, new OrganizationLimitsDto(3, null, null, null));
        var guard = scope.ServiceProvider.GetRequiredService<IPlanLimitGuard>();

        Assert.Null(await guard.CheckBranchAsync(organizationId, CancellationToken.None));
    }

    [Fact]
    public async Task Device_CountsOnlyLiveEnrollments()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var (organizationId, branchId) = await SeedAsync(db, new OrganizationLimitsDto(null, 2, null, null));
        SeedDevice(db, organizationId, branchId, DeviceEnrollmentStateNames.Approved);
        SeedDevice(db, organizationId, branchId, DeviceEnrollmentStateNames.Removed);
        SeedDevice(db, organizationId, branchId, DeviceEnrollmentStateNames.Rejected);
        await db.SaveChangesAsync();
        var guard = scope.ServiceProvider.GetRequiredService<IPlanLimitGuard>();

        // Живое устройство одно из двух разрешённых: снятые и отклонённые места не занимают.
        Assert.Null(await guard.CheckDeviceAsync(organizationId, branchId, CancellationToken.None));
    }

    [Fact]
    public async Task Device_LimitIsPerBranch()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var (organizationId, branchId) = await SeedAsync(db, new OrganizationLimitsDto(null, 1, null, null));
        var otherBranchId = Guid.NewGuid();
        db.Branches.Add(new BranchEntity
        {
            BranchId = otherBranchId,
            OrganizationId = organizationId,
            Slug = "branch-" + otherBranchId.ToString("N")[..8],
            Name = "Второй",
            CreatedAtUtc = Now
        });
        SeedDevice(db, organizationId, branchId, DeviceEnrollmentStateNames.Approved);
        await db.SaveChangesAsync();
        var guard = scope.ServiceProvider.GetRequiredService<IPlanLimitGuard>();

        Assert.NotNull(await guard.CheckDeviceAsync(organizationId, branchId, CancellationToken.None));
        Assert.Null(await guard.CheckDeviceAsync(organizationId, otherBranchId, CancellationToken.None));
    }

    [Fact]
    public async Task Session_CountsLiveStatesAcrossOrganization_AndIgnoresEnded()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var (organizationId, branchId) = await SeedAsync(db, new OrganizationLimitsDto(null, null, 2, null));
        SeedSession(db, organizationId, branchId, SessionStateNames.Active);
        SeedSession(db, organizationId, branchId, SessionStateNames.Ended);
        SeedSession(db, organizationId, branchId, SessionStateNames.Reconciled);
        await db.SaveChangesAsync();
        var guard = scope.ServiceProvider.GetRequiredService<IPlanLimitGuard>();

        Assert.Null(await guard.CheckConcurrentSessionAsync(organizationId, CancellationToken.None));

        SeedSession(db, organizationId, branchId, SessionStateNames.Paused);
        await db.SaveChangesAsync();

        var verdict = await guard.CheckConcurrentSessionAsync(organizationId, CancellationToken.None);
        Assert.NotNull(verdict);
        Assert.Equal(PlanLimitNames.ConcurrentSessions, verdict!.LimitName);
        Assert.Equal(2, verdict.Current);
    }

    [Fact]
    public async Task Staff_CountsActiveUsersAndPendingInvitesOfBranch()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var (organizationId, branchId) = await SeedAsync(db, new OrganizationLimitsDto(null, null, null, 2));

        var activeUserId = Guid.NewGuid();
        var disabledUserId = Guid.NewGuid();
        foreach (var (staffUserId, isActive) in new[] { (activeUserId, true), (disabledUserId, false) })
        {
            db.StaffUsers.Add(new StaffUserEntity
            {
                StaffUserId = staffUserId,
                OrganizationId = organizationId,
                UserName = staffUserId.ToString("N")[..8],
                NormalizedUserName = staffUserId.ToString("N")[..8].ToUpperInvariant(),
                DisplayName = "Сотрудник",
                IsActive = isActive,
                CreatedAtUtc = Now
            });
            db.StaffRoleAssignments.Add(new StaffRoleAssignmentEntity
            {
                StaffRoleAssignmentId = Guid.NewGuid(),
                StaffUserId = staffUserId,
                OrganizationId = organizationId,
                BranchId = branchId,
                RoleName = OrganizationRoleNames.Cashier
            });
        }
        await db.SaveChangesAsync();
        var guard = scope.ServiceProvider.GetRequiredService<IPlanLimitGuard>();

        // Отключённый сотрудник места не занимает: один активный из двух разрешённых.
        Assert.Null(await guard.CheckStaffUserAsync(organizationId, branchId, CancellationToken.None));

        db.StaffInvites.Add(new StaffInviteEntity
        {
            StaffInviteId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            UserName = "newbie",
            NormalizedUserName = "NEWBIE",
            DisplayName = "Новичок",
            Email = "newbie@example.test",
            RoleNamesCsv = OrganizationRoleNames.Cashier,
            TokenHash = [1, 2, 3],
            CreatedAtUtc = Now,
            ExpiresAtUtc = Now.AddDays(7)
        });
        await db.SaveChangesAsync();

        // Непринятое приглашение занимает место заранее — иначе три приглашения на филиал
        // с лимитом два перепрыгнут границу все разом в момент приёма.
        var verdict = await guard.CheckStaffUserAsync(organizationId, branchId, CancellationToken.None);
        Assert.NotNull(verdict);
        Assert.Equal(PlanLimitNames.StaffUsersPerBranch, verdict!.LimitName);
        Assert.Equal(2, verdict.Current);
    }

    [Fact]
    public async Task UnknownOrganization_IsNotRefused()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var guard = scope.ServiceProvider.GetRequiredService<IPlanLimitGuard>();

        // Несуществующая организация — не повод отказывать по лимиту: за «нет такой» отвечает
        // вызывающий код своей ошибкой, иначе пользователь получит ложное объяснение отказа.
        Assert.Null(await guard.CheckBranchAsync(Guid.NewGuid(), CancellationToken.None));
    }
}
```

- [ ] **Step 2: Прогнать тест и убедиться, что он падает**

```bash
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter FullyQualifiedName~PlanLimitGuardTests
```

Ожидание: ошибка компиляции — `IPlanLimitGuard`, `PlanLimitNames`, `OrganizationLimitsJson` не
существуют.

- [ ] **Step 3: Контракты**

`src/AFK4.Shared.Contracts/Platform/Organizations/PlanLimitNames.cs`:

```csharp
namespace AFK4.Shared.Contracts.Platform.Organizations;

/// <summary>
/// Машинные имена лимитов тарифа и код отказа. Фразу для человека собирает клиент —
/// сервер отдаёт только код и числа.
/// </summary>
public static class PlanLimitNames
{
    public const string ReachedCode = "plan_limit_reached";

    public const string Branches = "branches";

    public const string DevicesPerBranch = "devices_per_branch";

    public const string ConcurrentSessions = "concurrent_sessions";

    public const string StaffUsersPerBranch = "staff_users_per_branch";
}
```

`src/AFK4.Shared.Contracts/Platform/Organizations/PlanLimitExceededDto.cs`:

```csharp
namespace AFK4.Shared.Contracts.Platform.Organizations;

/// <summary>
/// Тело отказа по лимиту тарифа. <paramref name="Current"/> и <paramref name="Limit"/> едут
/// клиенту, чтобы отказ читался как «филиалов 2 из 2», а не как «нельзя».
/// </summary>
public sealed record PlanLimitExceededDto(
    string Code,
    string LimitName,
    int Limit,
    int Current,
    string PlanCode);
```

- [ ] **Step 4: Общий разбор `LimitsJson`**

`src/AFK4.Platform.Api/Platform/Entitlements/OrganizationLimitsJson.cs`:

```csharp
using System.Text.Json;
using AFK4.Shared.Contracts.Platform.Organizations;

namespace AFK4.Platform.Api.Platform.Entitlements;

/// <summary>
/// Единственное место, где лимиты организации превращаются из jsonb в объект и обратно.
/// Второй экземпляр этого правила разошёлся бы с первым молча и ни один тест бы этого не заметил.
/// </summary>
public static class OrganizationLimitsJson
{
    public static readonly OrganizationLimitsDto None = new(null, null, null, null);

    public static string Serialize(OrganizationLimitsDto? limits) =>
        JsonSerializer.Serialize(limits ?? None);

    public static OrganizationLimitsDto Deserialize(string? limitsJson)
    {
        if (string.IsNullOrWhiteSpace(limitsJson) || limitsJson == "{}")
        {
            return None;
        }

        try
        {
            return JsonSerializer.Deserialize<OrganizationLimitsDto>(limitsJson) ?? None;
        }
        catch (JsonException)
        {
            // Испорченный jsonb не должен ронять запрос: неизвестные лимиты = без ограничений,
            // потому что отказать по неизвестной причине хуже, чем пропустить.
            return None;
        }
    }
}
```

- [ ] **Step 5: Убрать вторую копию разбора лимитов**

В `src/AFK4.Platform.Api/Platform/Tenancy/EfPlatformOrganizationService.cs` удалить приватные
методы `SerializeLimits` и `DeserializeLimits` целиком и заменить все их вызовы на
`OrganizationLimitsJson.Serialize(...)` / `OrganizationLimitsJson.Deserialize(...)`
(вызовы находятся на строках создания организации, обновления лимитов и построения
`OrganizationDetailDto`). Добавить `using AFK4.Platform.Api.Platform.Entitlements;`.

- [ ] **Step 6: Реализовать `IPlanLimitGuard`**

`src/AFK4.Platform.Api/Platform/Entitlements/IPlanLimitGuard.cs`:

```csharp
using AFK4.Shared.Contracts.Platform.Organizations;

namespace AFK4.Platform.Api.Platform.Entitlements;

/// <summary>
/// Проверки лимитов тарифа в точках роста. Возвращают <c>null</c>, если добавлять можно,
/// и <see cref="PlanLimitExceededDto"/> с числами, если нельзя.
/// </summary>
public interface IPlanLimitGuard
{
    Task<PlanLimitExceededDto?> CheckBranchAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<PlanLimitExceededDto?> CheckDeviceAsync(Guid organizationId, Guid branchId, CancellationToken cancellationToken);

    Task<PlanLimitExceededDto?> CheckConcurrentSessionAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<PlanLimitExceededDto?> CheckStaffUserAsync(Guid organizationId, Guid branchId, CancellationToken cancellationToken);
}
```

`src/AFK4.Platform.Api/Platform/Entitlements/EfPlanLimitGuard.cs`:

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Platform.Organizations;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Entitlements;

public sealed class EfPlanLimitGuard(PlatformDbContext dbContext) : IPlanLimitGuard
{
    // Снятое и отклонённое устройство места на филиале не занимает; ожидающее одобрения — занимает,
    // иначе очередь из ожидающих перепрыгнет лимит в момент одобрения.
    private static readonly string[] LiveDeviceStates =
        [DeviceEnrollmentStateNames.Approved, DeviceEnrollmentStateNames.Pending];

    // «Одновременный» сеанс — любой, который ещё не закрыт.
    private static readonly string[] LiveSessionStates =
        [SessionStateNames.Requested, SessionStateNames.Active, SessionStateNames.Paused, SessionStateNames.Ending];

    public async Task<PlanLimitExceededDto?> CheckBranchAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var plan = await LoadPlanAsync(organizationId, cancellationToken);
        if (plan?.Limits.MaxBranches is not { } limit)
        {
            return null;
        }

        var current = await dbContext.Branches
            .CountAsync(branch => branch.OrganizationId == organizationId, cancellationToken);

        return Verdict(PlanLimitNames.Branches, limit, current, plan.PlanCode);
    }

    public async Task<PlanLimitExceededDto?> CheckDeviceAsync(Guid organizationId, Guid branchId, CancellationToken cancellationToken)
    {
        var plan = await LoadPlanAsync(organizationId, cancellationToken);
        if (plan?.Limits.MaxDevicesPerBranch is not { } limit)
        {
            return null;
        }

        var current = await dbContext.Devices
            .CountAsync(
                device => device.OrganizationId == organizationId
                    && device.BranchId == branchId
                    && LiveDeviceStates.Contains(device.EnrollmentState),
                cancellationToken);

        return Verdict(PlanLimitNames.DevicesPerBranch, limit, current, plan.PlanCode);
    }

    public async Task<PlanLimitExceededDto?> CheckConcurrentSessionAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var plan = await LoadPlanAsync(organizationId, cancellationToken);
        if (plan?.Limits.MaxConcurrentSessions is not { } limit)
        {
            return null;
        }

        var current = await dbContext.Sessions
            .CountAsync(
                session => session.OrganizationId == organizationId
                    && LiveSessionStates.Contains(session.State),
                cancellationToken);

        return Verdict(PlanLimitNames.ConcurrentSessions, limit, current, plan.PlanCode);
    }

    public async Task<PlanLimitExceededDto?> CheckStaffUserAsync(Guid organizationId, Guid branchId, CancellationToken cancellationToken)
    {
        var plan = await LoadPlanAsync(organizationId, cancellationToken);
        if (plan?.Limits.MaxStaffUsersPerBranch is not { } limit)
        {
            return null;
        }

        var activeUsers = await dbContext.StaffRoleAssignments
            .Where(assignment => assignment.OrganizationId == organizationId && assignment.BranchId == branchId)
            .Where(assignment => dbContext.StaffUsers
                .Any(user => user.StaffUserId == assignment.StaffUserId && user.IsActive))
            .Select(assignment => assignment.StaffUserId)
            .Distinct()
            .CountAsync(cancellationToken);

        // Непринятое живое приглашение занимает место заранее: иначе три приглашения на филиал
        // с лимитом два пройдут проверку по очереди и перепрыгнут границу в момент приёма.
        var pendingInvites = await dbContext.StaffInvites
            .CountAsync(
                invite => invite.OrganizationId == organizationId
                    && invite.BranchId == branchId
                    && invite.AcceptedAtUtc == null,
                cancellationToken);

        return Verdict(PlanLimitNames.StaffUsersPerBranch, limit, activeUsers + pendingInvites, plan.PlanCode);
    }

    private async Task<PlanSnapshot?> LoadPlanAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var row = await dbContext.Organizations
            .AsNoTracking()
            .Where(organization => organization.OrganizationId == organizationId)
            .Select(organization => new { organization.LimitsJson, organization.PlanCode })
            .SingleOrDefaultAsync(cancellationToken);

        // Организации нет — отказывать по лимиту нельзя: за «нет такой» отвечает вызывающий код
        // своей ошибкой, иначе пользователь получит ложное объяснение отказа.
        return row is null ? null : new PlanSnapshot(OrganizationLimitsJson.Deserialize(row.LimitsJson), row.PlanCode);
    }

    // «Стоп на рост»: спрашиваем, станет ли больше разрешённого, а не превышено ли сейчас.
    // Клуб, уже находящийся выше лимита, продолжает работать — проверка зовётся только перед
    // добавлением нового.
    private static PlanLimitExceededDto? Verdict(string limitName, int limit, int current, string planCode) =>
        current >= limit
            ? new PlanLimitExceededDto(PlanLimitNames.ReachedCode, limitName, limit, current, planCode)
            : null;

    private sealed record PlanSnapshot(OrganizationLimitsDto Limits, string PlanCode);
}
```

- [ ] **Step 7: Зарегистрировать сервис**

В `src/AFK4.Platform.Api/Program.cs` рядом с `builder.Services.AddScoped<IOrganizationStatusGuard, EfOrganizationStatusGuard>();`:

```csharp
builder.Services.AddScoped<IPlanLimitGuard, EfPlanLimitGuard>();
```

Добавить `using AFK4.Platform.Api.Platform.Entitlements;`, если его нет.

- [ ] **Step 8: Прогнать тесты**

```bash
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter FullyQualifiedName~PlanLimitGuardTests
```

Ожидание: 8 passed. Затем весь проект, чтобы перенос разбора лимитов ничего не сломал:

```bash
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj
```

- [ ] **Step 9: Коммит**

```bash
git add src/AFK4.Shared.Contracts/Platform/Organizations/PlanLimitNames.cs \
        src/AFK4.Shared.Contracts/Platform/Organizations/PlanLimitExceededDto.cs \
        src/AFK4.Platform.Api/Platform/Entitlements/ \
        src/AFK4.Platform.Api/Platform/Tenancy/EfPlatformOrganizationService.cs \
        src/AFK4.Platform.Api/Program.cs \
        tests/AFK4.Platform.Api.Tests/Platform/Entitlements/PlanLimitGuardTests.cs
git commit -m "feat(platform): единая проверка лимитов тарифа"
```

---

### Task 2: Лимит устройств при привязке

**Files:**
- Modify: `src/AFK4.Platform.Api/Devices/EfDeviceEnrollmentService.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/Entitlements/DeviceEnrollmentPlanLimitTests.cs`

**Interfaces:**
- Consumes: `IPlanLimitGuard.CheckDeviceAsync`, `PlanLimitNames`, `PlanLimitExceededDto`.
- Produces: `DeviceEnrollmentResult.PlanLimit` — свойство `PlanLimitExceededDto?` на существующем
  результате привязки, заполняется только при отказе по лимиту.

- [ ] **Step 1: Написать падающий тест**

Создать `tests/AFK4.Platform.Api.Tests/Platform/Entitlements/DeviceEnrollmentPlanLimitTests.cs`.
Тест сидит организацию с `MaxDevicesPerBranch = 1`, одно живое устройство и валидный код привязки,
затем зовёт `IDeviceEnrollmentService.EnrollAsync` и ожидает отказ с заполненным `PlanLimit`:

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.Platform.Entitlements;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform.Entitlements;

public sealed class DeviceEnrollmentPlanLimitTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 8, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Enroll_RefusesWithNumbers_WhenBranchIsAtDeviceLimit()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var (organizationId, branchId) = await SeedOrganizationAsync(db, maxDevicesPerBranch: 1);
        SeedDevice(db, organizationId, branchId);
        var code = await SeedEnrollmentCodeAsync(db, organizationId, branchId);
        var service = scope.ServiceProvider.GetRequiredService<IDeviceEnrollmentService>();

        var result = await service.EnrollAsync(
            new DeviceEnrollmentRequestContext(organizationId, branchId, code, "PC-2", "1.0.0", "1.0.0"),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.PlanLimit);
        Assert.Equal(PlanLimitNames.DevicesPerBranch, result.PlanLimit!.LimitName);
        Assert.Equal(1, result.PlanLimit.Limit);
        Assert.Equal(1, result.PlanLimit.Current);
        Assert.Equal(1, await db.Devices.CountAsync());
    }

    [Fact]
    public async Task Enroll_ConsumesNothing_WhenRefusedByLimit()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var (organizationId, branchId) = await SeedOrganizationAsync(db, maxDevicesPerBranch: 1);
        SeedDevice(db, organizationId, branchId);
        var code = await SeedEnrollmentCodeAsync(db, organizationId, branchId);
        var service = scope.ServiceProvider.GetRequiredService<IDeviceEnrollmentService>();

        await service.EnrollAsync(
            new DeviceEnrollmentRequestContext(organizationId, branchId, code, "PC-2", "1.0.0", "1.0.0"),
            CancellationToken.None);

        // Отказ по лимиту не должен сжигать одноразовый код: клуб поднимет тариф и повторит
        // привязку тем же кодом, а не пойдёт выпрашивать новый.
        var stored = await db.DeviceEnrollmentCodes.SingleAsync();
        Assert.Null(stored.ConsumedAtUtc);
    }

    [Fact]
    public async Task Enroll_Succeeds_WhenBelowLimit()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var (organizationId, branchId) = await SeedOrganizationAsync(db, maxDevicesPerBranch: 2);
        SeedDevice(db, organizationId, branchId);
        var code = await SeedEnrollmentCodeAsync(db, organizationId, branchId);
        var service = scope.ServiceProvider.GetRequiredService<IDeviceEnrollmentService>();

        var result = await service.EnrollAsync(
            new DeviceEnrollmentRequestContext(organizationId, branchId, code, "PC-2", "1.0.0", "1.0.0"),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Null(result.PlanLimit);
    }
}
```

**Прежде чем писать этот файл**, прочитать `src/AFK4.Platform.Api/Devices/IDeviceEnrollmentService.cs`
и привести имена типа запроса (`DeviceEnrollmentRequestContext` выше — предположение по вызову
внутри сервиса), сигнатуру `EnrollAsync` и вспомогательные `SeedOrganizationAsync` /
`SeedDevice` / `SeedEnrollmentCodeAsync` в соответствие с реальным кодом; `SeedOrganizationAsync`
повторяет сидинг из `PlanLimitGuardTests` с нужным лимитом, `SeedEnrollmentCodeAsync` создаёт
`DeviceEnrollmentCodeEntity` с непросроченным `ExpiresAtUtc` и `ConsumedAtUtc = null`, возвращая
плейнтекст кода в том виде, который принимает `DeviceCredentialSecrets.NormalizeEnrollmentCode`.

- [ ] **Step 2: Прогнать тест и убедиться, что он падает**

```bash
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter FullyQualifiedName~DeviceEnrollmentPlanLimitTests
```

Ожидание: ошибка компиляции — у `DeviceEnrollmentResult` нет `PlanLimit`.

- [ ] **Step 3: Добавить `PlanLimit` в результат привязки**

В файле, где объявлен `DeviceEnrollmentResult`, добавить необязательное свойство
`PlanLimitExceededDto? PlanLimit` (значение по умолчанию `null`) и фабрику:

```csharp
public static DeviceEnrollmentResult PlanLimitReached(PlanLimitExceededDto planLimit) =>
    // Текст здесь — для лога и для старых клиентов; человеку фразу собирает интерфейс из PlanLimit.
    new(false, "Plan device limit for this branch has been reached.", null, planLimit);
```

Точную форму (record с позиционными параметрами или класс) взять из существующего объявления и не
менять — добавляется только одно необязательное поле в конец.

- [ ] **Step 4: Вызвать проверку в `EfDeviceEnrollmentService`**

Добавить `IPlanLimitGuard planLimitGuard` в первичный конструктор сервиса. Проверку вставить
**после** всех проверок кода привязки (код существует, не использован, не истёк, совпадает с
филиалом) и **до** `dbContext.Devices.Add(...)`:

```csharp
var planLimit = await planLimitGuard.CheckDeviceAsync(
    request.OrganizationId,
    request.BranchId,
    cancellationToken);
if (planLimit is not null)
{
    return DeviceEnrollmentResult.PlanLimitReached(planLimit);
}
```

Порядок важен: отказ по лимиту не должен помечать одноразовый код использованным — код помечается
`ConsumedAtUtc` ниже по течению, и ранний выход обязан произойти до этого.

- [ ] **Step 5: Прогнать тесты**

```bash
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter FullyQualifiedName~DeviceEnrollment
```

Ожидание: новые 3 passed, существующие тесты привязки — тоже passed.

- [ ] **Step 6: Коммит**

```bash
git add src/AFK4.Platform.Api/Devices/ tests/AFK4.Platform.Api.Tests/Platform/Entitlements/DeviceEnrollmentPlanLimitTests.cs
git commit -m "feat(platform): лимит устройств на филиал при привязке"
```

---

### Task 3: Лимит одновременных сеансов и понятный отказ в Операторе

**Files:**
- Modify: `src/AFK4.Platform.Api/Sessions/ISessionCommandService.cs`
- Modify: `src/AFK4.Platform.Api/Sessions/EfSessionStartWorkflow.cs`
- Modify: `src/AFK4.Platform.Api/Endpoints/SessionEndpoints.cs`
- Modify: `src/AFK4.OrganizationAdmin.Web/src/apiErrors.ts`
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/Entitlements/SessionStartPlanLimitTests.cs`
- Test: `src/AFK4.OrganizationAdmin.Web/src/apiErrors.test.ts`

**Interfaces:**
- Consumes: `IPlanLimitGuard.CheckConcurrentSessionAsync`, `PlanLimitNames.ReachedCode`.
- Produces: `SessionCommandServiceResult.PlanLimit` (`PlanLimitExceededDto?`, по умолчанию `null`) и
  фабрика `SessionCommandServiceResult.PlanLimitReached(PlanLimitExceededDto)`; ключ сообщения
  `op.error.code.planLimitReached`.

- [ ] **Step 1: Написать падающий тест бэкенда**

`tests/AFK4.Platform.Api.Tests/Platform/Entitlements/SessionStartPlanLimitTests.cs` — проверяет, что
при достигнутом лимите старт сеанса отказывает конфликтом с кодом и числами, а тело ответа
эндпоинта их несёт:

```csharp
[Fact]
public async Task Start_RefusesWithNumbers_WhenOrganizationIsAtSessionLimit()
{
    // Сидим организацию с MaxConcurrentSessions = 1, один активный сеанс,
    // место с одобренным устройством и свободным вторым местом.
    // Ожидание: result.Conflict == true, result.Code == PlanLimitNames.ReachedCode,
    // result.PlanLimit!.Limit == 1, result.PlanLimit.Current == 1,
    // и в базе по-прежнему ровно один сеанс.
}

[Fact]
public async Task Start_Succeeds_WhenBelowSessionLimit()
{
    // Тот же сидинг, но MaxConcurrentSessions = 2. Ожидание: result.Succeeded, PlanLimit == null,
    // в базе два сеанса.
}
```

Сидинг сеанса, места и назначения устройства взять из ближайшего существующего теста старта
сеанса (`tests/AFK4.Platform.Api.Tests/Sessions/`) — он уже умеет собирать валидную сцену
«место + одобренное устройство + тариф»; повторно изобретать её не нужно. Тела `[Fact]` заполнить
настоящим кодом по этому образцу, комментарии-описания выше заменить на реальные утверждения.

- [ ] **Step 2: Прогнать тест и убедиться, что он падает**

```bash
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter FullyQualifiedName~SessionStartPlanLimitTests
```

Ожидание: ошибка компиляции — у `SessionCommandServiceResult` нет `PlanLimit`.

- [ ] **Step 3: Расширить результат команды сеанса**

В `src/AFK4.Platform.Api/Sessions/ISessionCommandService.cs` добавить последним параметром записи:

```csharp
    int? CurrentVersion = null,
    // Заполняется только при отказе по лимиту тарифа: клиент собирает из этих чисел фразу
    // «сеансов 40 из 40», а не показывает голое «нельзя».
    PlanLimitExceededDto? PlanLimit = null)
```

и фабрику рядом с остальными:

```csharp
    public static SessionCommandServiceResult PlanLimitReached(PlanLimitExceededDto planLimit) =>
        new(false, true, false, "Plan concurrent-session limit has been reached.", null,
            PlanLimitNames.ReachedCode, null, planLimit);
```

Добавить `using AFK4.Shared.Contracts.Platform.Organizations;`.

- [ ] **Step 4: Вызвать проверку в `EfSessionStartWorkflow`**

Добавить `IPlanLimitGuard planLimitGuard` в первичный конструктор. Проверку вставить **после**
`HasBlockingSessionAsync` и **до** `sessionBillingService.ValidateStartAsync` — деньги игрока не
должны трогаться, если сеанс всё равно не стартует:

```csharp
var planLimit = await planLimitGuard.CheckConcurrentSessionAsync(
    request.OrganizationId,
    cancellationToken);
if (planLimit is not null)
{
    return new SessionStartStage(
        SessionCommandServiceResult.PlanLimitReached(planLimit),
        DeviceId: null,
        Command: null);
}
```

Проверка стоит в `EfSessionStartWorkflow`, а не в эндпоинте, потому что через workflow идут **оба**
пути старта — команда Оператора (`EfSessionCommandService`) и старт из брони
(`EfReservationSessionCoordinator`). Проверка в эндпоинте оставила бы второй путь без лимита.

- [ ] **Step 5: Отдать числа наружу**

В `src/AFK4.Platform.Api/Endpoints/SessionEndpoints.cs` в ветке `if (result.Conflict)` заменить
тело ответа на:

```csharp
return Results.Conflict(new { Error = result.Error, result.Code, result.CurrentVersion, result.PlanLimit });
```

- [ ] **Step 6: Прогнать тесты бэкенда**

```bash
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter FullyQualifiedName~Session
```

Ожидание: новые 2 passed, существующие тесты сеансов — passed.

- [ ] **Step 7: Написать падающий тест Оператора**

В `src/AFK4.OrganizationAdmin.Web/src/apiErrors.test.ts` добавить:

```ts
it('превращает отказ по лимиту тарифа во фразу с числами', () => {
  const t = createTranslator('ru');
  const error = new PlatformApiError('conflict', 409, 'Conflict', JSON.stringify({
    code: 'plan_limit_reached',
    planLimit: { code: 'plan_limit_reached', limitName: 'concurrent_sessions', limit: 40, current: 40, planCode: 'growth' }
  }));

  const projection = projectOperatorError(error, t);

  expect(projection.detail).toContain('40');
});
```

Импорты `createTranslator`, `PlatformApiError`, `projectOperatorError` уже есть в этом файле —
свериться с его шапкой и не дублировать.

- [ ] **Step 8: Прогнать тест и убедиться, что он падает**

```bash
cd src/AFK4.OrganizationAdmin.Web && ~/.bun/bin/bun test src/apiErrors.test.ts
```

Ожидание: FAIL — деталь не содержит «40» (код неизвестен, отдаётся общий текст).

- [ ] **Step 9: Добавить строки в каталоги**

В `locales/ru.json` добавить ключ (в алфавитном порядке среди соседних `op.error.code.*`):

```json
"op.error.code.planLimitReached": "Тариф исчерпан: занято {current} из {limit}. Повысьте тариф или освободите место."
```

В `locales/en.json`:

```json
"op.error.code.planLimitReached": "Plan limit reached: {current} of {limit} in use. Upgrade the plan or free up a slot."
```

В `locales/tg.json`:

```json
"op.error.code.planLimitReached": "Маҳдудияти таъриф пур шуд: аз {limit} ҷой {current} банд аст. Тарифро баланд кунед ё ҷой холӣ кунед."
```

Затем перегенерировать типизированные каталоги:

```bash
cd packages/i18n && ~/.bun/bin/bun run gen
```

- [ ] **Step 10: Пробросить числа в `projectOperatorError`**

В `src/AFK4.OrganizationAdmin.Web/src/apiErrors.ts` добавить `plan_limit_reached` в
`codeMessageKeys` и научить проекцию читать значения:

```ts
const codeMessageKeys = {
  // ...существующие...
  plan_limit_reached: 'op.error.code.planLimitReached'
} as const satisfies Record<string, MessageKey>;

interface PlanLimitBody {
  limit: number;
  current: number;
}

function readPlanLimit(body: string): PlanLimitBody | null {
  try {
    const parsed = JSON.parse(body) as { planLimit?: { limit?: unknown; current?: unknown } };
    const limit = parsed.planLimit?.limit;
    const current = parsed.planLimit?.current;
    return typeof limit === 'number' && typeof current === 'number' ? { limit, current } : null;
  } catch {
    return null;
  }
}
```

и в `projectOperatorError`, внутри ветки известного кода, перед общим возвратом:

```ts
    if (code !== null && code in codeMessageKeys) {
      const planLimit = code === 'plan_limit_reached' ? readPlanLimit(error.body) : null;
      return {
        title,
        detail: t(codeMessageKeys[code as keyof typeof codeMessageKeys], planLimit ?? undefined)
      };
    }
```

- [ ] **Step 11: Прогнать тесты фронта и сборку**

```bash
cd src/AFK4.OrganizationAdmin.Web && ~/.bun/bin/bun test && ~/.bun/bin/bun run build
cd packages/i18n && ~/.bun/bin/bun test
```

Ожидание: всё зелёное. `bun run build` обязателен — он тайпчекает и тестовые файлы, и зелёный
`bun test` без него не доказывает сборку.

- [ ] **Step 12: Коммит**

```bash
git add src/AFK4.Platform.Api/Sessions/ src/AFK4.Platform.Api/Endpoints/SessionEndpoints.cs \
        tests/AFK4.Platform.Api.Tests/Platform/Entitlements/SessionStartPlanLimitTests.cs \
        src/AFK4.OrganizationAdmin.Web/src/apiErrors.ts src/AFK4.OrganizationAdmin.Web/src/apiErrors.test.ts \
        locales/ packages/i18n/src/
git commit -m "feat(platform): лимит одновременных сеансов и понятный отказ в Операторе"
```

---

### Task 4: Лимит сотрудников на филиал

**Files:**
- Modify: `src/AFK4.Platform.Api/Identity/EfStaffInviteService.cs`
- Modify: `src/AFK4.Platform.Api/Endpoints/StaffOnboardingEndpoints.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/Entitlements/StaffInvitePlanLimitTests.cs`

**Interfaces:**
- Consumes: `IPlanLimitGuard.CheckStaffUserAsync`.
- Produces: `StaffInviteCreateResult.PlanLimit` (`PlanLimitExceededDto?`, по умолчанию `null`).

- [ ] **Step 1: Написать падающий тест**

```csharp
[Fact]
public async Task CreateInvite_RefusesWithNumbers_WhenBranchIsAtStaffLimit()
{
    // Сидим организацию с MaxStaffUsersPerBranch = 1 и одним активным сотрудником на филиале.
    // Ожидание: результат неуспешен, PlanLimit!.LimitName == PlanLimitNames.StaffUsersPerBranch,
    // Limit == 1, Current == 1, приглашение в базе не появилось.
}

[Fact]
public async Task CreateInvite_CountsPendingInvitesTowardTheLimit()
{
    // MaxStaffUsersPerBranch = 2, один активный сотрудник, одно непринятое приглашение.
    // Ожидание: второе приглашение отказано с Current == 2 — иначе очередь приглашений
    // перепрыгнет границу разом в момент приёма.
}

[Fact]
public async Task AcceptInvite_RefusesWhenLimitDroppedBelowUsageSinceTheInvite()
{
    // Приглашение выписано при лимите 5, затем лимит опущен до 1 при одном активном сотруднике.
    // Ожидание: приём отказан по лимиту, сотрудник не создан, приглашение осталось непринятым.
}

[Fact]
public async Task AcceptInvite_Succeeds_WhenBelowLimit()
{
    // MaxStaffUsersPerBranch = 3, один активный сотрудник, живое приглашение.
    // Ожидание: сотрудник создан, приглашение помечено принятым.
}
```

Тела `[Fact]` заполнить настоящим кодом: сидинг организации — как в `PlanLimitGuardTests`, работа с
приглашениями — по образцу существующих тестов `EfStaffInviteService`
(`tests/AFK4.Platform.Api.Tests/Identity/`), включая получение плейнтекста токена, который
возвращает `CreateInviteAsync`.

- [ ] **Step 2: Прогнать тест и убедиться, что он падает**

```bash
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter FullyQualifiedName~StaffInvitePlanLimitTests
```

Ожидание: ошибка компиляции — у `StaffInviteCreateResult` нет `PlanLimit`.

- [ ] **Step 3: Расширить результат создания приглашения**

Добавить в `StaffInviteCreateResult` необязательное `PlanLimitExceededDto? PlanLimit = null` и
фабрику:

```csharp
public static StaffInviteCreateResult PlanLimitReached(PlanLimitExceededDto planLimit) =>
    new(false, null, "Plan staff limit for this branch has been reached.", planLimit);
```

Точную форму взять из существующего объявления `StaffInviteCreateResult.Failed` и повторить её
порядок параметров.

- [ ] **Step 4: Проверять лимит в обеих точках**

В `EfStaffInviteService` добавить `IPlanLimitGuard planLimitGuard` в первичный конструктор.

В `CreateInviteAsync` — сразу после проверки «такой логин уже занят» и до `db.StaffInvites.Add(...)`:

```csharp
var planLimit = await planLimitGuard.CheckStaffUserAsync(organizationId, branchId, cancellationToken);
if (planLimit is not null)
{
    return StaffInviteCreateResult.PlanLimitReached(planLimit);
}
```

В `AcceptInviteAsync` — после того как приглашение найдено, признано живым и непросроченным, и
до `db.StaffUsers.Add(...)`. Приглашение при отказе **не** помечается принятым: клуб поднимет
тариф и воспользуется тем же письмом.

Проверять надо в обеих точках: между выпиской приглашения и его приёмом лимит могли опустить, а
приглашение живёт неделю.

- [ ] **Step 5: Отдать отказ наружу**

В `src/AFK4.Platform.Api/Endpoints/StaffOnboardingEndpoints.cs` в обработчиках
`POST branches/{branchId:guid}/staff/invites` и `POST /api/staff/invites/accept` вернуть 409 с
телом, когда `PlanLimit` заполнен, сохранив текущее поведение остальных отказов:

```csharp
if (result.PlanLimit is not null)
{
    return Results.Conflict(new { Error = result.Error, result.PlanLimit.Code, PlanLimit = result.PlanLimit });
}
```

- [ ] **Step 6: Прогнать тесты**

```bash
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter FullyQualifiedName~StaffInvite
```

Ожидание: новые 4 passed, существующие тесты приглашений — passed.

- [ ] **Step 7: Коммит**

```bash
git add src/AFK4.Platform.Api/Identity/ src/AFK4.Platform.Api/Endpoints/StaffOnboardingEndpoints.cs \
        tests/AFK4.Platform.Api.Tests/Platform/Entitlements/StaffInvitePlanLimitTests.cs
git commit -m "feat(platform): лимит сотрудников на филиал при приглашении и приёме"
```

---

### Task 5: Создание филиала в панели платформы

**Files:**
- Create: `src/AFK4.Shared.Contracts/Platform/Organizations/CreateBranchRequest.cs`
- Create: `src/AFK4.Platform.Api/Endpoints/PlatformBranchEndpoints.cs`
- Modify: `src/AFK4.Platform.Api/Platform/Tenancy/IPlatformOrganizationService.cs`
- Modify: `src/AFK4.Platform.Api/Platform/Tenancy/EfPlatformOrganizationService.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs` (регистрация маршрутов)
- Test: `tests/AFK4.Platform.Api.Tests/Platform/PlatformBranchEndpointTests.cs`

**Interfaces:**
- Consumes: `IPlanLimitGuard.CheckBranchAsync`, `PlatformOrganizationOperationResult<T>`.
- Produces: `CreateBranchRequest(string Slug, string Name, string City, string? PreferredTimeZone)`;
  `IPlatformOrganizationService.CreateBranchAsync(Guid organizationId, CreateBranchRequest request,
  Guid platformAdminUserId, CancellationToken)` →
  `PlatformOrganizationOperationResult<OrganizationBranchDto>`; эндпоинт
  `POST /api/platform/organizations/{organizationId:guid}/branches`.

- [ ] **Step 1: Написать падающий тест**

`tests/AFK4.Platform.Api.Tests/Platform/PlatformBranchEndpointTests.cs`, по образцу
`PlatformOrganizationEndpointTests` (авторизация через `PlatformAdminTestHelper.AuthorizeAsAsync`):

```csharp
[Fact]
public async Task Post_RequiresAuthentication() { /* 401 без токена */ }

[Fact]
public async Task Post_RequiresCreateOrganizationPermission() { /* platform_support → 403 */ }

[Fact]
public async Task Post_CreatesBranchWithDefaultZone()
{
    // Организация с MaxBranches = 3 и одним филиалом.
    // Ожидание: 200, в базе два филиала, у нового есть зона «Общий зал»,
    // PreferredTimeZone по умолчанию "Asia/Dushanbe".
}

[Fact]
public async Task Post_RefusesWithNumbers_WhenBranchLimitReached()
{
    // MaxBranches = 1, один филиал. Ожидание: 409, тело содержит
    // code == "plan_limit_reached", limit == 1, current == 1; второй филиал не создан.
}

[Fact]
public async Task Post_RejectsDuplicateSlugWithinOrganization()
{
    // Ожидание: 409 и НЕ код лимита — иначе клиент покажет ложное объяснение отказа.
}

[Fact]
public async Task Post_ReturnsNotFound_ForUnknownOrganization() { /* 404 */ }
```

Тела заполнить настоящим кодом; проверку разрешения `platform_support → 403` писать по образцу
существующих тестов прав в `PlatformOrganizationEndpointTests`.

- [ ] **Step 2: Прогнать тест и убедиться, что он падает**

```bash
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter FullyQualifiedName~PlatformBranchEndpointTests
```

Ожидание: 404 на неизвестный маршрут / ошибка компиляции — метода и эндпоинта нет.

- [ ] **Step 3: Контракт запроса**

`src/AFK4.Shared.Contracts/Platform/Organizations/CreateBranchRequest.cs`:

```csharp
namespace AFK4.Shared.Contracts.Platform.Organizations;

/// <summary>Заявка на добавление филиала существующему клубу.</summary>
public sealed record CreateBranchRequest(
    string Slug,
    string Name,
    string City,
    string? PreferredTimeZone);
```

- [ ] **Step 4: Метод сервиса**

В `IPlatformOrganizationService`:

```csharp
Task<PlatformOrganizationOperationResult<OrganizationBranchDto>> CreateBranchAsync(
    Guid organizationId,
    CreateBranchRequest request,
    Guid platformAdminUserId,
    CancellationToken cancellationToken);
```

В `EfPlatformOrganizationService` (добавив `IPlanLimitGuard planLimitGuard` в первичный
конструктор) реализовать:

```csharp
public async Task<PlatformOrganizationOperationResult<OrganizationBranchDto>> CreateBranchAsync(
    Guid organizationId,
    CreateBranchRequest request,
    Guid platformAdminUserId,
    CancellationToken cancellationToken)
{
    var organization = await dbContext.Organizations
        .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId, cancellationToken);
    if (organization is null)
    {
        return PlatformOrganizationOperationResult<OrganizationBranchDto>.NotFound("Organization was not found.");
    }

    var slug = (request.Slug ?? string.Empty).Trim();
    var slugError = SlugValidator.Validate(slug);
    if (slugError is not null)
    {
        return PlatformOrganizationOperationResult<OrganizationBranchDto>.BadRequest(slugError);
    }

    if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.City))
    {
        return PlatformOrganizationOperationResult<OrganizationBranchDto>.BadRequest("Branch name and city are required.");
    }

    var slugTaken = await dbContext.Branches.AnyAsync(
        candidate => candidate.OrganizationId == organizationId && candidate.Slug == slug,
        cancellationToken);
    if (slugTaken)
    {
        return PlatformOrganizationOperationResult<OrganizationBranchDto>.Conflict(
            $"Branch slug '{slug}' is already in use in this organization.");
    }

    var planLimit = await planLimitGuard.CheckBranchAsync(organizationId, cancellationToken);
    if (planLimit is not null)
    {
        return PlatformOrganizationOperationResult<OrganizationBranchDto>.PlanLimitReached(planLimit);
    }

    var now = timeProvider.GetUtcNow();
    var branch = new BranchEntity
    {
        BranchId = Guid.NewGuid(),
        OrganizationId = organizationId,
        Slug = slug,
        Name = request.Name.Trim(),
        City = request.City.Trim(),
        CreatedAtUtc = now
    };
    if (!string.IsNullOrWhiteSpace(request.PreferredTimeZone))
    {
        branch.PreferredTimeZone = request.PreferredTimeZone.Trim();
    }

    dbContext.Branches.Add(branch);
    dbContext.Zones.Add(new ZoneEntity
    {
        ZoneId = Guid.NewGuid(),
        OrganizationId = organizationId,
        BranchId = branch.BranchId,
        Name = "Общий зал",
        SortOrder = 1,
        CreatedAtUtc = now
    });
    organization.UpdatedAtUtc = now;
    await dbContext.SaveChangesAsync(cancellationToken);

    return PlatformOrganizationOperationResult<OrganizationBranchDto>.Success(ToBranchDto(branch));
}
```

Порядок проверок важен: занятый slug должен отвечать своей ошибкой, а не отказом по лимиту —
ложное объяснение отказа хуже отказа. Зона по умолчанию создаётся ровно так же, как при создании
организации, иначе новый филиал будет без единой зоны и на нём нельзя разместить места.

`SlugValidator.Validate`, `ToBranchDto` и точное имя DTO филиала взять из существующего кода
`EfPlatformOrganizationService`; если проекция филиала там встроена в другой метод — вынести её в
приватный `ToBranchDto` и использовать в обоих местах.

- [ ] **Step 5: Добавить исход «лимит» в результат операции**

В `src/AFK4.Platform.Api/Platform/Tenancy/PlatformOrganizationOperationResult.cs` добавить в
перечисление `PlatformOrganizationOperationStatus` значение `PlanLimitReached`, поле
`PlanLimitExceededDto? PlanLimit = null` и фабрику:

```csharp
public static PlatformOrganizationOperationResult<T> PlanLimitReached(PlanLimitExceededDto planLimit) =>
    new(PlatformOrganizationOperationStatus.PlanLimitReached, default, "Plan branch limit has been reached.", planLimit);
```

Сверить порядок параметров с существующим объявлением записи и добавить новое поле последним.

- [ ] **Step 6: Эндпоинт**

`src/AFK4.Platform.Api/Endpoints/PlatformBranchEndpoints.cs`:

```csharp
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Platform.Api.Platform.Tenancy;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Platform.Organizations;

namespace AFK4.Platform.Api.Endpoints;

public static class PlatformBranchEndpoints
{
    public static void MapPlatformBranchEndpoints(this WebApplication app)
    {
        app.MapPost("/api/platform/organizations/{organizationId:guid}/branches", async (
            Guid organizationId,
            CreateBranchRequest request,
            PlatformAdminAuthorizationService authorizationService,
            IPlatformOrganizationService organizationService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.CreateOrganization);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var result = await organizationService.CreateBranchAsync(
                organizationId,
                request,
                authorization.PlatformAdminUserId,
                cancellationToken);

            return result.Status switch
            {
                PlatformOrganizationOperationStatus.Succeeded => Results.Ok(result.Value),
                PlatformOrganizationOperationStatus.NotFound => Results.NotFound(new { Error = result.Error }),
                PlatformOrganizationOperationStatus.PlanLimitReached =>
                    Results.Conflict(new { Error = result.Error, result.PlanLimit!.Code, PlanLimit = result.PlanLimit }),
                PlatformOrganizationOperationStatus.Conflict => Results.Conflict(new { Error = result.Error }),
                _ => Results.BadRequest(new { Error = result.Error })
            };
        });
    }
}
```

Аудит записать после успеха тем же способом, каким пишет создание организации в
`PlatformOrganizationEndpoints` — взять оттуда точную форму `AuditRecordWriteRequest` и действие;
если подходящего имени действия нет, добавить `AuditActionNames.CreateBranch`.
Имя свойства с идентификатором администратора (`authorization.PlatformAdminUserId`) свериться
с `PlatformAdminAuthorizationService`.

Зарегистрировать в `Program.cs` рядом с остальными `Map...Endpoints()`:

```csharp
app.MapPlatformBranchEndpoints();
```

- [ ] **Step 7: Прогнать тесты**

```bash
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj
```

Ожидание: все зелёные, новые 6 в том числе.

- [ ] **Step 8: Коммит**

```bash
git add src/AFK4.Shared.Contracts/Platform/Organizations/CreateBranchRequest.cs \
        src/AFK4.Platform.Api/Endpoints/PlatformBranchEndpoints.cs \
        src/AFK4.Platform.Api/Platform/Tenancy/ src/AFK4.Platform.Api/Program.cs \
        tests/AFK4.Platform.Api.Tests/Platform/PlatformBranchEndpointTests.cs
git commit -m "feat(platform): добавление филиала клубу с проверкой лимита тарифа"
```

---

### Task 6: Панель — добавление филиала и занятость лимитов

**Files:**
- Create: `src/AFK4.PlatformControl.Web/src/platform/organizations/NewBranchDialog.tsx`
- Create: `src/AFK4.PlatformControl.Web/src/platform/organizations/NewBranchDialog.test.tsx`
- Modify: `src/AFK4.PlatformControl.Web/src/api/platformClients/organizations.ts`
- Modify: `src/AFK4.PlatformControl.Web/src/api/types.ts`
- Modify: `src/AFK4.PlatformControl.Web/src/platform/organizations/OrganizationClubsTab.tsx`
- Modify: `src/AFK4.PlatformControl.Web/src/platform/organizations/OrganizationClubsTab.test.tsx` (создать, если файла нет)
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json`

**Interfaces:**
- Consumes: `POST /api/platform/organizations/{organizationId}/branches`, тело отказа
  `{ code: 'plan_limit_reached', planLimit: { limit, current, planCode } }`.
- Produces: `OrganizationsApi.createBranch(organizationId, request)`.

- [ ] **Step 1: Написать падающие тесты**

`NewBranchDialog.test.tsx` — три проверки:

```tsx
it('создаёт филиал и отдаёт его наверх', async () => {
  // createBranch возвращает филиал; ожидание: onCreated вызван с ним.
});

it('показывает занятость тарифа при отказе по лимиту', async () => {
  // createBranch отклоняется PlatformApiError с телом
  // { code: 'plan_limit_reached', planLimit: { limit: 1, current: 1, planCode: 'starter' } }.
  // Ожидание: на экране видно «1» и текст лимита, диалог не закрыт.
});

it('показывает занятый короткий адрес отдельной ошибкой', async () => {
  // createBranch отклоняется 409 без planLimit.
  // Ожидание: сообщение о занятом slug, НЕ сообщение про тариф.
});
```

`OrganizationClubsTab.test.tsx` — одна проверка:

```tsx
it('показывает занятость лимита филиалов', () => {
  // organization.limits.maxBranches = 3, branches.length = 2 → на экране «2 / 3».
  // При maxBranches = null счётчика нет вовсе (без ограничения нечего показывать).
});
```

Тела заполнить настоящим кодом по образцу соседних тестов
(`OrganizationLimitsSection.test.tsx`, `OrganizationDynamicsTab.test.tsx`): они показывают, как
здесь подставляют фейковый клиент и оборачивают в `I18nProvider`.

- [ ] **Step 2: Прогнать тесты и убедиться, что они падают**

```bash
cd src/AFK4.PlatformControl.Web && ~/.bun/bin/bun test src/platform/organizations
```

Ожидание: FAIL — `NewBranchDialog` не существует.

- [ ] **Step 3: Строки**

Добавить в `locales/ru.json` (и соответствующие переводы в `en.json`, `tg.json` — таджикский
писать по-таджикски, копия русского запрещена и ловится тестом каталогов):

```json
"platform.organization.branches.add": "Добавить филиал",
"platform.organization.branches.dialogTitle": "Новый филиал",
"platform.organization.branches.name": "Название",
"platform.organization.branches.city": "Город",
"platform.organization.branches.slug": "Короткий адрес",
"platform.organization.branches.timeZone": "Часовой пояс",
"platform.organization.branches.create": "Создать",
"platform.organization.branches.slugTaken": "Такой короткий адрес в этом клубе уже занят.",
"platform.organization.branches.planLimit": "Тариф {planCode}: филиалов {current} из {limit}. Повысьте тариф, чтобы добавить ещё один.",
"platform.organization.branches.usage": "Филиалов: {current} из {limit}"
```

Перегенерировать каталоги:

```bash
cd packages/i18n && ~/.bun/bin/bun run gen
```

- [ ] **Step 4: Клиент API**

В `src/AFK4.PlatformControl.Web/src/api/types.ts` добавить:

```ts
export interface CreateBranchRequest {
  slug: string;
  name: string;
  city: string;
  preferredTimeZone: string | null;
}

export interface PlanLimitExceeded {
  code: string;
  limitName: string;
  limit: number;
  current: number;
  planCode: string;
}
```

В `src/AFK4.PlatformControl.Web/src/api/platformClients/organizations.ts` добавить в `OrganizationsApi`
метод по образцу соседних:

```ts
createBranch(organizationId: string, request: CreateBranchRequest): Promise<OrganizationBranch> {
  return this.client.post(`/api/platform/organizations/${organizationId}/branches`, request);
}
```

Точную форму (имя транспортного метода, обработка ответа) взять из соседнего метода этого же файла
и не изобретать свою.

- [ ] **Step 5: Диалог**

`NewBranchDialog.tsx` — форма с четырьмя полями (название, город, короткий адрес, часовой пояс с
предзаполненным `Asia/Dushanbe`), кнопкой создания и разбором ошибки:

```tsx
function readPlanLimit(error: unknown): PlanLimitExceeded | null {
  if (!(error instanceof PlatformApiError)) return null;
  try {
    const parsed = JSON.parse(error.body) as { planLimit?: PlanLimitExceeded };
    return parsed.planLimit ?? null;
  } catch {
    return null;
  }
}
```

При отказе с `planLimit` показать `t('platform.organization.branches.planLimit', {
planCode: planLimit.planCode, current: planLimit.current, limit: planLimit.limit })` и оставить
диалог открытым; при прочем 409 — `platform.organization.branches.slugTaken`; при остальных
ошибках — существующий общий `platform.organization.action.error`.

Разметку, компоненты диалога и стили брать из соседнего `OrganizationProfileDialog.tsx` — свой
стиль не привносить.

- [ ] **Step 6: Кнопка и счётчик на вкладке «Клубы»**

В `OrganizationClubsTab.tsx`: принять в пропсах `limits: OrganizationLimits` и
`onBranchCreated: (branch: OrganizationBranch) => void`, показать над сеткой карточек кнопку
«Добавить филиал», открывающую `NewBranchDialog`, и — только когда `limits.maxBranches !== null` —
строку `t('platform.organization.branches.usage', { current: branches.length, limit: limits.maxBranches })`.

Пробросить новые пропсы из `OrganizationPage.tsx`, где вкладка отрисовывается; обновление списка
филиалов делать тем же способом, каким страница уже обновляет организацию после других действий
(`onUpdated`), а не локальной копией.

- [ ] **Step 7: Прогнать тесты и сборку**

```bash
cd src/AFK4.PlatformControl.Web && ~/.bun/bin/bun test && ~/.bun/bin/bun run build
cd packages/i18n && ~/.bun/bin/bun test
```

Ожидание: всё зелёное.

- [ ] **Step 8: Коммит**

```bash
git add src/AFK4.PlatformControl.Web/src/ locales/ packages/i18n/src/
git commit -m "feat(platform-control): добавление филиала и занятость лимита в паспорте клуба"
```

---

## Финальная проверка перед завершением ветки

```bash
dotnet build AFK4.sln
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj
cd src/AFK4.PlatformControl.Web && ~/.bun/bin/bun test && ~/.bun/bin/bun run build
cd ../AFK4.OrganizationAdmin.Web && ~/.bun/bin/bun test && ~/.bun/bin/bun run build
cd ../../packages/i18n && ~/.bun/bin/bun test
```

Postgres-прогон бэкенда обязателен, если в окружении задана
`AFK4_PLATFORM_ADMIN_POSTGRES_TEST_CONNECTION_STRING`: пропущенные Postgres-тесты в этой ветке
означают непроверенную работу с настоящей базой.

## Отклонения от спеки (§1) и почему

- **§1 разрезан надвое.** Этот план — только лимиты. Каталог фич, лестница разрешений, ручные
  исключения, отдача фич клубским приложениям и скрытие выключенных разделов в интерфейсах
  выделены в отдельный план: вместе это один непроверяемый ком, по отдельности — две работающие
  поставки.
- **Добавлено создание филиала**, которого в спеке §1 не было. Без него лимит `MaxBranches`
  проверял бы то, что в продукте физически не может произойти: второй филиал сегодня создать
  нечем. Мёртвая проверка ради галочки — ровно тот ложно-зелёный результат, который проект
  запрещает.
- **Непринятые приглашения занимают место в лимите сотрудников.** В спеке это не оговорено;
  без этого правила три приглашения на филиал с лимитом два проходят проверку по очереди и
  перепрыгивают границу все разом в момент приёма.
- **Лимит сеансов проверяется в `EfSessionStartWorkflow`, а не в эндпоинте.** Через workflow идут
  оба пути старта — команда Оператора и старт из брони; проверка в эндпоинте оставила бы второй
  путь без лимита.
