# «Клуб» — профиль клуба (редизайн) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Превратить экран «Клуб» из заглушки на 2 поля (Name/City) в полноценный профиль клуба: лицо для игрока (название/описание/логотип), адрес и контакты, часы работы по 7 дням, настройки филиала (часовой пояс/язык/валюта-RO) — с живым предпросмотром «как видит игрок».

**Architecture:** Бэкенд расширяет `BranchEntity` новыми колонками + JSON-часы; профиль-эндпоинты (`GET/PATCH /api/branches/{id}/profile`) переводятся на право `ManageBranchSettings` (унификация — сейчас рассинхрон с фронт-гейтом) и отдают/принимают расширенный `BranchProfileDto`/`UpdateBranchProfileRequest`. Фронт разбивает экран на презентационные блоки на kit-классах + переиспользует `MediaUpload` (под-проект 1) для логотипа + двухколоночную раскладку с превью.

**Tech Stack:** .NET 10 minimal APIs, EF Core (PostgreSQL/Npgsql), xUnit + WebApplicationFactory (InMemory); React + TS, `@afk4/i18n` (ICU, генерируется из `/locales/*.json`), kit-компоненты (`mgmt-form`/`mgmt-section-title`/`mgmt-form-grid`/`ui-btn*`), `bun test` (happy-dom).

## Global Constraints

- **i18n:** все UI-строки — через `@afk4/i18n`; источник `/locales/{ru,en,tg}.json` (корень репо) → `cd packages/i18n && bun run gen` регенерирует `packages/i18n/src/messages.ts`. НЕ править `messages.ts` руками. Паритет ru/en/tg обязателен (`messages.test.ts`); `tg` — настоящий таджикский, не копия ru (guard-тест); бренды/имена собственные (Telegram) — в whitelist гварда.
- **Имена через назначение:** название клуба — человекочитаемое, НИКОГДА не UUID.
- **Деньги/время:** валюта — read-only (уровень сети); часовой пояс ХРАНИМ и редактируем как конфиг, но НЕ перепроводим в логику lease/биллинга в этом плане (отложенный пункт time-handling-аудита).
- **Секреты:** только через env, не в коде/логах.
- **Право доступа:** весь раздел + все его эндпоинты (profile + media) гейтятся на `branches.settings.manage` (`StaffPermissionNames.ManageBranchSettings` / `permissionNames.manageBranchSettings`).
- **Фронт:** `bun test` (happy-dom); `bun run build` = `tsc -b && vite` тайпчекает И тест-файлы (зелёный `bun test` ≠ зелёная сборка → типизировать bun-моки). Компоненты определять на module-scope, НИКОГДА не внутри других компонентов (иначе remount + потеря фокуса инпутов).
- **Тач-таргеты ≥44px** во всех интерактивных элементах (готовность к мобильной обёртке).
- **Часы работы — модель:** `dayOfWeek` по ISO-8601: 1=Понедельник … 7=Воскресенье. Время — строка `"HH:mm"` (24ч).

## Файловая структура

**Бэкенд (создать):**
- `src/AFK4.Shared.Contracts/Branches/BranchWorkingHoursDayDto.cs` — контракт одного дня.
- `src/AFK4.Platform.Api/Branches/BranchWorkingHours.cs` — статик-хелпер: Default/Serialize/Deserialize/Validate.
- `tests/AFK4.Platform.Api.Tests/BranchWorkingHoursTests.cs` — юнит-тесты хелпера.
- `src/AFK4.Platform.Api/Data/Migrations/<ts>_AddBranchClubProfile.cs` (+ Designer) — генерируется `dotnet ef`.
- `tests/AFK4.Platform.Api.Tests/BranchProfileEndpointTests.cs` — тесты расширенного эндпоинта.

**Бэкенд (изменить):**
- `src/AFK4.Shared.Contracts/Branches/BranchProfileDto.cs` — новые поля.
- `src/AFK4.Shared.Contracts/Branches/UpdateBranchProfileRequest.cs` — новые поля.
- `src/AFK4.Platform.Api/Data/BranchEntity.cs` — новые колонки.
- `src/AFK4.Platform.Api/Data/PlatformDbContext.cs` — маппинг колонок.
- `src/AFK4.Platform.Api/Endpoints/BranchProfileLayoutEndpoints.cs` — profile GET/PATCH: право + новые поля + часы.
- `src/AFK4.Platform.Api/Endpoints/EndpointHelpers.Dtos.cs` — `ToBranchProfileDto`.
- `src/AFK4.Platform.Api/Endpoints/EndpointHelpers.Validation.cs` — `ValidateUpdateBranchProfileRequest`.

**Фронт (создать):**
- `src/AFK4.Operator.App.Web/src/settings/club/workingHours.ts` — TS-модель + default/normalize + weekday-i18n-ключи.
- `src/AFK4.Operator.App.Web/src/settings/club/workingHours.test.ts`
- `src/AFK4.Operator.App.Web/src/settings/club/WorkingHoursEditor.tsx`
- `src/AFK4.Operator.App.Web/src/settings/club/WorkingHoursEditor.test.tsx`
- `src/AFK4.Operator.App.Web/src/settings/club/ClubProfileFields.tsx`
- `src/AFK4.Operator.App.Web/src/settings/club/ClubProfileFields.test.tsx`
- `src/AFK4.Operator.App.Web/src/settings/club/ClubPlayerPreview.tsx`

**Фронт (изменить):**
- `/locales/{ru,en,tg}.json` — новые ключи.
- `src/AFK4.Operator.App.Web/src/api/clients/settings.ts` — TS-типы `BranchWorkingHoursDay` + расширенный `UpdateBranchProfileRequest`.
- `src/AFK4.Operator.App.Web/src/management/destinations/ClubDestination.tsx` — переписать контейнер.
- `src/AFK4.Operator.App.Web/src/management/destinations/ClubDestination.test.tsx` — расширить.
- `src/AFK4.Operator.App.Web/src/styles/23-management-crud.css` — раскладка Клуба.
- `src/AFK4.Operator.App.Web/src/settings/SettingsProfileSection.tsx` — НЕ трогать (используется Setup-Wizard/BackendSettingsWorkspace; Клуб перестаёт его импортировать).

---

### Task 1: Часы работы — контракт, хелпер, валидация (бэкенд, чистая логика)

**Files:**
- Create: `src/AFK4.Shared.Contracts/Branches/BranchWorkingHoursDayDto.cs`
- Create: `src/AFK4.Platform.Api/Branches/BranchWorkingHours.cs`
- Test: `tests/AFK4.Platform.Api.Tests/BranchWorkingHoursTests.cs`

**Interfaces:**
- Produces:
  - `record BranchWorkingHoursDayDto(int DayOfWeek, bool IsClosed, string? OpenTime, string? CloseTime)` (namespace `AFK4.Shared.Contracts.Branches`).
  - `static class BranchWorkingHours` (namespace `AFK4.Platform.Api.Branches`):
    - `IReadOnlyList<BranchWorkingHoursDayDto> Default()`
    - `string Serialize(IReadOnlyList<BranchWorkingHoursDayDto> days)`
    - `IReadOnlyList<BranchWorkingHoursDayDto> Deserialize(string? json)` — при null/пусто/битом JSON возвращает `Default()`, всегда нормализует к 7 дням 1..7.
    - `string? Validate(IReadOnlyList<BranchWorkingHoursDayDto> days)` — null если ок, иначе текст ошибки.

- [ ] **Step 1: Написать контракт дня**

Create `src/AFK4.Shared.Contracts/Branches/BranchWorkingHoursDayDto.cs`:

```csharp
namespace AFK4.Shared.Contracts.Branches;

/// <summary>Один день расписания клуба. DayOfWeek по ISO-8601: 1=Пн … 7=Вс.
/// Время — строка "HH:mm" (24ч); при IsClosed времена игнорируются.</summary>
public sealed record BranchWorkingHoursDayDto(int DayOfWeek, bool IsClosed, string? OpenTime, string? CloseTime);
```

- [ ] **Step 2: Написать падающие юнит-тесты хелпера**

Create `tests/AFK4.Platform.Api.Tests/BranchWorkingHoursTests.cs`:

```csharp
using AFK4.Platform.Api.Branches;
using AFK4.Shared.Contracts.Branches;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public class BranchWorkingHoursTests
{
    [Fact]
    public void Default_ReturnsSevenDays_MondayToSunday_AllOpen()
    {
        var days = BranchWorkingHours.Default();
        Assert.Equal(7, days.Count);
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7 }, days.Select(d => d.DayOfWeek).ToArray());
        Assert.All(days, d => Assert.False(d.IsClosed));
        Assert.All(days, d => Assert.Equal("10:00", d.OpenTime));
        Assert.All(days, d => Assert.Equal("22:00", d.CloseTime));
    }

    [Fact]
    public void Deserialize_Null_ReturnsDefault()
    {
        var days = BranchWorkingHours.Deserialize(null);
        Assert.Equal(7, days.Count);
    }

    [Fact]
    public void SerializeThenDeserialize_RoundTrips()
    {
        var input = BranchWorkingHours.Default()
            .Select(d => d.DayOfWeek == 7 ? d with { IsClosed = true } : d)
            .ToList();
        var json = BranchWorkingHours.Serialize(input);
        var back = BranchWorkingHours.Deserialize(json);
        Assert.True(back.Single(d => d.DayOfWeek == 7).IsClosed);
        Assert.False(back.Single(d => d.DayOfWeek == 1).IsClosed);
    }

    [Fact]
    public void Validate_ValidWeek_ReturnsNull()
    {
        Assert.Null(BranchWorkingHours.Validate(BranchWorkingHours.Default()));
    }

    [Fact]
    public void Validate_WrongDayCount_ReturnsError()
    {
        var days = BranchWorkingHours.Default().Take(6).ToList();
        Assert.NotNull(BranchWorkingHours.Validate(days));
    }

    [Fact]
    public void Validate_DuplicateDay_ReturnsError()
    {
        var days = BranchWorkingHours.Default().ToList();
        days[6] = days[6] with { DayOfWeek = 1 };
        Assert.NotNull(BranchWorkingHours.Validate(days));
    }

    [Fact]
    public void Validate_OpenNotBeforeClose_ReturnsError()
    {
        var days = BranchWorkingHours.Default().ToList();
        days[0] = days[0] with { OpenTime = "22:00", CloseTime = "10:00" };
        Assert.NotNull(BranchWorkingHours.Validate(days));
    }

    [Fact]
    public void Validate_BadTimeFormat_ReturnsError()
    {
        var days = BranchWorkingHours.Default().ToList();
        days[0] = days[0] with { OpenTime = "9am", CloseTime = "22:00" };
        Assert.NotNull(BranchWorkingHours.Validate(days));
    }

    [Fact]
    public void Validate_ClosedDay_IgnoresTimes()
    {
        var days = BranchWorkingHours.Default().ToList();
        days[0] = days[0] with { IsClosed = true, OpenTime = null, CloseTime = null };
        Assert.Null(BranchWorkingHours.Validate(days));
    }
}
```

- [ ] **Step 3: Запустить тесты — убедиться, что падают (нет типа/класса)**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~BranchWorkingHoursTests`
Expected: FAIL — `BranchWorkingHours`/`BranchWorkingHoursDayDto` не существуют (ошибка компиляции).

- [ ] **Step 4: Реализовать хелпер**

Create `src/AFK4.Platform.Api/Branches/BranchWorkingHours.cs`:

```csharp
using System.Globalization;
using System.Text.Json;
using AFK4.Shared.Contracts.Branches;

namespace AFK4.Platform.Api.Branches;

/// <summary>Сериализация/валидация расписания клуба (7 дней). Хранится одной JSON-колонкой
/// branches.WorkingHoursJson; читается всегда нормализованно к дням 1..7.</summary>
public static class BranchWorkingHours
{
    private const string DefaultOpen = "10:00";
    private const string DefaultClose = "22:00";

    public static IReadOnlyList<BranchWorkingHoursDayDto> Default() =>
        Enumerable.Range(1, 7)
            .Select(day => new BranchWorkingHoursDayDto(day, false, DefaultOpen, DefaultClose))
            .ToList();

    public static string Serialize(IReadOnlyList<BranchWorkingHoursDayDto> days) =>
        JsonSerializer.Serialize(days);

    public static IReadOnlyList<BranchWorkingHoursDayDto> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Default();
        }

        List<BranchWorkingHoursDayDto>? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<List<BranchWorkingHoursDayDto>>(json);
        }
        catch (JsonException)
        {
            return Default();
        }

        if (parsed is null || parsed.Count == 0)
        {
            return Default();
        }

        // Нормализуем к 7 дням 1..7: берём известные дни, недостающие добираем дефолтом.
        var byDay = parsed
            .Where(d => d.DayOfWeek is >= 1 and <= 7)
            .GroupBy(d => d.DayOfWeek)
            .ToDictionary(g => g.Key, g => g.First());

        return Enumerable.Range(1, 7)
            .Select(day => byDay.TryGetValue(day, out var found)
                ? found
                : new BranchWorkingHoursDayDto(day, false, DefaultOpen, DefaultClose))
            .ToList();
    }

    public static string? Validate(IReadOnlyList<BranchWorkingHoursDayDto> days)
    {
        if (days.Count != 7)
        {
            return "Working hours must contain exactly 7 days.";
        }

        if (days.Select(d => d.DayOfWeek).OrderBy(x => x).SequenceEqual(Enumerable.Range(1, 7)) == false)
        {
            return "Working hours must cover days 1..7 exactly once.";
        }

        foreach (var day in days)
        {
            if (day.IsClosed)
            {
                continue;
            }

            if (!TryParseTime(day.OpenTime, out var open) || !TryParseTime(day.CloseTime, out var close))
            {
                return "Open/close time must be in HH:mm format for non-closed days.";
            }

            if (open >= close)
            {
                return "Open time must be earlier than close time.";
            }
        }

        return null;
    }

    private static bool TryParseTime(string? value, out TimeOnly time)
    {
        if (!string.IsNullOrWhiteSpace(value)
            && TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time))
        {
            return true;
        }

        time = default;
        return false;
    }
}
```

- [ ] **Step 5: Запустить тесты — убедиться, что проходят**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~BranchWorkingHoursTests`
Expected: PASS (9 тестов).

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Shared.Contracts/Branches/BranchWorkingHoursDayDto.cs src/AFK4.Platform.Api/Branches/BranchWorkingHours.cs tests/AFK4.Platform.Api.Tests/BranchWorkingHoursTests.cs
git commit -m "feat(branches): working-hours contract + serialize/validate helper"
```

---

### Task 2: Новые колонки филиала + маппинг + миграция

**Files:**
- Modify: `src/AFK4.Platform.Api/Data/BranchEntity.cs`
- Modify: `src/AFK4.Platform.Api/Data/PlatformDbContext.cs:234-246`
- Create: `src/AFK4.Platform.Api/Data/Migrations/<ts>_AddBranchClubProfile.cs` (генерируется)

**Interfaces:**
- Produces: `BranchEntity` c новыми свойствами `Description`, `Address`, `Phone`, `Telegram`, `Website`, `WorkingHoursJson` (все `string?`), `LogoUrl` (`string?`), `LogoMediaId` (`Guid?`). (`PreferredTimeZone`/`PreferredLocale` уже есть — не добавлять.)

- [ ] **Step 1: Добавить свойства в BranchEntity**

В `src/AFK4.Platform.Api/Data/BranchEntity.cs` добавить после существующего `public string City { get; set; }` (сохранить стиль соседних свойств — те же аннотации/дефолты, что у остальных nullable-полей):

```csharp
    public string? Description { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Telegram { get; set; }
    public string? Website { get; set; }
    public string? LogoUrl { get; set; }
    public Guid? LogoMediaId { get; set; }
    public string? WorkingHoursJson { get; set; }
```

- [ ] **Step 2: Замапить колонки в PlatformDbContext**

В `src/AFK4.Platform.Api/Data/PlatformDbContext.cs` внутри блока `modelBuilder.Entity<BranchEntity>(...)` (строки ~234-246), перед `entity.HasIndex(...)`, добавить:

```csharp
            entity.Property(b => b.Description).HasMaxLength(500);
            entity.Property(b => b.Address).HasMaxLength(300);
            entity.Property(b => b.Phone).HasMaxLength(40);
            entity.Property(b => b.Telegram).HasMaxLength(120);
            entity.Property(b => b.Website).HasMaxLength(300);
            entity.Property(b => b.LogoUrl).HasMaxLength(600);
            entity.Property(b => b.WorkingHoursJson).HasColumnType("jsonb");
```

(`LogoMediaId` — `Guid?`, конвенция EF, отдельная конфигурация не нужна.)

- [ ] **Step 3: Собрать проект (обязательно ДО ef, Linux-recipe)**

Run: `dotnet build src/AFK4.Platform.Api`
Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 4: Создать миграцию (--no-build, НЕ подключается к БД)**

Run:
```bash
dotnet ef migrations add AddBranchClubProfile --project src/AFK4.Platform.Api --output-dir Data/Migrations --no-build
```
Expected: созданы `Data/Migrations/<ts>_AddBranchClubProfile.cs` + `.Designer.cs`, обновлён `PlatformDbContextModelSnapshot.cs`. НИКОГДА не запускать `dotnet ef migrations remove` (лезет в Postgres).

- [ ] **Step 5: Проверить содержимое миграции**

Открыть сгенерированный `<ts>_AddBranchClubProfile.cs`: `Up` должен содержать `AddColumn` для `Description`, `Address`, `Phone`, `Telegram`, `Website`, `LogoUrl` (nullable text/varchar), `LogoMediaId` (nullable uuid), `WorkingHoursJson` (nullable jsonb). `Down` — соответствующие `DropColumn`. Все колонки nullable (существующие филиалы не ломаются).

- [ ] **Step 6: Пересобрать — миграция компилируется**

Run: `dotnet build src/AFK4.Platform.Api`
Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.Platform.Api/Data/BranchEntity.cs src/AFK4.Platform.Api/Data/PlatformDbContext.cs src/AFK4.Platform.Api/Data/Migrations/
git commit -m "feat(branches): add club-profile columns + AddBranchClubProfile migration"
```

---

### Task 3: Расширенный профиль — контракты, маппер, эндпоинт, право, тесты

**Files:**
- Modify: `src/AFK4.Shared.Contracts/Branches/BranchProfileDto.cs`
- Modify: `src/AFK4.Shared.Contracts/Branches/UpdateBranchProfileRequest.cs`
- Modify: `src/AFK4.Platform.Api/Endpoints/EndpointHelpers.Dtos.cs:148-156`
- Modify: `src/AFK4.Platform.Api/Endpoints/EndpointHelpers.Validation.cs:177-202`
- Modify: `src/AFK4.Platform.Api/Endpoints/BranchProfileLayoutEndpoints.cs:79-215`
- Create: `tests/AFK4.Platform.Api.Tests/BranchProfileEndpointTests.cs`

**Interfaces:**
- Consumes: `BranchWorkingHours` (Task 1), `BranchWorkingHoursDayDto` (Task 1), новые `BranchEntity`-колонки (Task 2).
- Produces (JSON camelCase на границе API): `BranchProfileDto` и `UpdateBranchProfileRequest` с полями `name, city, description?, address?, phone?, telegram?, website?, logoUrl?, logoMediaId?, timeZone, locale, workingHours[]`.

- [ ] **Step 1: Расширить BranchProfileDto**

Заменить `src/AFK4.Shared.Contracts/Branches/BranchProfileDto.cs`:

```csharp
namespace AFK4.Shared.Contracts.Branches;

public sealed record BranchProfileDto(
    Guid OrganizationId,
    Guid BranchId,
    string Name,
    string City,
    string? Description,
    string? Address,
    string? Phone,
    string? Telegram,
    string? Website,
    string? LogoUrl,
    Guid? LogoMediaId,
    string TimeZone,
    string Locale,
    IReadOnlyList<BranchWorkingHoursDayDto> WorkingHours,
    DateTimeOffset CreatedAtUtc);
```

- [ ] **Step 2: Расширить UpdateBranchProfileRequest**

Заменить `src/AFK4.Shared.Contracts/Branches/UpdateBranchProfileRequest.cs`:

```csharp
namespace AFK4.Shared.Contracts.Branches;

public sealed record UpdateBranchProfileRequest(
    Guid OrganizationId,
    string Name,
    string City,
    string? Description,
    string? Address,
    string? Phone,
    string? Telegram,
    string? Website,
    string? LogoUrl,
    Guid? LogoMediaId,
    string TimeZone,
    string Locale,
    IReadOnlyList<BranchWorkingHoursDayDto> WorkingHours);
```

- [ ] **Step 3: Написать падающие тесты эндпоинта**

Create `tests/AFK4.Platform.Api.Tests/BranchProfileEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using AFK4.Shared.Contracts.Branches;
using AFK4.Shared.Contracts.Identity;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public class BranchProfileEndpointTests
{
    private static UpdateBranchProfileRequest FullRequest(Guid orgId) => new(
        OrganizationId: orgId,
        Name: "AFK4 Центр",
        City: "Душанбе",
        Description: "Лучший клуб",
        Address: "ул. Рудаки, 1",
        Phone: "+992900000000",
        Telegram: "afk4club",
        Website: "https://afk4.net",
        LogoUrl: null,
        LogoMediaId: null,
        TimeZone: "Asia/Dushanbe",
        Locale: "ru",
        WorkingHours: Enumerable.Range(1, 7)
            .Select(d => new BranchWorkingHoursDayDto(d, d == 7, d == 7 ? null : "10:00", d == 7 ? null : "23:00"))
            .ToList());

    [Fact]
    public async Task Patch_WithBranchManager_PersistsAllFields()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.BranchManager);

        var response = await client.PatchAsJsonAsync(
            $"/api/branches/{TestIds.BranchId}/profile", FullRequest(TestIds.OrganizationId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<BranchProfileDto>();
        Assert.NotNull(dto);
        Assert.Equal("AFK4 Центр", dto!.Name);
        Assert.Equal("Лучший клуб", dto.Description);
        Assert.Equal("afk4club", dto.Telegram);
        Assert.Equal("Asia/Dushanbe", dto.TimeZone);
        Assert.Equal(7, dto.WorkingHours.Count);
        Assert.True(dto.WorkingHours.Single(d => d.DayOfWeek == 7).IsClosed);
    }

    [Fact]
    public async Task Get_AfterPatch_ReturnsPersistedProfile()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.BranchManager);

        await client.PatchAsJsonAsync($"/api/branches/{TestIds.BranchId}/profile", FullRequest(TestIds.OrganizationId));
        var dto = await client.GetFromJsonAsync<BranchProfileDto>($"/api/branches/{TestIds.BranchId}/profile");

        Assert.NotNull(dto);
        Assert.Equal("ул. Рудаки, 1", dto!.Address);
        Assert.Equal("23:00", dto.WorkingHours.Single(d => d.DayOfWeek == 1).CloseTime);
    }

    [Fact]
    public async Task Get_DefaultBranch_ReturnsSevenWorkingHourDays()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.BranchManager);

        var dto = await client.GetFromJsonAsync<BranchProfileDto>($"/api/branches/{TestIds.BranchId}/profile");
        Assert.NotNull(dto);
        Assert.Equal(7, dto!.WorkingHours.Count);
    }

    [Fact]
    public async Task Patch_InvalidWorkingHours_ReturnsBadRequest()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.BranchManager);

        var bad = FullRequest(TestIds.OrganizationId) with
        {
            WorkingHours = new[] { new BranchWorkingHoursDayDto(1, false, "22:00", "10:00") }
        };
        var response = await client.PatchAsJsonAsync($"/api/branches/{TestIds.BranchId}/profile", bad);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Patch_WithCashier_ReturnsForbidden()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);

        var response = await client.PatchAsJsonAsync(
            $"/api/branches/{TestIds.BranchId}/profile", FullRequest(TestIds.OrganizationId));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
```

- [ ] **Step 4: Запустить тесты — убедиться, что падают**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~BranchProfileEndpointTests`
Expected: FAIL (компиляция — у DTO/request новые поля не заполнены в маппере/эндпоинте; или 403/500).

- [ ] **Step 5: Обновить маппер ToBranchProfileDto**

В `src/AFK4.Platform.Api/Endpoints/EndpointHelpers.Dtos.cs` заменить тело `ToBranchProfileDto` (строки 148-156):

```csharp
    public static BranchProfileDto ToBranchProfileDto(BranchEntity branch)
    {
        return new BranchProfileDto(
            branch.OrganizationId,
            branch.BranchId,
            branch.Name,
            branch.City,
            branch.Description,
            branch.Address,
            branch.Phone,
            branch.Telegram,
            branch.Website,
            branch.LogoUrl,
            branch.LogoMediaId,
            branch.PreferredTimeZone,
            branch.PreferredLocale,
            AFK4.Platform.Api.Branches.BranchWorkingHours.Deserialize(branch.WorkingHoursJson),
            branch.CreatedAtUtc);
    }
```

- [ ] **Step 6: Обновить валидацию запроса**

В `src/AFK4.Platform.Api/Endpoints/EndpointHelpers.Validation.cs` заменить тело `ValidateUpdateBranchProfileRequest` (строки 177-202):

```csharp
    public static string? ValidateUpdateBranchProfileRequest(UpdateBranchProfileRequest request)
    {
        if (request.OrganizationId == Guid.Empty)
        {
            return "OrganizationId is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Name is required.";
        }

        if (request.Name.Trim().Length > 160)
        {
            return "Name must contain 160 characters or fewer.";
        }

        if (string.IsNullOrWhiteSpace(request.City))
        {
            return "City is required.";
        }

        if (request.City.Trim().Length > 120)
        {
            return "City must contain 120 characters or fewer.";
        }

        if ((request.Description?.Length ?? 0) > 500) return "Description must contain 500 characters or fewer.";
        if ((request.Address?.Length ?? 0) > 300) return "Address must contain 300 characters or fewer.";
        if ((request.Phone?.Length ?? 0) > 40) return "Phone must contain 40 characters or fewer.";
        if ((request.Telegram?.Length ?? 0) > 120) return "Telegram must contain 120 characters or fewer.";
        if ((request.Website?.Length ?? 0) > 300) return "Website must contain 300 characters or fewer.";

        if (string.IsNullOrWhiteSpace(request.TimeZone) || request.TimeZone.Length > 64)
        {
            return "TimeZone is required and must contain 64 characters or fewer.";
        }

        if (string.IsNullOrWhiteSpace(request.Locale) || request.Locale.Length > 8)
        {
            return "Locale is required and must contain 8 characters or fewer.";
        }

        return AFK4.Platform.Api.Branches.BranchWorkingHours.Validate(request.WorkingHours);
    }
```

- [ ] **Step 7: Обновить эндпоинт — право + запись новых полей**

В `src/AFK4.Platform.Api/Endpoints/BranchProfileLayoutEndpoints.cs`:

(a) В GET (строка 88) и PATCH (строка 151) заменить `StaffPermissionNames.ManageLayout` → `StaffPermissionNames.ManageBranchSettings`. (Слои zones/seats ниже по файлу оставить на `ManageLayout` — не трогать.)

(b) В PATCH после блока валидации, заменить присваивание (строки 197-199) на:

```csharp
            branch.Name = request.Name.Trim();
            branch.City = request.City.Trim();
            branch.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            branch.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
            branch.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
            branch.Telegram = string.IsNullOrWhiteSpace(request.Telegram) ? null : request.Telegram.Trim();
            branch.Website = string.IsNullOrWhiteSpace(request.Website) ? null : request.Website.Trim();
            branch.LogoUrl = string.IsNullOrWhiteSpace(request.LogoUrl) ? null : request.LogoUrl.Trim();
            branch.LogoMediaId = request.LogoMediaId;
            branch.PreferredTimeZone = request.TimeZone.Trim();
            branch.PreferredLocale = request.Locale.Trim();
            branch.WorkingHoursJson = AFK4.Platform.Api.Branches.BranchWorkingHours.Serialize(request.WorkingHours);
            await dbContext.SaveChangesAsync(cancellationToken);
```

(Аудит-логи `new { branch.Name, branch.City }` в GET/PATCH оставить как есть — не расширять контактами.)

- [ ] **Step 8: Запустить новые тесты + существующие тесты профиля**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter "FullyQualifiedName~BranchProfileEndpointTests|FullyQualifiedName~PilotSetupEndpointTests"`
Expected: PASS. (Существующий `BranchProfile_WithBranchManagerRole_...` в `PilotSetupEndpointTests` слал старый 3-полевой request — TypeScript тут ни при чём, но C#-компилятор потребует новые поля. **Обновить тот тест**: заменить `new UpdateBranchProfileRequest(OrganizationId, "AFK4 Pilot", "Dushanbe")` на полный конструктор с `Description: null, Address: null, Phone: null, Telegram: null, Website: null, LogoUrl: null, LogoMediaId: null, TimeZone: "Asia/Dushanbe", Locale: "ru", WorkingHours: [полный 7-дневный набор через BranchWorkingHours.Default() эквивалент]`. Проще: `WorkingHours: Enumerable.Range(1,7).Select(d => new BranchWorkingHoursDayDto(d,false,"10:00","22:00")).ToList()`.)

- [ ] **Step 9: Полный прогон бэкенд-тестов (регрессия)**

Run: `dotnet test tests/AFK4.Platform.Api.Tests`
Expected: PASS, 0 failed (было 1417 pass/13 skip + новые).

- [ ] **Step 10: Commit**

```bash
git add src/AFK4.Shared.Contracts/Branches/ src/AFK4.Platform.Api/Endpoints/ tests/AFK4.Platform.Api.Tests/
git commit -m "feat(branches): extend profile endpoint with club fields + working hours, gate on ManageBranchSettings"
```

---

### Task 4: i18n-ключи «Клуб» (ru/en/tg + gen)

**Files:**
- Modify: `/locales/ru.json`, `/locales/en.json`, `/locales/tg.json`
- Regenerate: `packages/i18n/src/messages.ts` (через `bun run gen`)

**Interfaces:**
- Produces: i18n-ключи `op.club.*` (см. таблицу), используемые задачами 5-7.

- [ ] **Step 1: Добавить ключи в три файла локалей**

В `/locales/ru.json`, `/locales/en.json`, `/locales/tg.json` добавить ключи в том же формате/секции, что существующие `op.club.*`/`op.settings.profile.*` (следовать структуре файла — плоские точечные ключи). Значения:

| Ключ | ru | en | tg |
|---|---|---|---|
| `op.club.section.identity` | Лицо для игрока | Player-facing identity | Намуд барои бозингар |
| `op.club.section.contacts` | Адрес и контакты | Address & contacts | Суроға ва тамос |
| `op.club.section.hours` | Часы работы | Working hours | Соатҳои корӣ |
| `op.club.section.settings` | Настройки филиала | Branch settings | Танзимоти филиал |
| `op.club.section.preview` | Как видит игрок | Player preview | Чӣ тавр бозингар мебинад |
| `op.club.field.description` | Описание | Description | Тавсиф |
| `op.club.field.logo` | Логотип | Logo | Тамға |
| `op.club.field.address` | Адрес | Address | Суроға |
| `op.club.field.phone` | Телефон | Phone | Рақами телефон |
| `op.club.field.telegram` | Telegram | Telegram | Telegram |
| `op.club.field.website` | Сайт | Website | Вебсайт |
| `op.club.field.timezone` | Часовой пояс | Time zone | Минтақаи вақт |
| `op.club.field.locale` | Язык по умолчанию | Default language | Забони пешфарз |
| `op.club.logo.hint` | PNG, JPEG или WebP, до 10 МБ | PNG, JPEG or WebP, up to 10 MB | PNG, JPEG ё WebP, то 10 МБ |
| `op.club.hours.closed` | Выходной | Closed | Рӯзи истироҳат |
| `op.club.hours.open` | Открытие | Opens | Оғоз |
| `op.club.hours.close` | Закрытие | Closes | Анҷом |
| `op.club.weekday.1` | Понедельник | Monday | Душанбе |
| `op.club.weekday.2` | Вторник | Tuesday | Сешанбе |
| `op.club.weekday.3` | Среда | Wednesday | Чоршанбе |
| `op.club.weekday.4` | Четверг | Thursday | Панҷшанбе |
| `op.club.weekday.5` | Пятница | Friday | Ҷумъа |
| `op.club.weekday.6` | Суббота | Saturday | Шанбе |
| `op.club.weekday.7` | Воскресенье | Sunday | Якшанбе |
| `op.club.locale.ru` | Русский | Russian | Русӣ |
| `op.club.locale.en` | Английский | English | Англисӣ |
| `op.club.locale.tg` | Таджикский | Tajik | Тоҷикӣ |
| `op.club.preview.hint` | Так клуб выглядит в приложении игрока | How the club appears in the player app | Клуб дар барномаи бозингар чунин намоён мешавад |
| `op.club.preview.noLogo` | Логотип не задан | No logo | Тамға таъин нашудааст |
| `op.club.preview.today` | Сегодня | Today | Имрӯз |
| `op.club.preview.closedToday` | Сегодня выходной | Closed today | Имрӯз рӯзи истироҳат |

- [ ] **Step 2: Занести Telegram в whitelist гварда tg===ru**

Ключ `op.club.field.telegram` имеет одинаковое значение `Telegram` во всех локалях (бренд). Открыть guard-тест (`packages/i18n/**` тест на `tg===ru`, искать по `whitelist`/`loanword`) и добавить значение `Telegram` (или ключ `op.club.field.telegram`) в whitelist, как уже сделано для других брендов/loanword'ов.

- [ ] **Step 3: Регенерировать messages.ts**

Run: `cd packages/i18n && bun run gen`
Expected: `packages/i18n/src/messages.ts` обновлён новыми ключами.

- [ ] **Step 4: Прогнать i18n-гварды**

Run: `cd packages/i18n && bun test`
Expected: PASS — паритет ru/en/tg, `tg !== ru` (Telegram в whitelist), keys-exist.

- [ ] **Step 5: Commit**

```bash
git add locales/ packages/i18n/
git commit -m "i18n(operator): club profile keys (identity/contacts/hours/settings/preview)"
```

---

### Task 5: TS-модель часов + WorkingHoursEditor

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/api/clients/settings.ts:47-51`
- Create: `src/AFK4.Operator.App.Web/src/settings/club/workingHours.ts`
- Create: `src/AFK4.Operator.App.Web/src/settings/club/workingHours.test.ts`
- Create: `src/AFK4.Operator.App.Web/src/settings/club/WorkingHoursEditor.tsx`
- Create: `src/AFK4.Operator.App.Web/src/settings/club/WorkingHoursEditor.test.tsx`

**Interfaces:**
- Produces:
  - `interface BranchWorkingHoursDay { dayOfWeek: number; isClosed: boolean; openTime: string | null; closeTime: string | null }` (в `settings.ts`, экспорт).
  - `UpdateBranchProfileRequest` расширен полями `description/address/phone/telegram/website: string | null`, `logoUrl/logoMediaId: string | null`, `timeZone/locale: string`, `workingHours: BranchWorkingHoursDay[]`.
  - `workingHours.ts`: `defaultWorkingHours(): BranchWorkingHoursDay[]`, `normalizeWorkingHours(raw: unknown): BranchWorkingHoursDay[]`, `WEEKDAY_KEY: Record<number, string>`.
  - `WorkingHoursEditor` — props `{ value: BranchWorkingHoursDay[]; onChange: (days: BranchWorkingHoursDay[]) => void; disabled?: boolean }`.

- [ ] **Step 1: Расширить TS-типы в settings.ts**

В `src/AFK4.Operator.App.Web/src/api/clients/settings.ts` добавить перед `UpdateBranchProfileRequest` (строка 47) экспорт типа и расширить сам request:

```ts
export interface BranchWorkingHoursDay {
  dayOfWeek: number; // 1=Пн … 7=Вс
  isClosed: boolean;
  openTime: string | null;
  closeTime: string | null;
}

export interface UpdateBranchProfileRequest extends Record<string, unknown> {
  organizationId: Guid;
  name: string;
  city: string;
  description: string | null;
  address: string | null;
  phone: string | null;
  telegram: string | null;
  website: string | null;
  logoUrl: string | null;
  logoMediaId: string | null;
  timeZone: string;
  locale: string;
  workingHours: BranchWorkingHoursDay[];
}
```

- [ ] **Step 2: Написать падающий тест модели часов**

Create `src/AFK4.Operator.App.Web/src/settings/club/workingHours.test.ts`:

```ts
import { describe, expect, it } from 'bun:test';
import { defaultWorkingHours, normalizeWorkingHours } from './workingHours';

describe('workingHours model', () => {
  it('default has 7 days Mon..Sun, all open', () => {
    const days = defaultWorkingHours();
    expect(days.map((d) => d.dayOfWeek)).toEqual([1, 2, 3, 4, 5, 6, 7]);
    expect(days.every((d) => !d.isClosed)).toBe(true);
  });

  it('normalize null/undefined returns default 7 days', () => {
    expect(normalizeWorkingHours(undefined)).toHaveLength(7);
    expect(normalizeWorkingHours(null)).toHaveLength(7);
  });

  it('normalize fills missing days and keeps provided ones', () => {
    const days = normalizeWorkingHours([{ dayOfWeek: 3, isClosed: true, openTime: null, closeTime: null }]);
    expect(days).toHaveLength(7);
    expect(days.find((d) => d.dayOfWeek === 3)?.isClosed).toBe(true);
    expect(days.find((d) => d.dayOfWeek === 1)?.isClosed).toBe(false);
  });
});
```

- [ ] **Step 3: Запустить тест — падает**

Run: `cd src/AFK4.Operator.App.Web && bun test src/settings/club/workingHours.test.ts`
Expected: FAIL — модуль не существует.

- [ ] **Step 4: Реализовать модель часов**

Create `src/AFK4.Operator.App.Web/src/settings/club/workingHours.ts`:

```ts
import type { BranchWorkingHoursDay } from '../../api/clients/settings';

const DEFAULT_OPEN = '10:00';
const DEFAULT_CLOSE = '22:00';

// i18n-ключи названий дней (1=Пн … 7=Вс), см. Task 4.
export const WEEKDAY_KEY: Record<number, string> = {
  1: 'op.club.weekday.1',
  2: 'op.club.weekday.2',
  3: 'op.club.weekday.3',
  4: 'op.club.weekday.4',
  5: 'op.club.weekday.5',
  6: 'op.club.weekday.6',
  7: 'op.club.weekday.7'
};

export function defaultWorkingHours(): BranchWorkingHoursDay[] {
  return [1, 2, 3, 4, 5, 6, 7].map((dayOfWeek) => ({
    dayOfWeek,
    isClosed: false,
    openTime: DEFAULT_OPEN,
    closeTime: DEFAULT_CLOSE
  }));
}

// Всегда нормализует к 7 дням 1..7: провайдер (сервер/state) мог прислать частичный/пустой набор.
export function normalizeWorkingHours(raw: unknown): BranchWorkingHoursDay[] {
  const byDay = new Map<number, BranchWorkingHoursDay>();
  if (Array.isArray(raw)) {
    for (const item of raw as BranchWorkingHoursDay[]) {
      if (item && typeof item.dayOfWeek === 'number' && item.dayOfWeek >= 1 && item.dayOfWeek <= 7) {
        byDay.set(item.dayOfWeek, {
          dayOfWeek: item.dayOfWeek,
          isClosed: Boolean(item.isClosed),
          openTime: item.openTime ?? DEFAULT_OPEN,
          closeTime: item.closeTime ?? DEFAULT_CLOSE
        });
      }
    }
  }
  return [1, 2, 3, 4, 5, 6, 7].map(
    (dayOfWeek) =>
      byDay.get(dayOfWeek) ?? { dayOfWeek, isClosed: false, openTime: DEFAULT_OPEN, closeTime: DEFAULT_CLOSE }
  );
}
```

- [ ] **Step 5: Запустить тест — проходит**

Run: `cd src/AFK4.Operator.App.Web && bun test src/settings/club/workingHours.test.ts`
Expected: PASS.

- [ ] **Step 6: Написать падающий тест редактора**

Create `src/AFK4.Operator.App.Web/src/settings/club/WorkingHoursEditor.test.tsx`:

```tsx
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { WorkingHoursEditor } from './WorkingHoursEditor';
import { defaultWorkingHours } from './workingHours';

afterEach(cleanup);

describe('WorkingHoursEditor', () => {
  it('renders 7 day rows', () => {
    render(
      <I18nProvider initialLocale="ru">
        <WorkingHoursEditor value={defaultWorkingHours()} onChange={() => {}} />
      </I18nProvider>
    );
    expect(screen.getByText('Понедельник')).toBeInTheDocument();
    expect(screen.getByText('Воскресенье')).toBeInTheDocument();
  });

  it('toggling closed emits updated day', () => {
    const onChange = mock((_: unknown) => {});
    render(
      <I18nProvider initialLocale="ru">
        <WorkingHoursEditor value={defaultWorkingHours()} onChange={onChange} />
      </I18nProvider>
    );
    const checkboxes = screen.getAllByRole('checkbox');
    fireEvent.click(checkboxes[0]);
    expect(onChange).toHaveBeenCalled();
    const next = onChange.mock.calls[0][0] as ReturnType<typeof defaultWorkingHours>;
    expect(next[0].isClosed).toBe(true);
  });
});
```

- [ ] **Step 7: Запустить — падает**

Run: `cd src/AFK4.Operator.App.Web && bun test src/settings/club/WorkingHoursEditor.test.tsx`
Expected: FAIL — компонент не существует.

- [ ] **Step 8: Реализовать редактор**

Create `src/AFK4.Operator.App.Web/src/settings/club/WorkingHoursEditor.tsx`:

```tsx
import { useI18n } from '@afk4/i18n';
import type { BranchWorkingHoursDay } from '../../api/clients/settings';
import { WEEKDAY_KEY } from './workingHours';

interface WorkingHoursEditorProps {
  value: BranchWorkingHoursDay[];
  onChange: (days: BranchWorkingHoursDay[]) => void;
  disabled?: boolean;
}

export function WorkingHoursEditor({ value, onChange, disabled }: WorkingHoursEditorProps) {
  const { t } = useI18n();

  const patchDay = (dayOfWeek: number, patch: Partial<BranchWorkingHoursDay>) => {
    onChange(value.map((day) => (day.dayOfWeek === dayOfWeek ? { ...day, ...patch } : day)));
  };

  return (
    <div className="club-hours">
      {value.map((day) => (
        <div className="club-hours-row" key={day.dayOfWeek}>
          <span className="club-hours-day">{t(WEEKDAY_KEY[day.dayOfWeek])}</span>
          <label className="mgmt-check">
            <input
              type="checkbox"
              checked={day.isClosed}
              disabled={disabled}
              onChange={(event) => patchDay(day.dayOfWeek, { isClosed: event.currentTarget.checked })}
            />
            {t('op.club.hours.closed')}
          </label>
          <label className="club-hours-time">
            <span className="club-hours-time-label">{t('op.club.hours.open')}</span>
            <input
              type="time"
              value={day.openTime ?? ''}
              disabled={disabled || day.isClosed}
              onChange={(event) => patchDay(day.dayOfWeek, { openTime: event.currentTarget.value })}
            />
          </label>
          <label className="club-hours-time">
            <span className="club-hours-time-label">{t('op.club.hours.close')}</span>
            <input
              type="time"
              value={day.closeTime ?? ''}
              disabled={disabled || day.isClosed}
              onChange={(event) => patchDay(day.dayOfWeek, { closeTime: event.currentTarget.value })}
            />
          </label>
        </div>
      ))}
    </div>
  );
}
```

- [ ] **Step 9: Запустить оба теста — проходят**

Run: `cd src/AFK4.Operator.App.Web && bun test src/settings/club/`
Expected: PASS.

- [ ] **Step 10: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/api/clients/settings.ts src/AFK4.Operator.App.Web/src/settings/club/workingHours.ts src/AFK4.Operator.App.Web/src/settings/club/workingHours.test.ts src/AFK4.Operator.App.Web/src/settings/club/WorkingHoursEditor.tsx src/AFK4.Operator.App.Web/src/settings/club/WorkingHoursEditor.test.tsx
git commit -m "feat(operator): working-hours TS model + 7-day editor"
```

---

### Task 6: ClubProfileFields (презентационные блоки + логотип)

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/settings/club/ClubProfileFields.tsx`
- Create: `src/AFK4.Operator.App.Web/src/settings/club/ClubProfileFields.test.tsx`

**Interfaces:**
- Consumes: `MediaUpload` (`src/components/MediaUpload.tsx`), `MediaPurposeNames.BranchLogo`, `WorkingHoursEditor` (Task 5), `BranchWorkingHoursDay` (Task 5).
- Produces: `interface ClubProfileForm { name; city; description; address; phone; telegram; website; logoUrl: string|null; logoMediaId: string|null; timeZone; locale; workingHours: BranchWorkingHoursDay[] }` (экспорт) + `ClubProfileFields` — props `{ form: ClubProfileForm; currencyCode: string; backend: OperatorBackendContext; disabled?: boolean; onField: <K extends keyof ClubProfileForm>(key: K, value: ClubProfileForm[K]) => void }`.

- [ ] **Step 1: Определить тип формы + написать падающий тест**

Create `src/AFK4.Operator.App.Web/src/settings/club/ClubProfileFields.test.tsx`:

```tsx
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ClubProfileFields, type ClubProfileForm } from './ClubProfileFields';
import { defaultWorkingHours } from './workingHours';

const backend = { config: { platformBaseUrl: 'http://x' }, session: { accessToken: 't', organizationId: 'o1' }, branchId: 'b1' } as never;

const form: ClubProfileForm = {
  name: 'AFK4 Центр',
  city: 'Душанбе',
  description: '',
  address: '',
  phone: '',
  telegram: '',
  website: '',
  logoUrl: null,
  logoMediaId: null,
  timeZone: 'Asia/Dushanbe',
  locale: 'ru',
  workingHours: defaultWorkingHours()
};

afterEach(cleanup);

describe('ClubProfileFields', () => {
  it('renders name value and section titles', () => {
    render(
      <I18nProvider initialLocale="ru">
        <ClubProfileFields form={form} currencyCode="TJS" backend={backend} onField={() => {}} />
      </I18nProvider>
    );
    expect(screen.getByDisplayValue('AFK4 Центр')).toBeInTheDocument();
    expect(screen.getByText('Адрес и контакты')).toBeInTheDocument();
    expect(screen.getByText('TJS')).toBeInTheDocument();
  });

  it('editing name calls onField', () => {
    const onField = mock((_k: unknown, _v: unknown) => {});
    render(
      <I18nProvider initialLocale="ru">
        <ClubProfileFields form={form} currencyCode="TJS" backend={backend} onField={onField} />
      </I18nProvider>
    );
    fireEvent.change(screen.getByDisplayValue('AFK4 Центр'), { target: { value: 'AFK4 X' } });
    expect(onField).toHaveBeenCalledWith('name', 'AFK4 X');
  });
});
```

- [ ] **Step 2: Запустить — падает**

Run: `cd src/AFK4.Operator.App.Web && bun test src/settings/club/ClubProfileFields.test.tsx`
Expected: FAIL — компонент не существует.

- [ ] **Step 3: Реализовать ClubProfileFields**

Create `src/AFK4.Operator.App.Web/src/settings/club/ClubProfileFields.tsx`:

```tsx
import { useI18n } from '@afk4/i18n';
import { MediaUpload } from '../../components/MediaUpload';
import { MediaPurposeNames } from '@afk4/contracts';
import type { OperatorBackendContext } from '../../operatorTypes';
import type { BranchWorkingHoursDay } from '../../api/clients/settings';
import { WorkingHoursEditor } from './WorkingHoursEditor';

export interface ClubProfileForm {
  name: string;
  city: string;
  description: string;
  address: string;
  phone: string;
  telegram: string;
  website: string;
  logoUrl: string | null;
  logoMediaId: string | null;
  timeZone: string;
  locale: string;
  workingHours: BranchWorkingHoursDay[];
}

const TIME_ZONES = ['Asia/Dushanbe', 'Asia/Tashkent', 'Asia/Almaty', 'Asia/Bishkek', 'Europe/Moscow', 'Asia/Yekaterinburg'];
const LOCALES: Array<{ value: string; key: string }> = [
  { value: 'ru', key: 'op.club.locale.ru' },
  { value: 'tg', key: 'op.club.locale.tg' },
  { value: 'en', key: 'op.club.locale.en' }
];

interface ClubProfileFieldsProps {
  form: ClubProfileForm;
  currencyCode: string;
  backend: OperatorBackendContext;
  disabled?: boolean;
  onField: <K extends keyof ClubProfileForm>(key: K, value: ClubProfileForm[K]) => void;
}

export function ClubProfileFields({ form, currencyCode, backend, disabled, onField }: ClubProfileFieldsProps) {
  const { t } = useI18n();

  return (
    <div className="mgmt-form">
      <div className="mgmt-section-title"><span>{t('op.club.section.identity')}</span></div>
      <div className="mgmt-form-grid">
        <label>{t('op.settings.profile.clubName')}
          <input value={form.name} disabled={disabled} onChange={(e) => onField('name', e.currentTarget.value)} />
        </label>
        <label className="mgmt-form-wide">{t('op.club.field.description')}
          <input value={form.description} disabled={disabled} onChange={(e) => onField('description', e.currentTarget.value)} />
        </label>
      </div>
      <label className="club-logo-field">{t('op.club.field.logo')}
        <MediaUpload
          value={form.logoUrl}
          purpose={MediaPurposeNames.BranchLogo}
          branchId={backend.branchId}
          backend={backend}
          disabled={disabled}
          onChange={(media) => {
            onField('logoUrl', media?.url ?? null);
            onField('logoMediaId', media?.mediaId ?? null);
          }}
        />
        <span className="media-upload-hint">{t('op.club.logo.hint')}</span>
      </label>

      <div className="mgmt-section-title"><span>{t('op.club.section.contacts')}</span></div>
      <div className="mgmt-form-grid">
        <label>{t('op.club.field.address')}
          <input value={form.address} disabled={disabled} onChange={(e) => onField('address', e.currentTarget.value)} />
        </label>
        <label>{t('op.settings.profile.city')}
          <input value={form.city} disabled={disabled} onChange={(e) => onField('city', e.currentTarget.value)} />
        </label>
        <label>{t('op.club.field.phone')}
          <input value={form.phone} disabled={disabled} onChange={(e) => onField('phone', e.currentTarget.value)} />
        </label>
        <label>{t('op.club.field.telegram')}
          <input value={form.telegram} disabled={disabled} onChange={(e) => onField('telegram', e.currentTarget.value)} />
        </label>
        <label>{t('op.club.field.website')}
          <input value={form.website} disabled={disabled} onChange={(e) => onField('website', e.currentTarget.value)} />
        </label>
      </div>

      <div className="mgmt-section-title"><span>{t('op.club.section.hours')}</span></div>
      <WorkingHoursEditor value={form.workingHours} disabled={disabled} onChange={(days) => onField('workingHours', days)} />

      <div className="mgmt-section-title"><span>{t('op.club.section.settings')}</span></div>
      <div className="mgmt-form-grid">
        <label>{t('op.club.field.timezone')}
          <select value={form.timeZone} disabled={disabled} onChange={(e) => onField('timeZone', e.currentTarget.value)}>
            {TIME_ZONES.map((tz) => <option key={tz} value={tz}>{tz}</option>)}
          </select>
        </label>
        <label>{t('op.club.field.locale')}
          <select value={form.locale} disabled={disabled} onChange={(e) => onField('locale', e.currentTarget.value)}>
            {LOCALES.map((l) => <option key={l.value} value={l.value}>{t(l.key)}</option>)}
          </select>
        </label>
      </div>
      <div className="mgmt-meta-grid">
        <div className="mgmt-meta-row">
          <span className="mgmt-meta-label">{t('op.settings.profile.currency')}</span>
          <span className="mgmt-meta-value">{currencyCode}</span>
        </div>
      </div>
    </div>
  );
}
```

**⚠ Импорт `MediaPurposeNames`:** проверить фактический экспорт — `MediaPurposeNames.BranchLogo` определён в `src/AFK4.Shared.Contracts/Media/MediaPurposeNames.cs`; на фронте он приходит через пакет контрактов (искать `MediaPurposeNames` в `src/AFK4.Operator.App.Web/src` — как его импортирует `MediaUpload.tsx`/media-клиент). Использовать тот же путь импорта, что и существующий код; если фронт не реэкспортит — использовать строковый литерал `'branch-logo'` (значение `MediaPurposeNames.BranchLogo`), но приоритет — существующий импорт.

- [ ] **Step 4: Запустить тест — проходит**

Run: `cd src/AFK4.Operator.App.Web && bun test src/settings/club/ClubProfileFields.test.tsx`
Expected: PASS. Если падает на импорте `MediaPurposeNames` — исправить путь импорта по факту (см. ⚠ выше), затем повторить.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/settings/club/ClubProfileFields.tsx src/AFK4.Operator.App.Web/src/settings/club/ClubProfileFields.test.tsx
git commit -m "feat(operator): club profile fields (identity/logo/contacts/hours/settings)"
```

---

### Task 7: Переписать ClubDestination + превью игрока + CSS

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/settings/club/ClubPlayerPreview.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/management/destinations/ClubDestination.tsx` (переписать)
- Modify: `src/AFK4.Operator.App.Web/src/management/destinations/ClubDestination.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/styles/23-management-crud.css`

**Interfaces:**
- Consumes: `ClubProfileFields` + `ClubProfileForm` (Task 6), `ClubPlayerPreview`, `normalizeWorkingHours`/`defaultWorkingHours`/`WEEKDAY_KEY` (Task 5), `settings.getBranchProfile`/`updateBranchProfile` (существуют), расширенный `UpdateBranchProfileRequest` (Task 5).

- [ ] **Step 1: Реализовать ClubPlayerPreview**

Create `src/AFK4.Operator.App.Web/src/settings/club/ClubPlayerPreview.tsx`:

```tsx
import { useI18n } from '@afk4/i18n';
import type { ClubProfileForm } from './ClubProfileFields';

// «Как видит игрок»: управление вниманием — оператор сразу видит эффект правок.
export function ClubPlayerPreview({ form }: { form: ClubProfileForm }) {
  const { t } = useI18n();
  // JS getDay(): 0=Вс..6=Сб → в ISO 1=Пн..7=Вс. Без Date.now() в тестах: берём new Date() в рантайме UI.
  const isoToday = ((new Date().getDay() + 6) % 7) + 1;
  const today = form.workingHours.find((d) => d.dayOfWeek === isoToday);

  return (
    <aside className="club-preview">
      <div className="mgmt-section-title"><span>{t('op.club.section.preview')}</span></div>
      <div className="club-preview-card">
        {form.logoUrl
          ? <img className="club-preview-logo" src={form.logoUrl} alt="" />
          : <div className="club-preview-logo club-preview-logo--empty">{t('op.club.preview.noLogo')}</div>}
        <div className="club-preview-name">{form.name}</div>
        {form.description && <div className="club-preview-desc">{form.description}</div>}
        {(form.address || form.city) && (
          <div className="club-preview-address">{[form.address, form.city].filter(Boolean).join(', ')}</div>
        )}
        <div className="club-preview-today">
          {today?.isClosed || !today
            ? t('op.club.preview.closedToday')
            : `${t('op.club.preview.today')}: ${today.openTime}–${today.closeTime}`}
        </div>
        {form.phone && <div className="club-preview-contact">{form.phone}</div>}
        {form.telegram && <div className="club-preview-contact">{form.telegram}</div>}
      </div>
      <p className="club-preview-hint">{t('op.club.preview.hint')}</p>
    </aside>
  );
}
```

- [ ] **Step 2: Переписать ClubDestination**

Заменить `src/AFK4.Operator.App.Web/src/management/destinations/ClubDestination.tsx`:

```tsx
import { useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen, type SaveState } from '../ManagementScreen';
import { ClubProfileFields, type ClubProfileForm } from '../../settings/club/ClubProfileFields';
import { ClubPlayerPreview } from '../../settings/club/ClubPlayerPreview';
import { normalizeWorkingHours } from '../../settings/club/workingHours';
import { projectOperatorError } from '../../apiErrors';
import {
  createAuthenticatedOperatorClients,
  emptyFeedback,
  readString,
  triggerFeedback
} from '../../operatorHelpers';
import { useFeedbackToasts } from '../../useFeedbackToasts';
import type { BranchProfileDto, UpdateBranchProfileRequest } from '../../api/clients/settings';
import type { Feedback } from '../../operatorTypes';
import type { DestinationProps } from './types';

const emptyForm: ClubProfileForm = {
  name: 'AFK4', city: 'Dushanbe', description: '', address: '', phone: '', telegram: '', website: '',
  logoUrl: null, logoMediaId: null, timeZone: 'Asia/Dushanbe', locale: 'ru', workingHours: normalizeWorkingHours(null)
};

const blankToNull = (value: string): string | null => (value.trim() === '' ? null : value.trim());

// Клуб: полный профиль филиала (лицо игрока + контакты + часы + настройки). Название — человекочитаемое,
// НИКОГДА не UUID. Гейт раздела — manageBranchSettings (managementNav); эндпоинт profile — то же право.
export function ClubDestination({ backend, currencyCode, onDirtyChange }: DestinationProps) {
  const { t } = useI18n();
  const [form, setForm] = useState<ClubProfileForm>(emptyForm);
  const [dirty, setDirty] = useState(false);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  useFeedbackToasts(feedback);

  useEffect(() => {
    if (backend === null) return undefined;
    let active = true;
    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    clients.settings.getBranchProfile(backend.branchId)
      .then((profile) => {
        if (!active) return;
        setForm({
          name: readString(profile, 'name', 'AFK4'),
          city: readString(profile, 'city', ''),
          description: readString(profile, 'description', ''),
          address: readString(profile, 'address', ''),
          phone: readString(profile, 'phone', ''),
          telegram: readString(profile, 'telegram', ''),
          website: readString(profile, 'website', ''),
          logoUrl: (profile.logoUrl as string | null) ?? null,
          logoMediaId: (profile.logoMediaId as string | null) ?? null,
          timeZone: readString(profile, 'timeZone', 'Asia/Dushanbe'),
          locale: readString(profile, 'locale', 'ru'),
          workingHours: normalizeWorkingHours(profile.workingHours)
        });
        setDirty(false);
      })
      .catch((error) => {
        if (!active) return;
        setFeedback({ label: t('op.settings.profile.loadFeedbackLabel'), state: 'failed', detail: projectOperatorError(error, t).detail });
      });
    return () => { active = false; };
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken]);

  useEffect(() => { onDirtyChange?.(dirty); }, [dirty, onDirtyChange]);

  const onField = <K extends keyof ClubProfileForm>(key: K, value: ClubProfileForm[K]) => {
    setForm((prev) => ({ ...prev, [key]: value }));
    setDirty(true);
    setSaved(false);
  };

  const save = async () => {
    if (backend === null) return;
    if (!form.name.trim() || !form.city.trim()) {
      triggerFeedback(setFeedback, t('op.settings.profile.feedbackLabel'), 'failed', t('op.settings.profile.errorRequiredFields'));
      return;
    }
    setSaving(true);
    setFeedback({ label: t('op.settings.profile.feedbackLabel'), state: 'pending' });
    try {
      const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
      const request: UpdateBranchProfileRequest = {
        organizationId: backend.session.organizationId,
        name: form.name.trim(),
        city: form.city.trim(),
        description: blankToNull(form.description),
        address: blankToNull(form.address),
        phone: blankToNull(form.phone),
        telegram: blankToNull(form.telegram),
        website: blankToNull(form.website),
        logoUrl: form.logoUrl,
        logoMediaId: form.logoMediaId,
        timeZone: form.timeZone,
        locale: form.locale,
        workingHours: form.workingHours
      };
      const profile: BranchProfileDto = await clients.settings.updateBranchProfile(backend.branchId, request);
      setForm((prev) => ({
        ...prev,
        name: readString(profile, 'name', prev.name),
        city: readString(profile, 'city', prev.city),
        workingHours: normalizeWorkingHours(profile.workingHours)
      }));
      setDirty(false);
      setSaved(true);
      setFeedback({ label: t('op.settings.profile.feedbackLabel'), state: 'confirmed' });
    } catch (error) {
      setFeedback({ label: t('op.settings.profile.feedbackLabel'), state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setSaving(false);
    }
  };

  const saveState: SaveState = saving ? 'saving' : dirty ? 'dirty' : saved ? 'saved' : 'clean';

  return (
    <ManagementScreen
      title={t('op.management.dest.club')}
      subtitle={t('op.management.dest.club.subtitle')}
      save={{ state: saveState, onSave: () => void save(), disabled: backend === null }}
    >
      <div className="club-profile-layout">
        <div className="management-panel">
          {backend !== null && (
            <ClubProfileFields form={form} currencyCode={currencyCode} backend={backend} onField={onField} />
          )}
        </div>
        <ClubPlayerPreview form={form} />
      </div>
    </ManagementScreen>
  );
}
```

- [ ] **Step 3: Обновить тест ClubDestination**

Заменить `src/AFK4.Operator.App.Web/src/management/destinations/ClubDestination.test.tsx`. Мок `getBranchProfile` теперь отдаёт расширенный профиль; проверяем: рендер имени, наличие превью, что валюта — мета-значение:

```tsx
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, render, screen } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '../../operatorToast';
import type { BranchProfileDto } from '../../api/clients/settings';

const getBranchProfile = mock(async (): Promise<BranchProfileDto> => ({
  name: 'AFK4 Центр',
  city: 'Душанбе',
  timeZone: 'Asia/Dushanbe',
  locale: 'ru',
  workingHours: [1, 2, 3, 4, 5, 6, 7].map((d) => ({ dayOfWeek: d, isClosed: false, openTime: '10:00', closeTime: '22:00' }))
}));
const actual = (globalThis as Record<string, unknown>).__afk4RealOperatorHelpers as Record<string, unknown>;
mock.module('../../operatorHelpers', () => ({
  ...actual,
  createAuthenticatedOperatorClients: () => ({
    settings: {
      getBranchProfile,
      updateBranchProfile: mock(async (_b: string, request: unknown): Promise<BranchProfileDto> => request as BranchProfileDto)
    }
  })
}));

const { ClubDestination } = await import('./ClubDestination');
const backend = { config: { platformBaseUrl: 'http://x' }, session: { accessToken: 't', organizationId: 'o1' }, branchId: 'b1' } as never;

afterEach(() => { getBranchProfile.mockClear(); cleanup(); });

describe('ClubDestination', () => {
  it('renders full club profile with player preview', async () => {
    const { container } = render(
      <I18nProvider initialLocale="ru">
        <ToastProvider>
          <ClubDestination backend={backend} session={{ permissions: [], organizationId: 'o1' } as never} currencyCode="TJS" />
        </ToastProvider>
      </I18nProvider>
    );
    expect(await screen.findByDisplayValue('AFK4 Центр')).toBeInTheDocument();
    expect(container.querySelector('.club-preview')).not.toBeNull();
    expect(container.querySelector('.mgmt-meta-value')).not.toBeNull();
    expect(screen.getByText('TJS')).toBeInTheDocument();
    // 7 дней часов работы
    expect(container.querySelectorAll('.club-hours-row')).toHaveLength(7);
  });
});
```

- [ ] **Step 4: Добавить CSS раскладки**

В конец `src/AFK4.Operator.App.Web/src/styles/23-management-crud.css` добавить:

```css
/* Клуб: профиль + живой предпросмотр «как видит игрок» */
.club-profile-layout {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(0, 22rem);
  gap: 1.5rem;
  align-items: start;
}
@media (max-width: 960px) {
  .club-profile-layout { grid-template-columns: 1fr; }
}
.club-preview { position: sticky; top: 1rem; display: flex; flex-direction: column; gap: 0.75rem; }
.club-preview-card {
  display: flex; flex-direction: column; gap: 0.5rem;
  padding: 1rem; border-radius: 14px;
  background: var(--surface-card, #fff); box-shadow: var(--shadow-card);
}
.club-preview-logo { width: 64px; height: 64px; border-radius: 12px; object-fit: cover; }
.club-preview-logo--empty {
  display: flex; align-items: center; justify-content: center;
  font-size: 0.7rem; text-align: center; opacity: 0.6;
  background: var(--surface-sunken, rgba(0,0,0,0.05));
}
.club-preview-name { font-weight: 600; font-size: 1.05rem; }
.club-preview-desc, .club-preview-address, .club-preview-contact { font-size: 0.85rem; opacity: 0.85; }
.club-preview-today { font-size: 0.85rem; font-weight: 500; }
.club-preview-hint { font-size: 0.75rem; opacity: 0.6; }

.club-logo-field { display: flex; flex-direction: column; gap: 0.5rem; margin: 0.75rem 0; }
.club-hours { display: flex; flex-direction: column; gap: 0.25rem; }
.club-hours-row {
  display: grid; grid-template-columns: 8rem auto 1fr 1fr; gap: 0.75rem;
  align-items: center; padding: 0.35rem 0;
}
.club-hours-day { font-weight: 500; }
.club-hours-time { display: flex; align-items: center; gap: 0.4rem; }
.club-hours-time input[type="time"], .club-hours-row .mgmt-check input { min-height: 44px; }
@media (max-width: 560px) {
  .club-hours-row { grid-template-columns: 1fr 1fr; }
}
```

(Если каких-то CSS-переменных `--surface-card`/`--shadow-card`/`--surface-sunken` нет — использовать те, что реально определены в `@afk4/tokens`/соседних стилях; fallback-значения в `var(...)` уже заданы.)

- [ ] **Step 5: Запустить тесты Клуба**

Run: `cd src/AFK4.Operator.App.Web && bun test src/management/destinations/ClubDestination.test.tsx src/settings/club/`
Expected: PASS.

- [ ] **Step 6: Полный прогон + сборка фронта (build тайпчекает тесты!)**

Run: `cd src/AFK4.Operator.App.Web && bun test && bun run build`
Expected: `bun test` — 0 fail; `bun run build` — успешно (tsc-b + vite, включая тайпчек тест-файлов).

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/settings/club/ClubPlayerPreview.tsx src/AFK4.Operator.App.Web/src/management/destinations/ClubDestination.tsx src/AFK4.Operator.App.Web/src/management/destinations/ClubDestination.test.tsx src/AFK4.Operator.App.Web/src/styles/23-management-crud.css
git commit -m "feat(operator): rebuild Клуб as full club profile with player preview"
```

---

## Self-Review

**Spec coverage (против decisions-дока, п. «Под-проект 2 — Клуб»):**
- Гейт `manageBranchSettings` на весь раздел → Task 3 (эндпоинт переведён; фронт-гейт уже был). ✔
- Блок 1 «Лицо для игрока» (название/описание/логотип) → Task 6 (identity-секция + MediaUpload). ✔
- Блок 2 «Адрес и контакты» (город/адрес/телефон/telegram/сайт) → Task 2 колонки + Task 3 контракт + Task 6 UI. ✔
- Блок 3 «Часы работы» 7-дневный редактор, JSON-хранение → Task 1 (модель/сериализация) + Task 5 (редактор). ✔
- Блок 4 «Настройки филиала» (tz/язык/валюта-RO) → Task 6 (tz/locale select + currency read-only). ✔ (tz/locale — существующие колонки, подняты в профиль.)
- Логотип через под-проект 1 + `logoMediaId` рядом с `logoUrl` (надёжное удаление) → Task 2 колонки `LogoUrl`+`LogoMediaId`, Task 3 запись обоих, Task 6 MediaUpload отдаёт оба. ✔
- Раскладка: одна колонка карточек + живой предпросмотр справа → Task 7 (`club-profile-layout` + `ClubPlayerPreview`). ✔
- Тач ≥44px → Task 7 CSS (`min-height: 44px`). ✔
- Time-handling граница (tz храним, не перепроводим в lease/биллинг) → нигде не трогаем lease/billing; только конфиг-колонка. ✔

**Placeholder-скан:** код приведён полностью во всех шагах; единственные явные «проверить по факту» — путь импорта `MediaPurposeNames` (Task 6, ⚠ с конкретным fallback `'branch-logo'`) и наличие CSS-переменных (Task 7, с fallback в `var()`). Это осознанные точки сверки с реальным кодом, не заглушки.

**Type consistency:** `ClubProfileForm` (Task 6) ↔ `emptyForm`/`onField`/load-mapping (Task 7) — поля совпадают. `BranchWorkingHoursDay` (Task 5) ↔ `BranchWorkingHoursDayDto` (Task 1, camelCase на границе API). `UpdateBranchProfileRequest` TS (Task 5) ↔ C# record (Task 3) — поля совпадают по именам (camelCase JSON). `normalizeWorkingHours` используется в Task 7 из Task 5. ✔

**Открытый ops-долг (не код, вне плана):** миграция `AddBranchClubProfile` (Task 2) должна быть применена к staging БД (гейт Coolify) вместе с `AddUploadedMedia` из под-проекта 1; media-бакет MinIO должен быть провижн для реальной работы логотипа.
