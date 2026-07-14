# Operator «Клиенты» — S1b (серверный журнал ledger: keyset-эндпоинт + фильтр + «Показать ещё») Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Заменить источник таба «История» в карточке клиента со снимка `walletSummary.recentEntries` (последние 25 записей, без фильтра/пагинации) на настоящий серверный журнал: новый keyset-эндпоинт `GET /api/players/{id}/ledger` с серверным фильтром по типу операции, опциональным `accountType`, кнопкой «Показать ещё» (курсорная пагинация) и фильтр-чипами. Кошелёк по-прежнему берёт баланс/долг из `wallet-summary`. БЕЗ изменений в Кошелёк/Пакеты/поиске/действиях/сегментах.

**Architecture:** Slice S1b спеки `docs/superpowers/specs/2026-06-22-operator-clients-overhaul-design.md` (раздел «1. Богатая история + серверная пагинация (S1, S1b)», строки 84-94; «Честные ограничения», строки 159-164; «Слайсинг» строка S1b 155). S0+S1 уже в main: раздел переписан в master-detail, `HistorySection.tsx` — презентационный компонент поверх `recentEntries`. S1b:
- **Бэкенд:** новый чистый проектор `PlayerLedgerProjector.GetLedgerPageAsync(...)` зеркалит keyset-стратегию `PlayerHistoryProjector` (`src/AFK4.Platform.Api/Players/PlayerHistoryProjector.cs`): порядок `(CreatedAtUtc DESC, LedgerEntryId DESC)`, курсор через готовый `CursorToken` (base64 `<unixMillis>:<guidN>`), windowSize-удвоение при наличии курсора, in-memory tie-break (EF Core InMemory не транслирует `Guid.CompareTo`), hasMore-срез. Возвращает готовый `CursorPage<LedgerEntryDto>` (новый response-тип НЕ заводим). Проекция — через существующий `LedgerBalanceProjector.ToDto`. Валидация фильтров (`entryType`/`accountType` против `*Names`, `limit` clamp) вынесена в чистый статический хелпер `PlayerLedgerFilter`, юнит-тестируемый без интеграционного харнесса. Эндпоинт `GET /api/players/{playerAccountId:guid}/ledger` в `PlayerManagementEndpoints.cs` зеркалит `wallet-summary` (тот же `LoadPlayerScopedEndpointAsync(..., StaffPermissionNames.ViewBilling, ...)` → org-IDOR-guard 401/403/404), читает query, валидирует (400 на плохой фильтр), зовёт проектор, `Results.Ok(page)`.
- **Фронтенд:** клиент `players.getLedger(...)` (метод в `createPlayerClient`, `api/clients/players.ts`) + TS-зеркало `CursorPageDto<T>`; новые i18n-ключи `op.players.history.filterAll`/`loadMore` (метки типов в чипах — ПЕРЕИСПОЛЬЗУЮТ существующий `ledgerTypeLabel` → `ledger.type.*`, не дублируем); `HistorySection.tsx` обрастает фильтр-чипами + «Показать ещё» + отложенным скелетоном первой загрузки, оставаясь презентационным (управляется оркестратором); оркестратор `BackendPlayersWorkspace.tsx` получает новое ledger-состояние и грузит журнал при активном табе «История»; dev-mock `devMockBackend.ts` отдаёт длинный синтетический журнал (40-60 записей) с keyset-пагинацией+фильтром по роуту `/ledger`.

**Tech Stack:**
- Бэкенд: C# / .NET 10, ASP.NET Core minimal API, EF Core (InMemory в тестах), xUnit (`tests/AFK4.Platform.Api.Tests`).
- Контракты: `AFK4.Shared.Contracts` (`CursorPage<T>`, `LedgerEntryDto`, `LedgerEntryTypeNames`, `LedgerAccountTypeNames`).
- Фронтенд: React + TypeScript (Vite), тесты `bun test` (happy-dom + jest-dom, НЕ vitest), i18n `@afk4/i18n` (типизированные `MessageKey`), деньги `@afk4/money` (minor units), `lucide-react` иконки.

**Behavior-preservation contract (НЕ ломать):** меняется ТОЛЬКО источник таба «История» (`recentEntries` → `getLedger`). Должно по-прежнему работать без изменений:
- Кошелёк-таб: баланс/долг из `wallet-summary`, формы Пополнить/Погасить долг (`topUp`/`payDebt`);
- Пакеты-таб: список пакетов + покупка (`buyPackage`);
- список (поиск-дебаунс 180 мс, сегменты `all/vip/debt/inactive`, skeleton/empty);
- действия `topUp`/`writeOffDebt`/`buyPackage`/`booking`/`newCard` и их гейтинг по правам;
- глобальные `StateFlag` в шапке, `NewClientModal`, fixture-режим при `backend===null`.

## Global Constraints

- **Фронт Bun:** все команды через `/home/fedya/.bun/bin/bun`. Тесты — `bun test` (happy-dom + jest-dom, НЕ vitest). Тайпчек/сборка — `bun run build` (= `tsc` + `vite`); сами тесты НЕ тайпчекают, тайп-ошибки ловит только `bun run build`.
- **Рабочая директория фронта:** `/home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web`.
- **`App.test.tsx` — ОТДЕЛЬНЫМ прогоном** `bun test src/App.test.tsx` (утечка `mock.module` process-wide; общий `bun test` его флакает).
- **Бэкенд dotnet:** все команды через `/home/fedya/.dotnet/dotnet` (10.0.300). Тест — `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj`. Сборка — `dotnet build`.
- **Рабочая директория решения:** `/home/fedya/projects/afk4.net`.
- **НИКАКИХ date-бомб в бэкенд-тестах:** сиды только на СТАТИЧЕСКОЙ `DateTimeOffset` (как `LedgerBalanceProjectorTests.cs:12` — `DateTimeOffset.Parse("2026-05-13T10:00:00Z")`). НЕ `DateTimeOffset.UtcNow`, не хардкод будущих/прошлых дат, что протухают. Это известная боль проекта (память `afk4-time-handling-audit`; рабочий стиль #39).
- **Переиспользовать готовую инфру (#29 — не дублировать):** `CursorPage<T>` (`src/AFK4.Shared.Contracts/Common/CursorPage.cs`) как response-тип; `CursorToken.Encode/TryDecode` (`src/AFK4.Platform.Api/Common/CursorToken.cs`) для курсора; keyset-паттерн `PlayerHistoryProjector` (`src/AFK4.Platform.Api/Players/PlayerHistoryProjector.cs`) — зеркалить, НЕ изобретать заново; `LedgerBalanceProjector.ToDto` для проекции; `LoadPlayerScopedEndpointAsync` для org-guard; `ledgerTypeLabel` (`src/AFK4.Operator.App.Web/src/players/playersModel.ts`) для меток типов на фронте.
- **keyset по `DateTimeOffset`** через unix-millis (`CursorToken`), порядок `(CreatedAtUtc DESC, LedgerEntryId DESC)`; `limit` дефолт 50, clamp [1, 100]; фильтр `entryType`/`accountType` валидируется против `*Names` (невалид → HTTP 400).
- **Ветка:** `feat/operator-clients-s1b`.
- **Стейджить ТОЛЬКО файлы своего таска явным `git add <path>`** — НЕ `git commit -a/-am` (в репо `.claude/memory/` под гитом, sweeping ловит лишнее).
- **Никаких AI-подписей** нигде (ни в коммитах, ни в коде, ни в комментариях).
- **Деньги** в minor units; форматирование на границе UI существующими `formatMinorUnits` — своих форматтеров не плодить.
- **i18n:** новые ключи добавляются в `locales/{ru,en,tg}.json` (в КОРНЕ репо), затем `bun run gen` в `packages/i18n` регенерит `messages.ts`. tg — РЕАЛЬНЫЙ таджикский, не копия ru (guard `messages.test.ts` против `tg===ru`; новые ключи НЕ добавлять в whitelist).
- **Акцент оператора синий `#1f6feb`** (через токены `var(--accent)`); тёмная тема по умолчанию.

---

### Task 1: Бэкенд — `PlayerLedgerProjector.GetLedgerPageAsync` + `PlayerLedgerFilter` (валидация) + unit-тесты

Новый проектор и чистый хелпер валидации фильтров. Размещение: **новый файл** `src/AFK4.Platform.Api/Players/PlayerLedgerProjector.cs` (а НЕ в `LedgerBalanceProjector`). Обоснование когезии: `LedgerBalanceProjector` отвечает за СВОДКУ (баланс/долг + 25 последних записей для wallet-summary) — это аггрегаты. Постраничный журнал с курсором/фильтром — другая ответственность (история, не сводка), и keyset-логика естественно ложится рядом с `PlayerHistoryProjector` в неймспейсе `AFK4.Platform.Api.Players` (зеркало visits/purchases). Проекцию делаем через переиспользуемый `LedgerBalanceProjector.ToDto` (он `public static`), так что дублирования маппинга нет.

**Files:**
- Create: `src/AFK4.Platform.Api/Players/PlayerLedgerProjector.cs`
- Create: `tests/AFK4.Platform.Api.Tests/PlayerLedgerProjectorTests.cs`

**Interfaces:**
- Consumes: `PlatformDbContext` (`src/AFK4.Platform.Api/Data`), `LedgerEntryEntity` (PK `LedgerEntryId: Guid`, `CreatedAtUtc: DateTimeOffset`, `AmountMinorUnits: long`, `CurrencyCode`, `EntryType`/`AccountType: string`, `PlayerAccountId`); `CursorPage<T>` (`AFK4.Shared.Contracts.Common`); `LedgerEntryDto` (`AFK4.Shared.Contracts.Billing`); `CursorToken` (`AFK4.Platform.Api.Common`); `LedgerBalanceProjector.ToDto`; `LedgerEntryTypeNames`/`LedgerAccountTypeNames` (`AFK4.Shared.Contracts.Billing`).
- Produces:
  - `static class PlayerLedgerFilter`:
    - `const int DefaultLimit = 50;`, `const int MaxLimit = 100;`, `const int MinLimit = 1;`
    - `static int ClampLimit(int? limit) -> int` (null → 50; иначе clamp [1, 100])
    - `static bool IsValidEntryType(string? entryType) -> bool` (null/пусто → true «нет фильтра»; иначе ∈ известных типов)
    - `static bool IsValidAccountType(string? accountType) -> bool` (аналогично)
  - `static class PlayerLedgerProjector`:
    - `static Task<CursorPage<LedgerEntryDto>> GetLedgerPageAsync(PlatformDbContext dbContext, Guid playerAccountId, string? entryType, string? accountType, string? before, int limit, CancellationToken cancellationToken)`

- [ ] **Step 1: Написать падающий тест** `tests/AFK4.Platform.Api.Tests/PlayerLedgerProjectorTests.cs`

```csharp
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Players;
using AFK4.Shared.Contracts.Billing;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

public sealed class PlayerLedgerProjectorTests
{
    private static readonly Guid PlayerAccountId = Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid OtherPlayerAccountId = Guid.Parse("dddddddd-dddd-4ddd-dddd-dddddddddddd");
    private static readonly Guid StaffUserId = Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134");
    // Статическая база времени — никаких date-бомб (Global Constraints).
    private static readonly DateTimeOffset Base = DateTimeOffset.Parse("2026-05-13T10:00:00Z");

    [Fact]
    public async Task GetLedgerPageAsync_ReturnsNewestFirst_AndCapsAtLimitWithNextCursor()
    {
        await using var db = CreateDbContext();
        SeedPlayer(db, PlayerAccountId);
        // 5 записей, по минуте друг от друга; newest = index 4.
        for (var i = 0; i < 5; i++)
        {
            db.LedgerEntries.Add(CreateEntry(
                LedgerEntryTypeNames.TopUp,
                LedgerAccountTypeNames.Wallet,
                (i + 1) * 1000,
                createdAtUtc: Base.AddMinutes(i)));
        }
        await db.SaveChangesAsync();

        var page = await PlayerLedgerProjector.GetLedgerPageAsync(
            db, PlayerAccountId, entryType: null, accountType: null, before: null, limit: 2, CancellationToken.None);

        Assert.Equal(2, page.Items.Count);
        // newest first: amount 5000 (index 4), затем 4000 (index 3).
        Assert.Equal(5000, page.Items[0].Amount.MinorUnits);
        Assert.Equal(4000, page.Items[1].Amount.MinorUnits);
        Assert.NotNull(page.NextCursor); // есть ещё страницы
    }

    [Fact]
    public async Task GetLedgerPageAsync_SecondPageByCursor_DoesNotOverlapFirst_AndExhaustsCleanly()
    {
        await using var db = CreateDbContext();
        SeedPlayer(db, PlayerAccountId);
        for (var i = 0; i < 5; i++)
        {
            db.LedgerEntries.Add(CreateEntry(
                LedgerEntryTypeNames.TopUp,
                LedgerAccountTypeNames.Wallet,
                (i + 1) * 1000,
                createdAtUtc: Base.AddMinutes(i)));
        }
        await db.SaveChangesAsync();

        var first = await PlayerLedgerProjector.GetLedgerPageAsync(
            db, PlayerAccountId, null, null, before: null, limit: 2, CancellationToken.None);
        var second = await PlayerLedgerProjector.GetLedgerPageAsync(
            db, PlayerAccountId, null, null, before: first.NextCursor, limit: 2, CancellationToken.None);

        // вторая страница: amount 3000 (index 2), 2000 (index 1) — не пересекается с [5000, 4000].
        Assert.Equal(2, second.Items.Count);
        Assert.Equal(3000, second.Items[0].Amount.MinorUnits);
        Assert.Equal(2000, second.Items[1].Amount.MinorUnits);
        Assert.NotNull(second.NextCursor);

        var firstIds = first.Items.Select(e => e.LedgerEntryId).ToHashSet();
        Assert.DoesNotContain(second.Items[0].LedgerEntryId, firstIds);
        Assert.DoesNotContain(second.Items[1].LedgerEntryId, firstIds);

        var third = await PlayerLedgerProjector.GetLedgerPageAsync(
            db, PlayerAccountId, null, null, before: second.NextCursor, limit: 2, CancellationToken.None);
        // осталась одна запись (amount 1000, index 0) — последняя страница, курсора больше нет.
        Assert.Single(third.Items);
        Assert.Equal(1000, third.Items[0].Amount.MinorUnits);
        Assert.Null(third.NextCursor);
    }

    [Fact]
    public async Task GetLedgerPageAsync_FiltersByEntryType()
    {
        await using var db = CreateDbContext();
        SeedPlayer(db, PlayerAccountId);
        db.LedgerEntries.Add(CreateEntry(LedgerEntryTypeNames.TopUp, LedgerAccountTypeNames.Wallet, 5000, createdAtUtc: Base.AddMinutes(1)));
        db.LedgerEntries.Add(CreateEntry(LedgerEntryTypeNames.GameplayCharge, LedgerAccountTypeNames.Wallet, -1200, createdAtUtc: Base.AddMinutes(2)));
        db.LedgerEntries.Add(CreateEntry(LedgerEntryTypeNames.TopUp, LedgerAccountTypeNames.Wallet, 3000, createdAtUtc: Base.AddMinutes(3)));
        await db.SaveChangesAsync();

        var page = await PlayerLedgerProjector.GetLedgerPageAsync(
            db, PlayerAccountId, entryType: LedgerEntryTypeNames.TopUp, accountType: null, before: null, limit: 50, CancellationToken.None);

        Assert.Equal(2, page.Items.Count);
        Assert.All(page.Items, e => Assert.Equal(LedgerEntryTypeNames.TopUp, e.EntryType));
    }

    [Fact]
    public async Task GetLedgerPageAsync_FiltersByAccountType()
    {
        await using var db = CreateDbContext();
        SeedPlayer(db, PlayerAccountId);
        db.LedgerEntries.Add(CreateEntry(LedgerEntryTypeNames.TopUp, LedgerAccountTypeNames.Wallet, 5000, createdAtUtc: Base.AddMinutes(1)));
        db.LedgerEntries.Add(CreateEntry(LedgerEntryTypeNames.PostpaidDebt, LedgerAccountTypeNames.Debt, 700, createdAtUtc: Base.AddMinutes(2)));
        await db.SaveChangesAsync();

        var page = await PlayerLedgerProjector.GetLedgerPageAsync(
            db, PlayerAccountId, entryType: null, accountType: LedgerAccountTypeNames.Debt, before: null, limit: 50, CancellationToken.None);

        Assert.Single(page.Items);
        Assert.Equal(LedgerAccountTypeNames.Debt, page.Items[0].AccountType);
    }

    [Fact]
    public async Task GetLedgerPageAsync_ScopesToSinglePlayer()
    {
        await using var db = CreateDbContext();
        SeedPlayer(db, PlayerAccountId);
        SeedPlayer(db, OtherPlayerAccountId);
        db.LedgerEntries.Add(CreateEntry(LedgerEntryTypeNames.TopUp, LedgerAccountTypeNames.Wallet, 5000, createdAtUtc: Base.AddMinutes(1)));
        db.LedgerEntries.Add(CreateEntry(LedgerEntryTypeNames.TopUp, LedgerAccountTypeNames.Wallet, 9000, playerAccountId: OtherPlayerAccountId, createdAtUtc: Base.AddMinutes(2)));
        await db.SaveChangesAsync();

        var page = await PlayerLedgerProjector.GetLedgerPageAsync(
            db, PlayerAccountId, null, null, before: null, limit: 50, CancellationToken.None);

        Assert.Single(page.Items);
        Assert.Equal(5000, page.Items[0].Amount.MinorUnits);
    }

    [Fact]
    public async Task GetLedgerPageAsync_BadCursor_FallsBackToFirstPage()
    {
        await using var db = CreateDbContext();
        SeedPlayer(db, PlayerAccountId);
        db.LedgerEntries.Add(CreateEntry(LedgerEntryTypeNames.TopUp, LedgerAccountTypeNames.Wallet, 5000, createdAtUtc: Base.AddMinutes(1)));
        await db.SaveChangesAsync();

        var page = await PlayerLedgerProjector.GetLedgerPageAsync(
            db, PlayerAccountId, null, null, before: "not-a-valid-cursor", limit: 50, CancellationToken.None);

        Assert.Single(page.Items); // битый курсор → первая страница, не падаем
    }

    [Theory]
    [InlineData(null, 50)]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(50, 50)]
    [InlineData(100, 100)]
    [InlineData(101, 100)]
    [InlineData(1000, 100)]
    public void ClampLimit_BoundsToOneHundred(int? input, int expected)
    {
        Assert.Equal(expected, PlayerLedgerFilter.ClampLimit(input));
    }

    [Fact]
    public void IsValidEntryType_AcceptsKnownAndNull_RejectsUnknown()
    {
        Assert.True(PlayerLedgerFilter.IsValidEntryType(null));
        Assert.True(PlayerLedgerFilter.IsValidEntryType(""));
        Assert.True(PlayerLedgerFilter.IsValidEntryType(LedgerEntryTypeNames.TopUp));
        Assert.True(PlayerLedgerFilter.IsValidEntryType(LedgerEntryTypeNames.Refund));
        Assert.False(PlayerLedgerFilter.IsValidEntryType("mystery_type"));
    }

    [Fact]
    public void IsValidAccountType_AcceptsKnownAndNull_RejectsUnknown()
    {
        Assert.True(PlayerLedgerFilter.IsValidAccountType(null));
        Assert.True(PlayerLedgerFilter.IsValidAccountType(""));
        Assert.True(PlayerLedgerFilter.IsValidAccountType(LedgerAccountTypeNames.Wallet));
        Assert.True(PlayerLedgerFilter.IsValidAccountType(LedgerAccountTypeNames.Debt));
        Assert.False(PlayerLedgerFilter.IsValidAccountType("mystery_account"));
    }

    private static PlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new PlatformDbContext(options);
    }

    private static void SeedPlayer(PlatformDbContext db, Guid playerAccountId)
    {
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = playerAccountId,
            OrganizationId = TestIds.OrganizationId,
            HomeBranchId = TestIds.BranchId,
            DisplayName = "Player",
            PhoneNumber = null,
            IsActive = true,
            CreatedAtUtc = Base
        });
    }

    private static LedgerEntryEntity CreateEntry(
        string entryType,
        string accountType,
        long amountMinorUnits,
        Guid? playerAccountId = null,
        DateTimeOffset? createdAtUtc = null)
    {
        return new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            PlayerAccountId = playerAccountId ?? PlayerAccountId,
            SessionId = null,
            PlayerPackageId = null,
            EntryType = entryType,
            AccountType = accountType,
            AmountMinorUnits = amountMinorUnits,
            QuantitySeconds = 0,
            CurrencyCode = "TJS",
            Description = entryType,
            Reason = "test",
            ReversesLedgerEntryId = null,
            CreatedByStaffUserId = StaffUserId,
            CreatedAtUtc = createdAtUtc ?? Base
        };
    }
}
```

- [ ] **Step 2: Запустить тест — убедиться, что падает**

Run: `cd /home/fedya/projects/afk4.net && /home/fedya/.dotnet/dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~PlayerLedgerProjectorTests"`
Expected: FAIL компиляции — `PlayerLedgerProjector` и `PlayerLedgerFilter` ещё не существуют (`The type or namespace name 'PlayerLedgerProjector' could not be found`).

- [ ] **Step 3: Создать `src/AFK4.Platform.Api/Players/PlayerLedgerProjector.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Common;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Common;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Players;

// Валидация и нормализация query-параметров журнала ledger. Вынесена из эндпоинт-лямбды,
// чтобы юнит-тестировать без интеграционного харнеса (его в проекте нет — Global Constraints).
public static class PlayerLedgerFilter
{
    public const int DefaultLimit = 50;
    public const int MinLimit = 1;
    public const int MaxLimit = 100;

    private static readonly HashSet<string> KnownEntryTypes = new(StringComparer.Ordinal)
    {
        LedgerEntryTypeNames.TopUp,
        LedgerEntryTypeNames.GameplayCharge,
        LedgerEntryTypeNames.PackagePurchase,
        LedgerEntryTypeNames.PackageConsumption,
        LedgerEntryTypeNames.BonusGrant,
        LedgerEntryTypeNames.BonusConsumption,
        LedgerEntryTypeNames.Refund,
        LedgerEntryTypeNames.ManualCorrection,
        LedgerEntryTypeNames.PostpaidDebt,
        LedgerEntryTypeNames.DebtPayment,
        LedgerEntryTypeNames.WalletPayment,
        LedgerEntryTypeNames.Reversal,
        LedgerEntryTypeNames.Cashback
    };

    private static readonly HashSet<string> KnownAccountTypes = new(StringComparer.Ordinal)
    {
        LedgerAccountTypeNames.Wallet,
        LedgerAccountTypeNames.Debt,
        LedgerAccountTypeNames.PackageTime,
        LedgerAccountTypeNames.BonusTime
    };

    // null дефолтит к 50; иначе зажимаем в [1, 100].
    public static int ClampLimit(int? limit)
    {
        if (limit is null)
        {
            return DefaultLimit;
        }

        return Math.Clamp(limit.Value, MinLimit, MaxLimit);
    }

    // Пустой/null фильтр = «нет фильтра» (валиден). Непустой — должен быть из известных значений.
    public static bool IsValidEntryType(string? entryType) =>
        string.IsNullOrEmpty(entryType) || KnownEntryTypes.Contains(entryType);

    public static bool IsValidAccountType(string? accountType) =>
        string.IsNullOrEmpty(accountType) || KnownAccountTypes.Contains(accountType);
}

// Постраничный журнал ledger игрока (keyset, не offset). Зеркалит стратегию PlayerHistoryProjector:
// курсор кодирует (CreatedAtUtc DESC, LedgerEntryId DESC); WHERE-фильтр по timestamp в SQL/InMemory,
// затем точный tie-break (CreatedAtUtc, LedgerEntryId) в памяти (EF Core InMemory не транслирует
// Guid.CompareTo внутри LINQ Where). Проекция — через переиспользуемый LedgerBalanceProjector.ToDto.
public static class PlayerLedgerProjector
{
    public static async Task<CursorPage<LedgerEntryDto>> GetLedgerPageAsync(
        PlatformDbContext dbContext,
        Guid playerAccountId,
        string? entryType,
        string? accountType,
        string? before,
        int limit,
        CancellationToken cancellationToken)
    {
        var pageSize = PlayerLedgerFilter.ClampLimit(limit);

        var query = dbContext.LedgerEntries
            .AsNoTracking()
            .Where(entry => entry.PlayerAccountId == playerAccountId);

        if (!string.IsNullOrEmpty(entryType))
        {
            query = query.Where(entry => entry.EntryType == entryType);
        }

        if (!string.IsNullOrEmpty(accountType))
        {
            query = query.Where(entry => entry.AccountType == accountType);
        }

        // Битый/пустой курсор → false → первая страница (CursorToken.TryDecode не бросает).
        bool hasCursor = CursorToken.TryDecode(before, out var afterTs, out var afterId);

        if (hasCursor)
        {
            query = query.Where(entry => entry.CreatedAtUtc <= afterTs);
        }

        // Удвоенное окно при курсоре: даёт запас кандидатов на in-memory tie-break + pageSize+1 для hasMore.
        var windowSize = hasCursor ? (pageSize + 1) * 2 : pageSize + 1;
        var candidates = await query
            .OrderByDescending(entry => entry.CreatedAtUtc)
            .ThenByDescending(entry => entry.LedgerEntryId)
            .Take(windowSize)
            .ToListAsync(cancellationToken);

        List<LedgerEntryEntity> entries;
        if (hasCursor)
        {
            entries = candidates
                .Where(entry =>
                    entry.CreatedAtUtc < afterTs ||
                    (entry.CreatedAtUtc == afterTs && entry.LedgerEntryId.CompareTo(afterId) < 0))
                .Take(pageSize + 1)
                .ToList();
        }
        else
        {
            entries = candidates.Take(pageSize + 1).ToList();
        }

        var hasMore = entries.Count > pageSize;
        if (hasMore)
        {
            entries.RemoveAt(entries.Count - 1);
        }

        var items = entries.Select(LedgerBalanceProjector.ToDto).ToList();

        string? nextCursor = hasMore && entries.Count > 0
            ? CursorToken.Encode(entries[^1].CreatedAtUtc, entries[^1].LedgerEntryId)
            : null;

        return new CursorPage<LedgerEntryDto>(items, nextCursor);
    }
}
```

- [ ] **Step 4: Запустить тест — убедиться PASS**

Run: `cd /home/fedya/projects/afk4.net && /home/fedya/.dotnet/dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~PlayerLedgerProjectorTests"`
Expected: PASS — все факты/теории зелёные (keyset, фильтры, скоупинг, битый курсор, clamp, валидация).

- [ ] **Step 5: Коммит**

```bash
cd /home/fedya/projects/afk4.net
git add src/AFK4.Platform.Api/Players/PlayerLedgerProjector.cs tests/AFK4.Platform.Api.Tests/PlayerLedgerProjectorTests.cs
git commit -m "feat(platform-api): постраничный журнал ledger игрока (keyset) + валидация фильтров"
```

---

### Task 2: Бэкенд — эндпоинт `GET /api/players/{playerAccountId:guid}/ledger`

Добавить эндпоинт в `PlayerManagementEndpoints.cs`, зеркаля `wallet-summary` (строки 233-262): тот же `LoadPlayerScopedEndpointAsync(..., StaffPermissionNames.ViewBilling, ...)` (org-IDOR-guard 401/403/404), читаем query `entryType`/`accountType`/`before`/`limit`, валидируем фильтры (невалидный тип → 400), зовём проектор, `Results.Ok(page)`. Интеграционного харнеса player-эндпоинтов в проекте нет — org-guard покрыт на уровне переиспользуемого helper'а; новую логику (keyset/фильтр/валидация) уже юнит-покрыли в Task 1. Приёмка этого таска — компиляция (`dotnet build`).

**Files:**
- Modify: `src/AFK4.Platform.Api/Endpoints/PlayerManagementEndpoints.cs` (добавить `MapGet` после блока `wallet-summary`, перед `wallet/top-ups`)

**Interfaces:**
- Consumes: `LoadPlayerScopedEndpointAsync` (`EndpointHelpers.Loaders.cs`, через `using static AFK4.Platform.Api.Endpoints.EndpointHelpers`); `StaffPermissionNames.ViewBilling` (= `"billing.view"`); `PlayerLedgerProjector.GetLedgerPageAsync` + `PlayerLedgerFilter` (Task 1, неймспейс `AFK4.Platform.Api.Players` уже импортирован строкой 31); `PlatformDbContext`, `IStaffContextAccessor`, `StaffAuthorizationService`.
- Produces: маршрут `GET /api/players/{playerAccountId:guid}/ledger?entryType=&accountType=&before=&limit=` → `200 CursorPage<LedgerEntryDto>` / `400` (невалидный фильтр) / `401`/`403`/`404` (через guard).

- [ ] **Step 1: Добавить эндпоинт в `PlayerManagementEndpoints.cs`**

Вставить новый `app.MapGet(...)` СРАЗУ ПОСЛЕ закрывающей `});` блока `wallet-summary` (после строки 262) и ПЕРЕД `app.MapPost("/api/players/{playerAccountId:guid}/wallet/top-ups"` (строка 264):

```csharp
        app.MapGet("/api/players/{playerAccountId:guid}/ledger", async (
            Guid playerAccountId,
            string? entryType,
            string? accountType,
            string? before,
            int? limit,
            PlatformDbContext dbContext,
            IStaffContextAccessor staffContextAccessor,
            StaffAuthorizationService authorizationService,
            CancellationToken cancellationToken) =>
        {
            if (!PlayerLedgerFilter.IsValidEntryType(entryType))
            {
                return Results.BadRequest(new { Error = $"Unknown entryType '{entryType}'." });
            }

            if (!PlayerLedgerFilter.IsValidAccountType(accountType))
            {
                return Results.BadRequest(new { Error = $"Unknown accountType '{accountType}'." });
            }

            var player = await LoadPlayerScopedEndpointAsync(
                dbContext,
                staffContextAccessor,
                authorizationService,
                playerAccountId,
                StaffPermissionNames.ViewBilling,
                cancellationToken);
            if (player.Result is not null)
            {
                return player.Result;
            }

            if (!player.Authorization!.IsAllowed)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var page = await PlayerLedgerProjector.GetLedgerPageAsync(
                dbContext,
                playerAccountId,
                entryType,
                accountType,
                before,
                PlayerLedgerFilter.ClampLimit(limit),
                cancellationToken);

            return Results.Ok(page);
        });
```

Примечание: валидацию фильтров делаем ДО загрузки игрока — bad request не зависит от существования игрока, дешевле и не палит существование при невалидном вводе. `before` (битый/устаревший курсор) НЕ 400 — проектор сам падает на первую страницу (зеркало `CursorToken` поведения).

- [ ] **Step 2: Сборка бэкенда**

Run: `cd /home/fedya/projects/afk4.net && /home/fedya/.dotnet/dotnet build src/AFK4.Platform.Api/AFK4.Platform.Api.csproj`
Expected: `Build succeeded` — 0 ошибок (все типы/хелперы доступны: `PlayerLedgerProjector`/`PlayerLedgerFilter` из Task 1, `LoadPlayerScopedEndpointAsync` через `using static`, `StaffPermissionNames.ViewBilling`).

- [ ] **Step 3: Полный прогон бэкенд-тестов (регресс)**

Run: `cd /home/fedya/projects/afk4.net && /home/fedya/.dotnet/dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj`
Expected: все тесты зелёные (новые `PlayerLedgerProjectorTests` + существующие; эндпоинт добавлен без поломки прочих).

- [ ] **Step 4: Коммит**

```bash
cd /home/fedya/projects/afk4.net
git add src/AFK4.Platform.Api/Endpoints/PlayerManagementEndpoints.cs
git commit -m "feat(platform-api): эндпоинт GET /api/players/{id}/ledger (keyset + фильтр + org-guard)"
```

---

### Task 3: Фронт — клиент `players.getLedger` + `CursorPageDto<T>` (+ тест)

Добавить в `createPlayerClient` (`api/clients/players.ts`) метод `getLedger`, строящий query (`entryType`/`accountType`/`before`=cursor/`limit`), и TS-зеркало `CursorPageDto<T>` контракта `CursorPage<T>`. Тип реэкспортится из `operatorApiClients.ts` (там уже `export * from './api/clients/players'`).

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/api/clients/players.ts`
- Modify: `src/AFK4.Operator.App.Web/src/api/clients/players.test.ts`

**Interfaces:**
- Consumes: `PlatformApiClient.get<T>(path, query?: QueryParams)` где `QueryParams = Record<string, string | number | boolean | Date | null | undefined>` (`platformApi.ts:38`); `LedgerEntryDto` (уже определён в `players.ts:25`); `Guid` (`../types`).
- Produces:
  - `interface CursorPageDto<T> { items: T[]; nextCursor: string | null }`
  - в объекте `createPlayerClient`: `getLedger(playerAccountId: Guid, params?: { entryType?: string; accountType?: string; cursor?: string; limit?: number }): Promise<CursorPageDto<LedgerEntryDto>>`

- [ ] **Step 1: Написать падающий тест** — добавить в `src/AFK4.Operator.App.Web/src/api/clients/players.test.ts`

Заменить хелпер `fakeApi` так, чтобы `get` записывал query, и добавить новый `it`-блок. Полностью новый файл:

```ts
import { describe, expect, it } from 'bun:test';
import { createPlayerClient } from './players';

function fakeApi() {
  const calls: Array<{ method: string; path: string; body?: unknown; query?: unknown }> = [];
  const api = {
    get: async <T,>(path: string, query?: unknown) => {
      calls.push({ method: 'GET', path, query });
      return { items: [], nextCursor: null } as unknown as T;
    },
    post: async <T,>(path: string, body: unknown) => {
      calls.push({ method: 'POST', path, body });
      return body as T;
    },
    patch: async <T,>() => ({} as T)
  };
  return { api, calls };
}

const branchId = 'acfc0212-967f-4d84-94be-9003387b09c2';
const playerId = '12121212-1212-1212-1212-121212121212';
const organizationId = '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08';

describe('createPlayerClient', () => {
  it('maps wallet top-up and debt-payment routes with typed bodies', async () => {
    const { api, calls } = fakeApi();
    const client = createPlayerClient(api as never);

    await client.getWalletSummary(playerId);
    await client.topUpWallet(playerId, {
      organizationId,
      amount: { currencyCode: 'TJS', minorUnits: 10000 },
      reason: 'Касса',
      idempotencyKey: 'idem-top'
    });
    await client.payDebt(playerId, {
      organizationId,
      amount: { currencyCode: 'TJS', minorUnits: 3500 },
      reason: 'Возврат долга',
      idempotencyKey: 'idem-debt'
    });

    expect(calls.map((c) => `${c.method} ${c.path}`)).toEqual([
      `GET /api/players/${playerId}/wallet-summary`,
      `POST /api/players/${playerId}/wallet/top-ups`,
      `POST /api/players/${playerId}/debts/payments`
    ]);
    expect(calls[1].body).toMatchObject({ reason: 'Касса', amount: { minorUnits: 10000 } });
  });

  it('builds the ledger route with entryType/accountType/before/limit query', async () => {
    const { api, calls } = fakeApi();
    const client = createPlayerClient(api as never);

    await client.getLedger(playerId, { entryType: 'top_up', cursor: 'cur-123', limit: 50 });

    expect(calls[0].method).toBe('GET');
    expect(calls[0].path).toBe(`/api/players/${playerId}/ledger`);
    // cursor → query-параметр `before`; пустые поля не отправляются.
    expect(calls[0].query).toEqual({ entryType: 'top_up', before: 'cur-123', limit: 50 });
  });

  it('omits empty filter params from the ledger query', async () => {
    const { api, calls } = fakeApi();
    const client = createPlayerClient(api as never);

    await client.getLedger(playerId);

    expect(calls[0].query).toEqual({});
  });

  it('returns the cursor page shape', async () => {
    const { api } = fakeApi();
    const client = createPlayerClient(api as never);

    const page = await client.getLedger(playerId, { entryType: 'refund' });

    expect(page).toEqual({ items: [], nextCursor: null });
  });
});
```

- [ ] **Step 2: Запустить тест — убедиться, что падает**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/api/clients/players.test.ts`
Expected: FAIL — `client.getLedger is not a function`.

- [ ] **Step 3: Реализовать `getLedger` + `CursorPageDto` в `players.ts`**

Добавить интерфейс `CursorPageDto<T>` после блока `WalletSummaryDto` (после строки 48):

```ts
// Зеркало AFK4.Shared.Contracts.Common.CursorPage<T> (camelCase): страница + курсор следующей
// страницы (null = больше нет).
export interface CursorPageDto<T> {
  items: T[];
  nextCursor: string | null;
}
```

Добавить метод `getLedger` в возвращаемый объект `createPlayerClient` (после `getWalletSummary`, перед `getPlayerPackages`):

```ts
    getLedger(
      playerAccountId: Guid,
      params: { entryType?: string; accountType?: string; cursor?: string; limit?: number } = {}
    ): Promise<CursorPageDto<LedgerEntryDto>> {
      const query: Record<string, string | number> = {};
      if (params.entryType) query.entryType = params.entryType;
      if (params.accountType) query.accountType = params.accountType;
      if (params.cursor) query.before = params.cursor; // курсор уходит на бэк как `before`
      if (params.limit !== undefined) query.limit = params.limit;
      return api.get<CursorPageDto<LedgerEntryDto>>(`/api/players/${playerAccountId}/ledger`, query);
    },
```

- [ ] **Step 4: Прогнать тест + тайпчек**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/api/clients/players.test.ts && /home/fedya/.bun/bin/bun run build`
Expected: тест PASS (4 блока зелёные); `bun run build` чисто.

- [ ] **Step 5: Коммит**

```bash
cd /home/fedya/projects/afk4.net
git add src/AFK4.Operator.App.Web/src/api/clients/players.ts src/AFK4.Operator.App.Web/src/api/clients/players.test.ts
git commit -m "feat(operator-clients): клиент getLedger + CursorPageDto (зеркало CursorPage)"
```

---

### Task 4: i18n — `op.players.history.filterAll` + `op.players.history.loadMore` (ru/en/tg + gen)

Добавить два ключа для UI журнала: «Все» (фильтр-чип «без фильтра») и «Показать ещё». Метки типов в чипах НЕ заводим — переиспользуем существующий `ledgerTypeLabel` → `ledger.type.*` (#29; S1 уже маппит 13 типов). tg — реальный таджикский.

**Files:**
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json`
- Modify: `packages/i18n/src/messages.ts` (через `bun run gen`, не руками)

**Interfaces:**
- Produces: `MessageKey`-значения `op.players.history.filterAll`, `op.players.history.loadMore`, доступные `t()`.

- [ ] **Step 1: Добавить ключи в `locales/ru.json`**

Рядом с существующими `op.players.history.*` (после `op.players.history.reversalBadge`, строка ~1477):

```jsonc
"op.players.history.filterAll": "Все",
"op.players.history.loadMore": "Показать ещё"
```

- [ ] **Step 2: Добавить те же ключи в `locales/en.json`**

```jsonc
"op.players.history.filterAll": "All",
"op.players.history.loadMore": "Show more"
```

- [ ] **Step 3: Добавить те же ключи в `locales/tg.json` (реальный таджикский)**

```jsonc
"op.players.history.filterAll": "Ҳама",
"op.players.history.loadMore": "Бештар нишон диҳед"
```

(Оба tg-значения отличны от ru — в whitelist `messages.test.ts` добавлять не нужно.)

- [ ] **Step 4: Регенерить `messages.ts`**

Run: `cd /home/fedya/projects/afk4.net/packages/i18n && /home/fedya/.bun/bin/bun run gen`
Expected: `generated …/messages.ts from 3 locales`.

- [ ] **Step 5: Прогнать i18n-гарды + тайпчек фронта**

Run: `cd /home/fedya/projects/afk4.net/packages/i18n && /home/fedya/.bun/bin/bun test && cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run build`
Expected: i18n-тесты зелёные (parity ru=en=tg; tg≠ru guard проходит — оба новых ключа отличны от ru); `bun run build` чисто (новые ключи в `MessageKey`).

- [ ] **Step 6: Коммит**

```bash
cd /home/fedya/projects/afk4.net
git add locales/ru.json locales/en.json locales/tg.json packages/i18n/src/messages.ts
git commit -m "i18n(operator-clients): фильтр «Все» и «Показать ещё» для журнала истории (ru/en/tg)"
```

---

### Task 5: dev-mock — длинный синтетический журнал + роут `GET /api/players/{id}/ledger` (пагинация + фильтр)

Расширить `devMockBackend.ts`: синтетический журнал до 48 записей разных типов/дат (детерминированно, фикс-даты — без `Date.now`), обработать роут `/ledger` с keyset-пагинацией+фильтром, чтобы превью показывало фильтр-чипы и «Показать ещё». `wallet-summary` оставить как есть (его `recentEntries` теперь только для совместимости; История читает `/ledger`).

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/devMockBackend.ts`

**Interfaces:**
- Consumes: `money`, `ORG`, `BRANCH` (уже в файле); `json` (уже в файле).
- Produces: внутренние `ledgerLog()` (48 записей) + `ledgerPage(searchParams)` (вернёт `{ items, nextCursor }`); обработка `url.pathname.endsWith('/ledger')` в `devMockFetch`.

- [ ] **Step 1: Заменить `ledgerEntries()` на детерминированный длинный журнал**

Текущий `ledgerEntries()` (строки 269-282) использует `minutesAgoUtc` (= `Date.now`). Для пагинации нужен детерминированный набор с фикс-датами. Заменить функцию `ledgerEntries()` на новую `ledgerLog()` (фикс-даты, 48 записей), а `walletSummary()` пусть берёт первые 5 из неё. Заменить блок строк 267-286 на:

```ts
// Длинный детерминированный журнал операций клиента для превью пагинации/фильтра. Фикс-даты
// (без Date.now) — keyset стабилен между рендерами. 48 записей разных типов.
const LEDGER_BASE_DAY = '2026-05-13';
function ledgerLog(): Array<Record<string, unknown>> {
  const staff = '3db1367b-88c6-4b1c-99c3-bcbb5f4d5134';
  // Курируемый цикл типов, чтобы фильтр-чипы давали непустые наборы.
  const cycle: Array<{ type: string; account: string; amount: number; description: string; reason: string }> = [
    { type: 'top_up', account: 'wallet', amount: 50000, description: 'Пополнение кошелька', reason: 'Касса' },
    { type: 'gameplay_charge', account: 'wallet', amount: -12000, description: 'Списание за игру', reason: 'Сессия PC-03' },
    { type: 'package_purchase', account: 'wallet', amount: -25000, description: 'Покупка пакета «Ночной 5ч»', reason: 'Пакет' },
    { type: 'debt_payment', account: 'debt', amount: 3500, description: 'Погашение долга', reason: 'Касса' },
    { type: 'refund', account: 'wallet', amount: -5000, description: 'Возврат операции', reason: 'Ошибочное списание' },
    { type: 'manual_correction', account: 'wallet', amount: 2000, description: 'Ручная корректировка', reason: 'Сверка кассы' }
  ];
  const log: Array<Record<string, unknown>> = [];
  for (let i = 0; i < 48; i++) {
    const spec = cycle[i % cycle.length];
    // Время убывает с ростом i: самые свежие записи — с меньшим i (час 23 → вниз).
    const minutesBack = i * 17;
    const totalMinutes = 23 * 60 - minutesBack;
    const hh = String(Math.max(0, Math.floor(totalMinutes / 60))).padStart(2, '0');
    const mm = String(((totalMinutes % 60) + 60) % 60).padStart(2, '0');
    const isRefund = spec.type === 'refund';
    log.push({
      ledgerEntryId: `le-${String(i + 1).padStart(3, '0')}`,
      organizationId: ORG, branchId: BRANCH, playerAccountId: 'pl-1', sessionId: null, playerPackageId: null,
      entryType: spec.type, accountType: spec.account, amount: money(spec.amount),
      quantitySeconds: 0, description: spec.description, reason: spec.reason,
      reversesLedgerEntryId: isRefund ? 'le-001' : null,
      createdByStaffUserId: staff,
      createdAtUtc: `${LEDGER_BASE_DAY}T${hh}:${mm}:00Z`
    });
  }
  return log; // уже newest-first (i=0 — самая свежая)
}

// Keyset-страница для /ledger: фильтр по entryType/accountType, курсор по индексу записи в журнале.
// Курсор = base64(`${index}`) — простой и детерминированный (на бэке курсор иной, но клиенту он
// непрозрачен, важна лишь корректность «следующей страницы»).
function ledgerPage(searchParams: URLSearchParams): { items: Array<Record<string, unknown>>; nextCursor: string | null } {
  const entryType = searchParams.get('entryType');
  const accountType = searchParams.get('accountType');
  const before = searchParams.get('before');
  const limit = Math.min(Math.max(Number.parseInt(searchParams.get('limit') ?? '50', 10) || 50, 1), 100);

  let all = ledgerLog();
  if (entryType) all = all.filter((e) => e.entryType === entryType);
  if (accountType) all = all.filter((e) => e.accountType === accountType);

  let start = 0;
  if (before) {
    try {
      const decoded = Number.parseInt(atob(before), 10);
      if (Number.isFinite(decoded)) start = decoded;
    } catch { start = 0; }
  }

  const items = all.slice(start, start + limit);
  const nextIndex = start + limit;
  const nextCursor = nextIndex < all.length ? btoa(String(nextIndex)) : null;
  return { items, nextCursor };
}

function walletSummary() {
  return { playerAccountId: 'pl-1', walletBalance: money(45000), debtBalance: money(0), recentEntries: ledgerLog().slice(0, 5) };
}
```

- [ ] **Step 2: Обработать роут `/ledger` в `devMockFetch`**

В `devMockFetch` (после блока `/wallet-summary`, строки 324-326) добавить:

```ts
  if (url.pathname.endsWith('/ledger') && method === 'GET') {
    return json(ledgerPage(url.searchParams));
  }
```

- [ ] **Step 3: Тайпчек + прогон существующих фронт-тестов (регресс dev-mock)**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run build`
Expected: чисто (dev-mock типизирован свободно через `Record<string, unknown>`; `atob`/`btoa` доступны в DOM-окружении Vite).

- [ ] **Step 4: Коммит**

```bash
cd /home/fedya/projects/afk4.net
git add src/AFK4.Operator.App.Web/src/devMockBackend.ts
git commit -m "feat(operator-clients): dev-mock — длинный журнал ledger + роут /ledger с пагинацией/фильтром"
```

---

### Task 6: `HistorySection.tsx` — фильтр-чипы + «Показать ещё» + отложенный скелетон (+ тест)

Расширить презентационный компонент: ряд фильтр-чипов (Все + курируемый набор ключевых типов), кнопка «Показать ещё» (когда есть ещё), отложенный скелетон при первой загрузке. Компонент остаётся управляемым оркестратором — добавляются пропсы `activeFilter`/`onFilterChange`/`hasMore`/`onLoadMore`/`loading`. EmptyState сохранить. Метки чипов — через `ledgerTypeLabel` (#29).

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/players/HistorySection.tsx`
- Create: `src/AFK4.Operator.App.Web/src/players/HistorySection.test.tsx`

(Если `HistorySection.test.tsx` уже существует из S1 — Modify вместо Create; ниже приведён полный целевой файл, заменяющий содержимое.)

**Interfaces:**
- Consumes: `LedgerEntryDto` (`../operatorApiClients`); `projectLedgerEntry`, `ledgerTypeLabel` (`./playersModel`); `formatMinorUnits` (`../operatorHelpers`); `EmptyState`, `Skeleton` (`../operatorPrimitives`); `useI18n` (`@afk4/i18n`); `History`, `RefreshCw` (`lucide-react`).
- Produces:
```ts
const HISTORY_FILTER_TYPES = ['top_up', 'gameplay_charge', 'package_purchase', 'debt_payment', 'refund'] as const;
function HistorySection(props: {
  entries: LedgerEntryDto[];
  currencyCode: string;
  activeFilter: string | null;   // null = «Все»; иначе entryType
  onFilterChange: (entryType: string | null) => void;
  hasMore: boolean;
  onLoadMore: () => void;
  loading: boolean;              // первая загрузка (скелетон) или догрузка
}): JSX.Element
```

- [ ] **Step 1: Написать падающий тест** `src/AFK4.Operator.App.Web/src/players/HistorySection.test.tsx`

```tsx
import { describe, expect, it, afterEach, mock } from 'bun:test';
import { render, screen, cleanup, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { HistorySection } from './HistorySection';
import type { LedgerEntryDto } from '../operatorApiClients';

afterEach(cleanup);

const entry = (over: Partial<LedgerEntryDto>): LedgerEntryDto => ({
  ledgerEntryId: 'le-x', organizationId: 'o', branchId: 'b', playerAccountId: 'p',
  sessionId: null, playerPackageId: null, entryType: 'top_up', accountType: 'wallet',
  amount: { currencyCode: 'TJS', minorUnits: 5000 }, quantitySeconds: 0,
  description: '', reason: '', reversesLedgerEntryId: null,
  createdByStaffUserId: 's', createdAtUtc: '2026-06-22T09:00:00Z', ...over
});

const renderSection = (over: Partial<Parameters<typeof HistorySection>[0]> = {}) => {
  const onFilterChange = mock(() => {});
  const onLoadMore = mock(() => {});
  render(
    <I18nProvider initialLocale="ru">
      <HistorySection
        entries={[entry({ ledgerEntryId: 'le-1', entryType: 'top_up', description: 'Пополнение кошелька', reason: 'Касса' })]}
        currencyCode="TJS"
        activeFilter={null}
        onFilterChange={onFilterChange}
        hasMore={false}
        onLoadMore={onLoadMore}
        loading={false}
        {...over}
      />
    </I18nProvider>
  );
  return { onFilterChange, onLoadMore };
};

describe('HistorySection', () => {
  it('renders localized type, description, reason and amount sign class', () => {
    const { container } = renderSection({
      entries: [
        entry({ ledgerEntryId: 'le-1', entryType: 'top_up', description: 'Пополнение кошелька', reason: 'Касса' }),
        entry({ ledgerEntryId: 'le-2', entryType: 'gameplay_charge', amount: { currencyCode: 'TJS', minorUnits: -1200 }, description: 'Списание' })
      ]
    });
    expect(screen.getByText('Пополнение кошелька')).toBeInTheDocument();
    expect(screen.getByText(/Касса/)).toBeInTheDocument();
    expect(container.querySelector('.client-history-row.is-credit')).not.toBeNull();
    expect(container.querySelector('.client-history-row.is-debit')).not.toBeNull();
  });

  it('renders the filter chips including «Все» and fires onFilterChange', () => {
    const { onFilterChange } = renderSection();
    const allChip = screen.getByRole('button', { name: 'Все' });
    expect(allChip).toBeInTheDocument();
    // чип «Пополнение» (ledger.type.top_up) → entryType top_up
    fireEvent.click(screen.getByRole('button', { name: 'Пополнение' }));
    expect(onFilterChange).toHaveBeenCalledWith('top_up');
  });

  it('fires onFilterChange(null) when «Все» chip is clicked', () => {
    const { onFilterChange } = renderSection({ activeFilter: 'top_up' });
    fireEvent.click(screen.getByRole('button', { name: 'Все' }));
    expect(onFilterChange).toHaveBeenCalledWith(null);
  });

  it('shows «Показать ещё» only when hasMore and fires onLoadMore', () => {
    const { onLoadMore } = renderSection({ hasMore: true });
    const more = screen.getByRole('button', { name: /Показать ещё/ });
    fireEvent.click(more);
    expect(onLoadMore).toHaveBeenCalled();
  });

  it('hides «Показать ещё» when hasMore is false', () => {
    renderSection({ hasMore: false });
    expect(screen.queryByRole('button', { name: /Показать ещё/ })).not.toBeInTheDocument();
  });

  it('renders the EmptyState when there are no entries and not loading', () => {
    renderSection({ entries: [], loading: false });
    expect(screen.getByText('Операций нет')).toBeInTheDocument();
  });

  it('renders skeleton rows (not empty state) during the first load', () => {
    const { container } = renderSection({ entries: [], loading: true });
    expect(container.querySelector('.skeleton-block')).not.toBeNull();
    expect(screen.queryByText('Операций нет')).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Запустить тест — убедиться, что падает**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/players/HistorySection.test.tsx`
Expected: FAIL — компонент ещё не принимает `activeFilter`/`onFilterChange`/`hasMore`/`onLoadMore`/`loading`; чипов/кнопки нет (`Unable to find ... button «Все»`).

- [ ] **Step 3: Переписать `src/AFK4.Operator.App.Web/src/players/HistorySection.tsx`**

```tsx
import { useI18n } from '@afk4/i18n';
import { History, RefreshCw } from 'lucide-react';
import type { LedgerEntryDto } from '../operatorApiClients';
import { formatMinorUnits } from '../operatorHelpers';
import { EmptyState, Skeleton } from '../operatorPrimitives';
import { ledgerTypeLabel, projectLedgerEntry } from './playersModel';

// Курируемый набор ключевых типов для фильтр-чипов (метки — через ledgerTypeLabel → ledger.type.*).
// Полный список значений в AFK4.Shared.Contracts/Billing/LedgerEntryTypeNames; здесь только частые.
const HISTORY_FILTER_TYPES = ['top_up', 'gameplay_charge', 'package_purchase', 'debt_payment', 'refund'] as const;

// Серверный журнал операций клиента (источник — paged ledger-эндпоинт). Презентационный:
// данные/фильтр/пагинацию держит оркестратор. activeFilter=null → «Все».
export function HistorySection({
  entries,
  currencyCode,
  activeFilter,
  onFilterChange,
  hasMore,
  onLoadMore,
  loading
}: {
  entries: LedgerEntryDto[];
  currencyCode: string;
  activeFilter: string | null;
  onFilterChange: (entryType: string | null) => void;
  hasMore: boolean;
  onLoadMore: () => void;
  loading: boolean;
}) {
  const { t } = useI18n();

  return (
    <div className="clients-history-section">
      <div className="clients-history-filters" role="group" aria-label={t('op.players.tabs.history')}>
        <button
          type="button"
          className={`clients-history-chip${activeFilter === null ? ' active' : ''}`}
          onClick={() => onFilterChange(null)}
        >
          {t('op.players.history.filterAll')}
        </button>
        {HISTORY_FILTER_TYPES.map((type) => (
          <button
            key={type}
            type="button"
            className={`clients-history-chip${activeFilter === type ? ' active' : ''}`}
            onClick={() => onFilterChange(type)}
          >
            {ledgerTypeLabel(type, t)}
          </button>
        ))}
      </div>

      {loading && entries.length === 0 ? (
        <div className="clients-history-skeleton" aria-hidden="true">
          {Array.from({ length: 6 }).map((_, index) => (
            <Skeleton key={index} className="client-history-skel" />
          ))}
        </div>
      ) : entries.length === 0 ? (
        <EmptyState
          icon={<History size={20} aria-hidden="true" />}
          title={t('op.players.history.emptyTitle')}
          description={t('op.players.history.emptyDescription')}
        />
      ) : (
        <>
          <div className="clients-history-list">
            {entries.map((raw) => {
              const view = projectLedgerEntry(raw, t);
              const sign = view.isCredit ? '+' : '−';
              const amount = formatMinorUnits(Math.abs(view.amountMinorUnits), view.currencyCode || currencyCode);
              return (
                <article key={view.id} className={`client-history-row ${view.isCredit ? 'is-credit' : 'is-debit'}`}>
                  <span className="client-history-time">{view.timeLabel}</span>
                  <div className="client-history-body">
                    <strong>
                      {view.typeLabel}
                      {view.isReversal && <em className="client-history-reversal">{t('op.players.history.reversalBadge')}</em>}
                    </strong>
                    {(view.description || view.reason) && (
                      <span className="client-history-detail">
                        {[view.description, view.reason].filter(Boolean).join(' · ')}
                      </span>
                    )}
                  </div>
                  <b className="client-history-amount">{sign}{amount}</b>
                </article>
              );
            })}
          </div>
          {hasMore && (
            <button type="button" className="clients-history-more" disabled={loading} onClick={onLoadMore}>
              <RefreshCw size={14} aria-hidden="true" />{t('op.players.history.loadMore')}
            </button>
          )}
        </>
      )}
    </div>
  );
}
```

Примечание про CSS: новые классы `.clients-history-section`, `.clients-history-filters`, `.clients-history-chip` (+`.active`), `.clients-history-skeleton`, `.client-history-skel`, `.clients-history-more` НЕ покрыты `12-players.css` из S1. CSS в jsdom не тестируется — компонент работает функционально без стилей. Базовые подгонки добавлены в Task 8 Step 1 (класс-контракт). До этого превью «грубое», но функциональное.

- [ ] **Step 4: Прогнать тест + тайпчек**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/players/HistorySection.test.tsx && /home/fedya/.bun/bin/bun run build`
Expected: тест PASS (7 блоков); `bun run build` чисто.

- [ ] **Step 5: Коммит**

```bash
cd /home/fedya/projects/afk4.net
git add src/AFK4.Operator.App.Web/src/players/HistorySection.tsx src/AFK4.Operator.App.Web/src/players/HistorySection.test.tsx
git commit -m "feat(operator-clients): HistorySection — фильтр-чипы + «Показать ещё» + скелетон загрузки"
```

---

### Task 7: Оркестратор — ledger-состояние + переключение источника Истории + App.test

Добавить в `BackendPlayersWorkspace.tsx` состояние журнала (`ledgerEntries`/`ledgerCursor`/`ledgerFilter`/`ledgerLoading`), эффект загрузки первой страницы при активном табе «История» + backend-клиенте (право `billing.view`), «Показать ещё» (догрузка по курсору + аппенд), смену фильтра (сброс + перезагрузка). Передать новые пропсы в `ClientDetail` → `HistorySection`. Кошелёк/Пакеты/действия НЕ трогаем. Обновить App.test (History-регион под новый источник).

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/BackendPlayersWorkspace.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/players/ClientDetail.tsx` (проброс новых пропсов в `HistorySection`)
- Modify: `src/AFK4.Operator.App.Web/src/App.test.tsx` (History-регион)

**Interfaces:**
- Consumes: `LedgerEntryDto` (`./operatorApiClients`); `players.getLedger` (Task 3); `hasPermission`/`permissionNames.viewBilling`; существующее `selectedClient`/`activeTab`/`backend`.
- Produces (новые пропсы `ClientDetail`, проброс в `HistorySection`):
```ts
// в ClientDetail добавляются пропсы:
//   ledgerEntries: LedgerEntryDto[]; ledgerFilter: string | null; ledgerHasMore: boolean; ledgerLoading: boolean;
//   onLedgerFilterChange: (entryType: string | null) => void; onLedgerLoadMore: () => void;
```

- [ ] **Step 1: Проброс новых пропсов в `ClientDetail.tsx`**

В `src/AFK4.Operator.App.Web/src/players/ClientDetail.tsx`:

(a) Удалить из пропсов `recentEntries: LedgerEntryDto[];` (History больше не из recentEntries) и добавить ledger-пропсы. В сигнатуре `ClientDetail(props: {...})` заменить строку `recentEntries: LedgerEntryDto[];` на:

```ts
  ledgerEntries: LedgerEntryDto[];
  ledgerFilter: string | null;
  ledgerHasMore: boolean;
  ledgerLoading: boolean;
  onLedgerFilterChange: (entryType: string | null) => void;
  onLedgerLoadMore: () => void;
```

(b) В блоке рендера History-таба заменить:

```tsx
        {props.activeTab === 'history' && (
          <HistorySection entries={props.recentEntries} currencyCode={props.currencyCode} />
        )}
```

на:

```tsx
        {props.activeTab === 'history' && (
          <HistorySection
            entries={props.ledgerEntries}
            currencyCode={props.currencyCode}
            activeFilter={props.ledgerFilter}
            onFilterChange={props.onLedgerFilterChange}
            hasMore={props.ledgerHasMore}
            onLoadMore={props.onLedgerLoadMore}
            loading={props.ledgerLoading}
          />
        )}
```

(c) Проверить `ClientDetail.test.tsx` (из S1): тест истории передавал `recentEntries: []`. Обновить `baseProps` в `ClientDetail.test.tsx` — заменить `recentEntries: [],` на:

```ts
  ledgerEntries: [],
  ledgerFilter: null,
  ledgerHasMore: false,
  ledgerLoading: false,
  onLedgerFilterChange: () => {},
  onLedgerLoadMore: () => {},
```

И в тесте `it('renders the history section on the history tab', ...)` заменить `renderDetail({ activeTab: 'history', recentEntries: [] })` на `renderDetail({ activeTab: 'history', ledgerEntries: [], ledgerLoading: false })`. Ожидание `screen.getByText('Операций нет')` остаётся валидным (пустой журнал + не loading → EmptyState).

- [ ] **Step 2: Добавить ledger-состояние и эффект в `BackendPlayersWorkspace.tsx`**

(a) Добавить состояние (после `const [newPlayerPhone, setNewPlayerPhone] = useState('');`, строка 48):

```ts
  const [ledgerEntries, setLedgerEntries] = useState<LedgerEntryDto[]>([]);
  const [ledgerCursor, setLedgerCursor] = useState<string | null>(null);
  const [ledgerFilter, setLedgerFilter] = useState<string | null>(null);
  const [ledgerLoading, setLedgerLoading] = useState(false);
```

(b) Добавить импорт типа `LedgerEntryDto` — в строке 5 расширить импорт из `./operatorApiClients`:

```ts
import type { LedgerEntryDto, PackageOptionDto, PlayerPackageDto, WalletSummaryDto } from './operatorApiClients';
```

(c) Добавить производную права на просмотр журнала (рядом с `canCreateClientReservation`, после строки 187):

```ts
  const canViewLedger = backend !== null
    && selectedClient !== null
    && selectedClient.source === 'backend'
    && Boolean(selectedClient.playerAccountId)
    && hasPermission(backend.session, permissionNames.viewBilling);
```

(d) Добавить эффект загрузки первой страницы журнала — после wallet-loader эффекта (после строки 136). Грузим, когда таб «История» активен, выбран backend-клиент и есть право; ключ эффекта включает `activeTab`, `selectedClient.playerAccountId`, `ledgerFilter`:

```ts
  // Журнал истории: серверный источник (paged ledger-эндпоинт), отдельно от wallet-summary.
  // Грузим первую страницу при входе на таб «История» / смене клиента / смене фильтра.
  useEffect(() => {
    if (!canViewLedger || activeTab !== 'history' || selectedClient === null || !selectedClient.playerAccountId) {
      return undefined;
    }

    const nextBackend = backend;
    if (nextBackend === null) {
      return undefined;
    }

    const playerAccountId = selectedClient.playerAccountId;
    let disposed = false;
    const loadLedger = async () => {
      setLedgerLoading(true);
      try {
        const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
        const page = await apiClients.players.getLedger(playerAccountId, {
          entryType: ledgerFilter ?? undefined,
          limit: 50
        });
        if (!disposed) {
          setLedgerEntries(page.items);
          setLedgerCursor(page.nextCursor);
        }
      } catch (error) {
        if (!disposed) {
          setLedgerEntries([]);
          setLedgerCursor(null);
          setFeedback({ label: t('op.players.tabs.history'), state: 'failed', detail: projectOperatorError(error, t).detail });
        }
      } finally {
        if (!disposed) {
          setLedgerLoading(false);
        }
      }
    };

    void loadLedger();
    return () => {
      disposed = true;
    };
  }, [
    backend?.branchId,
    backend?.config.platformBaseUrl,
    backend?.session.accessToken,
    activeTab,
    selectedClient?.playerAccountId,
    selectedClient?.source,
    ledgerFilter,
    canViewLedger
  ]);
```

(e) Добавить хендлеры «Показать ещё» и «сменить фильтр» (рядом с `submitNewClient`, после строки 344):

```ts
  const loadMoreLedger = async () => {
    if (backend === null || selectedClient === null || !selectedClient.playerAccountId || ledgerCursor === null) {
      return;
    }

    const playerAccountId = selectedClient.playerAccountId;
    setLedgerLoading(true);
    try {
      const apiClients = createAuthenticatedOperatorClients(backend.config, backend.session);
      const page = await apiClients.players.getLedger(playerAccountId, {
        entryType: ledgerFilter ?? undefined,
        cursor: ledgerCursor,
        limit: 50
      });
      setLedgerEntries((current) => [...current, ...page.items]);
      setLedgerCursor(page.nextCursor);
    } catch (error) {
      setFeedback({ label: t('op.players.tabs.history'), state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setLedgerLoading(false);
    }
  };

  // Смена фильтра: сброс журнала и курсора — эффект перезагрузит первую страницу (ledgerFilter в deps).
  const changeLedgerFilter = (entryType: string | null) => {
    setLedgerEntries([]);
    setLedgerCursor(null);
    setLedgerFilter(entryType);
  };
```

(f) Передать новые пропсы в `ClientDetail` (в JSX, заменить `recentEntries={recentEntries}` строка 389):

```tsx
          ledgerEntries={ledgerEntries}
          ledgerFilter={ledgerFilter}
          ledgerHasMore={ledgerCursor !== null}
          ledgerLoading={ledgerLoading}
          onLedgerFilterChange={changeLedgerFilter}
          onLedgerLoadMore={() => void loadMoreLedger()}
```

(g) Удалить теперь-неиспользуемую производную `recentEntries` (строка 148): `const recentEntries = walletSummary?.recentEntries ?? [];` — удалить (она больше не передаётся). Если `bun run build` ругнётся на неиспользуемую переменную — это и есть сигнал удалить её.

- [ ] **Step 3: Прогнать тесты секций (регресс) + тайпчек**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/players/ && /home/fedya/.bun/bin/bun run build`
Expected: все `src/players/*.test.*` PASS (включая обновлённый `ClientDetail.test.tsx`); `bun run build` чисто.

- [ ] **Step 4: Обновить History-регион в `src/App.test.tsx`**

(a) Добавить mock-обработчик роута `/ledger` в fetch-мок App.test. Рядом с блоком `/wallet-summary` (строки 3142-3153) добавить ПЕРЕД ним (роут `/ledger` специфичнее, чем `/wallet-summary`, но pathname разные — порядок не критичен; ставим рядом):

```ts
  if (pathname.endsWith('/ledger')) {
    const url = new URL(String(input));
    const entryType = url.searchParams.get('entryType');
    const all = [
      { ledgerEntryId: '13131313-1313-1313-1313-131313131313', organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08', branchId: 'acfc0212-967f-4d84-94be-9003387b09c2', playerAccountId: '12121212-1212-1212-1212-121212121212', sessionId: null, playerPackageId: null, entryType: 'top_up', accountType: 'wallet', amount: { currencyCode: 'TJS', minorUnits: 20000 }, quantitySeconds: 0, description: 'Пополнение кошелька', reason: 'Касса', reversesLedgerEntryId: null, createdByStaffUserId: '3db1367b-88c6-4b1c-99c3-bcbb5f4d5134', createdAtUtc: '2026-05-21T09:00:00Z' },
      { ledgerEntryId: '14141414-1414-1414-1414-141414141414', organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08', branchId: 'acfc0212-967f-4d84-94be-9003387b09c2', playerAccountId: '12121212-1212-1212-1212-121212121212', sessionId: null, playerPackageId: null, entryType: 'gameplay_charge', accountType: 'wallet', amount: { currencyCode: 'TJS', minorUnits: -1200 }, quantitySeconds: 0, description: 'Списание за игру', reason: 'Сессия', reversesLedgerEntryId: null, createdByStaffUserId: '3db1367b-88c6-4b1c-99c3-bcbb5f4d5134', createdAtUtc: '2026-05-21T08:00:00Z' }
    ];
    const items = entryType ? all.filter((e) => e.entryType === entryType) : all;
    return jsonResponse({ items, nextCursor: null });
  }
```

(b) Добавить новый `it`-тест в players-регион (после head-теста, рядом с прочими players-тестами — найти по `getByTitle('Клиенты')`). Покрывает: вход на таб «История» грузит `/ledger`, рендерит записи, фильтр по типу перезапрашивает:

```ts
  it('История клиента читает серверный журнал /ledger и фильтрует по типу', async () => {
    renderApp();
    fireEvent.click(screen.getByTitle('Клиенты'));
    expect(await screen.findByTitle(/Сервер на связи/)).toBeInTheDocument();

    fireEvent.click(screen.getByRole('tab', { name: 'История' }));

    // первая запись журнала из /ledger (top_up) — описание видно
    expect(await screen.findByText('Пополнение кошелька')).toBeInTheDocument();
    // источник — именно /ledger, не recentEntries из wallet-summary
    expect(fetchMock.mock.calls.some(([input]) =>
      String(input).includes('/api/players/12121212-1212-1212-1212-121212121212/ledger'))).toBe(true);

    // фильтр по типу top_up → перезапрос /ledger?entryType=top_up
    fireEvent.click(screen.getByRole('button', { name: 'Пополнение' }));
    await waitFor(() => expect(fetchMock.mock.calls.some(([input]) =>
      String(input).includes('/ledger') && String(input).includes('entryType=top_up'))).toBe(true));
  });
```

Примечание: имя fetch-мока (`fetchMock`) и хелпер `renderApp`/`waitFor`/`within` — взять как в соседних players-тестах App.test (они уже импортированы; см. buy-package/create-player тесты S1). Если переменная мока называется иначе (напр. `fetchSpy`), использовать существующее имя. `getByRole('button', { name: 'Пополнение' })` — чип фильтра (`ledger.type.top_up` = «Пополнение»).

- [ ] **Step 5: Полный прогон App.test (ОТДЕЛЬНО) + остальные тесты + сборка**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/App.test.tsx`
Expected: весь players-регион зелёный, включая новый ledger-тест.

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run build`
Expected: `bun run build` чисто (нет неиспользуемой `recentEntries`; новые пропсы согласованы фронт↔компонент).

- [ ] **Step 6: Коммит**

```bash
cd /home/fedya/projects/afk4.net
git add src/AFK4.Operator.App.Web/src/BackendPlayersWorkspace.tsx src/AFK4.Operator.App.Web/src/players/ClientDetail.tsx src/AFK4.Operator.App.Web/src/players/ClientDetail.test.tsx src/AFK4.Operator.App.Web/src/App.test.tsx
git commit -m "feat(operator-clients): История читает серверный журнал /ledger (фильтр + «Показать ещё»)"
```

---

### Task 8: CSS `styles/12-players.css` — фильтр-чипы/«Показать ещё»/скелетон истории + финальная верификация

Добавить baseline-CSS для новых классов истории (фильтр-чипы, кнопка «Показать ещё», скелетон) поверх существующего `12-players.css` из S1, зеркаля паттерны соседнего CSS (акцент синий `var(--accent)`, тёмная тема, hover/focus-visible/active как у `.clients-segment-chip`). Затем прогнать полную верификацию слайса.

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/styles/12-players.css` (добавить секцию истории-фильтров)

**Класс-контракт (имена из Task 6 — CSS обязан их покрыть):**
- `.clients-history-section`, `.clients-history-filters`, `.clients-history-chip` (+`.active`), `.clients-history-skeleton`, `.client-history-skel`, `.clients-history-more`.
- Существующие `.clients-history-list`, `.client-history-row` (+`.is-credit`/`.is-debit`), `.client-history-time`, `.client-history-body`, `.client-history-detail`, `.client-history-reversal`, `.client-history-amount` — уже в `12-players.css` из S1, НЕ дублировать.

- [ ] **Step 1: Добавить секцию CSS в `src/AFK4.Operator.App.Web/src/styles/12-players.css`**

Дописать в КОНЕЦ файла (после существующих `.client-history-*` правил):

```css
/* ── История: фильтр-чипы + «Показать ещё» + скелетон (S1b) ─────────────────── */
.clients-history-section {
  display: flex;
  flex-direction: column;
  gap: 10px;
  min-height: 0;
}

.clients-history-filters {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.clients-history-chip {
  display: inline-flex;
  align-items: center;
  height: 26px;
  border: 1px solid var(--border-default);
  border-radius: 999px;
  padding: 0 11px;
  background: var(--surface-card);
  color: var(--text-secondary);
  font-size: 11px;
  font-weight: 600;
  transition: border-color 120ms ease, background 120ms ease, color 120ms ease, transform 120ms ease;
}

.clients-history-chip:hover:not(.active) {
  border-color: var(--accent);
  transform: translateY(-1px);
}

.clients-history-chip:focus-visible {
  outline: 2px solid rgba(var(--accent-rgb), 0.82);
  outline-offset: 2px;
}

.clients-history-chip:active {
  transform: scale(0.97);
}

.clients-history-chip.active {
  border-color: var(--accent);
  background: rgba(var(--accent-rgb), 0.16);
  color: var(--text-primary);
}

.clients-history-skeleton {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.client-history-skel {
  height: 38px;
  border-radius: 6px;
}

.clients-history-more {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  align-self: center;
  height: 30px;
  margin-top: 4px;
  border: 1px solid var(--border-default);
  border-radius: 6px;
  padding: 0 14px;
  background: var(--surface-card);
  color: var(--text-secondary);
  font-size: 12px;
  font-weight: 600;
  transition: border-color 120ms ease, background 120ms ease, transform 120ms ease;
}

.clients-history-more:hover:not(:disabled) {
  border-color: var(--accent);
  transform: translateY(-1px);
}

.clients-history-more:focus-visible {
  outline: 2px solid rgba(var(--accent-rgb), 0.82);
  outline-offset: 2px;
}

.clients-history-more:active:not(:disabled) {
  transform: scale(0.97);
}

.clients-history-more:disabled {
  cursor: not-allowed;
  color: var(--text-quaternary);
}

@media (prefers-reduced-motion: reduce) {
  .clients-history-chip,
  .clients-history-more {
    transition: none;
  }
}
```

Примечание: если в `12-players.css` из S1 переменные именуются иначе (напр. `--surface-elevated` вместо `--surface-card`), привести к фактическим токенам соседних правил того же файла (проверить `var(--...)` в существующем `.clients-segment-chip`). Класс-контракт (имена) — обязателен; точные значения цвета зеркалят соседний CSS.

- [ ] **Step 2: Сборка фронта (CSS в jsdom не тестируется — критерий = чистая сборка)**

Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run build`
Expected: `bun run build` без ошибок (Vite собирает CSS; tsc чист).

- [ ] **Step 3: Полная верификация слайса S1b**

Фронт-сьют (все subdir-тесты):
Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run test`
Expected: первый прогон (рекурсивная дискавери из S1) — все `src/`/`src/players/`/`src/api/clients/` зелёные, включая `HistorySection.test.tsx` (7), `players.test.ts` (4), `ClientDetail.test.tsx`; затем отдельный прогон `src/App.test.tsx` зелёный.

App.test отдельно (страховка изоляции):
Run: `cd /home/fedya/projects/afk4.net/src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/App.test.tsx`
Expected: players-регион зелёный, новый ledger-тест зелёный.

i18n-гарды:
Run: `cd /home/fedya/projects/afk4.net/packages/i18n && /home/fedya/.bun/bin/bun test`
Expected: parity ru=en=tg, voice, tg≠ru guard — зелёные.

Бэкенд:
Run: `cd /home/fedya/projects/afk4.net && /home/fedya/.dotnet/dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj`
Expected: все тесты зелёные (`PlayerLedgerProjectorTests` + существующие).

Run: `cd /home/fedya/projects/afk4.net && /home/fedya/.dotnet/dotnet build`
Expected: `Build succeeded`, 0 ошибок по всему решению.

- [ ] **Step 4: Коммит**

```bash
cd /home/fedya/projects/afk4.net
git add src/AFK4.Operator.App.Web/src/styles/12-players.css
git commit -m "style(operator-clients): фильтр-чипы/«Показать ещё»/скелетон истории — baseline CSS"
```

---

## Self-Review

**1. Spec coverage (S1b — раздел «1. Богатая история + серверная пагинация», строки 84-94 + «Честные ограничения»):**

| Требование спеки | Задача |
|---|---|
| Новый эндпоинт `GET /api/players/{id}/ledger?entryType=&accountType=&before=&limit=50` | Task 2 |
| → `{ entries: LedgerEntryDto[], nextCursor? }` (реально `CursorPage<LedgerEntryDto>` = `{ items, nextCursor }`) | Task 1/2 — см. ниже «неоднозначность» |
| Org-защита и право как у wallet-summary (`LoadPlayerScopedEndpointAsync` + `billing.view`) | Task 2 |
| Курсор = `(CreatedAtUtc, LedgerEntryId)`, стабилен при вставках (keyset) | Task 1 |
| Фильтр server-side `entryType`/`accountType`, валидация против `*Names` | Task 1 (`PlayerLedgerFilter`) + Task 2 (400) |
| Разделение источников (#34): История из ledger, wallet-summary только баланс/долг | Task 7 |
| Клиент `players.getLedger(id, { entryType?, accountType?, cursor?, limit? })` | Task 3 |
| Фильтр-чипы + «Показать ещё» | Task 6 (UI) + Task 7 (оркестратор) |
| dev-mock: длинный журнал + роут `/ledger` пагинация/фильтр | Task 5 |
| i18n filterAll/loadMore (ru/en/tg) | Task 4 |
| keyset/фильтр/валидация юнит-тестируемы без интеграционного харнеса | Task 1 (все unit) |
| НИКАКИХ date-бомб (статические даты) | Task 1 (`Base = "2026-05-13T10:00:00Z"`), Task 5 (`LEDGER_BASE_DAY`) |

Все требования S1b покрыты. Поведение Кошелёк/Пакеты/поиск/действия/сегменты не трогается (Task 7 меняет только History-источник).

**2. Placeholder scan:** Плейсхолдеров нет — весь код приведён целиком (проектор, эндпоинт, клиент, компонент, оркестратор-патчи, dev-mock, CSS, все тесты). Команды с ожидаемым выводом. Единственные условные места явно помечены как «привести к фактическому имени» (имя fetch-мока в App.test, имена CSS-токенов) — это адаптация к существующему коду соседних тестов/файлов, не пропущенная реализация.

**3. Type consistency:**
- Бэк: `GetLedgerPageAsync(... string? entryType, string? accountType, string? before, int limit ...)` (Task 1) ↔ вызов в эндпоинте `PlayerLedgerProjector.GetLedgerPageAsync(dbContext, playerAccountId, entryType, accountType, before, PlayerLedgerFilter.ClampLimit(limit), ct)` (Task 2) — совпадает. `PlayerLedgerFilter.ClampLimit/IsValidEntryType/IsValidAccountType` — сигнатуры совпадают между Task 1 и Task 2.
- Контракт ↔ клиент: `CursorPage<LedgerEntryDto>` = `{ Items, NextCursor }` (PascalCase C#) сериализуется в camelCase `{ items, nextCursor }` ↔ `CursorPageDto<T> = { items: T[]; nextCursor: string | null }` (Task 3) — совпадает.
- Клиент ↔ оркестратор: `getLedger(playerAccountId, { entryType?, accountType?, cursor?, limit? }): Promise<CursorPageDto<LedgerEntryDto>>` ↔ вызовы `apiClients.players.getLedger(playerAccountId, { entryType: ..., limit: 50 })` и `{ entryType, cursor: ledgerCursor, limit: 50 }` (Task 7) — совпадает; `page.items`/`page.nextCursor` читаются корректно.
- Оркестратор ↔ ClientDetail ↔ HistorySection: `ledgerEntries`/`ledgerFilter`/`ledgerHasMore`/`ledgerLoading`/`onLedgerFilterChange`/`onLedgerLoadMore` (Task 7) пробрасываются в `HistorySection` пропсы `entries`/`activeFilter`/`hasMore`/`loading`/`onFilterChange`/`onLoadMore` (Task 6) — имена согласованы.
- `HISTORY_FILTER_TYPES` (`top_up`/`gameplay_charge`/`package_purchase`/`debt_payment`/`refund`) — все ∈ `LedgerEntryTypeNames` (валидны на бэке) и ∈ `LEDGER_TYPE_KEYS` в `ledgerTypeLabel` (есть метки).
