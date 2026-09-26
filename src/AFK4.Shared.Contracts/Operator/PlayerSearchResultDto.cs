namespace AFK4.Shared.Contracts.Operator;

public sealed record PlayerSearchResultDto(
    Guid PlayerAccountId,
    string DisplayName,
    string? PhoneNumber,
    long WalletBalanceMinorUnits,
    long DebtBalanceMinorUnits,
    int ActivePackageCount,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? LastActivityAtUtc,
    string? ActivePackageName,
    long ActivePackageRemainingMinutes,
    // См. <see cref="Billing.PlayerAccountDto"/>: те же два ответа на «кто это и откуда он взялся»,
    // потому что в списке клиентов они нужны раньше, чем в карточке.
    Guid? PlatformPersonId = null,
    bool CreatedFromApp = false,
    // День рождения, если гость ввёл его в приложении (по желанию): стойка поздравит, а игры с
    // возрастом проверит глазами.
    DateOnly? BirthDate = null);
