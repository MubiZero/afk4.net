namespace AFK4.Shared.Contracts.Billing;

public sealed record PlayerAccountDto(
    Guid PlayerAccountId,
    Guid OrganizationId,
    Guid HomeBranchId,
    string DisplayName,
    string? PhoneNumber,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    // Личность за карточкой — то, чем оператор спрашивает сеть про знакомого ему человека, не
    // диктуя его телефон в запись аудита. Null — нормальный случай: карточку завели на стойке,
    // и никакой личности за ней пока нет.
    Guid? PlatformPersonId = null,
    // Карточка завелась сама, первым действием игрока из приложения. Список клиентов растёт без
    // участия стойки, и это единственное, чем ей объяснить незнакомую строку.
    bool CreatedFromApp = false);
