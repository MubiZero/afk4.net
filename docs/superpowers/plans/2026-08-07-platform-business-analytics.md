# Бизнес-аналитика платформы — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** На вопрос «мы растём?» появляется ответ: выручка по месяцам, приход и отток клубов, средний чек.

**Architecture:** Выручка по месяцам считается **из выставленных счетов** — у счёта есть вид, период и сумма, поэтому годовой план раскладывается на свои двенадцать месяцев, а цифра всегда сходится с тем, что реально выставлено. Состояние подписки счетами не восстановить (клуб, ушедший в июне, сегодня в базе неотличим от того, кто не платил никогда), поэтому суточное задание пишет по строке на организацию в `subscription_daily_snapshots` — и приход с оттоком считаются только из них.

**Tech Stack:** .NET 10 minimal APIs, EF Core 10 / Npgsql, xUnit, React 19 + TypeScript, `bun test`, `recharts` (уже в зависимостях панели), `@afk4/i18n` (ICU MessageFormat).

**Спека:** `docs/superpowers/specs/2026-08-07-platform-observability-and-analytics-design.md`, раздел §2.
**Ветка:** `feat/platform-analytics-wave-c` (создана от `main` после мержа плана 1).

## Решение по размещению экрана

Аналитика — **четвёртая вкладка раздела «Деньги»** (`/admin/money?tab=analytics`), а не новый пункт рейла. Причина: то же право `platform.billing.view`, тот же предмет разговора (деньги), а шестой пункт в рейле из пяти удорожает навигацию ради одного экрана. Спека говорит «экран „Аналитика“ под правом на биллинг» и этому не противоречит.

## Global Constraints

- **Деньги — в минорных единицах**, конвертация в мажорные только на границе интерфейса (`minorToMajor`), формат — через `@afk4/money`. Валюта TJS.
- **Сервер отдаёт числа и коды, не готовые пользовательские строки.** Названия месяцев, подписи и множественное число — на клиенте, через `packages/i18n`, множественное число ICU-плюралами.
- **Ни одного запроса в цикле по сущностям.** Фиксированное число запросов, группировка в памяти — образец `EfPlatformPulseService`.
- **Идемпотентность суточного задания — уникальным ключом в БД** (организация, дата), а не проверкой «а не запускались ли мы уже»: двойной запуск и повтор после падения это норма.
- **Новые таблицы — snake_case** (`subscription_daily_snapshots`), колонки PascalCase в кавычках; сырой SQL миграций сверять с `PlatformDbContext`, а не с именем C#-класса.
- **Право проверяется на сервере до обращения к данным.** Экран, не получивший данных, показывает ошибку с повтором, а не пустой график, выглядящий как «выручки нет».
- **Периодическое задание наследует `PlatformPeriodicJob`** (`src/AFK4.Platform.Api/Platform/Health/`) — прямое наследование `BackgroundService` запрещено архитектурным тестом `PeriodicJobRegistrationTests`. Имя задания добавляется в `PlatformJobNames`, в его список `Watched` и в `PlatformJobIntervalCatalog`, иначе задание выпадет из наблюдения (проверяется тестом `JobIntervals_CoverEveryWatchedJob`), а на экране «Здоровье» появится дыра.
- **`bun` вызывать полным путём:** `BUN=/home/fedya/.bun/bin/bun`.
- Никаких секретов в коде, выводе и логах. Никаких AI-подписей в коммитах, коде и комментариях.

## Существующий код, на который опирается план

- `InvoiceEntity` (`src/AFK4.Platform.Api/Data/`): `Kind`, `PeriodStartUtc`, `PeriodEndUtc`, `IssuedAtUtc`, `AmountMinorUnits`, `CurrencyCode`, `Status`, `VoidedAtUtc`.
- `InvoiceKindNames`: `subscription`, `proration`, `one_off` (и `credit_note`, если объявлен — сверить файл).
- `InvoiceStatusNames`: `issued`, `paid`, `void`, `overdue`.
- `OrganizationSubscriptionEntity`: `OrganizationId`, `PlanCode`, `Status`, `AmountMinorUnits`, `CurrencyCode`, `BillingInterval`, `DiscountPercent`, `DiscountAmountMinorUnits`, `DiscountUntilUtc`.
- `SubscriptionStatusNames`: `trial`, `active`, `past_due`, `cancelled`. `BillingIntervalNames`: `monthly`, `yearly`.
- `SubscriptionDiscount.Apply(long grossMinorUnits, int? percent, long? fixedAmountMinorUnits)` в `src/AFK4.Platform.Api/Platform/Billing/`.
- `EfBillingMetricsService` — существующие сводные метрики «сейчас»; эта работа его НЕ заменяет и НЕ переписывает.

---

### Task 1: Таблица суточных снимков подписок

**Files:**
- Create: `src/AFK4.Platform.Api/Data/SubscriptionDailySnapshotEntity.cs`
- Modify: `src/AFK4.Platform.Api/Data/PlatformDbContext.cs` (DbSet рядом с `PlatformIncidents`; конфигурация рядом с блоком `platform_incidents` в `OnModelCreating`)
- Create: миграция в `src/AFK4.Platform.Api/Data/Migrations/`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/SubscriptionSnapshotStoreTests.cs`

**Interfaces:**
- Produces: `SubscriptionDailySnapshotEntity`, `PlatformDbContext.SubscriptionDailySnapshots`.

- [ ] **Step 1: Написать сущность**

`src/AFK4.Platform.Api/Data/SubscriptionDailySnapshotEntity.cs`:

```csharp
namespace AFK4.Platform.Api.Data;

/// <summary>
/// Состояние подписки организации на конец суток. Заводится потому, что состояние — не событие:
/// подписка хранит только сегодняшний статус, и клуб, ушедший в июне, сегодня в базе неотличим
/// от того, кто не платил никогда. Приход и отток считаются только отсюда.
/// </summary>
public sealed class SubscriptionDailySnapshotEntity
{
    public Guid SubscriptionDailySnapshotId { get; set; }

    public Guid OrganizationId { get; set; }

    /// <summary>Сутки в UTC, к которым относится снимок (без времени).</summary>
    public DateOnly SnapshotDate { get; set; }

    public string Status { get; set; } = string.Empty;

    public string PlanCode { get; set; } = string.Empty;

    /// <summary>Цена, приведённая к месяцу и с учётом действующей скидки — то, что клуб реально платит.</summary>
    public long MonthlyAmountMinorUnits { get; set; }

    public string CurrencyCode { get; set; } = "TJS";

    public DateTimeOffset CreatedAtUtc { get; set; }
}
```

- [ ] **Step 2: Зарегистрировать в PlatformDbContext**

Рядом с `PlatformIncidents`/`PlatformJobRuns`:

```csharp
    public DbSet<SubscriptionDailySnapshotEntity> SubscriptionDailySnapshots => Set<SubscriptionDailySnapshotEntity>();
```

В `OnModelCreating` рядом с блоком `platform_incidents`:

```csharp
        modelBuilder.Entity<SubscriptionDailySnapshotEntity>(entity =>
        {
            entity.ToTable("subscription_daily_snapshots");
            entity.HasKey(snapshot => snapshot.SubscriptionDailySnapshotId);
            entity.Property(snapshot => snapshot.Status).HasMaxLength(32).IsRequired();
            entity.Property(snapshot => snapshot.PlanCode).HasMaxLength(64).IsRequired();
            entity.Property(snapshot => snapshot.CurrencyCode).HasMaxLength(3).IsRequired();
            // Идемпотентность суточного задания держит база: повторный запуск за те же сутки
            // не заведёт вторую строку, а двойной запуск и повтор после падения — норма.
            entity.HasIndex(snapshot => new { snapshot.OrganizationId, snapshot.SnapshotDate })
                .IsUnique()
                .HasDatabaseName("IX_subscription_daily_snapshots_Organization_Date");
            entity.HasIndex(snapshot => snapshot.SnapshotDate);
        });
```

- [ ] **Step 3: Написать тест**

`tests/AFK4.Platform.Api.Tests/Platform/SubscriptionSnapshotStoreTests.cs`:

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class SubscriptionSnapshotStoreTests
{
    [Fact]
    public async Task Snapshot_RoundTrips()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.SubscriptionDailySnapshots.Add(new SubscriptionDailySnapshotEntity
        {
            SubscriptionDailySnapshotId = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            SnapshotDate = new DateOnly(2026, 8, 7),
            Status = SubscriptionStatusNames.Active,
            PlanCode = "pro",
            MonthlyAmountMinorUnits = 290000,
            CurrencyCode = "TJS",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var stored = await db.SubscriptionDailySnapshots.SingleAsync();
        Assert.Equal(new DateOnly(2026, 8, 7), stored.SnapshotDate);
        Assert.Equal(290000, stored.MonthlyAmountMinorUnits);
    }
}
```

- [ ] **Step 4: Прогнать тест**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter SubscriptionSnapshotStoreTests`
Expected: PASS после шагов 1-2 (до них — ошибка компиляции: `SubscriptionDailySnapshots` не существует).

- [ ] **Step 5: Собрать и создать миграцию**

Порядок обязателен — `--no-build` берёт последнюю сборку, без свежего build миграция выйдет пустой:

```bash
dotnet build src/AFK4.Platform.Api
dotnet ef migrations add AddSubscriptionDailySnapshots \
  --project src/AFK4.Platform.Api --output-dir Data/Migrations --no-build
```

Открыть сгенерированный `.cs` и проверить: `Up` не пустой, создаёт `subscription_daily_snapshots`, уникальный индекс называется `IX_subscription_daily_snapshots_Organization_Date`. Если `Up` пуст — удалить `.cs` и `.Designer.cs`, пересобрать, повторить (`dotnet ef migrations remove` требует живой БД и здесь не сработает).

- [ ] **Step 6: Прогнать тест повторно и закоммитить**

```bash
dotnet test tests/AFK4.Platform.Api.Tests --filter SubscriptionSnapshotStoreTests
git add src/AFK4.Platform.Api tests/AFK4.Platform.Api.Tests
git commit -m "feat(platform): таблица суточных снимков подписок"
```

---

### Task 2: Суточное задание снимков

**Files:**
- Create: `src/AFK4.Platform.Api/Platform/Analytics/ISubscriptionSnapshotRunner.cs`
- Create: `src/AFK4.Platform.Api/Platform/Analytics/EfSubscriptionSnapshotRunner.cs`
- Create: `src/AFK4.Platform.Api/Platform/Analytics/SubscriptionSnapshotJob.cs`
- Create: `src/AFK4.Platform.Api/Platform/Analytics/PlatformAnalyticsOptions.cs`
- Modify: `src/AFK4.Platform.Api/Platform/Health/PlatformJobNames.cs` (константа + `Watched`)
- Modify: `src/AFK4.Platform.Api/Platform/Health/PlatformJobIntervalCatalog.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/SubscriptionSnapshotRunnerTests.cs`

**Interfaces:**
- Consumes: `SubscriptionDailySnapshotEntity` (Task 1); `PlatformPeriodicJob`, `PlatformJobNames`, `PlatformJobIntervalCatalog` из `src/AFK4.Platform.Api/Platform/Health/`.
- Produces: `ISubscriptionSnapshotRunner.RunAsync(DateTimeOffset now, CancellationToken) -> Task<int>` (возвращает число записанных снимков); `PlatformJobNames.SubscriptionSnapshots = "subscription_snapshots"`.

- [ ] **Step 1: Написать опции**

`src/AFK4.Platform.Api/Platform/Analytics/PlatformAnalyticsOptions.cs`:

```csharp
namespace AFK4.Platform.Api.Platform.Analytics;

public sealed class PlatformAnalyticsOptions
{
    public const string ConfigurationSection = "Analytics";

    /// <summary>
    /// Как часто задание проверяет, есть ли снимок за прошедшие сутки. Чаще суток — не вред:
    /// уникальный ключ (организация, дата) делает повтор безобидным, а частый тик означает,
    /// что снимок появится вскоре после перезапуска процесса, а не через сутки.
    /// </summary>
    public TimeSpan SnapshotInterval { get; set; } = TimeSpan.FromHours(1);
}
```

- [ ] **Step 2: Написать падающий тест**

`tests/AFK4.Platform.Api.Tests/Platform/SubscriptionSnapshotRunnerTests.cs`:

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Analytics;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class SubscriptionSnapshotRunnerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 7, 3, 0, 0, TimeSpan.Zero);

    private static async Task<Guid> SeedSubscriptionAsync(
        PlatformDbContext db, string status, long amount, string interval, int? discountPercent = null)
    {
        var organizationId = Guid.NewGuid();
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = organizationId,
            Slug = "club-" + organizationId.ToString("N")[..8],
            Name = "Club",
            Status = OrganizationStatusNames.Active,
            CreatedAtUtc = Now.AddMonths(-3),
            UpdatedAtUtc = Now.AddMonths(-3)
        });
        db.OrganizationSubscriptions.Add(new OrganizationSubscriptionEntity
        {
            OrganizationSubscriptionId = Guid.NewGuid(),
            OrganizationId = organizationId,
            PlanCode = "pro",
            Status = status,
            CurrentPeriodStartUtc = Now.AddDays(-10),
            CurrentPeriodEndUtc = Now.AddDays(20),
            AmountMinorUnits = amount,
            CurrencyCode = "TJS",
            BillingInterval = interval,
            DiscountPercent = discountPercent,
            DiscountUntilUtc = discountPercent is null ? null : Now.AddMonths(6),
            CreatedAtUtc = Now.AddMonths(-3),
            UpdatedAtUtc = Now.AddMonths(-3)
        });
        await db.SaveChangesAsync();
        return organizationId;
    }

    // Поля OrganizationEntity сверить с фактической сущностью: если Slug/Status называются иначе,
    // использовать фактические имена, а не эти.

    [Fact]
    public async Task Run_WritesOneSnapshotPerOrganizationForYesterday()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await SeedSubscriptionAsync(db, SubscriptionStatusNames.Active, 290000, BillingIntervalNames.Monthly);
        var runner = scope.ServiceProvider.GetRequiredService<ISubscriptionSnapshotRunner>();

        var written = await runner.RunAsync(Now, CancellationToken.None);

        Assert.Equal(1, written);
        var snapshot = await db.SubscriptionDailySnapshots.SingleAsync();
        Assert.Equal(new DateOnly(2026, 8, 6), snapshot.SnapshotDate);
        Assert.Equal(SubscriptionStatusNames.Active, snapshot.Status);
        Assert.Equal(290000, snapshot.MonthlyAmountMinorUnits);
    }

    [Fact]
    public async Task Run_Twice_DoesNotDuplicateSnapshot()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await SeedSubscriptionAsync(db, SubscriptionStatusNames.Active, 290000, BillingIntervalNames.Monthly);
        var runner = scope.ServiceProvider.GetRequiredService<ISubscriptionSnapshotRunner>();

        await runner.RunAsync(Now, CancellationToken.None);
        var secondRun = await runner.RunAsync(Now, CancellationToken.None);

        Assert.Equal(0, secondRun);
        Assert.Equal(1, await db.SubscriptionDailySnapshots.CountAsync());
    }

    [Fact]
    public async Task Run_NormalizesYearlyPlanToMonthlyAmount()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await SeedSubscriptionAsync(db, SubscriptionStatusNames.Active, 3480000, BillingIntervalNames.Yearly);
        var runner = scope.ServiceProvider.GetRequiredService<ISubscriptionSnapshotRunner>();

        await runner.RunAsync(Now, CancellationToken.None);

        var snapshot = await db.SubscriptionDailySnapshots.SingleAsync();
        Assert.Equal(290000, snapshot.MonthlyAmountMinorUnits);
    }

    [Fact]
    public async Task Run_AppliesActiveDiscount()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await SeedSubscriptionAsync(db, SubscriptionStatusNames.Active, 300000, BillingIntervalNames.Monthly, discountPercent: 10);
        var runner = scope.ServiceProvider.GetRequiredService<ISubscriptionSnapshotRunner>();

        await runner.RunAsync(Now, CancellationToken.None);

        var snapshot = await db.SubscriptionDailySnapshots.SingleAsync();
        Assert.Equal(270000, snapshot.MonthlyAmountMinorUnits);
    }

    [Fact]
    public async Task Run_BackfillsMissedDay()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await SeedSubscriptionAsync(db, SubscriptionStatusNames.Active, 290000, BillingIntervalNames.Monthly);
        var runner = scope.ServiceProvider.GetRequiredService<ISubscriptionSnapshotRunner>();

        // Процесс стоял двое суток: снимок за пропущенный день всё равно должен появиться,
        // иначе в графике оттока навсегда останется дыра.
        await runner.RunAsync(Now.AddDays(-1), CancellationToken.None);
        var written = await runner.RunAsync(Now, CancellationToken.None);

        Assert.Equal(1, written);
        var dates = await db.SubscriptionDailySnapshots.Select(s => s.SnapshotDate).OrderBy(d => d).ToListAsync();
        Assert.Equal([new DateOnly(2026, 8, 5), new DateOnly(2026, 8, 6)], dates);
    }
}
```

- [ ] **Step 3: Прогнать — убедиться, что падает**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter SubscriptionSnapshotRunnerTests`
Expected: FAIL — `ISubscriptionSnapshotRunner` не существует.

- [ ] **Step 4: Написать раннер**

`src/AFK4.Platform.Api/Platform/Analytics/ISubscriptionSnapshotRunner.cs`:

```csharp
namespace AFK4.Platform.Api.Platform.Analytics;

public interface ISubscriptionSnapshotRunner
{
    /// <summary>Дописывает недостающие суточные снимки вплоть до вчерашнего дня. Возвращает число записанных строк.</summary>
    Task<int> RunAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
```

`src/AFK4.Platform.Api/Platform/Analytics/EfSubscriptionSnapshotRunner.cs`:

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Billing;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Analytics;

/// <summary>
/// Пишет снимок состояния подписок за прошедшие сутки. Снимается ВЧЕРАШНИЙ день, а не сегодняшний:
/// сутки должны кончиться, прежде чем про них можно сказать что-то окончательное.
/// </summary>
public sealed class EfSubscriptionSnapshotRunner(PlatformDbContext dbContext, TimeProvider timeProvider)
    : ISubscriptionSnapshotRunner
{
    /// <summary>Насколько глубоко задание готово доснять пропущенные дни после долгого простоя.</summary>
    private const int MaxBackfillDays = 30;

    public async Task<int> RunAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var lastCompleteDay = DateOnly.FromDateTime(now.UtcDateTime).AddDays(-1);
        var earliest = lastCompleteDay.AddDays(-MaxBackfillDays);

        // Отталкиваемся от ПОСЛЕДНЕЙ известной даты каждой организации, а не от фиксированного окна:
        // иначе организация без единого снимка получила бы при первом запуске сразу весь диапазон
        // выдуманной истории вместо одного вчерашнего дня.
        var lastSnapshotDates = await dbContext.SubscriptionDailySnapshots
            .AsNoTracking()
            .GroupBy(snapshot => snapshot.OrganizationId)
            .Select(group => new { OrganizationId = group.Key, LastDate = group.Max(snapshot => snapshot.SnapshotDate) })
            .ToDictionaryAsync(row => row.OrganizationId, row => row.LastDate, cancellationToken);

        var subscriptions = await dbContext.OrganizationSubscriptions
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var written = 0;
        var createdAt = timeProvider.GetUtcNow();

        foreach (var subscription in subscriptions)
        {
            // Нет истории — пишем только вчера. Есть история — закрываем разрыв от следующего дня
            // после последнего снимка, но не глубже MaxBackfillDays.
            var startDay = lastSnapshotDates.TryGetValue(subscription.OrganizationId, out var lastDate)
                ? lastDate.AddDays(1)
                : lastCompleteDay;
            if (startDay < earliest) startDay = earliest;

            for (var day = startDay; day <= lastCompleteDay; day = day.AddDays(1))
            {
                // Досняли пропущенный день — но состояние берём СЕГОДНЯШНЕЕ: восстановить, каким
                // оно было позавчера, нечем. Это честная цена простоя, а не точная реконструкция.
                dbContext.SubscriptionDailySnapshots.Add(new SubscriptionDailySnapshotEntity
                {
                    SubscriptionDailySnapshotId = Guid.NewGuid(),
                    OrganizationId = subscription.OrganizationId,
                    SnapshotDate = day,
                    Status = subscription.Status,
                    PlanCode = subscription.PlanCode,
                    MonthlyAmountMinorUnits = MonthlyAmount(subscription, createdAt),
                    CurrencyCode = subscription.CurrencyCode,
                    CreatedAtUtc = createdAt
                });
                written++;
            }
        }

        if (written > 0) await dbContext.SaveChangesAsync(cancellationToken);
        return written;
    }

    private static long MonthlyAmount(OrganizationSubscriptionEntity subscription, DateTimeOffset now)
    {
        var gross = subscription.BillingInterval == BillingIntervalNames.Yearly
            ? subscription.AmountMinorUnits / 12
            : subscription.AmountMinorUnits;

        var discountApplies = subscription.DiscountUntilUtc is null || subscription.DiscountUntilUtc > now;
        if (!discountApplies) return gross;

        // Фиксированная скидка задана на период выставления; у годового плана её тоже надо
        // привести к месяцу, иначе месячная цена уедет в минус на порядок.
        var fixedDiscount = subscription.DiscountAmountMinorUnits is { } amount
            ? (subscription.BillingInterval == BillingIntervalNames.Yearly ? amount / 12 : amount)
            : (long?)null;

        return gross - SubscriptionDiscount.Apply(gross, subscription.DiscountPercent, fixedDiscount);
    }
}
```

Сверить фактическую сигнатуру `SubscriptionDiscount.Apply` в `src/AFK4.Platform.Api/Platform/Billing/SubscriptionDiscount.cs`: он возвращает **размер скидки**, а не итоговую сумму. Если сигнатура иная — использовать фактическую и отметить в отчёте.

- [ ] **Step 5: Написать задание**

`src/AFK4.Platform.Api/Platform/Analytics/SubscriptionSnapshotJob.cs`:

```csharp
using AFK4.Platform.Api.Platform.Health;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Platform.Analytics;

public sealed class SubscriptionSnapshotJob(
    IServiceProvider serviceProvider,
    TimeProvider timeProvider,
    IOptions<PlatformAnalyticsOptions> options,
    ILogger<SubscriptionSnapshotJob> logger)
    : PlatformPeriodicJob(serviceProvider, timeProvider, logger)
{
    private readonly PlatformAnalyticsOptions options = options.Value;

    protected override string JobName => PlatformJobNames.SubscriptionSnapshots;

    protected override TimeSpan Interval => options.SnapshotInterval;

    protected override Task<int> TickAsync(IServiceProvider scopedServices, CancellationToken cancellationToken) =>
        scopedServices.GetRequiredService<ISubscriptionSnapshotRunner>().RunAsync(GetUtcNow(), cancellationToken);
}
```

- [ ] **Step 6: Подключить к наблюдению и DI**

В `PlatformJobNames` добавить константу и внести её в список `Watched`:

```csharp
    public const string SubscriptionSnapshots = "subscription_snapshots";
```

В `PlatformJobIntervalCatalog` добавить запись для нового задания (интервал — `PlatformAnalyticsOptions.SnapshotInterval`); каталог обязан покрывать весь `Watched`, это проверяется тестом.

В `Program.cs`:

```csharp
builder.Services.Configure<PlatformAnalyticsOptions>(
    builder.Configuration.GetSection(PlatformAnalyticsOptions.ConfigurationSection));
builder.Services.AddScoped<ISubscriptionSnapshotRunner, EfSubscriptionSnapshotRunner>();
builder.Services.AddHostedService<SubscriptionSnapshotJob>();
```

Добавить строку `platform.health.job.subscription_snapshots` во все три каталога `locales/{ru,en,tg}.json` (экран «Здоровье» переводит имена заданий по значению) и перегенерировать: `cd packages/i18n && "$BUN" run gen`.

- [ ] **Step 7: Прогнать тесты**

```bash
dotnet test tests/AFK4.Platform.Api.Tests --filter "SubscriptionSnapshotRunnerTests|JobIntervals_CoverEveryWatchedJob|PeriodicJobRegistrationTests"
```
Expected: PASS. Затем полный прогон — число зелёных не должно упасть.

- [ ] **Step 8: Коммит**

```bash
git add src/AFK4.Platform.Api tests/AFK4.Platform.Api.Tests locales packages/i18n
git commit -m "feat(platform): суточные снимки подписок"
```

---

### Task 3: Раскладка счетов на месяцы

**Files:**
- Create: `src/AFK4.Platform.Api/Platform/Analytics/MonthlyRevenue.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/MonthlyRevenueTests.cs`

**Interfaces:**
- Produces: `MonthlyRevenue.Spread(IReadOnlyCollection<InvoiceRevenueRow> invoices, DateOnly firstMonth, DateOnly lastMonth) -> IReadOnlyList<MonthlyRevenuePoint>`; `record InvoiceRevenueRow(string Kind, string Status, DateTimeOffset PeriodStartUtc, DateTimeOffset PeriodEndUtc, long AmountMinorUnits)`; `record MonthlyRevenuePoint(int Year, int Month, long RecurringMinorUnits, long OneOffMinorUnits)`.

- [ ] **Step 1: Написать падающий тест**

`tests/AFK4.Platform.Api.Tests/Platform/MonthlyRevenueTests.cs`:

```csharp
using AFK4.Platform.Api.Platform.Analytics;
using AFK4.Shared.Contracts.Platform.Billing;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class MonthlyRevenueTests
{
    private static readonly DateOnly First = new(2026, 1, 1);
    private static readonly DateOnly Last = new(2026, 12, 1);

    private static InvoiceRevenueRow Subscription(int year, int month, long amount, int months = 1) =>
        new(InvoiceKindNames.Subscription, InvoiceStatusNames.Issued,
            new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(months),
            amount);

    [Fact]
    public void MonthlyInvoice_LandsInItsOwnMonth()
    {
        var points = MonthlyRevenue.Spread([Subscription(2026, 3, 290000)], First, Last);

        var march = points.Single(point => point.Month == 3);
        Assert.Equal(290000, march.RecurringMinorUnits);
        Assert.All(points.Where(point => point.Month != 3), point => Assert.Equal(0, point.RecurringMinorUnits));
    }

    [Fact]
    public void YearlyInvoice_SpreadsAcrossTwelveMonths()
    {
        var points = MonthlyRevenue.Spread([Subscription(2026, 1, 3480000, months: 12)], First, Last);

        Assert.All(points, point => Assert.Equal(290000, point.RecurringMinorUnits));
        Assert.Equal(3480000, points.Sum(point => point.RecurringMinorUnits));
    }

    [Fact]
    public void SpreadRemainder_GoesToTheFirstMonths_SoTheTotalIsExact()
    {
        // 100 сомони на 3 месяца не делится нацело: сумма частей обязана сойтись с суммой счёта,
        // иначе годовая выручка в отчёте не сойдётся с выставленным.
        var invoice = new InvoiceRevenueRow(
            InvoiceKindNames.Subscription, InvoiceStatusNames.Issued,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
            10000);

        var points = MonthlyRevenue.Spread([invoice], First, Last);

        Assert.Equal(10000, points.Sum(point => point.RecurringMinorUnits));
        Assert.Equal(3334, points.Single(point => point.Month == 1).RecurringMinorUnits);
        Assert.Equal(3333, points.Single(point => point.Month == 3).RecurringMinorUnits);
    }

    [Fact]
    public void VoidedInvoice_IsIgnored()
    {
        var voided = Subscription(2026, 3, 290000) with { Status = InvoiceStatusNames.Void };

        var points = MonthlyRevenue.Spread([voided], First, Last);

        Assert.All(points, point => Assert.Equal(0, point.RecurringMinorUnits));
    }

    [Fact]
    public void OneOffAndProration_CountSeparatelyFromRecurring()
    {
        var oneOff = new InvoiceRevenueRow(
            InvoiceKindNames.OneOff, InvoiceStatusNames.Issued,
            new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero),
            50000);

        var points = MonthlyRevenue.Spread([oneOff], First, Last);

        var may = points.Single(point => point.Month == 5);
        Assert.Equal(0, may.RecurringMinorUnits);
        Assert.Equal(50000, may.OneOffMinorUnits);
    }

    [Fact]
    public void CreditNote_ReducesTheMonthItBelongsTo()
    {
        // Кредит-нота — отрицательная сумма, а не отдельный флаг: выручка месяца должна уменьшиться.
        var credit = new InvoiceRevenueRow(
            InvoiceKindNames.OneOff, InvoiceStatusNames.Issued,
            new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            -20000);

        var points = MonthlyRevenue.Spread([credit], First, Last);

        Assert.Equal(-20000, points.Single(point => point.Month == 6).OneOffMinorUnits);
    }

    [Fact]
    public void MonthsOutsideTheWindow_AreClipped_NotDropped()
    {
        // Годовой счёт, начавшийся до окна: в окно попадают только его месяцы, лежащие внутри.
        var points = MonthlyRevenue.Spread([Subscription(2025, 7, 1200000, months: 12)], First, Last);

        Assert.Equal(600000, points.Sum(point => point.RecurringMinorUnits));
        Assert.Equal(100000, points.Single(point => point.Month == 1).RecurringMinorUnits);
        Assert.Equal(0, points.Single(point => point.Month == 7).RecurringMinorUnits);
    }

    [Fact]
    public void EveryMonthOfTheWindow_IsPresent_EvenWithoutInvoices()
    {
        var points = MonthlyRevenue.Spread([], First, Last);

        Assert.Equal(12, points.Count);
        Assert.All(points, point => Assert.Equal(2026, point.Year));
    }
}
```

- [ ] **Step 2: Прогнать — убедиться, что падает**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter MonthlyRevenueTests`
Expected: FAIL — `MonthlyRevenue` не существует.

- [ ] **Step 3: Написать раскладку**

`src/AFK4.Platform.Api/Platform/Analytics/MonthlyRevenue.cs`:

```csharp
using AFK4.Shared.Contracts.Platform.Billing;

namespace AFK4.Platform.Api.Platform.Analytics;

public sealed record InvoiceRevenueRow(
    string Kind,
    string Status,
    DateTimeOffset PeriodStartUtc,
    DateTimeOffset PeriodEndUtc,
    long AmountMinorUnits);

public sealed record MonthlyRevenuePoint(int Year, int Month, long RecurringMinorUnits, long OneOffMinorUnits);

/// <summary>
/// Раскладывает выставленные счета по календарным месяцам. Считаем ИЗ СЧЕТОВ, а не накапливаем
/// отдельную метрику: у счёта есть период и сумма, поэтому цифра всегда сходится с тем, что реально
/// выставлено, и не может разойтись с биллингом.
/// </summary>
public static class MonthlyRevenue
{
    public static IReadOnlyList<MonthlyRevenuePoint> Spread(
        IReadOnlyCollection<InvoiceRevenueRow> invoices, DateOnly firstMonth, DateOnly lastMonth)
    {
        ArgumentNullException.ThrowIfNull(invoices);

        var recurring = new Dictionary<(int Year, int Month), long>();
        var oneOff = new Dictionary<(int Year, int Month), long>();

        foreach (var invoice in invoices)
        {
            // Аннулированный счёт не выручка: деньги по нему не ждут и не придут.
            if (invoice.Status == InvoiceStatusNames.Void) continue;

            var isRecurring = invoice.Kind == InvoiceKindNames.Subscription;
            var target = isRecurring ? recurring : oneOff;

            if (!isRecurring)
            {
                // Разовые счета, доплаты и кредит-ноты не растягиваются: они относятся к моменту,
                // а не к периоду.
                Add(target, invoice.PeriodStartUtc.Year, invoice.PeriodStartUtc.Month, invoice.AmountMinorUnits);
                continue;
            }

            var months = MonthsOf(invoice).ToList();
            if (months.Count == 0)
            {
                Add(target, invoice.PeriodStartUtc.Year, invoice.PeriodStartUtc.Month, invoice.AmountMinorUnits);
                continue;
            }

            // Остаток от деления кладём в первые месяцы, чтобы сумма частей ТОЧНО равнялась сумме
            // счёта: отчёт, где годовая выручка не сходится с выставленным, бесполезен.
            var share = invoice.AmountMinorUnits / months.Count;
            var remainder = invoice.AmountMinorUnits - share * months.Count;
            for (var index = 0; index < months.Count; index++)
            {
                var extra = index < Math.Abs(remainder) ? Math.Sign(remainder) : 0;
                Add(target, months[index].Year, months[index].Month, share + extra);
            }
        }

        var points = new List<MonthlyRevenuePoint>();
        for (var month = firstMonth; month <= lastMonth; month = month.AddMonths(1))
        {
            var key = (month.Year, month.Month);
            points.Add(new MonthlyRevenuePoint(
                month.Year,
                month.Month,
                recurring.GetValueOrDefault(key),
                oneOff.GetValueOrDefault(key)));
        }

        return points;
    }

    private static IEnumerable<(int Year, int Month)> MonthsOf(InvoiceRevenueRow invoice)
    {
        var start = new DateOnly(invoice.PeriodStartUtc.Year, invoice.PeriodStartUtc.Month, 1);
        var endExclusive = new DateOnly(invoice.PeriodEndUtc.Year, invoice.PeriodEndUtc.Month, 1);
        if (endExclusive <= start) yield break;

        for (var month = start; month < endExclusive; month = month.AddMonths(1))
            yield return (month.Year, month.Month);
    }

    private static void Add(Dictionary<(int Year, int Month), long> target, int year, int month, long amount) =>
        target[(year, month)] = target.GetValueOrDefault((year, month)) + amount;
}
```

- [ ] **Step 4: Прогнать тесты**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter MonthlyRevenueTests`
Expected: PASS все восемь.

- [ ] **Step 5: Коммит**

```bash
git add src/AFK4.Platform.Api/Platform/Analytics/MonthlyRevenue.cs tests/AFK4.Platform.Api.Tests/Platform/MonthlyRevenueTests.cs
git commit -m "feat(platform): раскладка счетов по месяцам"
```

---

### Task 4: Приход и отток из снимков

**Files:**
- Create: `src/AFK4.Platform.Api/Platform/Analytics/SubscriptionMovement.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/SubscriptionMovementTests.cs`

**Interfaces:**
- Produces: `SubscriptionMovement.Compute(IReadOnlyCollection<SnapshotRow> snapshots, DateOnly firstMonth, DateOnly lastMonth) -> IReadOnlyList<MovementPoint>`; `record SnapshotRow(Guid OrganizationId, DateOnly SnapshotDate, string Status)`; `record MovementPoint(int Year, int Month, int Joined, int Left, int PayingAtMonthEnd)`.

- [ ] **Step 1: Написать падающий тест**

`tests/AFK4.Platform.Api.Tests/Platform/SubscriptionMovementTests.cs`:

```csharp
using AFK4.Platform.Api.Platform.Analytics;
using AFK4.Shared.Contracts.Platform.Organizations;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class SubscriptionMovementTests
{
    private static readonly Guid Club = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherClub = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateOnly First = new(2026, 1, 1);
    private static readonly DateOnly Last = new(2026, 4, 1);

    private static SnapshotRow Row(Guid club, int month, int day, string status) =>
        new(club, new DateOnly(2026, month, day), status);

    [Fact]
    public void ClubBecomingActive_CountsAsJoinedInThatMonth()
    {
        var points = SubscriptionMovement.Compute(
        [
            Row(Club, 1, 31, SubscriptionStatusNames.Trial),
            Row(Club, 2, 28, SubscriptionStatusNames.Active)
        ], First, Last);

        Assert.Equal(1, points.Single(point => point.Month == 2).Joined);
        Assert.Equal(0, points.Single(point => point.Month == 2).Left);
    }

    [Fact]
    public void ClubLeavingActive_CountsAsLeftInThatMonth()
    {
        var points = SubscriptionMovement.Compute(
        [
            Row(Club, 1, 31, SubscriptionStatusNames.Active),
            Row(Club, 2, 28, SubscriptionStatusNames.Cancelled)
        ], First, Last);

        Assert.Equal(1, points.Single(point => point.Month == 2).Left);
        Assert.Equal(0, points.Single(point => point.Month == 2).Joined);
    }

    [Fact]
    public void PastDue_IsStillPaying_NotChurn()
    {
        // Клуб, которому шлют напоминания, ещё не ушёл: отток — это cancelled, а не долг.
        var points = SubscriptionMovement.Compute(
        [
            Row(Club, 1, 31, SubscriptionStatusNames.Active),
            Row(Club, 2, 28, SubscriptionStatusNames.PastDue)
        ], First, Last);

        Assert.Equal(0, points.Single(point => point.Month == 2).Left);
        Assert.Equal(1, points.Single(point => point.Month == 2).PayingAtMonthEnd);
    }

    [Fact]
    public void ClubAppearingAlreadyActive_CountsAsJoined()
    {
        // Организации не было в снимках вовсе — значит она новая, а не «всегда была».
        var points = SubscriptionMovement.Compute([Row(Club, 3, 31, SubscriptionStatusNames.Active)], First, Last);

        Assert.Equal(1, points.Single(point => point.Month == 3).Joined);
    }

    [Fact]
    public void ReturningClub_CountsAsJoinedAgain()
    {
        var points = SubscriptionMovement.Compute(
        [
            Row(Club, 1, 31, SubscriptionStatusNames.Active),
            Row(Club, 2, 28, SubscriptionStatusNames.Cancelled),
            Row(Club, 3, 31, SubscriptionStatusNames.Active)
        ], First, Last);

        Assert.Equal(1, points.Single(point => point.Month == 2).Left);
        Assert.Equal(1, points.Single(point => point.Month == 3).Joined);
    }

    [Fact]
    public void PayingCount_IsTakenFromTheLastSnapshotOfTheMonth()
    {
        var points = SubscriptionMovement.Compute(
        [
            Row(Club, 2, 1, SubscriptionStatusNames.Active),
            Row(Club, 2, 28, SubscriptionStatusNames.Cancelled),
            Row(OtherClub, 2, 28, SubscriptionStatusNames.Active)
        ], First, Last);

        Assert.Equal(1, points.Single(point => point.Month == 2).PayingAtMonthEnd);
    }

    [Fact]
    public void EveryMonthOfTheWindow_IsPresent_EvenWithoutSnapshots()
    {
        var points = SubscriptionMovement.Compute([], First, Last);

        Assert.Equal(4, points.Count);
        Assert.All(points, point => Assert.Equal(0, point.PayingAtMonthEnd));
    }
}
```

- [ ] **Step 2: Прогнать — убедиться, что падает**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter SubscriptionMovementTests`
Expected: FAIL — `SubscriptionMovement` не существует.

- [ ] **Step 3: Написать расчёт**

`src/AFK4.Platform.Api/Platform/Analytics/SubscriptionMovement.cs`:

```csharp
using AFK4.Shared.Contracts.Platform.Organizations;

namespace AFK4.Platform.Api.Platform.Analytics;

public sealed record SnapshotRow(Guid OrganizationId, DateOnly SnapshotDate, string Status);

public sealed record MovementPoint(int Year, int Month, int Joined, int Left, int PayingAtMonthEnd);

/// <summary>
/// Приход и отток клубов по месяцам. Считается ТОЛЬКО из суточных снимков: подписка хранит лишь
/// сегодняшний статус, и клуб, ушедший в июне, сегодня неотличим от того, кто не платил никогда.
/// </summary>
public static class SubscriptionMovement
{
    /// <summary>Платящий = active или past_due: клуб, которому шлют напоминания, ещё не ушёл.</summary>
    private static bool IsPaying(string status) =>
        status == SubscriptionStatusNames.Active || status == SubscriptionStatusNames.PastDue;

    public static IReadOnlyList<MovementPoint> Compute(
        IReadOnlyCollection<SnapshotRow> snapshots, DateOnly firstMonth, DateOnly lastMonth)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        // Последний снимок каждого клуба в каждом месяце — состояние на конец месяца.
        var monthEndStatus = snapshots
            .GroupBy(row => (row.OrganizationId, row.SnapshotDate.Year, row.SnapshotDate.Month))
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(row => row.SnapshotDate).First().Status);

        var clubs = snapshots.Select(row => row.OrganizationId).Distinct().ToList();
        var points = new List<MovementPoint>();

        for (var month = firstMonth; month <= lastMonth; month = month.AddMonths(1))
        {
            var previous = month.AddMonths(-1);
            var joined = 0;
            var left = 0;
            var paying = 0;

            foreach (var club in clubs)
            {
                var nowPaying = monthEndStatus.TryGetValue((club, month.Year, month.Month), out var current)
                    && IsPaying(current);
                var wasPaying = monthEndStatus.TryGetValue((club, previous.Year, previous.Month), out var before)
                    && IsPaying(before);

                if (nowPaying) paying++;
                if (nowPaying && !wasPaying) joined++;
                if (!nowPaying && wasPaying) left++;
            }

            points.Add(new MovementPoint(month.Year, month.Month, joined, left, paying));
        }

        return points;
    }
}
```

- [ ] **Step 4: Прогнать тесты**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter SubscriptionMovementTests`
Expected: PASS все семь.

- [ ] **Step 5: Коммит**

```bash
git add src/AFK4.Platform.Api/Platform/Analytics/SubscriptionMovement.cs tests/AFK4.Platform.Api.Tests/Platform/SubscriptionMovementTests.cs
git commit -m "feat(platform): приход и отток клубов из снимков"
```

---

### Task 5: Служба аналитики и эндпоинт

**Files:**
- Create: `src/AFK4.Shared.Contracts/Platform/Analytics/PlatformAnalyticsContracts.cs`
- Create: `src/AFK4.Platform.Api/Platform/Analytics/IPlatformAnalyticsService.cs`
- Create: `src/AFK4.Platform.Api/Platform/Analytics/EfPlatformAnalyticsService.cs`
- Create: `src/AFK4.Platform.Api/Endpoints/PlatformAnalyticsEndpoints.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/PlatformAnalyticsEndpointTests.cs`

**Interfaces:**
- Consumes: `MonthlyRevenue.Spread`, `InvoiceRevenueRow`, `MonthlyRevenuePoint` (Task 3); `SubscriptionMovement.Compute`, `SnapshotRow`, `MovementPoint` (Task 4).
- Produces: `GET /api/platform/analytics/overview?months=12` → `PlatformAnalyticsOverviewDto`.

- [ ] **Step 1: Написать контракты**

`src/AFK4.Shared.Contracts/Platform/Analytics/PlatformAnalyticsContracts.cs`:

```csharp
namespace AFK4.Shared.Contracts.Platform.Analytics;

/// <summary>
/// Точка помесячного ряда. Год и месяц едут числами: название месяца — дело клиента,
/// у которого есть язык пользователя.
/// </summary>
public sealed record AnalyticsMonthDto(
    int Year,
    int Month,
    long RecurringMinorUnits,
    long OneOffMinorUnits,
    int Joined,
    int Left,
    int PayingAtMonthEnd);

public sealed record PlatformAnalyticsOverviewDto(
    DateTimeOffset GeneratedAtUtc,
    string CurrencyCode,
    IReadOnlyList<AnalyticsMonthDto> Months,
    long CurrentMrrMinorUnits,
    int CurrentPayingClubs,
    long AverageRevenuePerClubMinorUnits,
    long OutstandingMinorUnits);
```

- [ ] **Step 2: Написать падающий тест эндпоинта**

`tests/AFK4.Platform.Api.Tests/Platform/PlatformAnalyticsEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Analytics;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Platform.Billing;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformAnalyticsEndpointTests
{
    [Fact]
    public async Task GET_overview_WithPermission_ReturnsMonthlySeries()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var now = DateTimeOffset.UtcNow;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var organizationId = await BillingListEndpointTests.SeedOrgWithSubscriptionAsync(
                db, "analytics-club", "Analytics Club", SubscriptionStatusNames.Active);
            db.Invoices.Add(new InvoiceEntity
            {
                InvoiceId = Guid.NewGuid(),
                OrganizationId = organizationId,
                Number = 1,
                Kind = InvoiceKindNames.Subscription,
                PeriodStartUtc = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero),
                PeriodEndUtc = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(1),
                IssuedAtUtc = now,
                DueAtUtc = now.AddDays(7),
                AmountMinorUnits = 290000,
                GrossAmountMinorUnits = 290000,
                CurrencyCode = "TJS",
                Status = InvoiceStatusNames.Issued,
                Description = "analytics test",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/platform/analytics/overview?months=12");
        var overview = await response.Content.ReadFromJsonAsync<PlatformAnalyticsOverviewDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(overview);
        Assert.Equal(12, overview!.Months.Count);
        Assert.Equal("TJS", overview.CurrencyCode);
        var currentMonth = overview.Months.Single(month => month.Year == now.Year && month.Month == now.Month);
        Assert.Equal(290000, currentMonth.RecurringMinorUnits);
    }

    [Fact]
    public async Task GET_overview_ClampsMonthsToSaneRange()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var response = await client.GetAsync("/api/platform/analytics/overview?months=999");
        var overview = await response.Content.ReadFromJsonAsync<PlatformAnalyticsOverviewDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(overview);
        Assert.Equal(36, overview!.Months.Count);
    }

    [Fact]
    public async Task GET_overview_WithoutAuthentication_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/platform/analytics/overview");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GET_overview_WithoutPermission_ReturnsForbidden()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(
            factory, client, userName: "noanalytics@platform.test", roles: []);

        var response = await client.GetAsync("/api/platform/analytics/overview");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
```

Сверить фактическую сигнатуру `BillingListEndpointTests.SeedOrgWithSubscriptionAsync` и `PlatformAdminTestHelper.AuthorizeAsAsync` (образец применения обоих — `PlatformDebtEndpointTests` и `PlatformHealthEndpointTests`); если параметры отличаются, использовать фактические.

- [ ] **Step 3: Прогнать — убедиться, что падает**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter PlatformAnalyticsEndpointTests`
Expected: FAIL — маршрут не зарегистрирован (404), контракты не существуют.

- [ ] **Step 4: Написать службу**

`src/AFK4.Platform.Api/Platform/Analytics/IPlatformAnalyticsService.cs`:

```csharp
using AFK4.Shared.Contracts.Platform.Analytics;

namespace AFK4.Platform.Api.Platform.Analytics;

public interface IPlatformAnalyticsService
{
    Task<PlatformAnalyticsOverviewDto> GetOverviewAsync(int months, CancellationToken cancellationToken);
}
```

`src/AFK4.Platform.Api/Platform/Analytics/EfPlatformAnalyticsService.cs`:

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Analytics;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Analytics;

public sealed class EfPlatformAnalyticsService(PlatformDbContext dbContext, TimeProvider timeProvider)
    : IPlatformAnalyticsService
{
    public const int MinMonths = 3;
    public const int MaxMonths = 36;
    private const string DefaultCurrency = "TJS";

    public async Task<PlatformAnalyticsOverviewDto> GetOverviewAsync(int months, CancellationToken cancellationToken)
    {
        var window = Math.Clamp(months, MinMonths, MaxMonths);
        var now = timeProvider.GetUtcNow();
        var lastMonth = new DateOnly(now.Year, now.Month, 1);
        var firstMonth = lastMonth.AddMonths(-(window - 1));
        var windowStart = new DateTimeOffset(firstMonth.Year, firstMonth.Month, 1, 0, 0, 0, TimeSpan.Zero);

        // Счета берём с запасом назад: годовой счёт, выставленный до окна, всё ещё отдаёт
        // в окно свои месяцы — без запаса выручка начала окна была бы занижена.
        var invoiceCutoff = windowStart.AddMonths(-12);
        var invoices = await dbContext.Invoices
            .AsNoTracking()
            .Where(invoice => invoice.PeriodEndUtc >= invoiceCutoff)
            .Select(invoice => new InvoiceRevenueRow(
                invoice.Kind, invoice.Status, invoice.PeriodStartUtc, invoice.PeriodEndUtc, invoice.AmountMinorUnits))
            .ToListAsync(cancellationToken);

        var snapshots = await dbContext.SubscriptionDailySnapshots
            .AsNoTracking()
            .Where(snapshot => snapshot.SnapshotDate >= firstMonth.AddMonths(-1))
            .Select(snapshot => new SnapshotRow(snapshot.OrganizationId, snapshot.SnapshotDate, snapshot.Status))
            .ToListAsync(cancellationToken);

        var revenue = MonthlyRevenue.Spread(invoices, firstMonth, lastMonth);
        var movement = SubscriptionMovement.Compute(snapshots, firstMonth, lastMonth);
        var movementByMonth = movement.ToDictionary(point => (point.Year, point.Month));

        var monthDtos = revenue
            .Select(point =>
            {
                var moves = movementByMonth.GetValueOrDefault((point.Year, point.Month))
                    ?? new MovementPoint(point.Year, point.Month, 0, 0, 0);
                return new AnalyticsMonthDto(
                    point.Year, point.Month,
                    point.RecurringMinorUnits, point.OneOffMinorUnits,
                    moves.Joined, moves.Left, moves.PayingAtMonthEnd);
            })
            .ToList();

        var payingSubscriptions = await dbContext.OrganizationSubscriptions
            .AsNoTracking()
            .Where(subscription => subscription.Status == SubscriptionStatusNames.Active
                || subscription.Status == SubscriptionStatusNames.PastDue)
            .Select(subscription => new { subscription.AmountMinorUnits, subscription.BillingInterval, subscription.CurrencyCode })
            .ToListAsync(cancellationToken);

        var currentMrr = payingSubscriptions.Sum(subscription => subscription.BillingInterval == BillingIntervalNames.Yearly
            ? subscription.AmountMinorUnits / 12
            : subscription.AmountMinorUnits);

        var outstanding = await dbContext.Invoices
            .AsNoTracking()
            .Where(invoice => invoice.Status == InvoiceStatusNames.Issued || invoice.Status == InvoiceStatusNames.Overdue)
            .SumAsync(invoice => invoice.AmountMinorUnits, cancellationToken);

        return new PlatformAnalyticsOverviewDto(
            GeneratedAtUtc: now,
            CurrencyCode: payingSubscriptions.Count > 0 ? payingSubscriptions[0].CurrencyCode : DefaultCurrency,
            Months: monthDtos,
            CurrentMrrMinorUnits: currentMrr,
            CurrentPayingClubs: payingSubscriptions.Count,
            // Средний чек на платящий клуб; без платящих клубов это ноль, а не деление на ноль.
            AverageRevenuePerClubMinorUnits: payingSubscriptions.Count > 0 ? currentMrr / payingSubscriptions.Count : 0,
            OutstandingMinorUnits: outstanding);
    }
}
```

- [ ] **Step 5: Написать эндпоинт и зарегистрировать**

`src/AFK4.Platform.Api/Endpoints/PlatformAnalyticsEndpoints.cs`:

```csharp
using AFK4.Platform.Api.Platform.Analytics;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Shared.Contracts.Platform.Auth;

namespace AFK4.Platform.Api.Endpoints;

internal static class PlatformAnalyticsEndpoints
{
    public static void MapPlatformAnalyticsEndpoints(this WebApplication app)
    {
        app.MapGet("/api/platform/analytics/overview", async (
            PlatformAdminAuthorizationService authorizationService,
            IPlatformAnalyticsService analyticsService,
            int? months,
            CancellationToken cancellationToken) =>
        {
            // Право проверяется ДО обращения к данным.
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ViewBilling);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            return Results.Ok(await analyticsService.GetOverviewAsync(months ?? 12, cancellationToken));
        });
    }
}
```

В `Program.cs` рядом с `MapPlatformHealthEndpoints()` добавить `app.MapPlatformAnalyticsEndpoints();` и рядом с регистрацией `IPlatformHealthOverviewService`:

```csharp
builder.Services.AddScoped<IPlatformAnalyticsService, EfPlatformAnalyticsService>();
```

- [ ] **Step 6: Прогнать тесты**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter PlatformAnalyticsEndpointTests`
Expected: PASS все четыре, включая настоящий 403.

- [ ] **Step 7: Коммит**

```bash
git add src/AFK4.Platform.Api src/AFK4.Shared.Contracts tests/AFK4.Platform.Api.Tests
git commit -m "feat(platform): эндпоинт бизнес-аналитики"
```

---

### Task 6: Вкладка «Аналитика» в разделе «Деньги»

**Files:**
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json`
- Modify: `src/AFK4.PlatformControl.Web/src/api/types.ts`
- Create: `src/AFK4.PlatformControl.Web/src/api/platformClients/analytics.ts`
- Modify: `src/AFK4.PlatformControl.Web/src/api/platformApi.ts` (поле `analytics` в фасаде — сверить фактическое имя файла: `grep -rn "new DebtApi" src/AFK4.PlatformControl.Web/src`)
- Create: `src/AFK4.PlatformControl.Web/src/platform/billing/analyticsModel.ts`
- Create: `src/AFK4.PlatformControl.Web/src/platform/billing/useAnalytics.ts`
- Create: `src/AFK4.PlatformControl.Web/src/platform/billing/AnalyticsTab.tsx`
- Modify: `src/AFK4.PlatformControl.Web/src/routing/platformRoute.ts` (`BillingTab` + множество `BILLING_TABS`)
- Modify: `src/AFK4.PlatformControl.Web/src/platform/billing/BillingScreen.tsx`
- Test: `src/AFK4.PlatformControl.Web/src/platform/billing/analyticsModel.test.ts`, `src/AFK4.PlatformControl.Web/src/platform/billing/AnalyticsTab.test.tsx`

**Interfaces:**
- Consumes: `GET /api/platform/analytics/overview?months=12` → `PlatformAnalyticsOverviewDto` (Task 5).

- [ ] **Step 1: Добавить строки в три каталога**

Ключи (значения ru приведены; в `en.json` и `tg.json` — настоящие переводы, НЕ копия русского: guard-тест ловит `tg === ru`):

```
platform.billing.tab.analytics       Аналитика
platform.analytics.revenue.title     Выручка по месяцам
platform.analytics.revenue.recurring Подписки
platform.analytics.revenue.oneOff    Разовые и корректировки
platform.analytics.movement.title    Приход и отток клубов
platform.analytics.movement.joined   Пришли
platform.analytics.movement.left     Ушли
platform.analytics.movement.paying   Платят
platform.analytics.summary.mrr       Выручка в месяц
platform.analytics.summary.clubs     {count, plural, one {# платящий клуб} few {# платящих клуба} other {# платящих клубов}}
platform.analytics.summary.average   Средний чек на клуб
platform.analytics.summary.outstanding Не оплачено
platform.analytics.empty             Данных пока нет — первые цифры появятся, когда пройдут сутки
platform.analytics.month.1           янв.
platform.analytics.month.2           фев.
platform.analytics.month.3           март
platform.analytics.month.4           апр.
platform.analytics.month.5           май
platform.analytics.month.6           июнь
platform.analytics.month.7           июль
platform.analytics.month.8           авг.
platform.analytics.month.9           сент.
platform.analytics.month.10          окт.
platform.analytics.month.11          нояб.
platform.analytics.month.12          дек.
```

После правки: `BUN=/home/fedya/.bun/bin/bun; cd packages/i18n && "$BUN" run gen && cd ../..`

- [ ] **Step 2: Написать модель и тест**

`analyticsModel.ts` — чистые функции без React:

```typescript
import { minorToMajor } from '@/lib/money';
import type { AnalyticsMonth, AnalyticsOverview } from '@/api/types';

export interface RevenuePoint {
  label: string;
  recurring: number;
  oneOff: number;
}

// Подписи месяцев собираются на клиенте из года и номера месяца: сервер отдаёт числа,
// потому что название месяца зависит от языка пользователя, которого сервер не знает.
export function toRevenueSeries(
  months: readonly AnalyticsMonth[],
  monthLabel: (month: number) => string
): RevenuePoint[] {
  return months.map(month => ({
    label: monthLabel(month.month),
    // Конвертация только через minorToMajor: своё деление рядом с готовым форматтером
    // разойдётся с ним молча при первом же изменении точности.
    recurring: minorToMajor(month.recurringMinorUnits),
    oneOff: minorToMajor(month.oneOffMinorUnits)
  }));
}

export function totalRevenue(months: readonly AnalyticsMonth[]): number {
  return months.reduce((sum, month) => sum + month.recurringMinorUnits + month.oneOffMinorUnits, 0);
}

// «Данных нет» — это когда во всех месяцах и выручка, и движение по нулям. Отличать от ошибки
// загрузки обязательно: пустой график и несостоявшийся запрос выглядят одинаково, но означают
// противоположное.
export function isEmpty(overview: AnalyticsOverview): boolean {
  return overview.months.every(month =>
    month.recurringMinorUnits === 0
    && month.oneOffMinorUnits === 0
    && month.payingAtMonthEnd === 0);
}
```

`analyticsModel.test.ts` — тесты: `toRevenueSeries` переводит минорные единицы в мажорные и берёт подписи из переданной функции; `totalRevenue` складывает обе составляющие; `isEmpty` истинно на нулевых месяцах и ложно, если хотя бы в одном есть платящий клуб или ненулевая выручка.

- [ ] **Step 3: Написать клиент и хук**

`api/platformClients/analytics.ts`:

```typescript
import type { PlatformTransport } from '../platformTransport';
import type { AnalyticsOverview } from '../types';

export class AnalyticsApi {
  public constructor(private readonly transport: PlatformTransport) {}

  public getOverview(months = 12): Promise<AnalyticsOverview> {
    return this.transport.send<AnalyticsOverview>('GET', `/api/platform/analytics/overview?months=${months}`);
  }
}
```

`useAnalytics.ts` — по образцу `useDebt.ts`: состояния `loading | error | ready` с `retry`. Ошибку не проглатывать.

- [ ] **Step 4: Написать вкладку**

`AnalyticsTab.tsx`: карточки сводки (выручка в месяц, платящие клубы, средний чек, не оплачено) и два графика на `recharts` (она уже в зависимостях панели: `src/AFK4.PlatformControl.Web/package.json`) — столбцы выручки с разделением на подписки и разовое, и линии прихода/оттока. Обязательно:

- состояние загрузки — `LoadingCards`, состояние ошибки — `ErrorState` с повтором;
- при `isEmpty(overview)` — текст `platform.analytics.empty`, а не пустые оси, выглядящие как «выручки нет»;
- денежные значения печатать через `formatCurrency` из `useI18n` после `minorToMajor` (`@/lib/money`), как в `DebtSection.tsx`;
- графики обернуть в `ResponsiveContainer`, чтобы вкладка не ломала верстку на узком экране.

- [ ] **Step 5: Подключить вкладку**

- `platformRoute.ts`: в тип `BillingTab` добавить `'analytics'`, в `BILLING_TABS` — соответствующее значение.
- `BillingScreen.tsx`: пункт `{ value: 'analytics', label: t('platform.billing.tab.analytics') }` в `items` и строку `{tab === 'analytics' ? <AnalyticsTab client={client.analytics} /> : null}` в `role="tabpanel"`.

- [ ] **Step 6: Написать тест вкладки**

`AnalyticsTab.test.tsx` — три теста с фейковым клиентом (объект с `getOverview`), без `mock.module` (он течёт на весь процесс):

1. Обзор с ненулевой выручкой → на экране видна сумма и заголовок «Выручка по месяцам».
2. Обзор со всеми нулями → виден текст `platform.analytics.empty`.
3. Клиент реджектит → виден `ErrorState` с кнопкой повтора, и НЕТ текста `platform.analytics.empty` — несостоявшийся запрос не должен выглядеть как «данных нет».

- [ ] **Step 7: Прогнать тесты и сборку**

```bash
BUN=/home/fedya/.bun/bin/bun
cd src/AFK4.PlatformControl.Web && "$BUN" test && "$BUN" run build && cd ../..
cd packages/i18n && "$BUN" test && cd ../..
```

`bun test` не тайпчекает — `bun run build` (`tsc -b && vite build`) обязателен.

- [ ] **Step 8: Коммит**

```bash
git add locales packages/i18n src/AFK4.PlatformControl.Web
git commit -m "feat(platform-control): вкладка бизнес-аналитики"
```

---

## Финальная проверка плана

```bash
CONN="<строка подключения к тестовой БД, имя которой оканчивается на _test>"
export AFK4_PLATFORM_ADMIN_POSTGRES_TEST_CONNECTION_STRING="$CONN" AFK4_RESERVATION_POSTGRES_TEST_CONNECTION_STRING="$CONN" AFK4_COMMERCE_TEST_POSTGRES="$CONN" AFK4_POS_POSTGRES_TEST_CONNECTION_STRING="$CONN"
AFK4_REQUIRE_POSTGRES_TESTS=1 dotnet test tests/AFK4.Platform.Api.Tests
BUN=/home/fedya/.bun/bin/bun
cd src/AFK4.PlatformControl.Web && "$BUN" test && "$BUN" run build
cd ../../packages/i18n && "$BUN" test
```

Ожидание: 0 упавших, 0 пропущенных. База на момент старта плана — 1720 зелёных бэкенд-тестов, 209 тестов панели, 39 тестов каталога переводов; число зелёных должно вырасти, падение означает потерянный тест, а не «стало чище».
