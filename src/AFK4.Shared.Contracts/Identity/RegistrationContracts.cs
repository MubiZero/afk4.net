using AFK4.Shared.Contracts.Players;

namespace AFK4.Shared.Contracts.Identity;

/// <summary>Просьба прислать код на номер. Клуб здесь не называется: человек заводит себя сам.</summary>
public sealed record RegistrationStartRequest(string PhoneNumber);

/// <summary>
/// Ответ на просьбу прислать код. Он одинаков для знакомого и незнакомого номера — ни одного поля,
/// по которому можно отличить одно от другого, здесь нет и быть не должно.
/// </summary>
public sealed record RegistrationStartedResponse(int ExpiresInSeconds, int ResendAfterSeconds);

public sealed record RegistrationConfirmRequest(string PhoneNumber, string Code);

/// <summary>
/// Сессия человека. Первые восемь полей — дословно те же, что в <see cref="PlayerSignInResponse"/>,
/// поэтому старый клиент читает этот ответ, не заметив разницы. Отличие одно и оно про модель:
/// клуба может не быть вовсе — так выглядит человек, зарегистрировавшийся дома и ещё никуда не
/// зашедший.
/// </summary>
public sealed record PlatformPersonSessionResponse(
    Guid? PlayerAccountId,
    Guid? OrganizationId,
    string DisplayName,
    bool PhoneVerified,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc,
    Guid PlatformPersonId,
    string? PreferredLocale,
    /// <summary>Спрошены ли имя и язык. Показывать ли экран «как вас зовут», решает сервер.</summary>
    bool ProfileCompleted);

/// <summary>
/// Имя и язык человека — ровно те два поля, которые спрашиваются при регистрации. PIN сюда не
/// входит: его задают позже и в ту секунду, когда он впервые нужен.
/// </summary>
public sealed record UpdateMyProfileRequest(string DisplayName, string? PreferredLocale);
