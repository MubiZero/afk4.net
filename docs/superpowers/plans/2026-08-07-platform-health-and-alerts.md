# Здоровье платформы и оповещения — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Платформа начинает следить за собой: каждый периодический прогон записывается, поломка становится инцидентом, а инцидент — письмом (и SMS на самое страшное) и строкой на экране «Здоровье».

**Architecture:** Одна общая обёртка `PlatformPeriodicJob` заменяет шесть самописных циклов `BackgroundService` и пишет строку в `platform_job_runs` вокруг каждого тика. Отдельное задание `PlatformHealthWatchJob` раз в 5 минут применяет правила к этим строкам и к двум очередям, открывая и закрывая записи в `platform_incidents` (частичный уникальный индекс не даёт завести второй открытый инцидент на тот же ключ). Открытие и закрытие инцидента вызывает `IPlatformAlertNotifier`, который критические письма шлёт напрямую через `ISmtpTransport`, минуя очередь, — потому что одна из аварий, о которой он кричит, это смерть очереди.

**Tech Stack:** .NET 10 minimal APIs, EF Core 10 / Npgsql, xUnit, React 19 + TypeScript + Vite, `bun test`, `@afk4/i18n` (ICU MessageFormat).

**Спека:** `docs/superpowers/specs/2026-08-07-platform-observability-and-analytics-design.md` (§0, §1, §4).
**Ветка:** `feat/platform-observability-wave-c` (уже создана, спека в ней закоммичена).

## Global Constraints

- **Никакого пре-рендеренного текста с сервера.** Сервер отдаёт вид инцидента (`Kind`) и числа; строки живут в `locales/{ru,en,tg}.json`, множественное число — ICU-плюралами. Клиент никогда не рендерит серверную строку как пользовательский текст.
- **Новые таблицы — snake_case** (`platform_incidents`, `platform_job_runs`), колонки — PascalCase в кавычках. Сырой SQL в миграциях сверять с `PlatformDbContext`, а не с именем C#-класса.
- **Права проверяются на сервере до обращения к данным.** Экран без данных показывает «неизвестно», а не утверждение об их отсутствии.
- **Ни одного запроса в цикле по сущностям.** Фиксированное число запросов, группировка в памяти — как в `EfPlatformPulseService`.
- **Идемпотентность через ключ в БД**, а не через проверку «а не делали ли мы уже».
- **Тесты на гонки — только на настоящем Postgres.** Частичный уникальный индекс на InMemory не проверяется вовсе; делать вид, что проверяется, нельзя.
- **Деньги в минорных единицах**, валюта TJS; в этом плане денег на экране нет.
- **`bun` вызывать полным путём:** `BUN=/home/fedya/.bun/bin/bun`.
- **Секреты не хардкодить** и не печатать в логах и ответах.
- **Не добавлять AI-подписи** в коммиты, код и комментарии.

---

### Task 1: Сущности инцидентов и прогонов + миграция

**Files:**
- Create: `src/AFK4.Platform.Api/Data/PlatformIncidentEntity.cs`
- Create: `src/AFK4.Platform.Api/Data/PlatformJobRunEntity.cs`
- Modify: `src/AFK4.Platform.Api/Data/PlatformDbContext.cs` (DbSet-ы рядом с `PlatformSupportAccessGrants` на строке ~119; конфигурация рядом с блоком `platform_support_access_grants` на строке ~984)
- Create: миграция в `src/AFK4.Platform.Api/Data/Migrations/`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/PlatformIncidentStoreTests.cs`

**Interfaces:**
- Produces: `PlatformIncidentEntity`, `PlatformJobRunEntity`, `PlatformIncidentKindNames`, `PlatformIncidentSeverityNames`, `PlatformJobOutcomeNames`, `PlatformDbContext.PlatformIncidents`, `PlatformDbContext.PlatformJobRuns`.

- [ ] **Step 1: Написать сущности**

`src/AFK4.Platform.Api/Data/PlatformIncidentEntity.cs`:

```csharp
namespace AFK4.Platform.Api.Data;

/// <summary>
/// Одна открытая или закрытая проблема платформы. Ключ дедупликации держит инвариант
/// «один открытый инцидент на ключ» — повторное обнаружение двигает LastSeenAtUtc,
/// а не заводит вторую строку и второе письмо.
/// </summary>
public sealed class PlatformIncidentEntity
{
    public Guid PlatformIncidentId { get; set; }

    public string Kind { get; set; } = string.Empty;

    /// <summary>Ключ дедупликации, например "job_overdue:invoice_generation".</summary>
    public string DedupKey { get; set; } = string.Empty;

    public string Severity { get; set; } = PlatformIncidentSeverityNames.Warning;

    /// <summary>Короткий машинный контекст (числа и идентификаторы), НЕ готовая фраза.</summary>
    public string DetailsJson { get; set; } = "{}";

    public DateTimeOffset OpenedAtUtc { get; set; }

    public DateTimeOffset LastSeenAtUtc { get; set; }

    public DateTimeOffset? ResolvedAtUtc { get; set; }

    public DateTimeOffset? LastNotifiedAtUtc { get; set; }
}

public static class PlatformIncidentKindNames
{
    public const string JobOverdue = "job_overdue";
    public const string JobFailing = "job_failing";
    public const string NotificationQueueStuck = "notification_queue_stuck";
    public const string BillingOutboxStuck = "billing_outbox_stuck";
}

public static class PlatformIncidentSeverityNames
{
    public const string Warning = "warning";
    public const string Critical = "critical";
}
```

`src/AFK4.Platform.Api/Data/PlatformJobRunEntity.cs`:

```csharp
namespace AFK4.Platform.Api.Data;

/// <summary>Один прогон периодического задания: чем кончился и сколько обработал.</summary>
public sealed class PlatformJobRunEntity
{
    public Guid PlatformJobRunId { get; set; }

    public string JobName { get; set; } = string.Empty;

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset FinishedAtUtc { get; set; }

    public string Outcome { get; set; } = PlatformJobOutcomeNames.Succeeded;

    public int ItemsProcessed { get; set; }

    /// <summary>Усечённый текст ошибки; null при успехе.</summary>
    public string? Error { get; set; }
}

public static class PlatformJobOutcomeNames
{
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
}
```

- [ ] **Step 2: Зарегистрировать в PlatformDbContext**

Рядом с `PlatformSupportAccessGrants` добавить:

```csharp
    public DbSet<PlatformIncidentEntity> PlatformIncidents => Set<PlatformIncidentEntity>();
    public DbSet<PlatformJobRunEntity> PlatformJobRuns => Set<PlatformJobRunEntity>();
```

Рядом с блоком `platform_support_access_grants` в `OnModelCreating`:

```csharp
        modelBuilder.Entity<PlatformIncidentEntity>(entity =>
        {
            entity.ToTable("platform_incidents");
            entity.HasKey(incident => incident.PlatformIncidentId);
            entity.Property(incident => incident.Kind).HasMaxLength(64).IsRequired();
            entity.Property(incident => incident.DedupKey).HasMaxLength(200).IsRequired();
            entity.Property(incident => incident.Severity).HasMaxLength(16).IsRequired();
            entity.Property(incident => incident.DetailsJson).HasMaxLength(1000).IsRequired();
            // Инвариант «один ОТКРЫТЫЙ инцидент на ключ» держит база: без частичного индекса
            // два тика сторожа, наложившись, завели бы две строки и два письма про одно и то же.
            entity.HasIndex(incident => incident.DedupKey)
                .IsUnique()
                .HasFilter("\"ResolvedAtUtc\" IS NULL");
            entity.HasIndex(incident => incident.OpenedAtUtc);
        });

        modelBuilder.Entity<PlatformJobRunEntity>(entity =>
        {
            entity.ToTable("platform_job_runs");
            entity.HasKey(run => run.PlatformJobRunId);
            entity.Property(run => run.JobName).HasMaxLength(64).IsRequired();
            entity.Property(run => run.Outcome).HasMaxLength(16).IsRequired();
            entity.Property(run => run.Error).HasMaxLength(2000);
            entity.HasIndex(run => new { run.JobName, run.StartedAtUtc });
        });
```

- [ ] **Step 3: Написать падающий тест**

`tests/AFK4.Platform.Api.Tests/Platform/PlatformIncidentStoreTests.cs`:

```csharp
using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformIncidentStoreTests
{
    [Fact]
    public async Task JobRun_And_Incident_RoundTrip()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        var now = DateTimeOffset.UtcNow;

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.PlatformJobRuns.Add(new PlatformJobRunEntity
        {
            PlatformJobRunId = Guid.NewGuid(),
            JobName = "invoice_generation",
            StartedAtUtc = now.AddSeconds(-2),
            FinishedAtUtc = now,
            Outcome = PlatformJobOutcomeNames.Succeeded,
            ItemsProcessed = 3
        });
        db.PlatformIncidents.Add(new PlatformIncidentEntity
        {
            PlatformIncidentId = Guid.NewGuid(),
            Kind = PlatformIncidentKindNames.JobOverdue,
            DedupKey = "job_overdue:invoice_generation",
            Severity = PlatformIncidentSeverityNames.Critical,
            DetailsJson = "{\"minutes\":180}",
            OpenedAtUtc = now,
            LastSeenAtUtc = now
        });
        await db.SaveChangesAsync();

        Assert.Equal(1, await db.PlatformJobRuns.CountAsync());
        Assert.Equal(1, await db.PlatformIncidents.CountAsync(incident => incident.ResolvedAtUtc == null));
    }
}
```

- [ ] **Step 4: Прогнать тест — убедиться, что падает**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter PlatformIncidentStoreTests`
Expected: FAIL (`PlatformIncidents` не существует), пока Steps 1-2 не сделаны; после них — PASS.

- [ ] **Step 5: Собрать проект и создать миграцию**

Порядок обязателен: `--no-build` берёт ПОСЛЕДНЮЮ сборку, и без свежего build миграция выйдет пустой.

```bash
dotnet build src/AFK4.Platform.Api
dotnet ef migrations add AddPlatformIncidentsAndJobRuns \
  --project src/AFK4.Platform.Api --output-dir Data/Migrations --no-build
```

Открыть сгенерированный `.cs` и убедиться, что `Up` не пустой и создаёт `platform_incidents` и `platform_job_runs` с частичным индексом (`filter: "\"ResolvedAtUtc\" IS NULL"`). Если `Up` пуст — удалить оба файла (`.cs` и `.Designer.cs`), пересобрать и повторить (`dotnet ef migrations remove` требует живой БД и здесь не сработает).

- [ ] **Step 6: Прогнать тесты**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter PlatformIncidentStoreTests`
Expected: PASS.

- [ ] **Step 7: Коммит**

```bash
git add src/AFK4.Platform.Api/Data tests/AFK4.Platform.Api.Tests/Platform/PlatformIncidentStoreTests.cs
git commit -m "feat(platform): таблицы инцидентов и прогонов заданий"
```

---

### Task 2: Общая обёртка периодических заданий

**Files:**
- Create: `src/AFK4.Platform.Api/Platform/Health/PlatformPeriodicJob.cs`
- Create: `src/AFK4.Platform.Api/Platform/Health/IJobRunRecorder.cs`
- Create: `src/AFK4.Platform.Api/Platform/Health/EfJobRunRecorder.cs`
- Create: `src/AFK4.Platform.Api/Platform/Health/PlatformJobNames.cs`
- Modify: `src/AFK4.Platform.Api/Platform/Billing/InvoiceGenerationHostedService.cs`, `src/AFK4.Platform.Api/Outbox/OutboxDispatcher.cs`, `src/AFK4.Platform.Api/Notifications/NotificationDispatcher.cs`, `src/AFK4.Platform.Api/Notifications/DailySummaryHostedService.cs`, `src/AFK4.Platform.Api/Notifications/ScheduledReportHostedService.cs`, `src/AFK4.Platform.Api/Sessions/AutoProtectionHostedService.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs` (регистрация `IJobRunRecorder`)
- Test: `tests/AFK4.Platform.Api.Tests/Platform/PlatformPeriodicJobTests.cs`, `tests/AFK4.Platform.Api.Tests/Architecture/PeriodicJobRegistrationTests.cs`

**Interfaces:**
- Consumes: `PlatformJobRunEntity`, `PlatformJobOutcomeNames` (Task 1).
- Produces: `abstract class PlatformPeriodicJob : BackgroundService` с `protected abstract string JobName { get; }`, `protected abstract TimeSpan Interval { get; }`, `protected abstract Task<int> TickAsync(CancellationToken)`; `IJobRunRecorder.RecordAsync(string jobName, DateTimeOffset startedAtUtc, DateTimeOffset finishedAtUtc, string outcome, int itemsProcessed, string? error, CancellationToken)`; константы `PlatformJobNames`.

- [ ] **Step 1: Написать константы имён заданий**

`src/AFK4.Platform.Api/Platform/Health/PlatformJobNames.cs`:

```csharp
namespace AFK4.Platform.Api.Platform.Health;

/// <summary>Имена периодических заданий — общий словарь для регистратора прогонов, правил сторожа и экрана.</summary>
public static class PlatformJobNames
{
    public const string InvoiceGeneration = "invoice_generation";
    public const string BillingOutbox = "billing_outbox";
    public const string NotificationDispatch = "notification_dispatch";
    public const string DailySummary = "daily_summary";
    public const string ScheduledReports = "scheduled_reports";
    public const string AutoProtection = "auto_protection";
    public const string HealthWatch = "health_watch";

    /// <summary>Доставка оповещений мимо очереди — результат тоже записывается как прогон.</summary>
    public const string AlertDelivery = "alert_delivery";

    /// <summary>
    /// Задания, за которыми следят правила здоровья. AlertDelivery сюда НЕ входит: он не
    /// периодический, его прогон появляется только когда есть о чём оповещать, и ждать его
    /// по расписанию значило бы заводить инцидент за тишину, которая означает «всё хорошо».
    /// </summary>
    public static readonly IReadOnlyList<string> Watched =
    [
        InvoiceGeneration,
        BillingOutbox,
        NotificationDispatch,
        DailySummary,
        ScheduledReports,
        AutoProtection,
        HealthWatch
    ];
}
```

- [ ] **Step 2: Написать регистратор**

`src/AFK4.Platform.Api/Platform/Health/IJobRunRecorder.cs`:

```csharp
namespace AFK4.Platform.Api.Platform.Health;

/// <summary>Шов записи прогонов: позволяет тестировать обёртку без базы.</summary>
public interface IJobRunRecorder
{
    Task RecordAsync(
        string jobName,
        DateTimeOffset startedAtUtc,
        DateTimeOffset finishedAtUtc,
        string outcome,
        int itemsProcessed,
        string? error,
        CancellationToken cancellationToken);
}
```

`src/AFK4.Platform.Api/Platform/Health/EfJobRunRecorder.cs`:

```csharp
using AFK4.Platform.Api.Data;

namespace AFK4.Platform.Api.Platform.Health;

public sealed class EfJobRunRecorder(PlatformDbContext dbContext) : IJobRunRecorder
{
    // Колонка Error — 2000 символов; исключение с длинным стеком иначе даст 22001 и уронит
    // сам регистратор, то есть авария съела бы запись о себе.
    private const int MaxErrorLength = 2000;

    public async Task RecordAsync(
        string jobName,
        DateTimeOffset startedAtUtc,
        DateTimeOffset finishedAtUtc,
        string outcome,
        int itemsProcessed,
        string? error,
        CancellationToken cancellationToken)
    {
        dbContext.PlatformJobRuns.Add(new PlatformJobRunEntity
        {
            PlatformJobRunId = Guid.NewGuid(),
            JobName = jobName,
            StartedAtUtc = startedAtUtc,
            FinishedAtUtc = finishedAtUtc,
            Outcome = outcome,
            ItemsProcessed = itemsProcessed,
            Error = error is null ? null : error[..Math.Min(error.Length, MaxErrorLength)]
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
```

- [ ] **Step 3: Написать обёртку**

`src/AFK4.Platform.Api/Platform/Health/PlatformPeriodicJob.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Platform.Health;

/// <summary>
/// Общий цикл периодического задания. Обёртка одна на всех намеренно: пока каждый сервис писал
/// свой while+Delay+catch, поломка задания видна была только в логе, а седьмое задание просто
/// забыли бы подключить к наблюдению.
/// </summary>
public abstract class PlatformPeriodicJob(
    IServiceProvider serviceProvider,
    TimeProvider timeProvider,
    ILogger logger) : BackgroundService
{
    protected abstract string JobName { get; }

    protected abstract TimeSpan Interval { get; }

    /// <summary>Одна итерация. Возвращает число обработанных единиц (для строки прогона).</summary>
    protected abstract Task<int> TickAsync(IServiceProvider scopedServices, CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOnceAsync(stoppingToken);

            try
            {
                await Task.Delay(Interval, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>Один тик с записью результата. Открыт для тестов — не требует запуска хоста.</summary>
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var startedAt = timeProvider.GetUtcNow();
        var processed = 0;
        string? error = null;

        try
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            processed = await TickAsync(scope.ServiceProvider, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            logger.LogError(exception, "Periodic job {JobName} tick failed.", JobName);
        }

        // Запись прогона идёт в собственном scope: у тика мог сдохнуть DbContext,
        // и попытка записаться через него потеряла бы ровно ту запись, ради которой всё делается.
        try
        {
            await using var recordScope = serviceProvider.CreateAsyncScope();
            var recorder = recordScope.ServiceProvider.GetRequiredService<IJobRunRecorder>();
            await recorder.RecordAsync(
                JobName,
                startedAt,
                timeProvider.GetUtcNow(),
                error is null ? PlatformJobOutcomeNames.Succeeded : PlatformJobOutcomeNames.Failed,
                processed,
                error,
                cancellationToken);
        }
        catch (Exception recordException)
        {
            logger.LogError(recordException, "Failed to record run of periodic job {JobName}.", JobName);
        }
    }
}
```

Импорт `PlatformJobOutcomeNames` — `using AFK4.Platform.Api.Data;`.

- [ ] **Step 4: Перевести шесть заданий на обёртку**

Каждый из шести сервисов теряет свой `while`/`Task.Delay`/`catch` и становится таким (пример — `InvoiceGenerationHostedService`, остальные по образцу со своими именами, интервалами и телом тика):

```csharp
using AFK4.Platform.Api.Platform.Health;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Platform.Billing;

public sealed class InvoiceGenerationHostedService(
    IServiceProvider serviceProvider,
    TimeProvider timeProvider,
    IOptions<BillingOptions> options,
    ILogger<InvoiceGenerationHostedService> logger)
    : PlatformPeriodicJob(serviceProvider, timeProvider, logger)
{
    private readonly BillingOptions options = options.Value;

    protected override string JobName => PlatformJobNames.InvoiceGeneration;

    protected override TimeSpan Interval => options.GenerationInterval;

    protected override async Task<int> TickAsync(IServiceProvider scopedServices, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var issued = await scopedServices.GetRequiredService<IInvoiceGenerationRunner>().RunAsync(now, cancellationToken);
        var notified = await scopedServices.GetRequiredService<IDunningRunner>().RunAsync(now, cancellationToken);
        return issued + notified;
    }
}
```

Соответствия для остальных пяти:

| Сервис | `JobName` | `Interval` | Тело тика |
|---|---|---|---|
| `OutboxDispatcher` | `PlatformJobNames.BillingOutbox` | `options.PollInterval` (`OutboxOptions`) | `OutboxDispatchRunner.RunAsync(options.DispatchBatchSize, ct)` |
| `NotificationDispatcher` | `PlatformJobNames.NotificationDispatch` | `options.PollInterval` (`NotificationOptions`) | `NotificationDispatchRunner.RunAsync(options.DispatchBatchSize, ct)` |
| `DailySummaryHostedService` | `PlatformJobNames.DailySummary` | `options.DailySummaryInterval` | `IDailySummaryRunner.RunAsync(now, ct)` |
| `ScheduledReportHostedService` | `PlatformJobNames.ScheduledReports` | `options.ScheduledReportInterval` | `IScheduledReportRunner.RunAsync(now, ct)` (сохранить существующую сигнатуру вызова из текущего файла) |
| `AutoProtectionHostedService` | `PlatformJobNames.AutoProtection` | `options.TickInterval` | существующее тело `TickAsync` из текущего файла |

Существующие `logger.LogInformation("… processed {Count} …")` внутри тиков сохранить — они не мешают и остаются полезными в логе.

- [ ] **Step 5: Зарегистрировать регистратор в Program.cs**

Рядом с `builder.Services.AddHostedService<InvoiceGenerationHostedService>();` (строка ~224) добавить до неё:

```csharp
builder.Services.AddScoped<IJobRunRecorder, EfJobRunRecorder>();
```

- [ ] **Step 6: Написать тест обёртки**

`tests/AFK4.Platform.Api.Tests/Platform/PlatformPeriodicJobTests.cs`:

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Health;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformPeriodicJobTests
{
    private sealed class RecordingRecorder : IJobRunRecorder
    {
        public List<(string JobName, string Outcome, int Items, string? Error)> Records { get; } = [];

        public Task RecordAsync(string jobName, DateTimeOffset startedAtUtc, DateTimeOffset finishedAtUtc,
            string outcome, int itemsProcessed, string? error, CancellationToken cancellationToken)
        {
            Records.Add((jobName, outcome, itemsProcessed, error));
            return Task.CompletedTask;
        }
    }

    private sealed class TestJob(IServiceProvider services, TimeProvider time, Func<Task<int>> body)
        : PlatformPeriodicJob(services, time, NullLogger.Instance)
    {
        protected override string JobName => "test_job";
        protected override TimeSpan Interval => TimeSpan.FromMinutes(5);
        protected override Task<int> TickAsync(IServiceProvider scopedServices, CancellationToken cancellationToken) => body();
    }

    private static (ServiceProvider Services, RecordingRecorder Recorder) BuildServices()
    {
        var recorder = new RecordingRecorder();
        var services = new ServiceCollection();
        services.AddScoped<IJobRunRecorder>(_ => recorder);
        return (services.BuildServiceProvider(), recorder);
    }

    [Fact]
    public async Task SuccessfulTick_RecordsSucceededWithItemCount()
    {
        var (services, recorder) = BuildServices();
        var job = new TestJob(services, new FakeTimeProvider(), () => Task.FromResult(7));

        await job.RunOnceAsync(CancellationToken.None);

        var record = Assert.Single(recorder.Records);
        Assert.Equal("test_job", record.JobName);
        Assert.Equal(PlatformJobOutcomeNames.Succeeded, record.Outcome);
        Assert.Equal(7, record.Items);
        Assert.Null(record.Error);
    }

    [Fact]
    public async Task ThrowingTick_RecordsFailedAndSwallowsException()
    {
        var (services, recorder) = BuildServices();
        var job = new TestJob(services, new FakeTimeProvider(), () => throw new InvalidOperationException("boom"));

        await job.RunOnceAsync(CancellationToken.None);

        var record = Assert.Single(recorder.Records);
        Assert.Equal(PlatformJobOutcomeNames.Failed, record.Outcome);
        Assert.Equal("boom", record.Error);
    }
}
```

Если пакета `Microsoft.Extensions.TimeProvider.Testing` в тестовом проекте нет — проверить `grep -rn "FakeTimeProvider" tests/AFK4.Platform.Api.Tests | head -3`; при отсутствии использовать `TimeProvider.System`, тест от этого не зависит.

- [ ] **Step 7: Написать архитектурный тест**

`tests/AFK4.Platform.Api.Tests/Architecture/PeriodicJobRegistrationTests.cs`:

```csharp
using System.Reflection;
using AFK4.Platform.Api.Platform.Health;
using AFK4.Platform.Api.Platform.Identity;
using Microsoft.Extensions.Hosting;

namespace AFK4.Platform.Api.Tests.Architecture;

public sealed class PeriodicJobRegistrationTests
{
    // Одноразовые сервисы старта: они не тикают, записывать им нечего.
    private static readonly HashSet<string> OneShotStartupServices = new(StringComparer.Ordinal)
    {
        "PlatformAdminBootstrapHostedService",
        "BillingPlanSeedHostedService"
    };

    // Смысл теста: наблюдение за фоновыми заданиями не должно держаться на памяти автора
    // седьмого задания. Новый BackgroundService, унаследованный напрямую, назовут поимённо здесь.
    [Fact]
    public void EveryPeriodicBackgroundService_DerivesFromPlatformPeriodicJob()
    {
        var offenders = typeof(PlatformAdminBootstrapHostedService).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsClass: true })
            .Where(type => typeof(BackgroundService).IsAssignableFrom(type))
            .Where(type => !typeof(PlatformPeriodicJob).IsAssignableFrom(type))
            .Where(type => !OneShotStartupServices.Contains(type.Name))
            .Select(type => type.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }
}
```

- [ ] **Step 8: Прогнать тесты**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter "PlatformPeriodicJobTests|PeriodicJobRegistrationTests"`
Expected: PASS.

Затем полный прогон, чтобы рефакторинг шести сервисов ничего не сломал:
Run: `dotnet test tests/AFK4.Platform.Api.Tests`
Expected: столько же зелёных, сколько было до задачи; ни одного нового падения.

- [ ] **Step 9: Коммит**

```bash
git add src/AFK4.Platform.Api tests/AFK4.Platform.Api.Tests
git commit -m "refactor(platform): общий цикл периодических заданий с записью прогонов"
```

---

### Task 3: Служба инцидентов

**Files:**
- Create: `src/AFK4.Platform.Api/Platform/Health/IPlatformIncidentService.cs`
- Create: `src/AFK4.Platform.Api/Platform/Health/EfPlatformIncidentService.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/PlatformIncidentServiceTests.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/PlatformIncidentConcurrencyPostgresTests.cs`

**Interfaces:**
- Consumes: `PlatformIncidentEntity`, `PlatformIncidentSeverityNames` (Task 1).
- Produces: `IPlatformIncidentService` с `OpenOrTouchAsync(string kind, string dedupKey, string severity, string detailsJson, CancellationToken) -> Task<IncidentTransition>`, `ResolveMissingAsync(IReadOnlyCollection<string> stillOpenKeys, CancellationToken) -> Task<IReadOnlyList<PlatformIncidentEntity>>`, `ListOpenAsync(CancellationToken)`; `record IncidentTransition(PlatformIncidentEntity Incident, bool IsNew, bool ShouldRemind)`.

- [ ] **Step 1: Написать падающий тест поведения**

`tests/AFK4.Platform.Api.Tests/Platform/PlatformIncidentServiceTests.cs`:

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Health;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformIncidentServiceTests
{
    [Fact]
    public async Task SecondDetection_DoesNotOpenSecondIncident()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IPlatformIncidentService>();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var first = await service.OpenOrTouchAsync(
            PlatformIncidentKindNames.JobOverdue, "job_overdue:daily_summary",
            PlatformIncidentSeverityNames.Warning, "{\"minutes\":200}", CancellationToken.None);
        var second = await service.OpenOrTouchAsync(
            PlatformIncidentKindNames.JobOverdue, "job_overdue:daily_summary",
            PlatformIncidentSeverityNames.Warning, "{\"minutes\":260}", CancellationToken.None);

        Assert.True(first.IsNew);
        Assert.False(second.IsNew);
        Assert.Equal(1, await db.PlatformIncidents.CountAsync(incident => incident.ResolvedAtUtc == null));
    }

    [Fact]
    public async Task ResolveMissing_ClosesIncidentsNoLongerDetected()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IPlatformIncidentService>();

        await service.OpenOrTouchAsync(
            PlatformIncidentKindNames.JobFailing, "job_failing:billing_outbox",
            PlatformIncidentSeverityNames.Warning, "{}", CancellationToken.None);
        await service.OpenOrTouchAsync(
            PlatformIncidentKindNames.JobFailing, "job_failing:daily_summary",
            PlatformIncidentSeverityNames.Warning, "{}", CancellationToken.None);

        var resolved = await service.ResolveMissingAsync(["job_failing:daily_summary"], CancellationToken.None);

        Assert.Equal("job_failing:billing_outbox", Assert.Single(resolved).DedupKey);
        var open = await service.ListOpenAsync(CancellationToken.None);
        Assert.Equal("job_failing:daily_summary", Assert.Single(open).DedupKey);
    }

    [Fact]
    public async Task ReopeningAfterResolve_CreatesNewIncident()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IPlatformIncidentService>();

        await service.OpenOrTouchAsync(
            PlatformIncidentKindNames.JobOverdue, "job_overdue:auto_protection",
            PlatformIncidentSeverityNames.Warning, "{}", CancellationToken.None);
        await service.ResolveMissingAsync([], CancellationToken.None);
        var reopened = await service.OpenOrTouchAsync(
            PlatformIncidentKindNames.JobOverdue, "job_overdue:auto_protection",
            PlatformIncidentSeverityNames.Warning, "{}", CancellationToken.None);

        Assert.True(reopened.IsNew);
    }
}
```

- [ ] **Step 2: Прогнать — убедиться, что падает**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter PlatformIncidentServiceTests`
Expected: FAIL — `IPlatformIncidentService` не зарегистрирован.

- [ ] **Step 3: Написать службу**

`src/AFK4.Platform.Api/Platform/Health/IPlatformIncidentService.cs`:

```csharp
using AFK4.Platform.Api.Data;

namespace AFK4.Platform.Api.Platform.Health;

/// <summary>Результат обнаружения: сама запись, была ли она заведена сейчас и пора ли напомнить.</summary>
public sealed record IncidentTransition(PlatformIncidentEntity Incident, bool IsNew, bool ShouldRemind);

public interface IPlatformIncidentService
{
    Task<IncidentTransition> OpenOrTouchAsync(
        string kind, string dedupKey, string severity, string detailsJson, CancellationToken cancellationToken);

    /// <summary>Закрывает все открытые инциденты, ключей которых нет в переданном наборе.</summary>
    Task<IReadOnlyList<PlatformIncidentEntity>> ResolveMissingAsync(
        IReadOnlyCollection<string> stillOpenKeys, CancellationToken cancellationToken);

    Task<IReadOnlyList<PlatformIncidentEntity>> ListOpenAsync(CancellationToken cancellationToken);
}
```

`src/AFK4.Platform.Api/Platform/Health/EfPlatformIncidentService.cs`:

```csharp
using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Health;

public sealed class EfPlatformIncidentService(PlatformDbContext dbContext, TimeProvider timeProvider)
    : IPlatformIncidentService
{
    /// <summary>Пока инцидент открыт, напоминание уходит не чаще раза в сутки.</summary>
    private static readonly TimeSpan ReminderInterval = TimeSpan.FromDays(1);

    public async Task<IncidentTransition> OpenOrTouchAsync(
        string kind, string dedupKey, string severity, string detailsJson, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var existing = await dbContext.PlatformIncidents
            .SingleOrDefaultAsync(incident => incident.DedupKey == dedupKey && incident.ResolvedAtUtc == null, cancellationToken);

        if (existing is not null)
        {
            existing.LastSeenAtUtc = now;
            existing.DetailsJson = detailsJson;
            // Ухудшение серьёзности повышаем, обратно НЕ понижаем: инцидент, разово скатившийся
            // из critical в warning, не должен тихо терять приоритет до закрытия.
            if (severity == PlatformIncidentSeverityNames.Critical)
                existing.Severity = PlatformIncidentSeverityNames.Critical;

            var shouldRemind = existing.LastNotifiedAtUtc is null
                || now - existing.LastNotifiedAtUtc.Value >= ReminderInterval;
            if (shouldRemind) existing.LastNotifiedAtUtc = now;

            await dbContext.SaveChangesAsync(cancellationToken);
            return new IncidentTransition(existing, IsNew: false, ShouldRemind: shouldRemind);
        }

        var incident = new PlatformIncidentEntity
        {
            PlatformIncidentId = Guid.NewGuid(),
            Kind = kind,
            DedupKey = dedupKey,
            Severity = severity,
            DetailsJson = detailsJson,
            OpenedAtUtc = now,
            LastSeenAtUtc = now,
            LastNotifiedAtUtc = now
        };
        dbContext.PlatformIncidents.Add(incident);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return new IncidentTransition(incident, IsNew: true, ShouldRemind: true);
        }
        catch (DbUpdateException) when (IsDuplicateOpenIncident(dbContext, incident))
        {
            // Гонка двух наблюдателей: частичный уникальный индекс отклонил вторую вставку.
            // Это НЕ ошибка вызывающего — инцидент уже заведён, письмо уже ушло.
            dbContext.Entry(incident).State = EntityState.Detached;
            var winner = await dbContext.PlatformIncidents
                .SingleAsync(row => row.DedupKey == dedupKey && row.ResolvedAtUtc == null, cancellationToken);
            return new IncidentTransition(winner, IsNew: false, ShouldRemind: false);
        }
    }

    private static bool IsDuplicateOpenIncident(PlatformDbContext dbContext, PlatformIncidentEntity incident) =>
        dbContext.Entry(incident).State == EntityState.Added;

    public async Task<IReadOnlyList<PlatformIncidentEntity>> ResolveMissingAsync(
        IReadOnlyCollection<string> stillOpenKeys, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var open = await dbContext.PlatformIncidents
            .Where(incident => incident.ResolvedAtUtc == null)
            .ToListAsync(cancellationToken);

        var resolved = open.Where(incident => !stillOpenKeys.Contains(incident.DedupKey)).ToList();
        foreach (var incident in resolved) incident.ResolvedAtUtc = now;

        if (resolved.Count > 0) await dbContext.SaveChangesAsync(cancellationToken);
        return resolved;
    }

    public async Task<IReadOnlyList<PlatformIncidentEntity>> ListOpenAsync(CancellationToken cancellationToken) =>
        await dbContext.PlatformIncidents
            .AsNoTracking()
            .Where(incident => incident.ResolvedAtUtc == null)
            .OrderByDescending(incident => incident.Severity == PlatformIncidentSeverityNames.Critical)
            .ThenBy(incident => incident.OpenedAtUtc)
            .ToListAsync(cancellationToken);
}
```

- [ ] **Step 4: Зарегистрировать**

В `Program.cs` рядом с `AddScoped<IJobRunRecorder, EfJobRunRecorder>()`:

```csharp
builder.Services.AddScoped<IPlatformIncidentService, EfPlatformIncidentService>();
```

- [ ] **Step 5: Прогнать поведенческие тесты**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter PlatformIncidentServiceTests`
Expected: PASS.

- [ ] **Step 6: Написать тест гонки на настоящем Postgres**

InMemory не знает частичных уникальных индексов и здесь ложно-зелёный. Образец организации Postgres-теста и `SaveOverlapGate` — `tests/AFK4.Platform.Api.Tests/Platform/PlatformSupportAccessTicketTests.cs`; повторить его схему подключения (строка подключения из переменной окружения, имя БД обязано заканчиваться на `_test`, иначе `PostgresTestAvailabilityTests` справедливо валит прогон при `AFK4_REQUIRE_POSTGRES_TESTS=1`).

`tests/AFK4.Platform.Api.Tests/Platform/PlatformIncidentConcurrencyPostgresTests.cs` — тест: два параллельных `OpenOrTouchAsync` с одним `dedupKey`, чьи `SaveChangesAsync` разведены `SaveOverlapGate` так, что вторая вставка гарантированно наложится на первую. Ожидания: ни один вызов не бросает исключение; ровно один результат с `IsNew == true`; в базе ровно одна открытая строка с этим ключом.

- [ ] **Step 7: Прогнать Postgres-тест**

Run: `AFK4_REQUIRE_POSTGRES_TESTS=1 dotnet test tests/AFK4.Platform.Api.Tests --filter PlatformIncidentConcurrencyPostgresTests`
Expected: PASS, 0 skipped. Если тест пропущен — соединение не настроено, и это надо чинить, а не принимать за зелёный результат.

- [ ] **Step 8: Коммит**

```bash
git add src/AFK4.Platform.Api tests/AFK4.Platform.Api.Tests
git commit -m "feat(platform): служба инцидентов с дедупликацией и закрытием"
```

---

### Task 4: Доставка оповещений

**Files:**
- Create: `src/AFK4.Platform.Api/Platform/Health/IPlatformAlertNotifier.cs`
- Create: `src/AFK4.Platform.Api/Platform/Health/PlatformAlertNotifier.cs`
- Create: `src/AFK4.Platform.Api/Platform/Health/PlatformAlertOptions.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs`
- Modify: `src/AFK4.Platform.Api/appsettings.json` (секция `Alerts`)
- Test: `tests/AFK4.Platform.Api.Tests/Platform/PlatformAlertNotifierTests.cs`

**Interfaces:**
- Consumes: `PlatformIncidentEntity`, `PlatformIncidentSeverityNames`, `PlatformIncidentKindNames` (Task 1); `IJobRunRecorder`, `PlatformJobNames` (Task 2).
- Produces: `IPlatformAlertNotifier` с `NotifyOpenedAsync(PlatformIncidentEntity, CancellationToken)` и `NotifyResolvedAsync(PlatformIncidentEntity, CancellationToken)`.

- [ ] **Step 1: Написать опции**

`src/AFK4.Platform.Api/Platform/Health/PlatformAlertOptions.cs`:

```csharp
namespace AFK4.Platform.Api.Platform.Health;

public sealed class PlatformAlertOptions
{
    public const string ConfigurationSection = "Alerts";

    /// <summary>
    /// Номера для аварийных SMS. Лежат в конфигурации, а не в базе: у сотрудника платформы
    /// телефона нет вообще, и заводить поле, экран ввода и подтверждение номера ради
    /// аварийного канала для команды из единиц человек — это переоткрывать волну A.
    /// Цена решения: смена дежурного номера требует деплоя. Пустой список — не ошибка.
    /// </summary>
    public IReadOnlyList<string> SmsRecipients { get; set; } = [];
}
```

- [ ] **Step 2: Написать падающий тест**

`tests/AFK4.Platform.Api.Tests/Platform/PlatformAlertNotifierTests.cs`:

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Platform.Health;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformAlertNotifierTests
{
    private sealed class CapturingSmtp : ISmtpTransport
    {
        public List<SmtpMessage> Sent { get; } = [];
        public Task SendAsync(SmtpMessage message, CancellationToken cancellationToken)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingSms : ISmsTransport
    {
        public List<SmsMessage> Sent { get; } = [];
        public Task SendAsync(SmsMessage message, CancellationToken cancellationToken)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    private static PlatformIncidentEntity Incident(string kind, string severity) => new()
    {
        PlatformIncidentId = Guid.NewGuid(),
        Kind = kind,
        DedupKey = kind + ":x",
        Severity = severity,
        DetailsJson = "{\"minutes\":180}",
        OpenedAtUtc = DateTimeOffset.UtcNow,
        LastSeenAtUtc = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task CriticalIncident_SendsSmsToConfiguredRecipients()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.SeedPlatformAdminAsync(factory, "watch@platform.test", "Watcher");

        var smtp = new CapturingSmtp();
        var sms = new CapturingSms();
        await using var scope = factory.Services.CreateAsyncScope();
        var notifier = new PlatformAlertNotifier(
            scope.ServiceProvider.GetRequiredService<PlatformDbContext>(),
            smtp, sms,
            scope.ServiceProvider.GetRequiredService<IOptions<NotificationOptions>>(),
            Options.Create(new PlatformAlertOptions { SmsRecipients = ["+992900000000"] }),
            scope.ServiceProvider.GetRequiredService<IJobRunRecorder>(),
            TimeProvider.System,
            NullLogger<PlatformAlertNotifier>.Instance);

        await notifier.NotifyOpenedAsync(
            Incident(PlatformIncidentKindNames.NotificationQueueStuck, PlatformIncidentSeverityNames.Critical),
            CancellationToken.None);

        Assert.NotEmpty(smtp.Sent);
        Assert.Equal("+992900000000", Assert.Single(sms.Sent).ToPhoneNumber);
    }

    [Fact]
    public async Task WarningIncident_SendsEmailButNoSms()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.SeedPlatformAdminAsync(factory, "watch2@platform.test", "Watcher");

        var smtp = new CapturingSmtp();
        var sms = new CapturingSms();
        await using var scope = factory.Services.CreateAsyncScope();
        var notifier = new PlatformAlertNotifier(
            scope.ServiceProvider.GetRequiredService<PlatformDbContext>(),
            smtp, sms,
            scope.ServiceProvider.GetRequiredService<IOptions<NotificationOptions>>(),
            Options.Create(new PlatformAlertOptions { SmsRecipients = ["+992900000000"] }),
            scope.ServiceProvider.GetRequiredService<IJobRunRecorder>(),
            TimeProvider.System,
            NullLogger<PlatformAlertNotifier>.Instance);

        await notifier.NotifyOpenedAsync(
            Incident(PlatformIncidentKindNames.JobFailing, PlatformIncidentSeverityNames.Warning),
            CancellationToken.None);

        Assert.NotEmpty(smtp.Sent);
        Assert.Empty(sms.Sent);
    }

    [Fact]
    public async Task InactiveAdmin_DoesNotReceiveAlert()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.SeedPlatformAdminAsync(factory, "active@platform.test", "Active");
        await PlatformAdminTestHelper.SeedPlatformAdminAsync(factory, "fired@platform.test", "Fired", isActive: false);

        var smtp = new CapturingSmtp();
        await using var scope = factory.Services.CreateAsyncScope();
        var notifier = new PlatformAlertNotifier(
            scope.ServiceProvider.GetRequiredService<PlatformDbContext>(),
            smtp, new CapturingSms(),
            scope.ServiceProvider.GetRequiredService<IOptions<NotificationOptions>>(),
            Options.Create(new PlatformAlertOptions()),
            scope.ServiceProvider.GetRequiredService<IJobRunRecorder>(),
            TimeProvider.System,
            NullLogger<PlatformAlertNotifier>.Instance);

        await notifier.NotifyOpenedAsync(
            Incident(PlatformIncidentKindNames.JobFailing, PlatformIncidentSeverityNames.Warning),
            CancellationToken.None);

        Assert.DoesNotContain(smtp.Sent, message => message.ToAddress == "fired@platform.test");
    }
}
```

- [ ] **Step 3: Прогнать — убедиться, что падает**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter PlatformAlertNotifierTests`
Expected: FAIL — `PlatformAlertNotifier` не существует.

- [ ] **Step 4: Написать оповещатель**

`src/AFK4.Platform.Api/Platform/Health/IPlatformAlertNotifier.cs`:

```csharp
using AFK4.Platform.Api.Data;

namespace AFK4.Platform.Api.Platform.Health;

public interface IPlatformAlertNotifier
{
    Task NotifyOpenedAsync(PlatformIncidentEntity incident, CancellationToken cancellationToken);

    Task NotifyResolvedAsync(PlatformIncidentEntity incident, CancellationToken cancellationToken);
}
```

`src/AFK4.Platform.Api/Platform/Health/PlatformAlertNotifier.cs`:

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Platform.Health;

/// <summary>
/// Доставка оповещений о здоровье платформы. Идёт МИМО очереди уведомлений намеренно: один из
/// видов аварии, о котором надо кричать, — смерть самой очереди, и письмо «очередь встала»,
/// положенное в очередь, не уйдёт никогда. Ретраев здесь нет, поэтому результат каждой попытки
/// пишется как прогон задания alert_delivery: провалившееся предупреждение не должно исчезать.
/// </summary>
public sealed class PlatformAlertNotifier(
    PlatformDbContext dbContext,
    ISmtpTransport smtpTransport,
    ISmsTransport smsTransport,
    IOptions<NotificationOptions> notificationOptions,
    IOptions<PlatformAlertOptions> alertOptions,
    IJobRunRecorder jobRunRecorder,
    TimeProvider timeProvider,
    ILogger<PlatformAlertNotifier> logger) : IPlatformAlertNotifier
{
    private readonly NotificationOptions notificationOptions = notificationOptions.Value;
    private readonly PlatformAlertOptions alertOptions = alertOptions.Value;

    // Виды, после которых теряются деньги или доверие клиентов. Список узкий намеренно:
    // SMS, приходящая на каждый warning, через неделю перестаёт читаться.
    private static readonly HashSet<string> SmsWorthyKinds = new(StringComparer.Ordinal)
    {
        PlatformIncidentKindNames.NotificationQueueStuck,
        PlatformIncidentKindNames.BillingOutboxStuck
    };

    public Task NotifyOpenedAsync(PlatformIncidentEntity incident, CancellationToken cancellationToken) =>
        SendAsync(incident, isResolved: false, cancellationToken);

    public Task NotifyResolvedAsync(PlatformIncidentEntity incident, CancellationToken cancellationToken) =>
        SendAsync(incident, isResolved: true, cancellationToken);

    private async Task SendAsync(PlatformIncidentEntity incident, bool isResolved, CancellationToken cancellationToken)
    {
        var startedAt = timeProvider.GetUtcNow();
        var delivered = 0;
        string? error = null;

        try
        {
            var recipients = await dbContext.PlatformAdminUsers
                .AsNoTracking()
                .Where(admin => admin.IsActive)
                .Select(admin => admin.UserName)
                .ToListAsync(cancellationToken);

            // Тело письма собирается ЗДЕСЬ и намеренно телеграфное: получатель — сотрудник
            // платформы, а не клиент, и его задача — открыть экран «Здоровье», а не прочитать прозу.
            var subject = $"[AFK4 {(isResolved ? "resolved" : incident.Severity)}] {incident.Kind}";
            var body = string.Join('\n',
            [
                $"kind: {incident.Kind}",
                $"key: {incident.DedupKey}",
                $"severity: {incident.Severity}",
                $"opened: {incident.OpenedAtUtc:O}",
                isResolved ? $"resolved: {incident.ResolvedAtUtc:O}" : $"last seen: {incident.LastSeenAtUtc:O}",
                $"details: {incident.DetailsJson}"
            ]);

            foreach (var address in recipients)
            {
                await smtpTransport.SendAsync(
                    new SmtpMessage(
                        notificationOptions.FromAddress,
                        notificationOptions.FromName,
                        address,
                        subject,
                        body,
                        $"<pre>{System.Net.WebUtility.HtmlEncode(body)}</pre>"),
                    cancellationToken);
                delivered++;
            }

            // Отбой по SMS не шлём: разбудить человека ради «всё снова хорошо» — верный способ
            // научить его игнорировать следующую SMS.
            var smsWorthy = !isResolved
                && incident.Severity == PlatformIncidentSeverityNames.Critical
                && (SmsWorthyKinds.Contains(incident.Kind) || IsInvoiceGenerationOverdue(incident));

            if (smsWorthy)
            {
                foreach (var phone in alertOptions.SmsRecipients)
                {
                    await smsTransport.SendAsync(new SmsMessage(phone, subject), cancellationToken);
                    delivered++;
                }
            }
        }
        catch (Exception exception)
        {
            error = exception.Message;
            logger.LogError(exception, "Failed to deliver platform alert for incident {DedupKey}.", incident.DedupKey);
        }

        await jobRunRecorder.RecordAsync(
            PlatformJobNames.AlertDelivery,
            startedAt,
            timeProvider.GetUtcNow(),
            error is null ? PlatformJobOutcomeNames.Succeeded : PlatformJobOutcomeNames.Failed,
            delivered,
            error,
            cancellationToken);
    }

    private static bool IsInvoiceGenerationOverdue(PlatformIncidentEntity incident) =>
        incident.Kind == PlatformIncidentKindNames.JobOverdue
        && incident.DedupKey.EndsWith(':' + PlatformJobNames.InvoiceGeneration, StringComparison.Ordinal);
}
```

- [ ] **Step 5: Зарегистрировать и добавить конфигурацию**

В `Program.cs` рядом с регистрацией службы инцидентов:

```csharp
builder.Services.Configure<PlatformAlertOptions>(
    builder.Configuration.GetSection(PlatformAlertOptions.ConfigurationSection));
builder.Services.AddScoped<IPlatformAlertNotifier, PlatformAlertNotifier>();
```

В `appsettings.json` добавить секцию верхнего уровня (пустой список — рабочее значение по умолчанию; реальные номера задаются переменными окружения на развёртывании, в репозиторий не попадают):

```json
  "Alerts": {
    "SmsRecipients": []
  }
```

- [ ] **Step 6: Прогнать тесты**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter PlatformAlertNotifierTests`
Expected: PASS.

- [ ] **Step 7: Коммит**

```bash
git add src/AFK4.Platform.Api tests/AFK4.Platform.Api.Tests
git commit -m "feat(platform): доставка оповещений об инцидентах мимо очереди"
```

---

### Task 5: Сторож здоровья

**Files:**
- Create: `src/AFK4.Platform.Api/Platform/Health/PlatformHealthRules.cs`
- Create: `src/AFK4.Platform.Api/Platform/Health/PlatformHealthWatchJob.cs`
- Create: `src/AFK4.Platform.Api/Platform/Health/PlatformHealthOptions.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/PlatformHealthRulesTests.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/PlatformHealthWatchJobTests.cs`

**Interfaces:**
- Consumes: `IPlatformIncidentService`, `IncidentTransition` (Task 3); `IPlatformAlertNotifier` (Task 4); `PlatformPeriodicJob`, `PlatformJobNames` (Task 2).
- Produces: `PlatformHealthRules.Evaluate(HealthSnapshot snapshot, DateTimeOffset now) -> IReadOnlyList<DetectedProblem>`; `record HealthSnapshot(IReadOnlyList<JobState> Jobs, int NotificationFailed, int NotificationStuck, int BillingOutboxFailed, int BillingOutboxStuck)`; `record JobState(string JobName, TimeSpan Interval, DateTimeOffset? LastSuccessAtUtc, int ConsecutiveFailures)`; `record DetectedProblem(string Kind, string DedupKey, string Severity, string DetailsJson)`.

- [ ] **Step 1: Написать чистые правила и тест к ним**

Правила — чистая функция без базы: их можно проверить на десятке случаев, не поднимая хост.

`src/AFK4.Platform.Api/Platform/Health/PlatformHealthRules.cs`:

```csharp
using System.Globalization;
using AFK4.Platform.Api.Data;

namespace AFK4.Platform.Api.Platform.Health;

public sealed record JobState(string JobName, TimeSpan Interval, DateTimeOffset? LastSuccessAtUtc, int ConsecutiveFailures);

public sealed record HealthSnapshot(
    IReadOnlyList<JobState> Jobs,
    int NotificationFailed,
    int NotificationStuck,
    int BillingOutboxFailed,
    int BillingOutboxStuck);

public sealed record DetectedProblem(string Kind, string DedupKey, string Severity, string DetailsJson);

/// <summary>
/// Правила здоровья — чистая функция от снимка состояния. Пороги берутся из интервала самого
/// задания, а не из отдельной таблицы порогов: интервал уже живёт в опциях задания, и второй
/// источник правды разошёлся бы с первым при первом же изменении.
/// </summary>
public static class PlatformHealthRules
{
    /// <summary>Нижняя граница окна ожидания: у задания с интервалом в 10 секунд тройной интервал — это шум.</summary>
    private static readonly TimeSpan MinimumOverdueWindow = TimeSpan.FromMinutes(15);

    private const int FailureStreakThreshold = 3;

    /// <summary>Виды инцидентов, которые умеет обнаруживать этот набор правил — ровно их и закрывает сторож.</summary>
    public static readonly IReadOnlyList<string> EvaluatedKinds =
    [
        PlatformIncidentKindNames.JobOverdue,
        PlatformIncidentKindNames.JobFailing,
        PlatformIncidentKindNames.NotificationQueueStuck,
        PlatformIncidentKindNames.BillingOutboxStuck
    ];

    /// <summary>Задания, чья остановка стоит денег: счета не выставляются, письма не уходят.</summary>
    private static readonly HashSet<string> CriticalJobs = new(StringComparer.Ordinal)
    {
        PlatformJobNames.InvoiceGeneration,
        PlatformJobNames.BillingOutbox,
        PlatformJobNames.NotificationDispatch
    };

    public static IReadOnlyList<DetectedProblem> Evaluate(HealthSnapshot snapshot, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var problems = new List<DetectedProblem>();

        foreach (var job in snapshot.Jobs)
        {
            var window = Max(job.Interval * 3, MinimumOverdueWindow);
            var overdueSince = job.LastSuccessAtUtc;
            if (overdueSince is null || now - overdueSince.Value > window)
            {
                var minutes = overdueSince is null ? -1 : (int)(now - overdueSince.Value).TotalMinutes;
                problems.Add(new DetectedProblem(
                    PlatformIncidentKindNames.JobOverdue,
                    $"{PlatformIncidentKindNames.JobOverdue}:{job.JobName}",
                    CriticalJobs.Contains(job.JobName)
                        ? PlatformIncidentSeverityNames.Critical
                        : PlatformIncidentSeverityNames.Warning,
                    Details(("job", job.JobName), ("minutes", minutes.ToString(CultureInfo.InvariantCulture)))));
            }

            if (job.ConsecutiveFailures >= FailureStreakThreshold)
            {
                problems.Add(new DetectedProblem(
                    PlatformIncidentKindNames.JobFailing,
                    $"{PlatformIncidentKindNames.JobFailing}:{job.JobName}",
                    PlatformIncidentSeverityNames.Warning,
                    Details(("job", job.JobName), ("failures", job.ConsecutiveFailures.ToString(CultureInfo.InvariantCulture)))));
            }
        }

        if (snapshot.NotificationFailed > 0 || snapshot.NotificationStuck > 0)
        {
            problems.Add(new DetectedProblem(
                PlatformIncidentKindNames.NotificationQueueStuck,
                PlatformIncidentKindNames.NotificationQueueStuck,
                PlatformIncidentSeverityNames.Critical,
                Details(
                    ("failed", snapshot.NotificationFailed.ToString(CultureInfo.InvariantCulture)),
                    ("stuck", snapshot.NotificationStuck.ToString(CultureInfo.InvariantCulture)))));
        }

        if (snapshot.BillingOutboxFailed > 0 || snapshot.BillingOutboxStuck > 0)
        {
            problems.Add(new DetectedProblem(
                PlatformIncidentKindNames.BillingOutboxStuck,
                PlatformIncidentKindNames.BillingOutboxStuck,
                PlatformIncidentSeverityNames.Critical,
                Details(
                    ("failed", snapshot.BillingOutboxFailed.ToString(CultureInfo.InvariantCulture)),
                    ("stuck", snapshot.BillingOutboxStuck.ToString(CultureInfo.InvariantCulture)))));
        }

        return problems;
    }

    private static TimeSpan Max(TimeSpan left, TimeSpan right) => left > right ? left : right;

    // Детали — только числа и идентификаторы. Готовую фразу здесь собирать нельзя:
    // текст живёт в каталоге переводов, иначе панель на таджикском покажет русскую строку.
    private static string Details(params (string Key, string Value)[] pairs) =>
        '{' + string.Join(',', pairs.Select(pair => $"\"{pair.Key}\":\"{pair.Value}\"")) + '}';
}
```

`tests/AFK4.Platform.Api.Tests/Platform/PlatformHealthRulesTests.cs`:

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Health;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformHealthRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 7, 12, 0, 0, TimeSpan.Zero);

    private static HealthSnapshot Snapshot(params JobState[] jobs) => new(jobs, 0, 0, 0, 0);

    [Fact]
    public void FreshSuccess_ProducesNoProblem()
    {
        var problems = PlatformHealthRules.Evaluate(
            Snapshot(new JobState(PlatformJobNames.DailySummary, TimeSpan.FromHours(1), Now.AddMinutes(-10), 0)), Now);

        Assert.Empty(problems);
    }

    [Fact]
    public void StaleJob_ProducesOverdueProblem()
    {
        var problems = PlatformHealthRules.Evaluate(
            Snapshot(new JobState(PlatformJobNames.DailySummary, TimeSpan.FromHours(1), Now.AddHours(-5), 0)), Now);

        var problem = Assert.Single(problems);
        Assert.Equal(PlatformIncidentKindNames.JobOverdue, problem.Kind);
        Assert.Equal("job_overdue:daily_summary", problem.DedupKey);
        Assert.Equal(PlatformIncidentSeverityNames.Warning, problem.Severity);
    }

    [Fact]
    public void StaleMoneyJob_IsCritical()
    {
        var problems = PlatformHealthRules.Evaluate(
            Snapshot(new JobState(PlatformJobNames.InvoiceGeneration, TimeSpan.FromHours(1), Now.AddHours(-5), 0)), Now);

        Assert.Equal(PlatformIncidentSeverityNames.Critical, Assert.Single(problems).Severity);
    }

    [Fact]
    public void FastJob_UsesMinimumWindowInsteadOfTripleInterval()
    {
        // Тройной интервал у outbox — 30 секунд; без нижней границы окна любая пауза
        // в полминуты порождала бы инцидент.
        var problems = PlatformHealthRules.Evaluate(
            Snapshot(new JobState(PlatformJobNames.BillingOutbox, TimeSpan.FromSeconds(10), Now.AddMinutes(-5), 0)), Now);

        Assert.Empty(problems);
    }

    [Fact]
    public void NeverRanJob_IsOverdue()
    {
        var problems = PlatformHealthRules.Evaluate(
            Snapshot(new JobState(PlatformJobNames.ScheduledReports, TimeSpan.FromHours(1), null, 0)), Now);

        Assert.Equal(PlatformIncidentKindNames.JobOverdue, Assert.Single(problems).Kind);
    }

    [Fact]
    public void ThreeConsecutiveFailures_ProduceFailingProblem()
    {
        var problems = PlatformHealthRules.Evaluate(
            Snapshot(new JobState(PlatformJobNames.DailySummary, TimeSpan.FromHours(1), Now.AddMinutes(-5), 3)), Now);

        Assert.Equal(PlatformIncidentKindNames.JobFailing, Assert.Single(problems).Kind);
    }

    [Fact]
    public void StuckNotificationQueue_IsCritical()
    {
        var problems = PlatformHealthRules.Evaluate(new HealthSnapshot([], 2, 0, 0, 0), Now);

        var problem = Assert.Single(problems);
        Assert.Equal(PlatformIncidentKindNames.NotificationQueueStuck, problem.Kind);
        Assert.Equal(PlatformIncidentSeverityNames.Critical, problem.Severity);
    }
}
```

- [ ] **Step 2: Прогнать тест правил**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter PlatformHealthRulesTests`
Expected: PASS.

- [ ] **Step 3: Написать опции сторожа**

`src/AFK4.Platform.Api/Platform/Health/PlatformHealthOptions.cs`:

```csharp
namespace AFK4.Platform.Api.Platform.Health;

public sealed class PlatformHealthOptions
{
    public const string ConfigurationSection = "Health";

    public TimeSpan WatchInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Сообщение очереди, ждущее дольше этого срока, считается застрявшим.</summary>
    public TimeSpan QueueStuckThreshold { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Прогоны старше этого срока удаляются — история outbox за год не нужна никому.</summary>
    public TimeSpan JobRunRetention { get; set; } = TimeSpan.FromDays(30);
}
```

- [ ] **Step 4: Написать сторожа**

`src/AFK4.Platform.Api/Platform/Health/PlatformHealthWatchJob.cs`:

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Outbox;
using AFK4.Platform.Api.Platform.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Platform.Health;

/// <summary>
/// Применяет правила здоровья к записям прогонов и к двум очередям, заводит и закрывает инциденты
/// и вызывает оповещатель. Само задание тоже периодическое и потому видно на собственном экране;
/// за смертью процесса целиком следит внешняя проверка /api/health.
/// </summary>
public sealed class PlatformHealthWatchJob(
    IServiceProvider serviceProvider,
    TimeProvider timeProvider,
    IOptions<PlatformHealthOptions> healthOptions,
    IOptions<BillingOptions> billingOptions,
    IOptions<NotificationOptions> notificationOptions,
    IOptions<OutboxOptions> outboxOptions,
    IOptions<SessionAutoProtectionOptions> autoProtectionOptions,
    ILogger<PlatformHealthWatchJob> logger)
    : PlatformPeriodicJob(serviceProvider, timeProvider, logger)
{
    private readonly PlatformHealthOptions healthOptions = healthOptions.Value;
    private DateTimeOffset lastPruneAtUtc = DateTimeOffset.MinValue;

    protected override string JobName => PlatformJobNames.HealthWatch;

    protected override TimeSpan Interval => healthOptions.WatchInterval;

    // Словарь покрывает ВЕСЬ PlatformJobNames.Watched. Задание, забытое здесь, молча выпадает
    // из наблюдения — ровно та дыра, которую закрывает этот план. Имя опций автозащиты
    // (`SessionAutoProtectionOptions.TickInterval` или как оно называется фактически) сверить
    // с конструктором AutoProtectionHostedService.
    private IReadOnlyDictionary<string, TimeSpan> JobIntervals => new Dictionary<string, TimeSpan>(StringComparer.Ordinal)
    {
        [PlatformJobNames.InvoiceGeneration] = billingOptions.Value.GenerationInterval,
        [PlatformJobNames.BillingOutbox] = outboxOptions.Value.PollInterval,
        [PlatformJobNames.NotificationDispatch] = notificationOptions.Value.PollInterval,
        [PlatformJobNames.DailySummary] = notificationOptions.Value.DailySummaryInterval,
        [PlatformJobNames.ScheduledReports] = notificationOptions.Value.ScheduledReportInterval,
        [PlatformJobNames.AutoProtection] = autoProtectionOptions.Value.TickInterval,
        [PlatformJobNames.HealthWatch] = healthOptions.WatchInterval
    };

    protected override async Task<int> TickAsync(IServiceProvider scopedServices, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var db = scopedServices.GetRequiredService<PlatformDbContext>();
        var incidents = scopedServices.GetRequiredService<IPlatformIncidentService>();
        var notifier = scopedServices.GetRequiredService<IPlatformAlertNotifier>();

        var snapshot = await BuildSnapshotAsync(db, now, cancellationToken);
        var problems = PlatformHealthRules.Evaluate(snapshot, now);

        var opened = 0;
        foreach (var problem in problems)
        {
            var transition = await incidents.OpenOrTouchAsync(
                problem.Kind, problem.DedupKey, problem.Severity, problem.DetailsJson, cancellationToken);
            if (transition.IsNew || transition.ShouldRemind)
            {
                await notifier.NotifyOpenedAsync(transition.Incident, cancellationToken);
                opened++;
            }
        }

        // Первый аргумент — виды, которые этот проход РЕАЛЬНО проверял. Без него служба
        // закрывала бы и чужие открытые инциденты просто потому, что их ключей нет в наборе.
        var resolved = await incidents.ResolveMissingAsync(
            PlatformHealthRules.EvaluatedKinds,
            problems.Select(problem => problem.DedupKey).ToHashSet(StringComparer.Ordinal),
            cancellationToken);
        foreach (var incident in resolved)
        {
            await notifier.NotifyResolvedAsync(incident, cancellationToken);
        }

        await PruneJobRunsAsync(db, now, cancellationToken);
        return opened + resolved.Count;
    }

    private async Task<HealthSnapshot> BuildSnapshotAsync(PlatformDbContext db, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var since = now - TimeSpan.FromDays(2);
        // Один запрос на всю историю прогонов за окно; группировка в памяти — как в пульсе.
        var runs = await db.PlatformJobRuns
            .AsNoTracking()
            .Where(run => run.StartedAtUtc >= since)
            .Select(run => new { run.JobName, run.StartedAtUtc, run.Outcome })
            .ToListAsync(cancellationToken);

        var runsByJob = runs
            .GroupBy(run => run.JobName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(run => run.StartedAtUtc).ToList(), StringComparer.Ordinal);

        var jobs = new List<JobState>();
        foreach (var (jobName, interval) in JobIntervals)
        {
            runsByJob.TryGetValue(jobName, out var jobRuns);
            var lastSuccess = jobRuns?
                .FirstOrDefault(run => run.Outcome == PlatformJobOutcomeNames.Succeeded)?.StartedAtUtc;
            var streak = jobRuns is null ? 0 : jobRuns.TakeWhile(run => run.Outcome == PlatformJobOutcomeNames.Failed).Count();
            jobs.Add(new JobState(jobName, interval, lastSuccess, streak));
        }

        var stuckBefore = now - healthOptions.QueueStuckThreshold;
        var notificationFailed = await db.NotificationOutbox
            .CountAsync(row => row.Status == NotificationOutboxStatus.Failed, cancellationToken);
        var notificationStuck = await db.NotificationOutbox
            .CountAsync(row => row.Status == NotificationOutboxStatus.Pending && row.CreatedUtc < stuckBefore, cancellationToken);
        var outboxFailed = await db.OutboxMessages
            .CountAsync(row => row.Status == OutboxMessageStatus.Failed, cancellationToken);
        var outboxStuck = await db.OutboxMessages
            .CountAsync(row => row.Status == OutboxMessageStatus.Pending && row.CreatedAtUtc < stuckBefore, cancellationToken);

        return new HealthSnapshot(jobs, notificationFailed, notificationStuck, outboxFailed, outboxStuck);
    }

    private async Task PruneJobRunsAsync(PlatformDbContext db, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (now - lastPruneAtUtc < TimeSpan.FromDays(1)) return;
        lastPruneAtUtc = now;

        var cutoff = now - healthOptions.JobRunRetention;
        await db.PlatformJobRuns.Where(run => run.StartedAtUtc < cutoff).ExecuteDeleteAsync(cancellationToken);
    }
}
```

Точные имена `DbSet` и полей для очередей (`NotificationOutbox`, `OutboxMessages`, `CreatedUtc`, `CreatedAtUtc`, `OutboxMessageStatus`) сверить с `PlatformDbContext` и с `src/AFK4.Platform.Api/Outbox/EfBillingOutbox.cs` — если имена отличаются, использовать фактические, а не эти.

- [ ] **Step 5: Зарегистрировать**

В `Program.cs` рядом с остальными `AddHostedService`:

```csharp
builder.Services.Configure<PlatformHealthOptions>(
    builder.Configuration.GetSection(PlatformHealthOptions.ConfigurationSection));
builder.Services.AddHostedService<PlatformHealthWatchJob>();
```

- [ ] **Step 6: Написать сквозной тест сторожа**

`tests/AFK4.Platform.Api.Tests/Platform/PlatformHealthWatchJobTests.cs` — два теста через `PlatformApiFactory` и `RunOnceAsync`:

1. **Застрявшая очередь заводит инцидент и шлёт оповещение.** Посеять строку `NotificationOutboxEntity` со `Status = Failed`, подменить `ISmtpTransport` на перехватывающий (через `factory.WithWebHostBuilder` + `ConfigureTestServices`, как это делают существующие тесты уведомлений — найти образец: `grep -rln "ISmtpTransport" tests/AFK4.Platform.Api.Tests`), выполнить `RunOnceAsync`. Ожидания: в `PlatformIncidents` одна открытая строка с `Kind == notification_queue_stuck`; перехватчик получил хотя бы одно письмо.
2. **Исчезновение проблемы закрывает инцидент.** После первого прогона удалить проблемную строку очереди и выполнить `RunOnceAsync` второй раз. Ожидания: у инцидента `ResolvedAtUtc != null`; открытых инцидентов этого вида не осталось.

Оба теста должны сеять хотя бы одного активного администратора платформы (`PlatformAdminTestHelper.SeedPlatformAdminAsync`), иначе получателей нет и письмо не с кем сверять.

Третий тест — на полноту словаря интервалов:

```csharp
    [Fact]
    public void JobIntervals_CoverEveryWatchedJob()
    {
        await using var factory = new PlatformApiFactory();
        _ = factory.CreateClient();
        var job = factory.Services.GetServices<IHostedService>().OfType<PlatformHealthWatchJob>().Single();

        // Словарь приватный: проверяем через наблюдаемый эффект — сторож должен знать
        // о каждом задании из Watched, иначе задание молча выпадает из наблюдения.
        var covered = PlatformHealthRules
            .Evaluate(new HealthSnapshot(
                PlatformJobNames.Watched.Select(name => new JobState(name, TimeSpan.FromHours(1), null, 0)).ToList(),
                0, 0, 0, 0), DateTimeOffset.UtcNow)
            .Select(problem => problem.DedupKey.Split(':')[1])
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(PlatformJobNames.Watched.ToHashSet(StringComparer.Ordinal), covered);
        Assert.NotNull(job);
    }
```

Если приватный словарь `JobIntervals` окажется удобнее проверить напрямую — сделать его `internal` и покрыть тестом буквально; смысл теста в том, что забытое в словаре задание должно валить сборку тестов, а не тихо выпадать из наблюдения.

- [ ] **Step 7: Прогнать тесты**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter "PlatformHealthWatchJobTests|PlatformHealthRulesTests"`
Expected: PASS.

- [ ] **Step 8: Коммит**

```bash
git add src/AFK4.Platform.Api tests/AFK4.Platform.Api.Tests
git commit -m "feat(platform): сторож здоровья заводит и закрывает инциденты"
```

---

### Task 6: Право и эндпоинт здоровья

**Files:**
- Modify: `src/AFK4.Shared.Contracts/Platform/Auth/PlatformAdminPermissionNames.cs`
- Modify: `src/AFK4.Platform.Api/Platform/Identity/PlatformAdminPermissionCatalog.cs`
- Create: `src/AFK4.Shared.Contracts/Platform/Health/PlatformHealthContracts.cs`
- Create: `src/AFK4.Platform.Api/Platform/Health/IPlatformHealthOverviewService.cs`
- Create: `src/AFK4.Platform.Api/Platform/Health/EfPlatformHealthOverviewService.cs`
- Create: `src/AFK4.Platform.Api/Endpoints/PlatformHealthEndpoints.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/PlatformHealthEndpointTests.cs`

**Interfaces:**
- Consumes: `IPlatformIncidentService` (Task 3), `PlatformJobNames` (Task 2).
- Produces: `GET /api/platform/health/overview` → `PlatformHealthOverviewDto`; константа `PlatformAdminPermissionNames.ViewPlatformHealth = "platform.health.view"`.

- [ ] **Step 1: Написать контракты**

`src/AFK4.Shared.Contracts/Platform/Health/PlatformHealthContracts.cs`:

```csharp
namespace AFK4.Shared.Contracts.Platform.Health;

/// <summary>
/// Состояние одного задания. Kind/JobName едут кодом: клиент никогда не рендерит серверную
/// строку как пользовательский текст — у каждого имени есть перевод в каталоге.
/// </summary>
public sealed record JobHealthDto(
    string JobName,
    DateTimeOffset? LastRunAtUtc,
    DateTimeOffset? LastSuccessAtUtc,
    string? LastOutcome,
    int LastItemsProcessed,
    string? LastError,
    int ConsecutiveFailures);

public sealed record QueueHealthDto(string QueueName, int PendingCount, int FailedCount, int StuckCount);

public sealed record IncidentDto(
    Guid IncidentId,
    string Kind,
    string DedupKey,
    string Severity,
    string DetailsJson,
    DateTimeOffset OpenedAtUtc,
    DateTimeOffset LastSeenAtUtc);

public sealed record PlatformHealthOverviewDto(
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<JobHealthDto> Jobs,
    IReadOnlyList<QueueHealthDto> Queues,
    IReadOnlyList<IncidentDto> OpenIncidents);

public static class PlatformQueueNames
{
    public const string Notifications = "notifications";
    public const string BillingOutbox = "billing_outbox";
}
```

- [ ] **Step 2: Добавить право**

В `PlatformAdminPermissionNames.cs`:

```csharp
    public const string ViewPlatformHealth = "platform.health.view";
```

В `PlatformAdminPermissionCatalog.cs` добавить `PlatformAdminPermissionNames.ViewPlatformHealth` **в оба** набора — и `PlatformAdmin`, и `PlatformSupport`: поддержке состояние платформы нужнее всех, она первой узнаёт о жалобах.

- [ ] **Step 3: Написать падающий тест эндпоинта**

`tests/AFK4.Platform.Api.Tests/Platform/PlatformHealthEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Health;
using AFK4.Shared.Contracts.Platform.Auth;
using AFK4.Shared.Contracts.Platform.Health;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformHealthEndpointTests
{
    [Fact]
    public async Task GET_overview_WithPermission_ReturnsJobsAndIncidents()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var now = DateTimeOffset.UtcNow;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            db.PlatformJobRuns.Add(new PlatformJobRunEntity
            {
                PlatformJobRunId = Guid.NewGuid(),
                JobName = PlatformJobNames.InvoiceGeneration,
                StartedAtUtc = now.AddMinutes(-3),
                FinishedAtUtc = now.AddMinutes(-3).AddSeconds(1),
                Outcome = PlatformJobOutcomeNames.Succeeded,
                ItemsProcessed = 2
            });
            db.PlatformIncidents.Add(new PlatformIncidentEntity
            {
                PlatformIncidentId = Guid.NewGuid(),
                Kind = PlatformIncidentKindNames.NotificationQueueStuck,
                DedupKey = PlatformIncidentKindNames.NotificationQueueStuck,
                Severity = PlatformIncidentSeverityNames.Critical,
                DetailsJson = "{\"failed\":\"2\"}",
                OpenedAtUtc = now.AddHours(-1),
                LastSeenAtUtc = now
            });
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/platform/health/overview");
        var overview = await response.Content.ReadFromJsonAsync<PlatformHealthOverviewDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(overview);
        Assert.Contains(overview!.Jobs, job => job.JobName == PlatformJobNames.InvoiceGeneration && job.LastItemsProcessed == 2);
        Assert.Contains(overview.OpenIncidents, incident => incident.Kind == PlatformIncidentKindNames.NotificationQueueStuck);
    }

    [Fact]
    public async Task GET_overview_WithoutAuthentication_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/platform/health/overview");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GET_overview_WithoutPermission_ReturnsForbidden()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(
            factory, client, userName: "nohealth@platform.test", roles: [PlatformAdminRoleNames.PlatformSupport]);

        // Право выдано обеим ролям, поэтому «без права» проверяется ролью с ПУСТЫМ набором:
        // сверить фактическую сигнатуру AuthorizeAsAsync и использовать существующий в проекте
        // способ выдать сессию без platform.health.view (см. другие *EndpointTests с Forbidden).
        var response = await client.GetAsync("/api/platform/health/overview");

        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }
}
```

Третий тест довести до настоящей проверки 403 по образцу `PlatformDebtEndpointTests.GET_debt_WithoutPermission_ReturnsForbidden` — там уже решён вопрос, как выдать сессию без нужного права; заглушка `NotEqual(InternalServerError)` в финальном коде недопустима.

- [ ] **Step 4: Написать службу обзора**

`src/AFK4.Platform.Api/Platform/Health/IPlatformHealthOverviewService.cs`:

```csharp
using AFK4.Shared.Contracts.Platform.Health;

namespace AFK4.Platform.Api.Platform.Health;

public interface IPlatformHealthOverviewService
{
    Task<PlatformHealthOverviewDto> GetOverviewAsync(CancellationToken cancellationToken);
}
```

`EfPlatformHealthOverviewService` собирает DTO тем же способом, что `PlatformHealthWatchJob.BuildSnapshotAsync`: один запрос прогонов за окно в 2 суток с группировкой в памяти, два счётчика на очередь, открытые инциденты через `IPlatformIncidentService.ListOpenAsync`. Список заданий — ключи того же словаря интервалов; чтобы словарь не разошёлся между сторожем и обзором, вынести его в `PlatformJobNames.All` (статический `IReadOnlyList<string>`) и в обоих местах строить интервалы по нему.

- [ ] **Step 5: Написать эндпоинт**

`src/AFK4.Platform.Api/Endpoints/PlatformHealthEndpoints.cs` — по образцу `PlatformDebtEndpoints.cs`:

```csharp
using AFK4.Platform.Api.Platform.Health;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Shared.Contracts.Platform.Auth;

namespace AFK4.Platform.Api.Endpoints;

internal static class PlatformHealthEndpoints
{
    public static void MapPlatformHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/api/platform/health/overview", async (
            PlatformAdminAuthorizationService authorizationService,
            IPlatformHealthOverviewService overviewService,
            CancellationToken cancellationToken) =>
        {
            // Право проверяется ДО обращения к данным — не после.
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ViewPlatformHealth);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            return Results.Ok(await overviewService.GetOverviewAsync(cancellationToken));
        });
    }
}
```

Зарегистрировать в `Program.cs` рядом с `MapPlatformPulseEndpoints()` и добавить `builder.Services.AddScoped<IPlatformHealthOverviewService, EfPlatformHealthOverviewService>();`.

- [ ] **Step 6: Прогнать тесты**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter PlatformHealthEndpointTests`
Expected: PASS, включая настоящий 403 в третьем тесте.

- [ ] **Step 7: Коммит**

```bash
git add src/AFK4.Platform.Api src/AFK4.Shared.Contracts tests/AFK4.Platform.Api.Tests
git commit -m "feat(platform): эндпоинт обзора здоровья под собственным правом"
```

---

### Task 7: Экран «Здоровье» в панели

**Files:**
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json`
- Modify: `src/AFK4.PlatformControl.Web/src/api/types.ts` (типы `JobHealth`, `QueueHealth`, `Incident`, `HealthOverview`)
- Create: `src/AFK4.PlatformControl.Web/src/api/platformClients/health.ts`
- Modify: `src/AFK4.PlatformControl.Web/src/api/platformClient.ts` (поле `health` в фасаде — сверить фактическое имя файла фасада: `grep -rn "new DebtApi" src/AFK4.PlatformControl.Web/src`)
- Create: `src/AFK4.PlatformControl.Web/src/platform/health/useHealth.ts`
- Create: `src/AFK4.PlatformControl.Web/src/platform/health/healthModel.ts`
- Create: `src/AFK4.PlatformControl.Web/src/platform/health/HealthScreen.tsx`
- Modify: `src/AFK4.PlatformControl.Web/src/auth/platformAccess.ts`
- Modify: `src/AFK4.PlatformControl.Web/src/platform/nav.ts`
- Modify: `src/AFK4.PlatformControl.Web/src/routing/platformRoute.ts`
- Modify: `src/AFK4.PlatformControl.Web/src/App.tsx`
- Test: `src/AFK4.PlatformControl.Web/src/platform/health/healthModel.test.ts`, `src/AFK4.PlatformControl.Web/src/platform/health/HealthScreen.test.tsx`

**Interfaces:**
- Consumes: `GET /api/platform/health/overview` → `PlatformHealthOverviewDto` (Task 6).
- Produces: маршрут `/admin/health`, capability `health.read`.

- [ ] **Step 1: Добавить строки в три каталога**

В `locales/ru.json` (и параллельно в `en.json`, `tg.json` — реальными переводами, не копией русского: guard-тест ловит `tg === ru`):

```
platform.health.title            Здоровье платформы
platform.health.subtitle         Фоновые задания, очереди доставки и открытые проблемы
platform.health.jobs.title       Задания
platform.health.queues.title     Очереди
platform.health.incidents.title  Открытые проблемы
platform.health.incidents.empty  Открытых проблем нет
platform.health.lastSuccess      Последний успех: {time}
platform.health.neverRan         Ни разу не отработало
platform.health.failureStreak    {count, plural, one {# неудача подряд} few {# неудачи подряд} other {# неудач подряд}}
platform.health.queue.pending    {count, plural, one {# в очереди} few {# в очереди} other {# в очереди}}
platform.health.queue.failed     {count, plural, one {# провалено} few {# провалено} other {# провалено}}
platform.health.queue.stuck      {count, plural, one {# застряло} few {# застряло} other {# застряло}}
platform.health.job.invoice_generation      Выставление счетов
platform.health.job.billing_outbox          Очередь биллинга
platform.health.job.notification_dispatch   Рассылка уведомлений
platform.health.job.daily_summary           Ежедневная сводка
platform.health.job.scheduled_reports       Отчёты по расписанию
platform.health.job.auto_protection         Автозащита сессий
platform.health.job.health_watch            Сторож здоровья
platform.health.job.alert_delivery          Доставка оповещений
platform.health.incident.job_overdue                Задание не отрабатывает
platform.health.incident.job_failing                Задание падает
platform.health.incident.notification_queue_stuck   Рассылка встала
platform.health.incident.billing_outbox_stuck       Очередь биллинга встала
platform.health.severity.warning    Требует внимания
platform.health.severity.critical   Критично
platform.health.queue.notifications Уведомления
platform.health.queue.billing_outbox Биллинг
nav.platform.health              Здоровье
```

Точный формат файла — существующий (плоские ключи, ICU-синтаксис как в соседних `platform.debt.*`). После правки:

```bash
BUN=/home/fedya/.bun/bin/bun; cd packages/i18n && "$BUN" run gen && cd ../..
```

- [ ] **Step 2: Написать модель и тест к ней**

`healthModel.ts` — чистые функции без React:

```typescript
import type { HealthOverview, Incident, JobHealth } from '@/api/types';

export type JobStatus = 'ok' | 'failing' | 'never';

// Статус выводится из данных задания, а не приходит строкой с сервера:
// сервер отдаёт факты, экран решает, как их назвать.
export function jobStatus(job: JobHealth): JobStatus {
  if (job.lastSuccessAtUtc === null && job.lastRunAtUtc === null) return 'never';
  if (job.consecutiveFailures > 0) return 'failing';
  return 'ok';
}

export function sortIncidents(incidents: readonly Incident[]): Incident[] {
  return [...incidents].sort((left, right) => {
    if (left.severity !== right.severity) return left.severity === 'critical' ? -1 : 1;
    return left.openedAtUtc.localeCompare(right.openedAtUtc);
  });
}

export function hasCritical(overview: HealthOverview): boolean {
  return overview.openIncidents.some(incident => incident.severity === 'critical');
}
```

`healthModel.test.ts` — тесты на все три функции: задание без прогонов → `'never'`; задание с двумя провалами подряд → `'failing'`; свежий успех → `'ok'`; сортировка ставит `critical` выше `warning`, а внутри одной серьёзности — старший инцидент первым; `hasCritical` истинно только при наличии критического.

- [ ] **Step 3: Написать клиент и хук**

`api/platformClients/health.ts`:

```typescript
import type { PlatformTransport } from '../platformTransport';
import type { HealthOverview } from '../types';

export class HealthApi {
  public constructor(private readonly transport: PlatformTransport) {}

  public getOverview(): Promise<HealthOverview> {
    return this.transport.send<HealthOverview>('GET', '/api/platform/health/overview');
  }
}
```

`platform/health/useHealth.ts` — по образцу `platform/billing/useDebt.ts`, состояние `{ status: 'loading' | 'error' | 'ready' }` с `retry`. Ошибку НЕ проглатывать: экран в состоянии `error` показывает `ErrorState` с кнопкой повтора, а не пустой список — иначе панель уверенно скажет «проблем нет» ровно тогда, когда не смогла их загрузить.

- [ ] **Step 4: Написать экран**

`platform/health/HealthScreen.tsx` — три блока (`Card`) в порядке «Открытые проблемы → Задания → Очереди»: проблема важнее списка. Использовать существующие `Card`/`Badge`/`ErrorState`/`LoadingCards` и `useI18n`. Имена заданий, видов инцидентов и очередей переводить через ключи `platform.health.job.*`, `platform.health.incident.*`, `platform.health.queue.*` по значению с сервера; для неизвестного значения показывать само значение (новый вид инцидента не должен ронять экран пустотой).

- [ ] **Step 5: Подключить маршрут, право и рейл**

- `platformAccess.ts`: в `PlatformCapability` добавить `'health.read'`, в `CAPABILITY_PERMISSIONS` — `'health.read': ['platform.health.view']`.
- `platformRoute.ts`: в `PlatformRoute` добавить `{ kind: 'health' }`; в `resolvePlatformRoute` — `if (path === '/admin/health') return { kind: 'health' };`; в `pathForPlatformRoute` — `case 'health': return '/admin/health';`.
- `nav.ts`: пункт `{ key: 'health', labelKey: 'nav.platform.health', path: '/admin/health', icon: Activity, capability: 'health.read' }` (иконка `Activity` из `lucide-react`) — поставить после `journal`, перед `settings`.
- `App.tsx`: в `capabilityForRoute` — `case 'health': return 'health.read';`; в цепочке рендера — `: route.kind === 'health' ? <HealthScreen client={client.health} />`.

- [ ] **Step 6: Написать тест экрана**

`HealthScreen.test.tsx` — три теста с фейковым клиентом (объект с `getOverview`), без `mock.module` (он течёт на весь процесс):

1. Обзор с критическим инцидентом → на экране виден его переведённый заголовок и метка «Критично».
2. Пустой список инцидентов → виден текст `platform.health.incidents.empty`, и НЕ виден ни один заголовок инцидента.
3. Отказ клиента (`getOverview` реджектит) → виден `ErrorState` с кнопкой повтора, и на экране НЕТ текста «Открытых проблем нет» — экран без данных не утверждает, что всё хорошо.

- [ ] **Step 7: Прогнать тесты и сборку**

```bash
BUN=/home/fedya/.bun/bin/bun
cd src/AFK4.PlatformControl.Web && "$BUN" test && "$BUN" run build && cd ../..
cd packages/i18n && "$BUN" test && cd ../..
```

Expected: всё зелёное. `bun test` не тайпчекает — `bun run build` (`tsc -b && vite build`) обязателен: он проверяет и тестовые файлы.

- [ ] **Step 8: Коммит**

```bash
git add locales src/AFK4.PlatformControl.Web packages/i18n
git commit -m "feat(platform-control): экран здоровья платформы"
```

---

## Финальная проверка плана

После Task 7 прогнать целиком, прежде чем считать план выполненным:

```bash
AFK4_REQUIRE_POSTGRES_TESTS=1 dotnet test tests/AFK4.Platform.Api.Tests
BUN=/home/fedya/.bun/bin/bun
cd src/AFK4.PlatformControl.Web && "$BUN" test && "$BUN" run build
```

Ожидание: 0 упавших, 0 пропущенных Postgres-тестов. Число зелёных бэкенд-тестов должно вырасти относительно базы `main` (1686 на момент закрытия волны B) — падение числа означает потерянный при рефакторинге Task 2 тест, а не «стало чище».
