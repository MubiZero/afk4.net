# История клуба (волна C, план 3) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Суточные снимки по каждому филиалу (`branch_daily_snapshots`) и вкладка «Динамика» в паспорте организации, показывающая 30 дней выручки, сеансов и дней без агента.

**Architecture:** Отдельное периодическое задание `branch_snapshots` (наследник существующего `PlatformPeriodicJob`) раз в час доснимает недостающие сутки по всем филиалам. Свёртка суток — чистая функция `BranchDailySnapshotBuilder`: раннер делает фиксированное число запросов (по одному на источник за всё окно), функция раскладывает строки по филиалам и календарным суткам **в часовом поясе филиала**. Чтение — один эндпоинт, отдающий готовые строки снимков без пересчёта.

**Tech Stack:** ASP.NET Core minimal API, EF Core 10 / Npgsql, xUnit; React 19 + TypeScript, `bun test` + happy-dom, `recharts`, `@afk4/i18n` (ICU), `@afk4/money`.

## Отклонения от спеки (§3) и почему

Спека — `docs/superpowers/specs/2026-08-07-platform-observability-and-analytics-design.md`, раздел «3. История клуба». Три места, где план сознательно расходится с её буквой:

1. **Не «то же суточное задание», а отдельное `branch_snapshots`.** Одно задание = одна запись прогона = одна причина отказа. Если клубную свёртку уронит одно кривое устройство, а живёт она внутри `subscription_snapshots`, экран здоровья будет уверенно врать, что провалились снимки подписок. Задание регистрируется в `PlatformJobNames.Watched` наравне с остальными.
2. **`AgentAlive` — `bool?`, а не `bool`.** Спека сама называет это поле единственным, не выводимым задним числом. Значит, для дня, доснятого после простоя, честное значение — «неизвестно», а не «мёртв». `false` в доснятом дне — это выдуманный факт ровно того сорта, против которого написан весь эпик: экран показал бы «клуб был мёртв 12 дней», когда мёртв был наш собственный процесс.
3. **Сутки считаются в часовом поясе филиала (`BranchEntity.PreferredTimeZone`), а не в UTC.** У поля в коде уже стоит комментарий, что оно и есть основа для границ суток в отчётах. Клубы в UTC+5 и работают ночью: по UTC-суткам вся выручка с полуночи до 05:00 по местному падала бы во «вчера». В снимке хранится дата местных суток.

Дополнительно к списку полей спеки в снимке хранится `OrganizationId` (чтобы читать историю организации одним запросом без join) и `CurrencyCode` (без неё интерфейсу нечем форматировать сумму; так же сделано в снимке подписок).

## Global Constraints

- **Ни одного пре-рендеренного текста с сервера.** Числа и коды едут кодом; строки живут в `packages/i18n`, множественное число — ICU-плюралами.
- **Деньги — в минорных единицах.** Конвертация в мажорные только на границе интерфейса, через существующий `minorToMajor` + `formatCurrency` из `@afk4/money`. Своего деления на 100 в коде быть не должно.
- **Ни одного запроса на клуб или на организацию в цикле.** Раннер делает фиксированное число запросов за всё окно и группирует в памяти.
- **Идемпотентность суточного задания** обеспечивается уникальным ключом (`BranchId`, `SnapshotDate`) в БД, а не проверкой «а не запускались ли мы уже».
- **Права проверяются на сервере до обращения к данным.** Экран, не получивший данных, показывает «неизвестно», а не утверждение об их отсутствии.
- **Отсутствующий снимок ≠ нулевой день.** Ни сервер, ни интерфейс не дорисовывают нулями сутки, за которые снимка нет: это разные факты («клуб не работал» и «мы не сняли»).
- **Новые таблицы — snake_case** (`branch_daily_snapshots`), колонки PascalCase в кавычках.
- **Определение выручки филиала не изобретается заново.** Единственный источник правды — правило, уже действующее в `EfOperatorDashboardService`: POS-нетто (платежи видов `payment`/`refund`) плюс игровая выручка (`GameplayCharge` + `PostpaidDebt` − `Refund` из `LedgerEntries`).
- **Тесты на гонки — только на настоящем Postgres** (`SaveOverlapGate`); уникальный индекс на InMemory не проверяется, и делать вид, что проверяется, нельзя.
- **Таджикские строки — настоящий таджикский.** Guard-тест `packages/i18n/src/messages.test.ts` валит `tg === ru` вне списка заимствований.

---

### Task 1: Таблица `branch_daily_snapshots`

**Files:**
- Create: `src/AFK4.Platform.Api/Data/BranchDailySnapshotEntity.cs`
- Modify: `src/AFK4.Platform.Api/Data/PlatformDbContext.cs` (DbSet рядом с `SubscriptionDailySnapshots`, строка ~125; конфигурация рядом со строкой ~1017)
- Create: миграция в `src/AFK4.Platform.Api/Data/Migrations/` (имя `AddBranchDailySnapshots`)
- Test: `tests/AFK4.Platform.Api.Tests/Data/BranchDailySnapshotSchemaTests.cs`

**Interfaces:**
- Produces: `BranchDailySnapshotEntity`, `PlatformDbContext.BranchDailySnapshots`, имя уникального индекса `IX_branch_daily_snapshots_Branch_Date`.

- [ ] **Step 1: Написать сущность**

`src/AFK4.Platform.Api/Data/BranchDailySnapshotEntity.cs`:

```csharp
namespace AFK4.Platform.Api.Data;

/// <summary>
/// Свёрнутые сутки одного филиала. Первые три метрики выводимы из событий и задним числом, но
/// хранятся, чтобы вкладка стоила один дешёвый запрос и чтобы «клуб был мёртв 12 дней из 30»
/// вообще существовало как факт, а не как результат ежеразового пересчёта.
/// </summary>
public sealed class BranchDailySnapshotEntity
{
    public Guid BranchDailySnapshotId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    /// <summary>
    /// Календарные сутки в ЧАСОВОМ ПОЯСЕ ФИЛИАЛА (<see cref="BranchEntity.PreferredTimeZone"/>),
    /// а не в UTC: клуб в UTC+5 работает ночью, и по UTC-суткам вся выручка с полуночи до пяти
    /// утра падала бы во «вчера».
    /// </summary>
    public DateOnly SnapshotDate { get; set; }

    public int SessionCount { get; set; }

    public long RevenueMinorUnits { get; set; }

    public string CurrencyCode { get; set; } = "TJS";

    public int ShiftOpenedCount { get; set; }

    /// <summary>
    /// Выходил ли клуб на связь. Единственное поле, не выводимое задним числом: heartbeat
    /// перезаписывается. <c>null</c> — «неизвестно»: так помечается день, доснятый после простоя
    /// самой платформы. Записать сюда <c>false</c> значило бы обвинить клуб в нашем простое.
    /// </summary>
    public bool? AgentAlive { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
```

- [ ] **Step 2: Подключить к контексту**

В `PlatformDbContext.cs` рядом с `SubscriptionDailySnapshots` добавить DbSet:

```csharp
public DbSet<BranchDailySnapshotEntity> BranchDailySnapshots => Set<BranchDailySnapshotEntity>();
```

и конфигурацию рядом с конфигурацией `SubscriptionDailySnapshotEntity`:

```csharp
modelBuilder.Entity<BranchDailySnapshotEntity>(entity =>
{
    entity.ToTable("branch_daily_snapshots");
    entity.HasKey(snapshot => snapshot.BranchDailySnapshotId);
    entity.Property(snapshot => snapshot.CurrencyCode).HasMaxLength(3).IsRequired();
    entity.HasIndex(snapshot => new { snapshot.BranchId, snapshot.SnapshotDate })
        .IsUnique()
        .HasDatabaseName("IX_branch_daily_snapshots_Branch_Date");
    entity.HasIndex(snapshot => new { snapshot.OrganizationId, snapshot.SnapshotDate });
});
```

- [ ] **Step 3: Написать тест схемы**

`tests/AFK4.Platform.Api.Tests/Data/BranchDailySnapshotSchemaTests.cs` — тест читает модель EF, не БД, поэтому идёт на InMemory-фабрике:

```csharp
using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests.Data;

public sealed class BranchDailySnapshotSchemaTests
{
    [Fact]
    public void Snapshot_HasUniqueIndexOnBranchAndDate()
    {
        using var factory = new PlatformApiFactory();
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var entityType = dbContext.Model.FindEntityType(typeof(BranchDailySnapshotEntity));
        Assert.NotNull(entityType);
        Assert.Equal("branch_daily_snapshots", entityType!.GetTableName());

        var unique = entityType.GetIndexes().Single(index => index.IsUnique);
        Assert.Equal(
            new[] { nameof(BranchDailySnapshotEntity.BranchId), nameof(BranchDailySnapshotEntity.SnapshotDate) },
            unique.Properties.Select(property => property.Name).ToArray());
    }

    [Fact]
    public void AgentAlive_IsNullable_SoBackfilledDaysCanSayUnknown()
    {
        using var factory = new PlatformApiFactory();
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var property = dbContext.Model
            .FindEntityType(typeof(BranchDailySnapshotEntity))!
            .FindProperty(nameof(BranchDailySnapshotEntity.AgentAlive));

        Assert.NotNull(property);
        Assert.True(property!.IsNullable);
    }
}
```

Уже существующие тесты в этой папке покажут точные `using`-и для фабрики и `GetRequiredService` — свериться с соседним файлом, а не угадывать.

- [ ] **Step 4: Прогнать тесты**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~BranchDailySnapshotSchemaTests`
Expected: PASS (сущность и конфигурация уже написаны на шагах 1-2).

- [ ] **Step 5: Создать миграцию**

Сборка ПЕРЕД генерацией обязательна: `--no-build` переиспользует прошлую сборку и молча выдаёт пустую миграцию.

```bash
dotnet build src/AFK4.Platform.Api
dotnet ef migrations add AddBranchDailySnapshots \
  --project src/AFK4.Platform.Api \
  --output-dir Data/Migrations \
  --no-build
```

Проверить глазами, что в `Up` есть `CreateTable("branch_daily_snapshots")` с колонкой `AgentAlive` типа `boolean` **nullable: true** и оба индекса. Пустая миграция (blank `Up`/`Down`) означает, что сборка не подхватилась — удалить `.cs` и `.Designer.cs`, пересобрать, повторить.

- [ ] **Step 6: Прогнать сборку и тесты проекта**

Run: `dotnet build src/AFK4.Platform.Api && dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~Data`
Expected: PASS

- [ ] **Step 7: Коммит**

```bash
git add src/AFK4.Platform.Api/Data tests/AFK4.Platform.Api.Tests/Data
git commit -m "feat(platform): таблица суточных снимков филиала"
```

---

### Task 2: Общее правило выручки филиала

Правило «сколько филиал заработал» уже живёт в `EfOperatorDashboardService` (строки ~117-126). Копировать его во второе место нельзя: два одинаковых денежных правила расходятся молча. Выносим в чистую функцию и переключаем дашборд на неё — поведение обязано остаться прежним.

**Files:**
- Create: `src/AFK4.Platform.Api/Platform/Analytics/BranchRevenue.cs`
- Modify: `src/AFK4.Platform.Api/Dashboard/EfOperatorDashboardService.cs:117-126`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/Analytics/BranchRevenueTests.cs`

**Interfaces:**
- Produces: `BranchRevenue.PosNet(...)`, `BranchRevenue.Gameplay(...)`, `BranchRevenue.PaymentCounts` (см. сигнатуры ниже) — Task 3 считает ими выручку суток.

- [ ] **Step 1: Написать падающий тест**

`tests/AFK4.Platform.Api.Tests/Platform/Analytics/BranchRevenueTests.cs`:

```csharp
using AFK4.Platform.Api.Platform.Analytics;
using AFK4.Shared.Contracts.Billing;

namespace AFK4.Platform.Api.Tests.Platform.Analytics;

public sealed class BranchRevenueTests
{
    [Fact]
    public void PosNet_CountsPaymentsAndRefunds_IgnoringOtherKinds()
    {
        var posNet = BranchRevenue.PosNet(
        [
            ("payment", 5_000L),
            ("refund", -1_500L),
            ("deposit", 90_000L)
        ]);

        Assert.Equal(3_500L, posNet);
    }

    [Fact]
    public void PosNet_MatchesKindsCaseInsensitively()
    {
        Assert.Equal(700L, BranchRevenue.PosNet([("Payment", 700L)]));
    }

    [Fact]
    public void Gameplay_AddsChargesAndDebt_SubtractsRefunds()
    {
        var gameplay = BranchRevenue.Gameplay(
        [
            (LedgerEntryTypeNames.GameplayCharge, -4_000L),
            (LedgerEntryTypeNames.PostpaidDebt, 2_000L),
            (LedgerEntryTypeNames.Refund, 500L),
            (LedgerEntryTypeNames.Topup, 100_000L)
        ]);

        Assert.Equal(4_000L + 2_000L - 500L, gameplay);
    }

    [Fact]
    public void Gameplay_IgnoresNegativePostpaidDebt()
    {
        // Погашение долга приходит той же строкой с отрицательной суммой — это не выручка суток,
        // выручка была зачтена в момент возникновения долга.
        Assert.Equal(0L, BranchRevenue.Gameplay([(LedgerEntryTypeNames.PostpaidDebt, -2_000L)]));
    }
}
```

Точное имя константы `Topup` (или ближайшего типа записи, не входящего в выручку) взять из `LedgerEntryTypeNames` — если такой нет, использовать любую другую существующую константу, лишь бы она не входила в тройку выручки.

- [ ] **Step 2: Прогнать тест — убедиться, что падает**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~BranchRevenueTests`
Expected: FAIL, компиляция не проходит — `BranchRevenue` не существует.

- [ ] **Step 3: Написать функцию**

`src/AFK4.Platform.Api/Platform/Analytics/BranchRevenue.cs`:

```csharp
using AFK4.Shared.Contracts.Billing;

namespace AFK4.Platform.Api.Platform.Analytics;

/// <summary>
/// Что считается выручкой филиала. Правило одно на весь проект: до этого оно жило только внутри
/// операторского дашборда, и второй его экземпляр в снимках разошёлся бы с первым молча — цифры
/// в двух местах перестали бы сходиться, и никакой тест этого не заметил бы.
/// </summary>
public static class BranchRevenue
{
    public const string DefaultCurrencyCode = "TJS";

    private const string PaymentKindPayment = "payment";
    private const string PaymentKindRefund = "refund";

    /// <summary>Нетто по кассе: платежи и возвраты (возврат приходит отрицательной суммой).</summary>
    public static long PosNet(IEnumerable<(string Kind, long AmountMinorUnits)> payments) =>
        payments.Sum(payment =>
            IsKind(payment.Kind, PaymentKindPayment) || IsKind(payment.Kind, PaymentKindRefund)
                ? payment.AmountMinorUnits
                : 0);

    /// <summary>Игровая выручка: списания за игру и возникший постоплатный долг минус возвраты.</summary>
    public static long Gameplay(IEnumerable<(string Kind, long AmountMinorUnits)> entries) =>
        entries.Sum(entry => entry.Kind switch
        {
            LedgerEntryTypeNames.GameplayCharge => Math.Abs(entry.AmountMinorUnits),
            LedgerEntryTypeNames.PostpaidDebt => Math.Max(0, entry.AmountMinorUnits),
            LedgerEntryTypeNames.Refund => -Math.Abs(entry.AmountMinorUnits),
            _ => 0
        });

    private static bool IsKind(string actual, string expected) =>
        string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 4: Прогнать тест — убедиться, что проходит**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~BranchRevenueTests`
Expected: PASS

- [ ] **Step 5: Переключить дашборд на общую функцию**

В `EfOperatorDashboardService.cs` заменить два выражения (строки ~117-126) на вызовы:

```csharp
var posNetSalesMinorUnits = BranchRevenue.PosNet(
    payments.Select(payment => (Kind: payment.PaymentKind, payment.AmountMinorUnits)));
var gameplayRevenueMinorUnits = BranchRevenue.Gameplay(
    ledgerEntries.Select(entry => (Kind: entry.EntryType, entry.AmountMinorUnits)));
```

Имена элементов кортежа проставлены явно (`Kind:`) намеренно: при `TreatWarningsAsErrors` расхождение выведенного имени с именем в сигнатуре — ошибка сборки, а не подсказка.

Локальные константы `PaymentKindPayment`/`PaymentKindRefund` и приватный `IsKind`, если после этого не осталось других их пользователей в файле, удалить — иначе мёртвый код на месте бывшего правила. `DefaultCurrencyCode` в дашборде оставить как есть: он там про другую роль (резолв валюты), трогать не нужно.

- [ ] **Step 6: Прогнать тесты дашборда — поведение обязано не измениться**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~Dashboard`
Expected: PASS, без единого изменения ожиданий в существующих тестах. Если какой-то тест дашборда пришлось поправить — это не рефакторинг, а изменение поведения: остановиться и доложить.

- [ ] **Step 7: Коммит**

```bash
git add src/AFK4.Platform.Api/Platform/Analytics/BranchRevenue.cs src/AFK4.Platform.Api/Dashboard/EfOperatorDashboardService.cs tests/AFK4.Platform.Api.Tests/Platform/Analytics/BranchRevenueTests.cs
git commit -m "refactor(platform): вынести правило выручки филиала в общую функцию"
```

---

### Task 3: Чистая свёртка суток филиала

**Files:**
- Create: `src/AFK4.Platform.Api/Platform/Analytics/BranchDailySnapshotBuilder.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/Analytics/BranchDailySnapshotBuilderTests.cs`

**Interfaces:**
- Consumes: `BranchRevenue` (Task 2).
- Produces: типы `BranchSnapshotBranch`, `BranchSnapshotInput`, `BranchDayFacts` и метод `BranchDailySnapshotBuilder.Build(BranchSnapshotInput input)` — Task 4 вызывает его и превращает `BranchDayFacts` в строки таблицы.

- [ ] **Step 1: Написать падающие тесты**

`tests/AFK4.Platform.Api.Tests/Platform/Analytics/BranchDailySnapshotBuilderTests.cs`:

```csharp
using AFK4.Platform.Api.Platform.Analytics;
using AFK4.Shared.Contracts.Billing;

namespace AFK4.Platform.Api.Tests.Platform.Analytics;

public sealed class BranchDailySnapshotBuilderTests
{
    private static readonly Guid Organization = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Branch = Guid.Parse("22222222-2222-2222-2222-222222222222");

    // Клуб в UTC+5 без перехода на летнее время.
    private static BranchSnapshotBranch Dushanbe(DateTimeOffset createdAtUtc) =>
        new(Branch, Organization, "Asia/Dushanbe", createdAtUtc);

    private static BranchSnapshotInput Input(
        DateTimeOffset now,
        BranchSnapshotBranch branch,
        DateOnly? lastSnapshotDate = null,
        IReadOnlyList<BranchSnapshotEvent>? sessionStarts = null,
        IReadOnlyList<BranchSnapshotMoney>? payments = null,
        IReadOnlyList<BranchSnapshotMoney>? ledgerEntries = null,
        IReadOnlyList<BranchSnapshotEvent>? shiftOpens = null,
        DateTimeOffset? lastHeartbeatUtc = null) =>
        new(
            now,
            [branch],
            lastSnapshotDate is null
                ? new Dictionary<Guid, DateOnly>()
                : new Dictionary<Guid, DateOnly> { [branch.BranchId] = lastSnapshotDate.Value },
            sessionStarts ?? [],
            payments ?? [],
            ledgerEntries ?? [],
            shiftOpens ?? [],
            lastHeartbeatUtc is null
                ? new Dictionary<Guid, DateTimeOffset>()
                : new Dictionary<Guid, DateTimeOffset> { [branch.BranchId] = lastHeartbeatUtc.Value });

    [Fact]
    public void NewBranch_GetsOnlyYesterday_NotAFabricatedMonth()
    {
        var now = new DateTimeOffset(2026, 8, 8, 3, 0, 0, TimeSpan.Zero); // 08:00 в Душанбе
        var input = Input(now, Dushanbe(new DateTimeOffset(2026, 8, 6, 0, 0, 0, TimeSpan.Zero)));

        var facts = BranchDailySnapshotBuilder.Build(input);

        var day = Assert.Single(facts);
        Assert.Equal(new DateOnly(2026, 8, 7), day.Date);
    }

    [Fact]
    public void BranchWithHistory_BackfillsFromItsOwnLastDate()
    {
        var now = new DateTimeOffset(2026, 8, 8, 3, 0, 0, TimeSpan.Zero);
        var input = Input(
            now,
            Dushanbe(new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero)),
            lastSnapshotDate: new DateOnly(2026, 8, 4));

        var facts = BranchDailySnapshotBuilder.Build(input);

        Assert.Equal(
            new[] { new DateOnly(2026, 8, 5), new DateOnly(2026, 8, 6), new DateOnly(2026, 8, 7) },
            facts.Select(fact => fact.Date).ToArray());
    }

    [Fact]
    public void Backfill_IsCappedAtThirtyDays()
    {
        var now = new DateTimeOffset(2026, 8, 8, 3, 0, 0, TimeSpan.Zero);
        var input = Input(
            now,
            Dushanbe(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            lastSnapshotDate: new DateOnly(2025, 1, 1));

        var facts = BranchDailySnapshotBuilder.Build(input);

        Assert.Equal(31, facts.Count); // окно [вчера-30; вчера] включительно
        Assert.Equal(new DateOnly(2026, 7, 8), facts[0].Date);
    }

    [Fact]
    public void Backfill_NeverStartsBeforeTheBranchExisted()
    {
        var now = new DateTimeOffset(2026, 8, 8, 3, 0, 0, TimeSpan.Zero);
        var input = Input(now, Dushanbe(new DateTimeOffset(2026, 8, 5, 20, 0, 0, TimeSpan.Zero)));

        // Филиала не было до 6 августа по местному времени (5 августа 20:00 UTC = 6 августа 01:00),
        // поэтому «нулевых суток» до его появления не выдумываем.
        var facts = BranchDailySnapshotBuilder.Build(
            input with { LastSnapshotDates = new Dictionary<Guid, DateOnly> { [Branch] = new DateOnly(2026, 7, 1) } });

        Assert.Equal(new DateOnly(2026, 8, 6), facts[0].Date);
    }

    [Fact]
    public void DayBoundary_FollowsBranchTimeZone_NotUtc()
    {
        var now = new DateTimeOffset(2026, 8, 8, 3, 0, 0, TimeSpan.Zero);
        // 7 августа 21:00 UTC = 8 августа 02:00 в Душанбе — это уже СЕГОДНЯ по местному,
        // и во вчерашние сутки попасть не должно.
        var lateNight = new DateTimeOffset(2026, 8, 7, 21, 0, 0, TimeSpan.Zero);
        // 6 августа 20:00 UTC = 7 августа 01:00 по местному — это вчерашние сутки.
        var earlyMorning = new DateTimeOffset(2026, 8, 6, 20, 0, 0, TimeSpan.Zero);

        var input = Input(
            now,
            Dushanbe(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            sessionStarts: [new BranchSnapshotEvent(Branch, lateNight), new BranchSnapshotEvent(Branch, earlyMorning)]);

        var day = Assert.Single(BranchDailySnapshotBuilder.Build(input));
        Assert.Equal(new DateOnly(2026, 8, 7), day.Date);
        Assert.Equal(1, day.SessionCount);
    }

    [Fact]
    public void Revenue_IsPosNetPlusGameplay()
    {
        var now = new DateTimeOffset(2026, 8, 8, 3, 0, 0, TimeSpan.Zero);
        var atNoon = new DateTimeOffset(2026, 8, 7, 7, 0, 0, TimeSpan.Zero); // 12:00 в Душанбе

        var input = Input(
            now,
            Dushanbe(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            payments: [new BranchSnapshotMoney(Branch, atNoon, "payment", 5_000L, "TJS")],
            ledgerEntries: [new BranchSnapshotMoney(Branch, atNoon, LedgerEntryTypeNames.GameplayCharge, -3_000L, "TJS")]);

        var day = Assert.Single(BranchDailySnapshotBuilder.Build(input));
        Assert.Equal(8_000L, day.RevenueMinorUnits);
        Assert.Equal("TJS", day.CurrencyCode);
    }

    [Fact]
    public void AgentAlive_IsTrue_WhenHeartbeatIsYoungerThanADay()
    {
        var now = new DateTimeOffset(2026, 8, 8, 3, 0, 0, TimeSpan.Zero);
        var input = Input(
            now,
            Dushanbe(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            lastHeartbeatUtc: now.AddHours(-2));

        Assert.True(Assert.Single(BranchDailySnapshotBuilder.Build(input)).AgentAlive);
    }

    [Fact]
    public void AgentAlive_IsFalse_WhenHeartbeatIsOlderThanADay()
    {
        var now = new DateTimeOffset(2026, 8, 8, 3, 0, 0, TimeSpan.Zero);
        var input = Input(
            now,
            Dushanbe(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            lastHeartbeatUtc: now.AddDays(-3));

        Assert.False(Assert.Single(BranchDailySnapshotBuilder.Build(input)).AgentAlive);
    }

    [Fact]
    public void AgentAlive_IsUnknown_ForBackfilledDays()
    {
        var now = new DateTimeOffset(2026, 8, 8, 3, 0, 0, TimeSpan.Zero);
        var input = Input(
            now,
            Dushanbe(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            lastSnapshotDate: new DateOnly(2026, 8, 4),
            lastHeartbeatUtc: now.AddHours(-1));

        var facts = BranchDailySnapshotBuilder.Build(input);

        // Живость меряется только «сейчас». Записать в позавчерашние сутки сегодняшний heartbeat —
        // выдумать факт; записать false — обвинить клуб в нашем простое.
        Assert.Null(facts[0].AgentAlive);
        Assert.Null(facts[1].AgentAlive);
        Assert.True(facts[^1].AgentAlive);
    }

    [Fact]
    public void UnknownTimeZoneId_FallsBackToUtc_WithoutThrowing()
    {
        var now = new DateTimeOffset(2026, 8, 8, 3, 0, 0, TimeSpan.Zero);
        var branch = new BranchSnapshotBranch(
            Branch, Organization, "Mars/Olympus", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var day = Assert.Single(BranchDailySnapshotBuilder.Build(Input(now, branch)));

        Assert.Equal(new DateOnly(2026, 8, 7), day.Date);
    }

    [Fact]
    public void ShiftOpens_AreCountedPerDay()
    {
        var now = new DateTimeOffset(2026, 8, 8, 3, 0, 0, TimeSpan.Zero);
        var atNoon = new DateTimeOffset(2026, 8, 7, 7, 0, 0, TimeSpan.Zero);

        var input = Input(
            now,
            Dushanbe(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            shiftOpens: [new BranchSnapshotEvent(Branch, atNoon), new BranchSnapshotEvent(Branch, atNoon.AddHours(8))]);

        Assert.Equal(2, Assert.Single(BranchDailySnapshotBuilder.Build(input)).ShiftOpenedCount);
    }
}
```

- [ ] **Step 2: Прогнать тесты — убедиться, что падают**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~BranchDailySnapshotBuilderTests`
Expected: FAIL, компиляция не проходит — типов не существует.

- [ ] **Step 3: Написать свёртку**

`src/AFK4.Platform.Api/Platform/Analytics/BranchDailySnapshotBuilder.cs`:

```csharp
namespace AFK4.Platform.Api.Platform.Analytics;

/// <summary>Филиал в том объёме, в каком свёртке нужно знать о нём.</summary>
public sealed record BranchSnapshotBranch(
    Guid BranchId,
    Guid OrganizationId,
    string TimeZoneId,
    DateTimeOffset CreatedAtUtc);

/// <summary>Событие без денег: старт сеанса, открытие смены.</summary>
public sealed record BranchSnapshotEvent(Guid BranchId, DateTimeOffset AtUtc);

/// <summary>Денежная строка: платёж или запись реестра.</summary>
public sealed record BranchSnapshotMoney(
    Guid BranchId,
    DateTimeOffset AtUtc,
    string Kind,
    long AmountMinorUnits,
    string CurrencyCode);

public sealed record BranchSnapshotInput(
    DateTimeOffset Now,
    IReadOnlyList<BranchSnapshotBranch> Branches,
    IReadOnlyDictionary<Guid, DateOnly> LastSnapshotDates,
    IReadOnlyList<BranchSnapshotEvent> SessionStarts,
    IReadOnlyList<BranchSnapshotMoney> Payments,
    IReadOnlyList<BranchSnapshotMoney> LedgerEntries,
    IReadOnlyList<BranchSnapshotEvent> ShiftOpens,
    IReadOnlyDictionary<Guid, DateTimeOffset> LastHeartbeatUtc);

public sealed record BranchDayFacts(
    Guid BranchId,
    Guid OrganizationId,
    DateOnly Date,
    int SessionCount,
    long RevenueMinorUnits,
    string CurrencyCode,
    int ShiftOpenedCount,
    bool? AgentAlive);

/// <summary>
/// Свёртка суток филиала. Чистая функция: раннер отвечает за запросы, эта — за правила, и её
/// правила (граница суток, «неизвестно» вместо выдуманного нуля) проверяются без базы.
/// </summary>
public static class BranchDailySnapshotBuilder
{
    /// <summary>Насколько глубоко задание готово доснять пропущенные дни после простоя.</summary>
    public const int MaxBackfillDays = 30;

    /// <summary>Порог живости: heartbeat старше суток — клуб на связь не выходил.</summary>
    private static readonly TimeSpan HeartbeatWindow = TimeSpan.FromDays(1);

    public static IReadOnlyList<BranchDayFacts> Build(BranchSnapshotInput input)
    {
        var sessionsByBranch = input.SessionStarts.ToLookup(item => item.BranchId);
        var paymentsByBranch = input.Payments.ToLookup(item => item.BranchId);
        var ledgerByBranch = input.LedgerEntries.ToLookup(item => item.BranchId);
        var shiftsByBranch = input.ShiftOpens.ToLookup(item => item.BranchId);

        var facts = new List<BranchDayFacts>();

        foreach (var branch in input.Branches)
        {
            var zone = ResolveZone(branch.TimeZoneId);
            var localToday = LocalDate(input.Now, zone);
            var lastCompleteDay = localToday.AddDays(-1);

            var startDay = input.LastSnapshotDates.TryGetValue(branch.BranchId, out var lastDate)
                ? lastDate.AddDays(1)
                : lastCompleteDay;

            // Досъёмка ограничена: чем дальше в прошлое, тем меньше оснований доверять
            // реконструкции задним числом.
            var earliest = lastCompleteDay.AddDays(-MaxBackfillDays);
            if (startDay < earliest) startDay = earliest;

            // И не раньше, чем филиал появился: сутки до его создания — не «ноль выручки»,
            // а отсутствие клуба.
            var born = LocalDate(branch.CreatedAtUtc, zone);
            if (startDay < born) startDay = born;

            if (startDay > lastCompleteDay) continue;

            var sessionsByDay = GroupEvents(sessionsByBranch[branch.BranchId], zone);
            var shiftsByDay = GroupEvents(shiftsByBranch[branch.BranchId], zone);
            var paymentsByDay = GroupMoney(paymentsByBranch[branch.BranchId], zone);
            var ledgerByDay = GroupMoney(ledgerByBranch[branch.BranchId], zone);
            var agentAliveNow = ResolveAgentAlive(input, branch.BranchId);

            for (var day = startDay; day <= lastCompleteDay; day = day.AddDays(1))
            {
                var dayPayments = paymentsByDay.TryGetValue(day, out var paid) ? paid : [];
                var dayLedger = ledgerByDay.TryGetValue(day, out var entries) ? entries : [];

                facts.Add(new BranchDayFacts(
                    branch.BranchId,
                    branch.OrganizationId,
                    day,
                    sessionsByDay.TryGetValue(day, out var sessions) ? sessions : 0,
                    BranchRevenue.PosNet(dayPayments.Select(item => (item.Kind, item.AmountMinorUnits)))
                        + BranchRevenue.Gameplay(dayLedger.Select(item => (item.Kind, item.AmountMinorUnits))),
                    ResolveCurrency(dayPayments, dayLedger),
                    shiftsByDay.TryGetValue(day, out var shifts) ? shifts : 0,
                    // Живость меряется только «сейчас» — heartbeat перезаписывается. Поэтому она
                    // ставится единственным суткам, которые только что закончились; доснятым
                    // задним числом честно остаётся «неизвестно».
                    day == lastCompleteDay ? agentAliveNow : null));
            }
        }

        return facts;
    }

    private static bool? ResolveAgentAlive(BranchSnapshotInput input, Guid branchId)
    {
        // Ни одного устройства не заведено — про связь клуба сказать нечего, и «мёртв» здесь
        // было бы неправдой про клуб, который просто ещё не разворачивали.
        if (!input.LastHeartbeatUtc.TryGetValue(branchId, out var heartbeat)) return null;
        return input.Now - heartbeat <= HeartbeatWindow;
    }

    private static string ResolveCurrency(
        IReadOnlyList<BranchSnapshotMoney> payments,
        IReadOnlyList<BranchSnapshotMoney> ledger) =>
        payments.FirstOrDefault()?.CurrencyCode
        ?? ledger.FirstOrDefault()?.CurrencyCode
        ?? BranchRevenue.DefaultCurrencyCode;

    private static Dictionary<DateOnly, int> GroupEvents(IEnumerable<BranchSnapshotEvent> events, TimeZoneInfo zone) =>
        events
            .GroupBy(item => LocalDate(item.AtUtc, zone))
            .ToDictionary(group => group.Key, group => group.Count());

    private static Dictionary<DateOnly, IReadOnlyList<BranchSnapshotMoney>> GroupMoney(
        IEnumerable<BranchSnapshotMoney> rows,
        TimeZoneInfo zone) =>
        rows
            .GroupBy(item => LocalDate(item.AtUtc, zone))
            .ToDictionary(group => group.Key, group => (IReadOnlyList<BranchSnapshotMoney>)group.ToList());

    private static DateOnly LocalDate(DateTimeOffset instant, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);

    private static TimeZoneInfo ResolveZone(string timeZoneId)
    {
        // Кривой идентификатор не должен ронять задание для ВСЕХ филиалов: считаем такой клуб
        // живущим по UTC и идём дальше.
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
```

- [ ] **Step 4: Прогнать тесты — убедиться, что проходят**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~BranchDailySnapshotBuilderTests`
Expected: PASS (все 11)

- [ ] **Step 5: Коммит**

```bash
git add src/AFK4.Platform.Api/Platform/Analytics/BranchDailySnapshotBuilder.cs tests/AFK4.Platform.Api.Tests/Platform/Analytics/BranchDailySnapshotBuilderTests.cs
git commit -m "feat(platform): свёртка суток филиала по его часовому поясу"
```

---

### Task 4: Задание `branch_snapshots`

**Files:**
- Create: `src/AFK4.Platform.Api/Platform/Analytics/IBranchSnapshotRunner.cs`
- Create: `src/AFK4.Platform.Api/Platform/Analytics/EfBranchSnapshotRunner.cs`
- Create: `src/AFK4.Platform.Api/Platform/Analytics/BranchSnapshotJob.cs`
- Modify: `src/AFK4.Platform.Api/Platform/Health/PlatformJobNames.cs` (константа + `Watched`)
- Modify: `src/AFK4.Platform.Api/Platform/Health/PlatformJobIntervalCatalog.cs` (интервал)
- Modify: `src/AFK4.Platform.Api/Program.cs:288-292` (регистрация рядом с `SubscriptionSnapshotJob`)
- Test: `tests/AFK4.Platform.Api.Tests/Platform/Analytics/BranchSnapshotRunnerTests.cs`

**Interfaces:**
- Consumes: `BranchDailySnapshotBuilder.Build`, `BranchDailySnapshotEntity`.
- Produces: `IBranchSnapshotRunner.RunAsync(DateTimeOffset now, CancellationToken)`, имя задания `PlatformJobNames.BranchSnapshots = "branch_snapshots"`.

- [ ] **Step 1: Написать падающие тесты**

`tests/AFK4.Platform.Api.Tests/Platform/Analytics/BranchSnapshotRunnerTests.cs`. Способ засеять организацию, филиал, сеанс, платёж и смену взять из соседних тестов той же папки (`SubscriptionSnapshotRunnerTests`) и из тестов дашборда — не изобретать свой сидер.

```csharp
[Fact]
public async Task Run_WritesYesterdayForEveryBranch()
{
    // два филиала одной организации → две строки за вчера, ни одной за сегодня
}

[Fact]
public async Task Run_IsIdempotent_SecondRunWritesNothing()
{
    // прогнать дважды с тем же now → 0 записанных во второй раз, строк в таблице столько же
}

[Fact]
public async Task Run_BackfillsOnlyMissingDaysOfEachBranch()
{
    // филиал A со снимком за позавчера → 1 новая строка; филиал B без снимков → тоже 1 (только вчера)
}

[Fact]
public async Task Run_CountsOnlyItsOwnBranchRows()
{
    // сеанс и платёж чужого филиала не должны попасть в снимок этого
}
```

Тела тестов дописать по образцу соседей; главное — перечисленные утверждения. `now` подаётся аргументом `RunAsync`, поэтому `FakeTimeProvider` для самих тестов раннера не обязателен.

- [ ] **Step 2: Прогнать — убедиться, что падают**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~BranchSnapshotRunnerTests`
Expected: FAIL, компиляция не проходит.

- [ ] **Step 3: Написать интерфейс и раннер**

`IBranchSnapshotRunner.cs`:

```csharp
namespace AFK4.Platform.Api.Platform.Analytics;

public interface IBranchSnapshotRunner
{
    /// <summary>Дописывает недостающие суточные снимки филиалов вплоть до вчерашнего дня. Возвращает число записанных строк.</summary>
    Task<int> RunAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
```

`EfBranchSnapshotRunner.cs`:

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Analytics;

/// <summary>
/// Собирает суточные снимки филиалов. Запросов фиксированное число — по одному на источник за всё
/// окно досъёмки, а не по одному на клуб: правило пульса действует и здесь.
/// </summary>
public sealed class EfBranchSnapshotRunner(PlatformDbContext dbContext, TimeProvider timeProvider)
    : IBranchSnapshotRunner
{
    public async Task<int> RunAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var branches = await dbContext.Branches
            .AsNoTracking()
            .Select(branch => new BranchSnapshotBranch(
                branch.BranchId, branch.OrganizationId, branch.PreferredTimeZone, branch.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        if (branches.Count == 0) return 0;

        var lastSnapshotDates = await dbContext.BranchDailySnapshots
            .AsNoTracking()
            .GroupBy(snapshot => snapshot.BranchId)
            .Select(group => new { BranchId = group.Key, LastDate = group.Max(snapshot => snapshot.SnapshotDate) })
            .ToDictionaryAsync(row => row.BranchId, row => row.LastDate, cancellationToken);

        // Окно с запасом в сутки по обе стороны: границы местных суток сдвинуты относительно UTC,
        // и строка, попадающая в первый снимаемый день по местному времени, в UTC может лежать
        // за его пределами.
        var windowStart = now.AddDays(-(BranchDailySnapshotBuilder.MaxBackfillDays + 2));
        var windowEnd = now.AddDays(1);

        var sessionStarts = await dbContext.Sessions
            .AsNoTracking()
            .Where(session =>
                session.StartedAtUtc != null &&
                session.StartedAtUtc >= windowStart &&
                session.StartedAtUtc <= windowEnd)
            .Select(session => new BranchSnapshotEvent(session.BranchId, session.StartedAtUtc!.Value))
            .ToListAsync(cancellationToken);

        var payments = await dbContext.Payments
            .AsNoTracking()
            .Where(payment => payment.CreatedAtUtc >= windowStart && payment.CreatedAtUtc <= windowEnd)
            .Select(payment => new BranchSnapshotMoney(
                payment.BranchId, payment.CreatedAtUtc, payment.PaymentKind, payment.AmountMinorUnits, payment.CurrencyCode))
            .ToListAsync(cancellationToken);

        var ledgerEntries = await dbContext.LedgerEntries
            .AsNoTracking()
            .Where(entry =>
                entry.CreatedAtUtc >= windowStart &&
                entry.CreatedAtUtc <= windowEnd &&
                (entry.EntryType == LedgerEntryTypeNames.GameplayCharge ||
                    entry.EntryType == LedgerEntryTypeNames.PostpaidDebt ||
                    entry.EntryType == LedgerEntryTypeNames.Refund))
            .Select(entry => new BranchSnapshotMoney(
                entry.BranchId, entry.CreatedAtUtc, entry.EntryType, entry.AmountMinorUnits, entry.CurrencyCode))
            .ToListAsync(cancellationToken);

        var shiftOpens = await dbContext.Shifts
            .AsNoTracking()
            .Where(shift => shift.OpenedAtUtc >= windowStart && shift.OpenedAtUtc <= windowEnd)
            .Select(shift => new BranchSnapshotEvent(shift.BranchId, shift.OpenedAtUtc))
            .ToListAsync(cancellationToken);

        var heartbeats = await dbContext.Devices
            .AsNoTracking()
            .Where(device => device.LastHeartbeatAtUtc != null)
            .GroupBy(device => device.BranchId)
            .Select(group => new { BranchId = group.Key, Last = group.Max(device => device.LastHeartbeatAtUtc!.Value) })
            .ToDictionaryAsync(row => row.BranchId, row => row.Last, cancellationToken);

        var facts = BranchDailySnapshotBuilder.Build(new BranchSnapshotInput(
            now, branches, lastSnapshotDates, sessionStarts, payments, ledgerEntries, shiftOpens, heartbeats));
        if (facts.Count == 0) return 0;

        var createdAt = timeProvider.GetUtcNow();
        foreach (var fact in facts)
        {
            dbContext.BranchDailySnapshots.Add(new BranchDailySnapshotEntity
            {
                BranchDailySnapshotId = Guid.NewGuid(),
                OrganizationId = fact.OrganizationId,
                BranchId = fact.BranchId,
                SnapshotDate = fact.Date,
                SessionCount = fact.SessionCount,
                RevenueMinorUnits = fact.RevenueMinorUnits,
                CurrencyCode = fact.CurrencyCode,
                ShiftOpenedCount = fact.ShiftOpenedCount,
                AgentAlive = fact.AgentAlive,
                CreatedAtUtc = createdAt
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return facts.Count;
    }
}
```

Точные имена полей (`PaymentKind`, `CurrencyCode` у `LedgerEntryEntity`, `OpenedAtUtc` у `ShiftEntity`, `StartedAtUtc` у `SessionEntity`) сверить с сущностями в `src/AFK4.Platform.Api/Data/`; если `Sessions.StartedAtUtc` окажется не-nullable, убрать `!.Value` и лишнее условие. Если EF откажется проецировать прямо в record — материализовать анонимный тип и собрать record в памяти.

- [ ] **Step 4: Написать задание**

`BranchSnapshotJob.cs` — точная калька `SubscriptionSnapshotJob`, тот же интервал из `PlatformAnalyticsOptions.SnapshotInterval`:

```csharp
using AFK4.Platform.Api.Platform.Health;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Platform.Analytics;

/// <summary>
/// Отдельное задание, а не довесок к снимкам подписок: одно задание — одна запись прогона и одна
/// причина отказа. Иначе падение клубной свёртки экран здоровья объявил бы провалом снимков подписок.
/// </summary>
public sealed class BranchSnapshotJob(
    IServiceProvider serviceProvider,
    TimeProvider timeProvider,
    IOptions<PlatformAnalyticsOptions> options,
    ILogger<BranchSnapshotJob> logger)
    : PlatformPeriodicJob(serviceProvider, timeProvider, logger)
{
    private readonly PlatformAnalyticsOptions options = options.Value;

    protected override string JobName => PlatformJobNames.BranchSnapshots;

    protected override TimeSpan Interval => options.SnapshotInterval;

    protected override Task<int> TickAsync(IServiceProvider scopedServices, CancellationToken cancellationToken) =>
        scopedServices.GetRequiredService<IBranchSnapshotRunner>().RunAsync(GetUtcNow(), cancellationToken);
}
```

- [ ] **Step 5: Подключить к наблюдению и DI**

В `PlatformJobNames.cs` добавить константу рядом с `SubscriptionSnapshots` и запись в `Watched`:

```csharp
public const string BranchSnapshots = "branch_snapshots";
```

В `PlatformJobIntervalCatalog.Build()` добавить строку:

```csharp
[PlatformJobNames.BranchSnapshots] = analyticsOptions.Value.SnapshotInterval,
```

В `Program.cs` рядом с регистрацией снимков подписок:

```csharp
builder.Services.AddScoped<IBranchSnapshotRunner, EfBranchSnapshotRunner>();
builder.Services.AddHostedService<BranchSnapshotJob>();
```

- [ ] **Step 6: Прогнать тесты**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter "FullyQualifiedName~BranchSnapshot|FullyQualifiedName~PeriodicJobRegistration|FullyQualifiedName~Health"`
Expected: PASS. Архитектурный тест `PeriodicJobRegistrationTests` обязан остаться зелёным без правок белого списка — новое задание наследует `PlatformPeriodicJob`. Если какой-то тест здоровья считает количество наблюдаемых заданий числом, поправить ожидание — это ожидаемое следствие седьмого наблюдаемого задания.

- [ ] **Step 7: Коммит**

```bash
git add src/AFK4.Platform.Api tests/AFK4.Platform.Api.Tests
git commit -m "feat(platform): суточное задание снимков филиалов"
```

---

### Task 5: Эндпоинт «Динамика»

**Files:**
- Create: `src/AFK4.Shared.Contracts/Platform/Analytics/BranchDynamicsContracts.cs`
- Create: `src/AFK4.Platform.Api/Platform/Analytics/IBranchDynamicsService.cs`
- Create: `src/AFK4.Platform.Api/Platform/Analytics/EfBranchDynamicsService.cs`
- Create: `src/AFK4.Platform.Api/Endpoints/PlatformBranchDynamicsEndpoints.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs` (регистрация сервиса + `Map...` рядом с прочими платформенными эндпоинтами)
- Test: `tests/AFK4.Platform.Api.Tests/Platform/Analytics/BranchDynamicsEndpointTests.cs`

**Interfaces:**
- Consumes: `BranchDailySnapshotEntity`, право `PlatformAdminPermissionNames.ViewOrganizations`.
- Produces: `GET /api/platform/organizations/{organizationId:guid}/branches/{branchId:guid}/dynamics?days=30` → `BranchDynamicsDto`.

- [ ] **Step 1: Написать контракты**

`src/AFK4.Shared.Contracts/Platform/Analytics/BranchDynamicsContracts.cs`:

```csharp
using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Platform.Analytics;

/// <summary>Одни свёрнутые сутки клуба. <c>AgentAlive == null</c> — «неизвестно», не «мёртв».</summary>
public sealed record BranchDynamicsDayDto(
    DateOnly Date,
    int SessionCount,
    MoneyDto Revenue,
    int ShiftOpenedCount,
    bool? AgentAlive);

public sealed record BranchDynamicsDto(
    Guid OrganizationId,
    Guid BranchId,
    DateOnly FromDate,
    DateOnly ToDate,
    MoneyDto TotalRevenue,
    int TotalSessionCount,
    int DaysWithoutAgent,
    int DaysWithUnknownAgent,
    /// <summary>Сутки окна, за которые снимка нет вовсе. Нулями они НЕ дорисовываются.</summary>
    int MissingDayCount,
    IReadOnlyList<BranchDynamicsDayDto> Days);
```

`MoneyDto` — тот же, что использует `EfOperatorDashboardService` (проверить пространство имён по его `using`-ам).

- [ ] **Step 2: Написать падающие тесты эндпоинта**

`tests/AFK4.Platform.Api.Tests/Platform/Analytics/BranchDynamicsEndpointTests.cs`. Авторизация — через `PlatformAdminTestHelper.AuthorizeAsAsync`, как в соседних тестах платформенных эндпоинтов.

```csharp
[Fact]
public async Task Get_ReturnsSnapshotDays_NewestLast()
{
    // засеять три снимка → 200, Days отсортированы по дате возрастанию, суммы сходятся
}

[Fact]
public async Task Get_DoesNotInventZeroDaysForMissingSnapshots()
{
    // окно 30 дней, снимков 2 → Days.Count == 2, MissingDayCount == 28
}

[Fact]
public async Task Get_CountsUnknownAgentSeparatelyFromDead()
{
    // снимки с AgentAlive = true/false/null → DaysWithoutAgent == 1, DaysWithUnknownAgent == 1
}

[Fact]
public async Task Get_ReturnsNotFound_WhenBranchBelongsToAnotherOrganization()
{
    // филиал чужой организации → 404, а не чужие данные
}

[Fact]
public async Task Get_ReturnsForbidden_WithoutViewOrganizationsPermission()
{
    // сотрудник без права → 403 (и данных в теле нет)
}

[Fact]
public async Task Get_ClampsDays_ToTheSupportedRange()
{
    // days=1000 → окно не шире 90 дней; days=0 → не уже 7
}
```

Тела дописать по образцу соседних тестов платформенных эндпоинтов; перечисленные утверждения обязательны, ни один тест из списка выбрасывать нельзя.

- [ ] **Step 3: Прогнать — убедиться, что падают**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~BranchDynamicsEndpointTests`
Expected: FAIL

- [ ] **Step 4: Написать сервис**

`IBranchDynamicsService.cs`:

```csharp
using AFK4.Shared.Contracts.Platform.Analytics;

namespace AFK4.Platform.Api.Platform.Analytics;

public interface IBranchDynamicsService
{
    /// <summary>Возвращает <c>null</c>, если такого филиала у этой организации нет.</summary>
    Task<BranchDynamicsDto?> GetAsync(Guid organizationId, Guid branchId, int days, CancellationToken cancellationToken);
}
```

`EfBranchDynamicsService.cs`:

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Platform.Analytics;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Analytics;

public sealed class EfBranchDynamicsService(PlatformDbContext dbContext, TimeProvider timeProvider)
    : IBranchDynamicsService
{
    private const int MinDays = 7;
    private const int MaxDays = 90;
    private const int DefaultDays = 30;

    public async Task<BranchDynamicsDto?> GetAsync(
        Guid organizationId,
        Guid branchId,
        int days,
        CancellationToken cancellationToken)
    {
        var branchExists = await dbContext.Branches
            .AsNoTracking()
            .AnyAsync(branch => branch.BranchId == branchId && branch.OrganizationId == organizationId, cancellationToken);
        if (!branchExists) return null;

        var window = days <= 0 ? DefaultDays : Math.Clamp(days, MinDays, MaxDays);
        var toDate = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime).AddDays(-1);
        var fromDate = toDate.AddDays(-(window - 1));

        var snapshots = await dbContext.BranchDailySnapshots
            .AsNoTracking()
            .Where(snapshot =>
                snapshot.BranchId == branchId &&
                snapshot.SnapshotDate >= fromDate &&
                snapshot.SnapshotDate <= toDate)
            .OrderBy(snapshot => snapshot.SnapshotDate)
            .ToListAsync(cancellationToken);

        var currencyCode = snapshots.FirstOrDefault()?.CurrencyCode ?? BranchRevenue.DefaultCurrencyCode;

        return new BranchDynamicsDto(
            organizationId,
            branchId,
            fromDate,
            toDate,
            new MoneyDto(currencyCode, snapshots.Sum(snapshot => snapshot.RevenueMinorUnits)),
            snapshots.Sum(snapshot => snapshot.SessionCount),
            snapshots.Count(snapshot => snapshot.AgentAlive == false),
            snapshots.Count(snapshot => snapshot.AgentAlive is null),
            window - snapshots.Count,
            snapshots.Select(snapshot => new BranchDynamicsDayDto(
                snapshot.SnapshotDate,
                snapshot.SessionCount,
                new MoneyDto(snapshot.CurrencyCode, snapshot.RevenueMinorUnits),
                snapshot.ShiftOpenedCount,
                snapshot.AgentAlive)).ToList());
    }
}
```

Порядок аргументов `MoneyDto` сверить с его определением (в дашборде это `Money(currencyCode, minorUnits)`).

- [ ] **Step 5: Написать эндпоинт**

`src/AFK4.Platform.Api/Endpoints/PlatformBranchDynamicsEndpoints.cs` — тот же образец проверки права, что в `PlatformPulseEndpoints.cs`: право проверяется ДО обращения к данным.

```csharp
using AFK4.Platform.Api.Platform.Analytics;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Shared.Contracts.Platform.Auth;

namespace AFK4.Platform.Api.Endpoints;

public static class PlatformBranchDynamicsEndpoints
{
    public static void MapPlatformBranchDynamicsEndpoints(this WebApplication app)
    {
        app.MapGet("/api/platform/organizations/{organizationId:guid}/branches/{branchId:guid}/dynamics", async (
            Guid organizationId,
            Guid branchId,
            int? days,
            PlatformAdminAuthorizationService authorizationService,
            IBranchDynamicsService dynamicsService,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ViewOrganizations);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var dynamics = await dynamicsService.GetAsync(organizationId, branchId, days ?? 0, cancellationToken);
            return dynamics is null ? Results.NotFound() : Results.Ok(dynamics);
        });
    }
}
```

Зарегистрировать в `Program.cs` рядом с остальными `Map...Endpoints()` и добавить `builder.Services.AddScoped<IBranchDynamicsService, EfBranchDynamicsService>();`.

- [ ] **Step 6: Прогнать тесты**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~BranchDynamics`
Expected: PASS

- [ ] **Step 7: Коммит**

```bash
git add src/AFK4.Shared.Contracts src/AFK4.Platform.Api tests/AFK4.Platform.Api.Tests
git commit -m "feat(platform): эндпоинт динамики клуба"
```

---

### Task 6: Вкладка «Динамика» в паспорте организации

**Files:**
- Create: `src/AFK4.PlatformControl.Web/src/api/platformClients/branchDynamics.ts`
- Modify: `src/AFK4.PlatformControl.Web/src/api/types.ts` (типы ответа), плюс место, где собираются под-клиенты (найти по образцу `analytics.ts`)
- Create: `src/AFK4.PlatformControl.Web/src/platform/organizations/dynamicsModel.ts`
- Create: `src/AFK4.PlatformControl.Web/src/platform/organizations/dynamicsModel.test.ts`
- Create: `src/AFK4.PlatformControl.Web/src/platform/organizations/useBranchDynamics.ts`
- Create: `src/AFK4.PlatformControl.Web/src/platform/organizations/OrganizationDynamicsTab.tsx`
- Create: `src/AFK4.PlatformControl.Web/src/platform/organizations/OrganizationDynamicsTab.test.tsx`
- Modify: `src/AFK4.PlatformControl.Web/src/routing/platformRoute.ts:3-9` (тип `OrganizationTab` + `ORGANIZATION_TABS`)
- Modify: `src/AFK4.PlatformControl.Web/src/platform/organizations/OrganizationPage.tsx:22-29,92-115` (пункт вкладки + панель)
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json`

**Interfaces:**
- Consumes: `GET /api/platform/organizations/{organizationId}/branches/{branchId}/dynamics?days=30` → `BranchDynamicsDto` (Task 5).

- [ ] **Step 1: Добавить строки в каталоги**

Ключи (значения ru — ниже; en и tg перевести по-настоящему, `tg === ru` завалит guard-тест):

| Ключ | ru |
|---|---|
| `platform.organization.tab.dynamics` | Динамика |
| `platform.dynamics.branch.label` | Клуб |
| `platform.dynamics.loading` | Загружаем историю клуба… |
| `platform.dynamics.error` | Не удалось загрузить историю клуба |
| `platform.dynamics.retry` | Повторить |
| `platform.dynamics.empty` | За последние 30 дней снимков по этому клубу нет |
| `platform.dynamics.summary.revenue` | Выручка за период |
| `platform.dynamics.summary.sessions` | Сеансов |
| `platform.dynamics.summary.daysWithoutAgent` | `{count, plural, one {# день без связи} few {# дня без связи} many {# дней без связи} other {# дня без связи}}` |
| `platform.dynamics.summary.daysUnknown` | `{count, plural, one {# день без наблюдения} few {# дня без наблюдения} many {# дней без наблюдения} other {# дня без наблюдения}}` |
| `platform.dynamics.summary.missingDays` | `{count, plural, one {# день без снимка} few {# дня без снимка} many {# дней без снимка} other {# дня без снимка}}` |
| `platform.dynamics.chart.revenue` | Выручка по дням |
| `platform.dynamics.chart.sessions` | Сеансы по дням |
| `platform.dynamics.agent.alive` | Клуб выходил на связь |
| `platform.dynamics.agent.dead` | Клуб не выходил на связь |
| `platform.dynamics.agent.unknown` | Нет данных о связи |
| `platform.dynamics.footnote` | Связь клуба проверяется раз в сутки: «клуб не выходил на связь весь день» — факт, а короткие обрывы внутри дня вкладка не покажет. |

Подписи плиток, которые рисуются рядом с числом (`summary.revenue`, `summary.sessions`), — обычные статичные строки. **Плюрал-ключ нельзя использовать как статичную подпись:** вызванный без `count`, он вернёт сырой ICU-шаблон прямо на экран.

После правки каталогов: `cd packages/i18n && bun run gen`, затем `bun test` в этом пакете.

- [ ] **Step 2: Написать падающий тест модели**

`dynamicsModel.test.ts`:

```ts
import { describe, expect, test } from 'bun:test';
import { toDynamicsSeries, summarizeAgentDays } from './dynamicsModel';

const day = (date: string, sessions: number, minorUnits: number, agentAlive: boolean | null) => ({
  date,
  sessionCount: sessions,
  revenue: { currencyCode: 'TJS', minorUnits },
  shiftOpenedCount: 1,
  agentAlive
});

describe('toDynamicsSeries', () => {
  test('переводит минорные единицы в мажорные для графика', () => {
    const series = toDynamicsSeries([day('2026-08-01', 4, 12_345, true)]);
    expect(series[0].revenue).toBe(123.45);
    expect(series[0].sessions).toBe(4);
  });

  test('сохраняет порядок дней', () => {
    const series = toDynamicsSeries([day('2026-08-01', 1, 100, true), day('2026-08-02', 2, 200, true)]);
    expect(series.map(point => point.date)).toEqual(['2026-08-01', '2026-08-02']);
  });
});

describe('summarizeAgentDays', () => {
  test('не смешивает «не выходил на связь» с «нет данных»', () => {
    const summary = summarizeAgentDays([
      day('2026-08-01', 0, 0, false),
      day('2026-08-02', 0, 0, null),
      day('2026-08-03', 0, 0, true)
    ]);
    expect(summary).toEqual({ alive: 1, dead: 1, unknown: 1 });
  });
});
```

- [ ] **Step 3: Прогнать — убедиться, что падает**

Run: `cd src/AFK4.PlatformControl.Web && bun test src/platform/organizations/dynamicsModel.test.ts`
Expected: FAIL — модуля нет.

- [ ] **Step 4: Написать модель**

`dynamicsModel.ts` — чистые функции, без React. Перевод денег — **только** через `minorToMajor` из `@afk4/money` (проверить, как его импортирует `billing/analyticsModel.ts`, и сделать так же):

```ts
import { minorToMajor } from '@afk4/money';
import type { BranchDynamicsDay } from '@/api/types';

export type DynamicsPoint = { date: string; revenue: number; sessions: number };

export function toDynamicsSeries(days: BranchDynamicsDay[]): DynamicsPoint[] {
  return days.map(day => ({
    date: day.date,
    revenue: minorToMajor(day.revenue.minorUnits, day.revenue.currencyCode),
    sessions: day.sessionCount
  }));
}

export function summarizeAgentDays(days: BranchDynamicsDay[]) {
  return {
    alive: days.filter(day => day.agentAlive === true).length,
    dead: days.filter(day => day.agentAlive === false).length,
    unknown: days.filter(day => day.agentAlive === null || day.agentAlive === undefined).length
  };
}
```

Сигнатуру `minorToMajor` сверить с пакетом — если она принимает только сумму, вызывать без валюты.

- [ ] **Step 5: Прогнать тест модели**

Run: `cd src/AFK4.PlatformControl.Web && bun test src/platform/organizations/dynamicsModel.test.ts`
Expected: PASS

- [ ] **Step 6: Написать клиент, хук и вкладку**

- `api/platformClients/branchDynamics.ts` — тонкий клиент по образцу `analytics.ts`, один метод `getBranchDynamics(organizationId, branchId, days)`.
- `useBranchDynamics.ts` — по образцу `useAnalytics.ts`: `loading`/`error`/`ready`, флаг `cancelled` в эффекте, `retry()` через тик-счётчик. Ключ эффекта включает `branchId` — при смене клуба данные перезапрашиваются.
- `OrganizationDynamicsTab.tsx` — принимает `branches: OrganizationBranch[]`:
  - если филиалов нет — строка `platform.dynamics.empty`;
  - если больше одного — селектор клуба (`platform.dynamics.branch.label`), по умолчанию первый;
  - три плитки сводки: выручка (через `formatCurrency` из `@afk4/money`), сеансы, дни без связи; отдельной строкой — дни без наблюдения и дни без снимка, если они не нули;
  - два графика `recharts` в `ResponsiveContainer` (выручка, сеансы) — как в `billing/AnalyticsTab.tsx`;
  - под графиками — сноска `platform.dynamics.footnote`;
  - явные состояния loading / error+retry / empty.

- [ ] **Step 7: Написать тест вкладки**

`OrganizationDynamicsTab.test.tsx` — обязательные утверждения:

```
- рисует сводку и сноску о суточной точности при успешном ответе
- показывает «нет данных о связи» отдельно от «не выходил на связь»
- на ошибке показывает сообщение и кнопку повтора, а не «нет данных»
- ни одна подпись на экране не содержит символа '{' (регрессия на ICU-шаблон, попавший в UI сырым)
```

Последний тест — буквально проверка `expect(container.textContent).not.toContain('{')` после рендера с непустыми данными.

Моки транспорта — по образцу существующих тестов вкладок; `recharts` в happy-dom рисует без размеров, поэтому проверять текст и подписи, а не сами графики (посмотреть, как это обходит `AnalyticsTab.test.tsx`, если он есть).

- [ ] **Step 8: Подключить вкладку**

- В `routing/platformRoute.ts` добавить `'dynamics'` в тип `OrganizationTab` и в `ORGANIZATION_TABS`.
- В `OrganizationPage.tsx` добавить пункт в `TABS`:

```tsx
{ value: 'dynamics', labelKey: 'platform.organization.tab.dynamics', allowed: () => true },
```

(право `platform.organizations.view` уже требуется для самой страницы — как у вкладки «Клубы»), и панель в блок `role="tabpanel"` с той же обёрткой `TabBoundary` и `resetKey`, что у соседей.

- [ ] **Step 9: Прогнать тесты и сборку панели**

Run: `cd src/AFK4.PlatformControl.Web && bun test && bun run build`
Expected: PASS + успешная сборка. `bun run build` = `tsc -b && vite build` и типизирует в том числе тестовые файлы — зелёный `bun test` сборку не гарантирует.

Run: `cd packages/i18n && bun test`
Expected: PASS (паритет каталогов и guard на `tg === ru`).

- [ ] **Step 10: Коммит**

```bash
git add src/AFK4.PlatformControl.Web locales packages/i18n
git commit -m "feat(platform-control): вкладка динамики клуба"
```

---

## Финальная проверка ветки

- `dotnet test tests/AFK4.Platform.Api.Tests` — с настоящим Postgres (четыре переменные окружения из памяти проекта + `AFK4_REQUIRE_POSTGRES_TESTS=1`), 0 failed, 0 skipped.
- `cd src/AFK4.PlatformControl.Web && bun test && bun run build`
- `cd packages/i18n && bun test`
