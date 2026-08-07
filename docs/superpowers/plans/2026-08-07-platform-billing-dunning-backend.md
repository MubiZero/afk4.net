# Волна B, план 1 (бэкенд) — цикл неплатежа и гибкая цена

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Замкнуть цикл денег между платформой и клубом на бэкенде: лестница напоминаний, автоматический `past_due` в обе стороны, работающая отсрочка, скидки, разовые счета и кредит-ноты, валюта TJS.

**Architecture:** Лестница напоминаний и переходы статуса выносятся в отдельный `EfDunningRunner`, который часовой `InvoiceGenerationHostedService` вызывает после выставления счетов; флип `issued → overdue` переезжает туда же, потому что это часть той же ответственности. Долг считается чистой функцией `BillingBalance.Compute` по знаковой сумме неоплаченных счетов — это единственное место, где кредит-нота вычитается из долга. Скидка живёт на подписке рядом с ценой плана, а не вместо неё, и раскладывается в счёте на три поля (до скидки, скидка, итог).

**Tech Stack:** ASP.NET Core minimal APIs + EF Core 10/Npgsql (`AFK4.Platform.Api`), xUnit + InMemory-провайдер и `PlatformApiFactory` (`tests/AFK4.Platform.Api.Tests`).

## Global Constraints

- Спека: `docs/superpowers/specs/2026-08-07-platform-billing-dunning-and-pricing-design.md`. Этот план покрывает слайсы 1–3 из пяти; панель платформы и полоса в админке клуба (слайсы 4–5) — отдельный план.
- **Новые NuGet-пакеты запрещены.** В `AFK4.Platform.Api.csproj` бережно пять зависимостей.
- Ветка: `feat/platform-billing-dunning-pricing`. Сообщения коммитов — на русском с conventional-префиксом (`feat(platform): ...`, `fix(platform): ...`), как в истории репозитория.
- Все суммы — в минорных единицах (`long`), нигде не появляется `decimal` для хранения. Валюта — `TJS`.
- Отрицательная сумма допустима **только** у счёта вида `credit`; для всех остальных видов сумма строго положительная, проверка на границе сервиса.
- Отсрочка (`OrganizationSubscriptionEntity.PaymentGraceUntilUtc` в будущем) подавляет **всё**: письма лестницы, письмо «скоро срок» и переход `active → past_due`. Существующий `past_due` при этом не откатывается.
- Приостановка организации (`OrganizationStatusNames.Suspended`) остаётся **только ручной**. Ни одна задача этого плана не меняет статус организации.
- Шаблоны писем — три локали: `ru`, `en`, `tg`. Таджикский — настоящий таджикский, guard-тест падает на `tg === ru` вне whitelist заимствований.
- Каждая новая мутация пишет аудит через `IAuditRecordWriter` с исходом `Succeeded`/`Denied` по образцу соседних платформенных эндпоинтов.
- Миграции: `dotnet ef migrations add <PascalName> --project src/AFK4.Platform.Api --startup-project src/AFK4.Platform.Api`; коммитить `.cs` + `.Designer.cs` + обновлённый `PlatformDbContextModelSnapshot.cs`.
- Прогон тестов: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~<Имя>`. Полный прогон в конце: `dotnet test tests/AFK4.Platform.Api.Tests -v minimal`.
- Имена по назначению: в аудите и API это «счёт», «скидка», «отсрочка», а не `dunning_stage_bump`.

---

## File Structure

**Новые файлы (`src/AFK4.Platform.Api`)**

| Файл | Ответственность |
|---|---|
| `Platform/Billing/MoneyFormatting.cs` | минорные единицы + код валюты → строка для письма |
| `Platform/Billing/BillingBalance.cs` | чистая функция: неоплаченные счета → знаковый долг и признак просрочки |
| `Platform/Billing/IDunningRunner.cs` | контракт прохода по неплатежам |
| `Platform/Billing/EfDunningRunner.cs` | флип в просрочку, лестница писем, переходы статуса подписки |
| `Notifications/Templates/{ru,en,tg}/invoice.due_soon.json` | письмо «скоро срок» |

**Изменяемые файлы**

| Файл | Что меняется |
|---|---|
| `Data/InvoiceEntity.cs` | `DunningStage`, `LastDunningAtUtc`, `DueSoonNotifiedAtUtc`, `GrossAmountMinorUnits`, `DiscountMinorUnits` |
| `Data/OrganizationSubscriptionEntity.cs` | поля скидки |
| `Data/SubscriptionPlanEntity.cs` | дефолт валюты |
| `Platform/Billing/BillingOptions.cs` | смещения лестницы, валюта по умолчанию |
| `Platform/Billing/EfInvoiceGenerationRunner.cs` | вычитается флип в просрочку, добавляется расчёт скидки |
| `Platform/Billing/EfInvoiceNotifier.cs` | стадия в сигнатуре, `daysOverdue`, форматирование через helper |
| `Platform/Billing/EfInvoiceService.cs` | создание разовых счетов и кредит-нот, возврат из `past_due` при оплате |
| `Platform/Billing/EfOrganizationSubscriptionService.cs` | валидация и применение скидки |
| `Platform/Billing/EfBillingMetricsService.cs` | MRR считает `active` + `past_due` |
| `Platform/Billing/BillingPlanSeedHostedService.cs` | цены в TJS, три годовых плана |
| `Endpoints/PlatformBillingEndpoints.cs` | `POST .../invoices` (разовый счёт и кредит-нота) |
| `Program.cs` | регистрация `IDunningRunner` |
| `InvoiceGenerationHostedService.cs` | вызов dunning-прохода после выставления |

Лестница и переходы вынесены из `EfInvoiceGenerationRunner` намеренно: у выставления счетов и у работы с неплатежом разные причины меняться, а файл уже несёт две ответственности.

---

## Слайс 1 — валюта и форматирование

### Task 1: Форматирование денег в письмах

**Files:**
- Create: `src/AFK4.Platform.Api/Platform/Billing/MoneyFormatting.cs`
- Modify: `src/AFK4.Platform.Api/Platform/Billing/EfInvoiceNotifier.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Billing/MoneyFormattingTests.cs`

**Interfaces:**
- Produces: `static string MoneyFormatting.ToMajorString(long minorUnits, string currencyCode)` — инвариантная строка с двумя знаками для валют с сотыми долями; используется задачами 4 и 7.

Сейчас `EfInvoiceNotifier` форматирует сумму хардкодом `(invoice.AmountMinorUnits / 100m).ToString("0.00")`. Кредит-нота из задачи 7 приносит отрицательные суммы, и хардкод должен исчезнуть до того, как они появятся.

- [ ] **Step 1: Написать падающий тест**

```csharp
using AFK4.Platform.Api.Platform.Billing;

namespace AFK4.Platform.Api.Tests.Billing;

public sealed class MoneyFormattingTests
{
    [Theory]
    [InlineData(290000, "TJS", "2900.00")]
    [InlineData(0, "TJS", "0.00")]
    [InlineData(5, "TJS", "0.05")]
    [InlineData(-150000, "TJS", "-1500.00")]
    public void ToMajorString_TwoDecimalCurrency_FormatsInvariantly(long minorUnits, string currency, string expected) =>
        Assert.Equal(expected, MoneyFormatting.ToMajorString(minorUnits, currency));

    [Fact]
    public void ToMajorString_UnknownCurrency_FallsBackToTwoDecimals() =>
        Assert.Equal("12.34", MoneyFormatting.ToMajorString(1234, "XYZ"));
}
```

- [ ] **Step 2: Убедиться, что тест падает**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~MoneyFormattingTests`
Expected: FAIL — тип `MoneyFormatting` не существует.

- [ ] **Step 3: Реализовать helper**

```csharp
using System.Globalization;

namespace AFK4.Platform.Api.Platform.Billing;

/// <summary>
/// Formats minor-unit amounts for notification bodies. The frontend uses @afk4/money; the backend
/// renders email templates and has no access to that package, so the exponent table lives here.
/// </summary>
public static class MoneyFormatting
{
    private const int DefaultExponent = 2;

    public static string ToMajorString(long minorUnits, string currencyCode)
    {
        var exponent = Exponent(currencyCode);
        var scale = (decimal)Math.Pow(10, exponent);
        return (minorUnits / scale).ToString("F" + exponent.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    }

    // TJS (somoni) is subdivided into 100 diram, like every currency this product bills in today.
    // The table exists so a zero-decimal currency does not silently render as "1500.00".
    private static int Exponent(string currencyCode) => currencyCode switch
    {
        "TJS" or "RUB" or "USD" or "EUR" => 2,
        _ => DefaultExponent
    };
}
```

- [ ] **Step 4: Убедиться, что тест проходит**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~MoneyFormattingTests`
Expected: PASS

- [ ] **Step 5: Перевести нотификатор на helper**

В `EfInvoiceNotifier.BuildTokens` заменить строку

```csharp
["amount"] = (invoice.AmountMinorUnits / 100m).ToString("0.00", CultureInfo.InvariantCulture),
```

на

```csharp
["amount"] = MoneyFormatting.ToMajorString(invoice.AmountMinorUnits, invoice.CurrencyCode),
```

- [ ] **Step 6: Прогнать тесты нотификатора**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~EfInvoiceNotifierTests`
Expected: PASS (поведение не изменилось, сумма форматируется так же)

- [ ] **Step 7: Коммит**

```bash
git add src/AFK4.Platform.Api/Platform/Billing/MoneyFormatting.cs \
        src/AFK4.Platform.Api/Platform/Billing/EfInvoiceNotifier.cs \
        tests/AFK4.Platform.Api.Tests/Billing/MoneyFormattingTests.cs
git commit -m "refactor(platform): форматирование сумм в письмах через общий helper"
```

---

### Task 2: Валюта TJS, цены и годовые тарифы

**Files:**
- Modify: `src/AFK4.Platform.Api/Platform/Billing/BillingOptions.cs`
- Modify: `src/AFK4.Platform.Api/Platform/Billing/BillingPlanSeedHostedService.cs`
- Modify: `src/AFK4.Platform.Api/Platform/Billing/EfBillingMetricsService.cs` (константа `DefaultCurrency`)
- Modify: `src/AFK4.Platform.Api/Data/SubscriptionPlanEntity.cs`, `Data/OrganizationSubscriptionEntity.cs`, `Data/InvoiceEntity.cs` (дефолты полей)
- Create: миграция `RebaseBillingCurrencyToTjs`
- Modify: `docs/operations/coolify-staging-deploy.md`
- Test: `tests/AFK4.Platform.Api.Tests/Billing/BillingPlanSeedHostedServiceTests.cs`

**Interfaces:**
- Consumes: ничего из предыдущих задач.
- Produces: коды планов `starter_yearly`, `growth_yearly`, `scale_yearly` — на них опирается задача 6 (смена плана сохраняет скидку) при выборе плана в тестах.

Годовые тарифы делаются здесь, а не в слайсе 3: это те же три строки сида в том же файле с тем же тестом, и разделять их значило бы дважды править одну таблицу.

- [ ] **Step 1: Написать падающий тест**

Дописать в `BillingPlanSeedHostedServiceTests.cs`:

```csharp
    [Fact]
    public async Task StartAsync_SeedsMonthlyAndYearlyPlansInSomoni()
    {
        await using var db = NewContext();
        var service = NewService(db);

        await service.StartAsync(CancellationToken.None);

        var plans = await db.SubscriptionPlans.ToListAsync();
        Assert.All(plans, plan => Assert.Equal("TJS", plan.CurrencyCode));

        var starter = plans.Single(plan => plan.PlanCode == "starter");
        Assert.Equal(290000, starter.PriceMinorUnits);
        Assert.Equal(BillingIntervalNames.Monthly, starter.BillingInterval);

        var starterYearly = plans.Single(plan => plan.PlanCode == "starter_yearly");
        Assert.Equal(2900000, starterYearly.PriceMinorUnits); // ten months, two free
        Assert.Equal(BillingIntervalNames.Yearly, starterYearly.BillingInterval);
        Assert.Equal(starter.MaxBranches, starterYearly.MaxBranches);
    }
```

Если в файле нет хелперов `NewContext`/`NewService`, повторить их из соседних тестов этого файла — не изобретать новые имена.

- [ ] **Step 2: Убедиться, что тест падает**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~BillingPlanSeedHostedServiceTests`
Expected: FAIL — валюта `RUB`, плана `starter_yearly` нет.

- [ ] **Step 3: Перевести дефолты на TJS**

- `BillingOptions.DefaultCurrencyCode` → `"TJS"`.
- `EfBillingMetricsService.DefaultCurrency` → `"TJS"`.
- В `SubscriptionPlanEntity`, `OrganizationSubscriptionEntity`, `InvoiceEntity` заменить `= "RUB"` на `= "TJS"` в инициализаторе `CurrencyCode`.

- [ ] **Step 4: Добавить годовые тарифы в сид**

В `BillingPlanSeedHostedService.DefaultPlans` у трёх существующих записей заменить `CurrencyCode = "RUB"` на `"TJS"` и добавить три записи:

```csharp
        new()
        {
            PlanCode = "starter_yearly",
            Name = "Starter, год",
            PriceMinorUnits = 2900000,
            CurrencyCode = "TJS",
            BillingInterval = BillingIntervalNames.Yearly,
            MaxBranches = 1,
            MaxDevicesPerBranch = 30,
            MaxConcurrentSessions = 40,
            MaxStaffUsersPerBranch = 10,
            IsActive = true,
            SortOrder = 4
        },
        new()
        {
            PlanCode = "growth_yearly",
            Name = "Growth, год",
            PriceMinorUnits = 7900000,
            CurrencyCode = "TJS",
            BillingInterval = BillingIntervalNames.Yearly,
            MaxBranches = 3,
            MaxDevicesPerBranch = 60,
            MaxConcurrentSessions = 80,
            MaxStaffUsersPerBranch = 20,
            IsActive = true,
            SortOrder = 5
        },
        new()
        {
            PlanCode = "scale_yearly",
            Name = "Scale, год",
            PriceMinorUnits = 19900000,
            CurrencyCode = "TJS",
            BillingInterval = BillingIntervalNames.Yearly,
            MaxBranches = 10,
            MaxDevicesPerBranch = 120,
            MaxConcurrentSessions = 200,
            MaxStaffUsersPerBranch = 50,
            IsActive = true,
            SortOrder = 6
        }
```

- [ ] **Step 5: Убедиться, что тест проходит**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~BillingPlanSeedHostedServiceTests`
Expected: PASS

- [ ] **Step 6: Создать миграцию перекодировки валюты**

```bash
dotnet ef migrations add RebaseBillingCurrencyToTjs --project src/AFK4.Platform.Api --startup-project src/AFK4.Platform.Api
```

В `Up` дописать перекодировку существующих строк **без пересчёта сумм**:

```csharp
            migrationBuilder.Sql("""
                UPDATE "SubscriptionPlans" SET "CurrencyCode" = 'TJS' WHERE "CurrencyCode" = 'RUB';
                UPDATE "OrganizationSubscriptions" SET "CurrencyCode" = 'TJS' WHERE "CurrencyCode" = 'RUB';
                UPDATE "Invoices" SET "CurrencyCode" = 'TJS' WHERE "CurrencyCode" = 'RUB';
                """);
```

`Down` оставить пустым телом с комментарием: обратная перекодировка вернула бы неверную валюту суммам, которые с тех пор могли быть выставлены в сомони.

- [ ] **Step 7: Записать предупреждение в рантбук деплоя**

В `docs/operations/coolify-staging-deploy.md`, в раздел про миграции, добавить абзац:

```markdown
Миграция `RebaseBillingCurrencyToTjs` меняет код валюты существующих тарифов,
подписок и счетов с `RUB` на `TJS` **без пересчёта сумм**: 2 900 рублей становятся
2 900 сомони. Для staging это осознанное решение — продакшн ещё не развёрнут.
Никакой конвертации по курсу не происходит, и читать её как конвертацию нельзя.
```

- [ ] **Step 8: Прогнать биллинговые тесты**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~Billing`
Expected: PASS. Тесты, где в фикстурах захардкожен `"RUB"`, поправить на `"TJS"` — они проверяют не валюту, а суммы.

- [ ] **Step 9: Коммит**

```bash
git add -A
git commit -m "feat(platform): перевести биллинг на сомони и добавить годовые тарифы"
```

---

## Слайс 2 — движок неплатежа

### Task 3: Колонки лестницы и расчёт долга

**Files:**
- Modify: `src/AFK4.Platform.Api/Data/InvoiceEntity.cs`
- Create: `src/AFK4.Platform.Api/Platform/Billing/BillingBalance.cs`
- Create: миграция `AddInvoiceDunningTracking`
- Test: `tests/AFK4.Platform.Api.Tests/Billing/BillingBalanceTests.cs`

**Interfaces:**
- Produces:
  - на `InvoiceEntity`: `int DunningStage` (0 = ничего не отправлено), `DateTimeOffset? LastDunningAtUtc`, `DateTimeOffset? DueSoonNotifiedAtUtc`, `long GrossAmountMinorUnits`, `long DiscountMinorUnits`;
  - `static OrganizationBalance BillingBalance.Compute(IReadOnlyCollection<InvoiceEntity> unpaidInvoices)`;
  - `sealed record OrganizationBalance(long OutstandingMinorUnits, bool InArrears, InvoiceEntity? OldestOverdue)`.
  - На это опираются задачи 4, 5 и оба слайса второго плана.

- [ ] **Step 1: Написать падающий тест**

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Billing;

namespace AFK4.Platform.Api.Tests.Billing;

public sealed class BillingBalanceTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-05-01T00:00:00Z");

    private static InvoiceEntity Invoice(long amount, string status, string kind = "subscription", int daysOld = 0) =>
        new()
        {
            InvoiceId = Guid.NewGuid(),
            OrganizationId = Guid.Empty,
            Kind = kind,
            Status = status,
            AmountMinorUnits = amount,
            CurrencyCode = "TJS",
            DueAtUtc = Start.AddDays(-daysOld),
            IssuedAtUtc = Start.AddDays(-daysOld)
        };

    [Fact]
    public void Compute_NoInvoices_IsZeroAndNotInArrears()
    {
        var balance = BillingBalance.Compute([]);

        Assert.Equal(0, balance.OutstandingMinorUnits);
        Assert.False(balance.InArrears);
        Assert.Null(balance.OldestOverdue);
    }

    [Fact]
    public void Compute_OverdueInvoice_IsInArrearsAndReportsOldest()
    {
        var older = Invoice(290000, InvoiceStatusNames.Overdue, daysOld: 10);
        var newer = Invoice(290000, InvoiceStatusNames.Overdue, daysOld: 2);

        var balance = BillingBalance.Compute([newer, older]);

        Assert.Equal(580000, balance.OutstandingMinorUnits);
        Assert.True(balance.InArrears);
        Assert.Same(older, balance.OldestOverdue);
    }

    [Fact]
    public void Compute_CreditNoteCoversOverdue_LeavesNoArrears()
    {
        var overdue = Invoice(290000, InvoiceStatusNames.Overdue, daysOld: 5);
        var credit = Invoice(-290000, InvoiceStatusNames.Issued, kind: "credit");

        var balance = BillingBalance.Compute([overdue, credit]);

        Assert.Equal(0, balance.OutstandingMinorUnits);
        Assert.False(balance.InArrears);
    }

    [Fact]
    public void Compute_IssuedButNotYetOverdue_IsOutstandingWithoutArrears()
    {
        var balance = BillingBalance.Compute([Invoice(290000, InvoiceStatusNames.Issued)]);

        Assert.Equal(290000, balance.OutstandingMinorUnits);
        Assert.False(balance.InArrears);
    }
}
```

- [ ] **Step 2: Убедиться, что тест падает**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~BillingBalanceTests`
Expected: FAIL — типа `BillingBalance` не существует.

- [ ] **Step 3: Реализовать расчёт долга**

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Billing;

namespace AFK4.Platform.Api.Platform.Billing;

/// <summary>Signed outstanding balance for one organization. Credit notes carry a negative amount,
/// so a credited club stops being demanded money it no longer owes.</summary>
public sealed record OrganizationBalance(
    long OutstandingMinorUnits,
    bool InArrears,
    InvoiceEntity? OldestOverdue);

public static class BillingBalance
{
    /// <summary>Caller passes the organization's unpaid invoices (issued and overdue); paid and void
    /// invoices must be filtered out before the call.</summary>
    public static OrganizationBalance Compute(IReadOnlyCollection<InvoiceEntity> unpaidInvoices)
    {
        var outstanding = unpaidInvoices.Sum(invoice => invoice.AmountMinorUnits);
        var oldestOverdue = unpaidInvoices
            .Where(invoice => invoice.Status == InvoiceStatusNames.Overdue)
            .OrderBy(invoice => invoice.DueAtUtc)
            .FirstOrDefault();

        return new OrganizationBalance(
            OutstandingMinorUnits: outstanding,
            InArrears: oldestOverdue is not null && outstanding > 0,
            OldestOverdue: oldestOverdue);
    }
}
```

- [ ] **Step 4: Убедиться, что тест проходит**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~BillingBalanceTests`
Expected: PASS

- [ ] **Step 5: Добавить колонки лестницы**

В `InvoiceEntity`:

```csharp
    /// <summary>0 = nothing sent; 1..4 are the overdue ladder rungs (see the wave B design spec).</summary>
    public int DunningStage { get; set; }

    public DateTimeOffset? LastDunningAtUtc { get; set; }

    public DateTimeOffset? DueSoonNotifiedAtUtc { get; set; }

    /// <summary>Amount before the subscription discount; equals AmountMinorUnits when there is none.
    /// The discount itself arrives in task 6 — the columns land here so every later task can seed
    /// invoices without a second migration.</summary>
    public long GrossAmountMinorUnits { get; set; }

    public long DiscountMinorUnits { get; set; }
```

Все существующие места создания счёта (`EfInvoiceGenerationRunner.GenerateForSubscriptionAsync` и пропорциональный пересчёт в `EfOrganizationSubscriptionService`) заполняют `GrossAmountMinorUnits = AmountMinorUnits` и оставляют `DiscountMinorUnits = 0`.

- [ ] **Step 6: Создать миграцию**

```bash
dotnet ef migrations add AddInvoiceDunningTracking --project src/AFK4.Platform.Api --startup-project src/AFK4.Platform.Api
```

Проверить, что в `Up` пять колонок и что числовые получают `defaultValue: 0`. Дописать заполнение суммы до скидки у существующих счетов:

```csharp
            migrationBuilder.Sql("""UPDATE "Invoices" SET "GrossAmountMinorUnits" = "AmountMinorUnits";""");
```

- [ ] **Step 7: Прогнать биллинговые тесты**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~Billing`
Expected: PASS

- [ ] **Step 8: Коммит**

```bash
git add -A
git commit -m "feat(platform): знаковый расчёт долга и колонки лестницы напоминаний"
```

---

### Task 4: Лестница напоминаний

**Files:**
- Create: `src/AFK4.Platform.Api/Platform/Billing/IDunningRunner.cs`
- Create: `src/AFK4.Platform.Api/Platform/Billing/EfDunningRunner.cs`
- Create: `src/AFK4.Platform.Api/Notifications/Templates/ru/invoice.due_soon.json`, `.../en/...`, `.../tg/...`
- Modify: `src/AFK4.Platform.Api/Notifications/NotificationTemplateKeys.cs`
- Modify: `src/AFK4.Platform.Api/Platform/Billing/IInvoiceNotifier.cs`, `EfInvoiceNotifier.cs`
- Modify: `src/AFK4.Platform.Api/Platform/Billing/BillingOptions.cs`
- Modify: `src/AFK4.Platform.Api/Platform/Billing/EfInvoiceGenerationRunner.cs` (убрать флип в просрочку)
- Modify: `src/AFK4.Platform.Api/Platform/Billing/InvoiceGenerationHostedService.cs`, `Program.cs`
- Modify: `tests/AFK4.Platform.Api.Tests/Billing/RecordingInvoiceNotifier.cs`, `EfInvoiceGenerationRunnerTests.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Billing/EfDunningRunnerTests.cs`

**Interfaces:**
- Consumes: `InvoiceEntity.DunningStage`, `LastDunningAtUtc`, `DueSoonNotifiedAtUtc` (задача 3).
- Produces:
  - `interface IDunningRunner { Task<int> RunAsync(DateTimeOffset now, CancellationToken cancellationToken); }` — возвращает число отправленных уведомлений;
  - `Task IInvoiceNotifier.NotifyOverdueAsync(InvoiceEntity invoice, int stage, CancellationToken)` (сигнатура расширена стадией);
  - `Task IInvoiceNotifier.NotifyDueSoonAsync(InvoiceEntity invoice, CancellationToken)`;
  - `BillingOptions.DueSoonReminderBefore` и `BillingOptions.DunningOffsetsAfterDue`.
  - Задача 5 дописывает переходы статуса в этот же `EfDunningRunner`.

Флип `issued → overdue` переезжает из `EfInvoiceGenerationRunner` сюда: это часть работы с неплатежом, а не выставления. Два существующих теста (`RunAsync_FlipsIssuedInvoicesToOverdueAfterDueDate`, `RunAsync_FlippingInvoiceToOverdue_NotifiesOverdueOncePerInvoice`) переезжают в `EfDunningRunnerTests` вместе с ним.

- [ ] **Step 1: Написать падающий тест**

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Tests.Billing;

public sealed class EfDunningRunnerTests
{
    private static readonly DateTimeOffset Due = DateTimeOffset.Parse("2026-05-10T00:00:00Z");

    private static PlatformDbContext NewContext() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static EfDunningRunner NewRunner(PlatformDbContext db, RecordingInvoiceNotifier notifier) =>
        new(db, Options.Create(new BillingOptions()), notifier);

    private static async Task<InvoiceEntity> SeedAsync(
        PlatformDbContext db,
        string status = InvoiceStatusNames.Issued,
        DateTimeOffset? graceUntil = null)
    {
        var orgId = Guid.NewGuid();
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = orgId, Slug = "o", Name = "O", Status = OrganizationStatusNames.Active,
            PlanCode = "starter", SubscriptionStatus = SubscriptionStatusNames.Active, LimitsJson = "{}",
            CreatedAtUtc = Due, UpdatedAtUtc = Due
        });
        db.OrganizationSubscriptions.Add(new OrganizationSubscriptionEntity
        {
            OrganizationSubscriptionId = Guid.NewGuid(),
            OrganizationId = orgId,
            PlanCode = "starter",
            Status = SubscriptionStatusNames.Active,
            CurrentPeriodStartUtc = Due,
            CurrentPeriodEndUtc = Due.AddMonths(1),
            AmountMinorUnits = 290000,
            CurrencyCode = "TJS",
            BillingInterval = BillingIntervalNames.Monthly,
            PaymentGraceUntilUtc = graceUntil,
            CreatedAtUtc = Due,
            UpdatedAtUtc = Due
        });
        var invoice = new InvoiceEntity
        {
            InvoiceId = Guid.NewGuid(),
            OrganizationId = orgId,
            Number = 1,
            Kind = InvoiceKindNames.Subscription,
            PeriodStartUtc = Due.AddMonths(-1),
            PeriodEndUtc = Due,
            IssuedAtUtc = Due.AddDays(-7),
            DueAtUtc = Due,
            AmountMinorUnits = 290000,
            GrossAmountMinorUnits = 290000,
            CurrencyCode = "TJS",
            Status = status,
            Description = "d",
            CreatedAtUtc = Due.AddDays(-7),
            UpdatedAtUtc = Due.AddDays(-7)
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();
        return invoice;
    }

    [Fact]
    public async Task RunAsync_ThreeDaysBeforeDue_SendsDueSoonOnce()
    {
        await using var db = NewContext();
        await SeedAsync(db);
        var notifier = new RecordingInvoiceNotifier();
        var runner = NewRunner(db, notifier);

        await runner.RunAsync(Due.AddDays(-3), CancellationToken.None);
        await runner.RunAsync(Due.AddDays(-2), CancellationToken.None);

        Assert.Single(notifier.DueSoon);
        var invoice = await db.Invoices.SingleAsync();
        Assert.Equal(Due.AddDays(-3), invoice.DueSoonNotifiedAtUtc);
        Assert.Equal(0, invoice.DunningStage);
    }

    [Fact]
    public async Task RunAsync_PastDueDate_FlipsToOverdueAndSendsStageOne()
    {
        await using var db = NewContext();
        await SeedAsync(db);
        var notifier = new RecordingInvoiceNotifier();
        var runner = NewRunner(db, notifier);

        await runner.RunAsync(Due.AddHours(1), CancellationToken.None);

        var invoice = await db.Invoices.SingleAsync();
        Assert.Equal(InvoiceStatusNames.Overdue, invoice.Status);
        Assert.Equal(1, invoice.DunningStage);
        Assert.Equal(1, Assert.Single(notifier.Overdue).Stage);
    }

    [Fact]
    public async Task RunAsync_TenDaysOverdue_SendsOnlyHighestDueStage()
    {
        await using var db = NewContext();
        await SeedAsync(db);
        var notifier = new RecordingInvoiceNotifier();
        var runner = NewRunner(db, notifier);

        await runner.RunAsync(Due.AddDays(10), CancellationToken.None);

        Assert.Equal(3, Assert.Single(notifier.Overdue).Stage); // offsets 0,3,7,14 → +10 days is rung 3
        Assert.Equal(3, (await db.Invoices.SingleAsync()).DunningStage);
    }

    [Fact]
    public async Task RunAsync_SameStageTwice_DoesNotResend()
    {
        await using var db = NewContext();
        await SeedAsync(db);
        var notifier = new RecordingInvoiceNotifier();
        var runner = NewRunner(db, notifier);

        await runner.RunAsync(Due.AddDays(4), CancellationToken.None);
        await runner.RunAsync(Due.AddDays(5), CancellationToken.None);

        Assert.Single(notifier.Overdue);
    }

    [Fact]
    public async Task RunAsync_UnderGrace_SendsNothingAndKeepsStage()
    {
        await using var db = NewContext();
        await SeedAsync(db, graceUntil: Due.AddDays(30));
        var notifier = new RecordingInvoiceNotifier();
        var runner = NewRunner(db, notifier);

        await runner.RunAsync(Due.AddDays(10), CancellationToken.None);

        Assert.Empty(notifier.Overdue);
        Assert.Empty(notifier.DueSoon);
        Assert.Equal(0, (await db.Invoices.SingleAsync()).DunningStage);
    }

    [Fact]
    public async Task RunAsync_AfterGraceExpires_ResumesAtAgeAppropriateStage()
    {
        await using var db = NewContext();
        await SeedAsync(db, graceUntil: Due.AddDays(5));
        var notifier = new RecordingInvoiceNotifier();
        var runner = NewRunner(db, notifier);

        await runner.RunAsync(Due.AddDays(3), CancellationToken.None);   // silenced by grace
        await runner.RunAsync(Due.AddDays(16), CancellationToken.None);  // grace expired

        var sent = Assert.Single(notifier.Overdue);
        Assert.Equal(4, sent.Stage); // resumes at the rung its real age warrants, not at rung 1
    }

    [Fact]
    public async Task RunAsync_PaidInvoice_IsIgnored()
    {
        await using var db = NewContext();
        await SeedAsync(db, status: InvoiceStatusNames.Paid);
        var notifier = new RecordingInvoiceNotifier();
        var runner = NewRunner(db, notifier);

        await runner.RunAsync(Due.AddDays(10), CancellationToken.None);

        Assert.Empty(notifier.Overdue);
    }
}
```

- [ ] **Step 2: Расширить `RecordingInvoiceNotifier`**

```csharp
    public List<(InvoiceEntity Invoice, int Stage)> Overdue { get; } = [];
    public List<InvoiceEntity> DueSoon { get; } = [];

    public Task NotifyOverdueAsync(InvoiceEntity invoice, int stage, DateTimeOffset now, CancellationToken cancellationToken)
    {
        Overdue.Add((invoice, stage));
        return Task.CompletedTask;
    }

    public Task NotifyDueSoonAsync(InvoiceEntity invoice, DateTimeOffset now, CancellationToken cancellationToken)
    {
        DueSoon.Add(invoice);
        return Task.CompletedTask;
    }
```

Существующие обращения `notifier.Overdue` в `EfInvoiceGenerationRunnerTests` при переносе тестов подстроить под кортеж (`Assert.Single(notifier.Overdue).Invoice`).

- [ ] **Step 3: Убедиться, что тест падает**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~EfDunningRunnerTests`
Expected: FAIL — типа `EfDunningRunner` не существует.

- [ ] **Step 4: Расширить настройки**

В `BillingOptions`:

```csharp
    /// <summary>How long before the due date the pre-due reminder goes out.</summary>
    public TimeSpan DueSoonReminderBefore { get; set; } = TimeSpan.FromDays(3);

    /// <summary>Overdue ladder rungs, as day offsets past the due date. Index + 1 is the stage number.</summary>
    public int[] DunningOffsetsAfterDue { get; set; } = [0, 3, 7, 14];
```

- [ ] **Step 5: Расширить нотификатор**

Токен «дней просрочки» считается от текущего времени, а время в сервисы этого проекта приходит снаружи — `DateTimeOffset.UtcNow` внутри нотификатора недопустим. Поэтому обе новые сигнатуры несут `now`:

```csharp
    Task NotifyDueSoonAsync(InvoiceEntity invoice, DateTimeOffset now, CancellationToken cancellationToken);

    Task NotifyOverdueAsync(InvoiceEntity invoice, int stage, DateTimeOffset now, CancellationToken cancellationToken);
```

`NotifyIssuedAsync` и `NotifyPaidAsync` не трогать. В `EfInvoiceNotifier`:

```csharp
    public Task NotifyDueSoonAsync(InvoiceEntity invoice, DateTimeOffset now, CancellationToken cancellationToken) =>
        SendAsync(invoice, NotificationTemplateKeys.InvoiceDueSoon, $"invoice-due-soon:{invoice.InvoiceId:N}", now, cancellationToken);

    public Task NotifyOverdueAsync(InvoiceEntity invoice, int stage, DateTimeOffset now, CancellationToken cancellationToken) =>
        SendAsync(invoice, NotificationTemplateKeys.InvoiceOverdue, $"invoice-overdue:{invoice.InvoiceId:N}:{stage}", now, cancellationToken);
```

`SendAsync` и `BuildTokens` получают параметр `DateTimeOffset now`; у `NotifyIssuedAsync`/`NotifyPaidAsync` передаётся `invoice.IssuedAtUtc` и `invoice.PaidAtUtc ?? invoice.UpdatedAtUtc` соответственно, чтобы токен считался и для них. В `BuildTokens` добавить:

```csharp
            ["daysOverdue"] = Math.Max(0, (int)Math.Floor((now - invoice.DueAtUtc).TotalDays))
                .ToString(CultureInfo.InvariantCulture),
```

Константу `FirstDunningStage` и комментарий про отложенные стадии удалить — они больше не описывают правду.

- [ ] **Step 6: Добавить шаблон «скоро срок»**

`NotificationTemplateKeys`: `public const string InvoiceDueSoon = "invoice.due_soon";` и добавить его в массив известных ключей в конце файла.

`Notifications/Templates/ru/invoice.due_soon.json`:

```json
{
  "subject": "AFK4.NET: счёт №{{invoiceNumber}} — срок оплаты {{dueDate}}",
  "bodyText": "Здравствуйте, {{displayName}}. Напоминаем: счёт №{{invoiceNumber}} на сумму {{amount}} {{currency}} для клуба «{{organizationName}}» нужно оплатить до {{dueDate}}.",
  "bodyHtml": "<p>Здравствуйте, {{displayName}}.</p><p>Напоминаем: счёт <strong>№{{invoiceNumber}}</strong> на сумму <strong>{{amount}} {{currency}}</strong> для клуба «{{organizationName}}» нужно оплатить до {{dueDate}}.</p>"
}
```

`en/invoice.due_soon.json`:

```json
{
  "subject": "AFK4.NET: invoice #{{invoiceNumber}} is due on {{dueDate}}",
  "bodyText": "Hello {{displayName}}. A reminder: invoice #{{invoiceNumber}} for {{amount}} {{currency}} for the club \"{{organizationName}}\" is due on {{dueDate}}.",
  "bodyHtml": "<p>Hello {{displayName}}.</p><p>A reminder: invoice <strong>#{{invoiceNumber}}</strong> for <strong>{{amount}} {{currency}}</strong> for the club \"{{organizationName}}\" is due on {{dueDate}}.</p>"
}
```

`tg/invoice.due_soon.json`:

```json
{
  "subject": "AFK4.NET: ҳисобномаи №{{invoiceNumber}} — мӯҳлати пардохт {{dueDate}}",
  "bodyText": "Салом, {{displayName}}. Ёдовар мешавем: ҳисобномаи №{{invoiceNumber}} ба маблағи {{amount}} {{currency}} барои клуби «{{organizationName}}» то {{dueDate}} бояд пардохт шавад.",
  "bodyHtml": "<p>Салом, {{displayName}}.</p><p>Ёдовар мешавем: ҳисобномаи <strong>№{{invoiceNumber}}</strong> ба маблағи <strong>{{amount}} {{currency}}</strong> барои клуби «{{organizationName}}» то {{dueDate}} бояд пардохт шавад.</p>"
}
```

В `ru/invoice.overdue.json`, `en/`, `tg/` добавить в текст число дней просрочки, используя новый токен `{{daysOverdue}}` (например, в русском: «просрочен на {{daysOverdue}} дн.»).

- [ ] **Step 7: Реализовать проход по неплатежам**

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Platform.Billing;

public sealed class EfDunningRunner(
    PlatformDbContext dbContext,
    IOptions<BillingOptions> options,
    IInvoiceNotifier invoiceNotifier) : IDunningRunner
{
    private readonly BillingOptions options = options.Value;

    public async Task<int> RunAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var unpaid = await dbContext.Invoices
            .Where(invoice => invoice.Status == InvoiceStatusNames.Issued || invoice.Status == InvoiceStatusNames.Overdue)
            .ToListAsync(cancellationToken);
        if (unpaid.Count == 0)
        {
            return 0;
        }

        var organizationIds = unpaid.Select(invoice => invoice.OrganizationId).Distinct().ToList();
        var graceByOrganization = await dbContext.OrganizationSubscriptions
            .Where(subscription => organizationIds.Contains(subscription.OrganizationId))
            .ToDictionaryAsync(
                subscription => subscription.OrganizationId,
                subscription => subscription.PaymentGraceUntilUtc,
                cancellationToken);

        var pendingDueSoon = new List<InvoiceEntity>();
        var pendingOverdue = new List<(InvoiceEntity Invoice, int Stage)>();

        foreach (var invoice in unpaid)
        {
            if (invoice.Status == InvoiceStatusNames.Issued && invoice.DueAtUtc < now)
            {
                invoice.Status = InvoiceStatusNames.Overdue;
                invoice.UpdatedAtUtc = now;
            }

            // Grace is a promise not to chase, so it silences the whole ladder — including the
            // pre-due reminder — rather than only the emails after the due date.
            if (graceByOrganization.TryGetValue(invoice.OrganizationId, out var graceUntil)
                && graceUntil is not null
                && graceUntil > now)
            {
                continue;
            }

            if (invoice.DueSoonNotifiedAtUtc is null
                && now >= invoice.DueAtUtc - options.DueSoonReminderBefore
                && now < invoice.DueAtUtc)
            {
                invoice.DueSoonNotifiedAtUtc = now;
                invoice.UpdatedAtUtc = now;
                pendingDueSoon.Add(invoice);
                continue;
            }

            var stage = DueStage(invoice.DueAtUtc, now);
            if (stage > invoice.DunningStage)
            {
                invoice.DunningStage = stage;
                invoice.LastDunningAtUtc = now;
                invoice.UpdatedAtUtc = now;
                pendingOverdue.Add((invoice, stage));
            }
        }

        if (pendingDueSoon.Count == 0 && pendingOverdue.Count == 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return 0;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var invoice in pendingDueSoon)
        {
            await invoiceNotifier.NotifyDueSoonAsync(invoice, now, cancellationToken);
        }

        foreach (var (invoice, stage) in pendingOverdue)
        {
            await invoiceNotifier.NotifyOverdueAsync(invoice, stage, now, cancellationToken);
        }

        return pendingDueSoon.Count + pendingOverdue.Count;
    }

    /// <summary>Highest ladder rung the invoice's age has reached; 0 when it is not overdue yet.
    /// An invoice first seen ten days late sends one notice, not the whole ladder in a burst.</summary>
    private int DueStage(DateTimeOffset dueAtUtc, DateTimeOffset now)
    {
        var stage = 0;
        for (var index = 0; index < options.DunningOffsetsAfterDue.Length; index++)
        {
            if (now >= dueAtUtc.AddDays(options.DunningOffsetsAfterDue[index]))
            {
                stage = index + 1;
            }
        }

        return stage;
    }
}
```

`IDunningRunner.cs`:

```csharp
namespace AFK4.Platform.Api.Platform.Billing;

public interface IDunningRunner
{
    /// <summary>Flips due invoices to overdue, sends the pre-due reminder and the overdue ladder,
    /// and returns the number of notifications sent.</summary>
    Task<int> RunAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
```

- [ ] **Step 8: Убрать флип из генератора счетов**

В `EfInvoiceGenerationRunner.RunAsync` удалить блок, который выбирает просроченные счета, переводит их в `overdue` и зовёт `NotifyOverdueAsync`. Обновить XML-док у `IInvoiceGenerationRunner.RunAsync`: он больше не флипает просрочку.

- [ ] **Step 9: Подключить в хост и DI**

`Program.cs`: `builder.Services.AddScoped<IDunningRunner, EfDunningRunner>();` рядом с регистрацией `IInvoiceGenerationRunner`.

`InvoiceGenerationHostedService.TickAsync` — после выставления счетов:

```csharp
        var dunning = scope.ServiceProvider.GetRequiredService<IDunningRunner>();
        var notified = await dunning.RunAsync(now, cancellationToken);
        if (notified > 0)
        {
            logger.LogInformation("Dunning tick sent {Count} notice(s).", notified);
        }
```

- [ ] **Step 10: Перенести два теста флипа**

Из `EfInvoiceGenerationRunnerTests` перенести `RunAsync_FlipsIssuedInvoicesToOverdueAfterDueDate` и `RunAsync_FlippingInvoiceToOverdue_NotifiesOverdueOncePerInvoice` в `EfDunningRunnerTests`, переписав на `EfDunningRunner` и на кортеж в `notifier.Overdue`. В `EfInvoiceGenerationRunnerTests` оставить проверку, что генератор просрочку **не** трогает.

- [ ] **Step 11: Прогнать тесты**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~Billing`
Expected: PASS

- [ ] **Step 12: Коммит**

```bash
git add -A
git commit -m "feat(platform): лестница напоминаний о неоплаченных счетах"
```

---

### Task 5: Автоматический `past_due` и корректный MRR

**Files:**
- Modify: `src/AFK4.Platform.Api/Platform/Billing/EfDunningRunner.cs`
- Modify: `src/AFK4.Platform.Api/Platform/Billing/EfInvoiceService.cs` (`MarkPaidAsync`)
- Modify: `src/AFK4.Platform.Api/Platform/Billing/EfBillingMetricsService.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Billing/EfDunningRunnerTests.cs`, `EfInvoiceServiceTests.cs`, `EfBillingMetricsServiceTests.cs` (создать, если файла нет)

**Interfaces:**
- Consumes: `BillingBalance.Compute` (задача 3), `EfDunningRunner` (задача 4).
- Produces: инвариант «подписка в `past_due` ⇔ у клуба есть просроченный долг и нет действующей отсрочки», на который опирается план 2.

Статус организации не трогаем ни в одном шаге этой задачи — приостановка остаётся ручной.

- [ ] **Step 1: Написать падающие тесты**

Дописать в `EfDunningRunnerTests`:

```csharp
    [Fact]
    public async Task RunAsync_ArrearsWithoutGrace_MovesSubscriptionToPastDue()
    {
        await using var db = NewContext();
        await SeedAsync(db);
        var runner = NewRunner(db, new RecordingInvoiceNotifier());

        await runner.RunAsync(Due.AddDays(1), CancellationToken.None);

        var subscription = await db.OrganizationSubscriptions.SingleAsync();
        Assert.Equal(SubscriptionStatusNames.PastDue, subscription.Status);
        var organization = await db.Organizations.SingleAsync();
        Assert.Equal(SubscriptionStatusNames.PastDue, organization.SubscriptionStatus);
        Assert.Equal(OrganizationStatusNames.Active, organization.Status);
    }

    [Fact]
    public async Task RunAsync_ArrearsUnderGrace_LeavesSubscriptionActive()
    {
        await using var db = NewContext();
        await SeedAsync(db, graceUntil: Due.AddDays(30));
        var runner = NewRunner(db, new RecordingInvoiceNotifier());

        await runner.RunAsync(Due.AddDays(1), CancellationToken.None);

        Assert.Equal(SubscriptionStatusNames.Active, (await db.OrganizationSubscriptions.SingleAsync()).Status);
    }

    [Fact]
    public async Task RunAsync_CreditNoteCoversDebt_ReturnsSubscriptionToActive()
    {
        await using var db = NewContext();
        var invoice = await SeedAsync(db);
        var runner = NewRunner(db, new RecordingInvoiceNotifier());
        await runner.RunAsync(Due.AddDays(1), CancellationToken.None);

        db.Invoices.Add(new InvoiceEntity
        {
            InvoiceId = Guid.NewGuid(),
            OrganizationId = invoice.OrganizationId,
            Number = 2,
            Kind = InvoiceKindNames.Credit,
            PeriodStartUtc = Due,
            PeriodEndUtc = Due,
            IssuedAtUtc = Due,
            DueAtUtc = Due,
            AmountMinorUnits = -290000,
            GrossAmountMinorUnits = -290000,
            CurrencyCode = "TJS",
            Status = InvoiceStatusNames.Issued,
            Description = "Компенсация простоя",
            CreatedAtUtc = Due,
            UpdatedAtUtc = Due
        });
        await db.SaveChangesAsync();

        await runner.RunAsync(Due.AddDays(2), CancellationToken.None);

        Assert.Equal(SubscriptionStatusNames.Active, (await db.OrganizationSubscriptions.SingleAsync()).Status);
    }

    [Fact]
    public async Task RunAsync_TrialSubscription_IsNotMovedToPastDue()
    {
        await using var db = NewContext();
        await SeedAsync(db);
        var subscription = await db.OrganizationSubscriptions.SingleAsync();
        subscription.Status = SubscriptionStatusNames.Trial;
        await db.SaveChangesAsync();
        var runner = NewRunner(db, new RecordingInvoiceNotifier());

        await runner.RunAsync(Due.AddDays(1), CancellationToken.None);

        Assert.Equal(SubscriptionStatusNames.Trial, (await db.OrganizationSubscriptions.SingleAsync()).Status);
    }
```

Дописать в `EfInvoiceServiceTests` (`Now`, `NewContext`, `NewService`, `SeedOrganizationWithSubscriptionAsync` — существующие хелперы этого файла):

```csharp
    private static async Task<InvoiceEntity> AddOverdueInvoiceAsync(PlatformDbContext db, Guid orgId, int number)
    {
        var invoice = new InvoiceEntity
        {
            InvoiceId = Guid.NewGuid(),
            OrganizationId = orgId,
            Number = number,
            Kind = InvoiceKindNames.Subscription,
            PeriodStartUtc = Now.AddMonths(-1),
            PeriodEndUtc = Now,
            IssuedAtUtc = Now.AddDays(-10),
            DueAtUtc = Now.AddDays(-3),
            AmountMinorUnits = 290000,
            GrossAmountMinorUnits = 290000,
            CurrencyCode = "TJS",
            Status = InvoiceStatusNames.Overdue,
            Description = "d",
            CreatedAtUtc = Now.AddDays(-10),
            UpdatedAtUtc = Now.AddDays(-10)
        };
        db.Invoices.Add(invoice);

        var subscription = await db.OrganizationSubscriptions.SingleAsync();
        subscription.Status = SubscriptionStatusNames.PastDue;
        var organization = await db.Organizations.SingleAsync();
        organization.SubscriptionStatus = SubscriptionStatusNames.PastDue;
        await db.SaveChangesAsync();
        return invoice;
    }

    [Fact]
    public async Task MarkPaidAsync_LastOverdueInvoicePaid_ReturnsSubscriptionToActive()
    {
        await using var db = NewContext();
        var orgId = await SeedOrganizationWithSubscriptionAsync(db);
        var invoice = await AddOverdueInvoiceAsync(db, orgId, number: 1);
        var service = NewService(db, new FixedTimeProvider(Now));

        var result = await service.MarkPaidAsync(invoice.InvoiceId, new MarkInvoicePaidRequest(Reference: "cash"), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(SubscriptionStatusNames.Active, (await db.OrganizationSubscriptions.SingleAsync()).Status);
        Assert.Equal(SubscriptionStatusNames.Active, (await db.Organizations.SingleAsync()).SubscriptionStatus);
    }

    [Fact]
    public async Task MarkPaidAsync_AnotherOverdueInvoiceRemains_KeepsPastDue()
    {
        await using var db = NewContext();
        var orgId = await SeedOrganizationWithSubscriptionAsync(db);
        var first = await AddOverdueInvoiceAsync(db, orgId, number: 1);
        await AddOverdueInvoiceAsync(db, orgId, number: 2);
        var service = NewService(db, new FixedTimeProvider(Now));

        await service.MarkPaidAsync(first.InvoiceId, new MarkInvoicePaidRequest(Reference: "cash"), CancellationToken.None);

        Assert.Equal(SubscriptionStatusNames.PastDue, (await db.OrganizationSubscriptions.SingleAsync()).Status);
    }
```

Если позиционная сигнатура `MarkInvoicePaidRequest` в репозитории отличается — брать её из соседних тестов файла, а не подгонять под этот листинг.

- [ ] **Step 2: Убедиться, что тесты падают**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~EfDunningRunnerTests`
Expected: FAIL — подписка остаётся `active`.

- [ ] **Step 3: Реализовать переходы в проходе по неплатежам**

В конце `EfDunningRunner.RunAsync`, после сохранения, добавить синхронизацию статуса по каждой организации из `organizationIds`:

```csharp
        await SyncSubscriptionStatusesAsync(organizationIds, unpaid, graceByOrganization, now, cancellationToken);
```

```csharp
    /// <summary>Keeps "subscription is past_due" equivalent to "the club owes overdue money and has no
    /// live grace". Organization status is never touched here: suspension stays a human decision.</summary>
    private async Task SyncSubscriptionStatusesAsync(
        IReadOnlyCollection<Guid> organizationIds,
        IReadOnlyCollection<InvoiceEntity> unpaid,
        IReadOnlyDictionary<Guid, DateTimeOffset?> graceByOrganization,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var subscriptions = await dbContext.OrganizationSubscriptions
            .Where(subscription => organizationIds.Contains(subscription.OrganizationId))
            .ToListAsync(cancellationToken);
        var organizations = await dbContext.Organizations
            .Where(organization => organizationIds.Contains(organization.OrganizationId))
            .ToDictionaryAsync(organization => organization.OrganizationId, cancellationToken);

        var changed = false;
        foreach (var subscription in subscriptions)
        {
            if (subscription.Status is not (SubscriptionStatusNames.Active or SubscriptionStatusNames.PastDue))
            {
                continue; // trial and cancelled subscriptions are not part of the dunning cycle
            }

            var balance = BillingBalance.Compute(
                unpaid.Where(invoice => invoice.OrganizationId == subscription.OrganizationId).ToList());
            var underGrace = graceByOrganization.TryGetValue(subscription.OrganizationId, out var graceUntil)
                && graceUntil is not null
                && graceUntil > now;

            var target = balance.InArrears && !underGrace
                ? SubscriptionStatusNames.PastDue
                : SubscriptionStatusNames.Active;

            // Grace suppresses new transitions but does not settle debt: a subscription that was
            // already past_due when grace was granted stays past_due until the money arrives.
            if (underGrace && subscription.Status == SubscriptionStatusNames.PastDue && balance.InArrears)
            {
                continue;
            }

            if (subscription.Status == target)
            {
                continue;
            }

            subscription.Status = target;
            subscription.UpdatedAtUtc = now;
            if (organizations.TryGetValue(subscription.OrganizationId, out var organization))
            {
                organization.SubscriptionStatus = target;
                organization.UpdatedAtUtc = now;
            }

            changed = true;
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
```

- [ ] **Step 4: Вернуть подписку из `past_due` при оплате**

В `EfInvoiceService.MarkPaidAsync`, после `SaveChangesAsync` и до уведомления, добавить:

```csharp
        await RestoreSubscriptionIfSettledAsync(invoice.OrganizationId, now, cancellationToken);
```

```csharp
    /// <summary>Payment is a restoration, so it applies immediately rather than waiting for the next
    /// scheduler tick — the club should not stay flagged after it has paid.</summary>
    private async Task RestoreSubscriptionIfSettledAsync(
        Guid organizationId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var subscription = await dbContext.OrganizationSubscriptions
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId, cancellationToken);
        if (subscription is null || subscription.Status != SubscriptionStatusNames.PastDue)
        {
            return;
        }

        var unpaid = await dbContext.Invoices
            .Where(candidate => candidate.OrganizationId == organizationId
                && (candidate.Status == InvoiceStatusNames.Issued || candidate.Status == InvoiceStatusNames.Overdue))
            .ToListAsync(cancellationToken);
        if (BillingBalance.Compute(unpaid).InArrears)
        {
            return;
        }

        subscription.Status = SubscriptionStatusNames.Active;
        subscription.UpdatedAtUtc = now;
        var organization = await dbContext.Organizations
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId, cancellationToken);
        if (organization is not null)
        {
            organization.SubscriptionStatus = SubscriptionStatusNames.Active;
            organization.UpdatedAtUtc = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
```

- [ ] **Step 5: Починить MRR**

В `EfBillingMetricsService` заменить фильтр `s.Status == SubscriptionStatusNames.Active` на

```csharp
            .Where(s => s.Status == SubscriptionStatusNames.Active || s.Status == SubscriptionStatusNames.PastDue)
```

и обновить комментарий: MRR считает `active` и `past_due` — просрочивший клуб остаётся клиентом; `trial` и `cancelled` исключены. Поле `ActiveSubscriptions` в DTO по-прежнему означает число этих же подписок, менять контракт не нужно.

Файла `tests/AFK4.Platform.Api.Tests/Billing/EfBillingMetricsServiceTests.cs` в репозитории нет — создать:

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests.Billing;

public sealed class EfBillingMetricsServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-05-31T00:00:00Z");

    private static PlatformDbContext NewContext() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static void AddSubscription(PlatformDbContext db, string status, long amount) =>
        db.OrganizationSubscriptions.Add(new OrganizationSubscriptionEntity
        {
            OrganizationSubscriptionId = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            PlanCode = "starter",
            Status = status,
            CurrentPeriodStartUtc = Now,
            CurrentPeriodEndUtc = Now.AddMonths(1),
            AmountMinorUnits = amount,
            CurrencyCode = "TJS",
            BillingInterval = BillingIntervalNames.Monthly,
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        });

    [Fact]
    public async Task GetAsync_PastDueSubscription_IsCountedInMrr()
    {
        await using var db = NewContext();
        AddSubscription(db, SubscriptionStatusNames.Active, 290000);
        AddSubscription(db, SubscriptionStatusNames.PastDue, 290000);
        await db.SaveChangesAsync();

        var metrics = await new EfBillingMetricsService(db).GetAsync(CancellationToken.None);

        Assert.Equal(580000, metrics.MrrMinorUnits);
        Assert.Equal(2, metrics.ActiveSubscriptions);
    }

    [Fact]
    public async Task GetAsync_TrialAndCancelled_AreExcludedFromMrr()
    {
        await using var db = NewContext();
        AddSubscription(db, SubscriptionStatusNames.Trial, 290000);
        AddSubscription(db, SubscriptionStatusNames.Cancelled, 290000);
        await db.SaveChangesAsync();

        var metrics = await new EfBillingMetricsService(db).GetAsync(CancellationToken.None);

        Assert.Equal(0, metrics.MrrMinorUnits);
    }
}
```

- [ ] **Step 6: Убедиться, что тесты проходят**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~Billing`
Expected: PASS

- [ ] **Step 7: Коммит**

```bash
git add -A
git commit -m "feat(platform): автоматический перевод подписки в просрочку и обратно"
```

---

## Слайс 3 — гибкая цена

### Task 6: Скидка на срок

**Files:**
- Modify: `src/AFK4.Platform.Api/Data/OrganizationSubscriptionEntity.cs`
- Modify: `src/AFK4.Shared.Contracts/Platform/Billing/OrganizationSubscriptionDto.cs`, `UpdateSubscriptionRequest.cs`, `InvoiceDto.cs`
- Create: `src/AFK4.Platform.Api/Platform/Billing/SubscriptionDiscount.cs`
- Modify: `src/AFK4.Platform.Api/Platform/Billing/EfOrganizationSubscriptionService.cs`, `EfInvoiceGenerationRunner.cs`, `EfInvoiceService.cs` (маппинг DTO)
- Create: миграция `AddSubscriptionDiscount`
- Test: `tests/AFK4.Platform.Api.Tests/Billing/SubscriptionDiscountTests.cs`, дополнения в `EfOrganizationSubscriptionServiceTests.cs` и `EfInvoiceGenerationRunnerTests.cs`

**Interfaces:**
- Consumes: коды годовых планов из задачи 2; колонки `GrossAmountMinorUnits` и `DiscountMinorUnits` из задачи 3.
- Produces:
  - на подписке: `int? DiscountPercent`, `long? DiscountAmountMinorUnits`, `DateTimeOffset? DiscountUntilUtc`, `string? DiscountReason`;
  - `static long SubscriptionDiscount.Apply(long grossMinorUnits, int? percent, long? fixedAmountMinorUnits)` → размер скидки, не больше суммы до скидки;
  - `UpdateSubscriptionRequest` расширяется полями скидки и флагом `ClearDiscount`.
  - На поля счёта опирается план 2 (показ скидки в панели).

- [ ] **Step 1: Написать падающий тест на расчёт**

```csharp
using AFK4.Platform.Api.Platform.Billing;

namespace AFK4.Platform.Api.Tests.Billing;

public sealed class SubscriptionDiscountTests
{
    [Fact]
    public void Apply_NoDiscount_IsZero() =>
        Assert.Equal(0, SubscriptionDiscount.Apply(290000, percent: null, fixedAmountMinorUnits: null));

    [Fact]
    public void Apply_Percent_RoundsDownToMinorUnit() =>
        Assert.Equal(87000, SubscriptionDiscount.Apply(290000, percent: 30, fixedAmountMinorUnits: null));

    [Fact]
    public void Apply_FixedAmount_IsTakenAsIs() =>
        Assert.Equal(50000, SubscriptionDiscount.Apply(290000, percent: null, fixedAmountMinorUnits: 50000));

    [Fact]
    public void Apply_FixedAmountLargerThanGross_FloorsAtGross() =>
        Assert.Equal(290000, SubscriptionDiscount.Apply(290000, percent: null, fixedAmountMinorUnits: 400000));
}
```

- [ ] **Step 2: Убедиться, что тест падает**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~SubscriptionDiscountTests`
Expected: FAIL — типа `SubscriptionDiscount` не существует.

- [ ] **Step 3: Реализовать расчёт**

```csharp
namespace AFK4.Platform.Api.Platform.Billing;

/// <summary>A negotiated discount lives beside the plan price instead of overwriting the subscription
/// amount, so changing the plan no longer silently erases what was agreed with the club.</summary>
public static class SubscriptionDiscount
{
    public static long Apply(long grossMinorUnits, int? percent, long? fixedAmountMinorUnits)
    {
        var discount = percent is not null
            ? grossMinorUnits * percent.Value / 100
            : fixedAmountMinorUnits ?? 0;

        return Math.Clamp(discount, 0, Math.Max(0, grossMinorUnits));
    }
}
```

- [ ] **Step 4: Убедиться, что тест проходит**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~SubscriptionDiscountTests`
Expected: PASS

- [ ] **Step 5: Добавить поля и миграцию**

В `OrganizationSubscriptionEntity`:

```csharp
    public int? DiscountPercent { get; set; }

    public long? DiscountAmountMinorUnits { get; set; }

    public DateTimeOffset? DiscountUntilUtc { get; set; }

    public string? DiscountReason { get; set; }
```

Расширить `OrganizationSubscriptionDto` четырьмя полями скидки и `InvoiceDto` двумя полями суммы (добавлять в конец записи, чтобы не ломать позиционные вызовы). Расширить `UpdateSubscriptionRequest`:

```csharp
    int? DiscountPercent = null,
    long? DiscountAmountMinorUnits = null,
    DateTimeOffset? DiscountUntilUtc = null,
    string? DiscountReason = null,
    bool? ClearDiscount = null
```

Миграция:

```bash
dotnet ef migrations add AddSubscriptionDiscount --project src/AFK4.Platform.Api --startup-project src/AFK4.Platform.Api
```

- [ ] **Step 6: Написать падающий тест на валидацию и применение**

Дописать в `EfOrganizationSubscriptionServiceTests` (`Now`, `NewContext`, `SeedOrgAndPlansAsync`, `FixedTimeProvider` — существующие хелперы файла; во всех вызовах `UpdateSubscriptionRequest` аргументы именованные, поэтому новые поля просто дописываются):

```csharp
    [Fact]
    public async Task UpdateAsync_BothDiscountFormsSet_IsRejected()
    {
        await using var db = NewContext();
        var orgId = await SeedOrgAndPlansAsync(db, "starter");
        var service = new EfOrganizationSubscriptionService(db, new FixedTimeProvider(Now));
        await service.GetAsync(orgId, CancellationToken.None);

        var result = await service.UpdateAsync(orgId, new UpdateSubscriptionRequest(
            PlanCode: null, BillingInterval: null, Status: null, CancelAtPeriodEnd: null,
            AmountMinorUnits: null, CurrentPeriodEndUtc: null, PaymentGraceUntilUtc: null,
            DiscountPercent: 30, DiscountAmountMinorUnits: 50000), CancellationToken.None);

        Assert.Equal(BillingOperationStatus.BadRequest, result.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task UpdateAsync_PercentOutOfRange_IsRejected(int percent)
    {
        await using var db = NewContext();
        var orgId = await SeedOrgAndPlansAsync(db, "starter");
        var service = new EfOrganizationSubscriptionService(db, new FixedTimeProvider(Now));
        await service.GetAsync(orgId, CancellationToken.None);

        var result = await service.UpdateAsync(orgId, new UpdateSubscriptionRequest(
            PlanCode: null, BillingInterval: null, Status: null, CancelAtPeriodEnd: null,
            AmountMinorUnits: null, CurrentPeriodEndUtc: null, PaymentGraceUntilUtc: null,
            DiscountPercent: percent), CancellationToken.None);

        Assert.Equal(BillingOperationStatus.BadRequest, result.Status);
    }

    [Fact]
    public async Task UpdateAsync_PlanChange_KeepsDiscount()
    {
        await using var db = NewContext();
        var orgId = await SeedOrgAndPlansAsync(db, "starter");
        var service = new EfOrganizationSubscriptionService(db, new FixedTimeProvider(Now));
        await service.GetAsync(orgId, CancellationToken.None);
        await service.UpdateAsync(orgId, new UpdateSubscriptionRequest(
            PlanCode: null, BillingInterval: null, Status: null, CancelAtPeriodEnd: null,
            AmountMinorUnits: null, CurrentPeriodEndUtc: null, PaymentGraceUntilUtc: null,
            DiscountPercent: 30, DiscountReason: "Договорённость на запуск"), CancellationToken.None);

        var result = await service.UpdateAsync(orgId, new UpdateSubscriptionRequest(
            PlanCode: "scale", BillingInterval: null, Status: null, CancelAtPeriodEnd: null,
            AmountMinorUnits: null, CurrentPeriodEndUtc: null, PaymentGraceUntilUtc: null), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(30, result.Value!.DiscountPercent);
        Assert.Equal(1990000, result.Value.AmountMinorUnits);
    }

    [Fact]
    public async Task UpdateAsync_ClearDiscount_RemovesAllDiscountFields()
    {
        await using var db = NewContext();
        var orgId = await SeedOrgAndPlansAsync(db, "starter");
        var service = new EfOrganizationSubscriptionService(db, new FixedTimeProvider(Now));
        await service.GetAsync(orgId, CancellationToken.None);
        await service.UpdateAsync(orgId, new UpdateSubscriptionRequest(
            PlanCode: null, BillingInterval: null, Status: null, CancelAtPeriodEnd: null,
            AmountMinorUnits: null, CurrentPeriodEndUtc: null, PaymentGraceUntilUtc: null,
            DiscountPercent: 30, DiscountUntilUtc: Now.AddMonths(3), DiscountReason: "Запуск"), CancellationToken.None);

        var result = await service.UpdateAsync(orgId, new UpdateSubscriptionRequest(
            PlanCode: null, BillingInterval: null, Status: null, CancelAtPeriodEnd: null,
            AmountMinorUnits: null, CurrentPeriodEndUtc: null, PaymentGraceUntilUtc: null,
            ClearDiscount: true), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Null(result.Value!.DiscountPercent);
        Assert.Null(result.Value.DiscountAmountMinorUnits);
        Assert.Null(result.Value.DiscountUntilUtc);
        Assert.Null(result.Value.DiscountReason);
    }
```

Дописать в `EfInvoiceGenerationRunnerTests` (`Start`, `NewContext`, `NewRunner`, `SeedActiveDueSubscriptionAsync` — существующие хелперы файла):

```csharp
    [Fact]
    public async Task RunAsync_ActiveDiscount_SplitsInvoiceIntoGrossDiscountAndTotal()
    {
        await using var db = NewContext();
        var subscription = await SeedActiveDueSubscriptionAsync(db);
        subscription.DiscountPercent = 30;
        subscription.DiscountUntilUtc = Start.AddMonths(6);
        await db.SaveChangesAsync();
        var runner = NewRunner(db);

        await runner.RunAsync(Start.AddMonths(1), CancellationToken.None);

        var invoice = await db.Invoices.SingleAsync();
        Assert.Equal(290000, invoice.GrossAmountMinorUnits);
        Assert.Equal(87000, invoice.DiscountMinorUnits);
        Assert.Equal(203000, invoice.AmountMinorUnits);
    }

    [Fact]
    public async Task RunAsync_ExpiredDiscount_ChargesFullPrice()
    {
        await using var db = NewContext();
        var subscription = await SeedActiveDueSubscriptionAsync(db);
        subscription.DiscountPercent = 30;
        subscription.DiscountUntilUtc = Start.AddDays(1);
        await db.SaveChangesAsync();
        var runner = NewRunner(db);

        await runner.RunAsync(Start.AddMonths(1), CancellationToken.None);

        var invoice = await db.Invoices.SingleAsync();
        Assert.Equal(0, invoice.DiscountMinorUnits);
        Assert.Equal(290000, invoice.AmountMinorUnits);
    }
```

- [ ] **Step 7: Реализовать валидацию и применение**

В `EfOrganizationSubscriptionService.UpdateAsync`, рядом с существующими проверками отсрочки:

```csharp
        if (request.DiscountPercent is not null && request.DiscountAmountMinorUnits is not null)
        {
            return BillingOperationResult<OrganizationSubscriptionDto>.BadRequest(
                "Set either DiscountPercent or DiscountAmountMinorUnits, not both.");
        }

        if (request.DiscountPercent is not null and (< 1 or > 100))
        {
            return BillingOperationResult<OrganizationSubscriptionDto>.BadRequest(
                "DiscountPercent must be between 1 and 100.");
        }

        if (request.DiscountAmountMinorUnits is not null and < 1)
        {
            return BillingOperationResult<OrganizationSubscriptionDto>.BadRequest(
                "DiscountAmountMinorUnits must be positive.");
        }

        if (request.ClearDiscount == true
            && (request.DiscountPercent is not null || request.DiscountAmountMinorUnits is not null))
        {
            return BillingOperationResult<OrganizationSubscriptionDto>.BadRequest(
                "ClearDiscount and discount values must not be set together.");
        }
```

и в теле применения — присваивание полей, а при `ClearDiscount == true` обнуление всех четырёх. Блок смены плана **не** трогает поля скидки.

В `EfInvoiceGenerationRunner.GenerateForSubscriptionAsync` заменить расчёт суммы:

```csharp
        var gross = subscription.AmountMinorUnits;
        var discountApplies = subscription.DiscountUntilUtc is null || subscription.DiscountUntilUtc > now;
        var discount = discountApplies
            ? SubscriptionDiscount.Apply(gross, subscription.DiscountPercent, subscription.DiscountAmountMinorUnits)
            : 0;
```

и заполнить у счёта `GrossAmountMinorUnits = gross`, `DiscountMinorUnits = discount`, `AmountMinorUnits = gross - discount`. Скидка без даты окончания действует бессрочно — это осознанно: «до отмены» такой же нормальный договор, как «на три месяца».

Существующие места, где создаётся счёт (`EfOrganizationSubscriptionService` — пропорциональный пересчёт), заполняют `GrossAmountMinorUnits` равным `AmountMinorUnits` и `DiscountMinorUnits = 0`.

- [ ] **Step 8: Убедиться, что тесты проходят**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~Billing`
Expected: PASS

- [ ] **Step 9: Коммит**

```bash
git add -A
git commit -m "feat(platform): скидка на срок и разложение суммы счёта"
```

---

### Task 7: Разовые счета и кредит-ноты

**Files:**
- Modify: `src/AFK4.Shared.Contracts/Platform/Billing/InvoiceKindNames.cs`
- Create: `src/AFK4.Shared.Contracts/Platform/Billing/CreateInvoiceRequest.cs`
- Modify: `src/AFK4.Platform.Api/Platform/Billing/IInvoiceService.cs`, `EfInvoiceService.cs`
- Modify: `src/AFK4.Platform.Api/Endpoints/PlatformBillingEndpoints.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Billing/EfInvoiceServiceTests.cs`, `PlatformInvoiceEndpointTests.cs`

**Interfaces:**
- Consumes: `BillingBalance.Compute` (задача 3), поля счёта из задачи 6.
- Produces:
  - `InvoiceKindNames.OneOff = "one_off"`, `InvoiceKindNames.Credit = "credit"`;
  - `record CreateInvoiceRequest(string Kind, long AmountMinorUnits, string Description, DateTimeOffset? DueAtUtc)`;
  - `Task<BillingOperationResult<InvoiceDto>> IInvoiceService.CreateAsync(Guid organizationId, CreateInvoiceRequest request, CancellationToken)`;
  - `POST /api/platform/organizations/{organizationId:guid}/invoices` под правом `ManageInvoices`.

- [ ] **Step 1: Написать падающие тесты сервиса**

Дописать в `EfInvoiceServiceTests` (`Now`, `NewContext`, `NewService`, `SeedOrganizationWithSubscriptionAsync`, `AddOverdueInvoiceAsync` — хелперы файла, последний добавлен задачей 5):

```csharp
    [Fact]
    public async Task CreateAsync_OneOff_IssuesPositiveInvoiceWithNextNumber()
    {
        await using var db = NewContext();
        var orgId = await SeedOrganizationWithSubscriptionAsync(db);
        var service = NewService(db, new FixedTimeProvider(Now));

        var result = await service.CreateAsync(orgId, new CreateInvoiceRequest(
            Kind: InvoiceKindNames.OneOff,
            AmountMinorUnits: 150000,
            Description: "Настройка оборудования",
            DueAtUtc: null), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(InvoiceKindNames.OneOff, result.Value!.Kind);
        Assert.Equal(InvoiceStatusNames.Issued, result.Value.Status);
        Assert.Equal(1, result.Value.Number);
        var invoice = await db.Invoices.SingleAsync();
        Assert.Equal(150000, invoice.GrossAmountMinorUnits);
        Assert.Equal(0, invoice.DiscountMinorUnits);
        Assert.Equal(Now.AddDays(7), invoice.DueAtUtc);
    }

    [Fact]
    public async Task CreateAsync_Credit_AllowsNegativeAmountAndClearsArrears()
    {
        await using var db = NewContext();
        var orgId = await SeedOrganizationWithSubscriptionAsync(db);
        await AddOverdueInvoiceAsync(db, orgId, number: 1);
        var service = NewService(db, new FixedTimeProvider(Now));

        var result = await service.CreateAsync(orgId, new CreateInvoiceRequest(
            Kind: InvoiceKindNames.Credit,
            AmountMinorUnits: -290000,
            Description: "Компенсация простоя",
            DueAtUtc: null), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(-290000, result.Value!.AmountMinorUnits);
        Assert.Equal(SubscriptionStatusNames.Active, (await db.OrganizationSubscriptions.SingleAsync()).Status);
    }

    [Theory]
    [InlineData(InvoiceKindNames.OneOff, -1)]
    [InlineData(InvoiceKindNames.OneOff, 0)]
    [InlineData(InvoiceKindNames.Credit, 1)]
    [InlineData(InvoiceKindNames.Credit, 0)]
    public async Task CreateAsync_AmountSignDoesNotMatchKind_IsRejected(string kind, long amount)
    {
        await using var db = NewContext();
        var orgId = await SeedOrganizationWithSubscriptionAsync(db);
        var service = NewService(db, new FixedTimeProvider(Now));

        var result = await service.CreateAsync(orgId, new CreateInvoiceRequest(
            Kind: kind, AmountMinorUnits: amount, Description: "d", DueAtUtc: null), CancellationToken.None);

        Assert.Equal(BillingOperationStatus.BadRequest, result.Status);
        Assert.Equal(0, await db.Invoices.CountAsync());
    }

    [Theory]
    [InlineData(InvoiceKindNames.Subscription)]
    [InlineData(InvoiceKindNames.Proration)]
    public async Task CreateAsync_AutomaticKind_IsRejected(string kind)
    {
        await using var db = NewContext();
        var orgId = await SeedOrganizationWithSubscriptionAsync(db);
        var service = NewService(db, new FixedTimeProvider(Now));

        var result = await service.CreateAsync(orgId, new CreateInvoiceRequest(
            Kind: kind, AmountMinorUnits: 150000, Description: "d", DueAtUtc: null), CancellationToken.None);

        Assert.Equal(BillingOperationStatus.BadRequest, result.Status);
    }

    [Fact]
    public async Task CreateAsync_BlankDescription_IsRejected()
    {
        await using var db = NewContext();
        var orgId = await SeedOrganizationWithSubscriptionAsync(db);
        var service = NewService(db, new FixedTimeProvider(Now));

        var result = await service.CreateAsync(orgId, new CreateInvoiceRequest(
            Kind: InvoiceKindNames.OneOff, AmountMinorUnits: 150000, Description: "   ", DueAtUtc: null),
            CancellationToken.None);

        Assert.Equal(BillingOperationStatus.BadRequest, result.Status);
    }

    [Fact]
    public async Task CreateAsync_UnknownOrganization_ReturnsNotFound()
    {
        await using var db = NewContext();
        var service = NewService(db, new FixedTimeProvider(Now));

        var result = await service.CreateAsync(Guid.NewGuid(), new CreateInvoiceRequest(
            Kind: InvoiceKindNames.OneOff, AmountMinorUnits: 150000, Description: "d", DueAtUtc: null),
            CancellationToken.None);

        Assert.Equal(BillingOperationStatus.NotFound, result.Status);
    }
```

- [ ] **Step 2: Убедиться, что тесты падают**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~EfInvoiceServiceTests`
Expected: FAIL — метода `CreateAsync` не существует.

- [ ] **Step 3: Расширить виды счетов и контракт**

```csharp
namespace AFK4.Shared.Contracts.Platform.Billing;

public static class InvoiceKindNames
{
    public const string Subscription = "subscription";

    public const string Proration = "proration";

    /// <summary>Manually issued charge outside the subscription: setup, hardware, extra service.</summary>
    public const string OneOff = "one_off";

    /// <summary>Money owed back to the club. Carries a negative amount so the balance is arithmetic.</summary>
    public const string Credit = "credit";
}
```

```csharp
namespace AFK4.Shared.Contracts.Platform.Billing;

public sealed record CreateInvoiceRequest(
    string Kind,
    long AmountMinorUnits,
    string Description,
    DateTimeOffset? DueAtUtc);
```

- [ ] **Step 4: Реализовать создание**

В `EfInvoiceService`:

```csharp
    private const int MaxDescriptionLength = 400;

    public async Task<BillingOperationResult<InvoiceDto>> CreateAsync(
        Guid organizationId,
        CreateInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        var kind = request.Kind?.Trim() ?? string.Empty;
        if (kind is not (InvoiceKindNames.OneOff or InvoiceKindNames.Credit))
        {
            return BillingOperationResult<InvoiceDto>.BadRequest(
                "Kind must be 'one_off' or 'credit'; subscription and proration invoices are issued automatically.");
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return BillingOperationResult<InvoiceDto>.BadRequest("Description is required.");
        }

        if (request.Description.Trim().Length > MaxDescriptionLength)
        {
            return BillingOperationResult<InvoiceDto>.BadRequest(
                $"Description must be at most {MaxDescriptionLength} characters.");
        }

        // A negative amount is what makes a credit note subtract from the balance; allowing it
        // anywhere else would let a typo silently erase real debt.
        if (kind == InvoiceKindNames.Credit && request.AmountMinorUnits >= 0)
        {
            return BillingOperationResult<InvoiceDto>.BadRequest("A credit note must carry a negative amount.");
        }

        if (kind == InvoiceKindNames.OneOff && request.AmountMinorUnits <= 0)
        {
            return BillingOperationResult<InvoiceDto>.BadRequest("A one-off charge must carry a positive amount.");
        }

        var organization = await dbContext.Organizations
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId, cancellationToken);
        if (organization is null)
        {
            return BillingOperationResult<InvoiceDto>.NotFound("Organization was not found.");
        }

        var subscription = await dbContext.OrganizationSubscriptions
            .SingleOrDefaultAsync(candidate => candidate.OrganizationId == organizationId, cancellationToken);

        var now = timeProvider.GetUtcNow();
        var invoice = new InvoiceEntity
        {
            InvoiceId = Guid.NewGuid(),
            OrganizationId = organizationId,
            Number = ((await dbContext.Invoices.Select(candidate => (int?)candidate.Number)
                .MaxAsync(cancellationToken)) ?? 0) + 1,
            Kind = kind,
            PeriodStartUtc = now,
            PeriodEndUtc = now,
            IssuedAtUtc = now,
            DueAtUtc = request.DueAtUtc ?? now.Add(options.Value.InvoiceDueAfter),
            AmountMinorUnits = request.AmountMinorUnits,
            GrossAmountMinorUnits = request.AmountMinorUnits,
            DiscountMinorUnits = 0,
            CurrencyCode = subscription?.CurrencyCode ?? options.Value.DefaultCurrencyCode,
            Status = InvoiceStatusNames.Issued,
            Description = request.Description.Trim(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        dbContext.Invoices.Add(invoice);
        await dbContext.SaveChangesAsync(cancellationToken);

        // A credit note can clear the debt outright, so the club should stop being flagged at once
        // rather than at the next scheduler tick.
        if (kind == InvoiceKindNames.Credit)
        {
            await RestoreSubscriptionIfSettledAsync(organizationId, now, cancellationToken);
        }

        return BillingOperationResult<InvoiceDto>.Success(ToDto(invoice));
    }
```

Если `EfInvoiceService` не получает `IOptions<BillingOptions>` в конструкторе — добавить параметр и обновить регистрацию/тесты.

Объявить `CreateAsync` в `IInvoiceService` с XML-док-комментарием.

- [ ] **Step 5: Добавить эндпоинт**

В `PlatformBillingEndpoints`, рядом с `POST .../invoices/generate`, добавить `POST /api/platform/organizations/{organizationId:guid}/invoices` — целиком по образцу `mark-paid`: право `PlatformAdminPermissionNames.ManageInvoices`, поддержка `Idempotency-Key` со scope `platform.invoices.create`, аудит `Succeeded`/`Denied`.

Действие аудита: добавить в `AuditActionNames` константу `CreateInvoice = "platform.invoice.create"` (по образцу соседних значений файла), детали — `new { invoice.Kind, invoice.Number, invoice.AmountMinorUnits }`.

- [ ] **Step 6: Написать тест эндпоинта**

Дописать в `PlatformInvoiceEndpointTests` по образцу соседних тестов: успешное создание кредит-ноты под `platform_admin`, `403` под ролью без права `ManageInvoices`, `400` на положительной сумме кредит-ноты, повтор с тем же `Idempotency-Key` возвращает тот же счёт и заголовок `Idempotency-Replayed`.

- [ ] **Step 7: Убедиться, что тесты проходят**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~Billing`
Expected: PASS

- [ ] **Step 8: Полный прогон и коммит**

Run: `dotnet test tests/AFK4.Platform.Api.Tests -v minimal`
Expected: PASS, `Skipped: 0` при поднятом Postgres.

```bash
git add -A
git commit -m "feat(platform): разовые счета и кредит-ноты"
```

---

## Проверка плана

- Спека §3 (состояния, отсрочка, баланс, MRR) — задачи 3 и 5.
- Спека §4 (лестница) — задача 4.
- Спека §5 (скидка, поля счёта, разовые и кредит-ноты, годовые планы) — задачи 6, 7 и 2.
- Спека §6 (валюта, форматирование, рантбук) — задачи 1 и 2.
- Спека §7–§8 (панель, полоса) — вне этого плана, во втором.
- Спека §9 (тесты) — покрыто по задачам; конкурентные Postgres-прогоны идут через существующий CI-джоб `test-postgres`, отдельной задачи не требуют.
