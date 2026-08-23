namespace AFK4.Shared.Contracts.Platform.People;

/// <summary>
/// Человек сети глазами платформы: ровно столько, сколько нужно, чтобы решить вопрос о запрете.
/// Ни клубов, ни денег, ни визитов здесь нет — это клубные сведения, и панель платформы не место,
/// где их собирают в одну карточку.
/// </summary>
public sealed record NetworkPersonDto(
    Guid PlatformPersonId,
    string PhoneNumber,
    string DisplayName,
    DateTimeOffset RegisteredAtUtc,
    DateTimeOffset? NetworkBanAtUtc,
    string? NetworkBanReason);

/// <summary>Спрос по точному номеру. Поиска по части номера нет намеренно.</summary>
public sealed record NetworkPersonLookupRequest(string PhoneNumber);

/// <summary>Причина обязательна: запрет без неё некому объяснить и не на каком основании снять.</summary>
public sealed record SetNetworkBanRequest(string Reason);
