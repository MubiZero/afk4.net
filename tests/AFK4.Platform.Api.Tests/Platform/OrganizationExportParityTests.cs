using System.Text.RegularExpressions;

namespace AFK4.Platform.Api.Tests.Platform;

/// <summary>
/// Что клуб теряет при уходе — и что он успевает забрать.
///
/// Стирание необратимо, а выгрузка перед ним — единственный шанс сохранить своё. Эти два списка
/// однажды разошлись: выгружались пять таблиц, а стиралось около сорока, и клуб безвозвратно
/// терял финансовую историю игроков, смены, кассовые операции, чеки, платежи, тарифы и
/// собственный персонал — даже не имея возможности их увидеть.
///
/// Тест читает сам код стирания, а не список, переписанный руками: список руками отстаёт молча.
/// Каждая стираемая таблица обязана быть либо в выгрузке, либо в перечне исключений с причиной —
/// новая таблица в стирании заставит автора решить, что с ней, а не забыть о ней.
/// </summary>
public sealed class OrganizationExportParityTests
{
    /// <summary>
    /// Таблицы, которые стираются, но не выгружаются, и почему. Причина — не формальность: список
    /// читают, когда клуб спрашивает «а где мои данные».
    /// </summary>
    private static readonly Dictionary<string, string> NotExported = new()
    {
        // Секреты. Выгружать их в файл опаснее, чем потерять: на новой платформе они бесполезны,
        // а утечка архива превращает их в ключи от клуба.
        ["StaffAccessTokens"] = "токен доступа — секрет, на новой платформе бесполезен",
        ["StaffRefreshTokens"] = "токен обновления — секрет",
        ["StaffPhoneOtps"] = "одноразовые коды живут минуты",
        ["PasswordResetTokens"] = "одноразовые ссылки сброса пароля",
        ["PlayerAccessTokens"] = "токен доступа игрока — секрет",
        ["PlayerRefreshTokens"] = "токен обновления игрока — секрет",
        ["PlayerCredentials"] = "хеши паролей игроков",
        ["DeviceCredentials"] = "секреты устройств",
        ["DeviceEnrollmentCodes"] = "одноразовые коды подключения машин",
        ["InstallCodes"] = "коды тихой установки ПК — хеши секретов, живут дни",

        // Технические очереди и следы исполнения: клубу они ничего не объясняют, а восстановить по
        // ним ничего нельзя.
        ["OutboxMessages"] = "очередь доставки событий",
        ["NotificationOutbox"] = "очередь уведомлений",
        ["NotificationOutboxAttachments"] = "вложения той же очереди",
        ["NotificationPreferences"] = "настройки уведомлений сотрудников",
        ["BillingCommandIdempotency"] = "служебные ключи повторов",
        ["SessionCommandIdempotency"] = "служебные ключи повторов",
        ["DeviceCommands"] = "журнал команд на машины, живёт часами",
        ["UpdateRolloutTargets"] = "цели раскатки обновлений — данные платформы, не клуба",
        ["AnnouncementReads"] = "отметки о прочтении объявлений платформы",

        // Конфигурация, которая не переезжает: на новой платформе её заводят заново, а ключи
        // платёжных провайдеров — те же секреты.
        ["EskhataMerchantConfigs"] = "ключи платёжного провайдера",
        ["DcPayLinkConfigs"] = "ключи платёжного провайдера",
        ["OrganizationFeatureOverrides"] = "включённые возможности — настройка платформы",
        ["OrganizationLoyaltySettings"] = "настройка лояльности, заводится заново",
        ["ReportSchedules"] = "расписания рассылки отчётов",
        ["OrganizationOwnerInvites"] = "приглашения владельца — одноразовые ссылки",
        ["StaffInvites"] = "приглашения сотрудников — одноразовые ссылки",
        ["StaffMoneyCaps"] = "пороги согласования — настройка, заводится заново",
        ["BranchProtectionProfiles"] = "профиль защиты ПК — настройка, заводится заново",
        ["BranchGames"] = "библиотека игр ПК — настройка, заводится заново",
        ["BranchGameLibraries"] = "версия библиотеки игр — служебный счётчик",
        ["StaffRoleAssignments"] = "роли уходят колонкой roles в staff.csv",

        // Производные и служебные данные: считаются из того, что уже выгружено.
        ["BranchDailySnapshots"] = "витрина по дням, считается из сессий и продаж",
        ["SessionEvents"] = "события сессий — детализация к sessions.csv",
        ["SessionLeases"] = "краткоживущие разрешения машинам",
        ["MoneyActionRequests"] = "заявки на согласование, исход виден в ledger.csv",
        ["ProductBarcodes"] = "штрихкоды товаров — деталь к products.csv",
        ["PosProductCategories"] = "категории товаров — деталь к products.csv",
        ["PackageDefinitions"] = "шаблоны пакетов; проданные пакеты — в player_packages.csv",
        ["ShopOrderLines"] = "состав заказов — деталь к shop_orders.csv",
        ["DeviceInstalledApps"] = "список установленного на машинах",
        ["DeviceUpdateStatuses"] = "состояние обновлений машин",
        ["DeviceSeatAssignments"] = "привязка машин к местам — видна в seats.csv и devices.csv",
        ["UploadedMedia"] = "загруженные картинки лежат в хранилище, а не в базе",
        ["NewsItems"] = "новости клуба в приложении игрока",
        ["Zones"] = "залы уходят колонкой zone в seats.csv",
        ["AuditRecords"] = "журнал действий: персональные данные сотрудников, уходят с людьми"
    };

    /// <summary>Таблица → файл выгрузки, в котором её данные уезжают клубу.</summary>
    private static readonly Dictionary<string, string> Exported = new()
    {
        ["PlayerAccounts"] = "players.csv",
        ["Sessions"] = "sessions.csv",
        ["PosSales"] = "sales.csv",
        ["PosSaleLines"] = "sale_lines.csv",
        ["PosProducts"] = "products.csv",
        ["Reservations"] = "reservations.csv",
        ["StaffUsers"] = "staff.csv",
        ["LedgerEntries"] = "ledger.csv",
        ["Shifts"] = "shifts.csv",
        ["CashMovements"] = "cash_movements.csv",
        ["Receipts"] = "receipts.csv",
        ["Payments"] = "payments.csv",
        ["PaymentIntents"] = "payments.csv",
        ["Tariffs"] = "tariffs.csv",
        ["TariffVersions"] = "tariffs.csv",
        ["PlayerPackages"] = "player_packages.csv",
        ["ShopOrders"] = "shop_orders.csv",
        ["StockMovements"] = "stock_movements.csv",
        ["Branches"] = "branches.csv",
        ["Seats"] = "seats.csv",
        ["Devices"] = "devices.csv"
    };

    [Fact]
    public void EveryPurgedTable_IsEitherExportedOrExplainedAway()
    {
        var purged = PurgedTables();

        var unaccounted = purged
            .Where(table => !Exported.ContainsKey(table) && !NotExported.ContainsKey(table))
            .Order()
            .ToArray();

        Assert.True(
            unaccounted.Length == 0,
            $"Эти таблицы стираются при уходе клуба, но про них не сказано, выгружаются они или нет: "
            + $"{string.Join(", ", unaccounted)}. Добавьте их в выгрузку или в перечень исключений с причиной.");
    }

    // Список исключений живёт, пока живут таблицы: строка про таблицу, которую уже никто не
    // стирает, — это объяснение несуществующей потери.
    [Fact]
    public void TheExceptionListDoesNotOutliveTheTablesItExplains()
    {
        var purged = PurgedTables().ToHashSet();

        var stale = NotExported.Keys
            .Concat(Exported.Keys)
            .Where(table => !purged.Contains(table))
            .Order()
            .ToArray();

        Assert.True(
            stale.Length == 0,
            $"Эти таблицы больше не стираются при уходе клуба, а в списках про них ещё написано: {string.Join(", ", stale)}.");
    }

    // Ради этого всё и делается: без этих файлов клуб уносил историю продаж без денег и без людей.
    [Theory]
    [InlineData("staff.csv")]
    [InlineData("ledger.csv")]
    [InlineData("shifts.csv")]
    [InlineData("cash_movements.csv")]
    [InlineData("receipts.csv")]
    [InlineData("payments.csv")]
    [InlineData("tariffs.csv")]
    public void TheArchiveNamesTheFilesThatCarryMoneyAndPeople(string fileName)
    {
        Assert.Contains($"\"{fileName}\"", ExportSource(), StringComparison.Ordinal);
    }

    private static IReadOnlyCollection<string> PurgedTables()
    {
        var source = File.ReadAllText(SourcePath("OrganizationPurgeService.cs"));
        return Regex.Matches(source, @"dbContext\.(?<table>[A-Za-z]+)\s*\.Where")
            .Select(match => match.Groups["table"].Value)
            .Concat(Regex.Matches(source, @"DeleteAsync\(dbContext\.(?<table>[A-Za-z]+)")
                .Select(match => match.Groups["table"].Value))
            .Distinct()
            .ToArray();
    }

    private static string ExportSource() => File.ReadAllText(SourcePath("OrganizationExportService.cs"));

    private static string SourcePath(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AFK4.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory!.FullName, "src", "AFK4.Platform.Api", "Platform", "Offboarding", fileName);
    }
}
