# Фичи и лестница разрешений — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Платформа может включать и выключать фичи конкретному клубу — и продавать их тарифом, и выкатывать по одному, — а выключенная фича исчезает из интерфейса клубских приложений, а не отвечает «нельзя».

**Architecture:** Каталог фич объявлен в коде и заводится в таблицу при старте; значение для клуба считает `IOrganizationEntitlements` по лестнице «ручное исключение для клуба → тариф → умолчание фичи». Сервер проверяет разрешение в точках использования, а клубские приложения читают список включённых фич одним запросом и убирают соответствующие разделы.

**Tech Stack:** .NET 10 (Platform.Api, EF Core, Postgres), xUnit + `PlatformApiFactory`, React 19 + TypeScript (PlatformControl.Web, Customer.Web, Player.Shell.Web, OrganizationAdmin.Web), `bun test`, `@afk4/i18n`.

**Ветка:** `feat/platform-feature-entitlements-wave-d`

## Global Constraints

- **Лестница считается в одном месте.** Порядок «исключение клуба → тариф → умолчание фичи» существует ровно один раз, в `IOrganizationEntitlements`. Второй экземпляр разошёлся бы с первым молча.
- **Ответ говорит, ЧЕМ решено.** Панель показывает не только «включено/выключено», но и уровень решения (`override` / `plan` / `default`) — «не куплено» и «не выкачено» разные ответы клиенту.
- **Каталог объявлен в коде, живёт в базе.** Фичи заводятся при старте из объявлений в коде и редактируются в панели. Строка без кода и код без строки невозможны.
- **Сегодняшнее поведение не меняется.** Все четыре фичи стартуют включёнными для всех: `plan_features` пуст (у тарифа нет мнения), умолчание каждой фичи — «включена». Волна даёт механизм, а не новую отсечку.
- **Разрешение проверяется на сервере.** Скрытый в интерфейсе раздел — удобство; отказ эндпоинта — защита. Каждая фича имеет серверный гейт И скрытие в интерфейсе.
- **Сервер прозу не рендерит.** Отказ по выключенной фиче несёт код `feature_disabled` и ключ фичи; фразу собирает клиент из `@afk4/i18n`.
- **Каждое изменение исключения пишется в аудит платформы.**
- **Новые таблицы — snake_case** (`platform_features`, `plan_features`, `organization_feature_overrides`), колонки PascalCase в кавычках; сырой SQL в миграциях сверяется с `PlatformDbContext`, а не с именем C#-класса.
- **Тесты на гонки — только на настоящем Postgres** (`[PlatformAdminPostgresFact]`); уникальные индексы на InMemory не проверяются вовсе, и делать вид, что проверяются, нельзя.
- **Строки интерфейса — только через `@afk4/i18n`.** Источник — `locales/{ru,en,tg}.json`, `packages/i18n/src/messages.*.ts` генерируются `bun run gen` и руками не правятся. Таджикский пишется по-таджикски, копия русского запрещена (`packages/i18n/src/messages.test.ts` это стережёт).
- **`bun` только по полному пути** `~/.bun/bin/bun`. Сборка фронта обязательна: `bun run build` тайпчекает и тестовые файлы, зелёный `bun test` без неё ничего не доказывает.

## Что уже есть в коде (контекст для исполнителя)

- Предыдущий план волны (`2026-08-08-platform-plan-limits.md`, в main) построил `src/AFK4.Platform.Api/Platform/Entitlements/` с `IPlanLimitGuard` и `OrganizationLimitsJson`. Новый код живёт в той же папке и держится того же вида: интерфейс + `Ef`-реализация, отказ несёт машинный код и данные, не прозу.
- Прецедент «объявлено в коде, живёт в базе» — `BillingPlanSeedHostedService`: добавляет недостающие известные коды и никогда не переписывает существующие строки (панель авторитетна после создания).
- `OrganizationEntity.PlanCode` — код тарифа организации; каталог тарифов в `SubscriptionPlans` (`SubscriptionPlanEntity.PlanCode`).
- Права платформы: `PlatformAdminPermissionNames` в `src/AFK4.Shared.Contracts/Platform/Auth/`; проверка в эндпоинте — `authorizationService.RequirePermission(...)`, образец `src/AFK4.Platform.Api/Endpoints/PlatformBranchEndpoints.cs`.
- Аудит платформы: образец записи — тот же `PlatformBranchEndpoints.cs` (действие `AuditActionNames.CreateBranch`).
- Точки использования фич сегодня:
  - **Онлайн-бронирование:** `POST /api/me/reservations` в `src/AFK4.Platform.Api/Endpoints/PlayerSelfServiceEndpoints.cs:441`.
  - **Лояльность:** начисление кэшбэка — `LoyaltyAccrualService.BuildCashbackEntryAsync` в `src/AFK4.Platform.Api/Loyalty/LoyaltyAccrualService.cs`; чтение игроком — `GET /api/me/loyalty` в `PlayerLoyaltyEndpoints.cs`.
  - **Онлайн-пополнение:** `POST /api/me/wallet/top-up-intent` в `PlayerSelfServiceEndpoints.cs:219`.
  - **Магазин игрока:** `POST /api/me/shop/orders` и `GET /api/me/shop/catalog` в `PlayerShopEndpoints.cs`.
- Клубские приложения и их разделы:
  - `src/AFK4.Customer.Web` — личный кабинет игрока: вкладка «Брони» (`components/BottomNav.tsx`, `screens/reservations/`), пополнение кошелька (`screens/wallet/WalletPanel.tsx`). Клиент API — `src/api/playerApi.ts`.
  - `src/AFK4.Player.Shell.Web` — оболочка на игровой машине: `screens/ShopScreen.tsx`, `screens/LoyaltyScreen.tsx`, `screens/TopUpScreen.tsx`, меню `screens/SelfServiceMenu.tsx`. Клиент API — `src/shellApi.ts`.
  - `src/AFK4.OrganizationAdmin.Web` — Оператор: раздел «Лояльность» в управлении.
- Тесты бэкенда поднимаются через `PlatformApiFactory` + `factory.Services.CreateAsyncScope()`; образец — `tests/AFK4.Platform.Api.Tests/Platform/Entitlements/PlanLimitGuardTests.cs`.

## Структура файлов

**Создаются:**

| Файл | Ответственность |
|---|---|
| `src/AFK4.Shared.Contracts/Platform/Features/PlatformFeatureNames.cs` | Ключи фич и код отказа |
| `src/AFK4.Shared.Contracts/Platform/Features/FeatureContracts.cs` | DTO каталога, состояния фичи организации и запроса на исключение |
| `src/AFK4.Platform.Api/Data/PlatformFeatureEntity.cs` | Строка каталога фич |
| `src/AFK4.Platform.Api/Data/PlanFeatureEntity.cs` | Мнение тарифа о фиче |
| `src/AFK4.Platform.Api/Data/OrganizationFeatureOverrideEntity.cs` | Ручное исключение для клуба |
| `src/AFK4.Platform.Api/Platform/Entitlements/FeatureCatalog.cs` | Объявление фич в коде |
| `src/AFK4.Platform.Api/Platform/Entitlements/FeatureCatalogSeedHostedService.cs` | Заведение объявленных фич в базу при старте |
| `src/AFK4.Platform.Api/Platform/Entitlements/IOrganizationEntitlements.cs` | Контракт лестницы |
| `src/AFK4.Platform.Api/Platform/Entitlements/EfOrganizationEntitlements.cs` | Лестница и её единственная реализация |
| `src/AFK4.Platform.Api/Endpoints/PlatformFeatureEndpoints.cs` | Панель: список фич клуба и постановка/снятие исключения |
| `src/AFK4.Platform.Api/Endpoints/PlayerFeatureEndpoints.cs` | `GET /api/me/features` для клубских приложений |
| `src/AFK4.PlatformControl.Web/src/platform/organizations/OrganizationFeaturesTab.tsx` | Вкладка «Фичи» в паспорте клуба |

**Изменяются:** `PlatformDbContext` + миграция, `Program.cs`, `PlayerSelfServiceEndpoints.cs`, `PlayerShopEndpoints.cs`, `PlayerLoyaltyEndpoints.cs`, `LoyaltyAccrualService.cs`, `PlatformAdminPermissionNames` + `PlatformAdminPermissionCatalog`, клиенты и экраны трёх фронтов, `locales/*.json`.

---

### Task 1: Схема, каталог фич и сидер

**Files:**
- Create: `src/AFK4.Shared.Contracts/Platform/Features/PlatformFeatureNames.cs`
- Create: `src/AFK4.Platform.Api/Data/PlatformFeatureEntity.cs`
- Create: `src/AFK4.Platform.Api/Data/PlanFeatureEntity.cs`
- Create: `src/AFK4.Platform.Api/Data/OrganizationFeatureOverrideEntity.cs`
- Create: `src/AFK4.Platform.Api/Platform/Entitlements/FeatureCatalog.cs`
- Create: `src/AFK4.Platform.Api/Platform/Entitlements/FeatureCatalogSeedHostedService.cs`
- Modify: `src/AFK4.Platform.Api/Data/PlatformDbContext.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/Entitlements/FeatureCatalogSeedTests.cs`

**Interfaces:**
- Produces: `PlatformFeatureNames.OnlineBooking` = `"online_booking"`, `.Loyalty` = `"loyalty"`, `.OnlineTopUp` = `"online_topup"`, `.PlayerShop` = `"player_shop"`, `PlatformFeatureNames.DisabledCode` = `"feature_disabled"`, `PlatformFeatureNames.All` (`IReadOnlyList<string>`);
  `PlatformFeatureEntity(FeatureKey, Name, Description, EnabledByDefault, CreatedAtUtc, UpdatedAtUtc)`;
  `PlanFeatureEntity(PlanFeatureId, PlanCode, FeatureKey, IsIncluded)`;
  `OrganizationFeatureOverrideEntity(OrganizationFeatureOverrideId, OrganizationId, FeatureKey, IsEnabled, Reason, SetByPlatformAdminUserId, SetAtUtc)`;
  `FeatureCatalog.Declared` — объявленные фичи.

- [ ] **Step 1: Написать падающий тест**

`tests/AFK4.Platform.Api.Tests/Platform/Entitlements/FeatureCatalogSeedTests.cs`:

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform.Entitlements;

public sealed class FeatureCatalogSeedTests
{
    [Fact]
    public async Task Seed_CreatesEveryDeclaredFeature_EnabledByDefault()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var features = await db.PlatformFeatures.AsNoTracking().ToListAsync();

        Assert.Equal(PlatformFeatureNames.All.Count, features.Count);
        Assert.All(PlatformFeatureNames.All, key => Assert.Contains(features, feature => feature.FeatureKey == key));
        // Сегодняшнее поведение не меняется: всё, что работало, продолжает работать у всех.
        Assert.All(features, feature => Assert.True(feature.EnabledByDefault));
    }

    [Fact]
    public async Task Seed_DoesNotOverwriteAnExistingRow()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var feature = await db.PlatformFeatures.SingleAsync(row => row.FeatureKey == PlatformFeatureNames.PlayerShop);
        feature.EnabledByDefault = false;
        feature.Description = "Отредактировано в панели";
        await db.SaveChangesAsync();

        var seeder = scope.ServiceProvider.GetServices<IHostedService>()
            .OfType<FeatureCatalogSeedHostedService>()
            .Single();
        await seeder.StartAsync(CancellationToken.None);

        // Панель авторитетна после создания строки: повторный старт не откатывает осознанную правку.
        var reloaded = await db.PlatformFeatures.AsNoTracking()
            .SingleAsync(row => row.FeatureKey == PlatformFeatureNames.PlayerShop);
        Assert.False(reloaded.EnabledByDefault);
        Assert.Equal("Отредактировано в панели", reloaded.Description);
    }

    [Fact]
    public async Task Seed_AddsAMissingKnownFeature_WhenCatalogIsPartiallyFilled()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.PlatformFeatures.RemoveRange(
            await db.PlatformFeatures.Where(row => row.FeatureKey == PlatformFeatureNames.Loyalty).ToListAsync());
        await db.SaveChangesAsync();

        var seeder = scope.ServiceProvider.GetServices<IHostedService>()
            .OfType<FeatureCatalogSeedHostedService>()
            .Single();
        await seeder.StartAsync(CancellationToken.None);

        // Непустой каталог — не повод выйти рано: база из продакшена должна получить новые фичи.
        Assert.True(await db.PlatformFeatures.AnyAsync(row => row.FeatureKey == PlatformFeatureNames.Loyalty));
    }
}
```

Добавить `using Microsoft.Extensions.Hosting;` и `using AFK4.Platform.Api.Platform.Entitlements;`.

- [ ] **Step 2: Прогнать тест и убедиться, что он падает**

```bash
cd /home/fedya/projects/afk4.net
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter FullyQualifiedName~FeatureCatalogSeedTests
```

Ожидание: ошибка компиляции — `PlatformFeatureNames`, `PlatformFeatures`, `FeatureCatalogSeedHostedService` не существуют.

- [ ] **Step 3: Контракт имён**

`src/AFK4.Shared.Contracts/Platform/Features/PlatformFeatureNames.cs`:

```csharp
namespace AFK4.Shared.Contracts.Platform.Features;

/// <summary>
/// Ключи фич, которые платформа умеет включать и выключать клубу. Каждый ключ обязан иметь
/// точку проверки в коде: флаг без потребителя — мусор, который невозможно опознать через месяц.
/// </summary>
public static class PlatformFeatureNames
{
    /// <summary>Код отказа, когда фича выключена. Фразу собирает клиент.</summary>
    public const string DisabledCode = "feature_disabled";

    public const string OnlineBooking = "online_booking";

    public const string Loyalty = "loyalty";

    public const string OnlineTopUp = "online_topup";

    public const string PlayerShop = "player_shop";

    public static readonly IReadOnlyList<string> All =
        [OnlineBooking, Loyalty, OnlineTopUp, PlayerShop];
}
```

- [ ] **Step 4: Сущности**

`src/AFK4.Platform.Api/Data/PlatformFeatureEntity.cs`:

```csharp
namespace AFK4.Platform.Api.Data;

/// <summary>
/// Строка каталога фич. Заводится при старте из объявлений в коде (<c>FeatureCatalog</c>),
/// дальше редактируется в панели: строка без кода и код без строки невозможны.
/// </summary>
public sealed class PlatformFeatureEntity
{
    public string FeatureKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Последняя ступень лестницы: значение, когда ни клуб, ни тариф не высказались.</summary>
    public bool EnabledByDefault { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
```

`src/AFK4.Platform.Api/Data/PlanFeatureEntity.cs`:

```csharp
namespace AFK4.Platform.Api.Data;

/// <summary>
/// Мнение тарифа о фиче. Отсутствие строки означает «у тарифа мнения нет» — тогда решает
/// умолчание фичи. Пустая таблица = сегодняшнее поведение.
/// </summary>
public sealed class PlanFeatureEntity
{
    public Guid PlanFeatureId { get; set; }

    public string PlanCode { get; set; } = string.Empty;

    public string FeatureKey { get; set; } = string.Empty;

    public bool IsIncluded { get; set; }
}
```

`src/AFK4.Platform.Api/Data/OrganizationFeatureOverrideEntity.cs`:

```csharp
namespace AFK4.Platform.Api.Data;

/// <summary>
/// Ручное исключение для конкретного клуба — верхняя ступень лестницы. Несёт причину и автора:
/// рубильник раскатки без объяснения через месяц никто не опознает.
/// </summary>
public sealed class OrganizationFeatureOverrideEntity
{
    public Guid OrganizationFeatureOverrideId { get; set; }

    public Guid OrganizationId { get; set; }

    public string FeatureKey { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }

    public string Reason { get; set; } = string.Empty;

    public Guid SetByPlatformAdminUserId { get; set; }

    public DateTimeOffset SetAtUtc { get; set; }
}
```

- [ ] **Step 5: Регистрация в `PlatformDbContext`**

Добавить три `DbSet` рядом с остальными:

```csharp
    public DbSet<PlatformFeatureEntity> PlatformFeatures => Set<PlatformFeatureEntity>();

    public DbSet<PlanFeatureEntity> PlanFeatures => Set<PlanFeatureEntity>();

    public DbSet<OrganizationFeatureOverrideEntity> OrganizationFeatureOverrides => Set<OrganizationFeatureOverrideEntity>();
```

И конфигурацию в `OnModelCreating`, рядом с блоком `BranchDailySnapshotEntity`:

```csharp
        modelBuilder.Entity<PlatformFeatureEntity>(entity =>
        {
            entity.ToTable("platform_features");
            entity.HasKey(feature => feature.FeatureKey);
            entity.Property(feature => feature.FeatureKey).HasMaxLength(64);
            entity.Property(feature => feature.Name).HasMaxLength(128).IsRequired();
            entity.Property(feature => feature.Description).HasMaxLength(500).IsRequired();
        });

        modelBuilder.Entity<PlanFeatureEntity>(entity =>
        {
            entity.ToTable("plan_features");
            entity.HasKey(planFeature => planFeature.PlanFeatureId);
            entity.Property(planFeature => planFeature.PlanCode).HasMaxLength(64).IsRequired();
            entity.Property(planFeature => planFeature.FeatureKey).HasMaxLength(64).IsRequired();
            entity.HasIndex(planFeature => new { planFeature.PlanCode, planFeature.FeatureKey })
                .IsUnique()
                .HasDatabaseName("IX_plan_features_Plan_Feature");
        });

        modelBuilder.Entity<OrganizationFeatureOverrideEntity>(entity =>
        {
            entity.ToTable("organization_feature_overrides");
            entity.HasKey(featureOverride => featureOverride.OrganizationFeatureOverrideId);
            entity.Property(featureOverride => featureOverride.FeatureKey).HasMaxLength(64).IsRequired();
            entity.Property(featureOverride => featureOverride.Reason).HasMaxLength(500).IsRequired();
            entity.HasIndex(featureOverride => new { featureOverride.OrganizationId, featureOverride.FeatureKey })
                .IsUnique()
                .HasDatabaseName("IX_organization_feature_overrides_Organization_Feature");
        });
```

Уникальные индексы здесь — не украшение: «одно мнение тарифа на фичу» и «одно исключение клуба на фичу» должна держать база, а не аккуратность вызывающего кода.

- [ ] **Step 6: Объявление каталога и сидер**

`src/AFK4.Platform.Api/Platform/Entitlements/FeatureCatalog.cs`:

```csharp
using AFK4.Shared.Contracts.Platform.Features;

namespace AFK4.Platform.Api.Platform.Entitlements;

/// <summary>Фичи, объявленные кодом. Имя и описание — стартовые; дальше их правит панель.</summary>
public static class FeatureCatalog
{
    public sealed record Declaration(string FeatureKey, string Name, string Description, bool EnabledByDefault);

    public static readonly IReadOnlyList<Declaration> Declared =
    [
        new(PlatformFeatureNames.OnlineBooking, "Онлайн-бронирование",
            "Игрок сам бронирует место через личный кабинет.", EnabledByDefault: true),
        new(PlatformFeatureNames.Loyalty, "Лояльность и кэшбэк",
            "Начисление бонусов игрокам за игру и покупки.", EnabledByDefault: true),
        new(PlatformFeatureNames.OnlineTopUp, "Онлайн-пополнение",
            "Пополнение кошелька банковской картой.", EnabledByDefault: true),
        new(PlatformFeatureNames.PlayerShop, "Магазин и заказы игрока",
            "Заказ еды и товаров с игрового места.", EnabledByDefault: true)
    ];
}
```

`src/AFK4.Platform.Api/Platform/Entitlements/FeatureCatalogSeedHostedService.cs`:

```csharp
using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AFK4.Platform.Api.Platform.Entitlements;

public sealed class FeatureCatalogSeedHostedService(
    IServiceProvider serviceProvider,
    TimeProvider timeProvider,
    ILogger<FeatureCatalogSeedHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        // Добавляет только недостающие объявленные ключи и никогда не трогает существующую строку:
        // после создания авторитетна панель, и сидер, переписывающий имя/описание/умолчание на
        // каждом рестарте, молча откатывал бы осознанную правку. Раннего выхода «каталог непустой»
        // тоже нет: база из продакшена должна получать новые фичи следующим деплоем.
        var existingKeys = await dbContext.PlatformFeatures
            .Select(feature => feature.FeatureKey)
            .ToHashSetAsync(cancellationToken);

        var now = timeProvider.GetUtcNow();
        var added = 0;
        foreach (var declaration in FeatureCatalog.Declared)
        {
            if (existingKeys.Contains(declaration.FeatureKey))
            {
                continue;
            }

            dbContext.PlatformFeatures.Add(new PlatformFeatureEntity
            {
                FeatureKey = declaration.FeatureKey,
                Name = declaration.Name,
                Description = declaration.Description,
                EnabledByDefault = declaration.EnabledByDefault,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            added++;
        }

        if (added > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation("Feature catalog seed: added {Added} missing declared features.", added);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

Зарегистрировать в `Program.cs` рядом с `BillingPlanSeedHostedService`:

```csharp
builder.Services.AddSingleton<FeatureCatalogSeedHostedService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<FeatureCatalogSeedHostedService>());
```

Регистрация именно такой парой нужна, чтобы тест мог достать тот же экземпляр через `GetServices<IHostedService>()`; если рядом стоящий `BillingPlanSeedHostedService` зарегистрирован иначе — повтори его форму и поправь способ получения сидера в тесте, сохранив смысл проверки.

- [ ] **Step 7: Миграция**

```bash
cd /home/fedya/projects/afk4.net
dotnet ef migrations add AddFeatureEntitlements --project src/AFK4.Platform.Api --startup-project src/AFK4.Platform.Api --output-dir Data/Migrations
```

Прочитать сгенерированный файл и сверить имена таблиц и колонок с `PlatformDbContext`: таблицы `platform_features`, `plan_features`, `organization_feature_overrides`, оба уникальных индекса на месте.

- [ ] **Step 8: Прогнать тесты**

```bash
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter FullyQualifiedName~FeatureCatalogSeedTests
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj
```

Ожидание: 3 новых passed, весь проект зелёный.

- [ ] **Step 9: Коммит**

```bash
git add src/AFK4.Shared.Contracts/Platform/Features/ src/AFK4.Platform.Api/Data/ \
        src/AFK4.Platform.Api/Platform/Entitlements/ src/AFK4.Platform.Api/Program.cs \
        tests/AFK4.Platform.Api.Tests/Platform/Entitlements/FeatureCatalogSeedTests.cs
git commit -m "feat(platform): каталог фич — объявлен в коде, живёт в базе"
```

---

### Task 2: Лестница разрешений

**Files:**
- Create: `src/AFK4.Shared.Contracts/Platform/Features/FeatureContracts.cs`
- Create: `src/AFK4.Platform.Api/Platform/Entitlements/IOrganizationEntitlements.cs`
- Create: `src/AFK4.Platform.Api/Platform/Entitlements/EfOrganizationEntitlements.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/Entitlements/OrganizationEntitlementsTests.cs`

**Interfaces:**
- Consumes: `PlatformFeatureNames`, три сущности из задачи 1.
- Produces: `FeatureDecisionLevel` (`"override"` / `"plan"` / `"default"` — константы в `FeatureDecisionLevels`);
  `OrganizationFeatureStateDto(string FeatureKey, string Name, string Description, bool IsEnabled, string DecisionLevel, bool? OverrideValue, string? OverrideReason, DateTimeOffset? OverrideSetAtUtc, bool? PlanValue, bool DefaultValue)`;
  `IOrganizationEntitlements` с `Task<bool> IsEnabledAsync(Guid organizationId, string featureKey, CancellationToken)`, `Task<IReadOnlyList<string>> ListEnabledAsync(Guid organizationId, CancellationToken)` и `Task<IReadOnlyList<OrganizationFeatureStateDto>> DescribeAsync(Guid organizationId, CancellationToken)`.

- [ ] **Step 1: Написать падающий тест**

`tests/AFK4.Platform.Api.Tests/Platform/Entitlements/OrganizationEntitlementsTests.cs` — семь проверок:

```csharp
[Fact] public async Task Default_WinsWhenNobodyElseSpoke() { }
[Fact] public async Task Plan_BeatsDefault() { }
[Fact] public async Task Override_BeatsPlanAndDefault() { }
[Fact] public async Task Override_CanTurnOnWhatPlanTurnedOff() { }
[Fact] public async Task Describe_ReportsWhichLevelDecided() { }
[Fact] public async Task ListEnabled_ReturnsOnlyEnabledKeys() { }
[Fact] public async Task UnknownOrganization_ReportsEverythingDisabled() { }
```

Тела написать настоящим кодом. Сидинг организации — как в `tests/AFK4.Platform.Api.Tests/Platform/Entitlements/PlanLimitGuardTests.cs` (метод `SeedAsync`), с указанием `PlanCode`. Обязательные утверждения:

- `Default_WinsWhenNobodyElseSpoke`: без строк в `plan_features` и `organization_feature_overrides` все четыре фичи включены (`EnabledByDefault = true` из сидера), `DecisionLevel == FeatureDecisionLevels.Default`.
- `Plan_BeatsDefault`: строка `plan_features` с `IsIncluded = false` для тарифа организации → `IsEnabledAsync` возвращает `false`, `DecisionLevel == FeatureDecisionLevels.Plan`.
- `Override_BeatsPlanAndDefault`: поверх выключающего тарифа исключение с `IsEnabled = false` при умолчании `true` → `false`, уровень `Override`.
- `Override_CanTurnOnWhatPlanTurnedOff`: тариф выключил, исключение включило → `true`, уровень `Override`. Это и есть рубильник раскатки.
- `Describe_ReportsWhichLevelDecided`: в описании фичи с исключением видны `OverrideValue`, `OverrideReason` и `PlanValue` — панель обязана показать, чем решено, а не только итог.
- `ListEnabled_ReturnsOnlyEnabledKeys`: одна фича выключена тарифом → её ключа в списке нет, остальные три есть.
- `UnknownOrganization_ReportsEverythingDisabled`: для несуществующей организации `ListEnabledAsync` пуст и `IsEnabledAsync` возвращает `false`. Отсутствующий клуб не получает доступ по умолчанию — здесь молчание значит «нет», в отличие от лимитов, где молчание значило «не отказывать».

- [ ] **Step 2: Прогнать тест и убедиться, что он падает**

```bash
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter FullyQualifiedName~OrganizationEntitlementsTests
```

Ожидание: ошибка компиляции — `IOrganizationEntitlements` не существует.

- [ ] **Step 3: Контракты**

`src/AFK4.Shared.Contracts/Platform/Features/FeatureContracts.cs`:

```csharp
namespace AFK4.Shared.Contracts.Platform.Features;

/// <summary>Ступень лестницы, на которой принято решение о фиче.</summary>
public static class FeatureDecisionLevels
{
    public const string Override = "override";

    public const string Plan = "plan";

    public const string Default = "default";
}

/// <summary>
/// Состояние фичи для клуба вместе с тем, ЧЕМ оно решено: «не куплено» и «не выкачено» —
/// разные ответы клиенту, и панель обязана их различать.
/// </summary>
public sealed record OrganizationFeatureStateDto(
    string FeatureKey,
    string Name,
    string Description,
    bool IsEnabled,
    string DecisionLevel,
    bool? OverrideValue,
    string? OverrideReason,
    DateTimeOffset? OverrideSetAtUtc,
    bool? PlanValue,
    bool DefaultValue);

/// <summary>Постановка ручного исключения для клуба. Причина обязательна.</summary>
public sealed record SetFeatureOverrideRequest(bool IsEnabled, string Reason);

/// <summary>Список включённых фич для клубского приложения.</summary>
public sealed record EnabledFeaturesDto(IReadOnlyList<string> Features);
```

- [ ] **Step 4: Лестница**

`src/AFK4.Platform.Api/Platform/Entitlements/IOrganizationEntitlements.cs`:

```csharp
using AFK4.Shared.Contracts.Platform.Features;

namespace AFK4.Platform.Api.Platform.Entitlements;

/// <summary>
/// Единственное место, где считается, что клубу можно. Лестница: ручное исключение для клуба →
/// мнение тарифа → умолчание фичи. Второй экземпляр этого правила разошёлся бы с первым молча.
/// </summary>
public interface IOrganizationEntitlements
{
    Task<bool> IsEnabledAsync(Guid organizationId, string featureKey, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> ListEnabledAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<IReadOnlyList<OrganizationFeatureStateDto>> DescribeAsync(Guid organizationId, CancellationToken cancellationToken);
}
```

`src/AFK4.Platform.Api/Platform/Entitlements/EfOrganizationEntitlements.cs`:

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Features;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Entitlements;

public sealed class EfOrganizationEntitlements(PlatformDbContext dbContext) : IOrganizationEntitlements
{
    public async Task<bool> IsEnabledAsync(Guid organizationId, string featureKey, CancellationToken cancellationToken)
    {
        var states = await DescribeAsync(organizationId, cancellationToken);
        var state = states.SingleOrDefault(candidate => candidate.FeatureKey == featureKey);
        // Незнакомый ключ — выключено: молчание каталога не должно открывать доступ.
        return state?.IsEnabled ?? false;
    }

    public async Task<IReadOnlyList<string>> ListEnabledAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var states = await DescribeAsync(organizationId, cancellationToken);
        return states.Where(state => state.IsEnabled).Select(state => state.FeatureKey).ToList();
    }

    public async Task<IReadOnlyList<OrganizationFeatureStateDto>> DescribeAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var planCode = await dbContext.Organizations
            .AsNoTracking()
            .Where(organization => organization.OrganizationId == organizationId)
            .Select(organization => organization.PlanCode)
            .SingleOrDefaultAsync(cancellationToken);
        if (planCode is null)
        {
            // Несуществующий клуб не получает ничего: здесь молчание значит «нет».
            return [];
        }

        var features = await dbContext.PlatformFeatures.AsNoTracking().ToListAsync(cancellationToken);
        var planOpinions = await dbContext.PlanFeatures
            .AsNoTracking()
            .Where(planFeature => planFeature.PlanCode == planCode)
            .ToDictionaryAsync(planFeature => planFeature.FeatureKey, planFeature => planFeature.IsIncluded, cancellationToken);
        var overrides = await dbContext.OrganizationFeatureOverrides
            .AsNoTracking()
            .Where(featureOverride => featureOverride.OrganizationId == organizationId)
            .ToDictionaryAsync(featureOverride => featureOverride.FeatureKey, cancellationToken);

        return features
            .OrderBy(feature => feature.FeatureKey, StringComparer.Ordinal)
            .Select(feature =>
            {
                overrides.TryGetValue(feature.FeatureKey, out var featureOverride);
                var planValue = planOpinions.TryGetValue(feature.FeatureKey, out var included)
                    ? included
                    : (bool?)null;

                var (isEnabled, level) = featureOverride is not null
                    ? (featureOverride.IsEnabled, FeatureDecisionLevels.Override)
                    : planValue is { } fromPlan
                        ? (fromPlan, FeatureDecisionLevels.Plan)
                        : (feature.EnabledByDefault, FeatureDecisionLevels.Default);

                return new OrganizationFeatureStateDto(
                    feature.FeatureKey,
                    feature.Name,
                    feature.Description,
                    isEnabled,
                    level,
                    featureOverride?.IsEnabled,
                    featureOverride?.Reason,
                    featureOverride?.SetAtUtc,
                    planValue,
                    feature.EnabledByDefault);
            })
            .ToList();
    }
}
```

Зарегистрировать в `Program.cs` рядом с `IPlanLimitGuard`:

```csharp
builder.Services.AddScoped<IOrganizationEntitlements, EfOrganizationEntitlements>();
```

- [ ] **Step 5: Прогнать тесты**

```bash
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter FullyQualifiedName~OrganizationEntitlementsTests
```

Ожидание: 7 passed.

- [ ] **Step 6: Коммит**

```bash
git add src/AFK4.Shared.Contracts/Platform/Features/FeatureContracts.cs \
        src/AFK4.Platform.Api/Platform/Entitlements/ src/AFK4.Platform.Api/Program.cs \
        tests/AFK4.Platform.Api.Tests/Platform/Entitlements/OrganizationEntitlementsTests.cs
git commit -m "feat(platform): лестница разрешений — исключение, тариф, умолчание"
```

---

### Task 3: Панель платформы — эндпоинты фич

**Files:**
- Create: `src/AFK4.Platform.Api/Endpoints/PlatformFeatureEndpoints.cs`
- Modify: `src/AFK4.Shared.Contracts/Platform/Auth/PlatformAdminPermissionNames.cs`
- Modify: `src/AFK4.Platform.Api/Platform/Identity/PlatformAdminPermissionCatalog.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/PlatformFeatureEndpointTests.cs`

**Interfaces:**
- Consumes: `IOrganizationEntitlements.DescribeAsync`, `SetFeatureOverrideRequest`.
- Produces: `PlatformAdminPermissionNames.ManageOrganizationFeatures` = `"platform.organizations.features.manage"`;
  `GET /api/platform/organizations/{organizationId:guid}/features`,
  `PUT /api/platform/organizations/{organizationId:guid}/features/{featureKey}`,
  `DELETE /api/platform/organizations/{organizationId:guid}/features/{featureKey}`.

- [ ] **Step 1: Написать падающий тест**

`tests/AFK4.Platform.Api.Tests/Platform/PlatformFeatureEndpointTests.cs`, авторизация через `PlatformAdminTestHelper.AuthorizeAsAsync` по образцу `PlatformBranchEndpointTests`:

```csharp
[Fact] public async Task Get_RequiresAuthentication() { }                    // 401 без токена
[Fact] public async Task Get_ReturnsEveryFeatureWithDecisionLevel() { }      // 200, 4 фичи, у всех level = "default"
[Fact] public async Task Put_RequiresManageFeaturesPermission() { }          // platform_support → 403
[Fact] public async Task Put_SetsOverrideAndWritesAudit() { }                // 200, состояние стало "override", запись в аудит
[Fact] public async Task Put_RejectsEmptyReason() { }                        // 400: исключение без причины через месяц не опознать
[Fact] public async Task Put_RejectsUnknownFeatureKey() { }                  // 404
[Fact] public async Task Put_ReplacesAnExistingOverride() { }                // повторный вызов не плодит вторую строку
[Fact] public async Task Delete_RemovesOverrideAndFallsBackToPlan() { }      // после снятия level снова "default", аудит записан
[Fact] public async Task Get_ReturnsNotFound_ForUnknownOrganization() { }    // 404
```

Тела написать настоящим кодом с настоящими утверждениями; проверку аудита делать так же, как это делает `PlatformBranchEndpointTests`.

- [ ] **Step 2: Прогнать тест и убедиться, что он падает**

```bash
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter FullyQualifiedName~PlatformFeatureEndpointTests
```

Ожидание: 404 на неизвестный маршрут / ошибка компиляции.

- [ ] **Step 3: Право**

В `PlatformAdminPermissionNames` добавить:

```csharp
    public const string ManageOrganizationFeatures = "platform.organizations.features.manage";
```

В `PlatformAdminPermissionCatalog` выдать его роли `PlatformAdmin` (и **не** выдавать `PlatformSupport`: включение платной фичи — коммерческое решение, а не поддержка).

- [ ] **Step 4: Эндпоинты**

`src/AFK4.Platform.Api/Endpoints/PlatformFeatureEndpoints.cs` — три маршрута. Каждый:

1. `RequirePermission` до обращения к данным: чтение — `PlatformAdminPermissionNames.ViewOrganizations`, запись и снятие — `ManageOrganizationFeatures`;
2. проверка существования организации → 404;
3. для записи — проверка, что `featureKey` есть в каталоге (`dbContext.PlatformFeatures`) → 404, и что `Reason` непустой и не длиннее 500 → 400;
4. постановка исключения — «заменить или создать»: найти строку по (`OrganizationId`, `FeatureKey`), обновить или добавить, заполнив `SetByPlatformAdminUserId` и `SetAtUtc`;
5. аудит платформы на каждое изменение и на отказ по правам — форму `AuditRecordWriteRequest` взять из `PlatformBranchEndpoints.cs`, действия добавить в `AuditActionNames`: `SetOrganizationFeatureOverride`, `ClearOrganizationFeatureOverride`;
6. ответ — свежий `DescribeAsync` для этой организации, чтобы панель не додумывала итог сама.

Зарегистрировать в `Program.cs`: `app.MapPlatformFeatureEndpoints();`

- [ ] **Step 5: Прогнать тесты**

```bash
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj
```

Ожидание: 9 новых passed, весь проект зелёный. Учти, что в проекте есть построчный тест каталога прав — если добавление права его роняет, обнови ожидание в нём, а не обходи проверку.

- [ ] **Step 6: Коммит**

```bash
git add src/AFK4.Platform.Api/Endpoints/PlatformFeatureEndpoints.cs \
        src/AFK4.Shared.Contracts/Platform/Auth/PlatformAdminPermissionNames.cs \
        src/AFK4.Platform.Api/Platform/Identity/PlatformAdminPermissionCatalog.cs \
        src/AFK4.Platform.Api/Program.cs tests/AFK4.Platform.Api.Tests/
git commit -m "feat(platform): управление фичами клуба из панели"
```

---

### Task 4: Серверные гейты и список фич для клубских приложений

**Files:**
- Create: `src/AFK4.Platform.Api/Endpoints/PlayerFeatureEndpoints.cs`
- Modify: `src/AFK4.Platform.Api/Endpoints/PlayerSelfServiceEndpoints.cs`
- Modify: `src/AFK4.Platform.Api/Endpoints/PlayerShopEndpoints.cs`
- Modify: `src/AFK4.Platform.Api/Endpoints/PlayerLoyaltyEndpoints.cs`
- Modify: `src/AFK4.Platform.Api/Loyalty/LoyaltyAccrualService.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/Entitlements/FeatureGateTests.cs`

**Interfaces:**
- Consumes: `IOrganizationEntitlements.IsEnabledAsync` / `ListEnabledAsync`, `PlatformFeatureNames`.
- Produces: `GET /api/me/features` → `EnabledFeaturesDto`; отказ выключенной фичи — 403 с телом `{ error, code: "feature_disabled", featureKey }`.

- [ ] **Step 1: Написать падающий тест**

`tests/AFK4.Platform.Api.Tests/Platform/Entitlements/FeatureGateTests.cs`:

```csharp
[Fact] public async Task Booking_Refused_WhenOnlineBookingDisabled() { }        // POST /api/me/reservations → 403, code = feature_disabled, featureKey = online_booking; брони в базе нет
[Fact] public async Task Booking_Allowed_WhenEnabled() { }                       // та же сцена при включённой фиче — бронь создаётся
[Fact] public async Task TopUp_Refused_WhenOnlineTopUpDisabled() { }             // POST /api/me/wallet/top-up-intent → 403; намерение оплаты не создано
[Fact] public async Task ShopOrder_Refused_WhenPlayerShopDisabled() { }          // POST /api/me/shop/orders → 403; заказа нет
[Fact] public async Task ShopCatalog_Refused_WhenPlayerShopDisabled() { }        // GET /api/me/shop/catalog → 403
[Fact] public async Task Loyalty_NotAccrued_WhenLoyaltyDisabled() { }            // начисление кэшбэка не создаёт проводку
[Fact] public async Task Loyalty_Accrued_WhenEnabled() { }                       // та же сцена при включённой фиче — проводка есть
[Fact] public async Task Features_ListsOnlyEnabledKeys() { }                     // GET /api/me/features
[Fact] public async Task Features_RequiresAuthentication() { }                   // 401 без токена
```

Тела написать настоящим кодом. Сцену игрока (аккаунт, токен, филиал, места, тарифы, товары) не изобретай — скопируй из ближайших существующих тестов этих эндпоинтов в `tests/AFK4.Platform.Api.Tests/`; выключение фичи делать вставкой строки `OrganizationFeatureOverrideEntity` с `IsEnabled = false`. Проверки «в базе ничего не создалось» обязательны: гейт, который отвечает 403 после записи, — не гейт.

- [ ] **Step 2: Прогнать тест и убедиться, что он падает**

```bash
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter FullyQualifiedName~FeatureGateTests
```

Ожидание: падения — сегодня все эти вызовы проходят независимо от фич.

- [ ] **Step 3: Общий помощник отказа**

В `src/AFK4.Platform.Api/Platform/Entitlements/` добавить:

```csharp
using AFK4.Shared.Contracts.Platform.Features;

namespace AFK4.Platform.Api.Platform.Entitlements;

public static class FeatureGate
{
    /// <summary>
    /// Возвращает готовый отказ, если фича выключена, и <c>null</c>, если можно.
    /// Тело несёт код и ключ фичи — фразу для человека собирает клиент.
    /// </summary>
    public static async Task<IResult?> RequireAsync(
        this IOrganizationEntitlements entitlements,
        Guid organizationId,
        string featureKey,
        CancellationToken cancellationToken)
    {
        if (await entitlements.IsEnabledAsync(organizationId, featureKey, cancellationToken))
        {
            return null;
        }

        return Results.Json(
            new { Error = "FeatureDisabled", Code = PlatformFeatureNames.DisabledCode, FeatureKey = featureKey },
            statusCode: StatusCodes.Status403Forbidden);
    }
}
```

- [ ] **Step 4: Гейты в четырёх точках**

В каждом случае проверка стоит **после** аутентификации и **до** любой записи в базу:

- `PlayerSelfServiceEndpoints.cs`, `POST /api/me/reservations` — `PlatformFeatureNames.OnlineBooking`.
- `PlayerSelfServiceEndpoints.cs`, `POST /api/me/wallet/top-up-intent` — `PlatformFeatureNames.OnlineTopUp`.
- `PlayerShopEndpoints.cs`, `GET /api/me/shop/catalog` и `POST /api/me/shop/orders` — `PlatformFeatureNames.PlayerShop`.
- `PlayerLoyaltyEndpoints.cs`, `GET /api/me/loyalty` — `PlatformFeatureNames.Loyalty`.

Идентификатор организации брать из уже разрешённого контекста игрока, а не из тела запроса.

Начисление кэшбэка — в `LoyaltyAccrualService.BuildCashbackEntryAsync`: если фича выключена, вернуть `null` (то же значение, что и при «начислять нечего»). Это ключевой случай: гейт на чтении `/api/me/loyalty` прячет экран, но деньги начисляются в другом месте, и без этой проверки выключенная лояльность продолжала бы раздавать бонусы.

- [ ] **Step 5: Список фич для приложений**

`src/AFK4.Platform.Api/Endpoints/PlayerFeatureEndpoints.cs`:

```csharp
app.MapGet("/api/me/features", async (
    PlayerAuthorizationService authorizationService,
    IOrganizationEntitlements entitlements,
    CancellationToken cancellationToken) =>
{
    var authorization = await authorizationService.RequirePlayerAsync(cancellationToken);
    if (!authorization.IsAuthenticated) return Results.Unauthorized();

    var enabled = await entitlements.ListEnabledAsync(authorization.OrganizationId, cancellationToken);
    return Results.Ok(new EnabledFeaturesDto(enabled));
});
```

Имя сервиса авторизации игрока и способ получить `OrganizationId` взять из соседних маршрутов `/api/me/*` в `PlayerSelfServiceEndpoints.cs` и повторить дословно. Зарегистрировать в `Program.cs`.

- [ ] **Step 6: Прогнать тесты**

```bash
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj
```

Ожидание: 9 новых passed, весь проект зелёный.

- [ ] **Step 7: Коммит**

```bash
git add src/AFK4.Platform.Api/ tests/AFK4.Platform.Api.Tests/
git commit -m "feat(platform): серверные гейты фич и список включённых для приложений"
```

---

### Task 5: Панель — вкладка «Фичи» в паспорте клуба

**Files:**
- Create: `src/AFK4.PlatformControl.Web/src/platform/organizations/OrganizationFeaturesTab.tsx`
- Create: `src/AFK4.PlatformControl.Web/src/platform/organizations/OrganizationFeaturesTab.test.tsx`
- Create: `src/AFK4.PlatformControl.Web/src/api/platformClients/features.ts`
- Modify: `src/AFK4.PlatformControl.Web/src/api/types.ts`
- Modify: `src/AFK4.PlatformControl.Web/src/routing/platformRoute.ts`
- Modify: `src/AFK4.PlatformControl.Web/src/platform/organizations/OrganizationPage.tsx`
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json`

**Interfaces:**
- Consumes: `GET/PUT/DELETE /api/platform/organizations/{id}/features[/{key}]`, `OrganizationFeatureStateDto`.
- Produces: вкладка `'features'` в `OrganizationTab`.

- [ ] **Step 1: Написать падающие тесты**

`OrganizationFeaturesTab.test.tsx`:

```tsx
it('показывает для каждой фичи, чем решено', async () => {
  // Фича с level 'default' → подпись «по умолчанию»; с level 'plan' → «тариф»;
  // с level 'override' → «вручную» вместе с причиной.
});

it('ставит исключение с причиной', async () => {
  // Переключатель + поле причины → вызван setOverride(featureKey, { isEnabled, reason }).
});

it('не даёт поставить исключение без причины', async () => {
  // Кнопка недоступна при пустой причине; setOverride не вызывался.
});

it('снимает исключение и возвращает решение тарифу', async () => {
  // Кнопка «Вернуть как у тарифа» → вызван clearOverride(featureKey).
});
```

Тела написать настоящим кодом по образцу `OrganizationDynamicsTab.test.tsx` (фейковый клиент, обёртка `I18nProvider`).

- [ ] **Step 2: Прогнать тесты и убедиться, что они падают**

```bash
cd /home/fedya/projects/afk4.net/src/AFK4.PlatformControl.Web && ~/.bun/bin/bun test src/platform/organizations/OrganizationFeaturesTab.test.tsx
```

Ожидание: FAIL — компонента нет.

- [ ] **Step 3: Строки**

В `locales/ru.json` добавить (в алфавитном порядке среди соседних `platform.organization.*`), в `en.json` и `tg.json` — честные переводы, таджикский по-таджикски:

```json
"platform.organization.features.tab": "Фичи",
"platform.organization.features.enabled": "Включена",
"platform.organization.features.disabled": "Выключена",
"platform.organization.features.byDefault": "по умолчанию",
"platform.organization.features.byPlan": "тариф {planCode}",
"platform.organization.features.byOverride": "вручную",
"platform.organization.features.reason": "Причина",
"platform.organization.features.reasonRequired": "Укажите причину — через месяц никто не вспомнит, зачем это включили.",
"platform.organization.features.set": "Применить",
"platform.organization.features.clear": "Вернуть как у тарифа",
"platform.organization.features.updated": "Доступ к фиче изменён"
```

Перегенерировать каталоги:

```bash
cd /home/fedya/projects/afk4.net/packages/i18n && ~/.bun/bin/bun run gen
```

- [ ] **Step 4: Клиент API и типы**

В `src/api/types.ts`:

```ts
export interface OrganizationFeatureState {
  featureKey: string;
  name: string;
  description: string;
  isEnabled: boolean;
  decisionLevel: 'override' | 'plan' | 'default';
  overrideValue: boolean | null;
  overrideReason: string | null;
  overrideSetAtUtc: string | null;
  planValue: boolean | null;
  defaultValue: boolean;
}
```

В `src/api/platformClients/features.ts` — три метода (`listFeatures`, `setOverride`, `clearOverride`) по образцу соседнего `branchDynamics.ts`.

- [ ] **Step 5: Вкладка**

`OrganizationFeaturesTab.tsx` — список фич; у каждой имя, описание, состояние и **подпись, чем решено**: `default` → «по умолчанию», `plan` → «тариф growth», `override` → «вручную» плюс причина и дата. Действия: переключатель с обязательным полем причины (кнопка «Применить» недоступна при пустой причине) и «Вернуть как у тарифа», видимая только когда исключение стоит.

Разметку и компоненты брать из соседних вкладок паспорта, свой стиль не привносить.

Добавить `'features'` в `OrganizationTab` и `ORGANIZATION_TABS` в `routing/platformRoute.ts` и в `TABS` в `OrganizationPage.tsx` — так же, как это сделано для вкладки `'dynamics'`.

- [ ] **Step 6: Прогнать тесты и сборку**

```bash
cd /home/fedya/projects/afk4.net/src/AFK4.PlatformControl.Web && ~/.bun/bin/bun test && ~/.bun/bin/bun run build
cd /home/fedya/projects/afk4.net/packages/i18n && ~/.bun/bin/bun test
```

- [ ] **Step 7: Коммит**

```bash
git add src/AFK4.PlatformControl.Web/src/ locales/ packages/i18n/src/
git commit -m "feat(platform-control): вкладка «Фичи» в паспорте клуба"
```

---

### Task 6: Личный кабинет игрока — скрыть выключенное

**Files:**
- Modify: `src/AFK4.Customer.Web/src/api/playerApi.ts`
- Modify: `src/AFK4.Customer.Web/src/api/types.ts`
- Modify: `src/AFK4.Customer.Web/src/App.tsx`
- Modify: `src/AFK4.Customer.Web/src/components/BottomNav.tsx`
- Modify: `src/AFK4.Customer.Web/src/screens/wallet/WalletPanel.tsx`
- Test: соответствующие `*.test.tsx` рядом с изменёнными файлами

**Interfaces:**
- Consumes: `GET /api/me/features` → `{ features: string[] }`.
- Produces: `playerApi.getFeatures(): Promise<string[]>`.

- [ ] **Step 1: Написать падающие тесты**

```tsx
it('прячет вкладку «Брони», когда онлайн-бронирование выключено', async () => {
  // getFeatures вернул список без 'online_booking' → в нижней навигации три пункта, «Броней» нет.
});

it('показывает вкладку «Брони», когда фича включена', async () => { });

it('прячет пополнение кошелька, когда онлайн-пополнение выключено', async () => {
  // В WalletPanel нет кнопки пополнения.
});

it('не роняет экран, если список фич не пришёл', async () => {
  // getFeatures отклонён → приложение рисуется; выбор поведения зафиксируй в тесте явно.
});
```

Последний тест требует решения, и оно принимается здесь: **при недоступности списка фичи считаются включёнными**. Обоснование — это интерфейс, а не защита: сервер всё равно откажет, если фича выключена, а спрятать работающий раздел из-за сетевого сбоя хуже, чем показать лишний. Зафиксируй это комментарием в коде рядом с обработкой ошибки.

Тела написать настоящим кодом по образцу существующих тестов этих файлов.

- [ ] **Step 2: Прогнать тесты и убедиться, что они падают**

```bash
cd /home/fedya/projects/afk4.net/src/AFK4.Customer.Web && ~/.bun/bin/bun test
```

- [ ] **Step 3: Клиент и проброс**

В `playerApi.ts` добавить метод по образцу соседних:

```ts
async getFeatures(): Promise<string[]> {
  const response = await this.authedGet<{ features: string[] }>('/api/me/features');
  return response.features;
}
```

Загрузить список один раз при входе в приложение (`App.tsx`), хранить в состоянии и передавать вниз. Вкладку «Брони» убирать из `TABS` в `BottomNav.tsx`, когда `online_booking` не входит в список; кнопку пополнения в `WalletPanel.tsx` — когда нет `online_topup`.

Если активная вкладка оказалась скрытой, переключиться на `dashboard`: экран без входа в навигации — тупик.

- [ ] **Step 4: Прогнать тесты и сборку**

```bash
cd /home/fedya/projects/afk4.net/src/AFK4.Customer.Web && ~/.bun/bin/bun test && ~/.bun/bin/bun run build
```

- [ ] **Step 5: Коммит**

```bash
git add src/AFK4.Customer.Web/src/
git commit -m "feat(customer): скрывать выключенные фичи в личном кабинете"
```

---

### Task 7: Оболочка на игровой машине — скрыть выключенное

**Files:**
- Modify: `src/AFK4.Player.Shell.Web/src/shellApi.ts`
- Modify: `src/AFK4.Player.Shell.Web/src/screens/SelfServiceMenu.tsx`
- Modify: `src/AFK4.Player.Shell.Web/src/App.tsx`
- Test: `src/AFK4.Player.Shell.Web/src/screens/SelfServiceMenu.test.tsx` и рядом

**Interfaces:**
- Consumes: `GET /api/me/features`.
- Produces: `shellApi.getFeatures()`.

- [ ] **Step 1: Написать падающие тесты**

```tsx
it('прячет пункт «Магазин», когда магазин выключен', async () => { });
it('прячет пункт «Бонусы», когда лояльность выключена', async () => { });
it('прячет пункт «Пополнить», когда онлайн-пополнение выключено', async () => { });
it('показывает все пункты, когда включено всё', async () => { });
it('показывает все пункты, если список фич не пришёл', async () => { });
```

Последний — то же решение, что и в задаче 6: при недоступности списка считаем фичи включёнными, потому что это интерфейс, а не защита; зафиксируй комментарием в коде.

Тела написать настоящим кодом по образцу существующих тестов оболочки.

- [ ] **Step 2: Прогнать тесты и убедиться, что они падают**

```bash
cd /home/fedya/projects/afk4.net/src/AFK4.Player.Shell.Web && ~/.bun/bin/bun test
```

- [ ] **Step 3: Реализация**

В `shellApi.ts` добавить `getFeatures: () => call<{ features: string[] }>('/api/me/features')` по форме соседних вызовов. Загрузить один раз, хранить в состоянии `App.tsx`, скрывать пункты меню `SelfServiceMenu.tsx` по ключам `player_shop`, `loyalty`, `online_topup`.

Если оболочка умеет открывать скрытый экран напрямую (по маршруту или команде), закрой и этот вход — раздел, до которого можно дойти в обход спрятанного пункта меню, обесценивает скрытие.

- [ ] **Step 4: Прогнать тесты и сборку**

```bash
cd /home/fedya/projects/afk4.net/src/AFK4.Player.Shell.Web && ~/.bun/bin/bun test && ~/.bun/bin/bun run build
```

- [ ] **Step 5: Коммит**

```bash
git add src/AFK4.Player.Shell.Web/src/
git commit -m "feat(player-shell): скрывать выключенные фичи в оболочке"
```

---

### Task 8: Оператор — скрыть настройку выключенной лояльности

**Files:**
- Create: `src/AFK4.OrganizationAdmin.Web/src/api/clients/features.ts`
- Modify: `src/AFK4.OrganizationAdmin.Web/src/management/destinations/PaymentsLoyaltyDestination.tsx` (внутри — `payments/LoyaltySection.tsx`)
- Test: `src/AFK4.OrganizationAdmin.Web/src/management/destinations/PaymentsLoyaltyDestination.test.tsx`

**Interfaces:**
- Consumes: `GET /api/me/features` не подходит — Оператор аутентифицирован как сотрудник, не как игрок. Использовать `GET /api/platform/…` тоже нельзя: это права платформы. **Добавь `GET /api/organizations/{organizationId:guid}/features`** с проверкой `StaffAuthorizationService` и организацией из контекста сотрудника (не из маршрута — сверь совпадение, как это делают соседние org-эндпоинты, иначе появится IDOR).

- [ ] **Step 1: Написать падающие тесты**

Бэкенд, в `tests/AFK4.Platform.Api.Tests/Platform/Entitlements/FeatureGateTests.cs` (или соседнем файле):

```csharp
[Fact] public async Task StaffFeatures_ReturnsEnabledKeys() { }
[Fact] public async Task StaffFeatures_RequiresAuthentication() { }
[Fact] public async Task StaffFeatures_RefusesAnotherOrganization() { }   // 403, не 200 с чужими данными
```

Фронт:

```tsx
it('прячет раздел «Лояльность», когда фича выключена', async () => { });
it('показывает раздел «Лояльность», когда фича включена', async () => { });
```

Тела написать настоящим кодом.

- [ ] **Step 2: Прогнать тесты и убедиться, что они падают**

```bash
cd /home/fedya/projects/afk4.net && dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter FullyQualifiedName~StaffFeatures
cd src/AFK4.OrganizationAdmin.Web && ~/.bun/bin/bun test
```

- [ ] **Step 3: Реализация**

Добавить эндпоинт для сотрудника в `PlayerFeatureEndpoints.cs` (переименовав файл, если имя перестало соответствовать содержимому — например в `FeatureEndpoints.cs`), с проверкой совпадения организации маршрута и контекста сотрудника. На фронте Оператора загрузить список один раз и не отрисовывать `LoyaltySection` внутри `PaymentsLoyaltyDestination` при отсутствии ключа `loyalty` — раздел платежей остаётся на месте, исчезает только блок лояльности. Поведение при недоступности списка — то же, что в задачах 6 и 7: считаем включённым.

- [ ] **Step 4: Прогнать тесты и сборку**

```bash
cd /home/fedya/projects/afk4.net && dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj
cd src/AFK4.OrganizationAdmin.Web && ~/.bun/bin/bun test && ~/.bun/bin/bun run build
```

- [ ] **Step 5: Коммит**

```bash
git add src/AFK4.Platform.Api/ src/AFK4.OrganizationAdmin.Web/src/ tests/AFK4.Platform.Api.Tests/
git commit -m "feat(operator): скрывать настройку лояльности, когда фича выключена"
```

---

## Финальная проверка перед завершением ветки

```bash
cd /home/fedya/projects/afk4.net
dotnet build AFK4.sln
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj
for app in PlatformControl.Web Customer.Web Player.Shell.Web OrganizationAdmin.Web; do
  (cd "src/AFK4.$app" && ~/.bun/bin/bun test && ~/.bun/bin/bun run build)
done
(cd packages/i18n && ~/.bun/bin/bun test)
```

Прогон бэкенда с настоящим Postgres требует **четырёх** переменных подключения — `AFK4_PLATFORM_ADMIN_POSTGRES_TEST_CONNECTION_STRING`, `AFK4_RESERVATION_POSTGRES_TEST_CONNECTION_STRING`, `AFK4_POS_POSTGRES_TEST_CONNECTION_STRING`, `AFK4_COMMERCE_TEST_POSTGRES` — плюс `AFK4_REQUIRE_POSTGRES_TESTS=1`. С одной переменной часть тестов останется пропущенной.

## Отклонения от спеки (§1) и почему

- **`plan_features` стартует пустой, все фичи включены у всех.** Спека описывает механизм, но не говорит, какие фичи входят в какой тариф — это коммерческое решение, которого никто не принимал. Придумать раскладку означало бы молча изменить поведение продукта под видом инфраструктуры. Пустая таблица = сегодняшнее поведение в точности; продать фичу можно одной строкой в панели.
- **Право `ManageOrganizationFeatures` не выдаётся роли `platform_support`.** Включение платной фичи — коммерческое решение, а не действие поддержки; в спеке разграничение не оговорено.
- **При недоступности списка фич интерфейс считает их включёнными.** Это интерфейс, а не защита: сервер всё равно откажет. Спрятать работающий раздел из-за сетевого сбоя хуже, чем показать лишний.
- **Добавлен эндпоинт списка фич для сотрудника** (задача 8): Оператор аутентифицирован как сотрудник, `/api/me/features` для игрока ему не подходит, а платформенный маршрут требует чужих прав.
- **Гейт лояльности стоит и на начислении, а не только на чтении.** Спека называет фичу целиком; без проверки в `LoyaltyAccrualService` выключенная лояльность продолжала бы раздавать бонусы, а экран бы просто исчез — то есть выключение было бы враньём.
