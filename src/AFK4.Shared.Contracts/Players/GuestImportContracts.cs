using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Players;

/// <summary>
/// Перенос гостей из прежней программы клуба (план `2026-09-25-guest-import.md`): номер, имя,
/// баланс и бонусы становятся карточкой гостя и начальными остатками в журнале. Выгрузку делает
/// владелец клуба; сначала — пробный прогон без записи, потом перенос.
/// </summary>
public sealed record GuestImportRowDto(string? Phone, string? Name, long BalanceMinorUnits, long BonusMinorUnits);

public sealed record GuestImportRequest(
    Guid OrganizationId,
    string CurrencyCode,
    // Откуда перенос — «SmartShell», «Langame»: в журнал и в описание остатков.
    string Source,
    IReadOnlyList<GuestImportRowDto> Rows,
    // true — только проверить и посчитать, ничего не записывать.
    bool DryRun,
    string IdempotencyKey);

public sealed record GuestImportResultDto(
    bool Committed,
    int Total,
    // Новых карточек гостей.
    int Created,
    // Гость с этим номером уже есть в клубе — остатки легли на его карточку.
    int Matched,
    // Строки, которые не переносятся (причина — в Issues).
    int Skipped,
    MoneyDto BalanceTotal,
    MoneyDto BonusTotal,
    IReadOnlyList<GuestImportIssueDto> Issues);

public sealed record GuestImportIssueDto(
    // Номер строки в файле, с единицы, без заголовка.
    int Row,
    // Одно из GuestImportIssueNames
    string Code);

public static class GuestImportIssueNames
{
    public const string InvalidPhone = "invalid_phone";

    public const string MissingName = "missing_name";

    public const string NegativeAmount = "negative_amount";

    /// <summary>Тот же номер уже встречался выше в этом файле.</summary>
    public const string DuplicateInFile = "duplicate_in_file";

    /// <summary>Гостю уже переносили остатки — второй перенос удвоил бы деньги.</summary>
    public const string AlreadyImported = "already_imported";
}

public static class GuestImportLimits
{
    public const int MaxRows = 5000;

    public const int NameMax = 120;

    public const int SourceMax = 60;

    // Больше миллиона сомони на карточке гостя — почти наверняка ошибка выгрузки.
    public const long MaxAmountMinorUnits = 100_000_000;
}
